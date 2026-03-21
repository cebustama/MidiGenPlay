#if UNITY_EDITOR
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// <summary>
/// Package-owned Unity Editor window for authoring DrumPatternData assets.
///
/// Workflow:
///   1. Assign a target DrumPatternData asset (or leave empty to start a new pattern).
///   2. Edit time signature, measures, subdivisions, lanes and steps.
///   3. Apply To Asset (overwrite) or Save As New Asset.
///
/// Does NOT require a runtime scene.
/// Does NOT depend on RhythmPatternPanelController.
///
/// Phase 6: each lane row has a [T]/[V] mode toggle.
///   Trigger mode  — boolean step buttons (green = active, dark = inactive).
///   Velocity mode — per-step int fields; 0 = defer to lane defaultVelocity.
///                   Setting a field > 0 activates the step with an explicit velocity.
///                   Setting a field to 0 deactivates the step (velocity 0 = sentinel/off).
/// </summary>
public class DrumPatternEditorWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string MenuPath = "MidiGenPlay/Drum Pattern Editor...";
    private const string DefaultSaveFolder = "Assets/Resources/ScriptableObjects/Patterns/Drums";

    private const float LaneHeaderWidth = 172f;
    private const float ViewModeButtonW = 22f;   // [T] / [V]
    private const float VelocityLabelW = 16f;
    private const float VelocityFieldW = 34f;
    private const float StepSize = 28f;
    private const float RowHeight = 26f;
    private const float RowSpacing = 2f;

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var w = GetWindow<DrumPatternEditorWindow>("Drum Pattern Editor");
        w.minSize = new Vector2(560f, 400f);
    }

    // -------------------------------------------------------------------------
    // Serialised editor state (survives domain reload when asset is assigned)
    // -------------------------------------------------------------------------

    [SerializeField] private DrumPatternData targetAsset;
    [SerializeField] private TimeSignature editTimeSignature = TimeSignature.FourFour;
    [SerializeField] private int editMeasures = 2;
    [SerializeField] private int editSubdivisions = 2;  // steps per beat

    // -------------------------------------------------------------------------
    // Non-serialised working state
    // -------------------------------------------------------------------------

    private DrumPatternData _working;
    private DrumPatternData _lastBound;

    /// <summary>
    /// Rows whose view mode is currently Velocity.
    /// Not serialised: resets on domain reload (acceptable — mode is authoring UI state, not asset truth).
    /// </summary>
    private readonly HashSet<int> _velocityModeRows = new HashSet<int>();

    private Vector2 _mainScroll;
    private Vector2 _gridScroll;

    private GUIStyle _stepOnStyle;
    private GUIStyle _stepOffStyle;
    private bool _stylesBuilt;
    private bool _pendingRebuild;

    // X position of the first step cell, measured during Repaint from the actual lane rows.
    // Used to align column headers without guessing control widths and spacings.
    private float _firstStepX = -1f;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        if (targetAsset != null && (_working == null || _lastBound != targetAsset))
            BindAsset(targetAsset);
    }

    private void OnGUI()
    {
        EnsureStyles();

        _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

        DrawHeader();
        EditorGUILayout.Space(4f);
        DrawTimingControls();
        EditorGUILayout.Space(4f);
        DrawLanesAndGrid();
        EditorGUILayout.Space(6f);
        DrawActionButtons();

        EditorGUILayout.EndScrollView();

        if (_pendingRebuild)
        {
            _pendingRebuild = false;
            ApplySignatureToWorking();
        }
    }

    // -------------------------------------------------------------------------
    // Header
    // -------------------------------------------------------------------------

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Drum Pattern Editor", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newAsset = (DrumPatternData)EditorGUILayout.ObjectField(
            new GUIContent("Target Asset",
                "DrumPatternData to edit. Leave empty to author a new pattern."),
            targetAsset, typeof(DrumPatternData), false);
        if (EditorGUI.EndChangeCheck() && newAsset != _lastBound)
        {
            targetAsset = newAsset;
            BindAsset(targetAsset);
        }

        if (_working == null)
        {
            EditorGUILayout.HelpBox(
                "No pattern loaded. Assign a Target Asset or press 'New Pattern'.",
                MessageType.Info);
        }
        else
        {
            string src = targetAsset != null
                ? AssetDatabase.GetAssetPath(targetAsset)
                : "unsaved new pattern";
            EditorGUILayout.LabelField($"Editing: {src}", EditorStyles.miniLabel);
        }
    }

    // -------------------------------------------------------------------------
    // Timing controls
    // -------------------------------------------------------------------------

    private void DrawTimingControls()
    {
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        var newTs = (TimeSignature)EditorGUILayout.EnumPopup(
            new GUIContent("Time Signature",
                "Sets the meter. Beats per measure is derived from this."),
            editTimeSignature);

        int newMeasures = Mathf.Max(1,
            EditorGUILayout.IntField(
                new GUIContent("Measures", "Number of bars in this pattern."),
                editMeasures));

        int newSubs = Mathf.Clamp(
            EditorGUILayout.IntSlider(
                new GUIContent("Subdivisions",
                    "Steps per beat. 1 = quarter grid, 2 = eighth, 4 = sixteenth."),
                editSubdivisions, 1, 4),
            1, 4);

        if (EditorGUI.EndChangeCheck())
        {
            editTimeSignature = newTs;
            editMeasures = newMeasures;
            editSubdivisions = newSubs;
            _pendingRebuild = true;
            Repaint();
        }

        if (_working != null)
        {
            int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
            int total = editMeasures * bpm * editSubdivisions;
            EditorGUILayout.LabelField(
                $"{editMeasures} bars × {bpm} beats × {editSubdivisions} subdivisions = {total} steps",
                EditorStyles.miniLabel);
        }
    }

    // -------------------------------------------------------------------------
    // Lanes + grid
    // -------------------------------------------------------------------------

    private void DrawLanesAndGrid()
    {
        if (_working == null) return;

        EditorGUILayout.LabelField("Lanes & Steps", EditorStyles.boldLabel);

        _gridScroll = EditorGUILayout.BeginScrollView(
            _gridScroll, GUILayout.MaxHeight(420f));

        int totalSteps = _working.TotalSteps;
        int stepsPerMeasure = _working.beatsPerMeasure * _working.subdivisions;

        DrawColumnHeaders(totalSteps, stepsPerMeasure);

        for (int r = 0; r < _working.lanes.Count; r++)
            DrawLaneRow(r, totalSteps, stepsPerMeasure);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(2f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Lane", GUILayout.Width(72)))
                AddLane();

            GUI.enabled = _working.lanes.Count > 1;
            if (GUILayout.Button("− Last", GUILayout.Width(60)))
                RemoveLastLane();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear All Steps", GUILayout.Width(114)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Steps",
                    "Remove all active steps from every lane?",
                    "Clear", "Cancel"))
                {
                    _working.ClearAll();
                    Repaint();
                }
            }
        }
    }

    private void DrawColumnHeaders(int totalSteps, int stepsPerMeasure)
    {
        // Reserve a row of the correct height so the layout flow has the right spacing.
        Rect headerRow = GUILayoutUtility.GetRect(0f, RowHeight,
            GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight));

        // _firstStepX is captured from the reserved GUILayoutUtility.GetRect in DrawTriggerSteps/
        // DrawVelocitySteps — written on every event pass, so it is valid by the time we draw here.
        // Skip only until the first lane row has been processed (first frame after open or rebind).
        if (_firstStepX < 0f || Event.current.type != EventType.Repaint)
            return;

        float cellX = _firstStepX;
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                cellX += 3f + 1f; // measure gap (Space 3f) + inter-cell Space(1f)
            else if (s > 0)
                cellX += 1f; // inter-cell gap between steps

            bool isBarStart = s % stepsPerMeasure == 0;
            int posInMeasure = s % stepsPerMeasure;
            bool isBeatStart = !isBarStart && posInMeasure % _working.subdivisions == 0;

            if (isBarStart || isBeatStart)
            {
                string label = isBarStart ? $"|{s / stepsPerMeasure + 1}" : "·";
                var style = isBarStart ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel;
                GUI.Label(new Rect(cellX, headerRow.y, StepSize - 1f, headerRow.height), label, style);
            }

            cellX += StepSize - 1f;
        }
    }

    private void DrawLaneRow(int rowIndex, int totalSteps, int stepsPerMeasure)
    {
        var lane = _working.lanes[rowIndex];

        // Guard against signature changes mid-draw
        while (lane.steps.Count < totalSteps)
            lane.steps.Add(DrumPatternData.StepState.Off);

        bool isVelocityMode = _velocityModeRows.Contains(rowIndex);

        using (new EditorGUILayout.HorizontalScope())
        {
            // ---- View mode toggle [T] / [V] ----
            string modeLabel = isVelocityMode ? "V" : "T";
            var modeTooltip = isVelocityMode
                ? "Velocity mode — click to switch to Trigger mode"
                : "Trigger mode — click to switch to Velocity mode";

            if (GUILayout.Button(
                new GUIContent(modeLabel, modeTooltip),
                GUILayout.Width(ViewModeButtonW),
                GUILayout.Height(RowHeight)))
            {
                if (isVelocityMode)
                    _velocityModeRows.Remove(rowIndex);
                else
                    _velocityModeRows.Add(rowIndex);
                Repaint();
                return; // avoid mid-frame state inconsistency
            }

            // ---- Instrument selector ----
            var allNames = Enum.GetNames(typeof(GeneralMidiPercussion));
            var allValues = (GeneralMidiPercussion[])Enum.GetValues(typeof(GeneralMidiPercussion));
            int curIdx = Array.IndexOf(allValues, lane.instrument);
            if (curIdx < 0) curIdx = 0;

            int newIdx = EditorGUILayout.Popup(
                curIdx, allNames, GUILayout.Width(LaneHeaderWidth));
            if (newIdx != curIdx)
                lane.instrument = allValues[newIdx];

            // ---- Default velocity (compact) ----
            GUILayout.Label("v", GUILayout.Width(VelocityLabelW));
            int newVel = EditorGUILayout.IntField(
                lane.defaultVelocity, GUILayout.Width(VelocityFieldW));
            lane.defaultVelocity = Mathf.Clamp(newVel, 1, 127);

            GUILayout.Space(4f);

            // ---- Steps ----
            if (isVelocityMode)
                DrawVelocitySteps(lane, rowIndex, totalSteps, stepsPerMeasure);
            else
                DrawTriggerSteps(lane, totalSteps, stepsPerMeasure);

            // ---- Per-row remove ----
            if (GUILayout.Button("✕", GUILayout.Width(22f), GUILayout.Height(RowHeight)))
                RemoveLane(rowIndex);
        }

        GUILayout.Space(RowSpacing);
    }

    /// <summary>
    /// Trigger view: green toggle = active, dark = inactive.
    /// Toggling preserves existing per-step velocity.
    /// </summary>
    private void DrawTriggerSteps(
        DrumPatternData.Lane lane, int totalSteps, int stepsPerMeasure)
    {
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                GUILayout.Space(3f);
            else if (s > 0)
                GUILayout.Space(1f); // 1px gap between cells (matches pre-fix visual)

            // Reserve the rect before drawing so we have a guaranteed pixel position.
            // GUILayout.Toggle's GetLastRect() is unreliable; GetRect() before the draw is exact.
            Rect cellRect = GUILayoutUtility.GetRect(
                StepSize - 1f, RowHeight,
                GUILayout.Width(StepSize - 1f),
                GUILayout.Height(RowHeight));

            // Capture the X of the very first step cell so DrawColumnHeaders
            // can anchor its markers to the exact same pixel position.
            // Must be Repaint-only: during Layout pass GetRect returns the group origin, not the
            // accumulated intra-group position, so the value would be wrong.
            if (s == 0 && Event.current.type == EventType.Repaint)
                _firstStepX = cellRect.x;

            var step = lane.steps[s];
            bool cur = step.active;
            bool toggled = EditorGUI.Toggle(
                cellRect, cur,
                cur ? _stepOnStyle : _stepOffStyle);

            if (toggled != cur)
            {
                lane.steps[s] = toggled
                    ? DrumPatternData.StepState.On(step.velocity)
                    : DrumPatternData.StepState.Off;
                Repaint();
            }
        }
    }

    /// <summary>
    /// Velocity view: int field per step showing per-step velocity (0 = use lane default / inactive).
    /// Setting a field > 0 activates the step with that explicit velocity.
    /// Setting a field to 0 deactivates the step.
    /// A [clr] button at row end resets all per-step velocities to 0 (defer to default),
    /// while keeping activation state intact.
    /// </summary>
    private void DrawVelocitySteps(
        DrumPatternData.Lane lane, int rowIndex, int totalSteps, int stepsPerMeasure)
    {
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                GUILayout.Space(3f);
            else if (s > 0)
                GUILayout.Space(1f); // 1px gap between cells (matches trigger mode visual)

            var step = lane.steps[s];

            // Inactive steps show lane.defaultVelocity as a hint of what will be activated.
            // Writing 0 explicitly deactivates. Writing any value > 0 activates.
            int displayValue = step.active
                ? (step.velocity > 0 ? step.velocity : lane.defaultVelocity)
                : lane.defaultVelocity;

            bool wasActive = step.active;

            // Reserve the rect before drawing so we have a guaranteed pixel position.
            // EditorGUILayout.IntField's GetLastRect() is unreliable; GetRect() before draw is exact.
            Rect cellRect = GUILayoutUtility.GetRect(
                StepSize - 1f, RowHeight,
                GUILayout.Width(StepSize - 1f),
                GUILayout.Height(RowHeight));

            // Capture X anchor from first cell (covers the case where row 0 is in velocity mode).
            // Must be Repaint-only: Layout pass GetRect returns group origin, not intra-group position.
            if (s == 0 && Event.current.type == EventType.Repaint)
                _firstStepX = cellRect.x;

            // Tint active steps amber, inactive steps muted grey.
            // GUI.backgroundColor push/pop avoids passing a custom GUIStyle to IntField,
            // which causes IMGUI layout desync between Layout and Repaint passes.
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = step.active
                ? new Color(0.85f, 0.60f, 0.15f, 1f)
                : new Color(0.45f, 0.45f, 0.45f, 1f);

            int newValue = EditorGUI.IntField(cellRect, displayValue);

            GUI.backgroundColor = prevBg;

            newValue = Mathf.Clamp(newValue, 0, 127);

            if (newValue != displayValue)
            {
                if (newValue == 0)
                {
                    // 0 = deactivate
                    lane.steps[s] = DrumPatternData.StepState.Off;
                }
                else
                {
                    // > 0 = activate with explicit velocity
                    // Store as explicit override only if it differs from lane default,
                    // otherwise store 0 sentinel (defer) to keep assets clean.
                    int storeVel = (newValue == lane.defaultVelocity) ? 0 : newValue;
                    lane.steps[s] = DrumPatternData.StepState.On(storeVel);
                }
                Repaint();
            }
        }

        // [clr] button: reset all per-step velocities to 0 (defer to default),
        // leaving active/inactive state unchanged.
        if (GUILayout.Button(
            new GUIContent("clr", "Reset all per-step velocities to 0 (defer to lane default). Active steps remain active."),
            GUILayout.Width(28f),
            GUILayout.Height(RowHeight)))
        {
            for (int s = 0; s < lane.steps.Count; s++)
            {
                var st = lane.steps[s];
                if (st.velocity != 0)
                    lane.steps[s] = new DrumPatternData.StepState { active = st.active, velocity = 0 };
            }
            Repaint();
        }
    }

    // -------------------------------------------------------------------------
    // Action buttons
    // -------------------------------------------------------------------------

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Pattern"))
                CreateNewPattern();

            GUI.enabled = targetAsset != null && _working != null;
            if (GUILayout.Button("Apply To Asset"))
                ApplyToAsset();
            GUI.enabled = true;

            GUI.enabled = _working != null;
            if (GUILayout.Button("Save As New Asset…"))
                SaveAsNewAsset();
            GUI.enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Bind / new
    // -------------------------------------------------------------------------

    private void BindAsset(DrumPatternData asset)
    {
        _lastBound = asset;
        _velocityModeRows.Clear(); // reset row view modes on rebind
        _firstStepX = -1f;

        if (asset == null)
        {
            _working = null;
            Repaint();
            return;
        }

        _working = asset.DeepCloneRuntime();
        _working.InitializeIfEmpty();

        // Sync editor controls from asset
        editTimeSignature = _working.TimeSignature;
        editMeasures = Mathf.Max(1, _working.Measures);
        editSubdivisions = Mathf.Max(1, _working.subdivisions);

        Repaint();
    }

    private void CreateNewPattern()
    {
        targetAsset = null;
        _lastBound = null;
        _velocityModeRows.Clear();
        _firstStepX = -1f;

        _working = ScriptableObject.CreateInstance<DrumPatternData>();
        _working.name = "New Drum Pattern (unsaved)";

        int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        _working.SetSignature(bpm, editMeasures, editSubdivisions);
        _working.TimeSignature = editTimeSignature;
        _working.InitializeIfEmpty();

        Repaint();
    }

    // -------------------------------------------------------------------------
    // Signature apply (deferred one frame to avoid mid-draw resize)
    // -------------------------------------------------------------------------

    private void ApplySignatureToWorking()
    {
        if (_working == null)
        {
            CreateNewPattern();
            return;
        }

        // --- Capture old dimensions before any mutation ---
        int oldMeasures = _working.Measures;
        int oldBeats = _working.beatsPerMeasure;
        int oldSubs = _working.subdivisions;
        int oldTotalSteps = Mathf.Max(1, oldMeasures * oldBeats * oldSubs);

        int newBeats = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        int newMeasures = editMeasures;
        int newSubs = editSubdivisions;
        int newTotalSteps = Mathf.Max(1, newMeasures * newBeats * newSubs);

        // --- Remap each lane before SetSignature resizes the lists ---
        if (_working.lanes != null)
        {
            foreach (var lane in _working.lanes)
            {
                var remapped = new List<DrumPatternData.StepState>(newTotalSteps);
                for (int i = 0; i < newTotalSteps; i++)
                    remapped.Add(DrumPatternData.StepState.Off);

                for (int s = 0; s < lane.steps.Count && s < oldTotalSteps; s++)
                {
                    var step = lane.steps[s];
                    if (!step.active) continue;

                    float frac = s / (float)oldTotalSteps;
                    int newIndex = Mathf.RoundToInt(frac * newTotalSteps);
                    newIndex = Mathf.Clamp(newIndex, 0, newTotalSteps - 1);

                    // Collision: keep the step with higher resolved velocity.
                    var existing = remapped[newIndex];
                    if (!existing.active ||
                        step.ResolveVelocity(lane.defaultVelocity) >
                        existing.ResolveVelocity(lane.defaultVelocity))
                    {
                        remapped[newIndex] = step;
                    }
                }

                lane.steps = remapped;
            }
        }

        // --- Now apply signature; EnsureSizes will be a no-op (list already correct size) ---
        _working.SetSignature(newBeats, newMeasures, newSubs);
        _working.TimeSignature = editTimeSignature;
        _firstStepX = -1f;
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Lane management
    // -------------------------------------------------------------------------

    private void AddLane()
    {
        if (_working == null) return;
        _working.lanes ??= new List<DrumPatternData.Lane>();

        _working.lanes.Add(new DrumPatternData.Lane
        {
            instrument = GuessNextInstrument(),
            defaultVelocity = 100,
            steps = new List<DrumPatternData.StepState>(
                new DrumPatternData.StepState[_working.TotalSteps])
        });
        Repaint();
    }

    private void RemoveLastLane()
    {
        if (_working?.lanes == null || _working.lanes.Count == 0) return;
        int last = _working.lanes.Count - 1;
        _velocityModeRows.Remove(last);
        _working.lanes.RemoveAt(last);
        Repaint();
    }

    private void RemoveLane(int index)
    {
        if (_working?.lanes == null) return;
        if (index < 0 || index >= _working.lanes.Count) return;
        _velocityModeRows.Remove(index);
        // Remap velocity-mode rows above the removed index
        var above = new List<int>();
        foreach (var r in _velocityModeRows)
            if (r > index) above.Add(r);
        foreach (var r in above)
        {
            _velocityModeRows.Remove(r);
            _velocityModeRows.Add(r - 1);
        }
        _working.lanes.RemoveAt(index);
        Repaint();
    }

    private static readonly GeneralMidiPercussion[] _commonPerc =
    {
        GeneralMidiPercussion.BassDrum1,
        GeneralMidiPercussion.AcousticSnare,
        GeneralMidiPercussion.ClosedHiHat,
        GeneralMidiPercussion.OpenHiHat,
        GeneralMidiPercussion.HandClap,
        GeneralMidiPercussion.LowTom,
        GeneralMidiPercussion.HighTom,
        GeneralMidiPercussion.CrashCymbal1,
        GeneralMidiPercussion.RideCymbal1,
    };

    private GeneralMidiPercussion GuessNextInstrument()
    {
        if (_working?.lanes == null || _working.lanes.Count == 0)
            return GeneralMidiPercussion.BassDrum1;

        var used = new HashSet<GeneralMidiPercussion>();
        foreach (var l in _working.lanes) used.Add(l.instrument);

        foreach (var cand in _commonPerc)
            if (!used.Contains(cand)) return cand;

        return _commonPerc[_working.lanes.Count % _commonPerc.Length];
    }

    // -------------------------------------------------------------------------
    // Persistence
    // -------------------------------------------------------------------------

    private void ApplyToAsset()
    {
        if (targetAsset == null || _working == null) return;

        Undo.RecordObject(targetAsset, "Drum Pattern Editor: Apply");
        CopyWorkingInto(targetAsset);
        EditorUtility.SetDirty(targetAsset);
        AssetDatabase.SaveAssets();

        BindAsset(targetAsset);
        Debug.Log($"[DrumPatternEditor] Applied to {AssetDatabase.GetAssetPath(targetAsset)}");
    }

    private void SaveAsNewAsset()
    {
        if (_working == null) return;

        Directory.CreateDirectory(DefaultSaveFolder);
        AssetDatabase.Refresh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Drum Pattern As…",
            BuildDefaultName(),
            "asset",
            "Choose where to save the new drum pattern asset.",
            DefaultSaveFolder);

        if (string.IsNullOrEmpty(path)) return;

        var newAsset = ScriptableObject.CreateInstance<DrumPatternData>();
        AssetDatabase.CreateAsset(newAsset, path);

        Undo.RecordObject(newAsset, "Drum Pattern Editor: Save As New");
        CopyWorkingInto(newAsset, Path.GetFileNameWithoutExtension(path));
        EditorUtility.SetDirty(newAsset);
        AssetDatabase.SaveAssets();

        targetAsset = newAsset;
        BindAsset(targetAsset);
        Debug.Log($"[DrumPatternEditor] Saved new asset at {path}");
    }

    private void CopyWorkingInto(DrumPatternData dst, string nameOverride = null)
    {
        dst.DisplayName = string.IsNullOrEmpty(nameOverride)
                                ? _working.DisplayName
                                : nameOverride;
        dst.TimeSignature = _working.TimeSignature;
        dst.Measures = _working.Measures;
        dst.beatsPerMeasure = _working.beatsPerMeasure;
        dst.subdivisions = _working.subdivisions;

        dst.lanes ??= new List<DrumPatternData.Lane>();
        dst.lanes.Clear();
        foreach (var l in _working.lanes)
        {
            dst.lanes.Add(new DrumPatternData.Lane
            {
                instrument = l.instrument,
                defaultVelocity = l.defaultVelocity,
                steps = new List<DrumPatternData.StepState>(l.steps)
            });
        }
        dst.EnsureSizes();
    }

    private string BuildDefaultName()
    {
        if (_working == null) return "NewDrumPattern";

        string ts = $"{_working.beatsPerMeasure}-{_working.subdivisions}";
        string ins = (_working.lanes == null || _working.lanes.Count == 0)
            ? "empty"
            : string.Join("", _working.lanes.ConvertAll(l => InstrumentAbbrev(l.instrument)));
        return $"Drum_{ts}_{_working.Measures}m_{ins}";
    }

    private static string InstrumentAbbrev(GeneralMidiPercussion gmp)
    {
        switch (gmp)
        {
            case GeneralMidiPercussion.BassDrum1: return "BD";
            case GeneralMidiPercussion.AcousticSnare:
            case GeneralMidiPercussion.ElectricSnare: return "SN";
            case GeneralMidiPercussion.ClosedHiHat: return "CH";
            case GeneralMidiPercussion.OpenHiHat: return "OH";
            case GeneralMidiPercussion.HandClap: return "CL";
            case GeneralMidiPercussion.LowTom: return "LT";
            case GeneralMidiPercussion.HighTom: return "HT";
            case GeneralMidiPercussion.CrashCymbal1: return "CR";
            case GeneralMidiPercussion.RideCymbal1: return "RI";
            default:
                var s = gmp.ToString();
                return s.Length >= 2 ? s.Substring(0, 2).ToUpper() : s.ToUpper();
        }
    }

    // -------------------------------------------------------------------------
    // Styles
    // -------------------------------------------------------------------------

    private void EnsureStyles()
    {
        if (_stylesBuilt) return;

        _stepOnStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(2, 2, new Color(0.25f, 0.85f, 0.35f, 1f)) },
            hover = { background = MakeTex(2, 2, new Color(0.35f, 1.00f, 0.45f, 1f)) },
            active = { background = MakeTex(2, 2, new Color(0.15f, 0.65f, 0.25f, 1f)) },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _stepOffStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.18f, 1f)) },
            hover = { background = MakeTex(2, 2, new Color(0.28f, 0.28f, 0.28f, 1f)) },
            active = { background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.12f, 1f)) },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _stylesBuilt = true;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var t = new Texture2D(w, h);
        t.SetPixels(pix);
        t.Apply();
        return t;
    }
}
#endif
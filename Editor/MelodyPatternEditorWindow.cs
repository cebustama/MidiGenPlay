#if UNITY_EDITOR
using MidiGenPlay;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using MidiGenPlay.Composition;
using MidiGenPlay.Authoring;
using MidiGenPlay.Services;

/// <summary>
/// Package-owned Unity Editor window for authoring <see cref="MelodyPatternData"/> assets
/// (Roadmap_Melody_Authoring_MVP, Phase 2 — note-grid / "ladder" editor).
///
/// Structural references: <c>DrumPatternEditorWindow</c> (working-copy / Apply-Save flow,
/// deferred-rebuild on signature change) and <c>ChordProgressionEditorWindow</c> (rect-based
/// timeline grid with mouse hit-testing + a selection inspector). The melody grid extends the
/// chord rect-grid from one lane (time only) to two dimensions: Y = 7 diatonic scale-degree
/// rows × octave bands, X = time steps.
///
/// Workflow (the established package authoring loop — see SSoT_Authoring_Tools §2):
///   1. Assign a target MelodyPatternData asset (or press 'New Pattern').
///   2. Edit time signature / measures / subdivisions and the visible octave window.
///   3. Author notes on the ladder grid, normalize (snap to subdivisions), then
///      Apply To Asset (overwrite) or Save As New Asset.
///
/// Grid interaction:
///   - Left-click an empty cell  → place a note at that degree+octave+step (default 1 beat).
///   - Left-click an existing note → select it (its fields appear in the inspector below).
///   - Right-click a note  → remove it. (The inspector also has a Delete button.)
///   - Selected-note edits apply live to the WORKING COPY only; the asset is never mutated
///     until Apply / Save As.
///
/// Determinism boundary: this tool stores scale degrees + octave offsets + beat-relative
/// timing. Absolute pitch is NOT stored or resolved here — that is the Phase 4 runtime
/// ComposeFromPattern concern. This window makes NO runtime changes.
///
/// Scope note: the generation-parameters top section + a simplified, deterministic,
/// editor-only generator are Phase 3 (no runtime/ComposeFromPattern change — Phase 4).
/// No Text/DSL mode (rhythm/chord-only feature).
/// </summary>
public class MelodyPatternEditorWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string MenuPath = "MidiGenPlay/Melody Pattern Editor...";

    // PATTERN-PERSIST-1 / D4 + D5: the former DefaultSaveFolder constant (".../Patterns/Melody",
    // singular) was removed. Melody pattern writes now route through the shared store, whose
    // canonical root is ".../Patterns/Melodies" (plural) — matching PatternRepositoryResources'
    // read root and the shipped assets, closing the prior editor-writes-vs-repo-reads split.
    // Read the root via _melodyStore.AssetsSaveRootPath.
    // DefaultParamsFolder is a DIFFERENT asset kind (generation params) and is out of scope.
    private const string DefaultParamsFolder = "Assets/Resources/ScriptableObjects/GenerationParams/Melody";

    private const float RowHeight = 20f;
    private const float RowLabelWidth = 92f;   // degree-roman + octave-tag column

    private const int OctaveFloor = -4;        // clamp bounds for the visible octave window
    private const int OctaveCeil = 4;

    private static readonly string[] DegreeToRoman =
    {
        "I", "II", "III", "IV", "V", "VI", "VII"
    };

    // Stable per-degree hue palette (key-independent — we do not know the key at authoring time).
    private static readonly Color[] DegreeColors = BuildDegreeColors();

    private static Color[] BuildDegreeColors()
    {
        var c = new Color[7];
        for (int i = 0; i < 7; i++)
            c[i] = Color.HSVToRGB(i / 7f, 0.55f, 0.85f);
        return c;
    }

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var w = GetWindow<MelodyPatternEditorWindow>("Melody Pattern Editor");
        w.minSize = new Vector2(620f, 460f);
    }

    // -------------------------------------------------------------------------
    // Serialised editor state (survives domain reload while an asset is assigned)
    // -------------------------------------------------------------------------

    [SerializeField] private MelodyPatternData targetAsset;

    // PATTERN-PERSIST-1 — shared package persistence store (read + write). Save root
    // resolves to Assets/Resources/ScriptableObjects/Patterns/Melodies (D5=A realignment).
    private readonly TrackPatternConfigStoreResources<MelodyPatternData> _melodyStore = new("Melodies");

    // PATTERN-PERSIST-1 / D3 — foldout state for the "browse saved patterns" list.
    private bool _showBrowse;

    [SerializeField] private MelodyGenerationParamsSO genParams;
    [SerializeField] private bool _genFoldout = true;
    private UnityEditor.Editor _genParamsEditor;

    [SerializeField] private TimeSignature editTimeSignature = TimeSignature.FourFour;
    [SerializeField] private int editMeasures = 2;
    [SerializeField] private int editSubdivisions = 4;   // steps per beat (16th grid default)

    // Visible octave window (decision 3A). The per-note inspector octave is clamped to this
    // window so every note in the working copy is always renderable; on load the window is
    // auto-fitted to cover all notes (no data loss).
    [SerializeField] private int editMinOctave = -1;
    [SerializeField] private int editMaxOctave = 1;

    // -------------------------------------------------------------------------
    // Non-serialised working state
    // -------------------------------------------------------------------------

    private MelodyPatternData _working;
    private MelodyPatternData _lastBound;

    private bool _hasSelection;
    private int _selectedNoteIndex = -1;   // index into _working.notes

    private Vector2 _mainScroll;
    private bool _pendingRebuild;

    private GUIStyle _rowLabelStyle;
    private GUIStyle _noteLabelStyle;
    private bool _stylesBuilt;

    // -------------------------------------------------------------------------
    // Derived grid math (working copy is the source of truth for meter)
    // -------------------------------------------------------------------------

    private int Subs => _working != null ? Mathf.Max(1, _working.subdivisions) : 1;
    private int StepsPerMeasure =>
        _working != null ? Mathf.Max(1, _working.beatsPerMeasure * Subs) : 1;
    private int TotalSteps =>
        _working != null ? Mathf.Max(1, _working.Measures * StepsPerMeasure) : 1;

    private float BeatFromStep(int step) => step / (float)Subs;
    private int StepFromBeat(float beat) => Mathf.RoundToInt(beat * Subs);

    private int VisibleBandCount => Mathf.Max(1, editMaxOctave - editMinOctave + 1);
    private int VisibleRowCount => VisibleBandCount * 7;

    /// <summary>(degree, octave) → row index (0 = top). Returns -1 if outside the visible window.</summary>
    private int RowIndexFor(ScaleDegree degree, int octave)
    {
        if (octave < editMinOctave || octave > editMaxOctave) return -1;
        int bandFromTop = editMaxOctave - octave;   // 0 at the top band
        int degreeFromTop = 6 - (int)degree;        // VII at the top of each band, I at the bottom
        return bandFromTop * 7 + degreeFromTop;
    }

    private void RowToDegreeOctave(int rowIndex, out ScaleDegree degree, out int octave)
    {
        int band = rowIndex / 7;
        int degreeFromTop = rowIndex % 7;
        octave = editMaxOctave - band;
        degree = (ScaleDegree)(6 - degreeFromTop);
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        // PATTERN-PERSIST-1 / D3 — populate the browse cache once; saves keep it fresh.
        _melodyStore.Refresh();

        if (targetAsset != null && (_working == null || _lastBound != targetAsset))
            BindAsset(targetAsset);
    }

    private void OnDisable()
    {
        if (_genParamsEditor != null) { DestroyImmediate(_genParamsEditor); _genParamsEditor = null; }
    }

    private void OnGUI()
    {
        EnsureStyles();

        _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

        DrawHeader();
        EditorGUILayout.Space(4f);
        DrawGenerationParams();
        EditorGUILayout.Space(4f);
        DrawTimingControls();
        EditorGUILayout.Space(4f);
        DrawGridSection();
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
        EditorGUILayout.LabelField("Melody Pattern Editor", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newAsset = (MelodyPatternData)EditorGUILayout.ObjectField(
            new GUIContent("Target Asset",
                "MelodyPatternData to edit. Leave empty to author a new pattern."),
            targetAsset, typeof(MelodyPatternData), false);
        if (EditorGUI.EndChangeCheck() && newAsset != _lastBound)
        {
            targetAsset = newAsset;
            BindAsset(targetAsset);
        }

        // PATTERN-PERSIST-1 / D3 — browse patterns already saved under the canonical
        // Resources root. Additive: the Target Asset object field above still works.
        _showBrowse = EditorGUILayout.Foldout(_showBrowse, "Browse Saved Patterns", true);
        if (_showBrowse)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Refresh List"))
                    _melodyStore.Refresh();

                var saved = _melodyStore.GetAll();
                if (saved.Count == 0)
                {
                    EditorGUILayout.LabelField(
                        $"No saved patterns under {_melodyStore.AssetsSaveRootPath}.");
                }
                else
                {
                    foreach (var a in saved)
                    {
                        if (a == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(a, typeof(MelodyPatternData), false);
                            using (new EditorGUI.DisabledScope(a == targetAsset))
                            {
                                if (GUILayout.Button("Load", GUILayout.Width(52)))
                                {
                                    targetAsset = a;
                                    BindAsset(targetAsset);
                                }
                            }
                        }
                    }
                }
            }
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
    // Generation parameters (Phase 3) - top section: bind a MelodyGenerationParamsSO
    // and run the simplified, deterministic, editor-only generator into the working copy.
    // -------------------------------------------------------------------------

    private void DrawGenerationParams()
    {
        _genFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_genFoldout, "Generation (simplified)");
        if (_genFoldout)
        {
            EditorGUI.BeginChangeCheck();
            var newParams = (MelodyGenerationParamsSO)EditorGUILayout.ObjectField(
                new GUIContent("Params Asset",
                    "MelodyGenerationParamsSO driving the generator. Saved independently of the pattern."),
                genParams, typeof(MelodyGenerationParamsSO), false);
            if (EditorGUI.EndChangeCheck() && newParams != genParams)
            {
                genParams = newParams;
                if (_genParamsEditor != null) { DestroyImmediate(_genParamsEditor); _genParamsEditor = null; }
            }

            if (GUILayout.Button("New Params Asset…", GUILayout.Width(160f)))
                CreateNewParamsAsset();

            if (genParams == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a MelodyGenerationParamsSO to enable Generate.",
                    MessageType.Info);
            }
            else
            {
                if (_genParamsEditor == null || _genParamsEditor.target != genParams)
                    _genParamsEditor = UnityEditor.Editor.CreateEditor(genParams);

                using (new EditorGUI.IndentLevelScope())
                    _genParamsEditor.OnInspectorGUI();   // Tier-1 fields incl. seed + instrument hint

                EditorGUILayout.LabelField(
                    "Generate fills the current meter (set in Timing below). It overwrites the working " +
                    "copy only — the bound asset is untouched until Apply / Save As.",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate", GUILayout.Height(24f)))
                        GenerateIntoWorking();

                    if (GUILayout.Button(
                            new GUIContent("Randomize Seed", "Pick a new seed and regenerate."),
                            GUILayout.Width(130f)))
                    {
                        Undo.RecordObject(genParams, "Randomize Melody Seed");
                        genParams.seed = new System.Random().Next(int.MinValue, int.MaxValue);
                        EditorUtility.SetDirty(genParams);
                        GenerateIntoWorking();
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void GenerateIntoWorking()
    {
        if (genParams == null) return;
        if (_working == null) CreateNewPattern();   // uses the current edit* meter

        if (_working.notes != null && _working.notes.Count > 0 &&
            !EditorUtility.DisplayDialog(
                "Generate Melody",
                "Replace the current working-copy notes with a freshly generated pattern?\n\n" +
                "The bound asset is not modified until you Apply or Save As.",
                "Generate", "Cancel"))
            return;

        SimplifiedMelodyGenerator.Generate(_working, genParams, genParams.seed);

        FitOctaveWindowToNotes();
        ClearSelection();
        Repaint();
        Debug.Log($"[MelodyPatternEditor] Generated {_working.notes.Count} note(s) " +
                  $"(seed {genParams.seed}, {genParams.rhythmicStyle}, density {genParams.density:0.##}).");
    }

    private void CreateNewParamsAsset()
    {
        Directory.CreateDirectory(DefaultParamsFolder);
        AssetDatabase.Refresh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Melody Generation Params As…",
            "MelodyGenParams",
            "asset",
            "Choose where to save the new generation-params asset.",
            DefaultParamsFolder);
        if (string.IsNullOrEmpty(path)) return;

        var so = ScriptableObject.CreateInstance<MelodyGenerationParamsSO>();
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();

        genParams = so;
        if (_genParamsEditor != null) { DestroyImmediate(_genParamsEditor); _genParamsEditor = null; }
        Debug.Log($"[MelodyPatternEditor] Created params asset at {path}");
    }

    // -------------------------------------------------------------------------
    // Timing controls (meter + grid resolution + visible octave window)
    // -------------------------------------------------------------------------

    private void DrawTimingControls()
    {
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        var newTs = (TimeSignature)EditorGUILayout.EnumPopup(
            new GUIContent("Time Signature",
                "Sets the meter. Beats per measure is derived from this (package meter contract)."),
            editTimeSignature);

        int newMeasures = Mathf.Max(1,
            EditorGUILayout.IntField(
                new GUIContent("Measures", "Number of bars in this pattern."),
                editMeasures));

        int newSubs = Mathf.Clamp(
            EditorGUILayout.IntSlider(
                new GUIContent("Subdivisions",
                    "Steps per beat. 1 = quarter grid, 2 = eighth, 4 = sixteenth."),
                editSubdivisions, 1, 8),
            1, 8);

        if (EditorGUI.EndChangeCheck())
        {
            editTimeSignature = newTs;
            editMeasures = newMeasures;
            editSubdivisions = newSubs;
            _pendingRebuild = true;   // resize on the next frame to avoid mid-draw mutation
            Repaint();
        }

        // --- Visible octave window ---
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(new GUIContent("Octave window",
                "Lowest..highest octave band shown on the grid. The reference octave is 0. " +
                "Notes outside this window are kept but not drawn; widen the window to see them."),
                GUILayout.Width(EditorGUIUtility.labelWidth));

            int newMin = Mathf.Clamp(
                EditorGUILayout.IntField(editMinOctave, GUILayout.Width(44f)),
                OctaveFloor, OctaveCeil);
            EditorGUILayout.LabelField("to", GUILayout.Width(18f));
            int newMax = Mathf.Clamp(
                EditorGUILayout.IntField(editMaxOctave, GUILayout.Width(44f)),
                OctaveFloor, OctaveCeil);

            if (newMin > newMax) newMin = newMax;
            editMinOctave = newMin;
            editMaxOctave = newMax;

            GUILayout.Space(6f);
            if (GUILayout.Button(new GUIContent("▲", "Shift the window up one octave"),
                    GUILayout.Width(24f)))
                ShiftOctaveWindow(+1);
            if (GUILayout.Button(new GUIContent("▼", "Shift the window down one octave"),
                    GUILayout.Width(24f)))
                ShiftOctaveWindow(-1);
        }

        if (_working != null)
        {
            int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
            int total = editMeasures * bpm * editSubdivisions;
            EditorGUILayout.LabelField(
                $"{editMeasures} bars × {bpm} beats × {editSubdivisions} subdivisions = {total} steps · " +
                $"{VisibleBandCount} octave band(s) × 7 = {VisibleRowCount} rows",
                EditorStyles.miniLabel);
        }
    }

    private void ShiftOctaveWindow(int delta)
    {
        int min = editMinOctave + delta;
        int max = editMaxOctave + delta;
        if (min < OctaveFloor || max > OctaveCeil) return;
        editMinOctave = min;
        editMaxOctave = max;
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Grid section (ladder)
    // -------------------------------------------------------------------------

    private void DrawGridSection()
    {
        if (_working == null) return;

        EditorGUILayout.LabelField("Note Grid (ladder)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Left-click an empty cell to place a note · click a note to select it · " +
            "right-click a note to delete it. Y = scale degrees (I–VII) × octave bands, " +
            "X = time steps. Edits affect the working copy only until Apply / Save As.",
            MessageType.Info);

        int totalSteps = TotalSteps;
        int rows = VisibleRowCount;

        float gridHeight = rows * RowHeight;
        Rect fullRect = GUILayoutUtility.GetRect(0, 10000, gridHeight, gridHeight);

        var labelRect = new Rect(fullRect.x, fullRect.y, RowLabelWidth, fullRect.height);
        var gridRect = new Rect(
            fullRect.x + RowLabelWidth, fullRect.y,
            Mathf.Max(1f, fullRect.width - RowLabelWidth), fullRect.height);

        float stepWidth = gridRect.width / totalSteps;

        DrawGridBackground(gridRect, totalSteps, stepWidth, rows);
        DrawRowLabels(labelRect, rows);
        int hiddenNotes = DrawNotes(gridRect, totalSteps, stepWidth);
        HandleGridMouse(gridRect, totalSteps, stepWidth, rows);

        EditorGUILayout.Space(2f);

        DrawSelectionInspector(totalSteps);

        EditorGUILayout.Space(2f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(
                new GUIContent("Normalize (snap to grid)",
                    "Snap every note's start and duration to the current subdivision grid. " +
                    "Grid-placed notes are already snapped; this matters for off-grid notes."),
                GUILayout.Width(190f)))
                NormalizeWorking();

            GUILayout.FlexibleSpace();

            if (hiddenNotes > 0)
                EditorGUILayout.LabelField(
                    $"{hiddenNotes} note(s) outside the current length/window — widen Measures or the octave window to see them.",
                    EditorStyles.miniLabel);

            if (GUILayout.Button("Clear All Notes", GUILayout.Width(120f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Notes",
                    "Remove every note from the working copy?\n\n" +
                    "The asset is not modified until you Apply or Save As.",
                    "Clear", "Cancel"))
                {
                    _working.ClearAll();
                    ClearSelection();
                    Repaint();
                }
            }
        }
    }

    private void DrawGridBackground(Rect gridRect, int totalSteps, float stepWidth, int rows)
    {
        EditorGUI.DrawRect(gridRect, new Color(0.15f, 0.15f, 0.15f, 1f));

        // Alternate band shading + a faint tonic-row tint, so degrees and octaves read at a glance.
        for (int r = 0; r < rows; r++)
        {
            int band = r / 7;
            int degreeFromTop = r % 7;
            bool bandEven = (band & 1) == 0;
            bool isTonic = degreeFromTop == 6; // bottom row of each band == Tonic (I)

            var rowRect = new Rect(gridRect.xMin, gridRect.yMin + r * RowHeight,
                gridRect.width, RowHeight);

            if (bandEven)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.025f));
            if (isTonic)
                EditorGUI.DrawRect(rowRect, new Color(0.35f, 0.55f, 1f, 0.06f));
        }

        if (Event.current.type != EventType.Repaint)
            return;

        Handles.BeginGUI();

        // Horizontal row separators (brighter at band boundaries).
        for (int r = 0; r <= rows; r++)
        {
            float y = gridRect.yMin + r * RowHeight;
            bool bandLine = (r % 7) == 0;
            Handles.color = bandLine
                ? new Color(1f, 1f, 1f, 0.22f)
                : new Color(1f, 1f, 1f, 0.06f);
            Handles.DrawLine(new Vector2(gridRect.xMin, y), new Vector2(gridRect.xMax, y));
        }

        // Vertical step / beat / bar lines.
        for (int s = 0; s <= totalSteps; s++)
        {
            float x = gridRect.xMin + s * stepWidth;
            if (s % StepsPerMeasure == 0)
                Handles.color = new Color(1f, 0.9f, 0.1f, 0.9f);   // bar
            else if (s % Subs == 0)
                Handles.color = new Color(1f, 1f, 1f, 0.18f);      // beat
            else
                Handles.color = new Color(1f, 1f, 1f, 0.05f);      // subdivision
            Handles.DrawLine(new Vector2(x, gridRect.yMin), new Vector2(x, gridRect.yMax));
        }

        Handles.EndGUI();
    }

    private void DrawRowLabels(Rect labelRect, int rows)
    {
        for (int r = 0; r < rows; r++)
        {
            RowToDegreeOctave(r, out var deg, out int oct);
            string roman = DegreeToRoman[(int)deg];

            // Show the octave tag once per band, on the Tonic (root) row.
            string label = ((int)deg == (int)ScaleDegree.Tonic)
                ? $"oct {(oct > 0 ? "+" + oct : oct.ToString())} · {roman}"
                : roman;

            var rr = new Rect(labelRect.xMin, labelRect.yMin + r * RowHeight,
                labelRect.width - 4f, RowHeight);
            GUI.Label(rr, label, _rowLabelStyle);
        }
    }

    /// <summary>Returns the count of notes not drawn (outside length or octave window).</summary>
    private int DrawNotes(Rect gridRect, int totalSteps, float stepWidth)
    {
        int hidden = 0;
        var notes = _working.notes;
        if (notes == null) return 0;

        for (int i = 0; i < notes.Count; i++)
        {
            var n = notes[i];

            int row = RowIndexFor(n.degree, n.octaveOffset);
            int startStep = StepFromBeat(n.startBeat);
            int lengthSteps = Mathf.Max(1, StepFromBeat(n.durationBeats));
            int endStep = startStep + lengthSteps;

            if (row < 0 || startStep >= totalSteps || endStep <= 0)
            {
                hidden++;
                continue;
            }

            int drawStart = Mathf.Max(0, startStep);
            int drawEnd = Mathf.Min(totalSteps, endStep);
            float x = gridRect.xMin + drawStart * stepWidth;
            float w = Mathf.Max(2f, (drawEnd - drawStart) * stepWidth);
            float y = gridRect.yMin + row * RowHeight;

            Color col = DegreeColors[Mathf.Clamp((int)n.degree, 0, 6)];
            bool selected = _hasSelection && i == _selectedNoteIndex;

            var block = new Rect(x + 1f, y + 1.5f, w - 2f, RowHeight - 3f);

            if (selected)
            {
                // Selection border: a lighter rect behind a brightened fill.
                EditorGUI.DrawRect(new Rect(block.x - 1.5f, block.y - 1.5f,
                    block.width + 3f, block.height + 3f), Color.white);
                col = Color.Lerp(col, Color.white, 0.35f);
            }

            EditorGUI.DrawRect(block, col);

            if (w > 22f)
                GUI.Label(block, DegreeToRoman[(int)n.degree], _noteLabelStyle);
        }

        return hidden;
    }

    private void HandleGridMouse(Rect gridRect, int totalSteps, float stepWidth, int rows)
    {
        var evt = Event.current;
        if (evt.type != EventType.MouseDown) return;
        if (!gridRect.Contains(evt.mousePosition)) return;

        float localX = evt.mousePosition.x - gridRect.xMin;
        float localY = evt.mousePosition.y - gridRect.yMin;

        int step = Mathf.Clamp(Mathf.FloorToInt(localX / stepWidth), 0, totalSteps - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt(localY / RowHeight), 0, rows - 1);

        int covering = FindNoteCovering(row, step, totalSteps);

        if (evt.button == 1) // right-click → delete
        {
            if (covering >= 0)
            {
                _working.notes.RemoveAt(covering);
                if (_selectedNoteIndex == covering) ClearSelection();
                else if (_selectedNoteIndex > covering) _selectedNoteIndex--;
                evt.Use();
                Repaint();
            }
            return;
        }

        if (evt.button != 0) return; // left-click only beyond here

        if (covering >= 0)
        {
            _selectedNoteIndex = covering;
            _hasSelection = true;
        }
        else
        {
            RowToDegreeOctave(row, out var degree, out int octave);
            int defaultLen = Mathf.Clamp(Subs, 1, totalSteps - step); // default one beat
            var note = MelodyPatternData.MelodyNoteEvent.Create(
                degree,
                BeatFromStep(step),
                BeatFromStep(defaultLen),
                octave,
                100);

            _working.notes.Add(note);
            _selectedNoteIndex = _working.notes.Count - 1;
            _hasSelection = true;
        }

        GUI.FocusControl(null);
        evt.Use();
        Repaint();
    }

    /// <summary>Topmost (last-drawn) note covering (row, step), or -1.</summary>
    private int FindNoteCovering(int row, int step, int totalSteps)
    {
        var notes = _working.notes;
        int found = -1;
        for (int i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            if (RowIndexFor(n.degree, n.octaveOffset) != row) continue;
            int s = StepFromBeat(n.startBeat);
            int e = s + Mathf.Max(1, StepFromBeat(n.durationBeats));
            if (step >= s && step < e) found = i; // keep last match (topmost)
        }
        return found;
    }

    // -------------------------------------------------------------------------
    // Selection inspector — edits the selected note in the working copy, live
    // -------------------------------------------------------------------------

    private void DrawSelectionInspector(int totalSteps)
    {
        if (!_hasSelection || _selectedNoteIndex < 0 ||
            _selectedNoteIndex >= _working.notes.Count)
        {
            EditorGUILayout.LabelField("No note selected.", EditorStyles.miniLabel);
            return;
        }

        var n = _working.notes[_selectedNoteIndex];

        EditorGUILayout.LabelField("Selected Note", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUI.BeginChangeCheck();

            var degree = (ScaleDegree)EditorGUILayout.EnumPopup(
                new GUIContent("Degree", "Diatonic scale degree (I–VII)."), n.degree);

            int octave;
            if (editMinOctave == editMaxOctave)
            {
                // Single-band window: a slider with min == max is degenerate, so show a fixed value.
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField(
                        new GUIContent("Octave Offset",
                            "Only one octave band is visible; widen the octave window to move notes."),
                        editMinOctave);
                octave = editMinOctave;
            }
            else
            {
                octave = Mathf.Clamp(
                    EditorGUILayout.IntSlider(
                        new GUIContent("Octave Offset",
                            "Offset from the reference octave, clamped to the visible window."),
                        n.octaveOffset, editMinOctave, editMaxOctave),
                    editMinOctave, editMaxOctave);
            }

            int startStep = Mathf.Clamp(
                EditorGUILayout.IntField(
                    new GUIContent("Start Step", "Start position on the subdivision grid."),
                    StepFromBeat(n.startBeat)),
                0, totalSteps - 1);

            int lengthSteps = Mathf.Clamp(
                EditorGUILayout.IntField(
                    new GUIContent("Length (steps)", "Note length in grid steps."),
                    Mathf.Max(1, StepFromBeat(n.durationBeats))),
                1, totalSteps - startStep);

            int velocity = EditorGUILayout.IntSlider(
                new GUIContent("Velocity", "MIDI velocity 1–127."), n.velocity, 1, 127);

            EditorGUILayout.LabelField(
                $"= start {BeatFromStep(startStep):0.###} beats · " +
                $"duration {BeatFromStep(lengthSteps):0.###} beats",
                EditorStyles.miniLabel);

            if (EditorGUI.EndChangeCheck())
            {
                _working.notes[_selectedNoteIndex] = MelodyPatternData.MelodyNoteEvent.Create(
                    degree,
                    BeatFromStep(startStep),
                    BeatFromStep(lengthSteps),
                    octave,
                    velocity);
                Repaint();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Delete", GUILayout.Width(90f)))
            {
                _working.notes.RemoveAt(_selectedNoteIndex);
                ClearSelection();
                Repaint();
            }
            if (GUILayout.Button("Deselect", GUILayout.Width(90f)))
            {
                ClearSelection();
                Repaint();
            }
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _selectedNoteIndex = -1;
    }

    // -------------------------------------------------------------------------
    // Normalize (snap to subdivisions) — the explicit "normalize" step of the pipeline
    // -------------------------------------------------------------------------

    private void NormalizeWorking()
    {
        if (_working == null) return;

        int subs = Subs;
        float step = 1f / subs;
        var notes = _working.notes;
        if (notes == null) return;

        for (int i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            float snappedStart = Mathf.Max(0f, Mathf.Round(n.startBeat / step) * step);
            float snappedDur = Mathf.Round(n.durationBeats / step) * step;
            // Create() re-clamps and preserves degree/octave/velocity.
            notes[i] = MelodyPatternData.MelodyNoteEvent.Create(
                n.degree, snappedStart, Mathf.Max(step, snappedDur), n.octaveOffset, n.velocity);
        }

        // Deterministic stored order (start, degree, octave) — same convention as SnapshotOrdered.
        notes.Sort((a, b) =>
        {
            int c = a.startBeat.CompareTo(b.startBeat);
            if (c != 0) return c;
            c = ((int)a.degree).CompareTo((int)b.degree);
            if (c != 0) return c;
            return a.octaveOffset.CompareTo(b.octaveOffset);
        });

        ClearSelection(); // indices shifted by the sort
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Working-copy lifecycle
    // -------------------------------------------------------------------------

    private void BindAsset(MelodyPatternData asset)
    {
        _lastBound = asset;
        ClearSelection();

        if (asset == null)
        {
            _working = null;
            Repaint();
            return;
        }

        _working = asset.DeepCloneRuntime();
        _working.InitializeIfEmpty();

        editTimeSignature = _working.TimeSignature;
        editMeasures = Mathf.Max(1, _working.Measures);
        editSubdivisions = Mathf.Max(1, _working.subdivisions);

        FitOctaveWindowToNotes();
        Repaint();
    }

    private void CreateNewPattern()
    {
        targetAsset = null;
        _lastBound = null;
        ClearSelection();

        _working = ScriptableObject.CreateInstance<MelodyPatternData>();
        _working.name = "New Melody Pattern (unsaved)";

        int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        _working.SetSignature(bpm, editMeasures, editSubdivisions);
        _working.TimeSignature = editTimeSignature;
        _working.InitializeIfEmpty();

        editMinOctave = -1;
        editMaxOctave = 1;

        Repaint();
    }

    /// <summary>
    /// Expand the visible octave window so every existing note is renderable (no clamping on load).
    /// Always keeps at least the default −1..+1 around the reference octave.
    /// </summary>
    private void FitOctaveWindowToNotes()
    {
        int lo = -1, hi = 1;
        if (_working?.notes != null)
        {
            foreach (var n in _working.notes)
            {
                lo = Mathf.Min(lo, n.octaveOffset);
                hi = Mathf.Max(hi, n.octaveOffset);
            }
        }
        editMinOctave = Mathf.Clamp(lo, OctaveFloor, OctaveCeil);
        editMaxOctave = Mathf.Clamp(hi, OctaveFloor, OctaveCeil);
    }

    /// <summary>
    /// Apply meter/grid changes (deferred one frame to avoid mid-draw resize). Note timing is
    /// stored in absolute beats, so notes are meter-independent and require no remap — unlike the
    /// step-array remap in DrumPatternEditorWindow. Notes that fall outside a shortened pattern
    /// are preserved (not deleted); they simply stop rendering until Measures is increased.
    /// </summary>
    private void ApplySignatureToWorking()
    {
        if (_working == null)
        {
            CreateNewPattern();
            return;
        }

        int newBeats = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        _working.SetSignature(newBeats, editMeasures, editSubdivisions);
        _working.TimeSignature = editTimeSignature;
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Action buttons + persistence
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

    private void ApplyToAsset()
    {
        if (targetAsset == null || _working == null) return;

        Undo.RecordObject(targetAsset, "Melody Pattern Editor: Apply");
        CopyWorkingInto(targetAsset);
        // PATTERN-PERSIST-1 — store owns SetDirty + SaveAssets + cache refresh.
        _melodyStore.Save(targetAsset);

        BindAsset(targetAsset);
        Debug.Log($"[MelodyPatternEditor] Applied to {AssetDatabase.GetAssetPath(targetAsset)}");
    }

    private void SaveAsNewAsset()
    {
        if (_working == null) return;

        // Ensure the canonical root exists so the dialog can default into it.
        Directory.CreateDirectory(_melodyStore.AssetsSaveRootPath);
        AssetDatabase.Refresh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Melody Pattern As…",
            BuildDefaultName(),
            "asset",
            "Choose where to save the new melody pattern asset.",
            _melodyStore.AssetsSaveRootPath);

        if (string.IsNullOrEmpty(path)) return;

        // PATTERN-PERSIST-1 / D6=C — window keeps the naming dialog above; the store
        // owns the AssetDatabase write. Create at the chosen path, then populate under
        // Undo, then Save() to flush field edits + refresh the browse cache.
        var newAsset = ScriptableObject.CreateInstance<MelodyPatternData>();
        _melodyStore.PersistNewAtPath(newAsset, path);

        Undo.RecordObject(newAsset, "Melody Pattern Editor: Save As New");
        CopyWorkingInto(newAsset, Path.GetFileNameWithoutExtension(path));
        _melodyStore.Save(newAsset);

        targetAsset = newAsset;
        BindAsset(targetAsset);
        Debug.Log($"[MelodyPatternEditor] Saved new asset at {path}");
    }

    private void CopyWorkingInto(MelodyPatternData dst, string nameOverride = null)
    {
        dst.DisplayName = string.IsNullOrEmpty(nameOverride)
            ? _working.DisplayName
            : nameOverride;
        dst.TimeSignature = _working.TimeSignature;
        dst.Measures = _working.Measures;
        dst.beatsPerMeasure = _working.beatsPerMeasure;
        dst.subdivisions = _working.subdivisions;

        dst.notes ??= new List<MelodyPatternData.MelodyNoteEvent>();
        dst.notes.Clear();
        if (_working.notes != null)
            dst.notes.AddRange(_working.notes);
    }

    private string BuildDefaultName()
    {
        if (_working == null) return "NewMelodyPattern";
        string ts = $"{_working.beatsPerMeasure}-{_working.subdivisions}";
        int noteCount = _working.notes?.Count ?? 0;
        return $"Melody_{ts}_{_working.Measures}m_{noteCount}n";
    }

    // -------------------------------------------------------------------------
    // Styles
    // -------------------------------------------------------------------------

    private void EnsureStyles()
    {
        if (_stylesBuilt) return;

        _rowLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        _noteLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        _noteLabelStyle.normal.textColor = Color.black;

        _stylesBuilt = true;
    }
}
#endif
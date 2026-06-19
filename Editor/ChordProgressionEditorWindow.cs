#if UNITY_EDITOR
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// <summary>
/// Editor window to generate / overwrite a ChordProgressionData asset
/// from a simple Roman-numeral string like:
///     "I – V – vi – IV"
/// or  "i (2) – iv (1) – v (1)"
/// Durations are in measures; if omitted, a default duration is used.
/// The window will:
/// - compute measures & subdivisions
/// - fill the ChordEvent list (startStep, lengthSteps, degree, quality, velocity)
/// - set the allowed tonalities list.
/// </summary>
public partial class ChordProgressionEditorWindow : EditorWindow
{
    [MenuItem("MidiGenPlay/Chord Progression Editor...")]
    public static void Open()
    {
        GetWindow<ChordProgressionEditorWindow>("Chord Progression Editor");
    }

    [SerializeField] private ChordProgressionData targetAsset;
    [SerializeField] private ChordProgressionPaletteSO targetPalette;
    private enum InputMode { RomanString, Grid }
    [SerializeField] private InputMode inputMode = InputMode.RomanString;

    [SerializeField][TextArea(2, 4)] private string progressionInput = "I – V – vi – IV";
    [SerializeField] private float defaultDurationMeasures = 1f;
    [SerializeField][Range(1, 127)] private int defaultVelocity = 96;

    // --- Grid authoring state (for Grid mode) ---
    [SerializeField] private int gridMeasures = 4;
    [SerializeField] private int gridBeatsPerMeasure = 4;
    [SerializeField][Range(1, 8)] private int gridSubdivisions = 1;
    [SerializeField]
    private List<ChordProgressionData.ChordEvent> gridEvents
        = new List<ChordProgressionData.ChordEvent>();

    // --- Grid editing selection ---
    [SerializeField] private bool gridHasSelection;
    [SerializeField] private int gridSelectedIndex = -1; // -1 = brand new event
    [SerializeField] private ChordProgressionData.ChordEvent gridEditingEvent;
    [SerializeField] private bool gridInitializedFromAsset;

    [SerializeField] private TimeSignature timeSignature = TimeSignature.FourFour;

    [SerializeField] private Tonality referenceTonality = Tonality.Ionian;


    // how we auto-infer diatonic qualities when quality is *not* explicit.
    private enum AutoDiatonicMode
    {
        None,       // literal: case = triad quality, key ignored
        Triads,     // diatonic triads for (mode, degree)
        Sevenths    // diatonic seventh chords for (mode, degree)
    }

    [SerializeField] private AutoDiatonicMode autoDiatonicMode = AutoDiatonicMode.Triads;

    // TODO: Maybe make only Ionian true by default?
    private Dictionary<Tonality, bool> tonalityFlags; // Tonality toggles (all true by default)

    // Preview
    [SerializeField] private NoteName previewRoot = NoteName.C; // only for preview
    [SerializeField] private string previewChordNames = "";
    [SerializeField] private string previewGridText = "";
    [SerializeField] private int previewMeasures;
    [SerializeField] private int previewSubdivisions;
    [SerializeField] private int previewBeatsPerMeasure;

    [SerializeField] private Vector2 mainScroll;

    // Services
    private RomanProgressionParser romanParser = new RomanProgressionParser();
    private RhythmGridQuantizer rhythmQuantizer = new RhythmGridQuantizer();

    private bool showAllowedTonalities = true; // foldout state
    private GUIStyle gridPreviewStyle;
    private GUIStyle chordBlockLabelStyle;
    private ChordProgressionData lastLoadedAsset;
    private Vector2 previewGridScroll;

    private void OnEnable()
    {
        if (tonalityFlags == null)
        {
            tonalityFlags = Enum.GetValues(typeof(Tonality))
                .Cast<Tonality>()
                .ToDictionary(t => t, t => true);
        }
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        EditorGUILayout.LabelField(
            "Chord Progression Editor", EditorStyles.boldLabel);

        var newTargetAsset = (ChordProgressionData)EditorGUILayout.ObjectField(
            new GUIContent("Target Asset",
                "Existing ChordProgressionData to overwrite, " +
                "or leave empty to create a new one."),
            targetAsset, typeof(ChordProgressionData), false);

        if (newTargetAsset != targetAsset)
        {
            targetAsset = newTargetAsset;
            OnTargetAssetChanged();
        }

        targetPalette = (ChordProgressionPaletteSO)EditorGUILayout.ObjectField(
            new GUIContent("Progression Palette (optional)",
                "Palette asset where you can add this progression as a " +
                "weighted entry for card designers."),
            targetPalette,
            typeof(ChordProgressionPaletteSO),
            false);

        inputMode = (InputMode)GUILayout.Toolbar(
            (int)inputMode,
            new[] { "Roman", "Grid" });

        EditorGUILayout.Space();

        timeSignature = (TimeSignature)EditorGUILayout.EnumPopup(
            new GUIContent("Time Signature",
                "Meter used to quantize durations and compute total measures."),
            timeSignature);

        if (inputMode == InputMode.RomanString)
        {
            DrawRomanMode();
        }
        else
        {
            DrawGridMode();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tonality & Qualities", EditorStyles.boldLabel);

        referenceTonality = (Tonality)EditorGUILayout.EnumPopup(
            new GUIContent("Reference Tonality",
                "Used to derive diatonic triad quality (Major/Minor/Dim) for each degree."),
            referenceTonality);

        autoDiatonicMode = (AutoDiatonicMode)EditorGUILayout.EnumPopup(
            new GUIContent("Auto Diatonic Qualities",
                "None: literal – roman case = triad quality; key ignored.\n" +
                "Triads: infer diatonic triads for (mode, degree) when no suffix.\n" +
                "Sevenths: infer diatonic 7th chords for (mode, degree) when no suffix."),
            autoDiatonicMode);

        previewRoot = (NoteName)EditorGUILayout.EnumPopup(
            new GUIContent("Preview Root Note",
                "Key used only for displaying chord symbols (e.g. Cmaj7, G7). " +
                "Does not affect the stored progression."),
            previewRoot);

        EditorGUILayout.Space();

        showAllowedTonalities = EditorGUILayout.Foldout(
            showAllowedTonalities, "Allowed Tonalities", true);

        if (tonalityFlags == null)
            OnEnable();

        if (showAllowedTonalities)
        {
            EditorGUI.indentLevel++;
            foreach (var key in tonalityFlags.Keys.ToList())
            {
                tonalityFlags[key] = 
                    EditorGUILayout.ToggleLeft(key.ToString(), tonalityFlags[key]);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // Optional authoring metadata on the current asset
        DrawSongReferencesSection();

        EditorGUILayout.Space();

        // --- Preview header -------------------------------------------------------
        EditorGUILayout.LabelField(
            "Preview (Roman → concrete chords)",
            EditorStyles.boldLabel);

        // Simple linear preview line (always visible)
        EditorGUILayout.HelpBox(
            string.IsNullOrEmpty(previewChordNames)
                ? "Press 'Parse & Preview' or 'Apply To Target Asset' to update the preview."
                : previewChordNames,
            MessageType.None);

        // --- Grid preview (bars / beats / colors) --------------------------------
        if (previewMeasures > 0 &&
            previewBeatsPerMeasure > 0 &&
            previewSubdivisions > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Grid: {previewMeasures} bars, " +
                $"{previewBeatsPerMeasure} beats/bar, " +
                $"subdivisions x{previewSubdivisions}",
                EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(previewGridText))
            {
                if (gridPreviewStyle == null)
                {
                    gridPreviewStyle = new GUIStyle(EditorStyles.label)
                    {
                        richText = true,
                        wordWrap = true,
                        font = EditorStyles.miniFont
                    };
                }

                // Boxed scroll area so long progressions don't overlap with buttons.
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    // Height hint: at least a few lines, up to ~10 lines before scrolling
                    float line = EditorGUIUtility.singleLineHeight;
                    float minHeight = line * 3f;
                    float maxHeight = line * 10f;

                    previewGridScroll = EditorGUILayout.BeginScrollView(
                        previewGridScroll,
                        GUILayout.MinHeight(minHeight),
                        GUILayout.MaxHeight(maxHeight));

                    EditorGUILayout.LabelField(previewGridText, gridPreviewStyle);

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        EditorGUILayout.Space();

        // --- Action buttons ---------------------------------------------------
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Parse & Preview (no write)"))
            {
                if (inputMode == InputMode.RomanString)
                {
                    // Just parse the text field as before.
                    ParseAndPreview(onlyPreview: true);
                }
                else // Grid mode
                {
                    // 1) Turn the grid into a Roman string
                    var cleaned = GetSortedGridEvents();
                    var romanFromGrid = BuildRomanStringFromGrid(cleaned);

                    // 2) Store it in the shared progressionInput field
                    progressionInput = romanFromGrid;

                    // 3) Reuse the existing Roman pipeline for the previews
                    ParseAndPreview(onlyPreview: true);
                }
            }

            GUI.enabled = targetAsset != null;
            if (GUILayout.Button("Apply To Target Asset"))
            {
                if (inputMode == InputMode.RomanString)
                {
                    // Roman pipeline: Parse & write into asset via ApplyToAsset()
                    ParseAndPreview(onlyPreview: false);
                }
                else
                {
                    // Grid pipeline: write grid directly into the asset
                    ApplyGridToTarget();
                }
            }
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Progression"))
            {
                NewProgression();
            }

            if (GUILayout.Button("Save As New Asset..."))
            {
                SaveAsNewAsset();
            }

            GUI.enabled = targetPalette != null && targetAsset != null;
            if (GUILayout.Button("Add Current To Palette"))
            {
                AddCurrentToPalette();
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space();
        DrawLLMPanel();

        EditorGUILayout.EndScrollView();
    }

    private void DrawRomanMode()
    {
        EditorGUILayout.LabelField("Progression String");
        EditorGUILayout.HelpBox(
            "Examples:\n" +
            "  I – V – vi – IV\n" +
            "  i (2) – iv (1) – v (1)\n" +
            "Durations are in measures. If omitted, Default Duration is used.",
            MessageType.Info);

        progressionInput = EditorGUILayout.TextArea(progressionInput);

        defaultDurationMeasures = EditorGUILayout.FloatField(
            new GUIContent("Default Duration (measures)",
                "Used when a chord has no '(x)' duration suffix."),
            defaultDurationMeasures);

        defaultVelocity = EditorGUILayout.IntSlider(
            new GUIContent("Default Velocity", "Velocity for all chord events."),
            defaultVelocity, 1, 127);
    }

    private void DrawGridMode()
    {
        if (targetAsset == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Target Asset to author a progression in Grid mode.",
                MessageType.Info);
            return;
        }

        // Make sure we are looking at the current asset's data
        SyncGridFromAsset();

        EditorGUILayout.LabelField("Grid Parameters");

        // Measures
        gridMeasures = EditorGUILayout.IntField(
            new GUIContent("Measures",
                "Total number of bars for this progression."),
            gridMeasures);

        // Beats per bar (independent from TimeSignature enum for now)
        gridBeatsPerMeasure = EditorGUILayout.IntField(
            new GUIContent("Beats Per Measure",
                "Meter numerator. For 4/4 use 4, for 3/4 use 3, etc."),
            gridBeatsPerMeasure);

        // Subdivisions = timing steps per beat
        gridSubdivisions = EditorGUILayout.IntSlider(
            new GUIContent("Subdivisions (steps per beat)",
                "Horizontal grid resolution. 1 = quarter notes, " +
                "2 = eighths, 4 = sixteenths, etc."),
            gridSubdivisions, 1, 8);

        // Clamp to sensible minimums
        gridMeasures = Mathf.Max(1, gridMeasures);
        gridBeatsPerMeasure = Mathf.Max(1, gridBeatsPerMeasure);
        gridSubdivisions = Mathf.Max(1, gridSubdivisions);

        // Optional utility button to clear all events from the grid
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear Grid", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog(
                        "Clear Grid",
                        "Remove all chord events from the grid?\n\n" +
                        "Note: this only clears the editor grid; " +
                        "the asset is not modified until you Apply or Save.",
                        "Clear",
                        "Cancel"))
                {
                    gridEvents.Clear();
                    gridHasSelection = false;
                    gridSelectedIndex = -1;
                    gridEditingEvent = null;
                    Repaint();
                }
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Grid mode:\n" +
            "- Measures / Beats / Subdivisions define the horizontal grid.\n" +
            "- ChordEvents are shown as colored blocks (per degree).\n" +
            "- Click on a block to edit it, or on empty space to create a new one.",
            MessageType.Info);

        // Reserve a rect where the actual chord lane will be drawn.
        const float laneHeight = 32f;
        Rect gridRect = GUILayoutUtility.GetRect(
            0, 10000, laneHeight, laneHeight);

        // Background
        EditorGUI.DrawRect(gridRect, new Color(0.15f, 0.15f, 0.15f, 1f));

        int stepsPerMeasure = gridBeatsPerMeasure * gridSubdivisions;
        int totalSteps = Mathf.Max(1, gridMeasures * stepsPerMeasure);
        float stepWidth = gridRect.width / totalSteps;

        // Bar separators
        if (totalSteps > 0)
        {
            Handles.BeginGUI();
            // Bar lines
            Handles.color = new Color(1f, 0.9f, 0.1f, 0.9f);
            for (int bar = 0; bar <= gridMeasures; bar++)
            {
                float x = gridRect.xMin + bar * stepsPerMeasure * stepWidth;
                Handles.DrawLine(
                    new Vector2(x, gridRect.yMin),
                    new Vector2(x, gridRect.yMax));
            }

            // Light beat grid
            Handles.color = new Color(1f, 1f, 1f, 0.05f);
            for (int s = 0; s <= totalSteps; s++)
            {
                float x = gridRect.xMin + s * stepWidth;
                Handles.DrawLine(
                    new Vector2(x, gridRect.yMin),
                    new Vector2(x, gridRect.yMax));
            }

            Handles.EndGUI();
        }

        // Prepare degree → color mapping (using your existing note palette)
        var scale = GetScaleFromTonality(referenceTonality, previewRoot);
        var scaleNotes = GetNotesFromScale(scale, previewRoot, 4, 7)
                            .Select(n => n.NoteName)
                            .ToArray();

        Color[] degreeColors = new Color[7];
        for (int i = 0; i < degreeColors.Length; i++)
        {
            Color col;
            if (!ColorUtility.TryParseHtmlString(
                    "#" + ColorHexForNote(scaleNotes[i]),
                    out col))
            {
                col = Color.white;
            }
            degreeColors[i] = col;
        }

        // Label style for chord blocks
        if (chordBlockLabelStyle == null)
        {
            chordBlockLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            chordBlockLabelStyle.normal.textColor = Color.black;
        }

        // Draw each ChordEvent as a colored block
        if (gridEvents != null && gridEvents.Count > 0)
        {
            var qualityResolver = new ChordQualityResolver(
                referenceTonality,
                GetAutoChordQualityMode());

            for (int i = 0; i < gridEvents.Count; i++)
            {
                var e = gridEvents[i];

                int originalStart = e.startStep;
                int originalEnd = e.startStep + e.lengthSteps;

                // Completely outside current grid → don't draw at all
                if (originalStart >= totalSteps || originalEnd <= 0)
                    continue;

                // Clip to visible range [0, totalSteps)
                int start = Mathf.Max(0, originalStart);
                int end = Mathf.Min(totalSteps, originalEnd);
                int length = Mathf.Max(1, end - start);

                float x = gridRect.xMin + start * stepWidth;
                float w = length * stepWidth;

                int degIndex = Mathf.Clamp((int)e.degree, 0, 6);
                Color col = degreeColors[degIndex];

                bool isDiatonic = qualityResolver.IsChordDiatonic(e.degree, e.quality);
                if (!isDiatonic)
                    col = Color.Lerp(col, Color.black, 0.35f);

                if (gridHasSelection && i == gridSelectedIndex)
                    col = Color.Lerp(col, Color.white, 0.4f);

                var blockRect = new Rect(
                    x + 1f,
                    gridRect.yMin + 2f,
                    w - 2f,
                    gridRect.height - 4f);

                EditorGUI.DrawRect(blockRect, col);

                string rn = ToRomanRich(e.degree, e.quality);

                // Prefix accidentals
                if (e.degreeAccidental < 0)
                    rn = "b" + rn;
                else if (e.degreeAccidental > 0)
                    rn = "#" + rn;

                if (!isDiatonic)
                    rn = "<i>" + rn + "</i>";

                GUI.Label(blockRect, rn, chordBlockLabelStyle);
            }
        }

        // Handle mouse clicks for selection / creation
        HandleGridMouse(gridRect, totalSteps, stepWidth);

        // Inline editor for the currently selected event
        DrawGridSelectionInspector(totalSteps);
    }

    private void DrawSongReferencesSection()
    {
        if (targetAsset == null)
            return;

        if (targetAsset.songReferences == null)
            targetAsset.songReferences = new List<string>();

        EditorGUILayout.LabelField(
            "Song References (optional)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "List songs that use (or approximate) this progression so " +
            "designers/composers can quickly listen to examples.",
            MessageType.None);

        int removeIndex = -1;

        for (int i = 0; i < targetAsset.songReferences.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUILayout.TextField(
                $"Ref {i + 1}", targetAsset.songReferences[i]);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetAsset, "Edit Song Reference");
                targetAsset.songReferences[i] = newValue;
                EditorUtility.SetDirty(targetAsset);
            }

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                Undo.RecordObject(targetAsset, "Remove Song Reference");
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0 &&
            removeIndex < targetAsset.songReferences.Count)
        {
            targetAsset.songReferences.RemoveAt(removeIndex);
            EditorUtility.SetDirty(targetAsset);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Reference", GUILayout.Width(120)))
            {
                Undo.RecordObject(targetAsset, "Add Song Reference");
                targetAsset.songReferences.Add(string.Empty);
                EditorUtility.SetDirty(targetAsset);
            }
        }
    }

    private void HandleGridMouse(Rect gridRect, int totalSteps, float stepWidth)
    {
        var evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0)
            return;

        if (!gridRect.Contains(evt.mousePosition))
            return;

        // Convert x → step index
        float localX = evt.mousePosition.x - gridRect.xMin;
        int clickedStep = Mathf.Clamp(Mathf.FloorToInt(localX / stepWidth), 0, totalSteps - 1);

        int idx = FindGridEventCovering(clickedStep);
        if (idx >= 0)
        {
            // Editing existing event – copy into working struct
            var src = gridEvents[idx];
            gridEditingEvent = new ChordProgressionData.ChordEvent
            {
                startStep = src.startStep,
                lengthSteps = src.lengthSteps,
                degree = src.degree,
                quality = src.quality,
                velocity = src.velocity,
                degreeAccidental = src.degreeAccidental,
            };
            gridSelectedIndex = idx;
            gridHasSelection = true;
        }
        else
        {
            // Creating a new event at this step
            int defaultLen = DefaultLenOneMeasureFrom(clickedStep);
            var defaultDegree = ScaleDegree.Tonic;

            bool preferSeventh =
                autoDiatonicMode == AutoDiatonicMode.Sevenths;

            var defaultQuality = GetSuggestedQuality(
                referenceTonality,
                defaultDegree,
                preferSeventh);

            gridEditingEvent = new ChordProgressionData.ChordEvent
            {
                startStep = clickedStep,
                lengthSteps = defaultLen,
                degree = defaultDegree,
                quality = defaultQuality,
                velocity = defaultVelocity > 0 ? defaultVelocity : 96,
                degreeAccidental = 0
            };

            gridSelectedIndex = -1; // brand new
            gridHasSelection = true;
        }

        GUI.FocusControl(null);
        evt.Use();
        Repaint();
    }

    private void DrawGridSelectionInspector(int totalSteps)
    {
        if (!gridHasSelection || gridEditingEvent == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Chord Event", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            gridEditingEvent.startStep = 
                EditorGUILayout.IntField("Start Step", gridEditingEvent.startStep);
            gridEditingEvent.lengthSteps = 
                EditorGUILayout.IntField("Length (steps)", gridEditingEvent.lengthSteps);

            // Degree popup
            var oldDegree = gridEditingEvent.degree;
            gridEditingEvent.degree =
                (ScaleDegree)EditorGUILayout.EnumPopup("Degree", gridEditingEvent.degree);

            // Accidental popup
            gridEditingEvent.degreeAccidental = EditorGUILayout.IntPopup(
                "Degree Accidental",
                gridEditingEvent.degreeAccidental,
                new[] { "Flat (♭)", "Natural", "Sharp (♯)" },
                new[] { -1, 0, 1 });

            // If the degree changed, and we are in a diatonic auto mode,
            // pick a sensible quality for the new degree.
            if (gridEditingEvent.degree != oldDegree &&
                autoDiatonicMode != AutoDiatonicMode.None)
            {
                bool preferSeventh =
                    autoDiatonicMode == AutoDiatonicMode.Sevenths;

                gridEditingEvent.quality = GetSuggestedQuality(
                    referenceTonality,
                    gridEditingEvent.degree,
                    preferSeventh);
            }

            // Quality popup (user can always override the auto choice)
            gridEditingEvent.quality =
                (ChordQuality)EditorGUILayout.EnumPopup("Quality", gridEditingEvent.quality);

            gridEditingEvent.velocity = 
                EditorGUILayout.IntSlider("Velocity", gridEditingEvent.velocity, 1, 127);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("OK"))
            {
                CommitGridEdit(totalSteps);
            }

            if (gridSelectedIndex >= 0)
            {
                if (GUILayout.Button("Delete"))
                {
                    if (gridSelectedIndex >= 0 && gridSelectedIndex < gridEvents.Count)
                        gridEvents.RemoveAt(gridSelectedIndex);

                    gridHasSelection = false;
                    gridSelectedIndex = -1;
                    gridEditingEvent = null;
                    Repaint();
                }
            }

            if (GUILayout.Button("Cancel"))
            {
                gridHasSelection = false;
                gridSelectedIndex = -1;
                gridEditingEvent = null;
                Repaint();
            }
        }
    }

    // Commit logic + helpers (find/insert/remove)

    private void CommitGridEdit(int totalSteps)
    {
        if (gridEditingEvent == null)
            return;

        var ev = gridEditingEvent;

        // Bounds
        ev.startStep = Mathf.Clamp(ev.startStep, 0, totalSteps - 1);
        ev.lengthSteps = Mathf.Max(1, Mathf.Min(ev.lengthSteps, totalSteps - ev.startStep));
        ev.velocity = Mathf.Clamp(ev.velocity, 1, 127);

        int endExclusive = ev.startStep + ev.lengthSteps;

        // Remove overlaps
        RemoveGridOverlaps(ev.startStep, endExclusive);

        // If an event already starts here, update it; otherwise insert new
        int idx = FindGridEventStarting(ev.startStep);
        if (idx >= 0)
        {
            gridEvents[idx] = ev;
        }
        else
        {
            InsertGridEvent(ev);
        }

        gridHasSelection = false;
        gridSelectedIndex = -1;
        gridEditingEvent = null;

        Repaint();
    }

    private int FindGridEventCovering(int step)
    {
        for (int i = 0; i < gridEvents.Count; i++)
        {
            int s = gridEvents[i].startStep;
            int e = s + gridEvents[i].lengthSteps;
            if (step >= s && step < e) return i;
        }
        return -1;
    }

    private int FindGridEventStarting(int step)
    {
        for (int i = 0; i < gridEvents.Count; i++)
            if (gridEvents[i].startStep == step) return i;
        return -1;
    }

    private void InsertGridEvent(ChordProgressionData.ChordEvent ev)
    {
        gridEvents.Add(ev);
        gridEvents.Sort((a, b) => a.startStep.CompareTo(b.startStep));
    }

    private void RemoveGridOverlaps(int start, int endExclusive)
    {
        for (int i = gridEvents.Count - 1; i >= 0; i--)
        {
            int s = gridEvents[i].startStep;
            int e = s + gridEvents[i].lengthSteps;
            if (e > start && s < endExclusive)
                gridEvents.RemoveAt(i);
        }
    }

    // Same idea as in ChordProgressionPanelController.DefaultLenOneMeasureFrom
    private int DefaultLenOneMeasureFrom(int step)
    {
        int stepsPerMeasure = gridBeatsPerMeasure * gridSubdivisions;
        int currentBarStart = (step / stepsPerMeasure) * stepsPerMeasure;
        int currentBarEnd = currentBarStart + stepsPerMeasure;
        int remaining = Mathf.Clamp(currentBarEnd - step, 1, stepsPerMeasure);
        return remaining;
    }

    // --- Main application logic ---

    /// <summary>
    /// Common helper used by the two buttons:
    /// - Parses the Roman progression string.
    /// - If successful, updates the linear + grid preview.
    /// - If onlyPreview == false, also calls ApplyToAsset() to write into the asset
    ///   (and into the library if configured).
    /// </summary>
    private void ParseAndPreview(bool onlyPreview)
    {
        // Basic guard
        if (string.IsNullOrWhiteSpace(progressionInput))
        {
            EditorUtility.DisplayDialog(
                "Error",
                "Progression input string is empty.",
                "OK");
            return;
        }

        // Try to parse the Roman string into ParsedChord entries using the shared parser.
        bool inferFromCase = (autoDiatonicMode == AutoDiatonicMode.None);

        if (!romanParser.TryParse(
            progressionInput,
            defaultDurationMeasures,
            inferFromCase,
            out List<ParsedChord> chords,
            out string parseError))
        {
            // Show parse error and clear previews so it's obvious something failed
            if (!string.IsNullOrEmpty(parseError))
                EditorUtility.DisplayDialog("Parse Error", parseError, "OK");

            previewChordNames = "";
            previewGridText = "";
            previewMeasures = 0;
            previewSubdivisions = 0;
            previewBeatsPerMeasure = 0;
            return;
        }

        // Update both the linear text preview and the colored grid preview
        UpdatePreview(chords);

        // If this was the "Apply" button, also write everything to the asset
        if (!onlyPreview)
        {
            // For simplicity we reuse the existing ApplyToAsset(), which
            // re-parses the string internally and also:
            // - computes the timing grid,
            // - fills ChordProgressionData.events,
            // - updates DisplayName, originalInput, tonalities,
            ApplyToAsset();
        }
    }

    private void ApplyToAsset()
    {
        if (string.IsNullOrWhiteSpace(progressionInput))
        {
            EditorUtility.DisplayDialog("Error", 
                "Progression input string is empty.", "OK");
            return;
        }

        bool inferFromCase = (autoDiatonicMode == AutoDiatonicMode.None);

        if (!romanParser.TryParse(
                progressionInput,
                defaultDurationMeasures,
                inferFromCase,
                out List<ParsedChord> chords,
                out string parseError))
        {
            EditorUtility.DisplayDialog("Parse Error",
                parseError ?? "Unknown error.", "OK");
            return;
        }

        if (chords == null || chords.Count == 0)
        {
            EditorUtility.DisplayDialog("Parse Error",
                "No chords were parsed from the input.", "OK");
            return;
        }

        // Decide which TimeSignature we are using for this progression.
        //    - If there is already a target asset, respect its TS by default.
        //    - If not, fall back to the editor window field.
        TimeSignature effectiveTs =
            (targetAsset != null) ? targetAsset.TimeSignature : timeSignature;

        // Beats per bar from MusicTheory.TimeSignatureProperties
        var tsInfo = TimeSignatureProperties[effectiveTs];
        int beatsPerMeasure = tsInfo.BeatsPerMeasure;

        // Quantize chord durations into integer steps and pick a subdivisions value.
        if (!rhythmQuantizer.TryQuantizeChordDurations(
                chords,
                beatsPerMeasure,
                out int subdivisions,
                out List<int> lengthsSteps,
                out int totalSteps,
                out string durError))
        {
            EditorUtility.DisplayDialog("Quantization Error",
                durError ?? "Could not find a consistent grid (steps / subdivisions).",
                "OK");
            return;
        }

        // Create asset if needed
        if (targetAsset == null)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Chord Progression",
                "New Chord Progression Data",
                "asset",
                "Choose where to save the progression asset.");

            if (string.IsNullOrEmpty(path)) return;

            targetAsset = ScriptableObject.CreateInstance<ChordProgressionData>();
            AssetDatabase.CreateAsset(targetAsset, path);
        }

        Undo.RecordObject(targetAsset, "Update Chord Progression");

        // Basic fields on the asset
        targetAsset.TimeSignature = effectiveTs;

        int stepsPerMeasure = beatsPerMeasure * subdivisions;
        int totalMeasures = Mathf.Max(1, totalSteps / Mathf.Max(1, stepsPerMeasure));

        targetAsset.Measures = totalMeasures;
        targetAsset.subdivisions = subdivisions;

        // Store original string for debugging / display
        targetAsset.originalInput = progressionInput;

        // Allowed tonalities from the toggles
        targetAsset.tonalities.Clear();
        foreach (var kv in tonalityFlags)
        {
            if (kv.Value) 
                targetAsset.tonalities.Add(kv.Key);
        }

        // --- Build events in step units ---
        targetAsset.events.Clear();
        int currentStep = 0;

        var qualityResolver = new ChordQualityResolver(
            referenceTonality,
            GetAutoChordQualityMode());

        for (int i = 0; i < chords.Count; i++)
        {
            var pc = chords[i];
            int chordSteps = Mathf.Max(1, lengthsSteps[i]);

            // rests advance time but don't create events
            if (pc.isRest)
            {
                currentStep += chordSteps;
                continue;
            }

            var quality = qualityResolver.ResolveChordQuality(pc);
            bool isDiatonic = qualityResolver.IsChordDiatonic(pc.degree, quality);

            var evt = new ChordProgressionData.ChordEvent
            {
                degree = pc.degree,
                quality = quality,
                startStep = currentStep,
                lengthSteps = chordSteps,
                velocity = defaultVelocity,
                isDiatonic = isDiatonic,
                degreeAccidental = pc.degreeAccidental
            };

            targetAsset.events.Add(evt);
            currentStep += chordSteps;
        }

        targetAsset.UpdateDisplayNameAuto();

        // Keep grid view in sync with the asset we just wrote
        SyncGridFromAsset();

        EditorUtility.SetDirty(targetAsset);
        AssetDatabase.SaveAssets();

        // Keep the grid inspector view in sync with whatever we just wrote.
        SyncGridFromAsset(force: true);

        // Also refresh the concrete-chord preview after a successful apply
        UpdatePreview(chords);
    }

    /// <summary>
    /// Writes the current Grid state into the existing targetAsset.
    /// Also regenerates originalInput (Roman string) and DisplayName.
    /// </summary>
    private void ApplyGridToTarget()
    {
        if (gridEvents == null || gridEvents.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Grid Empty",
                "There are no ChordEvents in the grid to apply.",
                "OK");
            return;
        }

        if (targetAsset == null)
        {
            EditorUtility.DisplayDialog(
                "No Target Asset",
                "Assign a Target Asset in the object field or use 'Save As New Asset' instead.",
                "OK");
            return;
        }

        // Clean + clamp events to current grid size
        var cleaned = GetSortedGridEvents();

        Undo.RecordObject(targetAsset, "Apply Grid To Chord Progression");

        // Timing from grid parameters
        int beatsPerMeasure = Mathf.Max(1, gridBeatsPerMeasure);
        int subdivisions = Mathf.Max(1, gridSubdivisions);
        int stepsPerMeasure = beatsPerMeasure * subdivisions;

        targetAsset.TimeSignature = timeSignature;      // use window TS
        targetAsset.Measures = Mathf.Max(1, gridMeasures);
        targetAsset.subdivisions = subdivisions;

        // Copy tonalities from toggle dictionary
        if (targetAsset.tonalities == null)
            targetAsset.tonalities = new List<Tonality>();
        else
            targetAsset.tonalities.Clear();

        foreach (var kv in tonalityFlags)
        {
            if (kv.Value)
                targetAsset.tonalities.Add(kv.Key);
        }

        // Copy events from grid
        if (targetAsset.events == null)
            targetAsset.events = new List<ChordProgressionData.ChordEvent>();
        else
            targetAsset.events.Clear();

        targetAsset.events.AddRange(cleaned);

        // Build Roman string from the grid for metadata (originalInput + DisplayName)
        string romanFromGrid = BuildRomanStringFromGrid(cleaned);
        progressionInput = romanFromGrid;   // keep UI in sync
        targetAsset.originalInput = romanFromGrid;
        targetAsset.UpdateDisplayNameAuto();

        EditorUtility.SetDirty(targetAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Also refresh the previews based on the new Roman string
        ParseAndPreview(onlyPreview: true);
    }

    /// <summary>
    /// Builds:
    ///  - a linear preview like "Cmaj7 | G7 | Am7 | Fmaj7 (2)"
    ///  - a colored bar/beat grid,
    /// using the current previewRoot, referenceTonality and parsed chord qualities.
    /// </summary>
    private void UpdatePreview(List<ParsedChord> chords)
    {
        if (chords == null || chords.Count == 0)
        {
            previewChordNames = "";
            previewGridText = "";
            previewMeasures = 0;
            previewSubdivisions = 0;
            previewBeatsPerMeasure = 0;
            return;
        }

        // Use asset TS if available, otherwise window field
        TimeSignature effectiveTs =
            (targetAsset != null) ? targetAsset.TimeSignature : timeSignature;

        var tsInfo = TimeSignatureProperties[effectiveTs];
        int beatsPerMeasure = tsInfo.BeatsPerMeasure;

        // Compute timing grid for preview only
        if (!rhythmQuantizer.TryQuantizeChordDurations(
                chords,
                beatsPerMeasure,
                out int subdivisions,
                out List<int> lengthsSteps,
                out int totalSteps,
                out string durError))
        {
            previewChordNames = $"[Grid error: {durError}]";
            previewGridText = "";
            previewMeasures = 0;
            previewSubdivisions = 0;
            previewBeatsPerMeasure = 0;
            return;
        }

        int stepsPerMeasure = beatsPerMeasure * subdivisions;
        int measures = Mathf.Max(1, totalSteps / Mathf.Max(1, stepsPerMeasure));

        previewBeatsPerMeasure = beatsPerMeasure;
        previewSubdivisions = subdivisions;
        previewMeasures = measures;

        // --- Build chord symbols, colors and diatonic flags ---

        var scale = GetScaleFromTonality(referenceTonality, previewRoot);
        var scaleNotes = GetNotesFromScale(scale, previewRoot, 4, 7)
                            .Select(n => n.NoteName)
                            .ToArray();

        // One resolver per preview call (tonality + auto mode)
        var qualityResolver = new ChordQualityResolver(
            referenceTonality,
            GetAutoChordQualityMode());

        var chordSymbols = new List<string>(chords.Count);
        var chordColors = new List<string>(chords.Count);
        var chordIsDiatonic = new List<bool>(chords.Count);
        var linearParts = new List<string>(chords.Count);

        for (int i = 0; i < chords.Count; i++)
        {
            var pc = chords[i];

            if (pc.isRest)
            {
                // Linear preview: explicit Rest token
                string label = "Rest";
                if (Mathf.Abs(pc.durationMeasures - 1f) > 0.0001f)
                    label += $" ({pc.durationMeasures:g})";

                linearParts.Add(label);

                // For the grid, we won’t use the symbol/colors when isRest == true.
                chordSymbols.Add(string.Empty);
                chordColors.Add("ffffff");
                chordIsDiatonic.Add(true);
                continue;
            }

            int degIndex = Mathf.Clamp((int)pc.degree, 0, 6);
            var degreeRoot = scaleNotes[degIndex];

            // Apply accidental as a semitone offset
            degreeRoot = TransposeNoteName(degreeRoot, pc.degreeAccidental);

            var quality = qualityResolver.ResolveChordQuality(pc);

            bool isDiatonic = (pc.degreeAccidental == 0) &&
                  qualityResolver.IsChordDiatonic(pc.degree, quality);

            string symbol = GetChordSymbolSpelledForDegree(
                previewRoot, degIndex, degreeRoot, quality);

            chordSymbols.Add(symbol);
            chordColors.Add(ColorHexForNote(degreeRoot));
            chordIsDiatonic.Add(isDiatonic);

            string chordLabel = symbol;
            if (!isDiatonic)
                chordLabel = "*" + chordLabel; // mark borrowed chord in linear preview

            if (Mathf.Abs(pc.durationMeasures - 1f) > 0.0001f)
                chordLabel += $" ({pc.durationMeasures:g})";

            linearParts.Add(chordLabel);
        }

        // Simple linear preview
        previewChordNames = string.Join(" | ", linearParts);

        // --- Build per-beat grid ---
        int[] chordByStep = new int[totalSteps];
        int cursor = 0;
        for (int i = 0; i < chords.Count; i++)
        {
            int len = lengthsSteps[i];
            for (int s = 0; s < len && cursor + s < totalSteps; s++)
                chordByStep[cursor + s] = i;

            cursor += len;
            if (cursor >= totalSteps)
                break;
        }

        var sb = new System.Text.StringBuilder();

        for (int bar = 0; bar < measures; bar++)
        {
            sb.Append("|");
            for (int beat = 0; beat < beatsPerMeasure; beat++)
            {
                int stepIndex = bar * stepsPerMeasure + beat * subdivisions;
                if (stepIndex >= totalSteps)
                    stepIndex = totalSteps - 1;

                int chordIndex = chordByStep[stepIndex];
                bool restAtBeat = chords[chordIndex].isRest;

                // New chord at this beat?
                bool isNewChord = (bar == 0 && beat == 0);
                if (!isNewChord)
                {
                    int prevStepIndex = Mathf.Max(0, stepIndex - subdivisions);
                    int prevChordIndex = chordByStep[prevStepIndex];
                    isNewChord = chordIndex != prevChordIndex;
                }

                string cellText = "-";
                if (isNewChord)
                {
                    if (restAtBeat)
                    {
                        // Grey, italic "Rest" marker for the first beat of a rest span
                        cellText = "<color=#888888><i>Rest</i></color>";
                    }
                    else
                    {
                        string sym = chordSymbols[chordIndex];
                        string col = chordColors[chordIndex];
                        string colored = $"<color=#{col}>{sym}</color>";

                        if (!chordIsDiatonic[chordIndex])
                            colored = $"<i>{colored}</i>";

                        cellText = colored;
                    }
                }

                sb.Append(" ").Append(cellText).Append(" ");
            }
            sb.Append("|").AppendLine();
        }

        previewGridText = sb.ToString();
    }

    private static string ColorHexForNote(NoteName note)
    {
        // Simple, distinct-ish palette. Adjust to taste.
        switch (note)
        {
            case NoteName.C:        return "ff6666";
            case NoteName.CSharp:   return "ff9966";
            case NoteName.D:        return "ffcc66";
            case NoteName.DSharp:   return "ffff66";
            case NoteName.E:        return "ccff66";
            case NoteName.F:        return "99ff66";
            case NoteName.FSharp:   return "66ff99";
            case NoteName.G:        return "66ffff";
            case NoteName.GSharp:   return "6699ff";
            case NoteName.A:        return "9966ff";
            case NoteName.ASharp:   return "cc66ff";
            case NoteName.B:        return "ff66cc";
            default:                return "ffffff";
        }
    }

    /// <summary>
    /// Called whenever targetAsset changes in the object field.
    /// Loads its originalInput, time signature and allowed tonalities
    /// into the window, and refreshes the preview.
    /// </summary>
    private void OnTargetAssetChanged()
    {
        lastLoadedAsset = targetAsset;

        if (targetAsset == null)
        {
            progressionInput = "";
            previewChordNames = "";
            previewGridText = "";
            previewMeasures = 0;
            previewSubdivisions = 0;
            previewBeatsPerMeasure = 0;
            gridInitializedFromAsset = false;
            return;
        }

        // Use originalInput if present
        if (!string.IsNullOrWhiteSpace(targetAsset.originalInput))
            progressionInput = targetAsset.originalInput;

        // Sync meter
        timeSignature = targetAsset.TimeSignature;

        // Sync tonalities → toggle flags
        if (tonalityFlags == null)
            OnEnable();

        if (targetAsset.tonalities != null && targetAsset.tonalities.Count > 0)
        {
            var set = new HashSet<Tonality>(targetAsset.tonalities);
            foreach (var key in tonalityFlags.Keys.ToList())
                tonalityFlags[key] = set.Contains(key);
        }
        else
        {
            // If asset has no restrictions, default to "all allowed"
            // TODO: Default config
            foreach (var key in tonalityFlags.Keys.ToList())
                tonalityFlags[key] = true;
        }

        // sync grid state from the asset
        gridInitializedFromAsset = false;
        SyncGridFromAsset(force: true);

        // Refresh preview if we have a Roman string
        if (!string.IsNullOrWhiteSpace(progressionInput))
            ParseAndPreview(onlyPreview: true);

        Repaint();
    }

    /// <summary>
    /// Creates a brand new ChordProgressionData asset from the current editor state.
    /// - In Roman mode: parses progressionInput and quantizes as before.
    /// - In Grid mode: saves the current grid events directly and also derives a Roman string.
    /// Does NOT add it to any palette (use AddCurrentToPalette for that).
    /// </summary>
    private void SaveAsNewAsset()
    {
        // ----------------------------
        // 1) Roman-string pipeline
        // ----------------------------
        if (inputMode == InputMode.RomanString)
        {
            if (string.IsNullOrWhiteSpace(progressionInput))
            {
                EditorUtility.DisplayDialog("Error",
                    "Progression input string is empty.", "OK");
                return;
            }

            // Use case as explicit triad quality only when AutoDiatonicMode == None
            bool inferFromCase = (autoDiatonicMode == AutoDiatonicMode.None);

            if (!romanParser.TryParse(
                    progressionInput,
                    defaultDurationMeasures,
                    inferFromCase,
                    out List<ParsedChord> chords,
                    out string parseError))
            {
                EditorUtility.DisplayDialog("Parse Error",
                    parseError ?? "Unknown error.", "OK");
                return;
            }

            if (chords == null || chords.Count == 0)
            {
                EditorUtility.DisplayDialog("Parse Error",
                    "No chords were parsed from the input.", "OK");
                return;
            }

            // For a new asset, use the window's timeSignature field
            TimeSignature effectiveTs = timeSignature;
            var tsInfo = TimeSignatureProperties[effectiveTs];
            int beatsPerMeasure = tsInfo.BeatsPerMeasure;

            if (!rhythmQuantizer.TryQuantizeChordDurations(
                    chords,
                    beatsPerMeasure,
                    out int subdivisions,
                    out List<int> lengthsSteps,
                    out int totalSteps,
                    out string durError))
            {
                EditorUtility.DisplayDialog("Quantization Error",
                    durError ?? "Could not find a consistent grid (steps / subdivisions).",
                    "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Chord Progression As...",
                "New Chord Progression Data",
                "asset",
                "Choose where to save the new progression asset.");

            if (string.IsNullOrEmpty(path))
                return;

            var newAsset = ScriptableObject.CreateInstance<ChordProgressionData>();
            AssetDatabase.CreateAsset(newAsset, path);

            Undo.RecordObject(newAsset, "Create Chord Progression");

            int stepsPerMeasure = beatsPerMeasure * subdivisions;
            int totalMeasures = Mathf.Max(1, totalSteps / Mathf.Max(1, stepsPerMeasure));

            newAsset.TimeSignature = effectiveTs;
            newAsset.Measures = totalMeasures;
            newAsset.subdivisions = subdivisions;
            newAsset.originalInput = progressionInput;

            // Tonalities from toggles
            newAsset.tonalities.Clear();
            foreach (var kv in tonalityFlags)
                if (kv.Value)
                    newAsset.tonalities.Add(kv.Key);

            var qualityResolver = new ChordQualityResolver(
                referenceTonality,
                GetAutoChordQualityMode());

            // Events
            newAsset.events.Clear();
            int currentStep = 0;

            for (int i = 0; i < chords.Count; i++)
            {
                var pc = chords[i];
                int steps = Mathf.Max(1, lengthsSteps[i]);

                if (pc.isRest)
                {
                    // Silent span: just move the cursor forward.
                    currentStep += steps;
                    continue;
                }

                var quality = qualityResolver.ResolveChordQuality(pc);
                bool isDiatonic = qualityResolver.IsChordDiatonic(pc.degree, quality);

                var evt = new ChordProgressionData.ChordEvent
                {
                    degree = pc.degree,
                    quality = quality,
                    startStep = currentStep,
                    lengthSteps = steps,
                    velocity = defaultVelocity,
                    isDiatonic = isDiatonic,
                    degreeAccidental = pc.degreeAccidental
                };

                newAsset.events.Add(evt);
                currentStep += steps;
            }

            newAsset.UpdateDisplayNameAuto();

            // Point the window to the new asset and sync grid
            targetAsset = newAsset;
            SyncGridFromAsset(force: true);
            OnTargetAssetChanged();

            EditorUtility.SetDirty(newAsset);
            AssetDatabase.SaveAssets();
            return;
        }

        // ----------------------------
        // 2) Grid pipeline
        // ----------------------------
        if (gridEvents == null || gridEvents.Count == 0)
        {
            EditorUtility.DisplayDialog("Grid Empty",
                "There are no ChordEvents in the grid to save.", "OK");
            return;
        }

        string gridPath = EditorUtility.SaveFilePanelInProject(
            "Save Chord Progression As...",
            "New Chord Progression Data",
            "asset",
            "Choose where to save the new progression asset.");

        if (string.IsNullOrEmpty(gridPath))
            return;

        var assetFromGrid = ScriptableObject.CreateInstance<ChordProgressionData>();
        AssetDatabase.CreateAsset(assetFromGrid, gridPath);

        Undo.RecordObject(assetFromGrid, "Create Chord Progression (from grid)");

        // Timing from grid
        int gBeatsPerMeasure = Mathf.Max(1, gridBeatsPerMeasure);
        int gSubdivisions = Mathf.Max(1, gridSubdivisions);

        assetFromGrid.TimeSignature = timeSignature;
        assetFromGrid.Measures = Mathf.Max(1, gridMeasures);
        assetFromGrid.subdivisions = gSubdivisions;

        // Tonalities from toggles
        assetFromGrid.tonalities.Clear();
        foreach (var kv in tonalityFlags)
            if (kv.Value)
                assetFromGrid.tonalities.Add(kv.Key);

        // Events from cleaned grid
        var cleanedEvents = GetSortedGridEvents();
        assetFromGrid.events.Clear();
        assetFromGrid.events.AddRange(cleanedEvents);

        // Derive Roman string for metadata
        string romanFromGrid2 = BuildRomanStringFromGrid(cleanedEvents);
        progressionInput = romanFromGrid2;
        assetFromGrid.originalInput = romanFromGrid2;
        assetFromGrid.UpdateDisplayNameAuto();

        // Make the window edit this new asset
        targetAsset = assetFromGrid;
        SyncGridFromAsset(force: true);
        OnTargetAssetChanged();

        EditorUtility.SetDirty(assetFromGrid);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Adds the current targetAsset as a weighted entry to the assigned palette.
    /// Uses DisplayName (or asset name) as display label and a default weight of 1.
    /// </summary>
    private void AddCurrentToPalette()
    {
        if (targetPalette == null)
        {
            EditorUtility.DisplayDialog(
                "No Palette Assigned",
                "Assign a ChordProgressionPaletteSO in the 'Progression Palette' field first.",
                "OK");
            return;
        }

        if (targetAsset == null)
        {
            EditorUtility.DisplayDialog(
                "No Target Asset",
                "There is no ChordProgressionData to add. " +
                "Apply or Save As first.",
                "OK");
            return;
        }

        if (targetPalette.entries == null)
            targetPalette.entries = new List<ChordProgressionPaletteSO.WeightedEntry>();

        // Avoid duplicate entries for the same asset
        bool duplicate = targetPalette.entries.Any(e =>
            e != null && e.progression == targetAsset);

        if (duplicate)
        {
            EditorUtility.DisplayDialog(
                "Already In Palette",
                "This progression is already present in the selected palette.",
                "OK");
            return;
        }

        string label = !string.IsNullOrWhiteSpace(targetAsset.DisplayName)
            ? targetAsset.DisplayName
            : targetAsset.name;

        var entry = new ChordProgressionPaletteSO.WeightedEntry
        {
            progression = targetAsset,
            weight = 1f
        };

        targetPalette.entries.Add(entry);

        EditorUtility.SetDirty(targetPalette);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Added To Palette",
            $"Added '{label}' to palette '{targetPalette.name}'.",
            "OK");
    }


    private void SyncGridFromAsset(bool force = false)
    {
        if (targetAsset == null)
            return;

        if (gridEvents == null)
            gridEvents = new List<ChordProgressionData.ChordEvent>();

        // --- Measures / beats / subdivisions ---
        var tsInfo = TimeSignatureProperties[targetAsset.TimeSignature];

        if (force || gridMeasures <= 0)
            gridMeasures = Mathf.Max(1, targetAsset.Measures);

        if (force || gridBeatsPerMeasure <= 0)
            gridBeatsPerMeasure = tsInfo.BeatsPerMeasure;

        if (force || gridSubdivisions <= 0)
            gridSubdivisions = Mathf.Max(1, targetAsset.subdivisions);

        // --- Events cache ---
        // Only copy from asset when explicitly forced, or the first time.
        if (force || !gridInitializedFromAsset)
        {
            gridEvents.Clear();
            if (targetAsset.events != null)
                gridEvents.AddRange(targetAsset.events);

            gridInitializedFromAsset = true;
        }
    }

    private List<ChordProgressionData.ChordEvent> GetSortedGridEvents()
    {
        int beatsPerMeasure = Mathf.Max(1, gridBeatsPerMeasure);
        int stepsPerMeasure = beatsPerMeasure * Mathf.Max(1, gridSubdivisions);
        int totalSteps = Mathf.Max(1, gridMeasures * stepsPerMeasure);

        return gridEvents
            .Where(e => e != null)
            .Select(e =>
            {
                var copy = new ChordProgressionData.ChordEvent();
                copy.startStep = Mathf.Clamp(e.startStep, 0, totalSteps - 1);
                copy.lengthSteps = Mathf.Clamp(e.lengthSteps, 1, totalSteps - copy.startStep);
                copy.degree = e.degree;
                copy.quality = e.quality;
                copy.velocity = Mathf.Clamp(e.velocity, 0, 127);
                return copy;
            })
            .OrderBy(e => e.startStep)
            .ToList();
    }

    private static readonly string[] DegreeToRoman =
    {
        "I", "II", "III", "IV", "V", "VI", "VII"
    };

    internal bool IsSeventhQuality(ChordQuality q)
    {
        switch (q)
        {
            case ChordQuality.Major7:
            case ChordQuality.Minor7:
            case ChordQuality.Dominant7:
            case ChordQuality.HalfDiminished7:
            case ChordQuality.Diminished7:
            case ChordQuality.Dominant7sus4:   // v2 Tier A (ya estaba)
            case ChordQuality.Dominant9:       // v2 Tier B  ← FALTABA
            case ChordQuality.Major9:          // v2 Tier B  ← FALTABA
            case ChordQuality.Minor9:          // v2 Tier B  ← FALTABA
                return true;
            default:
                return false;
        }
    }

    internal string QualitySuffixForToken(ChordQuality q)
    {
        // IMPORTANT: use only strings your TryParseQualitySuffix already supports.
        switch (q)
        {
            // Triads
            case ChordQuality.Major: return "";
            case ChordQuality.Minor: return "m";
            case ChordQuality.Diminished: return "dim";
            case ChordQuality.Augmented: return "aug";
            case ChordQuality.Sus2: return "sus2";
            case ChordQuality.Sus4: return "sus4";

            // Sevenths
            case ChordQuality.Dominant7: return "7";
            case ChordQuality.Major7: return "maj7";
            case ChordQuality.Minor7: return "m7";
            case ChordQuality.HalfDiminished7: return "ø7";
            case ChordQuality.Diminished7: return "dim7";

            // Sixths (v2 Tier A — ya estaba)
            case ChordQuality.Major6: return "6";
            case ChordQuality.Minor6: return "m6";

            // Suspended dominant (v2 Tier A — ya estaba)
            case ChordQuality.Dominant7sus4: return "7sus4";

            // Ninths (v2 Tier B)  ← FALTABAN
            case ChordQuality.Dominant9: return "9";
            case ChordQuality.Major9: return "maj9";
            case ChordQuality.Minor9: return "m9";

            default: return "";
        }
    }

    private string BuildRomanTokenFromEvent(ChordProgressionData.ChordEvent e)
    {
        // Degree → base roman
        int idx = Mathf.Clamp((int)e.degree, 0, DegreeToRoman.Length - 1);
        string roman = DegreeToRoman[idx];

        // Case for auto-diatonic NONE: major vs minor families
        if (autoDiatonicMode == AutoDiatonicMode.None)
        {
            var family = ChordQualityResolver.GetTriadFamily(e.quality);

            if (family == TriadFamily.Minor || family == TriadFamily.Diminished)
                roman = roman.ToLowerInvariant();
            else
                roman = roman.ToUpperInvariant();
        }
        else
        {
            roman = roman.ToUpperInvariant();
        }

        string suffix = QualitySuffixForToken(e.quality);

        // prefix with b / # if this event has an accidental
        string prefix = e.degreeAccidental < 0 ? "b"
                       : e.degreeAccidental > 0 ? "#"
                       : string.Empty;

        return prefix + roman + suffix;
    }

    private string BuildRomanStringFromGrid(
        IReadOnlyList<ChordProgressionData.ChordEvent> sortedEvents)
    {
        if (sortedEvents == null || sortedEvents.Count == 0)
            return string.Empty;

        int beatsPerMeasure = Mathf.Max(1, gridBeatsPerMeasure);
        int stepsPerMeasure = beatsPerMeasure * Mathf.Max(1, gridSubdivisions);
        int totalSteps = Mathf.Max(1, gridMeasures * stepsPerMeasure);

        var tokens = new List<string>();
        int cursor = 0; // current step position in the grid

        foreach (var e in sortedEvents)
        {
            // 1) Leading gap before this event → rest token
            if (e.startStep > cursor)
            {
                int restSteps = e.startStep - cursor;
                float restMeasures = restSteps / (float)stepsPerMeasure;
                string restDurStr = restMeasures.ToString("0.##", CultureInfo.InvariantCulture);
                // Use "S" as the explicit rest marker
                tokens.Add($"S ({restDurStr})");
                cursor += restSteps;
            }

            // 2) Chord event itself
            float durMeasures = e.lengthSteps / (float)stepsPerMeasure;
            string durStr = durMeasures.ToString("0.##", CultureInfo.InvariantCulture);

            string roman = BuildRomanTokenFromEvent(e);
            tokens.Add($"{roman} ({durStr})");

            cursor += e.lengthSteps;
        }

        // 3) Trailing gap after the last event → rest token
        if (cursor < totalSteps)
        {
            int restSteps = totalSteps - cursor;
            float restMeasures = restSteps / (float)stepsPerMeasure;
            string restDurStr = restMeasures.ToString("0.##", CultureInfo.InvariantCulture);
            tokens.Add($"S ({restDurStr})");
        }

        return string.Join(" – ", tokens);
    }


    // Maps the editor-facing AutoDiatonicMode to the runtime AutoChordQualityMode.
    private AutoChordQualityMode GetAutoChordQualityMode()
    {
        switch (autoDiatonicMode)
        {
            case AutoDiatonicMode.Triads:
                return AutoChordQualityMode.DiatonicTriads;

            case AutoDiatonicMode.Sevenths:
                return AutoChordQualityMode.DiatonicSevenths;

            case AutoDiatonicMode.None:
            default:
                return AutoChordQualityMode.None;
        }
    }
}
#endif

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
public class ChordProgressionEditorWindow : EditorWindow
{
    [MenuItem("MidiGenPlay/Chord Progression Editor...")]
    public static void Open()
    {
        GetWindow<ChordProgressionEditorWindow>("Chord Progression Editor");
    }

    [SerializeField] private ChordProgressionData targetAsset;
    [SerializeField] private ChordProgressionLibrarySO targetLibrary;
    [SerializeField] private int selectedLibraryIndex = -1;
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

    private bool showAllowedTonalities = true; // foldout state
    private GUIStyle gridPreviewStyle;
    private GUIStyle chordBlockLabelStyle;

    private ChordProgressionData lastLoadedAsset;

    // Scroll position for the grid preview area so long progressions don't
    // push the buttons over the text.
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

        targetLibrary = (ChordProgressionLibrarySO)EditorGUILayout.ObjectField(
            new GUIContent("Progression Library (optional)",
                "Used for 'Clone From Library' and 'Add Current To Library'."),
            targetLibrary, typeof(ChordProgressionLibrarySO), false);

        if (targetLibrary != null &&
            targetLibrary.entries != null &&
            targetLibrary.entries.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clone From Library", EditorStyles.boldLabel);

            var entries = targetLibrary.entries;
            var labels = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string label = !string.IsNullOrWhiteSpace(e.id)
                    ? e.id
                    : (e.progression != null
                        ? (!string.IsNullOrWhiteSpace(e.progression.DisplayName)
                            ? e.progression.DisplayName
                            : e.progression.name)
                        : $"Entry {i}");

                labels[i] = label;
            }

            selectedLibraryIndex = Mathf.Clamp(selectedLibraryIndex, -1, entries.Count - 1);
            selectedLibraryIndex = EditorGUILayout.Popup(
                new GUIContent("Library Entry",
                    "Select an existing progression template to copy into the editor string."),
                selectedLibraryIndex,
                labels);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = selectedLibraryIndex >= 0 &&
                              selectedLibraryIndex < entries.Count;

                if (GUILayout.Button("Load Selected Into Editor"))
                {
                    CloneFromLibraryEntry(entries[selectedLibraryIndex]);
                }

                GUI.enabled = true;
            }
        }

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
                ParseAndPreview(onlyPreview: true);
            }

            GUI.enabled = targetAsset != null;
            if (GUILayout.Button("Save"))
            {
                ParseAndPreview(onlyPreview: false);
            }
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save As New Asset..."))
            {
                SaveAsNewAsset();
            }

            GUI.enabled = targetLibrary != null && targetAsset != null;
            if (GUILayout.Button("Add Current To Library"))
            {
                AddCurrentToLibrary();
            }
            GUI.enabled = true;
        }

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

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Grid mode:\n" +
            "- Measures / Beats / Subdivisions define the horizontal grid.\n" +
            "- Currently read-only: showing ChordEvents as colored blocks.\n" +
            "- Next step: clicking to create / edit events.",
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
            Handles.color = new Color(1f, 1f, 1f, 0.15f);

            for (int bar = 0; bar <= gridMeasures; bar++)
            {
                float x = gridRect.xMin + bar * stepsPerMeasure * stepWidth;
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
            foreach (var e in gridEvents)
            {
                int start = Mathf.Clamp(e.startStep, 0, totalSteps - 1);
                int length = Mathf.Clamp(e.lengthSteps, 1, totalSteps - start);

                float x = gridRect.xMin + start * stepWidth;
                float w = length * stepWidth;

                int degIndex = Mathf.Clamp((int)e.degree, 0, 6);
                Color col = degreeColors[degIndex];

                // Borrowed chords: darken the color a bit
                if (!e.isDiatonic)
                    col = Color.Lerp(col, Color.black, 0.35f);

                var blockRect = new Rect(x, gridRect.yMin, w, gridRect.height);
                EditorGUI.DrawRect(blockRect, col);

                string rn = ToRomanRich(e.degree, e.quality);
                // Borrowed chords: italic roman numeral
                if (!e.isDiatonic)
                    rn = "<i>" + rn + "</i>";

                GUI.Label(blockRect, rn, chordBlockLabelStyle);
            }
        }

        // (Next step will go here: mouse handling inside gridRect to edit/create events.)
    }


    // --- Data structures for parsing ---

    private struct ParsedChord
    {
        public ScaleDegree degree;
        public ChordQuality? explicitQuality; // null = let system infer
        public float durationMeasures; // in measures
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

        // Try to parse the Roman string into ParsedChord entries
        if (!TryParseProgression(
                progressionInput,
                defaultDurationMeasures,
                out var chords,
                out var parseError))
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
            // - adds the asset to the library if configured.
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

        if (!TryParseProgression(
            progressionInput, 
            defaultDurationMeasures,     
            out var chords, 
            out var parseError))
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
        if (!ComputeStepsAndSubdivisions(
            chords, 
            beatsPerMeasure,
            out int subdivisions,
            out List<int> lengthsSteps, 
            out int totalSteps, 
            out var durError))
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

        for (int i = 0; i < chords.Count; i++)
        {
            var pc = chords[i];
            int chordSteps = Mathf.Max(1, lengthsSteps[i]);

            var quality = ResolveChordQuality(pc);
            bool isDiatonic = IsChordDiatonic(pc.degree, quality);

            var evt = new ChordProgressionData.ChordEvent
            {
                degree = pc.degree,
                quality = quality,
                startStep = currentStep,
                lengthSteps = chordSteps,
                velocity = defaultVelocity,
                isDiatonic = isDiatonic
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

    // --- Parsing: "i (2) – iv (1) – v (1)" ---> List<ParsedChord> ---

    private bool TryParseProgression(
        string input,
        float defaultMeasuresPerChord,
        out List<ParsedChord> chords,
        out string error)
    {
        chords = new List<ParsedChord>();
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input string is empty.";
            return false;
        }

        input = input.Replace('\n', ' ');

        string[] tokens = input
            .Split(new[] { '–', '-', '—' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in tokens)
        {
            var token = raw.Trim();
            if (string.IsNullOrEmpty(token)) continue;

            // pattern: "I", "I (2)", "V7", "iiø7 (0.5)", etc.
            string romanPart = token;
            string durPart = null;

            int paren = token.IndexOf('(');
            if (paren >= 0)
            {
                romanPart = token.Substring(0, paren).Trim();
                durPart = token.Substring(paren).Trim(); // "(2)" or "(0.5)"
            }

            if (!TryParseRomanWithQuality(romanPart, out var degree,
                                          out var explicitQ, out var degErr))
            {
                error = degErr;
                return false;
            }

            if (!TryParseDuration(durPart, defaultMeasuresPerChord,
                                  out float dur, out var durErr))
            {
                error = durErr;
                return false;
            }

            chords.Add(new ParsedChord
            {
                degree = degree,
                explicitQuality = explicitQ,
                durationMeasures = dur
            });
        }

        if (chords.Count == 0)
        {
            error = "No valid chords found in the input.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryParseRomanWithQuality(
    string token,
    out ScaleDegree degree,
    out ChordQuality? explicitQuality,
    out string error)
    {
        degree = ScaleDegree.Tonic;
        explicitQuality = null;
        error = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Empty chord token.";
            return false;
        }

        token = token.Trim();

        // Split: roman core (I/V/X letters) + whatever remains as quality suffix
        int idx = 0;
        while (idx < token.Length && "IVXivx".IndexOf(token[idx]) >= 0)
            idx++;

        if (idx == 0)
        {
            error = $"Could not find a roman numeral in '{token}'.";
            return false;
        }

        string roman = token.Substring(0, idx);
        string suffix = token.Substring(idx); // may be empty, "7", "maj7", "ø7", etc.

        // --- Roman → degree index (0..6) ---
        if (!TryParseRomanToDegreeIndex(roman, out int degIndex))
        {
            error = $"Unsupported roman numeral '{roman}' in token '{token}'.";
            return false;
        }
        degree = (ScaleDegree)degIndex;

        // --- Quality from suffix (optional, highest priority) ---
        bool hasExplicitFromSuffix = false;
        suffix = suffix.Trim();
        if (!string.IsNullOrEmpty(suffix) && TryParseQualitySuffix(suffix, out var q))
        {
            explicitQuality = q;
            hasExplicitFromSuffix = true;
        }

        // --- In None mode, if there was no suffix, use case as explicit triad quality ---
        if (!hasExplicitFromSuffix && autoDiatonicMode == AutoDiatonicMode.None)
        {
            char c0 = roman[0];
            if (char.IsLetter(c0))
            {
                // all-lowercase → minor, all-uppercase → major
                bool anyLower = roman.Any(ch => char.IsLetter(ch) && char.IsLower(ch));
                bool anyUpper = roman.Any(ch => char.IsLetter(ch) && char.IsUpper(ch));

                if (anyLower && !anyUpper)
                    explicitQuality = ChordQuality.Minor;
                else if (anyUpper && !anyLower)
                    explicitQuality = ChordQuality.Major;
                // mixed case or weird → leave null, will fall back later
            }
        }

        return true;
    }


    /// <summary>
    /// Parses classic roman numerals I..VII to a degree index (0..6).
    /// Case is ignored.
    /// </summary>
    private bool TryParseRomanToDegreeIndex(string roman, out int index)
    {
        index = 0;
        roman = roman.Trim().ToUpperInvariant();

        switch (roman)
        {
            case "I": index = 0; return true;
            case "II": index = 1; return true;
            case "III": index = 2; return true;
            case "IV": index = 3; return true;
            case "V": index = 4; return true;
            case "VI": index = 5; return true;
            case "VII": index = 6; return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Maps common chord notation suffixes (7, maj7, M7, m7, dim, °, ø7, sus2, sus4, etc.)
    /// to the internal ChordQuality enum.
    /// </summary>
    private bool TryParseQualitySuffix(string suffix, out ChordQuality quality)
    {
        // Default – will be ignored if we return false
        quality = ChordQuality.Major;

        if (string.IsNullOrWhiteSpace(suffix))
            return false;

        // Normalize: remove spaces, to lower, replace some unicode aliases
        string s = suffix.Replace(" ", "")
                         .Replace("Δ", "maj")
                         .Replace("∆", "maj")
                         .ToLowerInvariant();

        // Some people write just "M" / "maj" / "m" etc.
        switch (s)
        {
            // --- Triads ---
            case "":       // shouldn't reach here
            case "maj":
            case "ma":
            case "mjr":
            case "mja":
            case "m":   // NOTE: only when written after roman like "IM"
            case "M":
                quality = ChordQuality.Major;
                return true;

            case "min":
            case "mi":
            case "mn":
            case "-":
            case "min3":
            case "mtri":
            case "mtriad":
                quality = ChordQuality.Minor;
                return true;

            case "dim":
            case "o":
            case "°":
                quality = ChordQuality.Diminished;
                return true;

            case "aug":
            case "+":
            case "+5":
                quality = ChordQuality.Augmented;
                return true;

            // --- Sevenths ---
            case "7":
            case "dom":
            case "dom7":
                quality = ChordQuality.Dominant7;
                return true;

            case "maj7":
            case "ma7":
            case "m7+": // sometimes seen in lead sheets
            case "mM7":
            case "mmaj7":
            case "M7":
                quality = ChordQuality.Major7;
                return true;

            case "m7":
            case "-7":
            case "min7":
                quality = ChordQuality.Minor7;
                return true;

            case "ø":
            case "ø7":
            case "m7b5":
            case "min7b5":
                quality = ChordQuality.HalfDiminished7;
                return true;

            case "dim7":
            case "o7":
            case "°7":
                quality = ChordQuality.Diminished7;
                return true;

            // --- Suspended / other ---
            case "sus2":
                quality = ChordQuality.Sus2;
                return true;

            case "sus4":
            case "sus":
                quality = ChordQuality.Sus4;
                return true;

            default:
                // Unknown: let caller fall back to diatonic inference.
                Debug.LogWarning($"[ChordProgressionEditor] " +
                    $"Unrecognized chord quality suffix '{suffix}'. " +
                    $"Falling back to diatonic / default quality.");

                return false;
        }
    }

    // --- Duration → subdivisions / steps ---

    /// <summary>
    /// Parses a duration token into measures.
    /// - rawDur can be null/empty (use default)
    /// - or something like "(2)", "(0.5)" or just "2"
    /// Durations are in *measures*.
    /// </summary>
    private bool TryParseDuration(
        string rawDur,
        float defaultMeasuresPerChord,
        out float duration,
        out string error)
    {
        error = null;

        // Sensible default if somebody configured 0 or negative
        if (defaultMeasuresPerChord <= 0f)
            defaultMeasuresPerChord = 1f;

        // No explicit duration → use default
        if (string.IsNullOrWhiteSpace(rawDur))
        {
            duration = defaultMeasuresPerChord;
            return true;
        }

        // Clean up string
        string s = rawDur.Trim();

        // Accept "(2)" or "(0.5)" as well as plain "2"
        if (s.StartsWith("(") && s.EndsWith(")"))
        {
            if (s.Length <= 2)
            {
                error = $"Malformed duration '{rawDur}'. " +
                    $"Expected something like '(2)' or '(0.5)'.";
                duration = 0f;
                return false;
            }

            s = s.Substring(1, s.Length - 2).Trim();
        }
        else if (s.StartsWith("(") || s.EndsWith(")"))
        {
            // One parenthesis but not the other → clearly a typo
            error = $"Malformed duration '{rawDur}'. Expected '(number)'.";
            duration = 0f;
            return false;
        }

        // Use invariant culture so 0.5 always works
        if (!float.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out duration))
        {
            error = $"Could not parse duration '{rawDur}'. " +
                $"Use a number like '(2)' or '(0.5)' " +
                "with a dot as decimal separator.";
            duration = 0f;
            return false;
        }

        if (duration <= 0f)
        {
            error = $"Duration must be > 0 in '{rawDur}'.";
            duration = 0f;
            return false;
        }

        return true;
    }

    /// <summary>
    /// For each chord (durationMeasures), finds a suitable subdivisions value (1..8)
    /// and computes integer step lengths so that:
    ///  lengthSteps = durationMeasures * beatsPerMeasure * subdivisions
    /// And the total adds up to a whole number of measures.
    /// </summary>
    private bool ComputeStepsAndSubdivisions(
        List<ParsedChord> chords,
        int beatsPerMeasure,
        out int subdivisions,
        out List<int> lengthsSteps,
        out int totalSteps,
        out string error)
    {
        subdivisions = 1;
        lengthsSteps = new List<int>();
        totalSteps = 0;
        error = null;

        if (chords == null || chords.Count == 0)
        {
            error = "No chords to compute durations for.";
            return false;
        }

        // Try subdivisions from 1 to 8 (ChordProgressionData clamps 1..8).
        for (int sub = 1; sub <= 8; sub++)
        {
            bool ok = true;
            lengthsSteps.Clear();
            totalSteps = 0;

            foreach (var c in chords)
            {
                // duration (measures) → steps
                float stepsF = c.durationMeasures * beatsPerMeasure * sub;
                int stepsInt = Mathf.RoundToInt(stepsF);

                // require that it's "close enough" to an integer
                if (Mathf.Abs(stepsF - stepsInt) > 0.001f || stepsInt <= 0)
                {
                    ok = false;
                    break;
                }

                lengthsSteps.Add(stepsInt);
                totalSteps += stepsInt;
            }

            if (!ok)
                continue;

            // Check totalSteps corresponds to a whole number of measures
            int stepsPerMeasure = beatsPerMeasure * sub;
            if (totalSteps % stepsPerMeasure != 0)
                continue;

            subdivisions = sub;
            return true;
        }

        error =
            "Could not find a valid 'subdivisions' value (1..8) for these durations.\n" +
            "Make sure the sum of durations is an integer number of measures " +
            "and each duration is a rational multiple of a beat.";
        return false;
    }

    private ChordQuality ResolveChordQuality(ParsedChord c)
    {
        // 1) Explicit quality (suffix or, in None mode, case) always wins.
        if (c.explicitQuality.HasValue)
            return c.explicitQuality.Value;

        // 2) Otherwise, infer from selected auto mode.
        switch (autoDiatonicMode)
        {
            case AutoDiatonicMode.Triads:
                // Diatonic triad for this mode+degree
                return GetDiatonicTriadQuality(referenceTonality, c.degree);

            case AutoDiatonicMode.Sevenths:
                // Diatonic 7th chord (Imaj7, iim7, V7, etc.)
                return GetDiatonicSeventhQuality(referenceTonality, c.degree);

            case AutoDiatonicMode.None:
            default:
                // Literal mode: no inference → default to plain major if nothing else is specified.
                return ChordQuality.Major;
        }
    }


    // Used only for analysis (diatonic vs borrowed)
    private enum TriadFamily { Major, Minor, Diminished, Augmented, Suspended, Other }

    private TriadFamily GetTriadFamily(ChordQuality q)
    {
        switch (q)
        {
            case ChordQuality.Major:
            case ChordQuality.Major7:
            case ChordQuality.Dominant7:
                return TriadFamily.Major;

            case ChordQuality.Minor:
            case ChordQuality.Minor7:
                return TriadFamily.Minor;

            case ChordQuality.Diminished:
            case ChordQuality.Diminished7:
            case ChordQuality.HalfDiminished7:
                return TriadFamily.Diminished;

            case ChordQuality.Augmented:
                return TriadFamily.Augmented;

            case ChordQuality.Sus2:
            case ChordQuality.Sus4:
                return TriadFamily.Suspended;

            default:
                return TriadFamily.Other;
        }
    }

    /// <summary>
    /// Returns true if 'quality' belongs to the same triad family as the
    /// diatonic triad for (referenceTonality, degree). This is our notion of
    /// "non-borrowed" versus "borrowed / modal mixture".
    /// </summary>
    private bool IsChordDiatonic(ScaleDegree degree, ChordQuality quality)
    {
        var expectedTriad = GetDiatonicTriadQuality(referenceTonality, degree);
        var expectedFamily = GetTriadFamily(expectedTriad);
        var actualFamily = GetTriadFamily(quality);
        return expectedFamily == actualFamily;
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
        if (!ComputeStepsAndSubdivisions(
            chords,
            beatsPerMeasure,
            out int subdivisions,
            out List<int> lengthsSteps,
            out int totalSteps,
            out var durError))
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

        var chordSymbols = new List<string>(chords.Count);
        var chordColors = new List<string>(chords.Count);
        var chordIsDiatonic = new List<bool>(chords.Count);
        var linearParts = new List<string>(chords.Count);

        for (int i = 0; i < chords.Count; i++)
        {
            var pc = chords[i];
            int degIndex = Mathf.Clamp((int)pc.degree, 0, 6);
            var degreeRoot = scaleNotes[degIndex];

            var q = ResolveChordQuality(pc);
            bool isDiatonic = IsChordDiatonic(pc.degree, q);

            string symbol = GetChordSymbolSpelledForDegree(
                previewRoot, degIndex, degreeRoot, q);

            chordSymbols.Add(symbol);
            chordColors.Add(ColorHexForNote(degreeRoot));
            chordIsDiatonic.Add(isDiatonic);

            string label = symbol;
            if (!isDiatonic)
                label = "*" + label; // mark borrowed chord in linear preview

            if (Mathf.Abs(pc.durationMeasures - 1f) > 0.0001f)
                label += $" ({pc.durationMeasures:g})";

            linearParts.Add(label);
        }

        // Simple linear preview
        previewChordNames = string.Join(" | ", linearParts);

        // --- Build per-beat grid ---

        int totalBeats = measures * beatsPerMeasure;
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

                // New chord at this beat?
                bool isNewChord = (bar == 0 && beat == 0);
                if (!isNewChord)
                {
                    int prevStepIndex = stepIndex - subdivisions;
                    if (prevStepIndex < 0)
                        prevStepIndex = 0;

                    int prevChordIndex = chordByStep[prevStepIndex];
                    isNewChord = chordIndex != prevChordIndex;
                }

                string cellText = "-";
                if (isNewChord)
                {
                    string sym = chordSymbols[chordIndex];
                    string col = chordColors[chordIndex];
                    string colored = $"<color=#{col}>{sym}</color>";

                    if (!chordIsDiatonic[chordIndex])
                        colored = $"<i>{colored}</i>"; // borrowed: italic

                    cellText = colored;
                }

                sb.Append(" ").Append(cellText).Append(" ");
            }
            sb.Append("|").AppendLine();
        }

        previewGridText = sb.ToString();
    }

    /// <summary>
    /// Loads an existing library entry into the editor:
    /// - Copies its original Roman string (if available) into progressionInput
    /// - Syncs time signature and allowed tonalities
    /// </summary>
    private void CloneFromLibraryEntry(ChordProgressionLibrarySO.Entry entry)
    {
        if (entry == null || entry.progression == null)
            return;

        var prog = entry.progression;

        // Prefer the original Roman string; fallback to DisplayName / id.
        if (!string.IsNullOrWhiteSpace(prog.originalInput))
            progressionInput = prog.originalInput;
        else if (!string.IsNullOrWhiteSpace(prog.DisplayName))
            progressionInput = prog.DisplayName;
        else if (!string.IsNullOrWhiteSpace(entry.id))
            progressionInput = entry.id;
        else
            progressionInput = prog.name;

        // Sync meter
        timeSignature = prog.TimeSignature;

        // Sync allowed tonalities into the toggles
        if (tonalityFlags == null)
            OnEnable();

        var progTonalities = prog.tonalities ?? new List<Tonality>();
        foreach (var key in tonalityFlags.Keys.ToList())
        {
            tonalityFlags[key] = progTonalities.Contains(key);
        }

        // Keep previewRoot & referenceTonality as-is for now.
        Repaint();
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
        SyncGridFromAsset(force: true);

        // Refresh preview if we have a Roman string
        if (!string.IsNullOrWhiteSpace(progressionInput))
            ParseAndPreview(onlyPreview: true);

        Repaint();
    }

    /// <summary>
    /// Creates a brand new ChordProgressionData asset from the current editor state.
    /// Does NOT add it to any library (use AddCurrentToLibrary for that).
    /// </summary>
    private void SaveAsNewAsset()
    {
        if (string.IsNullOrWhiteSpace(progressionInput))
        {
            EditorUtility.DisplayDialog("Error",
                "Progression input string is empty.", "OK");
            return;
        }

        if (!TryParseProgression(
            progressionInput,
            defaultDurationMeasures,
            out var chords,
            out var parseError))
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

        if (!ComputeStepsAndSubdivisions(
            chords,
            beatsPerMeasure,
            out int subdivisions,
            out List<int> lengthsSteps,
            out int totalSteps,
            out var durError))
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

        // Events
        newAsset.events.Clear();
        int currentStep = 0;
        for (int i = 0; i < chords.Count; i++)
        {
            var pc = chords[i];
            int chordSteps = Mathf.Max(1, lengthsSteps[i]);

            var quality = ResolveChordQuality(pc);
            bool isDiatonic = IsChordDiatonic(pc.degree, quality);

            var evt = new ChordProgressionData.ChordEvent
            {
                degree = pc.degree,
                quality = quality,
                startStep = currentStep,
                lengthSteps = chordSteps,
                velocity = defaultVelocity,
                isDiatonic = isDiatonic
            };

            newAsset.events.Add(evt);
            currentStep += chordSteps;
        }

        newAsset.UpdateDisplayNameAuto();

        // Keep grid view in sync with the newly created asset
        targetAsset = newAsset;
        SyncGridFromAsset();

        EditorUtility.SetDirty(newAsset);
        AssetDatabase.SaveAssets();

        // Make this the current target and sync UI/preview
        targetAsset = newAsset;
        OnTargetAssetChanged();
    }

    /// <summary>
    /// Adds the current targetAsset to targetLibrary if not already present
    /// with the same originalInput string (ignoring case/whitespace).
    /// </summary>
    private void AddCurrentToLibrary()
    {
        if (targetLibrary == null)
        {
            EditorUtility.DisplayDialog("No Library Assigned",
                "Assign a ChordProgressionLibrarySO in the 'Progression Library' field first.",
                "OK");
            return;
        }

        if (targetAsset == null)
        {
            EditorUtility.DisplayDialog("No Target Asset",
                "There is no ChordProgressionData to add. " +
                "Apply or Save As first.",
                "OK");
            return;
        }

        if (targetLibrary.entries == null)
            targetLibrary.entries = new List<ChordProgressionLibrarySO.Entry>();

        // Key string: prefer the asset's originalInput; fallback to current editor string.
        string keyString = !string.IsNullOrWhiteSpace(targetAsset.originalInput)
            ? targetAsset.originalInput.Trim()
            : (progressionInput ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(keyString))
        {
            EditorUtility.DisplayDialog("Missing Original String",
                "The progression has no original input string to use as a uniqueness key.\n" +
                "Set 'originalInput' or the editor string first.",
                "OK");
            return;
        }

        // Duplicate if ANY existing entry’s progression has the same originalInput
        bool duplicate = targetLibrary.entries.Any(e =>
            e != null &&
            e.progression != null &&
            !string.IsNullOrWhiteSpace(e.progression.originalInput) &&
            string.Equals(
                e.progression.originalInput.Trim(),
                keyString,
                StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            EditorUtility.DisplayDialog("Already In Library",
                "Another entry in this library already has the same original input string.\n" +
                "If you really want a variation, change the string slightly and try again.",
                "OK");
            return;
        }

        var entry = new ChordProgressionLibrarySO.Entry
        {
            id = string.IsNullOrWhiteSpace(targetAsset.DisplayName)
            ? targetAsset.name
            : targetAsset.DisplayName,
            progression = targetAsset,
            weight = 1f,
            compatibleTonalities =
            (targetAsset.tonalities == null || targetAsset.tonalities.Count == 0)
                ? new List<Tonality>()
                : new List<Tonality>(targetAsset.tonalities)
        };

        targetLibrary.entries.Add(entry);

        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Added To Library",
            $"Added '{entry.id}' to '{targetLibrary.name}'.",
            "OK");
    }

    // Keep grid parameters + editable event cache in sync with the target asset.
    private void SyncGridFromAsset(bool force = false)
    {
        if (targetAsset == null)
            return;

        // Init list if needed
        if (gridEvents == null)
            gridEvents = new List<ChordProgressionData.ChordEvent>();

        // When 'force' is true we always pull fresh values from the asset.
        // Otherwise we only initialise invalid/empty values so the user can
        // tweak the numbers in the inspector without them being overwritten.
        if (force || gridMeasures <= 0)
            gridMeasures = Mathf.Max(1, targetAsset.Measures);

        var tsInfo = TimeSignatureProperties[targetAsset.TimeSignature];

        if (gridBeatsPerMeasure <= 0)
            gridBeatsPerMeasure = tsInfo.BeatsPerMeasure;

        if (gridSubdivisions <= 0)
            gridSubdivisions = Mathf.Max(1, targetAsset.subdivisions);

        // Copy events from asset into the local editable cache
        gridEvents.Clear();
        if (targetAsset.events != null)
            gridEvents.AddRange(targetAsset.events);
    }
}
#endif

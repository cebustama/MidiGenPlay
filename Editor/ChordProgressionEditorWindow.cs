#if UNITY_EDITOR
using Melanchall.DryWetMidi.Interaction;
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

    [SerializeField][TextArea(2, 4)] private string progressionInput = "I – V – vi – IV";

    [SerializeField] private float defaultDurationMeasures = 1f;

    [SerializeField][Range(1, 127)] private int defaultVelocity = 96;

    [SerializeField] private TimeSignature timeSignature = TimeSignature.FourFour;

    [SerializeField] private Tonality referenceTonality = Tonality.Ionian;

    [SerializeField] private bool autoDiatonicTriads = true;

    // TODO: Maybe make only Ionian true by default?
    private Dictionary<Tonality, bool> tonalityFlags; // Tonality toggles (all true by default)

    [SerializeField] private NoteName previewRoot = NoteName.C; // only for preview
    [SerializeField] private string previewChordNames = "";

    private bool showAllowedTonalities = true; // foldout state

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
        EditorGUILayout.LabelField(
            "Chord Progression From Roman Numerals", EditorStyles.boldLabel);

        targetAsset = (ChordProgressionData)EditorGUILayout.ObjectField(
            new GUIContent("Target Asset",
                "Existing ChordProgressionData to overwrite, " +
                "or leave empty to create a new one."),
            targetAsset, typeof(ChordProgressionData), false);

        targetLibrary = (ChordProgressionLibrarySO)EditorGUILayout.ObjectField(
            new GUIContent("Progression Library (optional)",
                "If assigned, the created/updated asset will " +
                "automatically be added as an entry."),
            targetLibrary, typeof(ChordProgressionLibrarySO), false);

        EditorGUILayout.Space();

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

        timeSignature = (TimeSignature)EditorGUILayout.EnumPopup(
            new GUIContent("Time Signature",
                "Meter used to quantize durations and compute total measures."),
            timeSignature);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tonality & Qualities", EditorStyles.boldLabel);

        referenceTonality = (Tonality)EditorGUILayout.EnumPopup(
            new GUIContent("Reference Tonality",
                "Used to derive diatonic triad quality (Major/Minor/Dim) for each degree."),
            referenceTonality);

        autoDiatonicTriads = EditorGUILayout.Toggle(
            new GUIContent("Auto Diatonic Triads",
                "If true, chord quality = diatonic triad for (referenceTonality, degree)."),
            autoDiatonicTriads);

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
        EditorGUILayout.LabelField(
            "Preview (Roman → concrete chords)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            string.IsNullOrEmpty(previewChordNames)
                ? "Press 'Parse & Preview' or 'Apply To Target Asset' to update the preview."
                : previewChordNames,
            MessageType.None);



        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Parse & Preview (no write)"))
            {
                if (!TryParseProgression(
                    progressionInput, 
                    defaultDurationMeasures, 
                    out var chords,
                    out var parseError))
                {
                    if (!string.IsNullOrEmpty(parseError))
                        EditorUtility.DisplayDialog("Parse Error", parseError, "OK");
                }
                else
                {
                    UpdatePreview(chords);
                }
            }

            GUI.enabled = targetAsset != null;
            if (GUILayout.Button("Apply To Target Asset"))
            {
                ApplyToAsset();
            }
            GUI.enabled = true;
        }
    }

    // --- Data structures for parsing ---

    private struct ParsedChord
    {
        public ScaleDegree degree;
        public ChordQuality? explicitQuality; // null = let system infer
        public float durationMeasures; // in measures
    }

    // --- Main application logic ---

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

            var evt = new ChordProgressionData.ChordEvent
            {
                degree = pc.degree,
                quality = ResolveChordQuality(pc),
                startStep = currentStep,
                lengthSteps = chordSteps,
                velocity = defaultVelocity
            };

            targetAsset.events.Add(evt);
            currentStep += chordSteps;
        }

        targetAsset.UpdateDisplayNameAuto();

        EditorUtility.SetDirty(targetAsset);
        AssetDatabase.SaveAssets();

        // --- Library integration (optional) ---
        if (targetLibrary != null)
        {
            // avoid duplicates (same progression asset)
            bool exists = targetLibrary.entries.Any(e => e.progression == targetAsset);
            if (!exists)
            {
                targetLibrary.entries.Add(new ChordProgressionLibrarySO.Entry
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
                });

                EditorUtility.SetDirty(targetLibrary);
                AssetDatabase.SaveAssets();
            }
        }

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
        string suffix = token.Substring(idx); // may be empty, "7", "maj7", "ø7", "sus4", etc.

        // --- Roman → degree index (0..6) ---
        if (!TryParseRomanToDegreeIndex(roman, out int degIndex))
        {
            error = $"Unsupported roman numeral '{roman}' in token '{token}'.";
            return false;
        }
        degree = (ScaleDegree)degIndex;

        // --- Quality suffix (optional) ---
        suffix = suffix.Trim();
        if (!string.IsNullOrEmpty(suffix) && TryParseQualitySuffix(suffix, out var q))
        {
            explicitQuality = q;
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
        // 1) Explicit quality in the string wins.
        if (c.explicitQuality.HasValue)
            return c.explicitQuality.Value;

        // 2) If user asked for diatonic qualities, use those.
        if (autoDiatonicTriads)
            return GetDiatonicTriadQuality(referenceTonality, c.degree);

        // 3) Fallback: plain major triad.
        return ChordQuality.Major;
    }

    /// <summary>
    /// Builds a human-readable line like "Cmaj7 | G7 | Am7 | Fmaj7 (2)" 
    /// using the current previewRoot and referenceTonality.
    /// </summary>
    private void UpdatePreview(List<ParsedChord> chords)
    {
        if (chords == null || chords.Count == 0)
        {
            previewChordNames = "";
            return;
        }

        // Get scale degrees → pitch classes for the chosen tonality/key
        var scale = GetScaleFromTonality(referenceTonality, previewRoot);
        var scaleNames = GetNotesFromScale(scale, previewRoot, 4, 7)
                            .Select(n => n.NoteName)
                            .ToArray();

        var parts = new List<string>(chords.Count);

        foreach (var pc in chords)
        {
            int degIndex = (int)pc.degree;
            degIndex = Mathf.Clamp(degIndex, 0, 6);

            var degreeRoot = scaleNames[degIndex];
            var q = ResolveChordQuality(pc);

            string symbol = GetChordSymbolSpelledForDegree(
                previewRoot, degIndex, degreeRoot, q);

            // Show duration only if different from 1 bar
            string label = symbol;
            if (Mathf.Abs(pc.durationMeasures - 1f) > 0.0001f)
                label += $" ({pc.durationMeasures:g})";

            parts.Add(label);
        }

        previewChordNames = string.Join(" | ", parts);
    }
}
#endif

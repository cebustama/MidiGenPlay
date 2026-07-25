#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

/// <summary>
/// Phase M3 (Roadmap_MIDI_Import.md) — "MIDI File Import" panel for the Chord
/// Progression Editor, as a partial of <see cref="ChordProgressionEditorWindow"/>
/// (same pattern as the LLM panel partial). Call <see cref="DrawMidiImportPanel"/>
/// from the window's OnGUI, after <c>DrawLLMPanel()</c> and before
/// <c>EndScrollView</c>.
///
/// The panel feeds <see cref="ChordMidiImporter"/> (pure function) and applies a
/// Full result to the GRID working state only (gridEvents + grid parameters),
/// switching the window to Grid mode so the result is visible and editable.
/// Nothing is written to the asset until Apply / Save As — the authoring
/// invariant all three MIDI panels share.
///
/// M3-D4=A: the window's Timing controls are the meter authority. The importer
/// receives the window's <c>timeSignature</c> and Grid <c>gridSubdivisions</c>;
/// on apply, <c>gridBeatsPerMeasure</c> (a free int in Grid mode) is ALIGNED to
/// the time signature's beats-per-measure rather than trusted blindly.
/// </summary>
public partial class ChordProgressionEditorWindow
{
    // -- MIDI import panel state --
    [SerializeField] private bool midiImportFoldout = true;
    [SerializeField] private NoteName midiImportRoot = NoteName.C;
    [SerializeField] private Tonality midiImportTonality = Tonality.Ionian;
    [SerializeField] private int midiImportChannel1Based = 0; // 0 = all channels

    /// <summary>IMPORT-QOL-1 item 5 — maps to
    /// <see cref="ChordMidiImporter.Options.preserveReStrikes"/>. OFF by
    /// default = the M3 behavior (identical chords merge across gaps).</summary>
    [SerializeField] private bool midiImportPreserveReStrikes = false;

    /// <summary>IMPORT-QOL-1 item 6 — file name of the last APPLIED import,
    /// consumed by the main window's grid-apply paths to stamp provenance into
    /// the asset's originalInput. Serialized so it survives a domain reload
    /// between import and Apply (the grid events do too). Cleared on target
    /// rebind and on a Roman-path apply.</summary>
    [SerializeField] private string midiImportProvenanceFile = "";

    // Transient (not serialized): last-run reporting.
    [NonSerialized] private readonly List<string> midiImportWarnings = new List<string>();
    [NonSerialized] private string midiImportSummary = "";

    private void DrawMidiImportPanel()
    {
        midiImportFoldout = EditorGUILayout.Foldout(
            midiImportFoldout, "MIDI File Import", true);
        if (!midiImportFoldout) return;

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField(
                "Imports a chord progression from a standard MIDI file. Chords are " +
                "detected by vertical pitch-class sets on the current Time Signature " +
                "and Grid subdivisions (the window's Timing controls are the meter " +
                "authority; the file's own meter events are ignored). The key below " +
                "is required — degrees and accidentals are relative to it. The result " +
                "fills GRID mode's working state; nothing is written to the asset " +
                "until Apply / Save As. Inversions and voicings are not preserved " +
                "(voicing is runtime's job).",
                EditorStyles.wordWrappedMiniLabel);

            midiImportRoot = (NoteName)EditorGUILayout.EnumPopup(
                new GUIContent("Key Root",
                    "Root of the key the file is in. Chord roots resolve to " +
                    "degree + accidental relative to this note."),
                midiImportRoot);

            midiImportTonality = (Tonality)EditorGUILayout.EnumPopup(
                new GUIContent("Key Tonality",
                    "Mode of the key. Drives degree spelling and the " +
                    "diatonic/borrowed flag per chord."),
                midiImportTonality);

            // Same field Grid mode's Timing controls edit — surfaced here because
            // it IS the import resolution: minimum chord duration = one step, and
            // chord changes faster than one step get smeared into one segment
            // (typically surfacing as ChordReduced warnings). Raise it if the
            // file changes chords faster than one change per grid beat.
            // IMPORT-QOL-1 item 1: "Suggest…" probes a .mid and, on an explicit
            // press only, sets the slider — the user can always re-fix it.
            using (new EditorGUILayout.HorizontalScope())
            {
                gridSubdivisions = EditorGUILayout.IntSlider(
                    new GUIContent("Grid Subdivisions",
                        "Steps per beat used for quantization and segmentation (the " +
                        "same value as Grid mode's Subdivisions). Minimum chord " +
                        "duration on import = one step. Use 2 for eighth-note chord " +
                        "changes, 4 for sixteenths."),
                    gridSubdivisions, 1, 8);
                if (GUILayout.Button(
                    new GUIContent("Suggest…",
                        "Pick a .mid file and measure, for each candidate grid " +
                        "(1,2,3,4,6,8), the worst onset/end residual in beats. " +
                        "Sets the slider to the SMALLEST grid that explains the " +
                        "file within " + ChordMidiImporter.SuggestMaxErrorBeats +
                        " beats and reports the full residual table; if none " +
                        "passes, only reports (slider unchanged)."),
                    GUILayout.Width(70f)))
                    OnSuggestChordMidiSubdivisions();
            }

            midiImportChannel1Based = EditorGUILayout.IntSlider(
                new GUIContent("MIDI Channel (0 = all)",
                    "1-based channel filter. 0 reads every channel; merging " +
                    "multiple channels emits a warning listing per-channel note " +
                    "counts. Filter out melody channels for cleaner detection."),
                midiImportChannel1Based, 0, 16);

            midiImportPreserveReStrikes = EditorGUILayout.Toggle(
                new GUIContent("Preserve Re-strikes",
                    "ON: two strikes of the SAME chord separated by a rest " +
                    "import as separate events, keeping the file's harmonic " +
                    "rhythm (the runtime reproduces rests faithfully). " +
                    "OFF (default, M3 behavior): identical chords merge across " +
                    "gaps into one sustained region. Adjacent identical chords " +
                    "with no gap always merge."),
                midiImportPreserveReStrikes);

            if (targetAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Target Asset first — the import fills Grid mode, " +
                    "which authors against an asset-bound working copy.",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targetAsset == null))
                {
                    if (GUILayout.Button(
                        new GUIContent("Import MIDI File…",
                            "Choose a .mid file to segment and match into chord events."),
                        GUILayout.Width(170f)))
                        OnImportChordMidiFile();
                }

                // Diagnostic: writes nothing, needs no target asset. Same
                // quantization + matching cascade as the import — run it on the
                // source .mid and on a rendered .mid with the same key/grid to
                // compare harmony, boundaries and voicings line by line.
                if (GUILayout.Button(
                    new GUIContent("Analyze File (log)…",
                        "Log a per-segment chord timeline (locations, durations, " +
                        "pitch sets, exact notes with octaves, bass, importer " +
                        "verdict) to the Console and copy it to the clipboard. " +
                        "Read-only; no import happens."),
                    GUILayout.Width(160f)))
                    OnAnalyzeChordMidiFile();
            }

            if (midiImportWarnings.Count > 0)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    $"MIDI import notes ({midiImportWarnings.Count})",
                    EditorStyles.boldLabel);
                for (int i = 0; i < midiImportWarnings.Count; i++)
                    EditorGUILayout.LabelField(midiImportWarnings[i], EditorStyles.miniLabel);
            }

            if (!string.IsNullOrEmpty(midiImportSummary))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(midiImportSummary, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private void OnImportChordMidiFile()
    {
        if (targetAsset == null) return;

        string path = EditorUtility.OpenFilePanel("Import MIDI File", "", "mid");
        if (string.IsNullOrEmpty(path)) return; // user cancelled — not a warning

        midiImportWarnings.Clear();
        midiImportSummary = "";

        MidiFile file;
        try
        {
            file = MidiFile.Read(path);
        }
        catch (Exception ex)
        {
            midiImportWarnings.Add(
                $"Could not read '{Path.GetFileName(path)}': {ex.GetType().Name}: {ex.Message}");
            Repaint();
            return;
        }

        var options = new ChordMidiImporter.Options
        {
            rootNote = midiImportRoot,
            tonality = midiImportTonality,
            timeSignature = timeSignature,                       // M3-D4=A: window authority
            subdivisions = Mathf.Clamp(gridSubdivisions, 1, 8),  // Grid-mode resolution
            measures = 0,                                        // derive from content
            channel = midiImportChannel1Based - 1,               // 0 (all) → -1
            preserveReStrikes = midiImportPreserveReStrikes,     // IMPORT-QOL-1 item 5
        };

        var result = ChordMidiImporter.Import(file, options);

        foreach (var w in result.warnings)
            midiImportWarnings.Add(w.ToString());

        if (result.mode != ChordMidiImporter.ImportMode.Full)
        {
            Repaint(); // Failed: nothing applied; existing grid preserved.
            return;
        }

        // IMPORT-QOL-1 item 6 — remember the source file; the grid-apply
        // paths stamp it into originalInput (WithMidiProvenance).
        midiImportProvenanceFile = Path.GetFileName(path);

        ApplyChordMidiImport(result);
        Repaint();
    }

    /// <summary>
    /// IMPORT-QOL-1 item 1 — probe the candidate subdivisions against a .mid
    /// and report the residual table. On a passing suggestion, sets the slider
    /// (the explicit button press is the user's consent — never automatic and
    /// never silent: the table is always reported and the slider stays
    /// editable). Read-only with respect to the grid working state.
    /// </summary>
    private void OnSuggestChordMidiSubdivisions()
    {
        string path = EditorUtility.OpenFilePanel(
            "Suggest Grid Subdivisions (from MIDI)", "", "mid");
        if (string.IsNullOrEmpty(path)) return; // user cancelled — not a warning

        MidiFile file;
        try
        {
            file = MidiFile.Read(path);
        }
        catch (Exception ex)
        {
            midiImportWarnings.Clear();
            midiImportSummary = "";
            midiImportWarnings.Add(
                $"Could not read '{Path.GetFileName(path)}': {ex.GetType().Name}: {ex.Message}");
            Repaint();
            return;
        }

        var options = new ChordMidiImporter.Options
        {
            rootNote = midiImportRoot,
            tonality = midiImportTonality,
            timeSignature = timeSignature,          // M3-D4=A: window authority
            subdivisions = Mathf.Clamp(gridSubdivisions, 1, 8), // ignored by Suggest
            measures = 0,
            channel = midiImportChannel1Based - 1,  // same filter as Import
        };

        var s = ChordMidiImporter.SuggestSubdivisions(file, options);
        midiImportWarnings.Clear();

        if (!s.hasNotes)
        {
            midiImportSummary =
                $"'{Path.GetFileName(path)}': no notes to measure " +
                "(check the channel filter / file format).";
            Repaint();
            return;
        }

        string table = string.Join("   ", s.candidates.Select(c =>
            $"{c.subdivisions}: {c.maxErrorBeats:0.####}{(c.withinThreshold ? " ✓" : "")}"));

        if (s.suggestedWithinThreshold)
        {
            gridSubdivisions = s.suggested;
            midiImportSummary =
                $"Suggested Grid Subdivisions = {s.suggested} — the smallest grid whose " +
                $"max residual ≤ {ChordMidiImporter.SuggestMaxErrorBeats:0.####} beats " +
                $"(slider set). Residuals (sub: beats): {table}";
        }
        else
        {
            midiImportSummary =
                $"No candidate grid (≤ 8) explains '{Path.GetFileName(path)}' within " +
                $"{ChordMidiImporter.SuggestMaxErrorBeats:0.####} beats; the best is " +
                $"{s.suggested}. Slider unchanged. Residuals (sub: beats): {table}";
        }
        Repaint();
    }

    /// <summary>
    /// Read-only diagnostic: logs a per-segment chord timeline of a .mid to the
    /// Console and copies it to the clipboard (single paste-ready block). Uses
    /// the same Options as the import button, but never touches window state
    /// beyond the report fields.
    /// </summary>
    private void OnAnalyzeChordMidiFile()
    {
        string path = EditorUtility.OpenFilePanel(
            "Analyze MIDI File (chord timeline)", "", "mid");
        if (string.IsNullOrEmpty(path)) return; // user cancelled — not a warning

        MidiFile file;
        try
        {
            file = MidiFile.Read(path);
        }
        catch (Exception ex)
        {
            midiImportWarnings.Clear();
            midiImportSummary = "";
            midiImportWarnings.Add(
                $"Could not read '{Path.GetFileName(path)}': {ex.GetType().Name}: {ex.Message}");
            Repaint();
            return;
        }

        var options = new ChordMidiImporter.Options
        {
            rootNote = midiImportRoot,
            tonality = midiImportTonality,
            timeSignature = timeSignature,
            subdivisions = Mathf.Clamp(gridSubdivisions, 1, 8),
            measures = 0,
            channel = midiImportChannel1Based - 1,
        };

        string timeline =
            $"[ChordTimeline] file='{Path.GetFileName(path)}'\n" +
            ChordMidiImporter.DescribeChordTimeline(file, options);

        Debug.Log(timeline);
        EditorGUIUtility.systemCopyBuffer = timeline;
        midiImportSummary =
            "Chord timeline logged to the Console and copied to the clipboard (paste-ready).";
        Repaint();
    }

    /// <summary>
    /// Apply a Full import result to the GRID working state (never the asset).
    /// Grid parameters sync to the result; gridBeatsPerMeasure aligns to the
    /// time signature (M3-D4=A); the import key becomes the window's reference
    /// tonality + preview root so symbols and diatonic flags read consistently.
    /// </summary>
    private void ApplyChordMidiImport(ChordMidiImporter.Result result)
    {
        var tsInfo = TimeSignatureProperties[result.timeSignature];

        gridMeasures = Mathf.Max(1, result.measures);
        gridBeatsPerMeasure = tsInfo.BeatsPerMeasure;
        gridSubdivisions = Mathf.Clamp(result.subdivisions, 1, 8);

        if (gridEvents == null)
            gridEvents = new List<ChordProgressionData.ChordEvent>();
        gridEvents.Clear();
        foreach (var e in result.events)
        {
            gridEvents.Add(new ChordProgressionData.ChordEvent
            {
                startStep = e.startStep,
                lengthSteps = e.lengthSteps,
                degree = e.degree,
                quality = e.quality,
                velocity = e.velocity,
                isDiatonic = e.isDiatonic,
                degreeAccidental = e.degreeAccidental,
            });
        }

        // The working grid now owns this data; keep SyncGridFromAsset (non-forced)
        // from re-copying the asset's events over it.
        gridInitializedFromAsset = true;

        // Clear any in-flight grid selection so stale editors don't commit over
        // the imported events.
        gridHasSelection = false;
        gridSelectedIndex = -1;
        gridEditingEvent = null;

        // Make the window's harmonic display consistent with the import key.
        referenceTonality = midiImportTonality;
        previewRoot = midiImportRoot;

        // Show the result where it lives: Grid mode.
        inputMode = InputMode.Grid;

        midiImportSummary =
            $"Imported {gridEvents.Count} chord event(s) over {gridMeasures} measure(s) " +
            $"in {midiImportRoot} {midiImportTonality}:  {result.romanSummary}";
    }

    // -----------------------------------------------------------------------
    // IMPORT-QOL-1 item 6 — provenance suffix helpers (D-QOL1-5=B)
    // -----------------------------------------------------------------------

    /// <summary>Suffix format appended to originalInput after a MIDI import.
    /// ASCII-safe (it feeds UpdateDisplayNameAuto) and detectable by
    /// <see cref="StripMidiProvenance"/> so it never breaks Roman re-parsing
    /// and never accumulates across re-imports/re-applies.</summary>
    private const string MidiProvenancePrefix = "  [MIDI: ";

    /// <summary>Append the imported-file provenance to a grid-derived Roman
    /// string, when the current grid lineage is a MIDI import; identity
    /// otherwise. Callers must keep <c>progressionInput</c> on the CLEAN
    /// string — the suffix is asset metadata, not Roman grammar.</summary>
    private string WithMidiProvenance(string romanFromGrid)
        => string.IsNullOrEmpty(midiImportProvenanceFile)
            ? romanFromGrid
            : romanFromGrid + MidiProvenancePrefix + midiImportProvenanceFile + "]";

    /// <summary>Remove a trailing provenance suffix (if any) so originalInput
    /// can be loaded back into the parseable Roman input field.</summary>
    private static string StripMidiProvenance(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        int i = input.LastIndexOf(MidiProvenancePrefix, StringComparison.Ordinal);
        if (i >= 0 && input.TrimEnd().EndsWith("]", StringComparison.Ordinal))
            return input.Substring(0, i).TrimEnd();
        return input;
    }
}
#endif
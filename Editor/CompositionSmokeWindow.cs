#if UNITY_EDITOR
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.EditorTools
{
    /// <summary>
    /// PACKAGE-OWNED smoke harness: assembles a MULTI-TRACK song (any subset
    /// of Rhythm / Backing / Melody / Bassline, distinct roles) through the
    /// real package pipeline and writes a .mid — WITHOUT going through ALWTTT
    /// (no cards, no CompositionSession, no MidiMusicManager).
    ///
    /// Stage 1 of the multi-track smoke batch. The SongConfig assembly lives
    /// in the runtime-safe <see cref="SmokeSongConfigAssembler"/> so Stage 2's
    /// runtime CompositionSmokeRunner is a thin shell over the same brain;
    /// this window keeps only UI, in-memory style convenience, the render
    /// call, and the .mid write.
    ///
    /// Render entry: <c>Orchestrator.GenerateSinglePart(part, roles, 0,
    /// bpmOverride, null, seedOverride)</c> — NOT GenerateSong. Reason:
    /// GenerateSong ignores ExplicitBpm and rolls a random BPM from
    /// Part.TempoRange with an unseeded RNG, so the BPM field would be dead
    /// and renders tempo-nondeterministic. GenerateSinglePart performs the
    /// identical single-part assembly (meta + metronome + PASS 1/PASS 2) and
    /// honors both overrides.
    ///
    /// Behavior note vs the previous single-track window: per-track seeds now
    /// derive via ResolveTrackSeedPart instead of ResolveTrackSeedSong, so
    /// outputs are not bit-comparable to old renders (which were never
    /// tempo-stable anyway, per the GenerateSong BPM roll above). The old
    /// workflow itself IS reproduced: a one-entry Bassline list with no style
    /// asset exposes the same in-memory articulation fields as before
    /// (D-SMOKE-MT-1=B).
    ///
    /// Editor-only (Editor asmdef); touches no governed runtime semantics.
    /// </summary>
    public class CompositionSmokeWindow : EditorWindow
    {
        [MenuItem("MidiGenPlay/Smoke/Composition Smoke (multi-track → .mid)")]
        public static void Open()
        {
            var w = GetWindow<CompositionSmokeWindow>("Composition Smoke");
            w.minSize = new Vector2(480, 520);
        }

        // Roles the smoke supports in v1 (D-SMOKE-MT-4=B: Harmony deferred —
        // its card-config consumption path is legacy-field based/unverified).
        private static readonly TrackRole[] SupportedRoles =
        {
            TrackRole.Rhythm, TrackRole.Backing, TrackRole.Melody, TrackRole.Bassline
        };

        /// <summary>
        /// One UI row. Wraps the runtime spec plus the editor-side no-asset
        /// articulation fallback (D-SMOKE-MT-1=B): when 'spec.style' is null
        /// and the role is Backing or Bassline, these two fields are injected
        /// as an in-memory card SO right before assembly — preserving the
        /// previous single-track window's workflow verbatim.
        /// </summary>
        [System.Serializable]
        private class SmokeEntry
        {
            public SmokeTrackSpec spec = new SmokeTrackSpec();
            public ChordExpressionType chordExpression = ChordExpressionType.Block;
            public ArpeggioRate arpeggioRate = ArpeggioRate.Eighth;
        }

        // --- Serialized window state ---
        [SerializeField] private MidiGenPlayConfig config;
        [SerializeField] private SmokePartContext partContext = new SmokePartContext();
        [SerializeField] private List<SmokeEntry> entries = new List<SmokeEntry>();
        [SerializeField] private bool overrideSeed = false;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool stripMetronome = false; // D-SMOKE-MT-5=A

        private Vector2 _scroll;
        private string _lastPath;

        private void OnGUI()
        {
            // Domain-reload safety for serialized reference fields.
            partContext ??= new SmokePartContext();
            entries ??= new List<SmokeEntry>();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
            config = (MidiGenPlayConfig)EditorGUILayout.ObjectField(
                "MGP Config", config, typeof(MidiGenPlayConfig), false);

            EditorGUILayout.Space(8);
            DrawPartContext();

            EditorGUILayout.Space(8);
            DrawEntries();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
            overrideSeed = EditorGUILayout.Toggle("Override seed", overrideSeed);
            using (new EditorGUI.DisabledScope(!overrideSeed))
                seed = EditorGUILayout.IntField("Seed", seed);
            stripMetronome = EditorGUILayout.Toggle(
                new GUIContent("Strip metronome",
                    "Post-render: removes track chunks whose NOTE events all sit " +
                    "on the always-on metronome channel (" +
                    MidiGenerator.MetronomeChannel + "). The conductor/meta chunk " +
                    "is kept (it has no notes)."),
                stripMetronome);

            EditorGUILayout.Space(10);

            var issues = Validate();
            using (new EditorGUI.DisabledScope(issues.Count > 0))
            {
                if (GUILayout.Button("Render & Save .mid", GUILayout.Height(28)))
                    RenderAndSave();
            }
            if (issues.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Info);

            if (!string.IsNullOrEmpty(_lastPath))
                EditorGUILayout.HelpBox("Wrote: " + _lastPath, MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Bypasses ALWTTT entirely: builds a SongConfig via " +
                "SmokeSongConfigAssembler and renders through " +
                "Orchestrator.GenerateSinglePart (honors BPM + seed). Harmony " +
                "consumers (Bassline/Melody) read their chords from the part's " +
                "progression: assign the ChordProgressionData on the Backing " +
                "row's Pattern slot (or on the consumer's own Pattern slot if " +
                "there is no Backing row). Turn on the config's Log Generator " +
                "for composer traces.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void DrawPartContext()
        {
            EditorGUILayout.LabelField("Part context (shared by all tracks)",
                EditorStyles.boldLabel);
            partContext.tonality = (Tonality)EditorGUILayout.EnumPopup(
                "Tonality", partContext.tonality);
            partContext.rootNote = (NoteName)EditorGUILayout.EnumPopup(
                "Root Note", partContext.rootNote);
            partContext.timeSignature = (TimeSignature)EditorGUILayout.EnumPopup(
                "Time Signature", partContext.timeSignature);
            partContext.measures = Mathf.Max(1,
                EditorGUILayout.IntField("Measures", partContext.measures));
            partContext.bpm = Mathf.Clamp(
                EditorGUILayout.IntField("BPM", partContext.bpm), 20, 400);
        }

        private void DrawEntries()
        {
            EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Track {i} — {e.spec.role}",
                    EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(64)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();

                e.spec.role = (TrackRole)EditorGUILayout.EnumPopup("Role", e.spec.role);

                // Role-appropriate instrument slot (Rhythm reads
                // cfg.PercussionInstrument; everything else cfg.Instrument).
                if (e.spec.role == TrackRole.Rhythm)
                {
                    e.spec.percussionInstrument =
                        (MIDIPercussionInstrumentSO)EditorGUILayout.ObjectField(
                            "Drum Kit", e.spec.percussionInstrument,
                            typeof(MIDIPercussionInstrumentSO), false);
                }
                else
                {
                    e.spec.instrument = (MIDIInstrumentSO)EditorGUILayout.ObjectField(
                        "Instrument", e.spec.instrument,
                        typeof(MIDIInstrumentSO), false);
                }

                e.spec.pattern = (PatternDataSO)EditorGUILayout.ObjectField(
                    new GUIContent("Pattern", ExpectedPatternHint(e.spec.role)),
                    e.spec.pattern, typeof(PatternDataSO), false);
                var patWarn = PatternTypeWarning(e.spec);
                if (patWarn != null)
                    EditorGUILayout.HelpBox(patWarn, MessageType.Warning);

                e.spec.style = (TrackStyleBundleSO)EditorGUILayout.ObjectField(
                    new GUIContent("Style (card config)",
                        "Authored card config asset for Parameters.Style. Optional."),
                    e.spec.style, typeof(TrackStyleBundleSO), false);
                var styleWarn = StyleTypeWarning(e.spec);
                if (styleWarn != null)
                    EditorGUILayout.HelpBox(styleWarn, MessageType.Warning);

                // D-SMOKE-MT-1=B: no-asset articulation fallback.
                if (e.spec.style == null &&
                    (e.spec.role == TrackRole.Backing || e.spec.role == TrackRole.Bassline))
                {
                    EditorGUILayout.LabelField(
                        "No style asset — in-memory articulation:",
                        EditorStyles.miniLabel);
                    e.chordExpression = (ChordExpressionType)EditorGUILayout.EnumPopup(
                        "Chord Expression", e.chordExpression);
                    e.arpeggioRate = (ArpeggioRate)EditorGUILayout.EnumPopup(
                        "Arpeggio Rate", e.arpeggioRate);
                }

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0) entries.RemoveAt(removeAt);

            if (GUILayout.Button("+ Add track"))
                entries.Add(new SmokeEntry());
        }

        // ---------- Validation (button gating; assembler re-validates) ----------

        private List<string> Validate()
        {
            var issues = new List<string>();
            if (config == null) issues.Add("Assign a MGP Config.");
            if (entries.Count == 0) issues.Add("Add at least one track.");

            var seen = new HashSet<TrackRole>();
            for (int i = 0; i < entries.Count; i++)
            {
                var s = entries[i].spec;
                if (!SupportedRoles.Contains(s.role))
                    issues.Add($"Track {i}: role {s.role} is not supported by the " +
                               "v1 smoke (supported: Rhythm, Backing, Melody, Bassline).");
                if (!seen.Add(s.role))
                    issues.Add($"Track {i}: duplicate role {s.role} — the orchestrator " +
                               "caches tracks by role; use distinct roles.");
                if (s.role == TrackRole.Rhythm && s.percussionInstrument == null)
                    issues.Add($"Track {i} (Rhythm): assign a Drum Kit " +
                               "(MIDIPercussionInstrumentSO).");
                if (s.role != TrackRole.Rhythm && s.instrument == null)
                    issues.Add($"Track {i} ({s.role}): assign an Instrument.");
            }
            return issues;
        }

        private static string ExpectedPatternHint(TrackRole role) => role switch
        {
            TrackRole.Rhythm => "Expected: DrumPatternData.",
            TrackRole.Melody => "Expected: MelodyPatternData (authored melody) " +
                                "or ChordProgressionData (procedural over chords).",
            _ => "Expected: ChordProgressionData.",
        };

        /// <summary>Soft warning only — composers null-cast gracefully.</summary>
        private static string PatternTypeWarning(SmokeTrackSpec s)
        {
            if (s.pattern == null) return null;
            switch (s.role)
            {
                case TrackRole.Rhythm:
                    return s.pattern is DrumPatternData ? null :
                        "Pattern is not a DrumPatternData; the rhythm composer " +
                        "will ignore it.";
                case TrackRole.Melody:
                    return (s.pattern is MelodyPatternData ||
                            s.pattern is ChordProgressionData) ? null :
                        "Pattern is neither MelodyPatternData nor " +
                        "ChordProgressionData; the melody composer will ignore it.";
                default:
                    return s.pattern is ChordProgressionData ? null :
                        "Pattern is not a ChordProgressionData; this role will " +
                        "ignore it.";
            }
        }

        /// <summary>Soft warning only — composers cast Style per role.</summary>
        private static string StyleTypeWarning(SmokeTrackSpec s)
        {
            if (s.style == null) return null;
            bool ok = s.role switch
            {
                TrackRole.Rhythm => s.style is RhythmCardConfigSO,
                TrackRole.Backing => s.style is BackingCardConfigSO,
                TrackRole.Melody => s.style is MelodyCardConfigSO,
                TrackRole.Bassline => s.style is BasslineCardConfigSO,
                _ => true,
            };
            return ok ? null :
                $"Style asset type '{s.style.GetType().Name}' does not match role " +
                $"{s.role}; the composer will ignore it.";
        }

        // ---------- Render ----------

        private void RenderAndSave()
        {
            // 1) Editor-side spec build: inject the in-memory articulation SO
            //    where the fallback applies (assembler stays asset-agnostic).
            var specs = entries.Select(BuildEffectiveSpec).ToList();

            // 2) Assemble via the shared runtime-safe brain.
            SongConfig song;
            try
            {
                song = SmokeSongConfigAssembler.Assemble(partContext, specs);
            }
            catch (System.ArgumentException ex)
            {
                EditorUtility.DisplayDialog("Composition Smoke",
                    "Assembly failed:\n" + ex.Message, "OK");
                return;
            }

            // 3) Render through the real pipeline. GenerateSinglePart (not
            //    GenerateSong) so BPM + seed overrides are honored — see class doc.
            Melanchall.DryWetMidi.Core.MidiFile file;
            try
            {
                var gen = new MidiGenerator(config);
                var render = gen.Orchestrator.GenerateSinglePart(
                    song.Parts[0],
                    song.ChannelRoles,
                    partIndex: 0,
                    bpmOverride: partContext.bpm,
                    instrumentOverrides: null,
                    seedOverride: overrideSeed ? seed : (int?)null);
                file = render?.merged;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[CompositionSmoke] Render threw: " + ex);
                EditorUtility.DisplayDialog("Composition Smoke",
                    "Render threw:\n" + ex.Message, "OK");
                return;
            }

            if (file == null)
            {
                Debug.LogError("[CompositionSmoke] GenerateSinglePart returned null.");
                return;
            }

            if (stripMetronome)
                StripMetronomeChunks(file);

            int noteCount = 0;
            foreach (var _ in file.GetNotes()) noteCount++;

            // 4) Write .mid under Assets so it's easy to find/import.
            const string dir = "Assets/CompositionSmoke";
            Directory.CreateDirectory(dir);
            string rolesTag = string.Join("-",
                specs.Select(s => s.role.ToString().Substring(0, 2).ToLower()));
            string fileName =
                $"smoke_{rolesTag}_{System.DateTime.Now:HHmmss}.mid";
            string path = Path.Combine(dir, fileName);
            file.Write(path, overwriteFile: true);
            AssetDatabase.Refresh();

            _lastPath = path;
            Debug.Log($"<color=lime>[CompositionSmoke]</color> tracks={specs.Count} " +
                      $"({rolesTag}) bpm={partContext.bpm} notes={noteCount} " +
                      $"stripMetro={stripMetronome} -> {path}");
            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
        }

        /// <summary>
        /// Returns the spec to hand to the assembler. When the D-SMOKE-MT-1=B
        /// fallback applies (no style asset, Backing/Bassline role), a fresh
        /// in-memory card SO carrying the entry's articulation is injected —
        /// persistent card-level surface (D-EXP1=A), never saved as an asset,
        /// lives only for this render. Matches the previous single-track
        /// window's behavior (which always injected for these roles).
        /// </summary>
        private static SmokeTrackSpec BuildEffectiveSpec(SmokeEntry e)
        {
            var s = e.spec;
            if (s.style != null ||
                (s.role != TrackRole.Backing && s.role != TrackRole.Bassline))
                return s;

            TrackStyleBundleSO inMem;
            if (s.role == TrackRole.Bassline)
            {
                var b = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
                b.chordExpression = e.chordExpression;
                b.arpeggioRate = e.arpeggioRate;
                inMem = b;
            }
            else
            {
                var b = ScriptableObject.CreateInstance<BackingCardConfigSO>();
                b.chordExpression = e.chordExpression;
                b.arpeggioRate = e.arpeggioRate;
                inMem = b;
            }
            inMem.hideFlags = HideFlags.HideAndDontSave;

            // Do not mutate the serialized entry — copy the spec with the
            // in-memory style attached.
            return new SmokeTrackSpec
            {
                role = s.role,
                instrument = s.instrument,
                percussionInstrument = s.percussionInstrument,
                pattern = s.pattern,
                style = inMem,
            };
        }

        /// <summary>
        /// D-SMOKE-MT-5=A. Removes chunks that contain at least one NoteOn and
        /// whose NoteOns ALL sit on the metronome channel. Filtering by note
        /// events (not any ChannelEvent) deliberately spares the conductor/meta
        /// chunk, which carries an AllSoundOff ControlChange on the metronome
        /// channel but no notes.
        /// </summary>
        private static void StripMetronomeChunks(Melanchall.DryWetMidi.Core.MidiFile file)
        {
            var toRemove = new List<TrackChunk>();
            foreach (var chunk in file.GetTrackChunks())
            {
                var noteOns = chunk.Events.OfType<NoteOnEvent>().ToList();
                if (noteOns.Count > 0 &&
                    noteOns.All(n => n.Channel == MidiGenerator.MetronomeChannel))
                    toRemove.Add(chunk);
            }
            foreach (var c in toRemove)
                file.Chunks.Remove(c);
        }
    }
}
#endif
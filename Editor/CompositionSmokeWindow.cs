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

        // Row type promoted to the shared runtime SmokeEntry
        // (MidiGenPlay.Composition.SmokeEntry) so window + runner read one
        // SmokeSetupSO — D-SMOKE-RT-5=A. No nested duplicate here anymore.

        // --- Serialized window state ---
        [SerializeField] private MidiGenPlayConfig config;
        [SerializeField] private SmokePartContext partContext = new SmokePartContext();
        [SerializeField] private List<SmokeEntry> entries = new List<SmokeEntry>();
        [SerializeField] private bool overrideSeed = false;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool stripMetronome = false; // D-SMOKE-MT-5=A

        // D-SMOKE-RT-5=A: the shared source of truth. The window keeps its
        // rich inline authoring, but Save/Load round-trips the whole setup to
        // this asset so the runtime runner can replay identical inputs.
        [SerializeField] private SmokeSetupSO setup;

        private Vector2 _scroll;
        private string _lastPath;

        /// <summary>IMPORT-QOL-1 item 3 — EditorPrefs key remembering the last
        /// manually-assigned MGP Config by GUID (project-agnostic; a stale GUID
        /// simply resolves to nothing).</summary>
        private const string ConfigGuidPrefKey =
            "MidiGenPlay.CompositionSmoke.ConfigGuid";

        /// <summary>
        /// IMPORT-QOL-1 item 3 — auto-assign MGP Config when the field is
        /// empty on open: (1) the last manual selection, restored by GUID from
        /// EditorPrefs; (2) else, the project's config IF exactly one exists
        /// (with several candidates we never guess). The field stays fully
        /// editable; this only fills a blank.
        /// </summary>
        private void OnEnable()
        {
            if (config != null) return;

            string guid = EditorPrefs.GetString(ConfigGuidPrefKey, "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    config = AssetDatabase.LoadAssetAtPath<MidiGenPlayConfig>(path);
            }

            if (config == null)
            {
                var guids = AssetDatabase.FindAssets("t:MidiGenPlayConfig");
                if (guids.Length == 1)
                    config = AssetDatabase.LoadAssetAtPath<MidiGenPlayConfig>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void OnGUI()
        {
            // Domain-reload safety for serialized reference fields.
            partContext ??= new SmokePartContext();
            entries ??= new List<SmokeEntry>();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSharedSetup();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
            var newConfig = (MidiGenPlayConfig)EditorGUILayout.ObjectField(
                "MGP Config", config, typeof(MidiGenPlayConfig), false);
            if (newConfig != config)
            {
                config = newConfig;
                // IMPORT-QOL-1 item 3 — remember manual selections by GUID.
                if (config != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        config, out string cfgGuid, out long _))
                    EditorPrefs.SetString(ConfigGuidPrefKey, cfgGuid);
            }

            EditorGUILayout.Space(8);
            DrawPartContext();

            EditorGUILayout.Space(8);
            DrawEntries();

            DrawPatternMeasuresAdvisory();

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

        /// <summary>
        /// IMPORT-QOL-1 item 2 — advisory-only HelpBox listing every assigned
        /// pattern that declares MORE measures than the window (only the
        /// window's length renders, so the tail is cut); a differing time
        /// signature is noted as an extra clause on the same line. Patterns
        /// SHORTER than the window repeat — legitimate existing behavior,
        /// deliberately not warned. This NEVER changes measures automatically:
        /// "Fit to longest pattern" is an explicit button and touches ONLY
        /// partContext.measures, never the time signature.
        /// </summary>
        private void DrawPatternMeasuresAdvisory()
        {
            int longest = 0;
            List<string> lines = null;
            foreach (var e in entries)
            {
                var p = e?.spec?.pattern;
                if (p == null || p.Measures <= partContext.measures) continue;

                longest = Mathf.Max(longest, p.Measures);
                string tsNote = p.TimeSignature != partContext.timeSignature
                    ? $" (its time signature {p.TimeSignature} also differs " +
                      $"from the part's {partContext.timeSignature})"
                    : "";
                (lines ??= new List<string>()).Add(
                    $"'{p.name}' ({e.spec.role}) declares {p.Measures} measures; " +
                    $"the window renders {partContext.measures}, so its tail is " +
                    $"cut{tsNote}.");
            }
            if (lines == null) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(string.Join("\n", lines), MessageType.Warning);
            if (GUILayout.Button($"Fit to longest pattern ({longest} measures)"))
                partContext.measures = longest;
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

                    // MGP-ALWTTT-ARTIC-1 + CA-V1: Random selection knobs, now for
                    // Bassline too (D6 lifted) and for the rate sentinel.
                    if (e.chordExpression == ChordExpressionType.Random ||
                        e.arpeggioRate == ArpeggioRate.Random)
                        DrawRandomArticulationKnobs(e);

                    // CA-V1: jitter is independent of the Random sentinels.
                    e.velocityJitter = EditorGUILayout.IntSlider(
                        new GUIContent("Velocity Jitter",
                            "Seeded per-hit velocity offset, uniform in [-n, +n], " +
                            "clamped 1..127. 0 = exact legacy velocities. Applies " +
                            "to every figure, Block included."),
                        e.velocityJitter, 0, 32);
                }

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0) entries.RemoveAt(removeAt);

            if (GUILayout.Button("+ Add track"))
                entries.Add(new SmokeEntry());
        }

        /// <summary>
        /// MGP-ALWTTT-ARTIC-1 (SD-1=A / SD-2=A). Only drawn for a Backing row with
        /// no style asset and chordExpression = Random.
        ///
        /// - randomRerollChance: 1 = fresh figure per chord event (default);
        ///   0 = one figure for the whole render (per-LOOP variety then comes from
        ///   the host's per-render seed); intermediates = per-chord change chance.
        /// - randomFigureWeights: empty = uniform over the six Tier-1 figures.
        ///   Entries DEFINE the pool (unlisted excluded; weight &lt;= 0 excludes;
        ///   duplicates sum; a degenerate list falls back to uniform + a package
        ///   warning). Semantics live in the Backing composer SSoT §8.x — this
        ///   window only exposes the authoring surface.
        /// </summary>
        private static void DrawRandomArticulationKnobs(SmokeEntry e)
        {
            EditorGUI.indentLevel++;

            e.randomRerollChance = EditorGUILayout.Slider(
                new GUIContent("Reroll Chance",
                    "1 = roll a figure per chord event. 0 = one figure for the " +
                    "whole render (per-loop variety via the seed). Intermediates " +
                    "= chance of change per chord."),
                e.randomRerollChance, 0f, 1f);

            EditorGUILayout.LabelField(
                new GUIContent("Figure Weights",
                    "Empty = uniform over the six Tier-1 figures (Block included). " +
                    "Entries define the pool; weight <= 0 excludes; duplicates sum."),
                EditorStyles.miniLabel);

            e.randomFigureWeights ??= new List<ChordExpressionWeight>();

            int removeAt = -1;
            for (int i = 0; i < e.randomFigureWeights.Count; i++)
            {
                var w = e.randomFigureWeights[i];
                EditorGUILayout.BeginHorizontal();
                w.figure = (ChordExpressionType)EditorGUILayout.EnumPopup(w.figure);
                w.weight = EditorGUILayout.FloatField(w.weight, GUILayout.Width(60));
                if (GUILayout.Button("−", GUILayout.Width(22)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();
                e.randomFigureWeights[i] = w;

                if (w.figure == ChordExpressionType.Random)
                    EditorGUILayout.HelpBox(
                        "A 'Random' entry is ignored by the roll pool.",
                        MessageType.Info);
            }
            if (removeAt >= 0) e.randomFigureWeights.RemoveAt(removeAt);

            if (GUILayout.Button("+ Add weighted figure"))
                e.randomFigureWeights.Add(new ChordExpressionWeight
                {
                    figure = ChordExpressionType.Block,
                    weight = 1f,
                });

            if (e.randomFigureWeights.Count == 0)
                EditorGUILayout.LabelField(
                    "(empty → uniform pool of all six Tier-1 figures)",
                    EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
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

        // D-SMOKE-RT-5=A. The shared setup asset + explicit round-trip. The
        // window still authors inline (its proven GUI/render path is
        // untouched); Save writes the whole inline state into the SO verbatim
        // so the runtime runner replays byte-identical inputs. Workflow:
        // author here -> Save to SO -> assign that SO to the runner -> render
        // both -> compare. Re-Save after any inline edit before a parity run.
        private void DrawSharedSetup()
        {
            EditorGUILayout.LabelField("Shared setup (parity source, RT-5=A)",
                EditorStyles.boldLabel);
            setup = (SmokeSetupSO)EditorGUILayout.ObjectField(
                "Setup asset", setup, typeof(SmokeSetupSO), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(setup == null))
                {
                    if (GUILayout.Button("Save to SO"))
                        SaveToSetup();
                    if (GUILayout.Button("Load from SO"))
                        LoadFromSetup();
                }
            }
            EditorGUILayout.HelpBox(
                "Assign the SAME asset to the runtime CompositionSmokeRunner. " +
                "Save captures the inline state below into it; the runner " +
                "replays identical inputs. Re-Save after editing before a " +
                "parity run.", MessageType.None);
            EditorGUILayout.Space(4);
        }

        private void SaveToSetup()
        {
            if (setup == null) return;
            setup.config = config;
            setup.partContext = CloneContext(partContext);
            setup.entries = (entries ?? new List<SmokeEntry>())
                .Where(e => e != null).Select(e => e.Clone()).ToList();
            setup.overrideSeed = overrideSeed;
            setup.seed = seed;
            setup.stripMetronome = stripMetronome;
            EditorUtility.SetDirty(setup);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CompositionSmokeWindow] Saved {setup.entries.Count} " +
                      $"track(s) to '{setup.name}'.");
        }

        private void LoadFromSetup()
        {
            if (setup == null) return;
            config = setup.config;
            partContext = CloneContext(setup.partContext);
            entries = (setup.entries ?? new List<SmokeEntry>())
                .Where(e => e != null).Select(e => e.Clone()).ToList();
            overrideSeed = setup.overrideSeed;
            seed = setup.seed;
            stripMetronome = setup.stripMetronome;
        }

        private static SmokePartContext CloneContext(SmokePartContext c)
        {
            c ??= new SmokePartContext();
            return new SmokePartContext
            {
                partName = c.partName,
                tonality = c.tonality,
                rootNote = c.rootNote,
                timeSignature = c.timeSignature,
                measures = c.measures,
                bpm = c.bpm,
            };
        }

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

            // Fingerprint BEFORE strip, matching the runner, so two logs line
            // up chunk-for-chunk when diffing a parity failure.
            SmokeRenderUtil.LogRenderFingerprint(
                "window",
                overrideSeed ? seed : (config != null ? config.defaultSeed : 0),
                partContext, file);

            if (stripMetronome)
                SmokeRenderUtil.StripMetronomeChunks(file); // lifted, D-SMOKE-RT-3=A

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
        /// Returns the spec to hand to the assembler. The D-SMOKE-MT-1=B
        /// no-asset articulation fallback now lives in the shared runtime
        /// helper SmokeRenderUtil.BuildEffectiveSpec (D-SMOKE-RT-2=B) so the
        /// runtime CompositionSmokeRunner mirrors this window exactly.
        /// </summary>
        private static SmokeTrackSpec BuildEffectiveSpec(SmokeEntry e) =>
            SmokeRenderUtil.BuildEffectiveSpec(
                e.spec, e.chordExpression, e.arpeggioRate,
                e.randomRerollChance, e.randomFigureWeights, e.velocityJitter);

    }
}
#endif
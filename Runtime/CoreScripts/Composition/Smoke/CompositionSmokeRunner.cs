using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory; // TempoRange, TempoRule
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// SMOKE-MT Stage 2 (D-SMOKE-RT-1=A): runtime Composition Smoke runner.
    ///
    /// A MonoBehaviour a consumer can drop into any scene to render the same
    /// multi-track smoke song as the editor CompositionSmokeWindow, in Play
    /// mode or on device, with NO UnityEditor reference. Same brain
    /// (SmokeSongConfigAssembler), same render entry (GenerateSinglePart —
    /// NOT GenerateSong, which ignores ExplicitBpm and rolls tempo from an
    /// unseeded Random; see finding BPM-DET-1), same no-asset articulation
    /// fallback (SmokeRenderUtil.BuildEffectiveSpec, D-SMOKE-RT-2=B) and same
    /// metronome strip (SmokeRenderUtil.StripMetronomeChunks, D-SMOKE-RT-3=A).
    ///
    /// INPUTS (D-SMOKE-RT-5=A): all shared render inputs come from a single
    /// SmokeSetupSO 'setup' asset — the same asset the window authors via its
    /// Save/Load buttons — so window and runner cannot drift field-by-field.
    /// Assign the same SO to both, and identical seed + specs => byte-identical
    /// .mid (filenames differ by timestamp only). Runner-only knobs below
    /// (RT-4=A randomization, renderOnStart) are NOT part of the shared setup.
    ///
    /// Output (D-SMOKE-RT-1=A): a .mid written under
    /// Application.persistentDataPath/CompositionSmoke/. No AssetDatabase, no
    /// Application.dataPath writes, no playback, no synth.
    /// </summary>
    [AddComponentMenu("MidiGenPlay/Composition Smoke Runner")]
    public class CompositionSmokeRunner : MonoBehaviour
    {
        [Header("Shared setup (single source of truth, D-SMOKE-RT-5=A)")]
        [Tooltip("Assign the same SmokeSetupSO the window authors. Holds config, " +
                 "part context, track rows, seed and strip toggle.")]
        [SerializeField] private SmokeSetupSO setup;

        // --- RT-4=A: seeded Root/BPM range randomization (runner-only) -------
        // Rolled from a seeded System.Random(baseSeed), where baseSeed is the
        // same seed the render resolves (setup.overrideSeed ? setup.seed :
        // config.defaultSeed). Reproducible: a given toggle set + seed always
        // yields the same roll. The editor window has NO equivalent (RT-4=A),
        // so byte-parity with the window only holds with BOTH toggles OFF — use
        // randomization as a separate variety mode, validate parity in the off
        // state. Does NOT use MusicTheory.GetBPMFromRange (that one is unseeded
        // — BPM-DET-1); the valid-BPM filtering is replicated against the
        // seeded stream.
        [Header("Randomization (RT-4=A, seeded — breaks window parity when on)")]
        [SerializeField] private bool randomizeBpm = false;
        [SerializeField] private TempoRange bpmRange = TempoRange.Slow;
        [SerializeField] private TempoRule bpmRule = TempoRule.Any;
        [SerializeField] private bool randomizeRoot = false;
        [Tooltip("Allowed root notes to pick from. Empty = all 12 chromatic.")]
        [SerializeField] private List<NoteName> rootChoices = new List<NoteName>();

        // Same (Min,Max) inclusive bands as MusicTheory.TempoRanges. Duplicated
        // deliberately: that dictionary is private, and this is a dev tool.
        private static readonly Dictionary<TempoRange, (int Min, int Max)> BpmBands =
            new Dictionary<TempoRange, (int, int)>
            {
                { TempoRange.VerySlow, (61, 90) },
                { TempoRange.Slow,     (91, 120) },
                { TempoRange.Moderate, (121, 160) },
                { TempoRange.Fast,     (161, 200) },
                { TempoRange.VeryFast, (201, 240) },
            };

        [Header("Trigger")]
        [Tooltip("Render once automatically on Start().")]
        [SerializeField] private bool renderOnStart = false;

        /// <summary>Full path of the last successful export, or null.</summary>
        public string LastExportPath { get; private set; }

        private void Start()
        {
            if (renderOnStart)
                Render();
        }

        /// <summary>
        /// Assembles and renders the configured smoke song, writing the .mid
        /// under persistentDataPath/CompositionSmoke/. Safe to call
        /// repeatedly; each call is an independent render.
        /// </summary>
        [ContextMenu("Render Smoke (.mid → persistentDataPath)")]
        public void Render()
        {
            LastExportPath = null;

            if (setup == null)
            {
                Debug.LogError("[CompositionSmokeRunner] No SmokeSetupSO assigned.");
                return;
            }
            var config = setup.config;
            var entries = setup.entries;
            if (config == null)
            {
                Debug.LogError("[CompositionSmokeRunner] setup.config (MidiGenPlayConfig) is unassigned.");
                return;
            }
            if (entries == null || entries.Count == 0)
            {
                Debug.LogError("[CompositionSmokeRunner] setup has no track entries.");
                return;
            }

            // 0) Resolve the base seed the render will use, and roll RT-4=A
            //    Root/BPM from a dedicated seeded stream. Fixed roll order
            //    [root, bpm]; each draw is consumed only if its toggle is on,
            //    so a given toggle set + seed reproduces exactly.
            int baseSeed = setup.overrideSeed ? setup.seed : config.defaultSeed;
            var effectiveCtx = CloneContext(setup.partContext);
            if (randomizeRoot || randomizeBpm)
            {
                var roll = new System.Random(baseSeed);
                if (randomizeRoot)
                    effectiveCtx.rootNote = RollRoot(roll);
                if (randomizeBpm)
                    effectiveCtx.bpm = RollBpm(roll, bpmRange, bpmRule);
            }

            // 1) Effective specs: shared no-asset articulation fallback
            //    (D-SMOKE-RT-2=B — exact parity with the editor window).
            var specs = entries
                .Where(e => e != null)
                .Select(e => SmokeRenderUtil.BuildEffectiveSpec(
                    e.spec, e.chordExpression, e.arpeggioRate,
                    e.randomRerollChance, e.randomFigureWeights, e.velocityJitter))
                .ToList();

            // 2) Assemble via the shared runtime-safe brain.
            SongConfig song;
            try
            {
                song = SmokeSongConfigAssembler.Assemble(effectiveCtx, specs);
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogError("[CompositionSmokeRunner] Assembly failed: " + ex.Message);
                return;
            }

            // 3) Render through the real pipeline. GenerateSinglePart (not
            //    GenerateSong) so BPM + seed overrides are honored.
            Melanchall.DryWetMidi.Core.MidiFile file;
            try
            {
                var gen = new MidiGenerator(config);
                var render = gen.Orchestrator.GenerateSinglePart(
                    song.Parts[0],
                    song.ChannelRoles,
                    partIndex: 0,
                    bpmOverride: effectiveCtx.bpm,
                    instrumentOverrides: null,
                    seedOverride: setup.overrideSeed ? setup.seed : (int?)null,
                    defaultProgression: setup.defaultProgression);
                file = render?.merged;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[CompositionSmokeRunner] Render threw: " + ex);
                return;
            }

            if (file == null)
            {
                Debug.LogError("[CompositionSmokeRunner] GenerateSinglePart returned null.");
                return;
            }

            // Fingerprint BEFORE strip, matching the window's placement, so two
            // logs line up chunk-for-chunk when diffing a parity failure.
            SmokeRenderUtil.LogRenderFingerprint("runner", baseSeed, effectiveCtx, file);

            if (setup.stripMetronome)
                SmokeRenderUtil.StripMetronomeChunks(file); // D-SMOKE-RT-3=A

            int noteCount = 0;
            foreach (var _ in file.GetNotes())
                noteCount++;

            // 4) Write .mid under persistentDataPath (D-SMOKE-RT-1=A):
            //    retrievable in Play mode and pullable off a device. Same
            //    naming scheme as the editor window.
            string dir = Path.Combine(Application.persistentDataPath, "CompositionSmoke");
            Directory.CreateDirectory(dir);
            string rolesTag = string.Join("-",
                specs.Select(s => s.role.ToString().Substring(0, 2).ToLower()));
            string fileName =
                $"smoke_{rolesTag}_{System.DateTime.Now:HHmmss}.mid";
            string path = Path.Combine(dir, fileName);
            file.Write(path, overwriteFile: true);

            LastExportPath = path;
            string rollTag =
                (randomizeRoot ? $" root*={effectiveCtx.rootNote}" : "") +
                (randomizeBpm ? $" bpm*={effectiveCtx.bpm}({bpmRange}/{bpmRule})" : "");
            Debug.Log($"<color=lime>[CompositionSmokeRunner]</color> tracks={specs.Count} " +
                      $"({rolesTag}) bpm={effectiveCtx.bpm} notes={noteCount} " +
                      $"seed={(setup.overrideSeed ? setup.seed.ToString() : $"default({baseSeed})")}" +
                      $"{rollTag} stripMetro={setup.stripMetronome} -> {path}");
        }

        // --- RT-4=A helpers -------------------------------------------------

        private static SmokePartContext CloneContext(SmokePartContext c) =>
            new SmokePartContext
            {
                partName = c.partName,
                tonality = c.tonality,
                rootNote = c.rootNote,
                timeSignature = c.timeSignature,
                measures = c.measures,
                bpm = c.bpm,
            };

        private NoteName RollRoot(System.Random roll)
        {
            var pool = (rootChoices != null && rootChoices.Count > 0)
                ? rootChoices
                : System.Enum.GetValues(typeof(NoteName)).Cast<NoteName>().ToList();
            return pool[roll.Next(pool.Count)];
        }

        // Same contract as MusicTheory.GetBPMFromRange, but seeded.
        private static int RollBpm(System.Random roll, TempoRange range, TempoRule rule)
        {
            (int Min, int Max) band = BpmBands.TryGetValue(range, out var b) ? b : (91, 120);
            var valid = Enumerable.Range(band.Min, band.Max - band.Min + 1)
                .Where(bpm => rule switch
                {
                    TempoRule.MultiplesOfTen => bpm % 10 == 0,
                    TempoRule.MultiplesOfFive => bpm % 5 == 0,
                    TempoRule.OnlyEven => bpm % 2 == 0,
                    _ => true, // Any
                })
                .ToList();
            return valid.Count > 0 ? valid[roll.Next(valid.Count)] : band.Min;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Melanchall.DryWetMidi.Interaction;
using UnityEditor;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;
using MidiGenPlay.Composition.Diagnostics;
using static MidiGenPlay.MusicTheory.MusicTheory;

using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.EditorTools
{
    /// <summary>
    /// MGP-TONALITY-2: tonal/rhythmic regression matrix over the composition
    /// smoke harness. Editor-only; adds NO runtime dependency and modifies NO
    /// composer. Renders the cartesian product of
    ///   tonality profiles x meters x progressions x track combos x
    ///   bass tone modes x backing expressions
    /// through the exact smoke render path (SmokeSongConfigAssembler +
    /// SongOrchestrator.GenerateSinglePart) and records, per cell:
    ///
    ///  - the TonalityAudit counters (reset/snapshot; the audit stays log-only,
    ///    D-TON3=A) — what each composer BELIEVED, split by tier and origin;
    ///  - a runner-side CANONICAL re-classification (D-TON2-PARITY=A): every
    ///    emitted note of Backing/Bassline/Melody is re-judged against the
    ///    canonical chord pcs derived from (degree, degreeAccidental, quality)
    ///    via the shared law of SSoT chord identity —
    ///    TransposeNoteName(scaleNames[degree], accidental) + quality
    ///    intervals. This is what actually answers DoD question (1): the audit
    ///    alone cannot see a parity break (pre-D-TON10, the bass's wrong notes
    ///    were consistent with the bass's wrong chord belief), so the runner
    ///    diffs canonical vs believed per track (beliefDiv column);
    ///  - a positional walk-approach inference (D-TON2-WALK=B+): a bass
    ///    canonical red in an ImprovisedWalk cell, landing in the LAST BEAT of
    ///    its chord window and within 2 semitones of the next event's
    ///    canonical root, is tagged walk-approach(inferred) — intentional
    ///    chromaticism, never a defect. Composers are untouched; the
    ///    origin=walk-approach tag in BassTrackComposer remains a recorded
    ///    follow-up.
    ///
    /// Determinism (D-TON2-SEED=A): ONE configured seed for every cell,
    /// recorded per row. Reproduce any cell with RunCell(inputs, spec,
    /// verbose:true) — same axes + same seed => same output by the package's
    /// core invariant.
    ///
    /// Console hygiene: TonalityAudit.SuppressLogs is forced true for the
    /// sweep (restored in finally); config.logGenerator is toggled off
    /// IN MEMORY ONLY (never SetDirty, restored in finally) — no silent asset
    /// writes. TonalityAudit.Enabled is forced true after each
    /// MidiGenerator construction (the ctor syncs it from the config asset).
    ///
    /// Windows/chord lookups come from PartRender.sharedProgressionData — the
    /// SAME post-normalization clone the composers consumed — via
    /// ChordProgressionData.FindChordEventAt, so the canonical pass can never
    /// disagree with the engine about which chord was sounding.
    /// </summary>
    public static class TonalityMatrixRunner
    {
        // ------------------------------------------------------------------
        // Input / cell / result models
        // ------------------------------------------------------------------

        public sealed class MatrixInputs
        {
            public SmokeSetupSO setup;
            public List<ChordProgressionData> progressions = new();
            public int seed = 12345;
            /// <summary>Null/empty => persistentDataPath/TonalityMatrix.</summary>
            public string outputDir;
        }

        public sealed class CellSpec
        {
            public int index;
            public TonalityProfileSO profile;
            public TimeSignature meter;
            public ChordProgressionData progression;
            public bool hasMelody, hasBass, hasBacking;
            /// <summary>Valid only when hasBass.</summary>
            public BasslineCardConfigSO.BassArpeggioToneMode bassMode;
            /// <summary>Valid only when hasBacking. Block or ArpeggioUp.</summary>
            public ChordExpressionType backingMode;
            public int seed;
            /// <summary>Part root — from setup.partContext.rootNote (constant
            /// this batch; carried per cell so a future root axis is one edit).</summary>
            public NoteName rootNote;

            public string TracksLabel =>
                (hasMelody ? "M" : "") + (hasBass ? "B" : "") + (hasBacking ? "K" : "");

            public override string ToString() =>
                $"#{index} {profile?.tonality} {meter} '{progression?.name}' " +
                $"tracks={TracksLabel}" +
                (hasBass ? $" bass={bassMode}" : "") +
                (hasBacking ? $" backing={backingMode}" : "") +
                $" seed={seed}";
        }

        public sealed class TrackTally
        {
            // Audit (composer-belief) tiers, from TonalityAudit counters.
            public int auditInScale, auditYellow, auditRed;
            // Canonical re-classification tiers.
            public int canonInScale, canonYellow, canonRed;
            /// <summary>canonRed - auditRed. Nonzero => the composer believed a
            /// different chord than the canonical one (parity suspect).</summary>
            public int BeliefDiv => canonRed - auditRed;
        }

        public sealed class CellResult
        {
            public CellSpec spec;
            public int bpm;
            public Dictionary<string, int> auditCounters = new();
            public Dictionary<TrackRole, TrackTally> byTrack = new();
            /// <summary>Bass canonical reds explained as walk approach notes
            /// (positional inference, D-TON2-WALK=B+).</summary>
            public int walkApproachInferred;
            /// <summary>Canonical reds NOT explained as walk approach —
            /// the DoD-2 defect signal.</summary>
            public int ResidualReds =>
                byTrack.Values.Sum(t => t.canonRed) - walkApproachInferred;
            public string error;   // non-null => cell failed to render/measure
            public string warning; // non-fatal notes (e.g. no sharedProgressionData)

            public TrackTally Tally(TrackRole role)
            {
                if (!byTrack.TryGetValue(role, out var t))
                    byTrack[role] = t = new TrackTally();
                return t;
            }
        }

        // Audit track labels as emitted by the composers' Check calls.
        private static readonly Dictionary<TrackRole, string> AuditLabel = new()
        {
            { TrackRole.Melody,  "Melody"  },
            { TrackRole.Bassline, "Bass"   },
            { TrackRole.Backing, "Backing" },
        };

        private static readonly TrackRole[] TonalRoles =
            { TrackRole.Backing, TrackRole.Bassline, TrackRole.Melody };

        // ------------------------------------------------------------------
        // Cell enumeration
        // ------------------------------------------------------------------

        /// <summary>
        /// Expands the axes with collapse rules: bass modes only where bass is
        /// present, backing modes only where backing is present. Combo order
        /// is stable so cell indices are reproducible for identical inputs.
        /// </summary>
        public static List<CellSpec> BuildCells(MatrixInputs inputs, out List<string> validationNotes)
        {
            validationNotes = new List<string>();
            var cells = new List<CellSpec>();

            var config = inputs.setup != null ? inputs.setup.config : null;
            var profiles = (config != null && config.tonalityProfiles != null)
                ? config.tonalityProfiles.Where(p => p != null).ToList()
                : new List<TonalityProfileSO>();

            if (profiles.Count == 0)
                validationNotes.Add("config.tonalityProfiles is empty — no cells.");

            // The engine resolves profiles BY TONALITY (GetProfileForTonality),
            // so two profiles sharing a Tonality cannot be independently swept.
            var dupTon = profiles.GroupBy(p => p.tonality).Where(g => g.Count() > 1)
                                 .Select(g => g.Key.ToString()).ToList();
            if (dupTon.Count > 0)
                validationNotes.Add(
                    "Profiles with duplicate Tonality (engine keys the lookup on " +
                    "Tonality; only the first per tonality is exercised): " +
                    string.Join(", ", dupTon));

            bool anyDiatonic = inputs.progressions.Any(p => p != null &&
                (p.events == null || p.events.All(e => e.degreeAccidental == 0)));
            bool anyAccidental = inputs.progressions.Any(p => p != null &&
                p.events != null && p.events.Any(e => e.degreeAccidental != 0));
            if (!anyDiatonic)
                validationNotes.Add("No fully diatonic progression in the list (batch axis asks for >=1).");
            if (!anyAccidental)
                validationNotes.Add("No progression with degreeAccidental != 0 (batch axis asks for >=1 — F-TON-ACC-1 coverage).");

            var meters = new[] { TimeSignature.FourFour, TimeSignature.SixEight };

            // (M, B, K) combos: solos, pairs, trio — stable order.
            var combos = new (bool m, bool b, bool k)[]
            {
                (false, false, true),  // K
                (false, true,  false), // B
                (true,  false, false), // M
                (false, true,  true),  // BK
                (true,  false, true),  // MK
                (true,  true,  false), // MB
                (true,  true,  true),  // MBK
            };
            var bassModes = new[]
            {
                BasslineCardConfigSO.BassArpeggioToneMode.ChordToneWalk,
                BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk,
            };
            var backingModes = new[]
            {
                ChordExpressionType.Block,
                ChordExpressionType.ArpeggioUp,
            };

            int idx = 0;
            foreach (var profile in profiles)
                foreach (var meter in meters)
                    foreach (var prog in inputs.progressions.Where(p => p != null))
                        foreach (var (m, b, k) in combos)
                        {
                            var bm = b ? bassModes : new[] { default(BasslineCardConfigSO.BassArpeggioToneMode) };
                            var km = k ? backingModes : new[] { default(ChordExpressionType) };
                            foreach (var bassMode in bm)
                                foreach (var backMode in km)
                                {
                                    cells.Add(new CellSpec
                                    {
                                        index = idx++,
                                        profile = profile,
                                        meter = meter,
                                        progression = prog,
                                        hasMelody = m,
                                        hasBass = b,
                                        hasBacking = k,
                                        bassMode = bassMode,
                                        backingMode = backMode,
                                        seed = inputs.seed,
                                        rootNote = inputs.setup != null
                                            ? inputs.setup.partContext.rootNote
                                            : NoteName.C,
                                    });
                                }
                        }
            return cells;
        }

        // ------------------------------------------------------------------
        // Sweep
        // ------------------------------------------------------------------

        /// <summary>
        /// Runs every cell. progress(current, total, spec) is invoked before
        /// each cell; return true from cancelRequested to abort (partial
        /// results are returned).
        /// </summary>
        public static List<CellResult> RunSweep(
            MatrixInputs inputs,
            List<CellSpec> cells,
            Action<int, int, CellSpec> progress = null,
            Func<bool> cancelRequested = null)
        {
            var results = new List<CellResult>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                progress?.Invoke(i, cells.Count, cells[i]);
                if (cancelRequested != null && cancelRequested())
                    break;
                results.Add(RunCell(inputs, cells[i], verbose: false));
            }
            return results;
        }

        /// <summary>
        /// Renders and measures one cell. verbose=true is the drill-down /
        /// reproduction mode: audit logs are NOT suppressed and the config's
        /// own logGenerator setting is left as-is, so the cell replays with
        /// full console evidence. Same axes + same seed => same output.
        /// </summary>
        public static CellResult RunCell(MatrixInputs inputs, CellSpec spec, bool verbose)
        {
            var result = new CellResult { spec = spec };
            var setup = inputs.setup;
            var config = setup != null ? setup.config : null;
            if (setup == null || config == null)
            {
                result.error = "SmokeSetupSO or its config is unassigned.";
                return result;
            }

            // Templates per role from the setup rows (instruments; melody row
            // also carries its style/pattern pass-through — a procedural
            // melody row is recommended so the audit runs with harmonic
            // context).
            SmokeEntry tmplBacking = FindTemplate(setup, TrackRole.Backing);
            SmokeEntry tmplBass = FindTemplate(setup, TrackRole.Bassline);
            SmokeEntry tmplMelody = FindTemplate(setup, TrackRole.Melody);
            var missing = new List<string>();
            if (spec.hasBacking && tmplBacking == null) missing.Add("Backing");
            if (spec.hasBass && tmplBass == null) missing.Add("Bassline");
            if (spec.hasMelody && tmplMelody == null) missing.Add("Melody");
            if (missing.Count > 0)
            {
                result.error = "SmokeSetupSO has no template row for: " +
                               string.Join(", ", missing);
                return result;
            }

            // Part context: profile tonality + cell meter; root/measures/bpm
            // from the setup (root is deliberately NOT an axis this batch).
            var ctx = new SmokePartContext
            {
                partName = $"TonMatrix_{spec.index}",
                tonality = spec.profile.tonality,
                rootNote = setup.partContext.rootNote,
                timeSignature = spec.meter,
                measures = setup.partContext.measures,
                bpm = setup.partContext.bpm,
            };

            // Track specs — Backing FIRST (finding C4: harmony consumers read
            // the Backing row's progression). Bass/Backing articulation comes
            // from runner-owned in-memory cards (BuildEffectiveSpec cannot set
            // arpeggioToneMode — verified gap), destroyed per cell.
            var inMemCards = new List<TrackStyleBundleSO>(2);
            var specs = new List<SmokeTrackSpec>(3);

            if (spec.hasBacking)
            {
                var card = ScriptableObject.CreateInstance<BackingCardConfigSO>();
                card.chordExpression = spec.backingMode;
                card.arpeggioRate = ArpeggioRate.Eighth;
                card.randomRerollChance = 1f;
                card.velocityJitter = 0;
                card.hideFlags = HideFlags.HideAndDontSave;
                inMemCards.Add(card);
                specs.Add(new SmokeTrackSpec
                {
                    role = TrackRole.Backing,
                    instrument = tmplBacking.spec.instrument,
                    pattern = spec.progression, // the cell's harmony
                    style = card,
                });
            }
            if (spec.hasBass)
            {
                var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
                // Tone modes act on the arpeggio emission path.
                card.chordExpression = ChordExpressionType.ArpeggioUp;
                card.arpeggioRate = ArpeggioRate.Eighth;
                card.arpeggioToneMode = spec.bassMode;
                card.randomRerollChance = 1f;
                card.velocityJitter = 0;
                card.hideFlags = HideFlags.HideAndDontSave;
                inMemCards.Add(card);
                specs.Add(new SmokeTrackSpec
                {
                    role = TrackRole.Bassline,
                    instrument = tmplBass.spec.instrument,
                    pattern = null,
                    style = card,
                });
            }
            if (spec.hasMelody)
            {
                specs.Add(new SmokeTrackSpec
                {
                    role = TrackRole.Melody,
                    instrument = tmplMelody.spec.instrument,
                    pattern = tmplMelody.spec.pattern, // pass-through
                    style = tmplMelody.spec.style,     // pass-through
                });
            }

            // State we force for the sweep — everything restored in finally,
            // nothing marked dirty (no silent asset writes).
            bool prevSuppress = TonalityAudit.SuppressLogs;
            bool prevEnabled = TonalityAudit.Enabled;
            bool prevLogGen = config.logGenerator;
            try
            {
                if (!verbose) config.logGenerator = false; // in-memory only

                SongConfig song;
                try
                {
                    song = SmokeSongConfigAssembler.Assemble(ctx, specs);
                }
                catch (ArgumentException ex)
                {
                    result.error = "Assembly failed: " + ex.Message;
                    return result;
                }

                var gen = new MidiGenerator(config); // ctor syncs TonalityAudit.Enabled
                TonalityAudit.Enabled = true;        // force ON for measurement
                TonalityAudit.SuppressLogs = !verbose;
                TonalityAudit.ResetCounters();

                PartRender render;
                try
                {
                    render = gen.Orchestrator.GenerateSinglePart(
                        song.Parts[0],
                        song.ChannelRoles,
                        partIndex: 0,
                        bpmOverride: ctx.bpm,
                        instrumentOverrides: null,
                        seedOverride: spec.seed,
                        patternOverrides: null,
                        mixGains: null,
                        // Backing-less cells: seed the cell's progression as
                        // the host default (BASS-SOLO-1 path) so Bass/Melody
                        // have harmony. Backing cells: null (D-SOLO-GUARD=A).
                        defaultProgression: spec.hasBacking ? null : spec.progression);
                }
                catch (Exception ex)
                {
                    result.error = "Render threw: " + ex.Message;
                    return result;
                }
                if (render == null || render.merged == null)
                {
                    result.error = "GenerateSinglePart returned null.";
                    return result;
                }

                result.bpm = render.bpm;
                result.auditCounters = TonalityAudit.SnapshotCounters();

                // Fold audit counters into per-track tallies.
                foreach (var role in TonalRoles)
                {
                    var t = result.Tally(role);
                    var label = AuditLabel[role];
                    t.auditInScale = GetCount(result.auditCounters, $"{label}|InScale");
                    t.auditYellow = GetCount(result.auditCounters, $"{label}|ChordToneChromatic");
                    t.auditRed = GetCount(result.auditCounters, $"{label}|OutOfScaleAndChord");
                }

                // Canonical re-classification (D-TON2-PARITY=A).
                CanonicalPass(spec, render, result);
                return result;
            }
            finally
            {
                TonalityAudit.SuppressLogs = prevSuppress;
                TonalityAudit.Enabled = prevEnabled;
                config.logGenerator = prevLogGen;
                foreach (var so in inMemCards)
                    if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
        }

        private static SmokeEntry FindTemplate(SmokeSetupSO setup, TrackRole role)
            => setup.entries?.FirstOrDefault(e => e?.spec != null && e.spec.role == role);

        private static int GetCount(Dictionary<string, int> c, string key)
            => c != null && c.TryGetValue(key, out var v) ? v : 0;

        // ------------------------------------------------------------------
        // Canonical re-classification + walk-approach inference
        // ------------------------------------------------------------------

        private static void CanonicalPass(CellSpec spec, PartRender render, CellResult result)
        {
            var prog = render.sharedProgressionData; // post-normalization clone
            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                result.warning = AppendNote(result.warning,
                    "No sharedProgressionData on the render — canonical pass skipped.");
                return;
            }

            var ts = spec.meter;
            var tempoMap = TempoMap.Create(
                Tempo.FromBeatsPerMinute(Math.Max(1, render.bpm)));
            long ticksPerBeat = TimeConverter.ConvertFrom(GetBeatSpan(ts), tempoMap);
            if (ticksPerBeat <= 0)
            {
                result.warning = AppendNote(result.warning, "ticksPerBeat <= 0 — canonical pass skipped.");
                return;
            }

            int beatsPerMeasure = TimeSignatureProperties[ts].BeatsPerMeasure;
            int subdivisions = Math.Max(1, prog.subdivisions);
            int totalSteps = prog.TotalSteps(beatsPerMeasure);

            var scale = GetTonalityNoteNames(spec.profile.tonality, spec.rootNote);

            var ordered = prog.events.OrderBy(e => e.startStep).ToList();
            // Canonical pcs per event, precomputed.
            var canonPcs = ordered.Select(e => CanonicalChordPcs(scale, e)).ToList();
            var canonRoots = ordered.Select(e => CanonicalRootPc(scale, e)).ToList();

            foreach (var kvp in render.stemsByMusician)
            {
                var role = kvp.Key.Role;
                if (Array.IndexOf(TonalRoles, role) < 0) continue;
                var stem = kvp.Value;
                if (stem == null) continue;
                var tally = result.Tally(role);

                foreach (var note in stem.GetNotes())
                {
                    var pc = note.NoteName;
                    bool inScale = scale.Contains(pc);
                    if (inScale) { tally.canonInScale++; continue; }

                    int evIdx = FindEventIndexAtTick(
                        ordered, note.Time, ticksPerBeat, subdivisions, totalSteps);
                    bool inChord = evIdx >= 0 && canonPcs[evIdx].Contains(pc);
                    if (inChord) { tally.canonYellow++; continue; }

                    tally.canonRed++;

                    // Walk-approach positional inference (D-TON2-WALK=B+):
                    // Bassline + ImprovisedWalk + last beat of its chord
                    // window + within 2 semitones of the NEXT event's
                    // canonical root.
                    if (role == TrackRole.Bassline &&
                        spec.hasBass &&
                        spec.bassMode == BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk &&
                        evIdx >= 0 &&
                        IsInLastBeatOfWindow(ordered[evIdx], note.Time,
                                             ticksPerBeat, subdivisions, totalSteps))
                    {
                        var nextRoot = canonRoots[(evIdx + 1) % ordered.Count];
                        if (PcDistance(pc, nextRoot) <= 2)
                            result.walkApproachInferred++;
                    }
                }
            }
        }

        /// <summary>Root pc of an event under the shared chord-identity law:
        /// TransposeNoteName(scaleNames[degree], degreeAccidental).</summary>
        private static NoteName CanonicalRootPc(
            List<NoteName> scale, ChordProgressionData.ChordEvent e)
        {
            int di = ((int)e.degree % 7 + 7) % 7;
            return TransposeNoteName(scale[di], e.degreeAccidental);
        }

        private static HashSet<NoteName> CanonicalChordPcs(
            List<NoteName> scale, ChordProgressionData.ChordEvent e)
        {
            var root = CanonicalRootPc(scale, e);
            var set = new HashSet<NoteName>();
            var intervals = GetIntervalsForQuality(e.quality);
            if (intervals == null || intervals.Length == 0)
                intervals = new[] { 0, 4, 7 };
            foreach (var iv in intervals)
                set.Add(TransposeNoteName(root, iv));
            return set;
        }

        /// <summary>Mirror of ChordProgressionData.FindChordEventAt, returning
        /// the ordered-list index (the caller needs the NEXT event too).</summary>
        private static int FindEventIndexAtTick(
            List<ChordProgressionData.ChordEvent> ordered,
            long absTicks, long ticksPerBeat, int subdivisions, int totalSteps)
        {
            if (ordered.Count == 0) return -1;
            if (totalSteps <= 0) return 0;

            double beats = absTicks / (double)ticksPerBeat;
            int step = (int)Math.Floor(beats * subdivisions);
            step %= totalSteps;
            if (step < 0) step += totalSteps;

            int best = -1;
            for (int i = 0; i < ordered.Count; i++)
            {
                var e = ordered[i];
                if (step < e.startStep) break;
                if (step >= e.startStep && step < e.startStep + e.lengthSteps)
                    return i;
                best = i;
            }
            return best >= 0 ? best : ordered.Count - 1;
        }

        private static bool IsInLastBeatOfWindow(
            ChordProgressionData.ChordEvent e,
            long absTicks, long ticksPerBeat, int subdivisions, int totalSteps)
        {
            double beats = absTicks / (double)ticksPerBeat;
            int step = (int)Math.Floor(beats * subdivisions);
            if (totalSteps > 0)
            {
                step %= totalSteps;
                if (step < 0) step += totalSteps;
            }
            int endStep = e.startStep + e.lengthSteps;
            return step >= endStep - subdivisions; // final beat = last 'subdivisions' steps
        }

        private static int PcDistance(NoteName a, NoteName b)
        {
            int d = Math.Abs((int)a - (int)b) % 12;
            return Math.Min(d, 12 - d);
        }

        private static string AppendNote(string existing, string note)
            => string.IsNullOrEmpty(existing) ? note : existing + " | " + note;

        // ------------------------------------------------------------------
        // Reports
        // ------------------------------------------------------------------

        public static (string csvPath, string mdPath) WriteReports(
            MatrixInputs inputs, List<CellSpec> cells, List<CellResult> results,
            List<string> validationNotes)
        {
            string dir = string.IsNullOrEmpty(inputs.outputDir)
                ? Path.Combine(Application.persistentDataPath, "TonalityMatrix")
                : inputs.outputDir;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string csvPath = Path.Combine(dir, $"tonality_matrix_{stamp}.csv");
            string mdPath = Path.Combine(dir, $"tonality_matrix_{stamp}.md");

            // ---- CSV: one row per cell ----
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",",
                "cell", "profileId", "tonality", "meter", "progression",
                "tracks", "bassMode", "backingMode", "seed", "bpm",
                "audit_in_M", "audit_yel_M", "audit_red_M",
                "audit_in_B", "audit_yel_B", "audit_red_B",
                "audit_in_K", "audit_yel_K", "audit_red_K",
                "canon_yel_M", "canon_red_M",
                "canon_yel_B", "canon_red_B",
                "canon_yel_K", "canon_red_K",
                "beliefDiv_M", "beliefDiv_B", "beliefDiv_K",
                "walkApproachInferred", "residualReds",
                "auditOrigins", "warning", "error"));
            foreach (var r in results)
            {
                var m = r.Tally(TrackRole.Melody);
                var b = r.Tally(TrackRole.Bassline);
                var k = r.Tally(TrackRole.Backing);
                string origins = string.Join(";",
                    r.auditCounters.Where(x => x.Key.Split('|').Length == 3)
                        .OrderBy(x => x.Key)
                        .Select(x => $"{x.Key}={x.Value}"));
                sb.AppendLine(string.Join(",",
                    r.spec.index,
                    Csv(r.spec.profile ? r.spec.profile.profileId : ""),
                    r.spec.profile ? r.spec.profile.tonality.ToString() : "",
                    r.spec.meter,
                    Csv(r.spec.progression ? r.spec.progression.name : ""),
                    r.spec.TracksLabel,
                    r.spec.hasBass ? r.spec.bassMode.ToString() : "",
                    r.spec.hasBacking ? r.spec.backingMode.ToString() : "",
                    r.spec.seed, r.bpm,
                    m.auditInScale, m.auditYellow, m.auditRed,
                    b.auditInScale, b.auditYellow, b.auditRed,
                    k.auditInScale, k.auditYellow, k.auditRed,
                    m.canonYellow, m.canonRed,
                    b.canonYellow, b.canonRed,
                    k.canonYellow, k.canonRed,
                    m.BeliefDiv, b.BeliefDiv, k.BeliefDiv,
                    r.walkApproachInferred, r.ResidualReds,
                    Csv(origins), Csv(r.warning ?? ""), Csv(r.error ?? "")));
            }
            File.WriteAllText(csvPath, sb.ToString());

            // ---- Markdown summary: totals + DoD verdicts ----
            var ok = results.Where(r => r.error == null).ToList();
            var failed = results.Where(r => r.error != null).ToList();
            var beliefBad = ok.Where(r => TonalRoles.Any(ro => r.Tally(ro).BeliefDiv != 0)).ToList();
            var residualBad = ok.Where(r => r.ResidualReds > 0).ToList();
            var md = new StringBuilder();
            md.AppendLine($"# Tonality regression matrix — {stamp}");
            md.AppendLine();
            md.AppendLine($"Seed (all cells): {inputs.seed} · cells planned: {cells.Count} · " +
                          $"run: {results.Count} · failed: {failed.Count}");
            if (validationNotes != null && validationNotes.Count > 0)
            {
                md.AppendLine();
                md.AppendLine("Validation notes:");
                foreach (var n in validationNotes) md.AppendLine($"- {n}");
            }
            md.AppendLine();
            md.AppendLine("## DoD verdicts");
            md.AppendLine();
            md.AppendLine(beliefBad.Count == 0 && residualBad.Count == 0 && failed.Count == 0
                ? "- **(1) Chord-pitch-class parity: HOLDS** on every rendered cell " +
                  "(beliefDiv == 0 for every track; no canonical red outside the " +
                  "walk-approach inference)."
                : $"- **(1) Parity: SUSPECT** — {beliefBad.Count} cell(s) with " +
                  "beliefDiv != 0 (composer belief diverges from canonical chord).");
            md.AppendLine(residualBad.Count == 0
                ? "- **(2) No profile or track combination produces reds beyond " +
                  "bass walk approach notes.**"
                : $"- **(2) {residualBad.Count} cell(s) with residual reds** — " +
                  "defect candidates for a follow-up batch (this batch reports only):");
            foreach (var r in residualBad.Take(40))
                md.AppendLine($"  - {r.spec} → residual={r.ResidualReds} " +
                              $"(walkApproachInferred={r.walkApproachInferred})");
            if (residualBad.Count > 40)
                md.AppendLine($"  - … {residualBad.Count - 40} more (see CSV).");
            foreach (var r in beliefBad.Take(40))
                md.AppendLine($"  - beliefDiv: {r.spec} → M={r.Tally(TrackRole.Melody).BeliefDiv} " +
                              $"B={r.Tally(TrackRole.Bassline).BeliefDiv} " +
                              $"K={r.Tally(TrackRole.Backing).BeliefDiv}");
            foreach (var r in failed.Take(40))
                md.AppendLine($"  - FAILED: {r.spec} → {r.error}");
            md.AppendLine();
            md.AppendLine($"Full per-cell table: `{Path.GetFileName(csvPath)}` (same folder).");
            File.WriteAllText(mdPath, md.ToString());

            return (csvPath, mdPath);
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;   // ITimeSpan
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Composition
{
    /// Backing/chord track composer.
    /// - Voices chords via injected IChordVoicer (or simple realization if disabled)
    /// - Repeats progression to fill the part
    /// - Stamps "chd:..." meta tags
    /// - Sets bank/patch on ALL chunks and forces channel on ALL ChannelEvents
    public sealed class ChordTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly IChordVoicer _voicer;
        private readonly VoiceLeadingConfig _vl;

        private readonly struct DiaChord
        {
            public readonly ScaleDegree degree;
            public readonly ChordQuality quality;
            public readonly NoteName root;
            public readonly string roman;
            public readonly string symbol;
            public DiaChord(ScaleDegree d, ChordQuality q, NoteName r,
                string rn, string sym)
            { degree = d; quality = q; root = r; roman = rn; symbol = sym; }
        }

        private struct VampRuntime
        {
            public List<int> degreesSequence;
            public int barsRemaining;
        }

        // MGP-ALWTTT-MOD-DIR-1.1: optional cross-render memory hooks supplied by
        // ChordTrackComposerFactory. The composer itself is stateless and lives
        // for one Compose call; the factory holds the dictionary keyed by
        // (part.Name, trackCfg.MusicianId) and injects/collects values via these.
        private readonly int? _previousFirstChordPitch;
        private readonly Action<int> _reportFirstChordPitch;

        // CA-T1: Tier-1 chord articulation seam. Stateless and RNG-free by
        // contract (velocity/timing are pure functions of beat position), so a
        // single shared instance serves both render loops. Block emits the
        // exact legacy MoveToTime+Chord pair (bit-identical when unset).
        // See runtime/SSoT_Composer_Backing_Track.md §8.
        private static readonly IChordArticulator _articulator = new ChordArticulator();
        // CA-T2 (D-T2-SEAM=B): stateless, RNG-free voicing reshaper; runs between
        // VoiceChord and the articulator at BOTH emission sites.
        private static readonly IChordReshaper _reshaper = new ChordReshaper();

        public ChordTrackComposer(
            MidiGenPlayConfig settings,
            IChordVoicer voicer,
            int? previousFirstChordPitch = null,
            Action<int> reportFirstChordPitch = null)
        {
            _settings = settings;
            _voicer = voicer;
            _vl = settings != null ? settings.voiceLeading : null;
            _previousFirstChordPitch = previousFirstChordPitch;
            _reportFirstChordPitch = reportFirstChordPitch;
        }


        // ---------------------------------------------------------------------
        // TS normalization (Normalized Bar Time) — runtime reprojection (no asset mutation)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates a runtime clone if the progression was authored under a different TS (or if we need to upsample
        /// harmonic subdivisions). Events are reprojected by normalized bar time (fraction of a measure):
        ///   bars = step / stepsPerMeasure(src)
        ///   stepDst = Quantize(bars * stepsPerMeasure(dst))
        ///
        /// Durations are rebuilt from start anchors to avoid overlaps/gaps after quantization.
        /// </summary>
        private ChordProgressionData NormalizeProgressionForPartIfNeeded(
    SongConfig.PartConfig part,
    ChordProgressionData srcProg)
        {
            if (part == null || srcProg == null) return srcProg;

            int srcSub = Mathf.Max(1, srcProg.subdivisions);
            int minSub = Mathf.Max(1, _settings != null ? _settings.minHarmonicSubdivisions : 4);
            int dstSub = Mathf.Max(srcSub, minSub);

            var srcTS = srcProg.TimeSignature;
            var dstTS = part.TimeSignature;

            bool tsChanged = srcTS != dstTS;
            bool subChanged = dstSub != srcSub;

            if (!tsChanged && !subChanged)
                return srcProg;

            int measures = Mathf.Max(1, srcProg.Measures);

            int srcStepsPerMeasure = StepsPerMeasure(srcTS, srcSub);
            int dstStepsPerMeasure = StepsPerMeasure(dstTS, dstSub);

            int srcTotal = Mathf.Max(1, srcStepsPerMeasure * measures);
            int dstTotal = Mathf.Max(1, dstStepsPerMeasure * measures);

            var dst = ScriptableObject.CreateInstance<ChordProgressionData>();
            dst.DisplayName = srcProg.DisplayName;
            dst.TimeSignature = dstTS;
            dst.Measures = measures;
            dst.subdivisions = dstSub;

            dst.originalInput = srcProg.originalInput;

            // RUNTIME-REQUALITY (F-NORM-DROP fix): the TS/subdivision
            // reprojection builds dst field-by-field rather than cloning, so
            // EVERY new ChordProgressionData field must be copied here or it
            // silently reverts to its default on the runtime clone. Dropping
            // qualityRenderPolicy made requality a no-op for any progression
            // that needed normalization (i.e. almost all of them: authoring
            // writes sub x1, the composer wants x4).
            dst.qualityRenderPolicy = srcProg.qualityRenderPolicy;

            // HARMONY-PURE-1 additions to the copy list (same F-NORM-DROP
            // hazard): the color-table opt-in and the cadence metadata.
            dst.useColorTable = srcProg.useColorTable;
            dst.cadence = srcProg.cadence;

            dst.songReferences = srcProg.songReferences != null
                ? new List<string>(srcProg.songReferences)
                : new List<string>();
            dst.tonalities = srcProg.tonalities != null
                ? new List<Tonality>(srcProg.tonalities)
                : new List<Tonality>();
            dst.events = new List<ChordProgressionData.ChordEvent>();

            if (srcProg.events == null || srcProg.events.Count == 0)
                return dst;

            var mapped = new List<(int start, double bars, ChordProgressionData.ChordEvent ev)>(srcProg.events.Count);

            foreach (var e in srcProg.events.OrderBy(x => x.startStep))
            {
                int s = e.startStep;

                s %= srcTotal;
                if (s < 0) s += srcTotal;

                double bars = s / (double)srcStepsPerMeasure;
                int startDst = (int)System.Math.Round(bars * dstStepsPerMeasure, MidpointRounding.AwayFromZero);
                startDst = Mathf.Clamp(startDst, 0, dstTotal - 1);

                mapped.Add((startDst, bars, e));
            }

            mapped = mapped.OrderBy(m => m.start).ThenBy(m => m.bars).ToList();

            var uniq = new List<(int start, ChordProgressionData.ChordEvent ev)>(mapped.Count);
            int lastStart = -1;
            foreach (var m in mapped)
            {
                if (m.start == lastStart) continue;
                uniq.Add((m.start, m.ev));
                lastStart = m.start;
            }

            if (uniq.Count == 0)
                return dst;

            for (int i = 0; i < uniq.Count; i++)
            {
                int start = uniq[i].start;
                int end = (i + 1 < uniq.Count) ? uniq[i + 1].start : dstTotal;
                int len = Mathf.Max(1, end - start);

                var se = uniq[i].ev;

                dst.events.Add(new ChordProgressionData.ChordEvent
                {
                    startStep = start,
                    lengthSteps = len,
                    degree = se.degree,
                    quality = se.quality,
                    velocity = se.velocity,
                    isDiatonic = se.isDiatonic,
                    degreeAccidental = se.degreeAccidental,
                    // SECDOM-1 (F-NORM-DROP): the secondary-dominant opt-in
                    // must survive the field-by-field reprojection or the
                    // primitive silently dies on any normalized progression.
                    hasAppliedTarget = se.hasAppliedTarget,
                    appliedTarget = se.appliedTarget
                });
            }

            if (_settings?.logGenerator == true)
            {
                Debug.Log(
                    $"[ChordTrackComposer] NormalizedBarTime reprojection: '{srcProg.DisplayName}' " +
                    $"TS {srcTS} → {dstTS} | sub x{srcSub} → x{dstSub} | measures={measures} " +
                    $"| tsChanged={tsChanged} subChanged={subChanged}");
            }

            return dst;
        }

        /// <summary>
        /// Creates a backing/chord MIDI track for the given part/track config.
        /// If a ChordProgressionData is available (authored or cached), renders it;
        /// otherwise builds a procedural progression and renders that.
        /// </summary>
        /// <param name="part">Song part (tonality, meter, measures, tempo range).</param>
        /// <param name="cfg">Track configuration (instrument, parameters/pattern).</param>
        /// <param name="bpm">Beats per minute for this part repetition.</param>
        /// <param name="channel">MIDI channel (0..15) assigned by the orchestrator.</param>
        /// <param name="ctx">Cross-track context (rng, voicer, progression cache, helpers).</param>
        /// <returns>MIDI file (one or more chunks) containing the backing track.</returns>
        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var instrument = (MIDIInstrumentSO)cfg.Instrument;

            // Transient composer hints: snapshot and immediately clear from PartConfig
            // so each is consumed exactly once regardless of which render path runs
            // below. Defaults (Auto + null previous root; null inversion hints) are
            // no-ops (current behavior, bit-identical).
            var modulationHint = part.ModulationOctaveHint;
            var previousRoot = part.PreviousRootNote;
            var inversionHints = part.ChordInversionHints; // CQ-A1-OBJ2 per-chord pin (§7)
            part.ModulationOctaveHint = ModulationOctaveHint.Auto;
            part.PreviousRootNote = null;
            part.ChordInversionHints = null;

            // 0) Resolve card style (BackingCardConfigSO) from TrackParameters.Style
            var backingStyle = cfg.Parameters?.Style as BackingCardConfigSO;
            var effectiveVL = backingStyle?.voiceLeadingOverride ?? _vl;

            // CA-T1 (D-EXP1=A): persistent per-card expression selection, applied
            // to the whole render. Not a transient hint (§6/§7 lifecycle does NOT
            // apply); no snapshot-and-clear. Absent card => Block (legacy).
            var chordExpression = backingStyle != null
                ? backingStyle.chordExpression : ChordExpressionType.Block;
            var arpeggioRate = backingStyle != null
                ? backingStyle.arpeggioRate : ArpeggioRate.Eighth;

            // MGP-ALWTTT-ARTIC-1 (D1..D6, SD-1..3 = A): Random selection
            // policy, resolved composer-side per chord event from a DEDICATED
            // stream derived off the per-track seed (SEED-1 chain:
            // ResolveArticulationSeed(ctx.trackSeed)) — never ctx.rng (CA-T1
            // shared-stream hazard: voicing draws are untouched, so toggling
            // Fixed<->Random changes articulation only, never voicings). The
            // articulator stays RNG-free and never sees Random. Null roller
            // (any fixed figure) => CA-T1 behavior, bit-identical.
            int trackSeed = ctx != null ? ctx.trackSeed : 0;

            RandomArticulationRoller articRoller = null;
            if (chordExpression == ChordExpressionType.Random ||
                arpeggioRate == ArpeggioRate.Random)
            {
                // CA-V1: both streams are constructed whenever EITHER sentinel is
                // selected. The unused one costs one System.Random and is never
                // drawn from, which keeps the two axes independent: a fixed
                // figure + Random rate consumes zero figure draws.
                articRoller = new RandomArticulationRoller(
                    new System.Random(SongOrchestrator.ResolveArticulationSeed(trackSeed)),
                    backingStyle != null ? backingStyle.randomRerollChance : 1f,
                    backingStyle != null ? backingStyle.randomFigureWeights : null,
                    new System.Random(SongOrchestrator.ResolveArticulationRateSeed(trackSeed)));
            }

            // MGP-ARTIC-RATE-1 (D-MGP-ARTIC-2=B): assertion, not a degrade path.
            // §8.5 states the articulator NEVER receives either sentinel; the
            // roller gate above is what makes that true. If this fires, a
            // sentinel is about to be swallowed by the articulator's defensive
            // degrade (Block / Eighth) — the F-ARTIC-RATE-GRID-1 failure mode,
            // which shipped silently. Once per render, never per event.
            if (articRoller == null &&
                (chordExpression == ChordExpressionType.Random ||
                 arpeggioRate == ArpeggioRate.Random))
            {
                Debug.LogWarning(
                    "[ChordTrackComposer] Unresolved articulation sentinel " +
                    $"(expression={chordExpression}, rate={arpeggioRate}) with no " +
                    "roller: the articulator will degrade it (Block / Eighth). " +
                    "See runtime/SSoT_Composer_Backing_Track.md §8.5.");
            }

            // CA-V1 (D-V1-JIT-SRC=A): render-level jitter policy. NOT a stream —
            // a seed for a pure per-(event, hit) mix, so the articulator stays
            // RNG-free and ctx.rng is untouched. Amount 0 (default / no card) is
            // exact identity.
            var velocityJitter = new VelocityJitter(
                backingStyle != null ? backingStyle.velocityJitter : 0,
                SongOrchestrator.ResolveVelocityJitterSeed(trackSeed));

            // MGP-ALWTTT-DBG-1 (Ask A): readback payload for this render;
            // content fields fill as resolution progresses, identity is
            // stamped by the orchestrator's sink. Reported at most once, at
            // each return path.
            var resolvedChoice = new ResolvedTrackChoice();

            // 0) MGP-ALWTTT-DBG-3 (Ask C, D-DBG4=A) — precedence STEP 0: a
            // per-render override installed on the context wins over the card
            // pick, the shared cache and TrackParameters.Pattern.
            // Clone-on-apply; type mismatch = warn + ignore. Shared with the
            // other tracks under the SAME don't-overwrite discipline as the
            // card override below (the progression is deliberately shared
            // state — overriding backing IS overriding the part's harmony).
            ChordProgressionData prog = null;
            var renderOverride = ctx?.patternOverride;
            if (renderOverride != null)
            {
                if (renderOverride is ChordProgressionData overrideProg)
                {
                    prog = ScriptableObject.Instantiate(overrideProg); // clone-on-apply
                    resolvedChoice.source = ResolvedSource.RenderOverride;
                    resolvedChoice.sourceAssetName = overrideProg.name; // pre-clone (D-DBG3)

                    // Ask C / D-DBG4=A: the per-render override is precedence
                    // step 0 — the max-authority source for the whole part. It
                    // must IMPOSE the shared progression unconditionally (unlike
                    // the card-override path below, whose "don't overwrite"
                    // guard exists to avoid stepping on another track): otherwise
                    // GetProgressionForPart's authored fallback
                    // (FindProgressionForPart -> the Backing track's Pattern)
                    // keeps returning the pre-override progression and the bass /
                    // other shared-progression consumers diverge from the backing.
                    // Track order runs Backing before Bassline, so the bass sees
                    // the overridden progression.
                    ctx?.SetProgressionForPart?.Invoke(part, prog);

                    if (_settings?.logGenerator == true)
                    {
                        Debug.Log(
                            $"<color=green>[ChordTrackComposer]</color> " +
                            $"Per-render progression override used: '{overrideProg.name}' " +
                            $"for part='{part.Name}'.");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[ChordTrackComposer] patternOverride type mismatch for role " +
                        $"Backing: expected ChordProgressionData, got " +
                        $"{renderOverride.GetType().Name} ('{renderOverride.name}'). " +
                        $"Ignoring override.");
                }
            }

            // 1) Card-level progression override (if any)
            if (prog == null && backingStyle != null)
            {
                var rng = ctx?.rng ?? new System.Random();
                prog = backingStyle.PickProgressionOverride(
                    rng,
                    part.TimeSignature,
                    _settings,
                    out var pickInfo,
                    verbose: _settings?.logGenerator == true
                );

                if (prog != null)
                {
                    resolvedChoice.source = pickInfo.fromPalette
                        ? ResolvedSource.CardPalette : ResolvedSource.CardOverride;
                    resolvedChoice.sourceAssetName = pickInfo.sourceAssetName;
                    resolvedChoice.paletteName = pickInfo.paletteName;

                    // Share override with other tracks (melody, bass, etc.)
                    // but don't overwrite if some other system already set it.
                    if (ctx?.GetProgressionForPart?.Invoke(part) == null)
                    {
                        ctx?.SetProgressionForPart?.Invoke(part, prog);
                    }

                    if (_settings?.logGenerator == true)
                    {
                        Debug.Log(
                            $"<color=green>[ChordTrackComposer]</color> " +
                            $"Card-level progression override used: '{prog.DisplayName}' " +
                            $"for part='{part.Name}'.");
                    }
                }
            }

            // 2) If still null, use cached or explicitly authored pattern
            if (prog == null)
            {
                var cached = ctx?.GetProgressionForPart?.Invoke(part);
                if (cached != null)
                {
                    prog = cached;
                    resolvedChoice.source = ResolvedSource.SharedProgression;
                    resolvedChoice.sourceAssetName = cached.name;
                }
                else if (cfg.Parameters?.Pattern is ChordProgressionData trackProg)
                {
                    prog = trackProg;
                    resolvedChoice.source = ResolvedSource.TrackParameters;
                    resolvedChoice.sourceAssetName = trackProg.name;
                }
            }

            // 2a*) MGP-MEL-1 P4 (D3=C / D4=A) -- AdoptProgressionTonality.
            // Card-level opt-in: the card DELEGATES the part's tonality to
            // the progression it resolved. When that progression declares
            // reference tonalities that EXCLUDE the part's, the part adopts
            // the FIRST listed tonality (deterministic, zero rng draws; the
            // root note is unchanged -- mode change only, mirroring the
            // host-side TonalityEffect surface). Runs BEFORE 2b (so the
            // TONFILTER-1 mismatch signal does not fire for an adopted
            // render) and BEFORE 2c (so TS normalization / requality see the
            // FINAL tonality). Backing composes in PASS 0
            // (MGP-ALWTTT-BASS-ORDER-1), so bass / melody / harmony read the
            // adopted tonality via part.Tonality / GetTonalityProfileForPart.
            // D4=A precedence contract: compose-time adoption deliberately
            // wins over any pre-render tonality the host set for the part
            // (incl. an explicit TonalityEffect); combining both on one card
            // is an authoring error the HOST validates -- the composer cannot
            // distinguish "default tonality" from "effect-pinned tonality".
            if (backingStyle != null && backingStyle.adoptProgressionTonality &&
                prog != null && prog.tonalities != null &&
                prog.tonalities.Count > 0 &&
                !prog.tonalities.Contains(part.Tonality))
            {
                var adopted = prog.tonalities[0];
                var previous = part.Tonality;
                part.Tonality = adopted;

                resolvedChoice.tonalityAdopted = true;
                resolvedChoice.adoptedTonality = adopted;

                if (_settings?.logGenerator == true)
                {
                    Debug.Log(
                        $"<color=green>[ChordTrackComposer]</color> " +
                        $"AdoptProgressionTonality: part '{part.Name}' " +
                        $"{previous} -> {adopted} (progression " +
                        $"'{prog.DisplayName}' authored for " +
                        $"[{string.Join(", ", prog.tonalities)}]; card " +
                        $"'{backingStyle.name}' opts in; root unchanged).");
                }
            }

            // 2b) TONFILTER-1 (D-B2-1=C): the part's tonality is card
            // authority and is NEVER reverted by the progression asset.
            // ChordProgressionData.tonalities is descriptive metadata
            // (authoring reference / import provenance), not a runtime
            // veto. The pre-B2 alignment here consumed one ctx.rng draw
            // and overrode part.Tonality whenever the asset's list
            // excluded it — defeating both card authority and
            // RUNTIME-REQUALITY (which runs in 2c against the FINAL part
            // tonality and exists precisely to adapt out-of-reference
            // assets). Removing it removes that draw ONLY on renders
            // where the revert used to fire; all other rng streams are
            // unchanged (SEED-1 goldens are the detector).
            //
            // Conflict signal (D-B2-2=B): when the asset's authored
            // tonalities exclude the part tonality AND the asset renders
            // AsAuthored (no requality adaptation), say so — via readback
            // (testable) and a logGenerator-gated warning — instead of
            // failing silently through a filter. Pure: zero rng draws.
            if (prog != null && prog.tonalities != null && prog.tonalities.Count > 0 &&
                !prog.tonalities.Contains(part.Tonality) &&
                prog.qualityRenderPolicy ==
                    ChordProgressionData.QualityRenderPolicy.AsAuthored)
            {
                resolvedChoice.tonalityMismatch = true;

                if (_settings?.logGenerator == true)
                {
                    Debug.LogWarning(
                        $"[ChordTrackComposer] Progression '{prog.DisplayName}' was " +
                        $"authored for [{string.Join(", ", prog.tonalities)}] but part " +
                        $"'{part.Name}' is {part.Tonality}; rendering AsAuthored in the " +
                        $"part's tonality (card wins). Consider " +
                        $"qualityRenderPolicy=DiatonicToPart on the asset if it should " +
                        $"adapt diatonically.");
                }
            }


            // 2c) TS normalization (bar-normalized reprojection + min harmonic subdivisions)
            // If the progression was authored in a different TS (or has too-low subdivisions),
            // create a runtime clone and reproject its events to the Part TS.
            if (prog != null)
            {
                if (_settings?.logGenerator == true)
                    Debug.Log($"[ChordTrackComposer] PRE-NORM progTS={prog.TimeSignature} sub={prog.subdivisions} partTS={part.TimeSignature}");

                var runtimeProg = NormalizeProgressionForPartIfNeeded(part, prog);

                // RUNTIME-REQUALITY (D-RQ-SITE): diatonic re-resolution for
                // opt-in assets (qualityRenderPolicy == DiatonicToPart), applied
                // to the runtime clone AFTER step 2b so the FINAL part tonality
                // (including any tonality-filter alignment) is used, and INSIDE
                // the same clone/publication step as TS normalization so the
                // don't-overwrite publication guard below still compares against
                // the ORIGINAL prog and every shared-channel consumer (bass,
                // melody) sees the requalified data. AsAuthored (default) is a
                // same-reference no-op. Pure: zero rng draws.
                var requalified = ChordProgressionRequality.ApplyDiatonicRequality(
                    runtimeProg, part.Tonality);
                if (!ReferenceEquals(requalified, runtimeProg) &&
                    _settings?.logGenerator == true)
                {
                    Debug.Log(
                        $"<color=cyan>[ChordTrackComposer]</color> RUNTIME-REQUALITY " +
                        $"applied for part='{part.Name}' (tonality={part.Tonality}, " +
                        $"progression='{requalified.DisplayName}').");
                }
                runtimeProg = requalified;

                bool changed = !ReferenceEquals(runtimeProg, prog);

                // Upgrade cache only if it is empty or points to the same (un-normalized) progression.
                if (changed)
                {
                    var cached = ctx?.GetProgressionForPart?.Invoke(part);
                    if (cached == null || ReferenceEquals(cached, prog))
                        ctx?.SetProgressionForPart?.Invoke(part, runtimeProg);
                }

                prog = runtimeProg;

                if (_settings?.logGenerator == true)
                    Debug.Log($"[ChordTrackComposer] POST-NORM progTS={prog.TimeSignature} sub={prog.subdivisions} changed={changed}");
            }

            if (_settings?.logGenerator == true)
            {
                var progName = prog?.DisplayName ?? "(null)";
                var vlName = effectiveVL != null ? effectiveVL.name : "(none)";
                Debug.Log($"<color=green>[ChordTrackComposer]</color> part='{part.Name}' " +
                          $"inst='{instrument?.InstrumentName}' bpm={bpm} ch={channel} " +
                          $"progression='{progName}' evts={prog?.events?.Count ?? 0} " +
                          $"VL='{vlName}' " +
                          $"(card override=" +
                          $"{backingStyle != null && backingStyle.voiceLeadingOverride != null})");
            }

            // degree + quality → chord pcs
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7)
                            .Select(n => n.NoteName)
                            .ToArray();

            if (_settings?.logGenerator == true)
            {
                var spelled = Enumerable.Range(0, 7)
                    .Select(i => SpellNoteForDegree(scaleNames[i], part.RootNote, i))
                    .ToArray();
                Debug.Log($"<color=yellow>[ChordTrack] Tonality: {part.Tonality} over {part.RootNote}  " +
                          $"Scale labels: [{string.Join(", ", spelled)}]</color>");
            }

            var (triads, sevenths) = BuildDiatonicSets(part.Tonality, part.RootNote);
            if (_settings?.logGenerator == true)
                LogDiatonicSets(part.Tonality, part.RootNote, triads, sevenths, showSymbols: false);

            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                if (_settings?.logGenerator == true)
                    Debug.Log("[ChordTrackComposer] Procedural backing (no ChordProgressionData).");
                var proceduralFile = ComposeProcedural(instrument, bpm, part, cfg, ctx, channel, effectiveVL,
                         modulationHint, previousRoot, inversionHints,
                         chordExpression, arpeggioRate, articRoller, velocityJitter);

                // Ask A: procedural path. The built progression was cached by
                // ComposeProcedural via SetProgressionForPart; the resolved
                // figure sequence (Random only) is the roller's history (ID-3:
                // the roller's existing observability state — no new roller
                // responsibilities, no extra draws).
                resolvedChoice.source = ResolvedSource.Procedural;
                resolvedChoice.sourceAssetName = null;
                resolvedChoice.progressionRoman =
                    RomanSequence(ctx?.GetProgressionForPart?.Invoke(part));
                resolvedChoice.resolvedFigures = SnapshotRolls(articRoller);
                ctx?.ReportResolved?.Invoke(resolvedChoice);

                return proceduralFile;
            }

            // Grid info
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(part.TimeSignature);
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int partTotalSteps = Mathf.Max(1, part.Measures) * stepsPerMeasure;
            int patternMeasures = Mathf.Max(1, prog.Measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int numRepeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            int coveredSteps = 0;
            if (prog.events != null && prog.events.Count > 0)
            {
                coveredSteps = prog.events.Max(e =>
                    Mathf.Max(0, e.startStep) + Mathf.Max(1, e.lengthSteps));
            }

            int tailSteps = Mathf.Max(0, patternTotalSteps - coveredSteps);

            if (_settings?.logGenerator == true)
            {
                var tempoMapForGrid = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
                long ticksPerBeat = TimeConverter.ConvertFrom(beatSpan, tempoMapForGrid);
                long lenTicksExpected = ticksPerBeat * beatsPerBar * Mathf.Max(1, part.Measures);

                Debug.Log(
                    $"[ChordTrackComposer] GRID part='{part.Name}' " +
                    $"partTS={part.TimeSignature} progTS={prog.TimeSignature} " +
                    $"stepsPerBeat={stepsPerBeat} stepsPerMeasure={stepsPerMeasure} " +
                    $"patternMeasures={patternMeasures} patternTotalSteps={patternTotalSteps} " +
                    $"partTotalSteps={partTotalSteps} repeats={numRepeats} " +
                    $"coveredSteps={coveredSteps} tailSteps={tailSteps} " +
                    $"lenTicksExpected={lenTicksExpected}");
            }

            var chordMarkers =
                new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            var pb = new PatternBuilder();

            // Choose voicer
            var voicer = ctx?.ChordVoicer ?? _voicer;
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            Debug.Log($"[ChordTrackComposer] " +
                $"Voice Leading Check: Config={(effectiveVL != null)}, " +
                $"Enabled={(effectiveVL?.enableVoiceLeading)}, " +
                $"Voicer={(voicer != null)}");

            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int repeatStepOffset = repeat * patternTotalSteps;
                bool repeatIsFirst = (repeat == 0);
                int eventIndex = 0;

                foreach (var e in prog.events)
                {
                    var degreeRoot = scaleNames[(int)e.degree];
                    degreeRoot = TransposeNoteName(degreeRoot, e.degreeAccidental);
                    var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                    // First chord of the render: directional override if requested.
                    // D3 (CQ-A1-OBJ2): when the directional hint produces this chord,
                    // VoiceChord is never invoked for it, so an inversion pin at
                    // position 0 is inherently ignored on this one chord only. On
                    // later repeats, position 0's pin applies normally (D2a=a).
                    IReadOnlyList<DryWetMidiNote> playable = null;
                    bool isFirstChord = repeatIsFirst && (eventIndex == 0);
                    if (isFirstChord)
                    {
                        playable = TryDirectionalFirstChord(
                            chordPcs, degreeRoot, instrument,
                            modulationHint, previousRoot,
                            _previousFirstChordPitch, _settings);
                    }

                    if (playable == null)
                    {
                        // CQ-A1-OBJ2: sticky-per-position pin — resolved from the
                        // event position only, so it recurs on every repeat (§7).
                        int? inversionPin = ResolveInversionPin(inversionHints, eventIndex);
                        playable =
                            (effectiveVL != null && effectiveVL.enableVoiceLeading && voicer != null)
                            ? voicer.VoiceChord(chordPcs, instrument, lastVoicing, effectiveVL, inversionPin, ctx?.rng) // VL-DET-1
                            : RealizeChordSimple(chordPcs, instrument, ctx?.rng);
                    }

                    // MGP-ALWTTT-MOD-DIR-1.1: stash the actual first-chord root pitch
                    // so the factory's per-track memory anchors the NEXT render. Runs
                    // unconditionally for the first chord, whichever branch produced it.
                    if (isFirstChord && _reportFirstChordPitch != null)
                    {
                        int rootPitch = FindLowestRootPitch(playable, degreeRoot);
                        if (rootPitch != int.MinValue) _reportFirstChordPitch(rootPitch);
                    }

                    lastVoicing = playable;

                    var rn = ToRomanRich(e.degree, e.quality);
                    if (e.degreeAccidental < 0) rn = "b" + rn;
                    else if (e.degreeAccidental > 0) rn = "#" + rn;

                    var sym = GetChordSymbol(degreeRoot, e.quality);
                    int degIdx = ((int)e.degree) + 1;
                    string q = e.quality.ToString();

                    int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                    double startBeats = (double)startStepAbs / stepsPerBeat;
                    double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                    var startTime = beatSpan.Multiply(startBeats);

                    // CA-T1: single unconditional articulation call — the SAME
                    // line at both emission sites. MGP-ARTIC-RATE-1: the SAME
                    // ARGUMENTS too. §8.4's both-sites guarantee is about the
                    // resolved values reaching Emit, not just the call shape;
                    // this site drifted when CA-V1 widened the roller gate
                    // (F-ARTIC-RATE-GRID-1..3).
                    var effectiveExpression =
                        articRoller != null &&
                        chordExpression == ChordExpressionType.Random
                            ? articRoller.NextFigure() : chordExpression;
                    // CA-V1: independent axis on its own substream. A fixed
                    // figure with a Random rate rolls rates only, and vice versa
                    // — and consumes ZERO figure draws (§8.5).
                    var effectiveRate =
                        articRoller != null &&
                        arpeggioRate == ArpeggioRate.Random
                            ? articRoller.NextRate() : arpeggioRate;

                    var emitVoicing = _reshaper.Reshape(playable, chordPcs, effectiveExpression);

                    _articulator.Emit(pb, emitVoicing, startBeats, durBeats, beatSpan,
                                      beatsPerBar, e.velocity, stepsPerBeat,
                                      effectiveExpression, effectiveRate,
                                      velocityJitter.ForEvent(eventIndex));

                    chordMarkers.Add((startTime, rn, sym, degIdx, q));
                    eventIndex++;
                }
            }

            // MGP-ALWTTT-ARTIC-1 observability: the resolved figure sequence for
            // this render (logging only; no draws, no semantic effect).
            if (_settings?.logGenerator == true && articRoller != null)
            {
                Debug.Log($"<color=#c9f>[ChordTrackComposer]</color> ARTIC-1 roll " +
                          $"(grid) part='{part.Name}' {articRoller.DescribeRolls()}");
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            // Chord tags
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);

            // Bank/Patch on ALL chunks + force channel on ALL ChannelEvents
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings != null && _settings.logGenerator)
            {
                var chunks = file.GetTrackChunks().Count();
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[ChordTrackComposer] tracks={chunks} notes={notes} lastTick={lastTick}");
            }

            // Ask A: grid path. Roman sequence uses grid-site formatting
            // (accidental-prefixed); figures are the roller's history.
            resolvedChoice.progressionRoman = RomanSequence(prog);
            resolvedChoice.resolvedFigures = SnapshotRolls(articRoller);
            ctx?.ReportResolved?.Invoke(resolvedChoice);

            return file;
        }

        /// <summary>
        /// MGP-ALWTTT-DBG-1 (Ask A): compact roman-numeral sequence of a
        /// progression, formatted exactly like the grid emission site
        /// (accidental prefix "b"/"#"). Internal so BassTrackComposer's
        /// readback reuses the same formatting.
        /// </summary>
        internal static string RomanSequence(ChordProgressionData prog)
        {
            if (prog == null || prog.events == null || prog.events.Count == 0)
                return null;

            return string.Join(" ", prog.events.Select(e =>
            {
                var rn = ToRomanRich(e.degree, e.quality);
                if (e.degreeAccidental < 0) rn = "b" + rn;
                else if (e.degreeAccidental > 0) rn = "#" + rn;
                return rn;
            }));
        }

        /// <summary>
        /// MGP-ALWTTT-DBG-1 (Ask A / ID-3): snapshot of the roller's resolved
        /// figure history (emission order). Null roller (fixed articulation)
        /// => null. Copy, not the live list.
        /// </summary>
        private static List<ChordExpressionType> SnapshotRolls(
            RandomArticulationRoller roller)
            // CA-V1: a rate-only Random render builds a roller whose FIGURE
            // history stays empty. The DBG-1 contract is "fixed articulation
            // reports null figures", so an empty history must report null too —
            // R4: the readback is deliberately NOT extended to rates or jitter.
            => roller == null || roller.History.Count == 0
                ? null
                : new List<ChordExpressionType>(roller.History);

        /// <summary>
        /// Procedural path: builds a per-bar chord progression using modal rules
        /// (TonalityProfileSO if available), caches it in GenContext so other tracks
        /// can reuse it, then renders it.
        /// </summary>
        /// <param name="instrument">Instrument to voice the chords on.</param>
        /// <param name="bpm">Tempo for this part repetition.</param>
        /// <param name="part">Part info (tonality, measures, time signature).</param>
        /// <param name="cfg">Track config (mostly for logging / range).</param>
        /// <param name="ctx">Per-repetition context (rng, voicer, progression cache).</param>
        /// <param name="channel">MIDI channel for this track.</param>
        /// <returns>MIDI file containing the rendered procedural backing track.</returns>
        private MidiFile ComposeProcedural(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            MidiGenerator.GenContext ctx,
            int channel,
            VoiceLeadingConfig vlOverride,
            ModulationOctaveHint modulationHint,
            Melanchall.DryWetMidi.MusicTheory.NoteName? previousRoot,
            IReadOnlyList<int?> inversionHints,
            ChordExpressionType chordExpression,
            ArpeggioRate arpeggioRate,
            RandomArticulationRoller articRoller,
            VelocityJitter velocityJitter)
        {
            var rng = ctx?.rng ?? new System.Random();

            // Build (or profile-drive) a progression.
            var prog = BuildProceduralProgression(part, ctx, rng,
                verbose: _settings.logGenerator == true);

            // Cache progression in GenContext so bass / melody / harmony can reuse.
            ctx?.SetProgressionForPart?.Invoke(part, prog);

            // Debug log
            if (_settings?.logGenerator == true && prog != null && prog.events != null)
            {
                var romanSeq = prog.events.Select(e => ToRomanRich(e.degree, e.quality));
                // TODO: Include chosen chords (degree + quality)
                Debug.Log($"[ChordTrack] Built procedural progression for part '{part.Name}': " +
                          string.Join("  ", romanSeq));
            }

            // Render using the same path as authored progressions
            return RenderFromProgression(instrument, bpm, part, prog, channel, ctx, vlOverride,
                                 modulationHint, previousRoot, inversionHints,
                                 chordExpression, arpeggioRate, articRoller, velocityJitter);
        }

        /// <summary>
        /// Inserts "chd:..." text markers with roman numeral and chord symbol for debugging/DAW display.
        /// </summary>
        /// <param name="file">Target MIDI file (first chunk is used).</param>
        /// <param name="tempoMap">Tempo map for converting musical time to ticks.</param>
        /// <param name="markers">List of (time, roman, symbol, degreeIndex, quality) tuples.</param>
        /// <param name="channel">Track MIDI channel (for embedding in the tag text).</param>
        /// <param name="verbose">If true, can emit extra logs per tag.</param>
        private static void StampChordMarkers(
            MidiFile file,
            TempoMap tempoMap,
            List<(ITimeSpan when, string roman, string symbol, int deg, string quality)> markers,
            int channel,
            bool verbose)
        {
            if (markers == null || markers.Count == 0) return;
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null) return;

            using var mgr = chunk.ManageTimedEvents();
            foreach (var cm in markers)
            {
                long tick = TimeConverter.ConvertFrom(cm.when, tempoMap);
                var txt = $"chd:{channel}:{cm.roman}:{cm.symbol}:{cm.deg}:{cm.quality}";
                mgr.Objects.Add(new TimedEvent(new TextEvent(txt), tick));
                //if (verbose) Debug.Log($"[ChordTrackComposer] tag @tick={tick} '{txt}'");
            }
        }

        /// <summary>
        /// Simple, non–voice-leading chord realization: root position within the
        /// instrument's octave range. Used when voicer is disabled or null.
        /// </summary>
        /// <param name="pcs">Chord pitch classes (note names) for the chord.</param>
        /// <param name="inst">Instrument (octave min/max define playable range).</param>
        /// <param name="rng">Optional RNG for octave selection (for deterministic tests).</param>
        /// <returns>List of DryWetMidi notes (names+octaves) to play simultaneously.</returns>
        private static IReadOnlyList<DryWetMidiNote> RealizeChordSimple(
            NoteName[] pcs, MIDIInstrumentSO inst, System.Random rng = null)
        {
            // Legacy simple realization: root-position within instrument range
            int minOct = inst.octaveMin - 1;
            int maxOct = inst.octaveMax - 1;

            int startOct = (rng != null)
                ? rng.Next(minOct, maxOct + 1)
                : UnityEngine.Random.Range(minOct, maxOct + 1);

            return pcs.Select(nn => DryWetMidiNote.Get(nn, startOct))
                      .Select(n => DryWetMidiNote.Get(n.NoteName, Mathf.Clamp(n.Octave, minOct, maxOct)))
                      .ToArray();
        }

        /// <summary>
        /// Forces every ChannelEvent in the file to the provided channel (0..15).
        /// </summary>
        /// <param name="file">MIDI file whose events will be re-channeled.</param>
        /// <param name="channel">Target MIDI channel (0..15).</param>
        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        /// <summary>
        /// Writes Bank Select (CC0/CC32) and Program Change at the head of each track chunk.
        /// </summary>
        /// <param name="file">MIDI file whose chunks will be stamped.</param>
        /// <param name="inst">Instrument data (BankName numeric, PatchIndex program).</param>
        /// <param name="channel">MIDI channel (0..15) used for the events.</param>
        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[ChordTrackComposer] Instrument bank is not numeric: '{inst.BankName}'");
                bank = 0; // fallback to 0 like old behavior if parse failed
            }

            foreach (var chunk in file.GetTrackChunks())
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                // CC0 Bank Select MSB
                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // CC32 Bank Select LSB
                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // Program Change. Keep tiny DeltaTime after bank to ensure ordering.
                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        /// <summary>
        /// Builds the 7 diatonic triads and 7 diatonic seventh chords for the given
        /// tonality and root note, with roman labels and chord symbols spelled to degree.
        /// </summary>
        /// <param name="mode">Tonality/mode (Ionian, Dorian, etc.).</param>
        /// <param name="rootNote">Root note of the scale.</param>
        /// <returns>Two lists: triads and sevenths (degree, quality, root, roman, symbol).</returns>
        private static (List<DiaChord> triads, List<DiaChord> sevenths) BuildDiatonicSets(
            Tonality mode, NoteName rootNote)
        {
            // Scale degrees → scale note names (root mapped per degree)
            var scale = GetScaleFromTonality(mode, rootNote);
            var scaleNames =
                GetNotesFromScale(scale, rootNote, 4, 7).Select(n => n.NoteName).ToArray();

            var tri = new List<DiaChord>(7);
            var sev = new List<DiaChord>(7);
            for (int i = 0; i < 7; i++)
            {
                var deg = (ScaleDegree)i;

                var tq = GetDiatonicTriadQuality(mode, deg);
                var tRoot = scaleNames[i];
                tri.Add(new DiaChord(deg, tq, tRoot, ToRomanRich(deg, tq),
                    GetChordSymbolSpelledForDegree(rootNote, i, tRoot, tq)));

                var sq = GetDiatonicSeventhQuality(mode, deg);
                var sRoot = scaleNames[i];
                sev.Add(new DiaChord(deg, sq, sRoot, ToRomanRich(deg, sq),
                    GetChordSymbolSpelledForDegree(rootNote, i, sRoot, sq)));
            }
            return (tri, sev);
        }

        private static void LogDiatonicSets(
            Tonality mode,
            NoteName rootNote,
            List<DiaChord> tri,
            List<DiaChord> sev,
            bool showSymbols = false)
        {
            string triLine = showSymbols
                ? string.Join("  ", tri.Select(t => t.symbol))
                : string.Join("  ", tri.Select(t => t.roman));

            string sevLine = showSymbols
                ? string.Join("  ", sev.Select(s => s.symbol))
                : string.Join("  ", sev.Select(s => s.roman));

            Debug.Log($"<color=yellow>[ChordTrack] " +
                $"Diatonic triads in {mode}/{rootNote}: {triLine}</color>");
            Debug.Log($"<color=yellow>[ChordTrack] " +
                $"Diatonic sevenths in {mode}/{rootNote}: {sevLine}</color>");
        }

        /// <summary>
        /// Build a procedural chord progression for this part.
        /// - One chord per bar (downbeat, lasts the whole bar)
        /// - Returns a runtime ChordProgressionData ScriptableObject
        /// - If a TonalityProfileSO exists for this part's tonality, we use it
        ///   (characteristic degrees, vamp candidates, cadence rules, etc).
        ///   Otherwise we fall back to generic modal weighting.
        /// </summary>
        /// <param name="part">Song part (tonality, meter, measures, tempo range).</param>
        /// <param name="ctx">Generation context. We query ctx.GetTonalityProfileForPart(part).</param>
        /// <param name="rng">RNG to use for weighted degree picks.</param>
        /// <param name="baseW">Base weight for every scale degree when using fallback mode.</param>
        /// <param name="rootB">Extra weight for I in fallback mode.</param>
        /// <param name="domB">Extra weight for V in fallback mode.</param>
        /// <param name="charB">Extra weight for "characteristic" degrees in fallback mode.</param>
        /// <param name="defaultVelocity">Velocity to stamp on each chord event.</param>
        /// <returns>Runtime ChordProgressionData with events expressed in step units.</returns>
        public static ChordProgressionData BuildProceduralProgression(
            SongConfig.PartConfig part, MidiGenerator.GenContext ctx,
            System.Random rng,
            float baseW = 1f, float rootB = 3f, float domB = 1.5f, float charB = 2f,
            int defaultVelocity = 96,
            bool verbose = false)
        {
            TonalityProfileSO profile = ctx?.GetTonalityProfileForPart?.Invoke(part);

            // 1) Try to use a library template if one is configured
            var lib = ctx?.Settings?.progressionLibrary;
            if (lib != null)
            {
                var templateEntry = PickTemplateForPart(part, profile, lib, rng, verbose);
                if (templateEntry != null && templateEntry.progression != null)
                {
                    // Instantiate so we get a runtime copy and don't mutate the asset.
                    var progTemplate = ScriptableObject.Instantiate(templateEntry.progression);

                    if (verbose)
                    {
                        Debug.Log(
                            $"<color=cyan>[ChordTrackComposer] Using library template '{templateEntry.id}' " +
                            $"for part '{part.Name}' (tonality={part.Tonality}, measures={part.Measures}).</color>");
                    }

                    return progTemplate;
                }
            }

            if (profile != null)
            {
                // Use the profile-aware path
                return BuildProceduralProgressionWithProfile(
                    part,
                    profile,
                    rng,
                    defaultVelocity,
                    verbose
                );
            }

            // Build degree weights (Ionian baseline for major family, Aeolian for minor family)
            var weights = BuildDegreeWeights(part.Tonality, part.RootNote, baseW, rootB, domB, charB);

            // Meter grid info
            var ts = GetTimeSignatureDetails(part.TimeSignature, GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen));
            int beatsPerBar = ts.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);
            int subdivisions = 1; // one step per beat (MVP)
            int stepsPerMeasure = beatsPerBar * subdivisions;
            int totalSteps = stepsPerMeasure * measures;

            // Anchor array: true where a new chord event starts
            var anchors = new bool[totalSteps];
            for (int m = 0; m < measures; m++) anchors[m * stepsPerMeasure] = true;

            // Degree + quality for each bar
            var pickedPerBar = new List<(ScaleDegree deg, ChordQuality q)>(measures);
            for (int bar = 0; bar < measures; bar++)
            {
                ScaleDegree chosenDeg;
                if (bar == measures - 1)
                {
                    // Final bar cadences to I
                    chosenDeg = ScaleDegree.Tonic;
                }
                else
                {
                    // Weighted pick
                    var localWeights = (float[])weights.Clone();

                    // Intro bias to I on bar 0
                    if (bar == 0)
                        localWeights[(int)ScaleDegree.Tonic] += 2f;

                    // Roulette wheel
                    float total = localWeights.Sum();
                    float pick = (float)rng.NextDouble() * total;
                    int idx = 0;
                    for (; idx < 7; idx++)
                    {
                        if (pick <= localWeights[idx]) break;
                        pick -= localWeights[idx];
                    }
                    if (idx >= 7) idx = 6;

                    chosenDeg = (ScaleDegree)idx;
                }

                var q = GetDiatonicTriadQuality(part.Tonality, chosenDeg);
                pickedPerBar.Add((chosenDeg, q));
            }

            // Materialize ChordProgressionData
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.Measures = measures;
            prog.subdivisions = subdivisions;

            prog.TimeSignature = part.TimeSignature;
            prog.events = new List<ChordProgressionData.ChordEvent>();

            // walk 'anchors' and 'pickedPerBar' and produces proper startStep/lengthSteps/etc.
            prog.RebuildFromAnchors(anchors, pickedPerBar, defaultVelocity);

            return prog;
        }

        private static ChordProgressionData BuildProceduralProgressionWithProfile(
            SongConfig.PartConfig part,
            TonalityProfileSO profile,
            System.Random rng,
            int defaultVelocity = 96,
            bool verbose = false)
        {
            // 1. Derive base per-degree weights (size 7)
            // Scale degrees (0..6, 0 = I, 1 = II, ..., 6 = VII)
            var weights = new float[7];
            for (int i = 0; i < 7; i++)
            {
                float w = 1f;
                if (profile.baseDegreeWeights != null
                    && i < profile.baseDegreeWeights.Count
                    && profile.baseDegreeWeights[i] > 0f)
                    w = profile.baseDegreeWeights[i];

                if (i == 0) // tonic
                    w += profile.tonicBonus;

                if (i == profile.supportDegree)
                    w += profile.supportBonus;

                if (profile.characteristicDegrees != null
                    && profile.characteristicDegrees.Contains(i))
                    w += profile.characteristicBonus;

                weights[i] = w;
            }

            // Log
            if (verbose)
            {
                var weightLines = new List<string>();
                for (int i = 0; i < 7; i++)
                {
                    var deg = (ScaleDegree)i;
                    var qual = GetDiatonicTriadQuality(part.Tonality, deg);
                    var rn = ToRomanRich(deg, qual);
                    weightLines.Add($"{i}:{rn}= {weights[i]:0.##}");
                }

                Debug.Log($"[ChordProfile] Using profile for part '{part.Name}': " +
                          profile.ToDebugString(includeVamps: true));

                Debug.Log($"<color=orange>[ChordProfile] Base degree weights for {part.Tonality} " +
                    $"over {part.RootNote}: " +
                          string.Join(" | ", weightLines) + "</color>");
            }

            // Get grid info
            var ts = GetTimeSignatureDetails(
                part.TimeSignature,
                // TODO: BPM per part to avoid timing issues
                GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen)
            );

            int beatsPerBar = ts.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);
            int subdivisions = 1;
            int stepsPerMeasure = beatsPerBar * subdivisions;
            int totalSteps = stepsPerMeasure * measures;

            // 2. Decide if we’re going to use a vamp or just free-pick
            var vampRuntime = new VampRuntime { degreesSequence = null, barsRemaining = 0 };
            bool useVamp = false;

            // Roll whether to use any vamp at all for this part
            bool allowVamp = profile.vampUsageProbability > 0f &&
                             rng.NextDouble() < profile.vampUsageProbability;

            if (allowVamp)
            {
                var chosen = ChooseVamp(profile.vampCandidates, rng); // existing helper

                if (chosen.HasValue && chosen.Value.degreesSequence != null &&
                    chosen.Value.degreesSequence.Count > 0)
                {
                    // Total bars the vamp *would* occupy
                    int seqLen = chosen.Value.degreesSequence.Count;
                    int totalVampBars = chosen.Value.barsRemaining * seqLen;

                    // Clamp by profile's max coverage
                    int maxAllowedBars = Mathf.Max(
                        1,
                        Mathf.FloorToInt(measures * profile.maxVampCoverage));

                    totalVampBars = Mathf.Min(totalVampBars, maxAllowedBars);

                    // Convert back to "loops of the sequence"
                    int loops = Mathf.Max(1, Mathf.CeilToInt((float)totalVampBars / seqLen));

                    vampRuntime = new VampRuntime
                    {
                        degreesSequence = chosen.Value.degreesSequence,
                        barsRemaining = loops
                    };
                    useVamp = true;
                }
            }

            // logs
            if (verbose)
            {
                if (useVamp && vampRuntime.degreesSequence != null)
                {
                    var seq = string.Join(",", vampRuntime.degreesSequence);
                    Debug.Log($"[ChordProfile] Chosen vamp for part '{part.Name}': " +
                              $"degrees=[{seq}] loops={vampRuntime.barsRemaining} " +
                              $"(maxCoverage={profile.maxVampCoverage:0.##})");
                }
                else
                {
                    Debug.Log($"[ChordProfile] No vamp (or vamp disabled) for part '{part.Name}', " +
                              $"using free-pick chords.");
                }
            }

            //    (choose a vampCandidate by weight, or null if none)
            /*var chosen = ChooseVamp(profile.vampCandidates, rng); // returns (degrees[], barsToUse) or null

            // Wrap tuple in a mutable struct we can edit in-place.
            VampRuntime vampRuntime;
            bool useVamp = false;
            if (chosen.HasValue)
            {
                vampRuntime = new VampRuntime
                {
                    degreesSequence = chosen.Value.degreesSequence,
                    barsRemaining = chosen.Value.barsRemaining
                };
                useVamp = true;
            }
            else
            {
                vampRuntime = new VampRuntime
                {
                    degreesSequence = null,
                    barsRemaining = 0
                };
            }

            // Log
            if (verbose)
            {
                if (useVamp && vampRuntime.degreesSequence != null)
                {
                    var seq = string.Join(",", vampRuntime.degreesSequence);
                    Debug.Log($"[ChordProfile] Chosen vamp for part '{part.Name}': " +
                              $"degrees=[{seq}] bars={vampRuntime.barsRemaining}");
                }
                else
                {
                    Debug.Log($"[ChordProfile] No vamp chosen for part '{part.Name}', " +
                        $"using free-pick chords.");
                }
            }*/



            var anchors = new bool[totalSteps];
            for (int m = 0; m < measures; m++) anchors[m * stepsPerMeasure] = true;

            var pickedDegrees = new List<(ScaleDegree deg, ChordQuality q)>(measures);

            int bar = 0;
            while (bar < measures)
            {
                if (verbose)
                {
                    Debug.Log($"[ChordProfile] Entering vamp branch: " +
                        $"barsRemaining={vampRuntime.barsRemaining} " +
                              $"for part '{part.Name}'");
                }

                // --- Vamp branch ---
                if (useVamp && vampRuntime.barsRemaining > 0)
                {
                    // iterate the vamp's degree sequence across bars
                    for (int i = 0;
                        i < vampRuntime.degreesSequence.Count && bar < measures;
                        i++, bar++)
                    {
                        int degIdx = vampRuntime.degreesSequence[i];

                        // force cadence on last bar if profile says so
                        if (profile.forceCadenceToTonic && bar == measures - 1)
                            degIdx = 0;

                        var sd = (ScaleDegree)degIdx;
                        var qual = GetDiatonicTriadQuality(part.Tonality, sd);
                        pickedDegrees.Add((sd, qual));

                        if (verbose)
                        {
                            var rn = ToRomanRich(sd, qual);
                            Debug.Log($"[ChordProfile]   Bar {bar + 1}/{measures} " +
                                $"(vamp): degIdx={degIdx} rn={rn}");
                        }
                    }

                    vampRuntime.barsRemaining--;
                    continue;
                }

                // --- Free-pick branch ---
                // Build localWeights from profile weights each bar
                var localWeights = (float[])weights.Clone();

                // EXTRA tonic boost on first bar
                if (bar == 0)
                    localWeights[0] += profile.firstBarTonicBonus;

                // last bar force tonic if requested
                int chosenIdx;
                if (profile.forceCadenceToTonic && bar == measures - 1)
                {
                    chosenIdx = 0;
                }
                else
                {
                    float total = localWeights.Sum();
                    float pickVal = (float)rng.NextDouble() * total;

                    if (verbose)
                    {
                        var lwLines = new List<string>();
                        for (int i = 0; i < 7; i++)
                        {
                            var deg = (ScaleDegree)i;
                            var qual = GetDiatonicTriadQuality(part.Tonality, deg);
                            var rn = ToRomanRich(deg, qual);
                            lwLines.Add($"{i}:{rn} w={localWeights[i]:0.##}");
                        }

                        Debug.Log($"[ChordProfile] Bar {bar + 1}/{measures} free-pick weights: " +
                                  string.Join(" | ", lwLines) +
                                  $"  (roulette pick={pickVal:0.###} / total={total:0.###})");
                    }

                    chosenIdx = 0;
                    for (; chosenIdx < 7; chosenIdx++)
                    {
                        if (pickVal <= localWeights[chosenIdx]) break;
                        pickVal -= localWeights[chosenIdx];
                    }
                    if (chosenIdx >= 7) chosenIdx = 6;
                }

                var sdChosen = (ScaleDegree)chosenIdx;
                var qChosen = GetDiatonicTriadQuality(part.Tonality, sdChosen);
                pickedDegrees.Add((sdChosen, qChosen));

                if (verbose)
                {
                    var rn = ToRomanRich(sdChosen, qChosen);
                    Debug.Log($"[ChordProfile]   Bar {bar + 1}/{measures} " +
                        $"picked degree idx={chosenIdx} rn={rn}");
                }

                bar++;
            }

            // build progression asset in-memory
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.Measures = measures;
            prog.subdivisions = subdivisions;

            prog.TimeSignature = part.TimeSignature;
            prog.events = new List<ChordProgressionData.ChordEvent>();
            prog.RebuildFromAnchors(anchors, pickedDegrees, defaultVelocity);

            if (verbose && prog.events != null)
            {
                var seq = string.Join("  ",
                    prog.events.Select(e => ToRomanRich(e.degree, e.quality)));
                Debug.Log($"[ChordProfile] Final profile-driven progression for " +
                    $"part '{part.Name}': {seq}");
            }

            return prog;
        }

        private static (List<int> degreesSequence, int barsRemaining)? ChooseVamp(
            List<TonalityProfileSO.VampDefinition> vamps,
            System.Random rng)
        {
            if (vamps == null || vamps.Count == 0) return null;

            float total = vamps.Sum(v => v.weight);
            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            TonalityProfileSO.VampDefinition chosen = vamps[0];
            foreach (var v in vamps)
            {
                if (pick <= v.weight) { chosen = v; break; }
                pick -= v.weight;
            }

            int bars = Mathf.Clamp(
                rng.Next(chosen.minBars, chosen.maxBars + 1),
                1, 64);

            return (degreesSequence: chosen.degrees, barsRemaining: bars);
        }

        /// <summary>
        /// CQ-A1-OBJ2: resolves the per-chord inversion pin for a progression event
        /// position. Sticky-per-position (D2a=a): resolution depends only on the
        /// event position, so the pin recurs on every pattern repeat within the
        /// render — the per-render one-shot lifecycle is provided by the
        /// snapshot+clear in Compose, not here. A null list, a position beyond the
        /// list, or a null entry means no pin. The inversion VALUE is range-checked
        /// at the voicer, where chord arity is known (out-of-range => unset, D2b=a).
        /// Internal for direct testing via InternalsVisibleTo (no fixtures needed).
        /// </summary>
        /// <param name="hints">Per-chord hint list (index-aligned to prog.events), or null.</param>
        /// <param name="eventIndex">Event position within the pattern (resets each repeat).</param>
        /// <returns>The pinned inversion index, or null when unset.</returns>
        public static int? ResolveInversionPin(
            IReadOnlyList<int?> hints, int eventIndex)
        {
            if (hints == null) return null;
            if (eventIndex < 0 || eventIndex >= hints.Count) return null;
            return hints[eventIndex];
        }

        /// <summary>
        /// Renders a given ChordProgressionData by voicing each event's degree+quality
        /// under the part's tonality/root and writing notes at the appropriate times.
        /// </summary>
        /// <param name="instrument">Playback instrument.</param>
        /// <param name="bpm">Tempo for time conversion.</param>
        /// <param name="part">Part (tonality/root, meter, measures).</param>
        /// <param name="prog">Progression to render (events in steps).</param>
        /// <param name="channel">MIDI channel (0..15).</param>
        /// <param name="ctx">Context providing chord voicer and RNG.</param>
        /// <param name="vlOverride">Optional voice-leading config override.</param>
        /// <param name="modulationHint">One-shot directional first-chord hint (§6).</param>
        /// <param name="previousRoot">Previous tonic root for the directional hint.</param>
        /// <param name="inversionHints">Per-chord inversion pins, index-aligned to prog.events (§7).</param>
        /// <returns>MIDI file with the rendered progression.</returns>
        // MGP-ALWTTT-DBG (chd: contract promotion): internal so the marker
        // parity test (grid site vs this site) can drive both emission paths
        // with the same progression — the InternalsVisibleTo test-seam idiom.
        public MidiFile RenderFromProgression(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            ChordProgressionData prog,
            int channel,
            MidiGenerator.GenContext ctx,
            VoiceLeadingConfig vlOverride,
            ModulationOctaveHint modulationHint,
            Melanchall.DryWetMidi.MusicTheory.NoteName? previousRoot,
            IReadOnlyList<int?> inversionHints,
            ChordExpressionType chordExpression,
            ArpeggioRate arpeggioRate,
            RandomArticulationRoller articRoller,
            VelocityJitter velocityJitter)
        {
            // MGP-ARTIC-RATE-1 (D-MGP-ARTIC-2=B): same assertion as the grid
            // site. This entry point is reachable directly by callers that
            // build their own roller (or none), so the check cannot live only
            // in Compose.
            if (articRoller == null &&
                (chordExpression == ChordExpressionType.Random ||
                 arpeggioRate == ArpeggioRate.Random))
            {
                Debug.LogWarning(
                    "[ChordTrackComposer] Unresolved articulation sentinel " +
                    $"(expression={chordExpression}, rate={arpeggioRate}) with no " +
                    "roller: the articulator will degrade it (Block / Eighth). " +
                    "See runtime/SSoT_Composer_Backing_Track.md §8.5.");
            }

            // Defensive TS normalization: if progression TS differs from the Part TS (or needs upsample),
            // reproject to a runtime clone before rendering.
            prog = NormalizeProgressionForPartIfNeeded(part, prog);


            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            var beatSpan = GetBeatSpan(part.TimeSignature);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int partTotalSteps = Mathf.Max(1, part.Measures) * stepsPerMeasure;
            int patternMeasures = Mathf.Max(1, prog.Measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int numRepeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var chordMarkers = new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            var pb = new PatternBuilder();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            var voicer = ctx?.ChordVoicer ?? _voicer;
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            var effectiveVL = vlOverride ?? _vl;

            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int repeatStepOffset = repeat * patternTotalSteps;
                bool repeatIsFirst = (repeat == 0);
                int eventIndex = 0;

                foreach (var e in prog.events)
                {
                    var degreeRoot = scaleNames[(int)e.degree];
                    // chd: contract parity with the grid site: apply the
                    // degree accidental to root and marker. Guarded so the
                    // accidental==0 case (every procedural progression today)
                    // stays bit-identical to pre-batch output.
                    if (e.degreeAccidental != 0)
                        degreeRoot = TransposeNoteName(degreeRoot, e.degreeAccidental);
                    var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                    // D3 (CQ-A1-OBJ2): same precedence as the inline path — when the
                    // directional hint produces the first chord, its inversion pin is
                    // inherently skipped for that one chord only (§7).
                    IReadOnlyList<DryWetMidiNote> playable = null;
                    bool isFirstChord = repeatIsFirst && (eventIndex == 0);
                    if (isFirstChord)
                    {
                        playable = TryDirectionalFirstChord(
                            chordPcs, degreeRoot, instrument,
                            modulationHint, previousRoot,
                            _previousFirstChordPitch, _settings);
                    }

                    if (playable == null)
                    {
                        // CQ-A1-OBJ2: sticky-per-position pin (§7).
                        int? inversionPin = ResolveInversionPin(inversionHints, eventIndex);
                        playable =
                            (effectiveVL != null && effectiveVL.enableVoiceLeading && voicer != null)
                            ? voicer.VoiceChord(chordPcs, instrument, lastVoicing, effectiveVL, inversionPin, ctx?.rng) // VL-DET-1
                            : RealizeChordSimple(chordPcs, instrument, ctx?.rng);
                    }

                    // MGP-ALWTTT-MOD-DIR-1.1: same stash hook as the inline render path.
                    if (isFirstChord && _reportFirstChordPitch != null)
                    {
                        int rootPitch = FindLowestRootPitch(playable, degreeRoot);
                        if (rootPitch != int.MinValue) _reportFirstChordPitch(rootPitch);
                    }

                    lastVoicing = playable;

                    var rn = ToRomanRich(e.degree, e.quality);
                    // chd: contract parity with the grid site (accidental prefix).
                    if (e.degreeAccidental < 0) rn = "b" + rn;
                    else if (e.degreeAccidental > 0) rn = "#" + rn;
                    var sym = GetChordSymbol(degreeRoot, e.quality);
                    int degIdx = ((int)e.degree) + 1;
                    string q = e.quality.ToString();

                    int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                    double startBeats = (double)startStepAbs / stepsPerBeat;
                    double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                    var startTime = beatSpan.Multiply(startBeats);

                    // CA-T1: same unconditional articulation call as the grid path.
                    // MGP-ALWTTT-ARTIC-1: per-event figure resolution (null
                    // roller => fixed CA-T1 figure). Emit remains the single
                    // unconditional call; only the figure VALUE varies.
                    var effectiveExpression =
                        articRoller != null &&
                        chordExpression == ChordExpressionType.Random
                            ? articRoller.NextFigure() : chordExpression;
                    // CA-V1: independent axis on its own substream. A fixed
                    // figure with a Random rate rolls rates only, and vice versa.
                    var effectiveRate =
                        articRoller != null &&
                        arpeggioRate == ArpeggioRate.Random
                            ? articRoller.NextRate() : arpeggioRate;
                    // CA-T2: same reshape+emit as the grid path.
                    var emitVoicing = _reshaper.Reshape(playable, chordPcs, effectiveExpression);

                    _articulator.Emit(pb, emitVoicing, startBeats, durBeats, beatSpan,
                                      beatsPerBar, e.velocity, stepsPerBeat,
                                      effectiveExpression, effectiveRate,
                                      velocityJitter.ForEvent(eventIndex));

                    chordMarkers.Add((startTime, rn, sym, degIdx, q));
                    eventIndex++;
                }
            }

            if (_settings?.logGenerator == true)
            {
                if (articRoller != null)
                {
                    // MGP-ALWTTT-ARTIC-1 observability (logging only).
                    Debug.Log($"<color=#c9f>[ChordTrackComposer]</color> ARTIC-1 roll " +
                              $"(progression) part='{part.Name}' {articRoller.DescribeRolls()}");
                }

                var name = effectiveVL != null ? effectiveVL.name : "(none)";
                Debug.Log($"<color=red>[ChordTrackComposer] RenderFromProgression part='{part.Name}' " +
                          $"VL effective='{name}' (override param={vlOverride != null})</color>");
            }

            var file = pb.Build().ToFile(tempoMap);
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);
            return file;
        }

        /// <summary>
        /// Picks a chord progression template from the library for this part, if any fits.
        /// It takes into account:
        /// - Part tonality (must be compatible, if entry/progression defines tonalities)
        /// - Rough bar-length compatibility (prefer progressions that fit or loop nicely)
        /// - UsageHint (verse/chorus/bridge/intro)
        /// - Optional TonalityProfile cadence preference (ending on I if forceCadenceToTonic is true)
        /// 
        /// Returns the chosen Entry, or null if nothing is suitable.
        /// </summary>
        private static ChordProgressionLibrarySO.Entry PickTemplateForPart(
            SongConfig.PartConfig part,
            TonalityProfileSO profile,
            ChordProgressionLibrarySO library,
            System.Random rng,
            bool verbose = false)
        {
            if (library == null || library.entries == null || library.entries.Count == 0)
                return null;

            var desiredUsage = ChordProgressionLibrarySO.UsageHint.Any;
            // TODO:  Choose based on loop iteration number, part number, etc

            int partBars = Mathf.Max(1, part.Measures);
            var candidates = new List<(ChordProgressionLibrarySO.Entry entry, float score, bool tsExact)>();

            // ------------------------------------------------------------
            // 2. Score each entry based on tonality, length, usage, profile
            // ------------------------------------------------------------
            foreach (var e in library.entries)
            {
                if (e == null || e.progression == null)
                    continue;

                var prog = e.progression;

                // 2.a) Tonality compatibility
                bool tonalityOk = true;
                var tonality = part.Tonality;

                // Prefer entry.compatibleTonalities, otherwise use progression.tonalities
                List<Tonality> allowedTonalities = null;
                if (e.compatibleTonalities != null && e.compatibleTonalities.Count > 0)
                    allowedTonalities = e.compatibleTonalities;
                else if (prog.tonalities != null && prog.tonalities.Count > 0)
                    allowedTonalities = prog.tonalities;

                if (allowedTonalities != null && allowedTonalities.Count > 0)
                {
                    tonalityOk = allowedTonalities.Contains(tonality);
                }

                if (!tonalityOk)
                    continue;

                // 2.b) Length compatibility: prefer patterns that fit or loop the part nicely.
                int tplBars = Mathf.Max(1, prog.Measures);

                // Hard filter: ignore absurdly long templates for very short parts
                if (tplBars > partBars * 2) // heuristic, can be tuned later
                    continue;

                float lengthScore;
                if (tplBars == partBars)
                    lengthScore = 2.0f;            // perfect fit
                else if (partBars % tplBars == 0)
                    lengthScore = 1.5f;            // loops evenly
                else if (tplBars % partBars == 0)
                    lengthScore = 0.8f;            // template is longer than part
                else
                    lengthScore = 0.9f;            // acceptable but not perfect

                // 2.c) UsageHint: prefer entries that match our inferred part role
                float usageScore = 1f;
                if (desiredUsage != ChordProgressionLibrarySO.UsageHint.Any &&
                    e.usageHint != ChordProgressionLibrarySO.UsageHint.Any)
                {
                    usageScore = (e.usageHint == desiredUsage) ? 1.5f : 0.75f;
                }

                // 2.d) Cadence preference from TonalityProfile, if present
                float cadenceScore = 1f;
                if (profile != null && profile.forceCadenceToTonic &&
                    prog.events != null && prog.events.Count > 0)
                {
                    var lastEvt = prog.events[prog.events.Count - 1];
                    if (lastEvt.degree == ScaleDegree.Tonic)
                        cadenceScore = 1.3f;   // ends on I: slightly prefer
                    else
                        cadenceScore = 0.9f;   // ends elsewhere: slightly penalize
                }

                // 2.e) TimeSignature compatibility (two-step)
                // Tier A: exact TS match is preferred. If none exist, we rank fallbacks by a heuristic score.
                bool tsExact = (prog.TimeSignature == part.TimeSignature);
                float tsMult = tsExact ? 1.35f : ComputeTimeSignatureFallbackMultiplier(part, prog);

                // 2.f) Base weight from entry
                float baseWeight = Mathf.Max(0f, e.weight);

                float finalScore = baseWeight * lengthScore * usageScore * cadenceScore * tsMult;
                if (finalScore <= 0f)
                    continue;

                candidates.Add((e, finalScore, tsExact));
            }

            if (candidates.Count == 0)
                return null;

            // Two-step TS selection: if ANY exact-TS candidates exist, restrict to them.
            bool anyExactTs = candidates.Any(c => c.tsExact);
            if (anyExactTs)
            {
                candidates = candidates.Where(c => c.tsExact).ToList();
                if (verbose)
                    Debug.Log($"[ChordTrackComposer] Template selection tier=ExactTS (count={candidates.Count}) part='{part.Name}' TS={part.TimeSignature}.");
            }
            else if (verbose)
            {
                Debug.Log($"[ChordTrackComposer] Template selection tier=FallbackTS (ranked) (count={candidates.Count}) part='{part.Name}' TS={part.TimeSignature}.");
            }

            // ------------------------------------------------------------
            // 3. Roulette selection by score
            // ------------------------------------------------------------
            float totalScore = 0f;
            foreach (var c in candidates) totalScore += c.score;

            float pick = (float)rng.NextDouble() * totalScore;
            foreach (var c in candidates)
            {
                if (pick <= c.score)
                {
                    if (verbose)
                    {
                        Debug.Log(
                            $"[ChordTrackComposer] Template candidate chosen: '{c.entry.id}' " +
                            $"(score={c.score:0.###}, part='{part.Name}', " +
                            $"usageHint={c.entry.usageHint}).");
                    }
                    return c.entry;
                }
                pick -= c.score;
            }

            // Fallback (should be unreachable, but safe)
            return candidates[candidates.Count - 1].entry;
        }


        // ---------------------------------------------------------------------
        // TS fallback heuristic — used only when no exact-TS template exists
        // ---------------------------------------------------------------------

        /// <summary>
        /// Multiplier for ranking fallback templates when no progression matches the Part TS exactly.
        /// This does NOT change the Part TS; it only affects which template we pick before we reproject it
        /// via Normalized Bar Time.
        ///
        /// Heuristics (rough priority):
        /// - Prefer equal bar duration (e.g., 3/4 ≈ 6/8; both 3 quarter-notes per bar)
        /// - Prefer same beat unit (denominator)
        /// - Prefer odd-with-odd / even-with-even
        /// - Prefer closer numerators
        /// - Prefer higher subdivisions (less quantization artifacts)
        /// - Prefer chord-starts-per-bar close to an expected grouping count (helpful for 5/4 as 3+2)
        /// </summary>
        private static float ComputeTimeSignatureFallbackMultiplier(SongConfig.PartConfig part, ChordProgressionData prog)
        {
            if (part == null || prog == null) return 1f;

            var partProps = TimeSignatureProperties[part.TimeSignature];
            var progProps = TimeSignatureProperties[prog.TimeSignature];

            int partNum = partProps.BeatsPerMeasure;
            int partDen = partProps.BeatUnit;
            int progNum = progProps.BeatsPerMeasure;
            int progDen = progProps.BeatUnit;

            double partBarQN = GetBarDurationInQuarterNotes(partNum, partDen);
            double progBarQN = GetBarDurationInQuarterNotes(progNum, progDen);
            double barDiff = System.Math.Abs(partBarQN - progBarQN);

            float mult = 1.0f;

            // 1) Bar-duration equivalence (strong)
            if (barDiff < 0.001)
                mult *= 1.55f;
            else
                mult *= Mathf.Clamp((float)(1.35 / (1.0 + 0.55 * barDiff)), 0.70f, 1.20f);

            // 2) Same beat unit (denominator)
            if (partDen == progDen) mult *= 1.18f;

            // 3) Odd/even numerator parity match (mild)
            bool partOdd = (partNum & 1) == 1;
            bool progOdd = (progNum & 1) == 1;
            if (partOdd == progOdd) mult *= 1.08f;

            // 4) Numerator closeness
            int numDiff = System.Math.Abs(partNum - progNum);
            mult *= Mathf.Clamp(1.15f / (1f + 0.22f * numDiff), 0.75f, 1.15f);

            // 5) Prefer higher subdivisions
            int sub = Mathf.Max(1, prog.subdivisions);
            float subBonus = 1f + 0.05f * Mathf.Log(sub, 2f);
            mult *= Mathf.Clamp(subBonus, 1.0f, 1.20f);

            // 6) Match chord starts per bar to an expected grouping count (optional but cheap)
            int expectedGroups = GetExpectedGroupingCount(partNum, partDen);
            int startsPerBar = EstimateChordStartsPerBar(prog, progNum);

            if (expectedGroups > 0 && startsPerBar > 0)
            {
                int diff = System.Math.Abs(expectedGroups - startsPerBar);
                if (diff == 0) mult *= 1.12f;
                else if (diff == 1) mult *= 1.03f;
                else mult *= 0.92f;
            }

            return Mathf.Clamp(mult, 0.55f, 2.10f);
        }

        private static double GetBarDurationInQuarterNotes(int numerator, int denominator)
            => numerator * (4.0 / System.Math.Max(1, denominator));

        /// <summary>
        /// Minimal grouping-count presets used only for scoring (Phase 6).
        /// Phase 5 (optional) will carry explicit grouping per Part.
        /// </summary>
        private static int GetExpectedGroupingCount(int numerator, int denominator)
        {
            // Common feels. You can tweak these later without touching the reprojection contract.
            if (numerator == 6 && denominator == 8) return 2;  // 3+3
            if (numerator == 5 && denominator == 4) return 2;  // 3+2 (or 2+3)
            if (numerator == 4 && denominator == 4) return 2;  // 2+2 (strong on 1 & 3)
            if (numerator == 3 && denominator == 4) return 1;  // strong on 1
            if (numerator == 12 && denominator == 8) return 5; // 3+3+2+2+2 (flamenco-ish)
            return 1;
        }

        /// <summary>
        /// Rough estimate: how many chord events start within the FIRST bar of the progression.
        /// Uses the progression's authored numerator + its subdivisions.
        /// </summary>
        private static int EstimateChordStartsPerBar(ChordProgressionData prog, int beatsPerBar)
        {
            if (prog == null || prog.events == null || prog.events.Count == 0)
                return 0;

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerBar = Mathf.Max(1, beatsPerBar) * stepsPerBeat;

            int count = 0;
            for (int i = 0; i < prog.events.Count; i++)
            {
                int s = prog.events[i].startStep;
                if (s >= 0 && s < stepsPerBar)
                    count++;
            }

            return Mathf.Max(1, count);
        }


        // ---------------------------------------------------------------------
        // Modulation directional first-chord override
        // ---------------------------------------------------------------------

        /// <summary>
        /// If a modulation direction hint is set and a previous tonic is known,
        /// realizes the first chord as a root-position stack whose ROOT pitch
        /// is strictly above (Up) or strictly below (Down) the previous root.
        /// Returns null when the hint is Auto, when no previous root is known,
        /// or when inputs are degenerate (caller should then fall back to the
        /// normal voicer / simple realization).
        ///
        /// Range-limit fallback (R-A): if no octave within the instrument range
        /// satisfies the strict direction, clamps to the boundary octave on the
        /// requested side and logs a warning when logGenerator is enabled.
        /// </summary>
        private static IReadOnlyList<DryWetMidiNote> TryDirectionalFirstChord(
            NoteName[] firstChordPcs,
            NoteName firstChordRoot,
            MIDIInstrumentSO inst,
            ModulationOctaveHint hint,
            NoteName? previousRoot,
            int? previousFirstChordPitch,
            MidiGenPlayConfig settings)
        {
            if (hint == ModulationOctaveHint.Auto) return null;
            if (!previousRoot.HasValue) return null;
            if (firstChordPcs == null || firstChordPcs.Length == 0) return null;
            if (inst == null) return null;

            int minOct = inst.octaveMin - 1;
            int maxOct = inst.octaveMax - 1;
            return TryDirectionalFirstChordCore(
                firstChordPcs, firstChordRoot, minOct, maxOct,
                hint, previousRoot.Value, previousFirstChordPitch, settings);
        }

        /// <summary>
        /// Internal test seam for the directional first-chord helper. Same semantics
        /// as <see cref="TryDirectionalFirstChord"/> but takes octave range directly
        /// so unit tests don't need a <c>MIDIInstrumentSO</c> ScriptableObject. Not
        /// part of the public API.
        /// </summary>
        public static IReadOnlyList<DryWetMidiNote> TryDirectionalFirstChordCore(
            NoteName[] firstChordPcs,
            NoteName firstChordRoot,
            int minOct,
            int maxOct,
            ModulationOctaveHint hint,
            NoteName previousRoot,
            int? previousFirstChordPitch,
            MidiGenPlayConfig settings)
        {
            // Defensive: Core's contract mirrors the public adapter. Auto always
            // returns null so callers fall back to the standard voicer; SM-DIR-3
            // regression depends on this short-circuit.
            if (hint == ModulationOctaveHint.Auto) return null;
            if (firstChordPcs == null || firstChordPcs.Length == 0) return null;
            if (maxOct < minOct) return null;

            int centerOct = (minOct + maxOct) / 2;

            // MGP-ALWTTT-MOD-DIR-1.1: prefer the actual previous first-chord root
            // pitch (supplied by the factory's per-track memory). Cold start with
            // no remembered pitch falls back to the centerOct heuristic, which is
            // bit-identical to pre-1.1 behavior so SM-DIR-3 regression is unaffected.
            bool usedRememberedPitch = previousFirstChordPitch.HasValue;
            int prevPitch = usedRememberedPitch
                ? previousFirstChordPitch.Value
                : MidiPitch(previousRoot, centerOct);

            int? chosenOct = null;
            if (hint == ModulationOctaveHint.Up)
            {
                for (int o = minOct; o <= maxOct; o++)
                    if (MidiPitch(firstChordRoot, o) > prevPitch) { chosenOct = o; break; }
            }
            else // Down
            {
                for (int o = maxOct; o >= minOct; o--)
                    if (MidiPitch(firstChordRoot, o) < prevPitch) { chosenOct = o; break; }
            }

            bool clampedFallback = !chosenOct.HasValue;
            int finalOct = chosenOct ?? (hint == ModulationOctaveHint.Up ? maxOct : minOct);

            if (settings != null && settings.logGenerator)
            {
                Debug.Log(
                    $"[ChordTrackComposer/Mod-DIR] hint={hint} " +
                    $"prevRoot={previousRoot} newRoot={firstChordRoot} " +
                    $"range=[{minOct}..{maxOct}] centerOct={centerOct} " +
                    $"prevPitch={prevPitch} " +
                    $"anchor={(usedRememberedPitch ? "remembered" : "centerOct-fallback")} " +
                    $"chosenOct={(chosenOct.HasValue ? chosenOct.Value.ToString() : "(none)")} " +
                    $"finalOct={finalOct} clampedFallback={clampedFallback}");
            }

            if (clampedFallback && settings != null && settings.logGenerator)
            {
                Debug.LogWarning(
                    $"[ChordTrackComposer] Modulation hint {hint} could not be satisfied " +
                    $"strictly within instrument range [{minOct}..{maxOct}] for root " +
                    $"{firstChordRoot} vs previous pitch {prevPitch}. " +
                    $"Clamping first-chord octave to {finalOct}.");
            }

            return BuildRootPositionStack(firstChordPcs, finalOct, minOct, maxOct);
        }

        /// <summary>Builds an ascending root-position chord stack starting at rootOctave,
        /// bumping octave whenever a successive PC would not advance upward. Clamped to range.</summary>
        private static IReadOnlyList<DryWetMidiNote> BuildRootPositionStack(
            NoteName[] pcs, int rootOctave, int minOct, int maxOct)
        {
            var result = new DryWetMidiNote[pcs.Length];
            int prevPitch = int.MinValue;
            int curOct = rootOctave;

            for (int i = 0; i < pcs.Length; i++)
            {
                int p = MidiPitch(pcs[i], curOct);
                if (i > 0 && p <= prevPitch)
                {
                    curOct++;
                    p = MidiPitch(pcs[i], curOct);
                }
                int clampedOct = Mathf.Clamp(curOct, minOct, maxOct);
                result[i] = DryWetMidiNote.Get(pcs[i], clampedOct);
                prevPitch = p;
            }
            return result;
        }

        /// <summary>
        /// Returns the MIDI pitch of the lowest note in <paramref name="voicing"/>
        /// whose pitch class equals <paramref name="rootNoteName"/>. Used by
        /// MGP-ALWTTT-MOD-DIR-1.1 to capture the actual first-chord root pitch for
        /// the factory's per-track memory, regardless of inversion or Drop-2.
        /// Returns <c>int.MinValue</c> if the root note is not present in the
        /// voicing (theoretically possible with extreme rootless voicings; the
        /// caller treats this as "skip the stash").
        /// </summary>
        private static int FindLowestRootPitch(
            IReadOnlyList<DryWetMidiNote> voicing, NoteName rootNoteName)
        {
            if (voicing == null || voicing.Count == 0) return int.MinValue;
            int best = int.MaxValue;
            for (int i = 0; i < voicing.Count; i++)
            {
                var n = voicing[i];
                if (n.NoteName != rootNoteName) continue;
                int p = MidiPitch(n.NoteName, n.Octave);
                if (p < best) best = p;
            }
            return best == int.MaxValue ? int.MinValue : best;
        }

        /// <summary>MIDI pitch number for a (NoteName, octave). Mirrors DryWetMidi convention: C4 = 60.</summary>
        public static int MidiPitch(NoteName nn, int octave) => (octave + 1) * 12 + (int)nn;
    }
}
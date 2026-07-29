using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.Composition.BasslineCardConfigSO;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// Minimal bass composer: an articulated monophonic line, one selected note
    /// per chord event. Mode: root-only (default) or random chord tone
    /// (constructor flag).
    ///
    /// CA-F2 (D-PRIO=A Feature 2): consumes the shared Tier-1 articulation
    /// engine (IChordArticulator, SD-F2-1=A) with a 1-note voicing per event.
    /// Block / no BasslineCardConfigSO is bit-identical to the legacy
    /// MoveToTime+Note pair (test-pinned). The note-selection loop — including
    /// its per-event ctx.rng draw sequence — is deliberately unchanged; only the
    /// emission pair was replaced. See runtime/SSoT_Composer_Bass_Track.md.
    /// CA-V1: the bass now owns the Random roll too (D6 lifted) and carries the
    /// seeded velocity jitter. Both run on seed-derived substreams; ctx.rng and
    /// the note-selection draw order are untouched.
    /// MGP-ALWTTT-BASS-POCKET-1: opt-in SlapPocket coupling to the Rhythm
    /// track's published onsets (kick→slap on the selected note, snare→pop one
    /// octave up, drum-step velocity, short gate). Per-event SUBSTITUTION of
    /// the figure when onsets exist in the window; decoupled fallback
    /// otherwise. The emission body is restructured into a per-event segment
    /// list consumed by ONE unconditional Emit call site (the SD-F2-1
    /// anti-divergence discipline, now over segments). ZERO new ctx.rng draws:
    /// the pocket branch runs after both §2 selection draws and reads no rng;
    /// the CA-V1 roller keeps rolling per event whether or not its result is
    /// used, so source availability can never shift the roll stream — which is
    /// what makes pocket-on-without-source byte-identical to pocket-off.
    /// MGP-ALWTTT-BASS-POCKET-2: card-level pocket shaping. D-PKT-VEL2=B —
    /// additive pocketSlapBoost/pocketPopBoost over the drum-step velocity,
    /// pre-clamp 1..127, default 0 (byte-identical). D-PKT-LANES2=C —
    /// optional custom slap/pop lane lists replacing the v1 families
    /// (pocketCustomLanes off = v1 families exactly; empty list = class
    /// disabled; a lane in both lists is pop). All shaping lives inside
    /// BuildPocketPlan — the degrade path and the rng discipline are
    /// untouched by construction.
    /// B3 BASS-REG-1 (D-REG-1=C / D-REG-2=B / D-REG-3=B / D-REG-4=B): the bass
    /// now honours MIDIInstrumentSO.octaveMax. The §2 band narrows to TWO
    /// octaves (authored octaveMin..octaveMin+1; the -1 in code is the
    /// authored→DryWetMidi octave conversion, same as chord/melody) and is
    /// ceiling-capped; a walk voicing whose top exceeds the ceiling folds down
    /// a WHOLE octave (shape, intervals and strict ascent preserved); a pop
    /// folds back onto the selected note when +12 does not fit (pop IDENTITY —
    /// class, boost, pop-wins, gate — untouched). The §2 draw count/order and
    /// every substream are intact; only the octave draw's RANGE and the
    /// emitted pitches change. Declared render-affecting batch.
    /// B3 WALK-2 (D-W2-VOCAB=B / D-W2-LAST=A / D-W2-HOME=A / D-W2-SURF=A /
    /// D-W2-RNG=B / D-W2-POCKET=A): opt-in improvised walking bass,
    /// arpeggioToneMode = ImprovisedWalk. The composer plans PITCHES only
    /// (BuildWalkLine: event-root anchor, chord-tone middles chosen near the
    /// previous note, a chromatic/whole-step approach into the NEXT event's
    /// root, wrapping to the first event); rhythm and dynamics come from the
    /// engine's own arpeggio plan (PlanHits, called composer-side with the
    /// event jitter), re-emitted as one 1-note Block segment per hit through
    /// the SAME single unconditional Emit — Block's plan is a velocity
    /// passthrough, so accents and jitter are exactly the arpeggio's.
    /// Variation is a PURE MIX of (walk substream seed, eventIndex, hitIndex)
    /// — the VelocityJitter idiom — so no stream exists that a toggle could
    /// shift. ZERO ctx.rng draws; pocketed events still bypass the walk
    /// (§3.7 verbatim); every planned note folds -12 while above the
    /// D-REG-1=C ceiling (per-note adaptation of D-REG-3=B).
    public sealed class BassTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly bool _randomChordTone;

        // CA-F2: shared Tier-1 articulation seam (the SAME engine the
        // ChordTrackComposer uses, D-PRIO=A). Stateless and RNG-free by
        // contract — it never consumes ctx.rng, so the bass's own per-event
        // draw sequence below is unaffected — hence a single shared instance.
        private static readonly IChordArticulator _articulator = new ChordArticulator();

        public BassTrackComposer(MidiGenPlayConfig settings, bool randomChordTone = false)
        {
            _settings = settings;
            _randomChordTone = randomChordTone;
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = (MIDIInstrumentSO)cfg.Instrument;

            // MGP-ALWTTT-DBG-3 (Ask C): a patternOverride targeting Bassline is
            // warn + ignore in v1. The bass owns no pattern channel — it renders
            // the per-part SHARED progression — so honoring an override here
            // would create a second mutation path into shared state. Override
            // the Backing track instead (its override IS shared, by design).
            if (ctx?.patternOverride != null)
            {
                Debug.LogWarning(
                    $"[BassTrackComposer] patternOverride targeting Bassline is not " +
                    $"supported in v1 (got '{ctx.patternOverride.name}'). The bass " +
                    $"renders the shared progression; override the Backing track " +
                    $"instead. Ignoring.");
            }

            // MGP-ALWTTT-DBG-1 (Ask A): source-tracked resolution — same
            // precedence as before (shared cache, else TrackParameters).
            var sharedProg = ctx?.GetProgressionForPart?.Invoke(part);
            var prog = sharedProg ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                // Ask A: nothing rendered.
                ctx?.ReportResolved?.Invoke(new ResolvedTrackChoice
                {
                    source = ResolvedSource.None,
                    usesSharedProgression = false,
                });
                return new MidiFile();
            }

            // Ask A: the corrected bass payload — flag + shared progression
            // identity (roman formatted exactly like the backing readback).
            ctx?.ReportResolved?.Invoke(new ResolvedTrackChoice
            {
                source = sharedProg != null
                    ? ResolvedSource.SharedProgression
                    : ResolvedSource.TrackParameters,
                usesSharedProgression = sharedProg != null,
                sourceAssetName = prog.name,
                progressionRoman = ChordTrackComposer.RomanSequence(prog),
            });

            // CA-F2 (SD-F2-4=A / SD-F2-5=A / D-EXP1=A): persistent card-level
            // articulation selection, resolved once at entry from the track's
            // Style slot. No card (or a non-bass bundle) => Block, independent
            // of the backing card. No snapshot-and-clear: the §6/§7 transient
            // lifecycle does not apply.
            var (chordExpression, arpeggioRate) = ResolveArticulation(cfg);

            // CA-V1 (D-V1-BASS=B): the ARTIC-1 D6 limitation is LIFTED — the bass
            // now rolls its own figures/rates instead of degrading Random to
            // Block. Its substreams derive from the BASS trackSeed, which already
            // folds in role + musicianId (ResolveTrackSeed*), so backing and bass
            // on the same part never share a roll sequence.
            //
            // Critical: none of this touches ctx.rng. The note-selection loop
            // below keeps its exact per-event draw count and order (1 draw root
            // mode, 2 chord-tone mode) — the determinism surface of section 2 of
            // the Bass SSoT.
            var bassStyle = cfg?.Parameters?.Style as BasslineCardConfigSO;
            int trackSeed = ctx != null ? ctx.trackSeed : 0;
            var toneMode = bassStyle != null
                ? bassStyle.arpeggioToneMode : BassArpeggioToneMode.RepeatedNote;

            RandomArticulationRoller articRoller = null;
            if (chordExpression == ChordExpressionType.Random ||
                arpeggioRate == ArpeggioRate.Random)
            {
                articRoller = new RandomArticulationRoller(
                    new System.Random(SongOrchestrator.ResolveArticulationSeed(trackSeed)),
                    bassStyle != null ? bassStyle.randomRerollChance : 1f,
                    bassStyle != null ? bassStyle.randomFigureWeights : null,
                    new System.Random(SongOrchestrator.ResolveArticulationRateSeed(trackSeed)));
            }

            var velocityJitter = new VelocityJitter(
                bassStyle != null ? bassStyle.velocityJitter : 0,
                SongOrchestrator.ResolveVelocityJitterSeed(trackSeed));

            // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-EXPR=A): resolve the pocket
            // source ONCE at entry. `pocketOnsets != null` is the sole gate
            // the per-event branch consults; when it stays null the loop body
            // is draw-for-draw AND value-for-value the decoupled path — the
            // degrade contract (warn max, never error, never silence) and the
            // pocket-on-without-source ≡ pocket-off byte-identity both hang on
            // this. Fetching draws no rng.
            // MGP-ALWTTT-BASS-POCKET-2: card-level pocket shaping, resolved
            // once at entry. Defaults (0 / 0 / custom-lanes off => null lists)
            // make BuildPocketPlan byte-identical to POCKET-1; when custom
            // lanes are ON, a missing list is defensively treated as empty
            // (= class disabled), never as "fall back to the family".
            int pocketSlapBoost = 0, pocketPopBoost = 0;
            IReadOnlyList<GeneralMidiPercussion> pocketSlapLanes = null;
            IReadOnlyList<GeneralMidiPercussion> pocketPopLanes = null;

            List<MidiGenerator.RhythmOnset> pocketOnsets = null;
            bool pocketRequested = bassStyle != null &&
                bassStyle.pocketMode == PocketCouplingMode.SlapPocket;
            if (pocketRequested)
            {
                pocketSlapBoost = bassStyle.pocketSlapBoost;
                pocketPopBoost = bassStyle.pocketPopBoost;
                if (bassStyle.pocketCustomLanes)
                {
                    pocketSlapLanes = (IReadOnlyList<GeneralMidiPercussion>)
                        bassStyle.pocketSlapLanes
                        ?? System.Array.Empty<GeneralMidiPercussion>();
                    pocketPopLanes = (IReadOnlyList<GeneralMidiPercussion>)
                        bassStyle.pocketPopLanes
                        ?? System.Array.Empty<GeneralMidiPercussion>();
                }

                pocketOnsets = ctx?.GetRhythmOnsetsForPart?.Invoke(part);
                if (pocketOnsets == null || pocketOnsets.Count == 0)
                {
                    pocketOnsets = null;
                    Debug.LogWarning(
                        $"[BassTrackComposer] pocketMode=SlapPocket but no rhythm " +
                        $"onsets are published for part '{part?.Name}'. Causes: no " +
                        $"Rhythm track in the part, the Rhythm track composes AFTER " +
                        $"the bass (track-list order — put Rhythm before Bassline), " +
                        $"or the rhythm resolved to a procedural/legacy path (grid " +
                        $"patterns only publish in v1). Rendering the decoupled " +
                        $"figure.");
                }
            }

            // CA-F2 (SD-F2-3=B): meter authority — derive the beat grid from the
            // Part TS, mirroring ChordTrackComposer. NOTE the recorded deviation:
            // legacy bass emitted on MusicalTimeSpan.Quarter unconditionally, so
            // in beat-unit != 4 meters (e.g. 6/8) it was desynced from the
            // backing track. Output is bit-identical in every beat-unit == 4
            // meter; in others this is a deliberate, test-pinned sync fix.
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(part.TimeSignature);

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // scale → degree root names (for fast lookup)
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // bass register: favor lower region.
            // B3 BASS-REG-1 (D-REG-4=B / D-REG-1=C): two-octave low band,
            // ceiling-capped — see ResolveOctaveBand. The draw below keeps its
            // per-event count and order; only its RANGE changed (3→2 octaves),
            // which remaps same-seed draws — the batch's declared render change.
            var (minOct, maxOct) = ResolveOctaveBand(inst.octaveMin, inst.octaveMax);

            // B3 (D-REG-1=C): hard emission ceiling — top of the declared
            // register (B at authored octaveMax, i.e. DryWetMidi octaveMax-1).
            // Everything emitted above the drawn note (walk tops, pops) is
            // guaranteed <= ceiling; the ceiling wins over the band floor
            // (low is safe on a bass).
            int registerCeiling = ResolveRegisterCeiling(inst.octaveMax);

            var rng = ctx?.rng ?? new System.Random();

            // POCKET-1: per-Compose segment buffer (local — no composer state).
            var _segments = new List<EmitSegment>(4);

            // B3 WALK-2 (D-W2-VOCAB=B): the improvised walk needs the NEXT
            // event's root (approach-note target), so the ordered enumeration
            // is materialized. OrderBy is stable and ToList preserves its
            // sequence: iteration order — and with it the §2 draw order — is
            // IDENTICAL to the previous foreach. walkSeed is a pure hash,
            // computed unconditionally and read only by the ImprovisedWalk
            // branch (byte-inert for every other mode).
            var orderedEvents = prog.events.OrderBy(e => e.startStep).ToList();
            int walkSeed = SongOrchestrator.ResolveWalkSeed(trackSeed);

            for (int eventIndex = 0; eventIndex < orderedEvents.Count; eventIndex++)
            {
                var ce = orderedEvents[eventIndex];
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, ce.quality);

                // choose pitch class: root or random chord tone
                var pc = _randomChordTone
                    ? chordPcs[rng.Next(0, chordPcs.Length)]
                    : chordPcs[0]; // root is first

                // pick octave in a narrow low band
                int oct = rng.Next(minOct, maxOct + 1);
                var note = NoteTheory.Get(pc, oct);

                // timings
                double startBeats = ce.startStep / (double)stepsPerBeat;
                double lenBeats = Math.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                // CA-F2: the bass's single emission site — one unconditional
                // articulator call (same anti-divergence discipline as the two
                // chord sites), replacing the legacy MoveToTime+Note pair.
                // SD-F2-1=A: a 1-note voicing through Emit; Block's 1-note
                // pb.Chord is byte-identical to the legacy pb.Note (test-pinned;
                // contingency on record: an EmitMono translator sharing PlanHits).
                // SD-F2-2=A: figures apply to the selected note; arpeggios become
                // a repeated-note pulse. Velocity note: Block clamps 0..127 where
                // legacy raw-cast threw out-of-range — byte-identical for valid
                // 0..127 data, strictly more robust otherwise.
                // CA-V1 roll — ALWAYS executes, used or not (POCKET-1,
                // D-PKT-EXPR=A): keeping the roller's per-event consumption
                // unconditional means toggling pocket / source availability can
                // never shift the roll stream of later events.
                var effectiveExpression =
                    articRoller != null &&
                    chordExpression == ChordExpressionType.Random
                        ? articRoller.NextFigure() : chordExpression;
                var effectiveRate =
                    articRoller != null &&
                    arpeggioRate == ArpeggioRate.Random
                        ? articRoller.NextRate() : arpeggioRate;

                var evJitter = velocityJitter.ForEvent(eventIndex);

                // MGP-ALWTTT-BASS-POCKET-1: per-event segment plan. Decoupled
                // (or window without onsets) => ONE segment carrying exactly
                // the pre-batch arguments; pocketed => N Block segments, one
                // per planned slap/pop hit. Runs AFTER both §2 selection draws
                // and reads no rng (same structural argument as D-WALK-RNG=A).
                _segments.Clear();

                List<PocketHit> pocketPlan = pocketOnsets != null
                    ? BuildPocketPlan(pocketOnsets, startBeats, lenBeats,
                        pocketSlapBoost, pocketPopBoost,
                        pocketSlapLanes, pocketPopLanes)
                    : null;

                if (pocketPlan != null && pocketPlan.Count > 0)
                {
                    // D-PKT-WHAT=SlapPocket: kick→slap on the selected note,
                    // snare→pop one octave up (+12, D-PKT-POP-PITCH=A);
                    // drum-step velocity (D-PKT-VEL=A); short percussive gate
                    // (D-PKT-GATE=A, planned in BuildPocketPlan). Jitter scope:
                    // the event jitter refolded per pocket hit (ForEvent
                    // chaining is a pure avalanche), so pocket hits don't all
                    // share one delta; the decoupled path keeps the pre-batch
                    // evJitter verbatim.
                    // B3 (D-REG-2=B): +12 when it fits the ceiling, folded
                    // back onto the selected note otherwise. Pop identity
                    // (classification, popBoost, pop-wins, gate) untouched —
                    // BuildPocketPlan never sees the fold.
                    var popNote = ResolvePopNote(pc, oct, registerCeiling);
                    for (int k = 0; k < pocketPlan.Count; k++)
                    {
                        var h = pocketPlan[k];
                        _segments.Add(new EmitSegment(
                            new[] { h.pop ? popNote : note },
                            h.startBeats, h.lenBeats,
                            ChordExpressionType.Block, effectiveRate,
                            h.velocity, evJitter.ForEvent(k)));
                    }
                }
                else if (toneMode == BassArpeggioToneMode.ImprovisedWalk &&
                         (effectiveExpression == ChordExpressionType.ArpeggioUp ||
                          effectiveExpression == ChordExpressionType.ArpeggioDown) &&
                         chordPcs.Length >= 2 &&
                         ChordArticulator.ArpeggioFits(lenBeats, effectiveRate))
                {
                    // B3 WALK-2 (D-W2-HOME=A): the engine still owns rhythm and
                    // dynamics — PlanHits (public, pure) plans the arpeggio
                    // grid with the event velocity, accent curve and the SAME
                    // event jitter an arpeggio would get (noteCount: 1; the
                    // returned NoteIndex is ignored). The composer owns only
                    // the PITCHES: BuildWalkLine plans one note per grid hit,
                    // and each hit re-enters the single unconditional Emit as
                    // a 1-note Block segment with jitter OFF — BlockPlan is
                    // Clamp(base, 0..127) with no accent curve and
                    // ApplyJitter(default) is a no-op, so the planned velocity
                    // passes through verbatim (no double shaping).
                    // D-W2-RNG=B: variation is a pure mix keyed on
                    // (walkSeed, eventIndex, hitIndex); no stream exists, so
                    // no draw-count discipline is needed and pocket toggling
                    // cannot shift anything.
                    // D-W2-LAST=A: the last event approaches the FIRST
                    // event's root (loop-friendly wrap). The next-root lookup
                    // mirrors the loop's own degree lookup exactly (including
                    // its accidental-blindness, on record).
                    // D-W2-POCKET=A: structurally unreachable for pocketed
                    // events — this branch sits behind the pocket
                    // substitution, §3.7 verbatim.
                    var nextCe = orderedEvents[(eventIndex + 1) % orderedEvents.Count];
                    var nextRootPc = scaleNames[(int)nextCe.degree];

                    var grid = ChordArticulator.PlanHits(
                        effectiveExpression, effectiveRate, startBeats, lenBeats,
                        beatsPerBar, 1, ce.velocity, evJitter);
                    var line = BuildWalkLine(
                        chordPcs, nextRootPc, oct, registerCeiling, grid.Count,
                        effectiveExpression == ChordExpressionType.ArpeggioDown,
                        walkSeed, eventIndex);

                    for (int k = 0; k < grid.Count; k++)
                    {
                        _segments.Add(new EmitSegment(
                            new[] { line[k] },
                            grid[k].StartBeats, grid[k].DurBeats,
                            ChordExpressionType.Block, effectiveRate,
                            grid[k].Velocity, default));
                    }
                }
                else
                {
                    // BASS-WALK-1 (D-WALK-HOME=A / D-WALK-RNG=A): when the resolved figure is
                    // an arpeggio and walk mode is on, hand the SAME Emit a root-anchored
                    // triad and let the existing k % noteCount cycling do the walk. Zero new
                    // ctx.rng draws: 3rd/5th are deterministic from chordPcs, stacked above
                    // the already-drawn root octave. ArpeggioFits guards the degrade path so
                    // a too-short event never emits a 3-note chord (mono invariant).
                    NoteTheory[] playable;
                    if (toneMode == BassArpeggioToneMode.ChordToneWalk &&
                        (effectiveExpression == ChordExpressionType.ArpeggioUp ||
                         effectiveExpression == ChordExpressionType.ArpeggioDown) &&
                        chordPcs.Length >= 2 &&
                        ChordArticulator.ArpeggioFits(lenBeats, effectiveRate))
                    {
                        // B3 (D-REG-3=B): ceiling-aware overload — folds the
                        // WHOLE voicing down an octave when its top exceeds
                        // the ceiling. Shape and strict ascent preserved.
                        playable = BuildWalkVoicing(chordPcs, oct, registerCeiling);
                    }
                    else
                    {
                        playable = new[] { note };
                    }

                    _segments.Add(new EmitSegment(
                        playable, startBeats, lenBeats,
                        effectiveExpression, effectiveRate,
                        ce.velocity, evJitter));
                }

                // The bass's single emission site (SD-F2-1 discipline over
                // segments): one unconditional articulator call site.
                foreach (var seg in _segments)
                {
                    _articulator.Emit(pb, seg.playable, seg.startBeats, seg.lenBeats,
                                      beatSpan, beatsPerBar, seg.velocity, stepsPerBeat,
                                      seg.expression, seg.rate, seg.jitter);
                }
            }

            var file = pb.Build().ToFile(tempoMap);

            if (_settings?.logGenerator == true)
            {
                var all = file.GetNotes().OrderBy(n => n.Time).ToList();
                Debug.Log($"[BASS-WALK probe2] notes={all.Count} " +
                          $"distinctPitches={all.Select(n => (int)n.NoteNumber).Distinct().Count()} " +
                          $"first12={string.Join(",", all.Take(12).Select(n => (int)n.NoteNumber))}");
            }

            // channel + program (match other composers)
            ForceAllChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            if (_settings?.logGenerator == true)
            {
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[BassTrackComposer] notes={notes} lastTick={lastTick} " +
                          $"expr={chordExpression} rate={arpeggioRate} " +
                          $"jitter={velocityJitter.Amount}" +
                          (articRoller != null
                              ? $" | CA-V1 roll {articRoller.DescribeRolls()}"
                              : "") +
                          (pocketRequested
                              ? $" | POCKET-1 SlapPocket source=" +
                                (pocketOnsets != null
                                    ? $"published({pocketOnsets.Count} onsets)"
                                    : "NONE(decoupled)") +
                                $" | POCKET-2 boosts=({pocketSlapBoost:+0;-0;0}," +
                                $"{pocketPopBoost:+0;-0;0}) lanes=" +
                                (pocketSlapLanes != null
                                    ? $"custom(slap:{pocketSlapLanes.Count}," +
                                      $"pop:{pocketPopLanes.Count})"
                                    : "v1-families")
                              : ""));
            }

            return file;
        }

        /// <summary>
        /// POCKET-1: one planned emission segment. The per-event body builds a
        /// list of these (1 decoupled segment, or N pocket segments) and a
        /// single unconditional call site drains it — the SD-F2-1
        /// anti-divergence discipline, restructured over segments.
        /// </summary>
        private readonly struct EmitSegment
        {
            public readonly NoteTheory[] playable;
            public readonly double startBeats;
            public readonly double lenBeats;
            public readonly ChordExpressionType expression;
            public readonly ArpeggioRate rate;
            public readonly int velocity;
            public readonly VelocityJitter jitter;

            public EmitSegment(NoteTheory[] playable, double startBeats,
                double lenBeats, ChordExpressionType expression,
                ArpeggioRate rate, int velocity, VelocityJitter jitter)
            {
                this.playable = playable;
                this.startBeats = startBeats;
                this.lenBeats = lenBeats;
                this.expression = expression;
                this.rate = rate;
                this.velocity = velocity;
                this.jitter = jitter;
            }
        }

        /// <summary>
        /// POCKET-1 (D-PKT-WHAT=SlapPocket): one planned slap/pop hit inside a
        /// chord-event window. <c>pop</c> = snare-driven (pitch +12 at the
        /// call site); velocity is the DRUM step's resolved velocity
        /// (D-PKT-VEL=A).
        /// </summary>
        public readonly struct PocketHit
        {
            public readonly double startBeats;
            public readonly double lenBeats;
            public readonly int velocity;
            public readonly bool pop;

            public PocketHit(double startBeats, double lenBeats,
                int velocity, bool pop)
            {
                this.startBeats = startBeats;
                this.lenBeats = lenBeats;
                this.velocity = velocity;
                this.pop = pop;
            }
        }

        /// <summary>POCKET-1 (D-PKT-GATE=A): percussive gate ceiling, in Part
        /// beats. Hit length = min(gap to next planned hit, remaining event
        /// window, this ceiling).</summary>
        public const double PocketMaxGateBeats = 0.5;

        /// <summary>Kick family for SlapPocket classification (semantic lane,
        /// pre kit resolution).</summary>
        public static bool IsPocketKick(GeneralMidiPercussion i)
            => i == GeneralMidiPercussion.AcousticBassDrum
            || i == GeneralMidiPercussion.BassDrum1;

        /// <summary>Snare family for SlapPocket classification. Side stick is
        /// deliberately excluded in v1 (a rim click is not a backbeat pop).
        /// POCKET-2 (D-PKT-LANES2=C): this stays the DEFAULT — custom lane
        /// lists on the card replace it only when opted in.</summary>
        public static bool IsPocketSnare(GeneralMidiPercussion i)
            => i == GeneralMidiPercussion.AcousticSnare
            || i == GeneralMidiPercussion.ElectricSnare;

        /// <summary>POCKET-2: allocation-free membership test for the custom
        /// lane lists (semantic lanes, small lists — linear scan).</summary>
        private static bool LaneListContains(
            IReadOnlyList<GeneralMidiPercussion> lanes, GeneralMidiPercussion i)
        {
            for (int k = 0; k < lanes.Count; k++)
                if (lanes[k] == i) return true;
            return false;
        }

        /// <summary>
        /// POCKET-1: pure per-event pocket planner (test seam, same idiom as
        /// <see cref="BuildWalkVoicing"/> — deterministic, no rng, no state).
        ///
        /// Filters the published onsets to the event window
        /// <c>[eventStart, eventStart + eventLen)</c>, keeps kick/snare
        /// families only, and dedupes per beat position: on the SAME beat, pop
        /// (snare) wins over slap (kick) outright — flag AND velocity, the
        /// backbeat-cuts-through funk gesture, test-pinned — and within one
        /// class the max velocity wins (two kick-family lanes on one step).
        /// Beat equality is exact: all onsets of one publication share one
        /// integer step grid, so equal steps produce identical doubles.
        ///
        /// Lengths (D-PKT-GATE=A): min(gap to next hit, remaining window,
        /// <see cref="PocketMaxGateBeats"/>). Empty result = "figure applies"
        /// (the caller's per-event fallback, D-PKT-EXPR=A).
        ///
        /// MGP-ALWTTT-BASS-POCKET-2 extensions (all defaults = byte-identical
        /// POCKET-1 behavior):
        /// - D-PKT-VEL2=B: <c>slapBoost</c>/<c>popBoost</c> are additive
        ///   per-class offsets over the drum step's resolved velocity, clamped
        ///   1..127 (published onsets already arrive 1..127, so boost 0 is an
        ///   exact identity). Applied at classification time; observationally
        ///   equivalent to post-dedupe application because the boost is
        ///   uniform within a class (max-velocity dedupe is invariant under a
        ///   monotone per-class transform) and the same-beat pop-wins rule is
        ///   unconditional (never compares velocities across classes).
        /// - D-PKT-LANES2=C: <c>slapLanes</c>/<c>popLanes</c> null = the v1
        ///   built-in family (<see cref="IsPocketKick"/> /
        ///   <see cref="IsPocketSnare"/>); non-null = the list REPLACES the
        ///   family (empty list = class disabled). A lane in both lists
        ///   classifies as pop — the pop check runs first, consistent with
        ///   the pop-wins ethos. Matching is on the SEMANTIC lane, as v1.
        /// </summary>
        public static List<PocketHit> BuildPocketPlan(
            IReadOnlyList<MidiGenerator.RhythmOnset> onsets,
            double eventStartBeats,
            double eventLenBeats,
            int slapBoost = 0,
            int popBoost = 0,
            IReadOnlyList<GeneralMidiPercussion> slapLanes = null,
            IReadOnlyList<GeneralMidiPercussion> popLanes = null)
        {
            var hits = new List<PocketHit>();
            if (onsets == null || onsets.Count == 0 || eventLenBeats <= 0)
                return hits;

            double end = eventStartBeats + eventLenBeats;

            // classify + dedupe
            var acc = new List<(double beat, bool pop, int vel)>();
            for (int i = 0; i < onsets.Count; i++)
            {
                var o = onsets[i];
                if (o.beat < eventStartBeats || o.beat >= end) continue;

                // POCKET-2 (D-PKT-LANES2=C): pop first (both-lists => pop),
                // null list = v1 family, non-null list replaces it outright.
                bool pop;
                bool isPop = popLanes != null
                    ? LaneListContains(popLanes, o.instrument)
                    : IsPocketSnare(o.instrument);
                if (isPop) pop = true;
                else
                {
                    bool isSlap = slapLanes != null
                        ? LaneListContains(slapLanes, o.instrument)
                        : IsPocketKick(o.instrument);
                    if (isSlap) pop = false;
                    else continue;
                }

                // POCKET-2 (D-PKT-VEL2=B): additive per-class boost, clamped
                // 1..127. boost 0 is exact identity (input already 1..127).
                int vel = Mathf.Clamp(
                    o.velocity + (pop ? popBoost : slapBoost), 1, 127);

                int idx = acc.FindIndex(a => a.beat == o.beat);
                if (idx < 0)
                {
                    acc.Add((o.beat, pop, vel));
                }
                else if (pop != acc[idx].pop)
                {
                    if (pop) acc[idx] = (o.beat, true, vel); // pop wins
                }
                else if (vel > acc[idx].vel)
                {
                    acc[idx] = (acc[idx].beat, acc[idx].pop, vel);
                }
            }

            if (acc.Count == 0) return hits;
            acc.Sort((a, b) => a.beat.CompareTo(b.beat));

            for (int i = 0; i < acc.Count; i++)
            {
                double gapEnd = (i + 1 < acc.Count) ? acc[i + 1].beat : end;
                double len = Math.Min(
                    Math.Min(gapEnd - acc[i].beat, end - acc[i].beat),
                    PocketMaxGateBeats);
                hits.Add(new PocketHit(acc[i].beat, len, acc[i].vel, acc[i].pop));
            }
            return hits;
        }

        /// <summary>
        /// BASS-WALK-1: root/3rd/5th (first Min(3, chordPcs.Length) tones) stacked
        /// strictly ascending from the drawn root octave — each tone placed in the
        /// nearest octave above the previous note. Deterministic; no rng.
        /// This 2-arg form is ceiling-free (byte-identical to pre-B3 behavior;
        /// the existing WALK-1 pins run against it).
        /// </summary>
        public static NoteTheory[] BuildWalkVoicing(NoteName[] chordPcs, int rootOct)
            => BuildWalkVoicing(chordPcs, rootOct, int.MaxValue);

        /// <summary>
        /// B3 BASS-REG-1 (D-REG-3=B): ceiling-aware walk voicing. Builds the
        /// WALK-1 stack, then — while its TOP note exceeds <paramref name="ceiling"/>
        /// — rebuilds the whole stack one octave lower. A whole-voicing fold:
        /// shape, intervals, pitch-class order and strict ascent are preserved
        /// (the stacker is octave-invariant). The ceiling wins over the band
        /// floor; the only stop is the MIDI floor itself (root &gt;= 12 before a
        /// fold, so the folded root never goes below note 0). Deterministic,
        /// pure, no rng — the D-WALK-RNG=A argument is untouched.
        /// </summary>
        public static NoteTheory[] BuildWalkVoicing(
            NoteName[] chordPcs, int rootOct, int ceiling)
        {
            var notes = StackWalkVoicing(chordPcs, rootOct);
            while (notes.Length > 0 &&
                   notes[notes.Length - 1].NoteNumber > ceiling &&
                   notes[0].NoteNumber >= 12)
            {
                rootOct -= 1;
                notes = StackWalkVoicing(chordPcs, rootOct);
            }
            return notes;
        }

        /// <summary>The WALK-1 stacker verbatim (root-anchored, strictly
        /// ascending, wrapping tones lifted one octave).</summary>
        private static NoteTheory[] StackWalkVoicing(NoteName[] chordPcs, int rootOct)
        {
            int count = Math.Min(3, chordPcs.Length);
            var notes = new NoteTheory[count];
            if (count == 0) return notes;
            notes[0] = NoteTheory.Get(chordPcs[0], rootOct);
            for (int i = 1; i < count; i++)
            {
                var n = NoteTheory.Get(chordPcs[i], rootOct);
                if (n.NoteNumber <= notes[i - 1].NoteNumber)
                    n = NoteTheory.Get(chordPcs[i], rootOct + 1);
                notes[i] = n;
            }
            return notes;
        }

        /// <summary>
        /// B3 WALK-2 (D-W2-VOCAB=B / D-W2-LAST=A / D-W2-RNG=B / D-W2-REG):
        /// plans the improvised walking line for one chord event — hitCount
        /// pitches, one per engine arpeggio hit. Deterministic pure function
        /// of its arguments; ZERO rng (the variation source is a pure integer
        /// mix of (walkSeed, eventIndex, hitIndex) — the VelocityJitter
        /// idiom).
        ///
        /// Shape:
        /// - hit 0: the event root at the §2 drawn octave (the WALK-1 anchor);
        /// - middle hits: chord tones placed in the octave NEAREST to the
        ///   previous note (never the same pitch) — usually the closest such
        ///   tone, sometimes the 2nd/3rd closest (the mix decides), with
        ///   ArpeggioDown biasing equal-distance ties downward;
        /// - last hit: a chromatic (±1) or whole-step (±2) approach note into
        ///   <paramref name="nextRootPc"/> placed nearest to the previous
        ///   note — the thing that makes a walk read as a walk. The caller
        ///   passes the NEXT event's root, wrapping to the first event
        ///   (D-W2-LAST=A).
        ///
        /// Register (D-W2-REG): every planned note folds -12 while above
        /// <paramref name="ceiling"/> (per-note adaptation of D-REG-3=B; the
        /// unit here is the note — there is no voicing shape to preserve).
        /// Approach notes may dip below the §2 band floor — accepted, low is
        /// safe on a bass (B3 acta). Under a tight ceiling a fold may land on
        /// the previous pitch; the ceiling wins over variety.
        /// </summary>
        public static NoteTheory[] BuildWalkLine(
            NoteName[] chordPcs,
            NoteName nextRootPc,
            int rootOct,
            int ceiling,
            int hitCount,
            bool descendBias,
            int walkSeed,
            int eventIndex)
        {
            if (hitCount <= 0 || chordPcs == null || chordPcs.Length == 0)
                return Array.Empty<NoteTheory>();

            var line = new NoteTheory[hitCount];
            line[0] = FoldUnderCeiling(
                NoteTheory.Get(chordPcs[0], rootOct).NoteNumber, ceiling);
            if (hitCount == 1) return line;

            // Middle hits: chord tones near the previous note.
            var cands = new List<int>(chordPcs.Length);
            for (int k = 1; k <= hitCount - 2; k++)
            {
                int prev = line[k - 1].NoteNumber;
                cands.Clear();
                for (int i = 0; i < chordPcs.Length; i++)
                {
                    int n = NearestPitch(chordPcs[i], prev);
                    if (n != prev && n >= 0 && n <= 127) cands.Add(n);
                }
                if (cands.Count == 0)
                {
                    // Degenerate voicing (every pc lands on prev): hold.
                    line[k] = line[k - 1];
                    continue;
                }

                bool preferUp = !descendBias;
                cands.Sort((a, b) =>
                {
                    int da = Math.Abs(a - prev), db = Math.Abs(b - prev);
                    if (da != db) return da - db;
                    bool aUp = a > prev, bUp = b > prev;
                    if (aUp != bUp) return aUp == preferUp ? -1 : 1;
                    return a - b; // deterministic total order
                });

                double r = WalkMix01(walkSeed, eventIndex, k, 0u);
                int idx = r < 0.55 ? 0 : (r < 0.85 ? 1 : 2);
                if (idx > cands.Count - 1) idx = cands.Count - 1;
                line[k] = FoldUnderCeiling(cands[idx], ceiling);
            }

            // Last hit: approach note into the next event's root.
            int prevN = line[hitCount - 2].NoteNumber;
            int target = NearestPitch(nextRootPc, prevN);
            double r2 = WalkMix01(walkSeed, eventIndex, hitCount - 1, 1u);
            int offset = r2 < 0.35 ? -1 : r2 < 0.70 ? +1 : r2 < 0.85 ? -2 : +2;
            int approach = target + offset;
            if (approach == prevN) approach = target - offset; // never re-strike
            if (approach < 0) approach = target + Math.Abs(offset);   // MIDI floor
            if (approach > 127) approach = target - Math.Abs(offset); // MIDI top
            line[hitCount - 1] = FoldUnderCeiling(approach, ceiling);
            return line;
        }

        /// <summary>B3 WALK-2: the pitch of class <paramref name="pc"/>
        /// closest to <paramref name="reference"/> (ties break LOW — it is a
        /// bass). Pure; may return a value below 0 for references near the
        /// MIDI floor, which callers filter or clamp.</summary>
        public static int NearestPitch(NoteName pc, int reference)
        {
            int rel = ((reference - (int)pc) % 12 + 12) % 12;
            int below = reference - rel;
            int above = below + 12;
            return (reference - below <= above - reference) ? below : above;
        }

        /// <summary>B3 WALK-2 (D-W2-REG): per-note register fold — -12 while
        /// above the ceiling, stopped only by the MIDI floor. Total.</summary>
        private static NoteTheory FoldUnderCeiling(int noteNumber, int ceiling)
        {
            while (noteNumber > ceiling && noteNumber - 12 >= 0) noteNumber -= 12;
            if (noteNumber < 0) noteNumber = 0;
            if (noteNumber > 127) noteNumber = 127;
            return NoteTheory.Get((SevenBitNumber)noteNumber);
        }

        // B3 WALK-2 (D-W2-RNG=B): pure integer mix, the VelocityJitter idiom
        // (lowbias32 finalizer; distinct odd fold constants so the
        // (event, hit) matrix is not symmetric). Deliberately DUPLICATED
        // rather than exposing VelocityJitter's private helper: the struct's
        // byte-identity radius stays zero.
        private const uint WalkEventFold = 0x9E3779B9u; // golden ratio
        private const uint WalkHitFold = 0x85EBCA6Bu;   // murmur3 finalizer

        /// <summary>Uniform double in [0, 1) for (event, hit, salt) under the
        /// walk substream seed. Pure, allocation-free, runtime-stable
        /// (integer-only mixing — exactly pinnable goldens).</summary>
        internal static double WalkMix01(
            int walkSeed, int eventIndex, int hitIndex, uint salt)
        {
            unchecked
            {
                uint x = WalkAvalanche((uint)walkSeed
                    ^ ((uint)eventIndex * WalkEventFold));
                x = WalkAvalanche(x ^ ((uint)hitIndex * WalkHitFold) ^ salt);
                return x / 4294967296.0; // [0, 1)
            }
        }

        // lowbias32 finalizer (Bret Mulvey / H. Wellons) — the VelocityJitter
        // constants, verbatim.
        private static uint WalkAvalanche(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>
        /// B3 BASS-REG-1 (D-REG-4=B / D-REG-1=C): the §2 octave band, as a
        /// pure seam (the ResolveArticulation idiom). Two octaves in DryWetMidi
        /// terms — <c>octaveMin-1 .. min(octaveMin, octaveMax-1)</c> — where
        /// the -1 is the authored→DryWetMidi octave CONVERSION (the same one
        /// behind chord/melody's <c>octaveMin-1 .. octaveMax-1</c>), so in
        /// authored octaves the band reads <c>octaveMin .. octaveMin+1</c>,
        /// ceiling-capped. The outer Max collapses a degenerate asset
        /// (octaveMax &lt;= octaveMin) to a single octave; it never inverts.
        /// </summary>
        public static (int minOct, int maxOct) ResolveOctaveBand(
            int octaveMin, int octaveMax)
        {
            int minOct = Math.Max(0, octaveMin - 1);
            int maxOct = Math.Max(minOct, Math.Min(octaveMin, octaveMax - 1));
            return (minOct, maxOct);
        }

        /// <summary>
        /// B3 (D-REG-1=C): the hard emission ceiling as a MIDI note number —
        /// B at the top of the declared register (authored octave
        /// <paramref name="octaveMax"/> = DryWetMidi octave octaveMax-1, note
        /// number octaveMax*12 + 11), clamped to the MIDI range.
        /// </summary>
        public static int ResolveRegisterCeiling(int octaveMax)
            => Math.Min(127, octaveMax * 12 + 11);

        /// <summary>
        /// B3 (D-REG-2=B): pop pitch resolution, as a pure seam. The pop is
        /// the selected note +12 when that fits the ceiling (and the MIDI
        /// range — also closes a latent out-of-range Get for extreme assets);
        /// otherwise it FOLDS back onto the selected note. Only the pitch
        /// folds: pop classification, boosts, pop-wins dedupe and the gate are
        /// decided upstream and untouched.
        /// </summary>
        public static NoteTheory ResolvePopNote(NoteName pc, int oct, int ceiling)
        {
            var selected = NoteTheory.Get(pc, oct);
            int popNumber = selected.NoteNumber + 12;
            return (popNumber > ceiling || popNumber > 127)
                ? selected
                : NoteTheory.Get(pc, oct + 1);
        }

        /// <summary>
        /// CA-F2 articulation resolution (internal test seam, mirroring the
        /// ChordTrackComposer card-resolve pattern). SD-F2-4=A: the selection is
        /// a persistent field on <see cref="BasslineCardConfigSO"/> in the
        /// track's Style slot (D-EXP1=A). SD-F2-5=A: any other bundle type in
        /// the slot (including BackingCardConfigSO) resolves to the defaults, so
        /// an unset bass track is bit-identical regardless of what the backing
        /// track selects.
        /// </summary>
        public static (ChordExpressionType expression, ArpeggioRate rate)
            ResolveArticulation(SongConfig.PartConfig.TrackConfig cfg)
        {
            var style = cfg?.Parameters?.Style as BasslineCardConfigSO;
            return style != null
                ? (style.chordExpression, style.arpeggioRate)
                : (ChordExpressionType.Block, ArpeggioRate.Eighth);
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                chunk = new TrackChunk();
                file.Chunks.Add(chunk);
            }

            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[BassTrackComposer] Instrument bank is not numeric: '{inst.BankName}', fallback to 0");
                bank = 0;
            }

            chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)bank)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)0)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
            { Channel = (FourBitNumber)channel, DeltaTime = 1 });
        }
    }
}
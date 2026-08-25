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
    /// MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-SURF=A): pocketMode=SelfPocket — an
    /// AUTONOMOUS slap/pop figure over the shared progression. The card's own
    /// cycled Slap/Pop/Rest pattern (meter-anchored grid) is the hit source;
    /// planning is a pure function (BuildSelfPocketPlan: zero rng, zero
    /// cross-track reads — never wakes the ALWTTT boundary §8.4 hash duty).
    /// Rendering reuses the ENTIRE SlapPocket pipeline downstream of the plan:
    /// PocketHit segments, pop +12 with the D-REG-2=B ceiling fold, the
    /// D-PKT-GATE=A gate, per-hit jitter refold. Velocity base is the chord
    /// EVENT's authored velocity (vs the drum step's in SlapPocket) with the
    /// same additive boosts. Off and SlapPocket are byte-identical to
    /// pre-SLAPFIG output by construction (entry-branch only).
    /// MGP-ALWTTT-BASS-SLAPFIG-2 (D-SF2-VOCAB=C / D-SF2-PITCH=A / D-SF2-VEL=B
    /// / D-SF2-GATE=B / D-SF2-SWING=A): the SelfPocket articulation vocabulary.
    /// SelfPocketStep gains Ghost / GhostPop / HammerOn / PullOff (append-only;
    /// v1-only patterns render byte-identical). The plan stays PITCH-FREE:
    /// PocketHit carries an articulation CLASS, and every class's pitch is a
    /// pure call-site law — Slap/Ghost on the selected note, Pop/GhostPop
    /// through ResolvePopNote, HammerOn/PullOff at selected + card-declared
    /// semitone offset through ResolveOffsetNote (ceiling/floor folded).
    /// Velocity (D-SF2-VEL=B): Slap/Pop keep the v1 additive-boost law
    /// verbatim; new classes are a fixed multiplicative FACTOR of the event
    /// velocity (no boosts) so dynamics separate by proportion instead of
    /// flattening against the 127 clamp. Gate (D-SF2-GATE=B): ghost classes
    /// get a card-authored click-length ceiling; everything else keeps
    /// PocketMaxGateBeats. Swing (D-SF2-SWING=A): doctrine pinned — if it ever
    /// exists it is a CARD field, never read from the drummer — implementation
    /// deferred. Zero new ctx.rng draws; the planner stays pure; SlapPocket
    /// and Off are byte-identical to pre-SLAPFIG-2 output by construction.
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
    /// MGP-ALWTTT-BASS-BEND-1 (D-BEND-GEST=A / D-BEND-EMIT=B / D-BEND-DEG=A
    /// / D-BEND-ANCHOR=A / D-BEND-RESET=A / D-BEND-RANGE=A): TRUE legato for
    /// HammerOn/PullOff. A legato hit no longer strikes a note: the nearest
    /// preceding note-emitting hit becomes its CARRIER (BuildLegatoCarrierMap,
    /// a pure coalescing pass -- the PLAN is untouched byte-for-byte), the
    /// carrier's gate extends through its legato tail
    /// (ResolveLegatoGroupEndBeats, the declared gate-law change), and each
    /// tail hit becomes a STEP pitch bend gesture at its tick -- interval in
    /// SCALE DEGREES resolved from the pitch the chain has reached
    /// (ResolveLegatoDeltaSemitones; card fields hammerOffsetDegrees /
    /// pullOffsetDegrees, default +1/-1), cumulative detune for chains,
    /// reset at the carrier's note-off. Gestures are applied as POST-BUILD
    /// surgery via PitchBendWriter (ticks converted with the same
    /// beatSpan/tempoMap the notes use), before ForceAllChannel /
    /// StampBankAndPatch. An ORPHAN legato hit (opening its event window)
    /// degrades to an attacked note at the degree-resolved interval (warn
    /// once per Compose). Plans without legato classes take a carrier map of
    /// all -1 and an empty gesture list: the emission loop is line-for-line
    /// SLAPFIG-2 and the writer never runs -- structural byte-identity,
    /// pinned by the Ghost-vocabulary render canary. ZERO new ctx.rng draws.
    /// MGP-ALWTTT-BASS-PHRASE-1 (D-PH-SURF=D / D-PH-LEN=A / D-PH-ANCHOR=A
    /// / D-PH-FILL=C / D-PH-SCOPE=A / D-PH-BYTE=A / D-PH-SEAM=A /
    /// D-PH-INDEX=A / SD-PH-1..3=A): phrase-aware SelfPocket. The card gains
    /// an authored phrase length and a bar-substitution table (slot ->
    /// pattern variants); bar = floor(part beat / beatsPerBar) — METER
    /// absolute — slot = bar % phraseLength, phraseIndex = bar /
    /// phraseLength. A substituted slot plays one of its variants
    /// (SeededMix: pure integer mix of (phrase seed, phraseIndex, slot),
    /// the WalkMix01 idiom with its own duplicated constants; RoundRobin:
    /// phraseIndex % count); with the phrase ACTIVE every pattern — body
    /// included — indexes from its bar start (D-PH-INDEX=A). The extended
    /// BuildSelfPocketPlan overload carries it all as ARGUMENTS (still a
    /// pure static function: zero rng, zero cross-track reads — the §8.4
    /// autonomy pin holds); the legacy 8-arg signature delegates with a
    /// null table and is line-for-line the SLAPFIG-2b planner. The single
    /// OFF gate is the table being empty (D-PH-BYTE=A) — phrase length and
    /// the selection toggle are inert without it, so pre-PHRASE cards and
    /// SlapPocket/Off render byte-identical by construction. Table-defect
    /// degradation is LOCAL (SD-PH-1=A: duplicate slot -> last wins;
    /// out-of-range / empty variants -> inert), warned once per Compose.
    /// The phrase seed derives via SongOrchestrator.StableHash32
    /// ("|selfphrase") composer-side — a recorded deviation from the
    /// Resolve*-in-orchestrator convention to hold this batch's touched
    /// files to the verified-fresh pair; relocation is a no-render-change
    /// refactor candidate. ZERO new ctx.rng draws.
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

            // MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-SURF=A): SelfPocket source —
            // resolved once at entry, exactly the SlapPocket discipline. The
            // plan is a pure function of card + meter + progression event:
            // ZERO rng draws, ZERO cross-track reads (this mode never calls
            // GetRhythmOnsetsForPart, so it cannot wake the ALWTTT boundary
            // §8.4 consumer-side hash duty — the explicit design win of the
            // ask). SlapPocket and SelfPocket are mutually exclusive by the
            // enum; the per-event branch keys on which source field is
            // non-null, so Off keeps both null and the loop body stays
            // draw-for-draw AND value-for-value the decoupled path.
            IReadOnlyList<SelfPocketStep> selfPocketPattern = null;
            var selfPocketSubdivision = SelfPocketSubdivision.Beat;

            // SLAPFIG-2 (D-SF2-PITCH=A): card-declared semitone offsets for
            // the HammerOn/PullOff pitch law. Read only when SelfPocket is
            // requested; consumed only by hits of those classes.
            // BEND-1 (D-BEND-DEG=A) re-laws these: the offsets are now
            // SCALE DEGREES (default +1/-1 -- the scale neighbour; the Part
            // tonality decides each step's semitone size).
            int hammerOffsetDegrees = 1;
            int pullOffsetDegrees = -1;

            // SLAPFIG-2b: per-class velocity factors and ghost gate ceiling,
            // authored on the card. Defaults to the shipped tuning when no
            // card is present.
            var selfPocketTuning = SelfPocketTuning.Default;

            List<MidiGenerator.RhythmOnset> pocketOnsets = null;
            var pocketMode = bassStyle != null
                ? bassStyle.pocketMode : PocketCouplingMode.Off;
            bool pocketRequested = pocketMode == PocketCouplingMode.SlapPocket;
            bool selfPocketRequested = pocketMode == PocketCouplingMode.SelfPocket;
            if (selfPocketRequested)
            {
                pocketSlapBoost = bassStyle.pocketSlapBoost;
                pocketPopBoost = bassStyle.pocketPopBoost;
                selfPocketSubdivision = bassStyle.selfPocketSubdivision;
                hammerOffsetDegrees = bassStyle.hammerOffsetDegrees;
                pullOffsetDegrees = bassStyle.pullOffsetDegrees;
                selfPocketTuning = SelfPocketTuning.FromCard(bassStyle);

                var pat = bassStyle.selfPocketPattern;
                bool hasHit = false;
                if (pat != null)
                {
                    for (int k = 0; k < pat.Count; k++)
                    {
                        if (pat[k] != SelfPocketStep.Rest) { hasHit = true; break; }
                    }
                }

                if (hasHit)
                {
                    selfPocketPattern = pat;
                }
                else
                {
                    Debug.LogWarning(
                        $"[BassTrackComposer] pocketMode=SelfPocket but " +
                        $"selfPocketPattern is empty or all-Rest on card " +
                        $"'{bassStyle.name}'. Rendering the decoupled figure.");
                }
            }

            // MGP-ALWTTT-BASS-PHRASE-1: phrase surface, resolved once at
            // entry. The SINGLE gate is the substitution table (D-PH-BYTE=A):
            // null/empty table => every local below stays at its inert
            // default and the planner call degenerates to the SLAPFIG-2b
            // path, byte-identical by construction. Table validation is the
            // pure seam ResolvePhraseSubstitutions (SD-PH-1=A: local
            // degradation, last-wins duplicates, inert out-of-range); its
            // warnings are batched into ONE LogWarning per Compose. The
            // phrase seed is a dedicated derived substream key consumed only
            // by the pure variant mix — never a stream, never ctx.rng.
            IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<SelfPocketStep>>>
                phraseSubstitutions = null;
            int phraseLengthBars = 0;
            var phraseVariantSelection =
                BasslineCardConfigSO.SelfPocketVariantSelection.SeededMix;
            int phraseSeed = 0;
            if (selfPocketPattern != null &&
                bassStyle.selfPocketBarSubstitutions != null &&
                bassStyle.selfPocketBarSubstitutions.Count > 0)
            {
                var phraseWarnings = new List<string>();
                phraseSubstitutions = ResolvePhraseSubstitutions(
                    bassStyle.selfPocketBarSubstitutions,
                    bassStyle.selfPocketPhraseLengthBars,
                    phraseWarnings);
                if (phraseWarnings.Count > 0)
                {
                    Debug.LogWarning(
                        $"[BassTrackComposer] PHRASE-1 on card " +
                        $"'{bassStyle.name}': " +
                        string.Join(" ", phraseWarnings));
                }
                if (phraseSubstitutions != null)
                {
                    phraseLengthBars = bassStyle.selfPocketPhraseLengthBars;
                    phraseVariantSelection = bassStyle.selfPocketVariantSelection;
                    phraseSeed = ResolvePhraseSeed(trackSeed);
                }
            }
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

            // PHRASE-1 (D-PH-INDEX=A): non-divisor advisory — a pattern
            // whose length does not divide the bar's grid steps restarts
            // mid-cycle every bar under the within-bar law. Legal (the
            // restart IS the law), but usually a typo, so it warns once
            // per Compose. Informative only; never alters the plan.
            if (phraseSubstitutions != null)
            {
                double phStep =
                    selfPocketSubdivision ==
                        SelfPocketSubdivision.QuarterBeat ? 0.25 :
                    selfPocketSubdivision ==
                        SelfPocketSubdivision.HalfBeat ? 0.5 : 1.0;
                int stepsPerBar = (int)Math.Round(beatsPerBar / phStep);
                var offenders = new List<string>();
                if (selfPocketPattern.Count > 0 &&
                    stepsPerBar % selfPocketPattern.Count != 0)
                    offenders.Add($"body({selfPocketPattern.Count})");
                foreach (var kv in phraseSubstitutions)
                    for (int v = 0; v < kv.Value.Count; v++)
                        if (kv.Value[v].Count > 0 &&
                            stepsPerBar % kv.Value[v].Count != 0)
                            offenders.Add(
                                $"bar{kv.Key}.variant{v}({kv.Value[v].Count})");
                if (offenders.Count > 0)
                {
                    Debug.LogWarning(
                        $"[BassTrackComposer] PHRASE-1 on card " +
                        $"'{bassStyle.name}': pattern length(s) do not " +
                        $"divide the bar's {stepsPerBar} grid steps — " +
                        $"{string.Join(", ", offenders)}. They restart at " +
                        $"every bar (the within-bar law); if the phase " +
                        $"carry-over of v1 was intended, author the full " +
                        $"bar. (Warned once per render.)");
                }
            }
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

            // F-TON-WALK-DRIFT-1 (D-W2-FLOOR=B): the walk's lower bound — one
            // octave BELOW the §2 band floor. The octave of slack preserves
            // the documented allowance that an approach note may dip under the
            // band (B3 acta, "low is safe on a bass"); what it stops is the
            // CUMULATIVE drift of a long event window bottoming out at MIDI 0.
            int registerFloor = NoteTheory.Get(NoteName.C, minOct).NoteNumber - 12;

            var rng = ctx?.rng ?? new System.Random();

            // POCKET-1: per-Compose segment buffer (local — no composer state).
            var _segments = new List<EmitSegment>(4);

            // BEND-1 (D-BEND-EMIT=B): per-Compose legato gesture buffer, in
            // TICKS. The converter uses the SAME beatSpan/tempoMap the notes
            // go through, so gestures and note-ons share one rounding and
            // can never drift apart. Empty list => the writer is a hard
            // no-op (byte-identity for every render without legato tails).
            // Warn latch: the orphan degrade warns once per Compose.
            var legatoGestures = new List<PitchBendWriter.StepGesture>(4);
            bool warnedOrphanLegato = false;
            long BeatsToTicks(double beats) =>
                TimeConverter.ConvertFrom(beatSpan.Multiply(beats), tempoMap);

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
                // MGP-TONALITY-1 D-TON10 (F-TON-ACC-1): the authored
                // accidental is part of the chord's identity — bII is Db in
                // C, not D. ChordTrackComposer has always applied it; the
                // bass did not, so on accidental-bearing progressions the
                // two tracks played DIFFERENT chords a semitone apart
                // (confirmed in runtime on Prog_Min_Napolitana_bII).
                // Inert (identity) for every event with degreeAccidental==0.
                var degreeRoot = TransposeNoteName(
                    scaleNames[(int)ce.degree], ce.degreeAccidental);
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

                // SLAPFIG-1: the SelfPocket plan is the SlapPocket plan with
                // an internal source — same PocketHit list, same emission
                // branch, same jitter refold, same ResolvePopNote fold. Runs
                // AFTER both §2 selection draws and reads no rng, exactly the
                // POCKET-1 structural argument.
                List<PocketHit> pocketPlan = pocketOnsets != null
                    ? BuildPocketPlan(pocketOnsets, startBeats, lenBeats,
                        pocketSlapBoost, pocketPopBoost,
                        pocketSlapLanes, pocketPopLanes)
                    : selfPocketPattern != null
                        ? BuildSelfPocketPlan(startBeats, lenBeats,
                            selfPocketSubdivision, selfPocketPattern,
                            ce.velocity, pocketSlapBoost, pocketPopBoost,
                            selfPocketTuning,
                            // PHRASE-1: inert quintet while the table is
                            // null — the overload reduces to SLAPFIG-2b.
                            beatsPerBar, phraseLengthBars,
                            phraseSubstitutions, phraseVariantSelection,
                            phraseSeed)
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
                    // SLAPFIG-2 (D-SF2-PITCH=A): every class's pitch is a pure
                    // call-site law. Slap/Ghost sound on the SELECTED note;
                    // Pop/GhostPop in the pop domain (ResolvePopNote, fold
                    // intact). BEND-1 (D-BEND-GEST=A): HammerOn/PullOff are
                    // no longer notes - a hit with a carrier becomes a STEP
                    // BEND GESTURE on it; only the ORPHAN case (opening its
                    // event window) still resolves a pitch and attacks.
                    // SlapPocket plans only Slap/Pop, so its pitch mapping is
                    // byte-identical to the pre-SLAPFIG-2 pop ternary.
                    var popNote = ResolvePopNote(pc, oct, registerCeiling);

                    // BEND-1: pure coalescing pass. For a plan without legato
                    // classes every entry is -1 and the loop below is
                    // line-for-line the SLAPFIG-2 loop - same segment count,
                    // same arguments, same ForEvent(k) jitter derivations
                    // (ForEvent is a pure per-index avalanche, not a stream)
                    // - structural byte-identity.
                    var carrierMap = BuildLegatoCarrierMap(pocketPlan);

                    // Legato chain state (D-BEND-ANCHOR=A): reset at every
                    // note-emitting hit. carrierMap[0] is always -1, so the
                    // state is always seeded before any tail reads it.
                    int chainPitch = 0;
                    double chainDetune = 0;
                    double chainGroupEndBeats = 0;

                    for (int k = 0; k < pocketPlan.Count; k++)
                    {
                        var h = pocketPlan[k];

                        if (carrierMap[k] >= 0)
                        {
                            // Legato TAIL: no note-on. Interval in scale
                            // degrees from the pitch the chain has reached
                            // (D-BEND-DEG=A); cumulative detune so chains
                            // pass the writer an absolute target; reset at
                            // the carrier group's end (D-BEND-RESET=A - the
                            // writer coalesces mid-chain resets away).
                            int degOff =
                                h.articulation == SelfPocketStep.HammerOn
                                    ? hammerOffsetDegrees : pullOffsetDegrees;
                            int delta = ResolveLegatoDeltaSemitones(
                                chainPitch, degOff, scaleNames);
                            chainDetune += delta;
                            chainPitch += delta;
                            legatoGestures.Add(new PitchBendWriter.StepGesture(
                                BeatsToTicks(h.startBeats),
                                chainDetune,
                                BeatsToTicks(chainGroupEndBeats)));
                            continue;
                        }

                        NoteTheory hitNote;
                        switch (h.articulation)
                        {
                            case SelfPocketStep.Pop:
                            case SelfPocketStep.GhostPop:
                                hitNote = popNote;
                                break;
                            case SelfPocketStep.HammerOn:
                            case SelfPocketStep.PullOff:
                                // ORPHAN legato (first hit of the event
                                // window): nothing sounds yet, nothing to
                                // bend. Degrade to an attacked note at the
                                // degree-resolved interval from the SELECTED
                                // note - SLAPFIG-2 behavior with the interval
                                // law upgraded to degrees.
                                if (!warnedOrphanLegato)
                                {
                                    warnedOrphanLegato = true;
                                    Debug.LogWarning(
                                        $"[BassTrackComposer] BEND-1: a " +
                                        $"{h.articulation} step opens a " +
                                        $"chord-event window (first at beat " +
                                        $"{h.startBeats:0.##}) with no " +
                                        $"previous hit to bend from; " +
                                        $"emitting attacked note(s). Put a " +
                                        $"sounding step before it in " +
                                        $"selfPocketPattern for true legato. " +
                                        $"(Warned once per render.)");
                                }
                                hitNote = ResolveOffsetNote(
                                    pc, oct,
                                    ResolveLegatoDeltaSemitones(
                                        note.NoteNumber,
                                        h.articulation ==
                                            SelfPocketStep.HammerOn
                                            ? hammerOffsetDegrees
                                            : pullOffsetDegrees,
                                        scaleNames),
                                    registerCeiling);
                                break;
                            default: // Slap, Ghost
                                hitNote = note;
                                break;
                        }

                        // Note-emitting hit => new chain anchor.
                        chainPitch = hitNote.NoteNumber;
                        chainDetune = 0;

                        // BEND-1 carrier gate (declared law change): a hit
                        // followed by legato tails spans THROUGH them; its
                        // planned len applies verbatim otherwise (identity
                        // for tail-less plans).
                        double segLen = h.lenBeats;
                        double groupEnd = ResolveLegatoGroupEndBeats(
                            pocketPlan, carrierMap, k);
                        if (groupEnd > h.startBeats + h.lenBeats)
                            segLen = groupEnd - h.startBeats;
                        chainGroupEndBeats = groupEnd;

                        _segments.Add(new EmitSegment(
                            new[] { hitNote },
                            h.startBeats, segLen,
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
                    // D-TON10: the approach target must be the REAL next
                    // root (D-W2-LAST's recorded accidental-blindness is
                    // hereby retired, together with the main lookup above).
                    var nextRootPc = TransposeNoteName(
                        scaleNames[(int)nextCe.degree], nextCe.degreeAccidental);

                    var grid = ChordArticulator.PlanHits(
                        effectiveExpression, effectiveRate, startBeats, lenBeats,
                        beatsPerBar, 1, ce.velocity, evJitter);
                    var line = BuildWalkLine(
                        chordPcs, nextRootPc, oct, registerCeiling, grid.Count,
                        effectiveExpression == ChordExpressionType.ArpeggioDown,
                        walkSeed, eventIndex, registerFloor);

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
                    // MGP-TONALITY-1 Task 2 (log-only): audit every note-on
                    // this segment will strike. chordPcs is the event's own
                    // harmonic context, accidental-blind as the bass computes
                    // it today (F-TON-ACC-1).
                    for (int ai = 0; ai < seg.playable.Length; ai++)
                        Diagnostics.TonalityAudit.Check(
                            "Bass", seg.playable[ai], scaleNames, chordPcs,
                            seg.startBeats, beatsPerBar,
                            part.Tonality, part.RootNote,
                            "bass-segment", seg.expression.ToString(),
                            _settings == null || _settings.tonalityAuditShowInfo);

                    _articulator.Emit(pb, seg.playable, seg.startBeats, seg.lenBeats,
                                      beatSpan, beatsPerBar, seg.velocity, stepsPerBeat,
                                      seg.expression, seg.rate, seg.jitter);
                }
            }

            var file = pb.Build().ToFile(tempoMap);

            // BEND-1 (D-BEND-EMIT=B): apply the planned legato gestures as
            // post-build surgery -- BEFORE ForceAllChannel (which stamps the
            // bend events like any channel event) and BEFORE
            // StampBankAndPatch (whose program-change tick shift then
            // applies to bends and notes alike, keeping them aligned).
            // Empty list = hard no-op = the byte-identity guarantee for
            // every render without legato.
            PitchBendWriter.ApplyStepGestures(file, legatoGestures);

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
                              : "") +
                          (selfPocketRequested
                              ? $" | SLAPFIG-1 SelfPocket pattern=" +
                                (selfPocketPattern != null
                                    ? $"{selfPocketPattern.Count} steps " +
                                      $"({selfPocketSubdivision})"
                                    : "EMPTY(decoupled)") +
                                $" boosts=({pocketSlapBoost:+0;-0;0}," +
                                $"{pocketPopBoost:+0;-0;0})" +
                                $" | SLAPFIG-2/BEND-1 degOffsets=" +
                                $"(H{hammerOffsetDegrees:+0;-0;0}," +
                                $"P{pullOffsetDegrees:+0;-0;0})" +
                                $" legatoGestures={legatoGestures.Count}" +
                                (phraseSubstitutions != null
                                    ? $" | PHRASE-1 len={phraseLengthBars}" +
                                      $" slots={phraseSubstitutions.Count}" +
                                      $" sel={phraseVariantSelection}"
                                    : "")
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

            /// <summary>True when this hit sounds in the POP PITCH DOMAIN
            /// (+12 with the D-REG-2=B ceiling fold): Pop and — since
            /// SLAPFIG-2 — GhostPop. Kept for the SlapPocket pins and for
            /// pitch-domain queries; the emission branch itself keys on
            /// <see cref="articulation"/>.</summary>
            public readonly bool pop;

            /// <summary>SLAPFIG-2 (D-SF2-VOCAB=C): the articulation class of
            /// this hit. SlapPocket only ever produces Slap/Pop (the 4-arg
            /// constructor); SelfPocket may produce any non-Rest member.
            /// Never Rest.</summary>
            public readonly BasslineCardConfigSO.SelfPocketStep articulation;

            /// <summary>v1 constructor (SlapPocket, and the pre-SLAPFIG-2
            /// test surface): maps the pop flag onto the Slap/Pop classes.
            /// Byte-compatible with every existing call site.</summary>
            public PocketHit(double startBeats, double lenBeats,
                int velocity, bool pop)
                : this(startBeats, lenBeats, velocity,
                       pop ? BasslineCardConfigSO.SelfPocketStep.Pop
                           : BasslineCardConfigSO.SelfPocketStep.Slap)
            {
            }

            /// <summary>SLAPFIG-2 constructor: full articulation class. The
            /// pop flag derives from the class's pitch domain (Pop and
            /// GhostPop sound +12-folded).</summary>
            public PocketHit(double startBeats, double lenBeats,
                int velocity, BasslineCardConfigSO.SelfPocketStep articulation)
            {
                this.startBeats = startBeats;
                this.lenBeats = lenBeats;
                this.velocity = velocity;
                this.articulation = articulation;
                this.pop =
                    articulation == BasslineCardConfigSO.SelfPocketStep.Pop ||
                    articulation == BasslineCardConfigSO.SelfPocketStep.GhostPop;
            }
        }

        /// <summary>POCKET-1 (D-PKT-GATE=A): percussive gate ceiling, in Part
        /// beats. Hit length = min(gap to next planned hit, remaining event
        /// window, this ceiling).</summary>
        public const double PocketMaxGateBeats = 0.5;

        /// <summary>
        /// SLAPFIG-2b: the per-class NUMBERS for the SelfPocket articulation
        /// laws, fed from the card (<see cref="BasslineCardConfigSO"/>) and
        /// defaulted to the shipped tuning. The LAWS themselves do not move:
        /// D-SF2-VEL=B stays "a factor of the event velocity, never an
        /// additive boost", D-SF2-GATE=B stays "ghosts get a click ceiling,
        /// everything else keeps PocketMaxGateBeats". Only the constants are
        /// authorable — the ear at the gig, not the catalogue, gets the last
        /// word on how loud a ghost is.
        ///
        /// Slap and Pop have no entry on purpose: they keep the v1 additive
        /// boost law verbatim, which is what makes v1-only patterns
        /// byte-identical to SLAPFIG-1.
        /// </summary>
        public readonly struct SelfPocketTuning
        {
            public readonly float ghost;
            public readonly float ghostPop;
            public readonly float hammerOn;
            public readonly float pullOff;
            public readonly double ghostGateBeats;

            public SelfPocketTuning(float ghost, float ghostPop,
                float hammerOn, float pullOff, double ghostGateBeats)
            {
                this.ghost = ghost;
                this.ghostPop = ghostPop;
                this.hammerOn = hammerOn;
                this.pullOff = pullOff;
                this.ghostGateBeats = ghostGateBeats;
            }

            /// <summary>Shipped tuning — mirrors the card field defaults.
            /// Used when no card is supplied (tests, and any future call site
            /// that plans without a card).</summary>
            public static SelfPocketTuning Default =>
                new SelfPocketTuning(0.60f, 0.50f, 0.60f, 0.55f, 0.10);

            public static SelfPocketTuning FromCard(BasslineCardConfigSO card)
                => card == null
                    ? Default
                    : new SelfPocketTuning(
                        card.ghostVelocityFactor,
                        card.ghostPopVelocityFactor,
                        card.hammerOnVelocityFactor,
                        card.pullOffVelocityFactor,
                        card.ghostGateBeats);
        }

        /// <summary>
        /// SLAPFIG-2 (D-SF2-VEL=B): the SelfPocket per-class velocity law, as
        /// a pure seam. Slap/Pop: clamp(eventVelocity + class boost, 1..127)
        /// — the D-SFIG-VEL=A law verbatim (byte-identity for v1 patterns).
        /// Ghost/GhostPop/HammerOn/PullOff: clamp(round(eventVelocity *
        /// class factor), 1..127) — no boosts, proportions preserved under
        /// hot-authored events (the gig's (+64,+64) saturation finding).
        /// Rest (never planned) and unknown future members fall through to
        /// the slap law defensively.
        /// </summary>
        public static int ResolveSelfPocketVelocity(
            BasslineCardConfigSO.SelfPocketStep step,
            int eventVelocity, int slapBoost, int popBoost,
            SelfPocketTuning tuning)
        {
            switch (step)
            {
                case BasslineCardConfigSO.SelfPocketStep.Pop:
                    return Mathf.Clamp(eventVelocity + popBoost, 1, 127);
                case BasslineCardConfigSO.SelfPocketStep.Ghost:
                    return Mathf.Clamp(
                        Mathf.RoundToInt(eventVelocity * tuning.ghost), 1, 127);
                case BasslineCardConfigSO.SelfPocketStep.GhostPop:
                    return Mathf.Clamp(
                        Mathf.RoundToInt(eventVelocity * tuning.ghostPop), 1, 127);
                case BasslineCardConfigSO.SelfPocketStep.HammerOn:
                    return Mathf.Clamp(
                        Mathf.RoundToInt(eventVelocity * tuning.hammerOn), 1, 127);
                case BasslineCardConfigSO.SelfPocketStep.PullOff:
                    return Mathf.Clamp(
                        Mathf.RoundToInt(eventVelocity * tuning.pullOff), 1, 127);
                default: // Slap, and any defensive fall-through
                    return Mathf.Clamp(eventVelocity + slapBoost, 1, 127);
            }
        }

        /// <summary>SLAPFIG-2 (D-SF2-GATE=B): the per-class gate ceiling, as
        /// a pure seam. Ghost classes get the click ceiling; everything else
        /// keeps the POCKET-1 ceiling (byte-identity for v1 patterns).</summary>
        public static double ResolveSelfPocketGateCeiling(
            BasslineCardConfigSO.SelfPocketStep step, SelfPocketTuning tuning)
            => step == BasslineCardConfigSO.SelfPocketStep.Ghost ||
               step == BasslineCardConfigSO.SelfPocketStep.GhostPop
                ? tuning.ghostGateBeats
                : PocketMaxGateBeats;

        /// <summary>
        /// SLAPFIG-2 (D-SF2-PITCH=A): offset-note resolution for HammerOn /
        /// PullOff, as a pure seam (the ResolvePopNote idiom). The hit sounds
        /// at the SELECTED note + a card-declared semitone offset — the plan
        /// stays pitch-free and memoryless; relative-to-previous-hit fidelity
        /// is a declared loss (catalogue §B.5), revisit by ear. Register: -12
        /// while above the ceiling (the D-W2-REG per-note fold); a result
        /// below the MIDI floor folds UP an octave (never clamp-distorts the
        /// interval), then hard-clamps as a last resort.
        /// </summary>
        public static NoteTheory ResolveOffsetNote(
            NoteName pc, int oct, int offsetSemitones, int ceiling)
        {
            int n = NoteTheory.Get(pc, oct).NoteNumber + offsetSemitones;
            while (n > ceiling && n - 12 >= 0) n -= 12;
            if (n < 0) n += 12;
            if (n < 0) n = 0;
            if (n > 127) n = 127;
            return NoteTheory.Get((SevenBitNumber)n);
        }

        /// <summary>
        /// BEND-1 (D-BEND-GEST=A): pure coalescing pass over a pocket plan.
        /// One entry per hit: the plan index of the hit's legato CARRIER when
        /// the hit is a HammerOn/PullOff with something to bend from (the
        /// nearest preceding hit that emits its own note-on; chains collapse
        /// onto the chain's root carrier), or -1 when the hit emits its own
        /// note-on - every non-legato class, and an ORPHAN legato hit at
        /// index 0 of the event window. The PLAN itself is never modified:
        /// BuildSelfPocketPlan and all its pins are untouched byte-for-byte;
        /// the reinterpretation lives entirely in this map. Plans without
        /// legato classes map to all -1 - the consuming loop is then
        /// line-for-line the SLAPFIG-2 loop (structural byte-identity).
        /// Pure, deterministic, no rng.
        /// </summary>
        public static int[] BuildLegatoCarrierMap(
            IReadOnlyList<PocketHit> plan)
        {
            var map = new int[plan?.Count ?? 0];
            for (int k = 0; k < map.Length; k++)
            {
                var a = plan[k].articulation;
                bool legato =
                    a == BasslineCardConfigSO.SelfPocketStep.HammerOn ||
                    a == BasslineCardConfigSO.SelfPocketStep.PullOff;
                map[k] = legato && k > 0
                    ? (map[k - 1] == -1 ? k - 1 : map[k - 1])
                    : -1;
            }
            return map;
        }

        /// <summary>
        /// BEND-1 (D-BEND-GEST=A): the end, in Part beats, of the legato
        /// group anchored at <paramref name="carrierIndex"/> - the end of its
        /// LAST tail hit, or the carrier's own planned end when no tails
        /// follow (the identity case). Tails of one carrier are consecutive
        /// by construction of the carrier map, and plan hits are time-sorted
        /// with positive lengths, so the last tail's end is the group
        /// maximum. Pure.
        /// </summary>
        public static double ResolveLegatoGroupEndBeats(
            IReadOnlyList<PocketHit> plan, int[] carrierMap, int carrierIndex)
        {
            double end = plan[carrierIndex].startBeats
                       + plan[carrierIndex].lenBeats;
            for (int j = carrierIndex + 1;
                 j < plan.Count && carrierMap[j] == carrierIndex; j++)
                end = plan[j].startBeats + plan[j].lenBeats;
            return end;
        }

        /// <summary>
        /// BEND-1 (D-BEND-DEG=A): resolves a legato interval declared in
        /// SCALE DEGREES to a signed semitone delta from
        /// <paramref name="fromNoteNumber"/>. Walks the scale one degree at a
        /// time (any |offset|, octave crossings for free; the tonality
        /// decides each step's size - a harmonic-minor augmented second is a
        /// legitimate 3-semitone step, clamped later to the GM range by the
        /// writer, declared degradation). If the starting pitch class is NOT
        /// a scale member (borrowed/requalified chord tone), falls back to
        /// whole tones - offsetDegrees * 2 semitones, the SLAPFIG-2 chromatic
        /// law per degree; silent by design (a data-dependent per-hit
        /// condition, not a config degrade - deviation from warn-max on
        /// record). Pure, deterministic, no rng.
        /// </summary>
        public static int ResolveLegatoDeltaSemitones(
            int fromNoteNumber, int offsetDegrees, NoteName[] scaleNames)
        {
            if (offsetDegrees == 0) return 0;
            if (scaleNames == null || scaleNames.Length == 0)
                return offsetDegrees * 2;

            int pc = ((fromNoteNumber % 12) + 12) % 12;
            int idx = -1;
            for (int i = 0; i < scaleNames.Length; i++)
                if ((int)scaleNames[i] == pc) { idx = i; break; }
            if (idx < 0) return offsetDegrees * 2; // off-scale fallback

            int len = scaleNames.Length;
            int dir = Math.Sign(offsetDegrees);
            int steps = Math.Abs(offsetDegrees);
            int cur = pc;
            int delta = 0;
            for (int s = 0; s < steps; s++)
            {
                int next = ((idx + dir) % len + len) % len;
                int nextPc = (int)scaleNames[next];
                int step = dir > 0
                    ? ((nextPc - cur) % 12 + 12) % 12
                    : -(((cur - nextPc) % 12 + 12) % 12);
                if (step == 0) step = dir * 12; // duplicate-pc guard
                delta += step;
                cur = nextPc;
                idx = next;
            }
            return delta;
        }

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
        /// MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-SURF=A / D-SFIG-PAT=A /
        /// D-SFIG-VEL=A): pure per-event SELF pocket planner — the autonomous
        /// counterpart of <see cref="BuildPocketPlan"/>. Deterministic by
        /// construction: zero rng, zero cross-track reads.
        ///
        /// Grid (D-SFIG-PAT=A): candidate hits at multiples of the
        /// subdivision step (Beat = 1.0, HalfBeat = 0.5) in PART beats,
        /// anchored to the METER (part beat 0) and intersected with the event
        /// window [eventStart, eventStart + eventLen) — inclusive start,
        /// exclusive end, the BuildPocketPlan convention. The cycled pattern
        /// is indexed by the ABSOLUTE grid index (% pattern length), so the
        /// figure keeps phase across chord changes — the same absolute-beat
        /// footing SlapPocket's published onsets stand on. Rest = skip.
        /// The small epsilons absorb FP noise from non-power-of-two
        /// subdivision grids (e.g. triplet stepsPerBeat), where the event's
        /// beat bounds are not exactly representable.
        ///
        /// Velocity (D-SFIG-VEL=A, extended by SLAPFIG-2 D-SF2-VEL=B): base
        /// is the chord EVENT's authored velocity (the decoupled path's
        /// base). Slap/Pop: + slapBoost / popBoost, clamped 1..127 — the v1
        /// additive law verbatim. Ghost/GhostPop/HammerOn/PullOff: a fixed
        /// per-class FACTOR of the event velocity, no boosts (see
        /// <see cref="ResolveSelfPocketVelocity"/>).
        ///
        /// Lengths: min(gap to next planned hit, remaining window, per-class
        /// ceiling) — <see cref="PocketMaxGateBeats"/> for everything except
        /// the ghost classes, which take the card's ghostGateBeats
        /// (SLAPFIG-2 D-SF2-GATE=B; v1 patterns keep D-PKT-GATE=A exactly).
        ///
        /// Empty result (window shorter than one grid step, or the cycle
        /// lands only on Rest inside the window) = "figure applies" — the
        /// caller's per-event fallback, identical to an empty SlapPocket
        /// plan. Pop pitch (+12) and the D-REG-2=B ceiling fold stay at the
        /// call site (ResolvePopNote), untouched.
        /// </summary>
        public static List<PocketHit> BuildSelfPocketPlan(
            double eventStartBeats,
            double eventLenBeats,
            SelfPocketSubdivision subdivision,
            IReadOnlyList<SelfPocketStep> pattern,
            int eventVelocity,
            int slapBoost = 0,
            int popBoost = 0,
            SelfPocketTuning? tuning = null)
            // PHRASE-1 (D-PH-SEAM=A): the pre-PHRASE signature delegates
            // with a null table — the extended body's null-table branch is
            // the v1 lookup verbatim, so every existing pin runs against
            // identical behaviour (structural byte-identity by delegation).
            => BuildSelfPocketPlan(eventStartBeats, eventLenBeats,
                subdivision, pattern, eventVelocity, slapBoost, popBoost,
                tuning, beatsPerBar: 0, phraseLengthBars: 0,
                barSubstitutions: null,
                variantSelection:
                    BasslineCardConfigSO.SelfPocketVariantSelection.SeededMix,
                phraseSeed: 0);

        /// <summary>
        /// MGP-ALWTTT-BASS-PHRASE-1 (D-PH-SEAM=A): the phrase-aware
        /// overload. Still a PURE function of its arguments — zero rng,
        /// zero cross-track reads (the SLAPFIG-1 autonomy pin's ground).
        /// <paramref name="barSubstitutions"/> null => the per-index lookup
        /// is <c>pattern[g % Count]</c>, the SLAPFIG-2b law verbatim.
        /// Non-null => ResolvePhraseStep decides each grid index's step:
        /// meter-absolute bar (D-PH-ANCHOR=A), slot = bar % phrase length,
        /// variant per the selection law (SD-PH-2/3=A), and WITHIN-BAR
        /// indexing for every pattern (D-PH-INDEX=A). Everything downstream
        /// of the step lookup — velocity law, gate law, accumulator,
        /// epsilons — is shared and untouched.
        /// </summary>
        public static List<PocketHit> BuildSelfPocketPlan(
            double eventStartBeats,
            double eventLenBeats,
            SelfPocketSubdivision subdivision,
            IReadOnlyList<SelfPocketStep> pattern,
            int eventVelocity,
            int slapBoost,
            int popBoost,
            SelfPocketTuning? tuning,
            double beatsPerBar,
            int phraseLengthBars,
            IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<SelfPocketStep>>>
                barSubstitutions,
            BasslineCardConfigSO.SelfPocketVariantSelection variantSelection,
            int phraseSeed)
        {
            var hits = new List<PocketHit>();
            if (pattern == null || pattern.Count == 0 || eventLenBeats <= 0)
                return hits;

            var tun = tuning ?? SelfPocketTuning.Default;

            // SLAPFIG-2b: QuarterBeat = sixteenths. Unknown future members
            // fall through to Beat rather than throwing.
            double step =
                subdivision == SelfPocketSubdivision.QuarterBeat ? 0.25 :
                subdivision == SelfPocketSubdivision.HalfBeat ? 0.5 : 1.0;
            double end = eventStartBeats + eventLenBeats;

            // First absolute grid index at or after the event start.
            int g = (int)Math.Ceiling(eventStartBeats / step - 1e-9);
            if (g < 0) g = 0;

            // SLAPFIG-2: the accumulator carries the articulation CLASS; the
            // velocity is the per-class law (Slap/Pop = v1 additive boosts
            // verbatim; new classes = fixed factor, no boosts — D-SF2-VEL=B).
            var acc = new List<(double beat, SelfPocketStep art, int vel)>();
            for (; ; g++)
            {
                double beat = g * step;
                if (beat >= end - 1e-9) break; // exclusive end

                // PHRASE-1: null table = the v1 lookup verbatim (the
                // delegation path); otherwise the phrase law decides.
                var s = barSubstitutions == null
                    ? pattern[g % pattern.Count]
                    : ResolvePhraseStep(beat, step, pattern, beatsPerBar,
                        phraseLengthBars, barSubstitutions,
                        variantSelection, phraseSeed);
                if (s == SelfPocketStep.Rest) continue;

                int vel = ResolveSelfPocketVelocity(
                    s, eventVelocity, slapBoost, popBoost, tun);
                acc.Add((beat, s, vel));
            }

            for (int i = 0; i < acc.Count; i++)
            {
                double gapEnd = (i + 1 < acc.Count) ? acc[i + 1].beat : end;
                // SLAPFIG-2 (D-SF2-GATE=B): per-class ceiling — ghosts are
                // clicks; everything else keeps the POCKET-1 ceiling. Same
                // min(gap, window, ceiling) law otherwise.
                double len = Math.Min(
                    Math.Min(gapEnd - acc[i].beat, end - acc[i].beat),
                    ResolveSelfPocketGateCeiling(acc[i].art, tun));
                hits.Add(new PocketHit(acc[i].beat, len, acc[i].vel, acc[i].art));
            }
            return hits;
        }

        /// <summary>
        /// PHRASE-1 (D-PH-ANCHOR=A / D-PH-INDEX=A / SD-PH-2/3=A): the
        /// per-grid-index step law with the phrase ACTIVE, as a pure seam.
        /// Bar = floor(beat / beatsPerBar + eps) — METER absolute, part
        /// beat 0 anchored, integer bar lengths in part beats (the TS
        /// table's BeatsPerMeasure) though the math stays in doubles with
        /// the planner's own epsilon discipline. Slot = bar % phrase
        /// length; a substituted slot resolves its variant via
        /// <see cref="ResolvePhraseVariantIndex"/>; EVERY effective pattern
        /// (body included) indexes from its bar start — the within-bar law
        /// that keeps fills aligned. A defensive empty effective pattern
        /// yields Rest (unreachable through ResolvePhraseSubstitutions,
        /// which drops empty variants).
        /// </summary>
        public static SelfPocketStep ResolvePhraseStep(
            double beat,
            double step,
            IReadOnlyList<SelfPocketStep> body,
            double beatsPerBar,
            int phraseLengthBars,
            IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<SelfPocketStep>>>
                barSubstitutions,
            BasslineCardConfigSO.SelfPocketVariantSelection variantSelection,
            int phraseSeed)
        {
            if (beatsPerBar <= 0 || phraseLengthBars < 1)
                return body[(int)Math.Floor(beat / step + 1e-9) % body.Count];

            int bar = (int)Math.Floor(beat / beatsPerBar + 1e-9);
            int slot = bar % phraseLengthBars;
            int phraseIndex = bar / phraseLengthBars;

            IReadOnlyList<SelfPocketStep> effective = body;
            if (barSubstitutions != null &&
                barSubstitutions.TryGetValue(slot, out var variants) &&
                variants != null && variants.Count > 0)
            {
                effective = variants[ResolvePhraseVariantIndex(
                    variantSelection, phraseSeed, phraseIndex, slot,
                    variants.Count)];
            }

            if (effective == null || effective.Count == 0)
                return SelfPocketStep.Rest;

            // D-PH-INDEX=A: within-bar index — grid points elapsed since
            // the bar started. For meters where beatsPerBar is a multiple
            // of the step (every shipped case: BeatsPerMeasure is an
            // integer and steps are 1 / 0.5 / 0.25) this is exact; the
            // floor+epsilon keeps it total for any future fractional bar.
            double barStartBeat = bar * beatsPerBar;
            int gBar = (int)Math.Floor((beat - barStartBeat) / step + 1e-9);
            if (gBar < 0) gBar = 0;
            return effective[gBar % effective.Count];
        }

        /// <summary>
        /// PHRASE-1 (SD-PH-2=A / SD-PH-3=A): the variant-selection law, as
        /// a pure seam. RoundRobin: phraseIndex % count (negative-safe).
        /// SeededMix: floor(mix01 * count) with mix01 in [0, 1) — the index
        /// is provably in range; the defensive clamp only guards FP edge
        /// noise. One variant short-circuits (both laws agree at 0).
        /// </summary>
        public static int ResolvePhraseVariantIndex(
            BasslineCardConfigSO.SelfPocketVariantSelection selection,
            int phraseSeed, int phraseIndex, int slot, int variantCount)
        {
            if (variantCount <= 1) return 0;
            if (selection ==
                BasslineCardConfigSO.SelfPocketVariantSelection.RoundRobin)
            {
                int r = phraseIndex % variantCount;
                return r < 0 ? r + variantCount : r;
            }
            int idx = (int)(PhraseMix01(phraseSeed, phraseIndex, slot, 0u)
                * variantCount);
            return idx >= variantCount ? variantCount - 1 : idx;
        }

        /// <summary>
        /// PHRASE-1 (SD-PH-1=A): pure table validation — the card's
        /// substitution list to the planner's lookup map. LOCAL
        /// degradation: a duplicate barIndex keeps the LAST entry; an
        /// out-of-range barIndex is inert; a variant with no steps is
        /// dropped; an entry left with zero variants is inert. An all-Rest
        /// variant is LEGAL (a silent break bar). Every defect appends one
        /// message to <paramref name="warnings"/> (the caller logs the
        /// batch once per Compose). Returns null when nothing usable
        /// survives — including phraseLengthBars &lt; 1, the one GLOBAL
        /// degrade (a phrase of no bars addresses no slots) — which is the
        /// caller's OFF signal (D-PH-BYTE=A).
        /// </summary>
        public static IReadOnlyDictionary<int,
                IReadOnlyList<IReadOnlyList<SelfPocketStep>>>
            ResolvePhraseSubstitutions(
                IReadOnlyList<BasslineCardConfigSO.SelfPocketBarSubstitution>
                    table,
                int phraseLengthBars,
                List<string> warnings)
        {
            if (table == null || table.Count == 0) return null;
            if (phraseLengthBars < 1)
            {
                warnings?.Add(
                    $"selfPocketPhraseLengthBars={phraseLengthBars} is " +
                    $"invalid (< 1); phrase substitutions disabled.");
                return null;
            }

            var map = new Dictionary<int,
                IReadOnlyList<IReadOnlyList<SelfPocketStep>>>();
            for (int i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry == null) continue;
                if (entry.barIndex < 0 ||
                    entry.barIndex >= phraseLengthBars)
                {
                    warnings?.Add(
                        $"substitution[{i}] barIndex={entry.barIndex} is " +
                        $"outside 0..{phraseLengthBars - 1}; entry ignored.");
                    continue;
                }

                var variants = new List<IReadOnlyList<SelfPocketStep>>();
                if (entry.variants != null)
                {
                    for (int v = 0; v < entry.variants.Count; v++)
                    {
                        var steps = entry.variants[v]?.steps;
                        if (steps == null || steps.Count == 0)
                        {
                            warnings?.Add(
                                $"substitution[{i}] (bar {entry.barIndex}) " +
                                $"variant[{v}] has no steps; variant " +
                                $"dropped.");
                            continue;
                        }
                        variants.Add(steps);
                    }
                }
                if (variants.Count == 0)
                {
                    warnings?.Add(
                        $"substitution[{i}] (bar {entry.barIndex}) has no " +
                        $"usable variants; entry ignored.");
                    continue;
                }
                if (map.ContainsKey(entry.barIndex))
                {
                    warnings?.Add(
                        $"duplicate substitution for bar {entry.barIndex}; " +
                        $"the LAST entry wins.");
                }
                map[entry.barIndex] = variants;
            }
            return map.Count > 0 ? map : null;
        }

        /// <summary>
        /// PHRASE-1: dedicated phrase-substream seed. Consumed only as the
        /// KEY of the pure variant mix (PhraseMix01) — never a stream.
        /// Derivation lives composer-side this batch (calling the SAME
        /// public StableHash32) as a recorded deviation from the
        /// Resolve*-in-SongOrchestrator convention, to hold the touched
        /// file set to the verified-fresh pair; relocating it is a
        /// no-render-change refactor candidate (same string, same hash).
        /// </summary>
        public static int ResolvePhraseSeed(int trackSeed)
            => SongOrchestrator.StableHash32($"{trackSeed}|selfphrase");

        // PHRASE-1 (SD-PH-3=A): pure integer mix for variant selection —
        // the WalkMix01 idiom, deliberately DUPLICATED (avalanche and all)
        // with its own fold constants so the (phraseIndex, slot) matrix is
        // asymmetric and no other seam's byte-identity radius grows.
        private const uint PhraseIndexFold = 0xC2B2AE35u; // murmur3 fin #2
        private const uint PhraseSlotFold = 0x27D4EB2Fu;  // xxh32 prime #5

        /// <summary>Uniform double in [0, 1) for (phraseIndex, slot, salt)
        /// under the phrase substream seed. Pure, allocation-free,
        /// integer-only mixing — exactly pinnable goldens. PUBLIC per the
        /// §5.6 named-seam convention (the F-IVT-STALE record: the
        /// InternalsVisibleTo escape hatch is not relied upon).</summary>
        public static double PhraseMix01(
            int phraseSeed, int phraseIndex, int slot, uint salt)
        {
            unchecked
            {
                uint x = PhraseAvalanche((uint)phraseSeed
                    ^ ((uint)phraseIndex * PhraseIndexFold));
                x = PhraseAvalanche(x ^ ((uint)slot * PhraseSlotFold) ^ salt);
                return x / 4294967296.0; // [0, 1)
            }
        }

        // lowbias32 finalizer — the VelocityJitter/Walk constants, verbatim
        // (the idiom is shared; the instance is duplicated on purpose).
        private static uint PhraseAvalanche(uint x)
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
        /// F-TON-WALK-DRIFT-1 (D-W2-FLOOR=B): middle-hit selection is
        /// prev-relative only and carries a negative expected drift, so over
        /// long event windows the line used to descend out of the band to the
        /// MIDI floor. Notes now also fold +12 while below
        /// <paramref name="floor"/> (ceiling wins). This CONTAINS the drift;
        /// the selection asymmetry itself is deferred (D-W2-DRIFT).
        /// </summary>
        public static NoteTheory[] BuildWalkLine(
            NoteName[] chordPcs,
            NoteName nextRootPc,
            int rootOct,
            int ceiling,
            int hitCount,
            bool descendBias,
            int walkSeed,
            int eventIndex,
            int floor = int.MinValue)
        {
            if (hitCount <= 0 || chordPcs == null || chordPcs.Length == 0)
                return Array.Empty<NoteTheory>();

            var line = new NoteTheory[hitCount];
            line[0] = FoldIntoRegister(
                NoteTheory.Get(chordPcs[0], rootOct).NoteNumber, ceiling, floor);
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
                line[k] = FoldIntoRegister(cands[idx], ceiling, floor);
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
            line[hitCount - 1] = FoldIntoRegister(approach, ceiling, floor);
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

        /// <summary>
        /// B3 WALK-2 (D-W2-REG + D-W2-FLOOR=B): two-sided per-note register
        /// fold. -12 while above <paramref name="ceiling"/> (verbatim WALK-2
        /// behaviour), then +12 while below <paramref name="floor"/> — the
        /// F-TON-WALK-DRIFT-1 containment. Folding is octave-wise, so pitch
        /// class, chord-tone membership and approach intervals are invariant.
        ///
        /// The CEILING WINS: the up-fold never lifts a note above the ceiling,
        /// so a degenerate asset (floor >= ceiling) degrades to the old
        /// ceiling-only behaviour rather than oscillating. floor ==
        /// int.MinValue disables the up-fold entirely (byte-identical to
        /// pre-fix WALK-2 — the default for callers that do not pass one).
        /// Total.
        /// </summary>
        private static NoteTheory FoldIntoRegister(
            int noteNumber, int ceiling, int floor)
        {
            while (noteNumber > ceiling && noteNumber - 12 >= 0) noteNumber -= 12;
            while (noteNumber < floor &&
                   noteNumber + 12 <= 127 &&
                   noteNumber + 12 <= ceiling) noteNumber += 12;
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
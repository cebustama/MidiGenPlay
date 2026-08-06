using Melanchall.DryWetMidi.Standards;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Bassline authoring bundle (CA-F2, SD-F2-4=A). Fills the TrackStyleBundles
    /// §4.1 "Bassline (TBD)" row. Resolved by BassTrackComposer from the track's
    /// Style slot, mirroring the ChordTrackComposer / BackingCardConfigSO pattern.
    ///
    /// SD-F2-5=A: the bass articulation is fully independent of the backing
    /// card — a bass track with no BasslineCardConfigSO in its Style slot always
    /// renders Block, regardless of what the backing track selects.
    ///
    /// MGP-ALWTTT-BASS-BEND-1 (D-BEND-DEG=A): the HammerOn/PullOff interval
    /// fields moved from SEMITONES to SCALE DEGREES (hammerOffsetDegrees /
    /// pullOffsetDegrees, defaults +1/-1) — the tonality decides each step's
    /// size. Renamed pre-production with [FormerlySerializedAs]; no shipped
    /// assets carried the old fields.
    ///
    /// See runtime/SSoT_Composer_Bass_Track.md.
    ///
    /// MGP-ALWTTT-BASS-PHRASE-1 (D-PH-SURF=D / D-PH-FILL=C): the SelfPocket
    /// PHRASE surface — an authored phrase length plus a bar-substitution
    /// table (bar slot -> one or more pattern variants) that lets the figure
    /// breathe across bars: body bars cycle selfPocketPattern; substituted
    /// slots (canonically the last bar of the phrase) play their own
    /// pattern. The table being empty is the single OFF gate (D-PH-BYTE=A):
    /// every other phrase field is inert until at least one substitution
    /// exists, so cards serialized before this batch render byte-identical.
    /// </summary>
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/BasslineCardConfig")]
    public class BasslineCardConfigSO : TrackStyleBundleSO
    {
        [Header("Chord Expression (Tier 1 articulation)")]
        [Tooltip("Rhythmic articulation applied over the bass line for the whole " +
                 "render (CA-F2, D-EXP1=A: persistent card-level selection, not a " +
                 "transient hint). Figures apply to the per-event selected note " +
                 "(SD-F2-2=A): on a monophonic line ArpeggioUp/Down become a " +
                 "repeated-note pulse at the arpeggio rate. Block (default) = one " +
                 "sustained note per chord event, bit-identical to legacy output. " +
                 "See runtime/SSoT_Composer_Bass_Track.md.")]
        public ChordExpressionType chordExpression = ChordExpressionType.Block;

        [Tooltip("Note rate for ArpeggioUp / ArpeggioDown (repeated-note pulse on " +
                 "a monophonic line); ignored by all other expressions. Eighth " +
                 "(default) = two hits per beat, built on the Part's beat span " +
                 "(meter authority), independent of the asset grid.")]
        public ArpeggioRate arpeggioRate = ArpeggioRate.Eighth;

        [Tooltip("CA-V1 (D-V1-BASS=B). Probability of re-rolling the figure and " +
                 "the rate on each chord event when the corresponding Random " +
                 "sentinel is selected. 1 = fresh roll per chord event; 0 = one " +
                 "choice for the whole render (per-loop variety comes from the " +
                 "host's per-render seed). Inert otherwise.")]
        [Range(0f, 1f)]
        public float randomRerollChance = 1f;

        [Tooltip("CA-V1 (D-V1-BASS=B). Optional weighted roll pool, consumed only " +
                 "when Chord Expression = Random. Empty = uniform over the six " +
                 "Tier-1 figures. NOTE for a monophonic line: ArpeggioUp and " +
                 "ArpeggioDown are indistinguishable (SD-F2-2=A), so the uniform " +
                 "pool gives the repeated-note pulse double weight — use this " +
                 "list to rebalance.")]
        public List<ChordExpressionWeight> randomFigureWeights =
            new List<ChordExpressionWeight>();

        [Tooltip("CA-V1 seeded per-hit velocity jitter, in MIDI velocity units " +
                 "(uniform in [-n, +n], clamped 1..127). 0 (default) = exact " +
                 "legacy velocities. Independent of the backing card's value.")]
        [Range(0, 32)]
        public int velocityJitter = 0;

        /// <summary>
        /// BASS-WALK-1 (D-WALK-SURF=A). Bass-only interpretation of the arpeggio
        /// figures. Append-only; values serialized, never renumbered.
        /// </summary>
        public enum BassArpeggioToneMode
        {
            RepeatedNote = 0,   // SD-F2-2=A legacy: pulse on the selected note
            ChordToneWalk = 1,  // BASS-WALK-1: cycle root/3rd/5th
            ImprovisedWalk = 2, // B3 WALK-2: seeded walking line w/ approach notes
        }

        [Tooltip("BASS-WALK-1 / B3 WALK-2. How ArpeggioUp/Down read a " +
         "monophonic line. RepeatedNote (default) = legacy pulse on the " +
         "selected note, bit-identical to CA-V1 output. ChordToneWalk = " +
         "cycle root/3rd/5th stacked ascending from the drawn bass octave. " +
         "ImprovisedWalk = a seeded walking line with bar-to-bar variation: " +
         "root anchor, chord-tone middles near the previous note, and a " +
         "chromatic/whole-step approach note into the NEXT chord's root " +
         "(wrapping to the first event) — rhythm, accents and jitter are " +
         "exactly the arpeggio's; ArpeggioDown biases contour ties downward. " +
         "Variation comes from a dedicated seed substream (same seed = same " +
         "line). Ignored by all non-arpeggio figures and bypassed on " +
         "pocketed events.")]
        public BassArpeggioToneMode arpeggioToneMode = BassArpeggioToneMode.RepeatedNote;

        /// <summary>
        /// MGP-ALWTTT-BASS-POCKET-1 (D-PKT-WHAT=SlapPocket). Opt-in coupling of
        /// the bass line to the resolved Rhythm pattern. Append-only; values
        /// serialized, never renumbered.
        /// </summary>
        public enum PocketCouplingMode
        {
            Off = 0,        // decoupled: figures/walk as before, bit-identical
            SlapPocket = 1, // kick→slap (selected note), snare→pop (+12)
            // MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-SURF=A): AUTONOMOUS slap/pop
            // figure over the shared progression — the card's own cycled
            // pattern is the hit source, ZERO reads of the Rhythm track
            // (never wakes the ALWTTT boundary §8.4 consumer-side hash duty).
            SelfPocket = 2,
        }

        [Header("Pocket Coupling (MGP-ALWTTT-BASS-POCKET-1)")]
        [Tooltip("Opt-in coupling to the Rhythm track's resolved pattern. " +
                 "Off (default) = decoupled, bit-identical to previous output. " +
                 "SlapPocket = per chord event, if the drummer's resolved GRID " +
                 "pattern has kick/snare onsets inside the event window, the " +
                 "bass replaces its figure there with slap hits on the kick " +
                 "onsets (the event's selected note) and pop hits on the snare " +
                 "onsets (same note one octave up), at the drum step's " +
                 "velocity, short percussive gate. Windows without onsets — " +
                 "and renders without a published source (no Rhythm track in " +
                 "the part, Rhythm composed AFTER the bass in track-list " +
                 "order, or a procedural/legacy rhythm path) — fall back to " +
                 "the decoupled figure: warn at most, never an error, never " +
                 "silence. The slap/pop TIMBRE comes from the bass patch " +
                 "(e.g. GM Slap Bass 1/2 on the MIDIInstrumentSO) — this mode " +
                 "shapes timing, register and dynamics only. " +
                 "SelfPocket (MGP-ALWTTT-BASS-SLAPFIG-1) = an AUTONOMOUS " +
                 "slap/pop figure that follows the shared progression using " +
                 "the card's own cycled pattern (Self Pocket section below) " +
                 "— no Rhythm track needed, zero cross-track reads, " +
                 "deterministic with no rng; same pop (+12, ceiling-folded), " +
                 "gate and timbre rules as SlapPocket.")]
        public PocketCouplingMode pocketMode = PocketCouplingMode.Off;

        [Tooltip("MGP-ALWTTT-BASS-POCKET-2 (D-PKT-VEL2=B). Additive velocity " +
                 "offset for SLAP hits, applied to the drum step's resolved " +
                 "velocity before the final 1..127 clamp. 0 (default) = exact " +
                 "POCKET-1 dynamics (byte-identical). Use to rebalance the bass " +
                 "against a soft- or hot-authored drum pattern without touching " +
                 "the pattern itself. Inert when pocketMode = Off or when the " +
                 "event window falls back to the decoupled figure.")]
        [Range(-64, 64)]
        public int pocketSlapBoost = 0;

        [Tooltip("MGP-ALWTTT-BASS-POCKET-2 (D-PKT-VEL2=B). Additive velocity " +
                 "offset for POP hits (same clamp rule as pocketSlapBoost). " +
                 "0 (default) = byte-identical to POCKET-1. Independent of the " +
                 "slap boost — pops read weaker than slaps at equal drum " +
                 "velocity, so a positive pop-only boost is the typical fix.")]
        [Range(-64, 64)]
        public int pocketPopBoost = 0;

        [Tooltip("MGP-ALWTTT-BASS-POCKET-2 (D-PKT-LANES2=C). Off (default) = " +
                 "the built-in v1 trigger families (slap: AcousticBassDrum, " +
                 "BassDrum1; pop: AcousticSnare, ElectricSnare; SideStick " +
                 "excluded) — byte-identical to POCKET-1, and the state every " +
                 "pre-POCKET-2 asset deserializes into. On = the two lane " +
                 "lists below REPLACE the families entirely: an empty list " +
                 "disables that trigger class (e.g. pop-only pocket), and a " +
                 "lane present in BOTH lists classifies as pop (consistent " +
                 "with the same-beat pop-wins rule).")]
        public bool pocketCustomLanes = false;

        [Tooltip("MGP-ALWTTT-BASS-POCKET-2. Semantic lanes that trigger SLAP " +
                 "hits when Custom Lanes is on (matched pre kit resolution, " +
                 "immune to PERC-FALLBACK-1 substitutions). Ignored when " +
                 "Custom Lanes is off. Empty + Custom Lanes on = no slap " +
                 "triggers.")]
        public List<GeneralMidiPercussion> pocketSlapLanes =
            new List<GeneralMidiPercussion>();

        [Tooltip("MGP-ALWTTT-BASS-POCKET-2. Semantic lanes that trigger POP " +
                 "hits when Custom Lanes is on. Typical Latin addition: " +
                 "SideStick, so a rim-click backbeat drives the pop. Ignored " +
                 "when Custom Lanes is off. Empty + Custom Lanes on = no pop " +
                 "triggers.")]
        public List<GeneralMidiPercussion> pocketPopLanes =
            new List<GeneralMidiPercussion>();

        /// <summary>
        /// MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-PAT=A): one step of the cycled
        /// SelfPocket pattern. Append-only; values serialized in assets and
        /// never renumbered.
        /// </summary>
        public enum SelfPocketStep
        {
            Slap = 0, // hit on the event's selected note
            Pop = 1,  // hit one octave up (+12, D-REG-2=B ceiling fold — pop identity as SlapPocket)
            Rest = 2, // no hit on this grid position
            // MGP-ALWTTT-BASS-SLAPFIG-2 (D-SF2-VOCAB=C): the slap articulation
            // vocabulary. Append-only over v1; a pattern containing only v1
            // members renders byte-identical to SLAPFIG-1 (test-pinned).
            // "Mute" is deliberately NOT a member: in MIDI a muted note IS a
            // ghost note (minimum-band velocity AND ultra-short gate, both at
            // once — catalogue §B.3 lists ghost/dead/muted as synonyms).
            // LeftHandSlap is deferred: its v1 MIDI parameters are
            // indistinguishable from Ghost (no distinguishing law yet).
            Ghost = 3,    // thumb-side ghost: selected note, low velocity factor, click gate
            GhostPop = 4, // pop-side ghost: pop pitch domain (+12 fold), lowest factor, click gate
            // MGP-ALWTTT-BASS-BEND-1 (D-BEND-GEST=A): TRUE legato — no new
            // attack. The previous sounding hit becomes the CARRIER (its gate
            // extends through the legato step) and a step pitch bend moves
            // the sounding pitch by the degree offset, resolved against the
            // Part scale from the pitch the chain has reached
            // (D-BEND-DEG=A / D-BEND-ANCHOR=A). A legato step that OPENS its
            // event window has nothing to bend and degrades to an attacked
            // note at the same interval (warn).
            HammerOn = 5, // legato: carrier + step bend, +hammerOffsetDegrees in the Part scale
            PullOff = 6,  // legato: carrier + step bend, +pullOffsetDegrees in the Part scale
        }

        /// <summary>
        /// MGP-ALWTTT-BASS-SLAPFIG-1 (D-SFIG-PAT=A): candidate-hit grid for
        /// SelfPocket, in Part beats. Append-only; values serialized.
        /// </summary>
        public enum SelfPocketSubdivision
        {
            Beat = 0,     // one candidate hit per beat
            HalfBeat = 1, // one candidate hit per half beat (eighths)
            // MGP-ALWTTT-BASS-SLAPFIG-2b: sixteenths. The entire classic-funk
            // ghost vocabulary (catalogue §C.2/C.3 — "T . t P", "T t t P")
            // lives on sixteenths; Beat and HalfBeat cannot express it. In
            // 4/4 this is 16 grid slots per bar, so an 16-step pattern is
            // exactly one bar. Append-only; v1 assets are unaffected.
            QuarterBeat = 2,
        }

        [Header("Self Pocket (MGP-ALWTTT-BASS-SLAPFIG-1)")]
        [Tooltip("SelfPocket only: the candidate-hit grid the cycled pattern " +
                 "is laid on, anchored to the METER (part beat 0), not to " +
                 "chord-event starts — the figure keeps phase across chord " +
                 "changes, like SlapPocket's absolute drum onsets do. Ignored " +
                 "by Off and SlapPocket.")]
        public SelfPocketSubdivision selfPocketSubdivision =
            SelfPocketSubdivision.Beat;

        [Tooltip("SelfPocket only: cycled articulation pattern over the grid " +
                 "(step = absolute grid index % pattern length). Default " +
                 "[Slap, Pop] = classic alternation. Empty or all-Rest = the " +
                 "decoupled figure renders (warn at entry — never an error, " +
                 "never silence). Velocity law (SLAPFIG-2, D-SF2-VEL=B): " +
                 "Slap/Pop = the chord event's authored velocity + " +
                 "pocketSlapBoost/pocketPopBoost (additive, clamped 1..127 — " +
                 "exactly v1); Ghost/GhostPop = a fixed per-class FACTOR of " +
                 "the event velocity (no boosts), so classes keep their " +
                 "proportions instead of flattening against the 127 clamp. " +
                 "Legato (BEND-1, D-BEND-GEST=A): HammerOn/PullOff steps do " +
                 "NOT strike a new note — the previous sounding hit becomes " +
                 "the CARRIER: its gate extends through the legato step(s) " +
                 "and a step pitch bend moves the sounding pitch by the " +
                 "card's degree offset (resolved against the Part scale), " +
                 "returning to center at the carrier's note-off. A legato " +
                 "step that OPENS an event window has nothing to bend and " +
                 "degrades to an attacked note (warn once per render). " +
                 "Gate (D-SF2-GATE=B): ghosts get a click-length ceiling; " +
                 "everything else keeps the SlapPocket gate; a carrier " +
                 "followed by legato steps spans through them (BEND-1 " +
                 "declared law change). Register ceiling and timbre rules " +
                 "are exactly SlapPocket's.")]
        public List<SelfPocketStep> selfPocketPattern =
            new List<SelfPocketStep> { SelfPocketStep.Slap, SelfPocketStep.Pop };

        // --- MGP-ALWTTT-BASS-PHRASE-1: phrase-level bar substitutions.
        // D-PH-SURF=D (base pattern + substitution table), D-PH-LEN=A
        // (authored fixed length, purely modular — no part-end lookahead),
        // D-PH-ANCHOR=A (meter-absolute: bar = floor(part beat /
        // beatsPerBar); parts whose first chord event starts late still
        // count the phrase from part beat 0), D-PH-FILL=C (variant LIST per
        // slot; seeded pure-mix selection by default, round-robin as a
        // toggle), D-PH-SCOPE=A (SelfPocket only — SlapPocket takes its grid
        // from the drummer's published onsets), D-PH-INDEX=A (with the
        // phrase active, EVERY pattern — body included — indexes from its
        // bar start; enabling the phrase may re-phase a body whose length
        // does not divide the bar's grid steps, a declared, opt-in change).
        // Unity cannot serialize List<List<T>>, hence the two wrapper
        // classes. Both are plain [Serializable] data — no behaviour.

        /// <summary>One pattern variant for a substituted bar slot. An
        /// all-Rest variant is LEGAL (a silent break bar); an empty steps
        /// list is invalid and dropped with a warning (SD-PH-1=A).</summary>
        [Serializable]
        public class SelfPocketPatternVariant
        {
            [Tooltip("The bar's articulation pattern, same alphabet and " +
                     "same grid as selfPocketPattern. Indexed from the BAR " +
                     "start (D-PH-INDEX=A) and cycled if shorter than the " +
                     "bar. All-Rest = a deliberate silent bar. Empty = " +
                     "invalid, dropped with a warning.")]
            public List<SelfPocketStep> steps = new List<SelfPocketStep>();
        }

        /// <summary>One substituted slot of the phrase. Defect semantics
        /// (SD-PH-1=A): a duplicate barIndex keeps the LAST entry; a
        /// barIndex outside 0..phraseLength-1 is inert; both warn once per
        /// render. Degradation is LOCAL — valid entries survive.</summary>
        [Serializable]
        public class SelfPocketBarSubstitution
        {
            [Tooltip("0-based slot within the phrase this entry replaces. " +
                     "phraseLength-1 is the phrase-closing bar — the " +
                     "'Aeroplane' fill slot. Out of range = entry ignored " +
                     "(warned once per render).")]
            public int barIndex;

            [Tooltip("Pattern variants for this slot. One variant = a fixed " +
                     "fill. Several = the selection law picks one per " +
                     "phrase occurrence (selfPocketVariantSelection). Empty " +
                     "= entry ignored (warned).")]
            public List<SelfPocketPatternVariant> variants =
                new List<SelfPocketPatternVariant>();
        }

        /// <summary>PHRASE-1 variant-selection law. Append-only; values
        /// serialized in assets.</summary>
        public enum SelfPocketVariantSelection
        {
            // SD-PH-2=A: seeded pure mix is the default — deterministic
            // per (phrase seed, phrase index, slot), the WalkMix01 idiom.
            // Same seed => same picks; no ctx.rng stream exists.
            SeededMix = 0,
            // phraseIndex % variant count — mechanical alternation,
            // useful for A/B auditioning and fully seed-independent.
            RoundRobin = 1,
        }

        [Tooltip("PHRASE-1 (D-PH-LEN=A): phrase length in bars. The " +
                 "substitution table's barIndex addresses slots " +
                 "0..length-1; the phrase repeats purely modularly over " +
                 "the part (a trailing partial phrase just truncates — no " +
                 "part-end fill logic in v1, deferred). INERT while the " +
                 "substitution table is empty (D-PH-BYTE=A: the table is " +
                 "the single ON/OFF gate).")]
        [Min(1)]
        public int selfPocketPhraseLengthBars = 4;

        [Tooltip("PHRASE-1 (D-PH-SURF=D): the bar-substitution table. " +
                 "EMPTY = phrase machinery OFF, v1 cycling byte-identical " +
                 "(the single gate, D-PH-BYTE=A). Each entry replaces one " +
                 "phrase slot with its own pattern variant(s); the " +
                 "canonical use is one entry at slot length-1 — the " +
                 "phrase-closing fill.")]
        public List<SelfPocketBarSubstitution> selfPocketBarSubstitutions =
            new List<SelfPocketBarSubstitution>();

        [Tooltip("PHRASE-1 (SD-PH-2=A): how a slot with several variants " +
                 "picks one per phrase occurrence. SeededMix (default) = " +
                 "deterministic seeded pure mix, varied but reproducible; " +
                 "RoundRobin = phraseIndex % count. Inert while the table " +
                 "is empty or no slot has more than one variant.")]
        public SelfPocketVariantSelection selfPocketVariantSelection =
            SelfPocketVariantSelection.SeededMix;


        [Tooltip("MGP-ALWTTT-BASS-BEND-1 (D-BEND-DEG=A). SCALE-DEGREE offset " +
                 "for HammerOn steps. Default +1 — the scale neighbour above; " +
                 "the Part tonality decides whether that is a whole or a half " +
                 "step (a harmonic-minor augmented second lands at 3 " +
                 "semitones and is clamped to the GM ±2 bend range, declared " +
                 "degradation). Resolved from the pitch the legato chain has " +
                 "reached — the CARRIER, D-BEND-ANCHOR=A. If that pitch " +
                 "class is not a scale member (borrowed/requalified chord " +
                 "tone), falls back to whole tones (offset × 2 semitones — " +
                 "the SLAPFIG-2 chromatic law, silent by design). Replaces " +
                 "hammerOffsetSemitones (renamed pre-production; no shipped " +
                 "assets carried the old field). Ignored unless the pattern " +
                 "contains HammerOn steps.")]
        [Range(-7, 7)]
        [FormerlySerializedAs("hammerOffsetSemitones")]
        public int hammerOffsetDegrees = 1;

        [Tooltip("MGP-ALWTTT-BASS-BEND-1 (D-BEND-DEG=A). SCALE-DEGREE offset " +
                 "for PullOff steps. Default -1 — the scale neighbour below, " +
                 "the descending phrase-closer. Same scale resolution, " +
                 "carrier anchor, whole-tone fallback and GM-range clamp " +
                 "rules as hammerOffsetDegrees. Replaces pullOffsetSemitones " +
                 "(renamed pre-production). Ignored unless the pattern " +
                 "contains PullOff steps.")]
        [Range(-7, 7)]
        [FormerlySerializedAs("pullOffsetSemitones")]
        public int pullOffsetDegrees = -1;

        // --- SLAPFIG-2b: per-class tuning, promoted from compile-time
        // constants to card fields (D-SF2-VEL=B stays the LAW — a factor of
        // the event velocity, not an additive boost; only the NUMBERS move to
        // the card). Defaults are the tuned values, so an asset that never
        // touches them behaves like the shipped default. Unity applies these
        // initializers to assets serialized before the fields existed, so
        // older cards deserialize to the defaults rather than to zero.
        // Slap and Pop deliberately have NO factor: they keep the v1 additive
        // boost law, which is what makes v1-only patterns byte-identical.

        [Tooltip("SelfPocket: Ghost velocity as a fraction of the chord " +
                 "event's authored velocity. Default 0.6 (tuned by ear — the " +
                 "catalogue's 0.35 read too quiet through a GM slap patch, " +
                 "whose attack transient is most of the sample). Lower = more " +
                 "of a click under the groove; higher = the ghosts start " +
                 "reading as notes. Result is clamped 1..127.")]
        [Range(0.05f, 1f)]
        public float ghostVelocityFactor = 0.60f;

        [Tooltip("SelfPocket: GhostPop velocity factor — the pop-register " +
                 "shadow. Kept slightly under ghostVelocityFactor (the " +
                 "catalogue's pop ghost is the driest hit of the set).")]
        [Range(0.05f, 1f)]
        public float ghostPopVelocityFactor = 0.50f;

        [Tooltip("SelfPocket: HammerOn velocity factor. BEND-1 note: the " +
                 "legato bend path emits NO new attack, so this factor only " +
                 "applies to the degraded ORPHAN case — a HammerOn that " +
                 "opens its event window and renders as an attacked note. " +
                 "It still feeds the plan (the seam law is unchanged); the " +
                 "carrier-path velocity is simply unused, a BEND-1 declared " +
                 "loss.")]
        [Range(0.05f, 1f)]
        public float hammerOnVelocityFactor = 0.60f;

        [Tooltip("SelfPocket: PullOff velocity factor. Same BEND-1 note as " +
                 "hammerOnVelocityFactor: consumed only by the degraded " +
                 "orphan case.")]
        [Range(0.05f, 1f)]
        public float pullOffVelocityFactor = 0.55f;

        [Tooltip("SelfPocket: gate ceiling for the ghost classes, in Part " +
                 "beats. A ghost is a click, not a short note. Default 0.10. " +
                 "The usual min(gap to next hit, remaining window, ceiling) " +
                 "law applies, so on a dense grid the gap wins anyway. " +
                 "Slap/Pop keep the SlapPocket gate ceiling; a hit followed " +
                 "by legato steps spans through them regardless of class " +
                 "(BEND-1 carrier law).")]
        [Range(0.02f, 0.5f)]
        public float ghostGateBeats = 0.10f;

        private void Reset()
        {
            // Unity editor-invoked message (not an editor-API dependency): assets
            // created via the Create menu tag their cosmetic role as Bassline.
            appliesTo = TrackRole.Bassline;
        }
    }
}
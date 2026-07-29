using Melanchall.DryWetMidi.Standards;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Bassline authoring bundle (CA-F2, SD-F2-4=A). Fills the TrackStyleBundles
    /// �4.1 "Bassline (TBD)" row. Resolved by BassTrackComposer from the track's
    /// Style slot, mirroring the ChordTrackComposer / BackingCardConfigSO pattern.
    ///
    /// SD-F2-5=A: the bass articulation is fully independent of the backing
    /// card � a bass track with no BasslineCardConfigSO in its Style slot always
    /// renders Block, regardless of what the backing track selects.
    ///
    /// See runtime/SSoT_Composer_Bass_Track.md.
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
                 "pool gives the repeated-note pulse double weight � use this " +
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
                 "shapes timing, register and dynamics only.")]
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

        private void Reset()
        {
            // Unity editor-invoked message (not an editor-API dependency): assets
            // created via the Create menu tag their cosmetic role as Bassline.
            appliesTo = TrackRole.Bassline;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

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
        }

        [Tooltip("BASS-WALK-1. How ArpeggioUp/Down read a monophonic line. " +
         "RepeatedNote (default) = legacy pulse on the selected note, " +
         "bit-identical to CA-V1 output. ChordToneWalk = cycle " +
         "root/3rd/5th stacked ascending from the drawn bass octave — " +
         "note this also makes Up and Down distinguishable again, " +
         "removing the pool double-weight noted for RepeatedNote. " +
         "Ignored by all non-arpeggio figures.")]
        public BassArpeggioToneMode arpeggioToneMode = BassArpeggioToneMode.RepeatedNote;

        private void Reset()
        {
            // Unity editor-invoked message (not an editor-API dependency): assets
            // created via the Create menu tag their cosmetic role as Bassline.
            appliesTo = TrackRole.Bassline;
        }
    }
}
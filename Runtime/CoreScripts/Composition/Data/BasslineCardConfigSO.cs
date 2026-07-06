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
    }
}
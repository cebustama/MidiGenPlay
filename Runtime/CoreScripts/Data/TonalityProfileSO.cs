using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Describes stylistic / harmonic weight preferences for a given tonality,
    /// so procedural generators can sound idiomatic without hardcoding modes.
    /// Works for modes, pentatonics, synthetic scales, etc.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TonalityProfile",
        menuName = "MidiGenPlay/Tonality Profile")]
    public class TonalityProfileSO : ScriptableObject
    {
        [Header("Identity")]
        public string profileId;

        [Tooltip("Human-friendly name for debug/UI.")]
        public string displayName;

        [Header("Pitch/Scale semantics")]
        [Tooltip("Which Tonality enum this profile expects (Ionian, Dorian...)")]
        public Tonality tonality;

        [Tooltip("Scale degrees (0..6, 0 = I, 1 = II, ..., 6 = VII) that define the color.\n" +
             "E.g. Lydian: {3} for #4; Mixolydian: {6} for b7; Dorian: {5} for natural 6.")]
        public List<int> characteristicDegrees = new();

        [Header("Chord weighting")]
        [Tooltip("Base weight for each diatonic degree's chord when choosing tonic-per-bar.\n" +
             "If empty or shorter than 7, the generator will fill/extend with 1.0f.")]
        public List<float> baseDegreeWeights = new(); // size up to 7

        [Tooltip("Extra weight added to tonic degree (index 0).")]
        public float tonicBonus = 3f;

        [Tooltip("Extra weight added to a supporting anchor degree " +
             "(often dominant-like, e.g. V in Ionian/Mixo, IV in Dorian, etc.).")]
        public int supportDegree = 4; // e.g. 4 -> scale degree index 4 == 'V'
        public float supportBonus = 1.5f;

        [Tooltip("Extra weight added to each characteristic degree.")]
        public float characteristicBonus = 2f;

        [Header("Form / placement rules")]
        [Tooltip("Additional bias added to tonic on the very first bar.")]
        public float firstBarTonicBonus = 2f;

        [Tooltip("Force last bar to tonic?")]
        public bool forceCadenceToTonic = true;

        [Header("Modal loops / vamps")]
        [Tooltip("Optional short repeating degree sequences that strongly advertise this tonality.\n" +
             "Degrees are indices 0..6 in local scale, e.g. {0,3} could mean i-7 to IV7 in Dorian.\n" +
             "Tiled across multiple bars when chosen.")]
        public List<VampDefinition> vampCandidates = new();

        [System.Serializable]
        public struct VampDefinition
        {
            [Tooltip("Sequence of degrees (0..6).")]
            public List<int> degrees;

            [Tooltip("How likely we are to pick this vamp vs just free-choice chords.")]
            public float weight;

            [Tooltip("Min bars to repeat this vamp once chosen.")]
            public int minBars;

            [Tooltip("Max bars to repeat this vamp once chosen.")]
            public int maxBars;
        }
    }
}
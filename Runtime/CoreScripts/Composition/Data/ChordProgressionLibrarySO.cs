using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Chord Progression Library", 
        fileName = "ChordProgressionLibrary")]
    public class ChordProgressionLibrarySO : ScriptableObject
    {
        /// <summary>
        /// High-level hint about in which song sections this progression is most idiomatic.
        /// This is *not* enforced; we use it as a soft weight when picking templates.
        /// </summary>
        public enum UsageHint
        {
            Any,
            Verse,
            Chorus,
            Bridge,
            IntroOutro,
            Vamp
        }

        [System.Serializable]
        public class Entry
        {
            [Tooltip("Unique id for debugging / references. Example: 'Pop_I_V_vi_IV'.")]
            public string id;

            [Tooltip("Chord progression template asset, defined in degrees (I, IV, V, etc.).")]
            public ChordProgressionData progression;

            [Tooltip(
                "Optional override for compatible tonalities. " +
                "If empty, we fall back to progression.tonalities. " +
                "If *both* are empty, we consider the progression usable in any tonality.")]
            public List<Tonality> compatibleTonalities;

            [Tooltip("Soft hint for where this progression works best (verse, chorus, etc.).")]
            public UsageHint usageHint = UsageHint.Any;

            [Tooltip(
                "Relative weight when randomly picking among library entries. " +
                "Acts as a base multiplier before tonality / section adjustments.")]
            public float weight = 1f;

            [Tooltip("Free-form notes for designers. E.g. 'Axis of Awesome I–V–vi–IV pop loop'.")]
            [TextArea]
            public string notes;
        }

        [Tooltip("All progression templates available in this library.")]
        public List<Entry> entries;
    }
}
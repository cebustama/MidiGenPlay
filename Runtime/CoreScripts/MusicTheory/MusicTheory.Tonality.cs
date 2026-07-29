using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.MusicTheory
{
    public static partial class MusicTheory
    {
        public enum Tonality
        {
            // Modal Scales
            Ionian,
            Dorian,
            Phrygian,
            Lydian,
            Mixolydian,
            Aeolian,
            Locrian,
        }

        private static readonly Dictionary<Tonality, Interval[]> TonalityIntervals = new()
        {
            { Tonality.Ionian, new[]
            { Interval.Two, Interval.Two, Interval.One,
                Interval.Two, Interval.Two, Interval.Two, Interval.One } },

            { Tonality.Dorian, new[]
            { Interval.Two, Interval.One, Interval.Two, Interval.Two,
                Interval.Two, Interval.One, Interval.Two } },

            { Tonality.Phrygian, new[]
            { Interval.One, Interval.Two, Interval.Two, Interval.Two,
                Interval.One, Interval.Two, Interval.Two } },

            { Tonality.Lydian, new[]
            { Interval.Two, Interval.Two, Interval.Two, Interval.One,
                Interval.Two, Interval.Two, Interval.One } },

            { Tonality.Mixolydian, new[]
            { Interval.Two, Interval.Two, Interval.One, Interval.Two,
                Interval.Two, Interval.One, Interval.Two } },

            { Tonality.Aeolian, new[]
            { Interval.Two, Interval.One, Interval.Two, Interval.Two,
                Interval.One, Interval.Two, Interval.Two } },

            { Tonality.Locrian, new[]
            { Interval.One, Interval.Two, Interval.Two, Interval.One,
                Interval.Two, Interval.Two, Interval.Two } },
        };

        // ------ Sets (kept here so they�re easy to tweak) ------

        private static readonly Tonality[] MajorishModes =
        {
            Tonality.Ionian, Tonality.Lydian, Tonality.Mixolydian
        };

        private static readonly Tonality[] MinorishModes =
        {
            Tonality.Dorian, Tonality.Phrygian, Tonality.Aeolian, Tonality.Locrian
        };

        /// <summary>
        /// HARMONY-PURE-1 helper. Semitone offset of each scale degree
        /// (index 0..6 = Tonic..LeadingTone) above the tonic for a mode,
        /// derived from TonalityIntervals so there is a single source of
        /// truth for mode shapes. Returns a fresh array (callers may not
        /// mutate shared state). Pure — no rng, no engine calls.
        /// </summary>
        public static int[] GetDegreeSemitoneOffsets(Tonality tonality)
        {
            if (!TonalityIntervals.TryGetValue(tonality, out var intervals))
                intervals = TonalityIntervals[Tonality.Ionian];

            var offsets = new int[7];
            int acc = 0;
            for (int i = 0; i < 7; i++)
            {
                offsets[i] = acc;
                acc += intervals[i].HalfSteps;
            }
            return offsets;
        }

        public static bool IsMajorish(Tonality t) => MajorishModes.Contains(t);
        public static bool IsMinorish(Tonality t) => MinorishModes.Contains(t);



        private static readonly Dictionary<Tonality, int> TonalityWeights = new()
        {
            // Modal Scales
            { Tonality.Ionian, 5 },        // Essentially the Major scale
            { Tonality.Dorian, 3 },        // Popular in jazz, folk, and medieval music
            { Tonality.Phrygian, 3 },      // Spanish/Middle Eastern influence
            { Tonality.Lydian, 3 },        // Bright and uplifting
            { Tonality.Mixolydian, 3 },    // Common in blues and rock
            { Tonality.Aeolian, 5 },       // Equivalent to Natural Minor scale
            { Tonality.Locrian, 1 },       // Rare, used in experimental music
        };

        /// <summary>
        /// Selects a random Tonality based on the weighted TonalityWeights dictionary.
        /// </summary>
        /// <returns>A randomly chosen Tonality according to weights.</returns>
        public static Tonality GetRandomTonalityByWeight()
        {
            int totalWeight = TonalityWeights.Values.Sum();
            if (totalWeight <= 0)
            {
                throw new InvalidOperationException("Total weight must be greater than zero.");
            }

            int randomValue = UnityEngine.Random.Range(0, totalWeight);
            int cumulativeWeight = 0;

            foreach (var tonality in TonalityWeights)
            {
                cumulativeWeight += tonality.Value;
                if (randomValue < cumulativeWeight)
                {
                    return tonality.Key;
                }
            }

            // Fallback (should not be reached with properly configured weights)
            return Tonality.Ionian;
        }

        /// <summary>
        /// Selects a random Tonality from a provided list of tonalities.
        /// </summary>
        /// <param name="tonalityList">List of tonalities to choose from.</param>
        /// <returns>A randomly chosen Tonality from the list.</returns>
        public static Tonality GetRandomTonalityFromList(IEnumerable<Tonality> tonalityList)
        {
            var tonalityArray = tonalityList.ToArray();
            if (tonalityArray.Length == 0)
            {
                throw new ArgumentException("The tonality list cannot be empty.");
            }

            int randomIndex = UnityEngine.Random.Range(0, tonalityArray.Length);
            return tonalityArray[randomIndex];
        }

        public static Tonality GetRandomAnyTonality()
        {
            return GetRandomTonalityByWeight();
        }

        public static Tonality GetRandomMajorishTonality()
        {
            var majors = Enum.GetValues(typeof(Tonality))
                             .Cast<Tonality>()
                             .Where(IsMajorish);
            return GetRandomTonalityFromList(majors);
        }

        public static Tonality GetRandomMinorishTonality()
        {
            var minors = Enum.GetValues(typeof(Tonality))
                             .Cast<Tonality>()
                             .Where(IsMinorish);
            return GetRandomTonalityFromList(minors);
        }

        // NOTE: DryWetMidi ScaleIntervals class
        // https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.MusicTheory.ScaleIntervals.html
    }
}
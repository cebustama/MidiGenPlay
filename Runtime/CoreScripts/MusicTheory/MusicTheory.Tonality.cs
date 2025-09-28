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

        // ------ Sets (kept here so they’re easy to tweak) ------

        private static readonly Tonality[] MajorishModes =
        {
            Tonality.Ionian, Tonality.Lydian, Tonality.Mixolydian
        };

        private static readonly Tonality[] MinorishModes =
        {
            Tonality.Dorian, Tonality.Phrygian, Tonality.Aeolian, Tonality.Locrian
        };

        public static bool IsMajorish(Tonality t) => MajorishModes.Contains(t);
        public static bool IsMinorish(Tonality t) => MinorishModes.Contains(t);



        private static readonly Dictionary<Tonality, int> TonalityWeights = new()
        {
            // Modal Scales
            { Tonality.Ionian, 1 },        // Essentially the Major scale
            { Tonality.Dorian, 0 },        // Popular in jazz, folk, and medieval music
            { Tonality.Phrygian, 0 },      // Spanish/Middle Eastern influence
            { Tonality.Lydian, 0 },        // Bright and uplifting
            { Tonality.Mixolydian, 0 },    // Common in blues and rock
            { Tonality.Aeolian, 0 },       // Equivalent to Natural Minor scale
            { Tonality.Locrian, 0 },       // Rare, used in experimental music
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

        // NOTE: DryWetMidi ScaleIntervals class
        // https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.MusicTheory.ScaleIntervals.html
    }
}


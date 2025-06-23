using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay
{
    public static partial class MusicTheory
    {
        public enum Tonality
        {
            // Common Western Tonalities
            Major,
            Minor,

            // Modal Scales
            Ionian,
            Dorian,
            Phrygian,
            Lydian,
            Mixolydian,
            Aeolian,
            Locrian,

            // Scales with Specific Characteristics
            PentatonicMajor,
            PentatonicMinor,
            Blues,
            HarmonicMinor,
            MelodicMinor,
            NeapolitanMinor,
            Gypsy,
            DoubleHarmonic,
            LydianDominant,

            // Symmetrical and Experimental Scales
            Chromatic,
            WholeTone,
            Octatonic,
            Microtonal,

            // Atonal or Undefined
            Atonal
        }

        private static readonly Dictionary<Tonality, Interval[]> TonalityIntervals = new()
        {
            // Common Western Tonalities
            { Tonality.Major, new[] 
            { 
                Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.Two, Interval.One 
            } },
            { Tonality.Minor, new[] 
            { 
                Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two } 
            },

            // Modes
            { Tonality.Dorian, new[] { Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.Two, Interval.One, Interval.Two } },
            { Tonality.Phrygian, new[] { Interval.One, Interval.Two, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two } },
            { Tonality.Lydian, new[] { Interval.Two, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.One } },
            { Tonality.Mixolydian, new[] { Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.One, Interval.Two } },
            { Tonality.Aeolian, new[] { Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two } },
            { Tonality.Locrian, new[] { Interval.One, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.Two } },

            // Pentatonic Scales
            { Tonality.PentatonicMajor, new[] { Interval.Two, Interval.Two, Interval.Three, Interval.Two, Interval.Three } },
            { Tonality.PentatonicMinor, new[] { Interval.Three, Interval.Two, Interval.Two, Interval.Three, Interval.Two } },

            // Blues Scales
            { Tonality.Blues, new[] { Interval.Three, Interval.Two, Interval.One, Interval.One, Interval.Three, Interval.Two } },

            // Harmonic and Melodic Minor
            { Tonality.HarmonicMinor, new[] { Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.One, Interval.Three, Interval.One } },
            { Tonality.MelodicMinor, new[] { Interval.Two, Interval.One, Interval.Two, Interval.Two, Interval.Two, Interval.Two, Interval.One } },

            // Exotic Scales
            { Tonality.NeapolitanMinor, new[] { Interval.One, Interval.Two, Interval.Two, Interval.Two, Interval.One, Interval.Three, Interval.One } },
            { Tonality.Gypsy, new[] { Interval.Two, Interval.One, Interval.Three, Interval.One, Interval.One, Interval.Three, Interval.One } },
            { Tonality.DoubleHarmonic, new[] { Interval.One, Interval.Three, Interval.One, Interval.Two, Interval.One, Interval.Three, Interval.One } },
            { Tonality.LydianDominant, new[] { Interval.Two, Interval.Two, Interval.Two, Interval.One, Interval.Two, Interval.One, Interval.Two } },

            // Symmetrical and Experimental Scales
            { Tonality.Chromatic, new[] { Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One, Interval.One } },
            { Tonality.WholeTone, new[] { Interval.Two, Interval.Two, Interval.Two, Interval.Two, Interval.Two, Interval.Two } },
            { Tonality.Octatonic, new[] { Interval.Two, Interval.One, Interval.Two, Interval.One, Interval.Two, Interval.One, Interval.Two, Interval.One } },

            // Microtonal and Atonal
            { Tonality.Microtonal, Array.Empty<Interval>() }, // Microtonal intervals vary widely; handled separately.
            { Tonality.Atonal, Array.Empty<Interval>() }      // No tonal center or specific interval pattern.
        };

        private static readonly Dictionary<MusicTheory.Tonality, int> TonalityWeights = new()
        {
            // Common Western Tonalities
            { Tonality.Major, 1 },        // Most common tonality
            { Tonality.Minor, 1 },        // Second most common tonality

            // Modal Scales
            { Tonality.Ionian, 0 },        // Essentially the Major scale
            { Tonality.Dorian, 0 },        // Popular in jazz, folk, and medieval music
            { Tonality.Phrygian, 0 },      // Spanish/Middle Eastern influence
            { Tonality.Lydian, 0 },        // Bright and uplifting
            { Tonality.Mixolydian, 0 },    // Common in blues and rock
            { Tonality.Aeolian, 0 },       // Equivalent to Natural Minor scale
            { Tonality.Locrian, 0 },       // Rare, used in experimental music

            // Pentatonic Scales
            { Tonality.PentatonicMajor, 0 }, // Common in folk, pop, and rock
            { Tonality.PentatonicMinor, 0 }, // Common in blues and traditional music

            // Blues Scales
            { Tonality.Blues, 0 },         // Essential for blues and jazz styles

            // Harmonic and Melodic Minor
            { Tonality.HarmonicMinor, 0 }, // Used in classical and neoclassical
            { Tonality.MelodicMinor, 0 },  // Jazz and classical contexts

            // Exotic Scales
            { Tonality.NeapolitanMinor, 0 }, // Rare, classical
            { Tonality.Gypsy, 0 },           // Romani music
            { Tonality.DoubleHarmonic, 0 },  // Middle Eastern/Eastern European
            { Tonality.LydianDominant, 0 },  // Common in jazz/fusion

            // Symmetrical and Experimental Scales
            { Tonality.Chromatic, 0 },      // Atonal and experimental
            { Tonality.WholeTone, 0 },      // Impressionistic (Debussy)
            { Tonality.Octatonic, 0 },      // 20th-century classical/jazz
            { Tonality.Microtonal, 0 },     // Experimental and rare

            // Atonal or Undefined
            { Tonality.Atonal, 0 }          // Experimental/modern classical
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
            return Tonality.Major;
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


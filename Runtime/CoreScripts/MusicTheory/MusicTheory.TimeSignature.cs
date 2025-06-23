using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay
{
    public static partial class MusicTheory
    {
        public enum TimeSignature
        {
            FourFour,    // 4/4 (Common time)
            ThreeFour,   // 3/4 (Waltz)
            TwoFour,     // 2/4 (March)
            SixEight,    // 6/8 (Compound duple)
            NineEight,   // 9/8 (Compound triple)
            TwelveEight, // 12/8 (Compound quadruple)
            FiveFour,    // 5/4 (Odd time)
            SevenEight   // 7/8 (Odd time)
        }

        public static readonly Dictionary<TimeSignature, (int BeatsPerMeasure, int BeatUnit)> TimeSignatureProperties = new()
        {
            { TimeSignature.FourFour, (4, 4) },
            { TimeSignature.ThreeFour, (3, 4) },
            { TimeSignature.TwoFour, (2, 4) },
            { TimeSignature.SixEight, (6, 8) },
            { TimeSignature.NineEight, (9, 8) },
            { TimeSignature.TwelveEight, (12, 8) },
            { TimeSignature.FiveFour, (5, 4) },
            { TimeSignature.SevenEight, (7, 8) }
        };

        private static readonly Dictionary<TimeSignature, int> TimeSignatureWeights = new()
        {
            { TimeSignature.FourFour, 1 },    // Most common time signature
            { TimeSignature.ThreeFour, 0 },   // Common in waltzes and classical music
            { TimeSignature.TwoFour, 0 },     // Used in marches
            { TimeSignature.SixEight, 0 },    // Common in compound duple rhythms
            { TimeSignature.NineEight, 0 },    // Less common, compound triple
            { TimeSignature.TwelveEight, 0 },  // Used in compound quadruple forms
            { TimeSignature.FiveFour, 0 },     // Odd time, used in progressive music
            { TimeSignature.SevenEight, 0 }    // Rare, used in experimental or progressive music
        };

        public static TimeSignature GetRandomTimeSignatureByWeight()
        {
            int totalWeight = TimeSignatureWeights.Values.Sum();
            if (totalWeight <= 0)
            {
                throw new InvalidOperationException("Total weight must be greater than zero.");
            }

            int randomValue = UnityEngine.Random.Range(0, totalWeight);
            int cumulativeWeight = 0;

            foreach (var timeSignature in TimeSignatureWeights)
            {
                cumulativeWeight += timeSignature.Value;
                if (randomValue < cumulativeWeight)
                {
                    return timeSignature.Key;
                }
            }

            // Fallback (should not be reached with properly configured weights)
            return TimeSignature.FourFour;
        }

        public static TimeSignature GetRandomTimeSignatureFromList(IEnumerable<TimeSignature> timeSignatureList)
        {
            var timeSignatureArray = timeSignatureList.ToArray();
            if (timeSignatureArray.Length == 0)
            {
                throw new ArgumentException("The time signature list cannot be empty.");
            }

            int randomIndex = UnityEngine.Random.Range(0, timeSignatureArray.Length);
            return timeSignatureArray[randomIndex];
        }

        public static (int BeatsPerMeasure, double BeatUnitDuration) GetTimeSignatureDetails(TimeSignature timeSignature, int bpm)
        {
            if (!TimeSignatureProperties.TryGetValue(timeSignature, out var properties))
                throw new ArgumentException($"Invalid TimeSignature: {timeSignature}");

            int beatsPerMeasure = properties.BeatsPerMeasure;
            int beatUnit = properties.BeatUnit;

            // Calculate the duration of the beat unit in seconds
            double beatUnitDuration = 60.0 / bpm * (4.0 / beatUnit);

            return (beatsPerMeasure, beatUnitDuration);
        }
    }
}

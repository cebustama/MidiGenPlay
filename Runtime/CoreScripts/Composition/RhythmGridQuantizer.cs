using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Utility that turns durations (in measures) into a tempo-agnostic
    /// step grid:
    ///
    ///   durationMeasures * beatsPerMeasure * subdivisions → integer steps
    ///
    /// It searches for a subdivisions value so that:
    ///   - every duration maps cleanly to an integer step count, and
    ///   - the total number of steps is a whole number of measures.
    ///
    /// This is the extracted version of the logic previously in
    /// ChordProgressionEditorWindow.ComputeStepsAndSubdivisions.
    /// </summary>
    public sealed class RhythmGridQuantizer
    {
        /// <summary>
        /// Convenience overload for chord progressions:
        /// reads durationMeasures from ParsedChord entries.
        /// </summary>
        public bool TryQuantizeChordDurations(
            IReadOnlyList<ParsedChord> chords,
            int beatsPerMeasure,
            out int subdivisions,
            out List<int> lengthsSteps,
            out int totalSteps,
            out string error)
        {
            if (chords == null || chords.Count == 0)
            {
                subdivisions = 1;
                lengthsSteps = new List<int>();
                totalSteps = 0;
                error = "No chords to compute durations for.";
                return false;
            }

            // Extract durations in measures
            var durations = new List<float>(chords.Count);
            for (int i = 0; i < chords.Count; i++)
                durations.Add(chords[i].durationMeasures);

            // Use the generic duration quantizer
            return TryQuantizeDurations(
                durations,
                beatsPerMeasure,
                1,    // min subdivisions
                8,    // max subdivisions (ChordProgressionData clamps 1..8)
                out subdivisions,
                out lengthsSteps,
                out totalSteps,
                out error);
        }

        /// <summary>
        /// Generic duration quantizer: takes any list of durations in MEASURES
        /// and computes:
        ///
        ///   - subdivisions in [minSubdivisions, maxSubdivisions]
        ///   - step lengths per duration
        ///   - totalSteps
        ///
        /// such that:
        ///   lengthSteps[i] = durations[i] * beatsPerMeasure * subdivisions
        /// is an integer for all i, and totalSteps is a whole number of bars.
        /// </summary>
        public bool TryQuantizeDurations(
            IReadOnlyList<float> durationsMeasures,
            int beatsPerMeasure,
            int minSubdivisions,
            int maxSubdivisions,
            out int subdivisions,
            out List<int> lengthsSteps,
            out int totalSteps,
            out string error)
        {
            subdivisions = 1;
            lengthsSteps = new List<int>();
            totalSteps = 0;
            error = null;

            if (durationsMeasures == null || durationsMeasures.Count == 0)
            {
                error = "No durations to quantize.";
                return false;
            }

            if (beatsPerMeasure <= 0)
            {
                error = "Beats per measure must be > 0.";
                return false;
            }

            if (minSubdivisions <= 0) minSubdivisions = 1;
            if (maxSubdivisions < minSubdivisions) maxSubdivisions = minSubdivisions;

            // Try each candidate subdivisions value
            for (int sub = minSubdivisions; sub <= maxSubdivisions; sub++)
            {
                bool ok = true;
                lengthsSteps.Clear();
                totalSteps = 0;

                foreach (var dur in durationsMeasures)
                {
                    // duration (measures) → fractional steps
                    float stepsF = dur * beatsPerMeasure * sub;
                    int stepsInt = Mathf.RoundToInt(stepsF);

                    // Require "close enough" to an integer and positive
                    if (Mathf.Abs(stepsF - stepsInt) > 0.001f || stepsInt <= 0)
                    {
                        ok = false;
                        break;
                    }

                    lengthsSteps.Add(stepsInt);
                    totalSteps += stepsInt;
                }

                if (!ok)
                    continue;

                // Check totalSteps corresponds to a whole number of measures
                int stepsPerMeasure = beatsPerMeasure * sub;
                if (stepsPerMeasure <= 0)
                    continue;

                if (totalSteps % stepsPerMeasure != 0)
                    continue;

                // Success
                subdivisions = sub;
                return true;
            }

            error =
                "Could not find a valid 'subdivisions' value for these durations.\n" +
                "Make sure each duration is a rational multiple of a beat, and that " +
                "the sum of all durations equals an integer number of measures.";
            return false;
        }
    }
}
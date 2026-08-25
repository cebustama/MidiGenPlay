using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    public abstract class PhraseArchetypeSO : ScriptableObject
    {
        [Range(-1, 1)] public int forcedContourDir = 0;

        [Header("Phrase-end rest (MGP-TONALITY-1 D-TON7)")]
        [Range(0f, 0.5f)]
        [Tooltip("Fraction of the chord span left SILENT after the phrase's " +
                 "final note, so consecutive phrases don't run together. " +
                 "0 = legacy behavior (the phrase fills its whole span, " +
                 "which is why inter-phrase silence was impossible). The " +
                 "trim is clamped so the final note always keeps at least " +
                 "25% of its planned length (and 1/8 beat).")]
        public float endRestFraction = 0f;

        [Header("Meter fit (MGP-TONALITY-1 D-TON8)")]
        [Tooltip("Restrict slot counts and burst/pickup durations to a " +
                 "metric grid, so onsets land on real subdivisions of the " +
                 "span instead of arbitrary fractions (e.g. 9 slots over 8 " +
                 "beats -> 0.89-beat onsets). Off = legacy behavior.")]
        public bool meterFitSlots = false;

        [Tooltip("With meterFitSlots on, ALSO admit triplet-family " +
                 "durations (span/3, span/6, ...). Off = powers of two " +
                 "only, which is the default and the safe choice for " +
                 "straight meters.")]
        public bool allowTupletSubdivisions = false;

        /// <summary>Shortest slot any snap will produce (1/16 note in beats).</summary>
        protected const double MinSlotBeats = 0.0625;

        /// <summary>
        /// D-TON8: is <paramref name="d"/> (in beats) a power-of-two
        /// duration (16 .. 1/16), or — when tuplets are allowed — a
        /// third of one? Tolerance-based; never exact float equality.
        /// </summary>
        protected static bool IsMetricDuration(
            double d, bool allowTuplets, double minSlotBeats = MinSlotBeats)
        {
            if (d <= 0.0 || d < minSlotBeats - 1e-9) return false;
            if (IsPowerOfTwoBeats(d)) return true;
            if (allowTuplets && IsPowerOfTwoBeats(d * 3.0)) return true;
            return false;
        }

        private static bool IsPowerOfTwoBeats(double d)
        {
            for (int n = -4; n <= 4; n++)
            {
                double p = System.Math.Pow(2.0, n);
                if (System.Math.Abs(d - p) <= 1e-6 * System.Math.Max(1.0, p))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// D-TON8: nearest slot count to <paramref name="desired"/> whose
        /// resulting slot duration (span/k) sits on the metric grid.
        /// Pure and RNG-free — callers draw FIRST and snap the result, so
        /// enabling meterFitSlots never shifts a draw stream. Ties resolve
        /// to the LARGER count (keeps density closer to the authored
        /// min/max intent); arbitrary but deterministic. Returns the
        /// clamped input if no candidate qualifies (never 0).
        /// </summary>
        protected static int SnapSlotCountToMeter(
            int desired, double spanBeats, bool allowTuplets,
            double minSlotBeats = MinSlotBeats)
        {
            int best = -1;
            int bestDist = int.MaxValue;
            for (int k = 1; k <= 32; k++)
            {
                if (!IsMetricDuration(spanBeats / k, allowTuplets, minSlotBeats))
                    continue;
                int dist = System.Math.Abs(k - desired);
                // ascending k => on a tie, k is always the larger candidate
                if (dist <= bestDist) { best = k; bestDist = dist; }
            }
            return best > 0 ? best : Mathf.Clamp(desired, 1, 32);
        }

        /// <summary>
        /// D-TON8: largest metric duration not exceeding
        /// <paramref name="dur"/>. Snapping DOWN keeps a burst inside the
        /// span it was planned to fit. Returns the input unchanged when no
        /// candidate exists (degenerate spans).
        /// </summary>
        protected static double SnapDurationToMeter(
            double dur, bool allowTuplets, double minSlotBeats = MinSlotBeats)
        {
            double best = -1.0;
            for (int n = -4; n <= 4; n++)
            {
                double p = System.Math.Pow(2.0, n);
                if (p <= dur + 1e-9 && p >= minSlotBeats - 1e-9 && p > best) best = p;
                if (allowTuplets)
                {
                    double t = p / 3.0;
                    if (t <= dur + 1e-9 && t >= minSlotBeats - 1e-9 && t > best) best = t;
                }
            }
            return best > 0.0 ? best : dur;
        }

        public abstract List<PhrasePlanner.PhraseSlot> Build(
            double startBeat, 
            double spanBeats, 
            int beatsPerBar,
            int phraseId, 
            int contourDir, 
            System.Random rng,
            TonalityProfileSO profile, 
            MelodicLeadingConfig cfg);
    }
}
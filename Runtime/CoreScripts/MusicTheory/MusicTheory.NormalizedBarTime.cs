using System;
using Melanchall.DryWetMidi.Interaction;

namespace MidiGenPlay.MusicTheory
{
    public static partial class MusicTheory
    {
        public enum QuantizeMode
        {
            Floor,
            Round,
            Ceil
        }

        /// <summary>
        /// Returns the musical time-span representing a single beat unit of the given time signature.
        /// Example: 4/4 → Quarter, 6/8 → Eighth.
        /// </summary>
        public static MusicalTimeSpan GetBeatSpan(TimeSignature ts)
        {
            if (!TimeSignatureProperties.TryGetValue(ts, out var props))
                return MusicalTimeSpan.Quarter;

            return props.BeatUnit switch
            {
                2 => MusicalTimeSpan.Half,
                4 => MusicalTimeSpan.Quarter,
                8 => MusicalTimeSpan.Eighth,
                16 => MusicalTimeSpan.Sixteenth,
                _ => MusicalTimeSpan.Quarter
            };
        }

        /// <summary>
        /// Steps per measure for a given TS and harmonic subdivisions (steps per beat-unit).
        /// </summary>
        public static int StepsPerMeasure(TimeSignature ts, int subdivisions)
        {
            if (!TimeSignatureProperties.TryGetValue(ts, out var props))
                return Math.Max(1, subdivisions) * 4;

            int beatsPerMeasure = props.BeatsPerMeasure;
            return Math.Max(1, beatsPerMeasure) * Math.Max(1, subdivisions);
        }

        /// <summary>
        /// Converts an absolute step index into bar-normalized time (fractional bars).
        /// step=0 => 0 bars, step=StepsPerMeasure => 1.0 bars, etc.
        /// </summary>
        public static double StepsToBars(int steps, TimeSignature ts, int subdivisions)
        {
            int spm = StepsPerMeasure(ts, subdivisions);
            if (spm <= 0) return 0.0;
            return steps / (double)spm;
        }

        /// <summary>
        /// Converts bar-normalized time (fractional bars) into an absolute step index in the target TS/subdivisions.
        /// </summary>
        public static int BarsToSteps(double bars, TimeSignature ts, int subdivisions, QuantizeMode mode = QuantizeMode.Round)
        {
            int spm = StepsPerMeasure(ts, subdivisions);
            double raw = bars * spm;

            return mode switch
            {
                QuantizeMode.Floor => (int)Math.Floor(raw),
                QuantizeMode.Ceil => (int)Math.Ceiling(raw),
                _ => (int)Math.Round(raw, MidpointRounding.AwayFromZero)
            };
        }

        /// <summary>
        /// Convenience for durations: converts a bar-length into step-length (clamped to >= 1).
        /// </summary>
        public static int BarsToLengthSteps(double barLength, TimeSignature ts, int subdivisions, QuantizeMode mode = QuantizeMode.Round)
        {
            int len = BarsToSteps(barLength, ts, subdivisions, mode);
            return Math.Max(1, len);
        }
    }
}

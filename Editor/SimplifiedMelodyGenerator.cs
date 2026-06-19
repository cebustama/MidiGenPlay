#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;
using MelodyNoteEvent = MidiGenPlay.MelodyPatternData.MelodyNoteEvent;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Phase-3 simplified, EDITOR-ONLY melody generator (Roadmap_Melody_Authoring_MVP,
    /// accepted decision #9). Maps Tier-1 <see cref="MelodyGenerationParamsSO"/> params
    /// into a deterministic <see cref="MelodyPatternData"/> note list. It does NOT invoke
    /// the procedural MelodyTrackComposer + PhrasePlanner pipeline (that is Phase D3) and
    /// has NO runtime dependency (guarded by UNITY_EDITOR, lives in the editor assembly).
    ///
    /// Determinism (core package invariant): pitch + octave are drawn from a single
    /// System.Random(seed) - the package convention (cf. RhythmTrackComposer) and the
    /// deliberate inverse of the UnityEngine.Random per-note draw removed in M-3. Onset
    /// placement (the rhythmic skeleton) is a PURE function of style + density + meter and
    /// does NOT consume the RNG, so the same params reproduce the same groove while a new
    /// seed re-rolls only the melody's pitches over that groove.
    ///
    /// Scope: intentionally simple - a starting point for authoring, not a replacement for
    /// the procedural pipeline. tonalityHint is carried but does NOT gate the degree set in
    /// the MVP (D-MEL3.3); all seven diatonic degrees stay available, stability-weighted.
    /// instrumentHint is not consulted (informational-only; the pattern stores no instrument).
    /// </summary>
    public static class SimplifiedMelodyGenerator
    {
        // Stability-biased diatonic weights (degrees only - pitch is resolved at runtime).
        // Tonic / Dominant / Mediant favoured so output reads as a melody, not a random walk.
        private static readonly (ScaleDegree degree, int weight)[] DegreeWeights =
        {
            (ScaleDegree.Tonic,       5),
            (ScaleDegree.Dominant,    4),
            (ScaleDegree.Mediant,     3),
            (ScaleDegree.Subdominant, 2),
            (ScaleDegree.Submediant,  2),
            (ScaleDegree.Supertonic,  2),
            (ScaleDegree.LeadingTone, 1),
        };

        private static readonly int DegreeWeightSum = SumWeights();

        private static int SumWeights()
        {
            int s = 0;
            foreach (var d in DegreeWeights) s += d.weight;
            return s;
        }

        /// <summary>
        /// Overwrite <paramref name="target"/>.notes with a freshly generated sequence for the
        /// target's current meter (beatsPerMeasure / Measures / subdivisions). The CALLER owns
        /// working-copy isolation: pass the wizard's working clone, never the bound asset.
        /// </summary>
        public static void Generate(MelodyPatternData target, MelodyGenerationParamsSO p, int seed)
        {
            if (target == null || p == null) return;
            p.Normalize();

            var rng = new System.Random(seed);

            target.InitializeIfEmpty();
            target.notes.Clear();

            int bpm = Mathf.Max(1, target.beatsPerMeasure);
            int subs = Mathf.Max(1, target.subdivisions);
            int measures = Mathf.Max(1, target.Measures);
            int stepsPerMeasure = bpm * subs;

            int octLo = Mathf.Clamp(Mathf.Min(p.octaveRangeMin, p.octaveRangeMax), -4, 4);
            int octHi = Mathf.Clamp(Mathf.Max(p.octaveRangeMin, p.octaveRangeMax), -4, 4);

            for (int m = 0; m < measures; m++)
            {
                foreach (int localStep in PlaceOnsets(p.rhythmicStyle, p.density, stepsPerMeasure, subs, bpm))
                {
                    int globalStep = m * stepsPerMeasure + localStep;
                    float startBeat = globalStep / (float)subs;
                    float durBeats = DurationBeatsFor(p.rhythmicStyle, subs);
                    ScaleDegree degree = PickDegree(rng);
                    int octave = (octLo == octHi) ? octLo : octLo + rng.Next(octHi - octLo + 1);
                    int velocity = VelocityFor(localStep, subs);

                    target.notes.Add(MelodyNoteEvent.Create(degree, startBeat, durBeats, octave, velocity));
                }
            }

            // Deterministic stored order (start, degree, octave) - matches SnapshotOrdered / the editor.
            target.notes.Sort((a, b) =>
            {
                int c = a.startBeat.CompareTo(b.startBeat);
                if (c != 0) return c;
                c = ((int)a.degree).CompareTo((int)b.degree);
                if (c != 0) return c;
                return a.octaveOffset.CompareTo(b.octaveOffset);
            });
        }

        /// <summary>
        /// Local step indices (0..stepsPerMeasure-1) that should carry an onset this measure.
        /// Pure function of style + density + meter; does not consume the RNG.
        /// </summary>
        private static List<int> PlaceOnsets(
            MelodyRhythmicStyle style, float density, int stepsPerMeasure, int subs, int bpm)
        {
            var steps = new List<int>();
            density = Mathf.Clamp01(density);

            switch (style)
            {
                case MelodyRhythmicStyle.Even:
                    {
                        int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, bpm, density)), 1, bpm);
                        for (int i = 0; i < n; i++)
                        {
                            int s = Mathf.Clamp(Mathf.RoundToInt(i * (float)stepsPerMeasure / n),
                                                0, stepsPerMeasure - 1);
                            if (!steps.Contains(s)) steps.Add(s);
                        }
                        break;
                    }
                case MelodyRhythmicStyle.Syncopated:
                    {
                        int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, bpm, density)), 1, bpm);
                        // Push onto the "and" of the beat; at a quarter grid (subs == 1) push by one beat.
                        int offset = subs >= 2 ? subs / 2 : 1;
                        for (int i = 0; i < n; i++)
                        {
                            int even = Mathf.RoundToInt(i * (float)stepsPerMeasure / n);
                            int s = Mathf.Clamp(even + offset, 0, stepsPerMeasure - 1);
                            if (!steps.Contains(s)) steps.Add(s);
                        }
                        break;
                    }
                case MelodyRhythmicStyle.Burst:
                    {
                        int clusters = Mathf.Clamp(
                            Mathf.RoundToInt(Mathf.Lerp(1, Mathf.Max(1, bpm / 2), density)), 1, bpm);
                        int runLen = Mathf.Clamp(
                            Mathf.RoundToInt(Mathf.Lerp(2, subs * 2, density)), 1, stepsPerMeasure);
                        for (int c = 0; c < clusters; c++)
                        {
                            int start = Mathf.Clamp(Mathf.RoundToInt(c * (float)stepsPerMeasure / clusters),
                                                    0, stepsPerMeasure - 1);
                            for (int k = 0; k < runLen; k++)
                            {
                                int s = start + k;
                                if (s >= stepsPerMeasure) break;
                                if (!steps.Contains(s)) steps.Add(s);
                            }
                        }
                        break;
                    }
            }

            steps.Sort();
            return steps;
        }

        private static float DurationBeatsFor(MelodyRhythmicStyle style, int subs) => style switch
        {
            MelodyRhythmicStyle.Even => 1f,            // ~ a beat
            MelodyRhythmicStyle.Syncopated => 0.5f,    // half-beat push
            MelodyRhythmicStyle.Burst => 1f / subs,    // one subdivision (short)
            _ => 1f
        };

        // Deterministic velocity shape (no RNG): bar downbeat > beat > off-beat.
        private static int VelocityFor(int localStep, int subs)
        {
            if (localStep == 0) return 110;
            if (localStep % subs == 0) return 100;
            return 85;
        }

        private static ScaleDegree PickDegree(System.Random rng)
        {
            int roll = rng.Next(DegreeWeightSum);
            int acc = 0;
            foreach (var (degree, weight) in DegreeWeights)
            {
                acc += weight;
                if (roll < acc) return degree;
            }
            return ScaleDegree.Tonic;
        }
    }
}
#endif
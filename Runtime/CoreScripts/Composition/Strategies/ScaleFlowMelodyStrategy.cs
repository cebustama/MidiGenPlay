using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// Modal/scalar strategy:
    /// - Prefers stepwise moves (±1 / ±2 scale steps).
    /// - Weights chord tones higher, but allows scale tones.
    /// - Uses weighted randomness so it doesn't get stuck on the single nearest.
    public class ScaleFlowMelodyStrategy : IMelodyStrategy
    {
        public Note PickNext(
            NoteName[] chordPitchClasses,
            NoteName[] scalePitchClasses,
            Note last,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            System.Random rng,
            PhraseState phrase)
        {
            // Build a full candidate pool from the *scale* across the instrument's range
            var cand = (from oct in Enumerable.Range(inst.octaveMin, inst.octaveMax - inst.octaveMin + 1)
                        from pc in scalePitchClasses
                        select Note.Get(pc, oct)).ToList();

            if (cand.Count == 0) return null;

            // First note: choose a chord tone near middle if possible, else scale
            if (last == null)
            {
                var center = 
                    Note.Get(cand[0].NoteName, (inst.octaveMin + inst.octaveMax) / 2);
                var chordSet = chordPitchClasses.ToHashSet();
                var chordCand = cand.Where(n => chordSet.Contains(n.NoteName));
                var seq = 
                    (chordCand.Any() ? 
                    chordCand 
                    : cand).OrderBy(n => Math.Abs(Semis(n) - Semis(center)));
                return seq.First();
            }

            // Build weights
            var weights = new List<(Note n, double w)>(cand.Count);
            var chordHash = chordPitchClasses.ToHashSet();
            foreach (var n in cand)
            {
                int distSemis = Math.Abs(Semis(n) - Semis(last));
                if (distSemis == 0)
                {
                    // Only allow repeat by chance; otherwise weight = 0 to avoid it
                    var w = (rng.NextDouble() <= cfg.chanceRepeatNote) ? 1.0 : 0.0;
                    weights.Add((n, w));
                    continue;
                }

                // Base weight: prefer stepwise movement (±1 or ±2 scale steps approx)
                // We'll approximate stepwise by small semitone distance and scale membership (already ensured).
                double wBase = distSemis <= 2 ? 3.0 :
                               distSemis <= 4 ? 1.5 : 0.25;

                // Chord tones get a bump
                if (chordHash.Contains(n.NoteName))
                    wBase *= 2.0;

                // Respect max step by crushing weight if it exceeds the limit
                if (distSemis > cfg.maxStepSemitones)
                    wBase *= 0.01;

                weights.Add((n, wBase));
            }

            // Normalize + pick weighted random
            double total = weights.Sum(t => t.w);
            if (total <= 0.0001)  // all zero? fall back to nearest
            {
                return cand.OrderBy(n => Math.Abs(Semis(n) - Semis(last))).First();
            }

            double r = rng.NextDouble() * total;
            foreach (var (n, w) in weights)
            {
                if ((r -= w) <= 0) return n;
            }
            return weights.Last().n;
        }

        static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}
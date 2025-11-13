using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Modal / scalar “flow” strategy.
    /// - Builds candidates from chord/scale + NoteSource and optional allowed degrees.
    /// - First note: same ordered start as NearestChordTone.
    /// - Subsequent notes: computes a motion weight for each candidate based on:
    ///   interval size (prefers small steps), chord-tone membership, and
    ///   characteristic degrees (modal colour), plus optional cadential nudges.
    /// - Uses weighted randomness to pick from the pool, producing fluid,
    ///   non-deterministic scalar lines that still feel harmonically grounded.
    /// </summary>
    public class ScaleFlowMelodyStrategy : IMelodyStrategy
    {
        public Note PickNext(
            NoteName[] chordPitchClasses,
            NoteName[] scalePitchClasses,
            Dictionary<NoteName, int> degreeLookup,
            Note last,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            System.Random rng,
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile,
            MelodyPartState part,
            HashSet<int> allowedDegrees)
        {
            var candidates = MelodyStrategyCommon.BuildCandidatesWithFilter(
                chordPitchClasses, scalePitchClasses,
                degreeLookup, inst, cfg, allowedDegrees);

            if (candidates.Count == 0)
                return null;

            var chordSet = chordPitchClasses.ToHashSet();

            // FIRST NOTE?
            if (last == null)
            {
                var orderedFirst = MelodyStrategyCommon.OrderFirstNoteCandidates(
                    candidates,
                    inst,
                    cfg,
                    profile,
                    degreeLookup,
                    chordSet
                );

                return orderedFirst.FirstOrDefault();
            }

            // SUBSEQUENT NOTES:
            // Compute weights per candidate, then choose weighted random
            var weights = new List<double>(candidates.Count);

            foreach (var n in candidates)
            {
                double w = MelodyStrategyCommon.ComputeMotionWeight(
                    n,
                    last,
                    cfg,
                    phrase,
                    profile,
                    degreeLookup,
                    chordSet,
                    rng
                );

                weights.Add(w);
            }

            var picked = MelodyStrategyCommon.PickWeightedRandom(
                candidates,
                weights,
                last,
                rng
            );

            return picked;
        }

        static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}
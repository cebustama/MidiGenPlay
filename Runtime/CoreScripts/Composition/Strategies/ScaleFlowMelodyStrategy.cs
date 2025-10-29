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
        /// <summary>
        /// Modal / scalar "flow" strategy:
        /// - Prefers small melodic steps,
        /// - Gives chord tones extra weight (when allowed),
        /// - Favors tonality profile's characteristic degrees,
        /// - Nudges tonic on strong beats if the profile says to cadence home,
        /// - Picks stochastically from a weighted pool instead of always nearest.
        /// </summary>
        public Note PickNext(
            NoteName[] chordPitchClasses,
            NoteName[] scalePitchClasses,
            Dictionary<NoteName, int> degreeLookup,
            Note last,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            System.Random rng,
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile)
        {
            // 1. candidate pitch classes (per NoteSource)
            var poolPCs = MelodyStrategyCommon.BuildPitchClassPool(
                cfg, chordPitchClasses, scalePitchClasses);

            // 2. expand to actual playable notes
            var candidates = MelodyStrategyCommon.ExpandToInstrumentRange(
                poolPCs, inst);

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
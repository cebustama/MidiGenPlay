using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// “Climbing” contour strategy.
    /// - Builds candidates from chord/scale + NoteSource and optional allowed degrees.
    /// - First note: prefers a low tonic (if available), otherwise a strong ordered start.
    /// - Normal slots: prefers notes above the last one, within <c>maxStepSemitones</c>,
    ///   with small upward steps weighted more, plus bonuses for chord tones and
    ///   characteristic degrees. Allows occasional small backsteps if no upward step fits.
    /// - Final slot of the part: cadences deliberately to a tonic above the reference,
    ///   using <see cref="MelodyStrategyCommon.ComputeTargetTonicAbove"/>.
    /// - Uses weighted randomness inside the upward pool for a natural, rising line.
    /// </summary>
    public class AscendingClimbMelodyStrategy : IMelodyStrategy
    {
        public Note PickNext(
            NoteName[] chordPCs,
            NoteName[] scalePCs,
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
                chordPCs, scalePCs,
                degreeLookup, inst, cfg, allowedDegrees);

            if (candidates.Count == 0) return null;

            // First note: prefer a low tonic opening
            if (last == null)
            {
                var chordSet = chordPCs.ToHashSet();
                var ordered = MelodyStrategyCommon.OrderFirstNoteCandidates(
                    candidates, inst, cfg, profile, degreeLookup, chordSet);
                var tonic = ordered.FirstOrDefault(n => MelodyStrategyCommon.IsTonicDegree(n, degreeLookup));
                return tonic ?? ordered.FirstOrDefault();
            }

            // FINAL LANDING: if this is the final slot of the entire part,
            // cadence to root + n oct
            int octs = 2;
            if (part.IsFinalSlotOfPart)
            {
                var target = MelodyStrategyCommon.ComputeTargetTonicAbove(
                    part.TonicPC, inst, /*reference*/ last, /*octavesUp*/ octs);
                return target;
            }

            // Normal ascending behaviour: prefer notes above 'last',
            // favor characteristic tones
            // and (optionally) chord tones.
            int lastSemis = MelodyStrategyCommon.Semis(last);
            var chordSet2 = chordPCs.ToHashSet();

            var upward = candidates
                .Where(n => MelodyStrategyCommon.Semis(n) > lastSemis).ToList();
            // respect max step
            var upwardLimited = upward
                .Where(n => MelodyStrategyCommon.Semis(n) 
                            - lastSemis <= cfg.maxStepSemitones).ToList();

            var pickPool = upwardLimited;
            // No options before final
            if (pickPool.Count == 0)
            {
                // allow a small backstep (1–2) then resume ascending on next slot
                pickPool = candidates
                    .Where(n =>
                    {
                        int d = lastSemis - MelodyStrategyCommon.Semis(n);
                        return d > 0 && d <= Math.Max(2, cfg.maxStepSemitones);
                    }).ToList();
            }

            if (pickPool.Count == 0)
            {
                var anyUp = upward
                    .OrderBy(n => MelodyStrategyCommon.Semis(n) - lastSemis)
                    .FirstOrDefault();
                if (anyUp != null) return anyUp;
                if (rng.NextDouble() <= cfg.chanceRepeatNote) return last;
                return candidates
                    .OrderBy(n => 
                    Math.Abs(MelodyStrategyCommon.Semis(n) - lastSemis)).First();
            }

            var weights = new List<double>(pickPool.Count);
            foreach (var n in pickPool)
            {
                double w = 1.0;
                int step = Math.Abs(MelodyStrategyCommon.Semis(n) - lastSemis);
                w *= step <= 2 ? 3.0 : step <= 4 ? 1.5 : 0.5;

                if (cfg.noteSource != MelodicLeadingConfig.NoteSource.ScaleOnly &&
                    chordSet2.Contains(n.NoteName)) w *= 1.8;

                if (MelodyStrategyCommon
                    .IsCharacteristic(n, profile, degreeLookup)) w *= 2.0;

                weights.Add(w);
            }

            return MelodyStrategyCommon
                .PickWeightedRandom(pickPool, weights, last, rng);
        }
    }
}
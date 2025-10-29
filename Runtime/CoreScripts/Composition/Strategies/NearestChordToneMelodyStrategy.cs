using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.Composition
{
    /// Picks the nearest chord tone to the previous note, within max step; otherwise nearest.
    public sealed class NearestChordToneMelodyStrategy : IMelodyStrategy
    {
        /// <summary>
        /// Deterministic / 'glued to harmony' strategy.
        /// - Chooses primarily chord tones (if asked),
        /// - or chord+scale union,
        /// - or just scale,
        /// then tries to stay near the last note in pitch,
        /// and biases the tonality profile's characteristic degrees.
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

            // 2. concrete notes in instrument range
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
            // rank by:
            //   1) chord priority (if allowed),
            //   2) characteristic-ness,
            //   3) closeness in semitones
            var ranked = candidates
                .Select(n => new
                {
                    note = n,
                    chordPrio = MelodyStrategyCommon.ChordPriority(n, cfg, chordSet),
                    favored = MelodyStrategyCommon.IsCharacteristic(n, profile, degreeLookup),
                    distSemis = Math.Abs(
                        MelodyStrategyCommon.Semis(n) -
                        MelodyStrategyCommon.Semis(last))
                })
                .OrderBy(x => x.chordPrio)               // chord tones first (if allowed)
                .ThenBy(x => x.favored ? 0 : 1)          // then modal color
                .ThenBy(x => x.distSemis)                // then closeness to last
                .Select(x => x.note)
                .ToList();

            if (ranked.Count == 0)
                return null;

            var best = ranked[0];

            // handle same-note repeat logic:
            if (MelodyStrategyCommon.Semis(best) ==
                MelodyStrategyCommon.Semis(last))
            {
                if (rng.NextDouble() <= cfg.chanceRepeatNote)
                    return best;

                // pick closest alternative within maxStepSemitones
                foreach (var n in ranked.Skip(1))
                {
                    var step = Math.Abs(
                        MelodyStrategyCommon.Semis(n) -
                        MelodyStrategyCommon.Semis(last));
                    if (step > 0 && step <= cfg.maxStepSemitones)
                        return n;
                }

                // fallback: nearest different ignoring max step
                var alt = ranked
                    .Skip(1)
                    .FirstOrDefault(n => MelodyStrategyCommon.Semis(n) != MelodyStrategyCommon.Semis(last));
                if (alt != null)
                    return alt;

                return best;
            }

            // normal case: first candidate within max step
            foreach (var n in ranked)
            {
                var step = Math.Abs(
                    MelodyStrategyCommon.Semis(n) -
                    MelodyStrategyCommon.Semis(last));
                if (step <= cfg.maxStepSemitones)
                    return n;
            }

            // fallback if nothing within step limit
            return best;
        }

        static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}

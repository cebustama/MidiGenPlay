using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.Composition
{
    /// Picks the nearest chord tone to the previous note, within max step; otherwise nearest.
    public sealed class NearestChordToneMelodyStrategy : IMelodyStrategy
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
            // Build candidates from *source*:
            // - ChordTonesOnly: chord tones only
            // - PreferChordTonesAllowScale: both, but chord tones come first
            // - ScaleOnly: scale tones only
            IEnumerable<NoteName> pcs =
                cfg.source == MelodicLeadingConfig.NoteSource.ScaleOnly
                    ? scalePitchClasses
                    : chordPitchClasses;

            var cand = from oct in Enumerable.Range(
                inst.octaveMin, inst.octaveMax - inst.octaveMin + 1)
                       from pc in pcs
                       select Note.Get(pc, oct);

            if (cfg.source == MelodicLeadingConfig.NoteSource.PreferChordTonesAllowScale)
            {
                // Put chord-tone candidates first, then scale-only candidates.
                var chordSet = chordPitchClasses.ToHashSet();
                // Order according to whether note belongs to chord
                cand = cand.OrderBy(n => chordSet.Contains(n.NoteName) ? 0 : 1);
            }

            // First note: start near the instrument center
            if (last == null)
            {
                int center = (inst.octaveMin + inst.octaveMax) / 2;
                return cand.OrderBy(
                    n => Math.Abs(Semis(n) - Semis(Note.Get(n.NoteName, center)))).First();
            }

            // Rank by distance
            var ordered = cand.OrderBy(n => Math.Abs(Semis(n) - Semis(last)));

            // If nearest is *exactly* last note, respect chanceRepeatNote
            var nearest = ordered.First();
            if (Semis(nearest) == Semis(last))
            {
                // Only repeat if the dice says so
                if (rng.NextDouble() <= cfg.chanceRepeatNote)
                    return nearest;

                // else pick the next-closest *different* candidate within max step if possible
                foreach (var n in ordered.Skip(1))
                    if (Math.Abs(Semis(n) - Semis(last)) <= cfg.maxStepSemitones)
                        return n;

                // fallback: next-closest even if it violates max step
                return ordered.Skip(1).FirstOrDefault() ?? nearest;
            }

            // Normal case: take the nearest within max step if available
            foreach (var n in ordered)
                if (Math.Abs(Semis(n) - Semis(last)) <= cfg.maxStepSemitones)
                    return n;

            // fallback
            return ordered.First();
        }

        static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}

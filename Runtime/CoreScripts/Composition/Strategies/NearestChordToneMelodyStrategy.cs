using System;
using System.Linq;
using Melanchall.DryWetMidi.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Picks the nearest chord tone to the previous note, within max step; otherwise nearest.
    public sealed class NearestChordToneMelodyStrategy : IMelodyStrategy
    {
        public Note PickNext(
            NoteName[] chordPitchClasses,
            Note last,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            System.Random rng)
        {
            // Gather candidates across instrument range.
            var cand = from oct in Enumerable.Range(
                            inst.octaveMin, inst.octaveMax - inst.octaveMin + 1)
                       from pc in chordPitchClasses
                       select Note.Get(pc, oct);

            if (last == null)
            {
                // Start near instrument center.
                int center = (inst.octaveMin + inst.octaveMax) / 2;
                return cand.OrderBy(
                    n => Math.Abs(Semis(n) - Semis(Note.Get(n.NoteName, center)))).First();
            }

            var ordered = cand.OrderBy(n => Math.Abs(Semis(n) - Semis(last)));

            // Respect max step if possible.
            foreach (var n in ordered)
                if (Math.Abs(Semis(n) - Semis(last)) <= cfg.maxStepSemitones)
                    return n;

            return ordered.First();
        }

        static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}

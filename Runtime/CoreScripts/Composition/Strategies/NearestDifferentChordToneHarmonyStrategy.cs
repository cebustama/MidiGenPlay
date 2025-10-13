using System;
using System.Linq;
using Melanchall.DryWetMidi.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Nearest chord tone to previous harmony; avoid unison with melody; respect min/max distance.
    public sealed class NearestDifferentChordToneHarmonyStrategy : IHarmonyStrategy
    {
        public Note PickHarmony(
            NoteName[] chordPitchClasses,
            Note melody,
            Note lastHarmony,
            MIDIInstrumentSO inst,
            HarmonicLeadingConfig cfg,
            System.Random rng)
        {
            var cand = from oct in Enumerable.Range(
                            inst.octaveMin, inst.octaveMax - inst.octaveMin + 1)
                       from pc in chordPitchClasses
                       let n = Note.Get(pc, oct)
                       // avoid unison
                       where !(n.NoteName == melody.NoteName && n.Octave == melody.Octave) 
                       select n;

            if (!cand.Any()) return null;

            Func<Note, int> S = n => (int)(byte)n.NoteNumber;
            int m = S(melody);

            // Start near melody if first, else near last harmony.
            var ordered = (lastHarmony == null)
                ? cand.OrderBy(n => Math.Abs(S(n) - m))
                : cand.OrderBy(n => Math.Abs(S(n) - S(lastHarmony)));

            // Optional relation flavors (sample)
            Note pick = ordered.First();

            // Enforce min/max distance from melody.
            int d = Math.Abs(S(pick) - m);
            if (d < cfg.minDistanceFromMelody)
            {
                var pushed = 
                    cand.OrderBy(n => Math.Abs((S(n) - m) - cfg.minDistanceFromMelody))
                    .FirstOrDefault();

                if (pushed != null) pick = pushed;
            }
            if (Math.Abs(S(pick) - m) > cfg.maxDistanceFromMelody) return null;

            return pick;
        }
    }
}

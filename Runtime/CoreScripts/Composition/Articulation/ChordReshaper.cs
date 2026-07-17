using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// CA-T2 default reshaper. Stateless, RNG-free, deterministic. PowerChord and
    /// Chugging reduce the voicing to root + perfect fifth (+ octave); every other
    /// expression is identity. See runtime/SSoT_Composer_Backing_Track.md §8.
    /// </summary>
    public sealed class ChordReshaper : IChordReshaper
    {
        public IReadOnlyList<Note> Reshape(
            IReadOnlyList<Note> voiced,
            NoteName[] rootPositionPcs,
            ChordExpressionType expression)
        {
            // Only the two reshaping figures mutate pitch; identity otherwise so
            // CA-T1 paths stay bit-identical.
            if (expression != ChordExpressionType.PowerChord &&
                expression != ChordExpressionType.Chugging)
                return voiced;

            if (voiced == null || voiced.Count == 0 ||
                rootPositionPcs == null || rootPositionPcs.Length == 0)
                return voiced; // nothing to reshape (never-silent: caller emits as-is)

            // Root pitch-class semitone (0..11). DryWetMidi NoteName aligns with
            // NoteNumber % 12 (C = 0).
            int rootPc = ((int)rootPositionPcs[0]) % 12;

            // Lowest voiced note = the voicing's bass (post-inversion).
            int bass = int.MaxValue;
            for (int i = 0; i < voiced.Count; i++)
            {
                int n = (int)voiced[i].NoteNumber;
                if (n < bass) bass = n;
            }

            // Anchor the power chord at the root pitch AT OR BELOW the bass, so the
            // reduction stays in the chord's register regardless of the voicer's
            // inversion/Drop-2 choice (D-T2-PIN=A: a pin that moved the third is a
            // no-op here — the third is gone; a pin that moved root/fifth is honored
            // via the already-inverted bass).
            int delta = ((bass % 12) - rootPc + 12) % 12;
            int rootMidi = bass - delta;

            var reshaped = new List<Note>(3);
            AddIfInRange(reshaped, rootMidi);       // root
            AddIfInRange(reshaped, rootMidi + 7);   // perfect fifth
            AddIfInRange(reshaped, rootMidi + 12);  // octave (fullness)

            return reshaped.Count > 0 ? reshaped : voiced;
        }

        private static void AddIfInRange(List<Note> list, int midi)
        {
            if (midi < 0 || midi > 127) return;
            var note = Note.Get((SevenBitNumber)midi);
            for (int i = 0; i < list.Count; i++)
                if ((int)list[i].NoteNumber == midi) return; // no duplicate pitches
            list.Add(note);
        }
    }
}
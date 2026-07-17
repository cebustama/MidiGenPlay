using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// CA-T2 pre-articulation voicing reshaper (D-T2-SEAM=B). Runs AFTER the
    /// voicer and BEFORE the pitch-preserving articulator, at both chord emission
    /// sites in ChordTrackComposer. Owns the ONLY pitch mutation in the chord
    /// path for the Tier-2 reshaping figures; the voicer keeps register/
    /// inversions/Drop-2 (§7) and the articulator keeps rhythm/velocity (§8).
    ///
    /// Contract:
    /// - Deterministic and RNG-free (same category as the Tier-1 articulator).
    /// - Identity on every non-Tier-2 expression (Tier-1 / Block / Random) — the
    ///   input list is returned unchanged, so CA-T1 output stays bit-identical.
    /// - Never null; never empty when the input voicing is non-empty
    ///   (never-silent: at least the root survives).
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md §8 (Tier-2).
    /// </summary>
    public interface IChordReshaper
    {
        /// <param name="voiced">The voiced chord (voicer output, absolute pitches).</param>
        /// <param name="rootPositionPcs">Chord pitch classes in root-position order;
        /// index 0 is the harmonic root (used to place the power chord regardless
        /// of the voicer's inversion choice).</param>
        /// <param name="expression">Selected figure. Only PowerChord/Chugging
        /// reshape; all others return <paramref name="voiced"/> unchanged.</param>
        IReadOnlyList<Note> Reshape(
            IReadOnlyList<Note> voiced,
            NoteName[] rootPositionPcs,
            ChordExpressionType expression);
    }
}
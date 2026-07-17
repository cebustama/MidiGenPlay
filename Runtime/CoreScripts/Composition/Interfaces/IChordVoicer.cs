using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;

namespace MidiGenPlay.Composition
{
    public interface IChordVoicer
    {
        /// <param name="pitchClasses">Chord pitch classes in root-position order.</param>
        /// <param name="instrument">Playback instrument (octave range).</param>
        /// <param name="lastVoicing">Previous voicing for voice-leading continuity.</param>
        /// <param name="cfg">Voice-leading configuration.</param>
        /// <param name="forcedInversion">
        /// Optional per-chord inversion pin (CQ-A1-OBJ2, D0=A pin semantics).
        /// A value in 0..pitchClasses.Length-1 forces exactly that rotation
        /// (0 = root position, 1 = 1st inversion, ...); the voicer still owns
        /// register and spacing. null or an out-of-range value = unset: the
        /// candidate set and scoring are bit-identical to prior behavior.
        /// See runtime/SSoT_Composer_Backing_Track.md �7.
        /// </param>
        /// <param name="rng">
        /// VL-DET-1: optional deterministic RNG for the first chord's random
        /// start-register modes (RandomAroundCenter / Uniform01AroundCenter).
        /// When supplied, those modes draw from this seeded stream instead of
        /// the global unseeded UnityEngine.Random, making them reproducible.
        /// null preserves prior behavior bit-identically; non-random start
        /// modes never touch it, so they are unaffected either way.
        /// </param>
        IReadOnlyList<Note> VoiceChord(
            NoteName[] pitchClasses,
            MIDIInstrumentSO instrument,
            IReadOnlyList<Note> lastVoicing,
            VoiceLeadingConfig cfg,
            int? forcedInversion = null,
            System.Random rng = null);
    }
}
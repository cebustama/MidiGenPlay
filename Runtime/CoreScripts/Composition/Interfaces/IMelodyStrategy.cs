using Melanchall.DryWetMidi.MusicTheory;

namespace MidiGenPlay.Composition
{
    public enum MelodyStrategyId
    {
        NearestChordTone,
        ScaleFlow
        // extend as new implementations created
    }

    public struct PhraseState
    {
        public int PhraseIndex;      // 0,1,2...
        public int NoteIndexInPhrase;
        public Note PhraseStartNote;
        public Note PhrasePeakNote;
        public bool IsStrongBeat;    // optional rhythmic accent hint
    }

    // TODO:  Cadence / target awareness
    // “In 2 beats we’re going to hit the I chord, aim toward its 3rd…”
    // foresight into upcoming chords or the remaining duration of the current chord.

    // TODO: accents/velocity
    // return a tiny struct { Note note; int velocity; float legatoFactor; } instead of just Note.

    /// Pick the next melodic note given the current chord, last melody note, and instrument range.
    public interface IMelodyStrategy
    {
        /// Return null to emit a rest.
        Note PickNext(
            NoteName[] chordPitchClasses,       // e.g., {C, E, G}
            NoteName[] scalePitchClasses,       // modal scale for current tonality/root (7 pitch classes)
            Note lastMelody,                    // may be null for the first note
            MIDIInstrumentSO instrument,        // min/max octaves etc.
            MelodicLeadingConfig cfg,           // melodic constraints/taste
            System.Random rng,
            PhraseState phrase);                 // deterministic RNG if needed
    }
}
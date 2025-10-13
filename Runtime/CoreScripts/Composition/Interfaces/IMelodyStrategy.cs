using Melanchall.DryWetMidi.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Pick the next melodic note given the current chord, last melody note, and instrument range.
    public interface IMelodyStrategy
    {
        /// Return null to emit a rest.
        Note PickNext(
            NoteName[] chordPitchClasses,       // e.g., {C, E, G}
            Note lastMelody,                    // may be null for the first note
            MIDIInstrumentSO instrument,        // min/max octaves etc.
            MelodicLeadingConfig cfg,           // melodic constraints/taste
            System.Random rng);                 // deterministic RNG if needed
    }
}
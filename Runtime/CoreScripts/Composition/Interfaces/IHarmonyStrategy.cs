using Melanchall.DryWetMidi.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Pick the harmony note given chord tones, the concurrent melody note, and last harmony.
    public interface IHarmonyStrategy
    {
        /// Return null to skip a harmony note at this moment.
        Note PickHarmony(
            NoteName[] chordPitchClasses,
            Note melodyNote,                    // the leader at this instant
            Note lastHarmony,                   // may be null
            MIDIInstrumentSO instrument,
            HarmonicLeadingConfig cfg,
            System.Random rng);
    }
}

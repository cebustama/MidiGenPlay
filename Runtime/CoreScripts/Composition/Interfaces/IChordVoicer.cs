using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;

namespace MidiGenPlay.Composition
{
    public interface IChordVoicer
    {
        IReadOnlyList<Note> VoiceChord(
            NoteName[] pitchClasses,
            MIDIInstrumentSO instrument,
            IReadOnlyList<Note> lastVoicing,
            VoiceLeadingConfig cfg);
    }
}
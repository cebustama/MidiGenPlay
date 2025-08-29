using Melanchall.DryWetMidi.Core;

namespace MidiGenPlay.Interfaces
{
    /// <summary>
    /// Transport-level MIDI playback abstraction.
    /// Converts MidiFile to bytes, stops current playback, and starts new playback.
    /// </summary>
    public interface IMidiPlayback
    {
        void Play(MidiFile song);
        void Stop();
    }
}
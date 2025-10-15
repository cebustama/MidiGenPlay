using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;

namespace MidiGenPlay
{
    /// <summary>
    /// Post-generation MIDI edit: timing, velocity, duplication, mistakes, mix priming, etc.
    /// Return same or a new MidiFile; do not mutate input in place unless documented.
    /// </summary>
    public interface IMidiPostProcessor
    {
        string Name { get; }
        int Order { get; } // Lower runs first. Use 0 as default.

        MidiFile Process(MidiFile midi, IPostProcessContext ctx);
    }

    public interface IPostProcessContext
    {
        TempoMap TempoMap { get; }
        IReadOnlyDictionary<string, IMusicianPersonality> Personalities { get; }
        System.Random Rng { get; }
        void Log(string message);
    }
}
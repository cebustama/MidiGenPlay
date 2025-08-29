using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Low-level contract for playing raw MIDI data.
    /// Host projects can implement this against whatever MIDI player they like.
    /// </summary>
    public interface IPlayMidi
    {
        /// <summary>Stop any currently-playing MIDI.</summary>
        void Stop();

        /// <summary>Play a new MIDI song from raw bytes.</summary>
        void Play(byte[] midiData);
    }
}
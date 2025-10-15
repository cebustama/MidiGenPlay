using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Small, testable façade for per-channel mix control.
    /// Values are normalized [0..1] and mapped to MIDI [0..127] internally.
    /// </summary>
    public interface IMixController
    {
        /// <summary>Set a single MIDI channel volume (normalized 0..1).</summary>
        void SetChannelVolume01(int channel, float volume01);

        /// <summary>Set multiple MIDI channel volumes in one call (normalized 0..1).</summary>
        void SetMultipleChannelVolumes01(IReadOnlyDictionary<int, float> volumes01);

        /// <summary>Get the last known normalized volume for a channel.</summary>
        float GetChannelVolume01(int channel);

        /// <summary>Set all 16 channels to a given normalized volume (default = 1.0).</summary>
        void ResetAll(float toVolume01 = 1f);
    }
}
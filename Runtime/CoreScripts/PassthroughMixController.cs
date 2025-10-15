using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Minimal pass-through mixer: forwards normalized volumes to the IPlayMidi backend.
    /// Keeps a tiny in-memory snapshot for queries and debugging.
    /// </summary>
    public sealed class PassthroughMixController : IMixController
    {
        private readonly IPlayMidi _player;
        private readonly float[] _lastVol01 = new float[16];

        public PassthroughMixController(IPlayMidi player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            for (int ch = 0; ch < 16; ch++) _lastVol01[ch] = 1f;
        }

        public void SetChannelVolume01(int channel, float volume01)
        {
            channel = Mathf.Clamp(channel, 0, 15);
            volume01 = Mathf.Clamp01(volume01);

            _lastVol01[channel] = volume01;

            // Map [0..1] -> [0..127] for the low-level player.
            int v127 = Mathf.RoundToInt(volume01 * 127f);
            _player.SetChannelVolume(channel, v127);
        }

        public void SetMultipleChannelVolumes01(IReadOnlyDictionary<int, float> volumes01)
        {
            if (volumes01 == null) return;
            foreach (var kv in volumes01)
                SetChannelVolume01(kv.Key, kv.Value);
        }

        public float GetChannelVolume01(int channel)
        {
            channel = Mathf.Clamp(channel, 0, 15);
            return _lastVol01[channel];
        }

        public void ResetAll(float toVolume01 = 1f)
        {
            toVolume01 = Mathf.Clamp01(toVolume01);
            for (int ch = 0; ch < 16; ch++)
                SetChannelVolume01(ch, toVolume01);
        }
    }
}
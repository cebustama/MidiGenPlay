using System;
using System.Linq;
using Melanchall.DryWetMidi.Core;

namespace MidiGenPlay
{
    /// <summary>
    /// Scales all tempo events (BPM) by a fixed factor (e.g., 0.75x = slower, 1.25x = faster).
    /// Implemented as a post-processor so we don't need to change the orchestrator.
    /// </summary>
    public sealed class TempoScalePostProcessor : IMidiPostProcessor
    {
        public string Name => $"Tempo Scale x{_factor:0.###}";
        public int Order { get; }

        private readonly double _factor;

        /// <param name="factor">>1.0 = faster; <1.0 = slower. Clamped to [0.25, 4.0]</param>
        /// <param name="order">Run early so other processors see final tempo; default -100.</param>
        public TempoScalePostProcessor(float factor, int order = -100)
        {
            if (!float.IsFinite(factor) || factor <= 0f) factor = 1f;
            _factor = Math.Clamp((double)factor, 0.25, 4.0);
            Order = order;
        }

        public MidiFile Process(MidiFile midi, IPostProcessContext ctx)
        {
            if (midi == null) return null;
            if (Math.Abs(_factor - 1.0) < 1e-6) return midi; // no-op

            int changed = 0;

            foreach (var chunk in midi.GetTrackChunks())
            {
                foreach (var ev in chunk.Events.OfType<SetTempoEvent>())
                {
                    // BPM' = factor * BPM = 60e6 / us'
                    // => us' = us / factor
                    var us = ev.MicrosecondsPerQuarterNote;
                    var scaled = (int)Math.Max(1, Math.Round(us / _factor));
                    if (scaled != us)
                    {
                        ev.MicrosecondsPerQuarterNote = scaled;
                        changed++;
                    }
                }
            }

            // If there were no tempo events at all, inject one at t=0
            if (changed == 0)
            {
                var first = midi.GetTrackChunks().FirstOrDefault();
                if (first != null)
                {
                    // default 120 BPM = 500,000 µs/qn
                    int usDefault = 500_000;
                    int usScaled = (int)Math.Max(1, Math.Round(usDefault / _factor));
                    first.Events.Insert(0, new SetTempoEvent(usScaled));
                    changed = 1;
                }
            }

            ctx?.Log($"[Post] Tempo scale x{_factor:0.###} (tempo events changed: {changed})");
            return midi; // in place
        }
    }
}

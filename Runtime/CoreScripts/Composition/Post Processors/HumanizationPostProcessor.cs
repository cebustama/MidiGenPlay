using System;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiGenPlay
{
    /// <summary>
    /// Slight, seeded timing/length/velocity variability for a more "human" feel.
    /// Deterministic via ctx.Rng. Mutates notes in place for performance.
    /// </summary>
    public sealed class HumanizationPostProcessor : IMidiPostProcessor
    {
        public string Name => "Humanization";
        public int Order { get; }

        private readonly int _maxTickOffset;       // ±ticks shift on Note start
        private readonly int _maxVelocityJitter;   // ±velocity
        private readonly int _maxLengthJitter;     // ±ticks on Note length
        private readonly bool _affectDrums;
        private readonly int _metronomeChannel;

        /// <param name="order">Lower runs first; leave 0 unless you need reordering.</param>
        /// <param name="affectDrums">Set false to avoid shifting drums (ch 9).</param>
        /// <param name="metronomeChannel">Channel reserved for metronome; never touched.</param>
        public HumanizationPostProcessor(
            int maxTickOffset,
            int maxVelocityJitter,
            int maxLengthJitter,
            int order = 0,
            bool affectDrums = true,
            int metronomeChannel = MidiGenerator.MetronomeChannel)
        {
            _maxTickOffset = Math.Max(0, maxTickOffset);
            _maxVelocityJitter = Math.Max(0, maxVelocityJitter);
            _maxLengthJitter = Math.Max(0, maxLengthJitter);
            Order = order;
            _affectDrums = affectDrums;
            _metronomeChannel = metronomeChannel;
        }

        public MidiFile Process(MidiFile midi, IPostProcessContext ctx)
        {
            if (midi == null) return null;

            var rng = ctx?.Rng ?? new Random();

            foreach (var chunk in midi.GetTrackChunks())
            {
                // Sort for deterministic traversal order
                var notes = chunk.GetNotes()
                                 .OrderBy(n => n.Time)
                                 .ThenBy(n => n.NoteNumber)
                                 .ThenBy(n => n.Channel)
                                 .ToList();

                foreach (var n in notes)
                {
                    var ch = (int)n.Channel;

                    // Never humanize the metronome; optionally skip drums (ch 9).
                    if (ch == _metronomeChannel) continue;
                    if (!_affectDrums && ch == 9) continue;

                    // --- timing (start) ---
                    long dt = _maxTickOffset == 0 ? 0
                             : rng.Next(-_maxTickOffset, _maxTickOffset + 1);
                    long newStart = n.Time + dt;
                    if (newStart < 0) newStart = 0;

                    // --- length ---
                    int lj = _maxLengthJitter == 0 ? 0
                           : rng.Next(-_maxLengthJitter, _maxLengthJitter + 1);
                    long newLen = n.Length + lj;
                    if (newLen < 1) newLen = 1;

                    // --- velocity ---
                    int vj = _maxVelocityJitter == 0 ? 0
                           : rng.Next(-_maxVelocityJitter, _maxVelocityJitter + 1);
                    int newVel = Math.Clamp((int)n.Velocity + vj, 1, 127);

                    // Apply
                    n.Time = newStart;
                    n.Length = newLen;
                    n.Velocity = (SevenBitNumber)newVel;
                }
            }

            ctx?.Log($"[Post] Humanization ±{_maxTickOffset}t, " +
                $"vel±{_maxVelocityJitter}, len±{_maxLengthJitter}");

            return midi; // in-place
        }
    }
}

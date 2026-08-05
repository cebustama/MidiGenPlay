using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// MGP-ALWTTT-BASS-BEND-1 (D-BEND-EMIT=B): the shared post-build pitch
    /// bend writer. A composer that planned legato gestures calls
    /// <see cref="ApplyStepGestures"/> on its OWN built <c>MidiFile</c>,
    /// immediately after <c>pb.Build().ToFile(tempoMap)</c> and BEFORE
    /// <c>ForceAllChannel</c> / <c>StampBankAndPatch</c> — the same
    /// post-build-surgery molde those helpers already follow, so the channel
    /// stamp and the bank/patch tick shift apply to bend events and notes
    /// alike.
    ///
    /// Domain: pure MIDI. The writer knows NOTHING about beats, meters or
    /// scales — gestures arrive in TICKS, converted by the composer against
    /// the same tempoMap/beatSpan its notes used (meter authority stays
    /// where SSoT_CONTRACTS §5 puts it). It draws no rng, reads no state,
    /// touches no other file: same gestures + same file => same bytes.
    ///
    /// Byte-identity guarantee: a null/empty gesture list is a hard no-op —
    /// the file is not read, not rebuilt, not touched. Every render that
    /// plans no gestures is byte-identical to a build without this class.
    ///
    /// Contract (single-channel track files): the writer operates on the
    /// FIRST track chunk (the StampBankAndPatch convention). Pitch bend is
    /// CHANNEL state — on a polyphonic channel a gesture would detune every
    /// sounding voice. Composers with dedicated monophonic channels (bass,
    /// melody) are the intended callers; the backing track is a declared
    /// non-consumer.
    ///
    /// Laws:
    /// - D-BEND-RANGE=A: values are scaled against <paramref
    ///   name="rangeSemitones"/> (default = the GM ±2 assumption). Targets
    ///   beyond the range CLAMP with a warning — a shrunk interval, never a
    ///   wrong pitch class direction. No RPN is emitted in v1; the range
    ///   parameter is the declared seam for the slide follow-up.
    /// - D-BEND-RESET=A: every gesture carries its own reset tick (the
    ///   carrier's note-off); the writer emits center (8192) there. A reset
    ///   that lands on the SAME tick as a later bend point coalesces away
    ///   (last value at a tick wins — chained legato stays clean). Closing
    ///   invariant: after coalescing, if the final bend point is not center,
    ///   a defensive reset is appended at the end of the chunk — no render
    ///   can leave the channel detuned past its last gesture.
    /// - Same-tick event order (the BEND-1 ordering law): at any tick, a
    ///   bend point is written AFTER every non-note-on event already there
    ///   (note-offs first) and BEFORE the first sounding note-on — a note
    ///   starting on the reset tick starts centered, never bent-for-0-ticks.
    ///
    /// Degrade contract (warn max, never throw, never silence): a gesture
    /// with <c>resetTick &lt; bendTick</c> or a negative bend tick is
    /// skipped with a warning; a file without track chunks warns and
    /// returns.
    /// </summary>
    public static class PitchBendWriter
    {
        private const string LogTag = "[PitchBendWriter]";

        /// <summary>14-bit pitch bend center — no detune.</summary>
        public const ushort Center = 8192;

        /// <summary>Maximum 14-bit pitch bend value.</summary>
        public const ushort MaxValue = 16383;

        /// <summary>D-BEND-RANGE=A: the GM default pitch bend sensitivity
        /// (±2 semitones) assumed in v1 — no RPN negotiation.</summary>
        public const int DefaultRangeSemitones = 2;

        /// <summary>
        /// One step gesture (D-BEND-GEST=A): at <see cref="bendTick"/> the
        /// channel detunes INSTANTLY to <see cref="targetSemitones"/> from
        /// center (absolute channel detune, NOT an increment — chained
        /// legato passes the cumulative target); at <see cref="resetTick"/>
        /// it returns to center. Ramps (slide) are a declared follow-up,
        /// not a hidden feature of this struct.
        /// </summary>
        public readonly struct StepGesture
        {
            public readonly long bendTick;
            public readonly double targetSemitones;
            public readonly long resetTick;

            public StepGesture(long bendTick, double targetSemitones,
                long resetTick)
            {
                this.bendTick = bendTick;
                this.targetSemitones = targetSemitones;
                this.resetTick = resetTick;
            }
        }

        /// <summary>
        /// Pure value law: signed semitones-from-center → 14-bit bend value
        /// against the given range. Clamps the input to ±range first (the
        /// D-BEND-RANGE=A degradation), then clamps the result to 0..16383
        /// (+range maps to 16384 pre-clamp; the one-unit loss at the top is
        /// ~0.24 cents at range 2 — inaudible, on record). Deterministic
        /// rounding: away-from-zero, immune to banker's-rounding surprises.
        /// </summary>
        public static ushort SemitonesToBendValue(
            double semitones, int rangeSemitones = DefaultRangeSemitones)
        {
            int range = Math.Max(1, rangeSemitones);
            double clamped = Math.Max(-range, Math.Min(range, semitones));
            long v = Center + (long)Math.Round(
                Center * clamped / range, MidpointRounding.AwayFromZero);
            if (v < 0) v = 0;
            if (v > MaxValue) v = MaxValue;
            return (ushort)v;
        }

        /// <summary>
        /// Applies the gestures to <paramref name="file"/>'s first track
        /// chunk. See the class doc for the full contract. The channel on
        /// the inserted events defaults to 0 — callers following the house
        /// molde run <c>ForceAllChannel</c> afterwards, which stamps bend
        /// events like any other channel event; standalone callers pass
        /// their channel explicitly.
        /// </summary>
        public static void ApplyStepGestures(
            MidiFile file,
            IReadOnlyList<StepGesture> gestures,
            int rangeSemitones = DefaultRangeSemitones,
            int channel = 0)
        {
            // Byte-identity fast path: no gestures => the file is untouched.
            if (file == null || gestures == null || gestures.Count == 0)
                return;

            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                Debug.LogWarning($"{LogTag} file has no track chunks; " +
                    $"gestures dropped (degrade contract: warn, never throw).");
                return;
            }

            int range = Math.Max(1, rangeSemitones);

            // 1) Gestures -> raw bend points, input order preserved.
            var points = new List<(long tick, ushort value)>(
                gestures.Count * 2);
            for (int i = 0; i < gestures.Count; i++)
            {
                var g = gestures[i];
                if (g.bendTick < 0 || g.resetTick < g.bendTick)
                {
                    Debug.LogWarning($"{LogTag} gesture {i} skipped: " +
                        $"bendTick={g.bendTick} resetTick={g.resetTick} " +
                        $"(need 0 <= bendTick <= resetTick).");
                    continue;
                }
                if (Math.Abs(g.targetSemitones) > range)
                {
                    Debug.LogWarning($"{LogTag} gesture {i} target " +
                        $"{g.targetSemitones:+0.##;-0.##} exceeds the " +
                        $"±{range} semitone range and is clamped " +
                        $"(D-BEND-RANGE=A declared degradation).");
                }
                points.Add((g.bendTick,
                    SemitonesToBendValue(g.targetSemitones, range)));
                points.Add((g.resetTick, Center));
            }
            if (points.Count == 0) return;

            // 2) Stable sort by tick (OrderBy is stable: same-tick points
            //    keep gesture order, so a chain's reset precedes the next
            //    bend and coalesces away below).
            var sorted = points.OrderBy(p => p.tick).ToList();

            // 3) Coalesce same-tick points: the LAST value at a tick wins —
            //    one bend event per tick, chains stay byte-minimal.
            var final = new List<(long tick, ushort value)>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (final.Count > 0 && final[final.Count - 1].tick ==
                    sorted[i].tick)
                    final[final.Count - 1] = sorted[i];
                else
                    final.Add(sorted[i]);
            }

            // 4) Closing invariant (D-BEND-RESET=A, defensive half): the
            //    last surviving point must be center. Unreachable through
            //    well-formed gestures (each carries its reset), kept as the
            //    belt-and-suspenders guard for future gesture kinds.
            long chunkEnd = 0;
            foreach (var ev in chunk.Events) chunkEnd += ev.DeltaTime;
            if (final[final.Count - 1].value != Center)
                final.Add((Math.Max(final[final.Count - 1].tick, chunkEnd),
                    Center));

            // 5) Merge into the event stream under the same-tick ordering
            //    law: a bend point at tick T goes after every existing
            //    event at T that is NOT a sounding note-on, and before the
            //    first sounding note-on at T.
            var existing = new List<(long tick, MidiEvent ev)>(
                chunk.Events.Count);
            long acc = 0;
            foreach (var ev in chunk.Events)
            {
                acc += ev.DeltaTime;
                existing.Add((acc, ev));
            }

            var merged = new List<(long tick, MidiEvent ev)>(
                existing.Count + final.Count);
            int bi = 0;
            for (int ei = 0; ei <= existing.Count; ei++)
            {
                bool atEnd = ei == existing.Count;
                long here = atEnd ? long.MaxValue : existing[ei].tick;
                var ev = atEnd ? null : existing[ei].ev;

                while (bi < final.Count &&
                       (final[bi].tick < here ||
                        (final[bi].tick == here && IsSoundingNoteOn(ev))))
                {
                    merged.Add((final[bi].tick,
                        new PitchBendEvent(final[bi].value)
                        { Channel = (FourBitNumber)channel }));
                    bi++;
                }
                if (!atEnd) merged.Add(existing[ei]);
            }

            // 6) Rewrite delta times (canonical: delta = tick difference —
            //    identical to the originals for every untouched event) and
            //    swap the chunk's event list in place.
            long prev = 0;
            chunk.Events.Clear();
            for (int i = 0; i < merged.Count; i++)
            {
                merged[i].ev.DeltaTime = merged[i].tick - prev;
                prev = merged[i].tick;
                chunk.Events.Add(merged[i].ev);
            }
        }

        private static bool IsSoundingNoteOn(MidiEvent ev)
            => ev is NoteOnEvent n && n.Velocity > 0;
    }
}
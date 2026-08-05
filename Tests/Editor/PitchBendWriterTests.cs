#if UNITY_EDITOR
// MGP-ALWTTT-BASS-BEND-1, step 2a — pins for the shared post-build pitch
// bend writer (D-BEND-EMIT=B / D-BEND-RESET=A / D-BEND-RANGE=A), BEFORE it
// is wired into any composer. Everything here is pure-MIDI: hand-built
// TrackChunks, no orchestrator, no fixtures — the writer must be provably
// correct in isolation so the composer integration diff stays small.
//
// Laws pinned:
// - VALUE (D-BEND-RANGE=A): center 8192; ±1 st at range 2 = ±4096; +range
//   clamps to 16383, -range to 0; out-of-range targets clamp (with warn);
//   range parameter rescales; away-from-zero rounding.
// - NO-OP (byte-identity guarantee): null/empty gestures leave the file
//   untouched — event-for-event, delta-for-delta.
// - ORDER (same-tick law): note-off -> bend/reset -> note-on at the same
//   tick; a note starting on the reset tick starts centered.
// - RESET (D-BEND-RESET=A): every gesture's reset lands at its resetTick;
//   a chain's mid-reset coalesces into the next bend (one event per tick);
//   the LAST bend event in the chunk is always center.
// - DEGRADE: invalid gestures warn + skip; existing note ticks survive
//   every insertion unchanged.

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace MidiGenPlay.Tests.Editor
{
    public class PitchBendWriterTests
    {
        // ------------------------------------------------------------------
        // Harness — hand-built single-chunk files, absolute-tick flattening
        // ------------------------------------------------------------------

        private static MidiFile TwoNotesFile()
        {
            // Note A: [0, 480). Note B: [480, 960). Note-off(A) and
            // note-on(B) share tick 480 — the ordering law's hot spot.
            var chunk = new TrackChunk(
                new NoteOnEvent((SevenBitNumber)40, (SevenBitNumber)100)
                { DeltaTime = 0 },
                new NoteOffEvent((SevenBitNumber)40, (SevenBitNumber)0)
                { DeltaTime = 480 },
                new NoteOnEvent((SevenBitNumber)42, (SevenBitNumber)100)
                { DeltaTime = 0 },
                new NoteOffEvent((SevenBitNumber)42, (SevenBitNumber)0)
                { DeltaTime = 480 });
            return new MidiFile(chunk);
        }

        private static List<(long tick, MidiEvent ev)> Flatten(MidiFile file)
        {
            var chunk = file.GetTrackChunks().First();
            var list = new List<(long, MidiEvent)>();
            long acc = 0;
            foreach (var ev in chunk.Events)
            {
                acc += ev.DeltaTime;
                list.Add((acc, ev));
            }
            return list;
        }

        private static List<(long tick, ushort value)> Bends(MidiFile file)
            => Flatten(file)
                .Where(t => t.ev is PitchBendEvent)
                .Select(t => (t.tick, ((PitchBendEvent)t.ev).PitchValue))
                .ToList();

        private static List<long> NoteTicks(MidiFile file)
            => Flatten(file)
                .Where(t => t.ev is NoteOnEvent n && n.Velocity > 0)
                .Select(t => t.tick)
                .ToList();

        private static PitchBendWriter.StepGesture G(
            long bend, double semis, long reset)
            => new PitchBendWriter.StepGesture(bend, semis, reset);

        private const ushort C = PitchBendWriter.Center;

        // ------------------------------------------------------------------
        // Value law (D-BEND-RANGE=A)
        // ------------------------------------------------------------------

        [Test]
        public void Value_CenterHalfAndFullScale_AtGmRange()
        {
            Assert.That(PitchBendWriter.SemitonesToBendValue(0), Is.EqualTo(C));
            Assert.That(PitchBendWriter.SemitonesToBendValue(+1),
                Is.EqualTo(C + 4096), "+1 st = half scale up at range 2");
            Assert.That(PitchBendWriter.SemitonesToBendValue(-1),
                Is.EqualTo(C - 4096));
            Assert.That(PitchBendWriter.SemitonesToBendValue(+2),
                Is.EqualTo(16383),
                "+range maps to 16384 pre-clamp; the one-unit top loss is " +
                "the documented ~0.24-cent degradation");
            Assert.That(PitchBendWriter.SemitonesToBendValue(-2),
                Is.EqualTo(0));
        }

        [Test]
        public void Value_BeyondRange_ClampsToTheExtremes()
        {
            Assert.That(PitchBendWriter.SemitonesToBendValue(+5),
                Is.EqualTo(16383));
            Assert.That(PitchBendWriter.SemitonesToBendValue(-5),
                Is.EqualTo(0));
        }

        [Test]
        public void Value_RangeParameter_Rescales()
        {
            // +1 st at range 12: 8192 + round(8192/12) = 8192 + 683
            // (682.666… rounds away from zero).
            Assert.That(PitchBendWriter.SemitonesToBendValue(+1, 12),
                Is.EqualTo(8192 + 683));
            // +2 st at range 12 no longer saturates.
            Assert.That(PitchBendWriter.SemitonesToBendValue(+2, 12),
                Is.EqualTo(8192 + 1365), "round(16384/12) = 1365.33 -> 1365");
        }

        // ------------------------------------------------------------------
        // No-op fast path — the byte-identity guarantee
        // ------------------------------------------------------------------

        [Test]
        public void NoGestures_FileIsUntouched_EventForEvent()
        {
            var a = TwoNotesFile();
            var reference = Flatten(a)
                .Select(t => (t.tick, t.ev.GetType(), t.ev.DeltaTime))
                .ToList();

            PitchBendWriter.ApplyStepGestures(a, null);
            PitchBendWriter.ApplyStepGestures(a,
                new List<PitchBendWriter.StepGesture>());

            var after = Flatten(a)
                .Select(t => (t.tick, t.ev.GetType(), t.ev.DeltaTime))
                .ToList();
            Assert.That(after, Is.EqualTo(reference),
                "null/empty gestures must not rebuild, re-delta or touch " +
                "the file in any way — this IS the composer-side " +
                "byte-identity argument");
            Assert.That(Bends(a), Is.Empty);
        }

        // ------------------------------------------------------------------
        // Single gesture — placement, reset, ordering at the shared tick
        // ------------------------------------------------------------------

        [Test]
        public void SingleGesture_BendMidNote_ResetAtNoteBoundary()
        {
            var f = TwoNotesFile();
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(bend: 240, semis: +1, reset: 480) });

            Assert.That(Bends(f), Is.EqualTo(
                new List<(long, ushort)> { (240, (ushort)(C + 4096)),
                                           (480, C) }));
        }

        [Test]
        public void OrderingLaw_AtTheSharedTick_OffThenResetThenOn()
        {
            var f = TwoNotesFile();
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(240, +1, 480) });

            // At tick 480 the stream must read: note-off(A), reset, then
            // note-on(B) — B starts centered, never bent-for-0-ticks.
            var at480 = Flatten(f).Where(t => t.tick == 480)
                .Select(t => t.ev).ToList();
            Assert.That(at480.Count, Is.EqualTo(3));
            Assert.That(at480[0], Is.InstanceOf<NoteOffEvent>());
            Assert.That(at480[1], Is.InstanceOf<PitchBendEvent>());
            Assert.That(((PitchBendEvent)at480[1]).PitchValue, Is.EqualTo(C));
            Assert.That(at480[2], Is.InstanceOf<NoteOnEvent>());
        }

        [Test]
        public void Insertion_NeverMovesExistingNotes()
        {
            var f = TwoNotesFile();
            var notesBefore = NoteTicks(f);
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(0, +1, 100), G(240, -1, 480), G(700, +2, 960) });
            Assert.That(NoteTicks(f), Is.EqualTo(notesBefore),
                "surgery adds events; it never re-times the music");
        }

        [Test]
        public void GestureBeyondChunkEnd_AppendsAtItsTick()
        {
            var f = TwoNotesFile(); // ends at tick 960
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(2000, +1, 2400) });

            Assert.That(Bends(f), Is.EqualTo(
                new List<(long, ushort)> { (2000, (ushort)(C + 4096)),
                                           (2400, C) }));
            var flat = Flatten(f);
            Assert.That(flat[flat.Count - 1].tick, Is.EqualTo(2400));
        }

        // ------------------------------------------------------------------
        // Chains — coalescing and the closing invariant (D-BEND-RESET=A)
        // ------------------------------------------------------------------

        [Test]
        public void Chain_MidResetCoalescesIntoTheNextBend_OneEventPerTick()
        {
            var f = TwoNotesFile();
            // Hammer at 100 (+1 -> reset 200), chained hammer at 200
            // (cumulative +2 -> reset 300). At tick 200 the first gesture's
            // reset and the second's bend collide: the LAST value wins —
            // the channel steps 0 -> +1 -> +2 -> 0 with exactly one event
            // per tick, no zero-length center dip.
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(100, +1, 200), G(200, +2, 300) });

            Assert.That(Bends(f), Is.EqualTo(
                new List<(long, ushort)>
                {
                    (100, (ushort)(C + 4096)),
                    (200, (ushort)16383),
                    (300, C)
                }));
        }

        [Test]
        public void Chain_LastBendEventIsAlwaysCenter()
        {
            var f = TwoNotesFile();
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(0, +1, 120), G(120, +2, 240), G(700, -1, 900) });

            var bends = Bends(f);
            Assert.That(bends.Last().value, Is.EqualTo(C),
                "no render may leave the channel detuned past its last " +
                "gesture — the rented-apartment law");
        }

        // ------------------------------------------------------------------
        // Degrade contract
        // ------------------------------------------------------------------

        [Test]
        public void InvalidGesture_WarnsAndIsSkipped_OthersStillApply()
        {
            var f = TwoNotesFile();
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "PitchBendWriter.*skipped"));
            PitchBendWriter.ApplyStepGestures(f,
                new[]
                {
                    G(bend: 500, semis: +1, reset: 100), // reset < bend
                    G(bend: 240, semis: +1, reset: 480), // valid
                });

            Assert.That(Bends(f), Is.EqualTo(
                new List<(long, ushort)> { (240, (ushort)(C + 4096)),
                                           (480, C) }),
                "warn max, never throw — and the valid gesture survives");
        }

        [Test]
        public void OutOfRangeTarget_WarnsAndClampsInsideTheApply()
        {
            var f = TwoNotesFile();
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "PitchBendWriter.*clamped"));
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(240, +4, 480) });

            Assert.That(Bends(f)[0].value, Is.EqualTo(16383),
                "D-BEND-RANGE=A: a too-wide chained interval shrinks to the " +
                "range edge; direction and reset stay correct");
        }

        [Test]
        public void ChannelParameter_StampsTheInsertedEvents()
        {
            var f = TwoNotesFile();
            PitchBendWriter.ApplyStepGestures(f,
                new[] { G(240, +1, 480) }, channel: 3);

            var evs = Flatten(f).Where(t => t.ev is PitchBendEvent)
                .Select(t => (PitchBendEvent)t.ev).ToList();
            Assert.That(evs.All(e => (int)e.Channel == 3), Is.True,
                "standalone callers pass their channel; house-molde callers " +
                "rely on ForceAllChannel stamping bends like any channel " +
                "event afterwards");
        }

        // ------------------------------------------------------------------
        // Determinism
        // ------------------------------------------------------------------

        [Test]
        public void SameGestures_SameFile_SameBytes()
        {
            (List<(long, System.Type, long)> shape, List<(long, ushort)> bends)
                Run()
            {
                var f = TwoNotesFile();
                PitchBendWriter.ApplyStepGestures(f,
                    new[] { G(100, +1, 200), G(200, +2, 300),
                            G(700, -1, 900) });
                return (
                    Flatten(f).Select(t =>
                        (t.tick, t.ev.GetType(), t.ev.DeltaTime)).ToList(),
                    Bends(f));
            }

            var a = Run();
            var b = Run();
            Assert.That(a.shape, Is.EqualTo(b.shape));
            Assert.That(a.bends, Is.EqualTo(b.bends));
        }
    }
}
#endif
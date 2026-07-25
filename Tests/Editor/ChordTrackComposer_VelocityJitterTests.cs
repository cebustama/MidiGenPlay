#if UNITY_EDITOR
// CA-V1 (part 2) — EditMode tests for the seeded velocity jitter.
//
// Unlike the ARTIC-1 roller tests (which use the SEED-1 variance idiom because
// System.Random sequences are runtime-stable but not specified across .NET
// versions), the jitter is an integer-only pure mix, so EXACT goldens are
// pinnable here. If a golden fails, the mix or the substream derivation changed
// and every consumer's jittered render shifts.
//
// Internal visibility via Runtime/AssemblyInfo.cs:
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using MidiGenPlay.Composition;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_VelocityJitterTests
    {
        // Golden FNV-1a 32-bit values for the two CA-V1 substreams, computed
        // independently of the implementation.
        private const int JitterSeedFromZero = 1866958453;    // "0|articvel"
        private const int JitterSeedFrom12345 = 1954672100;   // "12345|articvel"
        private const int RateSeedFromZero = 2061144176;      // "0|articrate"
        private const int RateSeedFrom12345 = -1686614589;    // "12345|articrate"

        // ---------- substream derivation ----------

        [Test]
        public void ResolveVelocityJitterSeed_MatchesGoldenHash()
        {
            Assert.AreEqual(JitterSeedFromZero,
                SongOrchestrator.ResolveVelocityJitterSeed(0));
            Assert.AreEqual(JitterSeedFrom12345,
                SongOrchestrator.ResolveVelocityJitterSeed(12345));
        }

        [Test]
        public void ResolveArticulationRateSeed_MatchesGoldenHash()
        {
            Assert.AreEqual(RateSeedFromZero,
                SongOrchestrator.ResolveArticulationRateSeed(0));
            Assert.AreEqual(RateSeedFrom12345,
                SongOrchestrator.ResolveArticulationRateSeed(12345));
        }

        [Test]
        public void ArticulationSubstreams_AreMutuallyDistinct()
        {
            int artic = SongOrchestrator.ResolveArticulationSeed(0);
            int rate = SongOrchestrator.ResolveArticulationRateSeed(0);
            int vel = SongOrchestrator.ResolveVelocityJitterSeed(0);

            Assert.AreNotEqual(artic, rate);
            Assert.AreNotEqual(artic, vel);
            Assert.AreNotEqual(rate, vel);
        }

        // ---------- the pure mix ----------

        [Test]
        public void DeltaFor_MatchesGoldens()
        {
            var render = new VelocityJitter(8, JitterSeedFromZero);

            CollectionAssert.AreEqual(
                new[] { 5, -3, -1, 6, 4, 0 },
                Enumerable.Range(0, 6).Select(h => render.ForEvent(0).DeltaFor(h)).ToArray());

            CollectionAssert.AreEqual(
                new[] { 8, -4, -3, -7, -8, -7 },
                Enumerable.Range(0, 6).Select(h => render.ForEvent(1).DeltaFor(h)).ToArray());

            CollectionAssert.AreEqual(
                new[] { -1, 7, -3, -5, -3, -2 },
                Enumerable.Range(0, 6).Select(h => render.ForEvent(2).DeltaFor(h)).ToArray());
        }

        [Test]
        public void DeltaFor_NeverExceedsAmount()
        {
            foreach (int amount in new[] { 1, 3, 8, 32, VelocityJitter.MaxAmount })
            {
                var render = new VelocityJitter(amount, JitterSeedFromZero);
                for (int e = 0; e < 200; e++)
                {
                    var ev = render.ForEvent(e);
                    for (int h = 0; h < 16; h++)
                        Assert.LessOrEqual(System.Math.Abs(ev.DeltaFor(h)), amount);
                }
            }
        }

        [Test]
        public void DeltaFor_CoversTheWholeRange()
        {
            var render = new VelocityJitter(2, JitterSeedFromZero);
            var seen = new HashSet<int>();
            for (int e = 0; e < 400; e++)
            {
                var ev = render.ForEvent(e);
                for (int h = 0; h < 8; h++) seen.Add(ev.DeltaFor(h));
            }
            CollectionAssert.AreEquivalent(new[] { -2, -1, 0, 1, 2 }, seen.ToArray());
        }

        [Test]
        public void EventAndHitFolds_AreNotSymmetric()
        {
            var render = new VelocityJitter(8, JitterSeedFromZero);
            Assert.AreNotEqual(
                render.ForEvent(1).DeltaFor(2),
                render.ForEvent(2).DeltaFor(1));
        }

        [Test]
        public void ZeroAmount_IsIdentityAndDefaultIsOff()
        {
            Assert.IsTrue(default(VelocityJitter).IsOff);
            Assert.IsTrue(new VelocityJitter(0, 12345).IsOff);
            Assert.AreEqual(0, new VelocityJitter(0, 12345).ForEvent(7).DeltaFor(3));
        }

        [Test]
        public void Amount_IsClampedToMaxAmount()
        {
            Assert.AreEqual(VelocityJitter.MaxAmount,
                new VelocityJitter(9999, 1).Amount);
            Assert.AreEqual(0, new VelocityJitter(-5, 1).Amount);
        }

        [Test]
        public void DifferentRenderSeeds_ProduceDifferentJitter()
        {
            var a = new VelocityJitter(8, JitterSeedFromZero);
            var b = new VelocityJitter(8, JitterSeedFrom12345);

            var seqA = Enumerable.Range(0, 24)
                .Select(i => a.ForEvent(i / 4).DeltaFor(i % 4)).ToArray();
            var seqB = Enumerable.Range(0, 24)
                .Select(i => b.ForEvent(i / 4).DeltaFor(i % 4)).ToArray();

            CollectionAssert.AreNotEqual(seqA, seqB);
        }

        // ---------- PlanHits integration ----------

        [Test]
        public void PlanHits_DefaultJitter_IsExactIdentity()
        {
            foreach (ChordExpressionType expr in
                     System.Enum.GetValues(typeof(ChordExpressionType)))
            {
                var withoutArg = ChordArticulator.PlanHits(
                    expr, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 100);
                var withOff = ChordArticulator.PlanHits(
                    expr, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 100,
                    default(VelocityJitter));

                CollectionAssert.AreEqual(
                    withoutArg.Select(h => h.Velocity).ToArray(),
                    withOff.Select(h => h.Velocity).ToArray(),
                    $"{expr} velocities drifted under a default jitter");
            }
        }

        [Test]
        public void PlanHits_Block_AppliesFirstHitDelta()
        {
            var jitter = new VelocityJitter(8, JitterSeedFromZero).ForEvent(0);
            var hits = ChordArticulator.PlanHits(
                ChordExpressionType.Block, ArpeggioRate.Eighth,
                0.0, 4.0, 4, 3, 100, jitter);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(105, hits[0].Velocity);   // 100 + 5 (golden)
        }

        [Test]
        public void PlanHits_PerBeat_AppliesPerHitDeltaOverTheCurve()
        {
            var jitter = new VelocityJitter(8, JitterSeedFromZero).ForEvent(0);
            var hits = ChordArticulator.PlanHits(
                ChordExpressionType.PerBeat, ArpeggioRate.Eighth,
                0.0, 4.0, 4, 3, 100, jitter);

            // Curve: 100 (downbeat) / 85 / 85 / 85; goldens: +5 / -3 / -1 / +6.
            CollectionAssert.AreEqual(
                new[] { 105, 82, 84, 91 },
                hits.Select(h => h.Velocity).ToArray());
        }

        [Test]
        public void PlanHits_Jitter_ClampsToOneAnd127()
        {
            // Lower: base 1, event 2 hit 0 golden = -1 => 0 => clamped to 1.
            var low = ChordArticulator.PlanHits(
                ChordExpressionType.Block, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 1,
                new VelocityJitter(8, JitterSeedFromZero).ForEvent(2));
            Assert.AreEqual(1, low[0].Velocity);

            // Upper: base 127, event 1 hit 0 golden = +8 => 135 => clamped to 127.
            var high = ChordArticulator.PlanHits(
                ChordExpressionType.Block, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 127,
                new VelocityJitter(8, JitterSeedFromZero).ForEvent(1));
            Assert.AreEqual(127, high[0].Velocity);
        }

        [Test]
        public void PlanHits_Jitter_LeavesTimingAndNoteIndicesUntouched()
        {
            var plain = ChordArticulator.PlanHits(
                ChordExpressionType.ArpeggioUp, ArpeggioRate.Sixteenth,
                0.0, 2.0, 4, 3, 100);
            var jittered = ChordArticulator.PlanHits(
                ChordExpressionType.ArpeggioUp, ArpeggioRate.Sixteenth,
                0.0, 2.0, 4, 3, 100,
                new VelocityJitter(8, JitterSeedFromZero).ForEvent(0));

            Assert.AreEqual(plain.Count, jittered.Count);
            for (int i = 0; i < plain.Count; i++)
            {
                Assert.AreEqual(plain[i].StartBeats, jittered[i].StartBeats, 1e-9);
                Assert.AreEqual(plain[i].DurBeats, jittered[i].DurBeats, 1e-9);
                Assert.AreEqual(plain[i].NoteIndex, jittered[i].NoteIndex);
            }
        }

        [Test]
        public void PlanHits_Jitter_IsDeterministic()
        {
            var j = new VelocityJitter(8, JitterSeedFromZero).ForEvent(3);
            var a = ChordArticulator.PlanHits(
                ChordExpressionType.Offbeat, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 100, j);
            var b = ChordArticulator.PlanHits(
                ChordExpressionType.Offbeat, ArpeggioRate.Eighth, 0.0, 4.0, 4, 3, 100, j);

            CollectionAssert.AreEqual(
                a.Select(h => h.Velocity).ToArray(),
                b.Select(h => h.Velocity).ToArray());
        }

        // ---------- rate sentinel degrade ----------

        [Test]
        public void ArpeggioIntervalBeats_RandomSentinel_DegradesToEighth()
        {
            Assert.AreEqual(
                ChordArticulator.ArpeggioIntervalBeats(ArpeggioRate.Eighth),
                ChordArticulator.ArpeggioIntervalBeats(ArpeggioRate.Random),
                1e-9);
        }
    }
}
#endif
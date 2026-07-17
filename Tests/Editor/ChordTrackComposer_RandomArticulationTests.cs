#if UNITY_EDITOR
// MGP-ALWTTT-ARTIC-1 — EditMode tests for the Random articulation policy.
//
// Targets the internal seams (the SEED-1 / CA-T1 idiom — no SongConfig or
// asset-DB fixtures):
//   - SongOrchestrator.ResolveArticulationSeed (golden FNV-1a values computed
//     independently of the implementation)
//   - RandomArticulationRoller.NextFigure (determinism, variance across
//     seeds, rerollChance semantics, never-returns-Random)
//   - RandomArticulationRoller.BuildWeightTable (SD-2 pool semantics:
//     uniform default, entries-define-the-pool, exclusion, duplicate
//     summing, Random-entry ignore, degenerate fallback)
//   - ChordArticulator.PlanHits defensive Random -> Block degrade (D6)
//
// Variance assertions use the SEED-1 idiom (distinct seeds => distinct
// sequences; same seed => same sequence) rather than exact roll goldens,
// because System.Random sequences are runtime-stable but not specified
// across .NET versions.
//
// Internal visibility via Runtime/AssemblyInfo.cs:
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using MidiGenPlay.Composition;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_RandomArticulationTests
    {
        // Golden FNV-1a 32-bit values for "{trackSeed}|artic", computed
        // independently from the implementation. If these fail, the
        // derivation string format or the hash changed and every consumer's
        // Random renders shift.
        private const int GoldenArtic_0 = -72461630;   // "0|artic"
        private const int GoldenArtic_12345 = 224119985;   // "12345|artic"
        private const int GoldenArtic_TrackSeed = 1620298623;  // "-215185059|artic"

        private static RandomArticulationRoller Roller(
            int seed, float chance = 1f,
            IReadOnlyList<ChordExpressionWeight> weights = null)
            => new RandomArticulationRoller(new System.Random(seed), chance, weights);

        private static List<ChordExpressionType> Sequence(
            RandomArticulationRoller roller, int n)
        {
            var seq = new List<ChordExpressionType>(n);
            for (int i = 0; i < n; i++) seq.Add(roller.NextFigure());
            return seq;
        }

        private static ChordExpressionWeight W(ChordExpressionType f, float w)
            => new ChordExpressionWeight { figure = f, weight = w };

        // ---------------- Seed derivation seam ----------------

        [Test]
        public void ResolveArticulationSeed_MatchesGoldenValues()
        {
            Assert.That(SongOrchestrator.ResolveArticulationSeed(0),
                Is.EqualTo(GoldenArtic_0));
            Assert.That(SongOrchestrator.ResolveArticulationSeed(12345),
                Is.EqualTo(GoldenArtic_12345));
            // A realistic trackSeed (the SEED-1 golden for
            // "0|p=0|r=Rhythm|m=drummer") chained into the artic substream.
            Assert.That(SongOrchestrator.ResolveArticulationSeed(-215185059),
                Is.EqualTo(GoldenArtic_TrackSeed));
        }

        [Test]
        public void ResolveArticulationSeed_DiffersFromItsTrackSeed()
        {
            // The substream must not alias the track stream itself.
            foreach (var s in new[] { 0, 1, 12345, -215185059 })
                Assert.That(SongOrchestrator.ResolveArticulationSeed(s),
                    Is.Not.EqualTo(s));
        }

        // ---------------- Determinism (the held-loop guarantee) ----------------

        [Test]
        public void SameSeed_SameRollSequence()
        {
            var a = Sequence(Roller(seed: 42), 16);
            var b = Sequence(Roller(seed: 42), 16);
            Assert.That(a, Is.EqualTo(b),
                "Same seed must reproduce the identical figure sequence " +
                "(ALWTTT held-loop replay).");
        }

        [Test]
        public void SameSeed_SameRollSequence_AtIntermediateChance()
        {
            var a = Sequence(Roller(seed: 7, chance: 0.5f), 16);
            var b = Sequence(Roller(seed: 7, chance: 0.5f), 16);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void DistinctSeeds_ProduceDistinctSequences()
        {
            // SEED-1 variance idiom: over several seeds, at least two
            // distinct 8-event sequences must appear.
            var distinct = new HashSet<string>();
            for (int seed = 1; seed <= 6; seed++)
                distinct.Add(string.Join(",", Sequence(Roller(seed), 8)));
            Assert.That(distinct.Count, Is.GreaterThanOrEqualTo(2),
                "Distinct seeds must yield distinct roll sequences.");
        }

        // ---------------- rerollChance (SD-1=A) ----------------

        [Test]
        public void ChanceZero_OneFigureForTheWholeRender()
        {
            var seq = Sequence(Roller(seed: 42, chance: 0f), 16);
            Assert.That(seq.Distinct().Count(), Is.EqualTo(1),
                "chance=0 must hold the first rolled figure for every event.");
        }

        [Test]
        public void ChanceOne_RollsVaryAcrossEvents()
        {
            // Uniform pool of 6 over 16 events: an all-equal sequence has
            // probability ~6^-15 — treated as impossible for a fixed seed.
            var seq = Sequence(Roller(seed: 42, chance: 1f), 16);
            Assert.That(seq.Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                "chance=1 must re-roll per chord event.");
        }

        [Test]
        public void ChanceIsClamped()
        {
            // Out-of-range values behave as their clamped equivalents.
            var neg = Sequence(Roller(seed: 9, chance: -3f), 12);
            Assert.That(neg.Distinct().Count(), Is.EqualTo(1),
                "chance < 0 clamps to 0 (single figure).");
            var big = Sequence(Roller(seed: 9, chance: 5f), 16);
            Assert.That(big.Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                "chance > 1 clamps to 1 (per-event roll).");
        }

        // ---------------- Pool / sentinel discipline ----------------

        [Test]
        public void NextFigure_NeverReturnsRandom()
        {
            var roller = Roller(seed: 123);
            for (int i = 0; i < 200; i++)
            {
                var f = roller.NextFigure();
                Assert.That((int)f, Is.InRange(0,
                    RandomArticulationRoller.ConcretePoolSize - 1),
                    "Rolled figures must be concrete Tier-1 members.");
            }
        }

        [Test]
        public void UniformDefault_ReachesMultipleFiguresIncludingBlockPool()
        {
            // D4=A: Block is in the default pool. Over 200 per-event rolls the
            // uniform pool must visit most figures; require at least 4 distinct
            // (extremely conservative for 200 uniform draws over 6 bins).
            var seq = Sequence(Roller(seed: 5), 200);
            Assert.That(seq.Distinct().Count(), Is.GreaterThanOrEqualTo(4));
        }

        // ---------------- Weights (SD-2=A) ----------------

        [Test]
        public void WeightTable_NullOrEmpty_IsUniformSix()
        {
            foreach (var input in new IReadOnlyList<ChordExpressionWeight>[]
                     { null, new List<ChordExpressionWeight>() })
            {
                var (figures, cumulative, total, usedFallback, hadEntries) =
                    RandomArticulationRoller.BuildWeightTable(input);
                Assert.That(figures.Length, Is.EqualTo(6));
                Assert.That(total, Is.EqualTo(6.0).Within(1e-9));
                Assert.That(hadEntries, Is.False);
                Assert.That(usedFallback, Is.True);
                Assert.That(cumulative.Last(), Is.EqualTo(total).Within(1e-9));
            }
        }

        [Test]
        public void WeightTable_EntriesDefineThePool_UnlistedExcluded()
        {
            var (figures, _, total, usedFallback, _) =
                RandomArticulationRoller.BuildWeightTable(new[]
                {
                    W(ChordExpressionType.Offbeat, 2f),
                    W(ChordExpressionType.ArpeggioUp, 1f),
                });
            Assert.That(figures, Is.EqualTo(new[]
            {
                ChordExpressionType.Offbeat, ChordExpressionType.ArpeggioUp
            }));
            Assert.That(total, Is.EqualTo(3.0).Within(1e-9));
            Assert.That(usedFallback, Is.False);
        }

        [Test]
        public void WeightTable_ZeroAndNegativeExclude_DuplicatesSum_RandomIgnored()
        {
            var (figures, cumulative, total, usedFallback, _) =
                RandomArticulationRoller.BuildWeightTable(new[]
                {
                    W(ChordExpressionType.Block, 0f),        // excluded
                    W(ChordExpressionType.Staccato, -1f),    // excluded
                    W(ChordExpressionType.PerBeat, 1f),
                    W(ChordExpressionType.PerBeat, 2f),      // sums to 3
                    W(ChordExpressionType.Random, 99f),      // ignored
                });
            Assert.That(figures, Is.EqualTo(new[] { ChordExpressionType.PerBeat }));
            Assert.That(total, Is.EqualTo(3.0).Within(1e-9));
            Assert.That(cumulative, Is.EqualTo(new[] { 3.0 }).Within(1e-9));
            Assert.That(usedFallback, Is.False);
        }

        [Test]
        public void WeightTable_DegenerateList_FallsBackToUniform()
        {
            var (figures, _, total, usedFallback, hadEntries) =
                RandomArticulationRoller.BuildWeightTable(new[]
                {
                    W(ChordExpressionType.Block, 0f),
                    W(ChordExpressionType.Random, 5f),
                });
            Assert.That(usedFallback, Is.True);
            Assert.That(hadEntries, Is.True);
            Assert.That(figures.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(6.0).Within(1e-9));
        }

        [Test]
        public void SingleFigureWeights_RollAlwaysThatFigure()
        {
            var roller = Roller(seed: 11, chance: 1f,
                weights: new[] { W(ChordExpressionType.Staccato, 1f) });
            var seq = Sequence(roller, 50);
            Assert.That(seq.Distinct().Single(),
                Is.EqualTo(ChordExpressionType.Staccato));
        }

        // ---------------- Articulator defensive degrade (D6) ----------------

        [Test]
        public void PlanHits_Random_DegradesToBlockPlan()
        {
            var block = ChordArticulator.PlanHits(
                ChordExpressionType.Block, ArpeggioRate.Eighth,
                startBeats: 0, durBeats: 4, beatsPerBar: 4,
                noteCount: 3, baseVelocity: 100);
            var random = ChordArticulator.PlanHits(
                ChordExpressionType.Random, ArpeggioRate.Eighth,
                startBeats: 0, durBeats: 4, beatsPerBar: 4,
                noteCount: 3, baseVelocity: 100);

            Assert.That(random.Count, Is.EqualTo(block.Count));
            for (int i = 0; i < block.Count; i++)
            {
                Assert.That(random[i].StartBeats,
                    Is.EqualTo(block[i].StartBeats).Within(1e-9));
                Assert.That(random[i].DurBeats,
                    Is.EqualTo(block[i].DurBeats).Within(1e-9));
                Assert.That(random[i].Velocity, Is.EqualTo(block[i].Velocity));
                Assert.That(random[i].NoteIndex, Is.EqualTo(block[i].NoteIndex));
            }
        }
    }
}
#endif
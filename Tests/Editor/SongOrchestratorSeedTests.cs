#if UNITY_EDITOR
// MGP-ALWTTT-SEED-1 — EditMode tests for the per-render seed surface.
//
// Targets the internal seed-derivation seams on SongOrchestrator (ResolveBaseSeed /
// ResolveRepContextSeed / ResolvePartContextSeed / ResolveTrackSeedSong /
// ResolveTrackSeedPart / StableHash32), the same internal-seam idiom as
// MelodyTrackComposer_PatternDeterminismTests — no SongConfig / asset-DB fixtures.
// Internal visibility via Runtime/AssemblyInfo.cs:
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]
//
// Covers:
//  - StableHash32 golden values (FNV-1a 32-bit; algorithm regression guard)
//  - Track-seed string format bit-identity with the pre-batch inline expressions
//    (golden ints captured from the pre-batch format: "{seed}|p=..|r=..|m=.." and
//    "{seed}|p=..|rep=..|r=..|m=..")
//  - Null seedOverride == explicit defaultSeed (backward-compat equivalence)
//  - Rep/part context-seed arithmetic incl. the original operator precedence
//    ((base + idx*397) ^ rep)
//  - Different supplied seeds => different track seeds
//  - End-to-end pick variance: threading distinct base seeds through
//    ResolveTrackSeedPart into PaletteSelector.Pick yields >= 2 distinct picks
//    over a 6-entry palette, while the same seed always re-picks the same entry
//    (in-package mirror of the ALWTTT S5g acceptance).

using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    public class SongOrchestratorSeedTests
    {
        // Golden FNV-1a 32-bit values, computed independently from the batch
        // implementation against the PRE-BATCH seed-string formats. If any of
        // these fail, the hash algorithm or the seed-string format changed and
        // backward-compat bit-identity is broken.
        private const int GoldenPart_0_Rhythm_Drummer   = -215185059;  // "0|p=0|r=Rhythm|m=drummer"
        private const int GoldenSong_0_Rep0_Rhythm      = 580835759;   // "0|p=0|rep=0|r=Rhythm|m=drummer"
        private const int GoldenPart_12345_2_Backing    = 1622533202;  // "12345|p=2|r=Backing|m=bass"

        // ---------------- Hash algorithm ----------------

        [Test]
        public void StableHash32_MatchesGoldenValues()
        {
            Assert.That(SongOrchestrator.StableHash32("0|p=0|r=Rhythm|m=drummer"),
                Is.EqualTo(GoldenPart_0_Rhythm_Drummer));
            Assert.That(SongOrchestrator.StableHash32("0|p=0|rep=0|r=Rhythm|m=drummer"),
                Is.EqualTo(GoldenSong_0_Rep0_Rhythm));
            Assert.That(SongOrchestrator.StableHash32("12345|p=2|r=Backing|m=bass"),
                Is.EqualTo(GoldenPart_12345_2_Backing));
        }

        // ---------------- Track-seed format bit-identity ----------------

        [Test]
        public void ResolveTrackSeedPart_IsBitIdenticalToLegacyFormat()
        {
            Assert.That(
                SongOrchestrator.ResolveTrackSeedPart(0, 0, TrackRole.Rhythm, "drummer"),
                Is.EqualTo(GoldenPart_0_Rhythm_Drummer));
            Assert.That(
                SongOrchestrator.ResolveTrackSeedPart(12345, 2, TrackRole.Backing, "bass"),
                Is.EqualTo(GoldenPart_12345_2_Backing));
        }

        [Test]
        public void ResolveTrackSeedSong_IsBitIdenticalToLegacyFormat()
        {
            Assert.That(
                SongOrchestrator.ResolveTrackSeedSong(0, 0, 0, TrackRole.Rhythm, "drummer"),
                Is.EqualTo(GoldenSong_0_Rep0_Rhythm));
        }

        // ---------------- Base-seed resolution (backward compat) ----------------

        [Test]
        public void ResolveBaseSeed_NullOverride_FallsBackToDefaultSeed()
        {
            Assert.That(SongOrchestrator.ResolveBaseSeed(null, 42), Is.EqualTo(42));
            Assert.That(SongOrchestrator.ResolveBaseSeed(7, 42), Is.EqualTo(7));
        }

        [Test]
        public void NullOverride_YieldsSameDerivedSeeds_AsExplicitDefaultSeed()
        {
            const int defaultSeed = 0; // MidiGenPlayConfig's shipped value

            int viaNull = SongOrchestrator.ResolveBaseSeed(null, defaultSeed);
            int viaExplicit = SongOrchestrator.ResolveBaseSeed(defaultSeed, defaultSeed);

            Assert.That(viaNull, Is.EqualTo(viaExplicit));
            Assert.That(
                SongOrchestrator.ResolveTrackSeedPart(viaNull, 3, TrackRole.Melody, "lead"),
                Is.EqualTo(SongOrchestrator.ResolveTrackSeedPart(viaExplicit, 3, TrackRole.Melody, "lead")));
            Assert.That(
                SongOrchestrator.ResolveRepContextSeed(viaNull, 1, 2),
                Is.EqualTo(SongOrchestrator.ResolveRepContextSeed(viaExplicit, 1, 2)));
        }

        // ---------------- Context-seed arithmetic ----------------

        [Test]
        public void RepContextSeed_PreservesOriginalOperatorPrecedence()
        {
            // Pre-batch inline: defaultSeed + PartIndex * 397 ^ rep
            // == ((defaultSeed + PartIndex * 397) ^ rep) in C#.
            Assert.That(SongOrchestrator.ResolveRepContextSeed(10, 3, 5),
                Is.EqualTo((10 + 3 * 397) ^ 5));
            Assert.That(SongOrchestrator.ResolvePartContextSeed(10, 3),
                Is.EqualTo(10 + 3 * 397));
        }

        // ---------------- Seed variance ----------------

        [Test]
        public void DifferentBaseSeeds_ProduceDifferentTrackSeeds()
        {
            var seeds = new HashSet<int>();
            for (int baseSeed = 0; baseSeed < 10; baseSeed++)
                seeds.Add(SongOrchestrator.ResolveTrackSeedPart(
                    baseSeed, 0, TrackRole.Backing, "keys"));

            Assert.That(seeds.Count, Is.EqualTo(10),
                "distinct base seeds must produce distinct per-track seeds");
        }

        // ---------------- End-to-end: seed threading changes the palette pick ----------------

        private static Candidate<string> C(string id) =>
            new Candidate<string>(id, 1f,
                new TsFeatures(TimeSignature.FourFour, 4, 2f));

        [Test]
        public void ThreadedSeeds_YieldDistinctPaletteSelectorPicks_AndAreRepeatable()
        {
            // 6 equal-weight, same-TS candidates: the ALWTTT scenario (palette >= 6)
            // reduced to the selector level. All Tier-B multipliers are identical,
            // so this is a uniform roulette — pick identity is decided solely by
            // the single NextDouble of the seeded RNG.
            var cands = new List<Candidate<string>>
            {
                C("one"), C("two"), C("three"), C("four"), C("five"), C("six"),
            };

            string PickWithBaseSeed(int baseSeed)
            {
                int trackSeed = SongOrchestrator.ResolveTrackSeedPart(
                    baseSeed, 0, TrackRole.Backing, "keys");
                return PaletteSelector.Pick(
                    cands, TimeSignature.FourFour, preferExactTs: false,
                    minHarmonicSubdivisions: 4, rng: new System.Random(trackSeed));
            }

            var picks = Enumerable.Range(0, 10).Select(PickWithBaseSeed).ToList();

            Assert.That(picks.Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                "distinct supplied seeds must be able to reach distinct palette entries");

            // Repeatability: the same supplied seed always re-picks the same entry
            // (the property ALWTTT relies on for mid-song re-renders).
            for (int baseSeed = 0; baseSeed < 10; baseSeed++)
                Assert.That(PickWithBaseSeed(baseSeed), Is.EqualTo(picks[baseSeed]));
        }

        // ---------------- BPM-DET-1: tempo seed + seeded roll ----------------

        [Test]
        public void ResolveTempoSeed_MatchesStringFormat_AndOmitsRep()
        {
            // Format guard (algorithm itself is guarded by StableHash32_MatchesGoldenValues).
            Assert.That(SongOrchestrator.ResolveTempoSeed(0, 0),
                Is.EqualTo(SongOrchestrator.StableHash32("0|p=0|tempo")));
            Assert.That(SongOrchestrator.ResolveTempoSeed(12345, 2),
                Is.EqualTo(SongOrchestrator.StableHash32("12345|p=2|tempo")));
            // rep intentionally absent (D-BPM2-KEY=A): tempo is per part-occurrence.
        }

        [Test]
        public void RollTempoBpm_IsDeterministicForSameSeed_AndOnGridInBand()
        {
            int seed = SongOrchestrator.ResolveTempoSeed(42, 1);
            int a = SongOrchestrator.RollTempoBpm(seed, TempoRange.Moderate, TempoRule.MultiplesOfTen);
            int b = SongOrchestrator.RollTempoBpm(seed, TempoRange.Moderate, TempoRule.MultiplesOfTen);
            Assert.That(a, Is.EqualTo(b), "same tempo seed must reproduce the same BPM");
            Assert.That(a % 10, Is.EqualTo(0));
            Assert.That(a, Is.InRange(121, 160)); // Moderate band
        }

        [Test]
        public void RollTempoBpm_DifferentSeeds_CanReachDifferentBpms()
        {
            var bpms = new HashSet<int>();
            for (int baseSeed = 0; baseSeed < 20; baseSeed++)
                bpms.Add(SongOrchestrator.RollTempoBpm(
                    SongOrchestrator.ResolveTempoSeed(baseSeed, 0),
                    TempoRange.Moderate, TempoRule.MultiplesOfTen));
            Assert.That(bpms.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
#endif

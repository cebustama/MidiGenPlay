#if UNITY_EDITOR
// MGP-ALWTTT-BASS-SOLO-1 — EditMode tests for the host-default-progression seam.
//
// Targets the pure seam SongOrchestrator.TrySeedDefaultProgression (same
// seam idiom as SongOrchestratorSeedTests.ResolveTrackSeedPart — no
// full-render fixture; end-to-end behavior is exercised by the Composition
// Smoke gates). The seam is PUBLIC, matching the house pattern of the other
// pure test seams in this codebase.
//
// Covers (decision surface of the batch):
//  - D-SOLO-SURF=A2 seeding: backing-less part => Seeded, cache holds the
//    default (clone-on-seed: distinct instance, same name, same event content).
//  - D-SOLO-GUARD=A: part WITH a Backing track => warn + ignore, cache
//    untouched (the fork hazard the guard exists to prevent).
//  - Null default => NotSupplied, cache untouched (byte-identity guard: the
//    legacy path executes no seeding code).
//  - Robustness: null tracks list / null track entries don't break the guard.
//  - D-SOLO-DET is structural: the seam signature takes no System.Random and
//    the method body performs a single dictionary write — asserted here only
//    indirectly (no draw source exists to perturb), pinned end-to-end by the
//    smoke gate "seeded default ≡ same asset via the bass row's Pattern slot".

using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class SongOrchestrator_DefaultProgressionTests
    {
        // ---------------- Fixtures ----------------

        private static ChordProgressionData MakeProgression(string name = "SoloDefault")
        {
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.name = name;
            prog.Measures = 4;
            prog.subdivisions = 1;
            prog.TimeSignature = TimeSignature.FourFour;
            prog.events = new List<ChordProgressionData.ChordEvent>
            {
                new ChordProgressionData.ChordEvent
                {
                    startStep = 0, lengthSteps = 8,
                    degree = ScaleDegree.Tonic, quality = ChordQuality.Major,
                    velocity = 90,
                },
                new ChordProgressionData.ChordEvent
                {
                    startStep = 8, lengthSteps = 8,
                    degree = ScaleDegree.Dominant, quality = ChordQuality.Major,
                    velocity = 90,
                },
            };
            return prog;
        }

        private static SongConfig.PartConfig MakePart(params TrackRole[] roles)
        {
            var part = new SongConfig.PartConfig
            {
                Name = "SoloPart",
                Tracks = new List<SongConfig.PartConfig.TrackConfig>(),
                TimeSignature = TimeSignature.FourFour,
                Measures = 4,
            };
            foreach (var r in roles)
                part.Tracks.Add(new SongConfig.PartConfig.TrackConfig { Role = r });
            return part;
        }

        // ---------------- Null default => NotSupplied ----------------

        [Test]
        public void NullDefault_ReturnsNotSupplied_AndLeavesCacheUntouched()
        {
            var part = MakePart(TrackRole.Bassline);
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var result = SongOrchestrator.TrySeedDefaultProgression(part, null, cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.NotSupplied));
            Assert.That(cache, Is.Empty);
        }

        // ---------------- Backing-less part => Seeded ----------------

        [Test]
        public void BackinglessPart_SeedsCache_WithNamePreservingClone()
        {
            var part = MakePart(TrackRole.Rhythm, TrackRole.Bassline);
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();
            var prog = MakeProgression("MyJamDefault");

            var result = SongOrchestrator.TrySeedDefaultProgression(part, prog, cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.Seeded));
            Assert.That(cache.ContainsKey(part), Is.True);

            var seeded = cache[part];
            Assert.That(seeded, Is.Not.Null);
            // Clone-on-seed: decoupled instance, readback-preserving name.
            Assert.That(ReferenceEquals(seeded, prog), Is.False,
                "Seeded progression must be a clone, not the asset instance.");
            Assert.That(seeded.name, Is.EqualTo("MyJamDefault"),
                "Clone must keep the source asset's name (no '(Clone)').");
            // Content equality of the harmonic payload.
            Assert.That(seeded.events.Count, Is.EqualTo(prog.events.Count));
            for (int i = 0; i < prog.events.Count; i++)
            {
                Assert.That(seeded.events[i].degree, Is.EqualTo(prog.events[i].degree));
                Assert.That(seeded.events[i].quality, Is.EqualTo(prog.events[i].quality));
                Assert.That(seeded.events[i].startStep, Is.EqualTo(prog.events[i].startStep));
                Assert.That(seeded.events[i].lengthSteps, Is.EqualTo(prog.events[i].lengthSteps));
            }
        }

        [Test]
        public void MelodyOnlyPart_AlsoSeeds_SharedChannelIsRoleAgnostic()
        {
            // The seam is not bass-specific: any backing-less part with harmony
            // consumers benefits (melody reads GetProgressionForPart too).
            var part = MakePart(TrackRole.Melody);
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part, MakeProgression(), cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.Seeded));
            Assert.That(cache.ContainsKey(part), Is.True);
        }

        // ---------------- D-SOLO-GUARD=A: Backing present => warn + ignore ----

        [Test]
        public void BackingPresent_WarnsAndIgnores_CacheUntouched()
        {
            var part = MakePart(TrackRole.Backing, TrackRole.Bassline);
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();
            var prog = MakeProgression();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "defaultProgression.*Backing track.*Ignoring"));

            var result = SongOrchestrator.TrySeedDefaultProgression(part, prog, cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.IgnoredBackingPresent));
            Assert.That(cache, Is.Empty,
                "Seeding under a Backing track would fork the backing render " +
                "from the shared channel (card-palette publish is guarded by " +
                "don't-overwrite) — the guard must leave the cache untouched.");
        }

        // ---------------- Robustness ----------------

        [Test]
        public void NullTracksList_TreatedAsBackingless_Seeds()
        {
            var part = new SongConfig.PartConfig { Name = "NoTracks", Tracks = null };
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part, MakeProgression(), cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.Seeded));
        }

        [Test]
        public void NullTrackEntry_DoesNotThrow_GuardStillDetectsBacking()
        {
            var part = MakePart(TrackRole.Bassline);
            part.Tracks.Add(null); // hole in the list
            part.Tracks.Add(new SongConfig.PartConfig.TrackConfig
            {
                Role = TrackRole.Backing
            });
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "defaultProgression.*Backing track.*Ignoring"));

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part, MakeProgression(), cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.IgnoredBackingPresent));
            Assert.That(cache, Is.Empty);
        }
    }
}
#endif
#if UNITY_EDITOR
// MGP-TRIAGE-ALWTTT-R3 — E3, clone identity on the shared progression channel.
//
// ALWTTT consumed PartRender.sharedProgressionData and found its Unity object
// `name` EMPTY, then fed that clone back as a per-render override and got the
// package logging an empty name at itself ("Per-render progression override
// used: ''"). The reported asset name (sharedProgressionAssetName) was correct
// throughout — the two are different things and only the clone's `.name` was
// broken.
//
// Root cause: NormalizeProgressionForPartIfNeeded builds its reprojected clone
// field-by-field from CreateInstance, which leaves `.name` empty, and that
// clone is what gets published to the shared cache. It is NOT source-specific:
// the host saw it on CardPalette only because that is the render that
// normalized. Authoring writes subdivisions x1 and the composer wants x4, so
// normalization fires on nearly every render regardless of which source won.
//
// D-MGPT-3b also closed the remaining Instantiate sites, whose clones carried
// a "(Clone)" suffix instead of the asset name — same identity defect, smaller
// blast radius.
//
// Contract pinned here: on EVERY precedence step, the published runtime clone
// is a distinct object from the asset AND carries the pre-clone asset name, so
// `sharedProgressionData.name == sharedProgressionAssetName`.
//
// Fixtures: Dbg1Fixtures (same assembly; SongOrchestratorKeyingTests.cs).

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordProgression_CloneIdentityTests
    {
        private const string AssetName = "Prog_Identity_Fixture";

        /// <summary>4/4, 1 measure, subdivisions = 1 — the shape the authoring
        /// tools actually emit. With minHarmonicSubdivisions = 4 this GUARANTEES
        /// the TS/subdivision reprojection runs, which is the code path that
        /// dropped the name.</summary>
        private static ChordProgressionData LowSubProgression(
            string assetName = AssetName)
        {
            var p = ScriptableObject.CreateInstance<ChordProgressionData>();
            p.name = assetName;
            p.DisplayName = assetName;
            p.TimeSignature = TimeSignature.FourFour;
            p.Measures = 1;
            p.subdivisions = 1;
            p.events = new List<ChordProgressionData.ChordEvent>
            {
                new ChordProgressionData.ChordEvent
                {
                    degree = ScaleDegree.Tonic, quality = ChordQuality.Major,
                    degreeAccidental = 0, startStep = 0, lengthSteps = 2,
                    velocity = 96,
                },
                new ChordProgressionData.ChordEvent
                {
                    degree = ScaleDegree.Dominant, quality = ChordQuality.Major,
                    degreeAccidental = 0, startStep = 2, lengthSteps = 2,
                    velocity = 96,
                },
            };
            return p;
        }

        private static MidiGenPlayConfig NormalizingSettings()
        {
            var s = Dbg1Fixtures.Settings();
            s.minHarmonicSubdivisions = 4;
            return s;
        }

        private static BackingCardConfigSO CardWithOverride(
            ChordProgressionData prog)
        {
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = "IdentityOverrideCard";
            c.progressionOverride = prog;
            return c;
        }

        private static BackingCardConfigSO CardWithPalette(
            ChordProgressionData prog)
        {
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = "IdentityPaletteCard";
            var pal = ScriptableObject.CreateInstance<ChordProgressionPaletteSO>();
            pal.name = "IdentityPalette";
            pal.entries = new List<ChordProgressionPaletteSO.WeightedEntry>
            {
                new ChordProgressionPaletteSO.WeightedEntry
                {
                    progression = prog, weight = 1f,
                },
            };
            c.progressionPalette = pal;
            return c;
        }

        /// <summary>The whole contract, asserted once and reused per source.</summary>
        private static void AssertPublishedCloneIdentity(
            PartRender render,
            ChordProgressionData sourceAsset,
            ResolvedSource expectedSource)
        {
            Assert.That(render.sharedProgressionSource, Is.EqualTo(expectedSource),
                "the render resolved harmony through a different precedence " +
                "step than this test intends to cover");

            Assert.That(render.sharedProgressionData, Is.Not.Null,
                "a resolved shared progression must be published (P7)");

            Assert.That(
                ReferenceEquals(render.sharedProgressionData, sourceAsset),
                Is.False,
                "the published progression must be a runtime clone, never the " +
                "asset reference");

            Assert.That(render.sharedProgressionData.subdivisions,
                Is.GreaterThan(sourceAsset.subdivisions),
                "guard: if reprojection did not run, this test would pass " +
                "vacuously without exercising the site that dropped the name");

            Assert.That(render.sharedProgressionData.name, Is.EqualTo(AssetName),
                "E3: the runtime clone must carry the pre-clone asset name — " +
                "not empty (CreateInstance) and not \"(Clone)\" (Instantiate)");

            Assert.That(render.sharedProgressionAssetName, Is.EqualTo(AssetName));
            Assert.That(render.sharedProgressionData.name,
                Is.EqualTo(render.sharedProgressionAssetName),
                "clone identity and reported asset name must agree");
        }

        // ------------------------------------------------------------------
        // One test per precedence step
        // ------------------------------------------------------------------

        [Test]
        public void AuthoredBackingPattern_PublishedClone_KeepsAssetName()
        {
            // Reports SharedProgression, NOT TrackParameters — and that is the
            // shipped behaviour, not a fixture accident. ChordTrackComposer
            // step 2 asks ctx.GetProgressionForPart FIRST, and the orchestrator
            // wires that delegate with an authored fallback
            // (SongOrchestrator.FindProgressionForPart) that returns the FIRST
            // Backing track's Parameters.Pattern. So a Backing track carrying
            // its own authored progression is always served by the cache
            // delegate and never reaches the `else if (cfg.Parameters?.Pattern
            // is ChordProgressionData)` branch below it.
            //
            // Consequence worth knowing: ResolvedSource.TrackParameters is
            // effectively unreachable for Backing through the orchestrator. It
            // survives for a second Backing track in the same part (the
            // fallback returns only the first one's Pattern) and for composers
            // driven with a ctx whose GetProgressionForPart is null.
            //
            // Either way the clone-identity contract is what this test pins,
            // and it holds: the published object is a normalized clone of the
            // authored asset and carries the asset's name.
            var prog = LowSubProgression();
            var orch = Dbg1Fixtures.Orchestrator(NormalizingSettings());
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing,
                    Dbg1Fixtures.Instrument(), pattern: prog));

            AssertPublishedCloneIdentity(
                Dbg1Fixtures.Render(orch, part), prog,
                ResolvedSource.SharedProgression);
        }

        [Test]
        public void CardOverride_PublishedClone_KeepsAssetName()
        {
            var prog = LowSubProgression();
            var orch = Dbg1Fixtures.Orchestrator(NormalizingSettings());
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing,
                    Dbg1Fixtures.Instrument(), style: CardWithOverride(prog)));

            AssertPublishedCloneIdentity(
                Dbg1Fixtures.Render(orch, part), prog,
                ResolvedSource.CardOverride);
        }

        [Test]
        public void CardPalette_PublishedClone_KeepsAssetName()
        {
            // The path ALWTTT actually observed empty.
            var prog = LowSubProgression();
            var orch = Dbg1Fixtures.Orchestrator(NormalizingSettings());
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing,
                    Dbg1Fixtures.Instrument(), style: CardWithPalette(prog)));

            AssertPublishedCloneIdentity(
                Dbg1Fixtures.Render(orch, part), prog,
                ResolvedSource.CardPalette);
        }

        [Test]
        public void RenderOverride_PublishedClone_KeepsAssetName()
        {
            // Closes the JAM-1 loop: the host imposes a previously published
            // clone as a per-render override, so a nameless clone here would
            // propagate its own blindness into the next render.
            var prog = LowSubProgression();
            var orch = Dbg1Fixtures.Orchestrator(NormalizingSettings());
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, Dbg1Fixtures.Instrument()));

            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Backing)]
                    = prog,
            };

            AssertPublishedCloneIdentity(
                Dbg1Fixtures.Render(orch, part, overrides), prog,
                ResolvedSource.RenderOverride);
        }

        // ------------------------------------------------------------------
        // Round trip — the actual host workflow
        // ------------------------------------------------------------------

        [Test]
        public void ImposingAPublishedClone_PreservesNameAcrossRenders()
        {
            var prog = LowSubProgression();
            var settings = NormalizingSettings();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var first = Dbg1Fixtures.Render(orch, Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing,
                    Dbg1Fixtures.Instrument(), style: CardWithPalette(prog))));

            var carried = first.sharedProgressionData;
            Assert.That(carried.name, Is.EqualTo(AssetName));

            var second = Dbg1Fixtures.Render(orch, Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Backing,
                        Dbg1Fixtures.Instrument())),
                new Dictionary<MusicianTrackKey, PatternDataSO>
                {
                    [new MusicianTrackKey(
                        Dbg1Fixtures.Musician, TrackRole.Backing)] = carried,
                });

            Assert.That(second.sharedProgressionAssetName, Is.EqualTo(AssetName),
                "the carried clone must still identify itself on the next render");
            Assert.That(second.sharedProgressionData.name, Is.EqualTo(AssetName));
        }
    }
}
#endif
#if UNITY_EDITOR
// MGP-ALWTTT-BASS-ORDER-1 — order-independent shared-harmony resolution.
//
// Closes F-BASS-ORDER-1 (ALWTTT gig report, 2026-07-30): with the Bassline
// track BEFORE the Backing track in the list, the bass composed first, the
// shared cache was empty, FindProgressionForPart returned null (card harmony
// lives in the Style bundle, not in Pattern) and the part rendered permanent
// bass silence. The fix is scheduling (D-ORD-MECH=A): Backing composes in a
// dedicated PASS 0 while the byte layout is preserved by a deferred
// track-list-index-ordered merge.
//
// Three layers:
//
// 1) PURE SEAMS:
//    - BackingTrackCarriesHarmonySource (D-ORD-GUARD=A): the STATIC sniff —
//      per-render override / card progressionOverride / valid palette entry /
//      authored Pattern; articulation-only card => false.
//    - TrySeedDefaultProgression (4-param ORDER-1 overload): sniff-guarded
//      seeding + the new SeededBackingArticulationOnly result. The legacy
//      3-param overload keeps the ORIGINAL binary guard verbatim
//      (SongOrchestrator_DefaultProgressionTests stays green untouched — that
//      suite IS the BC pin for the old seam).
//    - StampSharedProgressionReadback (D-ORD-RB): the mapping table onto
//      PartRender.sharedProgressionSource (incl. ResolvedSource.HostDefault).
//
// 2) RENDER GATES (Dbg1Fixtures + FNV idiom):
//    - F-BASS-ORDER-1 REGRESSION: [Bassline, Backing(card override)] renders
//      a NON-EMPTY bass stem with the card's harmony — same note content as
//      the [Backing, Bassline] order (channels differ by list position, so
//      the comparison is note times+pitches on the bass stem, not bytes).
//    - CHUNK-ORDER PIN (the BC layout argument): merged chunk sequence
//      follows the track LIST, not compose order — the "mus:" tags appear in
//      list order even when Backing composed first.
//    - GUARD RESURGE (criterion 2): articulation-only Backing + host default
//      => the default drives every consumer; sharedProgressionSource ==
//      HostDefault.
//    - CARD WINS: Backing with a card source + host default => warn+ignore,
//      sharedProgressionSource == the card source.
//    - DETERMINISM: same seed + config => same bytes, in the hazard order.
//
// 3) READBACK GATES: sharedProgressionSource for the backing-less seeded
//    (HostDefault) and private-Pattern (None) cases.
//
// Decisions covered: D-ORD-MECH=A, D-ORD-GUARD=A, D-ORD-SCOPE=A, D-ORD-RB.

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class SongOrchestrator_HarmonyOrderTests
    {
        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static BackingCardConfigSO ArticulationOnlyCard(
            ChordExpressionType expr = ChordExpressionType.Offbeat)
        {
            // No progressionOverride, no palette: the "future bossa/ska/power
            // chords" shape of the ask — harmony-free by construction.
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = "ArticOnlyCard";
            c.chordExpression = expr;
            return c;
        }

        private static BackingCardConfigSO CardWithOverride(
            ChordProgressionData prog)
        {
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = "OverrideCard";
            c.progressionOverride = prog;
            return c;
        }

        private static BackingCardConfigSO CardWithPalette(
            params (ChordProgressionData prog, float weight)[] entries)
        {
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = "PaletteCard";
            var pal = ScriptableObject.CreateInstance<ChordProgressionPaletteSO>();
            pal.name = "TestPalette";
            pal.entries = new List<ChordProgressionPaletteSO.WeightedEntry>();
            foreach (var (prog, weight) in entries)
            {
                pal.entries.Add(new ChordProgressionPaletteSO.WeightedEntry
                {
                    progression = prog,
                    weight = weight,
                });
            }
            c.progressionPalette = pal;
            return c;
        }

        private static PartRender RenderWithDefault(
            SongOrchestrator orch,
            SongConfig.PartConfig part,
            ChordProgressionData defaultProgression,
            int seed = 7)
        {
            var roles = part.Tracks.Select(t => t.Role).ToList();
            return orch.GenerateSinglePart(
                part, roles, partIndex: 0, bpmOverride: 120,
                instrumentOverrides: null, seedOverride: seed,
                patternOverrides: null, mixGains: null,
                defaultProgression: defaultProgression);
        }

        private static ResolvedTrackChoice Readback(
            PartRender render, TrackRole role)
        {
            render.resolvedByTrack.TryGetValue(
                new MusicianTrackKey(Dbg1Fixtures.Musician, role), out var rc);
            return rc;
        }

        /// <summary>(time, pitch) sequence of a stem — the channel-agnostic
        /// note content used to compare across track-list orders (channel
        /// allocation follows list position, so bytes legitimately differ).</summary>
        private static List<(long time, int note)> StemNotes(
            PartRender render, TrackRole role)
        {
            render.stemsByMusician.TryGetValue(
                new MusicianTrackKey(Dbg1Fixtures.Musician, role), out var stem);
            if (stem == null) return new List<(long, int)>();
            return stem.GetNotes()
                .OrderBy(n => n.Time).ThenBy(n => n.NoteNumber)
                .Select(n => (n.Time, (int)n.NoteNumber))
                .ToList();
        }

        // ------------------------------------------------------------------
        // 1) Pure seam — BackingTrackCarriesHarmonySource (D-ORD-GUARD=A)
        // ------------------------------------------------------------------

        [Test]
        public void Sniff_NullConfig_False()
        {
            Assert.IsFalse(SongOrchestrator.BackingTrackCarriesHarmonySource(
                null, null));
        }

        [Test]
        public void Sniff_ArticulationOnlyCard_False()
        {
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(), style: ArticulationOnlyCard());
            Assert.IsFalse(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, null),
                "a bundle with no override/palette/Pattern carries no harmony");
        }

        [Test]
        public void Sniff_CardProgressionOverride_True()
        {
            var prog = Dbg1Fixtures.Progression("SniffProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(), style: CardWithOverride(prog));
            Assert.IsTrue(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, null));
        }

        [Test]
        public void Sniff_PaletteWithValidEntry_True()
        {
            var prog = Dbg1Fixtures.Progression("SniffPalProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(),
                style: CardWithPalette((prog, 1f)));
            Assert.IsTrue(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, null));
        }

        [Test]
        public void Sniff_PaletteWithOnlyInvalidEntries_False()
        {
            // Mirrors PickRandomProgression's valid filter exactly:
            // null progression and weight <= 0 entries never pick.
            var prog = Dbg1Fixtures.Progression("ZeroWeight",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(),
                style: CardWithPalette((null, 1f), (prog, 0f)));
            Assert.IsFalse(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, null));
        }

        [Test]
        public void Sniff_AuthoredPattern_True()
        {
            var prog = Dbg1Fixtures.Progression("AuthoredProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(), pattern: prog,
                style: ArticulationOnlyCard());
            Assert.IsTrue(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, null));
        }

        [Test]
        public void Sniff_RenderOverride_ProgressionTypeOnly()
        {
            var cfg = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(), style: ArticulationOnlyCard());

            var prog = Dbg1Fixtures.Progression("OvrProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            Assert.IsTrue(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, prog),
                "a per-render ChordProgressionData override is precedence " +
                "step 0 — max authority");

            var drums = Dbg1Fixtures.DrumPattern("WrongType");
            Assert.IsFalse(SongOrchestrator.BackingTrackCarriesHarmonySource(
                cfg, drums),
                "a type-mismatched override is warn+ignore at the composer — " +
                "it is NOT a harmony source");
        }

        // ------------------------------------------------------------------
        // 1) Pure seam — TrySeedDefaultProgression (ORDER-1 overload)
        // ------------------------------------------------------------------

        [Test]
        public void Seed4_BackingWithHarmonySource_WarnsAndIgnores()
        {
            var cardProg = Dbg1Fixtures.Progression("CardProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, Dbg1Fixtures.Instrument(),
                    style: CardWithOverride(cardProg)),
                Dbg1Fixtures.Track(TrackRole.Bassline, Dbg1Fixtures.Instrument()));
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "defaultProgression.*Backing track.*Ignoring"));

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part,
                Dbg1Fixtures.Progression("HostDefault",
                    (ScaleDegree.Subdominant, ChordQuality.Major)),
                cache,
                patternOverrides: null);

            Assert.That(result, Is.EqualTo(
                SongOrchestrator.DefaultProgressionSeedResult.IgnoredBackingPresent));
            Assert.That(cache, Is.Empty);
        }

        [Test]
        public void Seed4_ArticulationOnlyBacking_Seeds()
        {
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, Dbg1Fixtures.Instrument()),
                Dbg1Fixtures.Track(TrackRole.Backing, Dbg1Fixtures.Instrument(),
                    style: ArticulationOnlyCard()));
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part,
                Dbg1Fixtures.Progression("HostDefault",
                    (ScaleDegree.Tonic, ChordQuality.Major)),
                cache,
                patternOverrides: null);

            Assert.That(result, Is.EqualTo(SongOrchestrator
                .DefaultProgressionSeedResult.SeededBackingArticulationOnly),
                "criterion 2 of the ask: a solo-articulation Backing must NOT " +
                "displace the host default");
            Assert.That(cache.ContainsKey(part), Is.True);
            Assert.That(cache[part].name, Is.EqualTo("HostDefault"));
        }

        [Test]
        public void Seed4_NoBacking_SeedsAsBefore()
        {
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, Dbg1Fixtures.Instrument()));
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part,
                Dbg1Fixtures.Progression("HostDefault",
                    (ScaleDegree.Tonic, ChordQuality.Major)),
                cache,
                patternOverrides: null);

            Assert.That(result, Is.EqualTo(
                SongOrchestrator.DefaultProgressionSeedResult.Seeded));
            Assert.That(cache.ContainsKey(part), Is.True);
        }

        [Test]
        public void Seed4_RenderOverrideOnBacking_CountsAsHarmonySource()
        {
            var artic = Dbg1Fixtures.Track(TrackRole.Backing,
                Dbg1Fixtures.Instrument(), style: ArticulationOnlyCard());
            var part = Dbg1Fixtures.Part(
                artic,
                Dbg1Fixtures.Track(TrackRole.Bassline, Dbg1Fixtures.Instrument()));
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Backing)] =
                    Dbg1Fixtures.Progression("Step0",
                        (ScaleDegree.Dominant, ChordQuality.Major)),
            };

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "defaultProgression.*Backing track.*Ignoring"));

            var result = SongOrchestrator.TrySeedDefaultProgression(
                part,
                Dbg1Fixtures.Progression("HostDefault",
                    (ScaleDegree.Tonic, ChordQuality.Major)),
                cache, overrides);

            Assert.That(result, Is.EqualTo(
                SongOrchestrator.DefaultProgressionSeedResult.IgnoredBackingPresent));
            Assert.That(cache, Is.Empty);
        }

        // ------------------------------------------------------------------
        // 2) Render gates
        // ------------------------------------------------------------------

        [Test]
        public void OrderHazard_BasslineBeforeBacking_BassRendersCardHarmony()
        {
            // F-BASS-ORDER-1 REGRESSION — the exact gig failure. Pre-ORDER-1
            // this rendered `Trimmed [Bassline] notes=0` (permanent silence);
            // with PASS 0 the Backing publishes before the bass composes.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var cardProg = Dbg1Fixtures.Progression("GigProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            PartRender Render(bool bassFirst)
            {
                var backing = Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    style: CardWithOverride(cardProg));
                var bass = Dbg1Fixtures.Track(TrackRole.Bassline, inst);
                var part = bassFirst
                    ? Dbg1Fixtures.Part(bass, backing)
                    : Dbg1Fixtures.Part(backing, bass);
                return RenderWithDefault(orch, part, null, seed: 7);
            }

            var hazard = Render(bassFirst: true);
            var golden = Render(bassFirst: false);

            var hazardBass = StemNotes(hazard, TrackRole.Bassline);
            Assert.That(hazardBass, Is.Not.Empty,
                "the bass must render — the shared harmony exists (card " +
                "override) whatever the track-list order (criterion 1)");

            // Note content is order-independent (channels/bytes follow list
            // position, so the comparison is time+pitch on the bass stem).
            Assert.That(hazardBass,
                Is.EqualTo(StemNotes(golden, TrackRole.Bassline)),
                "same seed, same config => the bass line must not depend on " +
                "the track-list order");

            // Both consumers saw the SAME resolved progression.
            Assert.That(Readback(hazard, TrackRole.Bassline).progressionRoman,
                Is.EqualTo(Readback(hazard, TrackRole.Backing).progressionRoman),
                "single resolution per render (criterion 3)");
            Assert.That(hazard.sharedProgressionSource,
                Is.EqualTo(ResolvedSource.CardOverride));
            Assert.That(hazard.sharedProgressionAssetName, Is.EqualTo("GigProg"));
        }

        [Test]
        public void ChunkOrder_FollowsTrackList_NotComposeOrder()
        {
            // The BC layout pin of D-ORD-MECH=A: Backing composes FIRST
            // (PASS 0) but the deferred merge keeps the chunk sequence in
            // track-LIST order — here Bassline's tagged chunk precedes
            // Backing's, matching the pre-ORDER-1 byte layout rule.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var cardProg = Dbg1Fixtures.Progression("LayoutProg",
                (ScaleDegree.Tonic, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst),
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    style: CardWithOverride(cardProg)));
            var render = RenderWithDefault(orch, part, null, seed: 7);

            var taggedRoles = new List<TrackRole>();
            foreach (var chunk in render.merged.GetTrackChunks())
            {
                var tag = chunk.Events.OfType<TextEvent>().FirstOrDefault(
                    te => te.Text != null && te.Text.StartsWith("mus:"));
                if (tag != null && SongOrchestrator.TryParseMusicianTag(
                        tag.Text, out _, out var role))
                    taggedRoles.Add(role);
            }

            Assert.That(taggedRoles, Is.EqualTo(new[]
                { TrackRole.Bassline, TrackRole.Backing }),
                "merged chunk order must follow the track LIST, independent " +
                "of the Backing-first compose order");
        }

        [Test]
        public void ArticulationOnlyBacking_HostDefaultResurges()
        {
            // Criterion 2: replace a harmony-carrying card by a
            // solo-articulation one and the host default drives the part —
            // for the Backing itself AND for the bass, in the hazard order.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var hostDefault = Dbg1Fixtures.Progression("HostDefault",
                (ScaleDegree.Submediant, ChordQuality.Minor),
                (ScaleDegree.Subdominant, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst),   // hazard order
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    style: ArticulationOnlyCard()));
            var render = RenderWithDefault(orch, part, hostDefault, seed: 7);

            Assert.That(StemNotes(render, TrackRole.Bassline), Is.Not.Empty);
            Assert.That(StemNotes(render, TrackRole.Backing), Is.Not.Empty,
                "the articulation-only Backing renders the seeded default, " +
                "not procedural");

            var backingRb = Readback(render, TrackRole.Backing);
            Assert.That(backingRb.source,
                Is.EqualTo(ResolvedSource.SharedProgression),
                "composer-level truth: the Backing consumed the shared cache");
            Assert.That(backingRb.sourceAssetName, Is.EqualTo("HostDefault"));

            Assert.That(render.sharedProgressionSource,
                Is.EqualTo(ResolvedSource.HostDefault),
                "D-ORD-RB: the orchestrator maps seeded SharedProgression " +
                "consumption to HostDefault — the host's dp: cache key");
            Assert.That(render.sharedProgressionAssetName,
                Is.EqualTo("HostDefault"));

            Assert.That(Readback(render, TrackRole.Bassline).progressionRoman,
                Is.EqualTo(backingRb.progressionRoman),
                "criterion 3: one winner, every consumer reads it");
        }

        [Test]
        public void CardHarmony_WinsOverHostDefault_AndReadbackSaysSo()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var cardProg = Dbg1Fixtures.Progression("CardProg",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var hostDefault = Dbg1Fixtures.Progression("HostDefault",
                (ScaleDegree.Subdominant, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    style: CardWithOverride(cardProg)),
                Dbg1Fixtures.Track(TrackRole.Bassline, inst));

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "defaultProgression.*Backing track.*Ignoring"));

            var render = RenderWithDefault(orch, part, hostDefault, seed: 7);

            Assert.That(render.sharedProgressionSource,
                Is.EqualTo(ResolvedSource.CardOverride));
            Assert.That(render.sharedProgressionAssetName,
                Is.EqualTo("CardProg"));
            Assert.That(Readback(render, TrackRole.Bassline).progressionRoman,
                Is.EqualTo(Readback(render, TrackRole.Backing).progressionRoman));
        }

        [Test]
        public void HazardOrder_SameSeed_SameBytes()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();

            ulong Render()
            {
                var cardProg = Dbg1Fixtures.Progression("DetProg",
                    (ScaleDegree.Tonic, ChordQuality.Major),
                    (ScaleDegree.Dominant, ChordQuality.Major));
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst),
                    Dbg1Fixtures.Track(TrackRole.Backing, inst,
                        style: CardWithOverride(cardProg)));
                return Dbg1Fixtures.Fnv(
                    RenderWithDefault(orch, part, null, seed: 7).merged);
            }

            Assert.That(Render(), Is.EqualTo(Render()),
                "determinism: same seed + same config => same bytes");
        }

        // ------------------------------------------------------------------
        // 3) Readback mapping — backing-less parts
        // ------------------------------------------------------------------

        [Test]
        public void BackinglessPart_SeededDefault_StampsHostDefault()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var hostDefault = Dbg1Fixtures.Progression("SoloDefault",
                (ScaleDegree.Tonic, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst));
            var render = RenderWithDefault(orch, part, hostDefault, seed: 7);

            Assert.That(StemNotes(render, TrackRole.Bassline), Is.Not.Empty,
                "SOLO-1 semantics unchanged");
            Assert.That(render.sharedProgressionSource,
                Is.EqualTo(ResolvedSource.HostDefault));
            Assert.That(render.sharedProgressionAssetName,
                Is.EqualTo("SoloDefault"));
        }

        [Test]
        public void BackinglessPart_PrivatePatternOnly_StampsNone()
        {
            // The bass's own TrackParameters.Pattern is PRIVATE harmony, not
            // the shared channel — nothing won the shared progression.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var priv = Dbg1Fixtures.Progression("PrivateProg",
                (ScaleDegree.Tonic, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst, pattern: priv));
            var render = RenderWithDefault(orch, part, null, seed: 7);

            Assert.That(StemNotes(render, TrackRole.Bassline), Is.Not.Empty);
            Assert.That(render.sharedProgressionSource,
                Is.EqualTo(ResolvedSource.None));
            Assert.That(render.sharedProgressionAssetName, Is.Null);
        }
    }
}
#endif
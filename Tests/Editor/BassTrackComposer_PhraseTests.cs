#if UNITY_EDITOR
// MGP-ALWTTT-BASS-PHRASE-1 — EditMode pins for the phrase-aware SelfPocket.
//
// PIN SCOPE — read before editing:
//
// * PHRASE-1 is opt-in: no existing render bytes are replaced, so the strict
//   D-PIN-FIRST=A trigger (tests before touching emission that REPLACES
//   bytes) does not fire. These pins ship in the same batch as the seams
//   they photograph, and the OFF path's byte-identity is guarded two ways:
//   the DELEGATION pins here (legacy signature == extended signature with a
//   null table, plan-for-plan) plus the pre-existing
//   GhostVocabulary_Render_IsDeterministic canary in
//   BassTrackComposer_SelfPocketVocabularyTests, which keeps watching the
//   phrase-off render.
// * The laws pinned: D-PH-ANCHOR=A (meter-absolute bar), D-PH-LEN=A (pure
//   modular slot/phraseIndex), D-PH-INDEX=A (within-bar indexing for every
//   pattern once the phrase is active), D-PH-FILL=C + SD-PH-2=A (SeededMix
//   default, RoundRobin toggle), SD-PH-3=A (per-(phraseIndex, slot) mix),
//   SD-PH-1=A (local table degradation: last-wins duplicates, inert
//   out-of-range, dropped empty variants, all-Rest variants LEGAL),
//   D-PH-BYTE=A (empty table = single OFF gate; phrase length and the
//   selection toggle are byte-inert without it).

using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_PhraseTests
    {
        // ------------------------------------------------------------------
        // Shorthand
        // ------------------------------------------------------------------

        private const BasslineCardConfigSO.SelfPocketStep Slap =
            BasslineCardConfigSO.SelfPocketStep.Slap;
        private const BasslineCardConfigSO.SelfPocketStep Pop =
            BasslineCardConfigSO.SelfPocketStep.Pop;
        private const BasslineCardConfigSO.SelfPocketStep Rest =
            BasslineCardConfigSO.SelfPocketStep.Rest;
        private const BasslineCardConfigSO.SelfPocketStep Ghost =
            BasslineCardConfigSO.SelfPocketStep.Ghost;
        private const BasslineCardConfigSO.SelfPocketStep GhostPop =
            BasslineCardConfigSO.SelfPocketStep.GhostPop;

        private const BasslineCardConfigSO.SelfPocketSubdivision Beat =
            BasslineCardConfigSO.SelfPocketSubdivision.Beat;
        private const BasslineCardConfigSO.SelfPocketSubdivision HalfBeat =
            BasslineCardConfigSO.SelfPocketSubdivision.HalfBeat;

        private const BasslineCardConfigSO.SelfPocketVariantSelection Mix =
            BasslineCardConfigSO.SelfPocketVariantSelection.SeededMix;
        private const BasslineCardConfigSO.SelfPocketVariantSelection Robin =
            BasslineCardConfigSO.SelfPocketVariantSelection.RoundRobin;

        private static List<BasslineCardConfigSO.SelfPocketStep> Steps(
            params BasslineCardConfigSO.SelfPocketStep[] s)
            => new List<BasslineCardConfigSO.SelfPocketStep>(s);

        private static BasslineCardConfigSO.SelfPocketPatternVariant Variant(
            params BasslineCardConfigSO.SelfPocketStep[] s)
            => new BasslineCardConfigSO.SelfPocketPatternVariant
            { steps = Steps(s) };

        private static BasslineCardConfigSO.SelfPocketBarSubstitution Sub(
            int barIndex,
            params BasslineCardConfigSO.SelfPocketPatternVariant[] variants)
            => new BasslineCardConfigSO.SelfPocketBarSubstitution
            {
                barIndex = barIndex,
                variants = new List<
                    BasslineCardConfigSO.SelfPocketPatternVariant>(variants),
            };

        /// <summary>Resolved map for tests; asserts the table survived
        /// validation (use ResolvePhraseSubstitutions directly for the
        /// defect pins).</summary>
        private static IReadOnlyDictionary<int, IReadOnlyList<
                IReadOnlyList<BasslineCardConfigSO.SelfPocketStep>>>
            Map(int phraseLen,
                params BasslineCardConfigSO.SelfPocketBarSubstitution[] subs)
        {
            var warnings = new List<string>();
            var map = BassTrackComposer.ResolvePhraseSubstitutions(
                subs, phraseLen, warnings);
            Assert.That(map, Is.Not.Null,
                "fixture table must be valid: " +
                string.Join(" | ", warnings));
            return map;
        }

        private static List<BassTrackComposer.PocketHit> LegacyPlan(
            double start, double len,
            BasslineCardConfigSO.SelfPocketSubdivision sub,
            IReadOnlyList<BasslineCardConfigSO.SelfPocketStep> pattern,
            int vel = 100, int slapBoost = 0, int popBoost = 0)
            => BassTrackComposer.BuildSelfPocketPlan(
                start, len, sub, pattern, vel, slapBoost, popBoost, null);

        private static List<BassTrackComposer.PocketHit> PhrasePlan(
            double start, double len,
            BasslineCardConfigSO.SelfPocketSubdivision sub,
            IReadOnlyList<BasslineCardConfigSO.SelfPocketStep> pattern,
            double beatsPerBar, int phraseLen,
            IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<
                BasslineCardConfigSO.SelfPocketStep>>> map,
            BasslineCardConfigSO.SelfPocketVariantSelection sel,
            int phraseSeed,
            int vel = 100, int slapBoost = 0, int popBoost = 0)
            => BassTrackComposer.BuildSelfPocketPlan(
                start, len, sub, pattern, vel, slapBoost, popBoost, null,
                beatsPerBar, phraseLen, map, sel, phraseSeed);

        private static (double, double, int,
            BasslineCardConfigSO.SelfPocketStep)[] Shape(
            IEnumerable<BassTrackComposer.PocketHit> plan)
            => plan.Select(h =>
                (h.startBeats, h.lenBeats, h.velocity, h.articulation))
               .ToArray();

        // ------------------------------------------------------------------
        // Delegation — the OFF path is the SLAPFIG-2b planner, plan-for-plan
        // (the structural half of D-PH-BYTE=A; the render half is the
        // pre-existing Ghost-vocabulary canary)
        // ------------------------------------------------------------------

        [Test]
        public void NullTable_ExtendedOverload_EqualsLegacySignature()
        {
            var pattern = Steps(Slap, Ghost, Pop, GhostPop, Rest);
            foreach (var sub in new[] { Beat, HalfBeat })
                foreach (var (start, len) in new[]
                    { (0.0, 4.0), (2.0, 6.0), (3.5, 4.5), (0.0, 16.0) })
                {
                    var legacy = LegacyPlan(start, len, sub, pattern,
                        vel: 96, slapBoost: 10, popBoost: 20);
                    var extended = PhrasePlan(start, len, sub, pattern,
                        beatsPerBar: 4, phraseLen: 4, map: null,
                        sel: Mix, phraseSeed: 12345,
                        vel: 96, slapBoost: 10, popBoost: 20);
                    Assert.That(Shape(extended), Is.EqualTo(Shape(legacy)),
                        $"null table must be the v1 law verbatim " +
                        $"(sub={sub}, window=[{start},{start + len}))");
                }
        }

        // ------------------------------------------------------------------
        // Slot law — D-PH-ANCHOR=A / D-PH-LEN=A
        // ------------------------------------------------------------------

        [Test]
        public void Substitution_ReplacesOnlyItsSlot_MeterAnchored()
        {
            // 4/4, Beat grid, 4-bar phrase, fill at the closing slot (3).
            // One 16-beat event spanning the whole phrase.
            var body = Steps(Slap, Pop);
            var map = Map(4, Sub(3, Variant(Ghost, Ghost, Ghost, Ghost)));

            var plan = PhrasePlan(0, 16, Beat, body,
                beatsPerBar: 4, phraseLen: 4, map: map,
                sel: Mix, phraseSeed: 7);

            // Bars 0-2: within-bar body (Slap Pop Slap Pop); bar 3: Ghosts.
            var arts = plan.Select(h => h.articulation).ToArray();
            var expected = new List<BasslineCardConfigSO.SelfPocketStep>();
            for (int bar = 0; bar < 3; bar++)
                expected.AddRange(new[] { Slap, Pop, Slap, Pop });
            expected.AddRange(new[] { Ghost, Ghost, Ghost, Ghost });
            Assert.That(arts, Is.EqualTo(expected),
                "the fill owns exactly bar 3 (beats 12..16); the body owns " +
                "bars 0-2");
            Assert.That(
                plan.Where(h => h.articulation == Ghost)
                    .Select(h => h.startBeats),
                Is.EqualTo(new[] { 12.0, 13.0, 14.0, 15.0 }),
                "meter-absolute anchoring: the substituted bar starts at " +
                "part beat 12, not at any chord-event boundary");
        }

        [Test]
        public void Phrase_RepeatsModularly_AcrossBars()
        {
            // 2-bar phrase over 6 bars: the fill lands in bars 1, 3, 5.
            var body = Steps(Slap);
            var map = Map(2, Sub(1, Variant(Ghost)));

            var plan = PhrasePlan(0, 24, Beat, body,
                beatsPerBar: 4, phraseLen: 2, map: map,
                sel: Mix, phraseSeed: 7);

            var ghostBeats = plan.Where(h => h.articulation == Ghost)
                                 .Select(h => h.startBeats).ToArray();
            Assert.That(ghostBeats, Is.EqualTo(new[]
                { 4.0, 5.0, 6.0, 7.0, 12.0, 13.0, 14.0, 15.0,
                  20.0, 21.0, 22.0, 23.0 }),
                "slot = bar % phraseLength, purely modular over the part");
        }

        [Test]
        public void Substitution_LandsCorrectly_WhenEventsCrossBars()
        {
            // The same phrase seen through two half-phrase windows must
            // agree with the single-window plan on the substituted bar —
            // per-event windows never move the meter anchor.
            var body = Steps(Slap, Pop);
            var map = Map(4, Sub(3, Variant(Ghost, Ghost, Ghost, Ghost)));

            var w1 = PhrasePlan(0, 10, Beat, body, 4, 4, map, Mix, 7);
            var w2 = PhrasePlan(10, 6, Beat, body, 4, 4, map, Mix, 7);
            var ghosts = w1.Concat(w2)
                .Where(h => h.articulation == Ghost)
                .Select(h => h.startBeats).ToArray();
            Assert.That(ghosts, Is.EqualTo(new[]
                { 12.0, 13.0, 14.0, 15.0 }),
                "bar 3 is beats 12..16 regardless of how chord-event " +
                "windows slice the phrase");
        }

        // ------------------------------------------------------------------
        // Within-bar indexing — D-PH-INDEX=A
        // ------------------------------------------------------------------

        [Test]
        public void PhraseOn_BodyIndexesWithinBar_NonDivisorLengthRestarts()
        {
            // Body length 3 in a 4-step bar. v1 absolute indexing would
            // carry phase across the bar (S P G S | P G S P ...); the
            // phrase law restarts every bar (S P G S | S P G S).
            var body = Steps(Slap, Pop, Ghost);
            var map = Map(4, Sub(3, Variant(GhostPop)));

            var plan = PhrasePlan(0, 8, Beat, body,
                beatsPerBar: 4, phraseLen: 4, map: map,
                sel: Mix, phraseSeed: 7);

            Assert.That(plan.Select(h => h.articulation).ToArray(),
                Is.EqualTo(new[]
                { Slap, Pop, Ghost, Slap,   // bar 0: restart at index 3
                  Slap, Pop, Ghost, Slap }),// bar 1: restarts again
                "with the phrase active EVERY pattern indexes from its bar " +
                "start — the declared D-PH-INDEX=A re-phasing");
        }

        [Test]
        public void FillShorterThanBar_CyclesWithinItsBar()
        {
            var body = Steps(Slap);
            var map = Map(2, Sub(1, Variant(Ghost, GhostPop)));

            var plan = PhrasePlan(0, 8, Beat, body,
                beatsPerBar: 4, phraseLen: 2, map: map,
                sel: Mix, phraseSeed: 7);

            Assert.That(
                plan.Skip(4).Select(h => h.articulation).ToArray(),
                Is.EqualTo(new[] { Ghost, GhostPop, Ghost, GhostPop }),
                "a 2-step fill cycles within its 4-step bar, from the bar " +
                "start");
        }

        // ------------------------------------------------------------------
        // Compound meter — integer part-beat bars (7/8 => 7 beats)
        // ------------------------------------------------------------------

        [Test]
        public void CompoundMeter_SevenBeats_BarBoundariesHold()
        {
            var body = Steps(Slap);
            var map = Map(2, Sub(1, Variant(Ghost)));

            var plan = PhrasePlan(0, 14, Beat, body,
                beatsPerBar: 7, phraseLen: 2, map: map,
                sel: Mix, phraseSeed: 7);

            var ghostBeats = plan.Where(h => h.articulation == Ghost)
                                 .Select(h => h.startBeats).ToArray();
            Assert.That(ghostBeats,
                Is.EqualTo(new[] { 7.0, 8.0, 9.0, 10.0, 11.0, 12.0, 13.0 }),
                "bar 1 of 7/8 is part beats 7..14 — the bar math follows " +
                "the TS table's integer BeatsPerMeasure");
        }

        // ------------------------------------------------------------------
        // Variant selection — D-PH-FILL=C / SD-PH-2=A / SD-PH-3=A
        // ------------------------------------------------------------------

        [Test]
        public void RoundRobin_AlternatesByPhraseIndex()
        {
            // 8 bars = phrases 0 and 1 of a 4-bar phrase; two variants.
            var body = Steps(Slap, Pop);
            var map = Map(4, Sub(3,
                Variant(Ghost, Ghost, Ghost, Ghost),
                Variant(GhostPop, GhostPop, GhostPop, GhostPop)));

            var plan = PhrasePlan(0, 32, Beat, body,
                beatsPerBar: 4, phraseLen: 4, map: map,
                sel: Robin, phraseSeed: 999 /* must be inert for Robin */);

            Assert.That(
                plan.Where(h => h.startBeats >= 12 && h.startBeats < 16)
                    .Select(h => h.articulation),
                Is.All.EqualTo(Ghost),
                "phrase 0 -> variant 0");
            Assert.That(
                plan.Where(h => h.startBeats >= 28 && h.startBeats < 32)
                    .Select(h => h.articulation),
                Is.All.EqualTo(GhostPop),
                "phrase 1 -> variant 1 (phraseIndex % count)");
        }

        [Test]
        public void RoundRobin_IsSeedIndependent()
        {
            Assert.That(
                BassTrackComposer.ResolvePhraseVariantIndex(
                    Robin, phraseSeed: 1, phraseIndex: 5, slot: 3,
                    variantCount: 3),
                Is.EqualTo(BassTrackComposer.ResolvePhraseVariantIndex(
                    Robin, phraseSeed: 22222, phraseIndex: 5, slot: 3,
                    variantCount: 3)),
                "RoundRobin never reads the seed");
        }

        [Test]
        public void SeededMix_IsDeterministic_AndInRange()
        {
            for (int phraseIndex = 0; phraseIndex < 100; phraseIndex++)
            {
                int a = BassTrackComposer.ResolvePhraseVariantIndex(
                    Mix, phraseSeed: 4242, phraseIndex: phraseIndex,
                    slot: 3, variantCount: 3);
                int b = BassTrackComposer.ResolvePhraseVariantIndex(
                    Mix, phraseSeed: 4242, phraseIndex: phraseIndex,
                    slot: 3, variantCount: 3);
                Assert.That(a, Is.EqualTo(b), "same key => same pick");
                Assert.That(a, Is.InRange(0, 2), "floor(mix01 * count)");
            }
        }

        [Test]
        public void SeededMix_ActuallyVaries_AcrossPhraseIndices()
        {
            var picks = Enumerable.Range(0, 32)
                .Select(pi => BassTrackComposer.ResolvePhraseVariantIndex(
                    Mix, phraseSeed: 4242, phraseIndex: pi, slot: 3,
                    variantCount: 2))
                .Distinct().Count();
            Assert.That(picks, Is.EqualTo(2),
                "the mix is a selection law, not a constant — over 32 " +
                "phrases both variants of a pair appear");
        }

        [Test]
        public void SeededMix_VariesBySlot_TheAsymmetricMatrix()
        {
            // SD-PH-3=A: (phraseIndex, slot) both key the mix. Two slots
            // with the same variant count may pick differently in the same
            // phrase — pin that the slot is IN the key (distinct fold
            // constants make the matrix asymmetric), not that any specific
            // pair differs.
            var differs = Enumerable.Range(0, 64).Any(pi =>
                BassTrackComposer.ResolvePhraseVariantIndex(
                    Mix, 4242, pi, slot: 1, variantCount: 2) !=
                BassTrackComposer.ResolvePhraseVariantIndex(
                    Mix, 4242, pi, slot: 2, variantCount: 2));
            Assert.That(differs, Is.True,
                "slot participates in the mix key");
        }

        [Test]
        public void SingleVariant_BothLaws_PickZero()
        {
            Assert.That(BassTrackComposer.ResolvePhraseVariantIndex(
                Mix, 1, 9, 3, 1), Is.EqualTo(0));
            Assert.That(BassTrackComposer.ResolvePhraseVariantIndex(
                Robin, 1, 9, 3, 1), Is.EqualTo(0));
        }

        [Test]
        public void PhraseMix01_GoldenValues_PinTheConstants()
        {
            // Integer-only mixing => EXACT doubles (uint / 2^32 is exactly
            // representable). These goldens pin the duplicated avalanche +
            // the PHRASE-1 fold constants: if either moves, serialized
            // cards' variant picks move with it — a render-affecting
            // change that must be declared, not slipped.
            Assert.That(BassTrackComposer.PhraseMix01(0, 0, 0, 0u),
                Is.EqualTo(0.0),
                "the lowbias32 fixed point at zero — pins the avalanche");
            Assert.That(BassTrackComposer.PhraseMix01(4242, 7, 3, 0u),
                Is.EqualTo(0.6735154767520726),
                "pins the fold constants (0xC2B2AE35 / 0x27D4EB2F)");
            Assert.That(BassTrackComposer.PhraseMix01(12345, 1, 3, 0u),
                Is.EqualTo(0.8988195159472525),
                "a second key, so a lucky collision cannot hide a change");
        }

        // ------------------------------------------------------------------
        // Table validation — SD-PH-1=A
        // ------------------------------------------------------------------

        [Test]
        public void Validation_DuplicateSlot_LastWins_AndWarns()
        {
            var warnings = new List<string>();
            var map = BassTrackComposer.ResolvePhraseSubstitutions(
                new[]
                {
                    Sub(3, Variant(Ghost)),
                    Sub(3, Variant(GhostPop)),
                },
                phraseLengthBars: 4, warnings: warnings);

            Assert.That(map, Is.Not.Null);
            Assert.That(map[3][0][0], Is.EqualTo(GhostPop),
                "the LAST entry wins");
            Assert.That(warnings.Any(w => w.Contains("duplicate")),
                "the collision is warned, not silent");
        }

        [Test]
        public void Validation_OutOfRangeSlot_IsInert_AndWarns()
        {
            var warnings = new List<string>();
            var map = BassTrackComposer.ResolvePhraseSubstitutions(
                new[] { Sub(4, Variant(Ghost)), Sub(-1, Variant(Ghost)),
                        Sub(1, Variant(GhostPop)) },
                phraseLengthBars: 4, warnings: warnings);

            Assert.That(map, Is.Not.Null, "local degradation");
            Assert.That(map.Keys, Is.EquivalentTo(new[] { 1 }),
                "only the in-range entry survives");
            Assert.That(warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validation_EmptyVariant_IsDropped_EntryCanSurvive()
        {
            var warnings = new List<string>();
            var entry = Sub(2,
                new BasslineCardConfigSO.SelfPocketPatternVariant
                { steps = Steps() },              // invalid: no steps
                Variant(Ghost));                  // valid
            var map = BassTrackComposer.ResolvePhraseSubstitutions(
                new[] { entry }, 4, warnings);

            Assert.That(map, Is.Not.Null);
            Assert.That(map[2].Count, Is.EqualTo(1),
                "the empty variant is dropped; the valid one survives");
            Assert.That(warnings.Any(w => w.Contains("no steps")));
        }

        [Test]
        public void Validation_AllRestVariant_IsLegal()
        {
            var warnings = new List<string>();
            var map = BassTrackComposer.ResolvePhraseSubstitutions(
                new[] { Sub(3, Variant(Rest, Rest, Rest, Rest)) },
                4, warnings);

            Assert.That(map, Is.Not.Null,
                "an all-Rest variant is a silent break bar, not a defect");
            Assert.That(warnings, Is.Empty);

            var plan = PhrasePlan(0, 16, Beat, Steps(Slap),
                4, 4, map, Mix, 7);
            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[]
                { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0,
                  8.0, 9.0, 10.0, 11.0 }),
                "bar 3 is silent — the break renders as absence");
        }

        [Test]
        public void Validation_NothingUsable_ReturnsNull()
        {
            var warnings = new List<string>();
            Assert.That(BassTrackComposer.ResolvePhraseSubstitutions(
                new[] { Sub(9, Variant(Ghost)) }, 4, warnings),
                Is.Null, "all entries inert => OFF signal");
            Assert.That(BassTrackComposer.ResolvePhraseSubstitutions(
                new[] { Sub(0, Variant(Ghost)) }, 0, warnings),
                Is.Null, "phraseLengthBars < 1 => global degrade, OFF");
            Assert.That(BassTrackComposer.ResolvePhraseSubstitutions(
                null, 4, warnings), Is.Null, "null table => OFF, no warn");
        }

        // ------------------------------------------------------------------
        // Orchestrator gates — D-PH-BYTE=A at render level
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO BaseCard(string assetName)
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.name = assetName;
            c.pocketMode = BasslineCardConfigSO.PocketCouplingMode.SelfPocket;
            c.selfPocketSubdivision = HalfBeat;
            c.selfPocketPattern = Steps(Slap, Ghost, Pop, GhostPop);
            return c;
        }

        private static ulong RenderFnv(BasslineCardConfigSO card, int seed)
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("PhraseProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                    pattern: prog, style: card));
            return Dbg1Fixtures.Fnv(
                Dbg1Fixtures.Render(orch, part, null, seed).merged);
        }

        [Test]
        public void EmptyTable_PhraseFieldsAreByteInert()
        {
            // D-PH-BYTE=A: the table is the SINGLE gate. Cranking phrase
            // length and flipping the selection toggle with an empty table
            // must not move a byte.
            var baseline = BaseCard("PhraseOffBaseline");

            var poked = BaseCard("PhraseOffPoked");
            poked.selfPocketPhraseLengthBars = 9;
            poked.selfPocketVariantSelection = Robin;
            poked.selfPocketBarSubstitutions =
                new List<BasslineCardConfigSO.SelfPocketBarSubstitution>();

            Assert.That(RenderFnv(poked, seed: 7),
                Is.EqualTo(RenderFnv(baseline, seed: 7)),
                "phrase length and the selection toggle are inert while " +
                "the substitution table is empty");
        }

        [Test]
        public void Phrase_ChangesTheRender_WhenTheTableIsAuthored()
        {
            // The fixture progression is ONE 4/4 bar, so a 1-bar phrase
            // with slot 0 substituted replaces the whole figure.
            var card = BaseCard("PhraseOnCard");
            card.selfPocketPhraseLengthBars = 1;
            card.selfPocketBarSubstitutions =
                new List<BasslineCardConfigSO.SelfPocketBarSubstitution>
                { Sub(0, Variant(Ghost, Ghost, Ghost, Ghost,
                                 Ghost, Ghost, Ghost, Ghost)) };

            Assert.That(RenderFnv(card, seed: 7),
                Is.Not.EqualTo(RenderFnv(BaseCard("PhraseOffRef"), seed: 7)),
                "the substitution is audible in the bytes — not a no-op");
        }

        [Test]
        public void Phrase_Render_IsDeterministic_UnderSeededMix()
        {
            var card = BaseCard("PhraseMixCard");
            card.selfPocketPhraseLengthBars = 1;
            card.selfPocketVariantSelection = Mix;
            card.selfPocketBarSubstitutions =
                new List<BasslineCardConfigSO.SelfPocketBarSubstitution>
                { Sub(0,
                    Variant(Ghost, Ghost, Ghost, Ghost),
                    Variant(GhostPop, GhostPop, GhostPop, GhostPop)) };

            Assert.That(RenderFnv(card, seed: 7),
                Is.EqualTo(RenderFnv(card, seed: 7)),
                "same seed + same table => same bytes: SeededMix is a pure " +
                "keyed mix, never a stream");
        }

        [Test]
        public void PhraseSeed_DerivesFromTrackSeed_Deterministically()
        {
            Assert.That(BassTrackComposer.ResolvePhraseSeed(123),
                Is.EqualTo(BassTrackComposer.ResolvePhraseSeed(123)));
            Assert.That(BassTrackComposer.ResolvePhraseSeed(123),
                Is.Not.EqualTo(BassTrackComposer.ResolvePhraseSeed(124)),
                "distinct track seeds key distinct phrase substreams");
        }
    }
}
#endif
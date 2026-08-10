#if UNITY_EDITOR
// MGP-TRIAGE-ALWTTT-R3 — E1 / recorded gap F5.
//
// ALWTTT observed `slot=1/2` followed by `slot=2/3` inside a single
// SustainLeadIn phrase. Root cause: the archetype's pickup branch emits THREE
// slots but hardcoded `totalSlotsInPhrase = 2` on the first two and `3` on the
// last.
//
// This was recorded as cosmetic. It is not. MelodyTrackComposer's
// IsFinalSlotOfPart predicate is literally
//     slotIndexInPhrase == totalSlotsInPhrase - 1
// so a phrase whose denominator drifts satisfies it MORE THAN ONCE, and
// AscendingClimbMelodyStrategy short-circuits every such slot to its final
// tonic cadence. On the part's last chord span that fired on the pickup grace
// note as well as on the landing.
//
// These tests pin the invariant at the DATA level (no render, no rng beyond
// the archetype's own seeded draws): within one built phrase the denominator
// is constant, indices run 0..n-1, and EXACTLY ONE slot satisfies the
// final-slot predicate. EvenFlow and BurstThenHold are pinned alongside as the
// parity check — they were already correct and must stay that way.
//
// Semantics note: the field counts SLOTS, not audible notes. EvenFlow counts
// its rest slots, so SustainLeadIn's silent lead-in counts too (3, not 2).

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay.Composition;
using MidiGenPlay.Composition.Phrases;

namespace MidiGenPlay.Tests.Editor
{
    public class PhraseArchetype_SlotBookkeepingTests
    {
        private const double StartBeat = 0.0;
        private const double SpanBeats = 4.0;
        private const int BeatsPerBar = 4;
        private const int PhraseId = 11;

        // ------------------------------------------------------------------
        // Shared invariant
        // ------------------------------------------------------------------

        /// <summary>The three-part bookkeeping contract every archetype owes
        /// PhrasePlanner.PhraseSlot: constant denominator, dense 0..n-1
        /// indices, exactly one slot matching MelodyTrackComposer's
        /// IsFinalSlotOfPart predicate.</summary>
        private static void AssertSlotBookkeeping(
            IReadOnlyList<PhrasePlanner.PhraseSlot> slots, string label)
        {
            Assert.That(slots, Is.Not.Null.And.Not.Empty,
                $"{label}: archetype produced no slots");

            foreach (var s in slots)
            {
                Assert.That(s.totalSlotsInPhrase, Is.EqualTo(slots.Count),
                    $"{label}: totalSlotsInPhrase must equal the number of " +
                    $"slots actually built and must not vary within the " +
                    $"phrase (F5).");
                Assert.That(s.phraseId, Is.EqualTo(PhraseId),
                    $"{label}: every slot belongs to the same phrase");
            }

            var indices = slots.Select(s => s.slotIndexInPhrase).ToList();
            Assert.That(indices, Is.EqualTo(Enumerable.Range(0, slots.Count).ToList()),
                $"{label}: slotIndexInPhrase must run 0..n-1 in build order");

            int finalSlots = slots.Count(
                s => s.slotIndexInPhrase == s.totalSlotsInPhrase - 1);
            Assert.That(finalSlots, Is.EqualTo(1),
                $"{label}: MelodyTrackComposer.IsFinalSlotOfPart is " +
                $"`slotIndexInPhrase == totalSlotsInPhrase - 1`; more than one " +
                $"match fires AscendingClimb's final cadence repeatedly.");
        }

        // ------------------------------------------------------------------
        // SustainLeadIn — the E1 regression
        // ------------------------------------------------------------------

        private static SustainLeadInPhraseSO SustainLeadIn(float pickupChance)
        {
            var a = ScriptableObject.CreateInstance<SustainLeadInPhraseSO>();
            a.name = "TestSustainLeadIn";
            a.pickupChance = pickupChance;
            a.pickupSubdivisionBeats = 0.25f;
            return a;
        }

        [Test]
        public void SustainLeadIn_PickupBranch_DenominatorIsConstant()
        {
            var slots = SustainLeadIn(1f).Build(
                StartBeat, SpanBeats, BeatsPerBar, PhraseId,
                contourDir: 1, rng: new System.Random(7),
                profile: null, cfg: null);

            Assert.That(slots.Count, Is.EqualTo(3),
                "the pickup branch builds rest + pickup + sustain");
            AssertSlotBookkeeping(slots, "SustainLeadIn(pickup)");

            // The single final slot must be the sustain, not the grace note.
            var final = slots.Single(
                s => s.slotIndexInPhrase == s.totalSlotsInPhrase - 1);
            Assert.That(final.isPhraseEnd, Is.True,
                "the final-slot predicate must select the phrase-end sustain");
            Assert.That(final.playNote, Is.True);
        }

        [Test]
        public void SustainLeadIn_NoPickupBranch_SingleSlot()
        {
            var slots = SustainLeadIn(0f).Build(
                StartBeat, SpanBeats, BeatsPerBar, PhraseId,
                contourDir: -1, rng: new System.Random(7),
                profile: null, cfg: null);

            Assert.That(slots.Count, Is.EqualTo(1));
            AssertSlotBookkeeping(slots, "SustainLeadIn(no pickup)");
        }

        // ------------------------------------------------------------------
        // Parity — the two archetypes that were already correct
        // ------------------------------------------------------------------

        [Test]
        public void EvenFlow_DenominatorIsConstant([Values(1, 2, 3, 99)] int seed)
        {
            var a = ScriptableObject.CreateInstance<EvenFlowPhraseSO>();
            a.name = "TestEvenFlow";
            a.minSlots = 2;
            a.maxSlots = 5;

            var slots = a.Build(
                StartBeat, SpanBeats, BeatsPerBar, PhraseId,
                contourDir: 1, rng: new System.Random(seed),
                profile: null, cfg: null);

            AssertSlotBookkeeping(slots, $"EvenFlow(seed={seed})");
        }

        [Test]
        public void BurstThenHold_DenominatorIsConstant([Values(1, 2, 3, 99)] int seed)
        {
            var a = ScriptableObject.CreateInstance<BurstThenHoldPhraseSO>();
            a.name = "TestBurstThenHold";
            a.burstNoteCountMin = 2;
            a.burstNoteCountMax = 4;
            a.burstSubdivisionBeats = 0.25f;

            var slots = a.Build(
                StartBeat, SpanBeats, BeatsPerBar, PhraseId,
                contourDir: 1, rng: new System.Random(seed),
                profile: null, cfg: null);

            AssertSlotBookkeeping(slots, $"BurstThenHold(seed={seed})");
        }
    }
}
#endif
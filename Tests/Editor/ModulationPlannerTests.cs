#if UNITY_EDITOR
// MOD-1 (HARMONY-PURE-1) — EditMode tests for ModulationPlanner.
//
// Pure-seam idiom: no ScriptableObjects, no render — the planner is a pure
// function of (source key, target key, seed). Pins:
//  - D-MOD-OUT=A: plan-not-progression output; seed only orders ties.
//  - Pivot detection = intersection of diatonic triads by (root pc, quality).
//  - Ranking: subdominant-in-target band first; deterministic per seed.
//  - Functional dominant of the target: tonic+7 pc, Dominant7, expressed as
//    (Dominant degree, accidental vs target mode — 0 everywhere but Locrian).
//  - Common tones: scale pitch-class intersection, ascending.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ModulationPlannerTests
    {
        // C Ionian -> G Ionian: the textbook close-key modulation.
        private static ModulationPlanner.ModulationPlan PlanCtoG(int seed = 1234)
            => ModulationPlanner.Plan(0, Tonality.Ionian, 7, Tonality.Ionian, seed);

        [Test]
        public void CtoG_FindsTheFourTextbookPivots()
        {
            var plan = PlanCtoG();

            // C major (I -> IV), A minor (vi -> ii), E minor (iii -> vi),
            // G major (V -> I). D/Dm, F/F#dim, Bdim/Bm do NOT match.
            Assert.That(plan.pivots.Count, Is.EqualTo(4));

            var byRoot = plan.pivots.ToDictionary(p => p.rootPitchClass);
            Assert.That(byRoot.ContainsKey(0), "C major pivot");
            Assert.That(byRoot[0].quality, Is.EqualTo(ChordQuality.Major));
            Assert.That(byRoot[0].degreeInSource, Is.EqualTo(ScaleDegree.Tonic));
            Assert.That(byRoot[0].degreeInTarget, Is.EqualTo(ScaleDegree.Subdominant));

            Assert.That(byRoot.ContainsKey(9), "A minor pivot");
            Assert.That(byRoot[9].quality, Is.EqualTo(ChordQuality.Minor));
            Assert.That(byRoot[9].degreeInTarget, Is.EqualTo(ScaleDegree.Supertonic));

            Assert.That(byRoot.ContainsKey(4), "E minor pivot");
            Assert.That(byRoot[4].degreeInTarget, Is.EqualTo(ScaleDegree.Submediant));

            Assert.That(byRoot.ContainsKey(7), "G major pivot");
            Assert.That(byRoot[7].degreeInTarget, Is.EqualTo(ScaleDegree.Tonic));
        }

        [Test]
        public void CtoG_SubdominantInTargetBandRanksFirst()
        {
            var plan = PlanCtoG();

            // First two candidates must be the subdominant-function band in
            // the target (C -> IV, Am -> ii), in some seed-dependent order.
            Assert.That(plan.pivots[0].subdominantInTarget, Is.True);
            Assert.That(plan.pivots[1].subdominantInTarget, Is.True);
            Assert.That(plan.pivots[2].subdominantInTarget, Is.False);
            Assert.That(plan.pivots[3].subdominantInTarget, Is.False);

            var band = new HashSet<int>
            {
                plan.pivots[0].rootPitchClass,
                plan.pivots[1].rootPitchClass,
            };
            Assert.That(band.SetEquals(new[] { 0, 9 }),
                "The subdominant band is exactly {C, Am}.");
        }

        [Test]
        public void CtoG_FunctionalDominant_IsD7_Accidental0()
        {
            var plan = PlanCtoG();

            Assert.That(plan.dominantRootPitchClass, Is.EqualTo(2), "D");
            Assert.That(plan.dominantQuality, Is.EqualTo(ChordQuality.Dominant7));
            Assert.That(plan.dominantDegreeInTarget, Is.EqualTo(ScaleDegree.Dominant));
            Assert.That(plan.dominantAccidentalInTarget, Is.EqualTo(0));
        }

        [Test]
        public void CtoG_CommonTones_AreTheSixSharedPitchClasses()
        {
            var plan = PlanCtoG();

            // C scale ∩ G scale = everything but F (source-only) / F# (target-only).
            Assert.That(plan.commonTonePitchClasses,
                Is.EqualTo(new List<int> { 0, 2, 4, 7, 9, 11 }));
        }

        [Test]
        public void SameSeed_SamePlan_DownToListOrder()
        {
            var a = PlanCtoG(seed: 42);
            var b = PlanCtoG(seed: 42);

            Assert.That(a.pivots.Count, Is.EqualTo(b.pivots.Count));
            for (int i = 0; i < a.pivots.Count; i++)
            {
                Assert.That(a.pivots[i].rootPitchClass,
                    Is.EqualTo(b.pivots[i].rootPitchClass));
                Assert.That(a.pivots[i].degreeInTarget,
                    Is.EqualTo(b.pivots[i].degreeInTarget));
            }
            Assert.That(a.commonTonePitchClasses,
                Is.EqualTo(b.commonTonePitchClasses));
        }

        [Test]
        public void DifferentSeed_SameCandidateSet_SameBands()
        {
            // The seed may reorder WITHIN a band, never across bands and
            // never the membership.
            var a = PlanCtoG(seed: 1);
            var b = PlanCtoG(seed: 987654);

            Assert.That(
                a.pivots.Select(p => p.rootPitchClass).OrderBy(x => x),
                Is.EqualTo(b.pivots.Select(p => p.rootPitchClass).OrderBy(x => x)));
            Assert.That(
                a.pivots.Take(2).Select(p => p.rootPitchClass).OrderBy(x => x),
                Is.EqualTo(b.pivots.Take(2).Select(p => p.rootPitchClass).OrderBy(x => x)),
                "The subdominant band membership is seed-independent.");
        }

        [Test]
        public void LocrianTarget_DominantAccidentalIsPlusOne()
        {
            // Locrian's own Dominant degree sits 6 semitones up (b5); the
            // functional dominant at tonic+7 therefore reads as +1.
            var plan = ModulationPlanner.Plan(
                0, Tonality.Ionian, 0, Tonality.Locrian, seed: 7);

            Assert.That(plan.dominantRootPitchClass, Is.EqualTo(7));
            Assert.That(plan.dominantAccidentalInTarget, Is.EqualTo(+1));
        }

        [Test]
        public void ModalPair_AeolianToRelativeMajor_SharesAllSevenTriads()
        {
            // A Aeolian and C Ionian are the same pitch collection: all 7
            // diatonic triads intersect and all 7 pcs are common tones.
            var plan = ModulationPlanner.Plan(
                9, Tonality.Aeolian, 0, Tonality.Ionian, seed: 3);

            Assert.That(plan.pivots.Count, Is.EqualTo(7));
            Assert.That(plan.commonTonePitchClasses.Count, Is.EqualTo(7));
        }
    }
}
#endif
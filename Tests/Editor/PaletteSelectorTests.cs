#if UNITY_EDITOR
// EditMode tests for the CE-F1 palette selector. The Tier A/B logic and the
// heuristic are exercised with synthetic Candidate<string> values (no asset DB).
// The drum density path is covered both as a pure function (DrumStartsPerBar) and
// against a real DrumPatternData (FoundationOnsets kick-identification + fallback).
//
// Tier C (raw-weights fallback when every heuristic score is <= 0) is a defensive
// guard: with weights floored to 0.0001 and strictly-positive multipliers the
// total can never collapse to <= 0, so the branch is unreachable under normal
// inputs and is intentionally not exercised here.

using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    public class PaletteSelectorTests
    {
        private static Candidate<string> C(
            string id, TimeSignature ts, float weight, int subdivisions = 4, float startsPerBar = 2f)
            => new Candidate<string>(id, weight, new TsFeatures(ts, subdivisions, startsPerBar));

        // ---------------- Tier A ----------------

        [Test]
        public void Pick_TierA_RestrictsToExactTimeSignature_WhenToggleOn()
        {
            // Non-exact entries are heavier, but Tier A must keep only the exact-TS one.
            var cands = new List<Candidate<string>>
            {
                C("four", TimeSignature.FourFour, 1f),
                C("three", TimeSignature.ThreeFour, 5f),
                C("six", TimeSignature.SixEight, 5f),
            };

            for (int seed = 0; seed < 200; seed++)
            {
                var picked = PaletteSelector.Pick(
                    cands, TimeSignature.FourFour, preferExactTs: true,
                    minHarmonicSubdivisions: 4, rng: new System.Random(seed));
                Assert.AreEqual("four", picked, $"seed={seed} should pick the exact-TS entry");
            }
        }

        [Test]
        public void Pick_TierA_Skipped_AllowsNonExact_WhenToggleOff()
        {
            // With Tier A off, a heavily-weighted non-exact entry must be reachable.
            var cands = new List<Candidate<string>>
            {
                C("four", TimeSignature.FourFour, 1f),
                C("three", TimeSignature.ThreeFour, 1000f),
            };

            var seen = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
            {
                seen.Add(PaletteSelector.Pick(
                    cands, TimeSignature.FourFour, preferExactTs: false,
                    minHarmonicSubdivisions: 4, rng: new System.Random(seed)));
            }

            // If Tier A were applied for FourFour, "three" could never be chosen.
            Assert.IsTrue(seen.Contains("three"), "non-exact entry must be reachable when Tier A is skipped");
        }

        // ---------------- Determinism ----------------

        [Test]
        public void Pick_IsDeterministic_ForSameSeed()
        {
            var cands = new List<Candidate<string>>
            {
                C("a", TimeSignature.FourFour, 1f),
                C("b", TimeSignature.ThreeFour, 2f),
                C("c", TimeSignature.SixEight, 3f),
            };

            var first = PaletteSelector.Pick(
                cands, TimeSignature.FourFour, preferExactTs: false,
                minHarmonicSubdivisions: 4, rng: new System.Random(777));
            var second = PaletteSelector.Pick(
                cands, TimeSignature.FourFour, preferExactTs: false,
                minHarmonicSubdivisions: 4, rng: new System.Random(777));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Pick_TierA_IsDeterministic_ForSameSeed()
        {
            var cands = new List<Candidate<string>>
            {
                C("x", TimeSignature.FourFour, 1f),
                C("y", TimeSignature.FourFour, 3f),
                C("z", TimeSignature.ThreeFour, 5f),
            };

            var first = PaletteSelector.Pick(
                cands, TimeSignature.FourFour, preferExactTs: true,
                minHarmonicSubdivisions: 4, rng: new System.Random(99));
            var second = PaletteSelector.Pick(
                cands, TimeSignature.FourFour, preferExactTs: true,
                minHarmonicSubdivisions: 4, rng: new System.Random(99));

            Assert.AreEqual(first, second);
            Assert.IsTrue(first == "x" || first == "y", "Tier A pick must be an exact-TS entry");
        }

        // ---------------- Degenerate inputs ----------------

        [Test]
        public void Pick_SingleCandidate_AlwaysReturnsIt()
        {
            var cands = new List<Candidate<string>> { C("only", TimeSignature.ThreeFour, 1f) };
            // Desired TS has no exact match, so this exercises Tier B with one entry.
            var picked = PaletteSelector.Pick(
                cands, TimeSignature.FourFour, preferExactTs: true,
                minHarmonicSubdivisions: 4, rng: new System.Random(3));
            Assert.AreEqual("only", picked);
        }

        [Test]
        public void Pick_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(PaletteSelector.Pick(
                new List<Candidate<string>>(), TimeSignature.FourFour, true, 4, new System.Random(0)));
            Assert.IsNull(PaletteSelector.Pick<string>(
                null, TimeSignature.FourFour, true, 4, new System.Random(0)));
        }

        // ---------------- Heuristic (Tier B multiplier) ----------------

        [Test]
        public void Heuristic_PrefersExactOverDistantMeter()
        {
            float exact = PaletteSelector.ComputeTsHeuristicMultiplier(
                new TsFeatures(TimeSignature.FourFour, 4, 2f), TimeSignature.FourFour, 4);
            float distant = PaletteSelector.ComputeTsHeuristicMultiplier(
                new TsFeatures(TimeSignature.SevenEight, 4, 2f), TimeSignature.FourFour, 4);

            Assert.Greater(exact, distant);
        }

        [Test]
        public void DefaultGroupingCount_KnownMeters()
        {
            Assert.AreEqual(2, PaletteSelector.DefaultGroupingCount(TimeSignature.FourFour));
            Assert.AreEqual(2, PaletteSelector.DefaultGroupingCount(TimeSignature.FiveFour));
            Assert.AreEqual(3, PaletteSelector.DefaultGroupingCount(TimeSignature.SevenEight));
            Assert.AreEqual(1, PaletteSelector.DefaultGroupingCount(TimeSignature.ThreeFour));
        }

        // ---------------- Density helpers ----------------

        [Test]
        public void StartsPerBar_Chord_MatchesLegacyEstimate()
        {
            Assert.AreEqual(2f, PaletteSelector.StartsPerBar(4, 2), 1e-4f);
            Assert.AreEqual(1f, PaletteSelector.StartsPerBar(0, 1), 1e-4f); // max(1,0)/1
            Assert.AreEqual(3f, PaletteSelector.StartsPerBar(3, 1), 1e-4f);
        }

        [Test]
        public void DrumStartsPerBar_NeutralWhenNoOnsets()
        {
            // No foundation onsets => returns groupCount => B6 factor becomes 1 (neutral).
            Assert.AreEqual(2f, PaletteSelector.DrumStartsPerBar(0, 4, 2), 1e-4f);
        }

        [Test]
        public void DrumStartsPerBar_CapsBusyGrooves()
        {
            // 8 onsets / 2 bars = 4/bar, capped at groupCount 2.
            Assert.AreEqual(2f, PaletteSelector.DrumStartsPerBar(8, 2, 2), 1e-4f);
            // Very busy single bar capped at groupCount 3 (no penalty for busyness).
            Assert.AreEqual(3f, PaletteSelector.DrumStartsPerBar(10, 1, 3), 1e-4f);
        }

        [Test]
        public void DrumStartsPerBar_PenalizesUnderArticulation()
        {
            // 1 onset / 2 bars = 0.5/bar, below groupCount 4 => stays 0.5 (penalized in B6).
            Assert.AreEqual(0.5f, PaletteSelector.DrumStartsPerBar(1, 2, 4), 1e-4f);
        }

        // ---------------- FoundationOnsets (real DrumPatternData) ----------------

        private static DrumPatternData.Lane Lane(GeneralMidiPercussion instrument, params bool[] active)
        {
            var steps = new List<DrumPatternData.StepState>(active.Length);
            foreach (var a in active)
                steps.Add(a ? DrumPatternData.StepState.On() : DrumPatternData.StepState.Off);
            return new DrumPatternData.Lane { instrument = instrument, steps = steps };
        }

        [Test]
        public void FoundationOnsets_CountsKickLane()
        {
            var p = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                p.lanes = new List<DrumPatternData.Lane>
                {
                    Lane((GeneralMidiPercussion)36, true, false, true, false), // kick: 2 onsets
                    Lane((GeneralMidiPercussion)42, true, true, true, true),    // hat: 4 onsets (ignored)
                };
                Assert.AreEqual(2, PatternFinder.FoundationOnsets(p));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void FoundationOnsets_SumsMultipleKickLanes()
        {
            var p = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                p.lanes = new List<DrumPatternData.Lane>
                {
                    Lane((GeneralMidiPercussion)35, true, false), // acoustic bass drum: 1
                    Lane((GeneralMidiPercussion)36, true, true),  // bass drum 1: 2
                };
                Assert.AreEqual(3, PatternFinder.FoundationOnsets(p));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void FoundationOnsets_FallsBackToLowestNoteLane_WhenNoKick()
        {
            var p = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                p.lanes = new List<DrumPatternData.Lane>
                {
                    Lane((GeneralMidiPercussion)43, true, true), // note 43: 2 onsets
                    Lane((GeneralMidiPercussion)41, true),       // note 41 (lowest): 1 onset
                };
                // No kick (35/36) => use lowest-GM-note lane (41) => 1 onset.
                Assert.AreEqual(1, PatternFinder.FoundationOnsets(p));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void FoundationOnsets_ZeroWhenNoLanes()
        {
            var p = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                p.lanes = new List<DrumPatternData.Lane>();
                Assert.AreEqual(0, PatternFinder.FoundationOnsets(p));
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
#endif
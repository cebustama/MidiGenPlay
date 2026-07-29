#if UNITY_EDITOR
// MGP-ALWTTT-BASS-POCKET-1 — SlapPocket coupling of the bass to the Rhythm
// track's published onsets.
//
// Two layers, mirroring the batch's two load-bearing claims:
//
// 1) PURE SEAMS (same idiom as the BASS-WALK-1 suite): BuildPocketPlan is a
//    pure function of (onsets, window) — window filtering, kick→slap /
//    snare→pop classification on the SEMANTIC lane, the pop-wins same-beat
//    rule, max-velocity dedupe within a class, the D-PKT-GATE=A length rule
//    (min(gap, remaining window, PocketMaxGateBeats)), and purity/ordering.
//    Zero rng by construction — the purity test IS the D-PKT rng argument's
//    empirical companion (the structural argument lives in the composer: the
//    pocket branch runs after both §2 selection draws and reads no rng).
//
// 2) ORCHESTRATOR-LEVEL GATES (Dbg1Fixtures + FNV idiom, as
//    PatternOverrideAndReadbackTests):
//    - THE DEGRADE GATE: pocketMode=SlapPocket with NO published source (no
//      Rhythm track / bass composes first) is BYTE-IDENTICAL to
//      pocketMode=Off. This holds because the CA-V1 roller keeps rolling per
//      event whether or not its result is used, and the §2 selection draws
//      are untouched — source availability can never shift any stream.
//    - Pocket engaged (Rhythm before Bassline, grid pattern) CHANGES the
//      output and is DETERMINISTIC (same seed + config => same bytes).
//    - ORDER HAZARD: Bassline before Rhythm in the track list => no
//      publication at bass compose time => degrade (byte-identical to Off in
//      the same track order).
//
// Decisions covered: D-PKT-SRC=B, D-PKT-WHAT=SlapPocket, D-PKT-HOME=A,
// D-PKT-EXPR=A, D-PKT-ORDER=A, D-PKT-VEL=A, D-PKT-GATE=A, D-PKT-POP-PITCH=A.
// See runtime/SSoT_Composer_Bass_Track.md §3.7 (this batch).
//
// MGP-ALWTTT-BASS-POCKET-2 (appended section at the bottom): D-PKT-VEL2=B
// (additive per-class boosts, pre-clamp 1..127, default 0 = identity) and
// D-PKT-LANES2=C (custom lane lists replacing the v1 families; null = family,
// empty = class disabled, both-lists = pop). The POCKET-1 tests above run
// UNMODIFIED against the extended BuildPocketPlan signature (optional
// parameters) — their staying green IS the default-path byte-identity pin at
// the seam level; the two POCKET-2 orchestrator gates pin it at the render
// level and pin that non-default shaping changes bytes deterministically.

using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_PocketTests
    {
        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static MidiGenerator.RhythmOnset O(
            GeneralMidiPercussion i, double beat, int vel = 100)
            => new MidiGenerator.RhythmOnset
            { instrument = i, beat = beat, velocity = vel };

        private const GeneralMidiPercussion Kick =
            GeneralMidiPercussion.AcousticBassDrum;
        private const GeneralMidiPercussion Kick2 =
            GeneralMidiPercussion.BassDrum1;
        private const GeneralMidiPercussion Snare =
            GeneralMidiPercussion.AcousticSnare;
        private const GeneralMidiPercussion Hat =
            GeneralMidiPercussion.ClosedHiHat;

        // ------------------------------------------------------------------
        // Classifier + surface pins
        // ------------------------------------------------------------------

        [Test]
        public void Classifiers_PinTheV1Families()
        {
            Assert.IsTrue(BassTrackComposer.IsPocketKick(Kick));
            Assert.IsTrue(BassTrackComposer.IsPocketKick(Kick2));
            Assert.IsTrue(BassTrackComposer.IsPocketSnare(Snare));
            Assert.IsTrue(BassTrackComposer.IsPocketSnare(
                GeneralMidiPercussion.ElectricSnare));
            // Side stick is deliberately NOT a pop trigger in v1.
            Assert.IsFalse(BassTrackComposer.IsPocketSnare(
                GeneralMidiPercussion.SideStick));
            Assert.IsFalse(BassTrackComposer.IsPocketKick(Hat));
        }

        [Test]
        public void GateCeiling_IsHalfABeat()
        {
            Assert.That(BassTrackComposer.PocketMaxGateBeats, Is.EqualTo(0.5));
        }

        [Test]
        public void Card_PocketDefaultsOff_EnumValuesArePinned()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            Assert.That(card.pocketMode,
                Is.EqualTo(BasslineCardConfigSO.PocketCouplingMode.Off),
                "Opt-in surface: a fresh card must be decoupled.");
            // Append-only serialization pin (as BassArpeggioToneMode).
            Assert.That((int)BasslineCardConfigSO.PocketCouplingMode.Off,
                Is.EqualTo(0));
            Assert.That((int)BasslineCardConfigSO.PocketCouplingMode.SlapPocket,
                Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        // BuildPocketPlan — windowing & classification
        // ------------------------------------------------------------------

        [Test]
        public void Plan_WindowIsInclusiveStartExclusiveEnd()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0), O(Kick, 1.0), O(Kick, 2.0),
            };
            // window [1, 2): only the beat-1 kick.
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 1.0, 1.0);
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].startBeats, Is.EqualTo(1.0));
        }

        [Test]
        public void Plan_KickIsSlap_SnareIsPop_OtherLanesIgnored()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 110),
                O(Hat, 0.5, 80),     // ignored
                O(Snare, 1.0, 90),
            };
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 2.0);
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.IsFalse(plan[0].pop, "kick => slap");
            Assert.That(plan[0].velocity, Is.EqualTo(110),
                "D-PKT-VEL=A: the DRUM step's velocity");
            Assert.IsTrue(plan[1].pop, "snare => pop");
            Assert.That(plan[1].velocity, Is.EqualTo(90));
        }

        [Test]
        public void Plan_SameBeat_PopWinsFlagAndVelocity()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 1.0, 120),   // loud kick
                O(Snare, 1.0, 35),   // ghost snare, same step
            };
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.IsTrue(plan[0].pop,
                "pop (snare) wins the same-beat collision outright");
            Assert.That(plan[0].velocity, Is.EqualTo(35),
                "…including its velocity — the backbeat gesture, not a mix");
        }

        [Test]
        public void Plan_SameBeatSameClass_MaxVelocityWins()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 1.0, 60),
                O(Kick2, 1.0, 100), // second kick-family lane, same step
            };
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.IsFalse(plan[0].pop);
            Assert.That(plan[0].velocity, Is.EqualTo(100));
        }

        // ------------------------------------------------------------------
        // BuildPocketPlan — gate rule (D-PKT-GATE=A)
        // ------------------------------------------------------------------

        [Test]
        public void Plan_Gate_GapBelowCeiling_UsesGap()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0), O(Kick, 0.25),
            };
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            Assert.That(plan[0].lenBeats, Is.EqualTo(0.25));
        }

        [Test]
        public void Plan_Gate_LongGap_CapsAtCeiling()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0), O(Kick, 3.0),
            };
            var plan = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            Assert.That(plan[0].lenBeats,
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
        }

        [Test]
        public void Plan_Gate_LastHit_CapsAtRemainingWindowThenCeiling()
        {
            // last hit 0.3 beats before the window end: remaining wins.
            var shortTail = BassTrackComposer.BuildPocketPlan(
                new List<MidiGenerator.RhythmOnset> { O(Kick, 1.7) }, 0.0, 2.0);
            Assert.That(shortTail[0].lenBeats, Is.EqualTo(0.3).Within(1e-12));

            // last hit 1.5 beats before the end: ceiling wins.
            var longTail = BassTrackComposer.BuildPocketPlan(
                new List<MidiGenerator.RhythmOnset> { O(Kick, 0.5) }, 0.0, 2.0);
            Assert.That(longTail[0].lenBeats,
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
        }

        // ------------------------------------------------------------------
        // BuildPocketPlan — empties, purity, ordering
        // ------------------------------------------------------------------

        [Test]
        public void Plan_EmptyInputs_ReturnEmpty_MeaningFigureApplies()
        {
            Assert.That(BassTrackComposer.BuildPocketPlan(null, 0, 4), Is.Empty);
            Assert.That(BassTrackComposer.BuildPocketPlan(
                new List<MidiGenerator.RhythmOnset>(), 0, 4), Is.Empty);
            // onsets exist but none in the window
            Assert.That(BassTrackComposer.BuildPocketPlan(
                new List<MidiGenerator.RhythmOnset> { O(Kick, 5.0) }, 0, 4),
                Is.Empty);
            // hat-only window: filtered to nothing
            Assert.That(BassTrackComposer.BuildPocketPlan(
                new List<MidiGenerator.RhythmOnset> { O(Hat, 1.0) }, 0, 4),
                Is.Empty);
        }

        [Test]
        public void Plan_IsPure_AndSortedAscending()
        {
            // deliberately unsorted input (the published channel is sorted,
            // but the planner must not depend on it)
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Snare, 2.0, 90), O(Kick, 0.0, 110), O(Kick, 1.5, 100),
            };
            var a = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            var b = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);

            Assert.That(a.Select(h => h.startBeats),
                Is.EqualTo(new[] { 0.0, 1.5, 2.0 }), "sorted ascending");
            Assert.That(a.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop)),
                Is.EqualTo(b.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop))),
                "pure: identical inputs => identical plan (no rng, no state)");
        }

        // ------------------------------------------------------------------
        // Orchestrator-level gates (Dbg1Fixtures + FNV idiom)
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO BassCard(
            BasslineCardConfigSO.PocketCouplingMode mode)
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.pocketMode = mode;
            return c;
        }

        private static SongConfig.PartConfig.TrackConfig RhythmTrack(
            MIDIPercussionInstrumentSO kit, DrumPatternData pattern)
            => new SongConfig.PartConfig.TrackConfig
            {
                Role = TrackRole.Rhythm,
                MusicianId = Dbg1Fixtures.Musician,
                PercussionInstrument = kit,
                Parameters = new TrackParameters { Pattern = pattern },
            };

        [Test]
        public void PocketOn_WithoutAnyRhythmTrack_IsByteIdenticalToOff()
        {
            // THE DEGRADE GATE. Part contains only the bass; no publication
            // can exist, so SlapPocket must render byte-identically to Off.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("PocketProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            ulong Render(BasslineCardConfigSO.PocketCouplingMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog, style: BassCard(mode)));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            Assert.That(
                Render(BasslineCardConfigSO.PocketCouplingMode.SlapPocket),
                Is.EqualTo(Render(BasslineCardConfigSO.PocketCouplingMode.Off)),
                "pocket-on without a source must be BYTE-identical to pocket-off " +
                "(warn max, never error, never silence, no stream drift)");
        }

        [Test]
        public void PocketOn_BasslineBeforeRhythmInTrackList_Degrades()
        {
            // D-PKT-ORDER=A: the bass composes first => nothing published yet
            // => degrade, byte-identical to Off IN THE SAME TRACK ORDER.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);
            var inst = Dbg1Fixtures.Instrument();
            var kit = Dbg1Fixtures.Kit();
            var prog = Dbg1Fixtures.Progression("PocketProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var drums = Dbg1Fixtures.DrumPattern("PocketDrums");

            ulong Render(BasslineCardConfigSO.PocketCouplingMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog, style: BassCard(mode)),
                    RhythmTrack(kit, drums)); // Bassline FIRST — the hazard
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            Assert.That(
                Render(BasslineCardConfigSO.PocketCouplingMode.SlapPocket),
                Is.EqualTo(Render(BasslineCardConfigSO.PocketCouplingMode.Off)));
        }

        [Test]
        public void PocketOn_RhythmBeforeBassline_EngagesAndIsDeterministic()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);
            var inst = Dbg1Fixtures.Instrument();
            var kit = Dbg1Fixtures.Kit();
            var prog = Dbg1Fixtures.Progression("PocketProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var drums = Dbg1Fixtures.DrumPattern("PocketDrums");

            ulong Render(BasslineCardConfigSO.PocketCouplingMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    RhythmTrack(kit, drums), // Rhythm FIRST — publication ready
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog, style: BassCard(mode)));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            var off = Render(BasslineCardConfigSO.PocketCouplingMode.Off);
            var on1 = Render(BasslineCardConfigSO.PocketCouplingMode.SlapPocket);
            var on2 = Render(BasslineCardConfigSO.PocketCouplingMode.SlapPocket);

            Assert.That(on1, Is.Not.EqualTo(off),
                "with a published grid source, SlapPocket must change the render");
            Assert.That(on1, Is.EqualTo(on2),
                "determinism: same seed + same config => same bytes");
        }

        // ==================================================================
        // MGP-ALWTTT-BASS-POCKET-2 — velocity shaping (D-PKT-VEL2=B) and
        // custom trigger lanes (D-PKT-LANES2=C).
        // ==================================================================

        private const GeneralMidiPercussion Stick =
            GeneralMidiPercussion.SideStick;

        private static readonly GeneralMidiPercussion[] NoLanes =
            new GeneralMidiPercussion[0];

        // ------------------------------------------------------------------
        // Boosts — pure seam
        // ------------------------------------------------------------------

        [Test]
        public void Plan2_Boosts_AdditivePerClass_Independent()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 100),
                O(Snare, 1.0, 60),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0, slapBoost: -10, popBoost: 25);
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan[0].velocity, Is.EqualTo(90),
                "slap: drum velocity + slapBoost only");
            Assert.That(plan[1].velocity, Is.EqualTo(85),
                "pop: drum velocity + popBoost only — classes never cross");
        }

        [Test]
        public void Plan2_Boosts_ClampTo1And127()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 120),   // +20 => 140 => clamp 127
                O(Snare, 1.0, 10),   // -20 => -10 => clamp 1
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0, slapBoost: 20, popBoost: -20);
            Assert.That(plan[0].velocity, Is.EqualTo(127));
            Assert.That(plan[1].velocity, Is.EqualTo(1));
        }

        [Test]
        public void Plan2_BoostZero_IsExactIdentityWithPocket1Call()
        {
            // The load-bearing default: the POCKET-1 3-arg call and the
            // POCKET-2 call with explicit defaults must produce equal plans.
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Snare, 2.0, 90), O(Kick, 0.0, 110), O(Kick2, 0.0, 115),
            };
            var v1 = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0);
            var v2 = BassTrackComposer.BuildPocketPlan(onsets, 0.0, 4.0,
                slapBoost: 0, popBoost: 0, slapLanes: null, popLanes: null);
            Assert.That(
                v2.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop)),
                Is.EqualTo(
                v1.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop))));
        }

        [Test]
        public void Plan2_Boost_DedupeComparesBoostedValues_SameClassMax()
        {
            // Uniform per-class boost preserves the intra-class argmax; the
            // stored velocity is the boosted one.
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 1.0, 60),
                O(Kick2, 1.0, 100),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 4.0, slapBoost: 15);
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].velocity, Is.EqualTo(115));
        }

        // ------------------------------------------------------------------
        // Custom lanes — pure seam
        // ------------------------------------------------------------------

        [Test]
        public void Plan2_Lanes_NullMeansV1Family_SideStickStillIgnored()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Stick, 1.0, 90),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0, slapLanes: null, popLanes: null);
            Assert.That(plan, Is.Empty,
                "null lists = the v1 families exactly (SideStick excluded)");
        }

        [Test]
        public void Plan2_Lanes_SideStickInPopList_TriggersPop()
        {
            // The Latin case that motivated D-PKT-LANES2=C.
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 100),
                O(Stick, 1.0, 90),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0,
                slapLanes: new[] { Kick, Kick2 },
                popLanes: new[] { Stick });
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.IsFalse(plan[0].pop);
            Assert.IsTrue(plan[1].pop, "SideStick pops via the custom list");
            Assert.That(plan[1].velocity, Is.EqualTo(90));
        }

        [Test]
        public void Plan2_Lanes_NonNullListReplacesFamily_NotExtends()
        {
            // A pop list without AcousticSnare must NOT fall back to the
            // family for it — the list REPLACES the family outright.
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Snare, 1.0, 90),
                O(Stick, 2.0, 80),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 4.0,
                slapLanes: NoLanes,
                popLanes: new[] { Stick });
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].startBeats, Is.EqualTo(2.0),
                "AcousticSnare is not in the custom pop list => ignored");
        }

        [Test]
        public void Plan2_Lanes_EmptyListDisablesClass()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 100),
                O(Snare, 1.0, 90),
            };
            // pop-only pocket: slap class disabled, pop keeps the family
            // members via an explicit list.
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0,
                slapLanes: NoLanes,
                popLanes: new[] { Snare, GeneralMidiPercussion.ElectricSnare });
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.IsTrue(plan[0].pop);
            Assert.That(plan[0].startBeats, Is.EqualTo(1.0));
        }

        [Test]
        public void Plan2_Lanes_LaneInBothLists_ClassifiesAsPop()
        {
            var onsets = new List<MidiGenerator.RhythmOnset>
            {
                O(Kick, 0.0, 100),
            };
            var plan = BassTrackComposer.BuildPocketPlan(
                onsets, 0.0, 2.0, popBoost: 5,
                slapLanes: new[] { Kick },
                popLanes: new[] { Kick });
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.IsTrue(plan[0].pop,
                "both-lists overlap resolves to pop (pop check runs first)");
            Assert.That(plan[0].velocity, Is.EqualTo(105),
                "…and takes the POP boost, consistently with the flag");
        }

        // ------------------------------------------------------------------
        // Card surface pins
        // ------------------------------------------------------------------

        [Test]
        public void Card_Pocket2Defaults_ArePinned()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            Assert.That(card.pocketSlapBoost, Is.EqualTo(0),
                "D-PKT-VEL2=B: default 0 => byte-identical to POCKET-1");
            Assert.That(card.pocketPopBoost, Is.EqualTo(0));
            Assert.That(card.pocketCustomLanes, Is.False,
                "D-PKT-LANES2=C serialization C1: toggle off => v1 families");
            Assert.That(card.pocketSlapLanes, Is.Not.Null.And.Empty);
            Assert.That(card.pocketPopLanes, Is.Not.Null.And.Empty);
        }

        // ------------------------------------------------------------------
        // Orchestrator-level gates
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO BassCard2(
            BasslineCardConfigSO.PocketCouplingMode mode,
            int slapBoost, int popBoost, bool customLanes = false)
        {
            var c = BassCard(mode);
            c.pocketSlapBoost = slapBoost;
            c.pocketPopBoost = popBoost;
            c.pocketCustomLanes = customLanes;
            return c;
        }

        [Test]
        public void Pocket2On_WithoutSource_ShapingFieldsAreInert()
        {
            // The POCKET-2 extension of THE DEGRADE GATE: non-default boosts
            // and the custom-lanes toggle live entirely inside the pocket
            // branch, so pocket-on-without-source must STAY byte-identical to
            // pocket-off — the hard constraint of this batch.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("Pocket2Prog",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            ulong Render(BasslineCardConfigSO card)
            {
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog, style: card));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            var off = Render(BassCard(
                BasslineCardConfigSO.PocketCouplingMode.Off));
            var onShaped = Render(BassCard2(
                BasslineCardConfigSO.PocketCouplingMode.SlapPocket,
                slapBoost: 30, popBoost: -30, customLanes: true));

            Assert.That(onShaped, Is.EqualTo(off));
        }

        [Test]
        public void Pocket2On_NonZeroBoosts_ChangeEngagedRender_Deterministic()
        {
            // Engaged pocket (Rhythm before Bassline): non-zero boosts must
            // change the bytes and stay deterministic. Fixture assumption on
            // record: the Dbg1 drum pattern's kick/snare velocities are not
            // already saturated at 1, so a -30 boost is observable.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);
            var inst = Dbg1Fixtures.Instrument();
            var kit = Dbg1Fixtures.Kit();
            var prog = Dbg1Fixtures.Progression("Pocket2Prog",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var drums = Dbg1Fixtures.DrumPattern("Pocket2Drums");

            ulong Render(BasslineCardConfigSO card)
            {
                var part = Dbg1Fixtures.Part(
                    RhythmTrack(kit, drums),
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog, style: card));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            var plain = Render(BassCard(
                BasslineCardConfigSO.PocketCouplingMode.SlapPocket));
            var boosted1 = Render(BassCard2(
                BasslineCardConfigSO.PocketCouplingMode.SlapPocket,
                slapBoost: -30, popBoost: -30));
            var boosted2 = Render(BassCard2(
                BasslineCardConfigSO.PocketCouplingMode.SlapPocket,
                slapBoost: -30, popBoost: -30));

            Assert.That(boosted1, Is.Not.EqualTo(plain),
                "non-zero boosts must be observable in the engaged render");
            Assert.That(boosted1, Is.EqualTo(boosted2),
                "determinism: same seed + same shaping => same bytes");
        }
    }
}
#endif
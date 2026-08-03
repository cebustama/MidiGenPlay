#if UNITY_EDITOR
// MGP-ALWTTT-BASS-SLAPFIG-1 — SelfPocket: autonomous slap/pop figure.
//
// Two layers, the exact structure of the POCKET-1 suite:
//
// 1) PURE SEAM — BuildSelfPocketPlan is a pure function of (window,
//    subdivision, pattern, event velocity, boosts): meter-anchored grid
//    (absolute grid index % pattern length — phase survives chord changes),
//    inclusive-start/exclusive-end windowing, Rest skip, the D-SFIG-VEL=A
//    velocity law (event velocity + per-class boost, clamp 1..127) and the
//    D-PKT-GATE=A length rule (min(gap to next PLANNED hit, remaining
//    window, PocketMaxGateBeats)). Zero rng by construction — the purity
//    test is the determinism argument's empirical companion; the structural
//    half lives in the composer (the plan branch runs after both §2 draws
//    and reads no rng, verbatim POCKET-1).
//
// 2) ORCHESTRATOR GATES (Dbg1Fixtures + FNV idiom):
//    - SelfPocket CHANGES the render vs Off and is DETERMINISTIC.
//    - DEGRADE: empty/all-Rest pattern warns and renders BYTE-IDENTICAL to
//      Off (never error, never silence).
//    - AUTONOMY (criterion 1): the bass stem is byte-identical with and
//      without a Rhythm track present — SelfPocket reads no cross-track
//      state, so it can never wake the ALWTTT §8.4 consumer-side hash duty.
//
// Decisions covered: D-SFIG-SURF=A, D-SFIG-PAT=A, D-SFIG-VEL=A (+ the
// inherited D-PKT-GATE=A / D-REG-2=B pop identity, pinned at the plan level;
// the pop +12 fold itself is pinned by the BASS-REG-1 suite at ResolvePopNote).

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
    public class BassTrackComposer_SelfPocketTests
    {
        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private const BasslineCardConfigSO.SelfPocketStep Slap =
            BasslineCardConfigSO.SelfPocketStep.Slap;
        private const BasslineCardConfigSO.SelfPocketStep Pop =
            BasslineCardConfigSO.SelfPocketStep.Pop;
        private const BasslineCardConfigSO.SelfPocketStep Rest =
            BasslineCardConfigSO.SelfPocketStep.Rest;

        private const BasslineCardConfigSO.SelfPocketSubdivision Beat =
            BasslineCardConfigSO.SelfPocketSubdivision.Beat;
        private const BasslineCardConfigSO.SelfPocketSubdivision HalfBeat =
            BasslineCardConfigSO.SelfPocketSubdivision.HalfBeat;

        private static List<BassTrackComposer.PocketHit> Plan(
            double start, double len,
            BasslineCardConfigSO.SelfPocketSubdivision sub,
            BasslineCardConfigSO.SelfPocketStep[] pattern,
            int vel = 100, int slapBoost = 0, int popBoost = 0)
            => BassTrackComposer.BuildSelfPocketPlan(
                start, len, sub, pattern, vel, slapBoost, popBoost);

        // ------------------------------------------------------------------
        // Card surface pins
        // ------------------------------------------------------------------

        [Test]
        public void Card_SelfPocketSurface_DefaultsAndEnumValuesArePinned()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();

            // Append-only serialization pins.
            Assert.That((int)BasslineCardConfigSO.PocketCouplingMode.SelfPocket,
                Is.EqualTo(2));
            Assert.That((int)Slap, Is.EqualTo(0));
            Assert.That((int)Pop, Is.EqualTo(1));
            Assert.That((int)Rest, Is.EqualTo(2));
            Assert.That((int)Beat, Is.EqualTo(0));
            Assert.That((int)HalfBeat, Is.EqualTo(1));

            // Fresh-card defaults: decoupled mode, classic alternation ready.
            Assert.That(card.pocketMode,
                Is.EqualTo(BasslineCardConfigSO.PocketCouplingMode.Off));
            Assert.That(card.selfPocketSubdivision, Is.EqualTo(Beat));
            Assert.That(card.selfPocketPattern,
                Is.EqualTo(new[] { Slap, Pop }),
                "default pattern is the classic slap/pop alternation");
        }

        // ------------------------------------------------------------------
        // BuildSelfPocketPlan — grid, cycle, anchoring
        // ------------------------------------------------------------------

        [Test]
        public void Plan_DefaultAlternation_FullBar()
        {
            var plan = Plan(0.0, 4.0, Beat, new[] { Slap, Pop });

            Assert.That(plan.Count, Is.EqualTo(4));
            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0 }));
            Assert.That(plan.Select(h => h.pop),
                Is.EqualTo(new[] { false, true, false, true }),
                "even absolute grid indices slap, odd pop");
            // D-PKT-GATE=A: gap 1.0 and remaining >= 1.0, ceiling wins.
            Assert.That(plan.All(h => h.lenBeats == 0.5), Is.True);
            Assert.That(plan.All(h => h.velocity == 100), Is.True,
                "D-SFIG-VEL=A with zero boosts = the event's velocity");
        }

        [Test]
        public void Plan_IsMeterAnchored_NotEventAnchored()
        {
            // Event window [1, 3): grid indices 1 and 2 — the cycle reads
            // Pop at beat 1, Slap at beat 2. An event-anchored figure would
            // start with Slap; the meter anchor is the pinned behavior
            // (D-SFIG-PAT=A).
            var plan = Plan(1.0, 2.0, Beat, new[] { Slap, Pop });

            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan[0].startBeats, Is.EqualTo(1.0));
            Assert.That(plan[0].pop, Is.True, "grid index 1 => Pop");
            Assert.That(plan[1].startBeats, Is.EqualTo(2.0));
            Assert.That(plan[1].pop, Is.False, "grid index 2 => Slap");
        }

        [Test]
        public void Plan_PhaseSurvivesChordChanges()
        {
            // Splitting [0,4) into [0,2) + [2,4) yields the SAME hits as one
            // whole-bar plan — the figure never resets at a chord boundary.
            var whole = Plan(0.0, 4.0, Beat, new[] { Slap, Pop, Rest });
            var a = Plan(0.0, 2.0, Beat, new[] { Slap, Pop, Rest });
            var b = Plan(2.0, 2.0, Beat, new[] { Slap, Pop, Rest });

            var split = a.Concat(b)
                .Select(h => (h.startBeats, h.pop, h.velocity)).ToList();
            var reference = whole
                .Select(h => (h.startBeats, h.pop, h.velocity)).ToList();

            Assert.That(split, Is.EqualTo(reference),
                "classification is a function of the ABSOLUTE grid index only");
        }

        [Test]
        public void Plan_HalfBeatGrid_DoublesTheDensity()
        {
            var plan = Plan(0.0, 1.0, HalfBeat, new[] { Slap, Pop });

            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[] { 0.0, 0.5 }));
            Assert.That(plan.Select(h => h.pop),
                Is.EqualTo(new[] { false, true }));
            Assert.That(plan.All(h => h.lenBeats == 0.5), Is.True);
        }

        [Test]
        public void Plan_FractionalEventStart_SnapsToNextGridIndex()
        {
            // Event starting at beat 1.5 on the Beat grid: first candidate is
            // absolute index 2 (beat 2.0) — nothing is emitted off-grid.
            var plan = Plan(1.5, 2.5, Beat, new[] { Slap, Pop });

            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[] { 2.0, 3.0 }));
            Assert.That(plan.Select(h => h.pop),
                Is.EqualTo(new[] { false, true }),
                "indices 2 and 3 of the absolute cycle");
        }

        // ------------------------------------------------------------------
        // BuildSelfPocketPlan — Rest, gate, emptiness
        // ------------------------------------------------------------------

        [Test]
        public void Plan_RestSkips_AndGateSpansToNextPlannedHit()
        {
            // [Slap, Rest] over [0,4): hits at 0 and 2 only. The gate rule
            // measures the gap to the next PLANNED hit (2.0 away), so the
            // 0.5 ceiling still wins — pinned so a sparser pattern can never
            // stretch a percussive hit.
            var plan = Plan(0.0, 4.0, Beat, new[] { Slap, Rest });

            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[] { 0.0, 2.0 }));
            Assert.That(plan.All(h => !h.pop), Is.True);
            Assert.That(plan.All(h => h.lenBeats == 0.5), Is.True);
        }

        [Test]
        public void Plan_GateIsClippedByWindowEnd()
        {
            // Last hit at 3.75 in a window ending at 4.0 => len 0.25 (the
            // remaining window beats the 0.5 ceiling).
            var plan = Plan(3.75, 0.25, HalfBeat, new[] { Slap });
            // Grid index 8 (beat 4.0) is outside; index at 3.75? HalfBeat grid
            // has candidates at multiples of 0.5 only — no hit fits.
            Assert.That(plan, Is.Empty,
                "no grid candidate inside a window that starts off-grid and " +
                "ends before the next grid line");

            var plan2 = Plan(3.5, 0.75, HalfBeat, new[] { Slap });
            Assert.That(plan2.Count, Is.EqualTo(2));
            Assert.That(plan2[1].startBeats, Is.EqualTo(4.0));
            Assert.That(plan2[1].lenBeats, Is.EqualTo(0.25).Within(1e-12),
                "remaining window < gate ceiling => clipped");
        }

        [Test]
        public void Plan_AllRest_EmptyPattern_ZeroLen_AllEmpty()
        {
            Assert.That(Plan(0.0, 4.0, Beat, new[] { Rest, Rest }), Is.Empty);
            Assert.That(Plan(0.0, 4.0, Beat,
                new BasslineCardConfigSO.SelfPocketStep[0]), Is.Empty);
            Assert.That(BassTrackComposer.BuildSelfPocketPlan(
                0.0, 4.0, Beat, null, 100), Is.Empty);
            Assert.That(Plan(0.0, 0.0, Beat, new[] { Slap }), Is.Empty);
            Assert.That(Plan(0.0, -1.0, Beat, new[] { Slap }), Is.Empty);
        }

        // ------------------------------------------------------------------
        // BuildSelfPocketPlan — velocity law (D-SFIG-VEL=A)
        // ------------------------------------------------------------------

        [Test]
        public void Plan_Boosts_AdditivePerClass_OverEventVelocity()
        {
            var plan = Plan(0.0, 2.0, Beat, new[] { Slap, Pop },
                vel: 90, slapBoost: -10, popBoost: 25);

            Assert.That(plan[0].pop, Is.False);
            Assert.That(plan[0].velocity, Is.EqualTo(80),
                "slap: event velocity + slapBoost only");
            Assert.That(plan[1].pop, Is.True);
            Assert.That(plan[1].velocity, Is.EqualTo(115),
                "pop: event velocity + popBoost only — classes never cross");
        }

        [Test]
        public void Plan_Boosts_ClampTo1And127()
        {
            var plan = Plan(0.0, 2.0, Beat, new[] { Slap, Pop },
                vel: 120, slapBoost: 64, popBoost: -64);
            Assert.That(plan[0].velocity, Is.EqualTo(127));

            var low = Plan(0.0, 2.0, Beat, new[] { Slap, Pop },
                vel: 10, slapBoost: 0, popBoost: -64);
            Assert.That(low[1].velocity, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        // BuildSelfPocketPlan — purity
        // ------------------------------------------------------------------

        [Test]
        public void Plan_IsPure_SameInputsSameOutputs()
        {
            var pattern = new[] { Slap, Rest, Pop };
            var a = Plan(0.5, 3.5, HalfBeat, pattern, 96, 5, -5);
            var b = Plan(0.5, 3.5, HalfBeat, pattern, 96, 5, -5);

            Assert.That(a.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop)),
                Is.EqualTo(b.Select(h => (h.startBeats, h.lenBeats, h.velocity, h.pop))),
                "deterministic, no rng, no hidden state");
        }

        // ------------------------------------------------------------------
        // Orchestrator gates (Dbg1Fixtures + FNV idiom)
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO BassCard(
            BasslineCardConfigSO.PocketCouplingMode mode,
            List<BasslineCardConfigSO.SelfPocketStep> pattern = null)
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.name = "SelfPocketCard";
            c.pocketMode = mode;
            if (pattern != null) c.selfPocketPattern = pattern;
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
        public void SelfPocket_ChangesTheRender_AndIsDeterministic()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("SelfProg",
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

            var off = Render(BasslineCardConfigSO.PocketCouplingMode.Off);
            var on1 = Render(BasslineCardConfigSO.PocketCouplingMode.SelfPocket);
            var on2 = Render(BasslineCardConfigSO.PocketCouplingMode.SelfPocket);

            Assert.That(on1, Is.Not.EqualTo(off),
                "SelfPocket needs NO published source — the default " +
                "[Slap, Pop] pattern must change the render on its own");
            Assert.That(on1, Is.EqualTo(on2),
                "determinism: same seed + same config => same bytes");
        }

        [Test]
        public void SelfPocket_EmptyOrAllRestPattern_WarnsAndDegradesToOff()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("SelfProg",
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

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "SelfPocket.*empty or all-Rest"));
            var allRest = Render(BassCard(
                BasslineCardConfigSO.PocketCouplingMode.SelfPocket,
                new List<BasslineCardConfigSO.SelfPocketStep> { Rest, Rest }));

            Assert.That(allRest, Is.EqualTo(off),
                "degrade contract: warn max, never error, never silence, " +
                "byte-identical to Off");
        }

        [Test]
        public void SelfPocket_IgnoresTheRhythmTrack_BassStemIsByteIdentical()
        {
            // AUTONOMY GATE (criterion 1 of the ask): the bass stem must be
            // byte-identical with and without a Rhythm track in the part —
            // SelfPocket performs zero cross-track reads, so the drummer's
            // presence (and pattern) can never leak into the bass bytes.
            // (Per-track seeds key on (role, musicianId), never on the track
            // list — so the added Rhythm row shifts nothing.)
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);
            var inst = Dbg1Fixtures.Instrument();
            var kit = Dbg1Fixtures.Kit();
            var prog = Dbg1Fixtures.Progression("SelfProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var drums = Dbg1Fixtures.DrumPattern("SelfDrums");

            ulong BassStem(bool withRhythm)
            {
                var bass = Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                    pattern: prog,
                    style: BassCard(
                        BasslineCardConfigSO.PocketCouplingMode.SelfPocket));
                var part = withRhythm
                    ? Dbg1Fixtures.Part(RhythmTrack(kit, drums), bass)
                    : Dbg1Fixtures.Part(bass);
                var render = Dbg1Fixtures.Render(orch, part, null, seed: 7);
                render.stemsByMusician.TryGetValue(
                    new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Bassline),
                    out var stem);
                Assert.That(stem, Is.Not.Null);
                return Dbg1Fixtures.Fnv(stem);
            }

            Assert.That(BassStem(withRhythm: true),
                Is.EqualTo(BassStem(withRhythm: false)),
                "SelfPocket must not read the drummer — the ALWTTT §8.4 " +
                "consumer-side hash duty stays asleep by construction");
        }
    }
}
#endif
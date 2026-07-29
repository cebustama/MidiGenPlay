#if UNITY_EDITOR
// B3 WALK-2 — improvised walking bass (the closing phase of B3).
//
// Decisions covered:
//   D-W2-VOCAB=B  - chord tones on the middles + a chromatic (±1) or
//                   whole-step (±2) approach note into the NEXT event's root
//                   on the last hit.
//   D-W2-LAST=A   - the last event approaches the FIRST event's root (wrap).
//   D-W2-HOME=A   - composer-side: the engine plans rhythm/accents/jitter
//                   (PlanHits, called composer-side with noteCount 1); the
//                   composer plans PITCHES only (BuildWalkLine) and emits one
//                   1-note Block segment per hit through the single
//                   unconditional Emit (BlockPlan is a velocity passthrough).
//   D-W2-SURF=A   - BassArpeggioToneMode.ImprovisedWalk = 2 (append-only).
//   D-W2-RNG=B    - variation is a PURE MIX of (walkSeed, eventIndex,
//                   hitIndex) — the VelocityJitter idiom. No stream exists,
//                   so no draw-count discipline is needed and no toggle can
//                   shift anything. ZERO ctx.rng draws.
//   D-W2-POCKET=A - §3.7 verbatim: pocketed events bypass the walk.
//   D-W2-REG      - per-note fold -12 above the D-REG-1=C ceiling; approach
//                   notes may dip below the band floor (low is safe).
//
// Discipline: pinned at the PURE seam (BuildWalkLine) plus orchestrator-level
// gates in the Dbg1Fixtures + FNV idiom. The BC detectors for this batch are
// the EXISTING suites running unmodified (ArticulationTests WALK-1 pins,
// RegisterTests, PocketTests, SongOrchestratorSeedTests). Register gates read
// the BASS STEM from stemsByMusician, never PartRender.merged (the merge
// carries the metronome click, D5=74 / D#5=75 — the B3 authoring finding).
//
// See runtime/SSoT_Composer_Bass_Track.md §2 / §3.6 / §3.7 and
// planning/Roadmap_Chord_Articulation.md (B3).

using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_WalkImprovTests
    {
        private static readonly DwmNoteName[] CMajorPcs =
            { DwmNoteName.C, DwmNoteName.E, DwmNoteName.G };

        private const int GenerousCeiling = 127;
        private const int Seed = 12345;

        private static int[] Numbers(DwmNote[] line)
            => line.Select(n => (int)n.NoteNumber).ToArray();

        /// <summary>Minimum mod-12 distance from a note number to a pitch
        /// class (test-local helper).</summary>
        private static int PcDistance(int noteNumber, DwmNoteName pc)
        {
            int d = ((noteNumber - (int)pc) % 12 + 12) % 12;
            return System.Math.Min(d, 12 - d);
        }

        // ==================================================================
        // BuildWalkLine — pure seam (D-W2-VOCAB=B / D-W2-RNG=B / D-W2-REG)
        // ==================================================================

        [Test]
        public void WalkLine_IsPure_SameInputsSameSequence()
        {
            var a = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, rootOct: 2, GenerousCeiling,
                hitCount: 4, descendBias: false, Seed, eventIndex: 3);
            var b = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, rootOct: 2, GenerousCeiling,
                hitCount: 4, descendBias: false, Seed, eventIndex: 3);

            Assert.That(Numbers(a), Is.EqualTo(Numbers(b)),
                "pure: identical inputs => identical line (no rng, no state)");
        }

        [Test]
        public void WalkLine_AnchorsTheEventRootAtTheDrawnOctave()
        {
            var line = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, rootOct: 2, GenerousCeiling,
                hitCount: 4, descendBias: false, Seed, eventIndex: 0);

            Assert.That((int)line[0].NoteNumber,
                Is.EqualTo((int)DwmNote.Get(DwmNoteName.C, 2).NoteNumber),
                "hit 0 is the event root at the §2 drawn octave (WALK-1 anchor)");
        }

        [Test]
        public void WalkLine_LengthMatchesHitCount_AndOneHitIsJustTheRoot()
        {
            var four = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling, 4, false, Seed, 0);
            var one = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling, 1, false, Seed, 0);
            var none = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling, 0, false, Seed, 0);

            Assert.That(four.Length, Is.EqualTo(4));
            Assert.That(one.Length, Is.EqualTo(1));
            Assert.That((int)one[0].NoteNumber,
                Is.EqualTo((int)DwmNote.Get(DwmNoteName.C, 2).NoteNumber),
                "a 1-hit event degenerates to the root anchor");
            Assert.That(none, Is.Empty);
        }

        [Test]
        public void WalkLine_MiddleNotes_AreChordTones()
        {
            var line = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling,
                hitCount: 6, descendBias: false, Seed, eventIndex: 1);

            var chordPcInts = CMajorPcs.Select(p => (int)p).ToArray();
            for (int k = 1; k <= line.Length - 2; k++)
            {
                int pc = (int)line[k].NoteNumber % 12;
                Assert.That(chordPcInts, Does.Contain(pc),
                    $"middle hit {k} must be a chord tone (D-W2-VOCAB=B)");
            }
        }

        [Test]
        public void WalkLine_LastNote_ApproachesTheNextRoot()
        {
            // The approach note sits a semitone or a whole step from the NEXT
            // root's pitch class (±1 / ±2 by construction — never 0: the walk
            // approaches the root, it does not land on it early).
            for (int e = 0; e < 8; e++)
            {
                var line = BassTrackComposer.BuildWalkLine(
                    CMajorPcs, DwmNoteName.F, 2, GenerousCeiling,
                    hitCount: 4, descendBias: false, Seed, eventIndex: e);
                int last = (int)line[line.Length - 1].NoteNumber;
                Assert.That(PcDistance(last, DwmNoteName.F),
                    Is.EqualTo(1).Or.EqualTo(2),
                    $"event {e}: the last hit approaches the next root " +
                    "chromatically or by whole step (D-W2-VOCAB=B)");
            }
        }

        [Test]
        public void WalkLine_AdjacentNotes_NeverRepeat_UnderAGenerousCeiling()
        {
            // Middles exclude the previous pitch and the approach never
            // re-strikes it; under a tight ceiling a fold MAY land on the
            // previous pitch (ceiling wins over variety — documented), hence
            // the generous ceiling here.
            var line = BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling,
                hitCount: 6, descendBias: false, Seed, eventIndex: 2);

            for (int k = 1; k < line.Length; k++)
                Assert.That((int)line[k].NoteNumber,
                    Is.Not.EqualTo((int)line[k - 1].NoteNumber),
                    $"hits {k - 1} and {k} must differ");
        }

        [Test]
        public void WalkLine_VariesAcrossEvents()
        {
            // The WALK-2 ask itself: the same chord on a later event walks a
            // different line. The mix is deterministic, so this asserts an
            // EXISTS over 8 event indices (all-equal is astronomically
            // unlikely for this seed; if the algorithm's thresholds ever
            // change and this fails, bump the seed — the property under test
            // is bar-to-bar variation, not a specific golden).
            var first = Numbers(BassTrackComposer.BuildWalkLine(
                CMajorPcs, DwmNoteName.F, 2, GenerousCeiling, 4, false,
                Seed, eventIndex: 0));

            bool anyDiffers = Enumerable.Range(1, 8).Any(e =>
                !Numbers(BassTrackComposer.BuildWalkLine(
                    CMajorPcs, DwmNoteName.F, 2, GenerousCeiling, 4, false,
                    Seed, e)).SequenceEqual(first));

            Assert.That(anyDiffers, Is.True,
                "an improvised walk must vary between events (D-W2-RNG=B)");
        }

        [Test]
        public void WalkLine_HonoursTheCeiling()
        {
            // Ceiling E2 = 40: the root C2 = 36 fits; every planned note above
            // folds -12 (per-note D-W2-REG). Nothing emitted above 40, ever.
            const int ceiling = 40;
            for (int e = 0; e < 4; e++)
            {
                var line = BassTrackComposer.BuildWalkLine(
                    CMajorPcs, DwmNoteName.F, 2, ceiling,
                    hitCount: 6, descendBias: false, Seed, e);
                foreach (var n in line)
                    Assert.That((int)n.NoteNumber,
                        Is.LessThanOrEqualTo(ceiling).And.GreaterThanOrEqualTo(0),
                        $"event {e}: every walk note obeys the D-REG-1=C ceiling");
            }
        }

        [Test]
        public void NearestPitch_PicksTheClosestOctave_TiesBreakLow()
        {
            Assert.That(BassTrackComposer.NearestPitch(DwmNoteName.C, 36),
                Is.EqualTo(36), "exact class at the reference");
            Assert.That(BassTrackComposer.NearestPitch(DwmNoteName.B, 36),
                Is.EqualTo(35), "B below C is nearer than B above");
            Assert.That(BassTrackComposer.NearestPitch(DwmNoteName.FSharp, 36),
                Is.EqualTo(30), "the tritone tie breaks LOW (it is a bass)");
        }

        // ==================================================================
        // Card surface (D-W2-SURF=A)
        // ==================================================================

        [Test]
        public void CardSurface_ImprovisedWalkIsValueTwo_DefaultUnchanged()
        {
            Assert.That(
                (int)BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk,
                Is.EqualTo(2), "append-only: values serialized, never renumbered");

            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            Assert.That(card.arpeggioToneMode,
                Is.EqualTo(BasslineCardConfigSO.BassArpeggioToneMode.RepeatedNote),
                "the default tone mode is untouched (BC surface)");
        }

        // ==================================================================
        // Orchestrator-level gates (Dbg1Fixtures + FNV idiom)
        // ==================================================================

        private static BasslineCardConfigSO Card(
            ChordExpressionType expression,
            BasslineCardConfigSO.BassArpeggioToneMode mode,
            BasslineCardConfigSO.PocketCouplingMode pocket =
                BasslineCardConfigSO.PocketCouplingMode.Off)
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.chordExpression = expression;
            c.arpeggioToneMode = mode;
            c.pocketMode = pocket;
            return c;
        }

        [Test]
        public void ImprovisedWalk_WithANonArpeggioFigure_IsByteInert()
        {
            // The walk reads the ARPEGGIO figures only (same gate as WALK-1).
            // With Block, ImprovisedWalk must render byte-identically to
            // RepeatedNote — the mode's inertness half of the BC gate.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("W2Prog",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            ulong Render(BasslineCardConfigSO.BassArpeggioToneMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog,
                        style: Card(ChordExpressionType.Block, mode)));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            Assert.That(
                Render(BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk),
                Is.EqualTo(
                    Render(BasslineCardConfigSO.BassArpeggioToneMode.RepeatedNote)),
                "ImprovisedWalk under a non-arpeggio figure is byte-inert");
        }

        [Test]
        public void ImprovisedWalk_Engaged_ChangesTheRender_AndIsDeterministic()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("W2Prog",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            ulong Render(BasslineCardConfigSO.BassArpeggioToneMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog,
                        style: Card(ChordExpressionType.ArpeggioUp, mode)));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            var repeated = Render(
                BasslineCardConfigSO.BassArpeggioToneMode.RepeatedNote);
            var improv1 = Render(
                BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk);
            var improv2 = Render(
                BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk);

            Assert.That(improv1, Is.Not.EqualTo(repeated),
                "engaged, the walk plays chord tones and approach notes — " +
                "structurally different pitches from the repeated-note pulse");
            Assert.That(improv1, Is.EqualTo(improv2),
                "determinism: same seed + same config => same bytes");
        }

        [Test]
        public void ImprovisedWalk_Engaged_StaysUnderTheRegisterCeiling()
        {
            // Tight asset (octaveMin 3, octaveMax 4): ceiling B3 = 59
            // (D-REG-1=C). Approach notes may dip BELOW the band floor
            // (accepted, low is safe) — the governed bound is the ceiling.
            //
            // Audited on the BASS STEM, not on PartRender.merged: the merge
            // carries the metronome click (D5 = 74 / D#5 = 75), which is not
            // a composer note (the B3 authoring finding).
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            inst.octaveMin = 3;
            inst.octaveMax = 4;
            var prog = Dbg1Fixtures.Progression("W2RegProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Subdominant, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major),
                (ScaleDegree.Submediant, ChordQuality.Minor));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                    pattern: prog,
                    style: Card(ChordExpressionType.ArpeggioUp,
                        BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk)));
            var render = Dbg1Fixtures.Render(orch, part, null, seed: 7);

            var bassKey = new MusicianTrackKey(
                Dbg1Fixtures.Musician, TrackRole.Bassline);
            Assert.That(render.stemsByMusician.ContainsKey(bassKey), Is.True);
            var notes = render.stemsByMusician[bassKey].GetNotes().ToList();

            int ceiling = BassTrackComposer.ResolveRegisterCeiling(4); // 59

            Assert.That(notes, Is.Not.Empty);
            foreach (var n in notes)
                Assert.That((int)n.NoteNumber, Is.LessThanOrEqualTo(ceiling),
                    "every walk note honours the D-REG-1=C ceiling end to end");
        }

        [Test]
        public void ImprovisedWalk_UnderFullPocketCoverage_IsByteIdenticalToChordToneWalk()
        {
            // D-W2-POCKET=A pinned structurally: the fixture drum pattern has
            // a kick on beats 1 and 3, and the 2-chord progression makes the
            // event windows [0,2) and [2,4) — BOTH pocketed. With every event
            // substituted, arpeggioToneMode never participates (§3.7), so the
            // two walk modes must render byte-identically.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);
            var inst = Dbg1Fixtures.Instrument();
            var kit = Dbg1Fixtures.Kit();
            var prog = Dbg1Fixtures.Progression("W2PocketProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var drums = Dbg1Fixtures.DrumPattern("W2PocketDrums");

            ulong Render(BasslineCardConfigSO.BassArpeggioToneMode mode)
            {
                var part = Dbg1Fixtures.Part(
                    new SongConfig.PartConfig.TrackConfig
                    {
                        Role = TrackRole.Rhythm,
                        MusicianId = Dbg1Fixtures.Musician,
                        PercussionInstrument = kit,
                        Parameters = new TrackParameters { Pattern = drums },
                    },
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                        pattern: prog,
                        style: Card(ChordExpressionType.ArpeggioUp, mode,
                            BasslineCardConfigSO.PocketCouplingMode.SlapPocket)));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            Assert.That(
                Render(BasslineCardConfigSO.BassArpeggioToneMode.ImprovisedWalk),
                Is.EqualTo(
                    Render(BasslineCardConfigSO.BassArpeggioToneMode.ChordToneWalk)),
                "under full pocket coverage the walk mode is unreachable — " +
                "the pocket substitution wins per event (§3.7 verbatim)");
        }
    }
}
#endif
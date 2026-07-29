#if UNITY_EDITOR
// B3 BASS-REG-1 — the bass register, settled in one batch.
//
// Decisions covered:
//   D-REG-1=C  - MIDIInstrumentSO.octaveMax is honoured on BOTH surfaces: it
//                caps the §2 draw band AND is a hard ceiling on everything
//                emitted above the drawn note (walk tops, pops).
//   D-REG-2=B  - a pop that would exceed the ceiling FOLDS back onto the
//                selected note. Pitch only: pop identity (classification,
//                popBoost, pop-wins dedupe, gate) is decided upstream in
//                BuildPocketPlan, which never sees the fold.
//   D-REG-3=B  - a walk voicing whose top exceeds the ceiling folds down a
//                WHOLE octave (shape, intervals, pitch-class order and strict
//                ascent preserved). The ceiling wins over the band floor; the
//                only stop is the MIDI floor itself.
//   D-REG-4=B  - the §2 band narrows from three octaves to two:
//                DryWetMidi octaveMin-1 .. min(octaveMin, octaveMax-1)
//                (authored octaveMin .. octaveMin+1). The -1 is the
//                authored→DryWetMidi conversion, NOT "below the declared min".
//                The draw keeps its per-event count and order; only its RANGE
//                changed, which remaps same-seed octaves — the batch's
//                declared render-affecting change (BC gate reinterpreted: it
//                binds future WALK-2 surface additions, not this batch's
//                register decisions).
//
// Discipline: register behavior is pinned at PURE seams (ResolveOctaveBand,
// ResolveRegisterCeiling, ResolvePopNote, the 3-arg BuildWalkVoicing) plus two
// orchestrator-level gates in the Dbg1Fixtures + FNV idiom. The pre-B3 WALK-1
// pins in BassTrackComposer_ArticulationTests keep running unmodified against
// the ceiling-free 2-arg BuildWalkVoicing, which is byte-identical by
// construction (delegation with ceiling = int.MaxValue).
//
// See runtime/SSoT_Composer_Bass_Track.md §2 / §3.6 / §3.7 and
// planning/Roadmap_Chord_Articulation.md (B3).

using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Linq;
using static MidiGenPlay.MusicTheory.MusicTheory;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_RegisterTests
    {
        private static readonly DwmNoteName[] CMajorPcs =
            { DwmNoteName.C, DwmNoteName.E, DwmNoteName.G };
        private static readonly DwmNoteName[] AMinorPcs =
            { DwmNoteName.A, DwmNoteName.C, DwmNoteName.E };

        // ==================================================================
        // ResolveOctaveBand (D-REG-4=B / D-REG-1=C, band surface)
        // ==================================================================

        [Test]
        public void Band_IsTwoOctaves_WhenTheCeilingIsGenerous()
        {
            // Authored octaveMin..octaveMin+1, i.e. DryWetMidi
            // octaveMin-1..octaveMin. The old three-octave top
            // (octaveMin+1) is GONE.
            Assert.That(BassTrackComposer.ResolveOctaveBand(3, 5),
                Is.EqualTo((2, 3)), "the Dbg1Fixtures instrument's band");
            Assert.That(BassTrackComposer.ResolveOctaveBand(2, 9),
                Is.EqualTo((1, 2)), "default-octaveMax asset");
        }

        [Test]
        public void Band_CeilingCapsTheTop()
        {
            // octaveMax == octaveMin: only the conversion octave of the
            // declared min remains.
            Assert.That(BassTrackComposer.ResolveOctaveBand(3, 3),
                Is.EqualTo((2, 2)));
        }

        [Test]
        public void Band_DegenerateAsset_CollapsesToOneOctave_NeverInverts()
        {
            Assert.That(BassTrackComposer.ResolveOctaveBand(3, 2),
                Is.EqualTo((2, 2)), "octaveMax below octaveMin: collapse");
            Assert.That(BassTrackComposer.ResolveOctaveBand(0, 0),
                Is.EqualTo((0, 0)), "floor clamp keeps min >= 0 and max >= min");
        }

        [Test]
        public void Band_DrawRangeIsValidForRngNext()
        {
            // rng.Next(minOct, maxOct + 1) needs min <= max for every input.
            foreach (var (lo, hi) in new[] { (0, 0), (1, 9), (3, 5), (3, 3), (5, 1) })
            {
                var (minOct, maxOct) = BassTrackComposer.ResolveOctaveBand(lo, hi);
                Assert.That(maxOct, Is.GreaterThanOrEqualTo(minOct),
                    $"({lo},{hi}) must yield a non-inverted band");
                Assert.That(minOct, Is.GreaterThanOrEqualTo(0));
            }
        }

        // ==================================================================
        // ResolveRegisterCeiling (D-REG-1=C, emission surface)
        // ==================================================================

        [Test]
        public void Ceiling_IsBAtTheTopOfTheDeclaredRegister()
        {
            // B at DryWetMidi octave octaveMax-1 = note number octaveMax*12+11.
            Assert.That(BassTrackComposer.ResolveRegisterCeiling(5),
                Is.EqualTo((int)DwmNote.Get(DwmNoteName.B, 4).NoteNumber)
                    .And.EqualTo(71), "the Dbg1Fixtures instrument's ceiling");
            Assert.That(BassTrackComposer.ResolveRegisterCeiling(9),
                Is.EqualTo(119), "default-octaveMax asset");
        }

        [Test]
        public void Ceiling_ClampsToTheMidiRange()
        {
            Assert.That(BassTrackComposer.ResolveRegisterCeiling(11),
                Is.EqualTo(127), "absurd asset: never above the MIDI ceiling");
        }

        // ==================================================================
        // ResolvePopNote (D-REG-2=B)
        // ==================================================================

        [Test]
        public void Pop_IsPlusTwelve_WhenItFitsTheCeiling()
        {
            var pop = BassTrackComposer.ResolvePopNote(DwmNoteName.C, 3, ceiling: 71);
            Assert.That((int)pop.NoteNumber,
                Is.EqualTo((int)DwmNote.Get(DwmNoteName.C, 4).NoteNumber),
                "under the ceiling the POCKET-1 +12 gesture is untouched");
        }

        [Test]
        public void Pop_FoldsOntoTheSelectedNote_WhenPlusTwelveExceedsTheCeiling()
        {
            // Selected B4 = 71; +12 = 83 > 71 => the pop pitch IS the
            // selected note. Only pitch folds — identity is upstream.
            var pop = BassTrackComposer.ResolvePopNote(DwmNoteName.B, 4, ceiling: 71);
            Assert.That(pop, Is.EqualTo(DwmNote.Get(DwmNoteName.B, 4)),
                "fold target is the selected note, verbatim");
        }

        [Test]
        public void Pop_FoldsAtTheMidiRange_EvenWithAGenerousCeiling()
        {
            // Latent pre-B3 hazard closed: +12 above MIDI 127 must fold, not
            // throw inside DwmNote.Get.
            var pop = BassTrackComposer.ResolvePopNote(
                DwmNoteName.B, 8, ceiling: int.MaxValue); // B8=119, +12=131
            Assert.That((int)pop.NoteNumber, Is.EqualTo(119));
        }

        [Test]
        public void Pop_ExactlyAtTheCeiling_StillFires()
        {
            // popNumber > ceiling folds; popNumber == ceiling does not.
            var pop = BassTrackComposer.ResolvePopNote(DwmNoteName.B, 3, ceiling: 71);
            Assert.That((int)pop.NoteNumber, Is.EqualTo(71),
                "B3+12 = B4 = 71 = ceiling: fits");
        }

        // ==================================================================
        // BuildWalkVoicing, 3-arg (D-REG-3=B)
        // ==================================================================

        [Test]
        public void Walk_TwoArgOverload_IsCeilingFree_AndUnchangedFromWalk1()
        {
            // The pre-B3 pins run against this form; pin its equivalence to
            // ceiling = int.MaxValue and its exact WALK-1 values once here.
            var legacy = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2);
            var viaMax = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2, int.MaxValue);

            Assert.That(legacy.Select(n => (int)n.NoteNumber),
                Is.EqualTo(viaMax.Select(n => (int)n.NoteNumber)));
            Assert.That(legacy.Select(n => (int)n.NoteNumber),
                Is.EqualTo(new[] { 36, 40, 43 }), "C2/E2/G2 — WALK-1 verbatim");
        }

        [Test]
        public void Walk_TopAboveCeiling_FoldsTheWholeVoicingDownOneOctave()
        {
            // C2/E2/G2 top = 43; ceiling 42 => the WHOLE stack transposes -12.
            var v = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2, ceiling: 42);
            Assert.That(v.Select(n => (int)n.NoteNumber),
                Is.EqualTo(new[] { 24, 28, 31 }),
                "whole-voicing fold: shape and intervals intact, -12");
        }

        [Test]
        public void Walk_Fold_PreservesWrapLiftAndStrictAscent()
        {
            // A minor is the wrapping case (C and E below A in pc order).
            // A2/C3/E3 = 45/48/52; ceiling 51 => 33/36/40.
            var v = BassTrackComposer.BuildWalkVoicing(AMinorPcs, 2, ceiling: 51);

            Assert.That(v.Select(n => (int)n.NoteNumber),
                Is.EqualTo(new[] { 33, 36, 40 }));
            Assert.That(v.Select(n => n.NoteName), Is.EqualTo(new[]
                { DwmNoteName.A, DwmNoteName.C, DwmNoteName.E }),
                "pitch-class order keeps chordPcs order (root first)");
            for (int i = 1; i < v.Length; i++)
                Assert.That((int)v[i].NoteNumber,
                    Is.GreaterThan((int)v[i - 1].NoteNumber),
                    "strict ascent survives the fold");
        }

        [Test]
        public void Walk_ExactlyAtTheCeiling_DoesNotFold()
        {
            var v = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2, ceiling: 43);
            Assert.That(v.Select(n => (int)n.NoteNumber),
                Is.EqualTo(new[] { 36, 40, 43 }), "top == ceiling: fits");
        }

        [Test]
        public void Walk_FoldStopsAtTheMidiFloor_NeverThrows()
        {
            // Root C0 = 12 allows exactly one fold (to 0); an impossible
            // ceiling then STOPS rather than folding below note 0. The
            // ceiling-wins rule has a floor: the MIDI range itself.
            var v = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 0, ceiling: 0);
            Assert.That(v.Select(n => (int)n.NoteNumber),
                Is.EqualTo(new[] { 0, 4, 7 }),
                "one fold applied; further folding is impossible and skipped");
        }

        [Test]
        public void Walk_CeilingForm_IsPure()
        {
            var a = BassTrackComposer.BuildWalkVoicing(AMinorPcs, 2, 51);
            var b = BassTrackComposer.BuildWalkVoicing(AMinorPcs, 2, 51);
            Assert.That(a.Select(n => (int)n.NoteNumber),
                Is.EqualTo(b.Select(n => (int)n.NoteNumber)));
        }

        // ==================================================================
        // Orchestrator-level gates (Dbg1Fixtures + FNV idiom)
        // ==================================================================

        [Test]
        public void Bass_AllEmittedNotes_StayWithinTheDeclaredRegister()
        {
            // Tight asset (octaveMin 3, octaveMax 4): band 2..3 DryWetMidi,
            // ceiling B3 = 59. Default Block figure => only drawn notes are
            // emitted; every one must sit inside [C at minOct, ceiling].
            //
            // Audited on the BASS STEM, not on PartRender.merged: the merge
            // also carries the orchestrator's metronome click (D5 = 74 /
            // D#5 = 75 on MidiGenerator.MetronomeChannel, stamped by
            // GenerateMetronomeTrackFile), which is not a composer note and
            // has no business in a register assertion. stemsByMusician is
            // keyed on the (musicianId, role) chunk tag, so the bass is
            // isolated exactly.
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            inst.octaveMin = 3;
            inst.octaveMax = 4;
            var prog = Dbg1Fixtures.Progression("RegProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Subdominant, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major),
                (ScaleDegree.Submediant, ChordQuality.Minor));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst, pattern: prog));
            var render = Dbg1Fixtures.Render(orch, part, null, seed: 7);

            var bassKey = new MusicianTrackKey(
                Dbg1Fixtures.Musician, TrackRole.Bassline);
            Assert.That(render.stemsByMusician.ContainsKey(bassKey), Is.True,
                "the bass stem must exist for a single-bass part");
            var notes = render.stemsByMusician[bassKey].GetNotes().ToList();

            int floor = (int)DwmNote.Get(DwmNoteName.C, 2).NoteNumber; // 36
            int ceiling = BassTrackComposer.ResolveRegisterCeiling(4);  // 59

            Assert.That(notes, Is.Not.Empty);
            foreach (var n in notes)
                Assert.That((int)n.NoteNumber,
                    Is.InRange(floor, ceiling),
                    "the register band and ceiling are honoured end to end");
        }

        [Test]
        public void Bass_OctaveMaxAboveTheBand_IsInertOnTheDraw()
        {
            // D-REG-1=C caps, it does not widen: once octaveMax clears the
            // band top, its exact value cannot influence the render (the bass
            // must NOT silently adopt the chord/melody full-range band).
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var prog = Dbg1Fixtures.Progression("InertProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            ulong Render(int octaveMax)
            {
                var inst = Dbg1Fixtures.Instrument();
                inst.octaveMin = 3;
                inst.octaveMax = octaveMax;
                var part = Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Bassline, inst, pattern: prog));
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, null, seed: 7).merged);
            }

            Assert.That(Render(octaveMax: 5), Is.EqualTo(Render(octaveMax: 9)),
                "any octaveMax above the band top is byte-inert");
        }
    }
}
#endif
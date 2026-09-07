#if UNITY_EDITOR
// MGP-MEL-DEGRADE-1 -- ConstrainedMelodyStrategy chromatic-motif contract.
//
// Pins the resolution of F-MEL-DEGRADE-1: TransposeByScaleDegrees used to
// hand a value counted in DEGREES to a SEMITONE shift whenever the motif note
// was absent from degreeLookup. Traced from tonality matrix cell #157
// (Ionian / FourFour / Prog_Min_Napolitana_bII / tracks=M): motif D#5 -- a
// chord tone of the Neapolitan bII [G# C D#] and therefore chromatic in
// G Ionian -- with transposeSemitones = 2 emitted 63 + 2 = 65 = F5, in
// neither the scale nor the chord.
//
// Decisions pinned here:
//   D-DEGRADE-1 = A. A chromatic motif note is snapped to a scale degree and
//     then sequenced DIATONICALLY, so the echo is in-scale by construction.
//     This is the tonal (diatonic) sequence the ScaleDegrees mode names;
//     ChromaticSemitones remains the real (exact-interval) sequence and is
//     untouched -- see ConstrainedMelodyStrategy_MotifTests.
//   D-DEGRADE-2 = floor. On a tie the snap goes to the degree AT OR BELOW the
//     source, keeping the mapping monotone (the motif's contour can never
//     invert) and reproducing the b6 -> 5 / b2 -> 1 resolutions of the
//     borrowed-chord family that actually reaches this path.
//   D-DEGRADE-3 = identity. With no usable scale, "move N degrees" is
//     undefined and the directive degrades to the exact ostinato that
//     transposeSemitones == 0 already means -- NOT to a raw semitone shift.
//
// Fixture is G Ionian on purpose: it is the tonality of both cell #157 and the
// composition smoke anchor, so the numbers here are the numbers in the
// evidence. Determinism: the replay path consumes no rng, so every assertion
// is exact.

using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Tests.Editor
{
    public class ConstrainedMelodyStrategy_ChromaticMotifTests
    {
        /// <summary>Fake inner strategy: returns a scripted note sequence in
        /// order (null entries = rests).</summary>
        private sealed class ScriptedStrategy : IMelodyStrategy
        {
            private readonly Queue<DwmNote> _script;

            public ScriptedStrategy(params DwmNote[] notes)
            {
                _script = new Queue<DwmNote>(notes);
            }

            public DwmNote PickNext(
                NoteName[] chordPitchClasses, NoteName[] scalePitchClasses,
                Dictionary<NoteName, int> degreeLookup, DwmNote lastMelody,
                MIDIInstrumentSO instrument, MelodicLeadingConfig cfg,
                System.Random rng, PhrasePlanner.PhraseState phrase,
                TonalityProfileSO profile, MelodyPartState part,
                HashSet<int> allowedDegrees)
            {
                return _script.Count > 0 ? _script.Dequeue() : null;
            }
        }

        // --- fixture: G Ionian (G A B C D E F#), Neapolitan bII = [G# C D#] ---

        private static readonly NoteName[] GIonianScale =
            { NoteName.G, NoteName.A, NoteName.B, NoteName.C,
              NoteName.D, NoteName.E, NoteName.FSharp };

        private static readonly NoteName[] NeapolitanChord =
            { NoteName.GSharp, NoteName.C, NoteName.DSharp };

        private MIDIInstrumentSO _inst;
        private MelodicLeadingConfig _cfg;
        private Dictionary<NoteName, int> _degrees;

        [SetUp]
        public void SetUp()
        {
            _inst = ScriptableObject.CreateInstance<MIDIInstrumentSO>();
            _inst.octaveMin = 3;
            _inst.octaveMax = 6;

            _cfg = ScriptableObject.CreateInstance<MelodicLeadingConfig>();
            _cfg.noteSource =
                MelodicLeadingConfig.NoteSource.PreferChordTonesAllowScale;

            _degrees = new Dictionary<NoteName, int>();
            for (int i = 0; i < GIonianScale.Length; i++)
                _degrees[GIonianScale[i]] = i;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inst);
            Object.DestroyImmediate(_cfg);
        }

        /// <summary>Standard pick: full G Ionian fixture.</summary>
        private DwmNote Pick(ConstrainedMelodyStrategy sut, DwmNote last)
            => PickWith(sut, last, GIonianScale, _degrees);

        /// <summary>Pick with EXPLICIT scale metadata, so a null argument
        /// really reaches the composer as null. (A `scale ?? GIonianScale`
        /// default would silently substitute the fixture and make the
        /// D-DEGRADE-3 tests assert the diatonic path instead.)</summary>
        private DwmNote PickWith(
            ConstrainedMelodyStrategy sut,
            DwmNote last,
            NoteName[] scale,
            Dictionary<NoteName, int> degrees)
        {
            var phrase = new PhrasePlanner.PhraseState
            {
                PhraseStartNote = null,
                PhrasePeakNote = null,
            };
            return sut.PickNext(
                NeapolitanChord, scale, degrees,
                last, _inst, _cfg,
                new System.Random(1), phrase, null, default, null);
        }

        private static RepeatLastNotesDirective ScaleDegreeRepeat(
            int n, int steps) =>
            new RepeatLastNotesDirective
            {
                enabled = true,
                notesToRepeat = n,
                transposeMode =
                    RepeatLastNotesDirective.MotifTransposeMode.ScaleDegrees,
                transposeSemitones = steps,
            };

        // ---- D-DEGRADE-1 = A: the cell #157 case ----

        [Test]
        public void ChromaticMotifNote_ScaleDegrees_EchoesInScale_NotTheOldF5()
        {
            // Motif = the single chromatic chord tone D#5 (bII's third).
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.DSharp, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 2));

            var motif = Pick(sut, null);
            var echo = Pick(sut, motif);

            Assert.That(motif, Is.EqualTo(DwmNote.Get(NoteName.DSharp, 5)),
                "the motif STATEMENT keeps its chromatic colour; only the " +
                "echo is diatonicised");

            // D# snaps down to D (degree 4), +2 degrees => F# (degree 6):
            // 63 - 1 + 4 = 66. The pre-fix degrade emitted 63 + 2 = 65 = F5,
            // which is in neither the G Ionian scale nor [G# C D#].
            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.FSharp, 5)),
                "the echo must be diatonic; F5 was F-MEL-DEGRADE-1");
            Assert.That(System.Array.IndexOf(GIonianScale, echo.NoteName),
                Is.GreaterThanOrEqualTo(0),
                "in-scale by construction");
        }

        [Test]
        public void ChromaticMotifNote_AccumulatesDiatonicallyAcrossCycles()
        {
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.DSharp, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 2));

            var picks = new List<DwmNote>();
            for (int i = 0; i < 3; i++)
                picks.Add(Pick(sut, i == 0 ? null : picks[i - 1]));

            // cycle 1: D -> F# (deg 4 -> 6). cycle 2: D -> A an octave up
            // (deg 4 -> 8 => deg 1 + 1 octave). Both diatonic.
            Assert.That(picks, Is.EqualTo(new[]
            {
                DwmNote.Get(NoteName.DSharp, 5),
                DwmNote.Get(NoteName.FSharp, 5),
                DwmNote.Get(NoteName.A, 5),
            }));
        }

        // ---- D-DEGRADE-2 = floor ----

        [Test]
        public void ChromaticMotifNote_EquidistantBetweenDegrees_SnapsDown()
        {
            // A# sits exactly one semitone from A (degree 1) and from B
            // (degree 2). Floor picks A, so +1 degree lands on B.
            // Ceiling would pick B and land on C -- this pins the tie-break.
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.ASharp, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 1));

            Pick(sut, null);
            var echo = Pick(sut, DwmNote.Get(NoteName.ASharp, 4));

            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.B, 4)));
        }

        // ---- D-DEGRADE-3 = identity ----

        [Test]
        public void NoUsableScale_DegradesToExactOstinato_NotASemitoneShift()
        {
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.D, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 2));

            PickWith(sut, null, scale: null, degrees: null);
            var echo = PickWith(sut, DwmNote.Get(NoteName.D, 5),
                                scale: null, degrees: null);

            // Pre-fix this returned Transpose(D5, 2) = E5 -- a degree count
            // spent as semitones. With no scale the honest answer is no move.
            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.D, 5)));
        }

        [Test]
        public void NullDegreeLookup_DegradesToExactOstinato()
        {
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.D, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 3));

            PickWith(sut, null, GIonianScale, degrees: null);
            var echo = PickWith(sut, DwmNote.Get(NoteName.D, 5),
                                GIonianScale, degrees: null);

            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.D, 5)));
        }

        // ---- regression guard: the DIATONIC case is unchanged ----

        [Test]
        public void DiatonicMotifNote_SequencesExactlyAsBeforeTheFix()
        {
            // D5 is degree 4 of G Ionian; +2 degrees => F#5. This is the
            // MGP-TONALITY-1 D-TON6=A behaviour and MUST NOT move.
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.D, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: 2));

            Pick(sut, null);
            var echo = Pick(sut, DwmNote.Get(NoteName.D, 5));

            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.FSharp, 5)));
        }

        [Test]
        public void DescendingSteps_CrossOctaveDownwards_StayDiatonic()
        {
            // A4 is degree 1; -5 degrees => degree 3 (C) one octave down.
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.A, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                ScaleDegreeRepeat(n: 1, steps: -5));

            Pick(sut, null);
            var echo = Pick(sut, DwmNote.Get(NoteName.A, 4));

            Assert.That(echo, Is.EqualTo(DwmNote.Get(NoteName.C, 4)));
        }
    }
}
#endif
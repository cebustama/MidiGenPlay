#if UNITY_EDITOR
// MGP-MEL-1 (F1/F2/F3) -- ConstrainedMelodyStrategy contract tests.
//
// Pins the decorator seams fixed in this batch, without any composer or asset
// fixtures beyond ScriptableObject.CreateInstance (mirrors the fixture-light
// idiom of ChordTrackComposer_ArticulationTests):
//
//   1. INTENT (F1 defensive re-check): a non-null but DISABLED
//      RepeatLastNotesDirective never hijacks the phrase -- every slot
//      delegates to the inner strategy. (The composer-side gate is the
//      primary F1 fix; this pins the decorator's own contract so direct
//      constructions behave identically.)
//   2. MOTIF (F2, D8=B): with an enabled directive, the first N audible
//      picks form the motif; subsequent slots cycle it, transposed by
//      transposeSemitones once per completed cycle (0 => exact ostinato;
//      +k => classic sequence). Rests never enter the motif.
//   3. CONTOUR (F3, D9): AscendingOnly snaps a below-reference pick to the
//      NEAREST pool candidate strictly above the phrase reference --
//      scale-aware, never chromatic; when no candidate exists above
//      (range edge), the inner pick is kept unchanged.
//
// Determinism: the fake inner strategy consumes no rng; the decorator's
// replay path consumes none either, so every assertion is exact.

using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Tests.Editor
{
    public class ConstrainedMelodyStrategy_MotifTests
    {
        /// <summary>Fake inner strategy: returns a scripted note sequence in
        /// order (null entries = rests). Records how many times it was
        /// consulted so hijack/delegation can be asserted.</summary>
        private sealed class ScriptedStrategy : IMelodyStrategy
        {
            private readonly Queue<DwmNote> _script;
            public int Consultations { get; private set; }

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
                Consultations++;
                return _script.Count > 0 ? _script.Dequeue() : null;
            }
        }

        // --- shared fixture (C Ionian, generic mid-range instrument) ---

        private static readonly NoteName[] CMajChord =
            { NoteName.C, NoteName.E, NoteName.G };
        private static readonly NoteName[] CIonianScale =
            { NoteName.C, NoteName.D, NoteName.E, NoteName.F,
              NoteName.G, NoteName.A, NoteName.B };

        private MIDIInstrumentSO _inst;
        private MelodicLeadingConfig _cfg;
        private Dictionary<NoteName, int> _degrees;

        [SetUp]
        public void SetUp()
        {
            _inst = ScriptableObject.CreateInstance<MIDIInstrumentSO>();
            _inst.octaveMin = 3;
            _inst.octaveMax = 5;

            _cfg = ScriptableObject.CreateInstance<MelodicLeadingConfig>();
            _cfg.noteSource =
                MelodicLeadingConfig.NoteSource.PreferChordTonesAllowScale;

            _degrees = new Dictionary<NoteName, int>();
            for (int i = 0; i < CIonianScale.Length; i++)
                _degrees[CIonianScale[i]] = i;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inst);
            Object.DestroyImmediate(_cfg);
        }

        private DwmNote Pick(
            ConstrainedMelodyStrategy sut,
            DwmNote last,
            DwmNote phraseStart = null,
            DwmNote phrasePeak = null)
        {
            var phrase = new PhrasePlanner.PhraseState
            {
                PhraseStartNote = phraseStart,
                PhrasePeakNote = phrasePeak,
            };
            return sut.PickNext(
                CMajChord, CIonianScale, _degrees, last, _inst, _cfg,
                new System.Random(1), phrase, null, default, null);
        }

        private static RepeatLastNotesDirective Repeat(
            bool enabled, int n, int transpose = 0) =>
            new RepeatLastNotesDirective
            {
                enabled = enabled,
                notesToRepeat = n,
                transposeSemitones = transpose,
            };

        // ---- 1. INTENT (F1 defensive re-check) ----

        [Test]
        public void DisabledRepeatDirective_NeverHijacks_DelegatesEverySlot()
        {
            var inner = new ScriptedStrategy(
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.D, 4),
                DwmNote.Get(NoteName.E, 4), DwmNote.Get(NoteName.F, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None, Repeat(enabled: false, n: 1));

            var picks = new List<DwmNote>
            {
                Pick(sut, null),
                Pick(sut, DwmNote.Get(NoteName.C, 4),
                     phraseStart: DwmNote.Get(NoteName.C, 4)),
                Pick(sut, DwmNote.Get(NoteName.D, 4),
                     phraseStart: DwmNote.Get(NoteName.C, 4)),
                Pick(sut, DwmNote.Get(NoteName.E, 4),
                     phraseStart: DwmNote.Get(NoteName.C, 4)),
            };

            Assert.That(inner.Consultations, Is.EqualTo(4),
                "a disabled directive must delegate every slot");
            Assert.That(picks, Is.EqualTo(new[]
            {
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.D, 4),
                DwmNote.Get(NoteName.E, 4), DwmNote.Get(NoteName.F, 4),
            }));
        }

        // ---- 2. MOTIF (F2) ----

        [Test]
        public void EnabledRepeat_TransposeZero_CyclesMotifAsExactOstinato()
        {
            var inner = new ScriptedStrategy(
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4),
                DwmNote.Get(NoteName.G, 4),
                // must never be consulted past the motif:
                DwmNote.Get(NoteName.B, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None, Repeat(enabled: true, n: 3));

            var picks = new List<DwmNote>();
            for (int i = 0; i < 9; i++)
                picks.Add(Pick(sut, i == 0 ? null : picks[i - 1]));

            Assert.That(inner.Consultations, Is.EqualTo(3),
                "the inner strategy builds the motif and is then bypassed");
            Assert.That(picks, Is.EqualTo(new[]
            {
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4),
                DwmNote.Get(NoteName.G, 4),
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4),
                DwmNote.Get(NoteName.G, 4),
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4),
                DwmNote.Get(NoteName.G, 4),
            }));
        }

        [Test]
        public void EnabledRepeat_TransposePerCycle_ProducesSequence()
        {
            var inner = new ScriptedStrategy(
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None,
                Repeat(enabled: true, n: 2, transpose: 2));

            var picks = new List<DwmNote>();
            for (int i = 0; i < 6; i++)
                picks.Add(Pick(sut, i == 0 ? null : picks[i - 1]));

            // cycle 1: +2 semitones; cycle 2: +4 semitones.
            Assert.That(picks, Is.EqualTo(new[]
            {
                DwmNote.Get(NoteName.C, 4), DwmNote.Get(NoteName.E, 4),
                DwmNote.Get(NoteName.D, 4), DwmNote.Get(NoteName.FSharp, 4),
                DwmNote.Get(NoteName.E, 4), DwmNote.Get(NoteName.GSharp, 4),
            }));
        }

        [Test]
        public void EnabledRepeat_RestsDoNotEnterTheMotif()
        {
            var inner = new ScriptedStrategy(
                DwmNote.Get(NoteName.C, 4), null /* rest */,
                DwmNote.Get(NoteName.E, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.None, Repeat(enabled: true, n: 2));

            var p0 = Pick(sut, null);
            var p1 = Pick(sut, p0);       // rest
            var p2 = Pick(sut, p0);       // completes the motif
            var p3 = Pick(sut, p2);       // first replay slot

            Assert.That(p1, Is.Null, "inner rest must pass through");
            Assert.That(p3, Is.EqualTo(DwmNote.Get(NoteName.C, 4)),
                "replay starts at motif[0]; the rest was never recorded");
            Assert.That(inner.Consultations, Is.EqualTo(3));
        }

        // ---- 3. CONTOUR (F3) ----

        [Test]
        public void AscendingOnly_SnapsBelowReferencePick_ToNearestPoolNoteAbove()
        {
            // Reference (phrase peak) = E4; inner proposes C4 (below).
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.C, 4));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.AscendingOnly, repeat: null);

            var picked = Pick(sut, DwmNote.Get(NoteName.E, 4),
                phraseStart: DwmNote.Get(NoteName.C, 4),
                phrasePeak: DwmNote.Get(NoteName.E, 4));

            // Nearest pool note strictly above E4 in C Ionian
            // (chord+scale union) is F4 -- diatonic, NOT the chromatic F#4/E#4
            // a +1-semitone nudge family could produce.
            Assert.That(picked, Is.EqualTo(DwmNote.Get(NoteName.F, 4)));
        }

        [Test]
        public void AscendingOnly_NoCandidateAbove_KeepsInnerPick()
        {
            // Reference at the very top of the instrument pool (B5 with
            // octaveMax=5): nothing strictly above => inner pick unchanged.
            var inner = new ScriptedStrategy(DwmNote.Get(NoteName.A, 5));
            var sut = new ConstrainedMelodyStrategy(
                inner, ContourConstraint.AscendingOnly, repeat: null);

            var picked = Pick(sut, DwmNote.Get(NoteName.B, 5),
                phraseStart: DwmNote.Get(NoteName.C, 4),
                phrasePeak: DwmNote.Get(NoteName.B, 5));

            Assert.That(picked, Is.EqualTo(DwmNote.Get(NoteName.A, 5)));
        }
    }
}
#endif
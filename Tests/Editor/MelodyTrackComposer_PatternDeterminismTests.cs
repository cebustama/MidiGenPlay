#if UNITY_EDITOR
// Melody Authoring MVP Phase 5 (F-A) — determinism + behavior tests for the authored
// melody pattern path.
//
// Targets the internal seam MelodyTrackComposer.ResolvePatternNotesCore (which is
// byte-identical to ComposeFromPattern's note-resolution loop) plus the public
// MelodyPatternData.SnapshotOrdered, so the tests need NO MIDIInstrumentSO / SongConfig /
// GenContext fixtures — the same idiom as ChordTrackComposer_DirectionalFirstChordTests.
// Internal visibility is granted by Runtime/AssemblyInfo.cs:
//
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]
//
// MEL-BEATUNIT-1 (2026-07-24) additionally pins the emission-site timing seam
// MelodyTrackComposer.BeatsToSpan: 4/4 stays byte-identical to the legacy
// MusicalTimeSpan.Quarter mapping, X/8 renders on the Part beat unit. Mirrors the
// bass pin Block_MonoEmit_BitIdentityHoldsPerBeatSpan_EighthDiffersFromLegacyQuarter.
//
// Covers: SnapshotOrdered ordering (startBeat -> degree -> octaveOffset) + idempotence;
// ResolvePatternNotesCore determinism (repeat-call sequence equality), tiling
// (shorter-than-Part), onset truncation (longer-than-Part), octave clamp at the band
// extremes, duration floor + velocity clamp, empty -> empty, and degree-Tonic -> root pitch.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    public class MelodyTrackComposer_PatternDeterminismTests
    {
        // C Ionian = the major scale (no chromatic surprises for Tonic/Mediant/Dominant).
        private const Tonality Key = Tonality.Ionian;
        private const NoteName Root = NoteName.C;

        // A wide instrument band: octaveMin..octaveMax = 2..6 => register band 1..5,
        // refOct = clamp((1 + 5) / 2, 1, 5) = 3.
        private const int InstOctaveMin = 2;
        private const int InstOctaveMax = 6;
        private const int ExpectedMinOct = InstOctaveMin - 1; // 1
        private const int ExpectedMaxOct = InstOctaveMax - 1; // 5
        private const int ExpectedRefOct = 3;

        // Raw struct (bypasses MelodyNoteEvent.Create so the seam's own clamping of
        // out-of-range velocity / negative duration can be exercised).
        private static MelodyPatternData.MelodyNoteEvent Ev(
            ScaleDegree degree, float startBeat, float durationBeats,
            int octaveOffset = 0, int velocity = 100) =>
            new MelodyPatternData.MelodyNoteEvent
            {
                degree = degree,
                octaveOffset = octaveOffset,
                startBeat = startBeat,
                durationBeats = durationBeats,
                velocity = velocity
            };

        private static List<MelodyTrackComposer.ResolvedMelodyNote> Resolve(
            IReadOnlyList<MelodyPatternData.MelodyNoteEvent> ordered,
            double patternTotalBeats, int partMeasures, int beatsPerBar) =>
            MelodyTrackComposer.ResolvePatternNotesCore(
                ordered, Key, Root, InstOctaveMin, InstOctaveMax,
                patternTotalBeats, partMeasures, beatsPerBar,
                MelodyTrackComposer.MinNoteBeats);

        // ---- SnapshotOrdered: deterministic read order + idempotence ----

        [Test]
        public void SnapshotOrdered_SortsByStartThenDegreeThenOctave_AndIsIdempotent()
        {
            var p = ScriptableObject.CreateInstance<MelodyPatternData>();
            // Intentionally scrambled; two share startBeat 1.0 to exercise the degree tiebreak.
            p.notes = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Dominant, 2.0f, 1f),
                Ev(ScaleDegree.Mediant,  1.0f, 1f),
                Ev(ScaleDegree.Tonic,    1.0f, 1f),
                Ev(ScaleDegree.Tonic,    0.0f, 1f),
            };

            var a = p.SnapshotOrdered();
            var b = p.SnapshotOrdered();

            // Expected order: (0,Tonic) (1,Tonic) (1,Mediant) (2,Dominant).
            Assert.That(
                a.Select(n => (n.startBeat, (int)n.degree)),
                Is.EqualTo(new[]
                {
                    (0.0f, (int)ScaleDegree.Tonic),
                    (1.0f, (int)ScaleDegree.Tonic),
                    (1.0f, (int)ScaleDegree.Mediant),
                    (2.0f, (int)ScaleDegree.Dominant),
                }));

            // Pure function of the stored list => identical sequence on repeat.
            Assert.That(ProjectEvents(a), Is.EqualTo(ProjectEvents(b)));

            UnityEngine.Object.DestroyImmediate(p);
        }

        // ---- ResolvePatternNotesCore: determinism (the byte-equality guard) ----

        [Test]
        public void ResolvePatternNotesCore_IsDeterministic_AcrossRepeatCalls()
        {
            var ordered = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Tonic,    0f, 1f,   velocity: 90),
                Ev(ScaleDegree.Mediant,  1f, 0.5f, octaveOffset: 1, velocity: 110),
                Ev(ScaleDegree.Dominant, 2f, 2f,   velocity: 70),
            };

            var first = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 2, beatsPerBar: 4);
            var second = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 2, beatsPerBar: 4);

            Assert.That(ProjectResolved(first), Is.EqualTo(ProjectResolved(second)),
                "The pattern-resolution seam must be byte-stable (RNG-free).");
        }

        // ---- Tiling: a pattern shorter than the Part repeats ----

        [Test]
        public void Resolve_ShorterThanPart_TilesAcrossRepeats()
        {
            // 1 note at beat 0; pattern loop = 4 beats; Part = 2 bars of 4/4 = 8 beats.
            // repeats = ceil(8 / 4) = 2 => onsets at beat 0 and beat 4.
            var ordered = new List<MelodyPatternData.MelodyNoteEvent> { Ev(ScaleDegree.Tonic, 0f, 1f) };

            var r = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 2, beatsPerBar: 4);

            Assert.That(r.Count, Is.EqualTo(2));
            Assert.That(r.Select(n => n.WhenBeats), Is.EqualTo(new[] { 0.0, 4.0 }));
        }

        // ---- Truncation: a pattern longer than the Part drops onsets at/after the end ----

        [Test]
        public void Resolve_LongerThanPart_TruncatesByOnset()
        {
            // Pattern loop = 8 beats; Part = 1 bar of 4/4 = 4 beats; repeats = ceil(4 / 8) = 1.
            // Onsets at 1 and 3 survive; 4 (== part end) and 5 are dropped (guard is >=).
            var ordered = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Tonic,    1f, 1f),
                Ev(ScaleDegree.Mediant,  3f, 1f),
                Ev(ScaleDegree.Dominant, 4f, 1f),
                Ev(ScaleDegree.Tonic,    5f, 1f),
            };

            var r = Resolve(ordered, patternTotalBeats: 8.0, partMeasures: 1, beatsPerBar: 4);

            Assert.That(r.Select(n => n.WhenBeats), Is.EqualTo(new[] { 1.0, 3.0 }));
        }

        // ---- Octave clamp at the band extremes ----

        [Test]
        public void Resolve_OctaveOffsetExtremes_ClampToInstrumentBand()
        {
            var ordered = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Tonic, 0f, 1f, octaveOffset: 0),
                Ev(ScaleDegree.Tonic, 1f, 1f, octaveOffset: +100),
                Ev(ScaleDegree.Tonic, 2f, 1f, octaveOffset: -100),
            };

            var r = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 1, beatsPerBar: 4);

            Assert.That(r.Count, Is.EqualTo(3));
            Assert.That(r[0].Note.Octave, Is.EqualTo(ExpectedRefOct)); // 3
            Assert.That(r[1].Note.Octave, Is.EqualTo(ExpectedMaxOct)); // clamped up to 5
            Assert.That(r[2].Note.Octave, Is.EqualTo(ExpectedMinOct)); // clamped down to 1
            Assert.That(r.All(n => n.Note.NoteName == Root), Is.True); // Tonic resolves to the root pc
        }

        // ---- Duration floor + velocity clamp ----

        [Test]
        public void Resolve_FloorsDuration_AndClampsVelocity()
        {
            var ordered = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Tonic, 0f, 0f,   velocity: 0),   // dur floored; vel clamped up to 1
                Ev(ScaleDegree.Tonic, 1f, -2f,  velocity: 200), // dur floored; vel clamped down to 127
                Ev(ScaleDegree.Tonic, 2f, 0.5f, velocity: 64),  // untouched
            };

            var r = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 1, beatsPerBar: 4);

            Assert.That(r[0].DurBeats, Is.EqualTo(MelodyTrackComposer.MinNoteBeats));
            Assert.That(r[1].DurBeats, Is.EqualTo(MelodyTrackComposer.MinNoteBeats));
            Assert.That(r[2].DurBeats, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(r[0].Velocity, Is.EqualTo(1));
            Assert.That(r[1].Velocity, Is.EqualTo(127));
            Assert.That(r[2].Velocity, Is.EqualTo(64));
        }

        // ---- Empty pattern -> empty resolution (the render path then emits silence) ----

        [Test]
        public void Resolve_EmptyPattern_ProducesNoNotes()
        {
            var r = Resolve(new List<MelodyPatternData.MelodyNoteEvent>(),
                            patternTotalBeats: 4.0, partMeasures: 4, beatsPerBar: 4);
            Assert.That(r, Is.Empty);
        }

        // ---- Degree -> pitch sanity: Tonic resolves to the tonality root ----

        [Test]
        public void Resolve_DegreeTonic_ResolvesToRootPitchClass()
        {
            var ordered = new List<MelodyPatternData.MelodyNoteEvent> { Ev(ScaleDegree.Tonic, 0f, 1f) };

            var r = Resolve(ordered, patternTotalBeats: 4.0, partMeasures: 1, beatsPerBar: 4);

            Assert.That(r.Count, Is.EqualTo(1));
            Assert.That(r[0].Note.NoteName, Is.EqualTo(Root));
            Assert.That(r[0].Note.Octave, Is.EqualTo(ExpectedRefOct));
        }

        // ---- MEL-BEATUNIT-1: the emission-site beats -> span seam ----

        // Clean multiples only: at the DryWetMidi default 96 ticks/quarter an eighth is
        // 48 ticks, so every value below lands on an exact tick in BOTH mappings and the
        // 2x relation is asserted without rounding slack.
        private static readonly double[] BeatMultipliers = { 0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 8.0 };

        private static TempoMap PinTempoMap() => TempoMap.Create(Tempo.FromBeatsPerMinute(100));

        private static long Ticks(ITimeSpan span) => TimeConverter.ConvertFrom(span, PinTempoMap());

        /// <summary>Non-regression control: in every beat-unit == 4 meter the batch is a
        /// structural identity, because GetBeatSpan returns MusicalTimeSpan.Quarter there.</summary>
        [Test]
        public void BeatsToSpan_FourFour_IsBitIdenticalToLegacyQuarter()
        {
            var beatSpan = GetBeatSpan(TimeSignature.FourFour);

            Assert.That(beatSpan, Is.EqualTo(MusicalTimeSpan.Quarter),
                "4/4 beat span must BE the legacy quarter, not merely measure the same.");

            foreach (var m in BeatMultipliers)
            {
                Assert.That(
                    Ticks(MelodyTrackComposer.BeatsToSpan(m, beatSpan)),
                    Is.EqualTo(Ticks(MusicalTimeSpan.Quarter.Multiply(m))),
                    $"4/4 must stay byte-identical to the legacy mapping at {m} beats.");
            }
        }

        /// <summary>The fix itself: in 6/8 a beat is an eighth, so the rendered span is
        /// exactly half the legacy quarter mapping -- the desync F-1 described.</summary>
        [Test]
        public void BeatsToSpan_SixEight_IsHalfTheLegacyQuarterTicks()
        {
            var beatSpan = GetBeatSpan(TimeSignature.SixEight);

            Assert.That(beatSpan, Is.EqualTo(MusicalTimeSpan.Eighth));

            foreach (var m in BeatMultipliers)
            {
                long fixedTicks = Ticks(MelodyTrackComposer.BeatsToSpan(m, beatSpan));
                long legacyTicks = Ticks(MusicalTimeSpan.Quarter.Multiply(m));

                Assert.That(fixedTicks * 2, Is.EqualTo(legacyTicks),
                    $"6/8 must render on the eighth-note beat unit at {m} beats.");
            }
        }

        /// <summary>Every supported meter maps its beat to its own beat unit.</summary>
        [Test]
        public void BeatSpan_AllTimeSignatures_MatchTheirBeatUnit()
        {
            long quarterTicks = Ticks(MusicalTimeSpan.Quarter);

            foreach (var kv in TimeSignatureProperties)
            {
                long beatTicks = Ticks(GetBeatSpan(kv.Key));
                long expected = quarterTicks * 4 / kv.Value.BeatUnit;

                Assert.That(beatTicks, Is.EqualTo(expected), $"{kv.Key}");
            }
        }

        /// <summary>Guard on the batch's boundary: the RESOLUTION seam counts beats and is
        /// meter-unit agnostic, so MEL-BEATUNIT-1 changed nothing above the emission line.</summary>
        [Test]
        public void Resolve_SixEightPart_ResolutionSeamIsUnchanged()
        {
            // 6/8 Part, 2 bars => 12 beats of window; a 6-beat loop tiles twice.
            var ordered = new List<MelodyPatternData.MelodyNoteEvent>
            {
                Ev(ScaleDegree.Tonic,   0f, 1f),
                Ev(ScaleDegree.Mediant, 5f, 1f),
            };

            var r = Resolve(ordered, patternTotalBeats: 6.0, partMeasures: 2, beatsPerBar: 6);

            Assert.That(r.Select(n => n.WhenBeats), Is.EqualTo(new[] { 0.0, 5.0, 6.0, 11.0 }));
        }

        // ---- Helpers ----

        private static List<(string, int, double, double, int)> ProjectResolved(
            IEnumerable<MelodyTrackComposer.ResolvedMelodyNote> notes) =>
            notes.Select(n => (n.Note.NoteName.ToString(), n.Note.Octave, n.WhenBeats, n.DurBeats, n.Velocity))
                 .ToList();

        private static List<(string, int, double, double, int)> ProjectEvents(
            IEnumerable<MelodyPatternData.MelodyNoteEvent> notes) =>
            notes.Select(n => (n.degree.ToString(), n.octaveOffset, (double)n.startBeat, (double)n.durationBeats, n.velocity))
                 .ToList();
    }
}
#endif
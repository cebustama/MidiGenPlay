#if UNITY_EDITOR
// Phase M2 (Roadmap_MIDI_Import.md) — pure-function tests for MelodyMidiImporter.
// All MIDI files are synthesized in memory with DryWetMidi (no file I/O, no
// Unity-editor state), keeping the suite EditMode-deterministic. Same mold as
// DrumMidiImporterTests, extended with per-note length (melody keeps duration).
//
// Grid math under test: step ticks = tpqn × (4 / beatUnit) / subdivisions.
// With tpqn=480, 4/4, subdivisions=4 → one step = 120 ticks, one beat = 480.

using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using NUnit.Framework;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Tests.Editor
{
    public class MelodyMidiImporterTests
    {
        private const short Tpqn = 480;

        // MIDI note numbers used below (middle C = C4 = 60).
        private const int C4 = 60;
        private const int Cs4 = 61;
        private const int E4 = 64;
        private const int Fs4 = 66;
        private const int G4 = 67;
        private const int B3 = 59;
        private const int A4 = 69;
        private const int C5 = 72;

        // -------------------------------------------------------------------
        // In-memory MIDI construction (mold: DrumMidiImporterTests.BuildFile,
        // + explicit note length since melody preserves duration)
        // -------------------------------------------------------------------

        private static MidiFile BuildFile(
            params (long tick, long length, int note, int velocity, int channel)[] notes)
        {
            var timed = new List<(long time, MidiEvent ev)>();
            foreach (var n in notes)
            {
                timed.Add((n.tick, new NoteOnEvent(
                    (SevenBitNumber)n.note, (SevenBitNumber)n.velocity)
                { Channel = (FourBitNumber)n.channel }));
                timed.Add((n.tick + n.length, new NoteOffEvent(
                    (SevenBitNumber)n.note, (SevenBitNumber)0)
                { Channel = (FourBitNumber)n.channel }));
            }
            timed.Sort((a, b) => a.time.CompareTo(b.time));

            var chunk = new TrackChunk();
            long prev = 0;
            foreach (var (time, ev) in timed)
            {
                ev.DeltaTime = time - prev;
                prev = time;
                chunk.Events.Add(ev);
            }

            var file = new MidiFile(chunk)
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(Tpqn)
            };
            return file;
        }

        private static MelodyMidiImporter.Options CMajorFourFour(
            int subdivisions = 4, int measures = 0, int channel = -1,
            NoteName root = NoteName.C, Tonality tonality = Tonality.Ionian,
            TimeSignature ts = TimeSignature.FourFour)
            => new MelodyMidiImporter.Options
            {
                rootNote = root,
                tonality = tonality,
                timeSignature = ts,
                subdivisions = subdivisions,
                measures = measures,
                channel = channel,
            };

        private static bool HasKind(
            MelodyMidiImporter.Result r, MelodyMidiImporter.ImportWarningKind kind)
            => r.warnings.Any(w => w.kind == kind);

        // -------------------------------------------------------------------
        // Happy path
        // -------------------------------------------------------------------

        [Test]
        public void Diatonic_FourFour_MapsDegreesTimingAndDuration()
        {
            // C4 (beat 0, 1 beat), E4 (beat 1, half beat), G4 (beat 2, 2 beats).
            var file = BuildFile(
                (0, 480, C4, 100, 0),
                (480, 240, E4, 90, 0),
                (960, 960, G4, 110, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(3, r.notes.Count);
            Assert.AreEqual(1, r.measures);   // last end = beat 4 = exactly 1 measure

            Assert.AreEqual(ScaleDegree.Tonic, r.notes[0].degree);
            Assert.AreEqual(0f, r.notes[0].startBeat);
            Assert.AreEqual(1f, r.notes[0].durationBeats);
            Assert.AreEqual(100, r.notes[0].velocity);

            Assert.AreEqual(ScaleDegree.Mediant, r.notes[1].degree);
            Assert.AreEqual(1f, r.notes[1].startBeat);
            Assert.AreEqual(0.5f, r.notes[1].durationBeats);

            Assert.AreEqual(ScaleDegree.Dominant, r.notes[2].degree);
            Assert.AreEqual(2f, r.notes[2].startBeat);
            Assert.AreEqual(2f, r.notes[2].durationBeats);

            // All in the same scale octave → all offset 0, reference echoed.
            Assert.IsTrue(r.notes.All(n => n.octaveOffset == 0));
            Assert.AreEqual(4, r.referenceOctave);
            Assert.AreEqual(0, r.minOctaveOffset);
            Assert.AreEqual(0, r.maxOctaveOffset);
            Assert.AreEqual(0, r.warnings.Count);
        }

        [Test]
        public void NonCRoot_Aeolian_MapsDegreesAgainstThatScale()
        {
            // A Aeolian: A4 = Tonic, C5 = Mediant — both scale octave 4.
            var file = BuildFile(
                (0, 480, A4, 100, 0),
                (480, 480, C5, 100, 0));

            var r = MelodyMidiImporter.Import(
                file, CMajorFourFour(root: NoteName.A, tonality: Tonality.Aeolian));

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(ScaleDegree.Tonic, r.notes[0].degree);
            Assert.AreEqual(ScaleDegree.Mediant, r.notes[1].degree);
            Assert.IsTrue(r.notes.All(n => n.octaveOffset == 0));
            Assert.AreEqual(0, r.warnings.Count);
        }

        // -------------------------------------------------------------------
        // Chromatic snap (D-MIDI2=A + M2-D6=A: tie → down; in the 7 modes every
        // chromatic pitch is a tie, so the effective rule is one semitone down)
        // -------------------------------------------------------------------

        [Test]
        public void ChromaticNote_SnapsDownToNearestDegree_AndWarns()
        {
            // F#4 in C Ionian: equidistant F (down) / G (up) → down → Subdominant.
            var file = BuildFile((0, 480, Fs4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.notes.Count);
            Assert.AreEqual(ScaleDegree.Subdominant, r.notes[0].degree);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.ChromaticSnapped));
        }

        [Test]
        public void ChromaticAboveRoot_SnapsDownToTonic()
        {
            // C#4 in C Ionian: tie C (down) / D (up) → down → Tonic at the SAME octave.
            var file = BuildFile((0, 480, Cs4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ScaleDegree.Tonic, r.notes[0].degree);
            Assert.AreEqual(0, r.notes[0].octaveOffset);
            Assert.AreEqual(4, r.referenceOctave);
        }

        // -------------------------------------------------------------------
        // Reference octave (M2-D2=A)
        // -------------------------------------------------------------------

        [Test]
        public void ReferenceOctave_IsModal_TieGoesLower()
        {
            // B3 (scale octave 3) + C4 (scale octave 4): one note each → tie → 3.
            var file = BuildFile(
                (0, 480, B3, 100, 0),
                (480, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(3, r.referenceOctave);
            Assert.AreEqual(ScaleDegree.LeadingTone, r.notes[0].degree);
            Assert.AreEqual(0, r.notes[0].octaveOffset);   // B3 = reference band
            Assert.AreEqual(ScaleDegree.Tonic, r.notes[1].degree);
            Assert.AreEqual(1, r.notes[1].octaveOffset);   // C4 = one octave up
            Assert.AreEqual(0, r.minOctaveOffset);
            Assert.AreEqual(1, r.maxOctaveOffset);
        }

        [Test]
        public void ReferenceOctave_MajorityWins()
        {
            // Three notes in octave 4, one in octave 5 → reference 4, C5 offset +1.
            var file = BuildFile(
                (0, 480, C4, 100, 0),
                (480, 480, E4, 100, 0),
                (960, 480, G4, 100, 0),
                (1440, 480, C5, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(4, r.referenceOctave);
            Assert.AreEqual(0, r.notes[0].octaveOffset);
            Assert.AreEqual(1, r.notes[3].octaveOffset);
        }

        // -------------------------------------------------------------------
        // Monophonization (M2-D4=A)
        // -------------------------------------------------------------------

        [Test]
        public void SimultaneousNotes_KeepHighestPitch_AndWarn()
        {
            // C4+E4+G4 chord at beat 0 → only G4 (Dominant) survives.
            var file = BuildFile(
                (0, 480, C4, 100, 0),
                (0, 480, E4, 100, 0),
                (0, 480, G4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.notes.Count);
            Assert.AreEqual(ScaleDegree.Dominant, r.notes[0].degree);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.PolyphonyReduced));
        }

        [Test]
        public void OverlappingNote_TruncatedAtNextStart_AndWarns()
        {
            // C4 holds 2 beats but E4 enters at beat 1 → C4 truncated to 1 beat;
            // E4 keeps its own duration.
            var file = BuildFile(
                (0, 960, C4, 100, 0),
                (480, 480, E4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(2, r.notes.Count);
            Assert.AreEqual(1f, r.notes[0].durationBeats);
            Assert.AreEqual(1f, r.notes[1].startBeat);
            Assert.AreEqual(1f, r.notes[1].durationBeats);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.OverlapTruncated));
        }

        // -------------------------------------------------------------------
        // Timing quantization (M2-D5=A)
        // -------------------------------------------------------------------

        [Test]
        public void OffGridStart_SnapsToNearestStep_AndWarns()
        {
            // Tick 60 = 0.5 steps → rounds away-from-zero to step 1 (beat 0.25),
            // error 0.5 > 0.25 threshold.
            var file = BuildFile((60, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(0.25f, r.notes[0].startBeat);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.OffGridSnap));
        }

        [Test]
        public void OnGridNote_NoSnapWarnings()
        {
            var file = BuildFile((480, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.IsFalse(HasKind(r, MelodyMidiImporter.ImportWarningKind.OffGridSnap));
            Assert.IsFalse(HasKind(r, MelodyMidiImporter.ImportWarningKind.DurationSnapped));
        }

        [Test]
        public void TinyDuration_RaisedToOneStepFloor_AndWarns()
        {
            // 30 ticks = 0.25 steps → rounds to 0 → floored to 1 step = 0.25 beats.
            var file = BuildFile((0, 30, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(0.25f, r.notes[0].durationBeats);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.DurationSnapped));
        }

        // -------------------------------------------------------------------
        // Measures: derivation (covers last note END) + explicit range
        // -------------------------------------------------------------------

        [Test]
        public void DerivedMeasures_CoverLastNoteEnd()
        {
            // Start beat 3.75, duration 1 beat → end 4.75 beats → needs 2 measures,
            // even though the ONSET fits in measure 1 (end-based derivation).
            var file = BuildFile((1800, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(2, r.measures);
            Assert.AreEqual(0, r.warnings.Count);
        }

        [Test]
        public void ExplicitMeasures_DropsLateStarts_ClipsOverhangs()
        {
            // measures=1 (4 beats): note at beat 5 dropped; note starting at beat 3
            // with 2-beat duration clipped to 1 beat.
            var file = BuildFile(
                (1440, 960, C4, 100, 0),    // beat 3, 2 beats → clip to 1
                (2400, 480, E4, 100, 0));   // beat 5 → dropped

            var r = MelodyMidiImporter.Import(file, CMajorFourFour(measures: 1));

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.notes.Count);
            Assert.AreEqual(3f, r.notes[0].startBeat);
            Assert.AreEqual(1f, r.notes[0].durationBeats);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.NotesBeyondRange));
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.DurationClipped));
        }

        // -------------------------------------------------------------------
        // Channel filter (M2-D3=A)
        // -------------------------------------------------------------------

        [Test]
        public void AllChannels_MergesWithWarning()
        {
            var file = BuildFile(
                (0, 480, C4, 100, 0),
                (480, 480, E4, 100, 1));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour(channel: -1));

            Assert.AreEqual(2, r.notes.Count);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.ChannelsMerged));
        }

        [Test]
        public void ChannelFilter_KeepsOnlyThatChannel_NoMergeWarning()
        {
            var file = BuildFile(
                (0, 480, C4, 100, 0),
                (480, 480, E4, 100, 1));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour(channel: 0));

            Assert.AreEqual(1, r.notes.Count);
            Assert.AreEqual(ScaleDegree.Tonic, r.notes[0].degree);
            Assert.IsFalse(HasKind(r, MelodyMidiImporter.ImportWarningKind.ChannelsMerged));
        }

        [Test]
        public void ChannelFilter_NoMatchingNotes_FailsWithHint()
        {
            var file = BuildFile((0, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(file, CMajorFourFour(channel: 5));

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.NoNotesFound));
        }

        // -------------------------------------------------------------------
        // Meter awareness
        // -------------------------------------------------------------------

        [Test]
        public void SixEight_GridBeatIsEighthNote()
        {
            // 6/8: beatUnit 8 → one grid beat = one eighth = 240 ticks.
            // Note at tick 240 with quarter-note length → startBeat 1, duration 2 beats.
            var file = BuildFile((240, 480, C4, 100, 0));

            var r = MelodyMidiImporter.Import(
                file, CMajorFourFour(ts: TimeSignature.SixEight));

            Assert.AreEqual(1f, r.notes[0].startBeat);
            Assert.AreEqual(2f, r.notes[0].durationBeats);
            Assert.AreEqual(0, r.warnings.Count);
        }

        // -------------------------------------------------------------------
        // Failure modes + determinism
        // -------------------------------------------------------------------

        [Test]
        public void EmptyFile_Fails()
        {
            var file = BuildFile();

            var r = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r, MelodyMidiImporter.ImportWarningKind.NoNotesFound));
        }

        [Test]
        public void NullFile_FailsWithUnsupportedTimeDivision()
        {
            var r = MelodyMidiImporter.Import(null, CMajorFourFour());

            Assert.AreEqual(MelodyMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r,
                MelodyMidiImporter.ImportWarningKind.UnsupportedTimeDivision));
        }

        [Test]
        public void Import_IsDeterministic_SameInputsSameOutputs()
        {
            var file = BuildFile(
                (0, 960, C4, 100, 0),      // simultaneous with E4 → C4 dropped (lower pitch)
                (0, 480, E4, 100, 0),      // survivor at step 0
                (480, 480, Fs4, 90, 0),    // chromatic → snapped down
                (1020, 480, G4, 110, 0));  // 8.5 steps → snapped to 9, off-grid warning

            var a = MelodyMidiImporter.Import(file, CMajorFourFour());
            var b = MelodyMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(a.mode, b.mode);
            Assert.AreEqual(a.measures, b.measures);
            Assert.AreEqual(a.referenceOctave, b.referenceOctave);
            Assert.AreEqual(a.notes.Count, b.notes.Count);
            for (int i = 0; i < a.notes.Count; i++)
            {
                Assert.AreEqual(a.notes[i].degree, b.notes[i].degree);
                Assert.AreEqual(a.notes[i].octaveOffset, b.notes[i].octaveOffset);
                Assert.AreEqual(a.notes[i].startBeat, b.notes[i].startBeat);
                Assert.AreEqual(a.notes[i].durationBeats, b.notes[i].durationBeats);
                Assert.AreEqual(a.notes[i].velocity, b.notes[i].velocity);
            }
            Assert.AreEqual(
                a.warnings.Select(w => w.ToString()).ToList(),
                b.warnings.Select(w => w.ToString()).ToList());
        }
    }
}
#endif
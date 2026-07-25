#if UNITY_EDITOR
// Phase M1 (Roadmap_MIDI_Import.md) — pure-function tests for DrumMidiImporter.
// All MIDI files are synthesized in memory with DryWetMidi (no file I/O, no
// Unity-editor state), keeping the suite EditMode-deterministic.
//
// Grid math under test: step ticks = tpqn × (4 / beatUnit) / subdivisions.
// With tpqn=480, 4/4, subdivisions=4 → one step = 120 ticks.

using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using NUnit.Framework;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    public class DrumMidiImporterTests
    {
        private const short Tpqn = 480;

        // GM note numbers used below (resolved via DryWetMidi's own tables in
        // the assertions, so the tests do not hardcode the enum offset).
        private const int KickNote = 36;   // BassDrum1
        private const int SnareNote = 38;  // AcousticSnare

        // -------------------------------------------------------------------
        // In-memory MIDI construction
        // -------------------------------------------------------------------

        private static MidiFile BuildFile(
            params (long tick, int note, int velocity, int channel)[] hits)
        {
            var timed = new List<(long time, MidiEvent ev)>();
            foreach (var h in hits)
            {
                timed.Add((h.tick, new NoteOnEvent(
                    (SevenBitNumber)h.note, (SevenBitNumber)h.velocity)
                { Channel = (FourBitNumber)h.channel }));
                timed.Add((h.tick + 60, new NoteOffEvent(
                    (SevenBitNumber)h.note, (SevenBitNumber)0)
                { Channel = (FourBitNumber)h.channel }));
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

        private static DrumMidiImporter.Options FourFourOptions(
            int subdivisions = 4, int measures = 0,
            int channel = DrumMidiImporter.GmDrumChannel)
            => new DrumMidiImporter.Options
            {
                timeSignature = TimeSignature.FourFour,
                subdivisions = subdivisions,
                measures = measures,
                channel = channel,
            };

        private static List<int> ActiveSteps(DrumMidiImporter.LaneResult lane)
        {
            var result = new List<int>();
            for (int s = 0; s < lane.steps.Count; s++)
                if (lane.steps[s].active) result.Add(s);
            return result;
        }

        // -------------------------------------------------------------------
        // Happy path
        // -------------------------------------------------------------------

        [Test]
        public void FourFour_TwoLanes_MapsSteps_SentinelAndExplicitVelocities()
        {
            // 1 measure, 16 steps (step = 120 ticks).
            // Kick: steps 0 and 8, both vel 100 → modal default 100, both sentinel.
            // Snare: step 4 vel 110, step 12 vel 90 → tie, modal default = lower (90);
            //        step 4 explicit 110, step 12 sentinel.
            var file = BuildFile(
                (0, KickNote, 100, 9),
                (960, KickNote, 100, 9),
                (480, SnareNote, 110, 9),
                (1440, SnareNote, 90, 9));

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(r.measures, Is.EqualTo(1), "derived from content");
            Assert.That(r.lanes.Count, Is.EqualTo(2));

            // Lane order: GM note number ascending → kick (36) before snare (38).
            var kick = r.lanes[0];
            var snare = r.lanes[1];
            Assert.That(kick.instrument, Is.EqualTo(GeneralMidiPercussion.BassDrum1));
            Assert.That(snare.instrument, Is.EqualTo(GeneralMidiPercussion.AcousticSnare));

            Assert.That(kick.steps.Count, Is.EqualTo(16), "full-length step list");
            Assert.That(ActiveSteps(kick), Is.EqualTo(new List<int> { 0, 8 }));
            Assert.That(kick.defaultVelocity, Is.EqualTo(100));
            Assert.That(kick.steps[0].velocity, Is.EqualTo(0), "default → sentinel");
            Assert.That(kick.steps[8].velocity, Is.EqualTo(0), "default → sentinel");

            Assert.That(ActiveSteps(snare), Is.EqualTo(new List<int> { 4, 12 }));
            Assert.That(snare.defaultVelocity, Is.EqualTo(90),
                "modal tie breaks to the lower velocity (deterministic)");
            Assert.That(snare.steps[4].velocity, Is.EqualTo(110), "non-default is explicit");
            Assert.That(snare.steps[12].velocity, Is.EqualTo(0), "default → sentinel");
        }

        // -------------------------------------------------------------------
        // Channel filter + unmapped notes
        // -------------------------------------------------------------------

        [Test]
        public void ChannelFilter_ExcludesMelodicChannel_AndWarnsOnUnmappedNumbers()
        {
            var file = BuildFile(
                (0, KickNote, 100, 9),
                (0, 60, 100, 0),    // melodic note on channel 1 — filtered out
                (240, 20, 100, 9)); // channel 10 but below the GM percussion range

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(r.lanes.Count, Is.EqualTo(1), "only the kick lane survives");
            Assert.That(r.lanes[0].instrument, Is.EqualTo(GeneralMidiPercussion.BassDrum1));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.UnmappedNoteNumber),
                "note number 20 must be reported, not silently dropped");
        }

        [Test]
        public void ChannelFilter_NoDrumChannelNotes_FailsWithHint()
        {
            var file = BuildFile((0, 60, 100, 0)); // melodic content only

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Failed));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.NoNotesFound));
        }

        [Test]
        public void AllChannels_AcceptsPercussionOnAnyChannel()
        {
            // Some exports put drums on channel 1; channel = -1 must accept them.
            var file = BuildFile((0, KickNote, 100, 0));

            var r = DrumMidiImporter.Import(file, FourFourOptions(channel: -1));

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(r.lanes.Count, Is.EqualTo(1));
        }

        // -------------------------------------------------------------------
        // Quantization
        // -------------------------------------------------------------------

        [Test]
        public void OffGridNote_SnapsToNearestStep_AndWarns()
        {
            // Step = 120 ticks. Tick 160 → rawStep 1.33 → snaps to 1, error > 0.25.
            var file = BuildFile((160, KickNote, 100, 9));

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(ActiveSteps(r.lanes[0]), Is.EqualTo(new List<int> { 1 }));
            Assert.That(r.warnings.Count(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.OffGridSnap), Is.EqualTo(1));
        }

        [Test]
        public void OnGridNote_NoSnapWarning()
        {
            var file = BuildFile((120, KickNote, 100, 9)); // exactly step 1

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.OffGridSnap), Is.False);
        }

        // -------------------------------------------------------------------
        // Collisions
        // -------------------------------------------------------------------

        [Test]
        public void SameStepCollision_KeepsHigherVelocity_AndWarns()
        {
            var file = BuildFile(
                (0, KickNote, 60, 9),
                (10, KickNote, 100, 9)); // tick 10 snaps to step 0 as well

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            var kick = r.lanes[0];
            Assert.That(ActiveSteps(kick), Is.EqualTo(new List<int> { 0 }));
            // Single kept step → its velocity is the modal default → sentinel.
            Assert.That(kick.defaultVelocity, Is.EqualTo(100));
            Assert.That(kick.steps[0].velocity, Is.EqualTo(0));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.StepCollision));
        }

        // -------------------------------------------------------------------
        // Measures: explicit truncation + derivation
        // -------------------------------------------------------------------

        [Test]
        public void ExplicitMeasures_DropsNotesBeyondRange_AndWarns()
        {
            // Measure 2 starts at tick 1920; explicit measures = 1 must drop it.
            var file = BuildFile(
                (0, KickNote, 100, 9),
                (1920, KickNote, 100, 9));

            var r = DrumMidiImporter.Import(file, FourFourOptions(measures: 1));

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(r.measures, Is.EqualTo(1));
            Assert.That(ActiveSteps(r.lanes[0]), Is.EqualTo(new List<int> { 0 }));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.NotesBeyondRange));
        }

        [Test]
        public void DerivedMeasures_CoverLastNote()
        {
            // A hit at tick 1920 (step 16) needs a second measure.
            var file = BuildFile(
                (0, KickNote, 100, 9),
                (1920, KickNote, 100, 9));

            var r = DrumMidiImporter.Import(file, FourFourOptions(measures: 0));

            Assert.That(r.measures, Is.EqualTo(2));
            Assert.That(r.lanes[0].steps.Count, Is.EqualTo(32));
            Assert.That(ActiveSteps(r.lanes[0]), Is.EqualTo(new List<int> { 0, 16 }));
        }

        // -------------------------------------------------------------------
        // Beat-unit awareness (6/8)
        // -------------------------------------------------------------------

        [Test]
        public void SixEight_GridBeatIsEighthNote()
        {
            // 6/8: beatUnit 8 → one grid beat = eighth = 240 ticks. Subdivisions 2
            // → one step = 120 ticks. Tick 720 = quarterNotes 1.5 = gridBeats 3
            // → step 6. One measure = 6 × 2 = 12 steps.
            var file = BuildFile((720, KickNote, 100, 9));

            var options = new DrumMidiImporter.Options
            {
                timeSignature = TimeSignature.SixEight,
                subdivisions = 2,
                measures = 0,
                channel = DrumMidiImporter.GmDrumChannel,
            };
            var r = DrumMidiImporter.Import(file, options);

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Full));
            Assert.That(r.measures, Is.EqualTo(1));
            Assert.That(r.lanes[0].steps.Count, Is.EqualTo(12));
            Assert.That(ActiveSteps(r.lanes[0]), Is.EqualTo(new List<int> { 6 }));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.OffGridSnap), Is.False,
                "tick 720 is exactly on the 6/8 eighth-note grid");
        }

        // -------------------------------------------------------------------
        // Degenerate inputs
        // -------------------------------------------------------------------

        [Test]
        public void EmptyFile_Fails()
        {
            var file = new MidiFile(new TrackChunk())
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(Tpqn)
            };

            var r = DrumMidiImporter.Import(file, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Failed));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.NoNotesFound));
        }

        [Test]
        public void NullFile_FailsWithUnsupportedTimeDivision()
        {
            var r = DrumMidiImporter.Import(null, FourFourOptions());

            Assert.That(r.mode, Is.EqualTo(DrumMidiImporter.ImportMode.Failed));
            Assert.That(r.warnings.Any(w =>
                w.kind == DrumMidiImporter.ImportWarningKind.UnsupportedTimeDivision));
        }
    }
}
#endif
#if UNITY_EDITOR
// Phase M3 (Roadmap_MIDI_Import.md) — pure-function tests for ChordMidiImporter.
// All MIDI files are synthesized in memory with DryWetMidi (no file I/O, no
// Unity-editor state), keeping the suite EditMode-deterministic. Same mold as
// DrumMidiImporterTests / MelodyMidiImporterTests.
//
// Grid math under test: step ticks = tpqn × (4 / beatUnit) / subdivisions.
// With tpqn=480, 4/4, subdivisions=1 → one step = one beat = 480 ticks.

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
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordMidiImporterTests
    {
        private const short Tpqn = 480;
        private const long Beat = 480;          // 4/4, quarter-note beat
        private const long Bar = 4 * Beat;      // 4/4 measure

        // MIDI note numbers used below (middle C = C4 = 60).
        private const int E2 = 40;
        private const int A2 = 45;
        private const int C3 = 48;
        private const int Cs3 = 49;
        private const int D3 = 50;
        private const int E3 = 52;
        private const int F3 = 53;
        private const int G3 = 55;
        private const int Gs3 = 56;
        private const int A3 = 57;
        private const int As3 = 58;   // B♭3
        private const int B3 = 59;
        private const int C4 = 60;
        private const int D4 = 62;
        private const int E4 = 64;
        private const int F4 = 65;
        private const int G4 = 67;
        private const int A4 = 69;
        private const int B4 = 71;
        private const int C5 = 72;
        private const int D5 = 74;

        // -------------------------------------------------------------------
        // In-memory MIDI construction (mold: MelodyMidiImporterTests.BuildFile)
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

        /// <summary>A block chord: all notes share tick + length + velocity + channel.</summary>
        private static IEnumerable<(long, long, int, int, int)> Chord(
            long tick, long length, int velocity, int channel, params int[] notes)
            => notes.Select(n => (tick, length, n, velocity, channel));

        private static MidiFile BuildChords(
            params IEnumerable<(long, long, int, int, int)>[] chords)
            => BuildFile(chords.SelectMany(c => c).ToArray());

        private static ChordMidiImporter.Options CMajorFourFour(
            int subdivisions = 1, int measures = 0, int channel = -1,
            NoteName root = NoteName.C, Tonality tonality = Tonality.Ionian,
            TimeSignature ts = TimeSignature.FourFour)
            => new ChordMidiImporter.Options
            {
                rootNote = root,
                tonality = tonality,
                timeSignature = ts,
                subdivisions = subdivisions,
                measures = measures,
                channel = channel,
            };

        private static bool HasKind(
            ChordMidiImporter.Result r, ChordMidiImporter.ImportWarningKind kind)
            => r.warnings.Any(w => w.kind == kind);

        private static void AssertEvent(
            ChordProgressionData.ChordEvent e,
            int startStep, int lengthSteps,
            ScaleDegree degree, int accidental, ChordQuality quality, bool isDiatonic)
        {
            Assert.AreEqual(startStep, e.startStep, "startStep");
            Assert.AreEqual(lengthSteps, e.lengthSteps, "lengthSteps");
            Assert.AreEqual(degree, e.degree, "degree");
            Assert.AreEqual(accidental, e.degreeAccidental, "degreeAccidental");
            Assert.AreEqual(quality, e.quality, "quality");
            Assert.AreEqual(isDiatonic, e.isDiatonic, "isDiatonic");
        }

        // -------------------------------------------------------------------
        // Alphabet invariant backing the whole matcher
        // -------------------------------------------------------------------

        [Test]
        public void TemplatePcSets_AreUniquePerRoot()
        {
            // The M3-D5 cascade assumes that, from a fixed root, no two v1
            // qualities share a pitch-class set (mod 12, deduplicated). If a
            // future quality breaks this, exact matching stops being unique.
            var masks = new Dictionary<int, ChordQuality>();
            foreach (ChordQuality q in System.Enum.GetValues(typeof(ChordQuality)))
            {
                int m = 0;
                foreach (int iv in GetIntervalsForQuality(q))
                    m |= 1 << (((iv % 12) + 12) % 12);
                Assert.IsFalse(masks.ContainsKey(m),
                    $"{q} and {(masks.ContainsKey(m) ? masks[m] : default)} share a pc-set.");
                masks[m] = q;
            }
        }

        // -------------------------------------------------------------------
        // Happy paths: exact matching, degrees, diatonic flags
        // -------------------------------------------------------------------

        [Test]
        public void DiatonicTriads_IIVVI_FourEvents()
        {
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(1 * Bar, Bar, 100, 0, F3, A3, C4),
                Chord(2 * Bar, Bar, 100, 0, G3, B3, D4),
                Chord(3 * Bar, Bar, 100, 0, C4, E4, G4));

            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(4, r.measures);
            Assert.AreEqual(4, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            AssertEvent(r.events[1], 4, 4, ScaleDegree.Subdominant, 0, ChordQuality.Major, true);
            AssertEvent(r.events[2], 8, 4, ScaleDegree.Dominant, 0, ChordQuality.Major, true);
            AssertEvent(r.events[3], 12, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            Assert.AreEqual(0, r.warnings.Count, "clean file should import warning-free");
        }

        [Test]
        public void SeventhChord_G7_IsDominant7()
        {
            var file = BuildChords(Chord(0, Bar, 100, 0, G3, B3, D4, F4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Dominant, 0, ChordQuality.Dominant7, true);
        }

        [Test]
        public void Ninth_G9_FoldsMod12_IsDominant9()
        {
            var file = BuildChords(Chord(0, Bar, 100, 0, G3, B3, D4, F4, A4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(ChordQuality.Dominant9, r.events[0].quality);
            Assert.AreEqual(ScaleDegree.Dominant, r.events[0].degree);
        }

        [Test]
        public void BorrowedRoot_BbMajor_InCIonian_IsFlatVII_NotDiatonic()
        {
            // M3-D2=A / D2b: ♭VII spelled as LeadingTone with accidental -1 (flat
            // preferred), never snapped — the accidental field CAN express it.
            var file = BuildChords(Chord(0, Bar, 100, 0, As3, D4, F4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.LeadingTone, -1, ChordQuality.Major, false);
            Assert.IsFalse(HasKind(r, ChordMidiImporter.ImportWarningKind.RootSnapped));
        }

        [Test]
        public void BorrowedQuality_EMajor_InAAeolian_AccidentalZero_NotDiatonic()
        {
            // Root E IS diatonic in A Aeolian (Dominant); the MAJOR quality is the
            // borrowed part → accidental 0, isDiatonic false (triad-family test).
            var file = BuildChords(Chord(0, Bar, 100, 0, E3, Gs3, B3));
            var r = ChordMidiImporter.Import(
                file, CMajorFourFour(root: NoteName.A, tonality: Tonality.Aeolian));

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Dominant, 0, ChordQuality.Major, false);
        }

        // -------------------------------------------------------------------
        // M3-D5 cascade: bass root, inversions, ambiguity, reduction, unmatched
        // -------------------------------------------------------------------

        [Test]
        public void BassDisambiguates_C6_vs_Am7()
        {
            // Identical pitch-class set {C,E,G,A}; the bass decides (M3-D5 step 1).
            var c6 = ChordMidiImporter.Import(
                BuildChords(Chord(0, Bar, 100, 0, C3, E3, G3, A3)), CMajorFourFour());
            Assert.AreEqual(ChordQuality.Major6, c6.events[0].quality);
            Assert.AreEqual(ScaleDegree.Tonic, c6.events[0].degree);
            Assert.IsFalse(HasKind(c6, ChordMidiImporter.ImportWarningKind.ChordAmbiguityResolved));

            var am7 = ChordMidiImporter.Import(
                BuildChords(Chord(0, Bar, 100, 0, A2, C3, E3, G3)), CMajorFourFour());
            Assert.AreEqual(ChordQuality.Minor7, am7.events[0].quality);
            Assert.AreEqual(ScaleDegree.Submediant, am7.events[0].degree);
        }

        [Test]
        public void Inversion_CoverE_ResolvesToCMajor_NoWarning()
        {
            // C/E: the bass (E) is no valid root; all-member-roots finds exactly
            // one match (C Major). Inversion loss is a documented limitation, so
            // no per-chord warning is emitted (M2 octave-loss precedent).
            var file = BuildChords(Chord(0, Bar, 100, 0, E3, G3, C4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            Assert.AreEqual(0, r.warnings.Count);
        }

        [Test]
        public void Ambiguity_EBass_OverCEGA_KeepsC6_AndWarns()
        {
            // Bass E is no valid root for {C,E,G,A}; C Major6 and A Minor7 both
            // match exactly → tie-break (both diatonic, both 4 voices) falls to
            // lowest root pc → C Major6, with an informative warning (M3-D5 step 3).
            var file = BuildChords(Chord(0, Bar, 100, 0, E2, C4, E4, G4, A4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(ChordQuality.Major6, r.events[0].quality);
            Assert.AreEqual(ScaleDegree.Tonic, r.events[0].degree);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.ChordAmbiguityResolved));
        }

        [Test]
        public void Reduction_CAdd9_KeepsMajorTriad_AndWarns()
        {
            // {C,D,E,G} matches nothing exactly; contained templates from C are
            // Major and Sus2 (both 3 voices) → diatonic tie-break keeps Major.
            var file = BuildChords(Chord(0, Bar, 100, 0, C4, D4, E4, G4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.ChordReduced));
        }

        [Test]
        public void Unmatched_Cluster_SkippedWithWarning()
        {
            // Bar 1: chromatic cluster (no v1 chord contained) → gap + warning.
            // Bar 2: a real chord → single event.
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, Cs3 + 12, D4),   // {C,C#,D}
                Chord(1 * Bar, Bar, 100, 0, G3, B3, D4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(4, r.events[0].startStep);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.ChordUnmatched));
        }

        [Test]
        public void BelowThreshold_Dyad_GapAndWarning()
        {
            // M3-D3=B: bar 2's dyad is not a chord → gap + warning, import still Full.
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(1 * Bar, Bar, 100, 0, C4, G4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.SegmentBelowThreshold));
        }

        [Test]
        public void OnlyBelowThreshold_FailsWithNoChordsFound()
        {
            var file = BuildChords(Chord(0, Bar, 100, 0, C4, G4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.NoChordsFound));
        }

        // -------------------------------------------------------------------
        // M3-D1=A: quantize first, segment after
        // -------------------------------------------------------------------

        [Test]
        public void HumanizedStrum_CollapsesToOneSegment()
        {
            // Strummed/humanized onsets (up to 150 ticks ≈ 0.31 steps late at
            // subdivisions=1) land on the same quantized step → one event, no
            // spurious partial chords. Off-grid snaps warn per M1/M2 threshold.
            var file = BuildFile(
                (0, Bar, C4, 100, 0),
                (80, Bar - 80, E4, 100, 0),
                (150, Bar - 150, G4, 100, 0));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(1, r.events.Count);
            AssertEvent(r.events[0], 0, 4, ScaleDegree.Tonic, 0, ChordQuality.Major, true);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.OffGridSnap));
        }

        [Test]
        public void Restrikes_SameChord_CoalesceAcrossGaps_MeanVelocity()
        {
            // Four staccato strikes of the same chord (subdivisions=2: each strike
            // fills one step, leaving an empty step before the next) coalesce into
            // ONE harmonic region spanning the bar — strike rhythm belongs to the
            // runtime articulators. Velocity is the rounded mean (M3-D6).
            var file = BuildChords(
                Chord(0 * Beat, 240, 90, 0, C4, E4, G4),
                Chord(1 * Beat, 240, 100, 0, C4, E4, G4),
                Chord(2 * Beat, 240, 110, 0, C4, E4, G4),
                Chord(3 * Beat, 240, 100, 0, C4, E4, G4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour(subdivisions: 2));

            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(0, r.events[0].startStep);
            Assert.AreEqual(7, r.events[0].lengthSteps,
                "region spans first strike start → last strike end (step 7 of 8)");
            Assert.AreEqual(100, r.events[0].velocity);
        }

        [Test]
        public void HeldChord_AcrossBars_SingleLongEvent()
        {
            var file = BuildChords(Chord(0, 2 * Bar, 100, 0, F3, A3, C4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(2, r.measures);
            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(8, r.events[0].lengthSteps);
        }

        // -------------------------------------------------------------------
        // Channel filter, meter, ranges (M1/M2 contract parity)
        // -------------------------------------------------------------------

        [Test]
        public void ChannelFilter_KeepsOnlyRequestedChannel()
        {
            var file = BuildFile(
                (0, Bar, C4, 100, 0), (0, Bar, E4, 100, 0), (0, Bar, G4, 100, 0),
                (0, Bar, B4, 100, 1));   // stray melody note on another channel
            var r = ChordMidiImporter.Import(file, CMajorFourFour(channel: 0));

            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(ChordQuality.Major, r.events[0].quality);
            Assert.IsFalse(HasKind(r, ChordMidiImporter.ImportWarningKind.ChannelsMerged));
        }

        [Test]
        public void ChannelMerge_Warns()
        {
            var file = BuildFile(
                (0, Bar, C4, 100, 0), (0, Bar, E4, 100, 0), (0, Bar, G4, 100, 1));
            var r = ChordMidiImporter.Import(file, CMajorFourFour(channel: -1));

            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.ChannelsMerged));
        }

        [Test]
        public void SixEight_GridBeatIsEighthNote()
        {
            // 6/8: beat unit 8 → grid beat = eighth. A full 6/8 measure is
            // 1440 ticks (3 quarters) = 6 grid beats = 6 steps at subdivisions 1.
            var file = BuildChords(Chord(0, 1440, 100, 0, C4, E4, G4));
            var r = ChordMidiImporter.Import(
                file, CMajorFourFour(ts: TimeSignature.SixEight));

            Assert.AreEqual(1, r.measures);
            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(6, r.events[0].lengthSteps);
        }

        [Test]
        public void ExplicitMeasures_DropsLateStarts_ClipsOverhangs()
        {
            var file = BuildChords(
                Chord(0 * Bar, Bar + Beat, 100, 0, C4, E4, G4),   // overhangs bar 1
                Chord(2 * Bar, Bar, 100, 0, G3, B3, D4));         // starts beyond
            var r = ChordMidiImporter.Import(file, CMajorFourFour(measures: 1));

            Assert.AreEqual(1, r.measures);
            Assert.AreEqual(1, r.events.Count);
            Assert.AreEqual(4, r.events[0].lengthSteps);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.DurationClipped));
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.NotesBeyondRange));
        }

        // -------------------------------------------------------------------
        // Failure modes + summary + determinism
        // -------------------------------------------------------------------

        [Test]
        public void EmptyFile_Fails()
        {
            var file = BuildFile();
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.NoNotesFound));
        }

        [Test]
        public void NullFile_Fails()
        {
            var r = ChordMidiImporter.Import(null, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Failed, r.mode);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.UnsupportedTimeDivision));
        }

        [Test]
        public void RomanSummary_ReadsAsProgression()
        {
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(1 * Bar, Bar, 100, 0, A2, C3, E3),
                Chord(2 * Bar, Bar, 100, 0, As3, D4, F4));
            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            StringAssert.Contains("I", r.romanSummary);
            StringAssert.Contains("vi", r.romanSummary);
            StringAssert.Contains("♭VII", r.romanSummary);
        }

        [Test]
        public void DescribeChordTimeline_ShowsSegments_Rests_Inversions()
        {
            // C/E in bar 1 (bass visible with octave → inversion diagnosable),
            // silence in bar 2, G in bar 3. Display-only diagnostic; asserts are
            // deliberately loose (presence, not layout).
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, E3, G3, C4),
                Chord(2 * Bar, Bar, 100, 0, G3, B3, D4));
            var text = ChordMidiImporter.DescribeChordTimeline(file, CMajorFourFour());

            StringAssert.Contains("C Major", text);
            StringAssert.Contains("G Major", text);
            StringAssert.Contains("bass=E3", text);   // inversion visible
            StringAssert.Contains("rest", text);      // the empty bar is listed
            StringAssert.Contains("m1.1.1", text);

            // Same math as Import: verdicts agree with the imported events.
            var r = ChordMidiImporter.Import(file, CMajorFourFour());
            Assert.AreEqual(2, r.events.Count);
            Assert.AreEqual(ScaleDegree.Tonic, r.events[0].degree);
            Assert.AreEqual(ScaleDegree.Dominant, r.events[1].degree);
        }

        [Test]
        public void Determinism_SameInput_SameOutput()
        {
            var opts = CMajorFourFour();
            MidiFile Make() => BuildChords(
                Chord(0 * Bar, Bar, 100, 0, E2, C4, E4, G4, A4),  // ambiguous
                Chord(1 * Bar, Bar, 100, 0, C4, D4, E4, G4),      // reduced
                Chord(2 * Bar, Bar, 100, 0, G3, B3, D4, F4));

            var r1 = ChordMidiImporter.Import(Make(), opts);
            var r2 = ChordMidiImporter.Import(Make(), opts);

            Assert.AreEqual(r1.events.Count, r2.events.Count);
            for (int i = 0; i < r1.events.Count; i++)
            {
                Assert.AreEqual(r1.events[i].startStep, r2.events[i].startStep);
                Assert.AreEqual(r1.events[i].lengthSteps, r2.events[i].lengthSteps);
                Assert.AreEqual(r1.events[i].degree, r2.events[i].degree);
                Assert.AreEqual(r1.events[i].degreeAccidental, r2.events[i].degreeAccidental);
                Assert.AreEqual(r1.events[i].quality, r2.events[i].quality);
                Assert.AreEqual(r1.events[i].velocity, r2.events[i].velocity);
            }
            CollectionAssert.AreEqual(
                r1.warnings.Select(w => w.ToString()).ToList(),
                r2.warnings.Select(w => w.ToString()).ToList());
            Assert.AreEqual(r1.romanSummary, r2.romanSummary);
        }

        // -------------------------------------------------------------------
        // IMPORT-QOL-1 — preserve re-strikes (item 5) + subdivision
        // suggestion (item 1). The 25 pre-existing tests above are untouched:
        // default(Options).preserveReStrikes == false IS the M3 semantics.
        // -------------------------------------------------------------------

        [Test]
        public void ReStrikes_DefaultOff_IdenticalChordsMergeAcrossGap()
        {
            // Two C-major strikes with a full empty bar between them (m5.2/m5.3
            // smoke shape, reduced): M3 behavior fuses them into ONE region
            // spanning the gap; velocity is the mean of both strikes.
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(2 * Bar, Bar, 60, 0, C4, E4, G4));

            var r = ChordMidiImporter.Import(file, CMajorFourFour());

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.events.Count, "gapped identical strikes must merge by default");
            AssertEvent(r.events[0], 0, 12,
                ScaleDegree.Tonic, 0, ChordQuality.Major, isDiatonic: true);
            Assert.AreEqual(80, r.events[0].velocity, "merged velocity = rounded mean");
        }

        [Test]
        public void ReStrikes_PreserveOn_GappedStrikesStaySeparate()
        {
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(2 * Bar, Bar, 60, 0, C4, E4, G4));

            var opts = CMajorFourFour();
            opts.preserveReStrikes = true;
            var r = ChordMidiImporter.Import(file, opts);

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(2, r.events.Count, "the rest must keep the strikes apart");
            AssertEvent(r.events[0], 0, 4,
                ScaleDegree.Tonic, 0, ChordQuality.Major, isDiatonic: true);
            AssertEvent(r.events[1], 8, 4,
                ScaleDegree.Tonic, 0, ChordQuality.Major, isDiatonic: true);
            Assert.AreEqual(100, r.events[0].velocity, "per-strike velocity preserved");
            Assert.AreEqual(60, r.events[1].velocity, "per-strike velocity preserved");
            StringAssert.Contains(" – ", r.romanSummary,
                "the display summary must list both strikes");
        }

        [Test]
        public void ReStrikes_PreserveOn_ContiguousIdenticalIdentitiesStillMerge()
        {
            // Bar 1: clean C major. Bar 2 (no gap): C-D-E-G, which REDUCES to
            // C major — same harmonic identity, contiguous, so it must merge
            // even with preserveReStrikes ON (the flag only guards gaps).
            var file = BuildChords(
                Chord(0 * Bar, Bar, 100, 0, C4, E4, G4),
                Chord(1 * Bar, Bar, 100, 0, C4, D4, E4, G4));

            var opts = CMajorFourFour();
            opts.preserveReStrikes = true;
            var r = ChordMidiImporter.Import(file, opts);

            Assert.AreEqual(ChordMidiImporter.ImportMode.Full, r.mode);
            Assert.IsTrue(HasKind(r, ChordMidiImporter.ImportWarningKind.ChordReduced),
                "the second bar must arrive via a warned reduction");
            Assert.AreEqual(1, r.events.Count,
                "contiguous identical identities merge regardless of the flag");
            AssertEvent(r.events[0], 0, 8,
                ScaleDegree.Tonic, 0, ChordQuality.Major, isDiatonic: true);
        }

        [Test]
        public void Suggest_EighthNoteChanges_PicksSmallestPassingCandidate()
        {
            // Chord changes on exact eighth notes: sub=1 cannot explain the
            // half-beat onsets; sub=2 explains them perfectly; sub=4/8 also do,
            // but parsimony must pick 2.
            var file = BuildChords(
                Chord(0, Beat / 2, 100, 0, C4, E4, G4),
                Chord(Beat / 2, Beat / 2, 100, 0, G3, B3, D4));

            var s = ChordMidiImporter.SuggestSubdivisions(file, CMajorFourFour());

            Assert.IsTrue(s.hasNotes);
            Assert.IsTrue(s.suggestedWithinThreshold);
            Assert.AreEqual(2, s.suggested);
            Assert.AreEqual(ChordMidiImporter.SuggestCandidates.Length, s.candidates.Count);
            Assert.IsFalse(s.candidates[0].withinThreshold, "sub=1 must fail");
            Assert.IsTrue(s.candidates[1].withinThreshold, "sub=2 must pass");
        }

        [Test]
        public void Suggest_TripletContent_PicksThreeNotSix()
        {
            // Quarter-note triplets on the grid beat: only sub=3 (and its
            // multiple 6) explain them; parsimony picks 3.
            long third = Beat / 3;   // 160 ticks at tpqn 480
            var file = BuildChords(
                Chord(0 * third, third, 100, 0, C4, E4, G4),
                Chord(1 * third, third, 100, 0, F3, A3, C4),
                Chord(2 * third, third, 100, 0, G3, B3, D4));

            var s = ChordMidiImporter.SuggestSubdivisions(file, CMajorFourFour());

            Assert.IsTrue(s.suggestedWithinThreshold);
            Assert.AreEqual(3, s.suggested);
        }

        [Test]
        public void Suggest_NoCandidateExplainsFile_ReportsArgminUnapplied()
        {
            // A single chord at 0.3 beats (tick 144): every candidate's max
            // residual exceeds the threshold, so the suggestion must be marked
            // NOT within threshold and equal the argmin of the table (the
            // caller reports and leaves the slider unchanged).
            var file = BuildChords(
                Chord(144, Beat, 100, 0, C4, E4, G4));

            var s = ChordMidiImporter.SuggestSubdivisions(file, CMajorFourFour());

            Assert.IsTrue(s.hasNotes);
            Assert.IsFalse(s.suggestedWithinThreshold);
            foreach (var c in s.candidates)
                Assert.IsFalse(c.withinThreshold,
                    $"sub={c.subdivisions} unexpectedly passed ({c.maxErrorBeats})");
            double best = s.candidates.Min(c => c.maxErrorBeats);
            Assert.AreEqual(best,
                s.candidates.First(c => c.subdivisions == s.suggested).maxErrorBeats,
                1e-12, "suggested must be the argmin residual");
        }

        [Test]
        public void Suggest_ChannelFilter_MatchesImportsFilter()
        {
            // On-grid chords on ch0; one off-grid noise note on ch1. Filtered
            // to ch0 the file is perfectly explained by sub=1; unfiltered, the
            // noise poisons every candidate.
            var file = BuildFile(
                (0, Beat, C4, 100, 0),
                (0, Beat, E4, 100, 0),
                (0, Beat, G4, 100, 0),
                (144, Beat, D5, 100, 1));

            var filtered = ChordMidiImporter.SuggestSubdivisions(
                file, CMajorFourFour(channel: 0));
            Assert.IsTrue(filtered.suggestedWithinThreshold);
            Assert.AreEqual(1, filtered.suggested);

            var unfiltered = ChordMidiImporter.SuggestSubdivisions(
                file, CMajorFourFour(channel: -1));
            Assert.IsFalse(unfiltered.suggestedWithinThreshold,
                "the ch1 noise note must poison the unfiltered probe");
        }

        [Test]
        public void Suggest_NoNotes_HasNotesFalse()
        {
            var empty = BuildFile();
            var s = ChordMidiImporter.SuggestSubdivisions(empty, CMajorFourFour());
            Assert.IsFalse(s.hasNotes);
            Assert.AreEqual(0, s.candidates.Count);
        }
    }
}
#endif
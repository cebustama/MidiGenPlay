#if UNITY_EDITOR
using NUnit.Framework;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="ChordProgressionEditorImporter"/>.
    /// Pure-function tests over the "setup card + progression block" shape:
    /// full canonical parse, garbled/missing card fallback to
    /// ProgressionOnly, missing block → Failed, CRLF safety, and
    /// language-tagged fence extraction. Chord twin of
    /// <c>DrumPatternEditorImporterTests</c>.
    /// </summary>
    public class ChordProgressionEditorImporterTests
    {
        private const string CanonicalLf =
            "**Setup (Roman mode):**\n\n" +
            "- Time signature: FourFour\n" +
            "- Measures (total): 4\n" +
            "- Default duration (measures): 1\n" +
            "- Reference tonality: Ionian\n\n" +
            "```\n" +
            "ii7 – V7 – Imaj7 – vi7\n" +
            "```\n";

        [Test]
        public void Parse_FullCanonicalBlock_ReturnsFull_WithSetupAndProgression()
        {
            var r = ChordProgressionEditorImporter.Parse(CanonicalLf);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(TimeSignature.FourFour, r.timeSignature);
            Assert.AreEqual(4, r.measures);
            Assert.AreEqual(1f, r.defaultDurationMeasures);
            Assert.AreEqual(Tonality.Ionian, r.referenceTonality);
            StringAssert.Contains("ii7", r.progression);
            StringAssert.Contains("Imaj7", r.progression);
        }

        [Test]
        public void Parse_IsCrlfSafe_ProducesSameProgression()
        {
            string crlf = CanonicalLf.Replace("\n", "\r\n");
            var r = ChordProgressionEditorImporter.Parse(crlf);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            // No stray \r left in the extracted progression.
            StringAssert.DoesNotContain("\r", r.progression);
            StringAssert.Contains("ii7 – V7 – Imaj7 – vi7", r.progression);
        }

        [Test]
        public void Parse_GarbledSetupCard_FallsBackToProgressionOnly()
        {
            string garbled =
                "Here is your progression, enjoy!\n\n" +
                "```\n" +
                "I – V – vi – IV\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(garbled);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.ProgressionOnly, r.mode);
            StringAssert.Contains("I – V – vi – IV", r.progression);
            Assert.IsNotEmpty(r.warnings);
        }

        [Test]
        public void Parse_NoFencedBlock_ReturnsFailed()
        {
            string noBlock =
                "**Setup (Roman mode):**\n" +
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n";

            var r = ChordProgressionEditorImporter.Parse(noBlock);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Failed, r.mode);
            Assert.IsNotEmpty(r.warnings);
        }

        [Test]
        public void Parse_PartialSetupCard_MissingMeasures_DegradesToProgressionOnly()
        {
            string partial =
                "- Time signature: FourFour\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "I – IV – V – I\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(partial);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.ProgressionOnly, r.mode);
            StringAssert.Contains("I – IV – V – I", r.progression);
        }

        [Test]
        public void Parse_LanguageTaggedFence_ExtractsProgression()
        {
            string tagged =
                "- Time signature: FourFour\n" +
                "- Measures (total): 2\n" +
                "- Default duration (measures): 1\n\n" +
                "```text\n" +
                "i – iv\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(tagged);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            StringAssert.Contains("i – iv", r.progression);
        }

        [Test]
        public void Parse_MultiLineProgression_CollapsesToSingleSpacedString()
        {
            string multiline =
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "ii7 – V7\n" +
                "Imaj7 – vi7\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(multiline);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            // Newline between the two lines becomes a single space; no double spaces.
            StringAssert.DoesNotContain("  ", r.progression);
            StringAssert.Contains("V7 Imaj7", r.progression);
        }

        [Test]
        public void Parse_DefaultDurationDecimal_ParsesWithDotSeparator()
        {
            string halfDur =
                "- Time signature: FourFour\n" +
                "- Measures (total): 2\n" +
                "- Default duration (measures): 0.5\n\n" +
                "```\n" +
                "I – V – vi – IV\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(halfDur);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(0.5f, r.defaultDurationMeasures);
        }

        [Test]
        public void Parse_MissingTonality_DefaultsToIonian_StillFull()
        {
            string noTon =
                "- Time signature: ThreeFour\n" +
                "- Measures (total): 3\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "I – IV – V\n" +
                "```\n";

            var r = ChordProgressionEditorImporter.Parse(noTon);

            Assert.AreEqual(ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(Tonality.Ionian, r.referenceTonality);
            Assert.AreEqual(TimeSignature.ThreeFour, r.timeSignature);
        }
    }
}
#endif
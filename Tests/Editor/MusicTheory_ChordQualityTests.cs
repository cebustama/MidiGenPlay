#if UNITY_EDITOR
using NUnit.Framework;
using Melanchall.DryWetMidi.MusicTheory;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the v2 chord-quality additions in
    /// <see cref="MidiGenPlay.MusicTheory.MusicTheory"/>: the realization
    /// intervals, the compact chord symbols, and the append-only enum ordinals —
    /// the guarantee that existing serialized <c>ChordEvent.quality</c> values
    /// keep loading after the enum grew. Covers Tier A (sixths + 7sus4) and
    /// Tier B (ninths). If someone reorders the enum, the ordinal test fails.
    ///
    /// Precondition: the v2 edits to MusicTheory.ChordQuality are applied.
    /// </summary>
    public class MusicTheory_ChordQualityTests
    {
        // -------------------------------------------------------------
        // Realization intervals (root position, semitones)
        // -------------------------------------------------------------

        [Test]
        public void Intervals_Major6()
            => CollectionAssert.AreEqual(
                new[] { 0, 4, 7, 9 }, GetIntervalsForQuality(ChordQuality.Major6));

        [Test]
        public void Intervals_Minor6()
            => CollectionAssert.AreEqual(
                new[] { 0, 3, 7, 9 }, GetIntervalsForQuality(ChordQuality.Minor6));

        [Test]
        public void Intervals_Dominant7sus4()
            => CollectionAssert.AreEqual(
                new[] { 0, 5, 7, 10 }, GetIntervalsForQuality(ChordQuality.Dominant7sus4));

        [Test]
        public void Intervals_Dominant9()
            => CollectionAssert.AreEqual(
                new[] { 0, 4, 7, 10, 14 }, GetIntervalsForQuality(ChordQuality.Dominant9));

        [Test]
        public void Intervals_Major9()
            => CollectionAssert.AreEqual(
                new[] { 0, 4, 7, 11, 14 }, GetIntervalsForQuality(ChordQuality.Major9));

        [Test]
        public void Intervals_Minor9()
            => CollectionAssert.AreEqual(
                new[] { 0, 3, 7, 10, 14 }, GetIntervalsForQuality(ChordQuality.Minor9));

        // -------------------------------------------------------------
        // Compact chord symbols
        // -------------------------------------------------------------

        [Test]
        public void Symbols_TierA()
        {
            Assert.AreEqual("C6", GetChordSymbol(NoteName.C, ChordQuality.Major6));
            Assert.AreEqual("Cm6", GetChordSymbol(NoteName.C, ChordQuality.Minor6));
            Assert.AreEqual("C7sus4", GetChordSymbol(NoteName.C, ChordQuality.Dominant7sus4));
        }

        [Test]
        public void Symbols_TierB()
        {
            Assert.AreEqual("C9", GetChordSymbol(NoteName.C, ChordQuality.Dominant9));
            Assert.AreEqual("Cmaj9", GetChordSymbol(NoteName.C, ChordQuality.Major9));
            Assert.AreEqual("Cm9", GetChordSymbol(NoteName.C, ChordQuality.Minor9));
        }

        // -------------------------------------------------------------
        // Append-only ordinals (do NOT reorder the enum)
        // -------------------------------------------------------------

        [Test]
        public void Enum_Ordinals_AreAppendOnly()
        {
            // Existing members keep their serialized integer values...
            Assert.AreEqual(0, (int)ChordQuality.Major);
            Assert.AreEqual(1, (int)ChordQuality.Minor);
            Assert.AreEqual(2, (int)ChordQuality.Diminished);
            Assert.AreEqual(3, (int)ChordQuality.Augmented);
            Assert.AreEqual(4, (int)ChordQuality.Major7);
            Assert.AreEqual(5, (int)ChordQuality.Minor7);
            Assert.AreEqual(6, (int)ChordQuality.Dominant7);
            Assert.AreEqual(7, (int)ChordQuality.HalfDiminished7);
            Assert.AreEqual(8, (int)ChordQuality.Diminished7);
            Assert.AreEqual(9, (int)ChordQuality.Sus2);
            Assert.AreEqual(10, (int)ChordQuality.Sus4);

            // ...Tier A appended...
            Assert.AreEqual(11, (int)ChordQuality.Major6);
            Assert.AreEqual(12, (int)ChordQuality.Minor6);
            Assert.AreEqual(13, (int)ChordQuality.Dominant7sus4);

            // ...Tier B appended at the tail.
            Assert.AreEqual(14, (int)ChordQuality.Dominant9);
            Assert.AreEqual(15, (int)ChordQuality.Major9);
            Assert.AreEqual(16, (int)ChordQuality.Minor9);
        }
    }
}
#endif
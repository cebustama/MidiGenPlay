#if UNITY_EDITOR
using System.Text.RegularExpressions;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MidiGenPlay.Composition;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the v2 chord-quality additions to
    /// <see cref="RomanProgressionParser"/>: Tier A (Major6 / Minor6 /
    /// Dominant7sus4) and Tier B (Dominant9 / Major9 / Minor9). Verifies the new
    /// explicit suffixes parse to the right quality, that an explicit suffix
    /// outranks numeral case, and that the remaining forbidden extensions
    /// (11 / 13 / add9 / 6/9) keep warning-and-downgrading.
    ///
    /// Precondition: the v2 edits to RomanProgressionParser.TryParseQualitySuffix
    /// are applied.
    /// </summary>
    public class RomanProgressionParserTests
    {
        private static List<ParsedChord> Parse(string input, bool inferTriadFromCase = true)
        {
            var parser = new RomanProgressionParser();
            bool ok = parser.TryParse(input, 1f, inferTriadFromCase, out var chords, out var error);
            Assert.IsTrue(ok, $"Parse failed: {error}");
            return chords;
        }

        // -------------------------------------------------------------
        // Tier A — sixths + 7sus4
        // -------------------------------------------------------------

        [Test]
        public void Sixths_And_Sus4Seventh_ParseToExplicitQuality()
        {
            var chords = Parse("I6 – im6 – V7sus4");

            Assert.AreEqual(3, chords.Count);

            Assert.AreEqual(ScaleDegree.Tonic, chords[0].degree);
            Assert.AreEqual(ChordQuality.Major6, chords[0].explicitQuality);

            Assert.AreEqual(ScaleDegree.Tonic, chords[1].degree);
            Assert.AreEqual(ChordQuality.Minor6, chords[1].explicitQuality);

            Assert.AreEqual(ScaleDegree.Dominant, chords[2].degree);
            Assert.AreEqual(ChordQuality.Dominant7sus4, chords[2].explicitQuality);
        }

        [Test]
        public void Minor6_AltSpelling_min6_Parses()
        {
            var chords = Parse("Imin6");
            Assert.AreEqual(ChordQuality.Minor6, chords[0].explicitQuality);
        }

        [Test]
        public void ExplicitSixthSuffix_OutranksNumeralCase()
        {
            var major6 = Parse("vi6");
            Assert.AreEqual(ScaleDegree.Submediant, major6[0].degree);
            Assert.AreEqual(ChordQuality.Major6, major6[0].explicitQuality);

            var minor6 = Parse("vim6");
            Assert.AreEqual(ScaleDegree.Submediant, minor6[0].degree);
            Assert.AreEqual(ChordQuality.Minor6, minor6[0].explicitQuality);
        }

        // -------------------------------------------------------------
        // Tier B — ninths
        // -------------------------------------------------------------

        [Test]
        public void Ninths_ParseToExplicitQuality()
        {
            var chords = Parse("I9 – Imaj9 – iim9");

            Assert.AreEqual(3, chords.Count);
            Assert.AreEqual(ChordQuality.Dominant9, chords[0].explicitQuality);
            Assert.AreEqual(ChordQuality.Major9, chords[1].explicitQuality);

            Assert.AreEqual(ScaleDegree.Supertonic, chords[2].degree);
            Assert.AreEqual(ChordQuality.Minor9, chords[2].explicitQuality);
        }

        [Test]
        public void Ninth_AltSpellings_Parse()
        {
            Assert.AreEqual(ChordQuality.Dominant9, Parse("Idom9")[0].explicitQuality);
            Assert.AreEqual(ChordQuality.Major9, Parse("Ima9")[0].explicitQuality);
            Assert.AreEqual(ChordQuality.Minor9, Parse("Imin9")[0].explicitQuality);
        }

        [Test]
        public void ExplicitNinthSuffix_OutranksNumeralCase()
        {
            // 'vi9' = dominant-ninth on the submediant (suffix wins over the
            // lowercase); the minor-ninth needs the explicit 'm9'.
            var dom9 = Parse("vi9");
            Assert.AreEqual(ScaleDegree.Submediant, dom9[0].degree);
            Assert.AreEqual(ChordQuality.Dominant9, dom9[0].explicitQuality);

            var min9 = Parse("vim9");
            Assert.AreEqual(ChordQuality.Minor9, min9[0].explicitQuality);
        }

        // -------------------------------------------------------------
        // Limit — 11/13/add9/6-9 stay OUT (warn-and-downgrade)
        // -------------------------------------------------------------

        [Test]
        public void RemainingExtensions_StillRejected_WarnAndDowngrade()
        {
            var parser = new RomanProgressionParser();

            // case-inference OFF so an unrecognized suffix leaves quality null,
            // which is the clean signal that it was not in the alphabet.

            LogAssert.Expect(LogType.Warning,
                new Regex(@"Unrecognized chord quality suffix '11'"));
            Assert.IsTrue(parser.TryParse("I11", 1f, false, out var c11, out _));
            Assert.IsNull(c11[0].explicitQuality);

            LogAssert.Expect(LogType.Warning,
                new Regex(@"Unrecognized chord quality suffix '13'"));
            Assert.IsTrue(parser.TryParse("I13", 1f, false, out var c13, out _));
            Assert.IsNull(c13[0].explicitQuality);

            LogAssert.Expect(LogType.Warning,
                new Regex(@"Unrecognized chord quality suffix 'add9'"));
            Assert.IsTrue(parser.TryParse("Iadd9", 1f, false, out var cAdd9, out _));
            Assert.IsNull(cAdd9[0].explicitQuality);

            // bare 9 is now allowed, but 6/9 must remain forbidden.
            LogAssert.Expect(LogType.Warning,
                new Regex(@"Unrecognized chord quality suffix '6/9'"));
            Assert.IsTrue(parser.TryParse("I6/9", 1f, false, out var c69, out _));
            Assert.IsNull(c69[0].explicitQuality);
        }
    }
}
#endif
#if UNITY_EDITOR
using NUnit.Framework;
using MidiGenPlay.Composition;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode test for the v2 chord-quality additions in
    /// <see cref="ChordQualityResolver"/>: the new qualities must classify into
    /// their underlying triad family, so the diatonic/borrowed flag is correct.
    /// Without the v2 cases they fall through to <c>TriadFamily.Other</c> and are
    /// always flagged borrowed (even a plain I6 / Imaj9 on the tonic). Covers
    /// Tier A (sixths + 7sus4) and Tier B (ninths).
    ///
    /// Precondition: the v2 cases are added to ChordQualityResolver.GetTriadFamily.
    /// </summary>
    public class ChordQualityResolver_V2Tests
    {
        [Test]
        public void TriadFamily_TierA_MapToUnderlyingTriad()
        {
            Assert.AreEqual(TriadFamily.Major,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Major6));
            Assert.AreEqual(TriadFamily.Minor,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Minor6));
            Assert.AreEqual(TriadFamily.Suspended,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Dominant7sus4));
        }

        [Test]
        public void TriadFamily_TierB_Ninths_MapToUnderlyingTriad()
        {
            // Dominant9 and Major9 share a major triad; Minor9 a minor triad.
            Assert.AreEqual(TriadFamily.Major,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Dominant9));
            Assert.AreEqual(TriadFamily.Major,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Major9));
            Assert.AreEqual(TriadFamily.Minor,
                ChordQualityResolver.GetTriadFamily(ChordQuality.Minor9));
        }

        // -------------------------------------------------------------
        // EDITOR-CASE-1 (D-EC-SEM=B) — case-aware auto resolution
        // -------------------------------------------------------------
        // Precedence: suffix > unambiguous case > auto. The case fixes the
        // FAMILY; the auto mode fixes the SIZE. Overrides fire only when
        // the case CONTRADICTS the diatonic family.

        private static ParsedChord Chord(
            ScaleDegree degree, RomanCaseHint hint,
            ChordQuality? explicitQuality = null)
            => new ParsedChord
            {
                degree = degree,
                explicitQuality = explicitQuality,
                durationMeasures = 1f,
                isRest = false,
                degreeAccidental = 0,
                caseHint = hint,
            };

        [Test]
        public void Case_LowerOverMajorDegree_MinorFamily_SizeFromMode()
        {
            // Ionian IV is diatonically major; "iv" contradicts it.
            var sevenths = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.DiatonicSevenths);
            var triads = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.DiatonicTriads);
            var pc = Chord(ScaleDegree.Subdominant, RomanCaseHint.Lower);

            Assert.AreEqual(ChordQuality.Minor7,
                sevenths.ResolveChordQuality(pc),
                "Sevenths mode: minor family + seventh size = m7.");
            Assert.AreEqual(ChordQuality.Minor,
                triads.ResolveChordQuality(pc),
                "Triads mode: minor family + triad size = m.");
        }

        [Test]
        public void Case_UpperOverMinorDegree_MajorFamily_DominantOnV()
        {
            // Aeolian: iv and v are diatonically minor. "IV" => Major7 under
            // Sevenths; "V" => Dominant7 (functional expectation on V).
            var sevenths = new ChordQualityResolver(
                Tonality.Aeolian, AutoChordQualityMode.DiatonicSevenths);

            Assert.AreEqual(ChordQuality.Major7,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.Subdominant, RomanCaseHint.Upper)));
            Assert.AreEqual(ChordQuality.Dominant7,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.Dominant, RomanCaseHint.Upper)));
        }

        [Test]
        public void Case_AgreeingWithDiatonicFamily_KeepsAutoQuality()
        {
            // Ionian "vii" lowercase: the diatonic seventh is HalfDim7 and
            // Roman convention lowercases diminished degrees too — the case
            // AGREES, so the auto quality survives (NOT flattened to m7).
            var sevenths = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.DiatonicSevenths);

            Assert.AreEqual(ChordQuality.HalfDiminished7,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.LeadingTone, RomanCaseHint.Lower)));
            // And an agreeing lowercase over a plain minor degree is the
            // ordinary diatonic result.
            Assert.AreEqual(ChordQuality.Minor7,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.Supertonic, RomanCaseHint.Lower)));
        }

        [Test]
        public void Case_Mixed_FallsBackToAuto()
        {
            var sevenths = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.DiatonicSevenths);

            Assert.AreEqual(ChordQuality.Major7,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.Subdominant, RomanCaseHint.Mixed)),
                "Mixed case is discarded; the diatonic reading applies.");
        }

        [Test]
        public void Case_SuffixAlwaysWins()
        {
            var sevenths = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.DiatonicSevenths);

            Assert.AreEqual(ChordQuality.Major9,
                sevenths.ResolveChordQuality(
                    Chord(ScaleDegree.Subdominant, RomanCaseHint.Lower,
                        ChordQuality.Major9)),
                "Explicit suffix outranks the case hint.");
        }

        [Test]
        public void Case_NoneAutoMode_IgnoresHint()
        {
            // Literal mode keeps its legacy semantics: the parser already
            // promoted case to an explicit quality there; a stray hint with
            // no explicit quality must not re-activate the feature.
            var literal = new ChordQualityResolver(
                Tonality.Ionian, AutoChordQualityMode.None);

            Assert.AreEqual(ChordQuality.Major,
                literal.ResolveChordQuality(
                    Chord(ScaleDegree.Subdominant, RomanCaseHint.Lower)),
                "None mode: default Major fallback, hint untouched.");
        }
    }
}
#endif
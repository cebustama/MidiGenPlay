#if UNITY_EDITOR
using NUnit.Framework;
using MidiGenPlay.Composition;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;

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
    }
}
#endif
#if UNITY_EDITOR
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using MidiGenPlay.Authoring;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="LaneAliasDictionary"/> (D-L9). Confirms
    /// short-name resolution across kit sections, the SN/SD and side-stick
    /// aliases, case sensitivity, and unknown-token handling (null, no throw).
    /// </summary>
    public class LaneAliasDictionaryTests
    {
        [Test]
        public void TryResolve_CoreKitShortNames_ResolveCorrectly()
        {
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, LaneAliasDictionary.TryResolve("BD"));
            Assert.AreEqual(GeneralMidiPercussion.AcousticSnare, LaneAliasDictionary.TryResolve("SN"));
            Assert.AreEqual(GeneralMidiPercussion.ClosedHiHat, LaneAliasDictionary.TryResolve("HHc"));
            Assert.AreEqual(GeneralMidiPercussion.OpenHiHat, LaneAliasDictionary.TryResolve("HHo"));
            Assert.AreEqual(GeneralMidiPercussion.PedalHiHat, LaneAliasDictionary.TryResolve("HHp"));
        }

        [Test]
        public void TryResolve_SdIsAliasForAcousticSnare()
        {
            Assert.AreEqual(GeneralMidiPercussion.AcousticSnare, LaneAliasDictionary.TryResolve("SD"));
            // SD and SN must resolve to the same member.
            Assert.AreEqual(LaneAliasDictionary.TryResolve("SN"), LaneAliasDictionary.TryResolve("SD"));
        }

        [Test]
        public void TryResolve_ScIsSideStick()
        {
            Assert.AreEqual(GeneralMidiPercussion.SideStick, LaneAliasDictionary.TryResolve("SC"));
        }

        [Test]
        public void TryResolve_CymbalShortNames_ResolveCorrectly()
        {
            Assert.AreEqual(GeneralMidiPercussion.RideCymbal1, LaneAliasDictionary.TryResolve("RC"));
            Assert.AreEqual(GeneralMidiPercussion.RideBell, LaneAliasDictionary.TryResolve("RB"));
            Assert.AreEqual(GeneralMidiPercussion.CrashCymbal1, LaneAliasDictionary.TryResolve("CR"));
            Assert.AreEqual(GeneralMidiPercussion.CrashCymbal2, LaneAliasDictionary.TryResolve("CR2"));
            Assert.AreEqual(GeneralMidiPercussion.SplashCymbal, LaneAliasDictionary.TryResolve("SP"));
            Assert.AreEqual(GeneralMidiPercussion.ChineseCymbal, LaneAliasDictionary.TryResolve("CH"));
        }

        [Test]
        public void TryResolve_TomShortNames_ResolveCorrectly()
        {
            Assert.AreEqual(GeneralMidiPercussion.HighTom, LaneAliasDictionary.TryResolve("HT"));
            Assert.AreEqual(GeneralMidiPercussion.HiMidTom, LaneAliasDictionary.TryResolve("MT"));
            Assert.AreEqual(GeneralMidiPercussion.LowMidTom, LaneAliasDictionary.TryResolve("LMT"));
            Assert.AreEqual(GeneralMidiPercussion.LowTom, LaneAliasDictionary.TryResolve("LT"));
            Assert.AreEqual(GeneralMidiPercussion.HighFloorTom, LaneAliasDictionary.TryResolve("HFT"));
            Assert.AreEqual(GeneralMidiPercussion.LowFloorTom, LaneAliasDictionary.TryResolve("LFT"));
        }

        [Test]
        public void TryResolve_AuxPercussionShortNames_ResolveCorrectly()
        {
            Assert.AreEqual(GeneralMidiPercussion.Cowbell, LaneAliasDictionary.TryResolve("CB"));
            Assert.AreEqual(GeneralMidiPercussion.Tambourine, LaneAliasDictionary.TryResolve("TM"));
            Assert.AreEqual(GeneralMidiPercussion.Claves, LaneAliasDictionary.TryResolve("CL"));
            Assert.AreEqual(GeneralMidiPercussion.HandClap, LaneAliasDictionary.TryResolve("HCL"));
        }

        [Test]
        public void TryResolve_UnknownToken_ReturnsNull()
        {
            Assert.IsNull(LaneAliasDictionary.TryResolve("ZZ"));
            Assert.IsNull(LaneAliasDictionary.TryResolve("NotAnAlias"));
        }

        [Test]
        public void TryResolve_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(LaneAliasDictionary.TryResolve(null));
            Assert.IsNull(LaneAliasDictionary.TryResolve(""));
        }

        [Test]
        public void TryResolve_IsCaseSensitive()
        {
            // "HHc" resolves; the lowercased / uppercased variants must not,
            // because case distinguishes HHc / HHo / HHp.
            Assert.AreEqual(GeneralMidiPercussion.ClosedHiHat, LaneAliasDictionary.TryResolve("HHc"));
            Assert.IsNull(LaneAliasDictionary.TryResolve("hhc"), "lowercase should not resolve");
            Assert.IsNull(LaneAliasDictionary.TryResolve("HHC"), "uppercase should not resolve");
        }

        [Test]
        public void Contains_MatchesTryResolve()
        {
            Assert.IsTrue(LaneAliasDictionary.Contains("BD"));
            Assert.IsFalse(LaneAliasDictionary.Contains("ZZ"));
            Assert.IsFalse(LaneAliasDictionary.Contains(null));
        }

        [Test]
        public void Count_CoversAllRegisteredAliases()
        {
            // 23 short names registered (4 core + side stick + 3 hats + 6 cymbals
            // + 6 toms + 4 aux = 23, counting SD as a distinct key aliasing SN).
            Assert.AreEqual(23, LaneAliasDictionary.Count);
        }
    }
}
#endif
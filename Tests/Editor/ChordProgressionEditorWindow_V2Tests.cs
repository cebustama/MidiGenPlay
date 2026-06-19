#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the v2 chord-quality round-trip in
    /// <see cref="ChordProgressionEditorWindow"/>. The editor must rebuild the
    /// correct Roman suffix for the new qualities — otherwise an asset holding a
    /// Major6 (Tier A) or a Dominant9 (Tier B) would lose its suffix when its
    /// Roman string is regenerated (QualitySuffixForToken). It must also classify
    /// the seventh-bearing qualities for grid arity: Dominant7sus4 and all three
    /// ninths contain a real 7th (Decision A); the two added-6th qualities are
    /// deliberately NOT sevenths, a known grid-display limitation.
    ///
    /// Note on ninths: a ninth is a 5-voice chord, but the grid renders at most
    /// the 4 seventh-chord rows — the 9th never gets its own grid row (a known
    /// delta, same family as the added-6th limitation). The Roman / LLM / import
    /// path stores and plays all five voices correctly.
    ///
    /// Precondition: QualitySuffixForToken and IsSeventhQuality are internal, and
    /// the Editor assembly exposes internals to MidiGenPlay.Tests.Editor (already
    /// the case — the TryFindForbiddenToken guard tests rely on the same).
    ///
    /// Note: the window is created via CreateInstance (not GetWindow) so it is
    /// never shown; this fires OnEnable. If OnEnable grows heavy/asset-touching
    /// init, extract the two mappings into a pure static helper and test that
    /// instead.
    /// </summary>
    public class ChordProgressionEditorWindow_V2Tests
    {
        private ChordProgressionEditorWindow _window;

        [SetUp]
        public void SetUp()
            => _window = ScriptableObject.CreateInstance<ChordProgressionEditorWindow>();

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
        }

        // -------------------------------------------------------------
        // Suffix rebuild round-trips for the v2 qualities
        // -------------------------------------------------------------

        [Test]
        public void RebuildSuffix_TierA_RoundTrips()
        {
            Assert.AreEqual("6", _window.QualitySuffixForToken(ChordQuality.Major6));
            Assert.AreEqual("m6", _window.QualitySuffixForToken(ChordQuality.Minor6));
            Assert.AreEqual("7sus4", _window.QualitySuffixForToken(ChordQuality.Dominant7sus4));
        }

        [Test]
        public void RebuildSuffix_TierB_Ninths_RoundTrips()
        {
            Assert.AreEqual("9", _window.QualitySuffixForToken(ChordQuality.Dominant9));
            Assert.AreEqual("maj9", _window.QualitySuffixForToken(ChordQuality.Major9));
            Assert.AreEqual("m9", _window.QualitySuffixForToken(ChordQuality.Minor9));
        }

        // -------------------------------------------------------------
        // Grid arity (Decision A): qualities that contain a real seventh
        // -------------------------------------------------------------

        [Test]
        public void IsSeventhQuality_SeventhBearing_True_SixthsAreNot()
        {
            Assert.IsTrue(_window.IsSeventhQuality(ChordQuality.Dominant7sus4),
                "7sus4 has a real 7th → grid shows 4 chord-tone rows.");

            // Ninths contain a 7th → treated as sevenths for grid arity (4 rows;
            // the 9th itself has no grid row — known delta).
            Assert.IsTrue(_window.IsSeventhQuality(ChordQuality.Dominant9));
            Assert.IsTrue(_window.IsSeventhQuality(ChordQuality.Major9));
            Assert.IsTrue(_window.IsSeventhQuality(ChordQuality.Minor9));

            // Known limitation: added-6th qualities are 4-voice but not sevenths,
            // so the grid renders them as triads.
            Assert.IsFalse(_window.IsSeventhQuality(ChordQuality.Major6));
            Assert.IsFalse(_window.IsSeventhQuality(ChordQuality.Minor6));
        }
    }
}
#endif
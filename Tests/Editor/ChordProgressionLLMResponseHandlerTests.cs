#if UNITY_EDITOR
using NUnit.Framework;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="ChordProgressionLLMResponseHandler"/>.
    /// Covers the synchronous <c>FromPayload</c> unify path (Full /
    /// ProgressionOnly / Failed) and — the one net-new pattern for L4 — the
    /// D-L4.5 out-of-alphabet token guard, which must hard-fail tokens the
    /// parser would only warn-and-downgrade. Chord twin of
    /// <c>DrumPatternLLMResponseHandlerTests</c> plus the guard suite.
    /// </summary>
    public class ChordProgressionLLMResponseHandlerTests
    {
        private const string FullBlock =
            "- Time signature: FourFour\n" +
            "- Measures (total): 4\n" +
            "- Default duration (measures): 1\n" +
            "- Reference tonality: Ionian\n\n" +
            "```\n" +
            "ii7 – V7 – Imaj7 – vi7\n" +
            "```\n";

        // -----------------------------
        // Unify path
        // -----------------------------

        [Test]
        public void FromPayload_FullBlock_ProducesFullOutcome()
        {
            var outcome = ChordProgressionLLMResponseHandler.FromPayload(FullBlock);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Full, outcome.kind);
            Assert.AreEqual(TimeSignature.FourFour, outcome.timeSignature);
            Assert.AreEqual(4, outcome.measures);
            StringAssert.Contains("ii7", outcome.progression);
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void FromPayload_GarbledCard_ProducesProgressionOnlyOutcome()
        {
            string garbled =
                "Enjoy!\n\n```\nI – V – vi – IV\n```\n";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(garbled);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.ProgressionOnly, outcome.kind);
            StringAssert.Contains("I – V – vi – IV", outcome.progression);
        }

        [Test]
        public void FromPayload_NoBlock_ProducesFailedOutcome()
        {
            var outcome = ChordProgressionLLMResponseHandler.FromPayload(
                "Just prose, no fenced block here.");

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind);
            Assert.IsFalse(outcome.Success);
            Assert.IsNotEmpty(outcome.displayWarnings);
        }

        // -----------------------------
        // D-L4.5 guard (the net-new pattern)
        // -----------------------------

        [Test]
        public void Guard_OutOfAlphabetSuffix_ForcesFailed_NotApplied()
        {
            // "V13" is a real degree with an extended suffix the parser does NOT
            // accept — it would warn-and-downgrade to a plain V. The handler must
            // block it instead.
            string withBadToken =
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "ii7 – V13 – Imaj7 – vi7\n" +
                "```\n";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(withBadToken);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind,
                "Out-of-alphabet token must hard-fail, not apply a downgraded chord.");
            Assert.IsNotEmpty(outcome.displayWarnings);
        }

        [Test]
        public void Guard_AllInAlphabetSuffixes_PassThrough()
        {
            // A progression exercising several legal suffixes; must NOT be blocked.
            string allLegal =
                "- Time signature: FourFour\n" +
                "- Measures (total): 8\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "Imaj7 – ii7 – iiø7 – V7 – vi – bVII – #iv dim7 – Isus4\n" +
                "```\n";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(allLegal);

            Assert.AreNotEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind,
                "Legal suffixes must not be blocked by the guard.");
        }

        [Test]
        public void Guard_RestTokens_AreNotFlagged()
        {
            string withRest =
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "I – S (1) – V – I\n" +
                "```\n";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(withRest);

            Assert.AreNotEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind);
        }

        [Test]
        public void Guard_OutOfAlphabetSuffix_ForcesFailed_ViaFromPayload()
        {
            // End-to-end: the public path must surface the block as Failed.
            string withBadToken =
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n" +
                "- Default duration (measures): 1\n\n" +
                "```\n" +
                "ii7 – V13 – Imaj7 – vi7\n" +
                "```\n";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(withBadToken);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind,
                "Out-of-alphabet token must hard-fail, not apply a downgraded chord.");
            Assert.IsNotEmpty(outcome.displayWarnings);
        }

        // -----------------------------
        // Guard unit tests (direct, via internal helper — D-L4.6 = B)
        // -----------------------------

        [Test]
        public void TryFindForbiddenToken_DetectsExtendedChord()
        {
            bool found = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "ii7 – V13 – I", out string offending);
            Assert.IsTrue(found);
            StringAssert.Contains("V13", offending);
        }

        [Test]
        public void TryFindForbiddenToken_DetectsSlashChord()
        {
            bool found = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "I – V/V – I", out _);
            Assert.IsTrue(found);
        }

        [Test]
        public void TryFindForbiddenToken_AcceptsPlainTriadsAndSevenths()
        {
            bool found = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "I – ii7 – V7 – Imaj7 – vi", out _);
            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindForbiddenToken_AcceptsHalfDiminishedBothSpellings()
        {
            Assert.IsFalse(ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "iiø7 – V7 – i", out _));
            Assert.IsFalse(ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "iim7b5 – V7 – i", out _));
        }

        [Test]
        public void TryFindForbiddenToken_AcceptsAccidentalsAndDurations()
        {
            bool found = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "bVII (2) – #iv dim7 (0.5) – I", out _);
            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindForbiddenToken_AcceptsRests()
        {
            bool found = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "I – S – R (1) – V", out _);
            Assert.IsFalse(found);
        }
    }
}
#endif
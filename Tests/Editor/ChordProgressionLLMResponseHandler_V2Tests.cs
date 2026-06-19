#if UNITY_EDITOR
using NUnit.Framework;
using MidiGenPlay.Authoring;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the v2 chord-quality additions to the D-L4.5
    /// zero-warning guard in <see cref="ChordProgressionLLMResponseHandler"/>.
    /// The guard's allowlist (<c>AllowedSuffixes</c>) and the prompt alphabet and
    /// <c>RomanProgressionParser.TryParseQualitySuffix</c> must stay in lockstep:
    /// the new qualities — Tier A (6 / m6 / 7sus4) and Tier B (9 / maj9 / m9) —
    /// must pass the guard, while the remaining forbidden extensions
    /// (add9 / 11 / 13 / 6-9) must still trip it.
    ///
    /// Accesses <c>internal</c> <c>TryFindForbiddenToken</c> via
    /// InternalsVisibleTo (the editor asm already exposes internals to the test
    /// asm — see the existing handler guard tests).
    ///
    /// Precondition: the v2 edits to ChordProgressionLLMResponseHandler are applied.
    /// </summary>
    public class ChordProgressionLLMResponseHandler_V2Tests
    {
        // -------------------------------------------------------------
        // New v2 suffixes pass the guard
        // -------------------------------------------------------------

        [Test]
        public void Guard_Accepts_TierA_Suffixes()
        {
            Assert.IsFalse(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                    "I6 – vim6 – V7sus4", out _),
                "Tier A suffixes (6, m6, 7sus4) must not be flagged as out-of-alphabet.");
        }

        [Test]
        public void Guard_Accepts_TierB_Ninths()
        {
            Assert.IsFalse(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                    "V9 – Imaj9 – iim9", out _),
                "Tier B ninths (9, maj9, m9) must not be flagged as out-of-alphabet.");
        }

        [Test]
        public void Guard_Accepts_Ninth_AltSpellings()
        {
            Assert.IsFalse(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                    "Idom9 – Ima9 – Imin9", out _));
        }

        // -------------------------------------------------------------
        // Remaining extensions still trip the guard
        // -------------------------------------------------------------

        [Test]
        public void Guard_Rejects_Add9()
        {
            Assert.IsTrue(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                    "I – Iadd9 – V", out var bad));
            StringAssert.Contains("add9", bad);
        }

        [Test]
        public void Guard_Rejects_Eleventh_And_Thirteenth()
        {
            Assert.IsTrue(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken("V11", out _));
            Assert.IsTrue(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken("V13", out _));
        }

        [Test]
        public void Guard_Rejects_SixNine()
        {
            Assert.IsTrue(
                ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                    "I – I6/9 – V", out var bad));
            StringAssert.Contains("6/9", bad);
        }

        // -------------------------------------------------------------
        // FromPayload end-to-end: ninths apply, add9 fails
        // -------------------------------------------------------------

        [Test]
        public void FromPayload_NinthProgression_NotBlocked()
        {
            string payload =
                "**Setup (Roman mode):**\n" +
                "- Time signature: FourFour\n" +
                "- Measures (total): 4\n" +
                "- Default duration (measures): 1\n" +
                "- Reference tonality: Ionian\n\n" +
                "**Progression (paste into the Roman string field):**\n" +
                "```\nImaj9 – vi9 – iim9 – V9\n```";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(payload);

            Assert.AreNotEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind,
                "A valid ninth progression must not be blocked by the guard.");
            Assert.IsTrue(outcome.Success);
            StringAssert.Contains("maj9", outcome.progression);
        }

        [Test]
        public void FromPayload_Add9_IsBlocked()
        {
            string payload =
                "**Progression (paste into the Roman string field):**\n" +
                "```\nI – Iadd9 – V – I\n```";

            var outcome = ChordProgressionLLMResponseHandler.FromPayload(payload);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed, outcome.kind,
                "add9 is out-of-alphabet and must hard-fail rather than silently downgrade.");
        }
    }
}
#endif

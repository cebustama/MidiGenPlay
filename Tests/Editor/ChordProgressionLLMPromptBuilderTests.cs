#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="ChordProgressionLLMPromptBuilder"/>.
    /// Pure-function tests over the build path: happy path (parameter
    /// substitution), missing-genre fallback, sub-style cue inclusion,
    /// unknown-cue failure, budget overflow, and free-text inclusion. Chord twin
    /// of <c>DrumPatternLLMPromptBuilderTests</c>.
    /// </summary>
    public class ChordProgressionLLMPromptBuilderTests
    {
        // -----------------------------
        // Test fixture
        // -----------------------------

        private static ChordGenreVocabularySO BuildTestVocabulary()
        {
            var vocab = ScriptableObject.CreateInstance<ChordGenreVocabularySO>();

            var jazz = new ChordGenreEntry
            {
                genreName = "jazz",
                defaultMeter = TimeSignature.FourFour,
                defaultMeasures = 4,
                defaultDurationMeasures = 1f,
                styleDescriptors = "Extended harmony; ii-V-I motion; chromatic approach.",
                voicingHints = "Favour 7th chords; tonic is maj7, supertonic is m7.",
                cadenceCues = "End on an authentic V7 - I.",
                characteristicProgressions = new List<string>
                {
                    "ii7 – V7 – Imaj7 – vi7",
                    "iiø7 – V7 – i",
                },
                subStyleCues = new List<ChordSubStyleCue>
                {
                    new ChordSubStyleCue
                    {
                        name = "modal jazz",
                        guidance = "Static harmony; long dwell on a single modal centre.",
                        measuresOverride = 0,
                    },
                },
            };

            vocab.genres = new List<ChordGenreEntry> { jazz };
            return vocab;
        }

        private static ChordProgressionLLMPromptBuilder.Input MakeInput(
            string genre = "jazz",
            string cue = null,
            int measures = 4,
            string freeText = null,
            int budget = 0)
        {
            return new ChordProgressionLLMPromptBuilder.Input(
                genreName: genre,
                subStyleCueName: cue,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: measures,
                defaultDurationMeasures: 1f,
                userFreeText: freeText,
                maxCharBudget: budget);
        }

        // -----------------------------
        // Tests
        // -----------------------------

        [Test]
        public void Build_HappyPath_ReturnsSuccess_AndSubstitutesAllParameters()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(vocab, MakeInput());

            Assert.IsTrue(result.success, result.failureReason);
            Assert.IsNotEmpty(result.systemPrompt);
            Assert.IsNotEmpty(result.userPrompt);

            // Mechanical parameters substituted into the user prompt.
            StringAssert.Contains("jazz", result.userPrompt);
            StringAssert.Contains("FourFour", result.userPrompt);
            StringAssert.Contains("4", result.userPrompt); // measures
            // Genre knowledge surfaced.
            StringAssert.Contains("ii7 – V7 – Imaj7 – vi7", result.userPrompt);
            StringAssert.Contains("authentic V7", result.userPrompt); // cadence cue
        }

        [Test]
        public void Build_MissingGenre_ReturnsFailure_WithExplanatoryReason()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(vocab, MakeInput(genre: "polka"));

            Assert.IsFalse(result.success);
            StringAssert.Contains("polka", result.failureReason);
        }

        [Test]
        public void Build_WithSubStyleCue_IncludesGuidanceAndHeader()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(cue: "modal jazz"));

            Assert.IsTrue(result.success, result.failureReason);
            StringAssert.Contains("modal jazz", result.userPrompt);
            StringAssert.Contains("Static harmony", result.userPrompt);
        }

        [Test]
        public void Build_WithUnknownSubStyleCue_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(cue: "bebop-on-mars"));

            Assert.IsFalse(result.success);
            StringAssert.Contains("bebop-on-mars", result.failureReason);
        }

        [Test]
        public void Build_OverBudget_ReturnsFailure_WithBudgetInReason()
        {
            var vocab = BuildTestVocabulary();
            // Budget far below the static system prompt length.
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(budget: 50));

            Assert.IsFalse(result.success);
            StringAssert.Contains("budget", result.failureReason.ToLowerInvariant());
        }

        [Test]
        public void Build_NoBudget_HappyPath_DoesNotFailOnSize()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(budget: 0));

            Assert.IsTrue(result.success, result.failureReason);
            Assert.Greater(result.totalCharCount, 0);
        }

        [Test]
        public void Build_NullVocabulary_ReturnsFailure()
        {
            var result = ChordProgressionLLMPromptBuilder.Build(null, MakeInput());
            Assert.IsFalse(result.success);
        }

        [Test]
        public void Build_EmptyGenreName_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(genre: ""));
            Assert.IsFalse(result.success);
        }

        [Test]
        public void Build_WithUserFreeText_IncludesAdditionalDirectionSection()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(
                vocab, MakeInput(freeText: "moody, end unresolved"));

            Assert.IsTrue(result.success, result.failureReason);
            StringAssert.Contains("moody, end unresolved", result.userPrompt);
        }

        [Test]
        public void Build_SystemPrompt_DeclaresExactLengthPolicy()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(vocab, MakeInput());

            Assert.IsTrue(result.success, result.failureReason);
            // D-L4.4 reinforcement present in the system prompt.
            StringAssert.Contains("exactly", result.systemPrompt.ToLowerInvariant());
        }

        [Test]
        public void Build_SystemPrompt_ForbidsExtendedChords()
        {
            var vocab = BuildTestVocabulary();
            var result = ChordProgressionLLMPromptBuilder.Build(vocab, MakeInput());

            Assert.IsTrue(result.success, result.failureReason);
            // Parser cannot handle 9/11/13 — prompt must forbid them.
            StringAssert.Contains("13", result.systemPrompt);
        }
    }
}
#endif
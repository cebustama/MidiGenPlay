#if UNITY_EDITOR
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="DrumPatternLLMPromptBuilder"/>.
    /// Pure-function tests over the build path: happy path (parameter
    /// substitution), missing-genre fallback, sub-style cue inclusion,
    /// budget overflow detection, and a GM-number sanity check confirming
    /// (int)GeneralMidiPercussion.X yields the canonical MIDI number.
    /// </summary>
    public class DrumPatternLLMPromptBuilderTests
    {
        // -----------------------------
        // Test fixture
        // -----------------------------

        /// <summary>
        /// Build a minimal vocabulary with a single funk genre carrying the
        /// fields the tests assert against. Sized to match R1's funk entry
        /// for shape consistency.
        /// </summary>
        private static RhythmGenreVocabularySO BuildTestVocabulary()
        {
            var vocab = ScriptableObject.CreateInstance<RhythmGenreVocabularySO>();

            var funk = new GenreEntry
            {
                genreName = "funk",
                defaultMeter = TimeSignature.FourFour,
                defaultMeasures = 2,
                defaultSubdivisions = 4,
                defaultLaneComposition = new List<LaneSpec>
                {
                    new LaneSpec { instrument = GeneralMidiPercussion.BassDrum1,     defaultVelocity = 100 },
                    new LaneSpec { instrument = GeneralMidiPercussion.AcousticSnare, defaultVelocity = 110 },
                    new LaneSpec { instrument = GeneralMidiPercussion.ClosedHiHat,   defaultVelocity =  80 },
                    new LaneSpec { instrument = GeneralMidiPercussion.OpenHiHat,     defaultVelocity =  90 },
                },
                characteristicCells = new List<GlyphCell>
                {
                    new GlyphCell { laneIndex = 0, variant = "default", cell = "x..x..x...x....." },
                    new GlyphCell { laneIndex = 1, variant = "default", cell = "....x.......x..." },
                },
                subStyleCues = new List<SubStyleCue>
                {
                    new SubStyleCue
                    {
                        name = "JB-style",
                        guidance = "Dense kick syncopation; heavy ghost-note pocket on snare.",
                        subdivisionsOverride = 0,
                    },
                    new SubStyleCue
                    {
                        name = "shuffle",
                        guidance = "Triplet feel.",
                        subdivisionsOverride = 3,
                    },
                },
                velocityConventions = "Snare backbeat at lane default. Ghost notes use 'o'.",
                styleDescriptors = "Pocket. Syncopation. Ghost notes are the defining gesture.",
            };

            vocab.genres = new List<GenreEntry> { funk };
            return vocab;
        }

        /// <summary>
        /// Build a happy-path Input pointing at the test vocabulary's funk entry,
        /// at the funk genre defaults (4/4, 2 measures, 4 subdivisions, 4 lanes).
        /// </summary>
        private static DrumPatternLLMPromptBuilder.Input BuildHappyPathInput(
            RhythmGenreVocabularySO vocab,
            string subStyleCueName = null,
            string userFreeText = null,
            int maxCharBudget = 0)
        {
            var lanes = vocab.genres[0].defaultLaneComposition;
            return new DrumPatternLLMPromptBuilder.Input(
                genreName: "funk",
                subStyleCueName: subStyleCueName,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: 2,
                subdivisions: 4,
                laneComposition: lanes,
                userFreeText: userFreeText,
                maxCharBudget: maxCharBudget);
        }

        // -----------------------------
        // Test 1 — Happy path (parameter substitution)
        // -----------------------------

        [Test]
        public void Build_HappyPath_ReturnsSuccess_AndSubstitutesAllParameters()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsTrue(result.success, $"expected success; got: {result.failureReason}");
            Assert.IsNotEmpty(result.systemPrompt, "system prompt should be populated");
            Assert.IsNotEmpty(result.userPrompt, "user prompt should be populated");
            Assert.Greater(result.totalCharCount, 0, "totalCharCount should be positive");

            // Genre header
            StringAssert.Contains("## Genre: funk", result.userPrompt);

            // Mechanical parameters
            StringAssert.Contains("FourFour", result.userPrompt);
            StringAssert.Contains("beatsPerMeasure = 4", result.userPrompt);
            StringAssert.Contains("- Measures: 2", result.userPrompt);
            StringAssert.Contains("- Subdivisions per beat: 4", result.userPrompt);
            StringAssert.Contains("totalSteps = 4 × 2 × 4 = 32", result.userPrompt);

            // Lane composition — all four instruments
            StringAssert.Contains("BassDrum1", result.userPrompt);
            StringAssert.Contains("AcousticSnare", result.userPrompt);
            StringAssert.Contains("ClosedHiHat", result.userPrompt);
            StringAssert.Contains("OpenHiHat", result.userPrompt);

            // Default velocities surfaced
            StringAssert.Contains("default velocity 100", result.userPrompt);
            StringAssert.Contains("default velocity 110", result.userPrompt);

            // No cue → no sub-style header
            StringAssert.DoesNotContain("Sub-style guidance", result.userPrompt);

            // No userFreeText → no additional-direction header
            StringAssert.DoesNotContain("Additional user direction", result.userPrompt);
        }

        // -----------------------------
        // Test 2 — Missing genre
        // -----------------------------

        [Test]
        public void Build_MissingGenre_ReturnsFailure_WithExplanatoryReason()
        {
            var vocab = BuildTestVocabulary();
            var lanes = vocab.genres[0].defaultLaneComposition;
            var input = new DrumPatternLLMPromptBuilder.Input(
                genreName: "reggae",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4, measures: 2, subdivisions: 4,
                laneComposition: lanes,
                userFreeText: null);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsFalse(result.success, "expected failure for unknown genre");
            Assert.IsEmpty(result.systemPrompt, "systemPrompt should be empty on failure");
            Assert.IsEmpty(result.userPrompt, "userPrompt should be empty on failure");
            StringAssert.Contains("reggae", result.failureReason);
            StringAssert.Contains("not found", result.failureReason);
        }

        // -----------------------------
        // Test 3 — Sub-style cue inclusion
        // -----------------------------

        [Test]
        public void Build_WithSubStyleCue_IncludesGuidanceAndHeader()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab, subStyleCueName: "JB-style");

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsTrue(result.success, $"expected success; got: {result.failureReason}");
            StringAssert.Contains("## Genre: funk (sub-style: JB-style)", result.userPrompt);
            StringAssert.Contains("## Sub-style guidance (JB-style)", result.userPrompt);
            StringAssert.Contains("Dense kick syncopation", result.userPrompt);
        }

        [Test]
        public void Build_WithUnknownSubStyleCue_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab, subStyleCueName: "bossa");

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsFalse(result.success, "expected failure for cue not under funk");
            StringAssert.Contains("bossa", result.failureReason);
            StringAssert.Contains("funk", result.failureReason);
            StringAssert.Contains("not found", result.failureReason);
        }

        // -----------------------------
        // Test 4 — Budget overflow detection
        // -----------------------------

        [Test]
        public void Build_OverBudget_ReturnsFailure_WithBudgetInReason()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab, maxCharBudget: 100);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsFalse(result.success, "expected failure when budget exceeded");
            StringAssert.Contains("exceeds char budget", result.failureReason);
            StringAssert.Contains("/100", result.failureReason);
        }

        [Test]
        public void Build_NoBudget_HappyPath_DoesNotFailOnSize()
        {
            // Sanity check: confirm maxCharBudget = 0 (default) disables enforcement.
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab, maxCharBudget: 0);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsTrue(result.success, $"expected success with budget disabled; got: {result.failureReason}");
        }

        // -----------------------------
        // Test 5 — GM number sanity check
        // -----------------------------

        [Test]
        public void Build_LaneInstrumentsRenderCanonicalGMNumbers()
        {
            // Confirms (int)GeneralMidiPercussion.X yields the canonical GM
            // numbers the prompt advertises. If DryWetMidi ever changes the
            // enum's underlying values, this test surfaces the breakage at
            // build time rather than at LLM-response time.
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsTrue(result.success);
            StringAssert.Contains("BassDrum1 (GM 36)", result.userPrompt);
            StringAssert.Contains("AcousticSnare (GM 38)", result.userPrompt);
            StringAssert.Contains("ClosedHiHat (GM 42)", result.userPrompt);
            StringAssert.Contains("OpenHiHat (GM 46)", result.userPrompt);
        }

        // -----------------------------
        // Defensive validation tests
        // -----------------------------

        [Test]
        public void Build_NullVocabulary_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab);

            var result = DrumPatternLLMPromptBuilder.Build(null, input);

            Assert.IsFalse(result.success);
            StringAssert.Contains("Vocabulary is null", result.failureReason);
        }

        [Test]
        public void Build_EmptyGenreName_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var lanes = vocab.genres[0].defaultLaneComposition;
            var input = new DrumPatternLLMPromptBuilder.Input(
                genreName: "",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4, measures: 2, subdivisions: 4,
                laneComposition: lanes,
                userFreeText: null);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsFalse(result.success);
            StringAssert.Contains("genreName is empty", result.failureReason);
        }

        [Test]
        public void Build_EmptyLaneComposition_ReturnsFailure()
        {
            var vocab = BuildTestVocabulary();
            var input = new DrumPatternLLMPromptBuilder.Input(
                genreName: "funk",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4, measures: 2, subdivisions: 4,
                laneComposition: new List<LaneSpec>(),
                userFreeText: null);

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsFalse(result.success);
            StringAssert.Contains("laneComposition is empty", result.failureReason);
        }

        // -----------------------------
        // User free-text inclusion
        // -----------------------------

        [Test]
        public void Build_WithUserFreeText_IncludesAdditionalDirectionSection()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildHappyPathInput(vocab, userFreeText: "Add ride. No open hat.");

            var result = DrumPatternLLMPromptBuilder.Build(vocab, input);

            Assert.IsTrue(result.success, $"expected success; got: {result.failureReason}");
            StringAssert.Contains("## Additional user direction", result.userPrompt);
            StringAssert.Contains("Add ride. No open hat.", result.userPrompt);
        }
    }
}
#endif
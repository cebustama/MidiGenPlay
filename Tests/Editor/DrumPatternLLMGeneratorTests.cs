#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the async generation path of
    /// <see cref="DrumPatternLLMGenerator"/>, driven by <see cref="FakeLLMClient"/>
    /// (D-L3.2 = A). These make SMR-L5 deterministic: an invalid-DSL LLM response
    /// is simulated and asserted to route through <see cref="DrumPatternTextParser"/>
    /// with location-bearing warnings, never throwing.
    /// </summary>
    /// <remarks>
    /// All payloads use CRLF (<c>\r\n</c>) line endings to guard the L2 split
    /// regression (char-array split treated <c>\r\n</c> as two separators).
    /// Every test asserts <see cref="FakeLLMClient.WasCalled"/> so the suite fails
    /// loudly if the <c>PromptExecutionHelper</c> delegation ever stops reaching
    /// the client.
    /// </remarks>
    public class DrumPatternLLMGeneratorTests
    {
        // -----------------------------
        // Fixture
        // -----------------------------

        private static RhythmGenreVocabularySO BuildTestVocabulary()
        {
            var vocab = ScriptableObject.CreateInstance<RhythmGenreVocabularySO>();
            vocab.genres = new List<GenreEntry>
            {
                new GenreEntry
                {
                    genreName = "funk",
                    defaultMeter = TimeSignature.FourFour,
                    defaultMeasures = 1,
                    defaultSubdivisions = 4,
                    defaultLaneComposition = new List<LaneSpec>
                    {
                        new LaneSpec { instrument = GeneralMidiPercussion.BassDrum1,     defaultVelocity = 100 },
                        new LaneSpec { instrument = GeneralMidiPercussion.AcousticSnare, defaultVelocity = 110 },
                    },
                    styleDescriptors = "test",
                },
            };
            return vocab;
        }

        /// <summary>
        /// Input for a 1-measure 4/4 4-subdivision pattern with 2 lanes
        /// (16 total steps). Lane count (2) is the contract the DSL block must match.
        /// </summary>
        private static DrumPatternLLMPromptBuilder.Input BuildInput(RhythmGenreVocabularySO vocab)
        {
            return new DrumPatternLLMPromptBuilder.Input(
                genreName: "funk",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: 1,
                subdivisions: 4,
                laneComposition: vocab.genres[0].defaultLaneComposition,
                userFreeText: null,
                maxCharBudget: 0);
        }

        /// <summary>Wrap two lane lines in a fenced block with CRLF endings.</summary>
        private static string FencedCrlf(string laneA, string laneB) =>
            "Here is your pattern:\r\n\r\n```\r\n" + laneA + "\r\n" + laneB + "\r\n```\r\n";

        // -----------------------------
        // SMR-L5 (a) — invalid glyphs, correct lane count → parser warnings w/ location
        // -----------------------------

        [Test]
        public async Task InvalidGlyphs_RouteThroughParser_AsLocatedWarnings()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildInput(vocab);

            // 16 steps per lane; lane 0 carries two illegal glyphs ('q', 'z').
            // Correct lane count (2) so the generator reaches the parse stage.
            var fake = new FakeLLMClient(FencedCrlf(
                "x..q..x...z.....",
                "....x.......x..."));

            var result = await DrumPatternLLMGenerator.GenerateAsync(fake, vocab, input);

            Assert.IsTrue(fake.WasCalled,
                "FakeLLMClient was never invoked — PromptExecutionHelper delegation may have changed.");
            Assert.IsTrue(result.success, "Correct lane count should reach a parsed (warned) result.");
            Assert.IsNotEmpty(result.warnings, "Illegal glyphs must produce parser warnings.");

            var unknown = result.warnings
                .Where(w => w.kind == DrumPatternTextWarningKind.UnknownGlyph)
                .ToList();
            Assert.AreEqual(2, unknown.Count, "Both illegal glyphs ('q','z') should warn.");

            // Location info is present (the SMR-L5 'with location info' requirement).
            foreach (var w in unknown)
            {
                Assert.AreEqual(0, w.laneIndex, "Illegal glyphs were on lane 0.");
                Assert.GreaterOrEqual(w.columnIndex, 0, "Column index must carry the step location.");
            }
        }

        // -----------------------------
        // SMR-L5 (b) — no fenced DSL block → graceful failure, no throw
        // -----------------------------

        [Test]
        public async Task NoFencedBlock_FailsGracefully_NoThrow()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildInput(vocab);

            // Prose only — the model ignored the DSL-only instruction.
            var fake = new FakeLLMClient(
                "I'm sorry, I can't produce that pattern right now.\r\n");

            var result = await DrumPatternLLMGenerator.GenerateAsync(fake, vocab, input);

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsFalse(result.success, "A response with no fenced DSL block must fail.");
            Assert.IsNotEmpty(result.failureReason, "Failure must carry an explanatory reason.");
        }

        // -----------------------------
        // SMR-L5 (c) — lane-count mismatch → located failure
        // -----------------------------

        [Test]
        public async Task LaneCountMismatch_Fails_WithCounts()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildInput(vocab); // expects 2 lanes

            // Only one lane line in the block.
            var fake = new FakeLLMClient(
                "```\r\nx..x..x...x.....\r\n```\r\n");

            var result = await DrumPatternLLMGenerator.GenerateAsync(fake, vocab, input);

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsFalse(result.success, "Lane-count mismatch must fail.");
            StringAssert.Contains("Lane count mismatch", result.failureReason);
        }

        // -----------------------------
        // Control — happy path via the fake proves the seam carries good data too
        // -----------------------------

        [Test]
        public async Task ValidDsl_ViaFake_Succeeds_NoWarnings()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildInput(vocab);

            var fake = new FakeLLMClient(FencedCrlf(
                "x..x..x...x.....",
                "....x.......x..."));

            var result = await DrumPatternLLMGenerator.GenerateAsync(fake, vocab, input);

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsTrue(result.success);
            Assert.IsEmpty(result.warnings, "Valid 16-step lanes should parse cleanly.");
            Assert.AreEqual(2, result.parsedLanes.Length);
            Assert.AreEqual(100, result.inputTokens);
            Assert.AreEqual(50, result.outputTokens);
        }

        // -----------------------------
        // Null-client guard (generator contract)
        // -----------------------------

        [Test]
        public async Task NullClient_Fails_NoThrow()
        {
            var vocab = BuildTestVocabulary();
            var input = BuildInput(vocab);

            var result = await DrumPatternLLMGenerator.GenerateAsync(null, vocab, input);

            Assert.IsFalse(result.success);
            StringAssert.Contains("ILLMClient is null", result.failureReason);
        }
    }
}
#endif
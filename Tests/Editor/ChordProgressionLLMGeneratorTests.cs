#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the async path of
    /// <see cref="ChordProgressionLLMGenerator"/>, driven by
    /// <see cref="FakeLLMClient"/>. Every test that supplies a client asserts
    /// <see cref="FakeLLMClient.WasCalled"/> so the suite fails loudly if the
    /// PromptExecutionHelper delegation contract changes and the double is no
    /// longer on the call path. Chord twin of <c>DrumPatternLLMGeneratorTests</c>.
    /// </summary>
    public class ChordProgressionLLMGeneratorTests
    {
        private static ChordGenreVocabularySO BuildVocab()
        {
            var vocab = ScriptableObject.CreateInstance<ChordGenreVocabularySO>();
            vocab.genres = new List<ChordGenreEntry>
            {
                new ChordGenreEntry
                {
                    genreName = "jazz",
                    defaultMeter = TimeSignature.FourFour,
                    defaultMeasures = 4,
                    defaultDurationMeasures = 1f,
                    characteristicProgressions = new List<string> { "ii7 – V7 – Imaj7" },
                },
            };
            return vocab;
        }

        private static ChordProgressionLLMPromptBuilder.Input MakeInput() =>
            new ChordProgressionLLMPromptBuilder.Input(
                genreName: "jazz",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: 4,
                defaultDurationMeasures: 1f,
                userFreeText: null,
                maxCharBudget: 0);

        private static string Fenced(string body) => $"Here you go:\n\n```\n{body}\n```\n";
        private static string FencedCrlf(string body) =>
            $"Here you go:\r\n\r\n```\r\n{body}\r\n```\r\n";

        [Test]
        public async Task ValidProgression_ViaFake_Succeeds_AndParses()
        {
            var vocab = BuildVocab();
            var fake = new FakeLLMClient(FencedCrlf("ii7 – V7 – Imaj7 – vi7"));

            var result = await ChordProgressionLLMGenerator.GenerateAsync(fake, vocab, MakeInput());

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsTrue(result.success, result.failureReason);
            Assert.AreEqual(4, result.parsedChords.Count);
            Assert.AreEqual(4, result.targetMeasures);
            Assert.AreEqual(100, result.inputTokens);
            Assert.AreEqual(50, result.outputTokens);
        }

        [Test]
        public async Task NoFencedBlock_FailsGracefully_NoThrow()
        {
            var vocab = BuildVocab();
            var fake = new FakeLLMClient("Sorry, here is some prose with no code fence.");

            var result = await ChordProgressionLLMGenerator.GenerateAsync(fake, vocab, MakeInput());

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsFalse(result.success);
            StringAssert.Contains("fenced", result.failureReason.ToLowerInvariant());
        }

        [Test]
        public async Task EmptyOutput_Fails_NoThrow()
        {
            var vocab = BuildVocab();
            var fake = new FakeLLMClient("");

            var result = await ChordProgressionLLMGenerator.GenerateAsync(fake, vocab, MakeInput());

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsFalse(result.success);
        }

        [Test]
        public async Task UnparseableRoman_Fails_WithParseError()
        {
            var vocab = BuildVocab();
            // "Q9" has no Roman core → parser TryParse returns false.
            var fake = new FakeLLMClient(Fenced("Q9 – ZZ"));

            var result = await ChordProgressionLLMGenerator.GenerateAsync(fake, vocab, MakeInput());

            Assert.IsTrue(fake.WasCalled, "FakeLLMClient was never invoked.");
            Assert.IsFalse(result.success);
            StringAssert.Contains("parse", result.failureReason.ToLowerInvariant());
        }

        [Test]
        public async Task NullClient_Fails_NoThrow()
        {
            var vocab = BuildVocab();
            var result = await ChordProgressionLLMGenerator.GenerateAsync(null, vocab, MakeInput());
            Assert.IsFalse(result.success);
            StringAssert.Contains("ILLMClient", result.failureReason);
        }

        [Test]
        public async Task PromptBuildFailure_ShortCircuits_BeforeClientCall()
        {
            var vocab = BuildVocab();
            var fake = new FakeLLMClient(Fenced("ii7 – V7"));
            // Unknown genre → prompt build fails before any client call.
            var badInput = new ChordProgressionLLMPromptBuilder.Input(
                genreName: "klezmer",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: 4,
                defaultDurationMeasures: 1f,
                userFreeText: null,
                maxCharBudget: 0);

            var result = await ChordProgressionLLMGenerator.GenerateAsync(fake, vocab, badInput);

            Assert.IsFalse(result.success);
            Assert.IsFalse(fake.WasCalled, "Client must not be called when the prompt build fails.");
            StringAssert.Contains("Prompt build failed", result.failureReason);
        }
    }
}
#endif
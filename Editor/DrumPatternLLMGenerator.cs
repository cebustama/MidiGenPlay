#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BCS.LLM.Core.Clients;
using BCS.LLM.Core.Execution;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Wraps LLM Core's <see cref="PromptExecutionHelper"/> for drum pattern
    /// generation. Pipeline:
    /// <list type="number">
    ///   <item><description>Build prompts via <see cref="DrumPatternLLMPromptBuilder"/>.</description></item>
    ///   <item><description>Send via <see cref="PromptExecutionHelper.ExecuteAsync(ILLMClient, string, string, PromptExecutionOptions)"/> (single-shot, D-L10 = α).</description></item>
    ///   <item><description>Extract response text from <see cref="LLMCompletionResult.OutputText"/>.</description></item>
    ///   <item><description>Strip prose / code fences, locate the DSL block.</description></item>
    ///   <item><description>Split DSL into per-lane glyph strings.</description></item>
    ///   <item><description>Parse each lane via <see cref="DrumPatternTextParser"/>.</description></item>
    ///   <item><description>Return parsed lanes + warnings + token counts.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// L1 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c>. No retry, no
    /// orchestration, no editor UI — those are L2 territory. The wrapper is
    /// async; the L1 console harness blocks via <c>.GetAwaiter().GetResult()</c>.
    /// </remarks>
    public static class DrumPatternLLMGenerator
    {
        /// <summary>
        /// Outcome of a generation pass. Token counts are populated whenever the
        /// LLM call returned a completion (including parse-failure paths that
        /// completed the network round-trip).
        /// </summary>
        public readonly struct Result
        {
            public readonly bool success;

            /// <summary>Raw LLM response before any cleaning.</summary>
            public readonly string rawResponse;

            /// <summary>The DSL block extracted from the response (lines only).</summary>
            public readonly string cleanedDslBlock;

            /// <summary>Per-lane parsed StepState lists. parsedLanes[i] is lane i, in setup-card order.</summary>
            public readonly List<DrumPatternData.StepState>[] parsedLanes;

            /// <summary>All warnings emitted by <see cref="DrumPatternTextParser"/> across all lanes.</summary>
            public readonly List<DrumPatternTextWarning> warnings;

            /// <summary>Total step count derived from prompt input.</summary>
            public readonly int totalSteps;

            /// <summary>Populated when success=false.</summary>
            public readonly string failureReason;

            /// <summary>Input tokens reported by LLM Core. 0 if no completion was reached.</summary>
            public readonly int inputTokens;

            /// <summary>Output tokens reported by LLM Core. 0 if no completion was reached.</summary>
            public readonly int outputTokens;

            private Result(
                bool success,
                string rawResponse,
                string cleanedDslBlock,
                List<DrumPatternData.StepState>[] parsedLanes,
                List<DrumPatternTextWarning> warnings,
                int totalSteps,
                string failureReason,
                int inputTokens,
                int outputTokens)
            {
                this.success = success;
                this.rawResponse = rawResponse ?? string.Empty;
                this.cleanedDslBlock = cleanedDslBlock ?? string.Empty;
                this.parsedLanes = parsedLanes ?? Array.Empty<List<DrumPatternData.StepState>>();
                this.warnings = warnings ?? new List<DrumPatternTextWarning>();
                this.totalSteps = totalSteps;
                this.failureReason = failureReason ?? string.Empty;
                this.inputTokens = inputTokens;
                this.outputTokens = outputTokens;
            }

            public static Result Ok(
                string raw, string cleaned,
                List<DrumPatternData.StepState>[] lanes,
                List<DrumPatternTextWarning> warnings,
                int totalSteps,
                int inputTokens, int outputTokens) =>
                new Result(true, raw, cleaned, lanes, warnings, totalSteps, null,
                    inputTokens, outputTokens);

            public static Result Fail(
                string reason,
                string raw = null,
                int inputTokens = 0,
                int outputTokens = 0) =>
                new Result(false, raw, null, null, null, 0, reason,
                    inputTokens, outputTokens);
        }

        /// <summary>
        /// Generate a drum pattern via LLM. Builds the prompt, calls LLM Core,
        /// cleans the response, parses each lane through
        /// <see cref="DrumPatternTextParser"/>, returns parsed state + warnings.
        /// </summary>
        public static async Task<Result> GenerateAsync(
            ILLMClient client,
            RhythmGenreVocabularySO vocabulary,
            DrumPatternLLMPromptBuilder.Input input)
        {
            if (client == null)
                return Result.Fail("ILLMClient is null.");

            // ---- 1. Build prompts ----
            var build = DrumPatternLLMPromptBuilder.Build(vocabulary, input);
            if (!build.success)
                return Result.Fail($"Prompt build failed: {build.failureReason}");

            int totalSteps = input.beatsPerMeasure * input.measures * input.subdivisions;

            // ---- 2. Execute via LLM Core ----
            LLMCompletionResult completion;
            try
            {
                completion = await PromptExecutionHelper.ExecuteAsync(
                    client: client,
                    prompt: build.userPrompt,
                    instructions: build.systemPrompt);
            }
            catch (Exception ex)
            {
                return Result.Fail($"LLM call failed: {ex.GetType().Name}: {ex.Message}");
            }

            if (completion == null)
                return Result.Fail("LLM Core returned a null completion.");

            int inTok = completion.InputTokens;
            int outTok = completion.OutputTokens;

            // ---- 3. Extract response text ----
            string rawResponse = completion.OutputText;
            if (string.IsNullOrWhiteSpace(rawResponse))
                return Result.Fail("LLM returned empty OutputText.", rawResponse, inTok, outTok);

            // ---- 4. Locate DSL block ----
            string dslBlock = ExtractDslBlock(rawResponse);
            if (dslBlock == null)
                return Result.Fail(
                    "Could not locate a fenced DSL block in the response.",
                    rawResponse, inTok, outTok);

            // ---- 5. Split into lanes ----
            string[] laneLines = SplitIntoLanes(dslBlock);
            if (laneLines.Length == 0)
                return Result.Fail("DSL block contained no lane lines.", rawResponse, inTok, outTok);

            if (laneLines.Length != input.laneComposition.Count)
                return Result.Fail(
                    $"Lane count mismatch: setup expected {input.laneComposition.Count} lanes, " +
                    $"DSL block had {laneLines.Length}.",
                    rawResponse, inTok, outTok);

            // ---- 6. Parse each lane ----
            var parsedLanes = new List<DrumPatternData.StepState>[laneLines.Length];
            var allWarnings = new List<DrumPatternTextWarning>();

            for (int i = 0; i < laneLines.Length; i++)
            {
                var laneWarnings = new List<DrumPatternTextWarning>();
                int laneDefaultVelocity = input.laneComposition[i]?.defaultVelocity ?? 100;

                parsedLanes[i] = DrumPatternTextParser.Parse(
                    input: laneLines[i],
                    totalSteps: totalSteps,
                    laneDefaultVelocity: laneDefaultVelocity,
                    laneIndex: i,
                    warnings: laneWarnings);

                allWarnings.AddRange(laneWarnings);
            }

            return Result.Ok(rawResponse, dslBlock, parsedLanes, allWarnings, totalSteps,
                inTok, outTok);
        }

        // -----------------------------
        // DSL block extraction
        // -----------------------------

        /// <summary>
        /// Locate the first fenced code block in the LLM response. The system
        /// prompt asks for one DSL block; we extract the contents of the first
        /// fenced block found. Tolerates language tags after the opening fence
        /// (```, ```dsl, ```text) and prose before/after the fence.
        /// </summary>
        /// <returns>Fence contents, or null if no fence found.</returns>
        private static string ExtractDslBlock(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            var fenceRegex = new Regex(
                @"```[^\r\n]*\r?\n(?<body>[\s\S]*?)\r?\n```",
                RegexOptions.Compiled);

            var match = fenceRegex.Match(raw);
            return match.Success ? match.Groups["body"].Value : null;
        }

        // -----------------------------
        // Lane splitting
        // -----------------------------

        /// <summary>
        /// Split the DSL block into per-lane lines. Empty and whitespace-only
        /// lines are dropped. The parser handles further whitespace and bar
        /// separators inside each line.
        /// </summary>
        private static string[] SplitIntoLanes(string dslBlock)
        {
            if (string.IsNullOrEmpty(dslBlock)) return Array.Empty<string>();

            return dslBlock
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }
    }
}
#endif
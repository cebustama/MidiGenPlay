#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BCS.LLM.Core.Clients;
using BCS.LLM.Core.Execution;
using MidiGenPlay.Composition;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Wraps LLM Core's <see cref="PromptExecutionHelper"/> for chord-progression
    /// generation. Chord twin of <see cref="DrumPatternLLMGenerator"/> (Batch L4,
    /// copy-then-unify). Pipeline:
    /// <list type="number">
    ///   <item><description>Build prompts via <see cref="ChordProgressionLLMPromptBuilder"/>.</description></item>
    ///   <item><description>Send via <see cref="PromptExecutionHelper.ExecuteAsync"/> (single-shot).</description></item>
    ///   <item><description>Extract response text from <see cref="LLMCompletionResult.OutputText"/>.</description></item>
    ///   <item><description>Locate the fenced Roman-string block.</description></item>
    ///   <item><description>Parse via <see cref="RomanProgressionParser"/>.</description></item>
    ///   <item><description>Return parsed chords + parser error (if any) + token counts.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>Unlike the drum generator (N lane lines), a chord response carries a
    /// single Roman-numeral string. There is no lane composition and no alias
    /// resolution.</para>
    ///
    /// <para><b>Zero-warning caveat (D-L4.5):</b> <see cref="RomanProgressionParser"/>
    /// does not hard-fail an unknown quality suffix — it logs a warning and
    /// downgrades the chord. So <c>TryParse</c> succeeding is necessary but not
    /// sufficient for a clean result. This generator surfaces the parsed chords
    /// and the parser's <c>error</c>; the out-of-alphabet token guard lives in
    /// <see cref="ChordProgressionLLMResponseHandler"/>.</para>
    /// </remarks>
    public static class ChordProgressionLLMGenerator
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

            /// <summary>The Roman-string block extracted from the response.</summary>
            public readonly string cleanedProgression;

            /// <summary>Chords parsed by <see cref="RomanProgressionParser"/>, in order.</summary>
            public readonly List<ParsedChord> parsedChords;

            /// <summary>Target progression length in measures, derived from prompt input.</summary>
            public readonly int targetMeasures;

            /// <summary>Populated when success=false.</summary>
            public readonly string failureReason;

            public readonly int inputTokens;
            public readonly int outputTokens;

            private Result(
                bool success,
                string rawResponse,
                string cleanedProgression,
                List<ParsedChord> parsedChords,
                int targetMeasures,
                string failureReason,
                int inputTokens,
                int outputTokens)
            {
                this.success = success;
                this.rawResponse = rawResponse ?? string.Empty;
                this.cleanedProgression = cleanedProgression ?? string.Empty;
                this.parsedChords = parsedChords ?? new List<ParsedChord>();
                this.targetMeasures = targetMeasures;
                this.failureReason = failureReason ?? string.Empty;
                this.inputTokens = inputTokens;
                this.outputTokens = outputTokens;
            }

            public static Result Ok(
                string raw, string cleaned,
                List<ParsedChord> chords,
                int targetMeasures,
                int inputTokens, int outputTokens) =>
                new Result(true, raw, cleaned, chords, targetMeasures, null,
                    inputTokens, outputTokens);

            public static Result Fail(
                string reason,
                string raw = null,
                int inputTokens = 0,
                int outputTokens = 0) =>
                new Result(false, raw, null, null, 0, reason,
                    inputTokens, outputTokens);
        }

        /// <summary>
        /// Generate a chord progression via LLM. Builds the prompt, calls LLM
        /// Core, locates the Roman block, parses it through
        /// <see cref="RomanProgressionParser"/>, returns parsed chords.
        /// </summary>
        /// <param name="defaultMeasuresPerChord">
        /// Default duration in measures for a chord that omits its (x) suffix.
        /// Forwarded to the parser; sourced from prompt input.
        /// </param>
        /// <param name="inferTriadFromCaseWhenNoSuffix">
        /// Forwarded to the parser; mirrors the editor's case-inference toggle.
        /// </param>
        public static async Task<Result> GenerateAsync(
            ILLMClient client,
            ChordGenreVocabularySO vocabulary,
            ChordProgressionLLMPromptBuilder.Input input,
            bool inferTriadFromCaseWhenNoSuffix = true)
        {
            if (client == null)
                return Result.Fail("ILLMClient is null.");

            // ---- 1. Build prompts ----
            var build = ChordProgressionLLMPromptBuilder.Build(vocabulary, input);
            if (!build.success)
                return Result.Fail($"Prompt build failed: {build.failureReason}");

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

            // ---- 4. Locate the Roman block ----
            string progression = ExtractProgressionBlock(rawResponse);
            if (string.IsNullOrWhiteSpace(progression))
                return Result.Fail(
                    "Could not locate a fenced progression block in the response.",
                    rawResponse, inTok, outTok);

            // ---- 5. Parse via RomanProgressionParser ----
            var parser = new RomanProgressionParser();
            bool ok = parser.TryParse(
                input: progression,
                defaultMeasuresPerChord: input.defaultDurationMeasures,
                inferTriadFromCaseWhenNoSuffix: inferTriadFromCaseWhenNoSuffix,
                out List<ParsedChord> chords,
                out string parseError);

            if (!ok)
                return Result.Fail(
                    $"Roman parse failed: {parseError}", rawResponse, inTok, outTok);

            return Result.Ok(rawResponse, progression, chords, input.measures, inTok, outTok);
        }

        // -----------------------------
        // Progression block extraction
        // -----------------------------

        /// <summary>
        /// Locate the first fenced code block in the LLM response and return its
        /// contents as a single-line Roman string (newlines collapsed to spaces,
        /// which the parser also tolerates). Tolerates a language tag after the
        /// opening fence and prose before/after.
        /// </summary>
        /// <returns>Fence contents, or null if no fence found.</returns>
        private static string ExtractProgressionBlock(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            var fenceRegex = new Regex(
                @"```[^\r\n]*\r?\n(?<body>[\s\S]*?)\r?\n```",
                RegexOptions.Compiled);

            var match = fenceRegex.Match(raw);
            if (!match.Success) return null;

            string body = match.Groups["body"].Value;
            // Collapse internal newlines to spaces; the parser treats '\n' as a
            // space anyway, but normalizing here keeps cleanedProgression tidy for
            // preview/round-trip into the editor's single-line Roman field.
            body = body.Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
    }
}
#endif
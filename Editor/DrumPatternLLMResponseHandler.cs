#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BCS.LLM.Core.Clients;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Async glue between <see cref="DrumPatternLLMGenerator"/>,
    /// <see cref="DrumPatternEditorImporter"/>, and the editor window. Turns a
    /// generation request — or a pasted clipboard payload — into a single
    /// <see cref="Outcome"/> the window applies on the main thread.
    /// </summary>
    /// <remarks>
    /// <para>L2 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c> (D-L2.2 = A,
    /// D-L2.6). This type performs <b>no</b> Unity-API or editor-window mutation:
    /// it returns an <see cref="Outcome"/> describing what to apply (grid config,
    /// lanes, DSL rows) and the window owns the apply step. That keeps the apply
    /// logic in one place and keeps this glue testable.</para>
    ///
    /// <para><b>Generate and Import are unified through the importer.</b> The
    /// generator returns the full <c>rawResponse</c> (setup card + fenced DSL).
    /// Rather than write the generator's pre-parsed lanes directly (which would
    /// duplicate the apply path), this handler feeds <c>rawResponse</c> to
    /// <see cref="DrumPatternEditorImporter.Parse"/> — exactly the path the
    /// clipboard-Import button uses. Both routes therefore produce the same
    /// <see cref="Outcome"/> shape. The generator's own parse output is surfaced
    /// as preview info (token counts, parser warnings), not as the write path.</para>
    ///
    /// <para><b>Async discipline (D-L2.3, load-bearing).</b> The window must call
    /// <see cref="GenerateAsync"/> from an <c>async void</c> button handler and
    /// <c>await</c> it — never <c>.Result</c> / <c>.Wait()</c> /
    /// <c>.GetAwaiter().GetResult()</c>, which deadlocks the editor main thread.
    /// The awaited continuation resumes on the main thread, where the window
    /// applies the <see cref="Outcome"/>.</para>
    /// </remarks>
    public static class DrumPatternLLMResponseHandler
    {
        /// <summary>How the outcome should be applied by the window.</summary>
        public enum OutcomeKind
        {
            /// <summary>The call/parse failed; nothing to apply. See <see cref="Outcome.displayWarnings"/>.</summary>
            Failed,

            /// <summary>DSL rows are available but grid config is not — populate text rows only.</summary>
            DslOnly,

            /// <summary>Grid config + lanes + DSL rows are all available — auto-configure then populate.</summary>
            Full,
        }

        /// <summary>
        /// Everything the window needs to apply a generation or import result.
        /// Immutable; the window reads it on the main thread after the await.
        /// </summary>
        public readonly struct Outcome
        {
            public readonly OutcomeKind kind;

            // -- Grid config (valid only when kind == Full) --
            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly int subdivisions;
            public readonly IReadOnlyList<DrumPatternEditorImporter.LaneInfo> lanes;

            /// <summary>Per-lane DSL glyph strings, in order. Populated for Full and DslOnly.</summary>
            public readonly IReadOnlyList<string> dslLines;

            /// <summary>Human-readable warning/info lines for the editor warning panel.</summary>
            public readonly IReadOnlyList<string> displayWarnings;

            /// <summary>Input tokens reported by the LLM call (0 for clipboard import).</summary>
            public readonly int inputTokens;

            /// <summary>Output tokens reported by the LLM call (0 for clipboard import).</summary>
            public readonly int outputTokens;

            public Outcome(
                OutcomeKind kind,
                TimeSignature timeSignature,
                int measures,
                int subdivisions,
                IReadOnlyList<DrumPatternEditorImporter.LaneInfo> lanes,
                IReadOnlyList<string> dslLines,
                IReadOnlyList<string> displayWarnings,
                int inputTokens,
                int outputTokens)
            {
                this.kind = kind;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.lanes = lanes ?? Array.Empty<DrumPatternEditorImporter.LaneInfo>();
                this.dslLines = dslLines ?? Array.Empty<string>();
                this.displayWarnings = displayWarnings ?? Array.Empty<string>();
                this.inputTokens = inputTokens;
                this.outputTokens = outputTokens;
            }

            public bool Success => kind != OutcomeKind.Failed;
        }

        // -------------------------------------------------------------------
        // Generate path
        // -------------------------------------------------------------------

        /// <summary>
        /// Run an LLM generation and translate the response into an
        /// <see cref="Outcome"/>. Awaitable; call from an <c>async void</c>
        /// handler. Never throws for an LLM failure — failures come back as
        /// <see cref="OutcomeKind.Failed"/> with warnings.
        /// </summary>
        /// <param name="client">Resolved LLM client (see D-L2.1).</param>
        /// <param name="vocabulary">Genre vocabulary SO.</param>
        /// <param name="input">Prompt-builder input assembled by the window.</param>
        /// <param name="aliasResolver">
        /// Lane short-name resolver; pass <c>LaneAliasDictionary.TryResolve</c>.
        /// </param>
        public static async Task<Outcome> GenerateAsync(
            ILLMClient client,
            RhythmGenreVocabularySO vocabulary,
            DrumPatternLLMPromptBuilder.Input input,
            Func<string, GeneralMidiPercussion?> aliasResolver)
        {
            DrumPatternLLMGenerator.Result gen;
            try
            {
                gen = await DrumPatternLLMGenerator.GenerateAsync(client, vocabulary, input);
            }
            catch (Exception ex)
            {
                return Failed($"Generation threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (!gen.success)
            {
                // The generator failed before/at parse. If it still produced a raw
                // response, try to salvage DSL via the importer; otherwise fail.
                if (!string.IsNullOrWhiteSpace(gen.rawResponse))
                {
                    var salvage = TranslatePayload(
                        gen.rawResponse, aliasResolver, gen.inputTokens, gen.outputTokens,
                        leadWarning: $"LLM generation reported failure: {gen.failureReason}");
                    return salvage;
                }
                return Failed(
                    $"LLM generation failed: {gen.failureReason}",
                    gen.inputTokens, gen.outputTokens);
            }

            // Success: unify through the importer on the raw response so Generate
            // and Import share one apply path. Carry the token counts and any
            // generator-side parser warnings as preview info.
            var preludeWarnings = new List<string>();
            if (gen.warnings != null)
                foreach (var w in gen.warnings)
                    preludeWarnings.Add(w.ToString());

            return TranslatePayload(
                gen.rawResponse, aliasResolver, gen.inputTokens, gen.outputTokens,
                extraWarnings: preludeWarnings);
        }

        // -------------------------------------------------------------------
        // Import path (clipboard / pasted payload)
        // -------------------------------------------------------------------

        /// <summary>
        /// Translate a pasted payload (clipboard) into an <see cref="Outcome"/>
        /// via the importer. Synchronous — no LLM call. Token counts are 0.
        /// </summary>
        public static Outcome FromPayload(
            string payload,
            Func<string, GeneralMidiPercussion?> aliasResolver)
            => TranslatePayload(payload, aliasResolver, 0, 0);

        // -------------------------------------------------------------------
        // Shared translation
        // -------------------------------------------------------------------

        private static Outcome TranslatePayload(
            string payload,
            Func<string, GeneralMidiPercussion?> aliasResolver,
            int inputTokens,
            int outputTokens,
            string leadWarning = null,
            List<string> extraWarnings = null)
        {
            var import = DrumPatternEditorImporter.Parse(payload, aliasResolver);

            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(leadWarning)) warnings.Add(leadWarning);
            if (extraWarnings != null) warnings.AddRange(extraWarnings);
            foreach (var w in import.warnings) warnings.Add(w.ToString());

            switch (import.mode)
            {
                case DrumPatternEditorImporter.ImportMode.Full:
                    return new Outcome(
                        OutcomeKind.Full,
                        import.timeSignature, import.measures, import.subdivisions,
                        import.lanes, import.dslLines, warnings,
                        inputTokens, outputTokens);

                case DrumPatternEditorImporter.ImportMode.DslOnly:
                    return new Outcome(
                        OutcomeKind.DslOnly,
                        default, 0, 0,
                        null, import.dslLines, warnings,
                        inputTokens, outputTokens);

                default: // Failed
                    return new Outcome(
                        OutcomeKind.Failed,
                        default, 0, 0,
                        null, null, warnings,
                        inputTokens, outputTokens);
            }
        }

        private static Outcome Failed(string reason, int inputTokens = 0, int outputTokens = 0) =>
            new Outcome(
                OutcomeKind.Failed,
                default, 0, 0,
                null, null, new List<string> { reason },
                inputTokens, outputTokens);
    }
}
#endif
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BCS.LLM.Core.Clients;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Async glue between <see cref="ChordProgressionLLMGenerator"/>,
    /// <see cref="ChordProgressionEditorImporter"/>, and the editor window. Turns
    /// a generation request — or a pasted clipboard payload — into a single
    /// <see cref="Outcome"/> the window applies on the main thread. Chord twin of
    /// <see cref="DrumPatternLLMResponseHandler"/>.
    /// </summary>
    /// <remarks>
    /// <para>This type performs <b>no</b> Unity-API or editor-window mutation: it
    /// returns an <see cref="Outcome"/> describing what to apply (setup fields +
    /// Roman string) and the window owns the apply step via its existing
    /// ParseAndPreview/ApplyToAsset path. That keeps the apply logic in one place
    /// and keeps this glue testable.</para>
    ///
    /// <para><b>Generate and Import are unified through the importer.</b> The
    /// generator returns the full <c>rawResponse</c> (setup card + fenced Roman
    /// block). Rather than write the generator's pre-parsed chords directly, this
    /// handler feeds <c>rawResponse</c> to
    /// <see cref="ChordProgressionEditorImporter.Parse"/> — exactly the path the
    /// clipboard-Import button uses. Both routes therefore produce the same
    /// <see cref="Outcome"/> shape.</para>
    ///
    /// <para><b>Zero-warning guard (D-L4.5), load-bearing.</b>
    /// <see cref="MidiGenPlay.Composition.RomanProgressionParser"/> does not
    /// reject an unknown quality suffix — it logs a <c>Debug.LogWarning</c> and
    /// silently downgrades the chord to diatonic quality. A parse that
    /// "succeeds" can therefore still contain a token the alphabet forbids. To
    /// honor the no-silent-fallback contract, this handler re-scans the Roman
    /// string against the v1 quality-suffix allowlist BEFORE producing an
    /// applyable outcome; any out-of-alphabet suffix becomes a hard
    /// <see cref="OutcomeKind.Failed"/> with an explanatory warning, rather than
    /// an applied wrong chord. The allowlist mirrors the prompt's declared
    /// alphabet and <c>RomanProgressionParser.TryParseQualitySuffix</c>.</para>
    ///
    /// <para><b>Async discipline, load-bearing.</b> The window must call
    /// <see cref="GenerateAsync"/> from an <c>async void</c> button handler and
    /// <c>await</c> it — never <c>.Result</c> / <c>.Wait()</c> /
    /// <c>.GetAwaiter().GetResult()</c>, which deadlocks the editor main thread.</para>
    /// </remarks>
    public static class ChordProgressionLLMResponseHandler
    {
        /// <summary>How the outcome should be applied by the window.</summary>
        public enum OutcomeKind
        {
            /// <summary>The call/parse/guard failed; nothing to apply. See <see cref="Outcome.displayWarnings"/>.</summary>
            Failed,

            /// <summary>A Roman string is available but the setup card is not — populate the Roman field only.</summary>
            ProgressionOnly,

            /// <summary>Setup fields + Roman string are all available — auto-configure then populate.</summary>
            Full,
        }

        /// <summary>
        /// Everything the window needs to apply a generation or import result.
        /// Immutable; the window reads it on the main thread after the await,
        /// then routes through its existing ParseAndPreview/ApplyToAsset path.
        /// </summary>
        public readonly struct Outcome
        {
            public readonly OutcomeKind kind;

            // -- Setup fields (valid only when kind == Full) --
            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly float defaultDurationMeasures;
            public readonly Tonality referenceTonality;

            /// <summary>The Roman-numeral progression string. Populated for Full and ProgressionOnly.</summary>
            public readonly string progression;

            /// <summary>Human-readable warning/info lines for the editor warning panel.</summary>
            public readonly IReadOnlyList<string> displayWarnings;

            public readonly int inputTokens;
            public readonly int outputTokens;

            public Outcome(
                OutcomeKind kind,
                TimeSignature timeSignature,
                int measures,
                float defaultDurationMeasures,
                Tonality referenceTonality,
                string progression,
                IReadOnlyList<string> displayWarnings,
                int inputTokens,
                int outputTokens)
            {
                this.kind = kind;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.defaultDurationMeasures = defaultDurationMeasures;
                this.referenceTonality = referenceTonality;
                this.progression = progression ?? string.Empty;
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
        public static async Task<Outcome> GenerateAsync(
            ILLMClient client,
            ChordGenreVocabularySO vocabulary,
            ChordProgressionLLMPromptBuilder.Input input,
            bool inferTriadFromCaseWhenNoSuffix = true)
        {
            ChordProgressionLLMGenerator.Result gen;
            try
            {
                gen = await ChordProgressionLLMGenerator.GenerateAsync(
                    client, vocabulary, input, inferTriadFromCaseWhenNoSuffix);
            }
            catch (Exception ex)
            {
                return Failed($"Generation threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (!gen.success)
            {
                // The generator failed before/at parse. If it still produced a raw
                // response, try to salvage the Roman string via the importer.
                if (!string.IsNullOrWhiteSpace(gen.rawResponse))
                {
                    return TranslatePayload(
                        gen.rawResponse, gen.inputTokens, gen.outputTokens,
                        leadWarning: $"LLM generation reported failure: {gen.failureReason}");
                }
                return Failed(
                    $"LLM generation failed: {gen.failureReason}",
                    gen.inputTokens, gen.outputTokens);
            }

            // Success: unify through the importer on the raw response so Generate
            // and Import share one apply path.
            return TranslatePayload(
                gen.rawResponse, gen.inputTokens, gen.outputTokens);
        }

        // -------------------------------------------------------------------
        // Import path (clipboard / pasted payload)
        // -------------------------------------------------------------------

        /// <summary>
        /// Translate a pasted payload (clipboard) into an <see cref="Outcome"/>
        /// via the importer. Synchronous — no LLM call. Token counts are 0.
        /// </summary>
        public static Outcome FromPayload(string payload)
            => TranslatePayload(payload, 0, 0);

        // -------------------------------------------------------------------
        // Shared translation (+ D-L4.5 guard)
        // -------------------------------------------------------------------

        private static Outcome TranslatePayload(
            string payload,
            int inputTokens,
            int outputTokens,
            string leadWarning = null)
        {
            var import = ChordProgressionEditorImporter.Parse(payload);

            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(leadWarning)) warnings.Add(leadWarning);
            foreach (var w in import.warnings) warnings.Add(w.ToString());

            // ---- D-L4.5: out-of-alphabet token guard ----
            // Applies whenever a progression string exists (Full or ProgressionOnly).
            // The parser would only warn-and-downgrade an unknown suffix; we treat
            // it as a hard failure so a silently-wrong chord is never applied.
            if (!string.IsNullOrWhiteSpace(import.progression) &&
                TryFindForbiddenToken(import.progression, out string offending))
            {
                warnings.Add(
                    $"Out-of-alphabet chord token \"{offending}\": the parser would " +
                    "silently downgrade this rather than reject it, so it is blocked " +
                    "here (no silent fallback). Regenerate or correct the token.");
                return new Outcome(
                    OutcomeKind.Failed,
                    default, 0, 0f, default, import.progression, warnings,
                    inputTokens, outputTokens);
            }

            switch (import.mode)
            {
                case ChordProgressionEditorImporter.ImportMode.Full:
                    return new Outcome(
                        OutcomeKind.Full,
                        import.timeSignature, import.measures,
                        import.defaultDurationMeasures, import.referenceTonality,
                        import.progression, warnings,
                        inputTokens, outputTokens);

                case ChordProgressionEditorImporter.ImportMode.ProgressionOnly:
                    return new Outcome(
                        OutcomeKind.ProgressionOnly,
                        default, 0, 0f, default,
                        import.progression, warnings,
                        inputTokens, outputTokens);

                default: // Failed
                    return new Outcome(
                        OutcomeKind.Failed,
                        default, 0, 0f, default,
                        null, warnings,
                        inputTokens, outputTokens);
            }
        }

        private static Outcome Failed(string reason, int inputTokens = 0, int outputTokens = 0) =>
            new Outcome(
                OutcomeKind.Failed,
                default, 0, 0f, default,
                null, new List<string> { reason },
                inputTokens, outputTokens);

        // -------------------------------------------------------------------
        // D-L4.5 allowlist guard
        // -------------------------------------------------------------------

        /// <summary>
        /// v1 quality-suffix allowlist, lower-cased. Mirrors the accepted cases in
        /// <c>RomanProgressionParser.TryParseQualitySuffix</c> and the prompt's
        /// declared alphabet. An empty suffix (plain triad) is always allowed and
        /// is not listed here.
        /// </summary>
        private static readonly HashSet<string> AllowedSuffixes = new HashSet<string>(
            StringComparer.Ordinal)
        {
            // minor triad
            "m", "min", "mi", "mn", "-", "min3", "mtri", "mtriad",
            // major triad (explicit)
            "maj", "ma", "mjr", "mja",
            // diminished / augmented triads
            "dim", "o", "°", "aug", "+", "+5",
            // sevenths
            "7", "dom", "dom7",
            "maj7", "ma7", "m7+", "mm7", "mmaj7",
            "m7", "-7", "min7",
            "ø", "ø7", "m7b5", "min7b5",
            "dim7", "o7", "°7",
            // suspended
            "sus2", "sus4", "sus",
        };

        // Token shape: optional accidental (b/#/♭/♯) + Roman core (IVXivx) + suffix.
        // We isolate the suffix (everything after the Roman core) and test it.
        private static readonly Regex TokenSplitRegex = new Regex(
            @"^[b#♭♯]?(?<roman>[IVXivx]+)(?<suffix>.*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Scan the progression for any chord token whose quality suffix is not in
        /// the v1 allowlist. Rest tokens (S / REST / R) and bare durations are
        /// skipped. Returns the first offending token, if any.
        /// </summary>
        internal static bool TryFindForbiddenToken(string progression, out string offending)
        {
            offending = null;
            if (string.IsNullOrWhiteSpace(progression)) return false;

            // Same separators the parser splits on.
            string normalized = progression.Replace('\n', ' ');
            string[] tokens = normalized.Split(
                new[] { '–', '-', '—' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in tokens)
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;

                // Strip a trailing "(x)" duration before inspecting quality.
                int paren = token.IndexOf('(');
                if (paren >= 0) token = token.Substring(0, paren).Trim();
                if (token.Length == 0) continue; // bare duration → rest

                // Skip rests.
                string upper = token.ToUpperInvariant();
                if (upper == "S" || upper == "REST" || upper == "R") continue;

                var m = TokenSplitRegex.Match(token);
                if (!m.Success)
                {
                    // No recognizable Roman core at all — definitely not v1.
                    offending = raw.Trim();
                    return true;
                }

                string suffix = m.Groups["suffix"].Value.Trim();
                if (suffix.Length == 0) continue; // plain triad, allowed

                // Normalize the way the parser does before its switch.
                string s = suffix.Replace(" ", "")
                                 .Replace("Δ", "maj")
                                 .Replace("∆", "maj")
                                 .ToLowerInvariant();

                if (!AllowedSuffixes.Contains(s))
                {
                    offending = raw.Trim();
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
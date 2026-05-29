#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function parser for the "setup card + progression block" markdown
    /// shape produced by <see cref="ChordProgressionLLMPromptBuilder"/>'s system
    /// prompt (and pasteable by hand). Chord twin of
    /// <see cref="DrumPatternEditorImporter"/>; consumed by both the editor's
    /// clipboard-Import affordance and the Generate response path.
    /// </summary>
    /// <remarks>
    /// <para>No Unity API calls, no asset mutation, no editor-window state — the
    /// window owns applying the returned <see cref="Result"/>. This keeps the
    /// importer EditMode-testable.</para>
    ///
    /// <para>Unlike the drum importer (N lane lines + instrument/alias
    /// resolution), a chord payload carries a single Roman-numeral string and a
    /// setup card of mechanical fields only. There is therefore no lane parsing
    /// and no alias resolver.</para>
    ///
    /// <para><b>Canonical setup-card shape:</b></para>
    /// <code>
    /// **Setup (Roman mode):**
    ///
    /// - Time signature: FourFour
    /// - Measures (total): 4
    /// - Default duration (measures): 1
    /// - Reference tonality: Ionian
    ///
    /// ```
    /// ii7 – V7 – Imaj7 – vi7
    /// ```
    /// </code>
    ///
    /// <para><b>Fallback:</b> if the setup card is missing or garbled but a
    /// fenced progression block is present, the importer returns
    /// <see cref="ImportMode.ProgressionOnly"/> with the Roman string and a
    /// warning, so the window can populate the Roman text field and let the user
    /// set the mechanical fields by hand.</para>
    /// </remarks>
    public static class ChordProgressionEditorImporter
    {
        // -------------------------------------------------------------------
        // Result + warning types
        // -------------------------------------------------------------------

        public enum ImportMode
        {
            /// <summary>Nothing usable was found (no progression block).</summary>
            Failed,

            /// <summary>A progression string was found but the setup card was missing or unusable.</summary>
            ProgressionOnly,

            /// <summary>Both the setup card and progression parsed; Roman mode can be auto-configured.</summary>
            Full,
        }

        public enum ImportWarningKind
        {
            /// <summary>No fenced progression block was found in the payload.</summary>
            MissingProgressionBlock,

            /// <summary>The setup card was absent or could not be parsed; fell back to progression-only.</summary>
            MissingOrGarbledSetupCard,

            /// <summary>A required setup-card field was missing or invalid.</summary>
            MissingSetupField,
        }

        /// <summary>One importer-side warning.</summary>
        public readonly struct ImportWarning
        {
            public readonly ImportWarningKind kind;
            public readonly string detail;

            public ImportWarning(ImportWarningKind kind, string detail)
            {
                this.kind = kind;
                this.detail = detail;
            }

            public override string ToString() => $"[{kind}] {detail}";
        }

        /// <summary>
        /// Outcome of an import. On <see cref="ImportMode.Full"/>, the setup
        /// fields and <see cref="progression"/> are populated. On
        /// <see cref="ImportMode.ProgressionOnly"/>, only <see cref="progression"/>
        /// is meaningful. On <see cref="ImportMode.Failed"/>, inspect
        /// <see cref="warnings"/>.
        /// </summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            // -- Setup-card fields (valid only when mode == Full) --
            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly float defaultDurationMeasures;
            public readonly Tonality referenceTonality;

            /// <summary>The single Roman-numeral progression string. Populated whenever a block was found.</summary>
            public readonly string progression;

            public readonly IReadOnlyList<ImportWarning> warnings;

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                float defaultDurationMeasures,
                Tonality referenceTonality,
                string progression,
                IReadOnlyList<ImportWarning> warnings)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.defaultDurationMeasures = defaultDurationMeasures;
                this.referenceTonality = referenceTonality;
                this.progression = progression ?? string.Empty;
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
            }
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a full "setup card + progression block" payload.
        /// </summary>
        /// <param name="payload">The raw markdown text (clipboard or LLM response).</param>
        public static Result Parse(string payload)
        {
            var warnings = new List<ImportWarning>();

            // ---- 1. Progression block is mandatory ----
            string progression = ExtractProgression(payload);
            if (string.IsNullOrWhiteSpace(progression))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingProgressionBlock,
                    "No fenced progression block (``` ... ```) found in the payload."));
                return new Result(ImportMode.Failed,
                    default, 0, 0f, default, null, warnings);
            }

            // ---- 2. Setup card (optional → fallback) ----
            string cardRegion = ExtractCardRegion(payload);
            if (string.IsNullOrWhiteSpace(cardRegion))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingOrGarbledSetupCard,
                    "No setup card found; imported progression only. Configure Roman mode manually."));
                return new Result(ImportMode.ProgressionOnly,
                    default, 0, 0f, default, progression, warnings);
            }

            bool tsOk = TryParseEnumField(cardRegion, "Time signature", out TimeSignature ts);
            bool mOk = TryParseIntField(cardRegion, "Measures", out int measures) && measures > 0;
            bool dOk = TryParseFloatField(cardRegion, "Default duration", out float dur) && dur > 0f;
            // Tonality is optional context; default to Ionian if absent.
            bool tonOk = TryParseEnumField(cardRegion, "Reference tonality", out Tonality tonality);
            if (!tonOk) tonality = Tonality.Ionian;

            // If the load-bearing mechanical fields are unusable, degrade to
            // progression-only rather than ship a half-configured Roman setup.
            if (!tsOk || !mOk || !dOk)
            {
                if (!tsOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Time signature missing or not a valid TimeSignature enum name."));
                if (!mOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Measures missing or not a positive integer."));
                if (!dOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Default duration missing or not a positive number."));
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingOrGarbledSetupCard,
                    "Setup card incomplete; imported progression only. Configure Roman mode manually."));
                return new Result(ImportMode.ProgressionOnly,
                    default, 0, 0f, default, progression, warnings);
            }

            return new Result(ImportMode.Full,
                ts, measures, dur, tonality, progression, warnings);
        }

        // -------------------------------------------------------------------
        // Progression block extraction
        // -------------------------------------------------------------------

        /// <summary>
        /// Extract the Roman-numeral progression string from the payload. Takes
        /// the contents of the first fenced code block, collapses newlines to
        /// spaces (the parser treats them as separators anyway), and trims.
        /// CRLF-safe: splits on '\n' and trims trailing '\r' so a CRLF payload is
        /// not double-counted.
        /// </summary>
        internal static string ExtractProgression(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;

            var fenceRegex = new Regex(
                @"```[^\r\n]*\r?\n(?<body>[\s\S]*?)\r?\n```",
                RegexOptions.Compiled);

            var match = fenceRegex.Match(payload);
            if (!match.Success) return null;

            string body = match.Groups["body"].Value;
            // Normalize CRLF / lone CR / LF to single spaces, then collapse runs.
            body = body.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            body = Regex.Replace(body, @"\s{2,}", " ").Trim();
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }

        /// <summary>
        /// The region of the payload that holds the setup card: everything before
        /// the first fenced block. Returns null if no such region exists.
        /// </summary>
        private static string ExtractCardRegion(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;

            int fence = payload.IndexOf("```", StringComparison.Ordinal);
            string region = fence >= 0 ? payload.Substring(0, fence) : payload;
            return string.IsNullOrWhiteSpace(region) ? null : region;
        }

        // -------------------------------------------------------------------
        // Setup-card field parsing (CRLF-safe; label-tolerant)
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse "&lt;label&gt;: &lt;EnumName&gt;" into an enum value of type T.
        /// Label match is case-insensitive and tolerant of surrounding markdown.
        /// </summary>
        private static bool TryParseEnumField<T>(string card, string label, out T value)
            where T : struct, Enum
        {
            value = default;
            var rx = new Regex(
                LabelAnchor + Regex.Escape(label) + @"[^:\n]*:\s*(?<val>[A-Za-z][A-Za-z0-9]*)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var m = rx.Match(card);
            if (!m.Success) return false;
            return Enum.TryParse(m.Groups["val"].Value, ignoreCase: true, out value)
                   && Enum.IsDefined(typeof(T), value);
        }

        // Label must start a line (after an optional markdown bullet / whitespace),
        // and a label's trailing wildcard must not cross a newline. Without this,
        // a label like "Measures" can match the substring inside another field's
        // text (e.g. "Default duration (measures):"), producing a false positive
        // when the real "Measures" line is absent.
        private const string LabelAnchor = @"^\s*[-*]?\s*";

        private static bool TryParseIntField(string card, string label, out int value)
        {
            value = 0;
            var rx = new Regex(
                LabelAnchor + Regex.Escape(label) + @"[^:\n]*:\s*(?<val>-?\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var m = rx.Match(card);
            return m.Success && int.TryParse(m.Groups["val"].Value, out value);
        }

        private static bool TryParseFloatField(string card, string label, out float value)
        {
            value = 0f;
            // Accept dot decimal separator (InvariantCulture), e.g. "1" or "0.5".
            var rx = new Regex(
                LabelAnchor + Regex.Escape(label) + @"[^:\n]*:\s*(?<val>-?\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var m = rx.Match(card);
            return m.Success && float.TryParse(
                m.Groups["val"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }
    }
}
#endif
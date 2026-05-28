#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function parser for the "setup card + DSL block" markdown shape
    /// produced by <see cref="DrumPatternLLMPromptBuilder"/>'s system prompt
    /// (and pasteable by hand). Consumed by both the editor's clipboard-Import
    /// affordance and the Generate response path (D-L8 / D-L2.6).
    /// </summary>
    /// <remarks>
    /// <para>L2 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c>. No Unity
    /// API calls, no asset mutation, no editor-window state — the window owns
    /// applying the returned <see cref="Result"/>. This keeps the importer
    /// EditMode-testable.</para>
    ///
    /// <para><b>Canonical setup-card shape</b> (D-L2.6). The card is the prose
    /// block before the fenced DSL:</para>
    /// <code>
    /// **Setup (configure in Grid mode):**
    ///
    /// - Time signature: FourFour
    /// - Measures: 2
    /// - Subdivisions: 4
    /// - Lanes (in this order):
    ///   1. BassDrum1 (GM 36) — default velocity 100
    ///   2. AcousticSnare (GM 38) — default velocity 110
    ///   ...
    ///
    /// ```
    /// x..x..x...x.....
    /// ....x.......x...
    /// ...
    /// ```
    /// </code>
    ///
    /// <para><b>Fallback</b> (D-L2.6): if the setup card is missing or garbled
    /// but a fenced DSL block is present, the importer returns
    /// <see cref="ImportMode.DslOnly"/> with the DSL lines and a warning, so the
    /// window can populate the text rows and let the user configure Grid mode by
    /// hand. Instrument resolution never silently defaults — an unrecognised
    /// lane token produces a warning and that lane is omitted from
    /// <see cref="Result.lanes"/>.</para>
    /// </remarks>
    public static class DrumPatternEditorImporter
    {
        // -------------------------------------------------------------------
        // Result + warning types
        // -------------------------------------------------------------------

        public enum ImportMode
        {
            /// <summary>Nothing usable was found (no DSL block, no setup card).</summary>
            Failed,

            /// <summary>A DSL block was found but the setup card was missing or unusable.</summary>
            DslOnly,

            /// <summary>Both the setup card and DSL block parsed; Grid mode can be auto-configured.</summary>
            Full,
        }

        public enum ImportWarningKind
        {
            /// <summary>No fenced DSL block was found in the payload.</summary>
            MissingDslBlock,

            /// <summary>The setup card was absent or could not be parsed; fell back to DSL-only.</summary>
            MissingOrGarbledSetupCard,

            /// <summary>A required setup-card field (time signature / measures / subdivisions) was missing or invalid.</summary>
            MissingSetupField,

            /// <summary>A lane token resolved to neither a GeneralMidiPercussion enum name nor a known alias.</summary>
            UnknownInstrument,

            /// <summary>A lane line was malformed (no instrument token recoverable).</summary>
            MalformedLaneLine,

            /// <summary>The DSL block's lane line count did not match the setup card's lane count.</summary>
            LaneCountMismatch,
        }

        /// <summary>
        /// One importer-side warning. Mirrors <see cref="DrumPatternTextWarning"/>'s
        /// <c>ToString()</c> shape so the editor warning panel renders both
        /// uniformly, but carries importer-specific classification.
        /// </summary>
        public readonly struct ImportWarning
        {
            public readonly ImportWarningKind kind;

            /// <summary>Lane index this warning relates to, or -1 if not lane-specific.</summary>
            public readonly int laneIndex;

            public readonly string detail;

            public ImportWarning(ImportWarningKind kind, string detail, int laneIndex = -1)
            {
                this.kind = kind;
                this.laneIndex = laneIndex;
                this.detail = detail;
            }

            public override string ToString()
            {
                string loc = laneIndex >= 0 ? $"lane {laneIndex}" : "setup";
                return $"[{kind}] {loc}: {detail}";
            }
        }

        /// <summary>One resolved setup-card lane.</summary>
        public readonly struct LaneInfo
        {
            public readonly GeneralMidiPercussion instrument;
            public readonly int defaultVelocity;

            public LaneInfo(GeneralMidiPercussion instrument, int defaultVelocity)
            {
                this.instrument = instrument;
                this.defaultVelocity = defaultVelocity;
            }
        }

        /// <summary>
        /// Outcome of an import. On <see cref="ImportMode.Full"/>, both the grid
        /// parameters and lanes are populated and <see cref="dslLines"/> are the
        /// per-lane glyph strings in lane order. On <see cref="ImportMode.DslOnly"/>,
        /// only <see cref="dslLines"/> is meaningful. On <see cref="ImportMode.Failed"/>,
        /// inspect <see cref="warnings"/>.
        /// </summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            // -- Setup-card grid parameters (valid only when mode == Full) --
            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly int subdivisions;

            /// <summary>Resolved lanes in setup-card order (valid only when mode == Full).</summary>
            public readonly IReadOnlyList<LaneInfo> lanes;

            /// <summary>Per-lane DSL glyph strings, in order. Always populated when a DSL block was found.</summary>
            public readonly IReadOnlyList<string> dslLines;

            public readonly IReadOnlyList<ImportWarning> warnings;

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                int subdivisions,
                IReadOnlyList<LaneInfo> lanes,
                IReadOnlyList<string> dslLines,
                IReadOnlyList<ImportWarning> warnings)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.lanes = lanes ?? Array.Empty<LaneInfo>();
                this.dslLines = dslLines ?? Array.Empty<string>();
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
            }
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a full "setup card + DSL block" payload.
        /// </summary>
        /// <param name="payload">The raw markdown text (clipboard or LLM response).</param>
        /// <param name="aliasResolver">
        /// Resolves a short-name token to a <see cref="GeneralMidiPercussion"/>
        /// when the token is not a direct enum name. Pass
        /// <c>LaneAliasDictionary.TryResolve</c>. May be null (alias resolution
        /// is then skipped — only exact enum names resolve).
        /// </param>
        public static Result Parse(
            string payload,
            Func<string, GeneralMidiPercussion?> aliasResolver)
        {
            var warnings = new List<ImportWarning>();

            // ---- 1. DSL block is mandatory ----
            string[] dslLines = ExtractDslLines(payload);
            if (dslLines.Length == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingDslBlock,
                    "No fenced DSL block (``` ... ```) found in the payload."));
                return new Result(ImportMode.Failed,
                    default, 0, 0, null, null, warnings);
            }

            // ---- 2. Setup card (optional → fallback) ----
            string cardRegion = ExtractCardRegion(payload);
            if (string.IsNullOrWhiteSpace(cardRegion))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingOrGarbledSetupCard,
                    "No setup card found; imported DSL only. Configure Grid mode manually."));
                return new Result(ImportMode.DslOnly,
                    default, 0, 0, null, dslLines, warnings);
            }

            bool tsOk = TryParseTimeSignature(cardRegion, out var ts);
            bool mOk = TryParseIntField(cardRegion, "Measures", out int measures) && measures > 0;
            bool sOk = TryParseIntField(cardRegion, "Subdivisions", out int subs) && subs > 0;

            var lanes = ParseLanes(cardRegion, aliasResolver, warnings);

            // If the mechanical fields are unusable or no lanes resolved, degrade
            // to DSL-only rather than ship a half-configured grid.
            if (!tsOk || !mOk || !sOk || lanes.Count == 0)
            {
                if (!tsOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Time signature missing or not a valid TimeSignature enum name."));
                if (!mOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Measures missing or not a positive integer."));
                if (!sOk)
                    warnings.Add(new ImportWarning(ImportWarningKind.MissingSetupField,
                        "Subdivisions missing or not a positive integer."));
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingOrGarbledSetupCard,
                    "Setup card incomplete; imported DSL only. Configure Grid mode manually."));
                return new Result(ImportMode.DslOnly,
                    default, 0, 0, null, dslLines, warnings);
            }

            // ---- 3. Lane-count cross-check ----
            if (lanes.Count != dslLines.Length)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.LaneCountMismatch,
                    $"Setup card lists {lanes.Count} lane(s) but the DSL block has " +
                    $"{dslLines.Length} line(s). Imported DSL only; configure Grid manually."));
                return new Result(ImportMode.DslOnly,
                    default, 0, 0, null, dslLines, warnings);
            }

            return new Result(ImportMode.Full,
                ts, measures, subs, lanes, dslLines, warnings);
        }

        // -------------------------------------------------------------------
        // DSL block extraction (glyph-line detection; fence-agnostic)
        // -------------------------------------------------------------------

        // A line whose only non-whitespace characters are DSL glyphs (.-xXo) and
        // bar separators (|). This content test is the discriminator that lets
        // the importer find the DSL regardless of how it is fenced.
        private static readonly Regex GlyphLineRegex = new Regex(
            @"^[.\-xXo|\s]+$", RegexOptions.Compiled);

        /// <summary>
        /// Extract the per-lane DSL glyph lines from the payload.
        /// <para>Robust to three real-world shapes (D-L2.6): glyph lines inside
        /// their own fenced block, glyph lines as bare text after a
        /// <c>**DSL ...**</c> label, and the whole response wrapped in one outer
        /// code fence. The discriminator is content: a DSL line is one whose only
        /// non-whitespace characters are <c>.-xXo|</c>. The importer takes the
        /// longest contiguous run of such lines (ties → the last run), which
        /// isolates the glyph block from the prose setup card whether or not a
        /// fence is present.</para>
        /// </summary>
        internal static string[] ExtractDslLines(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return Array.Empty<string>();

            // Split on '\n' only and trim a trailing '\r' per line. Splitting on
            // the char array {'\r','\n'} would treat CRLF as TWO separators,
            // inserting an empty line between every real line and breaking the
            // contiguous-run detection below.
            var allLines = payload.Split('\n');

            // Find every maximal run of glyph-only, non-blank lines.
            List<string> best = null;
            List<string> current = null;

            foreach (var line in allLines)
            {
                string raw = line.TrimEnd('\r');
                bool isGlyph = !string.IsNullOrWhiteSpace(raw) && GlyphLineRegex.IsMatch(raw);
                if (isGlyph)
                {
                    if (current == null) current = new List<string>();
                    current.Add(raw.Trim());
                }
                else
                {
                    // End of a run; keep it if it is the longest so far (ties → later run).
                    if (current != null)
                    {
                        if (best == null || current.Count >= best.Count) best = current;
                        current = null;
                    }
                }
            }
            if (current != null && (best == null || current.Count >= best.Count))
                best = current;

            return best?.ToArray() ?? Array.Empty<string>();
        }

        /// <summary>
        /// The region of the payload that holds the setup card: everything before
        /// the first DSL glyph line. Fence-agnostic — works whether or not the
        /// DSL is fenced. Returns null if no setup-card region exists.
        /// </summary>
        private static string ExtractCardRegion(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;

            var allLines = payload.Split('\n');
            var card = new List<string>();

            foreach (var line in allLines)
            {
                string raw = line.TrimEnd('\r');
                bool isGlyph = !string.IsNullOrWhiteSpace(raw) && GlyphLineRegex.IsMatch(raw);
                if (isGlyph) break; // setup card ends where the glyph block begins
                card.Add(raw);
            }

            string region = string.Join("\n", card);
            return string.IsNullOrWhiteSpace(region) ? null : region;
        }

        // -------------------------------------------------------------------
        // Setup-card field parsing
        // -------------------------------------------------------------------

        // "Time signature: FourFour"  (label tolerant of spacing / case)
        private static readonly Regex TimeSigRegex = new Regex(
            @"time\s*signature\s*:\s*(?<val>[A-Za-z][A-Za-z0-9]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool TryParseTimeSignature(string card, out TimeSignature ts)
        {
            ts = default;
            var m = TimeSigRegex.Match(card);
            if (!m.Success) return false;
            return Enum.TryParse(m.Groups["val"].Value, ignoreCase: true, out ts)
                   && Enum.IsDefined(typeof(TimeSignature), ts);
        }

        private static bool TryParseIntField(string card, string label, out int value)
        {
            value = 0;
            // "<label>: <int>" — label tolerant of surrounding markdown bullets/spaces.
            var rx = new Regex(
                Regex.Escape(label) + @"\s*:\s*(?<val>-?\d+)",
                RegexOptions.IgnoreCase);
            var m = rx.Match(card);
            return m.Success && int.TryParse(m.Groups["val"].Value, out value);
        }

        // -------------------------------------------------------------------
        // Lane parsing
        // -------------------------------------------------------------------

        // Matches a numbered lane line, capturing the instrument token and the
        // default velocity. Tolerant of the "(GM NN)" annotation and the em-dash
        // or hyphen before "default velocity".
        //   "1. BassDrum1 (GM 36) — default velocity 100"
        //   "2. HHc (GM 42) - default velocity 80"
        //   "3. AcousticSnare default velocity 110"
        private static readonly Regex LaneLineRegex = new Regex(
            @"^\s*\d+\.\s*(?<inst>[A-Za-z][A-Za-z0-9#'\-]*)" +     // instrument token
            @"(?:\s*\(GM\s*\d+\))?" +                              // optional (GM NN)
            @".*?default\s*velocity\s*(?<vel>\d+)",                // ... default velocity NN
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A lane line that has a numbered instrument token but no recoverable velocity.
        private static readonly Regex LaneLineNoVelRegex = new Regex(
            @"^\s*\d+\.\s*(?<inst>[A-Za-z][A-Za-z0-9#'\-]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static List<LaneInfo> ParseLanes(
            string card,
            Func<string, GeneralMidiPercussion?> aliasResolver,
            List<ImportWarning> warnings)
        {
            var lanes = new List<LaneInfo>();

            // Only consider the region at/after a "Lanes" header if present, to
            // avoid matching numbered prose elsewhere. If no header, scan the
            // whole card (defensive).
            string region = card;
            var lanesHeader = Regex.Match(card, @"lanes\b", RegexOptions.IgnoreCase);
            if (lanesHeader.Success)
                region = card.Substring(lanesHeader.Index);

            var lines = region.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            int laneOrdinal = 0;

            foreach (var raw in lines)
            {
                // A numbered list item is the lane-line signal.
                if (!Regex.IsMatch(raw, @"^\s*\d+\.")) continue;

                var m = LaneLineRegex.Match(raw);
                int velocity;
                string instToken;

                if (m.Success)
                {
                    instToken = m.Groups["inst"].Value;
                    if (!int.TryParse(m.Groups["vel"].Value, out velocity))
                        velocity = 100;
                }
                else
                {
                    var m2 = LaneLineNoVelRegex.Match(raw);
                    if (!m2.Success)
                    {
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.MalformedLaneLine,
                            $"Could not read an instrument from lane line: \"{raw.Trim()}\".",
                            laneOrdinal));
                        laneOrdinal++;
                        continue;
                    }
                    instToken = m2.Groups["inst"].Value;
                    velocity = 100; // no velocity stated → lane default
                }

                velocity = Clamp(velocity, 1, 127);

                if (TryResolveInstrument(instToken, aliasResolver, out var instrument))
                {
                    lanes.Add(new LaneInfo(instrument, velocity));
                }
                else
                {
                    warnings.Add(new ImportWarning(
                        ImportWarningKind.UnknownInstrument,
                        $"\"{instToken}\" is neither a GeneralMidiPercussion enum name nor a " +
                        "known lane alias. Lane omitted; no silent fallback.",
                        laneOrdinal));
                }

                laneOrdinal++;
            }

            return lanes;
        }

        /// <summary>
        /// Resolve an instrument token: exact enum name first, then the alias
        /// resolver. No default fallback — returns false if both miss.
        /// </summary>
        private static bool TryResolveInstrument(
            string token,
            Func<string, GeneralMidiPercussion?> aliasResolver,
            out GeneralMidiPercussion instrument)
        {
            instrument = default;
            if (string.IsNullOrWhiteSpace(token)) return false;

            string t = token.Trim();

            // 1. Exact enum name (case-insensitive).
            if (Enum.TryParse(t, ignoreCase: true, out GeneralMidiPercussion parsed)
                && Enum.IsDefined(typeof(GeneralMidiPercussion), parsed))
            {
                instrument = parsed;
                return true;
            }

            // 2. Alias resolver (short names like BD, SN, HHc).
            if (aliasResolver != null)
            {
                var resolved = aliasResolver(t);
                if (resolved.HasValue)
                {
                    instrument = resolved.Value;
                    return true;
                }
            }

            return false;
        }

        private static int Clamp(int v, int lo, int hi) =>
            v < lo ? lo : (v > hi ? hi : v);
    }
}
#endif
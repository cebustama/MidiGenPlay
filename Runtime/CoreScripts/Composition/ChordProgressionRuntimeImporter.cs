using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// MGP-ALWTTT-DBG-4 (D-DBG4=A): the runtime-safe, single-grammar entry point
    /// for turning a Roman-numeral progression — bare, or wrapped in the
    /// "setup card + fenced block" payload shape — into an in-memory
    /// <see cref="ChordProgressionData"/> suitable for the Ask C
    /// <c>patternOverrides</c> channel.
    /// </summary>
    /// <remarks>
    /// <para><b>One grammar, one code path.</b> The setup-card parser below is the
    /// RELOCATED body of the former editor-only
    /// <c>MidiGenPlay.Authoring.ChordProgressionEditorImporter</c> (pure regex, no
    /// Unity-editor API); that editor symbol now delegates here (E-5=A), so the
    /// clipboard-Import path, the LLM Generate path, and this runtime API can
    /// never drift apart. The Roman token grammar itself is
    /// <see cref="RomanProgressionParser"/> — also shared.</para>
    ///
    /// <para><b>Zero-warning guard (D-L4.5), load-bearing.</b>
    /// <see cref="RomanProgressionParser"/> does not reject an unknown quality
    /// suffix — it warns and silently downgrades the chord to diatonic quality.
    /// To honor the no-silent-fallback contract, <see cref="TryParseRoman"/>
    /// re-scans the string against the quality-suffix allowlist
    /// (<see cref="TryFindForbiddenToken"/>) BEFORE building anything; any
    /// out-of-alphabet token is a hard failure with an explanatory warning,
    /// never an applied wrong chord. The editor response handler delegates to
    /// the same scan.</para>
    ///
    /// <para><b>Never persisted.</b> The returned instance is created with
    /// <see cref="HideFlags.DontSave"/> and is never written to the asset
    /// database — the no-silent-writes authoring invariant holds by
    /// construction. Its <c>name</c> is stamped so Ask A readback
    /// (pre-clone asset name, D-DBG3=A) stays meaningful.</para>
    ///
    /// <para><b>Determinism:</b> pure function of its inputs; no RNG, no
    /// composer state touched.</para>
    /// </remarks>
    public static class ChordProgressionRuntimeImporter
    {
        // ===================================================================
        // Relocated setup-card grammar (former ChordProgressionEditorImporter)
        // ===================================================================

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
        /// Outcome of a payload parse. On <see cref="ImportMode.Full"/>, the
        /// setup fields and <see cref="progression"/> are populated. On
        /// <see cref="ImportMode.ProgressionOnly"/>, only
        /// <see cref="progression"/> is meaningful. On
        /// <see cref="ImportMode.Failed"/>, inspect <see cref="warnings"/>.
        /// </summary>
        public readonly struct PayloadResult
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

            public PayloadResult(
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

        /// <summary>
        /// Parse a full "setup card + progression block" payload. Byte-for-byte
        /// the semantics of the former editor importer's <c>Parse</c> — the
        /// editor symbol now delegates to this method.
        /// </summary>
        public static PayloadResult ParsePayload(string payload)
        {
            var warnings = new List<ImportWarning>();

            // ---- 1. Progression block is mandatory ----
            string progression = ExtractProgression(payload);
            if (string.IsNullOrWhiteSpace(progression))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingProgressionBlock,
                    "No fenced progression block (``` ... ```) found in the payload."));
                return new PayloadResult(ImportMode.Failed,
                    default, 0, 0f, default, null, warnings);
            }

            // ---- 2. Setup card (optional → fallback) ----
            string cardRegion = ExtractCardRegion(payload);
            if (string.IsNullOrWhiteSpace(cardRegion))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.MissingOrGarbledSetupCard,
                    "No setup card found; imported progression only. Configure Roman mode manually."));
                return new PayloadResult(ImportMode.ProgressionOnly,
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
                return new PayloadResult(ImportMode.ProgressionOnly,
                    default, 0, 0f, default, progression, warnings);
            }

            return new PayloadResult(ImportMode.Full,
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
        /// not double-counted. Public: the editor forwarder re-exposes it.
        /// </summary>
        public static string ExtractProgression(string payload)
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

        // ===================================================================
        // D-L4.5 allowlist guard (relocated from ChordProgressionLLMResponseHandler)
        // ===================================================================

        /// <summary>
        /// Quality-suffix allowlist, lower-cased. Mirrors the accepted cases in
        /// <c>RomanProgressionParser.TryParseQualitySuffix</c> and the prompt's
        /// declared alphabet. An empty suffix (plain triad) is always allowed and
        /// is not listed here. Single canonical copy — the editor response
        /// handler delegates its guard to <see cref="TryFindForbiddenToken"/>.
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
            // sixth chords (v2 Tier A)
            "6", "m6", "min6",
            // suspended dominant (v2 Tier A)
            "7sus4",
            // ninths (v2 Tier B)
            "9", "dom9", "maj9", "ma9", "m9", "min9",
        };

        // Token shape: optional accidental (b/#/♭/♯) + Roman core (IVXivx) + suffix.
        // We isolate the suffix (everything after the Roman core) and test it.
        private static readonly Regex TokenSplitRegex = new Regex(
            @"^[b#♭♯]?(?<roman>[IVXivx]+)(?<suffix>.*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Scan the progression for any chord token whose quality suffix is not in
        /// the allowlist. Rest tokens (S / REST / R) and bare durations are
        /// skipped. Returns the first offending token, if any.
        /// </summary>
        public static bool TryFindForbiddenToken(string progression, out string offending)
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
                    // No recognizable Roman core at all — definitely not in alphabet.
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

        // ===================================================================
        // MGP-ALWTTT-DBG-4: runtime builder (Ask D)
        // ===================================================================

        /// <summary>
        /// Parse a full "setup card + fenced Roman block" payload and build an
        /// in-memory <see cref="ChordProgressionData"/> from it.
        /// </summary>
        /// <remarks>
        /// <see cref="ImportMode.ProgressionOnly"/> is a hard failure here: the
        /// mechanical context (TS / measures / default duration) is required to
        /// build a grid, and inventing it would be a silent fallback. Callers
        /// with out-of-band context should use <see cref="TryParseRoman"/>
        /// directly — that is the "bare Roman string" entry point.
        /// </remarks>
        /// <returns>true on success; on failure, <paramref name="data"/> is null
        /// and <paramref name="warnings"/> explains why.</returns>
        public static bool TryParsePayload(
            string payload,
            out ChordProgressionData data,
            out List<string> warnings)
        {
            data = null;
            warnings = new List<string>();

            var import = ParsePayload(payload);
            foreach (var w in import.warnings) warnings.Add(w.ToString());

            switch (import.mode)
            {
                case ImportMode.Full:
                    // Delegate; TryParseRoman appends its own warnings.
                    var romanWarnings = default(List<string>);
                    bool ok = TryParseRoman(
                        import.progression,
                        import.timeSignature,
                        import.measures,
                        import.defaultDurationMeasures,
                        import.referenceTonality,
                        out data,
                        out romanWarnings);
                    warnings.AddRange(romanWarnings);
                    return ok;

                case ImportMode.ProgressionOnly:
                    warnings.Add(
                        "Setup card missing or incomplete: the payload cannot be built " +
                        "without TS / measures / default duration. Use TryParseRoman with " +
                        "explicit context, or fix the setup card.");
                    return false;

                default: // Failed
                    return false;
            }
        }

        /// <summary>
        /// Build an in-memory <see cref="ChordProgressionData"/> from a bare
        /// Roman-numeral string plus explicit mechanical context. This is the
        /// same pipeline the editor's Roman apply path runs
        /// (<see cref="RomanProgressionParser"/> →
        /// <see cref="RhythmGridQuantizer"/> →
        /// <see cref="ChordQualityResolver"/>), minus any persistence.
        /// </summary>
        /// <param name="roman">Roman progression, e.g. "ii7 – V7 – Imaj7 – vi7".</param>
        /// <param name="ts">Meter; drives beats-per-measure for the step grid.</param>
        /// <param name="measures">Declared total measures. &lt;= 0 = derive from
        /// durations silently; &gt; 0 = derive AND warn (non-fatal) on mismatch.
        /// The derived value always wins — durations define the grid, exactly as
        /// in the editor.</param>
        /// <param name="defaultDurationMeasures">Duration for tokens without an
        /// explicit "(x)", in measures.</param>
        /// <param name="referenceTonality">Reference tonality: drives diatonic
        /// quality inference and becomes the single entry of the asset's
        /// <c>tonalities</c> filter. RootNote is NOT needed — the data is
        /// degree-relative.</param>
        /// <param name="data">The built, never-persisted instance
        /// (<see cref="HideFlags.DontSave"/>); null on failure.</param>
        /// <param name="warnings">Human-readable warnings (may be non-empty on
        /// success, e.g. measures mismatch).</param>
        /// <param name="autoMode">Diatonic inference mode for suffix-less tokens.
        /// Default mirrors the editor default (DiatonicTriads).</param>
        /// <param name="defaultVelocity">Velocity stamped on every event.
        /// Default mirrors the editor default (96).</param>
        public static bool TryParseRoman(
            string roman,
            TimeSignature ts,
            int measures,
            float defaultDurationMeasures,
            Tonality referenceTonality,
            out ChordProgressionData data,
            out List<string> warnings,
            AutoChordQualityMode autoMode = AutoChordQualityMode.DiatonicTriads,
            int defaultVelocity = 96)
        {
            data = null;
            warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(roman))
            {
                warnings.Add("Roman progression string is empty.");
                return false;
            }

            if (defaultDurationMeasures <= 0f)
            {
                warnings.Add("Default duration (measures) must be > 0.");
                return false;
            }

            // ---- D-L4.5 zero-warning guard: allowlist re-scan BEFORE parsing.
            // The parser would only warn-and-downgrade an unknown suffix; we
            // treat it as a hard failure so a silently-wrong chord is never
            // applied (same rule as the editor response handler).
            if (TryFindForbiddenToken(roman, out string offending))
            {
                warnings.Add(
                    $"Out-of-alphabet chord token \"{offending}\": the parser would " +
                    "silently downgrade this rather than reject it, so it is blocked " +
                    "here (no silent fallback). Correct the token.");
                return false;
            }

            // ---- Shared Roman grammar (same flag derivation as the editor:
            // infer-from-case only in literal mode).
            var parser = new RomanProgressionParser();
            bool inferFromCase = autoMode == AutoChordQualityMode.None;
            if (!parser.TryParse(
                    roman, defaultDurationMeasures, inferFromCase,
                    out List<ParsedChord> chords, out string parseError))
            {
                warnings.Add($"Parse error: {parseError}");
                return false;
            }

            if (chords == null || chords.Count == 0)
            {
                warnings.Add("No chords were parsed from the input.");
                return false;
            }

            // ---- Quantize durations into an integer step grid (hard fail,
            // same rule as the editor's Quantization Error dialog).
            var tsInfo = TimeSignatureProperties[ts];
            int beatsPerMeasure = tsInfo.BeatsPerMeasure;

            var quantizer = new RhythmGridQuantizer();
            if (!quantizer.TryQuantizeChordDurations(
                    chords, beatsPerMeasure,
                    out int subdivisions,
                    out List<int> lengthsSteps,
                    out int totalSteps,
                    out string durError))
            {
                warnings.Add(
                    $"Quantization error: {durError ?? "Could not find a consistent grid (steps / subdivisions)."}");
                return false;
            }

            int stepsPerMeasure = beatsPerMeasure * subdivisions;
            int derivedMeasures = Mathf.Max(1, totalSteps / Mathf.Max(1, stepsPerMeasure));
            if (measures > 0 && derivedMeasures != measures)
            {
                warnings.Add(
                    $"Declared measures ({measures}) differ from the total implied by the " +
                    $"durations ({derivedMeasures}); using {derivedMeasures} (durations " +
                    "define the grid, as in the editor).");
            }

            // ---- Materialize the in-memory asset (never persisted).
            var built = ScriptableObject.CreateInstance<ChordProgressionData>();
            built.hideFlags = HideFlags.DontSave; // authoring invariant, in code
            built.TimeSignature = ts;
            built.Measures = derivedMeasures;
            built.subdivisions = subdivisions;
            built.originalInput = roman;

            built.tonalities.Clear();
            built.tonalities.Add(referenceTonality);

            var qualityResolver = new ChordQualityResolver(referenceTonality, autoMode);

            built.events.Clear();
            int currentStep = 0;
            for (int i = 0; i < chords.Count; i++)
            {
                var pc = chords[i];
                int chordSteps = Mathf.Max(1, lengthsSteps[i]);

                // Rests advance time but don't create events (editor parity).
                if (pc.isRest)
                {
                    currentStep += chordSteps;
                    continue;
                }

                var quality = qualityResolver.ResolveChordQuality(pc);
                bool isDiatonic = qualityResolver.IsChordDiatonic(pc.degree, quality);

                built.events.Add(new ChordProgressionData.ChordEvent
                {
                    degree = pc.degree,
                    quality = quality,
                    startStep = currentStep,
                    lengthSteps = chordSteps,
                    velocity = Mathf.Clamp(defaultVelocity, 0, 127),
                    isDiatonic = isDiatonic,
                    degreeAccidental = pc.degreeAccidental,
                });

                currentStep += chordSteps;
            }

            // Readback identity (Ask A / D-DBG3=A is by asset name): give the
            // in-memory instance a stable, recognizable name before DisplayName.
            built.name = MakeRuntimeName(roman);
            built.UpdateDisplayNameAuto();

            data = built;
            return true;
        }

        /// <summary>Compact, single-line instance name for readback/logs.</summary>
        private static string MakeRuntimeName(string roman)
        {
            string flat = roman.Replace('\n', ' ').Trim();
            if (flat.Length > 48) flat = flat.Substring(0, 48).TrimEnd() + "…";
            return $"Runtime: {flat}";
        }
    }
}
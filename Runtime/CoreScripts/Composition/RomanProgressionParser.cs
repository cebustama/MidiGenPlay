using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Result of parsing a Roman chord token from a progression string.
    /// - degree: scale degree (0..6, Tonic..LeadingTone)
    /// - explicitQuality: if not null, this quality is "fixed" by the string
    ///   (via suffix or case). If null, caller may infer diatonic quality.
    /// - durationMeasures: duration in *measures* for this chord.
    /// </summary>
    [Serializable]
    public struct ParsedChord
    {
        public ScaleDegree degree;
        public ChordQuality? explicitQuality;
        public float durationMeasures;

        public ParsedChord(ScaleDegree degree, ChordQuality? explicitQuality, float durationMeasures)
        {
            this.degree = degree;
            this.explicitQuality = explicitQuality;
            this.durationMeasures = durationMeasures;
        }
    }

    /// <summary>
    /// Pure Roman progression parser. Converts strings like:
    ///   "I – V – vi – IV"
    ///   "iiø7 (0.5) – V7 (0.5) – I (1)"
    /// into a list of ParsedChord entries.
    /// 
    /// It does NOT know about tonality or diatonic inference. It only:
    /// - parses the Roman numeral → ScaleDegree
    /// - parses optional quality suffix → explicit ChordQuality
    /// - optionally interprets case as explicit triad quality
    ///   (e.g. "ii" → minor, "V" → major) when enabled.
    /// - parses duration in measures.
    /// </summary>
    public sealed class RomanProgressionParser
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="input">Full Roman progression string.</param>
        /// <param name="defaultMeasuresPerChord">
        /// Duration in measures to use when a chord has no explicit duration.
        /// </param>
        /// <param name="inferTriadFromCaseWhenNoSuffix">
        /// If true, and no quality suffix is given, lowercase roman → Minor,
        /// uppercase → Major (triads). If false, case is ignored and no
        /// explicit quality is set in that situation.
        /// </param>
        public bool TryParse(
            string input,
            float defaultMeasuresPerChord,
            bool inferTriadFromCaseWhenNoSuffix,
            out List<ParsedChord> chords,
            out string error)
        {
            chords = new List<ParsedChord>();

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Input string is empty.";
                return false;
            }

            // Allow multi-line input; treat newlines as spaces.
            input = input.Replace('\n', ' ');

            // Split by dash / en dash / em dash: "I – V – vi – IV"
            string[] tokens = input
                .Split(new[] { '–', '-', '—' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in tokens)
            {
                var token = raw.Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                // pattern: "I", "I (2)", "V7", "iiø7 (0.5)", etc.
                string romanPart = token;
                string durPart = null;

                int paren = token.IndexOf('(');
                if (paren >= 0)
                {
                    romanPart = token.Substring(0, paren).Trim();
                    durPart = token.Substring(paren).Trim(); // "(2)" or "(0.5)"
                }

                if (!TryParseRomanWithQuality(
                        romanPart,
                        inferTriadFromCaseWhenNoSuffix,
                        out var degree,
                        out var explicitQ,
                        out var degErr))
                {
                    error = degErr;
                    return false;
                }

                if (!TryParseDuration(
                        durPart,
                        defaultMeasuresPerChord,
                        out float dur,
                        out var durErr))
                {
                    error = durErr;
                    return false;
                }

                chords.Add(new ParsedChord(degree, explicitQ, dur));
            }

            if (chords.Count == 0)
            {
                error = "No valid chords found in the input.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Parses the Roman part + optional quality suffix into:
        /// - degree (ScaleDegree)
        /// - optional explicit quality (ChordQuality?)
        /// </summary>
        private bool TryParseRomanWithQuality(
            string token,
            bool inferTriadFromCaseWhenNoSuffix,
            out ScaleDegree degree,
            out ChordQuality? explicitQuality,
            out string error)
        {
            degree = ScaleDegree.Tonic;
            explicitQuality = null;
            error = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Empty chord token.";
                return false;
            }

            token = token.Trim();

            // Split: roman core (I/V/X letters) + whatever remains as quality suffix
            int idx = 0;
            while (idx < token.Length && "IVXivx".IndexOf(token[idx]) >= 0)
                idx++;

            if (idx == 0)
            {
                error = $"Could not find a roman numeral in '{token}'.";
                return false;
            }

            string roman = token.Substring(0, idx);
            string suffix = token.Substring(idx); // may be empty, "7", "maj7", "ø7", etc.

            // --- Roman → degree index (0..6) ---
            if (!TryParseRomanToDegreeIndex(roman, out int degIndex))
            {
                error = $"Unsupported roman numeral '{roman}' in token '{token}'.";
                return false;
            }
            degree = (ScaleDegree)degIndex;

            // --- Quality from suffix (optional, highest priority) ---
            suffix = suffix.Trim();
            bool hasExplicitFromSuffix = false;

            if (!string.IsNullOrEmpty(suffix) &&
                TryParseQualitySuffix(suffix, out var qFromSuffix))
            {
                explicitQuality = qFromSuffix;
                hasExplicitFromSuffix = true;
            }

            // --- Optionally, infer triad from case if no suffix ---
            if (!hasExplicitFromSuffix && inferTriadFromCaseWhenNoSuffix)
            {
                // Example convention:
                //   "ii" → minor
                //   "V"  → major
                // Mixed case → ignore and leave null.
                bool anyLower = roman.Any(ch => char.IsLetter(ch) && char.IsLower(ch));
                bool anyUpper = roman.Any(ch => char.IsLetter(ch) && char.IsUpper(ch));

                if (anyLower && !anyUpper)
                    explicitQuality = ChordQuality.Minor;
                else if (anyUpper && !anyLower)
                    explicitQuality = ChordQuality.Major;
            }

            return true;
        }

        /// <summary>
        /// Parses classic roman numerals I..VII to a degree index (0..6).
        /// Case is ignored.
        /// </summary>
        private bool TryParseRomanToDegreeIndex(string roman, out int index)
        {
            index = 0;
            roman = roman.Trim().ToUpperInvariant();

            switch (roman)
            {
                case "I": index = 0; return true;
                case "II": index = 1; return true;
                case "III": index = 2; return true;
                case "IV": index = 3; return true;
                case "V": index = 4; return true;
                case "VI": index = 5; return true;
                case "VII": index = 6; return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Maps common chord notation suffixes (7, maj7, M7, m7, dim, °, ø7, sus2, sus4, etc.)
        /// to the internal ChordQuality enum.
        /// </summary>
        private bool TryParseQualitySuffix(string suffix, out ChordQuality quality)
        {
            // Default – will be ignored if we return false
            quality = ChordQuality.Major;

            if (string.IsNullOrWhiteSpace(suffix))
                return false;

            // Normalize: remove spaces, to lower, replace some unicode aliases
            string s = suffix.Replace(" ", "")
                             .Replace("Δ", "maj")
                             .Replace("∆", "maj")
                             .ToLowerInvariant();

            switch (s)
            {
                // --- Triads (major) ---
                case "":
                case "maj":
                case "ma":
                case "mjr":
                case "mja":
                    quality = ChordQuality.Major;
                    return true;

                // Minor triad
                case "m":
                case "min":
                case "mi":
                case "mn":
                case "-":
                case "min3":
                case "mtri":
                case "mtriad":
                    quality = ChordQuality.Minor;
                    return true;

                case "dim":
                case "o":
                case "°":
                    quality = ChordQuality.Diminished;
                    return true;

                case "aug":
                case "+":
                case "+5":
                    quality = ChordQuality.Augmented;
                    return true;

                // --- Sevenths ---
                case "7":
                case "dom":
                case "dom7":
                    quality = ChordQuality.Dominant7;
                    return true;

                case "maj7":
                case "ma7":
                case "m7+":
                case "mm7":
                case "mmaj7":
                    quality = ChordQuality.Major7;
                    return true;

                case "m7":
                case "-7":
                case "min7":
                    quality = ChordQuality.Minor7;
                    return true;

                case "ø":
                case "ø7":
                case "m7b5":
                case "min7b5":
                    quality = ChordQuality.HalfDiminished7;
                    return true;

                case "dim7":
                case "o7":
                case "°7":
                    quality = ChordQuality.Diminished7;
                    return true;

                // --- Suspended / other ---
                case "sus2":
                    quality = ChordQuality.Sus2;
                    return true;

                case "sus4":
                case "sus":
                    quality = ChordQuality.Sus4;
                    return true;

                default:
                    // Unknown: let caller fall back to diatonic inference.
                    Debug.LogWarning(
                        $"[RomanProgressionParser] Unrecognized chord quality suffix '{suffix}'. " +
                        "Falling back to diatonic / default quality.");
                    return false;
            }
        }

        /// <summary>
        /// Parses a duration token into measures.
        /// - rawDur can be null/empty (use default)
        /// - or something like "(2)", "(0.5)" or just "2"
        /// Durations are in *measures*.
        /// </summary>
        private bool TryParseDuration(
            string rawDur,
            float defaultMeasuresPerChord,
            out float duration,
            out string error)
        {
            error = null;

            // Sensible default if somebody configured 0 or negative
            if (defaultMeasuresPerChord <= 0f)
                defaultMeasuresPerChord = 1f;

            // No explicit duration → use default
            if (string.IsNullOrWhiteSpace(rawDur))
            {
                duration = defaultMeasuresPerChord;
                return true;
            }

            // Clean up string
            string s = rawDur.Trim();

            // Accept "(2)" or "(0.5)" as well as plain "2"
            if (s.StartsWith("(") && s.EndsWith(")"))
            {
                if (s.Length <= 2)
                {
                    error = $"Malformed duration '{rawDur}'. " +
                        "Expected something like '(2)' or '(0.5)'.";
                    duration = 0f;
                    return false;
                }

                s = s.Substring(1, s.Length - 2).Trim();
            }
            else if (s.StartsWith("(") || s.EndsWith(")"))
            {
                // One parenthesis but not the other → clearly a typo
                error = $"Malformed duration '{rawDur}'. Expected '(number)'.";
                duration = 0f;
                return false;
            }

            // Use invariant culture so 0.5 always works
            if (!float.TryParse(
                    s,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out duration))
            {
                error = $"Could not parse duration '{rawDur}'. " +
                    "Use a number like '(2)' or '(0.5)' with a dot as decimal separator.";
                duration = 0f;
                return false;
            }

            if (duration <= 0f)
            {
                error = $"Duration must be > 0 in '{rawDur}'.";
                duration = 0f;
                return false;
            }

            return true;
        }
    }
}


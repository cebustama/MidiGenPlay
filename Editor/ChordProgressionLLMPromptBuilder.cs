#if UNITY_EDITOR
using System;
using System.Text;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function builder for the system + user prompt pair sent to the LLM
    /// when generating a chord progression. Chord analogue of
    /// <c>DrumPatternLLMPromptBuilder</c> (Batch L4, copy-then-unify per
    /// SSoT_Authoring_LLM_Generation.md §2). Produces a Roman-numeral string +
    /// setup card; output is applied through ChordProgressionEditorWindow's
    /// existing ParseAndPreview/ApplyToAsset path. No I/O, no Unity calls beyond
    /// reading the supplied vocabulary SO; EditMode-testable.
    /// </summary>
    public static class ChordProgressionLLMPromptBuilder
    {
        /// <summary>
        /// All inputs needed to build a single chord-progression generation
        /// prompt. All fields are caller-resolved (genre looked up by name,
        /// meter/length already finalized).
        /// </summary>
        public readonly struct Input
        {
            /// <summary>Resolved genre name; must match a ChordGenreEntry in the SO.</summary>
            public readonly string genreName;

            /// <summary>Optional sub-style cue name; if non-empty, must match a cue under the genre.</summary>
            public readonly string subStyleCueName;

            public readonly TimeSignature timeSignature;
            public readonly int beatsPerMeasure;

            /// <summary>Target progression length in measures (durations must sum to this).</summary>
            public readonly int measures;

            /// <summary>Default chord duration in measures when a chord omits its (x) suffix.</summary>
            public readonly float defaultDurationMeasures;

            /// <summary>Optional verbatim user direction (mood, target keys, specific chords).</summary>
            public readonly string userFreeText;

            /// <summary>Optional max total character count. 0 = no enforcement (pre-network cost cap).</summary>
            public readonly int maxCharBudget;

            public Input(
                string genreName,
                string subStyleCueName,
                TimeSignature timeSignature,
                int beatsPerMeasure,
                int measures,
                float defaultDurationMeasures,
                string userFreeText,
                int maxCharBudget = 0)
            {
                this.genreName = genreName;
                this.subStyleCueName = subStyleCueName;
                this.timeSignature = timeSignature;
                this.beatsPerMeasure = beatsPerMeasure;
                this.measures = measures;
                this.defaultDurationMeasures = defaultDurationMeasures;
                this.userFreeText = userFreeText;
                this.maxCharBudget = maxCharBudget;
            }
        }

        /// <summary>
        /// Result of a Build call. On success both prompt strings are populated;
        /// on failure failureReason is populated and prompt strings are empty.
        /// </summary>
        public readonly struct Result
        {
            public readonly bool success;
            public readonly string systemPrompt;
            public readonly string userPrompt;
            public readonly string failureReason;
            public readonly int totalCharCount;

            private Result(bool success, string systemPrompt, string userPrompt, string failureReason)
            {
                this.success = success;
                this.systemPrompt = systemPrompt ?? string.Empty;
                this.userPrompt = userPrompt ?? string.Empty;
                this.failureReason = failureReason ?? string.Empty;
                this.totalCharCount = this.systemPrompt.Length + this.userPrompt.Length;
            }

            public static Result Ok(string system, string user) =>
                new Result(true, system, user, null);

            public static Result Fail(string reason) =>
                new Result(false, null, null, reason);
        }

        /// <summary>
        /// Build the system + user prompt pair from the supplied vocabulary and
        /// input. Returns failure with an explanatory reason on invalid input,
        /// missing genre, missing cue (when specified), or exceeded char budget.
        /// </summary>
        public static Result Build(ChordGenreVocabularySO vocabulary, Input input)
        {
            // ---- Input validation ----
            if (vocabulary == null)
                return Result.Fail("Vocabulary is null.");
            if (string.IsNullOrWhiteSpace(input.genreName))
                return Result.Fail("genreName is empty.");
            if (input.beatsPerMeasure <= 0)
                return Result.Fail("beatsPerMeasure must be > 0.");
            if (input.measures <= 0)
                return Result.Fail("measures must be > 0.");
            if (input.defaultDurationMeasures <= 0f)
                return Result.Fail("defaultDurationMeasures must be > 0.");

            // ---- Genre lookup ----
            ChordGenreEntry genre = null;
            if (vocabulary.genres != null)
            {
                foreach (var g in vocabulary.genres)
                {
                    if (g != null &&
                        string.Equals(g.genreName, input.genreName, StringComparison.OrdinalIgnoreCase))
                    {
                        genre = g;
                        break;
                    }
                }
            }
            if (genre == null)
                return Result.Fail($"Genre '{input.genreName}' not found in vocabulary.");

            // ---- Optional cue lookup ----
            ChordSubStyleCue cue = null;
            if (!string.IsNullOrWhiteSpace(input.subStyleCueName))
            {
                if (genre.subStyleCues != null)
                {
                    foreach (var c in genre.subStyleCues)
                    {
                        if (c != null &&
                            string.Equals(c.name, input.subStyleCueName, StringComparison.OrdinalIgnoreCase))
                        {
                            cue = c;
                            break;
                        }
                    }
                }
                if (cue == null)
                    return Result.Fail($"Sub-style cue '{input.subStyleCueName}' not found under genre '{input.genreName}'.");
            }

            // ---- Build prompts ----
            string systemPrompt = SystemPrompt;
            string userPrompt = BuildUserPrompt(genre, cue, input);

            // ---- Budget check (pre-network cost cap, contract §3.6) ----
            int total = systemPrompt.Length + userPrompt.Length;
            if (input.maxCharBudget > 0 && total > input.maxCharBudget)
                return Result.Fail($"Prompt exceeds char budget: {total}/{input.maxCharBudget}.");

            return Result.Ok(systemPrompt, userPrompt);
        }

        // =====================================================================
        // System prompt — static across all calls.
        //
        // GRAMMAR AUTHORITY: every token rule below is verified against
        // RomanProgressionParser.TryParse (read side), not the editor write side.
        //
        // IMPORTANT parser behaviour the prompt must compensate for:
        // The parser does NOT hard-fail on an unknown quality suffix — it logs a
        // Debug.LogWarning and falls back to diatonic quality (TryParseQualitySuffix
        // returns false → caller leaves quality null). So an out-of-alphabet suffix
        // (e.g. "V13") still APPLIES, silently downgraded, with only a console
        // warning. The zero-warning contract for the chord adopter is therefore
        // enforced by THIS PROMPT plus a post-parse warning check in the response
        // handler — the parser degrades rather than rejects. Keep the alphabet
        // exhaustive and forbid extensions explicitly.
        //
        // Verified facts:
        //   - Degrees: I..VII only (TryParseRomanToDegreeIndex). No extended/added/slash chords.
        //   - Suffixes: see exhaustive list below (TryParseQualitySuffix switch).
        //   - Half-diminished: both "ø7" and "m7b5" accepted.
        //   - Accidental prefix: b / ♭ / # / ♯, prefix position only.
        //   - Rest token: S / REST / R (case-insensitive), or a bare "(x)" duration.
        //   - Duration: "(x)" measures, decimals OK ("(0.5)"), DOT decimal separator
        //     only (InvariantCulture), must be > 0.
        //   - Separators: en-dash / hyphen / em-dash, and newlines = spaces. We
        //     instruct the en-dash exclusively, because a bare hyphen "-" is also a
        //     minor-quality alias and is therefore ambiguous as a separator.
        // =====================================================================

        private const string SystemPrompt =
@"You generate chord-progression DSL for MidiGenPlay's Chord Progression Editor (Roman mode). Output must parse with ZERO warnings against RomanProgressionParser. This is binary — no partial credit, no ""kinda parses"". Note: the parser does not reject an unknown chord suffix; it silently downgrades the chord and logs a warning. A warning counts as failure here, so you must stay strictly inside the alphabet below.

## Output format

Produce exactly two parts, in this order:

1. A ""setup card"" — human-readable prep for the editor's Roman-mode fields:

   **Setup (Roman mode):**

   - Time signature: <TimeSignature enum name, e.g. FourFour>
   - Measures (total): <N>
   - Default duration (measures): <D>
   - Reference tonality: <Tonality enum name, e.g. Ionian>

2. A progression block — a single Roman-numeral string inside a fenced code block:

   **Progression (paste into the Roman string field):**

   ```
   <roman tokens separated by  –  >
   ```

## DSL alphabet (v1, exhaustive — do not exceed)

- Degree (required): Roman numerals I II III IV V VI VII only. Case may carry quality (see below). NO other degrees.
- Quality suffix (optional, appended directly to the numeral). Accepted EXACTLY:
  - (none) = major triad
  - `m` = minor triad
  - `dim` = diminished triad
  - `aug` = augmented triad
  - `7` = dominant 7th
  - `maj7` = major 7th
  - `m7` = minor 7th
  - `ø7` = half-diminished 7th (you may also write `m7b5`)
  - `dim7` = diminished 7th
  - `sus2`, `sus4` = suspended
- Accidental prefix (optional): `b` (flat) or `#` (sharp) immediately before the numeral, e.g. `bVII`, `#iv`.
- Rest / silent span: `S`, optionally with a duration, e.g. `S (1)`.
- Duration suffix (optional): `(x)` after a token, x in measures, e.g. `IV (2)` or `V7 (0.5)`. Decimals allowed; use a DOT as the decimal separator (`(0.5)`, never `(0,5)`). If omitted, the editor's Default Duration applies.
- Separator between tokens: ` – ` (space, EN-DASH, space). Use the en-dash only.

## Forbidden (these warn or break)

- NO extended/added chords: no 9, 11, 13, add9, 6, 6/9.
- NO slash / inversion chords: no `V/V`, no `I/3`.
- NO absolute chord names (no `Cmaj7`, `Am`) — Roman numerals only.
- NO quality suffix outside the list above (an unknown suffix logs a warning = failure).
- NO comma decimal separators in durations.

## Length policy

The sum of all chord durations (each chord's (x), or the default duration when omitted) must equal EXACTLY the target Measures. Emit exactly that many measures of music — never overfill, never pad with trailing rests to round out the bar count, never extend with a fill.

## Constraints (do NOT)

- Do not include prose, labels, or comments inside the fenced progression block.
- Do not output BPM, tempo, or key.
- Do not produce multi-section output (verse/chorus) — one progression per response.

## Self-check before emitting

1. Every token is (optional b/# accidental) + Roman numeral I..VII + (optional suffix from the list) + (optional (x) duration), or an S rest.
2. No extended, added, or slash chords; every suffix is in the alphabet.
3. Durations sum to exactly the target Measures; decimals use a dot.
4. Tokens separated by ` – `; no prose, tempo, or absolute names inside the block.

If any check fails, regenerate. Do not emit partial output.";

        // =====================================================================
        // User prompt — built per call
        // =====================================================================

        private static string BuildUserPrompt(
            ChordGenreEntry genre, ChordSubStyleCue cue, Input input)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("Generate a chord progression.");
            sb.AppendLine();

            // -- Genre header --
            sb.Append("## Genre: ").Append(genre.genreName);
            if (cue != null) sb.Append(" (sub-style: ").Append(cue.name).Append(')');
            sb.AppendLine();
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(genre.styleDescriptors))
            {
                sb.AppendLine(genre.styleDescriptors.Trim());
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(genre.voicingHints))
            {
                sb.AppendLine("### Voicing / quality conventions");
                sb.AppendLine();
                sb.AppendLine(genre.voicingHints.Trim());
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(genre.cadenceCues))
            {
                sb.AppendLine("### Cadence cues");
                sb.AppendLine();
                sb.AppendLine(genre.cadenceCues.Trim());
                sb.AppendLine();
            }

            // -- Mechanical parameters --
            sb.AppendLine("## Mechanical parameters");
            sb.AppendLine();
            sb.Append("- Time signature: ").Append(input.timeSignature)
              .Append(" (beatsPerMeasure = ").Append(input.beatsPerMeasure).Append(')').AppendLine();
            sb.Append("- Measures (total): ").Append(input.measures).AppendLine();
            sb.Append("- Default duration (measures): ").Append(input.defaultDurationMeasures).AppendLine();
            sb.AppendLine();

            // -- Characteristic progressions (anchors, not templates) --
            if (genre.characteristicProgressions != null && genre.characteristicProgressions.Count > 0)
            {
                sb.AppendLine("## Characteristic progressions (anchors, not templates — vary within style)");
                sb.AppendLine();
                foreach (var prog in genre.characteristicProgressions)
                {
                    if (string.IsNullOrWhiteSpace(prog)) continue;
                    sb.Append("- `").Append(prog.Trim()).Append('`').AppendLine();
                }
                sb.AppendLine();
            }

            // -- Sub-style guidance --
            if (cue != null && !string.IsNullOrWhiteSpace(cue.guidance))
            {
                sb.Append("## Sub-style guidance (").Append(cue.name).Append(')').AppendLine();
                sb.AppendLine();
                sb.AppendLine(cue.guidance.Trim());
                sb.AppendLine();
            }

            // -- User free text --
            if (!string.IsNullOrWhiteSpace(input.userFreeText))
            {
                sb.AppendLine("## Additional user direction");
                sb.AppendLine();
                sb.AppendLine(input.userFreeText.Trim());
                sb.AppendLine();
            }

            // -- Final demand (exact-length reinforcement, D-L4.4) --
            sb.Append("Emit the setup card and progression block per the system prompt's format. ")
              .Append("Chord durations must sum to exactly ").Append(input.measures)
              .Append(" measures — no overfill, no trailing rest padding.");

            return sb.ToString();
        }
    }
}
#endif
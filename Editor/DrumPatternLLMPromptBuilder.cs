#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function builder for the system + user prompt pair sent to the
    /// LLM when generating a drum pattern. Takes resolved inputs (genre
    /// already looked up by name, lane composition already finalized,
    /// sub-style overrides already applied) and produces two strings ready
    /// for LLM Core's PromptExecutionHelper.
    /// </summary>
    /// <remarks>
    /// L1 deliverable per Roadmap_LLM_Authoring_MVP.md (D-L10 = α, minimal
    /// path). No I/O, no Unity API calls beyond reading the supplied SO;
    /// suitable for EditMode tests.
    /// </remarks>
    public static class DrumPatternLLMPromptBuilder
    {
        /// <summary>
        /// All inputs needed to build a single drum-pattern generation prompt.
        /// All fields are caller-resolved — the builder does no name-to-entry
        /// lookup beyond fetching the GenreEntry from the vocabulary SO and
        /// optionally the cue from that GenreEntry.
        /// </summary>
        /// <remarks>
        /// Cue mechanical overrides (e.g., <see cref="SubStyleCue.subdivisionsOverride"/>)
        /// are NOT applied here. The caller is responsible for resolving them
        /// upstream and passing the final values in <see cref="beatsPerMeasure"/>,
        /// <see cref="measures"/>, and <see cref="subdivisions"/>. This keeps
        /// the builder pure and lets the UI display the resolved parameters
        /// before generation.
        /// </remarks>
        public readonly struct Input
        {
            /// <summary>Resolved genre name; must match a GenreEntry.genreName in the SO.</summary>
            public readonly string genreName;

            /// <summary>Optional sub-style cue name; if non-empty, must match a cue under the resolved genre.</summary>
            public readonly string subStyleCueName;

            public readonly TimeSignature timeSignature;
            public readonly int beatsPerMeasure;
            public readonly int measures;
            public readonly int subdivisions;

            /// <summary>Final lane composition (genre default ± user override applied upstream).</summary>
            public readonly IReadOnlyList<LaneSpec> laneComposition;

            /// <summary>Optional verbatim user direction (style cues, lane overrides as English).</summary>
            public readonly string userFreeText;

            /// <summary>
            /// Optional maximum total character count. 0 = no enforcement.
            /// When &gt; 0 and the produced prompt exceeds it, Build returns
            /// success=false with a budget-related failureReason.
            /// </summary>
            public readonly int maxCharBudget;

            public Input(
                string genreName,
                string subStyleCueName,
                TimeSignature timeSignature,
                int beatsPerMeasure,
                int measures,
                int subdivisions,
                IReadOnlyList<LaneSpec> laneComposition,
                string userFreeText,
                int maxCharBudget = 0)
            {
                this.genreName = genreName;
                this.subStyleCueName = subStyleCueName;
                this.timeSignature = timeSignature;
                this.beatsPerMeasure = beatsPerMeasure;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.laneComposition = laneComposition;
                this.userFreeText = userFreeText;
                this.maxCharBudget = maxCharBudget;
            }
        }

        /// <summary>
        /// Result of a Build call. On success, both prompt strings are populated;
        /// on failure, failureReason is populated and prompt strings are empty.
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
        /// Build the system + user prompt pair from the supplied vocabulary and input.
        /// Returns success on happy path; returns failure with an explanatory reason
        /// on invalid input, missing genre, missing cue (when one was specified), or
        /// exceeded char budget (when one was specified).
        /// </summary>
        public static Result Build(RhythmGenreVocabularySO vocabulary, Input input)
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
            if (input.subdivisions <= 0)
                return Result.Fail("subdivisions must be > 0.");
            if (input.laneComposition == null || input.laneComposition.Count == 0)
                return Result.Fail("laneComposition is empty.");

            // ---- Genre lookup ----
            GenreEntry genre = null;
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
            SubStyleCue cue = null;
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

            // ---- Compute totalSteps ----
            int totalSteps = input.beatsPerMeasure * input.measures * input.subdivisions;

            // ---- Build prompts ----
            string systemPrompt = SystemPrompt;
            string userPrompt = BuildUserPrompt(genre, cue, input, totalSteps);

            // ---- Budget check ----
            int total = systemPrompt.Length + userPrompt.Length;
            if (input.maxCharBudget > 0 && total > input.maxCharBudget)
                return Result.Fail($"Prompt exceeds char budget: {total}/{input.maxCharBudget}.");

            return Result.Ok(systemPrompt, userPrompt);
        }

        // -----------------------------
        // System prompt — static across all calls
        // -----------------------------

        private const string SystemPrompt =
@"You generate drum pattern DSL for MidiGenPlay's text-mode editor. Output must parse with ZERO warnings against DrumPatternTextParser. This is binary — no partial credit, no ""kinda parses"".

## Output format

Produce exactly two parts, in this order:

1. A ""setup card"" — human-readable Grid-mode prep:

   **Setup (configure in Grid mode):**

   - Time signature: <TimeSignature enum name, e.g. FourFour>
   - Measures: <N>
   - Subdivisions: <N>
   - Lanes (in this order):
     1. <GeneralMidiPercussion enum name> (GM <number>) — default velocity <V>
     2. ...

2. A DSL block — one bare glyph string per lane, in setup-card order, inside a fenced code block:

   **DSL (switch to Text mode, paste one line per lane):**

   ```
   <lane 1 glyphs>
   <lane 2 glyphs>
   ...
   ```

## DSL alphabet (v1, exhaustive)

- `.` or `-` — rest (inactive step)
- `x` — active at lane default velocity
- `X` — active at AccentVelocity (120)
- `o` — active at GhostVelocity (50)
- `|` — bar separator (ignored by parser; insert between measures for readability)
- whitespace — ignored

Any character outside this set fails parsing. The four velocity glyphs are the ENTIRE palette; the DSL cannot express off-tier velocities.

## Length policy

totalSteps = beatsPerMeasure × measures × subdivisions

Each lane string must have EXACTLY totalSteps parseable glyphs (after stripping `|` and whitespace). Shorter or longer fails parsing.

## Constraints (do NOT)

- Do not include prose, labels, or comments inside the fenced DSL block.
- Do not output BPM or tempo.
- Do not invent GeneralMidiPercussion enum members. Use only members from the lane composition supplied in the user prompt.
- Do not produce multi-section patterns (intro/verse/fill) — one pattern per response.
- Do not pad short patterns with rests to reach a rounder totalSteps. Produce exactly totalSteps.

## Self-check before emitting

1. Every character in every lane line is in `.-xXo|` or whitespace.
2. After stripping `|` and whitespace, each lane line length == totalSteps.
3. Lane order in DSL block matches lane order in setup card.
4. Every instrument name is a valid GeneralMidiPercussion enum member.
5. Every default velocity is in 1..127.

If any check fails, regenerate. Do not emit partial output.";

        // -----------------------------
        // User prompt — built per call
        // -----------------------------

        private static string BuildUserPrompt(
            GenreEntry genre, SubStyleCue cue, Input input, int totalSteps)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("Generate a drum pattern.");
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

            if (!string.IsNullOrWhiteSpace(genre.velocityConventions))
            {
                sb.AppendLine("### Velocity conventions");
                sb.AppendLine();
                sb.AppendLine(genre.velocityConventions.Trim());
                sb.AppendLine();
            }

            // -- Mechanical parameters --
            sb.AppendLine("## Mechanical parameters");
            sb.AppendLine();
            sb.Append("- Time signature: ").Append(input.timeSignature)
              .Append(" (beatsPerMeasure = ").Append(input.beatsPerMeasure).Append(')').AppendLine();
            sb.Append("- Measures: ").Append(input.measures).AppendLine();
            sb.Append("- Subdivisions per beat: ").Append(input.subdivisions).AppendLine();
            sb.Append("- totalSteps = ")
              .Append(input.beatsPerMeasure).Append(" × ")
              .Append(input.measures).Append(" × ")
              .Append(input.subdivisions)
              .Append(" = ").Append(totalSteps).AppendLine();
            sb.AppendLine();

            // -- Lane composition --
            sb.AppendLine("## Lane composition");
            sb.AppendLine();
            for (int i = 0; i < input.laneComposition.Count; i++)
            {
                var lane = input.laneComposition[i];
                if (lane == null) continue;
                sb.Append("  ").Append(i + 1).Append(". ")
                  .Append(lane.instrument).Append(" (GM ").Append((int)lane.instrument)
                  .Append(") — default velocity ").Append(lane.defaultVelocity).AppendLine();
            }
            sb.AppendLine();

            // -- Characteristic cells --
            if (genre.characteristicCells != null && genre.characteristicCells.Count > 0)
            {
                sb.AppendLine("## Characteristic 1-bar cells (anchors, not templates — vary within style)");
                sb.AppendLine();
                foreach (var cell in genre.characteristicCells)
                {
                    if (cell == null || string.IsNullOrWhiteSpace(cell.cell)) continue;

                    string instrumentLabel =
                        (cell.laneIndex >= 0
                         && cell.laneIndex < input.laneComposition.Count
                         && input.laneComposition[cell.laneIndex] != null)
                            ? input.laneComposition[cell.laneIndex].instrument.ToString()
                            : $"lane {cell.laneIndex}";

                    sb.Append("- Lane ").Append(cell.laneIndex)
                      .Append(" (").Append(instrumentLabel).Append("), variant \"")
                      .Append(cell.variant).Append("\": `").Append(cell.cell).Append('`').AppendLine();
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

            // -- Final demand --
            sb.Append("Emit the setup card and DSL block per the system prompt's format. ")
              .Append("Each lane string must have exactly ").Append(totalSteps)
              .Append(" parseable glyphs.");

            return sb.ToString();
        }
    }
}
#endif
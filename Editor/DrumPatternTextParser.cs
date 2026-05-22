#if UNITY_EDITOR
using MidiGenPlay;
using System.Collections.Generic;
using System.Text;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function parser/renderer converting between a single lane's
    /// <see cref="DrumPatternData.StepState"/> list and a compact drum-machine-style
    /// text representation.
    ///
    /// <para>Syntax (v1):</para>
    /// <list type="bullet">
    ///   <item><description><c>.</c> or <c>-</c> — rest (inactive step)</description></item>
    ///   <item><description><c>x</c> — active step at lane default velocity (<c>StepState.On(0)</c> — sentinel)</description></item>
    ///   <item><description><c>X</c> — active step at <see cref="AccentVelocity"/> (120)</description></item>
    ///   <item><description><c>o</c> — active step at <see cref="GhostVelocity"/> (50)</description></item>
    ///   <item><description><c>|</c> — ignored (bar separator)</description></item>
    ///   <item><description>whitespace — ignored</description></item>
    ///   <item><description>any other character — treated as rest, emits an <see cref="DrumPatternTextWarningKind.UnknownGlyph"/> warning</description></item>
    /// </list>
    ///
    /// <para>Length-mismatch handling (D-T7):</para>
    /// <list type="bullet">
    ///   <item><description>shorter than totalSteps → right-padded with rests, <see cref="DrumPatternTextWarningKind.LengthShort"/> warning</description></item>
    ///   <item><description>longer than totalSteps  → truncated from the right, <see cref="DrumPatternTextWarningKind.LengthLong"/> warning</description></item>
    /// </list>
    ///
    /// <para>Velocity round-trip note (D-T2):</para>
    /// The glyph alphabet covers three velocity tiers (default / accent / ghost).
    /// Per-step velocities that fall between tiers are lossy on render: they are
    /// snapped to the nearest tier and a <see cref="DrumPatternTextWarningKind.VelocitySnappedToTier"/>
    /// warning is emitted. The asset's per-step velocity remains the canonical truth;
    /// text is a coarse view. Use <see cref="ApplyTextEdits"/> for round-trip preservation
    /// of cells whose typed glyph hasn't changed.
    ///
    /// <para>All functions are deterministic: identical inputs produce identical outputs.
    /// No UnityEditor.* APIs are used in the parser core.</para>
    /// </summary>
    public static class DrumPatternTextParser
    {
        // -------------------------------------------------------------------
        // Glyph constants (v1)
        // -------------------------------------------------------------------

        public const char GlyphRestDot = '.';
        public const char GlyphRestDash = '-';
        public const char GlyphDefault = 'x';
        public const char GlyphAccent = 'X';
        public const char GlyphGhost = 'o';
        public const char GlyphBarSep = '|';

        /// <summary>Canonical accent velocity for the 'X' glyph (v1 constant; later configurable per-lane via asset).</summary>
        public const int AccentVelocity = 120;

        /// <summary>Canonical ghost velocity for the 'o' glyph (v1 constant; later configurable per-lane via asset).</summary>
        public const int GhostVelocity = 50;

        // -------------------------------------------------------------------
        // Public API: Parse
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a single lane's text into a list of exactly <paramref name="totalSteps"/> StepStates.
        /// Caller owns the <paramref name="warnings"/> list (may be null).
        ///
        /// <para>This is a "fresh" parse — it does not preserve any pre-existing per-step velocity
        /// outside the three glyph tiers. Use <see cref="ApplyTextEdits"/> to preserve un-edited cells.</para>
        /// </summary>
        public static List<DrumPatternData.StepState> Parse(
            string input,
            int totalSteps,
            int laneDefaultVelocity,
            int laneIndex = 0,
            List<DrumPatternTextWarning> warnings = null)
        {
            if (totalSteps < 0) totalSteps = 0;
            var result = new List<DrumPatternData.StepState>(totalSteps);

            string cleaned = StripIgnored(input);
            ApplyLengthMismatchWarnings(cleaned.Length, totalSteps, laneIndex, warnings);

            for (int i = 0; i < totalSteps; i++)
            {
                if (i < cleaned.Length)
                    result.Add(ParseGlyph(cleaned[i], laneIndex, i, warnings));
                else
                    result.Add(DrumPatternData.StepState.Off); // right-pad
            }

            return result;
        }

        /// <summary>
        /// Apply text edits to an existing step list, preserving cells whose glyph hasn't changed.
        ///
        /// <para>Per-cell diff: for each step, if the typed glyph matches what the previous
        /// <see cref="DrumPatternData.StepState"/> would render as, the previous StepState is kept
        /// (preserving custom velocity). Otherwise the cell is overwritten with the parsed glyph's
        /// canonical StepState.</para>
        ///
        /// <para>This preserves non-canonical per-step velocities across the text round-trip,
        /// honoring the principle that the text is a view and the asset is canonical.</para>
        /// </summary>
        public static List<DrumPatternData.StepState> ApplyTextEdits(
            IReadOnlyList<DrumPatternData.StepState> previous,
            string input,
            int totalSteps,
            int laneDefaultVelocity,
            int laneIndex = 0,
            List<DrumPatternTextWarning> warnings = null)
        {
            if (totalSteps < 0) totalSteps = 0;
            var result = new List<DrumPatternData.StepState>(totalSteps);

            string cleaned = StripIgnored(input);
            ApplyLengthMismatchWarnings(cleaned.Length, totalSteps, laneIndex, warnings);

            for (int i = 0; i < totalSteps; i++)
            {
                if (i >= cleaned.Length)
                {
                    result.Add(DrumPatternData.StepState.Off); // right-pad
                    continue;
                }

                char typed = cleaned[i];
                var prev = (previous != null && i < previous.Count)
                    ? previous[i]
                    : DrumPatternData.StepState.Off;

                char prevGlyph = StepToGlyph(prev, laneDefaultVelocity, out _);

                if (NormalizeGlyph(typed) == NormalizeGlyph(prevGlyph))
                {
                    // Unchanged in glyph terms; preserve exact previous step (including custom velocity).
                    result.Add(prev);
                }
                else
                {
                    // Cell changed; write canonical state from glyph.
                    result.Add(ParseGlyph(typed, laneIndex, i, warnings));
                }
            }

            return result;
        }

        // -------------------------------------------------------------------
        // Public API: Render
        // -------------------------------------------------------------------

        /// <summary>
        /// Render a lane's step list to canonical text. Optionally inserts <c>|</c> between measures.
        ///
        /// <para>Emits <see cref="DrumPatternTextWarningKind.VelocitySnappedToTier"/> warnings when
        /// a step's velocity does not match the lane default, <see cref="AccentVelocity"/>, or
        /// <see cref="GhostVelocity"/> exactly.</para>
        /// </summary>
        /// <param name="steps">Lane step list. Null is treated as empty.</param>
        /// <param name="laneDefaultVelocity">Lane default velocity, used for tier matching.</param>
        /// <param name="laneIndex">Lane index, only used to tag warnings.</param>
        /// <param name="warnings">Output warning list. May be null if caller doesn't care.</param>
        /// <param name="stepsPerMeasure">If &gt; 0, insert <c>|</c> between measures. 0 = no separators.</param>
        public static string Render(
            IReadOnlyList<DrumPatternData.StepState> steps,
            int laneDefaultVelocity,
            int laneIndex = 0,
            List<DrumPatternTextWarning> warnings = null,
            int stepsPerMeasure = 0)
        {
            if (steps == null || steps.Count == 0) return string.Empty;

            var sb = new StringBuilder(steps.Count + (stepsPerMeasure > 0 ? steps.Count / System.Math.Max(1, stepsPerMeasure) : 0));

            for (int s = 0; s < steps.Count; s++)
            {
                if (stepsPerMeasure > 0 && s > 0 && s % stepsPerMeasure == 0)
                    sb.Append(GlyphBarSep);

                char glyph = StepToGlyph(steps[s], laneDefaultVelocity, out bool snapped);
                if (snapped && warnings != null)
                {
                    int original = steps[s].velocity;
                    warnings.Add(new DrumPatternTextWarning(
                        laneIndex, s, glyph,
                        DrumPatternTextWarningKind.VelocitySnappedToTier,
                        $"velocity {original} snapped to {GlyphTierName(glyph)} ({GlyphTierVelocity(glyph, laneDefaultVelocity)})"));
                }
                sb.Append(glyph);
            }

            return sb.ToString();
        }

        // -------------------------------------------------------------------
        // Internals
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a single glyph into a StepState. Unknown glyphs become rests with an UnknownGlyph warning.
        /// </summary>
        private static DrumPatternData.StepState ParseGlyph(
            char glyph,
            int laneIndex,
            int columnIndex,
            List<DrumPatternTextWarning> warnings)
        {
            switch (glyph)
            {
                case GlyphRestDot:
                case GlyphRestDash:
                    return DrumPatternData.StepState.Off;

                case GlyphDefault:
                    return DrumPatternData.StepState.On(0); // 0 = defer-to-lane-default sentinel

                case GlyphAccent:
                    return DrumPatternData.StepState.On(AccentVelocity);

                case GlyphGhost:
                    return DrumPatternData.StepState.On(GhostVelocity);

                default:
                    warnings?.Add(new DrumPatternTextWarning(
                        laneIndex, columnIndex, glyph,
                        DrumPatternTextWarningKind.UnknownGlyph,
                        $"unknown glyph '{glyph}'; treated as rest"));
                    return DrumPatternData.StepState.Off;
            }
        }

        /// <summary>
        /// Render a single StepState to its canonical glyph, with priority:
        /// <list type="number">
        ///   <item><description>inactive → <c>.</c></description></item>
        ///   <item><description>velocity == 0 (sentinel) → <c>x</c></description></item>
        ///   <item><description>velocity == laneDefault → <c>x</c></description></item>
        ///   <item><description>velocity == AccentVelocity → <c>X</c></description></item>
        ///   <item><description>velocity == GhostVelocity → <c>o</c></description></item>
        ///   <item><description>else → nearest tier, snapped = true</description></item>
        /// </list>
        /// </summary>
        private static char StepToGlyph(
            DrumPatternData.StepState step,
            int laneDefaultVelocity,
            out bool snapped)
        {
            snapped = false;

            if (!step.active) return GlyphRestDot;

            int v = step.velocity;

            // Sentinel — clean default
            if (v == 0) return GlyphDefault;

            // Exact-match priority: default wins ties to keep the most common case unambiguous
            if (v == laneDefaultVelocity) return GlyphDefault;
            if (v == AccentVelocity) return GlyphAccent;
            if (v == GhostVelocity) return GlyphGhost;

            // Snap to nearest tier
            snapped = true;
            int dDef = System.Math.Abs(v - laneDefaultVelocity);
            int dAcc = System.Math.Abs(v - AccentVelocity);
            int dGho = System.Math.Abs(v - GhostVelocity);

            if (dDef <= dAcc && dDef <= dGho) return GlyphDefault;
            if (dGho < dDef && dGho <= dAcc) return GlyphGhost;
            return GlyphAccent;
        }

        /// <summary>Treat <c>.</c> and <c>-</c> as equivalent for diff-comparison purposes.</summary>
        private static char NormalizeGlyph(char g) => g == GlyphRestDash ? GlyphRestDot : g;

        private static string GlyphTierName(char glyph)
        {
            switch (glyph)
            {
                case GlyphAccent: return "accent";
                case GlyphGhost: return "ghost";
                default: return "default";
            }
        }

        private static int GlyphTierVelocity(char glyph, int laneDefaultVelocity)
        {
            switch (glyph)
            {
                case GlyphAccent: return AccentVelocity;
                case GlyphGhost: return GhostVelocity;
                default: return laneDefaultVelocity;
            }
        }

        /// <summary>
        /// Strip ignored characters (whitespace and bar separator <c>|</c>) from input.
        /// Result is a tight glyph-per-step string suitable for length comparison.
        /// </summary>
        private static string StripIgnored(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == GlyphBarSep) continue;
                if (char.IsWhiteSpace(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static void ApplyLengthMismatchWarnings(
            int cleanedLength,
            int totalSteps,
            int laneIndex,
            List<DrumPatternTextWarning> warnings)
        {
            if (warnings == null) return;
            if (cleanedLength == totalSteps) return;

            if (cleanedLength < totalSteps)
            {
                warnings.Add(new DrumPatternTextWarning(
                    laneIndex, -1, '\0',
                    DrumPatternTextWarningKind.LengthShort,
                    $"input length {cleanedLength}, expected {totalSteps}; right-padded with rests"));
            }
            else
            {
                warnings.Add(new DrumPatternTextWarning(
                    laneIndex, -1, '\0',
                    DrumPatternTextWarningKind.LengthLong,
                    $"input length {cleanedLength}, expected {totalSteps}; truncated from the right"));
            }
        }
    }
}
#endif
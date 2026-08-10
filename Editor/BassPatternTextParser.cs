#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using SelfPocketStep = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketStep;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function parser/renderer converting between a bass SelfPocket
    /// articulation pattern (<see cref="SelfPocketStep"/> list) and a compact
    /// text representation. MGP-BASSCARD-WIZARD-1; precedent:
    /// <see cref="DrumPatternTextParser"/> (structure, ignored-character law,
    /// UnknownGlyph degradation) — divergences below are deliberate and
    /// documented, not omissions.
    ///
    /// <para>Syntax (v1, D1=A — one glyph = one character, case-sensitive):</para>
    /// <list type="bullet">
    ///   <item><description><c>S</c> — Slap (hit on the event's selected note)</description></item>
    ///   <item><description><c>P</c> — Pop (hit one octave up, ceiling-folded)</description></item>
    ///   <item><description><c>.</c> or <c>-</c> — Rest (both spellings accepted; <c>.</c> is canonical on render)</description></item>
    ///   <item><description><c>g</c> — Ghost (slap-side ghost: low velocity factor, click gate)</description></item>
    ///   <item><description><c>G</c> — GhostPop (pop-side ghost). Case is the register mnemonic: lowercase = slap side, uppercase = pop side (P is uppercase too)</description></item>
    ///   <item><description><c>H</c> — HammerOn (legato: carrier + step bend, +hammerOffsetDegrees)</description></item>
    ///   <item><description><c>L</c> — PullOff (legato: carrier + step bend, +pullOffsetDegrees; L, not P, to avoid the Pop collision)</description></item>
    ///   <item><description><c>|</c> — ignored (bar separator, readability only — exactly the drum DSL law; bar structure is NOT data, see D5=A)</description></item>
    ///   <item><description>whitespace — ignored (any kind)</description></item>
    ///   <item><description>any other character — treated as Rest, emits an <see cref="BassPatternTextWarningKind.UnknownGlyph"/> warning. NOTE: the drum glyphs <c>x</c>/<c>X</c>/<c>o</c> are unknown HERE — the two DSLs share law, not alphabet.</description></item>
    /// </list>
    ///
    /// <para>Deliberate divergences from the drum parser:</para>
    /// <list type="bullet">
    ///   <item><description><b>No length policy (D13).</b> A bass pattern's
    ///   length IS content: the composer cycles the list over the grid, and
    ///   PHRASE-1 variants may legally differ in length from the body. The
    ///   parse result has exactly as many steps as the cleaned input has
    ///   glyphs — no totalSteps, no padding, no truncation. Zero glyphs
    ///   parses to an empty list with an <see cref="BassPatternTextWarningKind.EmptyPattern"/>
    ///   warning (the runtime treats empty as warn-and-fall-back).</description></item>
    ///   <item><description><b>Lossless round-trip, no ApplyTextEdits (D11).</b>
    ///   <see cref="SelfPocketStep"/> carries no per-step velocity; the glyph
    ///   map is total and bijective (Rest renders <c>.</c>). Pattern → text →
    ///   pattern is exact identity, so the drum parser's per-cell-diff
    ///   machinery has no job here. Do not "complete" this parser by
    ///   symmetry.</description></item>
    /// </list>
    ///
    /// <para>All functions are deterministic: identical inputs produce identical
    /// outputs. No UnityEditor.* APIs are used in the parser core.</para>
    /// </summary>
    public static class BassPatternTextParser
    {
        // -------------------------------------------------------------------
        // Glyph constants (v1)
        // -------------------------------------------------------------------

        public const char GlyphSlap = 'S';
        public const char GlyphPop = 'P';
        public const char GlyphRestDot = '.';
        public const char GlyphRestDash = '-';
        public const char GlyphGhost = 'g';
        public const char GlyphGhostPop = 'G';
        public const char GlyphHammerOn = 'H';
        public const char GlyphPullOff = 'L';
        public const char GlyphBarSep = '|';

        // -------------------------------------------------------------------
        // Public API: Parse
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a pattern's text into a step list whose length equals the
        /// cleaned glyph count (D13: no fixed container). Caller owns the
        /// <paramref name="warnings"/> list (may be null).
        /// </summary>
        /// <param name="input">Raw text. Whitespace and <c>|</c> are ignored.</param>
        /// <param name="bufferLabel">Human-readable buffer identity carried
        /// into warnings, e.g. <c>"body"</c> or <c>"bar 3 / variant 1"</c>.</param>
        /// <param name="warnings">Output warning list. May be null if the caller doesn't care.</param>
        public static List<SelfPocketStep> Parse(
            string input,
            string bufferLabel = "pattern",
            List<BassPatternTextWarning> warnings = null)
        {
            string cleaned = StripIgnored(input);
            var result = new List<SelfPocketStep>(cleaned.Length);

            if (cleaned.Length == 0)
            {
                warnings?.Add(new BassPatternTextWarning(
                    bufferLabel, -1, '\0',
                    BassPatternTextWarningKind.EmptyPattern,
                    "no glyphs; parsed as an empty pattern (runtime warns and falls back)"));
                return result;
            }

            for (int i = 0; i < cleaned.Length; i++)
                result.Add(ParseGlyph(cleaned[i], bufferLabel, i, warnings));

            return result;
        }

        // -------------------------------------------------------------------
        // Public API: Render
        // -------------------------------------------------------------------

        /// <summary>
        /// Render a step list to canonical text. Optionally inserts <c>|</c>
        /// between bars for readability. Render is TOTAL and lossless (D11):
        /// every enum member has exactly one canonical glyph, so this method
        /// can never warn — hence no warnings parameter, by design.
        /// </summary>
        /// <param name="steps">Pattern step list. Null is treated as empty.</param>
        /// <param name="stepsPerBar">If &gt; 0, insert <c>|</c> every that many
        /// steps (the window derives it from its preview meter, D5=A). 0 = no
        /// separators — the safe default when no meter is assumed.</param>
        public static string Render(
            IReadOnlyList<SelfPocketStep> steps,
            int stepsPerBar = 0)
        {
            if (steps == null || steps.Count == 0) return string.Empty;

            var sb = new StringBuilder(
                steps.Count + (stepsPerBar > 0 ? steps.Count / System.Math.Max(1, stepsPerBar) : 0));

            for (int s = 0; s < steps.Count; s++)
            {
                if (stepsPerBar > 0 && s > 0 && s % stepsPerBar == 0)
                    sb.Append(GlyphBarSep);
                sb.Append(StepToGlyph(steps[s]));
            }

            return sb.ToString();
        }

        /// <summary>Canonical glyph for a single step (total map; Rest → <c>.</c>).
        /// An unmapped enum value — only possible if the runtime alphabet grows
        /// ahead of this parser — renders as Rest so authoring degrades locally,
        /// but that state is a versioning bug to fix, not a supported input.</summary>
        public static char StepToGlyph(SelfPocketStep step)
        {
            switch (step)
            {
                case SelfPocketStep.Slap: return GlyphSlap;
                case SelfPocketStep.Pop: return GlyphPop;
                case SelfPocketStep.Rest: return GlyphRestDot;
                case SelfPocketStep.Ghost: return GlyphGhost;
                case SelfPocketStep.GhostPop: return GlyphGhostPop;
                case SelfPocketStep.HammerOn: return GlyphHammerOn;
                case SelfPocketStep.PullOff: return GlyphPullOff;
                default: return GlyphRestDot;
            }
        }

        // -------------------------------------------------------------------
        // Internals
        // -------------------------------------------------------------------

        /// <summary>Parse a single glyph. Unknown glyphs become Rest with an
        /// UnknownGlyph warning (drum-DSL degradation law).</summary>
        private static SelfPocketStep ParseGlyph(
            char glyph,
            string bufferLabel,
            int stepIndex,
            List<BassPatternTextWarning> warnings)
        {
            switch (glyph)
            {
                case GlyphSlap: return SelfPocketStep.Slap;
                case GlyphPop: return SelfPocketStep.Pop;
                case GlyphRestDot:
                case GlyphRestDash: return SelfPocketStep.Rest;
                case GlyphGhost: return SelfPocketStep.Ghost;
                case GlyphGhostPop: return SelfPocketStep.GhostPop;
                case GlyphHammerOn: return SelfPocketStep.HammerOn;
                case GlyphPullOff: return SelfPocketStep.PullOff;

                default:
                    warnings?.Add(new BassPatternTextWarning(
                        bufferLabel, stepIndex, glyph,
                        BassPatternTextWarningKind.UnknownGlyph,
                        $"unknown glyph '{glyph}'; treated as rest"));
                    return SelfPocketStep.Rest;
            }
        }

        /// <summary>
        /// Strip ignored characters (whitespace and bar separator <c>|</c>).
        /// Identical to the drum parser's law: <c>|</c> is readability only —
        /// bar structure is derived from the meter at render/preview time,
        /// never encoded in the pattern data (D5=A).
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
    }
}
#endif
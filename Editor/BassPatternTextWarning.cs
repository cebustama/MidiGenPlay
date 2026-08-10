#if UNITY_EDITOR
using System;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Classifies warnings produced by <see cref="BassPatternTextParser"/> during parse.
    ///
    /// <para>MGP-BASSCARD-WIZARD-1 (D10 / D13). Deliberately SMALLER than
    /// <see cref="DrumPatternTextWarningKind"/>:</para>
    /// <list type="bullet">
    ///   <item><description>No <c>LengthShort</c> / <c>LengthLong</c> — a bass
    ///   pattern's length IS content (the list is cycled by the composer;
    ///   there is no fixed step container to pad or truncate against). The
    ///   only length check is the ADVISORY bar-divisor check performed by the
    ///   editor window against its non-serialized preview meter (D5=A),
    ///   mirroring the runtime's informative non-divisor warning.</description></item>
    ///   <item><description>No <c>VelocitySnappedToTier</c> — <see
    ///   cref="MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketStep"/>
    ///   carries no per-step velocity; the glyph map is total and bijective,
    ///   so render is lossless and can never warn (D11).</description></item>
    /// </list>
    /// </summary>
    public enum BassPatternTextWarningKind
    {
        /// <summary>An unrecognised glyph was encountered. The offending step
        /// was treated as a rest (same degradation law as the drum DSL).</summary>
        UnknownGlyph,

        /// <summary>The cleaned input contained zero glyphs. The parse result
        /// is an empty step list. Mirrors the runtime law: an empty pattern
        /// warns and falls back — never an error, never silence.</summary>
        EmptyPattern,
    }

    /// <summary>
    /// Single warning entry from <see cref="BassPatternTextParser"/>.
    ///
    /// <para>Locator (D10): the bass card has no lanes — its text buffers are
    /// the pattern BODY and the phrase table's per-slot VARIANTS. The parser
    /// stays locator-agnostic: the caller passes a human-readable
    /// <see cref="bufferLabel"/> (e.g. <c>"body"</c>, <c>"bar 3 / variant 1"</c>)
    /// which the warning carries verbatim. Encoding slot/variant into a fake
    /// lane index would be a structural lie; a label is honest and free.</para>
    /// </summary>
    [Serializable]
    public readonly struct BassPatternTextWarning
    {
        /// <summary>Caller-supplied label identifying the offending text buffer.</summary>
        public readonly string bufferLabel;

        /// <summary>Zero-based step index in the cleaned (ignored-chars-stripped)
        /// input; -1 when not applicable (EmptyPattern).</summary>
        public readonly int stepIndex;

        /// <summary>Offending character; '\0' when not applicable.</summary>
        public readonly char glyph;

        /// <summary>Warning classification.</summary>
        public readonly BassPatternTextWarningKind kind;

        /// <summary>Short human-readable message.</summary>
        public readonly string detail;

        public BassPatternTextWarning(
            string bufferLabel,
            int stepIndex,
            char glyph,
            BassPatternTextWarningKind kind,
            string detail)
        {
            this.bufferLabel = bufferLabel;
            this.stepIndex = stepIndex;
            this.glyph = glyph;
            this.kind = kind;
            this.detail = detail;
        }

        public override string ToString()
        {
            string loc = stepIndex >= 0
                ? $"{bufferLabel}, step {stepIndex}"
                : bufferLabel;
            string g = glyph != '\0' ? $" '{glyph}'" : "";
            return $"[{kind}] {loc}{g}: {detail}";
        }
    }
}
#endif
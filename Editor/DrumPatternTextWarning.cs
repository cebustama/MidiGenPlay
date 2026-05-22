#if UNITY_EDITOR
using System;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Classifies warnings produced by <see cref="DrumPatternTextParser"/> during parse or render.
    /// </summary>
    public enum DrumPatternTextWarningKind
    {
        /// <summary>An unrecognised glyph was encountered. The offending step was treated as a rest.</summary>
        UnknownGlyph,

        /// <summary>Input was shorter than totalSteps. Right-padded with rests.</summary>
        LengthShort,

        /// <summary>Input was longer than totalSteps. Truncated from the right.</summary>
        LengthLong,

        /// <summary>
        /// During render (StepState → text), a step's velocity did not match any canonical
        /// glyph tier (default / accent / ghost) and was snapped to the nearest one.
        /// Velocity remains canonical in the asset; the text view is lossy.
        /// </summary>
        VelocitySnappedToTier,
    }

    /// <summary>
    /// Single warning entry from <see cref="DrumPatternTextParser"/>.
    /// Carries enough information for the editor's warning panel to identify the offending
    /// lane and step.
    /// </summary>
    [Serializable]
    public readonly struct DrumPatternTextWarning
    {
        /// <summary>Zero-based lane index this warning relates to.</summary>
        public readonly int laneIndex;

        /// <summary>Zero-based step index in the cleaned (ignored-chars-stripped) input; -1 when not applicable.</summary>
        public readonly int columnIndex;

        /// <summary>Offending or snapped-from character; '\0' when not applicable.</summary>
        public readonly char glyph;

        /// <summary>Warning classification.</summary>
        public readonly DrumPatternTextWarningKind kind;

        /// <summary>Short human-readable message.</summary>
        public readonly string detail;

        public DrumPatternTextWarning(
            int laneIndex,
            int columnIndex,
            char glyph,
            DrumPatternTextWarningKind kind,
            string detail)
        {
            this.laneIndex = laneIndex;
            this.columnIndex = columnIndex;
            this.glyph = glyph;
            this.kind = kind;
            this.detail = detail;
        }

        public override string ToString()
        {
            string loc = columnIndex >= 0
                ? $"lane {laneIndex}, step {columnIndex}"
                : $"lane {laneIndex}";
            string g = glyph != '\0' ? $" '{glyph}'" : "";
            return $"[{kind}] {loc}{g}: {detail}";
        }
    }
}
#endif
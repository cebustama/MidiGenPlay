using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// How to automatically infer chord qualities from tonality + degree,
    /// when the Roman string does not provide an explicit quality.
    /// 
    /// - None:  do not infer; fall back to a plain major triad.
    /// - DiatonicTriads:  use the diatonic triad (I, ii, iii, etc.).
    /// - DiatonicSevenths: use the diatonic 7th chord (Imaj7, iim7, V7, etc.).
    /// </summary>
    public enum AutoChordQualityMode
    {
        None,
        DiatonicTriads,
        DiatonicSevenths
    }

    /// <summary>
    /// Coarse triad "family" used to compare whether a chord is diatonic
    /// without caring about extensions (7ths, 6ths, add9, etc.).
    /// </summary>
    public enum TriadFamily
    {
        Major,
        Minor,
        Diminished,
        Augmented,
        Suspended,
        Other
    }

    /// <summary>
    /// Encapsulates logic for:
    ///  - resolving a final ChordQuality from a ParsedChord (degree + optional explicit quality),
    ///  - deciding whether a chord is "diatonic" (same triad family as the
    ///    scale's diatonic triad for that degree).
    /// 
    /// It is purely runtime / theory logic; the EditorWindow just configures it.
    /// </summary>
    public sealed class ChordQualityResolver
    {
        private readonly Tonality referenceTonality;
        private readonly AutoChordQualityMode autoMode;

        public ChordQualityResolver(Tonality referenceTonality, AutoChordQualityMode autoMode)
        {
            this.referenceTonality = referenceTonality;
            this.autoMode = autoMode;
        }

        /// <summary>
        /// Resolve the final ChordQuality for a ParsedChord:
        /// - If the Roman string provided an explicit quality (suffix or case),
        ///   that always wins.
        /// - Otherwise we may infer a diatonic triad or 7th depending on autoMode.
        /// - If autoMode is None, we fall back to a plain Major triad.
        /// </summary>
        public ChordQuality ResolveChordQuality(ParsedChord c)
        {
            // 1) Explicit quality (suffix or, in None mode, case) always wins.
            if (c.explicitQuality.HasValue)
                return c.explicitQuality.Value;

            // 2) Otherwise, infer from selected auto mode.
            switch (autoMode)
            {
                case AutoChordQualityMode.DiatonicTriads:
                    // Diatonic triad for this mode+degree
                    return GetDiatonicTriadQuality(referenceTonality, c.degree);

                case AutoChordQualityMode.DiatonicSevenths:
                    // Diatonic 7th chord (Imaj7, iim7, V7, etc.)
                    return GetDiatonicSeventhQuality(referenceTonality, c.degree);

                case AutoChordQualityMode.None:
                default:
                    // Literal mode: no inference → default to plain major if nothing else is specified.
                    return ChordQuality.Major;
            }
        }

        /// <summary>
        /// Returns true if 'quality' belongs to the same triad family as the
        /// diatonic triad for (referenceTonality, degree). This is our notion of
        /// "non-borrowed" versus "borrowed / modal mixture".
        /// </summary>
        public bool IsChordDiatonic(ScaleDegree degree, ChordQuality quality)
        {
            var expectedTriad = GetDiatonicTriadQuality(referenceTonality, degree);
            var expectedFamily = GetTriadFamily(expectedTriad);
            var actualFamily = GetTriadFamily(quality);
            return expectedFamily == actualFamily;
        }

        /// <summary>
        /// Classifies a ChordQuality into a coarse triad family (Major, Minor, etc.).
        /// Extensions (7ths, 6ths, add9, etc.) are ignored; only the underlying triad matters.
        /// </summary>
        public static TriadFamily GetTriadFamily(ChordQuality q)
        {
            switch (q)
            {
                // --- Major family (I, Imaj7, V7, etc.) ---
                case ChordQuality.Major:
                case ChordQuality.Major7:
                //case ChordQuality.Major6:
                //case ChordQuality.MajorAdd9:
                //case ChordQuality.Major6Add9:
                case ChordQuality.Dominant7:
                    return TriadFamily.Major;

                // --- Minor family (ii, ii7, vi, etc.) ---
                case ChordQuality.Minor:
                case ChordQuality.Minor7:
                //case ChordQuality.Minor6:
                //case ChordQuality.MinorAdd9:
                //case ChordQuality.Minor6Add9:
                    return TriadFamily.Minor;

                // --- Diminished family (vii°, iiø7, etc.) ---
                case ChordQuality.Diminished:
                case ChordQuality.Diminished7:
                case ChordQuality.HalfDiminished7:
                    return TriadFamily.Diminished;

                // --- Augmented family ---
                case ChordQuality.Augmented:
                //case ChordQuality.Augmented7:
                    return TriadFamily.Augmented;

                // --- Suspended ---
                case ChordQuality.Sus2:
                case ChordQuality.Sus4:
                    return TriadFamily.Suspended;

                // Fallback (any other exotic quality)
                default:
                    return TriadFamily.Other;
            }
        }
    }

}


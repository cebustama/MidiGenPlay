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
    /// without caring about extensions (7ths, 6ths, 9ths, add9, etc.).
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

            // 1b) EDITOR-CASE-1 (D-EC-SEM=B): under auto-diatonic modes,
            // UNAMBIGUOUS case is honored with the precedence
            // suffix > case > auto — previously the case was silently
            // discarded ("I – V – iv – i" parsed all-major under auto).
            // The case fixes the FAMILY; the auto mode fixes the SIZE
            // ("iv" under Sevenths => Minor7, under Triads => Minor). The
            // override only fires when the case genuinely CONTRADICTS the
            // diatonic family: lowercase over a diatonic minor/diminished
            // degree (Roman convention lowercases both) and uppercase over
            // a diatonic major degree keep the auto quality, so purely
            // diatonic strings resolve exactly as before. Mixed case is
            // ignored here (the editor warns). Parse-time only — saved
            // assets are untouched.
            if (autoMode != AutoChordQualityMode.None &&
                (c.caseHint == RomanCaseHint.Lower ||
                 c.caseHint == RomanCaseHint.Upper))
            {
                var diatonicFamily = GetTriadFamily(
                    GetDiatonicTriadQuality(referenceTonality, c.degree));
                bool seventh = autoMode == AutoChordQualityMode.DiatonicSevenths;

                if (c.caseHint == RomanCaseHint.Upper &&
                    diatonicFamily != TriadFamily.Major)
                {
                    // Major-family seventh: Dominant7 on V (functional
                    // expectation), Major7 elsewhere.
                    return seventh
                        ? (c.degree == ScaleDegree.Dominant
                            ? ChordQuality.Dominant7
                            : ChordQuality.Major7)
                        : ChordQuality.Major;
                }

                if (c.caseHint == RomanCaseHint.Lower &&
                    diatonicFamily != TriadFamily.Minor &&
                    diatonicFamily != TriadFamily.Diminished)
                {
                    return seventh ? ChordQuality.Minor7 : ChordQuality.Minor;
                }
                // Case agrees with the diatonic reading => fall through to
                // the auto quality below.
            }

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
        /// Extensions (7ths, 6ths, 9ths, add9, etc.) are ignored; only the underlying triad matters.
        /// </summary>
        public static TriadFamily GetTriadFamily(ChordQuality q)
        {
            switch (q)
            {
                // --- Major family (I, Imaj7, V7, I6, V9, Imaj9, etc.) ---
                case ChordQuality.Major:
                case ChordQuality.Major7:
                case ChordQuality.Major6:
                case ChordQuality.Dominant7:
                case ChordQuality.Dominant9:
                case ChordQuality.Major9:
                    //case ChordQuality.MajorAdd9:
                    //case ChordQuality.Major6Add9:
                    return TriadFamily.Major;

                // --- Minor family (ii, ii7, vi, im6, im9, etc.) ---
                case ChordQuality.Minor:
                case ChordQuality.Minor7:
                case ChordQuality.Minor6:
                case ChordQuality.Minor9:
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

                // --- Suspended (sus2, sus4, 7sus4 — no major/minor 3rd) ---
                case ChordQuality.Sus2:
                case ChordQuality.Sus4:
                case ChordQuality.Dominant7sus4:
                    return TriadFamily.Suspended;

                // Fallback (any other exotic quality)
                default:
                    return TriadFamily.Other;
            }
        }
    }

}
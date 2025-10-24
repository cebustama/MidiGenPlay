using System;
using UnityEngine;

namespace MidiGenPlay.MusicTheory
{
    public static partial class MusicTheory
    {
        public enum ChordQuality
        {
            // Triads
            Major,
            Minor,
            Diminished,
            Augmented,

            // Sevenths
            Major7,
            Minor7,
            Dominant7,
            HalfDiminished7,  // m7♭5
            Diminished7,      // °7

            // Suspended
            Sus2,
            Sus4,
        }

        #region Major Modes

        // Ionian base qualities (index = ScaleDegree 0..6)
        private static readonly ChordQuality[] IonianTriads =
        {
            ChordQuality.Major,     // I
            ChordQuality.Minor,     // ii
            ChordQuality.Minor,     // iii
            ChordQuality.Major,     // IV
            ChordQuality.Major,     // V
            ChordQuality.Minor,     // vi
            ChordQuality.Diminished // vii°
        };

        private static readonly ChordQuality[] IonianSevenths =
        {
            ChordQuality.Major7,         // Imaj7
            ChordQuality.Minor7,         // iim7
            ChordQuality.Minor7,         // iiim7
            ChordQuality.Major7,         // IVmaj7
            ChordQuality.Dominant7,      // V7
            ChordQuality.Minor7,         // vim7
            ChordQuality.HalfDiminished7 // viiø7
        };

        /// <summary>0..6 rotation offset for a mode (Ionian=0, Dorian=1, …, Locrian=6).</summary>
        private static int ModeOffset(Tonality t) => t switch
        {
            Tonality.Ionian => 0,
            Tonality.Dorian => 1,
            Tonality.Phrygian => 2,
            Tonality.Lydian => 3,
            Tonality.Mixolydian => 4,
            Tonality.Aeolian => 5,
            Tonality.Locrian => 6,
            _ => 0
        };

        #endregion

        /// <summary>
        /// Diatonic triad quality for a (mode, degree) by rotating the Ionian template.
        /// </summary>
        public static ChordQuality GetDiatonicTriadQuality(Tonality mode, ScaleDegree degree)
        {
            var idx = ClampDegreeIndex(degree);
            var off = ModeOffset(mode);
            return IonianTriads[(idx + off) % 7];
        }

        /// <summary>
        /// Diatonic 7th quality for a (mode, degree) by rotating the Ionian template.
        /// </summary>
        public static ChordQuality GetDiatonicSeventhQuality(Tonality mode, ScaleDegree degree)
        {
            var idx = ClampDegreeIndex(degree);
            var off = ModeOffset(mode);
            return IonianSevenths[(idx + off) % 7];
        }

        /// <summary>
        /// Quick suggestion API: pick triad or seventh for this (mode, degree).
        /// </summary>
        public static ChordQuality GetSuggestedQuality(
            Tonality mode, ScaleDegree degree, bool preferSeventh = false)
            => preferSeventh ? GetDiatonicSeventhQuality(mode, degree)
                             : GetDiatonicTriadQuality(mode, degree);

        private static int ClampDegreeIndex(ScaleDegree d)
        {
            int i = (int)d;
            return Math.Clamp(i, 0, 6);
        }

        /// <summary>
        /// Convenience: produce a roman numeral for (degree, mode) 
        /// using the diatonic suggestion.
        /// </summary>
        public static string ToRomanRich(
            ScaleDegree degree, Tonality mode, bool preferSeventh = false)
        {
            var q = GetSuggestedQuality(mode, degree, preferSeventh);
            return ToRomanRich(degree, q);
        }

        /// <summary>
        /// Returns a roman numeral string for a degree/quality pair,
        /// using TMP rich text tags for superscripts.
        /// </summary>
        public static string ToRomanRich(ScaleDegree deg, ChordQuality q)
        {
            string baseNum = deg switch
            {
                ScaleDegree.Tonic => "I",
                ScaleDegree.Supertonic => "II",
                ScaleDegree.Mediant => "III",
                ScaleDegree.Subdominant => "IV",
                ScaleDegree.Dominant => "V",
                ScaleDegree.Submediant => "VI",
                ScaleDegree.LeadingTone => "VII",
                _ => "?"
            };

            // lowercase if "minor-ish"
            bool minorish =
                q is ChordQuality.Minor or
                    ChordQuality.Minor7 or
                    ChordQuality.Diminished or
                    ChordQuality.HalfDiminished7;

            string rn = minorish ? baseNum.ToLower() : baseNum;

            return q switch
            {
                ChordQuality.Dominant7 => $"{rn}<sup>7</sup>",
                ChordQuality.Major7 => $"{rn}<sup>Δ7</sup>",
                ChordQuality.Minor7 => $"{rn}<sup>7</sup>",
                ChordQuality.Diminished => $"{rn}<sup>°</sup>",
                ChordQuality.HalfDiminished7 => $"{rn}<sup>ø7</sup>",
                ChordQuality.Sus2 => $"{rn} sus2",
                ChordQuality.Sus4 => $"{rn} sus4",
                _ => rn
            };
        }

        public static int[] GetIntervalsForQuality(ChordQuality q)
        {
            // Root position, ascending; enharmonics expressed with sharps.
            switch (q)
            {
                // Triads
                case ChordQuality.Major: return new[] { 0, 4, 7 };   // R  M3  P5
                case ChordQuality.Minor: return new[] { 0, 3, 7 };   // R  m3  P5
                case ChordQuality.Diminished: return new[] { 0, 3, 6 };   // R  m3  d5
                case ChordQuality.Augmented: return new[] { 0, 4, 8 };   // R  M3  A5

                // Sevenths
                case ChordQuality.Major7: return new[] { 0, 4, 7, 11 }; // R  M3  P5  M7
                case ChordQuality.Minor7: return new[] { 0, 3, 7, 10 }; // R  m3  P5  m7
                case ChordQuality.Dominant7: return new[] { 0, 4, 7, 10 }; // R  M3  P5  m7
                case ChordQuality.HalfDiminished7: return new[] { 0, 3, 6, 10 }; // R  m3  d5  m7
                case ChordQuality.Diminished7: return new[] { 0, 3, 6, 9 }; // R  m3  d5  d7

                // Suspended
                case ChordQuality.Sus2: return new[] { 0, 2, 7 };    // R  M2  P5
                case ChordQuality.Sus4: return new[] { 0, 5, 7 };    // R  P4  P5

                default: return new[] { 0, 4, 7 };    // sensible fallback
            }
        }

        // A compact symbol (C, Cm7, C7, Cmaj7, Cø7, C°7, Caug, Csus2…)
        public static string GetChordSymbol(Melanchall.DryWetMidi.MusicTheory.NoteName root, ChordQuality q)
        {
            string r = root.ToString();
            return q switch
            {
                ChordQuality.Major => r,
                ChordQuality.Minor => r + "m",
                ChordQuality.Diminished => r + "dim",
                ChordQuality.Augmented => r + "aug",

                ChordQuality.Major7 => r + "maj7",
                ChordQuality.Minor7 => r + "m7",
                ChordQuality.Dominant7 => r + "7",
                ChordQuality.HalfDiminished7 => r + "ø7",   // m7♭5
                ChordQuality.Diminished7 => r + "°7",

                ChordQuality.Sus2 => r + "sus2",
                ChordQuality.Sus4 => r + "sus4",
                _ => r
            };
        }

        public static string GetChordSymbolSpelledForDegree(
            Melanchall.DryWetMidi.MusicTheory.NoteName keyRoot,
            int degreeIndex,
            Melanchall.DryWetMidi.MusicTheory.NoteName chordRoot,
            ChordQuality q)
        {
            // Root label with context (e.g., D♭, E♭, A♭...)
            string r = MusicTheory.SpellNoteForDegree(chordRoot, keyRoot, degreeIndex);

            // Same suffix rules as GetChordSymbol
            return q switch
            {
                ChordQuality.Major => r,
                ChordQuality.Minor => r + "m",
                ChordQuality.Diminished => r + "dim",
                ChordQuality.Augmented => r + "aug",

                ChordQuality.Major7 => r + "maj7",
                ChordQuality.Minor7 => r + "m7",
                ChordQuality.Dominant7 => r + "7",
                ChordQuality.HalfDiminished7 => r + "ø7",
                ChordQuality.Diminished7 => r + "°7",

                ChordQuality.Sus2 => r + "sus2",
                ChordQuality.Sus4 => r + "sus4",
                _ => r
            };
        }
    }
}
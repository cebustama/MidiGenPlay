using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// PERC-FALLBACK-1 — package-owned, static substitution table for
    /// <see cref="GeneralMidiPercussion"/> members. For each member it returns
    /// a fixed, ordered list of same-family substitutes to try when the active
    /// kit (<c>MIDIPercussionInstrumentSO</c>) has no exact mapping.
    /// </summary>
    /// <remarks>
    /// Determinism invariant: the lists are static readonly arrays in a fixed
    /// authored order — no RNG, no dictionary iteration. Same kit + same input
    /// always resolves to the same substitute (first mapped entry wins; see
    /// <see cref="PercussionNoteResolver"/>).
    /// <para>
    /// Family grouping mirrors the editor-only <c>LaneShortNames</c> layout
    /// (core kit / cymbals / toms / aux). This file must NOT reference
    /// <c>LaneShortNames</c> — that class is <c>#if UNITY_EDITOR</c> and this
    /// table is runtime code.
    /// </para>
    /// <para>
    /// Tom ordering rule (D-PF4=A): substitutes sorted by ascending GM-number
    /// distance; ties break toward the lower-pitched member (register jumps
    /// upward are more jarring). The explicit lists below are the authority;
    /// the rule is their derivation.
    /// </para>
    /// <para>
    /// Cross-family substitution is intentionally NOT performed (D-PF2):
    /// crashes never fall back to rides, toms never fall back to snares, etc.
    /// Exceptions authored deliberately: HandClap and SideStick fall back to
    /// snares (backbeat role — an audible snare beats a muted lane).
    /// Singletons with no acceptable family substitute (Cowbell, Vibraslap)
    /// return an empty list and resolve to None / GmStandard.
    /// </para>
    /// </remarks>
    public static class PercussionFallbackTable
    {
        private static readonly GeneralMidiPercussion[] Empty =
            new GeneralMidiPercussion[0];

        // -----------------------------
        // Core kit
        // -----------------------------
        private static readonly GeneralMidiPercussion[] ForAcousticBassDrum =
            { GeneralMidiPercussion.BassDrum1 };
        private static readonly GeneralMidiPercussion[] ForBassDrum1 =
            { GeneralMidiPercussion.AcousticBassDrum };

        private static readonly GeneralMidiPercussion[] ForAcousticSnare =
            { GeneralMidiPercussion.ElectricSnare, GeneralMidiPercussion.SideStick };
        private static readonly GeneralMidiPercussion[] ForElectricSnare =
            { GeneralMidiPercussion.AcousticSnare, GeneralMidiPercussion.SideStick };
        private static readonly GeneralMidiPercussion[] ForSideStick =
            { GeneralMidiPercussion.AcousticSnare, GeneralMidiPercussion.ElectricSnare };
        private static readonly GeneralMidiPercussion[] ForHandClap =
            { GeneralMidiPercussion.AcousticSnare, GeneralMidiPercussion.ElectricSnare };

        private static readonly GeneralMidiPercussion[] ForClosedHiHat =
            { GeneralMidiPercussion.PedalHiHat, GeneralMidiPercussion.OpenHiHat };
        private static readonly GeneralMidiPercussion[] ForOpenHiHat =
            { GeneralMidiPercussion.ClosedHiHat, GeneralMidiPercussion.PedalHiHat };
        private static readonly GeneralMidiPercussion[] ForPedalHiHat =
            { GeneralMidiPercussion.ClosedHiHat, GeneralMidiPercussion.OpenHiHat };

        // -----------------------------
        // Toms (D-PF4=A: GM-distance ascending, ties toward lower pitch)
        // GM numbers: LFT=41, HFT=43, LT=45, LMT=47, HiMid=48, HT=50
        // -----------------------------
        private static readonly GeneralMidiPercussion[] ForHighTom =
        {
            GeneralMidiPercussion.HiMidTom, GeneralMidiPercussion.LowMidTom,
            GeneralMidiPercussion.LowTom, GeneralMidiPercussion.HighFloorTom,
            GeneralMidiPercussion.LowFloorTom,
        };
        private static readonly GeneralMidiPercussion[] ForHiMidTom =
        {
            GeneralMidiPercussion.LowMidTom, GeneralMidiPercussion.HighTom,
            GeneralMidiPercussion.LowTom, GeneralMidiPercussion.HighFloorTom,
            GeneralMidiPercussion.LowFloorTom,
        };
        private static readonly GeneralMidiPercussion[] ForLowMidTom =
        {
            GeneralMidiPercussion.HiMidTom, GeneralMidiPercussion.LowTom,
            GeneralMidiPercussion.HighTom, GeneralMidiPercussion.HighFloorTom,
            GeneralMidiPercussion.LowFloorTom,
        };
        private static readonly GeneralMidiPercussion[] ForLowTom =
        {
            GeneralMidiPercussion.HighFloorTom, GeneralMidiPercussion.LowMidTom,
            GeneralMidiPercussion.HiMidTom, GeneralMidiPercussion.LowFloorTom,
            GeneralMidiPercussion.HighTom,
        };
        private static readonly GeneralMidiPercussion[] ForHighFloorTom =
        {
            GeneralMidiPercussion.LowFloorTom, GeneralMidiPercussion.LowTom,
            GeneralMidiPercussion.LowMidTom, GeneralMidiPercussion.HiMidTom,
            GeneralMidiPercussion.HighTom,
        };
        private static readonly GeneralMidiPercussion[] ForLowFloorTom =
        {
            GeneralMidiPercussion.HighFloorTom, GeneralMidiPercussion.LowTom,
            GeneralMidiPercussion.LowMidTom, GeneralMidiPercussion.HiMidTom,
            GeneralMidiPercussion.HighTom,
        };

        // -----------------------------
        // Cymbals (crash-type and ride-type stay separate; D-PF2)
        // -----------------------------
        private static readonly GeneralMidiPercussion[] ForCrashCymbal1 =
        {
            GeneralMidiPercussion.CrashCymbal2, GeneralMidiPercussion.ChineseCymbal,
            GeneralMidiPercussion.SplashCymbal,
        };
        private static readonly GeneralMidiPercussion[] ForCrashCymbal2 =
        {
            GeneralMidiPercussion.CrashCymbal1, GeneralMidiPercussion.ChineseCymbal,
            GeneralMidiPercussion.SplashCymbal,
        };
        private static readonly GeneralMidiPercussion[] ForChineseCymbal =
        {
            GeneralMidiPercussion.CrashCymbal1, GeneralMidiPercussion.CrashCymbal2,
            GeneralMidiPercussion.SplashCymbal,
        };
        private static readonly GeneralMidiPercussion[] ForSplashCymbal =
        {
            GeneralMidiPercussion.CrashCymbal1, GeneralMidiPercussion.CrashCymbal2,
            GeneralMidiPercussion.ChineseCymbal,
        };

        private static readonly GeneralMidiPercussion[] ForRideCymbal1 =
            { GeneralMidiPercussion.RideCymbal2, GeneralMidiPercussion.RideBell };
        private static readonly GeneralMidiPercussion[] ForRideCymbal2 =
            { GeneralMidiPercussion.RideCymbal1, GeneralMidiPercussion.RideBell };
        private static readonly GeneralMidiPercussion[] ForRideBell =
            { GeneralMidiPercussion.RideCymbal1, GeneralMidiPercussion.RideCymbal2 };

        // -----------------------------
        // Aux / latin — natural pairs and small role-groups
        // -----------------------------
        private static readonly GeneralMidiPercussion[] ForHiBongo =
            { GeneralMidiPercussion.LowBongo };
        private static readonly GeneralMidiPercussion[] ForLowBongo =
            { GeneralMidiPercussion.HiBongo };

        private static readonly GeneralMidiPercussion[] ForMuteHiConga =
            { GeneralMidiPercussion.OpenHiConga, GeneralMidiPercussion.LowConga };
        private static readonly GeneralMidiPercussion[] ForOpenHiConga =
            { GeneralMidiPercussion.MuteHiConga, GeneralMidiPercussion.LowConga };
        private static readonly GeneralMidiPercussion[] ForLowConga =
            { GeneralMidiPercussion.OpenHiConga, GeneralMidiPercussion.MuteHiConga };

        private static readonly GeneralMidiPercussion[] ForHighTimbale =
            { GeneralMidiPercussion.LowTimbale };
        private static readonly GeneralMidiPercussion[] ForLowTimbale =
            { GeneralMidiPercussion.HighTimbale };

        private static readonly GeneralMidiPercussion[] ForHighAgogo =
            { GeneralMidiPercussion.LowAgogo };
        private static readonly GeneralMidiPercussion[] ForLowAgogo =
            { GeneralMidiPercussion.HighAgogo };

        private static readonly GeneralMidiPercussion[] ForShortWhistle =
            { GeneralMidiPercussion.LongWhistle };
        private static readonly GeneralMidiPercussion[] ForLongWhistle =
            { GeneralMidiPercussion.ShortWhistle };

        private static readonly GeneralMidiPercussion[] ForShortGuiro =
            { GeneralMidiPercussion.LongGuiro };
        private static readonly GeneralMidiPercussion[] ForLongGuiro =
            { GeneralMidiPercussion.ShortGuiro };

        private static readonly GeneralMidiPercussion[] ForMuteCuica =
            { GeneralMidiPercussion.OpenCuica };
        private static readonly GeneralMidiPercussion[] ForOpenCuica =
            { GeneralMidiPercussion.MuteCuica };

        private static readonly GeneralMidiPercussion[] ForMuteTriangle =
            { GeneralMidiPercussion.OpenTriangle };
        private static readonly GeneralMidiPercussion[] ForOpenTriangle =
            { GeneralMidiPercussion.MuteTriangle };

        // Shaker role-group (continuous timekeeping texture)
        private static readonly GeneralMidiPercussion[] ForMaracas =
            { GeneralMidiPercussion.Cabasa, GeneralMidiPercussion.Tambourine };
        private static readonly GeneralMidiPercussion[] ForCabasa =
            { GeneralMidiPercussion.Maracas, GeneralMidiPercussion.Tambourine };
        private static readonly GeneralMidiPercussion[] ForTambourine =
            { GeneralMidiPercussion.Maracas, GeneralMidiPercussion.Cabasa };

        // Clave / woodblock role-group
        private static readonly GeneralMidiPercussion[] ForClaves =
            { GeneralMidiPercussion.HiWoodBlock, GeneralMidiPercussion.LowWoodBlock };
        private static readonly GeneralMidiPercussion[] ForHiWoodBlock =
            { GeneralMidiPercussion.LowWoodBlock, GeneralMidiPercussion.Claves };
        private static readonly GeneralMidiPercussion[] ForLowWoodBlock =
            { GeneralMidiPercussion.HiWoodBlock, GeneralMidiPercussion.Claves };

        // Cowbell / Vibraslap: singletons — no acceptable family substitute.

        /// <summary>
        /// Ordered substitutes for <paramref name="percussion"/>, most
        /// preferred first. Never null; empty for singletons and for any enum
        /// member added by a future DryWetMidi version (forward-compat: an
        /// unknown member simply resolves to None / GmStandard).
        /// </summary>
        public static IReadOnlyList<GeneralMidiPercussion> GetSubstitutes(
            GeneralMidiPercussion percussion)
        {
            switch (percussion)
            {
                case GeneralMidiPercussion.AcousticBassDrum: return ForAcousticBassDrum;
                case GeneralMidiPercussion.BassDrum1: return ForBassDrum1;

                case GeneralMidiPercussion.AcousticSnare: return ForAcousticSnare;
                case GeneralMidiPercussion.ElectricSnare: return ForElectricSnare;
                case GeneralMidiPercussion.SideStick: return ForSideStick;
                case GeneralMidiPercussion.HandClap: return ForHandClap;

                case GeneralMidiPercussion.ClosedHiHat: return ForClosedHiHat;
                case GeneralMidiPercussion.OpenHiHat: return ForOpenHiHat;
                case GeneralMidiPercussion.PedalHiHat: return ForPedalHiHat;

                case GeneralMidiPercussion.HighTom: return ForHighTom;
                case GeneralMidiPercussion.HiMidTom: return ForHiMidTom;
                case GeneralMidiPercussion.LowMidTom: return ForLowMidTom;
                case GeneralMidiPercussion.LowTom: return ForLowTom;
                case GeneralMidiPercussion.HighFloorTom: return ForHighFloorTom;
                case GeneralMidiPercussion.LowFloorTom: return ForLowFloorTom;

                case GeneralMidiPercussion.CrashCymbal1: return ForCrashCymbal1;
                case GeneralMidiPercussion.CrashCymbal2: return ForCrashCymbal2;
                case GeneralMidiPercussion.ChineseCymbal: return ForChineseCymbal;
                case GeneralMidiPercussion.SplashCymbal: return ForSplashCymbal;

                case GeneralMidiPercussion.RideCymbal1: return ForRideCymbal1;
                case GeneralMidiPercussion.RideCymbal2: return ForRideCymbal2;
                case GeneralMidiPercussion.RideBell: return ForRideBell;

                case GeneralMidiPercussion.HiBongo: return ForHiBongo;
                case GeneralMidiPercussion.LowBongo: return ForLowBongo;
                case GeneralMidiPercussion.MuteHiConga: return ForMuteHiConga;
                case GeneralMidiPercussion.OpenHiConga: return ForOpenHiConga;
                case GeneralMidiPercussion.LowConga: return ForLowConga;
                case GeneralMidiPercussion.HighTimbale: return ForHighTimbale;
                case GeneralMidiPercussion.LowTimbale: return ForLowTimbale;
                case GeneralMidiPercussion.HighAgogo: return ForHighAgogo;
                case GeneralMidiPercussion.LowAgogo: return ForLowAgogo;
                case GeneralMidiPercussion.ShortWhistle: return ForShortWhistle;
                case GeneralMidiPercussion.LongWhistle: return ForLongWhistle;
                case GeneralMidiPercussion.ShortGuiro: return ForShortGuiro;
                case GeneralMidiPercussion.LongGuiro: return ForLongGuiro;
                case GeneralMidiPercussion.MuteCuica: return ForMuteCuica;
                case GeneralMidiPercussion.OpenCuica: return ForOpenCuica;
                case GeneralMidiPercussion.MuteTriangle: return ForMuteTriangle;
                case GeneralMidiPercussion.OpenTriangle: return ForOpenTriangle;

                case GeneralMidiPercussion.Maracas: return ForMaracas;
                case GeneralMidiPercussion.Cabasa: return ForCabasa;
                case GeneralMidiPercussion.Tambourine: return ForTambourine;

                case GeneralMidiPercussion.Claves: return ForClaves;
                case GeneralMidiPercussion.HiWoodBlock: return ForHiWoodBlock;
                case GeneralMidiPercussion.LowWoodBlock: return ForLowWoodBlock;

                default: return Empty; // Cowbell, Vibraslap, future members
            }
        }
    }
}
#if UNITY_EDITOR
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Static map from drum-lane short names (e.g. <c>BD</c>, <c>SN</c>,
    /// <c>HHc</c>) to <see cref="GeneralMidiPercussion"/> enum members.
    /// Consulted by <see cref="DrumPatternEditorImporter"/> when a setup-card
    /// lane token is not a direct enum name (D-L9 = ii).
    /// </summary>
    /// <remarks>
    /// <para>L2 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c>. The keys are
    /// taken directly from <see cref="LaneShortNames"/> constants so the two
    /// files cannot drift: if a short name string changes there, the key here
    /// changes with it.</para>
    ///
    /// <para><b>Case sensitivity.</b> Lookup is case-sensitive on purpose —
    /// R1's conventions distinguish <c>HHc</c> / <c>HHo</c> / <c>HHp</c>, which a
    /// case-insensitive map would collapse. Full enum names (any casing) are
    /// resolved upstream by the importer's exact-enum-name pass before the alias
    /// resolver is consulted, so this map only needs to cover the curated short
    /// tokens.</para>
    ///
    /// <para><b>No silent fallback.</b> <see cref="TryResolve"/> returns null for
    /// an unknown token; the importer surfaces that as an
    /// <c>UnknownInstrument</c> warning and omits the lane.</para>
    /// </remarks>
    public static class LaneAliasDictionary
    {
        private static readonly Dictionary<string, GeneralMidiPercussion> Map =
            new Dictionary<string, GeneralMidiPercussion>
            {
                // -----------------------------
                // Core kit
                // -----------------------------
                { LaneShortNames.BD,  GeneralMidiPercussion.BassDrum1 },     // GM 36
                { LaneShortNames.SN,  GeneralMidiPercussion.AcousticSnare }, // GM 38
                { LaneShortNames.SD,  GeneralMidiPercussion.AcousticSnare }, // alias for SN
                { LaneShortNames.SC,  GeneralMidiPercussion.SideStick },     // GM 37 (cross stick)
                { LaneShortNames.HHc, GeneralMidiPercussion.ClosedHiHat },   // GM 42
                { LaneShortNames.HHo, GeneralMidiPercussion.OpenHiHat },     // GM 46
                { LaneShortNames.HHp, GeneralMidiPercussion.PedalHiHat },    // GM 44

                // -----------------------------
                // Cymbals
                // -----------------------------
                { LaneShortNames.RC,  GeneralMidiPercussion.RideCymbal1 },   // GM 51
                { LaneShortNames.RB,  GeneralMidiPercussion.RideBell },      // GM 53
                { LaneShortNames.CR,  GeneralMidiPercussion.CrashCymbal1 },  // GM 49
                { LaneShortNames.CR2, GeneralMidiPercussion.CrashCymbal2 },  // GM 57
                { LaneShortNames.SP,  GeneralMidiPercussion.SplashCymbal },  // GM 55
                { LaneShortNames.CH,  GeneralMidiPercussion.ChineseCymbal }, // GM 52

                // -----------------------------
                // Toms
                // -----------------------------
                { LaneShortNames.HT,  GeneralMidiPercussion.HighTom },       // GM 50
                { LaneShortNames.MT,  GeneralMidiPercussion.HiMidTom },      // GM 48
                { LaneShortNames.LMT, GeneralMidiPercussion.LowMidTom },     // GM 47
                { LaneShortNames.LT,  GeneralMidiPercussion.LowTom },        // GM 45
                { LaneShortNames.HFT, GeneralMidiPercussion.HighFloorTom },  // GM 43
                { LaneShortNames.LFT, GeneralMidiPercussion.LowFloorTom },   // GM 41

                // -----------------------------
                // Auxiliary percussion (latin / pop)
                // -----------------------------
                { LaneShortNames.CB,  GeneralMidiPercussion.Cowbell },       // GM 56
                { LaneShortNames.TM,  GeneralMidiPercussion.Tambourine },    // GM 54
                { LaneShortNames.CL,  GeneralMidiPercussion.Claves },        // GM 75
                { LaneShortNames.HCL, GeneralMidiPercussion.HandClap },      // GM 39
            };

        /// <summary>
        /// Number of registered short-name aliases.
        /// </summary>
        public static int Count => Map.Count;

        /// <summary>
        /// Resolve a short-name token to a <see cref="GeneralMidiPercussion"/>
        /// member. Returns null if the token is not a registered alias.
        /// Suitable as the <c>aliasResolver</c> delegate for
        /// <see cref="DrumPatternEditorImporter.Parse"/>.
        /// </summary>
        /// <param name="shortName">Short-name token (case-sensitive, e.g. "HHc").</param>
        public static GeneralMidiPercussion? TryResolve(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return null;
            return Map.TryGetValue(shortName, out var instrument)
                ? instrument
                : (GeneralMidiPercussion?)null;
        }

        /// <summary>
        /// True if the token is a registered short-name alias (case-sensitive).
        /// </summary>
        public static bool Contains(string shortName) =>
            !string.IsNullOrEmpty(shortName) && Map.ContainsKey(shortName);
    }
}
#endif
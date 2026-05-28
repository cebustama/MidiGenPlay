#if UNITY_EDITOR
using Melanchall.DryWetMidi.Standards;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Canonical short-name conventions for drum kit lanes, implied by R1's
    /// <c>genre_vocabulary.md</c> GM number tables. Aligned with L2's
    /// <c>LaneAliasDictionary</c> (D-L9 = ii, locked 2026-05-25): the importer
    /// resolves these short names to the corresponding
    /// <see cref="GeneralMidiPercussion"/> enum members before invoking
    /// <c>DrumPatternTextParser</c>.
    /// </summary>
    /// <remarks>
    /// This file is the L1 alignment deliverable. L2's
    /// <c>LaneAliasDictionary</c> will be populated from it. Do not add new
    /// short names without updating L2 in lockstep — otherwise the importer
    /// will fail with an "unknown alias" warning on input the user reasonably
    /// expects to work.
    /// <para>
    /// Each constant value matches its identifier. Consumers build a
    /// <c>string → GeneralMidiPercussion</c> lookup keyed on the value, not
    /// the identifier; the comment column documents the canonical mapping
    /// inline.
    /// </para>
    /// <para>
    /// Coverage is intentionally larger than R1's currently-used set,
    /// anticipating L2 hand-typed input. If a name here is never used, L2
    /// still accepts it harmlessly. If a name is missing, L2 has to invent
    /// the mapping — worse failure mode.
    /// </para>
    /// </remarks>
    public static class LaneShortNames
    {
        // -----------------------------
        // Core kit
        // -----------------------------
        public const string BD = "BD";   // GeneralMidiPercussion.BassDrum1     (GM 36)
        public const string SN = "SN";   // GeneralMidiPercussion.AcousticSnare (GM 38)
        public const string SD = "SD";   // alias for SN
        public const string SC = "SC";   // GeneralMidiPercussion.SideStick     (GM 37) — "cross stick"
        public const string HHc = "HHc";  // GeneralMidiPercussion.ClosedHiHat   (GM 42)
        public const string HHo = "HHo";  // GeneralMidiPercussion.OpenHiHat     (GM 46)
        public const string HHp = "HHp";  // GeneralMidiPercussion.PedalHiHat    (GM 44)

        // -----------------------------
        // Cymbals
        // -----------------------------
        public const string RC = "RC";   // GeneralMidiPercussion.RideCymbal1   (GM 51)
        public const string RB = "RB";   // GeneralMidiPercussion.RideBell      (GM 53)
        public const string CR = "CR";   // GeneralMidiPercussion.CrashCymbal1  (GM 49)
        public const string CR2 = "CR2";  // GeneralMidiPercussion.CrashCymbal2  (GM 57)
        public const string SP = "SP";   // GeneralMidiPercussion.SplashCymbal  (GM 55)
        public const string CH = "CH";   // GeneralMidiPercussion.ChineseCymbal (GM 52)

        // -----------------------------
        // Toms
        // -----------------------------
        public const string HT = "HT";   // GeneralMidiPercussion.HighTom       (GM 50)
        public const string MT = "MT";   // GeneralMidiPercussion.HiMidTom      (GM 48)
        public const string LMT = "LMT";  // GeneralMidiPercussion.LowMidTom     (GM 47)
        public const string LT = "LT";   // GeneralMidiPercussion.LowTom        (GM 45)
        public const string HFT = "HFT";  // GeneralMidiPercussion.HighFloorTom  (GM 43)
        public const string LFT = "LFT";  // GeneralMidiPercussion.LowFloorTom   (GM 41)

        // -----------------------------
        // Auxiliary percussion (latin / pop)
        // -----------------------------
        public const string CB = "CB";   // GeneralMidiPercussion.Cowbell       (GM 56)
        public const string TM = "TM";   // GeneralMidiPercussion.Tambourine    (GM 54)
        public const string CL = "CL";   // GeneralMidiPercussion.Claves        (GM 75)
        public const string HCL = "HCL";  // GeneralMidiPercussion.HandClap      (GM 39)
    }
}
#endif
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Shared Part-level musical context for a smoke render (D-SMOKE-MT-2).
    /// One instance applies to ALL tracks in the single smoke Part.
    ///
    /// Runtime-safe (no UnityEditor); serializable so both the editor window
    /// and the future runtime runner (Stage 2) can expose it as-is.
    /// </summary>
    [System.Serializable]
    public class SmokePartContext
    {
        public string partName = "SmokePart";
        public Tonality tonality = Tonality.Ionian; // Ionian == major
        public NoteName rootNote = NoteName.C;
        public TimeSignature timeSignature = TimeSignature.FourFour;
        [Min(1)] public int measures = 4;

        [Tooltip("Rendered BPM. NOTE: honored via the GenerateSinglePart " +
                 "bpmOverride path; SongConfig.PartConfig.ExplicitBpm is also " +
                 "stamped for fidelity but SongOrchestrator.GenerateSong does " +
                 "not read it (it rolls a random BPM from Part.TempoRange).")]
        [Range(20, 400)] public int bpm = 100;
    }

    /// <summary>
    /// One track row of a multi-track smoke render (D-SMOKE-MT-1=B: asset
    /// refs only; any in-memory convenience bundles are injected by the UI
    /// layer BEFORE this spec reaches the assembler).
    ///
    /// Field semantics mirror SongConfig.PartConfig.TrackConfig exactly:
    /// - Rhythm reads <see cref="percussionInstrument"/> (RhythmTrackComposer
    ///   casts cfg.PercussionInstrument); all other roles read
    ///   <see cref="instrument"/>.
    /// - <see cref="pattern"/> lands on TrackParameters.Pattern. Expected
    ///   subtype per role: Rhythm=DrumPatternData, Melody=MelodyPatternData,
    ///   Backing/Bassline/Harmony=ChordProgressionData. Composers resolve
    ///   card overrides first, then this slot (see each composer).
    /// - <see cref="style"/> lands on TrackParameters.Style. Concrete SO per
    ///   role: RhythmCardConfigSO / BackingCardConfigSO / MelodyCardConfigSO /
    ///   BasslineCardConfigSO. (Harmony's card path is legacy-field based and
    ///   unverified; Harmony is out of v1 smoke scope, D-SMOKE-MT-4=B.)
    /// </summary>
    [System.Serializable]
    public class SmokeTrackSpec
    {
        public TrackRole role = TrackRole.Backing;

        [Tooltip("Melodic instrument — required for every role EXCEPT Rhythm.")]
        public MIDIInstrumentSO instrument;

        [Tooltip("Drum kit — required for the Rhythm role; ignored by all " +
                 "other roles.")]
        public MIDIPercussionInstrumentSO percussionInstrument;

        [Tooltip("Authored pattern asset for TrackParameters.Pattern. " +
                 "Rhythm: DrumPatternData. Melody: MelodyPatternData. " +
                 "Backing/Bassline: ChordProgressionData. The Backing row's " +
                 "progression is what feeds Bass/Melody chord lookups " +
                 "(FindProgressionForPart scans the part's Pattern slots).")]
        public PatternDataSO pattern;

        [Tooltip("Authored card config asset for TrackParameters.Style " +
                 "(RhythmCardConfigSO / BackingCardConfigSO / " +
                 "MelodyCardConfigSO / BasslineCardConfigSO). Optional; " +
                 "composers fall back to defaults when null.")]
        public TrackStyleBundleSO style;
    }
}
using UnityEngine;

/// <summary>
/// Tunable, swappable voicing rules used by chord generation.
/// Keep this narrow (only voice-leading choices) so you can audition different presets quickly.
/// </summary>
[CreateAssetMenu(fileName = "VoiceLeadingConfig", menuName = "MidiGenPlay/Voice Leading Config")]
public class VoiceLeadingConfig : ScriptableObject
{
    [Header("Enable / Candidate Set")]
    [Tooltip("If disabled, chords are realized without voice-leading (simple stack).")]
    public bool enableVoiceLeading = true;

    [Tooltip("Consider chord inversions (root, 1st, 2nd, and 3rd for seventh chords) as candidates.")]
    public bool useInversions = true;

    [Tooltip("Consider the classic Drop-2 voicing as an additional candidate.")]
    public bool useDrop2 = true;

    public enum StartRegisterMode
    {
        /// <summary>Start around the instrument's center (default).</summary>
        InstrumentCenter = 0,
        /// <summary>Start at a fixed octave (clamped to instrument range).</summary>
        FixedOctave = 1,
        /// <summary>Start at instrument center, then bias up/down by semitones.</summary>
        BiasFromCenter = 2,
        RandomAroundCenter = 3,
        Uniform01AroundCenter = 4  // normalized 0..1 spread across the range
    }

    [Header("Starting Register Type")]
    [Tooltip("How the first chord chooses its starting octave before voice-leading takes over.")]
    public StartRegisterMode startRegisterMode = StartRegisterMode.InstrumentCenter;

    [Header("Starting Register Settings")]
    [Tooltip("Used when StartRegisterMode = FixedOctave. Typical range 0..9 depending on instrument data.")]
    public int fixedStartingOctave = 4;

    [Tooltip("When StartRegisterMode = BiasFromCenter, positive moves upward; negative moves downward.")]
    [Range(-24, 24)] public int registerBiasSemitones = 0;

    [Tooltip("Max random deviation (in semitones) from instrument center when RandomAroundCenter is used.")]
    [Range(0, 24)] public int startRegisterRandomRangeSemitones = 12;

    [Tooltip("If Uniform01AroundCenter: 0 = exactly center, 1 = anywhere in full range.")]
    [Range(0f, 1f)] public float startRegisterSpread01 = 0.35f;

    [Header("Scoring (lower is better)")]
    [Tooltip("Weight for total movement (sum of absolute semitone motion between consecutive voices).")]
    [Range(0f, 2f)] public float weightMovement = 1.0f;

    [Tooltip("Weight to reward common tones (this is subtracted from the score per common tone).")]
    [Range(0f, 2f)] public float weightCommonTone = 0.25f;

    [Tooltip("Penalty for spacing outside the [Min, Max] interval between adjacent voices.")]
    [Range(0f, 2f)] public float weightSpacing = 0.10f;

    [Header("Spacing Guidance (semitones between adjacent voices)")]
    [Tooltip("Minimum interval between adjacent voices (e.g., 3 ~ minor 3rd).")]
    public int minTopInterval = 3;

    [Tooltip("Maximum interval between adjacent voices (e.g., 12 ~ octave).")]
    public int maxTopInterval = 12;

    [Header("Register Drift Between Chords")]
    [Tooltip("Maximum allowed average-octave change per chord (in octaves). 0 = keep same register.")]
    [Range(0, 3)] public int maxOctaveShiftPerChord = 1;

    [Tooltip("Penalty weight applied per octave beyond the allowed shift.")]
    [Range(0f, 4f)] public float weightShiftExcess = 1.5f;

    [Tooltip("If true, disqualify candidates whose avg octave shift exceeds the 'Max Octave Shift Per Chord'.")]
    public bool hardLimitOctaveShift = false;

    [Header("Debug")]
    [Tooltip("Print a detailed breakdown for every candidate voicing considered.")]
    public bool debugScoring = false;
}

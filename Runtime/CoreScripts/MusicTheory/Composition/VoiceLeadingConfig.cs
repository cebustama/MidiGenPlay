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
        BiasFromCenter = 2
    }

    [Header("Starting Register (first chord)")]
    [Tooltip("How the first chord chooses its starting octave before voice-leading takes over.")]
    public StartRegisterMode startRegisterMode = StartRegisterMode.InstrumentCenter;

    [Tooltip("Used when StartRegisterMode = FixedOctave. Typical range 0..9 depending on instrument data.")]
    public int fixedStartingOctave = 4;

    [Tooltip("When StartRegisterMode = BiasFromCenter, positive moves upward; negative moves downward.")]
    [Range(-24, 24)] public int registerBiasSemitones = 0;

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
}

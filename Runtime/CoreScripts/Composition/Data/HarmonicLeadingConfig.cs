using MidiGenPlay.Composition;
using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/Harmony Leading Config")]
public class HarmonicLeadingConfig : ScriptableObject
{
    public VoiceLeadingConfig voicingPreset;

    public enum HarmonyRelation
    {
        NearestDifferentChordTone,  // minimal version (used now)
        NextChordToneAbove,
        NextChordToneBelow,
        FixedIntervalSemitones,     // absolute interval from melody
        DiatonicStepsWithinChord    // e.g., “3rd above within the chord quality”
    }
    public HarmonyRelation relation = HarmonyRelation.NearestDifferentChordTone;

    [Header("Intervals (for the options that use them)")]
    [Range(-24, 24)] public int intervalSemitones = 7;  // e.g., 7 = perfect fifth
    [Range(-4, 4)] public int diatonicSteps = 2;        // chord steps (3rd=2, 5th=4, …)

    [Header("Register & Separation")]
    [Range(1, 24)] public int minDistanceFromMelody = 3;   // avoid unison
    [Range(1, 36)] public int maxDistanceFromMelody = 14;

    [Header("Length policy")]
    public MelodicLeadingConfig.LengthMode lengthMode = MelodicLeadingConfig.LengthMode.FillChord;
    [Range(1, 8)] public int fixedSubdivisions = 1;
}

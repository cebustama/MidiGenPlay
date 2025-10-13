using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/Melody Leading Config")]
public class MelodicLeadingConfig : ScriptableObject
{
    [Header("Core")]
    public VoiceLeadingConfig voicingPreset;

    [Tooltip("How many melody notes per chord, roughly.")]
    [Range(1, 8)] public int notesPerChord = 1;

    public enum NoteSource { ChordTonesOnly, PreferChordTonesAllowScale, ScaleOnly }
    public NoteSource source = NoteSource.ChordTonesOnly;

    public enum LengthMode { FillChord, FixedSubdivisions, TieAcrossChanges }
    [Header("Rhythm")]
    public LengthMode lengthMode = LengthMode.FillChord;
    [Range(1, 8)] public int fixedSubdivisions = 1; // if FixedSubdivisions

    [Header("Motion")]
    [Tooltip("Try to keep consecutive melody notes within this distance.")]
    [Range(1, 24)] public int maxStepSemitones = 7;
    [Range(0, 1)] public float chanceRepeatNote = 0.15f;
    [Range(0, 1)] public float chancePassingNote = 0.0f; // reserved for future
}

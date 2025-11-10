using MidiGenPlay.Composition.Phrases;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Melody Leading Config")]
    public class MelodicLeadingConfig : ScriptableObject
    {
        [Header("Core")]
        public VoiceLeadingConfig voicingPreset;

        public enum NoteSource { ChordTonesOnly, PreferChordTonesAllowScale, ScaleOnly }
        public NoteSource noteSource = NoteSource.ChordTonesOnly;

        [Header("Motion")]
        [Tooltip("Try to keep consecutive melody notes within this distance.")]
        [Range(1, 24)] public int maxStepSemitones = 7;
        [Range(0, 1)] public float chanceRepeatNote = 0.15f;
        [Range(0, 1)] public float chancePassingNote = 0.0f; // reserved for future

        [Header("Phrase Palette")]
        public PhrasePaletteSO phrasePalette;

        [Header("Expression")]
        [Tooltip("Velocity ranges for accents / normal / phrase-end landings.")]
        [Range(40, 127)] public int normalVelMin = 80;
        [Range(40, 127)] public int normalVelMax = 100;
        [Range(40, 127)] public int accentVelMin = 105;
        [Range(40, 127)] public int accentVelMax = 120;
        [Range(40, 127)] public int phraseEndVelMin = 95;
        [Range(40, 127)] public int phraseEndVelMax = 110;
    }
}
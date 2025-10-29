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

        [Header("Phrasing / Expression")]
        [Range(1, 8)] public int minSlotsPerPhrase = 2;
        [Range(1, 8)] public int maxSlotsPerPhrase = 4;

        [Tooltip("Chance (0-1) that instead of 'even flow' we do a burst of fast notes then hold/silence.")]
        [Range(0f, 1f)] public float burstPhraseChance = 0.35f;

        [Tooltip("Chance (0-1) that we lean toward a single long sustain w/ pickup.")]
        [Range(0f, 1f)] public float sustainPhraseChance = 0.25f;

        [Tooltip("Probability a given interior slot becomes a rest instead of a note.")]
        [Range(0f, 1f)] public float restProbabilityMidPhrase = 0.2f;

        [Tooltip("Velocity ranges for accents / normal / phrase-end landings.")]
        [Range(40, 127)] public int normalVelMin = 80;
        [Range(40, 127)] public int normalVelMax = 100;
        [Range(40, 127)] public int accentVelMin = 105;
        [Range(40, 127)] public int accentVelMax = 120;
        [Range(40, 127)] public int phraseEndVelMin = 95;
        [Range(40, 127)] public int phraseEndVelMax = 110;

        // Optional: subdivision for burst runs (e.g. 16ths)
        // TODO: Options
        [Range(0.1f, 1f)] public float burstSubdivisionBeats = 0.25f;
        [Range(2, 8)] public int burstNoteCountMin = 3;
        [Range(2, 8)] public int burstNoteCountMax = 5;
    }
}
using MidiGenPlay.Composition.Phrases;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Melody Leading Config")]
    public class MelodicLeadingConfig : ScriptableObject
    {
        [Header("Core")]
        // MGP-MEL-1 P2.5: reserved -- zero consumers anywhere in the melody
        // pipeline (strategies read noteSource/motion fields; chord voicing
        // lives on the Backing side). Hidden so it cannot be authored into
        // silence; unhide when a melody-side consumer exists.
        [HideInInspector] public VoiceLeadingConfig voicingPreset;

        public enum NoteSource { ChordTonesOnly, PreferChordTonesAllowScale, ScaleOnly }
        public NoteSource noteSource = NoteSource.ChordTonesOnly;

        [Header("Motion")]
        [Tooltip("Try to keep consecutive melody notes within this distance.")]
        [Range(1, 24)] public int maxStepSemitones = 7;
        [Range(0, 1)] public float chanceRepeatNote = 0.15f;
        // MGP-MEL-1 P2.4: reserved -- not consumed by any strategy. Hidden
        // until passing-note logic exists.
        [HideInInspector][Range(0, 1)] public float chancePassingNote = 0.0f;

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

        [Header("Pitch Preferences (optional)")]
        [Tooltip("If true and the list is not empty, " +
            "restrict candidate notes to these scale degrees (0 = tonic, 1 = 2nd, ...). " +
                 "Leave disabled or list empty to allow all degrees.")]
        public bool restrictToScaleDegrees = false;

        [Tooltip("Allowed scale degrees relative to the current tonality (0..6). " +
                 "Only used if restrictToScaleDegrees is true.")]
        public List<int> allowedScaleDegrees = new List<int>();
    }
}
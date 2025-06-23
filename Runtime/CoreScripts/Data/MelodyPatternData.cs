using UnityEngine;
using System.Collections.Generic;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;

[CreateAssetMenu(menuName = "MidiGenPlay/Melody Pattern")]
public class MelodyPatternData : ScriptableObject
{
    public string melodyName;     // Melody Name
    public int measures = 4;      // Number of measures
    public MusicTheory.TimeSignature timeSignature;

    public string stringPattern;

    public string midiFileName;

    [System.Serializable]
    public class MelodyNoteData
    {
        public List<ScaleDegree> possibleDegrees; // Possible degrees (1st, 3rd, 5th)
        public int startMeasure;                  // When the note starts (Measure)
        public int startBeat;                     // When the note starts (Beat)
        public int durationBeats;                 // How long the note lasts
        public int velocity = 64;                 // MIDI Velocity (0-127)
        //public bool allowTie;                     // Can the note tie into the next?
        //public bool allowOrnamentation;           // Allow trills/slides?
    }

    public List<MelodyNoteData> melodyNotes = new List<MelodyNoteData>(); // Notes in the melody
}

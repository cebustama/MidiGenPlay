using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MidiGenPlay/Chord Progression")]
public class ChordProgressionData : ScriptableObject
{
    // Track information
    public string progressionName;  // Name of the progression
    public int measures = 4;        // Number of measures in the progression
    public MusicTheory.TimeSignature timeSignature;
    public List<MusicTheory.Tonality> tonalities;

    // List of chords with their timing details
    [System.Serializable]
    public class ChordData
    {
        // TODO: Allow "ANY"
        public List<ScaleDegree> possibleDegrees;   // Possible degrees (tonic, dominant, etc)
        public int startMeasure;                 
        public int startBeat;                   // In terms of quarter notes
        public int durationBeats;               // In terms of quarter notes
        public int velocity = 64;               // (0-127 range)
    }

    public List<ChordData> chords = new List<ChordData>();  // List of chord data
}

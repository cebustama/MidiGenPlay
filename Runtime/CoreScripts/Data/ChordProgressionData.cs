using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Chord Progression")]
    public class ChordProgressionData : PatternDataSO
    {
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
}
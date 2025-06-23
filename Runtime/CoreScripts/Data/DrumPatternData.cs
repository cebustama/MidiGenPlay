using UnityEngine;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;

[CreateAssetMenu(menuName = "MidiGenPlay/Drum Pattern")]
public class DrumPatternData : ScriptableObject
{
    public string patternName;     // Name of the pattern
    public int measures = 4;       // Number of measures to loop
    public MusicalTimeSpan noteLength = MusicalTimeSpan.Eighth; // Default note duration
    public int velocity = 80;      // Default MIDI velocity

    public MusicTheory.TimeSignature timeSignature;

    [System.Serializable]
    public class DrumMapping
    {
        public string drumSymbol;   // Symbol used in PianoRoll notation (e.g., 'x' for HiHat)
        public GeneralMidiPercussion drumNote; // Mapped percussion instrument
    }

    public List<DrumMapping> drumMappings = new List<DrumMapping>();

    [TextArea(5, 10)]
    public string pianoRollPattern; // The full PianoRoll text pattern
}

using UnityEngine;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;

namespace MidiGenPlay
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Drum Pattern")]
    public class DrumPatternData : PatternDataSO
    {
        public MusicalTimeSpan noteLength = MusicalTimeSpan.Eighth; // Default note duration
        public int velocity = 80;      // Default MIDI velocity

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
}
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;

namespace MidiGenPlay
{
    public enum InstrumentType
    {
        Guitar,
        Bass,
        Drums,
        Vocals,
        Piano,
        AcousticGuitar,
        Strings, 
        BrassAndWoodwind
    }

    [CreateAssetMenu(fileName = "NewMIDIInstrument", menuName = "MIDI/MIDI Instrument")]
    public class MIDIInstrumentSO : ScriptableObject
    {
        public string InstrumentName;
        public InstrumentType InstrumentType;
        [SoundFontDropdown] public string SelectedSoundFont;
        [BankDropdown] public string BankName; // Store bank name for clarity
        [PatchDropdown] public string PatchName;
        public bool IsPercussion;

        // TODO: Only show the following fields if isPercussion is true
        [System.Serializable]
        public class PercussionData
        {
            public int kickNote;
            public int snareNote;
            public int hiHatClosedNote;
        }
        public PercussionData percussionData;

        public int PatchIndex;

        public void SetPatchIndex(int index)
        {
            PatchIndex = index;
        }

        [Header("Range of notes for generation")]
        [Range(1, 9)]
        public int octaveMin = 1;
        [Range(1, 9)]
        public int octaveMax = 9;
    }
}

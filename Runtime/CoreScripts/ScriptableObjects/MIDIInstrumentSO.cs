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
        [Header("⚠️ WARNING: DON'T SELECT MULTIPLE INSTRUMENTS ⚠️")]
        [Space]
        public string InstrumentName;
        public InstrumentType InstrumentType;
        [SoundFontDropdown] public string SelectedSoundFont;
        [BankDropdown] public string BankName; // Store bank name for clarity
        [PatchDropdown] public string PatchName;

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

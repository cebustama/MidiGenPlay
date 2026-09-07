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
        BrassAndWoodwind,
        Synth
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


        /// <summary>
        /// MGP-D-W2-DRIFT (D-LOWNOTE=A / D-LOWSCOPE=C): the lowest note this
        /// instrument can PHYSICALLY sound, as a MIDI note number.
        /// 0 = SENTINEL "unauthored" => every consumer is a hard no-op and
        /// existing assets stay byte-identical (the smoke anchor included).
        ///
        /// octaveMin/octaveMax remain the GENERATION band; this is the
        /// INSTRUMENT limit, and it is deliberately NOTE-granular — a bass's
        /// open E (28) is not an octave boundary, so an octave-granular floor
        /// cannot express it. Typical values: 28 = E1 (4-string bass /
        /// double bass), 23 = B0 (5-string), 24 = C1 (double bass with a C
        /// extension).
        /// </summary>
        [Range(0, 127)]
        public int lowestNote = 0;

        [Header("Mix")]
        [Range(0f, 1f)] public float volume01 = 1f;
    }
}

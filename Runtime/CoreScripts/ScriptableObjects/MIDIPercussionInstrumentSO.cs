using UnityEngine;
using System.Collections.Generic;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;

[CreateAssetMenu(fileName = "NewMIDIPercussionInstrument", menuName = "MIDI/MIDI Percussion Instrument")]
public class MIDIPercussionInstrumentSO : MIDIInstrumentSO
{
    [System.Serializable]
    public class PercussionMapping
    {
        public GeneralMidiPercussion percussionType; // The general MIDI percussion instrument
        public NoteName noteName; // Note without octave (e.g., C, D, F#)
        public int octave; // The octave of the note (e.g., 1, 2, 3)

        /// <summary>
        /// Converts the stored NoteName & octave into a DryWetMIDI Note object.
        /// </summary>
        public Note ToNote()
        {
            return Note.Get(noteName, octave + 1);
        }
    }

    [Header("Percussion Mappings")]
    [Tooltip("Includes common base drum kit mappings as default.")]
    public List<PercussionMapping> percussionMappings = new List<PercussionMapping>()
    {
        new PercussionMapping { percussionType = GeneralMidiPercussion.AcousticBassDrum, noteName = NoteName.C, octave = 1 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.AcousticSnare, noteName = NoteName.D, octave = 1 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.ClosedHiHat, noteName = NoteName.FSharp, octave = 1 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.OpenHiHat, noteName = NoteName.ASharp, octave = 1 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.CrashCymbal1, noteName = NoteName.CSharp, octave = 2 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.RideCymbal1, noteName = NoteName.DSharp, octave = 2 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.LowTom, noteName = NoteName.A, octave = 2 },
        new PercussionMapping { percussionType = GeneralMidiPercussion.HighTom, noteName = NoteName.B, octave = 2 }
    };

    /// <summary>
    /// Get the mapped note for a given General MIDI percussion type.
    /// </summary>
    public bool TryGetMappedNote(GeneralMidiPercussion percussion, out Note mappedNote)
    {
        // Search in percussion mappings
        foreach (var mapping in percussionMappings)
        {
            if (mapping.percussionType == percussion)
            {
                mappedNote = mapping.ToNote();
                return true;
            }
        }

        // Default to C0 if not found
        mappedNote = Note.Get(NoteName.C, 0);
        return false;
    }
}

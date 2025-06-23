using Melanchall.DryWetMidi.Core;
using System.IO;

namespace MidiGenPlay
{
    public enum Genre
    {
        Rock,
        Pop,
        Jazz,
        Electronic,
        Blues,
        Metal,
        Punk,
        Latin,
        HipHop,
        Folk,
        SoulRnb,
        Reggae,
        Country,
        Classical
    }

    public enum TrackRole
    {
        Rhythm,
        Backing,
        Lead
    }

    [System.Serializable]
    public class MIDITrack
    {
        public TrackRole Role;
        public MIDIInstrumentSO Instrument;
        public Genre Genre;
        public int Channel;
        public MidiFile MidiFile;
        public byte[] MidiData;

        public MIDITrack(TrackRole role, MIDIInstrumentSO instrument, Genre genre = Genre.Rock)
        {
            Role = role;
            Instrument = instrument;
            Genre = genre;
        }

        public MIDITrack(MidiFile midiFile)
        {
            SetMidiData(midiFile);
        }

        public void SetMidiData(MidiFile midiFile)
        {
            // Convert the MidiFile to a byte array
            using (var memoryStream = new MemoryStream())
            {
                midiFile.Write(memoryStream);
                MidiData = memoryStream.ToArray();
            }
        }
    }

}
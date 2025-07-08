using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.MusicTheory;
using MidiPlayerTK;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay
{
    [System.Serializable]
    public class MIDISongPart
    {
        public string PartName; // e.g., Verse, Chorus
        public int BPM;
        public MusicTheory.Tonality Tonality;
        public NoteName RootNote;
        public List<MIDITrack> Tracks;

        public MidiFile CurrentMidiFile;
        public MidiFile DummyMidiFile;

        public MIDISongPart(
            string partName,
            int bpm,
            MusicTheory.Tonality scaleType,
            NoteName rootNote)
        {
            PartName = partName;
            BPM = bpm;
            Tonality = scaleType;
            RootNote = rootNote;
            Tracks = new List<MIDITrack>();
        }

        public MIDISongPart(MidiFile midiFile)
        {
            CurrentMidiFile = midiFile;
        }

        public void AddTrack(MIDITrack track)
        {
            Tracks.Add(track);
        }

        public void GenerateTracks(MIDIGeneratorManager midiGenerator)
        {
            // TODO: Refactor this part
            List<Chord> diatonicChords = MusicTheory.GetChordsFromTonality(Tonality, RootNote, 4);
            // Note: chords are defined by NoteNames with no octave information

            int channel = 0;
            foreach (MIDITrack track in Tracks)
            {
                // Generate MIDI data for this track
                MidiFile trackMidiFile = midiGenerator.GenerateMidiFile(
                    track.Instrument.InstrumentType,
                    track.Role,
                    track.Genre,
                    BPM,
                    diatonicChords,
                    4, 
                    track.Instrument.octaveMin, 
                    track.Instrument.octaveMax,
                    channel++,
                    int.Parse(track.Instrument.BankName),
                    track.Instrument.PatchIndex
                );

                track.SetMidiData(trackMidiFile);
            }

            CurrentMidiFile = GenerateMidiFileFromTracks();
            DummyMidiFile = GenerateDummyMidiFile();

            Debug.Log($"Generated all tracks for part: {PartName}");
        }

        private MidiFile GenerateMidiFileFromTracks()
        {
            var midiFile = new MidiFile();

            foreach (var track in Tracks)
            {
                // Convert byte array to MidiFile (if data is stored as byte[])
                MidiFile trackMidiFile;
                using (var stream = new MemoryStream(track.MidiData))
                {
                    trackMidiFile = MidiFile.Read(stream);
                }

                // Create a new TrackChunk for each track
                var trackChunk = new TrackChunk();

                // Set the channel for each event in the track chunk
                foreach (var midiEvent in trackMidiFile.GetTrackChunks().SelectMany(chunk => chunk.Events))
                {
                    if (midiEvent is ChannelEvent channelEvent)
                    {
                        // Assign the channel from track.Channel (assumes Channel is set in MIDITrack)
                        //channelEvent.Channel = (FourBitNumber)track.Channel;
                    }

                    trackChunk.Events.Add(midiEvent);
                }

                // Add the configured trackChunk to the main midiFile
                midiFile.Chunks.Add(trackChunk);
            }

            return midiFile;
        }

        public MidiFile GenerateDummyMidiFile(int dummyNoteCount = 1, float dummyDurationInSeconds = 0.5f)
        {
            var dummyFile = new MidiFile();
            int channel = 0;

            foreach (var track in Tracks)
            {
                var trackChunk = new TrackChunk();

                // Add Bank Select MSB and LSB
                int bankNumber = int.Parse(track.Instrument.BankName);
                trackChunk.Events.Add(new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)(bankNumber >> 7))
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });
                trackChunk.Events.Add(new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)(bankNumber & 0x7F))
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // Add Program Change (Preset) event
                trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)track.Instrument.PatchIndex)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // Calculate DeltaTime for each dummy note based on duration
                int ticksPerSecond = 480; // Assuming 480 ticks per quarter note (adjust if different)
                int totalTicks = (int)(dummyDurationInSeconds * ticksPerSecond);
                int ticksPerNote = totalTicks / dummyNoteCount;

                // Add the specified number of dummy notes
                for (int i = 0; i < dummyNoteCount; i++)
                {
                    trackChunk.Events.Add(new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)1) // Low velocity
                    {
                        Channel = (FourBitNumber)channel,
                        DeltaTime = ticksPerNote
                    });
                    trackChunk.Events.Add(new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0)
                    {
                        Channel = (FourBitNumber)channel,
                        DeltaTime = ticksPerNote
                    });
                }

                dummyFile.Chunks.Add(trackChunk);
                channel++;
            }

            DummyMidiFile = dummyFile; // Store the dummy file in the object
            return dummyFile;
        }

        public float GetPartDuration(int measures = 4)
        {
            int beatsPerMeasure = 4; // Common time
            int totalBeats = measures * beatsPerMeasure;
            return (60f / BPM) * totalBeats;
        }

        public static TrackRole GetTrackRole(
            List<MIDITrack> currentTracks, MIDIInstrumentSO midiInstrument
        ) {
            // Determine if each role has been assigned at least once
            bool hasRhythm = currentTracks.Exists(track => track.Role == TrackRole.Rhythm);
            bool hasBacking = currentTracks.Exists(track => track.Role == TrackRole.Backing);
            bool hasLead = currentTracks.Exists(track => track.Role == TrackRole.Lead);

            // Prioritize assigning roles based on remaining required roles
            if (!hasRhythm && (midiInstrument.InstrumentType == InstrumentType.Drums))
            {
                return TrackRole.Rhythm;
            }
            else if (!hasLead)
            {
                return TrackRole.Lead;
            }
            else if (!hasBacking)
            {
                return TrackRole.Backing;
            }
            else
            {
                // All roles have been assigned at least once, so we can repeat roles
                TrackRole[] roles = { TrackRole.Rhythm, TrackRole.Backing, TrackRole.Lead };
                return roles[UnityEngine.Random.Range(0, roles.Length)];
            }
        }

        public MidiFile GetMidiFile()
        {
            // TODO: Go through each MidiTrack to create the MidiFile
            return CurrentMidiFile;
        }
    }
}

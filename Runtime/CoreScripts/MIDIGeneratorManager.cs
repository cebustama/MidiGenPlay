using UnityEngine;
using System.IO;
using System.Linq;

// DryWetMIDI namespaces
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;

using MusicTheoryNote = Melanchall.DryWetMidi.MusicTheory.Note;
using MusicTheoryChord = Melanchall.DryWetMidi.MusicTheory.Chord;
using Melanchall.DryWetMidi.Common;

namespace MidiGenPlay
{
    public class MIDIGeneratorManager : MonoBehaviour
    {
        public string midiFileName = "DrumsTest.mid";

        public string LastSavedPath { get; private set; }

        public MidiFile GenerateMidiFile(
            InstrumentType instrumentType,
            TrackRole role,
            Genre genre,
            int BPM = 100,
            List<MusicTheoryChord> chordOptions = null,
            int measures = 4,
            int minOctave = 1,
            int maxOctave = 9,
            int channel = 0,
            int bankNumber = 0,
            int presetNumber = 0)
        {
            Debug.Log($"Generating MIDI file for " +
                $"{instrumentType} {role} bank: {bankNumber} preset: {presetNumber}");

            EnhancedPatternBuilder enhancedPatternBuilder
                = new EnhancedPatternBuilder(instrumentType, role, genre);

            enhancedPatternBuilder.Generate(
                measures, 
                chordOptions, 
                minOctave, 
                maxOctave
            );

            Pattern pattern = enhancedPatternBuilder.Build();

            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(BPM));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // Add Bank Select and Program Change events at the start of each track
            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                // BANK
                // Split the bank number into MSB and LSB if it's greater than 127
                int msb = (bankNumber >> 7) & 0x7F; // Most significant byte
                int lsb = bankNumber & 0x7F;        // Least significant byte
                
                // Add the bank select MSB
                trackChunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)msb)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 10
                });

                // Add the bank select LSB
                trackChunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)lsb)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 10
                });

                // PATCH/PROGRAM/PRESET
                // Add the program change event for the preset
                trackChunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)presetNumber)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 10
                });
            }


            // TODO: Remember this loop to iterate through all events
            Debug.Log("Generated track events:");
            foreach (var midiEvent in midiFile.GetTrackChunks().SelectMany(chunk => chunk.Events))
            {
                Debug.Log(midiEvent.ToString() + " " + midiEvent.DeltaTime);
                if (midiEvent is ChannelEvent channelEvent)
                {
                    channelEvent.Channel = (FourBitNumber)channel;
                    //Debug.Log(channelEvent.Channel);
                }
            }

            return midiFile;
        }

        public string SaveMIDIFile(MidiFile midiFile)
        {
            return null;

            // Save the MIDI file
            string fullPath = GetUniqueFilePathInPersistentDataPath();
            midiFile.Write(fullPath, overwriteFile: true);
            LastSavedPath = fullPath;

            Debug.Log("MIDI file stored at: " + fullPath);

            return fullPath;
        }

        public string GetUniqueFilePathInPersistentDataPath()
        {
            string directoryPath = Path.Combine(Application.persistentDataPath, "MidiDB");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            string baseFileName = Path.GetFileNameWithoutExtension(midiFileName);
            string fileExtension = Path.GetExtension(midiFileName);

            // Determine a unique filename by appending a number
            int fileCount = Directory.GetFiles(directoryPath, $"{baseFileName}*{fileExtension}").Length;
            string newFileName = $"{baseFileName}_{fileCount + 1}{fileExtension}";
            string fullPath = Path.Combine(directoryPath, newFileName);

            return fullPath;
        }
    }
}

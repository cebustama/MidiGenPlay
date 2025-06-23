using Melanchall.DryWetMidi.Core;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    [System.Serializable]
    public class MIDISong
    {
        public string SongTag; // TODO Tag
        public List<MIDISongPart> SongParts;

        public MIDISong(string songName)
        {
            SongTag = songName;
            SongParts = new List<MIDISongPart>();
        }

        public MIDISong(MidiFile midiFile)
        {
            SongParts = new List<MIDISongPart>();

        }

        public void AddPart(MIDISongPart part)
        {
            SongParts.Add(part);
        }

        public void GenerateSong()
        {
            foreach (var part in SongParts)
            {
                //part.GenerateTracks(midiGenerator);
            }

            //Debug.Log($"Generated all parts for song: {SongTag}");
        }

        public float GetSongDuration()
        {
            float totalDuration = 0f;
            foreach (var part in SongParts)
            {
                totalDuration += part.GetPartDuration();
            }
            return totalDuration;
        }

        public MidiFile GetMidiFile()
        {
            // TODO: Go through each part and each track to generate the final MidiFile
            return new MidiFile();
        }
    }
}

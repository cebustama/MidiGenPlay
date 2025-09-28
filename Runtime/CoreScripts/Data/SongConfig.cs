using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    [System.Serializable]
    public class SongConfig
    {
        public List<PartConfig> Parts;
        public List<PartSequenceEntry> Structure;

        public List<string> ChannelMusicianOrder = new(); // channel index -> musicianId
        public List<TrackRole> ChannelRoles = new(); // channel index -> role (Rhythm/Lead/Backing)

        [System.Serializable]
        public class PartConfig
        {
            public string Name;
            public List<TrackConfig> Tracks;

            public Tonality Tonality;
            public NoteName RootNote;
            public TempoRange TempoRange;
            public TimeSignature TimeSignature;
            public int Measures;

            /// <summary>
            /// A single track’s configuration
            /// </summary>
            [System.Serializable]
            public class TrackConfig
            {
                public MIDIInstrumentSO Instrument;
                public MIDIPercussionInstrumentSO PercussionInstrument;
                public TrackRole Role;
                public TrackParameters Parameters;

                public string MusicianId;
            }
        }

        [System.Serializable]
        public class PartSequenceEntry
        {
            public int PartIndex;
            public int RepeatCount;
        }
    }

    /// <summary>
    /// Base for any role-specific data (drum patterns, chord progressions, melodies…)
    /// </summary>
    [System.Serializable]
    public class TrackParameters 
    {
        public PatternDataSO Pattern;
    }
}
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using System.Linq;
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
            public int Repetitions;

            public override string ToString()
            {
                var ts = $"{TimeSignatureProperties[TimeSignature].BeatsPerMeasure}/" +
                         $"{TimeSignatureProperties[TimeSignature].BeatUnit}";
                var tracks = Tracks != null
                    ? string.Join(", ", Tracks.Select((t, i) => $"[{i}:{t}]"))
                    : "(no tracks)";
                return $"Part '{Name}' rep={Repetitions} TS={ts} Ton={Tonality} Root={RootNote} :: {tracks}";
            }

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
                public int Channel = -1;

                public override string ToString()
                {
                    var inst = Instrument != null ? Instrument.name
                             : PercussionInstrument != null ? PercussionInstrument.name
                             : "-";
                    var pat = Parameters?.Pattern != null ? Parameters.Pattern.name : "-";
                    var mus = !string.IsNullOrEmpty(MusicianId) ? MusicianId : "(unassigned)";
                    var ch = Channel >= 0 ? Channel.ToString() : "-";
                    return $"role={Role} mus={mus} ch={ch} inst={inst} pattern={pat}";
                }
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
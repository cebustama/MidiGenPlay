using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    // Domain-facing indices to avoid passing references around
    public readonly struct PartIdx
    {
        public readonly int Value;
        public PartIdx(int value) { Value = value; }
        public override string ToString() => Value.ToString();
    }

    public readonly struct TrackIdx
    {
        public readonly int Value;
        public TrackIdx(int value) { Value = value; }
        public override string ToString() => Value.ToString();
    }

    // Event payloads
    public sealed class PartChangedEventArgs : EventArgs
    {
        public PartIdx Part { get; }
        public PartChangedEventArgs(PartIdx part) => Part = part;
    }
    public sealed class TrackChangedEventArgs : EventArgs
    {
        public PartIdx Part { get; }
        public TrackIdx Track { get; }
        public TrackChangedEventArgs(PartIdx p, TrackIdx t) { Part = p; Track = t; }
    }

    public interface ISongConfigManager
    {
        SongConfig Song { get; }
        PartIdx ActivePart { get; }
        TrackIdx ActiveTrack { get; }

        // Selection
        void SelectPart(PartIdx p);
        void SelectTrack(TrackIdx t);

        // Parts
        PartIdx AddPart(SongConfig.PartConfig template = null);
        void RemovePart(PartIdx p);

        // Tracks (within ActivePart)
        TrackIdx AddTrack(TrackRole defaultRole = TrackRole.Backing);
        void RemoveTrack(TrackIdx t);

        // Mutators
        void SetPartSignature(PartIdx p, TimeSignature ts, int measures);
        void SetPartTonality(PartIdx p, Tonality tonality, NoteName root);
        void SetTrackRole(PartIdx p, TrackIdx t, TrackRole role);
        void SetMelodicInstrument(PartIdx p, TrackIdx t, MIDIInstrumentSO inst);
        void SetPercInstrument(PartIdx p, TrackIdx t, MIDIPercussionInstrumentSO inst);
        void SetTrackPattern(PartIdx p, TrackIdx t, PatternDataSO patternAssetOrRuntime);

        // Structure
        bool TrySetStructureFromString(string input, out List<string> warnings);
        string SerializeStructure();

        // Replace entire runtime song (used by Load/Reset flows)
        void ReplaceSong(SongConfig newSong);

        // Events
        event EventHandler? SongReplaced;
        event EventHandler<PartChangedEventArgs>? PartAdded;
        event EventHandler<PartChangedEventArgs>? PartRemoved;
        event EventHandler<PartChangedEventArgs>? PartUpdated;
        event EventHandler<PartChangedEventArgs>? ActivePartChanged;
        event EventHandler<TrackChangedEventArgs>? TrackAdded;
        event EventHandler<TrackChangedEventArgs>? TrackRemoved;
        event EventHandler<TrackChangedEventArgs>? ActiveTrackChanged;
        event EventHandler<TrackChangedEventArgs>? TrackUpdated;
        event EventHandler? StructureChanged;
    }

    public sealed class SongConfigManager : ISongConfigManager
    {
        private readonly IInstrumentRepository _instruments;
        private readonly IPatternRepository _patterns;
        private readonly ISequenceSerializer _seq;
        private readonly ISongConfigStore _store;

        public SongConfig Song { get; private set; }
        public PartIdx ActivePart { get; private set; } = new PartIdx(-1);
        public TrackIdx ActiveTrack { get; private set; } = new TrackIdx(-1);

        public SongConfigManager(
            MidiGenPlayConfig cfg,
            IInstrumentRepository instruments,
            IPatternRepository patterns,
            ISequenceSerializer seq,
            ISongConfigStore store)
        {
            _instruments = instruments;
            _patterns = patterns;
            _seq = seq;
            _store = store;
            Song = new SongConfig { Parts = new(), Structure = new() };
        }

        // ----- Selection
        public void SelectPart(PartIdx p)
        {
            if (p.Value < 0 || p.Value >= (Song.Parts?.Count ?? 0)) return;
            ActivePart = p;
            ActiveTrack = new TrackIdx(-1);
            ActivePartChanged?.Invoke(this, new PartChangedEventArgs(p));
        }
        public void SelectTrack(TrackIdx t)
        {
            if (!HasActivePart()) return;
            var tracks = Song.Parts[ActivePart.Value].Tracks;
            if (t.Value < 0 || t.Value >= (tracks?.Count ?? 0)) return;
            ActiveTrack = t;
            ActiveTrackChanged?.Invoke(this, new TrackChangedEventArgs(ActivePart, t));
        }

        // ----- Parts
        public PartIdx AddPart(SongConfig.PartConfig template = null)
        {
            Song.Parts ??= new List<SongConfig.PartConfig>();
            var p = template ?? new SongConfig.PartConfig
            {
                Name = $"Part {Song.Parts.Count + 1}",
                Tonality = Tonality.Ionian,
                RootNote = NoteName.C,
                TempoRange = TempoRange.Fast,
                TimeSignature = TimeSignature.FourFour,
                Measures = 4,
                Tracks = new List<SongConfig.PartConfig.TrackConfig>()
            };
            Song.Parts.Add(p);
            var idx = new PartIdx(Song.Parts.Count - 1);
            PartAdded?.Invoke(this, new PartChangedEventArgs(idx));
            SelectPart(idx);
            return idx;
        }

        public void RemovePart(PartIdx p)
        {
            if (Song.Parts == null || Song.Parts.Count <= 1) return; // keep at least one
            if (p.Value < 0 || p.Value >= Song.Parts.Count) return;
            Song.Parts.RemoveAt(p.Value);
            PartRemoved?.Invoke(this, new PartChangedEventArgs(p));
            var next = new PartIdx(Mathf.Clamp(ActivePart.Value, 0, Song.Parts.Count - 1));
            SelectPart(next);
        }

        // ----- Tracks
        public TrackIdx AddTrack(TrackRole defaultRole = TrackRole.Backing)
        {
            if (!HasActivePart()) return new TrackIdx(-1);
            var part = Song.Parts[ActivePart.Value];
            part.Tracks ??= new List<SongConfig.PartConfig.TrackConfig>();

            var defaultMelodic = _instruments.GetMelodicInstruments().FirstOrDefault();
            var defaultPerc = _instruments.GetPercussionInstruments().FirstOrDefault();

            var tcfg = new SongConfig.PartConfig.TrackConfig
            {
                Role = defaultRole,
                Instrument = defaultMelodic,
                PercussionInstrument = defaultPerc,
                Parameters = new TrackParameters()
            };
            part.Tracks.Add(tcfg);
            var tIdx = new TrackIdx(part.Tracks.Count - 1);
            TrackAdded?.Invoke(this, new TrackChangedEventArgs(ActivePart, tIdx));
            SelectTrack(tIdx);
            return tIdx;
        }

        public void RemoveTrack(TrackIdx t)
        {
            if (!HasActivePart()) return;
            var tracks = Song.Parts[ActivePart.Value].Tracks;
            if (tracks == null || t.Value < 0 || t.Value >= tracks.Count) return;
            tracks.RemoveAt(t.Value);
            TrackRemoved?.Invoke(this, new TrackChangedEventArgs(ActivePart, t));
            var next = (tracks.Count == 0) ? new TrackIdx(-1) : new TrackIdx(Mathf.Clamp(t.Value, 0, tracks.Count - 1));
            if (next.Value >= 0) SelectTrack(next); else ActiveTrack = new TrackIdx(-1);
        }

        // ----- Mutators (raise TrackUpdated after change)
        public void SetPartSignature(PartIdx p, TimeSignature ts, int measures)
        {
            if (!IsValidPart(p)) return;
            var pc = Song.Parts[p.Value];
            pc.TimeSignature = ts; pc.Measures = measures;
            PartUpdated?.Invoke(this, new PartChangedEventArgs(p));
        }
        public void SetPartTonality(PartIdx p, Tonality tonality, NoteName root)
        {
            if (!IsValidPart(p)) return;
            var pc = Song.Parts[p.Value];
            pc.Tonality = tonality; pc.RootNote = root;
            PartUpdated?.Invoke(this, new PartChangedEventArgs(p));
        }

        public void SetTrackRole(PartIdx p, TrackIdx t, TrackRole role)
        {
            if (!IsValidTrack(p, t)) return;
            var tc = Song.Parts[p.Value].Tracks[t.Value];
            tc.Role = role;
            TrackUpdated?.Invoke(this, new TrackChangedEventArgs(p, t));
        }
        public void SetMelodicInstrument(PartIdx p, TrackIdx t, MIDIInstrumentSO inst)
        {
            if (!IsValidTrack(p, t)) return;
            Song.Parts[p.Value].Tracks[t.Value].Instrument = inst;
            TrackUpdated?.Invoke(this, new TrackChangedEventArgs(p, t));
        }
        public void SetPercInstrument(PartIdx p, TrackIdx t, MIDIPercussionInstrumentSO inst)
        {
            if (!IsValidTrack(p, t)) return;
            Song.Parts[p.Value].Tracks[t.Value].PercussionInstrument = inst;
            TrackUpdated?.Invoke(this, new TrackChangedEventArgs(p, t));
        }
        public void SetTrackPattern(PartIdx p, TrackIdx t, PatternDataSO pattern)
        {
            if (!IsValidTrack(p, t)) return;
            Song.Parts[p.Value].Tracks[t.Value].Parameters ??= new TrackParameters();
            Song.Parts[p.Value].Tracks[t.Value].Parameters.Pattern = pattern;
            TrackUpdated?.Invoke(this, new TrackChangedEventArgs(p, t));
        }

        // ----- Structure
        public bool TrySetStructureFromString(string input, out List<string> warnings)
        {
            warnings = new List<string>();
            if (_seq.TryParse(input, Song.Parts?.Count ?? 0, out var parsed, out var warns))
            {
                Song.Structure = parsed;
                if (warns != null) warnings.AddRange(warns);
                StructureChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            Song.Structure = new List<SongConfig.PartSequenceEntry>();
            if (warns != null) warnings.AddRange(warns);
            StructureChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
        public string SerializeStructure() => _seq.Serialize(Song.Structure);

        // ----- Helpers & extra events
        public event EventHandler? SongReplaced;
        public event EventHandler<PartChangedEventArgs>? PartAdded;
        public event EventHandler<PartChangedEventArgs>? PartRemoved;
        public event EventHandler<PartChangedEventArgs>? PartUpdated;
        public event EventHandler<PartChangedEventArgs>? ActivePartChanged;
        public event EventHandler<TrackChangedEventArgs>? TrackAdded;
        public event EventHandler<TrackChangedEventArgs>? TrackRemoved;
        public event EventHandler<TrackChangedEventArgs>? TrackUpdated;
        public event EventHandler<TrackChangedEventArgs>? ActiveTrackChanged;
        public event EventHandler? StructureChanged;

        private bool HasActivePart() => ActivePart.Value >= 0 && ActivePart.Value < (Song.Parts?.Count ?? 0);
        private bool IsValidPart(PartIdx p) => p.Value >= 0 && p.Value < (Song.Parts?.Count ?? 0);
        private bool IsValidTrack(PartIdx p, TrackIdx t)
            => IsValidPart(p) && Song.Parts[p.Value].Tracks != null &&
               t.Value >= 0 && t.Value < Song.Parts[p.Value].Tracks.Count;

        public void ReplaceSong(SongConfig newSong)
        {
            Song = newSong ?? 
                new SongConfig { 
                    Parts = new List<SongConfig.PartConfig>(), 
                    Structure = new List<SongConfig.PartSequenceEntry>()
                };

            ActivePart = new PartIdx(Song.Parts != null && Song.Parts.Count > 0 ? 0 : -1);
            ActiveTrack = new TrackIdx(-1);
            SongReplaced?.Invoke(this, EventArgs.Empty);
            if (ActivePart.Value >= 0)
                ActivePartChanged?.Invoke(this, new PartChangedEventArgs(ActivePart));
        }
    }
}
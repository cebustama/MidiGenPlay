using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    /// <summary>
    /// Prepend a short intro part before the first structured part.
    /// Minimal MVP: one track only (leader), copies musical setup from first part.
    /// </summary>
    public sealed class IntroMutator : IArrangementMutator
    {
        public enum IntroStyle { CountIn, Riff, Pad }

        public string Name => $"Intro({style},{measures}m,{musicianId})";
        public int Order { get; }

        private readonly string musicianId;
        private readonly int measures;
        private readonly IntroStyle style;

        public IntroMutator(string musicianId, int measures,
                            IntroStyle style,
                            int order = -100) // run very early
        {
            this.musicianId = musicianId ?? "";
            this.measures = Mathf.Max(1, measures);
            this.style = style;
            this.Order = order;
        }

        public SongConfig Mutate(SongConfig cfg, IArrangementContext ctx)
        {
            if (cfg == null || cfg.Parts == null || cfg.Parts.Count == 0 ||
                cfg.Structure == null || cfg.Structure.Count == 0)
                return cfg;

            // Find the first scheduled part as a musical reference.
            var firstEntry = cfg.Structure[0];
            var refPartIndex = Mathf.Clamp(firstEntry.PartIndex, 0, cfg.Parts.Count - 1);
            var refPart = cfg.Parts[refPartIndex];

            // Pick the leader track.
            SongConfig.PartConfig.TrackConfig leader = null;
            if (style == IntroStyle.CountIn)
            {
                leader = refPart.Tracks?.FirstOrDefault(t => t.Role == TrackRole.Rhythm)
                      ?? refPart.Tracks?.FirstOrDefault(t => t.MusicianId == musicianId)
                      ?? refPart.Tracks?.FirstOrDefault();
            }
            else // Riff / Pad
            {
                leader = refPart.Tracks?.FirstOrDefault(t => t.MusicianId == musicianId)
                      ?? refPart.Tracks?.FirstOrDefault(t => t.Role == TrackRole.Lead)
                      ?? refPart.Tracks?.FirstOrDefault();
            }

            if (leader == null) return cfg; // nothing we can do deterministically

            // Clone a minimal track for the intro (shallow + new params instance).
            var introTrack = new SongConfig.PartConfig.TrackConfig
            {
                Role = leader.Role,
                Instrument = leader.Instrument,
                PercussionInstrument = leader.PercussionInstrument,
                MusicianId = leader.MusicianId,
                Parameters = new TrackParameters { Pattern = leader.Parameters?.Pattern }
            };

            // Build the new intro part (copy musical setup from ref part).
            var introPart = new SongConfig.PartConfig
            {
                Name = "Intro",
                Tonality = refPart.Tonality,
                RootNote = refPart.RootNote,
                TempoRange = refPart.TempoRange,
                TimeSignature = refPart.TimeSignature,
                Measures = this.measures,
                Tracks = new List<SongConfig.PartConfig.TrackConfig> { introTrack }
            };

            // Append to Parts; insert at the beginning of Structure.
            int newPartIndex = cfg.Parts.Count;
            cfg.Parts.Add(introPart);
            cfg.Structure.Insert(0, new SongConfig.PartSequenceEntry { PartIndex = newPartIndex, RepeatCount = 1 });

            ctx?.Log($"IntroMutator: +Part idx={newPartIndex} style={style} measures={measures} leader={introTrack.MusicianId} role={introTrack.Role}");
            return cfg;
        }
    }
}

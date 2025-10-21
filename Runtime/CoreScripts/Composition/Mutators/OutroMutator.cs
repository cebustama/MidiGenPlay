using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    /// <summary>
    /// Append a short outro part after the last structured part.
    /// Minimal MVP: one track only (leader), copies musical setup from last part.
    /// Uses the same style enum as IntroMutator for parity.
    /// </summary>
    public sealed class OutroMutator : IArrangementMutator
    {
        public string Name => $"Outro({style},{measures}m,{musicianId})";
        public int Order { get; }

        private readonly string musicianId;
        private readonly int measures;
        private readonly IntroMutator.IntroStyle style;

        public OutroMutator(string musicianId, int measures,
                            IntroMutator.IntroStyle style,
                            int order = 100) // run late so it lands at the very end
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

            // Use the last scheduled part as the musical reference.
            var lastEntry = cfg.Structure[cfg.Structure.Count - 1];
            var refPartIndex = Mathf.Clamp(lastEntry.PartIndex, 0, cfg.Parts.Count - 1);
            var refPart = cfg.Parts[refPartIndex];

            // Pick the leader track (mirror Intro logic).
            SongConfig.PartConfig.TrackConfig leader = null;
            if (style == IntroMutator.IntroStyle.CountIn)
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

            if (leader == null) return cfg; // deterministic no-op if nothing to lead

            // Clone a minimal track for the outro (shallow + new params instance).
            var outroTrack = new SongConfig.PartConfig.TrackConfig
            {
                Role = leader.Role,
                Instrument = leader.Instrument,
                PercussionInstrument = leader.PercussionInstrument,
                MusicianId = leader.MusicianId,
                Parameters = new TrackParameters { Pattern = leader.Parameters?.Pattern }
            };

            // Build the new outro part (copy musical setup from ref part).
            var outroPart = new SongConfig.PartConfig
            {
                Name = "Outro",
                Tonality = refPart.Tonality,
                RootNote = refPart.RootNote,
                TempoRange = refPart.TempoRange,
                TimeSignature = refPart.TimeSignature,
                Measures = this.measures,
                Tracks = new List<SongConfig.PartConfig.TrackConfig> { outroTrack }
            };

            // Append to Parts; add at the end of Structure.
            int newPartIndex = cfg.Parts.Count;
            cfg.Parts.Add(outroPart);
            cfg.Structure.Add(new SongConfig.PartSequenceEntry { PartIndex = newPartIndex, RepeatCount = 1 });

            ctx?.Log($"OutroMutator: +Part idx={newPartIndex} style={style} measures={measures} leader={outroTrack.MusicianId} role={outroTrack.Role}");
            return cfg;
        }
    }
}

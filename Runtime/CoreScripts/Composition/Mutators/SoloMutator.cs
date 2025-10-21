using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Append a SOLO part at the end of the structure.
    /// MVP behavior:
    /// - Reuses musical setup (key/tempo/TS) from the last scheduled part.
    /// - Keeps light accompaniment (Rhythm + Backing/Harmony + Bass if present).
    /// - Promotes the chosen musician as the soloist (Lead).
    /// - Deterministic given the same inputs.
    /// </summary>
    public sealed class SoloMutator : IArrangementMutator
    {
        public enum SoloStyle { Emotional, Virtuoso, Facemelter }

        public string Name => $"Solo({style},{measures}m,{musicianId})";
        public int Order { get; } // runs after intros by default

        private readonly string musicianId;
        private readonly int measures;
        private readonly SoloStyle style;

        public SoloMutator(string musicianId, int measures, SoloStyle style, int order = 0)
        {
            this.musicianId = musicianId ?? "";
            this.measures = Mathf.Max(1, measures);
            this.style = style;
            Order = order;
        }

        public SongConfig Mutate(SongConfig cfg, IArrangementContext ctx)
        {
            if (cfg == null || cfg.Parts == null || cfg.Parts.Count == 0 ||
                cfg.Structure == null || cfg.Structure.Count == 0)
                return cfg;

            // Reference: last scheduled entry → its PartConfig
            var lastEntry = cfg.Structure[cfg.Structure.Count - 1];
            var refPartIndex = Mathf.Clamp(lastEntry.PartIndex, 0, cfg.Parts.Count - 1);
            var refPart = cfg.Parts[refPartIndex];
            if (refPart?.Tracks == null || refPart.Tracks.Count == 0) return cfg;

            // Pick/prepare soloist
            var soloSrc = refPart.Tracks.FirstOrDefault(t => t.MusicianId == musicianId)
                       ?? refPart.Tracks.FirstOrDefault(t => t.Role == TrackRole.Lead)
                       ?? refPart.Tracks.FirstOrDefault();

            if (soloSrc == null) return cfg;

            var soloTrack = CloneTrack(soloSrc);
            soloTrack.Role = TrackRole.Lead;            // ensure melodic composer
            soloTrack.MusicianId = string.IsNullOrEmpty(musicianId) ? soloSrc.MusicianId : musicianId;

            // Support band: keep it light so the solo cuts through.
            var tracks = new List<SongConfig.PartConfig.TrackConfig>();

            var rhythm = refPart.Tracks.FirstOrDefault(t => t.Role == TrackRole.Rhythm);
            if (rhythm != null) tracks.Add(CloneTrack(rhythm));

            var backing = refPart.Tracks.FirstOrDefault(t => t.Role == TrackRole.Backing)
                       ?? refPart.Tracks.FirstOrDefault(t => t.Role == TrackRole.Harmony);
            if (backing != null) tracks.Add(CloneTrack(backing));

            var bass = refPart.Tracks.FirstOrDefault(t => t.Role == TrackRole.Bassline);
            if (bass != null) tracks.Add(CloneTrack(bass));

            // Put solo last for readability (no functional difference)
            tracks.Add(soloTrack);

            // Build SOLO part with same musical setup as reference
            var soloPart = new SongConfig.PartConfig
            {
                Name = $"Solo",
                Tonality = refPart.Tonality,
                RootNote = refPart.RootNote,
                TempoRange = refPart.TempoRange,
                TimeSignature = refPart.TimeSignature,
                Measures = this.measures,
                Tracks = tracks
            };

            int newIndex = cfg.Parts.Count;
            cfg.Parts.Add(soloPart);
            cfg.Structure.Add(new SongConfig.PartSequenceEntry { PartIndex = newIndex, RepeatCount = 1 });

            ctx?.Log($"SoloMutator: +Part idx={newIndex} style={style} measures={measures} soloist={soloTrack.MusicianId}");
            return cfg;
        }

        private static SongConfig.PartConfig.TrackConfig CloneTrack(SongConfig.PartConfig.TrackConfig src)
        {
            return new SongConfig.PartConfig.TrackConfig
            {
                Role = src.Role,
                Instrument = src.Instrument,
                PercussionInstrument = src.PercussionInstrument,
                MusicianId = src.MusicianId,
                Parameters = new TrackParameters
                {
                    // Keep original pattern (MVP). Composers can still vary output per-part.
                    Pattern = src.Parameters?.Pattern
                }
            };
        }
    }
}

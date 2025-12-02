using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    /// <summary>
    /// Re-composes one musician's track by injecting a small pattern variant.
    /// - partIndexOrAll: -1 => apply to all parts, else just that part index.
    /// - strategyId (optional): hint for variant type ("rotate2", "rotate3", "busier", "sparser").
    /// </summary>
    public sealed class AlternateTrackMutator : IArrangementMutator
    {
        private readonly string musicianId;
        private readonly int partIndexOrAll;
        private readonly string strategyId;

        public string Name => $"AlternateTrack({musicianId},{partIndexOrAll},{strategyId ?? "none"})";
        public int Order { get; }

        public AlternateTrackMutator(
            string musicianId, int partIndexOrAll, string strategyId = null, int order = 0)
        {
            this.musicianId = musicianId;
            this.partIndexOrAll = partIndexOrAll;
            this.strategyId = strategyId;
            Order = order;
        }

        public SongConfig Mutate(SongConfig cfg, IArrangementContext ctx)
        {
            if (cfg == null || cfg.Parts == null || cfg.Parts.Count == 0 
                || string.IsNullOrEmpty(musicianId))
                return cfg;

            var rng = ctx?.Rng ?? new System.Random();

            int start = 0, end = cfg.Parts.Count - 1;
            if (partIndexOrAll >= 0)
            {
                start = Mathf.Clamp(partIndexOrAll, 0, cfg.Parts.Count - 1);
                end = start;
            }

            for (int i = start; i <= end; i++)
            {
                var part = cfg.Parts[i];
                if (part?.Tracks == null) continue;

                for (int t = 0; t < part.Tracks.Count; t++)
                {
                    var tr = part.Tracks[t];
                    if (!string.Equals(tr?.MusicianId, musicianId, StringComparison.Ordinal))
                        continue;

                    var clone = CloneTrack(tr);
                    clone.Parameters = CloneParams(tr.Parameters);

                    // Mutate only this track's pattern; keep instrument bindings intact.
                    if (clone.Parameters?.Pattern != null)
                        clone.Parameters.Pattern = 
                            MutatePattern(clone.Parameters.Pattern, part, rng, strategyId);

                    part.Tracks[t] = clone;
                }
            }

            ctx?.Log($"[AlternateTrackMutator] Applied to '{musicianId}' part={partIndexOrAll} " +
                $"strategy='{strategyId ?? "none"}'.");
            return cfg;
        }

        private static SongConfig.PartConfig.TrackConfig CloneTrack(
            SongConfig.PartConfig.TrackConfig src)
        {
            if (src == null) return null;
            return new SongConfig.PartConfig.TrackConfig
            {
                Role = src.Role,
                Instrument = src.Instrument,
                PercussionInstrument = src.PercussionInstrument,
                MusicianId = src.MusicianId,
                Parameters = CloneParams(src.Parameters)
            };
        }

        private static TrackParameters CloneParams(TrackParameters p)
        {
            if (p == null) return null;
            return new TrackParameters
            {
                Pattern = p.Pattern // we’ll replace when variant is built
            };
        }

        private static PatternDataSO MutatePattern(
            PatternDataSO original, SongConfig.PartConfig part, System.Random rng, string strategyId)
        {
            switch (original)
            {
                case DrumPatternData d:
                    var dClone = d.DeepCloneRuntime();
                    MutateDrumGridInPlace(dClone, rng, strategyId);
                    dClone.DisplayName = SafeName(d.DisplayName) + " (Alt)";
                    return dClone;

                case ChordProgressionData c:
                    var cClone = CloneChordProgression(c);
                    RotateChordProgressionInPlace(cClone, part, strategyId);
                    cClone.DisplayName = SafeName(c.DisplayName) + " (Alt)";
                    return cClone;

                default:
                    return original; // unknown type → no-op
            }
        }

        private static void MutateDrumGridInPlace(
            DrumPatternData p, System.Random rng, string strategyId)
        {
            if (p == null) return;
            p.InitializeIfEmpty();

            // 1) Rotate by one beat (>= 1 subdivision)
            int shift = Math.Max(1, p.subdivisions);
            if (string.Equals(strategyId, "rotate2", StringComparison.OrdinalIgnoreCase)) shift *= 2;
            if (string.Equals(strategyId, "rotate3", StringComparison.OrdinalIgnoreCase)) shift *= 3;

            foreach (var l in p.lanes)
            {
                var src = l.steps;
                var rotated = new bool[src.Count];
                for (int i = 0; i < src.Count; i++)
                    rotated[(i + shift) % src.Count] = src[i];
                l.steps = rotated.ToList();
            }

            // 2) Micro-variation off the strong beats
            float addProb = 0.06f, removeProb = 0.06f;
            if (string.Equals(strategyId, "busier", StringComparison.OrdinalIgnoreCase)) 
            { addProb = 0.12f; removeProb = 0.04f; }
            if (string.Equals(strategyId, "sparser", StringComparison.OrdinalIgnoreCase)) 
            { addProb = 0.03f; removeProb = 0.12f; }

            foreach (var l in p.lanes)
            {
                for (int i = 0; i < l.steps.Count; i++)
                {
                    bool strongBeat = (i % (p.subdivisions * p.beatsPerMeasure)) % p.subdivisions == 0;
                    if (l.steps[i])
                    {
                        if (!strongBeat && rng.NextDouble() < removeProb) l.steps[i] = false;
                    }
                    else
                    {
                        if (rng.NextDouble() < addProb) l.steps[i] = true;
                    }
                }
            }
        }

        private static ChordProgressionData CloneChordProgression(ChordProgressionData src)
        {
            var clone = ScriptableObject.CreateInstance<ChordProgressionData>();
            clone.name = src.name + " (Runtime)";
            clone.DisplayName = src.DisplayName;
            clone.Measures = src.Measures;
            clone.subdivisions = src.subdivisions;
            clone.tonalities = src.tonalities != null ? 
                new List<Tonality>(src.tonalities) : 
                new List<Tonality>();
            clone.events = new List<ChordProgressionData.ChordEvent>(src.events?.Count ?? 0);

            if (src.events != null)
            {
                foreach (var e in src.events)
                {
                    clone.events.Add(new ChordProgressionData.ChordEvent
                    {
                        startStep = e.startStep,
                        lengthSteps = e.lengthSteps,
                        degree = e.degree,
                        quality = e.quality,
                        velocity = e.velocity
                    });
                }
            }
            return clone;
        }

        private static void RotateChordProgressionInPlace(
            ChordProgressionData p, SongConfig.PartConfig part, string strategyId)
        {
            if (p == null || p.events == null || p.events.Count == 0) return;

            // one-beat rotation in steps (uses time signature for beats/measure)
            var tsInfo = TimeSignatureProperties[part.TimeSignature];
            int stepsPerBeat = Math.Max(1, p.subdivisions);
            int stepsPerMeasure = stepsPerBeat * tsInfo.BeatsPerMeasure;
            int totalSteps = Math.Max(stepsPerMeasure, p.Measures * stepsPerMeasure);

            int shift = stepsPerBeat; // rotate by one beat by default
            if (string.Equals(strategyId, "rotate2", StringComparison.OrdinalIgnoreCase)) shift *= 2;
            if (string.Equals(strategyId, "rotate3", StringComparison.OrdinalIgnoreCase)) shift *= 3;

            foreach (var e in p.events)
                e.startStep = (e.startStep + shift) % totalSteps;
        }

        private static string SafeName(string s) => string.IsNullOrEmpty(s) ? "Pattern" : s;
    }
}

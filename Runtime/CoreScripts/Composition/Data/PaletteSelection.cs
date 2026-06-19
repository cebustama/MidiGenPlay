using System.Collections.Generic;
using UnityEngine;
using MidiGenPlay;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Domain-neutral summary of a palette candidate's metric shape. This is the
    /// only thing <see cref="PaletteSelector"/> sees, which keeps the selector free
    /// of any concrete asset type and therefore unit-testable with synthetic values.
    /// Each track domain (chords, drums, and later melody/harmony) maps its own data
    /// type onto this struct.
    /// </summary>
    public readonly struct TsFeatures
    {
        public readonly TimeSignature TimeSignature;
        public readonly int Subdivisions;

        /// <summary>
        /// Structural events per bar. For chords this is harmonic rhythm
        /// (chord changes/bar); for drums it is the capped foundational-onset
        /// density (see <see cref="PaletteSelector.DrumStartsPerBar"/>).
        /// </summary>
        public readonly float StartsPerBar;

        public TsFeatures(TimeSignature timeSignature, int subdivisions, float startsPerBar)
        {
            TimeSignature = timeSignature;
            Subdivisions = subdivisions;
            StartsPerBar = startsPerBar;
        }
    }

    /// <summary>A weighted palette candidate plus its precomputed metric features.</summary>
    public readonly struct Candidate<T>
    {
        public readonly T Item;
        public readonly float Weight;
        public readonly TsFeatures Features;

        public Candidate(T item, float weight, TsFeatures features)
        {
            Item = item;
            Weight = weight;
            Features = features;
        }
    }

    /// <summary>
    /// Shared, deterministic palette selector extracted from BackingCardConfigSO
    /// (CE-F1). One home for the time-signature-aware selection policy used by every
    /// palette-backed card:
    ///   Tier A: exact TS match (only when the palette opts in).
    ///   Tier B: ranked fallback heuristic (bar length, beat unit, parity,
    ///           numerator closeness, subdivisions, density vs grouping).
    ///   Tier C: raw weights if every heuristic score collapses (defensive; with
    ///           positive weights and multipliers this branch is unreachable).
    /// Then a single weighted roulette draw within the chosen tier.
    ///
    /// Determinism invariant: exactly one <c>rng.NextDouble()</c> is consumed per
    /// <see cref="Pick{T}"/> call, so the same seed and the same candidate list
    /// always produce the same pick. The selector never clones or mutates assets;
    /// callers clone the returned reference.
    /// </summary>
    public static class PaletteSelector
    {
        /// <summary>
        /// Pick one candidate using the Tier A/B/C policy. Returns the chosen item
        /// (not cloned) or null if there are no usable candidates.
        /// </summary>
        public static T Pick<T>(
            IReadOnlyList<Candidate<T>> candidates,
            TimeSignature desiredTs,
            bool preferExactTs,
            int minHarmonicSubdivisions,
            System.Random rng,
            bool verbose = false,
            string label = "PICK",
            string paletteName = null) where T : class
        {
            rng ??= new System.Random();

            // Sanitize: drop null items, floor weights to a tiny positive value.
            var list = new List<Candidate<T>>(candidates?.Count ?? 0);
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (c.Item != null)
                        list.Add(new Candidate<T>(c.Item, Mathf.Max(0.0001f, c.Weight), c.Features));
                }
            }

            if (list.Count == 0)
                return null;

            // Tier A: exact TS (optional).
            if (preferExactTs)
            {
                var exact = new List<(T item, float weight)>();
                foreach (var c in list)
                    if (c.Features.TimeSignature == desiredTs)
                        exact.Add((c.Item, c.Weight));

                if (exact.Count > 0)
                {
                    var picked = Roulette(exact, rng);
                    if (verbose)
                        Debug.Log($"[PaletteSelector] {label} source=palette tier=A(exactTS) " +
                                  $"preferExactTs=True palette='{paletteName}' desiredTS={desiredTs} " +
                                  $"picked='{Describe(picked)}'.");
                    return picked;
                }
            }
            else if (verbose)
            {
                Debug.Log($"[PaletteSelector] {label} source=palette tier=A skipped " +
                          $"preferExactTs=False palette='{paletteName}' desiredTS={desiredTs}.");
            }

            // Tier B: ranked fallback heuristic.
            var scored = new List<(T item, float weight)>(list.Count);
            float total = 0f;
            foreach (var c in list)
            {
                float mult = ComputeTsHeuristicMultiplier(c.Features, desiredTs, minHarmonicSubdivisions);
                float s = c.Weight * mult;
                scored.Add((c.Item, s));
                total += s;
            }

            // Tier C: raw weights if all heuristic scores collapsed (defensive).
            if (total <= 0f)
            {
                var raw = new List<(T item, float weight)>(list.Count);
                foreach (var c in list) raw.Add((c.Item, c.Weight));
                var picked = Roulette(raw, rng);
                if (verbose)
                    Debug.Log($"[PaletteSelector] {label} source=palette tier=C(rawWeights) " +
                              $"palette='{paletteName}' desiredTS={desiredTs} picked='{Describe(picked)}'.");
                return picked;
            }

            var pickedB = Roulette(scored, rng);
            if (verbose)
                Debug.Log($"[PaletteSelector] {label} source=palette tier=B(fallbackTS) " +
                          $"palette='{paletteName}' desiredTS={desiredTs} picked='{Describe(pickedB)}'.");
            return pickedB;
        }

        /// <summary>
        /// Tier B fitness multiplier for a candidate against the desired TS. Ported
        /// verbatim from the pre-CE-F1 BackingCardConfigSO heuristic (B1–B6), with
        /// the only change being that the subdivisions floor is passed as a plain
        /// int instead of read off a MidiGenPlayConfig.
        /// </summary>
        public static float ComputeTsHeuristicMultiplier(
            TsFeatures cand, TimeSignature desiredTs, int minHarmonicSubdivisions)
        {
            // Default to mild preference if props missing.
            if (!TimeSignatureProperties.TryGetValue(cand.TimeSignature, out var src) ||
                !TimeSignatureProperties.TryGetValue(desiredTs, out var dst))
                return 1f;

            float srcBarQ = src.BeatsPerMeasure * (4f / src.BeatUnit);
            float dstBarQ = dst.BeatsPerMeasure * (4f / dst.BeatUnit);
            float barDiff = Mathf.Abs(srcBarQ - dstBarQ);

            float m = 1f;

            // B1) Bar-length equivalence (strong).
            if (barDiff < 0.001f) m *= 4.0f;
            else m *= 1f / (1f + barDiff);

            // B2) Same beat-unit (medium).
            if (src.BeatUnit == dst.BeatUnit) m *= 1.25f;

            // B3) Parity (mild).
            if ((src.BeatsPerMeasure & 1) == (dst.BeatsPerMeasure & 1)) m *= 1.10f;

            // B4) Numerator closeness (mild).
            m *= 1f / (1f + Mathf.Abs(src.BeatsPerMeasure - dst.BeatsPerMeasure) * 0.10f);

            // B5) Subdivisions (mild).
            int minSub = Mathf.Max(1, minHarmonicSubdivisions);
            int sub = Mathf.Max(1, cand.Subdivisions);
            if (sub >= minSub) m *= 1.05f;
            else m *= 0.95f;

            // B6) Density vs grouping count (mild, useful for 5/4 3+2 etc).
            int groupCount = DefaultGroupingCount(desiredTs);
            float startsPerBar = cand.StartsPerBar;
            m *= 1f / (1f + Mathf.Abs(startsPerBar - groupCount) * 0.25f);

            return m;
        }

        /// <summary>Natural metric grouping count per bar for a meter (e.g. 5/4 = 3+2 = 2).</summary>
        public static int DefaultGroupingCount(TimeSignature ts) => ts switch
        {
            TimeSignature.FourFour => 2,      // 2+2
            TimeSignature.ThreeFour => 1,     // [3]
            TimeSignature.TwoFour => 1,       // [2]
            TimeSignature.SixEight => 2,      // 3+3
            TimeSignature.NineEight => 3,     // 3+3+3
            TimeSignature.TwelveEight => 5,   // 3+3+2+2+2 (flamenco-ish)
            TimeSignature.FiveFour => 2,      // 3+2
            TimeSignature.SevenEight => 3,    // 2+2+3
            _ => 1
        };

        /// <summary>
        /// Chord harmonic rhythm for B6: max(1, eventCount) / max(1, measures).
        /// Matches the pre-CE-F1 EstimateChordStartsPerBar exactly.
        /// </summary>
        public static float StartsPerBar(int eventCount, int measures)
        {
            int bars = Mathf.Max(1, measures);
            int starts = Mathf.Max(1, eventCount);
            return starts / (float)bars;
        }

        /// <summary>
        /// Drum density for B6: capped foundational-onset density. <paramref name="foundationOnsets"/>
        /// is the kick (or fallback foundation lane) onset count across the whole
        /// pattern. Returns <paramref name="groupCount"/> (neutral — no B6 penalty)
        /// when there are no foundation onsets; otherwise min(onsets/bar, groupCount),
        /// so busy grooves are never penalized and only under-articulation of the
        /// meter's groups is.
        /// </summary>
        public static float DrumStartsPerBar(int foundationOnsets, int measures, int groupCount)
        {
            if (groupCount < 1) groupCount = 1;
            if (foundationOnsets <= 0) return groupCount; // neutral
            int bars = Mathf.Max(1, measures);
            return Mathf.Min(foundationOnsets / (float)bars, groupCount);
        }

        // Single-draw weighted roulette. Consumes exactly one rng.NextDouble().
        private static T Roulette<T>(IReadOnlyList<(T item, float weight)> list, System.Random rng)
            where T : class
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += Mathf.Max(0.0001f, list[i].weight);

            float pick = (float)rng.NextDouble() * total;
            for (int i = 0; i < list.Count; i++)
            {
                float w = Mathf.Max(0.0001f, list[i].weight);
                if (pick <= w) return list[i].item;
                pick -= w;
            }
            return list[list.Count - 1].item;
        }

        private static string Describe(object o)
        {
            if (o is UnityEngine.Object uo) return uo != null ? uo.name : "null";
            return o?.ToString() ?? "null";
        }
    }

    /// <summary>
    /// Typed chord-palette finder: maps a <see cref="ChordProgressionPaletteSO"/> to
    /// <see cref="Candidate{T}"/> values and delegates to <see cref="PaletteSelector"/>.
    /// Returns the chosen palette progression (NOT cloned — the caller clones).
    /// </summary>
    public static class ProgressionFinder
    {
        public static ChordProgressionData Pick(
            ChordProgressionPaletteSO palette,
            TimeSignature desiredTs,
            int minHarmonicSubdivisions,
            System.Random rng,
            bool verbose = false)
        {
            if (palette == null || palette.entries == null || palette.entries.Count == 0)
                return null;

            var candidates = new List<Candidate<ChordProgressionData>>(palette.entries.Count);
            foreach (var e in palette.entries)
            {
                if (e == null || e.progression == null) continue;
                candidates.Add(new Candidate<ChordProgressionData>(
                    e.progression, e.weight, FeaturesFor(e.progression)));
            }

            return PaletteSelector.Pick(
                candidates, desiredTs, palette.preferExactTsMatches,
                minHarmonicSubdivisions, rng, verbose, "PROG_PICK", palette.GetDisplayName());
        }

        public static TsFeatures FeaturesFor(ChordProgressionData p)
        {
            int events = (p != null && p.events != null) ? p.events.Count : 0;
            int measures = p != null ? p.Measures : 1;
            int subs = p != null ? p.subdivisions : 1;
            var ts = p != null ? p.TimeSignature : default;
            return new TsFeatures(ts, subs, PaletteSelector.StartsPerBar(events, measures));
        }
    }

    /// <summary>
    /// Typed drum-palette finder: maps a <see cref="DrumPatternPaletteSO"/> to
    /// <see cref="Candidate{T}"/> values and delegates to <see cref="PaletteSelector"/>.
    /// Returns the chosen palette pattern (NOT cloned — the caller clones). This is
    /// what gives the rhythm side the TS-awareness the chord side already had.
    /// </summary>
    public static class PatternFinder
    {
        // GM percussion note numbers for the kick (foundation voice).
        private const int AcousticBassDrumNote = 35;
        private const int BassDrum1Note = 36;

        public static DrumPatternData Pick(
            DrumPatternPaletteSO palette,
            TimeSignature desiredTs,
            int minHarmonicSubdivisions,
            System.Random rng,
            bool verbose = false)
        {
            if (palette == null || palette.entries == null || palette.entries.Count == 0)
                return null;

            int groupCount = PaletteSelector.DefaultGroupingCount(desiredTs);

            var candidates = new List<Candidate<DrumPatternData>>(palette.entries.Count);
            foreach (var e in palette.entries)
            {
                if (e == null || e.pattern == null) continue;
                candidates.Add(new Candidate<DrumPatternData>(
                    e.pattern, e.weight, FeaturesFor(e.pattern, groupCount)));
            }

            return PaletteSelector.Pick(
                candidates, desiredTs, palette.preferExactTimeSignatureMatches,
                minHarmonicSubdivisions, rng, verbose, "PATTERN_PICK", palette.GetDisplayName());
        }

        public static TsFeatures FeaturesFor(DrumPatternData p, int groupCount)
        {
            int measures = p != null ? p.Measures : 1;
            int subs = p != null ? p.subdivisions : 1;
            var ts = p != null ? p.TimeSignature : default;
            int onsets = FoundationOnsets(p);
            return new TsFeatures(ts, subs, PaletteSelector.DrumStartsPerBar(onsets, measures, groupCount));
        }

        /// <summary>
        /// Foundational onset count across the whole pattern: kick lanes (GM 35/36)
        /// if present; otherwise the single lowest-GM-note lane; 0 if no lanes. The
        /// kick is treated as the metric foundation, analogous to chord changes.
        /// </summary>
        public static int FoundationOnsets(DrumPatternData p)
        {
            if (p == null || p.lanes == null || p.lanes.Count == 0) return 0;

            int kickSum = 0;
            bool foundKick = false;
            foreach (var lane in p.lanes)
            {
                if (lane == null) continue;
                int note = (int)lane.instrument;
                if (note == AcousticBassDrumNote || note == BassDrum1Note)
                {
                    foundKick = true;
                    kickSum += CountActive(lane);
                }
            }
            if (foundKick) return kickSum;

            // Fallback: lowest-GM-note lane (most "foundational" available voice).
            DrumPatternData.Lane lowest = null;
            int lowestNote = int.MaxValue;
            foreach (var lane in p.lanes)
            {
                if (lane == null) continue;
                int note = (int)lane.instrument;
                if (note < lowestNote) { lowestNote = note; lowest = lane; }
            }
            return lowest != null ? CountActive(lowest) : 0;
        }

        private static int CountActive(DrumPatternData.Lane lane)
        {
            if (lane == null || lane.steps == null) return 0;
            int n = 0;
            for (int i = 0; i < lane.steps.Count; i++)
                if (lane.steps[i].active) n++;
            return n;
        }
    }
}
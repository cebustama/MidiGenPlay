using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MidiGenPlay;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Weighted-entry palette of <see cref="DrumPatternData"/> assets.
    ///
    /// Structural mirror of <c>ChordProgressionPaletteSO</c>: a curated, weighted
    /// collection of pattern templates plus light metadata. Lives in Runtime/ for
    /// asset-system convenience only — this is data. As of L5 (D-PAL.3 = author-only),
    /// no runtime caller consumes <see cref="PickRandomPattern"/>; it exists so the
    /// deterministic selection contract is ready when a future batch wires runtime
    /// consumption and decides seed ownership.
    /// </summary>
    [CreateAssetMenu(
        menuName = "MidiGenPlay/Drum Patterns/Palette",
        fileName = "DrumPatternPalette")]
    public class DrumPatternPaletteSO : ScriptableObject
    {
        [Serializable]
        public class WeightedEntry
        {
            [Tooltip("Drum pattern template included in this palette.")]
            public DrumPatternData pattern;

            [Tooltip("Relative weight when randomly picking among entries.")]
            [Min(0f)]
            public float weight = 1f;
        }

        [Header("Metadata")]
        [Tooltip("Optional label describing the palette theme (e.g. 'Funk 4/4', " +
                 "'Half-time Shuffles', 'Metal 7/8 Blasts'). If empty, asset name is used.")]
        public string paletteDisplayName;

        [Tooltip("General notes for this palette. E.g. usage hints, genre, feel, etc.")]
        [TextArea]
        public string paletteNotes;

        [Header("TS-aware Selection")]
        [Tooltip("If enabled, a future TS-aware selector should prefer exact Time Signature " +
                 "matches first (Tier A) before falling back. NOTE: PickRandomPattern does NOT " +
                 "consume this flag in L5 — it is a hint for an external selector, mirroring the " +
                 "chord palette's currently-inert toggle. Runtime consumption is a later decision (D-PAL.3).")]
        public bool preferExactTimeSignatureMatches = true;

        [Header("Patterns")]
        [Tooltip("Weighted list of candidate drum patterns for this palette.")]
        public List<WeightedEntry> entries = new();

        /// <summary>
        /// Returns a human name for UI/debugging.
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(paletteDisplayName))
                return paletteDisplayName;

            return name;
        }

        /// <summary>
        /// Picks a random pattern according to weights.
        /// If cloneResult is true, returns an instantiated copy so callers
        /// never mutate the original asset. Returns null if no valid entries.
        ///
        /// Determinism: given the same <paramref name="rng"/> state and the same
        /// entry list, the selection is reproducible. NOTE: the clone is produced
        /// via <see cref="ScriptableObject.Instantiate(UnityEngine.Object)"/> to match
        /// the chord palette; if you need the deep-cloned runtime form, call
        /// <see cref="DrumPatternData.DeepCloneRuntime"/> on the result.
        /// </summary>
        public DrumPatternData PickRandomPattern(System.Random rng, bool cloneResult = true)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var valid = entries
                .Where(e => e != null && e.pattern != null && e.weight > 0f)
                .ToList();

            if (valid.Count == 0)
                return null;

            var r = rng ?? new System.Random();

            float total = 0f;
            foreach (var e in valid) total += e.weight;

            double pick = r.NextDouble() * total;

            foreach (var e in valid)
            {
                if (pick <= e.weight)
                    return cloneResult
                        ? ScriptableObject.Instantiate(e.pattern)
                        : e.pattern;

                pick -= e.weight;
            }

            // Safety fallback
            var last = valid[valid.Count - 1].pattern;
            return cloneResult ? ScriptableObject.Instantiate(last) : last;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entries == null)
                entries = new List<WeightedEntry>();
        }
#endif
    }
}
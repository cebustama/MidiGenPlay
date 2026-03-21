using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(
        menuName = "MidiGenPlay/Chord Progressions/Palette",
        fileName = "ChordProgressionPalette")]
    public class ChordProgressionPaletteSO : ScriptableObject
    {
        [Serializable]
        public class WeightedEntry
        {
            [Tooltip("Progression template included in this palette.")]
            public ChordProgressionData progression;

            [Tooltip("Relative weight when randomly picking among entries.")]
            [Min(0f)]
            public float weight = 1f;
        }

        [Header("Metadata")]
        [Tooltip("Optional label describing the palette theme (e.g. 'Major 4/4 Pop', " +
                 "'Minor Waltzes (3/4)', 'Dorian Vamps'). If empty, asset name is used.")]
        public string paletteDisplayName;

        [Tooltip("General notes for this palette. E.g. usage hints, genre, feel, etc.")]
        [TextArea]
        public string paletteNotes;

        [Header("TS-aware Selection")]
        [Tooltip("If enabled, TS-aware selection prefers exact Time Signature matches first (Tier A). " +
                 "If disabled, exact TS is skipped and selection starts from Tier B fallback heuristic.")]
        public bool preferExactTsMatches = true;

        [Header("Progressions")]
        [Tooltip("Weighted list of candidate progressions for this palette.")]
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
        /// Picks a random progression according to weights.
        /// If cloneResult is true, returns an instantiated copy so runtime
        /// modifications never touch the original asset.
        /// Returns null if no valid entries.
        /// </summary>
        public ChordProgressionData PickRandomProgression(System.Random rng, bool cloneResult = true)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var valid = entries
                .Where(e => e != null && e.progression != null && e.weight > 0f)
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
                        ? ScriptableObject.Instantiate(e.progression)
                        : e.progression;

                pick -= e.weight;
            }

            // Safety fallback
            var last = valid[valid.Count - 1].progression;
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
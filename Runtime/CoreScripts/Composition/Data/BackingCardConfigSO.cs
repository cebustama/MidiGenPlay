using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/BackingCardConfig")]
    public class BackingCardConfigSO : TrackStyleBundleSO
    {
        [Header("Voice Leading (optional override)")]
        public VoiceLeadingConfig voiceLeadingOverride;

        [Header("Chord Progression (optional card override)")]
        [Tooltip("If set, this progression will be used for the backing track " +
                 "instead of library/procedural generation.")]
        public ChordProgressionData progressionOverride;

        [Serializable]
        public class WeightedProgression
        {
            [Tooltip("Candidate progression template for this card.")]
            public ChordProgressionData progression;

            [Tooltip("Relative weight when randomly picking among candidates.")]
            public float weight = 1f;
        }

        [Tooltip("Optional pool of candidate progressions. " +
                 "If 'progressionOverride' is null and this list has valid entries, " +
                 "one will be picked at random using the given weights.")]
        public List<WeightedProgression> progressionPool = new List<WeightedProgression>();

        /// <summary>
        /// Picks a chord progression override for this card, if any.
        /// Priority:
        /// 1) progressionOverride (always wins if not null).
        /// 2) Weighted pick from progressionPool.
        /// 3) null => no override; composer should fall back to library/procedural.
        /// 
        /// Returns an instantiated (cloned) progression so runtime mutations never
        /// affect the asset in the project.
        /// </summary>
        public ChordProgressionData PickProgressionOverride(System.Random rng)
        {
            // 1) Single explicit override
            if (progressionOverride != null)
            {
                return ScriptableObject.Instantiate(progressionOverride);
            }

            // 2) Weighted pool
            if (progressionPool != null && progressionPool.Count > 0)
            {
                var valid = progressionPool
                    .Where(e => e != null && e.progression != null && e.weight > 0f)
                    .ToList();

                if (valid.Count > 0)
                {
                    var r = rng ?? new System.Random();

                    float total = 0f;
                    foreach (var e in valid) total += e.weight;

                    double pick = r.NextDouble() * total;

                    foreach (var e in valid)
                    {
                        if (pick <= e.weight)
                            return ScriptableObject.Instantiate(e.progression);

                        pick -= e.weight;
                    }

                    // Safety fallback
                    return ScriptableObject.Instantiate(valid[valid.Count - 1].progression);
                }
            }

            // 3) No override defined
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure it always shows up as Backing in the inspector.
            appliesTo = TrackRole.Backing;
        }
#endif
    }
}
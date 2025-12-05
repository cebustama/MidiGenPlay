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

        [Tooltip("Optional palette of candidate progressions for this card. " +
                 "If 'progressionOverride' is null, one will be picked at random " +
                 "from this palette using its internal weights.")]
        public ChordProgressionPaletteSO progressionPalette;

        /// <summary>
        /// Picks a chord progression override for this card, if any.
        /// Priority:
        /// 1) progressionOverride (always wins if not null).
        /// 2) progressionPalette (weighted pick from palette asset).
        /// 3) progressionPool (legacy per-card list; to be phased out).
        /// 4) null => no override; composer should fall back to library/procedural.
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

            // 2) Palette-based override
            if (progressionPalette != null)
            {
                var picked = progressionPalette.PickRandomProgression(
                    rng, cloneResult: true);
                if (picked != null)
                    return picked;
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
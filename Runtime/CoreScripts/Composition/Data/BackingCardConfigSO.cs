using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/BackingCardConfig")]
    public class BackingCardConfigSO : TrackStyleBundleSO
    {
        [Header("Voice Leading (optional override)")]
        public VoiceLeadingConfig voiceLeadingOverride;

        [Header("Chord Expression (Tier 1 articulation)")]
        [Tooltip("Rhythmic articulation applied over the voiced chords for the whole " +
                 "render (CA-T1, D-EXP1=A: persistent card-level selection, not a " +
                 "transient hint). Block (default) = one sustained chord per event, " +
                 "bit-identical to legacy output. " +
                 "See runtime/SSoT_Composer_Backing_Track.md §8.")]
        public ChordExpressionType chordExpression = ChordExpressionType.Block;

        [Tooltip("Note rate for ArpeggioUp / ArpeggioDown; ignored by all other " +
                 "expressions. Eighth (default) = two notes per beat, built on the " +
                 "Part's beat span (meter authority), independent of the asset grid.")]
        public ArpeggioRate arpeggioRate = ArpeggioRate.Eighth;

        [Header("Chord Progression (optional card override)")]
        [Tooltip("If set, this progression will be used for the backing track " +
                 "instead of library/procedural generation.")]
        public ChordProgressionData progressionOverride;

        [Tooltip("Optional palette of candidate progressions for this card. " +
                 "If 'progressionOverride' is null, one will be picked at random " +
                 "from this palette using its internal weights.")]
        public ChordProgressionPaletteSO progressionPalette;

        /// <summary>
        /// Legacy picker (unchanged behavior):
        /// Priority:
        /// 1) progressionOverride (always wins if not null).
        /// 2) progressionPalette (weighted pick from palette asset).
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

            // 2) Palette-based override
            if (progressionPalette != null)
            {
                var picked = progressionPalette.PickRandomProgression(rng, cloneResult: true);
                if (picked != null)
                    return picked;
            }

            // 3) No override defined
            return null;
        }

        /// <summary>
        /// TS-aware override picker:
        /// - If a card override exists, it still wins.
        /// - Else, picks from progressionPalette using the shared, deterministic
        ///   <see cref="ProgressionFinder"/> (Tier A exact-TS → Tier B heuristic →
        ///   Tier C raw weights).
        /// - Always returns a CLONE (never mutates assets).
        ///
        /// As of CE-F1 the selection policy and palette extraction live in
        /// <see cref="PaletteSelector"/> / <see cref="ProgressionFinder"/>; the
        /// previous reflection-based candidate extraction has been removed. This
        /// does not replace runtime TS adaptation; the composer will still normalize
        /// the chosen progression to the Part TS when necessary.
        /// </summary>
        public ChordProgressionData PickProgressionOverride(
            System.Random rng,
            TimeSignature desiredTimeSignature,
            MidiGenPlayConfig settings,
            bool verbose = false)
        {
            rng ??= new System.Random();

            // 1) Single explicit override always wins (composer will adapt TS if needed)
            if (progressionOverride != null)
            {
                if (verbose && progressionOverride.TimeSignature != desiredTimeSignature)
                {
                    Debug.Log($"[BackingCardConfigSO] progressionOverride TS={progressionOverride.TimeSignature} " +
                              $"does not match desired TS={desiredTimeSignature}. Will rely on runtime normalization.");
                }
                return ScriptableObject.Instantiate(progressionOverride);
            }

            // 2) TS-aware palette pick via the shared Finder (clone-on-pick).
            if (progressionPalette != null)
            {
                int minHarmonicSubdivisions = settings != null ? settings.minHarmonicSubdivisions : 4;
                var picked = ProgressionFinder.Pick(
                    progressionPalette, desiredTimeSignature, minHarmonicSubdivisions, rng, verbose);
                if (picked != null)
                    return ScriptableObject.Instantiate(picked);
            }

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
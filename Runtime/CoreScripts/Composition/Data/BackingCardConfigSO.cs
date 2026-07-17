using System.Collections.Generic;
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

        [Tooltip("Note rate for ArpeggioUp / ArpeggioDown, and the pulse rate for " +
                 "Chugging (CA-T2). Ignored by all other expressions. Eighth " +
                 "(default) = two per beat, built on the Part's beat span (meter " +
                 "authority), independent of the asset grid.")]
        public ArpeggioRate arpeggioRate = ArpeggioRate.Eighth;

        [Header("Random Articulation (only when chordExpression = Random)")]
        [Tooltip("MGP-ALWTTT-ARTIC-1 (SD-1=A). Probability of re-rolling the " +
                 "figure at each chord event AFTER the first. 1 (default) = a " +
                 "fresh roll per chord; 0 = one figure for the whole render " +
                 "(per-loop variety then comes from the host's per-render " +
                 "seedOverride, SEED-1); intermediates = chance of change per " +
                 "chord. Deterministic per seed. Ignored unless " +
                 "chordExpression = Random.")]
        [Range(0f, 1f)]
        public float randomRerollChance = 1f;

        [Tooltip("MGP-ALWTTT-ARTIC-1 (SD-2=A). Optional weighted roll pool. " +
                 "Empty (default) = uniform over the six Tier-1 figures. " +
                 "Entries DEFINE the pool: unlisted figures are excluded, " +
                 "weight <= 0 excludes, duplicates sum, Random entries are " +
                 "ignored. Degenerate lists fall back to uniform with a " +
                 "warning. Ignored unless chordExpression = Random.")]
        public List<ChordExpressionWeight> randomFigureWeights =
            new List<ChordExpressionWeight>();

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
            // Delegates to the info-capturing overload; identical draws.
            return PickProgressionOverride(
                rng, desiredTimeSignature, settings, out _, verbose);
        }

        /// <summary>
        /// MGP-ALWTTT-DBG-1 (D-DBG3=A): same TS-aware pick, additionally
        /// reporting the source identity (pre-clone asset name + palette name
        /// + override-vs-palette) for the composer readback. Filling
        /// <paramref name="pickInfo"/> changes no draw and no pick behavior.
        /// </summary>
        public ChordProgressionData PickProgressionOverride(
            System.Random rng,
            TimeSignature desiredTimeSignature,
            MidiGenPlayConfig settings,
            out PatternPickInfo pickInfo,
            bool verbose = false)
        {
            pickInfo = default;
            rng ??= new System.Random();

            // 1) Single explicit override always wins (composer will adapt TS if needed)
            if (progressionOverride != null)
            {
                if (verbose && progressionOverride.TimeSignature != desiredTimeSignature)
                {
                    Debug.Log($"[BackingCardConfigSO] progressionOverride TS={progressionOverride.TimeSignature} " +
                              $"does not match desired TS={desiredTimeSignature}. Will rely on runtime normalization.");
                }
                pickInfo.fromPalette = false;
                pickInfo.sourceAssetName = progressionOverride.name; // pre-clone
                return ScriptableObject.Instantiate(progressionOverride);
            }

            // 2) TS-aware palette pick via the shared Finder (clone-on-pick).
            if (progressionPalette != null)
            {
                int minHarmonicSubdivisions = settings != null ? settings.minHarmonicSubdivisions : 4;
                var picked = ProgressionFinder.Pick(
                    progressionPalette, desiredTimeSignature, minHarmonicSubdivisions, rng, verbose);
                if (picked != null)
                {
                    pickInfo.fromPalette = true;
                    pickInfo.sourceAssetName = picked.name; // pre-clone
                    pickInfo.paletteName = progressionPalette.name;
                    return ScriptableObject.Instantiate(picked);
                }
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
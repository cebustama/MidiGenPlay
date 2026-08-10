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

        [Tooltip("CA-V1 (D-V1-JIT-SCOPE=A). Seeded per-hit velocity jitter, in " +
                 "MIDI velocity units: every articulation hit is offset by a " +
                 "deterministic amount uniform in [-n, +n] and clamped 1..127. " +
                 "0 (default) = exact legacy velocities. Applies to ALL figures, " +
                 "Block included (humanizing a block render is the point). " +
                 "Deterministic: same seed => same jitter => same bytes.")]
        [Range(0, 32)]
        public int velocityJitter = 0;

        [Header("Chord Progression (optional card override)")]
        [Tooltip("If set, this progression will be used for the backing track " +
                 "instead of library/procedural generation.")]
        public ChordProgressionData progressionOverride;

        [Tooltip("Optional palette of candidate progressions for this card. " +
                 "If 'progressionOverride' is null, one will be picked at random " +
                 "from this palette using its internal weights.")]
        public ChordProgressionPaletteSO progressionPalette;

        [Tooltip("MGP-MEL-1 P4 (D3=C / D4=A): when ON and the progression this " +
                 "card resolves declares reference tonalities that EXCLUDE the " +
                 "part's tonality, the part ADOPTS the progression's first " +
                 "listed tonality for this render (mode change; root " +
                 "unchanged). Runs in the Backing composer (PASS 0), so every " +
                 "downstream consumer (bass, melody, harmony) renders in the " +
                 "adopted mode. Applies at COMPOSE time and therefore wins " +
                 "over any pre-render tonality the host set for the part -- " +
                 "combining this with an explicit TonalityEffect on the same " +
                 "card is an authoring error (validate host-side). OFF " +
                 "(default) = existing behavior: the part tonality wins and " +
                 "AsAuthored assets render with the TONFILTER-1 mismatch " +
                 "signal. This is the clean authoring surface for " +
                 "multi-modal progression palettes (e.g. a card whose entries " +
                 "span Dorian / Phrygian / Lydian / Mixolydian).")]
        public bool adoptProgressionTonality = false;

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
                // MGP-TRIAGE-ALWTTT-R3 (E3, D-MGPT-3b): keep the pre-clone
                // name -- Instantiate would append "(Clone)" and the runtime
                // clone's identity would stop matching the reported asset name.
                var clone = ScriptableObject.Instantiate(progressionOverride);
                clone.name = progressionOverride.name;
                return clone;
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
                // MGP-TRIAGE-ALWTTT-R3 (E3, D-MGPT-3b): the clone carries the
                // pre-clone name, so clone.name == pickInfo.sourceAssetName.
                var overrideClone = ScriptableObject.Instantiate(progressionOverride);
                overrideClone.name = progressionOverride.name;
                return overrideClone;
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
                    // MGP-TRIAGE-ALWTTT-R3 (E3, D-MGPT-3b): same identity rule
                    // on the palette path -- this is the path ALWTTT observed.
                    var pickedClone = ScriptableObject.Instantiate(picked);
                    pickedClone.name = picked.name;
                    return pickedClone;
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
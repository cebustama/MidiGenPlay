using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/RhythmCardConfig")]
    public class RhythmCardConfigSO : TrackStyleBundleSO
    {
        [Header("Pattern / Style Selection")]
        [Tooltip("Optional authored drum pattern (grid/piano roll). " +
             "If set, this pattern is used instead of procedural styles.")]
        public DrumPatternData patternOverride;

        [Tooltip("Optional palette of candidate drum patterns for this card. " +
             "If 'patternOverride' is null, one is picked at random from this " +
             "palette using its internal weights. This is what gives a card a " +
             "distinct musical identity (see Palette_Card_Identity_Design).")]
        public DrumPatternPaletteSO patternPalette;

        [Tooltip("High-level recipe for procedural styles: density, feel, fills...")]
        public RhythmRecipe recipeOverride;

        [Tooltip("Optional explicit style id to force (e.g. 'rock_backbeat_4_4'). " +
             "If empty, style is chosen by meter + recipe weights.")]
        public string styleIdOverride; // How would this work?

        [Header("Phrasing (MVP hooks)")]
        [Tooltip("Every N measures, allow a fill / variation. 0 = never.")]
        public int fillEveryNMeasures = 0;
        [Tooltip("If > 0, treat the last K measures of the part as a fill region.")]
        public int lastMeasuresAsFill = 0;

        [Header("Density / Feel")]
        [Range(0f, 1f)] public float kickDensity = 0.5f;
        [Range(0f, 1f)] public float snareGhostNoteChance = 0.0f;
        [Range(0f, 1f)] public float hatSubdivisionBias = 0.5f; // 0=quarters, 1=16ths, in-between = 8ths/shuffle

        /// <summary>
        /// Legacy picker (unchanged behavior). Mirrors
        /// <c>BackingCardConfigSO.PickProgressionOverride(System.Random)</c>.
        ///
        /// Priority:
        /// 1) patternOverride (always wins if not null).
        /// 2) patternPalette (weighted pick from palette asset).
        /// 3) null => no override; composer falls back to TrackParameters.Pattern
        ///    or procedural styles.
        ///
        /// Determinism: the palette pick is seeded from the supplied composer RNG
        /// (ctx.rng), so same seed => same pick (determinism invariant). Returns an
        /// instantiated CLONE so runtime mutation never affects the project asset;
        /// no TS-aware tiering here.
        /// </summary>
        public DrumPatternData PickPatternOverride(System.Random rng)
        {
            // 1) Single explicit override
            if (patternOverride != null)
                return ScriptableObject.Instantiate(patternOverride);

            // 2) Palette-based override (weighted pick, clone-on-pick)
            if (patternPalette != null)
            {
                var picked = patternPalette.PickRandomPattern(rng, cloneResult: true);
                if (picked != null)
                    return picked;
            }

            // 3) No override defined
            return null;
        }

        /// <summary>
        /// TS-aware drum pattern picker (CE-F1), the rhythm twin of
        /// <c>BackingCardConfigSO.PickProgressionOverride(rng, ts, settings, verbose)</c>:
        /// - patternOverride still wins if set.
        /// - Else picks from patternPalette using the shared, deterministic
        ///   <see cref="PatternFinder"/> (Tier A exact-TS → Tier B heuristic →
        ///   Tier C raw weights), keyed on each pattern's TimeSignature.
        /// - Always returns a CLONE.
        ///
        /// Determinism: seeded from the composer RNG, so same seed => same pick.
        /// This adds the TS-awareness the legacy <see cref="PickPatternOverride(System.Random)"/>
        /// overload lacks; that overload is retained for callers that have no TS.
        /// </summary>
        public DrumPatternData PickPatternOverride(
            System.Random rng,
            TimeSignature desiredTimeSignature,
            MidiGenPlayConfig settings,
            bool verbose = false)
        {
            // Delegates to the info-capturing overload; identical draws.
            return PickPatternOverride(
                rng, desiredTimeSignature, settings, out _, verbose);
        }

        /// <summary>
        /// MGP-ALWTTT-DBG-1 (D-DBG3=A): same TS-aware pick, additionally
        /// reporting the source identity (pre-clone asset name + palette name
        /// + override-vs-palette) so the composer's readback can name what was
        /// picked without relying on Unity clone-name suffixes. Filling
        /// <paramref name="pickInfo"/> changes no draw and no pick behavior.
        /// </summary>
        public DrumPatternData PickPatternOverride(
            System.Random rng,
            TimeSignature desiredTimeSignature,
            MidiGenPlayConfig settings,
            out PatternPickInfo pickInfo,
            bool verbose = false)
        {
            pickInfo = default;
            rng ??= new System.Random();

            // 1) Single explicit override always wins.
            if (patternOverride != null)
            {
                pickInfo.fromPalette = false;
                pickInfo.sourceAssetName = patternOverride.name; // pre-clone
                return ScriptableObject.Instantiate(patternOverride);
            }

            // 2) TS-aware palette pick via the shared Finder (clone-on-pick).
            if (patternPalette != null)
            {
                int minHarmonicSubdivisions = settings != null ? settings.minHarmonicSubdivisions : 4;
                var picked = PatternFinder.Pick(
                    patternPalette, desiredTimeSignature, minHarmonicSubdivisions, rng, verbose);
                if (picked != null)
                {
                    pickInfo.fromPalette = true;
                    pickInfo.sourceAssetName = picked.name; // pre-clone
                    pickInfo.paletteName = patternPalette.name;
                    return ScriptableObject.Instantiate(picked);
                }
            }

            // 3) No override defined
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            appliesTo = TrackRole.Rhythm;
        }
#endif
    }
}
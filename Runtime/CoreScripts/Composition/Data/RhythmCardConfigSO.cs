using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/RhythmCardConfig")]
    public class RhythmCardConfigSO : TrackStyleBundleSO
    {
        [Header("Pattern / Style Selection")]
        [Tooltip("Optional authored drum pattern (grid/piano roll). " +
             "If set, this pattern is used instead of procedural styles.")]
        public DrumPatternData patternOverride;
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
    }
}



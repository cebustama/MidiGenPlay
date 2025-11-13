using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "ALWTTT/Cards/HarmonyCardConfig")]
    public class HarmonyCardConfigSO : TrackStyleBundleSO
    {
        public HarmonicLeadingConfig leadingOverride;
        public HarmonyStrategyId strategyIdOverride;
    }
}

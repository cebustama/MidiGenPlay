using UnityEngine;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/BackingCardConfig")]
    public class BackingCardConfigSO : TrackStyleBundleSO
    {
        [Header("Voice Leading (optional override)")]
        public VoiceLeadingConfig voiceLeadingOverride;

        // Punto de expansión futuro:
        // - public ChordProgressionData progressionOverride;
        // - flags de densidad rítmica, arpegios, etc.

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Para que siempre se marque como Backing en el inspector.
            appliesTo = TrackRole.Backing;
        }
#endif
    }
}
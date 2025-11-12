using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Base authoring bundle for any track role (melody, harmony, rhythm, backing, bass…).
    /// Concrete bundles (e.g., MelodyCardConfigSO) should derive from this.
    /// </summary>
    public class TrackStyleBundleSO : ScriptableObject
    {
        [Tooltip("Which role this style primarily targets.")]
        public TrackRole appliesTo = TrackRole.Melody;
    }
}
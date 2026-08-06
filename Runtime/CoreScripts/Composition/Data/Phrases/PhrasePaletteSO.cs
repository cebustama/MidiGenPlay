using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Phrases/Palette")]
    public class PhrasePaletteSO : ScriptableObject
    {
        [System.Serializable]
        public class WeightedArchetype
        {
            public PhraseArchetypeSO archetype;
            [Range(0f, 1f)] public float weight = 1f;
        }

        [Tooltip("Weighted list of archetypes to pick from for each phrase span.")]
        public List<WeightedArchetype> archetypes = new();

        [Tooltip("-1 bias descending, +1 bias ascending, 0 = let planner alternate/decide.")]
        [Range(-1, 1)] public int defaultContourBias = 0;

        // MGP-MEL-1 P2.3: reserved -- inert while the planner keeps the
        // "one chord span = one phrase" model (PhrasePlanner documents the
        // cross-chord extension as a TODO; no archetype emits slots beyond
        // spanBeats). Hidden so it cannot be authored into silence.
        [HideInInspector]
        public bool allowCrossChordPhrases = false;
    }
}
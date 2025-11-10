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

        [Tooltip("If true, archetypes are allowed to produce " +
            "slots that spill into the next chord span.")]
        public bool allowCrossChordPhrases = false;
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    public abstract class PhraseArchetypeSO : ScriptableObject
    {
        [Range(-1, 1)] public int forcedContourDir = 0;

        public abstract List<PhrasePlanner.PhraseSlot> Build(
            double startBeat, 
            double spanBeats, 
            int beatsPerBar,
            int phraseId, 
            int contourDir, 
            System.Random rng,
            TonalityProfileSO profile, 
            MelodicLeadingConfig cfg);
    }
}
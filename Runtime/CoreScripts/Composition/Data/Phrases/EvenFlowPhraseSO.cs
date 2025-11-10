using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Phrases/EvenFlow")]
    public class EvenFlowPhraseSO : PhraseArchetypeSO
    {
        [Header("Slots")]
        [Range(1, 32)] public int minSlots = 2;
        [Range(1, 32)] public int maxSlots = 4;

        [Header("Rests")]
        [Range(0, 1f)] public float restProbMid = 0.2f;

        public override List<PhrasePlanner.PhraseSlot> Build(
            double startBeat,
            double spanBeats,
            int beatsPerBar,
            int phraseId,
            int contourDir,
            System.Random rng,
            TonalityProfileSO profile,
            MelodicLeadingConfig cfg)
        {
            if (forcedContourDir != 0) contourDir = forcedContourDir;

            int slotsInPhrase = Mathf.Clamp(rng.Next(minSlots, maxSlots + 1), 1, 32);
            double slotDur = spanBeats / slotsInPhrase;

            var list = new List<PhrasePlanner.PhraseSlot>(slotsInPhrase);
            for (int i = 0; i < slotsInPhrase; i++)
            {
                double when = startBeat + i * slotDur;
                bool isLast = (i == slotsInPhrase - 1);

                bool forceRest = false;
                if (!isLast && i != 0)
                {
                    double restRoll = rng.NextDouble();
                    if (restRoll < restProbMid) forceRest = true;
                }

                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = when,
                    durBeats = slotDur,
                    playNote = !forceRest,
                    isAccent = (i == 0),
                    isPhraseEnd = isLast,
                    phraseId = phraseId,
                    slotIndexInPhrase = i,
                    totalSlotsInPhrase = slotsInPhrase,
                    desiredContourDir = contourDir
                });
            }

            return list;
        }
    }
}
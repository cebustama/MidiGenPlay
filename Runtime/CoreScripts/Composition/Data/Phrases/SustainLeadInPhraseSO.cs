using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Phrases/SustainLeadIn")]
    public class SustainLeadInPhraseSO : PhraseArchetypeSO
    {
        [Header("Pickup")]
        [Range(0, 1f)] public float pickupChance = 0.4f;
        [Tooltip("Pickup base duration in beats (p.ej. 0.25)")]
        [Range(0.0625f, 1f)] public float pickupSubdivisionBeats = 0.25f;

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

            var list = new List<PhrasePlanner.PhraseSlot>();

            bool doPickup = rng.NextDouble() < pickupChance;
            double pickupDur = pickupSubdivisionBeats;

            if (doPickup && pickupDur * 2 < spanBeats)
            {
                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = startBeat,
                    durBeats = pickupDur * 0.5,
                    playNote = false,
                    isAccent = false,
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = 0,
                    totalSlotsInPhrase = 2,
                    desiredContourDir = contourDir
                });

                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = startBeat + pickupDur * 0.5,
                    durBeats = pickupDur * 0.5,
                    playNote = true,
                    isAccent = true,
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = 1,
                    totalSlotsInPhrase = 2,
                    desiredContourDir = contourDir
                });

                double sustainWhen = startBeat + pickupDur;
                double sustainDur = spanBeats - pickupDur;

                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = sustainWhen,
                    durBeats = sustainDur,
                    playNote = true,
                    isAccent = false,
                    isPhraseEnd = true,
                    phraseId = phraseId,
                    slotIndexInPhrase = 2,
                    totalSlotsInPhrase = 3,
                    desiredContourDir = contourDir
                });
            }
            else
            {
                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = startBeat,
                    durBeats = spanBeats,
                    playNote = true,
                    isAccent = false,
                    isPhraseEnd = true,
                    phraseId = phraseId,
                    slotIndexInPhrase = 0,
                    totalSlotsInPhrase = 1,
                    desiredContourDir = contourDir
                });
            }

            return list;
        }
    }
}
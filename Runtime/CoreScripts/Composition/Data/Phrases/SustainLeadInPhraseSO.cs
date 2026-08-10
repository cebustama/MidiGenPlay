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
                // MGP-TRIAGE-ALWTTT-R3 / F5 (E1). The pickup branch emits
                // THREE slots (silent lead-in, pickup attack, sustain) and
                // previously hardcoded totalSlotsInPhrase = 2 on the first
                // two and 3 on the last -- the observed "slot=1/2 then
                // slot=2/3" within one phraseId.
                //
                // NOT cosmetic. MelodyTrackComposer.IsFinalSlotOfPart is
                // exactly `slotIndexInPhrase == totalSlotsInPhrase - 1`, so
                // the stale denominator made the predicate true TWICE on the
                // part's last chord span (slot 1: 1 == 2-1; slot 2: 2 == 3-1),
                // firing AscendingClimbMelodyStrategy's final tonic cadence on
                // the pickup grace note as well as on the landing.
                //
                // The field counts SLOTS, not audible notes -- EvenFlow counts
                // its rest slots too -- so the silent lead-in is included.
                const int slotsInPhrase = 3;
                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = startBeat,
                    durBeats = pickupDur * 0.5,
                    playNote = false,
                    isAccent = false,
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = 0,
                    totalSlotsInPhrase = slotsInPhrase,
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
                    totalSlotsInPhrase = slotsInPhrase,
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
                    totalSlotsInPhrase = slotsInPhrase,
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
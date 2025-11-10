using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition.Phrases
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Phrases/BurstThenHold")]
    public class BurstThenHoldPhraseSO : PhraseArchetypeSO
    {
        [Header("Burst")]
        [Range(1, 16)] public int burstNoteCountMin = 2;
        [Range(1, 16)] public int burstNoteCountMax = 4;
        [Tooltip("Each burst note duration in beats (p.ej. 0.25 = 16ths)")]
        [Range(0.0625f, 1f)] public float burstSubdivisionBeats = 0.25f;

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

            int burstCount = rng.Next(burstNoteCountMin, burstNoteCountMax + 1);

            double burstDur = burstSubdivisionBeats;
            double burstSpan = burstDur * burstCount;

            if (burstSpan > spanBeats)
            {
                burstSpan = spanBeats * 0.5;
                burstDur = burstSpan / Math.Max(1, burstCount);
            }

            double remain = Math.Max(0.0, spanBeats - burstSpan);

            var list = new List<PhrasePlanner.PhraseSlot>();

            for (int i = 0; i < burstCount; i++)
            {
                double when = startBeat + i * burstDur;
                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = when,
                    durBeats = burstDur,
                    playNote = true,
                    isAccent = (i == 0),
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = i,
                    totalSlotsInPhrase = burstCount + 1,
                    desiredContourDir = contourDir
                });
            }

            double sustainWhen = startBeat + burstSpan;
            double sustainDur = remain > 0 ? remain : burstDur;

            list.Add(new PhrasePlanner.PhraseSlot
            {
                whenBeat = sustainWhen,
                durBeats = sustainDur,
                playNote = true,
                isAccent = false,
                isPhraseEnd = true,
                phraseId = phraseId,
                slotIndexInPhrase = burstCount,
                totalSlotsInPhrase = burstCount + 1,
                desiredContourDir = contourDir
            });

            return list;
        }
    }
}
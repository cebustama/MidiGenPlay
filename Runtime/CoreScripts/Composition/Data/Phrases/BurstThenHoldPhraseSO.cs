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

        [Header("Rests (MGP-TONALITY-1 D-TON7)")]
        [Range(0f, 1f)]
        [Tooltip("Chance that a NON-FIRST burst note is silent, punching " +
                 "holes inside the burst. 0 = legacy (the burst never " +
                 "rests). Note: raising this above 0 introduces one rng " +
                 "draw per non-first burst note, which shifts this " +
                 "archetype's draw stream — deliberate, so that 0 stays " +
                 "byte-identical to every existing asset.")]
        public float restProbMid = 0f;

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

            // D-TON8: this overflow branch is the S3 mechanism — it divides
            // half the span by the drawn burst count, producing durations
            // like 0.667 that sit on no grid. Snapping DOWN keeps the burst
            // inside its planned window; the authored (non-overflow)
            // subdivision is snapped too, since the inspector slider is
            // continuous and can also be off-grid. RNG-free.
            if (meterFitSlots)
            {
                burstDur = SnapDurationToMeter(burstDur, allowTupletSubdivisions);
                burstSpan = burstDur * burstCount;
                if (burstSpan > spanBeats) burstSpan = spanBeats;
            }

            double remain = Math.Max(0.0, spanBeats - burstSpan);

            var list = new List<PhrasePlanner.PhraseSlot>();

            for (int i = 0; i < burstCount; i++)
            {
                double when = startBeat + i * burstDur;

                // D-TON7: intra-phrase holes. Gated on > 0 so the default
                // draws nothing (byte-identity); the first burst note is
                // always sounded so the phrase keeps its attack.
                bool forceRest = false;
                if (restProbMid > 0f && i != 0 && rng.NextDouble() < restProbMid)
                    forceRest = true;

                list.Add(new PhrasePlanner.PhraseSlot
                {
                    whenBeat = when,
                    durBeats = burstDur,
                    playNote = !forceRest,
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
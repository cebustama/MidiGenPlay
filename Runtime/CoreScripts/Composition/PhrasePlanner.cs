using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Responsible for turning a chord timespan (startBeat..startBeat+chordBeats)
    /// into an expressive sequence of PhraseSlots:
    /// - variable note durations
    /// - rests
    /// - accents
    /// - phrase grouping + contour hints
    ///
    /// MelodyTrackComposer will then feed each PhraseSlot to the IMelodyStrategy
    /// to pick actual pitches, and finally write MIDI with per-slot velocity.
    /// </summary>
    public class PhrasePlanner
    {
        #region Data Structs
        // where and how to articulate, not which pitch that articulation will get.
        public struct PhraseSlot
        {
            // WHEN / DURATION in beats (part timeline space)
            public double whenBeat;
            public double durBeats;

            // Should we actually attempt to play here?
            // false = rest (skip PickNext, just advance time).
            public bool playNote;

            // Expressive / phrasing hints
            public bool isAccent;       // strong attack -> higher velocity
            public bool isPhraseEnd;    // cadence / land / hold

            // Phrase grouping / contour
            public int phraseId;        // which phrase this slot belongs to
            public int slotIndexInPhrase;
            public int totalSlotsInPhrase;

            // Optional: desired contour (+1 = go up, -1 = go down, 0 = neutral)
            public int desiredContourDir;
        }

        // TODO: Improve, make smarter
        public struct PhraseMemory
        {
            public int lastPhraseId;
            public int lastContourDir;         // +1 up, -1 down, 0 neutral
            public Note lastPhraseEndNote;
        }

        // live melodic context
        public struct PhraseState
        {
            public int PhraseIndex;           // which phrase this slot belongs to (phraseId)
            public int NoteIndexInPhrase;     // 0..n-1 within that phrase
            public int TotalNotesInPhrase;    // n (helps end-weighting)
            public bool IsStrongBeat;         // rhythmic accent / downbeat
            public bool IsPhraseEnd;          // "land it" moment for cadential bias
            public int DesiredContourDir;     // +1 up, -1 down, 0 neutral (for call/response-ish shaping)

            public Note PhraseStartNote;      // first actual note of the phrase so far
            public Note PhrasePeakNote;       // highest note so far in the phrase
        }

        private enum PhraseArchetype
        {
            EvenFlow,      // steady rhythm
            BurstThenHold, // quick run then long sustain
            SustainLeadIn  // pickup then a held note
        }
        #endregion

        private PhraseMemory _memory; // running memory across phrases
        private readonly MelodicLeadingConfig _cfg;

        public PhrasePlanner(MelodicLeadingConfig cfg, PhraseMemory initialMemory)
        {
            _cfg = cfg;
            _memory = initialMemory;
        }

        /// <summary>
        /// Generate a list of PhraseSlots for this chord span.
        /// currently "one chord span = one phrase"
        /// TODO: span across multiple chords
        /// </summary>
        public List<PhraseSlot> PlanPhraseSlotsForSpan(
            double chordStartBeat,
            double chordBeats,
            int beatsPerBar,
            int chordIndex,
            System.Random rng,
            TonalityProfileSO profile)
        {
            PhraseArchetype arch = ChooseArchetype(rng);

            int contourDir = PickContourDirection(rng);

            // build slots for this phrase according to the archetype.
            var slots = BuildSlotsForArchetype(
                arch,
                chordStartBeat,
                chordBeats,
                beatsPerBar,
                chordIndex,
                contourDir,
                rng,
                profile
            );

            // Mark phrase boundaries & fill metadata
            FinalizePhraseSlots(slots, contourDir);

            // update memory for next call (call/response)
            _memory.lastPhraseId = chordIndex;
            _memory.lastContourDir = contourDir;
            // lastPhraseEndNote gets filled later by MelodyTrackComposer
            // after it knows the actual pitch chosen for the final slot.

            return slots;
        }

        private PhraseArchetype ChooseArchetype(System.Random rng)
        {
            float burstChance = _cfg.burstPhraseChance;
            float sustainChance = _cfg.sustainPhraseChance;

            double roll = rng.NextDouble();

            if (roll < burstChance)
                return PhraseArchetype.BurstThenHold;

            if (roll < burstChance + sustainChance)
                return PhraseArchetype.SustainLeadIn;

            return PhraseArchetype.EvenFlow;
        }

        // alternate +1 / -1 contour to hint at "call/response".
        private int PickContourDirection(System.Random rng)
        {
            // simplest possible: flip sign each phrase
            int dir = _memory.lastContourDir;
            if (dir == 0) dir = 1;        // first phrase: go "up"
            else dir = -dir;              // alternate
            return dir;
        }

        private List<PhraseSlot> BuildSlotsForArchetype(
            PhraseArchetype arch,
            double chordStartBeat,
            double chordBeats,
            int beatsPerBar,
            int phraseId,
            int contourDir,
            System.Random rng,
            TonalityProfileSO profile)
        {
            switch (arch)
            {
                case PhraseArchetype.BurstThenHold:
                    return BuildBurstThenHold(
                        chordStartBeat,
                        chordBeats,
                        phraseId,
                        contourDir,
                        rng
                    );
                case PhraseArchetype.SustainLeadIn:
                    return BuildSustainLeadIn(
                        chordStartBeat,
                        chordBeats,
                        phraseId,
                        contourDir,
                        rng
                    );
                default:
                    return BuildEvenFlow(
                        chordStartBeat,
                        chordBeats,
                        phraseId,
                        contourDir,
                        rng
                    );
            }
        }

        /// Attach final annotations like 'isPhraseEnd', totalSlotsInPhrase etc.
        /// (Right now we already fill those in Build* so this might stay light,
        /// but this is a hook to normalize/validate later).
        private void FinalizePhraseSlots(List<PhraseSlot> slots, int contourDir)
        {
            // Here we could clamp durations, merge overlaps, ensure monotonic time, etc.
            // For now, assume Build* methods yield well-formed slots.
        }

        public PhraseMemory GetMemory() => _memory;

        #region Build Methods
        /// EvenFlow = current subdivision logic, but with rests sprinkled in.
        private List<PhraseSlot> BuildEvenFlow(
            double startBeat,
            double spanBeats,
            int phraseId,
            int contourDir,
            System.Random rng)
        {
            // How many slots in this phrase?
            int slotsInPhrase = Mathf.Clamp(
                rng.Next(_cfg.minSlotsPerPhrase, _cfg.maxSlotsPerPhrase + 1),
                1, 32);

            // Each slot same length for now TODO: change
            double slotDur = spanBeats / slotsInPhrase;

            var list = new List<PhraseSlot>(slotsInPhrase);
            for (int i = 0; i < slotsInPhrase; i++)
            {
                double when = startBeat + i * slotDur;
                bool isLast = (i == slotsInPhrase - 1);

                // Random rest in the middle of the phrase
                bool forceRest = false;
                if (!isLast && i != 0)
                {
                    double restRoll = rng.NextDouble();
                    if (restRoll < _cfg.restProbabilityMidPhrase)
                        forceRest = true;
                }

                list.Add(new PhraseSlot
                {
                    whenBeat = when,
                    durBeats = slotDur,
                    playNote = !forceRest,
                    isAccent = (i == 0),     // first note pops a bit
                    isPhraseEnd = isLast,    // last note = cadence landing
                    phraseId = phraseId,
                    slotIndexInPhrase = i,
                    totalSlotsInPhrase = slotsInPhrase,
                    desiredContourDir = contourDir
                });
            }

            return list;
        }

        /// BurstThenHold = a quick run of short notes, then one long held "money" note.
        private List<PhraseSlot> BuildBurstThenHold(
            double startBeat,
            double spanBeats,
            int phraseId,
            int contourDir,
            System.Random rng)
        {
            // decide how many burst notes
            int burstCount = rng.Next(_cfg.burstNoteCountMin, _cfg.burstNoteCountMax + 1);

            // TODO: change to burstNoteDur
            double burstDur = _cfg.burstSubdivisionBeats;  // e.g. 0.25 for 16ths
            double burstSpan = burstDur * burstCount;

            // clamp burstSpan so it doesn't exceed the chord span
            if (burstSpan > spanBeats)
            {
                // TODO: Why .5?
                burstSpan = spanBeats * 0.5;
                burstDur = burstSpan / Math.Max(1, burstCount);
            }

            // remaining time goes to a single long sustain
            double remain = Math.Max(0.0, spanBeats - burstSpan);

            var list = new List<PhraseSlot>();

            // burst region
            for (int i = 0; i < burstCount; i++)
            {
                double when = startBeat + i * burstDur;
                list.Add(new PhraseSlot
                {
                    whenBeat = when,
                    durBeats = burstDur,
                    playNote = true,
                    isAccent = (i == 0),   // first hit accented
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = i,
                    totalSlotsInPhrase = burstCount + 1, // +1 for the hold after
                    desiredContourDir = contourDir
                });
            }

            // sustain slot after burst
            double sustainWhen = startBeat + burstSpan;
            double sustainDur = remain > 0 ? remain : burstDur; // fallback at least 1 burstDur
            list.Add(new PhraseSlot
            {
                whenBeat = sustainWhen,
                durBeats = sustainDur,
                playNote = true,
                isAccent = false,
                isPhraseEnd = true,      // land! TODO: Is being used?
                phraseId = phraseId,
                slotIndexInPhrase = burstCount,
                totalSlotsInPhrase = burstCount + 1,
                desiredContourDir = contourDir
            });

            return list;
        }

        /// SustainLeadIn = mostly one long held note,
        /// with a small pickup at the start or rest in the middle.
        private List<PhraseSlot> BuildSustainLeadIn(
            double startBeat,
            double spanBeats,
            int phraseId,
            int contourDir,
            System.Random rng)
        {
            var list = new List<PhraseSlot>();

            // tiny pickup?
            bool doPickup = rng.NextDouble() < 0.4; // dice roll
            double pickupDur = _cfg.burstSubdivisionBeats; // e.g. 0.25
            if (doPickup && pickupDur * 2 < spanBeats)
            {
                // rest first half of pickup, then a staccato jab
                list.Add(new PhraseSlot
                {
                    whenBeat = startBeat,
                    durBeats = pickupDur * 0.5,
                    playNote = false, // silence before jab
                    isAccent = false,
                    isPhraseEnd = false,
                    phraseId = phraseId,
                    slotIndexInPhrase = 0,
                    totalSlotsInPhrase = 2,
                    desiredContourDir = contourDir
                });

                // jab
                list.Add(new PhraseSlot
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

                // then a big sustain across the rest
                double sustainWhen = startBeat + pickupDur;
                double sustainDur = spanBeats - pickupDur;
                list.Add(new PhraseSlot
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
                // Just a single held tone, maybe consider it accented at attack or not
                list.Add(new PhraseSlot
                {
                    whenBeat = startBeat,
                    durBeats = spanBeats,
                    playNote = true,
                    isAccent = true,
                    isPhraseEnd = true,
                    phraseId = phraseId,
                    slotIndexInPhrase = 0,
                    totalSlotsInPhrase = 1,
                    desiredContourDir = contourDir
                });
            }

            return list;
        }

        #endregion
    }
}
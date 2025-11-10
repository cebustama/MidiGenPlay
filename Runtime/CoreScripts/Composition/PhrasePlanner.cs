using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition.Phrases;
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

        /// <summary>
        /// A slot in time produced by the PhrasePlanner.
        /// It encodes *when* and *how long* to play, whether this moment
        /// is a rest or an actual note, accent / phrase-end flags,
        /// and metadata about phrase grouping and contour direction.
        ///
        /// This is rhythmic / structural intent. No pitch here.
        /// </summary>
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

        /// <summary>
        /// Lightweight persistent state used to connect one phrase to the next.
        /// This lets us hint at call/response or contour alternation from phrase to phrase.
        /// </summary>
        // TODO: Improve, make smarter
        public struct PhraseMemory
        {
            public int lastPhraseId;
            public int lastContourDir;         // +1 up, -1 down, 0 neutral
            public Note lastPhraseEndNote;
        }

        /// <summary>
        /// Runtime phrase context passed to IMelodyStrategy for each slot.
        /// Includes both structural info (phrase position, desired contour)
        /// and evolving melodic info (what we've already played in the phrase).
        /// </summary>
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

        /// <summary>
        /// Internal phrase "shape recipe" for this span.
        /// EvenFlow = steady rhythm
        /// BurstThenHold = fast run then long sustain
        /// SustainLeadIn = pickup then a held note
        /// </summary>
        private enum PhraseArchetype
        {
            EvenFlow,      // steady rhythm
            BurstThenHold, // quick run then long sustain
            SustainLeadIn  // pickup then a held note
        }
        #endregion

        private const bool useLogs = true;

        private PhraseMemory _memory; // running memory across phrases
        private readonly MelodicLeadingConfig _cfg;

        /// <summary>
        /// Construct a PhrasePlanner for a particular melodic "performer".
        /// The planner holds onto MelodicLeadingConfig (personality knobs)
        /// and rolling PhraseMemory so it can do things like call/response contour.
        /// </summary>
        public PhrasePlanner(MelodicLeadingConfig cfg, PhraseMemory initialMemory)
        {
            _cfg = cfg;
            _memory = initialMemory;
        }

        /// <summary>
        /// Generate a list of PhraseSlots (the rhythmic/expressive plan)
        /// for a single chord span [chordStartBeat .. chordStartBeat+chordBeats).
        ///
        /// Currently we treat "one chord span = one phrase", but we can
        /// TODO later extend this to make phrases span multiple chords.
        /// 
        /// We:
        /// 1. choose a phrase archetype (EvenFlow, BurstThenHold, SustainLeadIn),
        /// 2. pick desired contour direction (+1/-1),
        /// 3. build slots accordingly (rests, bursts, sustain, accents, etc),
        /// 4. finalize/annotate, update memory,
        /// 5. return the slot list to the MelodyTrackComposer.
        /// </summary>
        public List<PhraseSlot> PlanPhraseSlotsForSpan(
            double chordStartBeat,
            double chordBeats,
            int beatsPerBar,
            int chordIndex,
            System.Random rng,
            TonalityProfileSO profile)
        {
            // 1) Try palette-driven path (if a palette is present in config)
            var palette = _cfg.phrasePalette;
            if (palette != null 
                && palette.archetypes != null 
                && palette.archetypes.Count > 0)
            {
                int contourDirArch = PickContourDirection(rng, palette.defaultContourBias);
                var picked = WeightedPick(palette.archetypes, rng);

                List<PhraseSlot> slotsArch;
                if (picked != null)
                {
                    // call the ScriptableObject archetype (data-driven)
                    slotsArch = picked.Build(
                        chordStartBeat,
                        chordBeats,
                        beatsPerBar,
                        chordIndex,
                        contourDirArch,
                        rng,
                        profile,
                        _cfg
                    );
                }
                else
                {
                    // extremely defensive fallback if palette was empty at runtime
                    slotsArch = new List<PhraseSlot>(0);
                }

                FinalizePhraseSlots(slotsArch, contourDirArch);
                _memory.lastPhraseId = chordIndex;
                _memory.lastContourDir = contourDirArch;

                if (useLogs)
                {
                    var archName = picked != null ? picked.name : "Palette(null)";
                    var header =    $"<color=yellow>" +
                                    $"[PhrasePlanner] arch={archName} chordIdx={chordIndex} " +
                                    $"start={chordStartBeat:0.00} " +
                                    $"beats={chordBeats:0.00} slots={slotsArch.Count}" +
                                    $"</color>";
                    Debug.Log(header);
                    for (int i = 0; i < slotsArch.Count; i++)
                    {
                        var s = slotsArch[i];
                        string dirTxt = s.desiredContourDir > 0 ? "+1" :
                                        s.desiredContourDir < 0 ? "-1" : "0";
                        Debug.Log(
                            $"   [{i}] t={s.whenBeat:0.00} dur={s.durBeats:0.00} " +
                            $"play={(s.playNote ? 1 : 0)} acc={(s.isAccent ? 1 : 0)} " +
                            $"end={(s.isPhraseEnd ? 1 : 0)} dir={dirTxt} " +
                            $"phraseId={s.phraseId} " +
                            $"idx={s.slotIndexInPhrase}/{s.totalSlotsInPhrase}"
                        );
                    }
                }

                return slotsArch;
            }

            // 2) Fallback to hard-coded logic
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

            // 6. optional debug logging
            if (useLogs)
            {
                // Example: "[PhrasePlanner] arch=BurstThenHold chordIdx=3 start=12.00 beats=4.00 slots=3"
                //          "   [0] t=12.00 dur=0.25 play=1 acc=1 end=0 dir=+1"
                //          "   [1] t=12.25 dur=0.25 play=1 acc=0 end=0 dir=+1"
                //          "   [2] t=12.50 dur=3.50 play=1 acc=0 end=1 dir=+1"
                var header = $"[PhrasePlanner] arch={arch} chordIdx={chordIndex} " +
                             $"start={chordStartBeat:0.00} beats={chordBeats:0.00} " +
                             $"slots={slots.Count}";
                Debug.Log(header);

                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    string dirTxt = s.desiredContourDir > 0 ? "+1" :
                                    s.desiredContourDir < 0 ? "-1" : "0";

                    Debug.Log(
                        $"   [{i}] t={s.whenBeat:0.00} dur={s.durBeats:0.00} " +
                        $"play={(s.playNote ? 1 : 0)} acc={(s.isAccent ? 1 : 0)} " +
                        $"end={(s.isPhraseEnd ? 1 : 0)} dir={dirTxt} " +
                        $"phraseId={s.phraseId} idx={s.slotIndexInPhrase}/{s.totalSlotsInPhrase}"
                    );
                }
            }

            return slots;
        }

        /// <summary>
        /// Decide which phrase archetype to use for this span.
        /// Uses MelodicLeadingConfig probabilities to bias:
        /// - BurstThenHold  (fast run then long held note)
        /// - SustainLeadIn  (pickup jab then long hold)
        /// - EvenFlow       (steady subdivision w/ occasional rests)
        /// </summary>
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

        /// <summary>
        /// Pick a desired melodic contour direction for this phrase.
        /// Right now this just alternates +1 / -1 between phrases to suggest
        /// "call and response" without having to do real motif analysis.
        /// </summary>
        private int PickContourDirection(System.Random rng)
        {
            // simplest possible: flip sign each phrase
            int dir = _memory.lastContourDir;
            if (dir == 0) dir = 1;        // first phrase: go "up"
            else dir = -dir;              // alternate
            return dir;
        }

        private int PickContourDirection(System.Random rng, int bias /* -1..+1 */)
        {
            // If caller gives a bias, respect it (non-zero).
            if (bias != 0) return bias;

            // Otherwise keep current alternating behavior.
            int dir = _memory.lastContourDir;
            if (dir == 0) dir = 1; else dir = -dir;
            return dir;
        }

        /// <summary>
        /// Dispatch to one of the concrete phrase-shape builders
        /// (EvenFlow / BurstThenHold / SustainLeadIn).
        /// Each of those returns a List<PhraseSlot> already populated with:
        /// - per-slot start/duration (beats),
        /// - play/rest,
        /// - accent/isPhraseEnd,
        /// - phrase grouping metadata.
        /// </summary>
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

        /// <summary>
        /// Hook to post-process / validate slots for a phrase.
        /// (E.g. clamp durations, ensure monotonically increasing whenBeat,
        /// ensure only one final isPhraseEnd, etc.)
        ///
        /// Right now the Build* methods already produce valid slots,
        /// so this is intentionally left light.
        /// </summary>
        private void FinalizePhraseSlots(List<PhraseSlot> slots, int contourDir)
        {
            // Here we could clamp durations, merge overlaps, ensure monotonic time, etc.
            // For now, assume Build* methods yield well-formed slots.
        }

        public PhraseMemory GetMemory() => _memory;

        public void SetMemory(PhraseMemory mem)
        {
            _memory = mem;
        }

        private PhraseArchetypeSO WeightedPick(
            List<PhrasePaletteSO.WeightedArchetype> list, System.Random rng)
        {
            if (list == null || list.Count == 0) return null;

            float sum = 0f;
            foreach (var e in list) 
                if (e?.archetype != null) 
                    sum += Mathf.Max(0f, e.weight);

            if (sum <= 0f)
            {
                // find first non-null archetype
                foreach (var e in list) if (e?.archetype != null) return e.archetype;
                return null;
            }

            double roll = rng.NextDouble() * sum;
            foreach (var e in list)
            {
                if (e?.archetype == null) continue;
                roll -= Mathf.Max(0f, e.weight);
                if (roll <= 0.0) return e.archetype;
            }

            // fallback (should be unreachable)
            for (int i = list.Count - 1; i >= 0; --i)
                if (list[i]?.archetype != null) return list[i].archetype;

            return null;
        }

        #region Build Methods

            /// <summary>
            /// EvenFlow:
            /// - Subdivide the chord span evenly into N slots (N is randomized within
            ///   [minSlotsPerPhrase, maxSlotsPerPhrase]).
            /// - Each slot gets consistent duration.
            /// - Some mid-phrase slots may become rests.
            /// - First slot is accented, last slot is marked as a phrase end / landing.
            ///
            /// Basically a more expressive version of the "robot subdivision"
            /// </summary>
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

        /// <summary>
        /// BurstThenHold:
        /// - A run of short "burst" notes at a fixed fast subdivision,
        ///   then one long sustain ("money note") that covers the rest.
        /// - The burst's first hit is accented.
        /// - The final long sustain is marked isPhraseEnd.
        /// </summary>
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

        /// <summary>
        /// SustainLeadIn:
        /// - Optionally creates a tiny pickup at the start (maybe a jab after a micro-rest),
        /// - Then sustains one long held note across the remainder.
        /// - The long hold is marked asPhraseEnd.
        /// </summary>
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
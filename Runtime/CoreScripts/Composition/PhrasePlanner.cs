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
            else
            {
                Debug.LogError("No palette.");
                return null;
            }
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
    }
}
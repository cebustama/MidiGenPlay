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

        private const bool useLogs = false;

        private PhraseMemory _memory; // running memory across phrases
        private readonly MelodicLeadingConfig _cfg;

        /// <summary>
        /// Construct a PhrasePlanner for a particular melodic "performer".
        /// The planner holds onto MelodicLeadingConfig (personality knobs)
        /// and rolling PhraseMemory so it can do things like call/response contour.
        /// </summary>
        /// <summary>
        /// MGP-ALWTTT-DBG-1 (Ask A): name of the phrase-archetype asset chosen
        /// by the most recent <see cref="PlanPhraseSlotsForSpan"/> call, or
        /// null (no usable palette / every entry's archetype reference null).
        /// Observability only — reading or setting it never affects the draws.
        /// </summary>
        public string LastPlannedArchetypeName { get; private set; }

        public PhrasePlanner(MelodicLeadingConfig cfg, PhraseMemory initialMemory)
        {
            _cfg = cfg;
            _memory = initialMemory;
        }

        /// <summary>
        /// MEL-NULL-1 � the single source of truth for "can the procedural melody
        /// pipeline actually run with this leading config?".
        ///
        /// A palette is USABLE only when the leading config exists, carries a
        /// PhrasePaletteSO, and that palette has at least one archetype entry.
        /// Anything less and <see cref="PlanPhraseSlotsForSpan"/> cannot plan a
        /// single slot.
        ///
        /// MelodyTrackComposer calls this as an up-front precondition so it can fail
        /// once (empty melody track + one error) instead of once per chord span, and
        /// the planner's own bail below uses it too � so the two checks can never
        /// drift apart.
        ///
        /// Uses UnityEngine.Object's == overload deliberately (NOT `is null`), so
        /// destroyed assets are reported as missing.
        /// </summary>
        public static bool HasUsablePalette(MelodicLeadingConfig cfg)
        {
            if (cfg == null) return false;

            var palette = cfg.phrasePalette;
            return palette != null
                && palette.archetypes != null
                && palette.archetypes.Count > 0;
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
        ///
        /// RETURN CONTRACT (MEL-NULL-1): NEVER returns null. When no usable phrase
        /// palette is present it returns an EMPTY list. Callers must read "no slots"
        /// as "no notes for this span", never as an error state to dereference.
        /// (It previously returned null here, and MelodyTrackComposer's slot loop
        /// dereferenced it � an NRE that aborted the ENTIRE song render, taking
        /// rhythm, backing and bass down with the melody.)
        /// </summary>
        public List<PhraseSlot> PlanPhraseSlotsForSpan(
            double chordStartBeat,
            double chordBeats,
            int beatsPerBar,
            int chordIndex,
            System.Random rng,
            TonalityProfileSO profile)
        {
            // MGP-ALWTTT-DBG-1 (Ask A): reset per span so a bail below never
            // leaks the previous span's archetype into the readback.
            LastPlannedArchetypeName = null;

            // 1) Palette-driven path (the only path). MelodyTrackComposer already
            //    enforces this precondition up front, so reaching the bail below means
            //    some OTHER caller skipped the check � hence it stays a LogError.
            if (!HasUsablePalette(_cfg))
            {
                Debug.LogError(
                    "[PhrasePlanner] No usable phrase palette on leading config " +
                    $"'{(_cfg == null ? "null" : _cfg.name)}' � a PhrasePaletteSO with at " +
                    "least one archetype is required to plan phrase slots. Returning an " +
                    "EMPTY slot list (no notes for this span); the render continues. " +
                    "Callers should gate on PhrasePlanner.HasUsablePalette(cfg).");

                return new List<PhraseSlot>(0);
            }

            var palette = _cfg.phrasePalette;

            int contourDirArch = PickContourDirection(rng, palette.defaultContourBias);
            var picked = WeightedPick(palette.archetypes, rng);

            // MGP-ALWTTT-DBG-1 (Ask A): observability only — the archetype
            // chosen for THIS span (null when every palette entry's archetype
            // reference is null). Read by MelodyTrackComposer's readback
            // accumulator right after this call; never affects the draws.
            LastPlannedArchetypeName = picked != null ? picked.name : null;

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
                // extremely defensive fallback: the palette has entries, but every
                // one of them has a null archetype reference.
                slotsArch = new List<PhraseSlot>(0);
            }

            // Never hand a null list back to the composer, whatever an archetype's
            // Build() decided to return (MEL-NULL-1).
            if (slotsArch == null)
                slotsArch = new List<PhraseSlot>(0);

            FinalizePhraseSlots(slotsArch, contourDirArch);
            _memory.lastPhraseId = chordIndex;
            _memory.lastContourDir = contourDirArch;

            if (useLogs)
            {
                var archName = picked != null ? picked.name : "Palette(null)";
                var header = $"<color=yellow>" +
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
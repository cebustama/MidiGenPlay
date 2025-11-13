using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    public enum MelodyStrategyId
    {
        NearestChordTone    = 0,
        ScaleFlow           = 1,
        AscendingClimb      = 2,

        // extend as new implementations created
        // add case in MidiGenerator.MelodyStrategyFactory
    }

    // TODO:  Cadence / target awareness
    // “In 2 beats we’re going to hit the I chord, aim toward its 3rd…”
    // foresight into upcoming chords or the remaining duration of the current chord.

    public struct MelodyPartState
    {
        public int ChordIndex;          // 0..TotalChords-1
        public int TotalChords;         // number of chord spans in this part
        public bool IsFinalSlotOfPart;  // true only for the very last playable slot of the part
        public double PartStartBeat;    // usually 0
        public double PartTotalBeats;   // measures * beatsPerBar
        public NoteName TonicPC;        // convenience (scale degree 0 for current tonality)
    }


    // TODO: accents/velocity
    // return a tiny struct { Note note; int velocity; float legatoFactor; } instead of just Note.

    /// Pick the next melodic note given the current chord, last melody note, and instrument range.
    public interface IMelodyStrategy
    {
        /// Return null to emit a rest.
        Note PickNext(
            NoteName[] chordPitchClasses,           // e.g., {C, E, G}
            NoteName[] scalePitchClasses,           // modal scale for current tonality/root (7 pitch classes)
            Dictionary<NoteName, int> degreeLookup, // maps NoteName -> scale degree index 0..6
            Note lastMelody,                        // may be null for the first note
            MIDIInstrumentSO instrument,            // min/max octaves etc.
            MelodicLeadingConfig cfg,               // melodic constraints/taste
            System.Random rng,                      // deterministic RNG if needed
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile,               // modal/tonality profile (may be null)
            MelodyPartState part,
            HashSet<int> allowedDegrees
        );                 
    }

    /// <summary>
    /// Shared helpers for melody strategies: building candidate pools,
    /// identifying characteristic tones, weighting motion, etc.
    /// The goal is to keep each IMelodyStrategy focused only on
    /// its actual note-selection policy.
    /// </summary>
    public static class MelodyStrategyCommon
    {
        /// <summary>
        /// Builds the pitch-class pool (NoteName set) according to the configured note source.
        /// </summary>
        /// <param name="cfg">Melodic config specifying where pitches may come from.</param>
        /// <param name="chordPitchClasses">Current chord's pitch classes (e.g., {C, E, G}).</param>
        /// <param name="scalePitchClasses">Current tonality's modal scale (7 pitch classes).</param>
        /// <returns>
        /// An enumerable of pitch classes: chord-only, scale-only, or union (distinct),
        /// depending on <see cref="MelodicLeadingConfig.NoteSource"/>.
        /// </returns>
        public static IEnumerable<NoteName> BuildPitchClassPool(
            MelodicLeadingConfig cfg,
            NoteName[] chordPitchClasses,
            NoteName[] scalePitchClasses)
        {
            switch (cfg.noteSource)
            {
                case MelodicLeadingConfig.NoteSource.ChordTonesOnly:
                    return chordPitchClasses;

                case MelodicLeadingConfig.NoteSource.ScaleOnly:
                    return scalePitchClasses;

                case MelodicLeadingConfig.NoteSource.PreferChordTonesAllowScale:
                default:
                    return chordPitchClasses
                        .Concat(scalePitchClasses)
                        .Distinct();
            }
        }

        /// <summary>
        /// Expands pitch classes to concrete candidate notes across the instrument's octave range.
        /// </summary>
        /// <param name="pitchClasses">Pitch classes to expand (e.g., {C, E, G}).</param>
        /// <param name="inst">Instrument definition (min/max octaves, etc.).</param>
        /// <returns>List of concrete <see cref="Note"/> candidates within the instrument range.</returns>
        public static List<Note> ExpandToInstrumentRange(
            IEnumerable<NoteName> pitchClasses,
            MIDIInstrumentSO inst)
        {
            return (from oct in Enumerable.Range(
                        inst.octaveMin,
                        inst.octaveMax - inst.octaveMin + 1)
                    from pc in pitchClasses
                    select Note.Get(pc, oct))
                .ToList();
        }

        /// <summary>
        /// Tests whether a note's pitch class corresponds to a "characteristic" degree
        /// of the active mode/tonality (e.g., Dorian's natural 6, Mixolydian's ♭7).
        /// </summary>
        /// <param name="n">Candidate note to test.</param>
        /// <param name="profile">Tonality profile describing characteristic degrees.</param>
        /// <param name="degreeLookup">Maps <see cref="NoteName"/> to scale degree index (0..6).</param>
        /// <returns>True if the note's degree is in <c>profile.characteristicDegrees</c>.</returns>
        public static bool IsCharacteristic(
            Note n,
            TonalityProfileSO profile,
            Dictionary<NoteName, int> degreeLookup)
        {
            if (profile == null || profile.characteristicDegrees == null)
                return false;
            if (degreeLookup == null)
                return false;

            if (!degreeLookup.TryGetValue(n.NoteName, out var idx))
                return false;

            return profile.characteristicDegrees.Contains(idx);
        }

        /// <summary>
        /// Tests whether a note sits on the tonic scale degree (degree 0).
        /// </summary>
        /// <param name="n">Note to test.</param>
        /// <param name="degreeLookup">Maps <see cref="NoteName"/> to scale degree index (0..6).</param>
        /// <returns>True if the note is the tonic degree in the current scale.</returns>
        public static bool IsTonicDegree(
            Note n,
            Dictionary<NoteName, int> degreeLookup)
        {
            if (degreeLookup == null)
                return false;
            if (!degreeLookup.TryGetValue(n.NoteName, out var idx))
                return false;
            return idx == 0;
        }

        /// <summary>
        /// Returns a simple priority flag for chord-tone preference.
        /// </summary>
        /// <param name="n">Candidate note.</param>
        /// <param name="cfg">Melodic config (inspects <c>noteSource</c>).</param>
        /// <param name="chordSet">Set of chord pitch classes.</param>
        /// <returns>
        /// 0 for "preferred" (chord tone) and 1 for "non-preferred", when chord tones are relevant;
        /// 0 for all when <c>ScaleOnly</c>.
        /// </returns>
        public static int ChordPriority(
            Note n,
            MelodicLeadingConfig cfg,
            HashSet<NoteName> chordSet)
        {
            if (cfg.noteSource == MelodicLeadingConfig.NoteSource.ScaleOnly)
                return 0;

            // In ChordTonesOnly and PreferChordTonesAllowScale we do care:
            return chordSet.Contains(n.NoteName) ? 0 : 1;
        }

        /// <summary>
        /// Orders first-note candidates to produce a musical opening:
        /// chord tones first (if allowed), then modal color tones, then mid-register proximity.
        /// </summary>
        /// <param name="candidates">Concrete note candidates.</param>
        /// <param name="inst">Instrument range (used to compute a register center).</param>
        /// <param name="cfg">Melodic config (source policy).</param>
        /// <param name="profile">Tonality profile (characteristic degrees).</param>
        /// <param name="degreeLookup">Maps <see cref="NoteName"/> to scale degree index (0..6).</param>
        /// <param name="chordSet">Set of chord pitch classes.</param>
        /// <returns>Ordered list of candidates: best first.</returns>
        public static List<Note> OrderFirstNoteCandidates(
            List<Note> candidates,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            TonalityProfileSO profile,
            Dictionary<NoteName, int> degreeLookup,
            HashSet<NoteName> chordSet)
        {
            int centerOct = (inst.octaveMin + inst.octaveMax) / 2;

            var ordered = candidates
                .Select(n => new
                {
                    note = n,
                    chordPrio = ChordPriority(n, cfg, chordSet),
                    favored = IsCharacteristic(n, profile, degreeLookup),
                    distCenter = Math.Abs(
                        Semis(n) - Semis(Note.Get(n.NoteName, centerOct)))
                })
                .OrderBy(x => x.chordPrio)           // chord tones first if allowed
                .ThenBy(x => x.favored ? 0 : 1)      // modal color next
                .ThenBy(x => x.distCenter)           // then center register
                .Select(x => x.note)
                .ToList();

            return ordered;
        }

        /// <summary>
        /// Computes a motion-based weight for a candidate note given the last note,
        /// phrase context, modal profile, and melodic constraints.
        /// Favor stepwise motion, chord tones (when allowed), and characteristic tones.
        /// Penalize large leaps and exact repeats (unless allowed by probability).
        /// </summary>
        /// <param name="candidate">Candidate note to score.</param>
        /// <param name="last">Previously played melody note (may be null for first note).</param>
        /// <param name="cfg">Melodic constraints (step limits, repeat chance, etc.).</param>
        /// <param name="phrase">Phrase context (accents, cadence, etc.).</param>
        /// <param name="profile">Tonality profile (characteristic degrees, cadence policy).</param>
        /// <param name="degreeLookup">Maps <see cref="NoteName"/> to scale degree index (0..6).</param>
        /// <param name="chordSet">Set of chord pitch classes.</param>
        /// <param name="rng">Deterministic RNG used for repeat gating.</param>
        /// <returns>A non-negative weight (0 can be used to exclude a candidate).</returns>
        public static double ComputeMotionWeight(
            Note candidate,
            Note last,
            MelodicLeadingConfig cfg,
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile,
            Dictionary<NoteName, int> degreeLookup,
            HashSet<NoteName> chordSet,
            System.Random rng)
        {
            // If this is literally the first melodic note, 'last' will be null.
            // Caller should normally special-case first note and not call this.
            if (last == null)
                return 0.0;

            int distSemis = Math.Abs(Semis(candidate) - Semis(last));

            // Base weight from motion size: prefer stepwise/small moves.
            double wBase = distSemis <= 2 ? 3.0 :
                           distSemis <= 4 ? 1.5 :
                           0.25;

            // Chord-tone bump if chord tones should be preferred
            if (cfg.noteSource != MelodicLeadingConfig.NoteSource.ScaleOnly &&
                chordSet.Contains(candidate.NoteName))
            {
                wBase *= 2.0;
            }

            // Modal color bump
            if (IsCharacteristic(candidate, profile, degreeLookup))
            {
                wBase *= 2.0;
            }

            // Gentle cadence bias to tonic on strong beats (if profile requests it)
            if (phrase.IsStrongBeat &&
                profile != null &&
                profile.forceCadenceToTonic &&
                IsTonicDegree(candidate, degreeLookup))
            {
                wBase *= 1.5;
            }

            // Respect max step by crushing weight if too large
            if (distSemis > cfg.maxStepSemitones)
            {
                wBase *= 0.01;
            }

            // Avoid exact repeats unless chanceRepeatNote succeeds
            if (distSemis == 0)
            {
                double roll = rng.NextDouble();
                wBase = (roll <= cfg.chanceRepeatNote) ? 1.0 : 0.0;
            }

            return wBase;
        }

        /// <summary>
        /// Picks a note using weighted random selection.
        /// If all weights are ~zero, falls back to the nearest-by-distance from <paramref name="last"/>.
        /// </summary>
        /// <param name="candidates">Candidate notes to pick from.</param>
        /// <param name="weights">Weight per candidate (same order as <paramref name="candidates"/>).</param>
        /// <param name="last">Previously played note (used by the fallback distance heuristic).</param>
        /// <param name="rng">Deterministic RNG.</param>
        /// <returns>The selected candidate note.</returns>
        public static Note PickWeightedRandom(
            List<Note> candidates,
            List<double> weights,
            Note last,
            System.Random rng)
        {
            double total = 0.0;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i];

            if (total <= 0.0001)
            {
                // fallback: closest to last
                if (last == null)
                {
                    return candidates.FirstOrDefault();
                }

                return candidates
                    .OrderBy(n => Math.Abs(Semis(n) - Semis(last)))
                    .First();
            }

            double r = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r <= 0)
                    return candidates[i];
            }

            return candidates.Last();
        }

        /*
        /// <summary>
        /// Returns the absolute MIDI semitone number of a note (0..127).
        /// </summary>
        /// <param name="n">Note to convert.</param>
        /// <returns>Integer MIDI note number (semitones from C-1).</returns>
        public static int Semis(Note n)
        {
            return (int)(byte)n.NoteNumber;
        }*/

        /// <summary>
        /// Returns the absolute semitone number for a DryWetMIDI <see cref="Note"/>.
        /// C0 = 0, C#0 = 1, ..., B0 = 11, C1 = 12, and so on.
        /// Returns -1 if <paramref name="n"/> is null.
        /// </summary>
        public static int Semis(Note n)
        {
            if (n == null) return -1;
            // DryWetMIDI NoteName enum is ordered C=0..B=11.
            return n.Octave * 12 + (int)n.NoteName;
        }

        /// <summary>
        /// Transposes the given note by a number of semitones (can be negative).
        /// Uses DryWetMIDI's built-in transposition via <c>note + semitones</c>.
        /// </summary>
        /// <param name="n">Source note (may be null).</param>
        /// <param name="semitones">Positive or negative number of semitone steps.</param>
        /// <returns>The transposed note, or null if input was null.</returns>
        public static Note Transpose(Note n, int semitones)
        {
            if (n == null)
                return null;

            return n + semitones; // Uses DryWetMIDI's operator overload
        }

        /// <summary>
        /// Moves the note up by <paramref name="semitones"/> semitones (default = 1).
        /// </summary>
        public static Note NudgeUp(Note n, int semitones = 1)
        {
            return Transpose(n, Math.Max(1, semitones));
        }

        /// <summary>
        /// Moves the note down by <paramref name="semitones"/> semitones (default = 1).
        /// </summary>
        public static Note NudgeDown(Note n, int semitones = 1)
        {
            return Transpose(n, -Math.Max(1, semitones));
        }

        /// <summary>
        /// Compute a cadence target on the tonic some number of octaves above a reference.
        /// If the exact +octaves target is out of range, returns the highest tonic in range.
        /// </summary>
        public static Note ComputeTargetTonicAbove(
            NoteName tonicPc,
            MIDIInstrumentSO inst,
            Note referenceOrNull,
            int octavesUp)
        {
            // If we have a reference tonic, try exact +N octaves above it.
            if (referenceOrNull != null && referenceOrNull.NoteName == tonicPc)
            {
                int refSemis = Semis(referenceOrNull);
                int targetSemis = refSemis + 12 * Mathf.Max(1, octavesUp);

                // clamp to instrument range for this PC
                int minOct = inst.octaveMin;
                int maxOct = inst.octaveMax;
                int minSemis = Semis(Note.Get(tonicPc, minOct));
                int maxSemis = Semis(Note.Get(tonicPc, maxOct));

                targetSemis = Mathf.Clamp(targetSemis, minSemis, maxSemis);
                int tgtOct = Mathf.Clamp(targetSemis / 12, minOct, maxOct);
                return Note.Get(tonicPc, tgtOct);
            }

            // Otherwise just return the highest tonic inside the instrument range.
            return Note.Get(tonicPc, inst.octaveMax);
        }

        // Proper mathematical modulo that handles negatives.
        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        // Floor-based integer division (mirrors Math.Floor(a / m) for ints).
        private static int FloorDiv(int a, int m)
        {
            int q = a / m;
            int r = a % m;
            // If remainder has opposite sign to divisor, subtract 1 to floor.
            return (r != 0 && ((r < 0) ^ (m < 0))) ? q - 1 : q;
        }

        public static IEnumerable<NoteName> ApplyAllowedDegreeFilter(
            IEnumerable<NoteName> pitchClasses,
            Dictionary<NoteName, int> degreeLookup,
            HashSet<int> allowedDegrees)
        {
            // No filter? Return as-is.
            if (allowedDegrees == null ||
                allowedDegrees.Count == 0 ||
                degreeLookup == null)
                return pitchClasses;

            var filtered = pitchClasses
                .Where(pc =>
                {
                    if (!degreeLookup.TryGetValue(pc, out var deg))
                        return false;
                    return allowedDegrees.Contains(deg);
                })
                .ToList();

            // If filter killed everything (e.g. misconfigured), fall back to original pool
            return filtered.Count > 0 ? filtered : pitchClasses;
        }

        public static List<Note> BuildCandidatesWithFilter(
            NoteName[] chordPCs,
            NoteName[] scalePCs,
            Dictionary<NoteName, int> degreeLookup,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            HashSet<int> allowedDegrees)
        {
            var pool = BuildPitchClassPool(cfg, chordPCs, scalePCs);
            pool = ApplyAllowedDegreeFilter(pool, degreeLookup, allowedDegrees);
            return ExpandToInstrumentRange(pool, inst);
        }
    }

}
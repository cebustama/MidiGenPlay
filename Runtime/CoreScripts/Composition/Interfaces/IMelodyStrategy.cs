using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.Composition
{
    public enum MelodyStrategyId
    {
        NearestChordTone    = 0,
        ScaleFlow           = 1,

        // extend as new implementations created
    }

    // TODO:  Cadence / target awareness
    // “In 2 beats we’re going to hit the I chord, aim toward its 3rd…”
    // foresight into upcoming chords or the remaining duration of the current chord.

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
            TonalityProfileSO profile               // modal/tonality profile (may be null)
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
        /// Build the pitch-class pool according to cfg.source.
        public static IEnumerable<NoteName> BuildPitchClassPool(
            MelodicLeadingConfig cfg,
            NoteName[] chordPitchClasses,
            NoteName[] scalePitchClasses)
        {
            switch (cfg.source)
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

        /// Expand pitch classes (NoteName) into concrete notes across the instrument range.
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

        /// Return true if this note's pitch class is one of the "characteristic" scale degrees
        /// defined by the active TonalityProfileSO (Dorian's 6, Mixolydian's b7, etc.).
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

        /// True if this note is on degree 0 (tonic) in the current scale.
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

        /// For strategies that want chord tones first 
        /// (ChordTonesOnly or PreferChordTonesAllowScale),
        /// return 0 for chord tones, 1 for others. For ScaleOnly, always 0.
        public static int ChordPriority(
            Note n,
            MelodicLeadingConfig cfg,
            HashSet<NoteName> chordSet)
        {
            if (cfg.source == MelodicLeadingConfig.NoteSource.ScaleOnly)
                return 0;

            // In ChordTonesOnly and PreferChordTonesAllowScale we do care:
            return chordSet.Contains(n.NoteName) ? 0 : 1;
        }

        /// For first-note placement:
        /// - prefer chord tones (if allowed by cfg.source),
        /// - then prefer characteristic notes,
        /// - then prefer mid register.
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

        /// Compute a weight for a candidate note given the last note, phrase info,
        /// modal profile, and melodic constraints.
        /// Used by "flow / weighted-random" style strategies.
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
            if (cfg.source != MelodicLeadingConfig.NoteSource.ScaleOnly &&
                chordSet.Contains(candidate.NoteName))
            {
                wBase *= 2.0;
            }

            // Modal color bump
            if (IsCharacteristic(candidate, profile, degreeLookup))
            {
                wBase *= 2.0;
            }

            // If we're on a strong beat and this profile wants to cadence to tonic,
            // gently prefer the tonic scale degree.
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
                // deterministic instead of UnityEngine.Random.value
                double roll = rng.NextDouble();
                wBase = (roll <= cfg.chanceRepeatNote) ? 1.0 : 0.0;
            }

            return wBase;
        }

        /// Given weighted candidates, do a weighted random pick.
        /// If everything is ~0, fall back to nearest-by-distance.
        public static Note PickWeightedRandom(
            List<Note> candidates,
            List<double> weights,
            Note last,
            System.Random rng)
        {
            // sum weights
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

        public static int Semis(Note n)
        {
            return (int)(byte)n.NoteNumber;
        }
    }
}
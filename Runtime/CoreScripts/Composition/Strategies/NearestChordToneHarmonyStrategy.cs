using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;

using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Simple nearest-chord-tone harmony strategy. This is the default IHarmonyStrategy
    /// we fall back to if the user doesn't inject a custom one.
    ///
    /// Behavior (MVP):
    /// - Take the chord pitch classes (C, E, G... as NoteName[]).
    /// - Expand them across the instrument's octave range.
    /// - Filter out pitches that are basically unison with the melody, and also
    ///   pitches that are either *too* close or *too* far away, based on the
    ///   HarmonicLeadingConfig distance window.
    /// - Pick the candidate with the smallest absolute semitone distance to the melody.
    /// - Tie-break by staying close to the previous harmony note if possible.
    ///
    /// Currently supports cfg.relation == NearestDifferentChordTone.
    /// Other relations in HarmonicLeadingConfig (FixedIntervalSemitones, etc.)
    /// can be added in follow-up.
    /// </summary>
    public sealed class NearestChordToneHarmonyStrategy : IHarmonyStrategy
    {
        public DryWetMidiNote PickHarmony(
                NoteName[] chordPitchClasses,
                DryWetMidiNote melodyNote,
                DryWetMidiNote lastHarmony,
                MIDIInstrumentSO instrument,
                HarmonicLeadingConfig cfg,
                System.Random rng)
        {
            if (melodyNote == null 
                || chordPitchClasses == null 
                || chordPitchClasses.Length == 0)
                return null;

            // Currently we only implement the main/default behavior
            if (cfg.relation != 
                HarmonicLeadingConfig.HarmonyRelation.NearestDifferentChordTone)
            {
                // Fallback - treat it as NearestDifferentChordTone
            }

            // 1. Build full set of chord tones in instrument range.
            var expanded = MelodyStrategyCommon.ExpandToInstrumentRange(
                chordPitchClasses,
                instrument
            );

            // 2. Filter by distance constraints.
            int melSemis = MelodyStrategyCommon.Semis(melodyNote);

            var viable = new List<DryWetMidiNote>();
            foreach (var cand in expanded)
            {
                int diff = Mathf.Abs(MelodyStrategyCommon.Semis(cand) - melSemis);

                // Exclude unison-ish or too close
                if (diff < cfg.minDistanceFromMelody)
                    continue;
                // Exclude too far (super wide intervals can sound weird in tight harmonies)
                if (diff > cfg.maxDistanceFromMelody)
                    continue;

                viable.Add(cand);
            }

            if (viable.Count == 0)
                return null; // No good harmony for this note right now.

            // 3. Score candidates:
            //    primary: closeness to melody
            //    secondary: closeness to previous harmony for smooth voice leading
            DryWetMidiNote best = viable[0];
            int bestMelDist = 999;
            int bestLeadDist = 999;

            foreach (var cand in viable)
            {
                int melDist = Mathf.Abs(MelodyStrategyCommon.Semis(cand) - melSemis);

                int leadDist = 0;
                if (lastHarmony != null)
                {
                    leadDist = Mathf.Abs(
                        MelodyStrategyCommon.Semis(cand) 
                        - MelodyStrategyCommon.Semis(lastHarmony));
                }

                bool better =
                    (melDist < bestMelDist) ||
                    (melDist == bestMelDist && leadDist < bestLeadDist);

                if (better)
                {
                    best = cand;
                    bestMelDist = melDist;
                    bestLeadDist = leadDist;
                }
            }

            return best;
        }
    }

}
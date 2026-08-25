using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Decorator that wraps any <see cref="IMelodyStrategy"/> and layers
    /// phrase-level constraints on top:
    /// - Optional motif repetition via <see cref="RepeatLastNotesDirective"/>:
    ///   the first <c>notesToRepeat</c> audible picks of the phrase form the
    ///   MOTIF (chosen by the inner strategy, contour applied); every
    ///   subsequent slot replays the motif cyclically, transposed by
    ///   <c>transposeSemitones</c> once per completed cycle
    ///   (MGP-MEL-1 F2, D8=B: transpose 0 = exact ostinato; +/-k = classic
    ///   melodic sequence).
    /// - Optional contour enforcement (AscendingOnly / DescendingOnly) that
    ///   snaps a violating pick to the NEAREST candidate of the same
    ///   harmonic pool on the required side of the phrase reference
    ///   (MGP-MEL-1 F3, D9: scale-aware -- the previous chromatic +/-1
    ///   semitone nudge could leave the scale and poison
    ///   lastMelody / PhraseStartNote for later phrases).
    /// The underlying strategy remains responsible for candidate building and
    /// harmonic logic; this class only post-processes its choice.
    ///
    /// LIFETIME CONTRACT: MelodyTrackComposer constructs ONE instance per
    /// phrase (per chord span), so the motif buffer is phrase-scoped by
    /// construction and never leaks across phrases.
    ///
    /// INTENT CONTRACT (MGP-MEL-1 F1): <see cref="RepeatLastNotesDirective"/>
    /// is a [Serializable] class -- Unity deserializes it as a non-null
    /// instance on every directive, so a non-null reference alone carries NO
    /// authoring intent. The composer gates on <c>.enabled</c> before
    /// passing it here; this class re-checks defensively so direct
    /// constructions keep the same contract.
    ///
    /// Determinism: the replay path consumes ZERO rng draws (build-phase
    /// picks draw exactly what the inner strategy draws). Same seed + same
    /// inputs => same output.
    /// </summary>
    public class ConstrainedMelodyStrategy : IMelodyStrategy
    {
        private readonly IMelodyStrategy _inner;
        private readonly ContourConstraint _contour;
        private readonly RepeatLastNotesDirective _repeat;

        // MGP-MEL-1 F2: phrase-scoped motif state (see LIFETIME CONTRACT).
        private readonly List<Note> _motif = new List<Note>();
        private int _replayCount;

        public ConstrainedMelodyStrategy(
            IMelodyStrategy inner,
            ContourConstraint contour,
            RepeatLastNotesDirective repeat)
        {
            _inner = inner;
            _contour = contour;
            _repeat = repeat;
        }

        /// <summary>
        /// Picks the next note for the melody slot.
        /// Steps:
        /// 1) If an ENABLED repeat directive is active and the motif buffer is
        ///    complete, replay the motif (cycled, transposed per cycle).
        /// 2) Otherwise, delegate to the inner strategy.
        /// 3) If a contour constraint is active, snap a violating pick to the
        ///    nearest in-pool candidate on the required side.
        /// 4) While the motif buffer is still filling, record the final pick.
        /// Returns null to emit a rest (rests never enter the motif).
        /// </summary>
        public Note PickNext(
            NoteName[] chordPitchClasses,
            NoteName[] scaleNames,
            Dictionary<NoteName, int> degreeLookup,
            Note lastNote,
            MIDIInstrumentSO instrument,
            MelodicLeadingConfig cfg,
            System.Random rng,
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile,
            MelodyPartState part,
            HashSet<int> allowedDegrees)
        {
            bool repeatActive = _repeat != null && _repeat.enabled;

            // 1) Motif replay (MGP-MEL-1 F2, D8=B)
            if (repeatActive)
            {
                int n = Mathf.Max(1, _repeat.notesToRepeat);
                if (_motif.Count >= n)
                {
                    int idx = _replayCount % n;
                    int cycle = (_replayCount / n) + 1;
                    _replayCount++;

                    // MGP-TONALITY-1 D-TON6=A: ScaleDegrees echoes the motif
                    // as a DIATONIC sequence (degree-wise, per cycle);
                    // ChromaticSemitones is the legacy raw-semitone path,
                    // byte-identical for every existing asset (enum default).
                    Note echoed;
                    if (_repeat.transposeMode ==
                        RepeatLastNotesDirective.MotifTransposeMode.ScaleDegrees)
                    {
                        echoed = TransposeByScaleDegrees(
                            _motif[idx], _repeat.transposeSemitones * cycle,
                            scaleNames, degreeLookup);
                    }
                    else
                    {
                        echoed = MelodyStrategyCommon.Transpose(
                            _motif[idx], _repeat.transposeSemitones * cycle);
                    }
                    return ClampToInstrument(echoed, instrument);
                }
                // Buffer still filling: fall through to the inner strategy and
                // record the (post-contour) pick below.
            }

            // 2) Delegate to base strategy
            var candidate = _inner.PickNext(
                chordPitchClasses, scaleNames, degreeLookup,
                lastNote, instrument, cfg, rng, phrase, profile, part, allowedDegrees);

            if (candidate == null) return null;

            // 3) Contour snapping (MGP-MEL-1 F3, D9: scale-aware)
            if (_contour != ContourConstraint.None && phrase.PhraseStartNote != null)
            {
                int refSemis = MelodyStrategyCommon.Semis(
                    phrase.PhrasePeakNote ?? phrase.PhraseStartNote);
                int candSemis = MelodyStrategyCommon.Semis(candidate);

                if (_contour == ContourConstraint.AscendingOnly && candSemis < refSemis)
                {
                    candidate = NearestPoolNote(
                        above: true, refSemis,
                        chordPitchClasses, scaleNames, degreeLookup,
                        instrument, cfg, allowedDegrees) ?? candidate;
                }
                else if (_contour == ContourConstraint.DescendingOnly && candSemis > refSemis)
                {
                    candidate = NearestPoolNote(
                        above: false, refSemis,
                        chordPitchClasses, scaleNames, degreeLookup,
                        instrument, cfg, allowedDegrees) ?? candidate;
                }
            }

            // 4) Record motif-build picks POST-contour, so the echoed motif is
            //    exactly what was audible.
            if (repeatActive)
                _motif.Add(candidate);

            return candidate;
        }

        /// <summary>
        /// MGP-TONALITY-1 D-TON6=A. Transposes a note by
        /// <paramref name="degreeSteps"/> positions along the 7-degree scale,
        /// crossing octaves as needed. Semitone delta is computed from the
        /// scale's own interval structure (ascending offsets from the tonic),
        /// so the result is in-scale by construction whenever the source
        /// pitch class is. A source pitch class NOT in the scale (cannot
        /// happen for pool-built motifs today) degrades to the legacy
        /// chromatic transpose of the same count. Pure and rng-free.
        /// </summary>
        private static Note TransposeByScaleDegrees(
            Note src,
            int degreeSteps,
            NoteName[] scaleNames,
            Dictionary<NoteName, int> degreeLookup)
        {
            if (src == null) return null;
            if (degreeSteps == 0) return src;
            if (scaleNames == null || scaleNames.Length < 7 ||
                degreeLookup == null ||
                !degreeLookup.TryGetValue(src.NoteName, out var srcDeg))
            {
                // Degrade: chromatic same count (documented fallback).
                return MelodyStrategyCommon.Transpose(src, degreeSteps);
            }

            // Ascending semitone offset of each degree from the tonic.
            // A well-formed 7-PC scale yields a strictly increasing array.
            int tonic = (int)scaleNames[0];
            Span<int> off = stackalloc int[7];
            for (int i = 0; i < 7; i++)
                off[i] = (((int)scaleNames[i] - tonic) + 12) % 12;

            int total = srcDeg + degreeSteps;
            int newDeg = ((total % 7) + 7) % 7;
            int octShift = (int)Math.Floor(total / 7.0)
                         - (int)Math.Floor(srcDeg / 7.0); // srcDeg in 0..6 => second term 0

            int deltaSemis = off[newDeg] - off[srcDeg] + 12 * octShift;
            return MelodyStrategyCommon.Transpose(src, deltaSemis);
        }

        private static Note NearestPoolNote(
            bool above,
            int refSemis,
            NoteName[] chordPCs,
            NoteName[] scaleNames,
            Dictionary<NoteName, int> degreeLookup,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg,
            HashSet<int> allowedDegrees)
        {
            var pool = MelodyStrategyCommon.BuildCandidatesWithFilter(
                chordPCs, scaleNames, degreeLookup, inst, cfg, allowedDegrees);

            Note best = null;
            int bestDist = int.MaxValue;
            foreach (var p in pool)
            {
                int s = MelodyStrategyCommon.Semis(p);
                if (above ? s <= refSemis : s >= refSemis) continue;
                int d = Math.Abs(s - refSemis);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        private static Note ClampToInstrument(Note n, MIDIInstrumentSO inst)
        {
            if (n == null || inst == null) return n;

            int min = inst.octaveMin;
            int max = inst.octaveMax;

            int oct = Mathf.Clamp(n.Octave, min, max);
            return Note.Get(n.NoteName, oct);
        }
    }
}
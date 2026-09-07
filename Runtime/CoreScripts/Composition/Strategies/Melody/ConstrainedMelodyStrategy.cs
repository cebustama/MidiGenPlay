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

        // MGP-MEL-DEGRADE-1: log-only signal for the chromatic-motif snap.
        // Default false => hard no-op, so direct constructions and tests are
        // silent. Never read by any decision; cannot affect output.
        private readonly bool _logSnaps;

        // MGP-MEL-1 F2: phrase-scoped motif state (see LIFETIME CONTRACT).
        private readonly List<Note> _motif = new List<Note>();
        private int _replayCount;

        public ConstrainedMelodyStrategy(
            IMelodyStrategy inner,
            ContourConstraint contour,
            RepeatLastNotesDirective repeat,
            bool logSnaps = false)
        {
            _inner = inner;
            _contour = contour;
            _repeat = repeat;
            _logSnaps = logSnaps;
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
                            scaleNames, degreeLookup, _logSnaps);
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
        /// MGP-TONALITY-1 D-TON6=A, corrected by MGP-MEL-DEGRADE-1.
        /// Transposes a note by <paramref name="degreeSteps"/> positions along
        /// the 7-degree scale, crossing octaves as needed. The semitone delta
        /// is computed from the scale's own interval structure (ascending
        /// offsets from the tonic), so the echoed pitch class is ALWAYS
        /// <c>scaleNames[newDegree]</c> -- this is the tonal (diatonic)
        /// sequence, in-scale by construction. Pure and rng-free.
        ///
        /// Two conditions that the pre-DEGRADE-1 code conflated into one
        /// wrong-unit fallback are now separated:
        ///
        /// (i)  NO USABLE SCALE (D-DEGRADE-3). With no scale, "move N degrees"
        ///      is undefined, so the directive degrades to the EXACT OSTINATO
        ///      that transposeSemitones == 0 already means. The old code
        ///      shifted by degreeSteps SEMITONES here -- a value counted in
        ///      degrees fed to a semitone shift.
        ///
        /// (ii) CHROMATIC SOURCE PITCH CLASS (D-DEGRADE-1 = A). The source is
        ///      snapped to a scale degree first, then sequenced diatonically.
        ///      The prior XML note claimed this "cannot happen for pool-built
        ///      motifs"; it is false. MelodyStrategyCommon.BuildPitchClassPool
        ///      includes the CHORD's pitch classes, so a chord chromatic to
        ///      the part tonality (Neapolitan bII, borrowed iv, secondary
        ///      dominants) puts non-scale notes in the motif by design. That
        ///      was finding F-MEL-DEGRADE-1: motif D#5 in G Ionian with
        ///      degreeSteps = 2 emitted 63 + 2 = F5, in neither the scale nor
        ///      the chord.
        ///
        /// Snap rule (D-DEGRADE-2): the nearest degree AT OR BELOW the source
        /// (floor). This keeps the mapping monotone, so the motif's contour can
        /// never invert, and it reproduces the textbook resolutions of the
        /// borrowed-chord family that actually reaches this path (b6 -> 5,
        /// b2 -> 1). KNOWN LIMITATION: raised alterations (the #4 of a
        /// secondary dominant) would ideally snap UP. We cannot tell the two
        /// apart because NoteName is a pitch class -- D# and Eb are the same
        /// value and the spelling is lost upstream. See F-SPELL-PROVENANCE-1
        /// in runtime/SSoT_Composer_Melody_Track.md for the replacement:
        /// carry the chord's degreeAccidental sign rather than the spelling.
        /// </summary>
        private static Note TransposeByScaleDegrees(
            Note src,
            int degreeSteps,
            NoteName[] scaleNames,
            Dictionary<NoteName, int> degreeLookup,
            bool logSnaps)
        {
            if (src == null) return null;
            if (degreeSteps == 0) return src;

            // (i) No usable scale metadata => exact ostinato (D-DEGRADE-3).
            if (scaleNames == null || scaleNames.Length < 7 || degreeLookup == null)
                return src;

            // Ascending semitone offset of each degree from the tonic.
            // A well-formed 7-PC scale yields a strictly increasing array.
            int tonic = (int)scaleNames[0];
            Span<int> off = stackalloc int[7];
            for (int i = 0; i < 7; i++)
                off[i] = (((int)scaleNames[i] - tonic) + 12) % 12;

            // (ii) Chromatic source => snap to the floor degree, then sequence.
            // The (uint) test also catches a malformed lookup carrying an index
            // outside 0..6, which would otherwise index off[] out of range.
            int snapSemis = 0;
            if (!degreeLookup.TryGetValue(src.NoteName, out var srcDeg) ||
                (uint)srcDeg >= 7u)
            {
                int srcFromTonic = (((int)src.NoteName - tonic) + 12) % 12;
                srcDeg = FloorDegree(srcFromTonic, off);
                snapSemis = off[srcDeg] - srcFromTonic; // <= 0 by construction

                if (logSnaps)
                {
                    Debug.Log(
                        "[ConstrainedMelodyStrategy] chromatic-motif snap | " +
                        $"note={src} -> degree={srcDeg} ({scaleNames[srcDeg]}) " +
                        $"snap={snapSemis}st steps={degreeSteps} " +
                        "(MGP-MEL-DEGRADE-1)");
                }
            }

            int total = srcDeg + degreeSteps;
            int newDeg = ((total % 7) + 7) % 7;
            int octShift = (int)Math.Floor(total / 7.0); // srcDeg in 0..6

            int deltaSemis = off[newDeg] - off[srcDeg] + 12 * octShift;
            return MelodyStrategyCommon.Transpose(src, snapSemis + deltaSemis);
        }

        /// <summary>
        /// Largest degree whose ascending offset from the tonic is at or below
        /// <paramref name="semisFromTonic"/> (D-DEGRADE-2: floor). Degree 0
        /// always qualifies because off[0] == 0 and the argument is in 0..11,
        /// so this is total. Deterministic even for a malformed (non-strictly
        /// increasing) offset array.
        /// </summary>
        private static int FloorDegree(int semisFromTonic, ReadOnlySpan<int> off)
        {
            int best = 0;
            for (int i = 1; i < off.Length; i++)
                if (off[i] <= semisFromTonic && off[i] >= off[best]) best = i;
            return best;
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
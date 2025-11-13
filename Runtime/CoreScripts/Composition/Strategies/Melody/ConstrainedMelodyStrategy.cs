using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Decorator that wraps any <see cref="IMelodyStrategy"/> and layers
    /// phrase-level constraints on top:
    /// - Optional motif repetition via <see cref="RepeatLastNotesDirective"/> to
    ///   echo or transpose the previous phrase peak/start.
    /// - Optional contour enforcement (AscendingOnly / DescendingOnly) that gently
    ///   nudges the chosen note up or down to respect the desired phrase shape.
    /// The underlying strategy remains responsible for building candidates and
    /// harmonic logic; this class only post-processes its choice.
    /// </summary>
    public class ConstrainedMelodyStrategy : IMelodyStrategy
    {
        private readonly IMelodyStrategy _inner;
        private readonly ContourConstraint _contour;
        private readonly RepeatLastNotesDirective _repeat;

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
        /// 1) If a repeat directive is active, return a repeated/ transposed motif note.
        /// 2) Otherwise, delegate to the inner strategy to select a candidate note.
        /// 3) If a contour constraint is active, gently nudge the result to respect it.
        /// Returns null to emit a rest.
        /// </summary>
        public Note PickNext(
            NoteName[] chordPitchClasses,
            NoteName[] scaleNames,
            System.Collections.Generic.Dictionary<NoteName, int> degreeLookup,
            Note lastNote,
            MIDIInstrumentSO instrument,
            MelodicLeadingConfig cfg,
            System.Random rng,
            PhrasePlanner.PhraseState phrase,
            TonalityProfileSO profile,
            MelodyPartState part,
            HashSet<int> allowedDegrees)
        {
            // 1) Optional motif repetition
            if (_repeat != null && phrase.NoteIndexInPhrase > 0 
                && phrase.PhraseStartNote != null)
            {
                var prev = phrase.PhrasePeakNote ?? phrase.PhraseStartNote;
                if (prev != null)
                {
                    // Very simple version: repeat/transpose the last known peak
                    return MelodyStrategyCommon.Transpose(prev, _repeat.transposeSemitones);
                }
            }

            // 2) Delegate to the base strategy
            var candidate = _inner.PickNext(
                chordPitchClasses, scaleNames, degreeLookup,
                lastNote, instrument, cfg, rng, phrase, profile, part, allowedDegrees);

            if (candidate == null) return null;

            // 3) Nudge to respect contour (light-touch)
            if (_contour != ContourConstraint.None && phrase.PhraseStartNote != null)
            {
                int lastSemis = MelodyStrategyCommon.Semis(
                    phrase.PhrasePeakNote ?? phrase.PhraseStartNote);

                int candSemis = MelodyStrategyCommon.Semis(candidate);

                if (_contour == ContourConstraint.AscendingOnly && candSemis < lastSemis)
                    return MelodyStrategyCommon.NudgeUp(candidate, 1);
                if (_contour == ContourConstraint.DescendingOnly && candSemis > lastSemis)
                    return MelodyStrategyCommon.NudgeDown(candidate, 1);
            }
            return candidate;
        }
    }
}
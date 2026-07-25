using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Post-voicing chord articulation seam (CA-T1). Invoked by BOTH chord
    /// emission sites in ChordTrackComposer (grid path and RenderFromProgression)
    /// in place of the legacy single MoveToTime+Chord pair, and later by the
    /// monophonic bass consumer (Feature 2).
    ///
    /// Contract:
    /// - Deterministic and RNG-free: timing is a pure function of beat position
    ///   within the meter, and velocity of that position plus the CA-V1 jitter,
    ///   which is itself a PURE MIX over (seed, event index, hit index) — no
    ///   stateful RNG is ever consumed here. SD-3=A therefore stands verbatim
    ///   after CA-V1; ctx.rng remains untouched by construction, which is what
    ///   protects the bass composer's per-event draw order.
    /// - CA-V1 took the recorded extension route: an optional trailing parameter
    ///   (mirroring IChordVoicer.VoiceChord's forcedInversion), not a signature
    ///   change. Omitting it is exact legacy behavior.
    /// - Meter authority: all figure math builds on the Part-derived beatSpan /
    ///   beatsPerBar passed in, never on asset-side values.
    /// - Block emits the exact legacy pair, bit-identically.
    /// - Never silent: every event produces at least one hit (figures that
    ///   cannot fit the event degrade to Block for that event).
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md §8.
    /// </summary>
    public interface IChordArticulator
    {
        /// <param name="pb">Pattern builder to emit into (MoveToTime + Note/Chord).</param>
        /// <param name="playable">Voiced notes, in voicer order. Chord hits use this
        /// order verbatim; arpeggio hits sort a copy by pitch.</param>
        /// <param name="startBeats">Event onset in beats from part start (Part meter).</param>
        /// <param name="durBeats">Event length in beats (Part meter).</param>
        /// <param name="beatSpan">One Part beat as a musical time span
        /// (MusicTheory.GetBeatSpan(part.TimeSignature)).</param>
        /// <param name="beatsPerBar">Beats per measure of the Part meter (accent grid).</param>
        /// <param name="baseVelocity">Per-event authored velocity (e.velocity), unclamped.</param>
        /// <param name="stepsPerBeat">Grid resolution of the rendered progression.
        /// Not used by the Tier-1 figures (rates are meter-based); carried for
        /// future quantization needs.</param>
        /// <param name="expression">Selected Tier-1 figure; Block = legacy.</param>
        /// <param name="arpeggioRate">Note rate for the arpeggio figures; ignored
        /// by all other expressions.</param>
        /// <param name="jitter">CA-V1 seeded velocity jitter, already scoped to
        /// this chord event by the caller (VelocityJitter.ForEvent). Default /
        /// Amount == 0 is exact identity. Never a rate/figure source — the
        /// selection sentinels are resolved composer-side.</param>
        void Emit(
            PatternBuilder pb,
            IReadOnlyList<Note> playable,
            double startBeats,
            double durBeats,
            MusicalTimeSpan beatSpan,
            int beatsPerBar,
            int baseVelocity,
            int stepsPerBeat,
            ChordExpressionType expression,
            ArpeggioRate arpeggioRate,
            VelocityJitter jitter = default);
    }
}
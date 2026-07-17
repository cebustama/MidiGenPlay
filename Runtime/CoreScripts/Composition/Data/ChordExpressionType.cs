using System;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Tier-1 chord articulation taxonomy (CA-T1, D-EXP2=Tier1): rhythm/velocity
    /// figures applied OVER the already-voiced chord. Articulation is post-voicing
    /// and orthogonal to inversions/Drop-2 and to the �6/�7 transient hints.
    ///
    /// Selection is a PERSISTENT field on the backing card surface
    /// (BackingCardConfigSO.chordExpression, D-EXP1=A) and applies to the whole
    /// render. It is NOT a transient one-shot hint: no snapshot-and-clear.
    ///
    /// <see cref="Block"/> is the default and reproduces the legacy
    /// single-sustained-chord emission bit-identically when unset.
    /// Tier 2 (voicing-reshaping figures) is a later batch and must extend this
    /// enum additively; existing member values are serialized in assets and must
    /// never be renumbered.
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md �8.
    /// </summary>
    public enum ChordExpressionType
    {
        /// <summary>One chord struck at the event onset, sustained for the full
        /// event length. Legacy behavior; bit-identical default.</summary>
        Block = 0,

        /// <summary>Chord re-struck on every meter-anchored beat within the
        /// event, each hit legato to the next (or to the event end).</summary>
        PerBeat = 1,

        /// <summary>Ska/reggae upstroke: short chord hits on every offbeat
        /// (beat + 0.5) within the event. Degrades to Block if no offbeat
        /// fits the event.</summary>
        Offbeat = 2,

        /// <summary>Chord re-struck on every meter-anchored beat within the
        /// event, each hit shortened to at most half a beat.</summary>
        Staccato = 3,

        /// <summary>Voicing notes played one at a time, low to high, cycling,
        /// at the card's <see cref="ArpeggioRate"/>. Degrades to Block if the
        /// event is shorter than one arpeggio hit.</summary>
        ArpeggioUp = 4,

        /// <summary>As <see cref="ArpeggioUp"/>, high to low.</summary>
        ArpeggioDown = 5,

        /// <summary>
        /// MGP-ALWTTT-ARTIC-1: selection-policy SENTINEL, not a figure. The
        /// backing composer resolves it per chord event via
        /// <see cref="RandomArticulationRoller"/> (dedicated seed-derived
        /// stream, SongOrchestrator.ResolveArticulationSeed); the articulator
        /// must never receive it (defensive Block-degrade if it leaks, e.g.
        /// from a bassline card before the bass roll is wired). Appended
        /// member: values 0..5 are serialized in assets and unchanged. The
        /// default uniform roll pool is exactly the concrete members with
        /// value &lt; Random; future Tier-2 members (appended after Random)
        /// do NOT enter the pool unless explicitly admitted.
        /// </summary>
        Random = 6,

        /// <summary>
        /// CA-T2 Tier-2 (voicing-RESHAPING). Drops the chord's third → root +
        /// perfect fifth (+ octave); rhythm is Block (one sustained hit). The
        /// pitch mutation is performed by IChordReshaper BEFORE the articulator;
        /// the articulator degrades a leaked PowerChord to Block. Appended after
        /// Random (value 7): 0..6 are serialized and unchanged. Does NOT enter the
        /// Random roll pool (§8.5 default; not admissible via randomFigureWeights
        /// in v1 — D-T2-POOL).
        /// </summary>
        PowerChord = 7,

        /// <summary>
        /// CA-T2 Tier-2 (voicing-RESHAPING). Palm-mute chug: the power-chord
        /// voicing (same reshape as <see cref="PowerChord"/>) re-struck at the
        /// card's <see cref="ArpeggioRate"/> (D-T2-RHYTHM: no new field). Pitch is
        /// reshaped upstream; the articulator renders the pulse (full-chord hits,
        /// pitch-preserving). Value 8; same pool exclusion as PowerChord.
        /// </summary>
        Chugging = 8,
    }

    /// <summary>
    /// MGP-ALWTTT-ARTIC-1 (SD-2=A): one entry of the optional per-card weighted
    /// roll pool (<c>BackingCardConfigSO.randomFigureWeights</c>), consumed only
    /// when <see cref="ChordExpressionType.Random"/> is selected. An empty list
    /// means the uniform six-figure Tier-1 pool. Providing entries DEFINES the
    /// pool: unlisted figures are excluded; weight &lt;= 0 excludes; duplicate
    /// figures sum; <see cref="ChordExpressionType.Random"/> entries are
    /// ignored. A degenerate list (nothing rollable) falls back to the uniform
    /// pool with a warning (never silent).
    /// </summary>
    [Serializable]
    public struct ChordExpressionWeight
    {
        [Tooltip("Concrete Tier-1 figure (Random itself is ignored).")]
        public ChordExpressionType figure;

        [Tooltip("Relative weight; <= 0 excludes the figure. Duplicates sum.")]
        public float weight;
    }

    /// <summary>
    /// Note rate for <see cref="ChordExpressionType.ArpeggioUp"/> /
    /// <see cref="ChordExpressionType.ArpeggioDown"/>, AND the pulse rate for
    /// <see cref="ChordExpressionType.Chugging"/> (CA-T2, D-T2-RHYTHM=A —
    /// overloaded, no new field). Ignored by all other expressions.
    /// </summary>
    public enum ArpeggioRate
    {
        /// <summary>One note per beat.</summary>
        PerBeat = 0,

        /// <summary>Two notes per beat (default).</summary>
        Eighth = 1,

        /// <summary>Four notes per beat.</summary>
        Sixteenth = 2,
    }
}
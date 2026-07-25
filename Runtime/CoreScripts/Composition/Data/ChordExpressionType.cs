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

        /// <summary>
        /// CA-T2-BOSSA Tier-2 (register-SELECTIVE, selection-extending — NOT
        /// voicing-reshaping): the voicing's lowest note (post-voicer, post-§7
        /// pin, post-CA-T2 reshape — D-BOSSA-BASSNOTE=A) sustained at the event
        /// onset and on each interior bar downbeat; the upper voices (all notes
        /// strictly above that lowest pitch) struck short on every offbeat
        /// (beat + 0.5). IChordReshaper is IDENTITY for this figure: the split
        /// is pure selection via Hit.NoteIndex (-2 = upper-voices sentinel,
        /// D-BOSSA-SEL=A) — no pitch value is created or altered, extending the
        /// BASS-WALK-1 reading of §8 pitch-preservation from "one note" to "a
        /// subset". <see cref="ArpeggioRate"/> is ignored (D-BOSSA-RHYTHM=A
        /// fixed template). Mono/empty voicings and events where no offbeat
        /// fits degrade to Block (never silent; register-safe per F-WALK-REG).
        /// Value 9: 0..8 are serialized in assets and unchanged; excluded from
        /// the §8.5 Random pool by value &gt;= ConcretePoolSize (same mechanism
        /// as PowerChord/Chugging, D-T2-POOL=A′).
        ///
        /// RENAMED from <c>Bossa</c> by CA-T2-BOSSA-V2 (OD-BOSSA-7=A,
        /// OD-BOSSA-7a=A): the regular downbeat/offbeat alternation is a
        /// register split, not the bossa nova comping rhythm — it reads as a
        /// calm ska upstroke (finding F-BOSSA-FEEL). The VALUE is untouched
        /// (append-only; Unity serializes enums by VALUE, so no authored asset
        /// changes meaning) and the name <see cref="Bossa"/> now belongs to
        /// the authentic figure below. Verified before renaming: this enum is
        /// never parsed or persisted by NAME anywhere in the package.
        /// </summary>
        BassUpperSplit = 9,

        /// <summary>
        /// CA-T2-BOSSA-V2 Tier-2 (register-SELECTIVE): the AUTHENTIC bossa
        /// nova comping figure — the lab spec's `basico_solo` 1-bar pattern
        /// (D-FEEL-SCOPE=A; the 2-bar patterns, the harmony-carrying
        /// anticipation and the LOW_ALT root/fifth alternation are recorded
        /// futures). A fixed 5-row template over a bar-length cycle anchored
        /// at absolute beat 0 (cycle position = startBeats mod beatsPerBar,
        /// spec §6.1; a chord change mid-cycle INHERITS the phase and never
        /// resets it, spec §6.2): LOW half-note pulse at 0.0 (medium) and 2.0
        /// (STRONG — the surdo weight sits on beat 2, NOT the downbeat, spec
        /// §0.3); UPPERS at 0.0 (medium), 1.0 (weak) and the syncopation at
        /// 2.5 (STRONG, sustained to the cycle end — deliberately no attack
        /// on beat 3, spec §6.6). Accents are TEMPLATE-supplied tiers reusing
        /// the SD-5 factor values (D-FEEL-ACCENT=A: strong ×1.00 /
        /// medium ×0.85 / weak ×0.80) — a documented per-figure exception to
        /// the §8.3 position-derived curve. Rows at/after the bar length are
        /// clipped in meters shorter than 4/4; every hit truncates at the
        /// cycle end and the event window (D-FEEL-TIE=A: no overshoot).
        /// LOW = index 0 of the ascending sort, UPPERS = the -2 sentinel —
        /// the same closed selection vocabulary as
        /// <see cref="BassUpperSplit"/>; IChordReshaper is IDENTITY.
        /// <see cref="ArpeggioRate"/> is ignored. Degrades to Block when the
        /// voicing has &lt;= 1 note, when beatsPerBar &lt;= 0, or when the
        /// event window contains no UPPERS attack (a bass-only fragment would
        /// be a silent register shift — F-WALK-REG). Value 10: 0..9 are
        /// serialized and unchanged; excluded from the §8.5 pool by the same
        /// &gt;= ConcretePoolSize mechanism.
        /// </summary>
        Bossa = 10,
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

        /// <summary>
        /// CA-V1 (D-V1-RATE-SEL=A): selection-policy SENTINEL, not a rate —
        /// the exact mirror of <see cref="ChordExpressionType.Random"/>. The
        /// composer resolves it per chord event via
        /// <see cref="RandomArticulationRoller.NextRate"/> on a DEDICATED
        /// substream (SongOrchestrator.ResolveArticulationRateSeed), so the
        /// figure roll sequence is unaffected by this knob. Appended member:
        /// values 0..2 are serialized in assets and unchanged. The pool is
        /// uniform over exactly the concrete members with value &lt; Random
        /// (no weight list in v1, D-V1-RATE-POOL=A). If it ever reaches the
        /// articulator it degrades to <see cref="Eighth"/> (never silent).
        /// </summary>
        Random = 3,
    }
}
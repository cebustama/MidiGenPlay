namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Tier-1 chord articulation taxonomy (CA-T1, D-EXP2=Tier1): rhythm/velocity
    /// figures applied OVER the already-voiced chord. Articulation is post-voicing
    /// and orthogonal to inversions/Drop-2 and to the §6/§7 transient hints.
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
    /// See runtime/SSoT_Composer_Backing_Track.md §8.
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
    }

    /// <summary>
    /// Note rate for <see cref="ChordExpressionType.ArpeggioUp"/> /
    /// <see cref="ChordExpressionType.ArpeggioDown"/> (SD-4=B: fixed configurable
    /// rate; randomized per-pattern/per-chord variety is deferred to the seeded
    /// variation batch because CA-T1 is RNG-free by contract, SD-3=A).
    /// Rates are built on the Part's beat span (meter authority), never the
    /// asset's grid resolution.
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
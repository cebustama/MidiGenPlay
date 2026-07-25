using System.Collections.Generic;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// One smoke track row, shared by the editor window and the runtime runner
    /// (D-SMOKE-RT-5=A). Promoted from the two surfaces' previously-duplicated
    /// nested row types so a single <see cref="SmokeSetupSO"/> can feed both —
    /// parity by construction, no per-field drift between two inspectors.
    /// Runtime-safe: no UnityEditor.
    ///
    /// Wraps the runtime spec plus the no-asset articulation fallback knobs
    /// (D-SMOKE-MT-1=B) consumed by SmokeRenderUtil.BuildEffectiveSpec when
    /// 'spec.style' is null and the role is Backing or Bassline. Authored Style
    /// ASSETS carry their own values and ignore these fields.
    ///
    /// UNITY SERIALIZATION NOTE: these field initializers apply only when a row
    /// is constructed in code (new SmokeEntry()). Unity does NOT run them for
    /// rows added via the default inspector "+" on a List&lt;SmokeEntry&gt; —
    /// such rows come up zero-valued (e.g. arpeggioRate = PerBeat, not Eighth;
    /// randomRerollChance = 0, not 1). Add rows through the surfaces' add-buttons
    /// (which call new SmokeEntry()) rather than the raw list "+", or set every
    /// field explicitly.
    /// </summary>
    [System.Serializable]
    public class SmokeEntry
    {
        public SmokeTrackSpec spec = new SmokeTrackSpec();
        public ChordExpressionType chordExpression = ChordExpressionType.Block;
        public ArpeggioRate arpeggioRate = ArpeggioRate.Eighth;

        // MGP-ALWTTT-ARTIC-1 knobs; inert unless chordExpression = Random AND
        // the role is Backing (a Bassline card degrades Random to Block, D6).
        public float randomRerollChance = 1f;
        public List<ChordExpressionWeight> randomFigureWeights =
            new List<ChordExpressionWeight>();

        // CA-V1: seeded velocity jitter for the no-asset fallback (Backing and
        // Bassline). 0 = legacy velocities.
        public int velocityJitter = 0;

        /// <summary>Field-wise copy (rows are shared refs otherwise; used when
        /// round-tripping window state into a SmokeSetupSO).</summary>
        public SmokeEntry Clone() => new SmokeEntry
        {
            spec = new SmokeTrackSpec
            {
                role = spec.role,
                instrument = spec.instrument,
                percussionInstrument = spec.percussionInstrument,
                pattern = spec.pattern,
                style = spec.style,
            },
            chordExpression = chordExpression,
            arpeggioRate = arpeggioRate,
            randomRerollChance = randomRerollChance,
            randomFigureWeights = randomFigureWeights != null
                ? new List<ChordExpressionWeight>(randomFigureWeights)
                : new List<ChordExpressionWeight>(),
            velocityJitter = velocityJitter,
        };
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// MGP-ALWTTT-ARTIC-1: composer-side selection policy for
    /// <see cref="ChordExpressionType.Random"/>. Owns the RNG POLICY that the
    /// CA-T1 articulator contract excludes (SD-3=A stands: the articulator
    /// itself remains RNG-free and never receives Random).
    ///
    /// Stream: constructed by ChordTrackComposer from a DEDICATED
    /// System.Random seeded via SongOrchestrator.ResolveArticulationSeed
    /// (FNV-1a over "{trackSeed}|artic"), fully derived from the SEED-1 base
    /// seed and independent of ctx.rng — so voicing/progression draws are
    /// never perturbed and toggling Fixed&lt;-&gt;Random changes articulation
    /// only. Same seed => identical roll sequence => bit-identical render
    /// (the ALWTTT held-loop replay guarantee).
    ///
    /// Draw discipline (deterministic, documented): first event = 1 figure
    /// draw; every subsequent event = 1 gate draw (NextDouble), plus 1 figure
    /// draw iff gate &lt; rerollChance. rerollChance (SD-1=A) collapses the
    /// granularity axis into one knob: 1 = fresh roll per chord event
    /// (default); 0 = one figure for the whole render (per-loop variety via
    /// the host's per-render seedOverride); intermediates = per-chord change
    /// probability.
    ///
    /// Pool (SD-2=A / D4=A): with no weights, uniform over the six concrete
    /// Tier-1 members (values &lt; Random; future Tier-2 members appended
    /// after Random stay out unless explicitly admitted). With weights, the
    /// entries DEFINE the pool — see <see cref="BuildWeightTable"/>. Figure
    /// picks use one NextDouble over the cumulative table (the rhythm SSoT
    /// one-draw-per-pick idiom).
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md §8 (Random selection
    /// policy) and runtime/SSoT_Runtime_Generation_Orchestration.md §5.1.
    /// </summary>
    public sealed class RandomArticulationRoller
    {
        /// <summary>Concrete Tier-1 pool size: exactly the members with value
        /// below the Random sentinel.</summary>
        public const int ConcretePoolSize = (int)ChordExpressionType.Random; // 6

        private readonly System.Random _rng;
        private readonly float _rerollChance;
        private readonly ChordExpressionType[] _figures;
        private readonly double[] _cumulative;
        private readonly double _total;
        private readonly bool _usedFallback;

        // Observability only (MGP-ALWTTT-ARTIC-1 smoke): the resolved figure per
        // chord event, in emission order. Read by ChordTrackComposer's
        // logGenerator-guarded trace and by tests. Never affects the draws.
        private readonly List<ChordExpressionType> _history =
            new List<ChordExpressionType>();

        private bool _hasCurrent;
        private ChordExpressionType _current;

        public RandomArticulationRoller(
            System.Random rng,
            float rerollChance,
            IReadOnlyList<ChordExpressionWeight> weights)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _rerollChance = Mathf.Clamp01(rerollChance);

            var (figures, cumulative, total, usedFallback, hadEntries) =
                BuildWeightTable(weights);
            _figures = figures;
            _cumulative = cumulative;
            _total = total;
            _usedFallback = usedFallback;

            // Never silent (Block-degrade spirit): a degenerate authored list
            // falls back to the uniform pool AND says so, once, at construction.
            if (usedFallback && hadEntries)
            {
                Debug.LogWarning(
                    "[RandomArticulationRoller] randomFigureWeights is " +
                    "degenerate (no concrete figure with weight > 0); falling " +
                    "back to the uniform six-figure Tier-1 pool.");
            }
        }

        /// <summary>
        /// Resolves the effective figure for the next chord event. Never
        /// returns <see cref="ChordExpressionType.Random"/>.
        /// </summary>
        public ChordExpressionType NextFigure()
        {
            if (!_hasCurrent)
            {
                _current = RollFigure();   // first event: 1 figure draw
                _hasCurrent = true;
                _history.Add(_current);
                return _current;
            }

            // Subsequent events: always exactly 1 gate draw (uniform draw
            // discipline regardless of the chance value), then a conditional
            // figure draw. NextDouble() in [0,1): chance 1 => always re-roll,
            // chance 0 => never.
            double gate = _rng.NextDouble();
            if (gate < _rerollChance)
                _current = RollFigure();

            _history.Add(_current);
            return _current;
        }

        /// <summary>Resolved figures so far, in emission order (observability).</summary>
        internal IReadOnlyList<ChordExpressionType> History => _history;

        /// <summary>One-line trace of the policy + the rolls so far. Feeds the
        /// composer's logGenerator-guarded trace; has no effect on the draws.</summary>
        internal string DescribeRolls()
            => $"chance={_rerollChance:0.##} " +
               $"pool=[{string.Join(",", _figures)}]" +
               (_usedFallback ? " (uniform fallback)" : "") +
               $" rolls=[{string.Join(", ", _history)}]";

        private ChordExpressionType RollFigure()
        {
            double r = _rng.NextDouble() * _total; // one draw per pick
            for (int i = 0; i < _cumulative.Length; i++)
                if (r < _cumulative[i])
                    return _figures[i];
            return _figures[_figures.Length - 1];  // fp edge guard
        }

        /// <summary>
        /// Pure table builder — the SD-2 test seam. Semantics:
        /// null/empty => uniform pool over the six concrete Tier-1 members
        /// (hadEntries=false, usedFallback=true by construction of totals).
        /// With entries: entries DEFINE the pool (unlisted figures excluded);
        /// weight &lt;= 0 excludes; duplicate figures sum; entries whose
        /// figure is Random or outside [0, ConcretePoolSize) are ignored.
        /// If nothing rollable survives, falls back to the uniform pool
        /// (usedFallback=true).
        /// </summary>
        public static (ChordExpressionType[] figures, double[] cumulative,
                         double total, bool usedFallback, bool hadEntries)
            BuildWeightTable(IReadOnlyList<ChordExpressionWeight> weights)
        {
            bool hadEntries = weights != null && weights.Count > 0;

            var acc = new double[ConcretePoolSize];
            double total = 0.0;

            if (hadEntries)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    var w = weights[i];
                    int v = (int)w.figure;
                    if (v < 0 || v >= ConcretePoolSize) continue; // Random / out-of-pool: ignored
                    if (w.weight <= 0f) continue;                 // 0 or negative: excluded
                    acc[v] += w.weight;                            // duplicates sum
                    total += w.weight;
                }
            }

            bool usedFallback = total <= 0.0;
            if (usedFallback)
            {
                for (int v = 0; v < ConcretePoolSize; v++) acc[v] = 1.0;
                total = ConcretePoolSize;
            }

            int n = 0;
            for (int v = 0; v < ConcretePoolSize; v++)
                if (acc[v] > 0.0) n++;

            var figures = new ChordExpressionType[n];
            var cumulative = new double[n];
            double run = 0.0;
            int k = 0;
            for (int v = 0; v < ConcretePoolSize; v++)
            {
                if (acc[v] <= 0.0) continue;
                run += acc[v];
                figures[k] = (ChordExpressionType)v;
                cumulative[k] = run;
                k++;
            }

            return (figures, cumulative, total, usedFallback, hadEntries);
        }
    }
}
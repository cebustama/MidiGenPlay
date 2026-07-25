namespace MidiGenPlay.Composition
{
    /// <summary>
    /// CA-V1 (D-V1-JIT-SRC=A): seeded per-hit velocity jitter for the Tier-1
    /// articulation engine.
    ///
    /// The jitter is a PURE FUNCTION of (seed, event index, hit index) — not a
    /// draw from a stateful stream. Consequences, all of them contract-level:
    /// - <see cref="ChordArticulator"/> / <c>PlanHits</c> stay pure and RNG-free:
    ///   SD-3=A holds verbatim, because no draw order exists inside the
    ///   articulator that could be perturbed.
    /// - <c>ctx.rng</c> is never touched, so the bass composer's per-event draw
    ///   contract (§2 of the Bass SSoT) is structurally safe rather than merely
    ///   "carefully avoided".
    /// - Immune to draw-order coupling: changing an earlier event's figure, rate
    ///   or hit count does NOT shift any later event's jitter.
    /// - Integer-only mixing, so goldens are exactly pinnable across .NET
    ///   versions — unlike System.Random, which is only runtime-stable (hence the
    ///   variance idiom the ARTIC-1 roller tests use).
    ///
    /// Lifecycle: the composer builds ONE render-level value from the card amount
    /// and the dedicated substream seed
    /// (<c>SongOrchestrator.ResolveVelocityJitterSeed(ctx.trackSeed)</c>), threads
    /// it through the render, and calls <see cref="ForEvent"/> per chord event;
    /// the articulator calls <see cref="DeltaFor"/> per planned hit.
    ///
    /// <see cref="Amount"/> == 0 (the default value of the struct) is EXACT
    /// identity: PlanHits returns its hit list untouched, so every pre-CA-V1
    /// render stays byte-identical.
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md §8.7.
    /// </summary>
    public readonly struct VelocityJitter
    {
        /// <summary>Defensive ceiling on the authored amount (the card surface
        /// already ranges 0..32). Keeps 2*Amount+1 far from overflow and keeps
        /// the modulo bias negligible.</summary>
        public const int MaxAmount = 64;

        // Distinct odd constants for the two folds, so the (event, hit) matrix is
        // NOT symmetric: ForEvent(a).DeltaFor(b) != ForEvent(b).DeltaFor(a).
        private const uint EventFold = 0x9E3779B9u;   // golden ratio
        private const uint HitFold = 0x85EBCA6Bu;     // murmur3 finalizer constant

        /// <summary>Maximum absolute velocity offset; &lt;= 0 disables (identity).</summary>
        public readonly int Amount;

        /// <summary>Seed for this scope: render-level as constructed by the
        /// composer, event-level after <see cref="ForEvent"/>.</summary>
        public readonly int Seed;

        public VelocityJitter(int amount, int seed)
        {
            Amount = amount <= 0 ? 0 : (amount > MaxAmount ? MaxAmount : amount);
            Seed = seed;
        }

        /// <summary>True when no jitter is applied — including <c>default</c>.</summary>
        public bool IsOff => Amount <= 0;

        /// <summary>
        /// Event-scoped jitter for the given chord event index. Off => returns
        /// itself (no work, no allocation).
        /// </summary>
        public VelocityJitter ForEvent(int eventIndex)
        {
            if (IsOff) return this;
            unchecked
            {
                uint mixed = Avalanche((uint)Seed ^ ((uint)eventIndex * EventFold));
                return new VelocityJitter(Amount, (int)mixed);
            }
        }

        /// <summary>
        /// Velocity offset for the given hit index, uniform over
        /// [-<see cref="Amount"/>, +<see cref="Amount"/>] (D-V1-JIT-SHAPE=A).
        /// Pure, total, allocation-free. The modulo bias over a 2^32 range with a
        /// span of at most 129 is below one part in 3.3e7 and is not corrected.
        /// </summary>
        public int DeltaFor(int hitIndex)
        {
            if (IsOff) return 0;
            unchecked
            {
                uint x = Avalanche((uint)Seed ^ ((uint)hitIndex * HitFold));
                int span = 2 * Amount + 1;
                return (int)(x % (uint)span) - Amount;
            }
        }

        // lowbias32 finalizer (Bret Mulvey / H. Wellons). Pure integer ops =>
        // identical on every runtime, platform and .NET version.
        private static uint Avalanche(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
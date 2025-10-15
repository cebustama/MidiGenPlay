using System.Collections.Generic;

namespace MidiGenPlay
{
    /// <summary>
    /// Pre-generation structural edit: parts/tracks/tempo/roles inside SongConfig.
    /// Mutators should be pure and return the same or a new config instance.
    /// </summary>
    public interface IArrangementMutator
    {
        /// <summary>For logs and diagnostics.</summary>
        string Name { get; }

        /// <summary>Lower runs first. Use 0 as default.</summary>
        int Order { get; }

        /// <summary>Apply structural edits. Must be deterministic given the same inputs.</summary>
        SongConfig Mutate(SongConfig config, IArrangementContext ctx);
    }

    /// <summary>Lightweight context; extend as needed without coupling to the manager.</summary>
    public interface IArrangementContext
    {
        /// <summary>Personalities keyed by musicianId (may be empty).</summary>
        IReadOnlyDictionary<string, IMusicianPersonality> Personalities { get; }

        /// <summary>Deterministic RNG derived from song/band/mutator ordering.</summary>
        System.Random Rng { get; }

        /// <summary>Optional trace hook (no-op allowed).</summary>
        void Log(string message);
    }
}
using System;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Factory for RhythmTrackComposer.
    /// Currently always returns the same basic groove logic, driven by RhythmTrackComposer.
    /// TODO: look at trackCfg.Parameters.Recipe / musician cards to pick alt styles.
    /// </summary>
    public sealed class RhythmTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;

        public RhythmTrackComposerFactory(MidiGenPlayConfig settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            // today: just basic drum groove
            return new RhythmTrackComposer(_settings);
        }
    }

    /// <summary>
    /// Factory for ChordTrackComposer
    /// Currently always returns the same ChordTrackComposer, which will either:
    /// - render an authored ChordProgressionData if provided
    /// - or build procedural modal chords and cache them in ctx for other tracks.
    /// </summary>
    public sealed class ChordTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly IChordVoicer _voicer;

        public ChordTrackComposerFactory(
            MidiGenPlayConfig settings,
            IChordVoicer voicer)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _voicer = voicer; // can be null, composer already handles that
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            return new ChordTrackComposer(_settings, _voicer);
        }
    }

    /// <summary>
    /// Factory for MelodyTrackComposer.
    ///
    /// What this does:
    /// - Start from global defaults (the ones passed into the ctor from MidiGenerator)
    /// - Check this specific TrackConfig for overrides:
    ///     * melodicLeadingOverride  -> replaces the MelodicLeadingConfig
    ///     * melodyStrategyId        -> picks another IMelodyStrategy
    ///
    /// </summary>
    public sealed class MelodyTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly MelodicLeadingConfig _melodicConfigDefault;
        private readonly IMelodyStrategy _strategyDefault;

        public MelodyTrackComposerFactory(
            MidiGenPlayConfig settings,
            MelodicLeadingConfig melodicConfig,
            IMelodyStrategy strategy)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _melodicConfigDefault = melodicConfig ?? throw new ArgumentNullException(nameof(melodicConfig));
            _strategyDefault = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            // --- 1. Start from global defaults ---
            var finalConfig = _melodicConfigDefault;
            var finalStrategy = _strategyDefault;

            // --- 2. Peek at per-track overrides (TrackParameters) ---
            var p = trackCfg?.Parameters;

            // 2a. Config override (voicing / density / motion)
            if (p != null && p.melodicLeadingOverride != null)
            {
                finalConfig = p.melodicLeadingOverride;
            }

            // 2b. Strategy override (how to pick notes)
            if (p != null)
            {
                // melodyStrategyId is an enum on TrackParameters.
                // We map it to an IMelodyStrategy using MidiGenerator.MelodyStrategyFactory.
                finalStrategy = MidiGenerator.MelodyStrategyFactory.Create(p.melodyStrategyId);
            }

            // --- 3. Debug trace so we can see what's happening in play mode ---
            if (_settings != null && _settings.logGenerator)
            {
                Debug.Log($"<color=yellow>"+
                    $"[Factory/Melody] part='{part?.Name}' mus={trackCfg?.MusicianId} " +
                    $"role={trackCfg?.Role} " +
                    $"cfg='{finalConfig?.name ?? "null"}' " +
                    $"strategy='{finalStrategy?.GetType().Name ?? "null"}'" +
                    $"</color>");
            }

            // --- 4. Build the actual composer with these choices ---
            return new MelodyTrackComposer(_settings, finalConfig, finalStrategy);
        }
    }

    /// <summary>
    /// Factory for Harmony
    /// This mirrors Melody but uses HarmonyComposerMinimal.
    /// </summary>
    public sealed class HarmonyTrackComposerFactory : ITrackComposerFactory
    {
        private readonly HarmonicLeadingConfig _harmonicCfg;
        private readonly IHarmonyStrategy _strategy;

        public HarmonyTrackComposerFactory(
            HarmonicLeadingConfig harmonicCfg,
            IHarmonyStrategy strategy)
        {
            _harmonicCfg = harmonicCfg ?? throw new ArgumentNullException(nameof(harmonicCfg));
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            return new HarmonyComposerMinimal(_harmonicCfg, _strategy);
        }
    }

    /// <summary>
    /// Optional: BassTrackComposerFactory.
    /// </summary>
    public sealed class BassTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly bool _randomChordTone;

        public BassTrackComposerFactory(
            MidiGenPlayConfig settings,
            bool randomChordTone)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _randomChordTone = randomChordTone;
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            return new BassTrackComposer(_settings, _randomChordTone);
        }
    }
}

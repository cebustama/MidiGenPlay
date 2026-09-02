using System;
using System.Collections.Generic;
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
    ///
    /// MGP-ALWTTT-MOD-DIR-1.1: the factory owns per-track memory of the actual
    /// first-chord root pitch from the previous render, keyed by
    /// (part.Name, trackCfg.MusicianId). This memory anchors the directional
    /// modulation hint on the next render. The composer itself is stateless and
    /// is rebuilt per render; the factory's lifetime matches MidiGenerator's
    /// (scene-lifetime singleton). Cold-start (no entry) falls back to the
    /// previous centerOct heuristic, so default Auto behavior is bit-identical
    /// to pre-1.1.
    /// </summary>
    public sealed class ChordTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly IChordVoicer _voicer;

        // MGP-ALWTTT-MOD-DIR-1.1: per-track memory of last first-chord root pitch.
        // Keyed on stable identity fields because SongConfig is rebuilt per render
        // by CompositionSession.BuildSongConfigFromUI -> SongConfigBuilder.FromUI,
        // so PartConfig / TrackConfig object references are NOT stable across loops.
        private readonly Dictionary<(string partName, string musicianId), int>
            _lastFirstChordPitch = new Dictionary<(string, string), int>();

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
            // Memory key derived from stable identity fields, NOT object references.
            // If either identifier is missing, the track is misconfigured for memory
            // purposes; we skip the lookup/stash and let the composer fall back to
            // the centerOct anchor (cold-start behavior).
            string partName = part?.Name;
            string musicianId = trackCfg?.MusicianId;
            bool keyValid = !string.IsNullOrEmpty(partName) && !string.IsNullOrEmpty(musicianId);

            int? remembered = null;
            Action<int> reportBack = null;

            if (keyValid)
            {
                var key = (partName, musicianId);
                if (_lastFirstChordPitch.TryGetValue(key, out var p)) remembered = p;
                reportBack = pitch => _lastFirstChordPitch[key] = pitch;
            }

            return new ChordTrackComposer(_settings, _voicer, remembered, reportBack);
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
            var finalMelConfig = _melodicConfigDefault;
            var finalMelStrategy = _strategyDefault;

            // --- 2. Peek at per-track overrides (TrackParameters) ---
            var p = trackCfg?.Parameters;

            // 2a. Config override (voicing / density / motion)
            if (p != null && p.melodicLeadingOverride != null)
            {
                finalMelConfig = p.melodicLeadingOverride;
            }

            // 2b. Strategy override (how to pick notes)
            if (p != null)
            {
                // melodyStrategyId is an enum on TrackParameters.
                // We map it to an IMelodyStrategy using MidiGenerator.MelodyStrategyFactory.
                finalMelStrategy = MidiGenerator.MelodyStrategyFactory.Create(p.melodyStrategyId);
            }

            // --- 3. Debug trace so we can see what's happening in play mode ---
            if (_settings != null && _settings.logGenerator)
            {
                Debug.Log($"<color=yellow>" +
                    $"[Factory/Melody] part='{part?.Name}' mus={trackCfg?.MusicianId} " +
                    $"role={trackCfg?.Role} " +
                    $"cfg='{finalMelConfig?.name ?? "null"}' " +
                    $"strategy='{finalMelStrategy?.GetType().Name ?? "null"}'" +
                    $"</color>");
            }

            // --- 4. Build the actual composer with these choices ---
            return new MelodyTrackComposer(_settings, finalMelConfig, finalMelStrategy);
        }
    }

    /// <summary>
    /// Factory for Harmony
    /// This mirrors Melody but uses HarmonyComposerMinimal.
    /// </summary>
    public sealed class HarmonyTrackComposerFactory : ITrackComposerFactory
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly HarmonicLeadingConfig _harmonicCfgDefault;
        private readonly IHarmonyStrategy _strategyDefault;

        public HarmonyTrackComposerFactory(
            MidiGenPlayConfig settings,
            HarmonicLeadingConfig harmonicCfg,
            IHarmonyStrategy strategy)
        {
            // MGP-ALWTTT-HARMONY-1 item 4 (F-HARM-4): _settings was never assigned,
            // so HarmonyTrackComposer received settings=null and the factory's own
            // logGenerator block was dead. Mirrors MelodyTrackComposerFactory.
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _harmonicCfgDefault = harmonicCfg ?? throw new ArgumentNullException(nameof(harmonicCfg));
            _strategyDefault = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public ITrackComposer CreateFor(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            MidiGenerator.GenContext ctx)
        {
            // --- 1. Start from global defaults we were constructed with ---
            var finalHarmCfg = _harmonicCfgDefault;
            var finalHarStrategy = _strategyDefault;

            // --- 2. Look for per-track overrides from TrackParameters ---
            var p = trackCfg?.Parameters;

            // Config override (register placement, interval relation rules, etc)
            if (p != null && p.harmonicLeadingOverride != null)
            {
                finalHarmCfg = p.harmonicLeadingOverride;
            }

            // Strategy override (how harmony notes are chosen relative to melody/chord)
            if (p != null)
            {
                // harmonyStrategyId should be an enum on TrackParameters
                // and HarmonyStrategyFactory maps that enum to an IHarmonyStrategy.
                finalHarStrategy = MidiGenerator.HarmonyStrategyFactory.Create(p.harmonyStrategyId);
            }

            // --- 3. Helpful debug so we can inspect what's happening in play mode ---
            if (_settings != null && _settings.logGenerator)
            {
                Debug.Log(
                    $"<color=yellow>" +
                    $"[Factory/Harmony] part='{part?.Name}' mus={trackCfg?.MusicianId} " +
                    $"role={trackCfg?.Role} " +
                    $"cfg='{finalHarmCfg?.name ?? "null"}' " +
                    $"strategy='{finalHarStrategy?.GetType().Name ?? "null"}'" +
                    $"</color>"
                );
            }

            // --- 4. Build and return the concrete composer for this track ---
            // HarmonyTrackComposer is our harmony generator that:
            // - looks up the melody (leader) for this musician/part from ctx
            // - uses finalHarmCfg + finalHarStrategy to generate the harmony line
            return new HarmonyTrackComposer(_settings, finalHarmCfg, finalHarStrategy);
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
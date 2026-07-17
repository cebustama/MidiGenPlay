using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay
{
    public class MidiGenerator
    {
        private const string DebugTag = "<color=green>[MidiGenerator]</color>";

        public const int MetronomeChannel = 15;

        private MidiGenPlayConfig settings;
        private readonly ISongOrchestrator _orchestrator;
        private readonly Dictionary<TrackRole, ITrackComposerFactory> _factories = new();
        private readonly IChordVoicer _voicer;

        public ISongOrchestrator Orchestrator => _orchestrator;

        public MidiGenerator(MidiGenPlayConfig config, IChordVoicer voicer = null)
        {
            settings = config;
            _voicer = voicer;

            // Pull global configs from MidiGenPlayConfig
            // (TODO: swap these out based on Cards/Musicians)
            var melodyCfg = settings.melodicLeading;
            var harmonyCfg = settings.harmonicLeading;

            // Pick which strategies are "default" for THIS generation run.
            // (TODO: swap these out based on Cards/Musicians)
            var melodyStrategy = new ScaleFlowMelodyStrategy(); // or NearestChordToneMelodyStrategy()
            var harmonyStrategy = new NearestDifferentChordToneHarmonyStrategy();


            // Register factories per role
            _factories[TrackRole.Melody] =
                new MelodyTrackComposerFactory(settings, melodyCfg, melodyStrategy);

            // Lead reuses same melodic behavior for now
            _factories[TrackRole.Lead] =
                _factories[TrackRole.Melody];

            _factories[TrackRole.Harmony] =
                new HarmonyTrackComposerFactory(settings, harmonyCfg, harmonyStrategy);

            _factories[TrackRole.Backing] =
                new ChordTrackComposerFactory(settings, _voicer);

            _factories[TrackRole.Rhythm] =
                new RhythmTrackComposerFactory(settings);

            _factories[TrackRole.Bassline] =
                new BassTrackComposerFactory(settings, randomChordTone: false);

            _orchestrator = new SongOrchestrator(settings, _factories, _voicer);

            if (settings != null && settings.logGenerator)
            {
                var roles = string.Join(", ", _factories.Keys.Select(r => r.ToString()));
                Debug.Log($"{DebugTag} ComposerFactory registry: [{roles}]  " +
                    $"| Voicer={(voicer != null ? voicer.GetType().Name : "null")}");
            }
        }

        public class GenContext
        {
            public MidiGenPlayConfig Settings;
            public System.Random rng;
            // MGP-ALWTTT-ARTIC-1: the per-track seed int behind rng, swap/
            // restored by SongOrchestrator.GenerateOne exactly like rng. Lets
            // composers derive dedicated deterministic substreams (e.g.
            // SongOrchestrator.ResolveArticulationSeed) WITHOUT consuming the
            // shared rng stream. 0 when a composer runs outside GenerateOne
            // (direct test/tooling calls) — still deterministic.
            public int trackSeed;
            // MGP-ALWTTT-DBG-1 (Ask A, D-DBG2=A): per-track readback sink.
            // Installed/collected by SongOrchestrator.GenerateOne with the
            // SAME swap/restore discipline as rng and trackSeed. Composers
            // invoke it AT MOST ONCE per Compose with what they actually
            // resolved (ResolvedTrackChoice); null outside GenerateOne or in
            // GenerateSong (no PartRender to collect into) — composers must
            // null-check (ctx?.ReportResolved?.Invoke(...)). ITrackComposer
            // is unchanged by design.
            public Action<ResolvedTrackChoice> ReportResolved;
            // MGP-ALWTTT-DBG-3 (Ask C, D-DBG4=A): per-render pattern/
            // progression override — precedence STEP 0 (wins over card
            // override/palette, TrackParameters.Pattern, recipes and
            // procedural). Stateless per call: swap/restored by
            // SongOrchestrator.GenerateOne exactly like rng/trackSeed, so it
            // can never leak across tracks. Composers clone-on-apply and
            // treat a type mismatch as warn + ignore (fall through to the
            // normal precedence chain). Bassline ignores it in v1 (the bass
            // renders the shared progression; override Backing instead).
            public PatternDataSO patternOverride;
            public IChordVoicer ChordVoicer;
            public VoiceLeadingConfig chordVoicingPreset;
            public MIDIInstrumentSO DefaultMelodicInstrument;

            public Func<SongConfig.PartConfig, TrackRole, MidiFile>
                GetTrackForRole;
            public Func<MidiFile, List<Melanchall.DryWetMidi.Interaction.Note>>
                ExtractMonophonicNotes;
            public Func<ChordProgressionData, TempoMap, MusicTheory.MusicTheory.TimeSignature, long, ChordProgressionData.ChordEvent>
                FindChordEventAt;
            // Progression
            public Func<SongConfig.PartConfig, ChordProgressionData>
                GetProgressionForPart;
            public Action<SongConfig.PartConfig, ChordProgressionData> SetProgressionForPart;
            // Tonalities
            public Func<SongConfig.PartConfig, TonalityProfileSO>
                GetTonalityProfileForPart;
            // Melodies
            public Func<SongConfig.PartConfig, string, List<GuideNote>>
                GetMelodyForPartMusician;
            public Action<SongConfig.PartConfig, string, List<GuideNote>>
                SetMelodyForPartMusician;
            public Func<SongConfig.PartConfig, string>
                GetFirstMelodyMusicianIdForPart;
        }

        #region Melody
        public static class MelodyStrategyFactory
        {
            public static IMelodyStrategy Create(MelodyStrategyId id)
            {
                switch (id)
                {
                    case MelodyStrategyId.NearestChordTone:
                        return new NearestChordToneMelodyStrategy();
                    case MelodyStrategyId.ScaleFlow:
                        return new ScaleFlowMelodyStrategy();
                    case MelodyStrategyId.AscendingClimb:
                        return new AscendingClimbMelodyStrategy();
                    // extend as new melody strategies implemented
                    default:
                        return new ScaleFlowMelodyStrategy();
                }
            }
        }

        public struct GuideNote
        {
            public double startBeats;
            public double durBeats;
            public DryWetMidiNote note; // absolute pitch
        }
        #endregion

        #region Harmony 

        public static class HarmonyStrategyFactory
        {
            public static IHarmonyStrategy Create(HarmonyStrategyId id)
            {
                switch (id)
                {
                    case HarmonyStrategyId.NearestChordTone:
                    default:
                        return new NearestChordToneHarmonyStrategy();
                }
            }
        }

        #endregion

        #region Generation Methods

        public MidiFile GenerateSong(SongConfig song)
        {
            // Preflight: list every track and its pattern
            if (settings?.logGenerator == true && song?.Parts != null)
            {
                for (int pi = 0; pi < song.Parts.Count; pi++)
                {
                    var part = song.Parts[pi];
                    Debug.Log($"{DebugTag} Part='{part.Name}' TS={TimeSignatureProperties[part.TimeSignature].BeatsPerMeasure}/" +
                              $"{TimeSignatureProperties[part.TimeSignature].BeatUnit} Ton={part.Tonality} Root={part.RootNote} meas={part.Measures}");
                    for (int ti = 0; ti < (part.Tracks?.Count ?? 0); ti++)
                    {
                        var cfg = part.Tracks[ti];
                        Debug.Log($"{DebugTag}   role={cfg.Role} mus={cfg.MusicianId} inst={InstName(cfg)} pattern={PatternName(cfg)}");
                    }
                }
            }

            Debug.Log($"{DebugTag} Generating Midi for song");

            return _orchestrator.GenerateSong(song);
        }
        #endregion

        public static void ApplyChannelVolume(MidiFile file, int channel, int volume01_127)
        {
            var vol = (SevenBitNumber)Mathf.Clamp(volume01_127, 0, 127);
            foreach (var chunk in file.GetTrackChunks())
            {
                // Insert after our bank/patch events (indexes 0..2), but be safe.
                int insertAt = Mathf.Min(3, chunk.Events.Count);
                chunk.Events.Insert(insertAt, new ControlChangeEvent((SevenBitNumber)7, vol)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });
            }
        }

        #region Private Methods

        private static string InstName(SongConfig.PartConfig.TrackConfig cfg)
        {
            if (cfg?.Instrument != null) return cfg.Instrument.InstrumentName;
            if (cfg?.PercussionInstrument != null) return cfg.PercussionInstrument.InstrumentName;
            return "-";
        }

        private static string PatternName(SongConfig.PartConfig.TrackConfig cfg)
        {
            var p = cfg?.Parameters?.Pattern;
            return p != null ? $"{p.GetType().Name}:{p.name}" : "-";
        }
        #endregion
    }
}
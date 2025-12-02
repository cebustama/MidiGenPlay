using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
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

        public MidiFile GenerateMelodyTrackWithPattern(
            MIDIInstrumentSO instrument,
            MelodyPatternData melodyPattern,
            Tonality tonality,
            NoteName rootNote,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int measures = 4,
            int channel = 0)
        {
            if (melodyPattern == null) Debug.LogError("EMPTY MELODY PATTERN");
            if (instrument == null) Debug.LogError("EMPTY INSTRUMENT");

            Debug.Log($"<color=green>Generating Melody Track: " +
                $"{melodyPattern.DisplayName} for {instrument.InstrumentName}</color> " +
                $"Tonality: {tonality.ToString()}");

            // 1️⃣ Retrieve scale and time signature details
            var scale = GetScaleFromTonality(tonality, rootNote);
            var timeSignatureInfo = GetTimeSignatureDetails(timeSignature, bpm);
            int beatsPerBar = timeSignatureInfo.BeatsPerMeasure;

            // Determine the number of times to repeat the melody pattern
            int patternLength = melodyPattern.Measures;
            int numRepeats = Mathf.CeilToInt((float)measures / patternLength);

            // 2️⃣ Initialize Pattern Builder
            PatternBuilder patternBuilder = new PatternBuilder();
            patternBuilder.MoveToStart(); // Ensure all notes align properly

            int minOct = instrument.octaveMin;
            int maxOct = instrument.octaveMax;

            // 3️⃣ Repeat the melody pattern across all measures
            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int measureOffset = repeat * patternLength * beatsPerBar;

                // Process each note in the melody pattern
                foreach (var noteData in melodyPattern.melodyNotes)
                {
                    // Choose a scale degree from possible options
                    MusicTheory.MusicTheory.ScaleDegree selectedDegree =
                        noteData.possibleDegrees[UnityEngine.Random.Range(0, noteData.possibleDegrees.Count)];

                    int octave = UnityEngine.Random.Range(minOct, maxOct + 1);

                    // Convert scale degree to actual note
                    if (!GetNoteFromScale(
                        scale, selectedDegree, rootNote, octave, out DryWetMidiNote note))
                    {
                        Debug.LogWarning($"Invalid Scale Degree {selectedDegree} in {melodyPattern.DisplayName}");
                        continue;
                    }

                    // Calculate note timing with repetition offset
                    var startTime = MusicalTimeSpan.Quarter * (noteData.startMeasure * beatsPerBar + measureOffset) +
                                    MusicalTimeSpan.Quarter * noteData.startBeat;

                    var duration = MusicalTimeSpan.Quarter * noteData.durationBeats;

                    // 4️⃣ Move to the correct position and add the note
                    patternBuilder.MoveToTime(startTime);
                    patternBuilder.Note(note, duration, (SevenBitNumber)noteData.velocity);
                }
            }

            // 5️⃣ Build MIDI pattern and create file
            Pattern pattern = patternBuilder.Build();
            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // 6️⃣ Assign instrument patch and bank
            int bankNumber = int.Parse(instrument.BankName);
            int presetNumber = instrument.PatchIndex;
            SetBankAndPatchEvents(midiFile, bankNumber, presetNumber, channel);
            SetChannel(midiFile, channel);

            return midiFile;
        }

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

        private void SetBankAndPatchEvents(MidiFile midiFile, int bankNumber, int presetNumber, int channel)
        {
            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                // BANK
                // Split the bank number into MSB and LSB if it's greater than 127
                int msb = (bankNumber >> 7) & 0x7F; // Most significant byte
                int lsb = bankNumber & 0x7F;        // Least significant byte

                // MPTK v2.13+ Assign bank number to lsb directly
                msb = bankNumber;
                lsb = 0;

                // Add the bank select MSB
                trackChunk.Events.Insert(
                    0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)msb
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // Add the bank select LSB
                trackChunk.Events.Insert(
                    1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)lsb
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // PATCH/PROGRAM/PRESET
                // Add the program change event for the preset
                trackChunk.Events.Insert(
                    2, new ProgramChangeEvent((SevenBitNumber)presetNumber
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 1
                });

                /*
                Debug.Log($"SetBankAndPatchEvents → " +
                    $"CH:{channel} " +
                    $"BANK: {bankNumber} (MSB:{msb}, LSB:{lsb}) " +
                    $"PATCH:{presetNumber}"
                );*/
            }
        }

        private void SetChannel(MidiFile midiFile, int channel)
        {
            foreach (var midiEvent in midiFile.GetTrackChunks().SelectMany(chunk => chunk.Events))
            {
                if (midiEvent is ChannelEvent channelEvent)
                {
                    channelEvent.Channel = (FourBitNumber)channel;
                }
            }
        }

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

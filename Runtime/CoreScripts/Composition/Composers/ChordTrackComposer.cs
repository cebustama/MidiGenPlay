using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;   // ITimeSpan
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// Backing/chord track composer.
    /// - Voices chords via injected IChordVoicer (or simple realization if disabled)
    /// - Repeats progression to fill the part
    /// - Stamps "chd:..." meta tags
    /// - Sets bank/patch on ALL chunks and forces channel on ALL ChannelEvents
    public sealed class ChordTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly IChordVoicer _voicer;
        private readonly VoiceLeadingConfig _vl;

        public ChordTrackComposer(MidiGenPlayConfig settings, IChordVoicer voicer)
        {
            _settings = settings;
            _voicer = voicer;
            _vl = settings != null ? settings.voiceLeading : null;
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var instrument = (MIDIInstrumentSO)cfg.Instrument;
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                       ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (_settings?.logGenerator == true)
            {
                Debug.Log($"<color=green>[ChordTrackComposer]</color> part='{part.Name}' " +
                          $"inst='{instrument?.InstrumentName}' bpm={bpm} ch={channel} " +
                          $"progression='{prog?.displayName ?? "(null)"}' evts={prog?.events?.Count ?? 0}");
            }

            if (prog == null || prog.events == null || prog.events.Count == 0)
                return new MidiFile();

            // Grid info
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int partTotalSteps = Mathf.Max(1, part.Measures) * stepsPerMeasure;
            int patternMeasures = Mathf.Max(1, prog.measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int numRepeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            // degree + quality → chord pcs
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            if (_settings?.logGenerator == true)
            {
                Debug.Log($"<color=yellow>[ChordTrack] Tonality: {part.Tonality} over {part.RootNote}  " +
                          $"Scale notes (C4..B7 window): [{string.Join(", ", scaleNames)}]</color>");
            }

            var chordMarkers = new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            var pb = new PatternBuilder();

            // Choose voicer
            var voicer = ctx?.ChordVoicer ?? _voicer;
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int repeatStepOffset = repeat * patternTotalSteps;

                foreach (var e in prog.events)
                {
                    var degreeRoot = scaleNames[(int)e.degree];
                    var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                    var playable =
                        (_vl != null && _vl.enableVoiceLeading && voicer != null)
                        ? voicer.VoiceChord(chordPcs, instrument, lastVoicing, _vl)
                        : RealizeChordSimple(chordPcs, instrument, ctx?.rng);

                    lastVoicing = playable;

                    var rn = ToRomanRich(e.degree, e.quality);
                    var sym = GetChordSymbol(degreeRoot, e.quality);
                    int degIdx = ((int)e.degree) + 1;
                    string q = e.quality.ToString();

                    int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                    double startBeats = (double)startStepAbs / stepsPerBeat;
                    double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                    var startTime = MusicalTimeSpan.Quarter.Multiply(startBeats);
                    var duration = MusicalTimeSpan.Quarter.Multiply(durBeats);

                    pb.MoveToTime(startTime);
                    pb.Chord(playable, duration, (SevenBitNumber)Mathf.Clamp(e.velocity, 0, 127));

                    chordMarkers.Add((startTime, rn, sym, degIdx, q));
                }
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            // Chord tags
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);

            // Bank/Patch on ALL chunks + force channel on ALL ChannelEvents
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings != null && _settings.logGenerator)
            {
                var chunks = file.GetTrackChunks().Count();
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[ChordTrackComposer] tracks={chunks} notes={notes} lastTick={lastTick}");
            }

            return file;
        }

        private static void StampChordMarkers(
            MidiFile file,
            TempoMap tempoMap,
            List<(ITimeSpan when, string roman, string symbol, int deg, string quality)> markers,
            int channel,
            bool verbose)
        {
            if (markers == null || markers.Count == 0) return;
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null) return;

            using var mgr = chunk.ManageTimedEvents();
            foreach (var cm in markers)
            {
                long tick = TimeConverter.ConvertFrom(cm.when, tempoMap);
                var txt = $"chd:{channel}:{cm.roman}:{cm.symbol}:{cm.deg}:{cm.quality}";
                mgr.Objects.Add(new TimedEvent(new TextEvent(txt), tick));
                //if (verbose) Debug.Log($"[ChordTrackComposer] tag @tick={tick} '{txt}'");
            }
        }

        private static IReadOnlyList<DryWetMidiNote> RealizeChordSimple(
            NoteName[] pcs, MIDIInstrumentSO inst, System.Random rng = null)
        {
            // Legacy simple realization: root-position within instrument range
            int minOct = inst.octaveMin - 1;
            int maxOct = inst.octaveMax - 1;

            int startOct = (rng != null)
                ? rng.Next(minOct, maxOct + 1)
                : UnityEngine.Random.Range(minOct, maxOct + 1);

            return pcs.Select(nn => DryWetMidiNote.Get(nn, startOct))
                      .Select(n => DryWetMidiNote.Get(n.NoteName, Mathf.Clamp(n.Octave, minOct, maxOct)))
                      .ToArray();
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[ChordTrackComposer] Instrument bank is not numeric: '{inst.BankName}'");
                bank = 0; // fallback to 0 like old behavior if parse failed
            }

            foreach (var chunk in file.GetTrackChunks())
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                // CC0 Bank Select MSB
                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // CC32 Bank Select LSB
                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // Program Change. Keep tiny DeltaTime after bank to ensure ordering.
                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }
    }
}

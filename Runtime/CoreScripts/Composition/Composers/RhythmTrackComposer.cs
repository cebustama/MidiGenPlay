using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay.Composition;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Rhythm/drum track composer. Supports grid + legacy piano-roll paths.
    /// Mirrors MidiGenerator.GenerateRhythmTrackWithPattern / Grid / Legacy behavior including bank/patch/channel.
    public sealed class RhythmTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;

        public RhythmTrackComposer(MidiGenPlayConfig settings) => _settings = settings;

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var kit = (MIDIPercussionInstrumentSO)cfg.PercussionInstrument;
            var data = cfg.Parameters?.Pattern as DrumPatternData;

            if (data == null || kit == null)
            {
                Debug.LogWarning("[DrumTrackComposer] Missing pattern or percussion instrument.");
                return new MidiFile();
            }

            // Choose path (grid or legacy)
            bool hasGrid =
                data.lanes != null && data.lanes.Count > 0 &&
                data.lanes.Exists(l => l.steps != null && l.steps.Count > 0);

            var file = hasGrid
                ? ComposeFromGrid(kit, data, bpm, part.TimeSignature, part.Measures, channel)
                : ComposeFromLegacy(kit, data, bpm, part.TimeSignature, part.Measures, channel);

            if (_settings != null && _settings.logGenerator)
            {
                var chunks = file.GetTrackChunks().Count();
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                  .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[DrumTrackComposer] tracks={chunks} notes={notes} lastTick={lastTick}");
            }

            return file;
        }

        private static MidiFile ComposeFromGrid(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData data,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int partMeasures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, data.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int patternMeasures = Mathf.Max(1, data.measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int partTotalSteps = Mathf.Max(1, partMeasures) * stepsPerMeasure;
            int repeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var stepDur = MusicalTimeSpan.Quarter.Multiply(1.0 / stepsPerBeat);
            var pb = new PatternBuilder().MoveToStart();

            // snapshot lanes → (instrument, velocity, step indices[])
            var lanes = data.SnapshotAsIndices();

            for (int r = 0; r < repeats; r++)
            {
                int stepOffset = r * patternTotalSteps;

                foreach (var lane in lanes)
                {
                    if (!kit.TryGetMappedNote(lane.instrument, out var note))
                    {
                        Debug.LogWarning($"[DrumTrackComposer] No mapped note for {lane.instrument}");
                        continue;
                    }

                    var vel = (SevenBitNumber)Mathf.Clamp(lane.velocity, 1, 127);

                    foreach (var s in lane.stepIndices)
                    {
                        int sAbs = stepOffset + s;
                        double beatsFromStart = (double)sAbs / stepsPerBeat;

                        var when = MusicalTimeSpan.Quarter.Multiply(beatsFromStart);
                        pb.MoveToTime(when);
                        pb.Note(note, stepDur, vel);
                    }
                }
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            StampBankAndPatch(file, kit, channel);
            SetAllNotesChannel(file, channel);
            return file;
        }

        private static MidiFile ComposeFromLegacy(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData patternData,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int measures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            // Expand piano-roll pattern by mapping symbols to concrete notes.
            var processedLines = new Dictionary<string, string>();
            var lines = (patternData.pianoRollPattern ?? "").Split('\n');

            foreach (string line in lines)
            {
                int i1 = line.IndexOf('{');
                int i2 = line.IndexOf('}');
                if (i1 == -1 || i2 == -1 || i2 <= i1) continue;

                string tag = line.Substring(i1, i2 - i1 + 1);
                string symbol = tag.Trim('{', '}');

                var gm = GeneralMidiPercussion.AcousticBassDrum;
                bool foundMap = false;

                foreach (var mapping in patternData.drumMappings)
                    if (mapping.drumSymbol == symbol) { gm = mapping.drumNote; foundMap = true; break; }

                if (!foundMap) { Debug.LogWarning($"[DrumTrackComposer] No map for symbol {symbol}"); continue; }

                if (!kit.TryGetMappedNote(gm, out var note))
                {
                    Debug.LogWarning($"[DrumTrackComposer] No MIDI note for {gm}");
                    continue;
                }

                string noteStr = $"{note.NoteName}{note.Octave}";
                string processed = line.Replace(tag, noteStr);
                processedLines[noteStr] = processed;
            }

            string pianoRoll = string.Join("\n", processedLines.Values);

            var pb = new PatternBuilder().MoveToStart();
            int patMeasures = Mathf.Max(1, patternData.measures);
            int repeats = Mathf.CeilToInt((float)measures / patMeasures);

            for (int r = 0; r < repeats; r++)
            {
                int measureOffset = r * patMeasures * beatsPerBar;
                pb.MoveToTime(MusicalTimeSpan.Quarter * measureOffset);
                pb.PianoRoll(pianoRoll);
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            StampBankAndPatch(file, kit, channel);
            SetAllNotesChannel(file, channel);
            return file;
        }

        private static void SetAllNotesChannel(MidiFile file, int channel)
        {
            foreach (var n in file.GetNotes()) n.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIPercussionInstrumentSO kit, int channel)
        {
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                chunk = new TrackChunk();
                file.Chunks.Add(chunk);
            }

            if (int.TryParse(kit.BankName, out var bank))
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });
            }

            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)kit.PatchIndex)
            { Channel = (FourBitNumber)channel, DeltaTime = 1 });
        }
    }
}

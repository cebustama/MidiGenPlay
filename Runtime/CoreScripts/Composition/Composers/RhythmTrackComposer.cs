using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;

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
            var cardCfg = cfg.Parameters?.Style as RhythmCardConfigSO;
            var data = cardCfg?.patternOverride as DrumPatternData
                ?? cfg.Parameters?.Pattern as DrumPatternData;

            var recipe = cardCfg?.recipeOverride ?? cfg.Parameters?.RhythmRecipe;

            // fully procedural drums when there's no pattern
            if ((data == null) && kit != null)
            {
                if (_settings?.logGenerator == true)
                    Debug.Log("[RhythmTrackComposer] Procedural rhythm (no DrumPatternData).");

                // choose a style for this meter; fall back to generic if none
                RhythmStyleRegistry.RegisterDefaults(); // safe no-op if already called

                IRhythmStyle style = null;
                if (cardCfg != null && !string.IsNullOrEmpty(cardCfg.styleIdOverride))
                {
                    style = RhythmStyleRegistry.ChooseById(
                        part.TimeSignature,
                        cardCfg.styleIdOverride);
                }

                if (style == null)
                {
                    style = RhythmStyleRegistry.Choose(
                        part.TimeSignature,
                        recipe,
                        (min, max) => UnityEngine.Random.Range(min, max));
                }

                MidiFile pFile = (style != null)
                    ? style.Compose(kit, bpm, part.Measures, channel, recipe)
                    : ComposeProcedural(kit, bpm, part.TimeSignature, part.Measures, channel);

                // Post-process here so all styles remain pure
                StampBankAndPatch(pFile, kit, channel);
                ForceAllChannel(pFile, channel);

                if (_settings?.logGenerator == true)
                {
                    var chunks = pFile.GetTrackChunks().Count();
                    var notes = pFile.GetNotes().Count();
                    var last = pFile.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                    .Select(te => te.Time).DefaultIfEmpty(0).Max();
                    Debug.Log($"[RhythmTrackComposer] (procedural) tracks={chunks} " +
                        $"notes={notes} lastTick={last}");
                }
                return pFile;
            }

            if (data == null || kit == null)
            {
                Debug.LogWarning("[RhythmTrackComposer] Missing pattern or percussion instrument.");
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
                Debug.Log($"[RhythmTrackComposer] tracks={chunks} " +
                    $"notes={notes} lastTick={lastTick}");
            }

            return file;
        }

        // simple, musical, meter-aware grid
        private static MidiFile ComposeProcedural(
            MIDIPercussionInstrumentSO kit,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int measures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Mappings (with safe fallbacks)
            bool hasKick = 
                kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            bool hasSnare = 
                kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);
            bool hasCHH = 
                kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out var chh);
            bool hasOHH = 
                kit.TryGetMappedNote(GeneralMidiPercussion.OpenHiHat, out var ohh);

            // Backbeat rule: 1 = kick, ceil(beats/2)+1 = snare (clamped to beatsPerBar)
            int backbeat = Mathf.Min(beatsPerBar, Mathf.CeilToInt(beatsPerBar / 2f) + 1);

            for (int m = 0; m < Mathf.Max(1, measures); m++)
            {
                for (int b0 = 0; b0 < beatsPerBar; b0++)
                {
                    int beat = b0 + 1; // 1-based beat number
                    double whenBeats = m * beatsPerBar + b0;

                    // Hi-hat logic:
                    // - CHH on every beat EXCEPT the final beat
                    if (beat != beatsPerBar && hasCHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(chh, MusicalTimeSpan.Quarter, (SevenBitNumber)60);
                    }
                    if (beat == beatsPerBar && hasOHH)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(ohh, MusicalTimeSpan.Quarter, (SevenBitNumber)80);
                    }
                    else if (beat == beatsPerBar && hasCHH)
                    {
                        // if no OHH mapping, keep CHH on the last beat
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(chh, MusicalTimeSpan.Quarter, (SevenBitNumber)60);
                    }

                    // Kick on beat 1
                    if (beat == 1 && hasKick)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(kick, MusicalTimeSpan.Quarter, (SevenBitNumber)96);
                    }

                    // Snare on the computed backbeat
                    if (beat == backbeat && hasSnare)
                    {
                        pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(whenBeats));
                        pb.Note(snare, MusicalTimeSpan.Quarter, (SevenBitNumber)96);
                    }
                }
            }

            var file = pb.Build().ToFile(tempoMap);

            // Match other paths: stamp kit bank/patch & force channel
            StampBankAndPatch(file, kit, channel);
            ForceAllChannel(file, channel);
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

            int patternMeasures = Mathf.Max(1, data.Measures);
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
                        Debug.LogWarning($"[RhythmTrackComposer] No mapped note for {lane.instrument}");
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
            ForceAllChannel(file, channel);
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

                if (!foundMap) { Debug.LogWarning($"[RhythmTrackComposer] No map for symbol {symbol}"); continue; }

                if (!kit.TryGetMappedNote(gm, out var note))
                {
                    Debug.LogWarning($"[RhythmTrackComposer] No MIDI note for {gm}");
                    continue;
                }

                string noteStr = $"{note.NoteName}{note.Octave}";
                string processed = line.Replace(tag, noteStr);
                processedLines[noteStr] = processed;
            }

            string pianoRoll = string.Join("\n", processedLines.Values);

            var pb = new PatternBuilder().MoveToStart();
            int patMeasures = Mathf.Max(1, patternData.Measures);
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
            ForceAllChannel(file, channel);
            return file;
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(
            MidiFile file, MIDIPercussionInstrumentSO kit, int channel)
        {
            if (!int.TryParse(kit.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[RhythmTrackComposer] " +
                    $"Percussion bank is not numeric: '{kit.BankName}', falling back to 0");
                bank = 0;
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

                // Program Change (small delta to keep ordering stable)
                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)kit.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }
    }
}

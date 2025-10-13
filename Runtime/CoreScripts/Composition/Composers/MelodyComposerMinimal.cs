
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using static MidiGenPlay.MusicTheory.MusicTheory;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{

    public sealed class MelodyComposerMinimal : ITrackComposer
    {
        private readonly MelodicLeadingConfig _cfg;
        private readonly IMelodyStrategy _strategy;

        public MelodyComposerMinimal(MelodicLeadingConfig cfg, IMelodyStrategy strategy)
        {
            _cfg = cfg;
            _strategy = strategy;
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            UnityEngine.Debug.Log($"[MelodyComposer] Start part='{part.Name}' " +
                $"inst='{trackCfg.Instrument?.InstrumentName}' " +
                $"role={trackCfg.Role} bpm={bpm} ch={channel}");

            var inst = trackCfg.Instrument;
            var prog =
                ctx.GetProgressionForPart?.Invoke(part) ??
                (trackCfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                UnityEngine.Debug.LogWarning("[MelodyComposer] No chord progression found → empty melody.");
                return ComposeRandomTestMelody(part, trackCfg, bpm, channel, ctx);
            }

            if (prog.events == null || prog.events.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[MelodyComposer] Progression has 0 events → empty melody.");
                return ComposeRandomTestMelody(part, trackCfg, bpm, channel, ctx);
            }

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

            var pb = new PatternBuilder();

            var ts = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int stepsPerBeat = Math.Max(1, prog.subdivisions);

            // running “last” for voice-leading
            Note last = null;

            // precompute scale → degree root
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            foreach (var ce in prog.events.OrderBy(e => e.startStep))
            {
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordNames = GetChordNoteNames(degreeRoot, ce.quality); // pitch classes of chord

                var nn = _strategy.PickNext(chordNames, last, inst, _cfg, ctx.rng);
                if (nn == null) continue;

                // grid steps -> beats
                double startBeats = ce.startStep / (double)stepsPerBeat;
                double lenBeats = Math.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(startBeats));
                pb.Note(nn, MusicalTimeSpan.Quarter.Multiply(lenBeats), (SevenBitNumber)90);

                last = nn;
            }

            var file = pb.Build().ToFile(tempoMap);

            // stamp channel + bank/patch so it sounds as-expected
            SetAllNotesChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            var notes = file.GetNotes().Count();
            var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                            .Select(te => te.Time).DefaultIfEmpty(0).Max();

            UnityEngine.Debug.Log($"[MelodyComposer] Done notes={notes} lastTick={lastTick}");

            return file;
        }

        private static void SetAllNotesChannel(MidiFile file, int channel)
        {
            foreach (var n in file.GetNotes())
                n.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                chunk = new TrackChunk();
                file.Chunks.Add(chunk);
            }

            // Match MidiGenerator.SetBankAndPatchEvents
            // (MPTK expects MSB = bankNumber, LSB = 0)
            if (int.TryParse(inst.BankName, out var bankNumber))
            {
                var msb = (SevenBitNumber)bankNumber;
                var lsb = (SevenBitNumber)0;

                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });
            }

            // small non-zero delta after bank to guarantee ordering
            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
            { Channel = (FourBitNumber)channel, DeltaTime = 1 });
        }

        private MidiFile ComposeRandomTestMelody(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = trackCfg.Instrument;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // Use the part’s scale (feels musical) but randomize notes
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var pcs = GetNotesFromScale(scale, part.RootNote, 4, 4).Select(n => n.NoteName).ToArray();

            var ts = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = ts.BeatsPerMeasure;
            int totalBeats = Math.Max(1, part.Measures) * beatsPerBar;

            var rng = ctx?.rng ?? new System.Random();

            // One quarter-note per beat (simple & loud)
            for (int beat = 0; beat < totalBeats; beat++)
            {
                var pc = pcs[rng.Next(0, pcs.Length)];
                int oct = Math.Clamp(inst.octaveMin + rng.Next(0, (inst.octaveMax - inst.octaveMin + 1)),
                                     inst.octaveMin, inst.octaveMax);
                var note = Note.Get(pc, oct);

                pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(beat));
                pb.Note(note, MusicalTimeSpan.Quarter, (SevenBitNumber)110);
            }

            var file = pb.Build().ToFile(tempoMap);
            SetAllNotesChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            UnityEngine.Debug.Log($"[MelodyComposer] (Fallback) random notes={file.GetNotes().Count()}");
            return file;
        }
    }
}

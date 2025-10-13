
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            Debug.Log($"[MelodyComposer] Start part='{part.Name}' " +
                $"inst='{trackCfg.Instrument?.InstrumentName}' " +
                $"role={trackCfg.Role} bpm={bpm} ch={channel}");

            var inst = trackCfg.Instrument;
            var prog =
                ctx.GetProgressionForPart?.Invoke(part) ??
                (trackCfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                Debug.LogWarning("[MelodyComposer] No progression → fallback random test melody.");
                return ComposeRandomTestMelody(part, trackCfg, bpm, channel, ctx);
            }

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            int stepsPerBeat = Math.Max(1, prog.subdivisions);

            // voice-leading memory for strategy
            Note last = null;

            // scale cache
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            // local PRNG for rhythm choices if needed (e.g., alternating counts)
            var rng = ctx?.rng ?? new System.Random();

            var chordIndex = 0;
            foreach (var ce in prog.events.OrderBy(e => e.startStep))
            {
                // chord → pitch classes
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordNames = GetChordNoteNames(degreeRoot, ce.quality);

                // chord timing in beats
                double startBeats = ce.startStep / (double)stepsPerBeat;
                double chordBeats = Math.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                int n = 
                    ChooseNoteCountPerChord(_cfg, ctx?.rng ?? new System.Random(), chordIndex);

                // figure out placement grid and per-note length
                var placements = EnumeratePlacements(startBeats, chordBeats, n, _cfg.lengthMode, _cfg.fixedSubdivisions);

                foreach (var (when, len) in placements)
                {
                    var nn = _strategy.PickNext(chordNames, last, inst, _cfg, rng);
                    if (nn == null) continue;

                    pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(when));
                    pb.Note(nn, MusicalTimeSpan.Quarter.Multiply(len), (SevenBitNumber)90);

                    last = nn;
                }

                chordIndex++;
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

        /// <summary>
        /// Return note placements for a chord: (whenInBeats, lenInBeats).
        /// FillChord → split chord evenly among n notes.
        /// FixedSubdivisions → split into K slots; place n notes across those slots (evenly spread).
        /// TieAcrossChanges → one sustained note across the whole chord (for now).
        /// </summary>
        private static IEnumerable<(double when, double len)> EnumeratePlacements(
            double startBeats,
            double chordBeats,
            int n,
            MelodicLeadingConfig.LengthMode mode,
            int fixedSubdivisions)
        {
            switch (mode)
            {
                case MelodicLeadingConfig.LengthMode.FillChord:
                    {
                        // n notes, equally split across chordBeats
                        double seg = chordBeats / n;
                        for (int i = 0; i < n; i++)
                            yield return (startBeats + i * seg, seg);
                        yield break;
                    }

                case MelodicLeadingConfig.LengthMode.FixedSubdivisions:
                    {
                        int slots = Math.Max(1, fixedSubdivisions);
                        double seg = chordBeats / slots;

                        if (n >= slots)
                        {
                            // one note per slot (or more, but clamp to slots)
                            for (int s = 0; s < slots; s++)
                                yield return (startBeats + s * seg, seg);
                        }
                        else
                        {
                            // spread n notes as evenly as possible across slots
                            // e.g., slots=4, n=2 -> place at slots 0 and 2
                            foreach (var s in EvenlySpacedIndices(n, slots))
                                yield return (startBeats + s * seg, seg);
                        }
                        yield break;
                    }

                case MelodicLeadingConfig.LengthMode.TieAcrossChanges:
                default:
                    {
                        // one sustained note for the whole chord duration (extend later to actually tie)
                        yield return (startBeats, chordBeats);
                        yield break;
                    }
            }
        }

        /// <summary>
        /// Evenly spread `count` indices in [0..slots-1], monotonic & deterministic.
        /// </summary>
        private static IEnumerable<int> EvenlySpacedIndices(int count, int slots)
        {
            if (count <= 1) { yield return 0; yield break; }
            if (count >= slots) { for (int i = 0; i < slots; i++) yield return i; yield break; }

            // Place at floor(i*(slots-1)/(count-1)), i = 0..count-1
            for (int i = 0; i < count; i++)
            {
                int idx = (int)Math.Floor(i * (slots - 1.0) / (count - 1.0));
                yield return idx;
            }
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

            Debug.Log($"[MelodyComposer] (Fallback) random notes={file.GetNotes().Count()}");
            return file;
        }

        private static int ChooseNoteCountPerChord(MelodicLeadingConfig cfg, System.Random rng, int chordIndex)
        {
            switch (cfg.noteDensityMode)
            {
                case MelodicLeadingConfig.NoteDensityMode.Fixed:
                    return Mathf.Max(1, cfg.notesPerChord);

                case MelodicLeadingConfig.NoteDensityMode.RangeRandom:
                    {
                        int lo = Mathf.Max(
                            1, Mathf.Min(cfg.minNotesPerChord, cfg.maxNotesPerChord));

                        int hi = Mathf.Max(
                            lo, Mathf.Max(cfg.minNotesPerChord, cfg.maxNotesPerChord));

                        return rng.Next(lo, hi + 1);
                    }

                case MelodicLeadingConfig.NoteDensityMode.Alternate:
                    // e.g., alternate min/max per chord
                    return (chordIndex % 2 == 0) ? Mathf.Max(1, cfg.minNotesPerChord)
                                                 : Mathf.Max(1, cfg.maxNotesPerChord);

                default:
                    return Mathf.Max(1, cfg.notesPerChord);
            }
        }
    }
}

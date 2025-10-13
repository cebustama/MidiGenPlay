using System;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// Minimal bass composer: one sustained note per chord event.
    /// Mode: root-only (default) or random chord tone (constructor flag).
    public sealed class BassTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly bool _randomChordTone;

        public BassTrackComposer(MidiGenPlayConfig settings, bool randomChordTone = false)
        {
            _settings = settings;
            _randomChordTone = randomChordTone;
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = (MIDIInstrumentSO)cfg.Instrument;
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                       ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null || prog.events == null || prog.events.Count == 0)
                return new MidiFile();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // scale → degree root names (for fast lookup)
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // bass register: favor lower region
            int minOct = Mathf.Max(0, inst.octaveMin - 1);
            int maxOct = Mathf.Max(minOct, inst.octaveMin + 1); // keep it low and stable

            var rng = ctx?.rng ?? new System.Random();

            foreach (var ce in prog.events.OrderBy(e => e.startStep))
            {
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, ce.quality);

                // choose pitch class: root or random chord tone
                var pc = _randomChordTone
                    ? chordPcs[rng.Next(0, chordPcs.Length)]
                    : chordPcs[0]; // root is first

                // pick octave in a narrow low band
                int oct = rng.Next(minOct, maxOct + 1);
                var note = NoteTheory.Get(pc, oct);

                // timings
                double startBeats = ce.startStep / (double)stepsPerBeat;
                double lenBeats = Math.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                pb.MoveToTime(MusicalTimeSpan.Quarter.Multiply(startBeats));
                pb.Note(note, MusicalTimeSpan.Quarter.Multiply(lenBeats), (SevenBitNumber)ce.velocity);
            }

            var file = pb.Build().ToFile(tempoMap);

            // channel + program (match other composers)
            ForceAllChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            if (_settings?.logGenerator == true)
            {
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[BassTrackComposer] notes={notes} lastTick={lastTick}");
            }

            return file;
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                chunk = new TrackChunk();
                file.Chunks.Add(chunk);
            }

            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[BassTrackComposer] Instrument bank is not numeric: '{inst.BankName}', fallback to 0");
                bank = 0;
            }

            chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)bank)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)0)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
            { Channel = (FourBitNumber)channel, DeltaTime = 1 });
        }
    }
}

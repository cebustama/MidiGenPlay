namespace MidiGenPlay.Composition
{
    using UnityEngine;
    using Melanchall.DryWetMidi.Common;
    using Melanchall.DryWetMidi.Composing;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using Melanchall.DryWetMidi.MusicTheory;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using static MidiGenPlay.MusicTheory.MusicTheory;
    using Note = Melanchall.DryWetMidi.MusicTheory.Note;

    public sealed class MelodyComposerMinimal : ITrackComposer
    {
        private readonly MelodicLeadingConfig _cfg;

        public MelodyComposerMinimal(MelodicLeadingConfig cfg) => _cfg = cfg;

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = cfg.Instrument;
            var prog =
                ctx.GetProgressionForPart?.Invoke(part) ??
                (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                UnityEngine.Debug.Log("<color=red>No chord progression fround in Melody Composer</color>");
                return new MidiFile();
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

                var nn = NearestAllowedTone(chordNames, last, inst, _cfg);
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

            return file;
        }

        private static ChordProgressionData ResolveProgression(
            SongConfig.PartConfig part, SongConfig.PartConfig.TrackConfig cfg)
        {
            // 1) explicit on this track?
            var prog = cfg.Parameters?.Pattern as ChordProgressionData;
            if (prog != null) return prog;

            // 2) fallback to the part's backing track
            prog = part.Tracks
                .FirstOrDefault(t => t.Role == TrackRole.Backing)
                ?.Parameters?.Pattern as ChordProgressionData;

            if (prog == null)
                throw new InvalidOperationException(
                    "[MelodyComposer] No chord progression found for this part. " +
                    "Add a Backing track or assign a progression to this track’s Parameters.Pattern.");

            return prog;
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

            if (int.TryParse(inst.BankName, out var bank))
            {
                var msb = (SevenBitNumber)(bank / 128);
                var lsb = (SevenBitNumber)(bank % 128);
                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb) { Channel = (FourBitNumber)channel });
                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb) { Channel = (FourBitNumber)channel });
            }

            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
            { Channel = (FourBitNumber)channel });
        }

        private Note NearestAllowedTone(
            NoteName[] chordPcs,
            Note last,
            MIDIInstrumentSO inst,
            MelodicLeadingConfig cfg)
        {
            // all chord tones across the instrument range
            var cand = new List<Note>();
            for (int oct = inst.octaveMin; oct <= inst.octaveMax; oct++)
                foreach (var pc in chordPcs)
                    cand.Add(Note.Get(pc, oct));

            if (cand.Count == 0) return null;

            if (last == null)
            {
                // start near the instrument center
                int centerOct = (inst.octaveMin + inst.octaveMax) / 2;
                return cand.OrderBy(n => Math.Abs(Semis(n) - Semis(Note.Get(n.NoteName, centerOct))))
                           .First();
            }

            // nearest to last within maxStep; otherwise nearest overall
            var ordered = cand.OrderBy(n => Math.Abs(Semis(n) - Semis(last)));
            foreach (var n in ordered)
                if (Math.Abs(Semis(n) - Semis(last)) <= cfg.maxStepSemitones)
                    return n;

            return ordered.First();
        }

        private static int Semis(Note n) => (int)(byte)n.NoteNumber;
    }
}

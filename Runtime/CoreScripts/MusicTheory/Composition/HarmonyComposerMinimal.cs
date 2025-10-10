namespace MidiGenPlay.Composition
{
    using Melanchall.DryWetMidi.Common;
    using Melanchall.DryWetMidi.Composing;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using Melanchall.DryWetMidi.MusicTheory;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using static MidiGenPlay.MusicTheory.MusicTheory;
    using Note = Melanchall.DryWetMidi.Interaction.Note;
    using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;

    public sealed class HarmonyComposerMinimal : ITrackComposer
    {
        private readonly HarmonicLeadingConfig _cfg;
        public HarmonyComposerMinimal(HarmonicLeadingConfig cfg) => _cfg = cfg;

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = (MIDIInstrumentSO)cfg.Instrument;
            var prog =
                ctx.GetProgressionForPart?.Invoke(part) ??
                (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                UnityEngine.Debug.Log("<color=red>No chord progression fround in Harmony Composer</color>");
                return new MidiFile();
            }

            var melodyFile = ctx.GetTrackForRole(part, TrackRole.Melody)
                           ?? ctx.GetTrackForRole(part, TrackRole.Lead);      // be generous
            if (melodyFile == null) return new MidiFile();

            // read melody notes
            List<Note> leader = ctx.ExtractMonophonicNotes(melodyFile);
            if (leader == null || leader.Count == 0) return new MidiFile();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // Scale cache for degree root → chord pcs
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scalePcs = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            NoteTheory lastHarmony = null;

            foreach (var mn in leader)
            {
                // which chord event covers this melody note?
                var ce = ctx.FindChordEventAt(prog, tempoMap, part.TimeSignature, mn.Time);
                if (ce == null) continue;

                var degreeRoot = scalePcs[(int)ce.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, ce.quality);

                var harmony = PickHarmonyNote(chordPcs, mn, inst, lastHarmony, _cfg);
                if (harmony == null) continue;

                var when = TimeConverter.ConvertTo<MusicalTimeSpan>(mn.Time, tempoMap);
                var len = TimeConverter.ConvertTo<MusicalTimeSpan>(mn.Length, tempoMap);

                pb.MoveToTime(when);
                pb.Note(harmony, len, (SevenBitNumber)85);

                lastHarmony = harmony;
            }

            return pb.Build().ToFile(tempoMap);
        }

        private static int Semis(NoteTheory n) => (int)(byte)n.NoteNumber;

        private NoteTheory PickHarmonyNote(
            NoteName[] chordPcs,
            Note melodyNote,                    // Interaction.Note
            MIDIInstrumentSO inst,
            NoteTheory lastHarmony,
            HarmonicLeadingConfig cfg)
        {
            var melodyAbs = NoteTheory.Get(melodyNote.NoteName, melodyNote.Octave);

            // all chord tones across instrument range except exact unison with melody
            var cand = from oct in Enumerable.Range(inst.octaveMin, inst.octaveMax - inst.octaveMin + 1)
                       from pc in chordPcs
                       let n = NoteTheory.Get(pc, oct)
                       where !(n.NoteName == melodyAbs.NoteName && n.Octave == melodyAbs.Octave)
                       select n;

            if (!cand.Any()) return null;

            // voice-leading: prefer nearest to previous harmony
            IEnumerable<NoteTheory> ordered =
                (lastHarmony == null)
                ? cand.OrderBy(n => Mathf.Abs(Semis(n) - Semis(melodyAbs))) // start near melody
                : cand.OrderBy(n => Mathf.Abs(Semis(n) - Semis(lastHarmony)));

            // interval “style”
            NoteTheory pick = null;
            switch (cfg.relation)
            {
                case HarmonicLeadingConfig.HarmonyRelation.NextChordToneAbove:
                    pick = ordered.FirstOrDefault(n => Semis(n) > Semis(melodyAbs))
                        ?? ordered.First();
                    break;

                case HarmonicLeadingConfig.HarmonyRelation.NextChordToneBelow:
                    pick = ordered.Reverse().FirstOrDefault(n => Semis(n) < Semis(melodyAbs))
                        ?? ordered.First();
                    break;

                case HarmonicLeadingConfig.HarmonyRelation.FixedIntervalSemitones:
                    {
                        int target = Semis(melodyAbs) + cfg.intervalSemitones;
                        pick = cand.OrderBy(n => Mathf.Abs(Semis(n) - target)).FirstOrDefault();
                        break;
                    }

                default: // NearestDifferentChordTone (to last harmony / melody start)
                    pick = ordered.First();
                    break;
            }

            if (pick == null) return null;

            // keep a comfortable distance from the melody if requested
            int d = Mathf.Abs(Semis(pick) - Semis(melodyAbs));
            if (d < cfg.minDistanceFromMelody)
            {
                var pushed = cand
                    .OrderBy(n => Mathf.Abs((Semis(n) - Semis(melodyAbs)) - cfg.minDistanceFromMelody))
                    .FirstOrDefault();
                if (pushed != null) pick = pushed;
            }
            if (d > cfg.maxDistanceFromMelody) return null;

            return pick;
        }
    }
}

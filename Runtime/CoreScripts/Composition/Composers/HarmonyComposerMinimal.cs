using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteInteraction = Melanchall.DryWetMidi.Interaction.Note;
using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    public sealed class HarmonyComposerMinimal : ITrackComposer
    {
        private readonly HarmonicLeadingConfig _cfg;
        private readonly IHarmonyStrategy _strategy;

        public HarmonyComposerMinimal(HarmonicLeadingConfig cfg, IHarmonyStrategy strategy)
        {
            _cfg = cfg;
            _strategy = strategy;
        }

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
            List<NoteInteraction> leader = ctx.ExtractMonophonicNotes(melodyFile);
            if (leader == null || leader.Count == 0) return new MidiFile();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // Scale cache for degree root → chord pcs
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scalePcs = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            NoteTheory lastHarmony = null;

            // melody notes
            foreach (var mn in leader)
            {
                // which chord event covers this melody note?
                var ce = ctx.FindChordEventAt(prog, tempoMap, part.TimeSignature, mn.Time);
                if (ce == null) continue;

                var degreeRoot = scalePcs[(int)ce.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, ce.quality);

                var theoryMelody = NoteTheory.Get(mn.NoteName, mn.Octave);
                var harmony = _strategy.PickHarmony(
                    chordPcs, theoryMelody, lastHarmony, inst, _cfg, ctx.rng);

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
    }
}

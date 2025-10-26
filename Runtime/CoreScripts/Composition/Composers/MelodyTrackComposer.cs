using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    public class MelodyTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly MelodicLeadingConfig _cfg;
        private readonly IMelodyStrategy _strategy;

        private struct Placement
        {
            public double whenBeat;
            public double durBeats;
            public Placement(double w, double d) { whenBeat = w; durBeats = d; }
        }

        public MelodyTrackComposer(
            MidiGenPlayConfig settings,
            MelodicLeadingConfig cfg,
            IMelodyStrategy strategy = null)
        {
            _settings = settings;
            _cfg = cfg;
            // Fallback to something sane if caller doesn't inject a strategy yet
            _strategy = strategy ?? new NearestChordToneMelodyStrategy();
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm, int channel,
            MidiGenerator.GenContext ctx)
        {
            var instrument = (MIDIInstrumentSO)cfg.Instrument;
            if (instrument == null)
            {
                Debug.LogWarning("[MelodyTrackComposer] Missing melodic instrument.");
                return new MidiFile();
            }

            // Get an existing progression or build+store one
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                    ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                var rng = ctx?.rng ?? new System.Random();
                prog = ChordTrackComposer.BuildProceduralProgression(part, rng);
                ctx?.SetProgressionForPart?.Invoke(part, prog);
                if (_settings?.logGenerator == true)
                    Debug.Log($"[MelodyTrackComposer] " +
                        $"Built & cached procedural progression for part '{part.Name}'.");
            }
            else if (_settings?.logGenerator == true)
            {
                var seq = string.Join("  ", prog.events.Select(e => ToRomanRich(e.degree, e.quality)));
                Debug.Log($"[MelodyTrackComposer] Using cached/authored progression: {seq}");
            }

            //return ComposePerBeatMelody(instrument, bpm, part, prog, channel, ctx);
            return ComposeMelodyFromProgression(instrument, bpm, part, prog, channel, ctx);
        }

        /// <summary>
        /// Generates one note per beat using the active chord's tones.
        /// - Repeats the progression to fill the part.
        /// - Picks a chord tone per beat (root, third, fifth cycling).
        /// </summary>
        private MidiFile ComposePerBeatMelody(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            ChordProgressionData prog,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var rng = ctx?.rng ?? new System.Random();

            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int patternMeasures = Mathf.Max(1, prog.measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;

            int partMeasures = Mathf.Max(1, part.Measures);
            int totalBeats = partMeasures * beatsPerBar;

            var pb = new PatternBuilder().MoveToStart();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

            // Scale degree roots for mapping event.degree -> concrete root note
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = 
                GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            // pre-sort events by startStep to speed up simple lookups
            var evts = (prog.events ?? 
                new List<ChordProgressionData.ChordEvent>()).OrderBy(e => e.startStep).ToList();
            if (evts.Count == 0) return new MidiFile(); // safety

            int toneCycle = 0;

            for (int b = 0; b < totalBeats; b++)
            {
                int stepInCycle = ((b * stepsPerBeat) % Mathf.Max(1, patternTotalSteps));

                var e = FindEventAtStep(evts, stepInCycle, patternTotalSteps);
                if (e == null) continue;

                // Get chord notes
                var degreeRoot = scaleNames[(int)e.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                // pick one tone in a simple cycle (root/3rd/5th[/7th])
                var pickIdx = toneCycle % chordPcs.Length;
                toneCycle++;

                // map to a playable octave range for the melodic instrument
                var note = ChooseMelodicRegister(chordPcs[pickIdx], instrument, rng);

                double whenBeats = b; // one note exactly on each beat
                var when = MusicalTimeSpan.Quarter.Multiply(whenBeats);
                var dur = MusicalTimeSpan.Quarter; // 1 beat

                pb.MoveToTime(when);
                // TODO: Randomize velocity within range
                pb.Note(note, dur, (SevenBitNumber)96);
            }

            var pattern = pb.Build();
            var file = pattern.ToFile(tempoMap);

            // match the other composers: set patch/bank & force channel
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, last) = Inspect(file);
                Debug.Log($"[MelodyTrackComposer] tracks={tracks} notes={notes} lastTick={last}");
            }

            return file;
        }

        private MidiFile ComposeMelodyFromProgression(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            ChordProgressionData prog,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var rng = ctx?.rng ?? new System.Random();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // timing info
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            // NOTE: events in prog are given in "steps" (startStep, lengthSteps)

            // prepare scale mapping so we can turn degree -> root note name
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames =
                GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            // Sort chord events in time
            var evts = (prog.events ?? new List<ChordProgressionData.ChordEvent>())
                        .OrderBy(e => e.startStep)
                        .ToList();

            if (evts.Count == 0)
                return new MidiFile();

            Melanchall.DryWetMidi.MusicTheory.Note lastMelody = null;
            int chordIndex = 0;

            foreach (var ce in evts)
            {
                // 1. Chord info
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordPitchClasses = GetChordNoteNames(degreeRoot, ce.quality); // NoteName[]

                // 2. Chord timing in beats
                double chordStartBeats = ce.startStep / (double)stepsPerBeat;
                double chordBeats = Mathf.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                // 3. How many melody notes do we want over this chord span?
                int noteCount = ChooseNoteCountForSpan(chordBeats, beatsPerBar, chordIndex, rng);

                // 4. Where do those notes land and how long do they last?
                var placements = EnumeratePlacements(chordStartBeats, chordBeats, noteCount);

                // 5. For each planned note: ask the melody strategy for pitch, then write
                foreach (var pl in placements)
                {
                    var scalePCs = scaleNames;
                    var picked = _strategy.PickNext(
                        chordPitchClasses,
                        scalePCs,
                        lastMelody,
                        instrument,
                        _cfg,
                        rng,
                        new PhraseState() // TODO: implement melodic phrases
                    );

                    if (picked == null)
                    {
                        // rest
                        lastMelody = null;
                        continue;
                    }

                    // Build timespans
                    var startTs = MusicalTimeSpan.Quarter.Multiply(pl.whenBeat);
                    var durTs = MusicalTimeSpan.Quarter.Multiply(pl.durBeats);

                    pb.MoveToTime(startTs);
                    // TODO: velocity shaping / accents per phrase
                    pb.Note(picked, durTs, (SevenBitNumber)96);

                    lastMelody = picked;
                }

                chordIndex++;
            }

            var file = pb.Build().ToFile(tempoMap);

            // Stamp program/bank and set the MIDI channel
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, lastTick) = Inspect(file);
                Debug.Log($"[MelodyTrackComposer] tracks={tracks} notes={notes} lastTick={lastTick}");
            }

            return file;
        }

        private int ChooseNoteCountForSpan(
            double beatsInThisChord,
            int beatsPerBar,
            int chordIndex,
            System.Random rng)
        {
            // Base density: notes per bar (what designer hears in their head)
            float basePerBar;

            switch (_cfg.noteDensityMode)
            {
                case MelodicLeadingConfig.NoteDensityMode.Fixed:
                    basePerBar = _cfg.notesPerChord;
                    break;

                case MelodicLeadingConfig.NoteDensityMode.RangeRandom:
                    basePerBar = rng.Next(_cfg.minNotesPerChord, _cfg.maxNotesPerChord + 1);
                    break;

                case MelodicLeadingConfig.NoteDensityMode.Alternate:
                    // simple even/odd flip: busy / sparse / busy / sparse...
                    bool busy = (chordIndex % 2 == 0);
                    basePerBar = busy ? _cfg.maxNotesPerChord : _cfg.minNotesPerChord;
                    break;

                default:
                    basePerBar = _cfg.notesPerChord;
                    break;
            }

            // Scale note count by how long THIS chord lasts, in bars.
            double barsSpanned = beatsInThisChord / (double)beatsPerBar;
            double rawCount = basePerBar * barsSpanned;

            // clamp to at least 1 so we always play something
            int finalCount = Mathf.Max(1, Mathf.RoundToInt((float)rawCount));
            return finalCount;
        }

        private List<Placement> EnumeratePlacements(
            double chordStartBeat,
            double chordBeats,
            int noteCount)
        {
            var list = new List<Placement>(noteCount);

            // guard
            if (noteCount <= 0)
                return list;

            switch (_cfg.lengthMode)
            {
                case MelodicLeadingConfig.LengthMode.TieAcrossChanges:
                    // One long note covering the whole chord span.
                    list.Add(new Placement(chordStartBeat, chordBeats));
                    break;

                case MelodicLeadingConfig.LengthMode.FixedSubdivisions:
                    {
                        // Force a grid (e.g. 8ths, 16ths).
                        // We'll just emit "noteCount" slots evenly across chordBeats,
                        // but each slot's duration snaps to (chordBeats / fixedSubdivisions)
                        double step = chordBeats / _cfg.fixedSubdivisions;
                        for (int i = 0; i < noteCount; i++)
                        {
                            double w = chordStartBeat + i * step;
                            double d = step;
                            list.Add(new Placement(w, d));
                        }
                        break;
                    }

                case MelodicLeadingConfig.LengthMode.FillChord:
                default:
                    {
                        // Evenly slice the chord duration among noteCount
                        double slot = chordBeats / noteCount;
                        for (int i = 0; i < noteCount; i++)
                        {
                            double w = chordStartBeat + i * slot;
                            double d = slot;
                            list.Add(new Placement(w, d));
                        }
                        break;
                    }
            }

            return list;
        }

        /// <summary>
        /// Finds the progression event active at a given absolute step within the repeating pattern.
        /// Chooses the last event whose startStep ≤ stepInCycle; falls back to first if none.
        /// </summary>
        private static ChordProgressionData.ChordEvent FindEventAtStep(
            List<ChordProgressionData.ChordEvent> orderedEvents, int stepInCycle, int patternTotalSteps)
        {
            ChordProgressionData.ChordEvent candidate = null;
            int best = int.MinValue;

            foreach (var ev in orderedEvents)
            {
                int s = Mathf.Clamp(ev.startStep, 0, Mathf.Max(0, patternTotalSteps - 1));
                if (s <= stepInCycle && s >= best) { candidate = ev; best = s; }
            }
            return candidate ?? orderedEvents.FirstOrDefault();
        }

        /// <summary>
        /// Picks a playable note for the melody from the instrument range,
        /// centered roughly in the instrument's mid register.
        /// </summary>
        private static DryWetMidiNote ChooseMelodicRegister(
            NoteName nn, MIDIInstrumentSO inst, System.Random rng)
        {
            int minOct = inst.octaveMin - 1;
            int maxOct = inst.octaveMax - 1;
            int mid = Mathf.Clamp((minOct + maxOct) / 2, minOct, maxOct);

            // small random wander around mid
            int oct = Mathf.Clamp(mid + (rng.Next(-1, 2)), minOct, maxOct);
            return DryWetMidiNote.Get(nn, oct);
        }

        /// <summary>Forces every ChannelEvent to a specific channel (0..15).</summary>
        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        /// <summary>Stamps Bank Select + Program Change at the head of each track chunk.</summary>
        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[MelodyTrackComposer] Instrument bank is not numeric: '{inst.BankName}'");
                bank = 0;
            }

            foreach (var chunk in file.GetTrackChunks())
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        /// <summary>Light inspection for logs.</summary>
        private static (int tracks, int notes, long lastTick) Inspect(MidiFile f)
        {
            if (f == null) return (0, 0, 0);
            var chunks = f.GetTrackChunks().ToList();
            var notes = f.GetNotes().Count();
            var last = chunks.SelectMany(c => c.GetTimedEvents())
                              .Select(te => te.Time).DefaultIfEmpty(0).Max();
            return (chunks.Count, notes, last);
        }
    }
}
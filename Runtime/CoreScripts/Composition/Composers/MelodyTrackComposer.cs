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

        private PhrasePlanner _phrasePlanner;
        private PhrasePlanner.PhraseMemory _phraseMemory; // running memory

        public MelodyTrackComposer(
            MidiGenPlayConfig settings,
            MelodicLeadingConfig cfg,
            IMelodyStrategy strategy = null)
        {
            _settings = settings;
            _cfg = cfg;
            _strategy = strategy ?? new NearestChordToneMelodyStrategy();

            _phraseMemory = new PhrasePlanner.PhraseMemory
            {
                lastPhraseId = -1,
                lastContourDir = 0,
                lastPhraseEndNote = null
            };

            _phrasePlanner = new PhrasePlanner(_cfg, _phraseMemory);
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

            // Tonality profile for this part (Dorian, Mixolydian, Pentatonic, etc.)
            var profile = ctx?.GetTonalityProfileForPart?.Invoke(part);

            // Get an existing progression or build+store one
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                    ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                var rng = ctx?.rng ?? new System.Random();
                prog = ChordTrackComposer.BuildProceduralProgression(part, ctx, rng);
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
            return ComposeMelodyFromProgression(instrument, bpm, part, prog, channel, ctx, profile);
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
            MidiGenerator.GenContext ctx,
            TonalityProfileSO profile)
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
            MidiGenerator.GenContext ctx,
            TonalityProfileSO profile)
        {
            var rng = ctx?.rng ?? new System.Random();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // === Global timing info for the part ===
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            // Progression timing resolution:
            // startStep / lengthSteps are in "steps", where stepsPerBeat = prog.subdivisions.
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // === Tonal / scale info ===
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7)
                                .Select(n => n.NoteName)
                                .ToArray();

            // NoteName -> 0..6 scale degree index in this tonality
            var degreeLookup = new Dictionary<NoteName, int>();
            for (int i = 0; i < scaleNames.Length && i < 7; i++)
            {
                if (!degreeLookup.ContainsKey(scaleNames[i]))
                    degreeLookup[scaleNames[i]] = i;
            }

            // === Sort chord progression events in time ===
            var evts = (prog.events ?? new List<ChordProgressionData.ChordEvent>())
                        .OrderBy(e => e.startStep)
                        .ToList();

            if (evts.Count == 0)
                return new MidiFile(); // nothing to do

            // remember the previous melodic note across phrases
            DryWetMidiNote lastMelody = null;

            // walk chord-by-chord (currently 1 phrase per chord span)
            for (int chordIndex = 0; chordIndex < evts.Count; chordIndex++)
            {
                var ce = evts[chordIndex];

                // --- 1. Harmonic context: chord pitch classes for this span ---
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordPitchClasses = GetChordNoteNames(degreeRoot, ce.quality);

                // --- 2. Convert chord event timing (steps) -> beats ---
                double chordStartBeats = ce.startStep / (double)stepsPerBeat;
                double chordBeats = Mathf.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                // --- 3. Ask the PhrasePlanner to create expressive phrase slots
                // (bursts, sustains, rests, accents, etc.) for this chord span ---
                var phraseSlots = _phrasePlanner.PlanPhraseSlotsForSpan(
                    chordStartBeats,
                    chordBeats,
                    beatsPerBar,
                    chordIndex,
                    rng,
                    profile
                );

                // track info about this phrase so we can fill in PhraseState:
                DryWetMidiNote phraseFirstNote = null;
                DryWetMidiNote phrasePeakNote = null;

                // --- 4. For each planned slot, pick pitch (or rest) and emit MIDI ---
                foreach (var slot in phraseSlots)
                {
                    // Rest slot: don't ask the strategy, just "breathe"
                    if (!slot.playNote)
                    {
                        lastMelody = null;
                        continue;
                    }

                    // Build the PhraseState for this slot (what the strategy sees):
                    var phraseState = new PhrasePlanner.PhraseState
                    {
                        PhraseIndex = slot.phraseId,
                        NoteIndexInPhrase = slot.slotIndexInPhrase,
                        TotalNotesInPhrase = slot.totalSlotsInPhrase,
                        IsStrongBeat = slot.isAccent,      // accent/downbeat hint
                        IsPhraseEnd = slot.isPhraseEnd,   // cadence / "land it"
                        DesiredContourDir = slot.desiredContourDir, // +1 up / -1 down, etc.

                        PhraseStartNote = phraseFirstNote,
                        PhrasePeakNote = phrasePeakNote
                    };

                    // Ask melodic strategy for the actual pitch to play here.
                    // (May return null for "no note", but usually not.)
                    var picked = _strategy.PickNext(
                        chordPitchClasses,      // chord context
                        scaleNames,             // scale context
                        degreeLookup,           // scale degree lookup
                        lastMelody,             // what we played last
                        instrument,             // range, etc.
                        _cfg,                   // player personality
                        rng,                    // deterministic random
                        phraseState,            // phrase context
                        profile                 // tonality profile (Dorian, etc.)
                    );

                    if (picked == null)
                    {
                        // Strategy chose to rest anyway
                        lastMelody = null;
                        continue;
                    }

                    // Track phrase-first and phrase-peak for future slots this phrase
                    if (phraseFirstNote == null)
                        phraseFirstNote = picked;

                    if (phrasePeakNote == null ||
                        MelodyStrategyCommon.Semis(picked) >
                        MelodyStrategyCommon.Semis(phrasePeakNote))
                    {
                        phrasePeakNote = picked;
                    }

                    // How loud should we play this slot?
                    int velocityVal = ChooseVelocityForSlot(slot, picked, profile, rng);
                    var velocity7 = (SevenBitNumber)Mathf.Clamp(velocityVal, 1, 127);

                    // Convert beats -> musical time spans
                    var startTs = MusicalTimeSpan.Quarter.Multiply(slot.whenBeat);
                    var durTs = MusicalTimeSpan.Quarter.Multiply(slot.durBeats);

                    // Emit note
                    pb.MoveToTime(startTs);
                    pb.Note(picked, durTs, velocity7);

                    // remember for next slot
                    lastMelody = picked;

                    // (Optional next step: if slot.isPhraseEnd, we could update
                    // internal memory for call/response, e.g. lastPhraseEndNote = picked.)
                    if (slot.isPhraseEnd)
                    {
                        _phraseMemory.lastPhraseEndNote = picked;
                    }
                }

                // pull the planner's structural memory (phraseId, contourDir)
                _phraseMemory = _phrasePlanner.GetMemory();
                // inject the melodic memory we know (lastPhraseEndNote)
                _phraseMemory.lastPhraseEndNote = lastMelody;
                // push it back so planner can see it next chord
                _phrasePlanner.SetMemory(_phraseMemory);
            }

            // --- 5. Finalize MIDI file ---
            var file = pb.Build().ToFile(tempoMap);

            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, lastTick) = Inspect(file);
                Debug.Log($"[MelodyTrackComposer] " +
                    $"tracks={tracks} notes={notes} lastTick={lastTick}");
            }

            return file;
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

        private int ChooseVelocityForSlot(
            PhrasePlanner.PhraseSlot slot,
            Melanchall.DryWetMidi.MusicTheory.Note picked,
            TonalityProfileSO profile,
            System.Random rng)
        {
            // Basic first-pass:
            // - if accent: use accentVel range
            // - else if phrase end: phraseEndVel range
            // - else normalVel range

            int RandomBetween(int lo, int hi)
            {
                if (hi < lo) (lo, hi) = (hi, lo);
                return lo + rng.Next(hi - lo + 1);
            }

            if (slot.isAccent)
            {
                return RandomBetween(_cfg.accentVelMin, _cfg.accentVelMax);
            }

            if (slot.isPhraseEnd)
            {
                return RandomBetween(_cfg.phraseEndVelMin, _cfg.phraseEndVelMax);
            }

            return RandomBetween(_cfg.normalVelMin, _cfg.normalVelMax);
        }
    }
}
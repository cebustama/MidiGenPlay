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
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    public class MelodyTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly MelodicLeadingConfig _cfg;
        private readonly IMelodyStrategy _strategy;

        private IMelodyStrategy _baseStrategy;      // replaces direct _strategy usage
        private MelodicStyleSO _melodicStyle;       // optional, from MelodyCardConfigSO

        private PhrasePlanner _phrasePlanner;
        private PhrasePlanner.PhraseMemory _phraseMemory; // running memory

        public MelodyTrackComposer(
            MidiGenPlayConfig settings,
            MelodicLeadingConfig cfg,
            IMelodyStrategy strategy = null)
        {
            _settings = settings;
            _cfg = cfg;
            _baseStrategy = strategy ?? new NearestChordToneMelodyStrategy();

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
            return ComposeMelodyFromProgression(instrument, bpm, part, cfg, prog, channel, ctx, profile);
        }

        private MidiFile ComposeMelodyFromProgression(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            ChordProgressionData prog,
            int channel,
            MidiGenerator.GenContext ctx,
            TonalityProfileSO profile)
        {
            // -------------------------------
            // 0) Resolve authoring from card
            // -------------------------------
            // Typed base in TrackParameters.Style; melody bundle derives from it.
            var cardCfg = trackCfg?.Parameters?.Style as MelodyCardConfigSO; // may be null
            // Build effective leading (leading override wins; else constructor _cfg)
            var effectiveLeading = cardCfg?.leadingOverride != null ? cardCfg.leadingOverride : _cfg;

            // If palette override is provided, clone the leading (avoid mutating assets) and swap its palette.
            if (cardCfg?.phrasePaletteOverride != null)
            {
                var clone = ScriptableObject.Instantiate(effectiveLeading);
                clone.phrasePalette = cardCfg.phrasePaletteOverride;
                effectiveLeading = clone;
            }

            // Resolve leading allowed degrees
            HashSet<int> allowedDegrees = null;
            if (effectiveLeading != null &&
                effectiveLeading.restrictToScaleDegrees &&
                effectiveLeading.allowedScaleDegrees != null &&
                effectiveLeading.allowedScaleDegrees.Count > 0)
            {
                allowedDegrees = new HashSet<int>(effectiveLeading.allowedScaleDegrees);
            }

            // Make the planner use the effective leading (so the palette is honored)
            _phrasePlanner = new PhrasePlanner(effectiveLeading, _phraseMemory);

            // Per-part base strategy from style (if any); otherwise keep constructor default
            var baseForThisPart = _baseStrategy;
            _melodicStyle = cardCfg?.style;
            if (_melodicStyle != null)
                baseForThisPart = ResolveStrategy(_melodicStyle.baseStrategy);

            if (_settings?.logGenerator == true)
                Debug.Log($"<color=yellow>[MelodyTrackComposer] Using melodic style '{_melodicStyle.name}' " +
                          $"baseStrategy={_melodicStyle.baseStrategy}</color>");

            // RNG, timeline, etc.
            var rng = ctx?.rng ?? new System.Random();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // === Global timing info for the part ===
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            // Progression timing resolution:
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // === Tonal / scale info ===
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7)
                                .Select(n => n.NoteName)
                                .ToArray();


            int partMeasures = Mathf.Max(1, part.Measures);
            double partTotalBeats = partMeasures * beatsPerBar;
            var tonicPc = scaleNames[0]; // degree-0 pitch class (tonic)

            // NoteName -> 0..6 scale degree index in this tonality
            var degreeLookup = new Dictionary<NoteName, int>();
            for (int i = 0; i < scaleNames.Length && i < 7; i++)
                if (!degreeLookup.ContainsKey(scaleNames[i])) degreeLookup[scaleNames[i]] = i;

            // === Sort chord progression events in time ===
            var evts = (prog.events ?? new List<ChordProgressionData.ChordEvent>())
                        .OrderBy(e => e.startStep)
                        .ToList();
            if (evts.Count == 0) return new MidiFile(); // nothing to do

            DryWetMidiNote lastMelody = null;
            var capturedMelody = new List<MidiGenerator.GuideNote>();

            // Local helper: weighted pick of per-phrase directives
            WeightedPhraseDirective PickDirective(
                List<WeightedPhraseDirective> list, System.Random r)
            {
                if (list == null || list.Count == 0) return null;
                float sum = 0f; foreach (var w in list) sum += Mathf.Max(0f, w.weight);
                float target = (float)(r.NextDouble() * Mathf.Max(sum, 0.0001f));
                foreach (var w in list) 
                { 
                    target -= Mathf.Max(0f, w.weight); 
                    if (target <= 0f) return w; 
                }
                return list[list.Count - 1];
            }

            DryWetMidiNote intervalAnchor = null;
            bool intervalAnchorSet = false;

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

                // --- 3. Ask the PhrasePlanner to create expressive phrase slots ---
                var phraseSlots = _phrasePlanner.PlanPhraseSlotsForSpan(
                    chordStartBeats, chordBeats, beatsPerBar, chordIndex, rng, profile);

                // Choose the active strategy for THIS phrase (style can override per-phrase)
                IMelodyStrategy activeStrategy = baseForThisPart;
                var contour = ContourConstraint.None;
                RepeatLastNotesDirective repeat = null;
                InterPhraseIntervalDirective interval = null;

                if (_melodicStyle != null && _melodicStyle.usePerPhraseOverrides
                    && _melodicStyle.perPhraseDirectives != null
                    && _melodicStyle.perPhraseDirectives.Count > 0)
                {
                    var pickedDir = PickDirective(_melodicStyle.perPhraseDirectives, rng);
                    if (pickedDir != null)
                    {
                        if (pickedDir.overrideStrategy.HasValue)
                            activeStrategy = ResolveStrategy(pickedDir.overrideStrategy.Value);

                        contour = pickedDir.contour;
                        repeat = pickedDir.repeatDirective;
                        interval = pickedDir.intervalDirective;
                    }

                    if (pickedDir != null && _settings?.logGenerator == true)
                    {
                        Debug.Log(
                            $"<color=yellow>[MelodyTrackComposer] Phrase dir | chordIdx={chordIndex} " +
                            $"overrideStrategy={pickedDir.overrideStrategy} " +
                            $"contour={pickedDir.contour} " +
                            $"intervalEnabled={pickedDir.intervalDirective?.enabled} " +
                            $"semisPerPhrase={pickedDir.intervalDirective?.semitonesPerPhrase}</color>");
                    }
                }

                // Wrap in constraint decorator only if needed
                if (contour != ContourConstraint.None || repeat != null)
                    activeStrategy = 
                        new ConstrainedMelodyStrategy(
                            activeStrategy, contour, repeat);

                // track info about this phrase so we can fill in PhraseState:
                DryWetMidiNote phraseFirstNote = null;
                DryWetMidiNote phrasePeakNote = null;

                bool IsFinalSlotOfPart(PhrasePlanner.PhraseSlot s)
                {
                    bool lastChord = (chordIndex == evts.Count - 1);
                    bool lastSlot = (s.slotIndexInPhrase == s.totalSlotsInPhrase - 1);
                    return lastChord && lastSlot;
                }

                // --- 4. For each planned slot, pick pitch (or rest) and emit MIDI ---
                foreach (var slot in phraseSlots)
                {
                    if (!slot.playNote) { lastMelody = null; continue; }

                    // Build the PhraseState for this slot (what the strategy sees):
                    var phraseState = new PhrasePlanner.PhraseState
                    {
                        PhraseIndex = slot.phraseId,
                        NoteIndexInPhrase = slot.slotIndexInPhrase,
                        TotalNotesInPhrase = slot.totalSlotsInPhrase,
                        IsStrongBeat = slot.isAccent,
                        IsPhraseEnd = slot.isPhraseEnd,
                        DesiredContourDir = slot.desiredContourDir,
                        PhraseStartNote = phraseFirstNote,
                        PhrasePeakNote = phrasePeakNote
                    };

                    // Part-level context
                    var partState = new MelodyPartState
                    {
                        ChordIndex = chordIndex,
                        TotalChords = evts.Count,
                        IsFinalSlotOfPart = IsFinalSlotOfPart(slot),
                        PartStartBeat = 0.0,
                        PartTotalBeats = partTotalBeats,
                        TonicPC = tonicPc
                    };

                    // Ask the chosen strategy for the pitch here
                    // (Style → Strategy → Constraints)
                    var picked = activeStrategy.PickNext(
                        chordPitchClasses,
                        scaleNames,
                        degreeLookup,
                        lastMelody,
                        instrument,
                        effectiveLeading,
                        rng,
                        phraseState,
                        profile,
                        partState,
                        allowedDegrees);

                    // Apply inter-phrase interval pattern here
                    picked = ApplyIntervalDirective(
                        picked,
                        interval,
                        partState,
                        instrument,
                        ref intervalAnchor,
                        ref intervalAnchorSet,
                        chordIndex, rng);

                    if (picked == null) { lastMelody = null; continue; }

                    // Track phrase-first and phrase-peak for future slots this phrase
                    if (phraseFirstNote == null) phraseFirstNote = picked;
                    if (phrasePeakNote == null ||
                        MelodyStrategyCommon.Semis(picked) > 
                        MelodyStrategyCommon.Semis(phrasePeakNote))
                    {
                        phrasePeakNote = picked;
                    }

                    int velocityVal = 
                        ChooseVelocityForSlot(slot, picked, profile, rng, effectiveLeading);
                    var velocity7 = (SevenBitNumber)Mathf.Clamp(velocityVal, 1, 127);

                    // --- DEBUG: per-note inspection ---
                    if (_settings?.logGenerator == true)
                    {
                        // Is this note a chord tone?
                        bool isChordTone = chordPitchClasses.Contains(picked.NoteName);

                        // Scale degree (0..6) if available, -1 otherwise
                        int degreeIdx = -1;
                        if (degreeLookup != null && degreeLookup.TryGetValue(picked.NoteName, out var idx))
                            degreeIdx = idx;

                        // Step in semitones from previous melody note (0 if none)
                        int stepFromLast = 0;
                        if (lastMelody != null)
                        {
                            stepFromLast = Mathf.Abs(
                                MelodyStrategyCommon.Semis(picked) -
                                MelodyStrategyCommon.Semis(lastMelody));
                        }

                        Debug.Log(
                            $"[MelodySlot] chord={chordIndex} " +
                            $"beat={slot.whenBeat:F2} dur={slot.durBeats:F2} " +
                            $"note={picked} degree={degreeIdx} " +
                            $"chordTone={isChordTone} step={stepFromLast} " +
                            $"vel={velocityVal} accent={slot.isAccent} " +
                            $"phraseEnd={slot.isPhraseEnd} " +
                            $"phraseId={slot.phraseId} slot={slot.slotIndexInPhrase}/{slot.totalSlotsInPhrase}");
                    }
                    // --- end DEBUG ---

                    var startTs = MusicalTimeSpan.Quarter.Multiply(slot.whenBeat);
                    var durTs = MusicalTimeSpan.Quarter.Multiply(slot.durBeats);

                    pb.MoveToTime(startTs);
                    pb.Note(picked, durTs, velocity7);

                    capturedMelody.Add(new MidiGenerator.GuideNote
                    {
                        startBeats = slot.whenBeat,
                        durBeats = slot.durBeats,
                        note = picked
                    });

                    lastMelody = picked;
                    if (slot.isPhraseEnd)
                        _phraseMemory.lastPhraseEndNote = picked;
                }

                // sync planner memory across chords
                _phraseMemory = _phrasePlanner.GetMemory();
                _phraseMemory.lastPhraseEndNote = lastMelody;
                _phrasePlanner.SetMemory(_phraseMemory);
            }

            // --- finalize MIDI ---
            var file = pb.Build().ToFile(tempoMap);
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, lastTick) = Inspect(file);
                Debug.Log($"[MelodyTrackComposer] " +
                    $"tracks={tracks} notes={notes} lastTick={lastTick}");
            }

            // cache guide notes for other systems
            if (ctx != null && ctx.SetMelodyForPartMusician != null)
            {
                var musicianId = trackCfg?.MusicianId;
                ctx.SetMelodyForPartMusician(part, musicianId, capturedMelody);
            }

            return file;
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
            System.Random rng,
            MelodicLeadingConfig leading)
        {
            // Use effectiveLeading if provided, otherwise fall back to constructor _cfg
            var cfg = leading != null ? leading : _cfg;

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
                return RandomBetween(cfg.accentVelMin, cfg.accentVelMax);
            }

            if (slot.isPhraseEnd)
            {
                return RandomBetween(cfg.phraseEndVelMin, cfg.phraseEndVelMax);
            }

            return RandomBetween(cfg.normalVelMin, cfg.normalVelMax);
        }

        // Helpers
        private IMelodyStrategy ResolveStrategy(MelodyStrategyId id)
        {
            switch (id)
            {
                case MelodyStrategyId.NearestChordTone: return new NearestChordToneMelodyStrategy();
                case MelodyStrategyId.AscendingClimb: return new AscendingClimbMelodyStrategy();
                case MelodyStrategyId.ScaleFlow:
                default: return new ScaleFlowMelodyStrategy();
            }
        }

        private WeightedPhraseDirective WeightedPickDirective(
            System.Collections.Generic.List<WeightedPhraseDirective> list, System.Random rng)
        {
            if (list == null || list.Count == 0) return null;
            float sum = 0f; foreach (var w in list) sum += Mathf.Max(0f, w.weight);
            float r = (float)(rng.NextDouble() * Mathf.Max(0.0001f, sum));
            foreach (var w in list) { r -= Mathf.Max(0f, w.weight); if (r <= 0f) return w; }
            return list[list.Count - 1];
        }

        private DryWetMidiNote ApplyIntervalDirective(
            DryWetMidiNote candidate,
            InterPhraseIntervalDirective interval,
            MelodyPartState part,
            MIDIInstrumentSO instrument,
            ref DryWetMidiNote anchor,
            ref bool anchorSet,
            int chordIndex,
            System.Random rng)
        {
            if (candidate == null || interval == null || !interval.enabled)
                return candidate;

            // 1. Establish anchor once (first note of the part)
            if (!anchorSet)
            {
                int dirForAnchor = (interval != null && interval.baseDirection != 0)
                                    ? Math.Sign(interval.baseDirection) : 1;

                switch (interval?.anchorStart ?? AnchorStartMode.AutoFromDirection)
                {
                    case AnchorStartMode.Lowest:
                        anchor = DryWetMidiNote.Get(candidate.NoteName, instrument.octaveMin);
                        break;

                    case AnchorStartMode.Highest:
                        anchor = DryWetMidiNote.Get(candidate.NoteName, instrument.octaveMax);
                        break;

                    case AnchorStartMode.Mid:
                        int mid = (instrument.octaveMin + instrument.octaveMax) / 2;
                        anchor = DryWetMidiNote.Get(candidate.NoteName, mid);
                        break;

                    case AnchorStartMode.Random:   // NEW
                        {
                            int low = instrument.octaveMin;
                            int high = instrument.octaveMax + 1; // .Next upper bound is exclusive
                            int rndOct = (rng != null) ? rng.Next(low, high)
                                                       : UnityEngine.Random.Range(low, high);
                            anchor = DryWetMidiNote.Get(candidate.NoteName, rndOct);
                            break;
                        }

                    case AnchorStartMode.AutoFromDirection:
                    default:
                        anchor = MelodyStrategyCommon.SnapToEdgeOctave(candidate, instrument, dirForAnchor);
                        break;
                }

                anchorSet = true;

                if (_settings?.logGenerator == true)
                    Debug.Log($"[MelodyTrackComposer] Anchor set → {anchor} (mode={interval?.anchorStart})");
            }

            // 2. Compute step index for this chord
            int stepIndex = chordIndex; // 0,1,2,... (one step per chord/event)

            int dir = interval.baseDirection;
            if (dir == 0) dir = 1;
            dir = Math.Sign(dir);

            if (interval.alternateDirection && (stepIndex % 2 == 1))
                dir = -dir;

            int semisOffset = stepIndex * interval.semitonesPerPhrase * dir;

            // 3. Apply offset from anchor
            /*var target = MelodyStrategyCommon.TransposePreservingPitchClass(
                anchor, semisOffset, instrument, true);*/

            DryWetMidiNote target;

            if (interval.lockPitchClassToAnchor)
            {
                // MODE 1: fixed pitch class (e.g., C -> C -> C, climbing)
                target = MelodyStrategyCommon.TransposePreservingPitchClass(
                    anchor, semisOffset, instrument);
            }
            else
            {
                // MODE 2: keep candidate pitch class, only move its octave
                // Use anchor's *octave* as reference, but candidate's NoteName.
                int baseOct = anchor.Octave;
                int octStep = interval.semitonesPerPhrase / 12; // assume multiples of 12 for octave motion
                int newOct = baseOct + stepIndex * octStep * dir;

                if (interval.clampToRange)
                {
                    newOct = Mathf.Clamp(newOct, instrument.octaveMin, instrument.octaveMax);
                }

                target = DryWetMidiNote.Get(candidate.NoteName, newOct);
            }

            if (interval.clampToRange && target != null)
            {
                int minOct = instrument.octaveMin;
                int maxOct = instrument.octaveMax;
                int clampedOct = Mathf.Clamp(target.Octave, minOct, maxOct);
                target = DryWetMidiNote.Get(target.NoteName, clampedOct);
            }

            // 4. Debug
            if (_settings != null && _settings.logGenerator)
            {
                Debug.Log(
                    $"[MelodyTrackComposer] Interval | chordIdx={chordIndex} " +
                    $"step={stepIndex} semisPerPhrase={interval.semitonesPerPhrase} dir={dir} " +
                    $"semisOffset={semisOffset} anchor={anchor} target={target}");
            }

            return target ?? candidate;
        }

    }
}
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay.Composition;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiChord = Melanchall.DryWetMidi.MusicTheory.Chord;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay
{
    public class MidiGenerator
    {
        private const string DebugTag = "<color=green>[MidiGenerator]</color>";

        public const int MetronomeChannel = 15;

        private MidiGenPlayConfig settings;

        public MidiGenerator(MidiGenPlayConfig config)
        {
            settings = config;
        }

        #region Generation Methods
        public MidiFile GenerateChordProgressionMidiTrackFile(
            MIDIInstrumentSO instrument,
            TrackRole role,
            Tonality tonality,
            NoteName rootNote,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int measures,
            int channel = 0,
            ChordProgressionData progressionData = null)
        {
            Debug.Log($"{DebugTag}<color=cyan> Generating Chord Progression " +
                $"using progression: {progressionData?.displayName ?? "(null)"} " +
                $"(id {progressionData?.GetInstanceID()}) | " +
                $"events={progressionData?.events?.Count ?? 0}</color>");

            Debug.Log($"{DebugTag} {DescribeScale(tonality, rootNote)}");

            // Voice Leading
            var voicer = new BasicVoiceLeadingVoicer();
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            var tsInfo = GetTimeSignatureDetails(timeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;

            // degree + quality → chord notes
            var scale = GetScaleFromTonality(tonality, rootNote);
            var scalePreview = GetNotesFromScale(scale, rootNote, 4, 7).Select(n => n.NoteName.ToString());
            Debug.Log($"{DebugTag} Part Tonality={tonality} Root={rootNote} | Scale= [{string.Join(" ", scalePreview)}]");
            var scaleNames = GetNotesFromScale(scale, rootNote, 4, 7)  // any octave; just for names
                             .Select(n => n.NoteName).ToArray();

            // collect meta markers to stamp after pattern is built
            var chordMarkers = 
                new List<(MusicalTimeSpan when, 
                string roman, string symbol, int deg, string quality)>();

            var patternBuilder = new PatternBuilder();

            if (progressionData != null && progressionData.events != null && 
                progressionData.events.Count > 0)
            {
                // Steps/measure for the PART (not the asset): part uses asset.subdivisions to snap chords
                int stepsPerBeat = Mathf.Max(1, progressionData.subdivisions);
                int stepsPerMeasure = beatsPerBar * stepsPerBeat;

                // How many steps the part spans & how many steps the pattern spans
                int partTotalSteps = Mathf.Max(1, measures) * stepsPerMeasure;
                int patternMeasures = Mathf.Max(1, progressionData.measures);
                int patternTotalSteps = patternMeasures * stepsPerMeasure;

                int numRepeats = Mathf.Max(1, Mathf.CeilToInt(
                    (float)partTotalSteps / patternTotalSteps));

                for (int repeat = 0; repeat < numRepeats; repeat++)
                {
                    Debug.Log($"{DebugTag} Repeat {repeat + 1}");

                    int repeatStepOffset = repeat * patternTotalSteps;

                    foreach (var e in progressionData.events)
                    {
                        // resolve the chord ROOT from the current scale degree in this tonality
                        NoteName degreeRoot = scaleNames[(int)e.degree];
                        // get pitch classes for this quality
                        var chordNames = GetChordNoteNames(degreeRoot, e.quality);

                        IReadOnlyList<DryWetMidiNote> playable;
                        var vl = settings?.voiceLeading;

                        if (vl != null && vl.enableVoiceLeading)
                            playable = voicer.VoiceChord(
                                chordNames, instrument, lastVoicing, vl);
                        else
                            playable = RealizeChordForInstrument(chordNames, instrument);

                        lastVoicing = playable;

                        // Debug

                        if (vl != null && settings.logGenerator)
                        {
                            int move = 0;
                            if (lastVoicing != null)
                                move = Enumerable.Range(0, Mathf.Min(
                                    lastVoicing.Count, playable.Count))
                                    .Sum(i => Mathf.Abs(
                                        BasicVoiceLeadingVoicer.Semis(lastVoicing[i]) -
                                        BasicVoiceLeadingVoicer.Semis(playable[i])));

                            Debug.Log($"{DebugTag} VL pick | movement={move} | " +
                                      $"candNotes=[{string.Join("-", playable.Select(n => $"{n.NoteName}{n.Octave}"))}]");
                        }

                        // Chord data
                        var rn = ToRomanRich(e.degree, e.quality);
                        var sym = GetChordSymbol(degreeRoot, e.quality);
                        int degIndex = ((int)e.degree) + 1;  // 1..7
                        string qName = e.quality.ToString();

                        var notesStr = 
                            string.Join("-", playable.Select(n => $"{n.NoteName}{n.Octave}"));

                        Debug.Log($"{DebugTag} step={e.startStep}, len={e.lengthSteps} " +
                            $"| {rn} ({sym}) | root={degreeRoot} | notes=[{notesStr}]");

                        // Convert step offsets to beats:
                        // 1 step = 1/stepsPerBeat beats; 1 beat = MusicalTimeSpan.Quarter
                        int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                        double startBeats = (double)startStepAbs / stepsPerBeat;
                        double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                        var startTime = MusicalTimeSpan.Quarter.Multiply(startBeats);
                        var duration = MusicalTimeSpan.Quarter.Multiply(durBeats);

                        patternBuilder.MoveToTime(startTime);
                        patternBuilder.Chord(playable, duration, 
                            (SevenBitNumber)Mathf.Clamp(e.velocity, 0, 127));

                        chordMarkers.Add((
                            (MusicalTimeSpan when, string roman, string symbol, int deg, string quality))
                            (startTime, rn, sym, degIndex, qName));
                    }
                }
            }

            // Build MIDI Pattern
            var pattern = patternBuilder.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            
            // Convert to Midi File
            var midiFile = pattern.ToFile(tempoMap);

            // --- Stamp chord meta tags at their exact ticks
            // Format: chd:<channel>:<roman>:<symbol>:<deg>:<quality>
            if (chordMarkers.Count > 0)
            {
                var chunk = midiFile.GetTrackChunks().FirstOrDefault();
                if (chunk != null)
                {
                    using (var mgr = chunk.ManageTimedEvents())
                    {
                        foreach (var cm in chordMarkers)
                        {
                            long tick = TimeConverter.ConvertFrom(cm.when, tempoMap); // absolute tick
                            var txt = $"chd:{channel}:{cm.roman}:{cm.symbol}:{cm.deg}:{cm.quality}";
                            mgr.Objects.Add(new TimedEvent(new TextEvent(txt), tick));

                            if (settings != null && settings.logGenerator)
                                Debug.Log($"[MidiGenerator] chd tag @tick={tick} '{txt}'");
                        }
                    }
                }
            }

            // Patch/bank/channel
            SetBankAndPatchEvents(midiFile, int.Parse(instrument.BankName), 
                instrument.PatchIndex, channel);
            SetChannel(midiFile, channel);

            return midiFile;
        }


        public MidiFile GenerateRhythmTrackWithPattern(
            MIDIPercussionInstrumentSO percussionInstrument,
            DrumPatternData patternData,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int measures,
            int channel = 9)
        {
            Debug.Log($"<color=cyan>Generating Drum Track: " +
                $"{patternData.displayName} with {percussionInstrument.InstrumentName}</color>");

            // Extract time signature details
            var timeSignatureInfo = GetTimeSignatureDetails(timeSignature, bpm);
            int beatsPerBar = timeSignatureInfo.BeatsPerMeasure;

            // Extract the lines of the PianoRoll pattern
            string[] patternLines = patternData.pianoRollPattern.Split('\n');

            // Determine the number of times to repeat the pattern
            int patternLength = patternData.measures;
            int numRepeats = Mathf.CeilToInt((float)measures / patternLength);

            // Dictionary for processed mappings
            Dictionary<string, string> processedLines = new Dictionary<string, string>();

            // Initialize pattern builder
            PatternBuilder patternBuilder = new PatternBuilder();
            patternBuilder.MoveToStart();

            // Process each line of the pattern
            foreach (string line in patternLines)
            {
                int firstBracket = line.IndexOf('{');
                int lastBracket = line.IndexOf('}');

                if (firstBracket == -1 || lastBracket == -1 || lastBracket <= firstBracket)
                {
                    Debug.LogWarning($"Skipping invalid pattern line: {line}");
                    continue;
                }

                // Extract tag (e.g., {x}, {o}, {O})
                string tag = line.Substring(firstBracket, lastBracket - firstBracket + 1);
                string drumSymbol = tag.Trim('{', '}');

                // Find the corresponding GeneralMidiPercussion type
                GeneralMidiPercussion percussionType = GeneralMidiPercussion.AcousticBassDrum;
                bool foundMapping = false;

                foreach (var mapping in patternData.drumMappings)
                {
                    if (mapping.drumSymbol == drumSymbol)
                    {
                        percussionType = mapping.drumNote;
                        foundMapping = true;
                        break;
                    }
                }

                if (!foundMapping)
                {
                    Debug.LogWarning($"No drum mapping found for symbol: {drumSymbol}");
                    continue;
                }

                // Get the mapped MIDI note
                if (!percussionInstrument.TryGetMappedNote(percussionType, out DryWetMidiNote mappedNote))
                {
                    Debug.LogWarning($"No mapped MIDI note found for {percussionType}");
                    continue;
                }

                // Convert note to string format
                string noteString = $"{mappedNote.NoteName}{mappedNote.Octave}";

                // Replace the drum symbol in the pattern with the mapped note name
                string processedLine = line.Replace(tag, noteString);
                processedLines[noteString] = processedLine;
            }

            // Convert processed lines into the final PianoRoll string
            string processedPattern = string.Join("\n", processedLines.Values);
            Debug.Log($"Generated PianoRoll Pattern:\n{processedPattern}");

            // Repeat the pattern for the required number of measures
            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int measureOffset = repeat * patternLength * beatsPerBar;
                patternBuilder.MoveToTime(MusicalTimeSpan.Quarter * measureOffset);
                patternBuilder.PianoRoll(processedPattern);
            }

            // Convert to MIDI
            Pattern pattern = patternBuilder.Build();
            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // Set bank and patch events
            int bankNumber = int.Parse(percussionInstrument.BankName);
            int presetNumber = percussionInstrument.PatchIndex;
            SetBankAndPatchEvents(midiFile, bankNumber, presetNumber, channel);
            SetChannel(midiFile, channel);

            return midiFile;
        }


        public MidiFile GenerateMelodyTrackWithPattern(
            MIDIInstrumentSO instrument,
            MelodyPatternData melodyPattern,
            Tonality tonality,
            NoteName rootNote,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int measures = 4,
            int channel = 0)
        {
            if (melodyPattern == null) Debug.LogError("EMPTY MELODY PATTERN");
            if (instrument == null) Debug.LogError("EMPTY INSTRUMENT");

            Debug.Log($"<color=green>Generating Melody Track: " +
                $"{melodyPattern.displayName} for {instrument.InstrumentName}</color> " +
                $"Tonality: {tonality.ToString()}");

            // 1️⃣ Retrieve scale and time signature details
            var scale = GetScaleFromTonality(tonality, rootNote);
            var timeSignatureInfo = GetTimeSignatureDetails(timeSignature, bpm);
            int beatsPerBar = timeSignatureInfo.BeatsPerMeasure;

            // Determine the number of times to repeat the melody pattern
            int patternLength = melodyPattern.measures;
            int numRepeats = Mathf.CeilToInt((float)measures / patternLength);

            // 2️⃣ Initialize Pattern Builder
            PatternBuilder patternBuilder = new PatternBuilder();
            patternBuilder.MoveToStart(); // Ensure all notes align properly

            int minOct = instrument.octaveMin;
            int maxOct = instrument.octaveMax;

            // 3️⃣ Repeat the melody pattern across all measures
            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int measureOffset = repeat * patternLength * beatsPerBar;

                // Process each note in the melody pattern
                foreach (var noteData in melodyPattern.melodyNotes)
                {
                    // Choose a scale degree from possible options
                    MusicTheory.MusicTheory.ScaleDegree selectedDegree =
                        noteData.possibleDegrees[Random.Range(0, noteData.possibleDegrees.Count)];

                    int octave = Random.Range(minOct, maxOct + 1);

                    // Convert scale degree to actual note
                    if (!GetNoteFromScale(
                        scale, selectedDegree, rootNote, octave, out DryWetMidiNote note))
                    {
                        Debug.LogWarning($"Invalid Scale Degree {selectedDegree} in {melodyPattern.displayName}");
                        continue;
                    }

                    // Calculate note timing with repetition offset
                    var startTime = MusicalTimeSpan.Quarter * (noteData.startMeasure * beatsPerBar + measureOffset) +
                                    MusicalTimeSpan.Quarter * noteData.startBeat;

                    var duration = MusicalTimeSpan.Quarter * noteData.durationBeats;

                    // 4️⃣ Move to the correct position and add the note
                    patternBuilder.MoveToTime(startTime);
                    patternBuilder.Note(note, duration, (SevenBitNumber)noteData.velocity);
                }
            }

            // 5️⃣ Build MIDI pattern and create file
            Pattern pattern = patternBuilder.Build();
            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // 6️⃣ Assign instrument patch and bank
            int bankNumber = int.Parse(instrument.BankName);
            int presetNumber = instrument.PatchIndex;
            SetBankAndPatchEvents(midiFile, bankNumber, presetNumber, channel);
            SetChannel(midiFile, channel);

            return midiFile;
        }


        public MidiFile GenerateMetronomeTrackFile(
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int bpm,
            int measures,
            int bankNumber = 1, int presetNumber = 0)
        {
            var timeSignatureInfo = GetTimeSignatureDetails(timeSignature, bpm);

            var metronomeTic = Notes.D5;
            var metronomeTac = Notes.DSharp5;

            PatternBuilder patternBuilder = new PatternBuilder();
            patternBuilder.MoveToStart();

            for (int i = 0; i < measures; i++)
            {
                for (int beat = 0; beat < timeSignatureInfo.BeatsPerMeasure; beat++)
                {
                    if (beat == 0)
                    {
                        // TIC
                        patternBuilder.Note(metronomeTic, MusicalTimeSpan.Quarter);
                    }
                    else
                    {
                        // TAC
                        patternBuilder.Note(metronomeTac, MusicalTimeSpan.Quarter);
                    }
                }
            }

            // Build pattern and MidiFile
            Pattern pattern = patternBuilder.Build();
            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // Config events
            SetBankAndPatchEvents(midiFile, bankNumber, presetNumber, MetronomeChannel);
            SetChannel(midiFile, MetronomeChannel);
            // Default volume at 0
            //ApplyChannelVolume(midiFile, MetronomeChannel, 0);

            return midiFile;
        }

        public MidiFile GenerateSong(SongConfig song)
        {
            Debug.Log($"{DebugTag} Generating Midi for song");

            var fullSong = new MidiFile();

            // meta track to host SetTempo/TimeSignature changes
            var metaChunk = new TrackChunk();
            fullSong.Chunks.Add(metaChunk);
            var metaMgr = metaChunk.ManageTimedEvents(); // absolute-time editor

            long currentTicks = 0;  // where the next part begins

            // Part loop
            foreach (var entry in song.Structure)
            {
                var part = song.Parts[entry.PartIndex];

                if (part.Tracks == null || part.Tracks.Count == 0) continue;

                int bpm = GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen);

                var channelMap = BuildChannelMap(
                    part.Tracks.Select(tr => tr.Role).ToList()
                );

                var partTempo = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

                // part:<index>:<name>:<tonality>:<root>
                var tag = $"part:{entry.PartIndex}:{part.Name}:{part.Tonality}:{part.RootNote}";

                // Part events
                metaMgr.Objects.Add(new TimedEvent(new TextEvent(tag), currentTicks));
                // (Human-friendly marker for DAWs)
                metaMgr.Objects.Add(new TimedEvent(
                    new MarkerEvent($"PART {entry.PartIndex} - {part.Name}"), currentTicks));

                Debug.Log($"<color=white>Part {part.Name} | Tag {tag}</color>");

                // Each part repetitions
                for (int rep = 0; rep < entry.RepeatCount; rep++)
                {
                    Debug.Log($"Repetition #{rep + 1}");

                    // stamp TS & Tempo at the start of this repetition
                    int tsNum = TimeSignatureProperties[part.TimeSignature].BeatsPerMeasure;
                    int tsDen = TimeSignatureProperties[part.TimeSignature].BeatUnit;

                    // Time signature event
                    metaMgr.Objects.Add(new TimedEvent(
                        new TimeSignatureEvent((byte)tsNum, (byte)tsDen, 24, 8), currentTicks));

                    // Tempo change event
                    int usPerQuarter = Mathf.RoundToInt(60000000f / Mathf.Max(1, bpm));
                    metaMgr.Objects.Add(new TimedEvent(
                        new SetTempoEvent(usPerQuarter), currentTicks));

                    Debug.Log($"Stamp TS {tsNum}/{tsDen} & Tempo {bpm} at ticks {currentTicks}");

                    // Metronome track
                    int metroBankNumber = 1;
                    int metroPatchNumber = 0;
                    // 1) create a metronome clip for this part
                    var metroFile = GenerateMetronomeTrackFile(
                                        part.TimeSignature,
                                        bpm,
                                        part.Measures,
                                        metroBankNumber, metroPatchNumber);

                    // 3) shift to this repetition’s start and merge
                    ShiftFile(metroFile, currentTicks);
                    MergeInto(fullSong, metroFile);

                    // advance the cursor by the part’s length
                    int beatsPerBar = GetTimeSignatureDetails(
                                            part.TimeSignature,
                                            GetBPMFromRange(
                                                part.TempoRange, TempoRule.MultiplesOfTen
                                            )
                                        ).BeatsPerMeasure;

                    long ticksPerBeat = TimeConverter.ConvertFrom(
                                            MusicalTimeSpan.Quarter, partTempo);
                    long partTicks = ticksPerBeat * beatsPerBar * part.Measures;

                    // generate every track in this part
                    for (int t = 0; t < part.Tracks.Count; t++)
                    {
                        var cfg = part.Tracks[t];
                        // stable mapping (all Rhythm on 9, others unique non-9)
                        int channel = channelMap[t];

                        MidiFile trackFile = GenerateTrack(cfg, part, channel, bpm);

                        // cut anything that spills past the end of the part
                        TrimFileToLength(trackFile, partTicks);

                        TagTrackWithMusician(trackFile, cfg.MusicianId);
                        // shift everything by the offset of this part
                        ShiftFile(trackFile, currentTicks);
                        // merge into the master file
                        MergeInto(fullSong, trackFile);
                    }

                    long ticksPerMeasure = ticksPerBeat * beatsPerBar;
                    currentTicks += ticksPerMeasure * part.Measures;
                    /*Debug.Log(
                        $"Advanced cursor by {ticksPerMeasure * part.Measures} " +
                        $"ticks → now at {currentTicks}"
                    );*/
                }
            }

            metaMgr.Dispose();
            return fullSong;
        }

        #endregion

        #region Public Methods

        public static void ApplyChannelVolume(MidiFile file, int channel, int volume01_127)
        {
            var vol = (SevenBitNumber)Mathf.Clamp(volume01_127, 0, 127);
            foreach (var chunk in file.GetTrackChunks())
            {
                // Insert after our bank/patch events (indexes 0..2), but be safe.
                int insertAt = Mathf.Min(3, chunk.Events.Count);
                chunk.Events.Insert(insertAt, new ControlChangeEvent((SevenBitNumber)7, vol)
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });
            }
        }

        #endregion

        #region Private Methods

        private MidiFile GenerateTrack(
            SongConfig.PartConfig.TrackConfig cfg,
            SongConfig.PartConfig part,
            int channel,
            int bpm)
        {
            switch (cfg.Role)
            {
                case TrackRole.Rhythm:
                    return GenerateRhythmTrackWithPattern(
                                cfg.PercussionInstrument,
                                (DrumPatternData)cfg.Parameters.Pattern,
                                bpm,
                                part.TimeSignature,
                                part.Measures,
                                channel);

                case TrackRole.Backing:
                    return GenerateChordProgressionMidiTrackFile(
                                cfg.Instrument,
                                cfg.Role,
                                part.Tonality,
                                part.RootNote,
                                bpm,
                                part.TimeSignature,
                                part.Measures,
                                channel,
                                (ChordProgressionData)cfg.Parameters.Pattern);

                case TrackRole.Lead:
                    return GenerateMelodyTrackWithPattern(
                                cfg.Instrument,
                                (MelodyPatternData)cfg.Parameters.Pattern,
                                part.Tonality,
                                part.RootNote,
                                bpm,
                                part.TimeSignature,
                                part.Measures,
                                channel);

                default:
                    throw new System.NotSupportedException($"Unhandled role {cfg.Role}");
            }
        }

        private void SetBankAndPatchEvents(MidiFile midiFile, int bankNumber, int presetNumber, int channel)
        {
            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                // BANK
                // Split the bank number into MSB and LSB if it's greater than 127
                int msb = (bankNumber >> 7) & 0x7F; // Most significant byte
                int lsb = bankNumber & 0x7F;        // Least significant byte

                // MPTK v2.13+ Assign bank number to lsb directly
                msb = bankNumber;
                lsb = 0;

                // Add the bank select MSB
                trackChunk.Events.Insert(
                    0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)msb
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // Add the bank select LSB
                trackChunk.Events.Insert(
                    1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)lsb
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 0
                });

                // PATCH/PROGRAM/PRESET
                // Add the program change event for the preset
                trackChunk.Events.Insert(
                    2, new ProgramChangeEvent((SevenBitNumber)presetNumber
                )
                {
                    Channel = (FourBitNumber)channel,
                    DeltaTime = 1
                });

                /*
                Debug.Log($"SetBankAndPatchEvents → " +
                    $"CH:{channel} " +
                    $"BANK: {bankNumber} (MSB:{msb}, LSB:{lsb}) " +
                    $"PATCH:{presetNumber}"
                );*/
            }
        }

        private void SetChannel(MidiFile midiFile, int channel)
        {
            foreach (var midiEvent in midiFile.GetTrackChunks().SelectMany(chunk => chunk.Events))
            {
                if (midiEvent is ChannelEvent channelEvent)
                {
                    channelEvent.Channel = (FourBitNumber)channel;
                }
            }
        }

        /// Shift every timed object (notes, CCs, meta…) in a MidiFile by <offset> ticks.
        private void ShiftFile(MidiFile file, long offset)
        {
            foreach (var trackChunk in file.GetTrackChunks())
            {
                // Opens a manager that lets us edit the chunk in absolute time…
                using (var timedEventsManager = trackChunk.ManageTimedEvents())
                {
                    // …shift every event’s absolute Time…
                    foreach (var te in timedEventsManager.Objects)
                        te.Time += offset;
                    // disposing the manager will rewrite all the chunk’s DeltaTime values
                    // so the file actually plays back at the new times
                }
            }
        }
        private void MergeInto(MidiFile target, MidiFile source)
        {
            foreach (var chunk in source.GetTrackChunks())
                target.Chunks.Add(chunk.Clone());
        }

        private DryWetMidiNote[] GetPlayableChordNotes(
            DryWetMidiChord chord,
            MIDIInstrumentSO instrument)
        {
            int minOct = instrument.octaveMin - 1;
            int maxOct = instrument.octaveMax - 1;

            int startOct = Random.Range(minOct, maxOct + 1);
            Debug.Log("<color=white>" + startOct + "</color>");
            var rawNotes = chord.ResolveNotes(Octave.Get(startOct));

            foreach (var note in rawNotes)
                Debug.Log($"note {note}");

            return rawNotes
                .Select(n => DryWetMidiNote.Get(
                    n.NoteName,
                    Mathf.Clamp(n.Octave, minOct, maxOct)))
                .ToArray();
        }

        private List<int> BuildChannelMap(List<TrackRole> roles)
        {
            var map = Enumerable.Repeat(-1, roles?.Count ?? 0).ToList();
            var used = new HashSet<int>();

            // 1) All rhythm (drums) -> channel 9
            for (int i = 0; i < map.Count; i++)
                if (roles[i] == TrackRole.Rhythm) { map[i] = 9; used.Add(9); }

            // 2) Others -> 0..15 skipping 9
            int Next()
            {
                for (int ch = 0; ch < 16; ch++) 
                    if (ch != 9 && !used.Contains(ch)) { used.Add(ch); return ch; }

                return 0; // fallback if too many
            }

            for (int i = 0; i < map.Count; i++) if (map[i] == -1) map[i] = Next();
            return map;
        }

        // Write a text meta event "mus:<id>" at the head of this track file
        private void TagTrackWithMusician(MidiFile trackFile, string musicianId)
        {
            var chunk = trackFile.GetTrackChunks().FirstOrDefault();
            if (chunk == null || string.IsNullOrEmpty(musicianId)) return;

            // Insert at the very beginning
            chunk.Events.Insert(0, new TextEvent($"mus:{musicianId}"));
        }

        // Cut/shorten everything in 'file' that spills past maxTicks (absolute time)
        private void TrimFileToLength(MidiFile file, long maxTicks)
        {
            foreach (var chunk in file.GetTrackChunks())
            {
                // 1) Shorten notes that cross the boundary
                //    Only touch notes where start < maxTicks AND end > maxTicks
                chunk.Events.ProcessNotes(
                    action: n =>
                    {
                        long newLen = maxTicks - n.Time;
                        if (newLen < 1) newLen = 1; // keep a minimal non-zero note if desired
                        n.Length = newLen;
                    },
                    match: n => n.Time < maxTicks && n.EndTime > maxTicks
                );

                // 2) Remove notes that start at/after the boundary
                chunk.Events.RemoveNotes(n => n.Time >= maxTicks);

                // 3) Remove any channel events occurring strictly after the boundary
                using (var evMgr = chunk.ManageTimedEvents())
                {
                    var toRemove = new List<TimedEvent>();
                    foreach (var te in evMgr.Objects)
                        if (te.Time > maxTicks && te.Event is ChannelEvent)
                            toRemove.Add(te);

                    foreach (var te in toRemove)
                        evMgr.Objects.Remove(te);
                }
            }
        }

        private DryWetMidiNote[] RealizeChordForInstrument(
            NoteName[] chordNames, MIDIInstrumentSO instrument)
        {
            int minOct = instrument.octaveMin - 1;
            int maxOct = instrument.octaveMax - 1;

            int startOct = Random.Range(minOct, maxOct + 1);

            // Build once near startOct
            var notes = chordNames
                .Select(nn => DryWetMidiNote.Get(nn, startOct))
                .Select(n => DryWetMidiNote.Get(n.NoteName, Mathf.Clamp(n.Octave, minOct, maxOct)))
                .ToArray();

            return notes;
        }
        #endregion
    }
}

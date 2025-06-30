using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory;
using MusicTheoryChord = Melanchall.DryWetMidi.MusicTheory.Chord;
using MusicTheoryNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay
{
    public class MidiGenerator
    {
        // TODO: Separar generación de creación de midi file
        public MidiFile GenerateChordProgressionMidiTrackFile(
            MIDIInstrumentSO instrument,
            TrackRole role,
            MusicTheory.Tonality tonality,
            NoteName rootNote,
            int bpm,
            MusicTheory.TimeSignature timeSignature,
            int measures,
            int channel = 0,
            ChordProgressionData progressionData = null)
        {
            Debug.Log($"<color=blue>Generating Chord Progression: " +
                $"{progressionData.displayName} with {instrument.InstrumentName}</color>");

            // Get chords and degrees from tonality
            var chords = MusicTheory.GetTonalityChords(tonality, rootNote, new List<int> { 3, 5 });
            var chordsByDegree = MusicTheory.GetChordsDegreeDictionary(chords);
            var timeSignatureInfo = MusicTheory.GetTimeSignatureDetails(timeSignature, bpm);
            int beatsPerBar = timeSignatureInfo.BeatsPerMeasure;

            // **Determine pattern repetitions**
            int patternLength = progressionData?.measures ?? 4; // Default to 4 measures
            int numRepeats = Mathf.CeilToInt((float)measures / patternLength); // Rounds up

            PatternBuilder patternBuilder = new PatternBuilder();

            if (progressionData != null)
            {
                Debug.Log($"Using ChordProgressionData: {progressionData.displayName}");

                for (int repeat = 0; repeat < numRepeats; repeat++)
                {
                    foreach (var chordData in progressionData.chords)
                    {
                        // Select a chord based on the possible degrees
                        MusicTheoryChord selectedChord =
                            chordsByDegree[chordData.possibleDegrees[
                                Random.Range(0, chordData.possibleDegrees.Count)]
                            ];

                        if (selectedChord == null)
                        {
                            Debug.LogWarning("No matching chord found for the specified degrees. Skipping chord.");
                            continue;
                        }

                        // Calculate start time (shifted by repetition offset)
                        var startTime = MusicalTimeSpan.Quarter * 
                            ((chordData.startMeasure + (repeat * patternLength)) * beatsPerBar)
                            + MusicalTimeSpan.Quarter * chordData.startBeat;

                        var playable = GetPlayableChordNotes(selectedChord, instrument);

                        Debug.Log("Playable chord notes:");
                        foreach (var n in playable)
                        {
                            Debug.Log(n);
                        }

                        // Move to time and add chord
                        patternBuilder.MoveToTime(startTime);
                        patternBuilder.Chord(
                            playable,
                            MusicalTimeSpan.Quarter * chordData.durationBeats,
                            (SevenBitNumber)chordData.velocity
                        );
                    }
                }
            }

            // Build the MIDI pattern
            Pattern pattern = patternBuilder.Build();
            TempoMap tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            MidiFile midiFile = pattern.ToFile(tempoMap);

            // Set instrument bank and patch
            SetBankAndPatchEvents(midiFile, int.Parse(instrument.BankName), instrument.PatchIndex, channel);
            SetChannel(midiFile, channel);

            return midiFile;
        }


        public MidiFile GenerateRhythmTrackWithPattern(
            MIDIPercussionInstrumentSO percussionInstrument,
            DrumPatternData patternData,
            int bpm,
            MusicTheory.TimeSignature timeSignature,
            int measures,
            int channel = 9)
        {
            Debug.Log($"<color=red>Generating Drum Track: " +
                $"{patternData.displayName} with {percussionInstrument.InstrumentName}</color>");

            // Extract time signature details
            var timeSignatureInfo = MusicTheory.GetTimeSignatureDetails(timeSignature, bpm);
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
                if (!percussionInstrument.TryGetMappedNote(percussionType, out MusicTheoryNote mappedNote))
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
            MusicTheory.Tonality tonality,
            NoteName rootNote,
            int bpm,
            MusicTheory.TimeSignature timeSignature,
            int measures = 4,
            int channel = 0)
        {
            Debug.Log($"<color=green>Generating Melody Track: " +
                $"{melodyPattern.displayName} for {instrument.InstrumentName}</color>");

            // 1️⃣ Retrieve scale and time signature details
            var scale = MusicTheory.GetScaleFromTonality(tonality, rootNote);
            var timeSignatureInfo = MusicTheory.GetTimeSignatureDetails(timeSignature, bpm);
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
                    ScaleDegree selectedDegree =
                        noteData.possibleDegrees[Random.Range(0, noteData.possibleDegrees.Count)];

                    int octave = Random.Range(minOct, maxOct + 1);

                    // Convert scale degree to actual note
                    if (!MusicTheory.GetNoteFromScale(
                        scale, selectedDegree, rootNote, octave, out MusicTheoryNote note))
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
            MusicTheory.TimeSignature timeSignature,
            int bpm,
            int measures)
        {
            var timeSignatureInfo = MusicTheory.GetTimeSignatureDetails(timeSignature, bpm);

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

            // BY DEFAULT
            int bankNumber = 128;
            int presetNumber = 0;
            SetBankAndPatchEvents(midiFile, bankNumber, presetNumber, 15);
            SetChannel(midiFile, 15);

            return midiFile;
        }

        private void SetBankAndPatchEvents(MidiFile midiFile, int bankNumber, int presetNumber, int channel)
        {
            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                // BANK
                // Split the bank number into MSB and LSB if it's greater than 127
                int msb = (bankNumber >> 7) & 0x7F; // Most significant byte
                int lsb = bankNumber & 0x7F;        // Least significant byte

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

        public MidiFile GenerateSong(SongConfig song)
        {
            var fullSong = new MidiFile();
            long currentTicks = 0;  // where the next part begins

            foreach (var entry in song.Structure)
            {
                var part = song.Parts[entry.PartIndex];
                int bpm = GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen);
                var partTempo = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

                Debug.Log($"{part.Name}");

                for (int rep = 0; rep < entry.RepeatCount; rep++)
                {
                    Debug.Log($"Repetition #{rep + 1}");

                    // generate every track in this part
                    for (int t = 0; t < part.Tracks.Count; t++)
                    {
                        var cfg = part.Tracks[t];
                        int channel = t;                // “track slot” → MIDI channel 0-15

                        MidiFile trackFile = GenerateTrack(cfg, part, channel, bpm);

                        // shift everything by the offset of this part
                        ShiftFile(trackFile, currentTicks);

                        // merge into the master file
                        MergeInto(fullSong, trackFile);
                    }

                    // advance the cursor by the part’s length
                    int beatsPerBar = GetTimeSignatureDetails(
                                            part.TimeSignature,
                                            GetBPMFromRange(
                                                part.TempoRange, TempoRule.MultiplesOfTen
                                            )
                                        ).BeatsPerMeasure;

                    long ticksPerBeat = TimeConverter.ConvertFrom(
                                            MusicalTimeSpan.Quarter, partTempo);

                    long ticksPerMeasure = ticksPerBeat * beatsPerBar;
                    currentTicks += ticksPerMeasure * part.Measures;
                    Debug.Log(
                        $"Advanced cursor by {ticksPerMeasure * part.Measures} " +
                        $"ticks → now at {currentTicks}"
                    );
                }
            }

            return fullSong;
        }

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

        private MusicTheoryNote[] GetPlayableChordNotes(
            MusicTheoryChord chord,
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
                .Select(n => MusicTheoryNote.Get(
                    n.NoteName,
                    Mathf.Clamp(n.Octave, minOct, maxOct)))
                .ToArray();
        }
    }
}

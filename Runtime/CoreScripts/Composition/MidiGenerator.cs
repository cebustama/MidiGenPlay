using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay
{
    public class MidiGenerator
    {
        private const string DebugTag = "<color=green>[MidiGenerator]</color>";

        public const int MetronomeChannel = 15;

        private MidiGenPlayConfig settings;
        private readonly Dictionary<TrackRole, ITrackComposer> _composers = new();
        private readonly IChordVoicer _voicer;

        public MidiGenerator(MidiGenPlayConfig config, IChordVoicer voicer = null)
        {
            settings = config;

            _voicer = voicer;
            var melodyCfg = settings.melodicLeading;
            var harmonyCfg = settings.harmonicLeading;
            var melodyStrategy = new NearestChordToneMelodyStrategy();
            var harmonyStrategy = new NearestDifferentChordToneHarmonyStrategy();

            _composers[TrackRole.Melody] = 
                new MelodyComposerMinimal(melodyCfg, melodyStrategy);
            _composers[TrackRole.Lead] = _composers[TrackRole.Melody]; // same for now

            _composers[TrackRole.Harmony] = 
                new HarmonyComposerMinimal(harmonyCfg, harmonyStrategy);

            _composers[TrackRole.Backing] =
                new ChordTrackComposer(settings, voicer);

            _composers[TrackRole.Rhythm] =
                new RhythmTrackComposer(settings);

            _composers[TrackRole.Bassline] = 
                new BassTrackComposer(settings, randomChordTone: false);

            if (settings != null && settings.logGenerator)
            {
                var roles = string.Join(", ", _composers.Keys.Select(r => r.ToString()));
                Debug.Log($"{DebugTag} Composer registry: [{roles}]  " +
                    $"| Voicer={(voicer != null ? voicer.GetType().Name : "null")}");
            }
        }

        public class GenContext
        {
            public System.Random rng;
            public IChordVoicer ChordVoicer;
            public VoiceLeadingConfig chordVoicingPreset;
            public MIDIInstrumentSO DefaultMelodicInstrument;

            public System.Func<SongConfig.PartConfig, TrackRole, MidiFile> 
                GetTrackForRole;
            public System.Func<MidiFile, List<Melanchall.DryWetMidi.Interaction.Note>> 
                ExtractMonophonicNotes;
            public System.Func<ChordProgressionData, TempoMap, MusicTheory.MusicTheory.TimeSignature, long, ChordProgressionData.ChordEvent> 
                FindChordEventAt;
            public System.Func<SongConfig.PartConfig, ChordProgressionData> 
                GetProgressionForPart;
        }

        #region Harmony 
        public enum HarmonyIntervalMode { ChordMember, ScaleDegree, SemitoneOffset }
        public enum HarmonyInterval { Unison, Third, Fifth, Sixth, Octave }

        public sealed class HarmonyOptions
        {
            public HarmonyIntervalMode mode = HarmonyIntervalMode.ChordMember; // minimal uses this
            public HarmonyInterval interval = HarmonyInterval.Third;           // not used yet
            public int semitoneOffset = 0;                                     // not used yet
            public bool preferAbove = true;                                    // not used yet
            public bool clampToRange = true;
        }

        // Minimal melodic voice-leading options (TODO move to a ScriptableObject)
        [System.Serializable]
        public sealed class MelodicLeadingOptions
        {
            public bool enabled = true;
            public int maxLeapSemitones = 12;     // soft cap; we still pick “nearest”
            public bool preferStepwise = true;    // used in tie-breaks
            public bool preferAbove = false;      // tie-break: bias upwards
            public bool clampToRange = true;
        }

        // Internal guide note to reuse across tracks in a part
        private struct GuideNote
        {
            public double startBeats;
            public double durBeats;
            public DryWetMidiNote note; // absolute pitch
        }

        #endregion

        #region Generation Methods

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

                    var producedByRole = new Dictionary<TrackRole, MidiFile>();
                    var ctx = new GenContext
                    {
                        // each repetition differs deterministically
                        rng = new System.Random(
                            settings.defaultSeed + entry.PartIndex * 397 ^ rep),

                        chordVoicingPreset = settings.voiceLeading,
                        ChordVoicer = _voicer,
                        DefaultMelodicInstrument = part.Tracks.FirstOrDefault(t => t.Instrument != null)?.Instrument,

                        GetTrackForRole = (p, role) => producedByRole.TryGetValue(role, out var f) ? f : null,
                        ExtractMonophonicNotes = (file) => file?.GetNotes()?.OrderBy(n => n.Time).ToList()
                                               ?? new List<Melanchall.DryWetMidi.Interaction.Note>(),
                        FindChordEventAt = (prog, tempoMap, ts, absTicks) =>
                            // prefer your real utility if you have one on ChordProgressionData
                            prog?.FindChordEventAt(tempoMap, ts, absTicks),

                        GetProgressionForPart = (p) => FindProgressionForPart(p)
                    };

                    // generate every track in this part
                    for (int t = 0; t < part.Tracks.Count; t++)
                    {
                        var cfg = part.Tracks[t];
                        // stable mapping (all Rhythm on 9, others unique non-9)
                        int channel = channelMap[t];

                        var trackFile = GenerateTrack(cfg, part, channel, bpm, ctx);

                        // cut anything that spills past the end of the part
                        TrimFileToLength(trackFile, partTicks);

                        // quick inspect before merge
                        if (settings != null && settings.logGenerator)
                        {
                            var i = Inspect(trackFile);
                            Debug.Log($"{DebugTag} Pre-merge [{cfg.Role}] " +
                                $"ch={channel} tracks={i.tracks} " +
                                $"notes={i.notes} lastTick={i.lastTick}");
                        }

                        TagTrackWithMusician(trackFile, cfg.MusicianId);
                        // shift everything by the offset of this part
                        ShiftFile(trackFile, currentTicks);
                        // merge into the master file
                        MergeInto(fullSong, trackFile);

                        producedByRole[cfg.Role] = trackFile;

                        // optional: confirm merged chunk presence
                        if (settings != null && settings.logGenerator)
                        {
                            var totalTracks = fullSong.GetTrackChunks().Count();
                            Debug.Log($"{DebugTag} Merged [{cfg.Role}] → " +
                                $"fullSong chunks={totalTracks}");
                        }
                    }

                    // Stamp a boundary event at the exact end:
                    long endTick = currentTicks + partTicks;
                    metaMgr.Objects.Add(new TimedEvent(
                        new ControlChangeEvent(
                            (SevenBitNumber)(byte)ControlName.AllSoundOff, 
                            (SevenBitNumber)0)
                        { Channel = (FourBitNumber)MetronomeChannel }, 
                        endTick)
                    );

                    // Advance the cursor to the next repetition
                    long ticksPerMeasure = ticksPerBeat * beatsPerBar;
                    currentTicks += ticksPerMeasure * part.Measures;
                    /*Debug.Log(
                        $"Advanced cursor by {ticksPerMeasure * part.Measures} " +
                        $"ticks → now at {currentTicks}"
                    );*/
                }
            }

            LogTrackEnds(fullSong, "AfterGenerateSong");
            metaMgr.Dispose();
            return fullSong;
        }
        #endregion

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

        #region Private Methods

        private MidiFile GenerateTrack(
            SongConfig.PartConfig.TrackConfig cfg,
            SongConfig.PartConfig part,
            int channel,
            int bpm,
            GenContext ctx)
        {
            if (settings != null && settings.logGenerator)
                Debug.Log($"{DebugTag} → GenerateTrack role={cfg.Role} ch={channel} bpm={bpm} " +
                          $"inst={(cfg.Instrument ? cfg.Instrument.InstrumentName : "-")} " +
                          $"perc={(cfg.PercussionInstrument ? cfg.PercussionInstrument.InstrumentName : "-")} " +
                          $"pattern={(cfg.Parameters?.Pattern ? cfg.Parameters.Pattern.name : "-")}");

            // Composer-based roles
            if (_composers.TryGetValue(cfg.Role, out var composer))
            {
                var r = composer.Compose(part, cfg, bpm, channel, ctx);
                if (settings != null && settings.logGenerator)
                {
                    var i = Inspect(r);
                    Debug.Log($"{DebugTag} ← {cfg.Role} composer={composer.GetType().Name} " +
                              $"tracks={i.tracks} notes={i.notes} lastTick={i.lastTick}");
                }
                return r;
            }

            Debug.LogWarning($"{DebugTag} No composer registered for role {cfg.Role}");
            return new MidiFile();
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

        private static void LogTrackEnds(MidiFile file, string tag = "Song")
        {
            var tempoMap = file.GetTempoMap();
            int i = 0;
            foreach (var chunk in file.GetTrackChunks())
            {
                var last = chunk.GetTimedEvents().LastOrDefault();
                var secs = last == null
                    ? 0.0
                    : TimeConverter.ConvertTo<MetricTimeSpan>(last.Time, tempoMap).TotalSeconds;
                Debug.Log($"[{tag}] Track {i++} last @tick={last?.Time} s={secs:0.###} evt={last?.Event}");
            }
        }

        private static ChordProgressionData FindProgressionForPart(SongConfig.PartConfig part)
        {
            if (part?.Tracks == null) return null;

            foreach (var tr in part.Tracks)
            {
                if (tr?.Role == TrackRole.Backing)
                    return tr.Parameters?.Pattern as ChordProgressionData;
            }
            return null;
        }

        private static (int tracks, int notes, long lastTick) Inspect(MidiFile f)
        {
            if (f == null) return (0, 0, 0);
            var chunks = f.GetTrackChunks().ToList();
            var notes = f.GetNotes().Count();
            var last = chunks.SelectMany(c => c.GetTimedEvents())
                              .Select(te => te.Time).DefaultIfEmpty(0).Max();
            return (chunks.Count, notes, last);
        }
        #endregion
    }
}

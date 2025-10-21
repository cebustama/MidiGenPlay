using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    public interface ISongOrchestrator
    {
        MidiFile GenerateSong(SongConfig song);
    }

    /// Coordinates parts/repetitions, meta events, metronome, composer calls, trimming, shifting, and merging.
    public sealed class SongOrchestrator : ISongOrchestrator
    {
        private const string LogTag = "<color=#4fe>[SongOrchestrator]</color>";

        private readonly MidiGenPlayConfig _settings;
        private readonly IReadOnlyDictionary<TrackRole, ITrackComposer> _composers;
        private readonly IChordVoicer _voicer; // forwarded into GenContext

        public SongOrchestrator(
            MidiGenPlayConfig settings,
            IReadOnlyDictionary<TrackRole, ITrackComposer> composers,
            IChordVoicer voicer = null)
        {
            _settings = settings;
            _composers = composers ?? throw new ArgumentNullException(nameof(composers));
            _voicer = voicer;
        }

        public MidiFile GenerateSong(SongConfig song)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));

            var fullSong = new MidiFile();

            // Meta track (tempo & time-signature stamps + markers)
            var metaChunk = new TrackChunk();
            fullSong.Chunks.Add(metaChunk);
            var metaMgr = metaChunk.ManageTimedEvents();

            long cursorTicks = 0; // where next part/repetition starts

            foreach (var entry in song.Structure)
            {
                var part = song.Parts[entry.PartIndex];
                if (part?.Tracks == null || part.Tracks.Count == 0) continue;

                int bpm = GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen);
                var partTempo = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

                // Channel allocation for this part
                var channelMap = BuildChannelMap(part.Tracks.Select(t => t.Role).ToList());

                // DAW-friendly labels
                var partTag = $"part:{entry.PartIndex}:{part.Name}:{part.Tonality}:{part.RootNote}";
                metaMgr.Objects.Add(new TimedEvent(new TextEvent(partTag), cursorTicks));
                metaMgr.Objects.Add(new TimedEvent(new MarkerEvent($"PART {entry.PartIndex} - {part.Name}"), cursorTicks));

                // Precompute part duration (ticks)
                int beatsPerBar = GetTimeSignatureDetails(part.TimeSignature, bpm).BeatsPerMeasure;
                long ticksPerBeat = TimeConverter.ConvertFrom(MusicalTimeSpan.Quarter, partTempo);
                long ticksPerMeasure = ticksPerBeat * beatsPerBar;
                long partTicks = ticksPerMeasure * Math.Max(1, part.Measures);

                for (int rep = 0; rep < entry.RepeatCount; rep++)
                {
                    // Stamp time-signature + tempo at the repetition start
                    int tsNum = TimeSignatureProperties[part.TimeSignature].BeatsPerMeasure;
                    int tsDen = TimeSignatureProperties[part.TimeSignature].BeatUnit;

                    metaMgr.Objects.Add(new TimedEvent(new TimeSignatureEvent((byte)tsNum, (byte)tsDen, 24, 8), cursorTicks));
                    int usPerQuarter = Mathf.RoundToInt(60000000f / Mathf.Max(1, bpm));
                    metaMgr.Objects.Add(new TimedEvent(new SetTempoEvent(usPerQuarter), cursorTicks));

                    if (_settings?.logGenerator == true)
                        Debug.Log($"{LogTag} Part='{part.Name}' rep={rep + 1} TS={tsNum}/{tsDen} BPM={bpm} @tick={cursorTicks}");

                    // Metronome clip for this repetition (optional)
                    var metro = GenerateMetronomeTrackFile(part.TimeSignature, bpm, part.Measures, bankNumber: 1, presetNumber: 0);
                    ShiftFile(metro, cursorTicks);
                    MergeInto(fullSong, metro);

                    // Per-repetition context & cache for inter-role comms
                    var producedByRole = new Dictionary<TrackRole, MidiFile>();
                    var ctx = new MidiGenerator.GenContext
                    {
                        rng = new System.Random(_settings.defaultSeed + entry.PartIndex * 397 ^ rep),
                        ChordVoicer = _voicer,
                        chordVoicingPreset = _settings.voiceLeading,
                        DefaultMelodicInstrument = part.Tracks.FirstOrDefault(t => t.Instrument != null)?.Instrument,

                        GetTrackForRole = (p, role) => producedByRole.TryGetValue(role, out var f) ? f : null,
                        ExtractMonophonicNotes = (mf) => mf?.GetNotes()?.OrderBy(n => n.Time).ToList()
                                                  ?? new List<Melanchall.DryWetMidi.Interaction.Note>(),
                        FindChordEventAt = (prog, tempoMap, ts, absTicks) => prog?.FindChordEventAt(tempoMap, ts, absTicks),
                        GetProgressionForPart = (p) => FindProgressionForPart(p)
                    };

                    // TODO: PASS 0: generate chord progressions

                    // PASS 1: generate all except Harmony (so Harmony can read Melody/Lead)
                    for (int i = 0; i < part.Tracks.Count; i++)
                    {
                        var cfg = part.Tracks[i];
                        if (cfg.Role == TrackRole.Harmony) continue;
                        GenerateOne(fullSong, part, cfg, channelMap[i], bpm, partTicks, cursorTicks, ctx, producedByRole);
                    }

                    // PASS 2: Harmony
                    for (int i = 0; i < part.Tracks.Count; i++)
                    {
                        var cfg = part.Tracks[i];
                        if (cfg.Role != TrackRole.Harmony) continue;
                        GenerateOne(fullSong, part, cfg, channelMap[i], bpm, partTicks, cursorTicks, ctx, producedByRole);
                    }

                    // Boundary event at exact end (safety)
                    long endTick = cursorTicks + partTicks;
                    metaMgr.Objects.Add(new TimedEvent(
                        new ControlChangeEvent((SevenBitNumber)(byte)ControlName.AllSoundOff, (SevenBitNumber)0)
                        { Channel = (FourBitNumber)MidiGenerator.MetronomeChannel }, endTick));

                    // Advance to next repetition
                    cursorTicks += partTicks;
                }
            }

            metaMgr.Dispose();
            if (_settings?.logGenerator == true) LogTrackEnds(fullSong, "Song");
            return fullSong;
        }

        private void GenerateOne(
            MidiFile fullSong,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int channel,
            int bpm,
            long partTicks,
            long cursorTicks,
            MidiGenerator.GenContext ctx,
            IDictionary<TrackRole, MidiFile> producedByRole)
        {
            if (!_composers.TryGetValue(cfg.Role, out var composer))
            {
                Debug.LogWarning($"{LogTag} No composer for role {cfg.Role}");
                return;
            }

            if (_settings?.logGenerator == true)
            {
                Debug.Log($"{LogTag} Start part='{part.Name}' role={cfg.Role} ch={channel} inst={InstName(cfg)} pattern={PatternName(cfg)} " +
                          $"@tick={cursorTicks} lenTicks={partTicks}");
            }

            var trackFile = composer.Compose(part, cfg, bpm, channel, ctx);

            TrimFileToLength(trackFile, partTicks);
            TagTrackWithMusician(trackFile, cfg.MusicianId);
            ShiftFile(trackFile, cursorTicks);
            MergeInto(fullSong, trackFile);

            producedByRole[cfg.Role] = trackFile;

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, last) = Inspect(trackFile);
                Debug.Log($"{LogTag} Merged [{cfg.Role}] ch={channel} inst='{InstName(cfg)}' pattern='{PatternName(cfg)}' " +
                          $"tracks={tracks} notes={notes} lastTick={last}");
            }
        }

        // ---------- Helpers (assembly concerns) ----------

        private static MidiFile GenerateMetronomeTrackFile(
            MusicTheory.MusicTheory.TimeSignature timeSignature,
            int bpm,
            int measures,
            int bankNumber = 1, int presetNumber = 0)
        {
            var ts = GetTimeSignatureDetails(timeSignature, bpm);
            var tic = Melanchall.DryWetMidi.MusicTheory.Notes.D5;
            var tac = Melanchall.DryWetMidi.MusicTheory.Notes.DSharp5;

            var pb = new PatternBuilder().MoveToStart();
            for (int m = 0; m < measures; m++)
                for (int beat = 0; beat < ts.BeatsPerMeasure; beat++)
                    pb.Note(beat == 0 ? tic : tac, MusicalTimeSpan.Quarter);

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            SetBankAndPatch(file, bankNumber, presetNumber, MidiGenerator.MetronomeChannel);
            ForceChannel(file, MidiGenerator.MetronomeChannel);
            return file;
        }

        private static void SetBankAndPatch(MidiFile mf, int bankNumber, int presetNumber, int channel)
        {
            foreach (var chunk in mf.GetTrackChunks())
            {
                int msb = bankNumber; // MPTK expects MSB=bank, LSB=0
                int lsb = 0;

                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)presetNumber)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        private static void ForceChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static List<int> BuildChannelMap(List<TrackRole> roles)
        {
            var map = Enumerable.Repeat(-1, roles?.Count ?? 0).ToList();
            var used = new HashSet<int>();

            // Drums → ch 9
            for (int i = 0; i < map.Count; i++)
                if (roles[i] == TrackRole.Rhythm) { map[i] = 9; used.Add(9); }

            int Next()
            {
                for (int ch = 0; ch < 16; ch++)
                    if (ch != 9 && !used.Contains(ch)) { used.Add(ch); return ch; }
                return 0;
            }

            for (int i = 0; i < map.Count; i++) if (map[i] == -1) map[i] = Next();
            return map;
        }

        private static void ShiftFile(MidiFile file, long offset)
        {
            foreach (var chunk in file.GetTrackChunks())
                using (var mgr = chunk.ManageTimedEvents())
                    foreach (var te in mgr.Objects) te.Time += offset;
        }

        private static void MergeInto(MidiFile target, MidiFile source)
        {
            foreach (var chunk in source.GetTrackChunks())
                target.Chunks.Add(chunk.Clone());
        }

        private static void TagTrackWithMusician(MidiFile trackFile, string musicianId)
        {
            var chunk = trackFile.GetTrackChunks().FirstOrDefault();
            if (chunk == null || string.IsNullOrEmpty(musicianId)) return;
            chunk.Events.Insert(0, new TextEvent($"mus:{musicianId}"));
        }

        private static void TrimFileToLength(MidiFile file, long maxTicks)
        {
            foreach (var chunk in file.GetTrackChunks())
            {
                chunk.Events.ProcessNotes(
                    action: n =>
                    {
                        long newLen = maxTicks - n.Time;
                        if (newLen < 1) newLen = 1;
                        n.Length = newLen;
                    },
                    match: n => n.Time < maxTicks && n.EndTime > maxTicks
                );

                chunk.Events.RemoveNotes(n => n.Time >= maxTicks);

                using (var evMgr = chunk.ManageTimedEvents())
                {
                    var toRemove = new List<TimedEvent>();
                    foreach (var te in evMgr.Objects)
                        if (te.Time > maxTicks && te.Event is ChannelEvent)
                            toRemove.Add(te);
                    foreach (var te in toRemove) evMgr.Objects.Remove(te);
                }
            }
        }

        private static void LogTrackEnds(MidiFile file, string tag = "Song")
        {
            var tempoMap = file.GetTempoMap();
            int idx = 0;
            foreach (var chunk in file.GetTrackChunks())
            {
                var last = chunk.GetTimedEvents().LastOrDefault();
                var secs = last == null
                    ? 0.0
                    : TimeConverter.ConvertTo<MetricTimeSpan>(last.Time, tempoMap).TotalSeconds;
                //Debug.Log($"[{tag}] Track {idx++} last @tick={last?.Time} s={secs:0.###} evt={last?.Event}");
            }
        }

        private static ChordProgressionData FindProgressionForPart(SongConfig.PartConfig part)
        {
            if (part?.Tracks == null) return null;
            foreach (var tr in part.Tracks)
                if (tr?.Role == TrackRole.Backing)
                    return tr.Parameters?.Pattern as ChordProgressionData;
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

        private static string InstName(SongConfig.PartConfig.TrackConfig cfg)
        {
            if (cfg?.Instrument != null) return cfg.Instrument.InstrumentName;
            if (cfg?.PercussionInstrument != null) return cfg.PercussionInstrument.InstrumentName;
            return "-";
        }
        private static string PatternName(SongConfig.PartConfig.TrackConfig cfg)
        {
            var p = cfg?.Parameters?.Pattern;
            return p != null ? $"{p.GetType().Name}:{p.name}" : "-";
        }
    }
}

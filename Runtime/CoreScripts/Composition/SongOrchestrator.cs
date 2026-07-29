using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    public sealed class PartRender
    {
        public MidiFile merged;
        // MGP-ALWTTT-DBG-1 (D-DBG1=A, BREAKING re-key): every per-track
        // surface is keyed on (musicianId, TrackRole). A musicianId alone is
        // not unique — the same musician can own several roles in one part
        // (the BASS-1 case) — so string keys were dropping stems/instruments.
        public Dictionary<MusicianTrackKey, MidiFile> stemsByMusician = new();
        public Dictionary<MusicianTrackKey, MIDIInstrumentSO> melInstByMusician = new();
        public Dictionary<MusicianTrackKey, MIDIPercussionInstrumentSO> percInstByMusician = new();
        // MGP-ALWTTT-DBG-1 (Ask A): per-track readback — what each composer
        // actually resolved this render (source, pre-clone asset name,
        // palette, figures/archetypes/progression per role). Populated by the
        // sink GenerateOne installs on ctx.ReportResolved; tracks whose
        // composer does not report (Harmony in v1, ID-2=A) simply have no
        // entry.
        public Dictionary<MusicianTrackKey, ResolvedTrackChoice> resolvedByTrack = new();
        // MGP-MIX-1 (D-MIX-5=A): the CC7 value actually emitted per track —
        // entries exist ONLY for melodic tracks that had a mixGains entry.
        // Orchestrator-stamped (this is applied by GenerateOne, not resolved
        // by a composer, so it does NOT belong on ResolvedTrackChoice).
        public Dictionary<MusicianTrackKey, int> appliedCc7ByTrack = new();
        public long partTicks;
        public int bpm;
    }

    public interface ISongOrchestrator
    {
        MidiFile GenerateSong(SongConfig song, int? seedOverride = null);

        PartRender GenerateSinglePart(
            SongConfig.PartConfig part,
            IReadOnlyList<TrackRole> rolesForChannels,
            int partIndex,
            int? bpmOverride = null,
            // MGP-ALWTTT-DBG-1 (D-DBG1=A, BREAKING): re-keyed to (musicianId, role).
            Dictionary<MusicianTrackKey, MIDIInstrumentSO> instrumentOverrides = null,
            int? seedOverride = null,
            // MGP-ALWTTT-DBG-3 (Ask C, D-DBG4=A): per-render pattern/progression
            // override, precedence step 0 in each composer. Value is the common
            // PatternDataSO base (DrumPatternData / ChordProgressionData /
            // MelodyPatternData); composers clone-on-apply and warn+ignore on
            // type mismatch. Bassline entries are warn+ignore in v1.
            IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> patternOverrides = null,
            // MGP-MIX-1 (D-MIX-2=A): per-render consumer mix gain, keyed
            // (musicianId, role). Entry present => one CC7 on THAT track's
            // channel: clamp(round(Instrument.volume01 * gain * 100), 0, 127)
            // — identity (1.0 * 1.0) lands on 100, the GM channel-volume
            // default, so a gain of 1.0 is level-neutral next to tracks with
            // no entry (D-MIX-3). Null map, empty map, or no entry => ZERO new
            // events => bit-identical to 1.1.0. Rhythm entries: warn + ignore
            // in v1 (all Rhythm tracks share MIDI channel 9, so per-musician
            // CC7 cannot target one drummer; D-MIX-4=A). Pure data: touches no
            // ctx.rng, no seed chain — same map + same seed => same bytes.
            IReadOnlyDictionary<MusicianTrackKey, float> mixGains = null,
            // MGP-ALWTTT-BASS-SOLO-1 (D-SOLO-SURF=A2): HOST-supplied default
            // progression, pre-seeded into the per-render shared cache for
            // parts with NO Backing track so harmony consumers (Bassline,
            // Melody, Harmony) can render. Warn + ignore when a Backing track
            // exists (D-SOLO-GUARD=A). Seeded as-is — no TS normalization on
            // this path (D-SOLO-NORM=A). Pure: zero rng draws; null leaves the
            // render byte-identical.
            ChordProgressionData defaultProgression = null);
    }

    /// Coordinates parts/repetitions, meta events, metronome, composer calls, trimming, shifting, and merging.
    public sealed class SongOrchestrator : ISongOrchestrator
    {
        private const string LogTag = "<color=#4fe>[SongOrchestrator]</color>";

        private readonly MidiGenPlayConfig _settings;
        private readonly IReadOnlyDictionary<TrackRole, ITrackComposerFactory> _factories;
        private readonly IChordVoicer _voicer; // forwarded into GenContext

        public SongOrchestrator(
            MidiGenPlayConfig settings,
            IReadOnlyDictionary<TrackRole, ITrackComposerFactory> factories,
            IChordVoicer voicer = null)
        {
            _settings = settings;
            _factories = factories ?? throw new ArgumentNullException(nameof(factories));
            _voicer = voicer;
        }

        public MidiFile GenerateSong(SongConfig song, int? seedOverride = null)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));

            // MGP-ALWTTT-SEED-1: resolve the base seed once per render call.
            // Null => bit-identical to previous behavior (defaultSeed).
            int baseSeed = ResolveBaseSeed(seedOverride, _settings.defaultSeed);

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

                // BPM-DET-1 (D-BPM1=A / D-BPM3=B): honor an explicit part BPM,
                // else roll the tempo on a dedicated seeded substream (D-BPM2=A)
                // instead of the unseeded MusicTheory helper — so a full-song
                // render is reproducible under the same seed. Per part-occurrence.
                int bpm = part.ExplicitBpm
                    ?? RollTempoBpm(ResolveTempoSeed(baseSeed, entry.PartIndex),
                                    part.TempoRange, TempoRule.MultiplesOfTen);
                var partTempo = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

                // Channel allocation for this part
                var channelMap = BuildChannelMap(part.Tracks.Select(t => t.Role).ToList());

                // DAW-friendly labels
                var partTag = $"part:{entry.PartIndex}:{part.Name}:{part.Tonality}:{part.RootNote}";
                metaMgr.Objects.Add(new TimedEvent(new TextEvent(partTag), cursorTicks));
                metaMgr.Objects.Add(new TimedEvent(new MarkerEvent($"PART {entry.PartIndex} - {part.Name}"), cursorTicks));

                // Precompute part duration (ticks)
                int beatsPerBar = GetTimeSignatureDetails(part.TimeSignature, bpm).BeatsPerMeasure;
                var beatSpan = GetBeatSpan(part.TimeSignature);
                long ticksPerBeat = TimeConverter.ConvertFrom(beatSpan, partTempo);
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

                    var progressionByPart = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();
                    var producedByRole = new Dictionary<TrackRole, MidiFile>();
                    var melodyByPartAndMusician =
                        new Dictionary<SongConfig.PartConfig,
                        Dictionary<string, List<MidiGenerator.GuideNote>>>();
                    // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B): rhythm onset
                    // channel, list-backed so "first publisher" is publication
                    // (track-list) order by construction.
                    var rhythmOnsetsByPart =
                        new Dictionary<SongConfig.PartConfig,
                        List<(string musicianId, List<MidiGenerator.RhythmOnset> onsets)>>();

                    // --- GENERATION CONTEXT ---
                    var ctx = new MidiGenerator.GenContext
                    {
                        Settings = _settings,
                        rng = new System.Random(ResolveRepContextSeed(baseSeed, entry.PartIndex, rep)),
                        ChordVoicer = _voicer,
                        chordVoicingPreset = _settings.voiceLeading,
                        DefaultMelodicInstrument = part.Tracks.FirstOrDefault(t => t.Instrument != null)?.Instrument,

                        GetTrackForRole = (p, role) => producedByRole.TryGetValue(role, out var f) ? f : null,
                        ExtractMonophonicNotes = (mf) => mf?.GetNotes()?.OrderBy(n => n.Time).ToList()
                                                  ?? new List<Melanchall.DryWetMidi.Interaction.Note>(),
                        FindChordEventAt = (prog, tempoMap, ts, absTicks) => prog?.FindChordEventAt(tempoMap, ts, absTicks),

                        // Progression cache
                        GetProgressionForPart = (p) =>
                        {
                            if (progressionByPart.TryGetValue(p, out var pr)) return pr;
                            return FindProgressionForPart(p); // existing authored-based lookup
                        },

                        SetProgressionForPart = CreateSetProgressionForPart(progressionByPart),

                        // Tonality cache
                        GetTonalityProfileForPart = (p) =>
                        {
                            // delegate to settings
                            return _settings != null
                                ? _settings.GetProfileForTonality(p.Tonality)
                                : null;
                        },

                        // Melodies cache
                        GetMelodyForPartMusician = (p, musicianId) =>
                        {
                            if (melodyByPartAndMusician.TryGetValue(p, out var dictForPart))
                            {
                                if (!string.IsNullOrEmpty(musicianId) &&
                                    dictForPart.TryGetValue(musicianId, out var guideNotes))
                                {
                                    return guideNotes;
                                }
                            }
                            return null;
                        },

                        SetMelodyForPartMusician = (p, musicianId, guideNotes) =>
                        {
                            if (string.IsNullOrEmpty(musicianId) || guideNotes == null)
                                return;

                            if (!melodyByPartAndMusician.TryGetValue(p, out var dictForPart))
                            {
                                dictForPart =
                                    new Dictionary<string, List<MidiGenerator.GuideNote>>();
                                melodyByPartAndMusician[p] = dictForPart;
                            }

                            dictForPart[musicianId] = guideNotes;

                            if (_settings?.logGenerator == true)
                            {
                                Debug.Log($"<color=yellow>{LogTag} Cached melody for part '{p.Name}' " +
                                          $"musician='{musicianId}' notes={guideNotes.Count}</color>");
                            }
                        },

                        GetFirstMelodyMusicianIdForPart = (p) =>
                        {
                            if (melodyByPartAndMusician.TryGetValue(p, out var dictForPart))
                            {
                                // pick first musicianId that actually has notes
                                foreach (var kvp in dictForPart)
                                {
                                    var musId = kvp.Key;
                                    var notes = kvp.Value;
                                    if (!string.IsNullOrEmpty(musId) &&
                                        notes != null &&
                                        notes.Count > 0)
                                    {
                                        return musId;
                                    }
                                }
                            }
                            return null;
                        },

                        // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B)
                        SetRhythmOnsetsForPartMusician =
                            CreateSetRhythmOnsetsForPartMusician(rhythmOnsetsByPart, _settings),
                        GetRhythmOnsetsForPart =
                            CreateGetRhythmOnsetsForPart(rhythmOnsetsByPart)
                    };

                    // TODO: PASS 0: generate chord progressions

                    // PASS 1: generate all except Harmony (so Harmony can read Melody/Lead)
                    for (int i = 0; i < part.Tracks.Count; i++)
                    {
                        var cfg = part.Tracks[i];
                        if (cfg.Role == TrackRole.Harmony) continue;

                        // Deterministically seed a RNG for THIS track in THIS rep
                        // (derived from the caller-supplied base seed when present).
                        var trackSeed = ResolveTrackSeedSong(
                            baseSeed, entry.PartIndex, rep, cfg.Role, cfg.MusicianId);

                        var trackRng = new System.Random(trackSeed);

                        GenerateOne(fullSong, part, cfg, channelMap[i], bpm,
                            partTicks, cursorTicks, ctx, producedByRole, trackRng,
                            trackSeed);

                    }

                    // PASS 2: Harmony
                    for (int i = 0; i < part.Tracks.Count; i++)
                    {
                        var cfg = part.Tracks[i];
                        if (cfg.Role != TrackRole.Harmony) continue;

                        var trackSeed = ResolveTrackSeedSong(
                            baseSeed, entry.PartIndex, rep, cfg.Role, cfg.MusicianId);

                        var trackRng = new System.Random(trackSeed);

                        GenerateOne(fullSong, part, cfg, channelMap[i], bpm,
                            partTicks, cursorTicks, ctx, producedByRole, trackRng,
                            trackSeed);
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

        public PartRender GenerateSinglePart(
            SongConfig.PartConfig part,
            IReadOnlyList<TrackRole> rolesForChannels,
            int partIndex,
            int? bpmOverride = null,
            Dictionary<MusicianTrackKey, MIDIInstrumentSO> instrumentOverrides = null,
            int? seedOverride = null,
            IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> patternOverrides = null,
            IReadOnlyDictionary<MusicianTrackKey, float> mixGains = null,
            ChordProgressionData defaultProgression = null)
        {
            if (part == null || part.Tracks == null || part.Tracks.Count == 0)
                return new PartRender { merged = new MidiFile(), stemsByMusician = new(), partTicks = 0, bpm = 120 };

            // MGP-ALWTTT-SEED-1: resolve the base seed once per render call.
            // Null => bit-identical to previous behavior (defaultSeed).
            int baseSeed = ResolveBaseSeed(seedOverride, _settings.defaultSeed);

            var full = new MidiFile();
            var metaChunk = new TrackChunk();
            full.Chunks.Add(metaChunk);
            var metaMgr = metaChunk.ManageTimedEvents();

            // --- Tempo / TS / timing ---
            // BPM-DET-1 (D-BPM1=A): bpmOverride (host) > part.ExplicitBpm >
            // seeded roll. ExplicitBpm becomes a live reader here and in
            // GenerateSong; for every current caller it is null and/or an override
            // is supplied, so the golden bpmOverride path stays bit-identical.
            int bpm = bpmOverride
                ?? part.ExplicitBpm
                ?? RollTempoBpm(ResolveTempoSeed(baseSeed, partIndex),
                                part.TempoRange, TempoRule.MultiplesOfTen);
            if (_settings?.logGenerator == true)
            {
                Debug.Log($"{LogTag} [BPM] Part='{part.Name}' idx={partIndex} " +
                          $"chosenBPM={bpm} (override={(bpmOverride.HasValue ? "yes" : "no")})");
            }
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

            int tsNum = TimeSignatureProperties[part.TimeSignature].BeatsPerMeasure;
            int tsDen = TimeSignatureProperties[part.TimeSignature].BeatUnit;

            // Stamp TS & tempo at tick 0
            metaMgr.Objects.Add(new TimedEvent(new TimeSignatureEvent((byte)tsNum, (byte)tsDen, 24, 8), 0));
            int usPerQuarter = Mathf.RoundToInt(60000000f / Mathf.Max(1, bpm));
            metaMgr.Objects.Add(new TimedEvent(new SetTempoEvent(usPerQuarter), 0));

            var beatSpan = GetBeatSpan(part.TimeSignature);
            long ticksPerBeat = TimeConverter.ConvertFrom(beatSpan, tempoMap);
            long ticksPerMeasure = ticksPerBeat * tsNum;
            long partTicks = ticksPerMeasure * Math.Max(1, part.Measures);

            var metro = GenerateMetronomeTrackFile(
                part.TimeSignature, bpm, part.Measures, bankNumber: 1, presetNumber: 0);
            // no shift needed (single-part render starts at 0)
            MergeInto(full, metro);

            // --- Per-part caches (identical concept to GenerateSong) ---
            var progressionByPart = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();

            // MGP-ALWTTT-BASS-SOLO-1 (D-SOLO-SRC=A / D-SOLO-SURF=A2): host-supplied
            // default progression for BACKING-LESS parts, pre-seeded into the
            // shared cache before the track loop so every harmony consumer
            // (Bassline, Melody, Harmony) sees it via GetProgressionForPart.
            // Warn + ignore when a Backing track exists (D-SOLO-GUARD=A). Pure
            // dictionary write: zero rng draws (D-SOLO-DET); null => byte-identical.
            TrySeedDefaultProgression(part, defaultProgression, progressionByPart);
            var melodyByPartAndMusician = new Dictionary<SongConfig.PartConfig, Dictionary<string, List<MidiGenerator.GuideNote>>>();
            var producedByRole = new Dictionary<TrackRole, MidiFile>();
            // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B): rhythm onset channel,
            // list-backed so "first publisher" is publication order.
            var rhythmOnsetsByPart =
                new Dictionary<SongConfig.PartConfig,
                List<(string musicianId, List<MidiGenerator.RhythmOnset> onsets)>>();

            // --- GenContext (same delegates as GenerateSong) ---
            var partSeed = ResolvePartContextSeed(baseSeed, partIndex);
            var ctx = new MidiGenerator.GenContext
            {
                Settings = _settings,
                rng = new System.Random(partSeed),
                ChordVoicer = _voicer,
                chordVoicingPreset = _settings.voiceLeading,
                DefaultMelodicInstrument = part.Tracks.FirstOrDefault(t => t.Instrument != null)?.Instrument,

                GetTrackForRole = (p, role) => producedByRole.TryGetValue(role, out var f) ? f : null,

                ExtractMonophonicNotes = (mf) =>
                    mf?.GetNotes()?.OrderBy(n => n.Time).ToList()
                    ?? new List<Melanchall.DryWetMidi.Interaction.Note>(),

                FindChordEventAt = (prog, tmap, ts, absTicks) => prog?.FindChordEventAt(tmap, ts, absTicks),

                // chord progression cache
                GetProgressionForPart = (p) =>
                {
                    if (progressionByPart.TryGetValue(p, out var pr)) return pr;
                    return FindProgressionForPart(p);
                },
                SetProgressionForPart = CreateSetProgressionForPart(progressionByPart),

                // tonality profile lookup delegated to settings
                GetTonalityProfileForPart = (p) =>
                {
                    return _settings != null ? _settings.GetProfileForTonality(p.Tonality) : null;
                },

                // melody cache (per part, per musician)
                GetMelodyForPartMusician = (p, musicianId) =>
                {
                    if (melodyByPartAndMusician.TryGetValue(p, out var dictForPart) &&
                        !string.IsNullOrEmpty(musicianId) &&
                        dictForPart.TryGetValue(musicianId, out var guideNotes))
                        return guideNotes;
                    return null;
                },
                SetMelodyForPartMusician = (p, musicianId, guideNotes) =>
                {
                    if (string.IsNullOrEmpty(musicianId) || guideNotes == null) return;

                    if (!melodyByPartAndMusician.TryGetValue(p, out var dictForPart))
                    {
                        dictForPart = new Dictionary<string, List<MidiGenerator.GuideNote>>();
                        melodyByPartAndMusician[p] = dictForPart;
                    }
                    dictForPart[musicianId] = guideNotes;

                    if (_settings?.logGenerator == true)
                        Debug.Log($"<color=yellow>{LogTag} Cached melody for part '{p.Name}' " +
                                  $"musician='{musicianId}' notes={guideNotes.Count}</color>");
                },
                GetFirstMelodyMusicianIdForPart = (p) =>
                {
                    if (melodyByPartAndMusician.TryGetValue(p, out var dictForPart))
                    {
                        foreach (var kvp in dictForPart)
                        {
                            var musId = kvp.Key;
                            var notes = kvp.Value;
                            if (!string.IsNullOrEmpty(musId) && notes != null && notes.Count > 0)
                                return musId;
                        }
                    }
                    return null;
                },

                // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B)
                SetRhythmOnsetsForPartMusician =
                    CreateSetRhythmOnsetsForPartMusician(rhythmOnsetsByPart, _settings),
                GetRhythmOnsetsForPart =
                    CreateGetRhythmOnsetsForPart(rhythmOnsetsByPart)
            };

            // Channel allocation must mirror the session channel layout
            var channelMap =
                BuildChannelMap((rolesForChannels ?? Array.Empty<TrackRole>()).ToList());


            var render = new PartRender { merged = full, partTicks = partTicks, bpm = bpm };

            // Local helper: run one generation “pass” controlled by a role predicate
            void GeneratePass(Func<TrackRole, bool> rolePredicate)
            {
                for (int i = 0; i < part.Tracks.Count; i++)
                {
                    var cfg = part.Tracks[i];
                    if (!rolePredicate(cfg.Role)) continue;

                    // MGP-ALWTTT-DBG-1: all per-track lookups keyed on
                    // (musicianId, role) — D-DBG1=A.
                    var trackKey = new MusicianTrackKey(cfg.MusicianId, cfg.Role);

                    // Honor pinned instrument, if any
                    if (instrumentOverrides != null
                        && !string.IsNullOrEmpty(cfg.MusicianId)
                        && instrumentOverrides.TryGetValue(trackKey, out var inst)
                        && inst != null)
                    {
                        if (_settings?.logGenerator == true)
                            Debug.Log($"{LogTag} [Override] Using pinned instrument '{inst.InstrumentName}' for mus='{cfg.MusicianId}' role={cfg.Role}.");
                        cfg.Instrument = inst; // composer must honor this
                    }

                    // MGP-ALWTTT-DBG-3 (Ask C): per-render pattern override for
                    // THIS track only. Stateless: looked up per call and handed
                    // to GenerateOne, which swap/restores it on the context.
                    PatternDataSO patternOverride = null;
                    if (patternOverrides != null &&
                        patternOverrides.TryGetValue(trackKey, out var po))
                    {
                        patternOverride = po;
                    }

                    // MGP-MIX-1 (D-MIX-2=A / D-MIX-4=A): per-render mix gain
                    // for THIS track only. Stateless per call, same discipline
                    // as patternOverride. Rhythm entries are ignored in v1:
                    // every Rhythm track lives on shared channel 9, so a
                    // per-musician CC7 there would leak across drummers.
                    float? mixGain = null;
                    if (mixGains != null &&
                        mixGains.TryGetValue(trackKey, out var mg))
                    {
                        if (cfg.Role == TrackRole.Rhythm)
                        {
                            Debug.LogWarning(
                                $"{LogTag} [MixGain] Rhythm entries are " +
                                $"ignored in v1 (shared channel 9) — " +
                                $"mus='{cfg.MusicianId}'. No CC7 emitted.");
                        }
                        else
                        {
                            mixGain = mg;
                        }
                    }

                    // MGP-ALWTTT-DBG-1 (Ask A): collection sink. The composer
                    // fills the content fields; identity (musicianId, role) is
                    // stamped HERE, authoritatively, from the track config.
                    void CollectResolved(ResolvedTrackChoice choice)
                    {
                        if (choice == null) return;
                        choice.musicianId = cfg.MusicianId;
                        choice.role = cfg.Role;
                        render.resolvedByTrack[trackKey] = choice;
                    }

                    // Deterministic per-track RNG (derived from the caller-supplied
                    // base seed when present).
                    var trackSeed = ResolveTrackSeedPart(baseSeed, partIndex, cfg.Role, cfg.MusicianId);
                    var trackRng = new System.Random(trackSeed);

                    GenerateOne(full, part, cfg, channelMap[i], bpm,
                        partTicks, cursorTicks: 0, ctx, producedByRole, trackRng,
                        trackSeed, patternOverride, CollectResolved,
                        mixGain,
                        // MGP-MIX-1 (D-MIX-5=A): record what was actually
                        // emitted; keyed identically to every other surface.
                        cc7 => render.appliedCc7ByTrack[trackKey] = cc7);

                    // Report back the actually-used instrument so caller can pin it
                    if (!string.IsNullOrEmpty(cfg.MusicianId) && cfg.Instrument != null)
                        render.melInstByMusician[trackKey] = cfg.Instrument;
                }
            }

            // PASS 1: everything except Harmony
            GeneratePass(role => role != TrackRole.Harmony);

            // PASS 2: only Harmony
            GeneratePass(role => role == TrackRole.Harmony);

            // Safety boundary at exact end of the part
            long endTick = partTicks; // cursorTicks = 0 in single-part
            metaMgr.Objects.Add(new TimedEvent(
                new ControlChangeEvent(
                    (SevenBitNumber)(byte)ControlName.AllSoundOff, (SevenBitNumber)0)
                { Channel = (FourBitNumber)MidiGenerator.MetronomeChannel }, endTick));

            // --- Collect stems by (musicianId, role) — MGP-ALWTTT-DBG-1 (ID-1=A):
            // the tag itself carries both fields ("mus:{id}:{role}", stamped by
            // TagTrackWithMusician in GenerateOne), so the chunk remains the
            // single source of its own identity (no side maps to keep in sync).
            render.stemsByMusician = new Dictionary<MusicianTrackKey, MidiFile>();
            foreach (var chunk in full.GetTrackChunks())
            {
                var tag = chunk.Events.OfType<TextEvent>()
                            .FirstOrDefault(te => te.Text != null && te.Text.StartsWith("mus:"));
                if (tag != null && TryParseMusicianTag(tag.Text, out var musId, out var role))
                {
                    var stemFile = new MidiFile(new TrackChunk(chunk.Events.ToArray()));
                    render.stemsByMusician[new MusicianTrackKey(musId, role)] = stemFile;
                }
            }

            metaMgr.Dispose();
            if (_settings?.logGenerator == true) LogTrackEnds(full, $"Part[{partIndex}]");

            return render;
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
            IDictionary<TrackRole, MidiFile> producedByRole,
            System.Random rng,
            int trackSeed,
            // MGP-ALWTTT-DBG (Ask C / Ask A): both trailing params are
            // stateless per call and default to null — GenerateSong passes
            // nothing (no override channel, no PartRender to collect into).
            PatternDataSO patternOverride = null,
            Action<ResolvedTrackChoice> reportResolved = null,
            // MGP-MIX-1: stateless per call, defaults preserve the exact
            // 1.1.0 behavior. mixGain is pre-filtered by the caller (Rhythm
            // never reaches here with a value; D-MIX-4=A).
            float? mixGain = null,
            Action<int> reportAppliedCc7 = null)
        {
            if (!_factories.TryGetValue(cfg.Role, out var factory))
            {
                Debug.LogWarning($"{LogTag} No composer factory for role {cfg.Role}");
                return;
            }

            // Ask the factory to build the right composer for THIS track
            var composer = factory.CreateFor(part, cfg, ctx);
            if (composer == null)
            {
                Debug.LogWarning($"{LogTag} Factory returned null composer for role {cfg.Role}");
                return;
            }

            if (_settings?.logGenerator == true)
            {
                Debug.Log($"{LogTag} Start part='{part.Name}' role={cfg.Role} " +
                    $"ch={channel} inst={InstName(cfg)}" +
                    $"@tick={cursorTicks} lenTicks={partTicks}");
            }

            var prev = ctx.rng;
            if (rng != null) ctx.rng = rng;
            // MGP-ALWTTT-ARTIC-1: expose the seed int behind trackRng so
            // composers can derive dedicated substreams (same swap/restore
            // discipline as rng).
            var prevSeed = ctx.trackSeed;
            ctx.trackSeed = trackSeed;
            // MGP-ALWTTT-DBG (D-DBG2=A / D-DBG4=A): install the per-render
            // pattern override and the readback sink for exactly the duration
            // of THIS Compose — the same swap/restore discipline as rng and
            // trackSeed, so neither can leak across tracks or renders.
            var prevOverride = ctx.patternOverride;
            ctx.patternOverride = patternOverride;
            var prevReport = ctx.ReportResolved;
            ctx.ReportResolved = reportResolved;

            var trackFile = composer.Compose(part, cfg, bpm, channel, ctx);

            ctx.ReportResolved = prevReport;
            ctx.patternOverride = prevOverride;
            ctx.trackSeed = prevSeed;
            if (rng != null) ctx.rng = prev;

            TrimFileToLength(trackFile, partTicks);
            TagTrackWithMusician(trackFile, cfg.MusicianId, cfg.Role);

            // MGP-MIX-1 (D-MIX-1=A / D-MIX-3): consumer mix gain, applied as
            // one CC7 on this track's channel, in the generated bytes (never a
            // playback-layer state — IMixController is a separate, live-mix
            // concern). Multiplicative law: effective = volume01 * gain,
            // identity mapped to the GM channel-volume default (100), so
            // gain 1.0 is level-neutral and gains up to 1.27 have headroom.
            // volume01=0 or gain=0 => CC7=0: the track is muted but its note
            // events remain in the file. Applied BEFORE ShiftFile so the CC7
            // travels with the bank/patch preamble to the part start. No RNG,
            // no seed involvement — determinism is by construction.
            if (mixGain.HasValue)
            {
                float vol01 = cfg.Instrument != null ? cfg.Instrument.volume01 : 1f;
                int cc7 = Mathf.Clamp(
                    Mathf.RoundToInt(vol01 * mixGain.Value * 100f), 0, 127);
                MidiGenerator.ApplyChannelVolume(trackFile, channel, cc7);
                reportAppliedCc7?.Invoke(cc7);

                if (_settings?.logGenerator == true)
                {
                    Debug.Log($"{LogTag} [MixGain] mus='{cfg.MusicianId}' " +
                              $"role={cfg.Role} ch={channel} vol01={vol01:0.###} " +
                              $"gain={mixGain.Value:0.###} => CC7={cc7}");
                }
            }

            if (_settings?.logGenerator == true)
            {
                var (tracks0, notes0, last0) = Inspect(trackFile);
                Debug.Log(
                    $"{LogTag} Trimmed [{cfg.Role}] ch={channel} inst='{InstName(cfg)}' " +
                    $"pattern='{PatternName(cfg)}' tracks={tracks0} notes={notes0} " +
                    $"lastTickRelative={last0} lenTicks={partTicks}");
            }

            ShiftFile(trackFile, cursorTicks);
            MergeInto(fullSong, trackFile);

            producedByRole[cfg.Role] = trackFile;

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, last) = Inspect(trackFile);
                Debug.Log(
                    $"{LogTag} Merged [{cfg.Role}] ch={channel} inst='{InstName(cfg)}' " +
                    $"pattern='{PatternName(cfg)}' tracks={tracks} notes={notes} " +
                    $"lastTickAbsolute={last} cursorTicks={cursorTicks} lenTicks={partTicks}");
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
            {
                for (int beat = 0; beat < ts.BeatsPerMeasure; beat++)
                {
                    var beatSpan = GetBeatSpan(timeSignature);
                    pb.Note(beat == 0 ? tic : tac, beatSpan);
                }
            }

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

        // MGP-ALWTTT-DBG-1 (ID-1=A): the "mus:" tag now carries the role —
        // "mus:{musicianId}:{TrackRole}" — because a musicianId alone cannot
        // disambiguate a musician owning two roles. The tag format is internal
        // surface (stamped and parsed only here); parse tolerates ':' inside
        // musicianId by treating the LAST segment as the role. A legacy
        // "mus:{id}" tag (no role segment) fails the parse and is skipped —
        // stamping and collection change together in this file, so no mixed
        // state exists within a render.
        private static void TagTrackWithMusician(
            MidiFile trackFile, string musicianId, TrackRole role)
        {
            var chunk = trackFile.GetTrackChunks().FirstOrDefault();
            if (chunk == null || string.IsNullOrEmpty(musicianId)) return;
            chunk.Events.Insert(0, new TextEvent(FormatMusicianTag(musicianId, role)));
        }

        // Internal for test access (Tests/Editor/SongOrchestratorKeyingTests.cs).
        public static string FormatMusicianTag(string musicianId, TrackRole role)
            => $"mus:{musicianId}:{role}";

        // Internal for test access (Tests/Editor/SongOrchestratorKeyingTests.cs).
        public static bool TryParseMusicianTag(
            string text, out string musicianId, out TrackRole role)
        {
            musicianId = null;
            role = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (!text.StartsWith("mus:", StringComparison.Ordinal)) return false;

            var payload = text.Substring(4);
            int sep = payload.LastIndexOf(':');
            if (sep <= 0 || sep >= payload.Length - 1) return false;

            var roleStr = payload.Substring(sep + 1);
            if (!Enum.TryParse(roleStr, out role)) return false;

            musicianId = payload.Substring(0, sep);
            return true;
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

        // ── Seed derivation seams (MGP-ALWTTT-SEED-1) ─────────────────────────
        // Internal for test access (Tests/Editor/SongOrchestratorSeedTests.cs).
        // These must stay bit-identical to the pre-batch inline expressions when
        // the caller supplies no seed: baseSeed == settings.defaultSeed reproduces
        // the original strings and arithmetic exactly. Seed POLICY (when the seed
        // changes, what it derives from) is host-side; the package only consumes
        // what it is given and never invents per-render entropy.

        public static int ResolveBaseSeed(int? seedOverride, int defaultSeed)
            => seedOverride ?? defaultSeed;

        // GenerateSong ctx.rng — original operator precedence preserved:
        // (baseSeed + partIndex * 397) ^ rep.
        public static int ResolveRepContextSeed(int baseSeed, int partIndex, int rep)
            => baseSeed + partIndex * 397 ^ rep;

        // GenerateSinglePart ctx.rng.
        public static int ResolvePartContextSeed(int baseSeed, int partIndex)
            => baseSeed + partIndex * 397;

        public static int ResolveTrackSeedSong(
            int baseSeed, int partIndex, int rep, TrackRole role, string musicianId)
            => StableHash32($"{baseSeed}|p={partIndex}|rep={rep}|r={role}|m={musicianId}");

        public static int ResolveTrackSeedPart(
            int baseSeed, int partIndex, TrackRole role, string musicianId)
            => StableHash32($"{baseSeed}|p={partIndex}|r={role}|m={musicianId}");

        // MGP-ALWTTT-ARTIC-1: dedicated articulation substream seed. Derived
        // from the per-track seed (itself derived from baseSeed, SEED-1 chain),
        // so the random-articulation stream is fully caller-seed deterministic
        // WITHOUT consuming ctx.rng (CA-T1 shared-stream hazard). Consumed by
        // ChordTrackComposer when a backing card selects
        // ChordExpressionType.Random.
        public static int ResolveArticulationSeed(int trackSeed)
            => StableHash32($"{trackSeed}|artic");

        // CA-V1 (D-V1-RATE-STREAM=A): dedicated arpeggio-rate substream. Kept
        // separate from "|artic" so that toggling the rate sentinel on a card
        // cannot shift the figure roll sequence — the same orthogonality the
        // articulation stream has against ctx.rng.
        public static int ResolveArticulationRateSeed(int trackSeed)
            => StableHash32($"{trackSeed}|articrate");

        // CA-V1 (D-V1-JIT-SRC=A): dedicated velocity-jitter substream. Consumed
        // as a SEED for a pure mix, not as a stream — see VelocityJitter. Since
        // trackSeed already folds in role + musicianId, backing and bass on the
        // same part jitter independently by construction.
        public static int ResolveVelocityJitterSeed(int trackSeed)
            => StableHash32($"{trackSeed}|articvel");

        // B3 WALK-2 (D-W2-RNG=B): dedicated walk substream seed. Consumed as
        // the KEY of a pure per-(event, hit) integer mix in BassTrackComposer
        // (the VelocityJitter idiom), never as a stateful stream — so there is
        // no draw order to protect and no roll discipline to maintain.
        public static int ResolveWalkSeed(int trackSeed)
            => StableHash32($"{trackSeed}|walk");

        // BPM-DET-1 (D-BPM2=A): dedicated tempo substream seed. FNV-1a over a
        // documented string keyed on (baseSeed, partIndex) — the tempo is chosen
        // per part-occurrence, not per rep, so rep is intentionally NOT in the
        // string (D-BPM2-KEY=A: two occurrences of the same part index roll the
        // same tempo). Distinct from the arithmetic ctx.rng seeds, so the tempo
        // roll cannot perturb any per-part/track draw.
        public static int ResolveTempoSeed(int baseSeed, int partIndex)
            => StableHash32($"{baseSeed}|p={partIndex}|tempo");

        // BPM-DET-1 (D-BPM3=B): the seeded tempo roll lives in the orchestrator,
        // NOT in MusicTheory (which stays a dumb, unseeded helper). Same valid-BPM
        // set as MusicTheory.GetBPMFromRange (via GetValidBpms) but drawn from a
        // seeded stream. Degenerate empty set (not reachable for the shipped ranges
        // + MultiplesOfTen) falls back to 120, matching the empty-part fallback.
        public static int RollTempoBpm(int tempoSeed, TempoRange range, TempoRule rule)
        {
            var valid = GetValidBpms(range, rule);
            if (valid.Count == 0) return 120;
            var rng = new System.Random(tempoSeed);
            return valid[rng.Next(valid.Count)];
        }

        public static int StableHash32(string s)
        {
            unchecked
            {
                uint hash = 2166136261;          // FNV-1a 32-bit
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }

        // ── MGP-ALWTTT-BASS-SOLO-1 (D-SOLO-SRC=A / D-SOLO-SURF=A2) ────────────
        // PUBLIC test seam, matching the house pattern of the other pure seams
        // in this codebase (SongOrchestrator.ResolveTrackSeedPart,
        // ChordTrackComposer.TryDirectionalFirstChordCore): both are public
        // despite AssemblyInfo.cs describing them as internal-with-
        // InternalsVisibleTo. See the batch notes — that InternalsVisibleTo
        // entry appears inert (test assembly name mismatch), so public is the
        // de-facto convention here.

        /// <summary>Outcome of the per-render default-progression seeding.</summary>
        public enum DefaultProgressionSeedResult
        {
            NotSupplied,            // no default passed — byte-identical legacy path
            Seeded,                 // part has no Backing track; cache pre-seeded
            IgnoredBackingPresent,  // D-SOLO-GUARD=A: Backing owns harmony — warn+ignore
        }

        /// <summary>
        /// MGP-ALWTTT-BASS-SOLO-1. Pre-seeds the per-render shared-progression
        /// cache with a HOST-supplied default so harmony-consuming tracks
        /// (Bassline, Melody, Harmony) can render in a part that has no Backing
        /// track — otherwise the only publishers of the shared channel are the
        /// Backing composer (card palette / override / procedural) and the
        /// authored fallback (<see cref="FindProgressionForPart"/>, which reads
        /// the Backing track's Pattern), so a backing-less part renders silence
        /// on those roles (Bass SSoT §1).
        ///
        /// D-SOLO-GUARD=A: if the part HAS a Backing track the default is
        /// warn + ignore — the Backing track owns the shared harmony. Seeding
        /// under it would fork the render (the Backing card-palette publish is
        /// guarded by "don't overwrite": backing would sound its own pick while
        /// the other tracks consumed the pre-seeded default). To impose harmony
        /// on a part WITH backing, use the per-render patternOverride on the
        /// Backing track (precedence step 0, imposes unconditionally).
        ///
        /// D-SOLO-NORM=A: seeded AS-IS; TS normalization is the Backing
        /// composer's site and does not run here. Hosts must author the default
        /// in the part TS (documented discipline; mirrors the Bass SSoT §1
        /// normalization-order hazard).
        ///
        /// D-SOLO-DET: pure dictionary write — zero rng draws, no stream
        /// perturbation. A null default leaves the render byte-identical.
        ///
        /// Clone-on-seed mirrors the override discipline (decouples runtime
        /// state from the asset instance); the clone keeps the source asset's
        /// name so the Ask-A readback stays meaningful.
        /// </summary>
        public static DefaultProgressionSeedResult TrySeedDefaultProgression(
            SongConfig.PartConfig part,
            ChordProgressionData defaultProgression,
            Dictionary<SongConfig.PartConfig, ChordProgressionData> progressionByPart)
        {
            if (defaultProgression == null)
                return DefaultProgressionSeedResult.NotSupplied;

            if (part?.Tracks != null &&
                part.Tracks.Any(t => t != null && t.Role == TrackRole.Backing))
            {
                Debug.LogWarning(
                    $"[SongOrchestrator] defaultProgression ('{defaultProgression.name}') " +
                    $"supplied but part '{part.Name}' has a Backing track. The Backing " +
                    $"track owns the shared harmony (D-SOLO-GUARD=A); to impose a " +
                    $"progression on a part WITH backing, use the per-render " +
                    $"patternOverride on the Backing track instead. Ignoring.");
                return DefaultProgressionSeedResult.IgnoredBackingPresent;
            }

            // RUNTIME-REQUALITY (D-RQ-SITE, site 2): in the backing-less path
            // no ChordTrackComposer runs, so the diatonic re-resolution for
            // opt-in assets happens here, against the part's tonality at seed
            // time. AsAuthored (default) returns the SAME reference => the
            // plain clone below runs, exactly the pre-REQUALITY behavior.
            // When requality DID clone, that clone is already a fresh runtime
            // instance — reuse it (single clone, no double Instantiate).
            var requalified = ChordProgressionRequality.ApplyDiatonicRequality(
                defaultProgression, part.Tonality);
            var clone = ReferenceEquals(requalified, defaultProgression)
                ? UnityEngine.Object.Instantiate(defaultProgression)
                : requalified;
            clone.name = defaultProgression.name; // keep readback identity (no "(Clone)")
            progressionByPart[part] = clone;
            return DefaultProgressionSeedResult.Seeded;
        }

        private Action<SongConfig.PartConfig, ChordProgressionData>
            CreateSetProgressionForPart(
            Dictionary<SongConfig.PartConfig, ChordProgressionData> progressionByPart)
        {
            return (p, pr) =>
            {
                progressionByPart[p] = pr;

                if (_settings?.logGenerator == true && pr != null)
                {
                    // 1) Roman numerals (existing behaviour)
                    var seqRoman = string.Join("  ",
                        pr.events.Select(e => ToRomanRich(e.degree, e.quality)));

                    // 2) Concrete chord labels using MusicTheory
                    var chordLabels = pr.events.Select(e =>
                    {
                        var pcs = ChordPitchClasses(p.Tonality, p.RootNote, e.degree, e.quality);
                        if (pcs == null || pcs.Length == 0)
                            return "?";

                        var rootPc = pcs[0];

                        // Spell root relative to the key (C, D♭, etc.)
                        var rootLabel = SpellNoteForDegree(rootPc, p.RootNote, (int)e.degree);

                        var notesStr = string.Join(" ", pcs.Select(n => n.ToString()));

                        // Example: "Cmaj7 [C E G B]" – e.quality.ToString() already carries the suffix
                        return $"{rootLabel}{e.quality} [{notesStr}]";
                    });

                    var seqChords = string.Join("  ", chordLabels);

                    Debug.Log(
                        $"<color=orange>{LogTag} " +
                        $"Cached progression for part '{p.Name}': {seqRoman}</color>");
                    Debug.Log(
                        $"<color=orange>{LogTag} " +
                        $"Progression chords for part '{p.Name}': {seqChords}</color>");
                }
            };
        }

        // ------------------------------------------------------------------
        // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B) — rhythm onset channel.
        // Same factory idiom as CreateSetProgressionForPart. The store is
        // LIST-backed per part so "first publisher" is publication order by
        // construction (never dictionary-enumeration order). Re-publication by
        // the same (part, musicianId) replaces its own entry in place (a
        // composer publishes at most once per Compose; the guard covers
        // repeated renders against a stale cache, which cannot occur today but
        // costs nothing). Empty/null payloads are ignored — publishing nothing
        // must be indistinguishable from not publishing (degrade contract).
        // ------------------------------------------------------------------
        public static Action<SongConfig.PartConfig, string, List<MidiGenerator.RhythmOnset>>
            CreateSetRhythmOnsetsForPartMusician(
            Dictionary<SongConfig.PartConfig,
                List<(string musicianId, List<MidiGenerator.RhythmOnset> onsets)>> store,
            MidiGenPlayConfig settings)
        {
            return (p, musicianId, onsets) =>
            {
                if (p == null || onsets == null || onsets.Count == 0) return;

                if (!store.TryGetValue(p, out var list))
                {
                    list = new List<(string, List<MidiGenerator.RhythmOnset>)>();
                    store[p] = list;
                }

                var key = musicianId ?? "";
                int existing = list.FindIndex(e => (e.musicianId ?? "") == key);
                if (existing >= 0) list[existing] = (key, onsets);
                else list.Add((key, onsets));

                if (settings?.logGenerator == true)
                {
                    Debug.Log($"<color=cyan>{LogTag} Published rhythm onsets for part " +
                              $"'{p.Name}' musician='{key}' count={onsets.Count}</color>");
                }
            };
        }

        public static Func<SongConfig.PartConfig, List<MidiGenerator.RhythmOnset>>
            CreateGetRhythmOnsetsForPart(
            Dictionary<SongConfig.PartConfig,
                List<(string musicianId, List<MidiGenerator.RhythmOnset> onsets)>> store)
        {
            return (p) =>
            {
                if (p == null || !store.TryGetValue(p, out var list)) return null;
                foreach (var (_, onsets) in list)
                    if (onsets != null && onsets.Count > 0)
                        return onsets;
                return null;
            };
        }

        private static MusicalTimeSpan GetBeatSpan(MusicTheory.MusicTheory.TimeSignature ts)
        {
            int unit = TimeSignatureProperties[ts].BeatUnit;
            if (unit == 2) return MusicalTimeSpan.Half;
            if (unit == 4) return MusicalTimeSpan.Quarter;
            if (unit == 8) return MusicalTimeSpan.Eighth;
            if (unit == 16) return MusicalTimeSpan.Sixteenth;
            return MusicalTimeSpan.Quarter;
        }
    }
}
using System.Collections.Generic;
using System.Linq;

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;   // ITimeSpan
using Melanchall.DryWetMidi.MusicTheory;

using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Composition
{
    /// Backing/chord track composer.
    /// - Voices chords via injected IChordVoicer (or simple realization if disabled)
    /// - Repeats progression to fill the part
    /// - Stamps "chd:..." meta tags
    /// - Sets bank/patch on ALL chunks and forces channel on ALL ChannelEvents
    public sealed class ChordTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly IChordVoicer _voicer;
        private readonly VoiceLeadingConfig _vl;

        private readonly struct DiaChord
        {
            public readonly ScaleDegree degree;
            public readonly ChordQuality quality;
            public readonly NoteName root;
            public readonly string roman;
            public readonly string symbol;
            public DiaChord(ScaleDegree d, ChordQuality q, NoteName r, 
                string rn, string sym)
            { degree = d; quality = q; root = r; roman = rn; symbol = sym; }
        }

        public ChordTrackComposer(MidiGenPlayConfig settings, IChordVoicer voicer)
        {
            _settings = settings;
            _voicer = voicer;
            _vl = settings != null ? settings.voiceLeading : null;
        }

        /// <summary>
        /// Creates a backing/chord MIDI track for the given part/track config.
        /// If a ChordProgressionData is available (authored or cached), renders it;
        /// otherwise builds a procedural progression and renders that.
        /// </summary>
        /// <param name="part">Song part (tonality, meter, measures, tempo range).</param>
        /// <param name="cfg">Track configuration (instrument, parameters/pattern).</param>
        /// <param name="bpm">Beats per minute for this part repetition.</param>
        /// <param name="channel">MIDI channel (0..15) assigned by the orchestrator.</param>
        /// <param name="ctx">Cross-track context (rng, voicer, progression cache, helpers).</param>
        /// <returns>MIDI file (one or more chunks) containing the backing track.</returns>
        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var instrument = (MIDIInstrumentSO)cfg.Instrument;
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                       ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (_settings?.logGenerator == true)
            {
                Debug.Log($"<color=green>[ChordTrackComposer]</color> part='{part.Name}' " +
                          $"inst='{instrument?.InstrumentName}' bpm={bpm} ch={channel} " +
                          $"progression='{prog?.displayName ?? "(null)"}' evts={prog?.events?.Count ?? 0}");
            }

            // degree + quality → chord pcs
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            if (_settings?.logGenerator == true)
            {
                var spelled = Enumerable.Range(0, 7)
                    .Select(i => SpellNoteForDegree(scaleNames[i], part.RootNote, i))
                    .ToArray();
                Debug.Log($"<color=yellow>[ChordTrack] Tonality: {part.Tonality} over {part.RootNote}  " +
                          $"Scale labels: [{string.Join(", ", spelled)}]</color>");
            }

            var (triads, sevenths) = BuildDiatonicSets(part.Tonality, part.RootNote);
            if (_settings?.logGenerator == true)
                LogDiatonicSets(part.Tonality, part.RootNote, triads, sevenths, showSymbols: false);

            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                if (_settings?.logGenerator == true)
                    Debug.Log("[ChordTrackComposer] Procedural backing (no ChordProgressionData).");
                return ComposeProcedural(instrument, bpm, part, cfg, ctx, channel);
            }

            // Grid info
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int partTotalSteps = Mathf.Max(1, part.Measures) * stepsPerMeasure;
            int patternMeasures = Mathf.Max(1, prog.measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int numRepeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var chordMarkers = new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            var pb = new PatternBuilder();

            // Choose voicer
            var voicer = ctx?.ChordVoicer ?? _voicer;
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int repeatStepOffset = repeat * patternTotalSteps;

                foreach (var e in prog.events)
                {
                    var degreeRoot = scaleNames[(int)e.degree];
                    var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                    var playable =
                        (_vl != null && _vl.enableVoiceLeading && voicer != null)
                        ? voicer.VoiceChord(chordPcs, instrument, lastVoicing, _vl)
                        : RealizeChordSimple(chordPcs, instrument, ctx?.rng);

                    lastVoicing = playable;

                    var rn = ToRomanRich(e.degree, e.quality);
                    var sym = GetChordSymbol(degreeRoot, e.quality);
                    int degIdx = ((int)e.degree) + 1;
                    string q = e.quality.ToString();

                    int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                    double startBeats = (double)startStepAbs / stepsPerBeat;
                    double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                    var startTime = MusicalTimeSpan.Quarter.Multiply(startBeats);
                    var duration = MusicalTimeSpan.Quarter.Multiply(durBeats);

                    pb.MoveToTime(startTime);
                    pb.Chord(playable, duration, (SevenBitNumber)Mathf.Clamp(e.velocity, 0, 127));

                    chordMarkers.Add((startTime, rn, sym, degIdx, q));
                }
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            // Chord tags
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);

            // Bank/Patch on ALL chunks + force channel on ALL ChannelEvents
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings != null && _settings.logGenerator)
            {
                var chunks = file.GetTrackChunks().Count();
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[ChordTrackComposer] tracks={chunks} notes={notes} lastTick={lastTick}");
            }

            return file;
        }
        /*
        private MidiFile ComposeProcedural(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            MidiGenerator.GenContext ctx,
            int channel)
        {
            var (triads, sevenths) = BuildDiatonicSets(part.Tonality, part.RootNote);
            if (_settings?.logGenerator == true)
                LogDiatonicSets(part.Tonality, part.RootNote, triads, sevenths, false);

            // --- weights (Ionian/Aeolian baselines + characteristic degrees) ---
            const float baseW = 1f, rootB = 3f, domB = 1.5f, charB = 2f;
            var degreeWeights = 
                BuildDegreeWeights(part.Tonality, part.RootNote, baseW, rootB, domB, charB);

            var voicer = ctx?.ChordVoicer ?? _voicer;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);

            var pb = new PatternBuilder().MoveToStart();
            var chordMarkers = 
                new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            var rng = ctx?.rng ?? new System.Random();
            var picked = new List<ScaleDegree>(measures);

            // Small entry/exit biases
            const float firstBarRootBonus = 2f;
            const float lastBarForceI = 1f; // just a flag we use to force I

            for (int m = 0; m < measures; m++)
            {
                ScaleDegree deg;
                if (m == measures - 1)
                {
                    deg = ScaleDegree.Tonic; // cadence to I
                }
                else
                {
                    // clone weights and bias first bar to I
                    var w = (float[])degreeWeights.Clone();
                    if (m == 0) w[(int)ScaleDegree.Tonic] += firstBarRootBonus;

                    // weighted pick 0..6
                    float total = w.Sum();
                    float pick = (float)rng.NextDouble() * total;
                    int idx = 0;
                    for (; idx < 7; idx++)
                    {
                        if (pick <= w[idx]) break;
                        pick -= w[idx];
                    }
                    if (idx >= 7) idx = 6;
                    deg = (ScaleDegree)idx;
                }
                picked.Add(deg);

                // Diatonic triad for this mode/degree (MVP)
                var q = GetDiatonicTriadQuality(part.Tonality, deg);

                // Chord pitch classes (names) → voice
                var pcs = ChordPitchClasses(part.Tonality, part.RootNote, deg, q);
                var playable =
                    (_vl != null && _vl.enableVoiceLeading && voicer != null)
                    ? voicer.VoiceChord(pcs, instrument, lastVoicing, _vl)
                    : RealizeChordSimple(pcs, instrument, ctx?.rng);

                lastVoicing = playable;

                double startBeats = m * beatsPerBar;
                double durBeats = beatsPerBar;

                var startTime = MusicalTimeSpan.Quarter.Multiply(startBeats);
                var duration = MusicalTimeSpan.Quarter.Multiply(durBeats);

                pb.MoveToTime(startTime);
                pb.Chord(playable, duration, (SevenBitNumber)96);

                var rn = ToRomanRich(deg, q);
                var sym = GetChordSymbol(pcs[0], q);
                chordMarkers.Add((startTime, rn, sym, ((int)deg) + 1, q.ToString()));
            }

            var file = pb.Build().ToFile(tempoMap);
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            // --- logs for tuning ---
            if (_settings?.logGenerator == true)
            {
                string weightsLine = string.Join("  ",
                    Enumerable.Range(0, 7).Select(i => $"{RomanBare((ScaleDegree)i)}={degreeWeights[i]:0.##}"));
                Debug.Log($"[ChordTrack] Degree weights: {weightsLine}");

                // Rebuild degree+quality per bar, then symbols (root+quality) for the same bars
                var dq = picked.Select(d => (deg: d, q: GetDiatonicTriadQuality(part.Tonality, d))).ToList();
                var symbols = dq.Select(t =>
                {
                    var pcs = ChordPitchClasses(part.Tonality, part.RootNote, t.deg, t.q); // degree root at pcs[0]
                    return GetChordSymbol(pcs[0], t.q);
                }).ToList();

                string seqDegrees = string.Join("  ", dq.Select(t => ToRomanRich(t.deg, t.q)));
                string seqChords = string.Join("  ", symbols);
                string seqCombined = string.Join("  ",
                    dq.Select((t, i) => $"{ToRomanRich(t.deg, t.q)}[{symbols[i]}]"));

                //Debug.Log($"<color=yellow>[ChordTrack] Procedural progression (degrees): {seqDegrees}</color>");
                //Debug.Log($"<color=yellow>[ChordTrack] Procedural progression (chords):  {seqChords}</color>");
                Debug.Log($"<color=yellow>[ChordTrack] Procedural progression:           {seqCombined}</color>");
            }

            return file;
        }*/

        /// <summary>
        /// Procedural path: builds a per-bar chord progression using weighted modal rules,
        /// stores it in the GenContext, then renders it to a MIDI file.
        /// </summary>
        /// <param name="instrument">Target GM/MPTK instrument for chord playback.</param>
        /// <param name="bpm">Tempo used to compute durations for chord events.</param>
        /// <param name="part">Part providing tonality, root, meter, and length.</param>
        /// <param name="cfg">Track config (used for logging/voicing range).</param>
        /// <param name="ctx">Context (rng, voicer, SetProgressionForPart).</param>
        /// <param name="channel">MIDI channel (0..15) for this track.</param>
        /// <returns>MIDI file with the procedurally generated backing track.</returns>
        private MidiFile ComposeProcedural(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            MidiGenerator.GenContext ctx,
            int channel)
        {
            var rng = ctx?.rng ?? new System.Random();

            // Build progression and stash it in context (so bass/melody/harmony can reuse it)
            var prog = BuildProceduralProgression(part, rng);
            ctx?.SetProgressionForPart?.Invoke(part, prog); // harmless if null

            // Optional logs for tuning (reuse your existing lines if you like)
            if (_settings?.logGenerator == true)
            {
                var degs = prog.events.Select(e => ToRomanRich(e.degree, e.quality));
                Debug.Log($"[ChordTrack] Built procedural progression: {string.Join("  ", degs)}");
            }

            // Render using the same path as authored progressions
            return RenderFromProgression(instrument, bpm, part, prog, channel, ctx);
        }

        /// <summary>
        /// Inserts "chd:..." text markers with roman numeral and chord symbol for debugging/DAW display.
        /// </summary>
        /// <param name="file">Target MIDI file (first chunk is used).</param>
        /// <param name="tempoMap">Tempo map for converting musical time to ticks.</param>
        /// <param name="markers">List of (time, roman, symbol, degreeIndex, quality) tuples.</param>
        /// <param name="channel">Track MIDI channel (for embedding in the tag text).</param>
        /// <param name="verbose">If true, can emit extra logs per tag.</param>
        private static void StampChordMarkers(
            MidiFile file,
            TempoMap tempoMap,
            List<(ITimeSpan when, string roman, string symbol, int deg, string quality)> markers,
            int channel,
            bool verbose)
        {
            if (markers == null || markers.Count == 0) return;
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null) return;

            using var mgr = chunk.ManageTimedEvents();
            foreach (var cm in markers)
            {
                long tick = TimeConverter.ConvertFrom(cm.when, tempoMap);
                var txt = $"chd:{channel}:{cm.roman}:{cm.symbol}:{cm.deg}:{cm.quality}";
                mgr.Objects.Add(new TimedEvent(new TextEvent(txt), tick));
                //if (verbose) Debug.Log($"[ChordTrackComposer] tag @tick={tick} '{txt}'");
            }
        }

        /// <summary>
        /// Simple, non–voice-leading chord realization: root position within the
        /// instrument's octave range. Used when voicer is disabled or null.
        /// </summary>
        /// <param name="pcs">Chord pitch classes (note names) for the chord.</param>
        /// <param name="inst">Instrument (octave min/max define playable range).</param>
        /// <param name="rng">Optional RNG for octave selection (for deterministic tests).</param>
        /// <returns>List of DryWetMidi notes (names+octaves) to play simultaneously.</returns>
        private static IReadOnlyList<DryWetMidiNote> RealizeChordSimple(
            NoteName[] pcs, MIDIInstrumentSO inst, System.Random rng = null)
        {
            // Legacy simple realization: root-position within instrument range
            int minOct = inst.octaveMin - 1;
            int maxOct = inst.octaveMax - 1;

            int startOct = (rng != null)
                ? rng.Next(minOct, maxOct + 1)
                : UnityEngine.Random.Range(minOct, maxOct + 1);

            return pcs.Select(nn => DryWetMidiNote.Get(nn, startOct))
                      .Select(n => DryWetMidiNote.Get(n.NoteName, Mathf.Clamp(n.Octave, minOct, maxOct)))
                      .ToArray();
        }

        /// <summary>
        /// Forces every ChannelEvent in the file to the provided channel (0..15).
        /// </summary>
        /// <param name="file">MIDI file whose events will be re-channeled.</param>
        /// <param name="channel">Target MIDI channel (0..15).</param>
        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        /// <summary>
        /// Writes Bank Select (CC0/CC32) and Program Change at the head of each track chunk.
        /// </summary>
        /// <param name="file">MIDI file whose chunks will be stamped.</param>
        /// <param name="inst">Instrument data (BankName numeric, PatchIndex program).</param>
        /// <param name="channel">MIDI channel (0..15) used for the events.</param>
        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[ChordTrackComposer] Instrument bank is not numeric: '{inst.BankName}'");
                bank = 0; // fallback to 0 like old behavior if parse failed
            }

            foreach (var chunk in file.GetTrackChunks())
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                // CC0 Bank Select MSB
                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // CC32 Bank Select LSB
                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                // Program Change. Keep tiny DeltaTime after bank to ensure ordering.
                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        /// <summary>
        /// Builds the 7 diatonic triads and 7 diatonic seventh chords for the given
        /// tonality and root note, with roman labels and chord symbols spelled to degree.
        /// </summary>
        /// <param name="mode">Tonality/mode (Ionian, Dorian, etc.).</param>
        /// <param name="rootNote">Root note of the scale.</param>
        /// <returns>Two lists: triads and sevenths (degree, quality, root, roman, symbol).</returns>
        private static (List<DiaChord> triads, List<DiaChord> sevenths) BuildDiatonicSets(
            Tonality mode, NoteName rootNote)
        {
            // Scale degrees → scale note names (root mapped per degree)
            var scale = GetScaleFromTonality(mode, rootNote);
            var scaleNames = 
                GetNotesFromScale(scale, rootNote, 4, 7).Select(n => n.NoteName).ToArray();

            var tri = new List<DiaChord>(7);
            var sev = new List<DiaChord>(7);
            for (int i = 0; i < 7; i++)
            {
                var deg = (ScaleDegree)i;

                var tq = GetDiatonicTriadQuality(mode, deg);
                var tRoot = scaleNames[i];
                tri.Add(new DiaChord(deg, tq, tRoot, ToRomanRich(deg, tq),
                    GetChordSymbolSpelledForDegree(rootNote, i, tRoot, tq)));

                var sq = GetDiatonicSeventhQuality(mode, deg);
                var sRoot = scaleNames[i];
                sev.Add(new DiaChord(deg, sq, sRoot, ToRomanRich(deg, sq),
                    GetChordSymbolSpelledForDegree(rootNote, i, sRoot, sq)));
            }
            return (tri, sev);
        }

        private static void LogDiatonicSets(
            Tonality mode,
            NoteName rootNote,
            List<DiaChord> tri,
            List<DiaChord> sev,
            bool showSymbols = false)
        {
            string triLine = showSymbols
                ? string.Join("  ", tri.Select(t => t.symbol))
                : string.Join("  ", tri.Select(t => t.roman));

            string sevLine = showSymbols
                ? string.Join("  ", sev.Select(s => s.symbol))
                : string.Join("  ", sev.Select(s => s.roman));

            Debug.Log($"<color=yellow>[ChordTrack] " +
                $"Diatonic triads in {mode}/{rootNote}: {triLine}</color>");
            Debug.Log($"<color=yellow>[ChordTrack] " +
                $"Diatonic sevenths in {mode}/{rootNote}: {sevLine}</color>");
        }

        /// <summary>
        /// Builds a one-chord-per-measure ChordProgressionData using weighted
        /// modal rules (I/V emphasis + characteristic degrees) and diatonic triads.
        /// Intended for reuse by other composers (bass/melody/harmony).
        /// </summary>
        /// <param name="part">Part (tonality/root, meter, measures, tempo range).</param>
        /// <param name="rng">RNG used for weighted selection.</param>
        /// <param name="baseW">Base weight for every degree.</param>
        /// <param name="rootB">Extra weight for I.</param>
        /// <param name="domB">Extra weight for V.</param>
        /// <param name="charB">Extra weight for mode-characteristic degrees.</param>
        /// <param name="defaultVelocity">Velocity for all chords.</param>
        /// <returns>Runtime ChordProgressionData with events in step units.</returns>
        public static ChordProgressionData BuildProceduralProgression(
            SongConfig.PartConfig part,
            System.Random rng,
            float baseW = 1f, float rootB = 3f, float domB = 1.5f, float charB = 2f,
            int defaultVelocity = 96)
        {
            // Build degree weights (Ionian baseline for major family, Aeolian for minor family)
            var weights = BuildDegreeWeights(part.Tonality, part.RootNote, baseW, rootB, domB, charB);

            // Grid
            var ts = GetTimeSignatureDetails(part.TimeSignature, GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen));
            int beatsPerBar = ts.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);
            int subdivisions = 1; // one step per beat (MVP)
            int stepsPerMeasure = beatsPerBar * subdivisions;
            int totalSteps = stepsPerMeasure * measures;

            // Build anchors: one start per bar
            var anchors = new bool[totalSteps];
            for (int m = 0; m < measures; m++) anchors[m * stepsPerMeasure] = true;

            // Pick degrees per bar (weighted) + force last to I
            var degrees = new List<(ScaleDegree deg, ChordQuality q)>(measures);
            for (int m = 0; m < measures; m++)
            {
                ScaleDegree d;
                if (m == measures - 1)
                {
                    d = ScaleDegree.Tonic;
                }
                else
                {
                    var w = (float[])weights.Clone();
                    if (m == 0) w[(int)ScaleDegree.Tonic] += 2f; // small entrance bias

                    float total = w.Sum();
                    float pick = (float)rng.NextDouble() * total;
                    int idx = 0;
                    for (; idx < 7; idx++)
                    {
                        if (pick <= w[idx]) break;
                        pick -= w[idx];
                    }
                    if (idx >= 7) idx = 6;
                    d = (ScaleDegree)idx;
                }

                var q = GetDiatonicTriadQuality(part.Tonality, d);
                degrees.Add((d, q));
            }

            // Materialize ChordProgressionData (runtime)
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.measures = measures;
            prog.subdivisions = subdivisions;
            prog.events = new List<ChordProgressionData.ChordEvent>();

            // Use the provided helper to convert anchors+degrees into events with lengths
            prog.RebuildFromAnchors(anchors, degrees, defaultVelocity);

            return prog;
        }

        /// <summary>
        /// Renders a given ChordProgressionData by voicing each event's degree+quality
        /// under the part's tonality/root and writing notes at the appropriate times.
        /// </summary>
        /// <param name="instrument">Playback instrument.</param>
        /// <param name="bpm">Tempo for time conversion.</param>
        /// <param name="part">Part (tonality/root, meter, measures).</param>
        /// <param name="prog">Progression to render (events in steps).</param>
        /// <param name="channel">MIDI channel (0..15).</param>
        /// <param name="ctx">Context providing chord voicer and RNG.</param>
        /// <returns>MIDI file with the rendered progression.</returns>
        private MidiFile RenderFromProgression(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            ChordProgressionData prog,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int partTotalSteps = Mathf.Max(1, part.Measures) * stepsPerMeasure;
            int patternMeasures = Mathf.Max(1, prog.measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int numRepeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var chordMarkers = new List<(ITimeSpan when, string roman, string symbol, int deg, string quality)>();
            var pb = new PatternBuilder();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));

            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            var voicer = ctx?.ChordVoicer ?? _voicer;
            IReadOnlyList<DryWetMidiNote> lastVoicing = null;

            for (int repeat = 0; repeat < numRepeats; repeat++)
            {
                int repeatStepOffset = repeat * patternTotalSteps;

                foreach (var e in prog.events)
                {
                    var degreeRoot = scaleNames[(int)e.degree];
                    var chordPcs = GetChordNoteNames(degreeRoot, e.quality);

                    var playable =
                        (_vl != null && _vl.enableVoiceLeading && voicer != null)
                        ? voicer.VoiceChord(chordPcs, instrument, lastVoicing, _vl)
                        : RealizeChordSimple(chordPcs, instrument, ctx?.rng);

                    lastVoicing = playable;

                    var rn = ToRomanRich(e.degree, e.quality);
                    var sym = GetChordSymbol(degreeRoot, e.quality);
                    int degIdx = ((int)e.degree) + 1;
                    string q = e.quality.ToString();

                    int startStepAbs = repeatStepOffset + Mathf.Max(0, e.startStep);
                    double startBeats = (double)startStepAbs / stepsPerBeat;
                    double durBeats = (double)Mathf.Max(1, e.lengthSteps) / stepsPerBeat;

                    var startTime = MusicalTimeSpan.Quarter.Multiply(startBeats);
                    var duration = MusicalTimeSpan.Quarter.Multiply(durBeats);

                    pb.MoveToTime(startTime);
                    pb.Chord(playable, duration, (SevenBitNumber)Mathf.Clamp(e.velocity, 0, 127));

                    chordMarkers.Add((startTime, rn, sym, degIdx, q));
                }
            }

            var file = pb.Build().ToFile(tempoMap);
            StampChordMarkers(file, tempoMap, chordMarkers, channel, _settings?.logGenerator == true);
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);
            return file;
        }
    }
}

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

        private struct VampRuntime
        {
            public List<int> degreesSequence;
            public int barsRemaining;
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

        /// <summary>
        /// Procedural path: builds a per-bar chord progression using modal rules
        /// (TonalityProfileSO if available), caches it in GenContext so other tracks
        /// can reuse it, then renders it.
        /// </summary>
        /// <param name="instrument">Instrument to voice the chords on.</param>
        /// <param name="bpm">Tempo for this part repetition.</param>
        /// <param name="part">Part info (tonality, measures, time signature).</param>
        /// <param name="cfg">Track config (mostly for logging / range).</param>
        /// <param name="ctx">Per-repetition context (rng, voicer, progression cache).</param>
        /// <param name="channel">MIDI channel for this track.</param>
        /// <returns>MIDI file containing the rendered procedural backing track.</returns>
        private MidiFile ComposeProcedural(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            MidiGenerator.GenContext ctx,
            int channel)
        {
            var rng = ctx?.rng ?? new System.Random();

            // Build (or profile-drive) a progression.
            var prog = BuildProceduralProgression(part, ctx, rng,
                verbose: _settings.logGenerator == true);
            // Cache progression in GenContext so bass / melody / harmony can reuse.
            ctx?.SetProgressionForPart?.Invoke(part, prog);

            // Debug log
            if (_settings?.logGenerator == true && prog != null && prog.events != null)
            {
                var romanSeq = prog.events.Select(e => ToRomanRich(e.degree, e.quality));
                // TODO: Include chosen chords (degree + quality)
                Debug.Log($"[ChordTrack] Built procedural progression for part '{part.Name}': " +
                          string.Join("  ", romanSeq));
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
        /// Build a procedural chord progression for this part.
        /// - One chord per bar (downbeat, lasts the whole bar)
        /// - Returns a runtime ChordProgressionData ScriptableObject
        /// - If a TonalityProfileSO exists for this part's tonality, we use it
        ///   (characteristic degrees, vamp candidates, cadence rules, etc).
        ///   Otherwise we fall back to generic modal weighting.
        /// </summary>
        /// <param name="part">Song part (tonality, meter, measures, tempo range).</param>
        /// <param name="ctx">Generation context. We query ctx.GetTonalityProfileForPart(part).</param>
        /// <param name="rng">RNG to use for weighted degree picks.</param>
        /// <param name="baseW">Base weight for every scale degree when using fallback mode.</param>
        /// <param name="rootB">Extra weight for I in fallback mode.</param>
        /// <param name="domB">Extra weight for V in fallback mode.</param>
        /// <param name="charB">Extra weight for "characteristic" degrees in fallback mode.</param>
        /// <param name="defaultVelocity">Velocity to stamp on each chord event.</param>
        /// <returns>Runtime ChordProgressionData with events expressed in step units.</returns>
        public static ChordProgressionData BuildProceduralProgression(
            SongConfig.PartConfig part, MidiGenerator.GenContext ctx,
            System.Random rng,
            float baseW = 1f, float rootB = 3f, float domB = 1.5f, float charB = 2f,
            int defaultVelocity = 96,
            bool verbose = false)
        {
            TonalityProfileSO profile = ctx?.GetTonalityProfileForPart?.Invoke(part);
            if (profile != null)
            {
                // Use the profile-aware path
                return BuildProceduralProgressionWithProfile(
                    part,
                    profile,
                    rng,
                    defaultVelocity,
                    verbose
                );
            }

            // Build degree weights (Ionian baseline for major family, Aeolian for minor family)
            var weights = BuildDegreeWeights(part.Tonality, part.RootNote, baseW, rootB, domB, charB);

            // Meter grid info
            var ts = GetTimeSignatureDetails(part.TimeSignature, GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen));
            int beatsPerBar = ts.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);
            int subdivisions = 1; // one step per beat (MVP)
            int stepsPerMeasure = beatsPerBar * subdivisions;
            int totalSteps = stepsPerMeasure * measures;

            // Anchor array: true where a new chord event starts
            var anchors = new bool[totalSteps];
            for (int m = 0; m < measures; m++) anchors[m * stepsPerMeasure] = true;

            // Degree + quality for each bar
            var pickedPerBar = new List<(ScaleDegree deg, ChordQuality q)>(measures);
            for (int bar = 0; bar < measures; bar++)
            {
                ScaleDegree chosenDeg;
                if (bar == measures - 1)
                {
                    // Final bar cadences to I
                    chosenDeg = ScaleDegree.Tonic;
                }
                else
                {
                    // Weighted pick
                    var localWeights = (float[])weights.Clone();

                    // Intro bias to I on bar 0
                    if (bar == 0)
                        localWeights[(int)ScaleDegree.Tonic] += 2f;

                    // Roulette wheel
                    float total = localWeights.Sum();
                    float pick = (float)rng.NextDouble() * total;
                    int idx = 0;
                    for (; idx < 7; idx++)
                    {
                        if (pick <= localWeights[idx]) break;
                        pick -= localWeights[idx];
                    }
                    if (idx >= 7) idx = 6;

                    chosenDeg = (ScaleDegree)idx;
                }

                var q = GetDiatonicTriadQuality(part.Tonality, chosenDeg);
                pickedPerBar.Add((chosenDeg, q));
            }

            // Materialize ChordProgressionData
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.measures = measures;
            prog.subdivisions = subdivisions;
            prog.events = new List<ChordProgressionData.ChordEvent>();

            // walk 'anchors' and 'pickedPerBar' and produces proper startStep/lengthSteps/etc.
            prog.RebuildFromAnchors(anchors, pickedPerBar, defaultVelocity);

            return prog;
        }

        private static ChordProgressionData BuildProceduralProgressionWithProfile(
            SongConfig.PartConfig part,
            TonalityProfileSO profile,
            System.Random rng,
            int defaultVelocity = 96,
            bool verbose = false)
        {
            // 1. Derive base per-degree weights (size 7)
            // Scale degrees (0..6, 0 = I, 1 = II, ..., 6 = VII)
            var weights = new float[7];
            for (int i = 0; i < 7; i++)
            {
                float w = 1f;
                if (profile.baseDegreeWeights != null 
                    && i < profile.baseDegreeWeights.Count 
                    && profile.baseDegreeWeights[i] > 0f)
                    w = profile.baseDegreeWeights[i];

                if (i == 0) // tonic
                    w += profile.tonicBonus;

                if (i == profile.supportDegree)
                    w += profile.supportBonus;

                if (profile.characteristicDegrees != null 
                    && profile.characteristicDegrees.Contains(i))
                    w += profile.characteristicBonus;

                weights[i] = w;
            }

            // Log
            if (verbose)
            {
                var weightLines = new List<string>();
                for (int i = 0; i < 7; i++)
                {
                    var deg = (ScaleDegree)i;
                    var qual = GetDiatonicTriadQuality(part.Tonality, deg);
                    var rn = ToRomanRich(deg, qual);
                    weightLines.Add($"{i}:{rn}= {weights[i]:0.##}");
                }

                Debug.Log($"[ChordProfile] Using profile for part '{part.Name}': " +
                          profile.ToDebugString(includeVamps: true));

                Debug.Log($"<color=orange>[ChordProfile] Base degree weights for {part.Tonality} " +
                    $"over {part.RootNote}: " +
                          string.Join(" | ", weightLines) + "</color>");
            }

            // 2. Decide if we’re going to use a vamp or just free-pick
            //    (choose a vampCandidate by weight, or null if none)
            var chosen = ChooseVamp(profile.vampCandidates, rng); // returns (degrees[], barsToUse) or null

            // Wrap tuple in a mutable struct we can edit in-place.
            VampRuntime vampRuntime;
            bool useVamp = false;
            if (chosen.HasValue)
            {
                vampRuntime = new VampRuntime
                {
                    degreesSequence = chosen.Value.degreesSequence,
                    barsRemaining = chosen.Value.barsRemaining
                };
                useVamp = true;
            }
            else
            {
                vampRuntime = new VampRuntime
                {
                    degreesSequence = null,
                    barsRemaining = 0
                };
            }

            // Log
            if (verbose)
            {
                if (useVamp && vampRuntime.degreesSequence != null)
                {
                    var seq = string.Join(",", vampRuntime.degreesSequence);
                    Debug.Log($"[ChordProfile] Chosen vamp for part '{part.Name}': " +
                              $"degrees=[{seq}] bars={vampRuntime.barsRemaining}");
                }
                else
                {
                    Debug.Log($"[ChordProfile] No vamp chosen for part '{part.Name}', " +
                        $"using free-pick chords.");
                }
            }

            var ts = GetTimeSignatureDetails(
                part.TimeSignature,
                // TODO: BPM per part to avoid timing issues
                GetBPMFromRange(part.TempoRange, TempoRule.MultiplesOfTen)
            );

            // TODO: Encapsulate obtaining these variables as tuple (bpb, m, sd, spm, ts)
            int beatsPerBar = ts.BeatsPerMeasure;
            int measures = Mathf.Max(1, part.Measures);
            int subdivisions = 1;
            int stepsPerMeasure = beatsPerBar * subdivisions;
            int totalSteps = stepsPerMeasure * measures;

            var anchors = new bool[totalSteps];
            for (int m = 0; m < measures; m++) anchors[m * stepsPerMeasure] = true;

            var pickedDegrees = new List<(ScaleDegree deg, ChordQuality q)>(measures);

            int bar = 0;
            while (bar < measures)
            {
                if (verbose)
                {
                    Debug.Log($"[ChordProfile] Entering vamp branch: " +
                        $"barsRemaining={vampRuntime.barsRemaining} " +
                              $"for part '{part.Name}'");
                }

                // --- Vamp branch ---
                if (useVamp && vampRuntime.barsRemaining > 0)
                {
                    // iterate the vamp's degree sequence across bars
                    for (int i = 0; 
                        i < vampRuntime.degreesSequence.Count && bar < measures; 
                        i++, bar++)
                    {
                        int degIdx = vampRuntime.degreesSequence[i];

                        // force cadence on last bar if profile says so
                        if (profile.forceCadenceToTonic && bar == measures - 1)
                            degIdx = 0;

                        var sd = (ScaleDegree)degIdx;
                        var qual = GetDiatonicTriadQuality(part.Tonality, sd);
                        pickedDegrees.Add((sd, qual));

                        if (verbose)
                        {
                            var rn = ToRomanRich(sd, qual);
                            Debug.Log($"[ChordProfile]   Bar {bar + 1}/{measures} " +
                                $"(vamp): degIdx={degIdx} rn={rn}");
                        }
                    }

                    vampRuntime.barsRemaining--;
                    continue;
                }

                // --- Free-pick branch ---
                // Build localWeights from profile weights each bar
                var localWeights = (float[])weights.Clone();

                // EXTRA tonic boost on first bar
                if (bar == 0)
                    localWeights[0] += profile.firstBarTonicBonus;

                // last bar force tonic if requested
                int chosenIdx;
                if (profile.forceCadenceToTonic && bar == measures - 1)
                {
                    chosenIdx = 0;
                }
                else
                {
                    float total = localWeights.Sum();
                    float pickVal = (float)rng.NextDouble() * total;

                    if (verbose)
                    {
                        var lwLines = new List<string>();
                        for (int i = 0; i < 7; i++)
                        {
                            var deg = (ScaleDegree)i;
                            var qual = GetDiatonicTriadQuality(part.Tonality, deg);
                            var rn = ToRomanRich(deg, qual);
                            lwLines.Add($"{i}:{rn} w={localWeights[i]:0.##}");
                        }

                        Debug.Log($"[ChordProfile] Bar {bar + 1}/{measures} free-pick weights: " +
                                  string.Join(" | ", lwLines) +
                                  $"  (roulette pick={pickVal:0.###} / total={total:0.###})");
                    }

                    chosenIdx = 0;
                    for (; chosenIdx < 7; chosenIdx++)
                    {
                        if (pickVal <= localWeights[chosenIdx]) break;
                        pickVal -= localWeights[chosenIdx];
                    }
                    if (chosenIdx >= 7) chosenIdx = 6;
                }

                var sdChosen = (ScaleDegree)chosenIdx;
                var qChosen = GetDiatonicTriadQuality(part.Tonality, sdChosen);
                pickedDegrees.Add((sdChosen, qChosen));

                if (verbose)
                {
                    var rn = ToRomanRich(sdChosen, qChosen);
                    Debug.Log($"[ChordProfile]   Bar {bar + 1}/{measures} " +
                        $"picked degree idx={chosenIdx} rn={rn}");
                }

                bar++;
            }

            // build progression asset in-memory
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.measures = measures;
            prog.subdivisions = subdivisions;
            prog.events = new List<ChordProgressionData.ChordEvent>();
            prog.RebuildFromAnchors(anchors, pickedDegrees, defaultVelocity);

            if (verbose && prog.events != null)
            {
                var seq = string.Join("  ",
                    prog.events.Select(e => ToRomanRich(e.degree, e.quality)));
                Debug.Log($"[ChordProfile] Final profile-driven progression for " +
                    $"part '{part.Name}': {seq}");
            }

            return prog;
        }

        private static (List<int> degreesSequence, int barsRemaining)? ChooseVamp(
            List<TonalityProfileSO.VampDefinition> vamps,
            System.Random rng)
        {
            if (vamps == null || vamps.Count == 0) return null;

            float total = vamps.Sum(v => v.weight);
            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            TonalityProfileSO.VampDefinition chosen = vamps[0];
            foreach (var v in vamps)
            {
                if (pick <= v.weight) { chosen = v; break; }
                pick -= v.weight;
            }

            int bars = Mathf.Clamp(
                rng.Next(chosen.minBars, chosen.maxBars + 1),
                1, 64);

            return (degreesSequence: chosen.degrees, barsRemaining: bars);
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

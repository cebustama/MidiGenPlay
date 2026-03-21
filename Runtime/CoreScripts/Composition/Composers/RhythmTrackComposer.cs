using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// Rhythm/drum track composer. Supports grid + legacy piano-roll paths.
    /// Mirrors MidiGenerator.GenerateRhythmTrackWithPattern / Grid / Legacy behavior including bank/patch/channel.
    public sealed class RhythmTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;

        public RhythmTrackComposer(MidiGenPlayConfig settings) => _settings = settings;

        private const string LogTag = "<color=#f8c>[RhythmTrackComposer]</color>";

        private bool LogEnabled => _settings != null && _settings.logGenerator;

        private static string SafeName(UnityEngine.Object o) => o != null ? o.name : "null";
        private static string SafeTypeName(object o) => o != null ? o.GetType().Name : "null";

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var kit = (MIDIPercussionInstrumentSO)cfg.PercussionInstrument;

            // Keep the raw styleBundle for logging (even if cast fails)
            var styleBundle = cfg.Parameters?.Style;
            var cardCfg = styleBundle as RhythmCardConfigSO;

            // Option A resolution (already present; keep behavior)
            var data = cardCfg?.patternOverride
                ?? cfg.Parameters?.Pattern as DrumPatternData;

            var recipe = cardCfg?.recipeOverride ?? cfg.Parameters?.RhythmRecipe;

            // fully procedural drums when there's no pattern
            if ((data == null) && kit != null)
            {
                if (_settings?.logGenerator == true)
                    Debug.Log("[RhythmTrackComposer] Procedural rhythm (no DrumPatternData).");

                // choose a style for this meter; fall back to generic if none
                RhythmStyleRegistry.RegisterDefaults(); // safe no-op if already called

                IRhythmStyle style = null;
                if (cardCfg != null && !string.IsNullOrEmpty(cardCfg.styleIdOverride))
                {
                    style = RhythmStyleRegistry.ChooseById(
                        part.TimeSignature,
                        cardCfg.styleIdOverride);
                }

                if (style == null)
                {
                    var srng = ctx?.rng;
                    if (srng == null)
                    {
                        Debug.LogWarning($"{LogTag} ctx.rng is null; using deterministic fallback RNG.");
                        srng = new System.Random(_settings != null ? _settings.defaultSeed : 0);
                    }

                    style = RhythmStyleRegistry.Choose(
                        part.TimeSignature,
                        recipe,
                        (min, max) => (max <= min) ? min : srng.Next(min, max));
                }

                // Phase 0 snapshot (before generating the MIDI)
                LogPhase0Snapshot(
                    part: part,
                    cfg: cfg,
                    kit: kit,
                    styleBundle: styleBundle,
                    rhythmStyle: cardCfg,
                    resolvedPattern: data,        // null here by definition
                    resolvedRecipe: recipe,
                    bpm: bpm,
                    channel: channel,
                    chosenPath: "Procedural(no pattern)",
                    chosenStyleId: style != null ? style.Id : null);

                MidiFile pFile = (style != null)
                    ? style.Compose(kit, bpm, part.Measures, channel, recipe)
                    : ComposeProcedural(kit, bpm, part.TimeSignature, part.Measures, channel);

                // Post-process here so all styles remain pure
                StampBankAndPatch(pFile, kit, channel);
                ForceAllChannel(pFile, channel);

                if (_settings?.logGenerator == true)
                {
                    var chunks = pFile.GetTrackChunks().Count();
                    var notes = pFile.GetNotes().Count();
                    var last = pFile.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                    .Select(te => te.Time).DefaultIfEmpty(0).Max();
                    Debug.Log($"[RhythmTrackComposer] (procedural) tracks={chunks} " +
                              $"notes={notes} lastTick={last}");
                }

                return pFile;
            }

            if (data == null || kit == null)
            {
                // Phase 0 snapshot (missing input path)
                LogPhase0Snapshot(
                    part: part,
                    cfg: cfg,
                    kit: kit,
                    styleBundle: styleBundle,
                    rhythmStyle: cardCfg,
                    resolvedPattern: data,        // null or non-null
                    resolvedRecipe: recipe,
                    bpm: bpm,
                    channel: channel,
                    chosenPath: "Missing(pattern or kit)");

                Debug.LogWarning("[RhythmTrackComposer] Missing pattern or percussion instrument.");
                return new MidiFile();
            }

            // Choose path (grid or legacy)
            bool hasGrid =
                data.lanes != null && data.lanes.Count > 0 &&
                data.lanes.Exists(l => l.steps != null && l.steps.Count > 0);

            // Phase 3: normalize GRID-authored patterns to Part meter (Part TS is authoritative)
            if (hasGrid)
            {
                data = NormalizeGridPatternForPartIfNeeded(data, part.TimeSignature);
            }

            // Phase 0 snapshot (pattern path)
            LogPhase0Snapshot(
                part: part,
                cfg: cfg,
                kit: kit,
                styleBundle: styleBundle,
                rhythmStyle: cardCfg,
                resolvedPattern: data,
                resolvedRecipe: recipe,
                bpm: bpm,
                channel: channel,
                chosenPath: hasGrid ? "Pattern(Grid)" : "Pattern(Legacy)");

            var file = hasGrid
                ? ComposeFromGrid(kit, data, bpm, part.TimeSignature, part.Measures, channel)
                : ComposeFromLegacy(kit, data, bpm, part.TimeSignature, part.Measures, channel);

            if (_settings != null && _settings.logGenerator)
            {
                var chunks = file.GetTrackChunks().Count();
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[RhythmTrackComposer] tracks={chunks} " +
                          $"notes={notes} lastTick={lastTick}");
            }

            return file;
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

        /// <summary>
        /// Phase 3: If a GRID-authored DrumPatternData (lanes/steps) was authored in a different
        /// beats-per-measure than the current Part meter, adapt it using normalized bar time.
        /// - Part.TimeSignature is authoritative.
        /// - No asset mutation: returns a runtime clone when adaptation is needed.
        ///
        /// Compile-fix note: lane.steps is now List&lt;StepState&gt;.
        /// Normalization maps active steps by bar-time fraction; per-step velocity is preserved.
        /// </summary>
        private DrumPatternData NormalizeGridPatternForPartIfNeeded(
            DrumPatternData src,
            MusicTheory.MusicTheory.TimeSignature partTs)
        {
            if (src == null) return null;

            int dstBeats;
            try
            {
                dstBeats = TimeSignatureProperties[partTs].BeatsPerMeasure;
            }
            catch
            {
                return src;
            }

            int srcBeats = Mathf.Max(1, src.beatsPerMeasure);
            int subdivisions = Mathf.Max(1, src.subdivisions);

            // Fast path: already compatible in beats-per-measure.
            if (srcBeats == dstBeats)
                return src;

            int measures = Mathf.Max(1, src.Measures);

            int srcStepsPerMeasure = srcBeats * subdivisions;
            int dstStepsPerMeasure = dstBeats * subdivisions;

            int srcTotalSteps = measures * srcStepsPerMeasure;
            int dstTotalSteps = measures * dstStepsPerMeasure;

            // Build a runtime clone with the destination signature
            var clone = ScriptableObject.CreateInstance<DrumPatternData>();
            clone.name = $"{src.name} (Norm {dstBeats})";
            clone.DisplayName = string.IsNullOrEmpty(src.DisplayName) ? src.name : src.DisplayName;
            clone.TimeSignature = partTs;
            clone.Measures = measures;
            clone.beatsPerMeasure = dstBeats;
            clone.subdivisions = subdivisions;

            clone.lanes = new List<DrumPatternData.Lane>();

            if (src.lanes != null)
            {
                foreach (var lane in src.lanes)
                {
                    if (lane == null) continue;

                    var nl = new DrumPatternData.Lane
                    {
                        instrument = lane.instrument,
                        defaultVelocity = Mathf.Clamp(lane.defaultVelocity, 1, 127),
                        steps = new List<DrumPatternData.StepState>(dstTotalSteps)
                    };

                    // pre-fill with StepState.Off so we can index-assign
                    for (int i = 0; i < dstTotalSteps; i++)
                        nl.steps.Add(DrumPatternData.StepState.Off);

                    var srcSteps = lane.steps;
                    if (srcSteps != null && srcSteps.Count > 0)
                    {
                        int max = Mathf.Min(srcSteps.Count, srcTotalSteps);

                        for (int sAbs = 0; sAbs < max; sAbs++)
                        {
                            // Compile-fix: was bool check, now StepState.active
                            if (!srcSteps[sAbs].active) continue;

                            // Map within the measure by normalized bar time.
                            int m = sAbs / srcStepsPerMeasure;
                            int sIn = sAbs - m * srcStepsPerMeasure;

                            double frac = (double)sIn / (double)srcStepsPerMeasure; // [0,1)
                            int dstIn = Mathf.Clamp(
                                Mathf.RoundToInt((float)(frac * dstStepsPerMeasure)),
                                0,
                                dstStepsPerMeasure - 1);

                            int dstAbs = m * dstStepsPerMeasure + dstIn;
                            if ((uint)dstAbs < (uint)dstTotalSteps)
                                // Preserve per-step velocity during normalization
                                nl.steps[dstAbs] = srcSteps[sAbs];
                        }
                    }

                    clone.lanes.Add(nl);
                }
            }

            clone.EnsureSizes();

            if (LogEnabled)
            {
                Debug.LogWarning(
                    $"{LogTag} Normalized DrumPatternData to Part meter (GRID): " +
                    $"'{SafeName(src)}' beats/measure {srcBeats} -> {dstBeats} " +
                    $"(subdiv={subdivisions}, meas={measures}). Runtime clone='{SafeName(clone)}'.");
            }

            return clone;
        }


        // simple, musical, meter-aware grid
        private static MidiFile ComposeProcedural(
            MIDIPercussionInstrumentSO kit,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int measures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(ts);

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Mappings (with safe fallbacks)
            bool hasKick =
                kit.TryGetMappedNote(GeneralMidiPercussion.AcousticBassDrum, out var kick);
            bool hasSnare =
                kit.TryGetMappedNote(GeneralMidiPercussion.AcousticSnare, out var snare);
            bool hasCHH =
                kit.TryGetMappedNote(GeneralMidiPercussion.ClosedHiHat, out var chh);
            bool hasOHH =
                kit.TryGetMappedNote(GeneralMidiPercussion.OpenHiHat, out var ohh);

            // Backbeat rule: 1 = kick, ceil(beats/2)+1 = snare (clamped to beatsPerBar)
            int backbeat = Mathf.Min(beatsPerBar, Mathf.CeilToInt(beatsPerBar / 2f) + 1);

            for (int m = 0; m < Mathf.Max(1, measures); m++)
            {
                for (int b0 = 0; b0 < beatsPerBar; b0++)
                {
                    int beat = b0 + 1; // 1-based beat number
                    double whenBeats = m * beatsPerBar + b0;

                    // Hi-hat logic:
                    // - CHH on every beat EXCEPT the final beat
                    if (beat != beatsPerBar && hasCHH)
                    {
                        pb.MoveToTime(beatSpan.Multiply(whenBeats));
                        pb.Note(chh, beatSpan, (SevenBitNumber)60);
                    }
                    if (beat == beatsPerBar && hasOHH)
                    {
                        pb.MoveToTime(beatSpan.Multiply(whenBeats));
                        pb.Note(ohh, beatSpan, (SevenBitNumber)80);
                    }
                    else if (beat == beatsPerBar && hasCHH)
                    {
                        pb.MoveToTime(beatSpan.Multiply(whenBeats));
                        pb.Note(chh, beatSpan, (SevenBitNumber)60);
                    }

                    // Kick on beat 1
                    if (beat == 1 && hasKick)
                    {
                        pb.MoveToTime(beatSpan.Multiply(whenBeats));
                        pb.Note(kick, beatSpan, (SevenBitNumber)96);
                    }

                    // Snare on backbeat
                    if (beat == backbeat && hasSnare)
                    {
                        pb.MoveToTime(beatSpan.Multiply(whenBeats));
                        pb.Note(snare, beatSpan, (SevenBitNumber)96);
                    }
                }
            }

            var pattern = pb.Build();
            var file = pattern.ToFile(tempoMap);

            StampBankAndPatch(file, kit, channel);
            ForceAllChannel(file, channel);
            return file;
        }

        private static MidiFile ComposeFromGrid(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData data,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int partMeasures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            int stepsPerBeat = Mathf.Max(1, data.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            var beatSpan = GetBeatSpan(ts);

            int patternMeasures = Mathf.Max(1, data.Measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int partTotalSteps = Mathf.Max(1, partMeasures) * stepsPerMeasure;
            int repeats = Mathf.Max(1, Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var stepDur = beatSpan.Multiply(1.0 / stepsPerBeat);
            var pb = new PatternBuilder().MoveToStart();

            // snapshot lanes → (instrument, velocity, step indices[])
            // SnapshotAsIndices returns lane defaultVelocity — runtime behavior unchanged.
            var lanes = data.SnapshotAsIndices();

            for (int r = 0; r < repeats; r++)
            {
                int stepOffset = r * patternTotalSteps;

                foreach (var lane in lanes)
                {
                    if (!kit.TryGetMappedNote(lane.instrument, out var note))
                    {
                        Debug.LogWarning($"[RhythmTrackComposer] No mapped note for {lane.instrument}");
                        continue;
                    }

                    var vel = (SevenBitNumber)Mathf.Clamp(lane.velocity, 1, 127);

                    foreach (var s in lane.stepIndices)
                    {
                        int sAbs = stepOffset + s;
                        double beatsFromStart = (double)sAbs / stepsPerBeat;

                        var when = beatSpan.Multiply(beatsFromStart);
                        pb.MoveToTime(when);
                        pb.Note(note, stepDur, vel);
                    }
                }
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            StampBankAndPatch(file, kit, channel);
            ForceAllChannel(file, channel);
            return file;
        }

        private static MidiFile ComposeFromLegacy(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData patternData,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int measures,
            int channel)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(ts);

            // Expand piano-roll pattern by mapping symbols to concrete notes.
            var processedLines = new Dictionary<string, string>();
            var lines = (patternData.pianoRollPattern ?? "").Split('\n');

            foreach (string line in lines)
            {
                int i1 = line.IndexOf('{');
                int i2 = line.IndexOf('}');
                if (i1 == -1 || i2 == -1 || i2 <= i1) continue;

                string tag = line.Substring(i1, i2 - i1 + 1);
                string symbol = tag.Trim('{', '}');

                var gm = GeneralMidiPercussion.AcousticBassDrum;
                bool foundMap = false;

                foreach (var mapping in patternData.drumMappings)
                    if (mapping.drumSymbol == symbol) { gm = mapping.drumNote; foundMap = true; break; }

                if (!foundMap)
                {
                    Debug.LogWarning($"[RhythmTrackComposer] No map for symbol {symbol}");
                    continue;
                }

                if (!kit.TryGetMappedNote(gm, out var note))
                {
                    Debug.LogWarning($"[RhythmTrackComposer] No MIDI note for {gm}");
                    continue;
                }

                string noteStr = $"{note.NoteName}{note.Octave}";
                string processed = line.Replace(tag, noteStr);
                processedLines[noteStr] = processed;
            }

            string pianoRoll = string.Join("\n", processedLines.Values);

            var pb = new PatternBuilder().MoveToStart();
            int patMeasures = Mathf.Max(1, patternData.Measures);
            int repeats = Mathf.CeilToInt((float)measures / patMeasures);

            for (int r = 0; r < repeats; r++)
            {
                int measureOffset = r * patMeasures * beatsPerBar;

                // Phase 2 fix: beat-unit aware offset
                pb.MoveToTime(beatSpan.Multiply(measureOffset));
                pb.PianoRoll(pianoRoll);
            }

            var pattern = pb.Build();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var file = pattern.ToFile(tempoMap);

            StampBankAndPatch(file, kit, channel);
            ForceAllChannel(file, channel);
            return file;
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(
            MidiFile file, MIDIPercussionInstrumentSO kit, int channel)
        {
            if (!int.TryParse(kit.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[RhythmTrackComposer] " +
                    $"Percussion bank is not numeric: '{kit.BankName}', falling back to 0");
                bank = 0;
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

                // Program Change (small delta to keep ordering stable)
                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)kit.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        #region Debug
        private void LogPhase0Snapshot(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            MIDIPercussionInstrumentSO kit,
            TrackStyleBundleSO styleBundle,
            RhythmCardConfigSO rhythmStyle,
            DrumPatternData resolvedPattern,
            RhythmRecipe resolvedRecipe,
            int bpm,
            int channel,
            string chosenPath,
            string chosenStyleId = null)
        {
            if (!LogEnabled) return;

            int beatsPerMeasure = 0;
            int beatUnit = 0;
            try
            {
                var props = TimeSignatureProperties[part.TimeSignature];
                beatsPerMeasure = props.BeatsPerMeasure;
                beatUnit = props.BeatUnit;
            }
            catch { /* keep 0/0 */ }

            bool hasCardCfg = (rhythmStyle != null);
            bool hasPatternOverride = (rhythmStyle != null && rhythmStyle.patternOverride != null);
            bool hasRecipeOverride = (rhythmStyle != null && rhythmStyle.recipeOverride != null);

            string patternSource =
                hasPatternOverride ? "RhythmCardConfigSO.patternOverride" :
                (cfg?.Parameters?.Pattern != null) ? "TrackParameters.Pattern" :
                "none";

            string recipeSource =
                hasRecipeOverride ? "RhythmCardConfigSO.recipeOverride" :
                (cfg?.Parameters?.RhythmRecipe != null) ? "TrackParameters.RhythmRecipe" :
                "none";

            string phrasingFeel =
                hasCardCfg
                    ? $"fillEveryN={rhythmStyle.fillEveryNMeasures}, lastAsFill={rhythmStyle.lastMeasuresAsFill}, " +
                      $"kickDensity={rhythmStyle.kickDensity:0.##}, snareGhost={rhythmStyle.snareGhostNoteChance:0.##}, hatBias={rhythmStyle.hatSubdivisionBias:0.##}"
                    : "n/a (no RhythmCardConfigSO)";

            string patternDetails =
                resolvedPattern != null
                    ? $"meas={resolvedPattern.Measures}, beats/measure={resolvedPattern.beatsPerMeasure}, subdiv={resolvedPattern.subdivisions}, lanes={(resolvedPattern.lanes != null ? resolvedPattern.lanes.Count : 0)}"
                    : "none";

            string styleIdOverride = hasCardCfg ? (rhythmStyle.styleIdOverride ?? "") : "";
            string recipeStyleId = resolvedRecipe != null ? (resolvedRecipe.RhythmStyleId ?? "") : "";

            var sb = new StringBuilder(512);
            sb.AppendLine($"{LogTag} Phase0 snapshot");
            sb.AppendLine($"  Path: {chosenPath}" + (string.IsNullOrEmpty(chosenStyleId) ? "" : $" | chosenStyleId='{chosenStyleId}'"));
            sb.AppendLine($"  Part: '{part?.Name ?? "null"}' meas={part?.Measures ?? 0} bpm={bpm} ts={beatsPerMeasure}/{beatUnit} ch={channel}");
            sb.AppendLine($"  Track: role={cfg?.Role.ToString() ?? "null"} mus='{cfg?.MusicianId ?? "null"}' kit='{SafeName(kit)}'");
            sb.AppendLine($"  StyleBundle: type={SafeTypeName(styleBundle)} (as RhythmCardConfigSO? {(rhythmStyle != null ? "YES" : "NO")})");
            if (hasCardCfg)
            {
                sb.AppendLine($"    patternOverride != null ? {hasPatternOverride}");
                sb.AppendLine($"    recipeOverride  != null ? {hasRecipeOverride}");
                sb.AppendLine($"    styleIdOverride='{styleIdOverride}'");
            }
            sb.AppendLine($"  Pattern: resolved='{SafeName(resolvedPattern)}' source={patternSource} | {patternDetails}");
            sb.AppendLine($"  Recipe:  resolvedSource={recipeSource} RhythmStyleId='{recipeStyleId}'");
            sb.AppendLine($"  Phrasing/Feel: {phrasingFeel}");

            Debug.Log(sb.ToString());
        }
        #endregion
    }
}

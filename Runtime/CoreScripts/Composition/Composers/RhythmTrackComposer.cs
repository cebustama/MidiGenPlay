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

            // Card-level pattern resolution (palette-aware).
            // PickPatternOverride is now TS-aware: checks patternOverride first, then patternPalette
            // (weighted, clone-on-pick), seeded from the composer RNG so the pick is
            // reproducible under the determinism invariant. Falls back to an explicitly
            // authored TrackParameters.Pattern when the card resolves nothing.
            var pickRng = ctx?.rng;
            if (pickRng == null && cardCfg?.patternPalette != null)
            {
                // Only matters when a palette would actually be picked from.
                Debug.LogWarning($"{LogTag} ctx.rng is null; using deterministic fallback RNG for palette pick.");
                pickRng = new System.Random(_settings != null ? _settings.defaultSeed : 0);
            }

            // MGP-ALWTTT-DBG-3 (Ask C, D-DBG4=A) — precedence STEP 0: a
            // per-render override installed on the context wins over the card
            // pick and TrackParameters.Pattern. Clone-on-apply (normalization
            // below mutates `data`); type mismatch = warn + ignore (fall
            // through to the normal chain). When absent, the flow below is
            // draw-for-draw identical to pre-batch (BC gate).
            DrumPatternData overrideData = null;
            var renderOverride = ctx?.patternOverride;
            if (renderOverride != null)
            {
                overrideData = renderOverride as DrumPatternData;
                if (overrideData == null)
                {
                    Debug.LogWarning(
                        $"{LogTag} patternOverride type mismatch for role Rhythm: " +
                        $"expected DrumPatternData, got {renderOverride.GetType().Name} " +
                        $"('{renderOverride.name}'). Ignoring override.");
                }
                else
                {
                    overrideData = ScriptableObject.Instantiate(overrideData); // clone-on-apply
                }
            }

            // MGP-ALWTTT-DBG-1 (Ask A): resolution with source tracking.
            DrumPatternData data;
            var resolvedSource = ResolvedSource.None;
            string resolvedName = null;
            string resolvedPalette = null;

            if (overrideData != null)
            {
                data = overrideData;
                resolvedSource = ResolvedSource.RenderOverride;
                resolvedName = renderOverride.name; // pre-clone caller asset (D-DBG3)
            }
            else
            {
                var pickInfo = default(PatternPickInfo);
                data = cardCfg != null
                    ? cardCfg.PickPatternOverride(
                        pickRng, part.TimeSignature, _settings, out pickInfo, LogEnabled)
                    : null;

                if (data != null)
                {
                    resolvedSource = pickInfo.fromPalette
                        ? ResolvedSource.CardPalette : ResolvedSource.CardOverride;
                    resolvedName = pickInfo.sourceAssetName;
                    resolvedPalette = pickInfo.paletteName;
                }
                else if (cfg.Parameters?.Pattern is DrumPatternData trackPattern)
                {
                    data = trackPattern;
                    resolvedSource = ResolvedSource.TrackParameters;
                    resolvedName = trackPattern.name;
                }
            }

            // Ask A report helper — at most one invoke per Compose, on every
            // return path below. No-op when no sink is installed.
            void ReportChoice(ResolvedSource src, string styleId = null)
            {
                ctx?.ReportResolved?.Invoke(new ResolvedTrackChoice
                {
                    source = src,
                    sourceAssetName = resolvedName,
                    paletteName = resolvedPalette,
                    proceduralStyleId = styleId,
                });
            }

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

                // Ask A: procedural resolution — identity is the style id.
                ReportChoice(ResolvedSource.Procedural, style != null ? style.Id : null);

                MidiFile pFile = (style != null)
                    ? style.Compose(kit, bpm, part.Measures, channel, recipe)
                    : ComposeProcedural(kit, bpm, part.TimeSignature, part.Measures, channel, LogEnabled);

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
                // Ask A: nothing rendered — report None so the caller sees the
                // track resolved nothing this render.
                resolvedName = null;
                resolvedPalette = null;
                ReportChoice(ResolvedSource.None);
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

            // Ask A: pattern path — source/name/palette captured at resolution.
            ReportChoice(resolvedSource);

            // MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B): publish the resolved
            // pattern's audible onsets for downstream consumers (bass
            // SlapPocket). GRID PATH ONLY in v1 — procedural and legacy paths
            // publish nothing, which is the documented degrade trigger on the
            // consumer side. Runs on the already TS-normalized `data`, so the
            // published beats are Part-meter truth. Skipped entirely when no
            // sink is installed (direct composer calls / tests without ctx).
            if (hasGrid && ctx?.SetRhythmOnsetsForPartMusician != null)
            {
                var onsets = ExtractResolvedOnsets(
                    kit, data, part.TimeSignature, part.Measures);
                ctx.SetRhythmOnsetsForPartMusician(part, cfg.MusicianId, onsets);
            }

            var file = hasGrid
                ? ComposeFromGrid(kit, data, bpm, part.TimeSignature, part.Measures, channel, LogEnabled)
                : ComposeFromLegacy(kit, data, bpm, part.TimeSignature, part.Measures, channel, LogEnabled);

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


        // -------------------------------------------------------------------
        // PERC-FALLBACK-1 — single percussion-resolution seam for the three
        // compose paths (procedural / grid / legacy). Wraps
        // PercussionNoteResolver and applies the D-PF3 log discipline:
        // Exact → silence; Substituted / GmStandard → informational log gated
        // by logSubstitutions (LogEnabled, D-PF5=B); None → hard warning
        // naming the missing percussion and the substitutes tried.
        // allowGmStandard is wired false for now (D-PF6=B); flip here (and
        // decide its config home) if a GM-compliant-soundfont escape hatch is
        // ever needed.
        // -------------------------------------------------------------------
        private static bool TryResolveForCompose(
            MIDIPercussionInstrumentSO kit,
            GeneralMidiPercussion percussion,
            bool logSubstitutions,
            out Melanchall.DryWetMidi.MusicTheory.Note note)
        {
            bool ok = PercussionNoteResolver.TryResolve(
                kit, percussion, allowGmStandard: false,
                out note, out var resolution, out var resolvedAs);

            switch (resolution)
            {
                case PercussionNoteResolver.Resolution.Substituted:
                    if (logSubstitutions)
                        Debug.Log(
                            $"{LogTag} Percussion substituted: {percussion} -> {resolvedAs} " +
                            $"(kit '{SafeName(kit)}' has no exact mapping).");
                    break;

                case PercussionNoteResolver.Resolution.GmStandard:
                    if (logSubstitutions)
                        Debug.Log(
                            $"{LogTag} Percussion {percussion}: emitting GM-standard note " +
                            $"(kit '{SafeName(kit)}' maps nothing in its family).");
                    break;

                case PercussionNoteResolver.Resolution.None:
                    Debug.LogWarning(
                        $"{LogTag} No playable percussion for {percussion}: kit '{SafeName(kit)}' " +
                        $"maps neither it nor its family substitutes " +
                        $"[{string.Join(", ", PercussionFallbackTable.GetSubstitutes(percussion))}]. " +
                        $"Lane muted. Add a kit mapping for {percussion} or one of its substitutes.");
                    break;

                    // Resolution.Exact: silence by design.
            }

            return ok;
        }

        // simple, musical, meter-aware grid
        private static MidiFile ComposeProcedural(
            MIDIPercussionInstrumentSO kit,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int measures,
            int channel,
            bool logSubstitutions)
        {
            var tsInfo = GetTimeSignatureDetails(ts, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(ts);

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Mappings (family-fallback aware; PERC-FALLBACK-1, D-PF7=A)
            bool hasKick = TryResolveForCompose(
                kit, GeneralMidiPercussion.AcousticBassDrum, logSubstitutions, out var kick);
            bool hasSnare = TryResolveForCompose(
                kit, GeneralMidiPercussion.AcousticSnare, logSubstitutions, out var snare);
            bool hasCHH = TryResolveForCompose(
                kit, GeneralMidiPercussion.ClosedHiHat, logSubstitutions, out var chh);
            bool hasOHH = TryResolveForCompose(
                kit, GeneralMidiPercussion.OpenHiHat, logSubstitutions, out var ohh);

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

        /// <summary>
        /// MGP-ALWTTT-BASS-POCKET-1 (D-PKT-SRC=B): the publication seam. Pure
        /// mirror of <see cref="ComposeFromGrid"/>'s step→beat math over the
        /// TS-NORMALIZED pattern, with three deliberate deltas, all
        /// contract-level:
        /// - TRUNCATION: onsets at or beyond the part end (`sAbs >=
        ///   partTotalSteps`) are dropped. ComposeFromGrid's ceil-repeat may
        ///   emit past the part boundary (silenced later by AllSoundOff); the
        ///   published channel is the part's musical surface and must not lead
        ///   a consumer to place hits beyond it.
        /// - AUDIBILITY FILTER: only lanes that RESOLVE on the kit are
        ///   published (same PERC-FALLBACK-1 resolution as composition,
        ///   silent — the compose loop already emits the substitution logs;
        ///   double-logging here would be noise). The published instrument is
        ///   the SEMANTIC authored lane, pre-substitution, so consumers
        ///   classify kick/snare independently of what concrete note sounds.
        /// - ORDERING: sorted by (beat, instrument) so the payload is
        ///   deterministic and consumer-friendly regardless of lane order.
        /// Velocity is the resolved, clamped 1..127 step velocity — identical
        /// to what ComposeFromGrid emits for the same step.
        /// Internal test seam (InternalsVisibleTo MidiGenPlay.Tests.Editor).
        /// </summary>
        public static List<MidiGenerator.RhythmOnset> ExtractResolvedOnsets(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData data,
            MusicTheory.MusicTheory.TimeSignature ts,
            int partMeasures)
        {
            var result = new List<MidiGenerator.RhythmOnset>();
            if (kit == null || data == null) return result;

            int beatsPerBar;
            try { beatsPerBar = TimeSignatureProperties[ts].BeatsPerMeasure; }
            catch { return result; }

            int stepsPerBeat = Mathf.Max(1, data.subdivisions);
            int stepsPerMeasure = beatsPerBar * stepsPerBeat;

            int patternMeasures = Mathf.Max(1, data.Measures);
            int patternTotalSteps = patternMeasures * stepsPerMeasure;
            int partTotalSteps = Mathf.Max(1, partMeasures) * stepsPerMeasure;
            int repeats = Mathf.Max(1,
                Mathf.CeilToInt((float)partTotalSteps / patternTotalSteps));

            var lanes = data.SnapshotAsStepVelocities();

            for (int r = 0; r < repeats; r++)
            {
                int stepOffset = r * patternTotalSteps;

                foreach (var lane in lanes)
                {
                    // Audibility filter — resolution outcome only, no logs.
                    if (!TryResolveForCompose(kit, lane.instrument,
                            logSubstitutions: false, out _))
                        continue;

                    foreach (var (stepIndex, velocity) in lane.steps)
                    {
                        int sAbs = stepOffset + stepIndex;
                        if (sAbs >= partTotalSteps) continue; // truncation

                        result.Add(new MidiGenerator.RhythmOnset
                        {
                            instrument = lane.instrument,
                            beat = (double)sAbs / stepsPerBeat,
                            velocity = Mathf.Clamp(velocity, 1, 127),
                        });
                    }
                }
            }

            result.Sort((a, b) =>
            {
                int c = a.beat.CompareTo(b.beat);
                return c != 0 ? c : ((int)a.instrument).CompareTo((int)b.instrument);
            });
            return result;
        }

        private static MidiFile ComposeFromGrid(
            MIDIPercussionInstrumentSO kit,
            DrumPatternData data,
            int bpm,
            MusicTheory.MusicTheory.TimeSignature ts,
            int partMeasures,
            int channel,
            bool logSubstitutions)
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

            // snapshot lanes → (instrument, (stepIndex, resolvedVelocity)[])
            // SnapshotAsStepVelocities resolves per-step velocity per the StepState
            // sentinel rule (velocity 0 → lane defaultVelocity). Per-step velocity
            // now reaches generated MIDI; see SSoT_Composer_Rhythm_Track.md §3-B.
            var lanes = data.SnapshotAsStepVelocities();

            for (int r = 0; r < repeats; r++)
            {
                int stepOffset = r * patternTotalSteps;

                foreach (var lane in lanes)
                {
                    // PERC-FALLBACK-1: family-fallback resolution replaces the
                    // old exact-enum match ("No mapped note for X" mute).
                    if (!TryResolveForCompose(kit, lane.instrument, logSubstitutions, out var note))
                        continue;

                    foreach (var (stepIndex, velocity) in lane.steps)
                    {
                        int sAbs = stepOffset + stepIndex;
                        double beatsFromStart = (double)sAbs / stepsPerBeat;

                        var when = beatSpan.Multiply(beatsFromStart);
                        pb.MoveToTime(when);
                        pb.Note(note, stepDur, (SevenBitNumber)Mathf.Clamp(velocity, 1, 127));
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
            int channel,
            bool logSubstitutions)
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

                // PERC-FALLBACK-1: family-fallback resolution (was exact match).
                if (!TryResolveForCompose(kit, gm, logSubstitutions, out var note))
                    continue;

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
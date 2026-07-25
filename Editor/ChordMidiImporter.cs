#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function importer: a standard MIDI file → the canonical
    /// <see cref="ChordProgressionData.ChordEvent"/> list (step-based timing).
    /// Phase M3 deliverable per <c>Roadmap_MIDI_Import.md</c> (D-MIDI3=A:
    /// restricted chord detection); consumed by the Chord Progression Editor's
    /// "MIDI File Import" panel.
    /// </summary>
    /// <remarks>
    /// <para>Same mold as <see cref="DrumMidiImporter"/> / <see cref="MelodyMidiImporter"/>:
    /// no Unity-API calls in the parse, no asset mutation, no editor-window state —
    /// the window owns applying the returned <see cref="Result"/> to its Grid-mode
    /// working state (Apply/Save As remains the only asset write path).
    /// EditMode-testable against in-memory DryWetMidi files. Only
    /// ticks-per-quarter-note files are supported (SMPTE is a hard fail).
    /// Grid-beat conversion is beat-unit aware:
    /// <c>gridBeats = quarterNotes × beatUnit / 4</c>. The caller's Timing
    /// controls are the meter authority (M3-D4=A); the file's own time-signature
    /// meta events are ignored.</para>
    ///
    /// <para><b>Segmentation (M3-D1=A).</b> Note starts and ends are quantized to
    /// the step grid FIRST; a segment is a maximal run of steps whose SOUNDING
    /// pitch-class set is identical. The grid therefore absorbs strums, arpeggiated
    /// attacks, and humanized onsets without a tolerance knob; residual partial
    /// segments fall into the below-threshold net (M3-D3) and surface as warnings,
    /// never as spurious chords. Empty steps produce no event (silence is not
    /// lossy; note that <see cref="ChordProgressionData.FindChordEventAt"/> sustains
    /// the preceding chord across gaps at runtime by design).</para>
    ///
    /// <para><b>What counts as a chord (M3-D3=B).</b> Channel filter (merge across
    /// channels warns, mirroring M2-D3=A) + a fixed minimum of
    /// <see cref="MinChordPitchClasses"/> distinct simultaneous pitch classes.
    /// Segments below the threshold are skipped with a warning and leave a gap.</para>
    ///
    /// <para><b>Identification (M3-D5, deterministic cascade).</b>
    /// (1) Try the segment's BASS pitch class as root against every
    /// <c>GetIntervalsForQuality</c> template compared as pitch-class sets
    /// (mod 12, deduplicated — so ninths fold their 14 onto 2); per root the
    /// v1 alphabet has no pc-set collisions, so a bass-rooted exact match is
    /// unique. (2) Otherwise try every member pitch class as root (covers
    /// inversions); a single exact match wins silently. (3) Multiple exact
    /// matches (e.g. {C,E,G,A} over an E bass = C6 vs Am7) tie-break:
    /// diatonic-in-key first, then fewer template voices, then lowest root pitch
    /// class, then enum order — and emit an informative
    /// <see cref="ImportWarningKind.ChordAmbiguityResolved"/> warning.
    /// (4) No exact match anywhere → REDUCTION with an explicit
    /// <see cref="ImportWarningKind.ChordReduced"/> warning (D-MIDI3=A:
    /// explicit-warn, never silent, mirroring the Roman path's degrade-guard
    /// philosophy): the largest template fully contained in the set wins;
    /// ties prefer diatonic, then the bass root, then lowest root pitch class,
    /// then enum order; dropped pitch classes are listed. (5) Nothing contained
    /// at all → <see cref="ImportWarningKind.ChordUnmatched"/>, segment skipped.</para>
    ///
    /// <para><b>Degree + accidental (M3-D2=A / D2b).</b> The chosen root's pitch
    /// class relative to the user key resolves to (<see cref="ScaleDegree"/>,
    /// accidental −1/0/+1) — `degreeAccidental` CAN express every chromatic root
    /// in all seven v1 modes, so M2's chromatic snap is not copied. Double
    /// spellings prefer the FLAT reading (the degree above, lowered: ♭II ♭III ♭VI
    /// ♭VII…), analogous in spirit to M2-D6's tie-downward. A nearest-degree snap
    /// (+ <see cref="ImportWarningKind.RootSnapped"/>) is kept only as a
    /// forward-compat guard for gapped scales; it is unreachable in the v1 modes.
    /// <c>isDiatonic</c> = accidental 0 AND
    /// <see cref="ChordQualityResolver.IsChordDiatonic"/> (triad-family test),
    /// matching the editor's Roman path.</para>
    ///
    /// <para><b>Coalescing.</b> Consecutive events with identical
    /// (degree, accidental, quality) merge into one harmonic region, absorbing
    /// re-articulations (comping strikes) and any empty steps between them —
    /// consistent with runtime's sustain-across-gaps semantics; chord-strike
    /// rhythm belongs to the runtime articulators, not to
    /// <see cref="ChordProgressionData"/>. Velocity is the rounded MEAN of the
    /// contributing notes' velocities (M3-D6). IMPORT-QOL-1 amendment (bounded,
    /// to M3-D5): <see cref="Options.preserveReStrikes"/> restricts the merge to
    /// CONTIGUOUS regions, keeping gapped re-strikes as separate events; default
    /// false = the M3 behavior.</para>
    ///
    /// <para><b>Documented limitation (not warned per chord).</b> Inversions and
    /// voicings are discarded: <see cref="ChordProgressionData.ChordEvent"/> has
    /// no inversion field and voicing is runtime's job (voice leading /
    /// articulators) — same precedent as M2's absolute-octave loss.</para>
    ///
    /// <para><b>No silent fallback.</b> Every lossy step emits an
    /// <see cref="ImportWarning"/> with the M1 shape <c>[Kind] loc: detail</c>,
    /// detailed up to <see cref="MaxDetailedWarnings"/> then aggregated.</para>
    /// </remarks>
    public static class ChordMidiImporter
    {
        /// <summary>Snap error (in step units) above which an off-grid warning is emitted.</summary>
        public const double SnapWarnThresholdSteps = 0.25;

        /// <summary>Cap for content-derived measure counts (pathological-file guard).</summary>
        public const int MaxDerivedMeasures = 64;

        /// <summary>Max per-item detail lines per warning kind before aggregating.</summary>
        public const int MaxDetailedWarnings = 8;

        /// <summary>IMPORT-QOL-1 — max residual (in GRID BEATS) for a candidate
        /// subdivision to count as "explaining" the file in
        /// <see cref="SuggestSubdivisions"/>. 1/32 of a grid beat (≈15.6 ms at
        /// 120 BPM on a quarter-note beat): tight enough that even sub=8 (whose
        /// worst-case residual is 1/16 beat) is falsifiable, loose enough for
        /// lightly-humanized quantized files. Judged on the MAX residual, not
        /// the mean — one unexplained onset invalidates the grid. Public and
        /// documented so it can be tuned after corpus experience; the selection
        /// logic (smallest passing candidate) is independent of the value.</summary>
        public const double SuggestMaxErrorBeats = 0.03125;

        /// <summary>Candidate subdivisions probed by
        /// <see cref="SuggestSubdivisions"/>, ascending — parsimony order, so
        /// the SMALLEST grid that explains the file wins and humanization is
        /// not over-fit by a needlessly fine grid. Matches the editor slider's
        /// 1–8 clamp.</summary>
        public static readonly int[] SuggestCandidates = { 1, 2, 3, 4, 6, 8 };

        /// <summary>M3-D3=B: minimum distinct simultaneous pitch classes for a
        /// segment to be considered a chord. Fixed, not a user knob — the v1
        /// alphabet starts at triads, so a dyad cannot match any template.</summary>
        public const int MinChordPitchClasses = 3;

        // -------------------------------------------------------------------
        // Options / warnings / result
        // -------------------------------------------------------------------

        /// <summary>Caller-supplied import parameters (from the editor's UI).</summary>
        public struct Options
        {
            /// <summary>Key root (D-MIDI1=A: user-specified, DryWetMidi
            /// <see cref="NoteName"/> — the same type the runtime seam uses).</summary>
            public NoteName rootNote;

            /// <summary>Mode; selects the package interval table for root → degree.</summary>
            public Tonality tonality;

            /// <summary>Target meter; drives beats-per-measure and the grid beat
            /// unit (M3-D4=A: the window's Timing controls are the authority).</summary>
            public TimeSignature timeSignature;

            /// <summary>Steps per grid beat (the chord editor's Grid mode clamps 1–8).</summary>
            public int subdivisions;

            /// <summary>Declared measure count. &lt;= 0 = derive from content —
            /// covering the LAST NOTE END (capped at <see cref="MaxDerivedMeasures"/>);
            /// &gt; 0 = fixed: notes starting beyond are dropped, notes ending
            /// beyond are clipped, with warnings.</summary>
            public int measures;

            /// <summary>0-based MIDI channel filter; -1 = accept all channels
            /// (a merge across channels warns, M2-D3=A precedent).</summary>
            public int channel;

            /// <summary>IMPORT-QOL-1 (bounded amendment to M3-D5): when true,
            /// consecutive identical harmonic identities coalesce ONLY when
            /// contiguous (no empty steps between them) — a rest between two
            /// strikes of the same chord is preserved as an event boundary, so
            /// a comping file keeps its harmonic rhythm (the runtime reproduces
            /// rests faithfully). When false — the default, and therefore the
            /// semantics of <c>default(Options)</c> and of every pre-existing
            /// M3 test — identical identities also merge ACROSS gaps.</summary>
            public bool preserveReStrikes;
        }

        public enum ImportMode { Failed, Full }

        public enum ImportWarningKind
        {
            UnsupportedTimeDivision,
            NoNotesFound,
            ChannelsMerged,
            OffGridSnap,
            DurationSnapped,
            NotesBeyondRange,
            DurationClipped,
            MeasuresCapped,
            SegmentBelowThreshold,
            ChordAmbiguityResolved,
            ChordReduced,
            ChordUnmatched,
            RootSnapped,
            NoChordsFound,
        }

        /// <summary>Mirrors the M1/M2 importers' ToString shape
        /// (<c>[Kind] loc: detail</c>) so the window's warning list renders all
        /// three importers uniformly. Chords have no lanes; loc is always "file".</summary>
        public readonly struct ImportWarning
        {
            public readonly ImportWarningKind kind;
            public readonly string detail;

            public ImportWarning(ImportWarningKind kind, string detail)
            {
                this.kind = kind;
                this.detail = detail;
            }

            public override string ToString() => $"[{kind}] file: {detail}";
        }

        /// <summary>
        /// Outcome. On <see cref="ImportMode.Full"/>, <see cref="events"/> is the
        /// coalesced, step-based chord list ordered by startStep, and
        /// <see cref="romanSummary"/> is a display-only Roman rendering for
        /// traceability (echoed in the panel; NOT guaranteed to round-trip through
        /// the Roman parser). On <see cref="ImportMode.Failed"/>, inspect
        /// <see cref="warnings"/>.
        /// </summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly int subdivisions;

            public readonly IReadOnlyList<ChordProgressionData.ChordEvent> events;

            /// <summary>Display-only Roman rendering of the imported progression
            /// (accidental prefix, casing by triad family, durations in measures).</summary>
            public readonly string romanSummary;

            public readonly IReadOnlyList<ImportWarning> warnings;

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                int subdivisions,
                IReadOnlyList<ChordProgressionData.ChordEvent> events,
                string romanSummary,
                IReadOnlyList<ImportWarning> warnings)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.events = events ?? Array.Empty<ChordProgressionData.ChordEvent>();
                this.romanSummary = romanSummary ?? string.Empty;
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
            }

            internal static Result Fail(
                TimeSignature ts, int measures, int subdivisions, List<ImportWarning> warnings)
                => new Result(ImportMode.Failed, ts, measures, subdivisions,
                    null, null, warnings);
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        public static Result Import(MidiFile file, Options options)
        {
            var warnings = new List<ImportWarning>();

            TimeSignature ts = options.timeSignature;
            int subdivisions = Math.Max(1, options.subdivisions);

            // ---- 1. Time division: only PPQ files are supported. ----
            if (file == null || !(file.TimeDivision is TicksPerQuarterNoteTimeDivision tpqDiv))
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.UnsupportedTimeDivision,
                    "File is null or uses SMPTE time division; only ticks-per-quarter-note files are supported."));
                return Result.Fail(ts, 0, subdivisions, warnings);
            }
            double tpqn = tpqDiv.TicksPerQuarterNote;

            // ---- 2. Collect notes through the channel filter. ----
            var allNotes = file.GetNotes();
            var filtered = new List<Note>();
            foreach (var n in allNotes)
            {
                if (options.channel >= 0 && (int)n.Channel != options.channel) continue;
                filtered.Add(n);
            }

            if (filtered.Count == 0)
            {
                string hint = options.channel >= 0
                    ? $"no notes on MIDI channel {options.channel + 1}; the file has " +
                      $"{allNotes.Count()} note(s) total — try channel 0 (= all)."
                    : "the file contains no notes.";
                warnings.Add(new ImportWarning(ImportWarningKind.NoNotesFound, hint));
                return Result.Fail(ts, 0, subdivisions, warnings);
            }

            // Merging channels is legal but never silent (M2-D3=A precedent).
            var channelCounts = filtered
                .GroupBy(n => (int)n.Channel)
                .OrderBy(g => g.Key)
                .ToList();
            if (channelCounts.Count > 1)
            {
                string perChannel = string.Join(", ",
                    channelCounts.Select(g => $"ch{g.Key + 1}: {g.Count()}"));
                warnings.Add(new ImportWarning(
                    ImportWarningKind.ChannelsMerged,
                    $"Notes from {channelCounts.Count} channels were merged ({perChannel}). " +
                    "If this mixes parts (e.g. a melody over the chords), re-import with a channel filter."));
            }

            // ---- 3. Grid math (beat-unit aware) + key tables. ----
            var tsProps = TimeSignatureProperties[ts];
            int beatsPerMeasure = tsProps.BeatsPerMeasure;
            int beatUnit = tsProps.BeatUnit;
            int stepsPerMeasure = beatsPerMeasure * subdivisions;
            double gridBeatsPerQuarter = beatUnit / 4.0;

            // Cumulative semitone offsets per degree, from the package's own
            // interval tables (GetScaleFromTonality is the single authority seam).
            var scale = GetScaleFromTonality(options.tonality, options.rootNote);
            var intervals = scale.Intervals.ToList();
            int degreeCount = intervals.Count;                    // 7 for all v1 modes
            var cumPc = new int[degreeCount];
            int cumAcc = 0;
            for (int i = 1; i < degreeCount; i++)
            {
                cumAcc += intervals[i - 1].HalfSteps;
                cumPc[i] = ((cumAcc % 12) + 12) % 12;
            }
            int keyRootPc = (int)options.rootNote;                // DryWetMidi NoteName: C=0..B=11

            // One resolver: the single authority for the diatonic (triad-family) test.
            var resolver = new ChordQualityResolver(options.tonality, AutoChordQualityMode.None);

            // ---- 4. Quantize every note to the step grid (M3-D1=A: quantize FIRST,
            //          segment after — the grid absorbs strums/arpeggios/humanization). ----
            var quantized = new List<(int startStep, int endStep, int pitch, int velocity)>(filtered.Count);

            int offGridDetail = 0, offGridTotal = 0;
            int durSnapDetail = 0, durSnapTotal = 0;

            foreach (var n in filtered)
            {
                double rawStartSteps = (n.Time / tpqn) * gridBeatsPerQuarter * subdivisions;
                int startStep = (int)Math.Round(rawStartSteps, MidpointRounding.AwayFromZero);
                double startErr = Math.Abs(rawStartSteps - startStep);
                if (startStep < 0) startStep = 0;

                if (startErr > SnapWarnThresholdSteps)
                {
                    offGridTotal++;
                    if (offGridDetail < MaxDetailedWarnings)
                    {
                        offGridDetail++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.OffGridSnap,
                            $"Note {n.NoteNumber} at tick {n.Time} is {startErr:0.##} steps off the grid; " +
                            $"snapped to step {startStep}."));
                    }
                }

                double rawEndSteps = ((n.Time + n.Length) / tpqn) * gridBeatsPerQuarter * subdivisions;
                int endStep = (int)Math.Round(rawEndSteps, MidpointRounding.AwayFromZero);
                double endErr = Math.Abs(rawEndSteps - endStep);
                bool floored = endStep <= startStep;
                if (floored) endStep = startStep + 1;             // one-step floor (M2-D5 precedent)

                if (endErr > SnapWarnThresholdSteps || floored)
                {
                    durSnapTotal++;
                    if (durSnapDetail < MaxDetailedWarnings)
                    {
                        durSnapDetail++;
                        string why = floored
                            ? "raised to the one-step floor"
                            : $"snapped ({endErr:0.##} steps of error at the note end)";
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.DurationSnapped,
                            $"Note {n.NoteNumber} at tick {n.Time}: duration {why}."));
                    }
                }

                int velocity = Math.Min(127, Math.Max(1, (int)n.Velocity));
                quantized.Add((startStep, endStep, n.NoteNumber, velocity));
            }

            if (offGridTotal > offGridDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.OffGridSnap,
                    $"{offGridTotal - offGridDetail} further off-grid note(s) snapped (details omitted)."));
            if (durSnapTotal > durSnapDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.DurationSnapped,
                    $"{durSnapTotal - durSnapDetail} further duration(s) adjusted (details omitted)."));

            // ---- 5. Resolve measure count (content-derived covers the last note END). ----
            int measures;
            int maxEndStep = quantized.Max(q => q.endStep);
            if (options.measures > 0)
            {
                measures = options.measures;
            }
            else
            {
                measures = Math.Max(1, (int)Math.Ceiling(maxEndStep / (double)stepsPerMeasure));
                if (measures > MaxDerivedMeasures)
                {
                    warnings.Add(new ImportWarning(
                        ImportWarningKind.MeasuresCapped,
                        $"Content implies {measures} measures; capped at {MaxDerivedMeasures}. " +
                        "Notes beyond the cap are dropped or clipped."));
                    measures = MaxDerivedMeasures;
                }
            }
            int totalSteps = measures * stepsPerMeasure;

            // ---- 6. Range handling: drop late starts, clip overhanging ends. ----
            int droppedBeyond = quantized.RemoveAll(q => q.startStep >= totalSteps);
            if (droppedBeyond > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NotesBeyondRange,
                    $"{droppedBeyond} note(s) start beyond the {measures}-measure range; dropped."));

            int clipped = 0;
            for (int i = 0; i < quantized.Count; i++)
            {
                var q = quantized[i];
                if (q.endStep > totalSteps)
                {
                    clipped++;
                    quantized[i] = (q.startStep, totalSteps, q.pitch, q.velocity);
                }
            }
            if (clipped > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.DurationClipped,
                    $"{clipped} note(s) extended past the {measures}-measure range; clipped to the pattern end."));

            if (quantized.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NoNotesFound,
                    "All notes fell outside the measure range; nothing to import."));
                return Result.Fail(ts, measures, subdivisions, warnings);
            }

            // ---- 7. Per-step sounding pitch-class mask, then segment on set change. ----
            var maskPerStep = new int[totalSteps];
            foreach (var q in quantized)
            {
                int bit = 1 << (((q.pitch % 12) + 12) % 12);
                for (int s = q.startStep; s < q.endStep; s++)
                    maskPerStep[s] |= bit;
            }

            var segments = new List<(int start, int end, int mask)>();
            int runStart = -1, runMask = 0;
            for (int s = 0; s <= totalSteps; s++)
            {
                int m = (s < totalSteps) ? maskPerStep[s] : 0;
                if (m != runMask)
                {
                    if (runMask != 0)
                        segments.Add((runStart, s, runMask));
                    runStart = s;
                    runMask = m;
                }
            }

            // ---- 8. Identify each segment (M3-D5 cascade) → provisional events. ----
            var provisional = new List<(int start, int end, ScaleDegree degree, int accidental,
                                        ChordQuality quality, bool isDiatonic, List<int> velocitySamples)>();

            int belowDetail = 0, belowTotal = 0;
            int ambigDetail = 0, ambigTotal = 0;
            int reducedDetail = 0, reducedTotal = 0;
            int unmatchedDetail = 0, unmatchedTotal = 0;
            int rootSnapTotal = 0;

            foreach (var seg in segments)
            {
                int pcCount = PopCount(seg.mask);

                // Contributing notes: overlap the segment span.
                int bassPitch = int.MaxValue;
                var velocitySamples = new List<int>();
                foreach (var q in quantized)
                {
                    if (q.startStep < seg.end && q.endStep > seg.start)
                    {
                        if (q.pitch < bassPitch) bassPitch = q.pitch;
                        velocitySamples.Add(q.velocity);
                    }
                }
                int bassPc = ((bassPitch % 12) + 12) % 12;

                // M3-D3=B: below the pitch-class threshold → gap + warning.
                if (pcCount < MinChordPitchClasses)
                {
                    belowTotal++;
                    if (belowDetail < MaxDetailedWarnings)
                    {
                        belowDetail++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.SegmentBelowThreshold,
                            $"{Loc(seg.start, stepsPerMeasure, subdivisions)}: only {pcCount} pitch class(es) " +
                            $"sounding ({PcSetToString(seg.mask)}); below the {MinChordPitchClasses}-note chord " +
                            "threshold — no chord emitted (likely melody or a dyad)."));
                    }
                    continue;
                }

                // M3-D5 cascade — shared with DescribeChordTimeline so the
                // diagnostic can never drift from the import decision.
                var outcome = MatchSegment(seg.mask, bassPc, keyRootPc, cumPc, resolver,
                    out int rootPc, out ChordQuality quality, out var exactCandidates);

                if (outcome == MatchOutcome.ExactAmbiguous)
                {
                    ambigTotal++;
                    if (ambigDetail < MaxDetailedWarnings)
                    {
                        ambigDetail++;
                        string alternatives = string.Join(" / ",
                            exactCandidates.Select(c => $"{PcName(c.rootPc)} {c.q}"));
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.ChordAmbiguityResolved,
                            $"{Loc(seg.start, stepsPerMeasure, subdivisions)}: pitch-class set " +
                            $"{PcSetToString(seg.mask)} matches {alternatives}; kept " +
                            $"{PcName(rootPc)} {quality} " +
                            "(diatonic-first, then fewest voices, then lowest root)."));
                    }
                }
                else if (outcome == MatchOutcome.Reduced)
                {
                    int chosenTm = TemplateMask(quality);
                    int extra = Rotate(seg.mask, rootPc) & ~chosenTm;
                    reducedTotal++;
                    if (reducedDetail < MaxDetailedWarnings)
                    {
                        reducedDetail++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.ChordReduced,
                            $"{Loc(seg.start, stepsPerMeasure, subdivisions)}: pitch-class set " +
                            $"{PcSetToString(seg.mask)} is outside the v1 chord alphabet; reduced to " +
                            $"{PcName(rootPc)} {quality}, dropping {PopCount(extra)} pitch class(es) " +
                            $"({PcSetToString(RotateBack(extra, rootPc))})."));
                    }
                }
                else if (outcome == MatchOutcome.Unmatched)
                {
                    // (5) Nothing in the alphabet is even contained → skip.
                    unmatchedTotal++;
                    if (unmatchedDetail < MaxDetailedWarnings)
                    {
                        unmatchedDetail++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.ChordUnmatched,
                            $"{Loc(seg.start, stepsPerMeasure, subdivisions)}: pitch-class set " +
                            $"{PcSetToString(seg.mask)} contains no v1 chord — no chord emitted."));
                    }
                    continue;
                }

                // Degree + accidental (M3-D2=A / D2b: flat preferred).
                int relToKey = ((rootPc - keyRootPc) % 12 + 12) % 12;
                if (!TryResolveDegreeAccidental(relToKey, cumPc, out int degIndex, out int accidental))
                {
                    // Unreachable in the seven v1 modes (every chromatic pc is ±1
                    // from a scale tone); guard kept for gapped-scale forward-compat.
                    rootSnapTotal++;
                    NearestDegree(relToKey, cumPc, out degIndex);
                    accidental = 0;
                }

                bool isDiatonic = accidental == 0 &&
                                  resolver.IsChordDiatonic((ScaleDegree)degIndex, quality);

                provisional.Add((seg.start, seg.end, (ScaleDegree)degIndex, accidental,
                                 quality, isDiatonic, velocitySamples));
            }

            if (belowTotal > belowDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.SegmentBelowThreshold,
                    $"{belowTotal - belowDetail} further below-threshold segment(s) skipped (details omitted)."));
            if (ambigTotal > ambigDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.ChordAmbiguityResolved,
                    $"{ambigTotal - ambigDetail} further ambiguous segment(s) resolved (details omitted)."));
            if (reducedTotal > reducedDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.ChordReduced,
                    $"{reducedTotal - reducedDetail} further segment(s) reduced (details omitted)."));
            if (unmatchedTotal > unmatchedDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.ChordUnmatched,
                    $"{unmatchedTotal - unmatchedDetail} further unmatched segment(s) skipped (details omitted)."));
            if (rootSnapTotal > 0)
                warnings.Add(new ImportWarning(ImportWarningKind.RootSnapped,
                    $"{rootSnapTotal} chord root(s) could not be expressed as degree ± accidental; " +
                    "snapped to the nearest degree (gapped-scale guard)."));

            if (provisional.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NoChordsFound,
                    "No segment produced a chord (all were below the pitch-class threshold or unmatched)."));
                return Result.Fail(ts, measures, subdivisions, warnings);
            }

            // ---- 9. Coalesce consecutive identical harmonic identities.
            //          Default: re-articulations AND gaps between same-chord
            //          strikes merge (M3-D6). IMPORT-QOL-1: with
            //          options.preserveReStrikes, only CONTIGUOUS identical
            //          identities merge — a gap keeps the strikes separate. ----
            var coalesced = new List<(int start, int end, ScaleDegree degree, int accidental,
                                      ChordQuality quality, bool isDiatonic, List<int> velocitySamples)>();
            foreach (var p in provisional)
            {
                if (coalesced.Count > 0)
                {
                    var last = coalesced[coalesced.Count - 1];
                    if (last.degree == p.degree &&
                        last.accidental == p.accidental &&
                        last.quality == p.quality &&
                        (!options.preserveReStrikes || last.end == p.start))
                    {
                        last.velocitySamples.AddRange(p.velocitySamples);
                        coalesced[coalesced.Count - 1] =
                            (last.start, p.end, last.degree, last.accidental,
                             last.quality, last.isDiatonic, last.velocitySamples);
                        continue;
                    }
                }
                coalesced.Add(p);
            }

            // ---- 10. Emit ChordEvents + the display-only Roman summary. ----
            var events = new List<ChordProgressionData.ChordEvent>(coalesced.Count);
            var romanParts = new List<string>(coalesced.Count);
            foreach (var c in coalesced)
            {
                int velocity = (int)Math.Round(c.velocitySamples.Average(),
                                               MidpointRounding.AwayFromZero);
                velocity = Math.Min(127, Math.Max(1, velocity));

                events.Add(new ChordProgressionData.ChordEvent
                {
                    startStep = c.start,
                    lengthSteps = c.end - c.start,
                    degree = c.degree,
                    quality = c.quality,
                    velocity = velocity,
                    isDiatonic = c.isDiatonic,
                    degreeAccidental = c.accidental,
                });

                romanParts.Add(RomanLabel(c.degree, c.accidental, c.quality,
                    (c.end - c.start) / (double)stepsPerMeasure));
            }

            string romanSummary = string.Join(" – ", romanParts);

            return new Result(ImportMode.Full, ts, measures, subdivisions,
                events, romanSummary, warnings);
        }

        // -------------------------------------------------------------------
        // Subdivision suggestion (IMPORT-QOL-1, item 1)
        // -------------------------------------------------------------------

        /// <summary>Residual of one candidate subdivision over the whole file.</summary>
        public readonly struct SubdivisionCandidate
        {
            /// <summary>Steps per grid beat probed.</summary>
            public readonly int subdivisions;

            /// <summary>Worst residual over ALL note onsets and ends, in grid
            /// beats: max over events of |t − round(t·s)/s|, t in beats.</summary>
            public readonly double maxErrorBeats;

            /// <summary><c>maxErrorBeats &lt;= SuggestMaxErrorBeats</c>.</summary>
            public readonly bool withinThreshold;

            public SubdivisionCandidate(int subdivisions, double maxErrorBeats)
            {
                this.subdivisions = subdivisions;
                this.maxErrorBeats = maxErrorBeats;
                withinThreshold = maxErrorBeats <= SuggestMaxErrorBeats;
            }
        }

        /// <summary>Outcome of <see cref="SuggestSubdivisions"/>.</summary>
        public readonly struct SubdivisionSuggestion
        {
            /// <summary>False when the file is unreadable (null / SMPTE) or has
            /// no notes after the channel filter; nothing else is valid then.</summary>
            public readonly bool hasNotes;

            /// <summary>The SMALLEST candidate within threshold (parsimony
            /// first); if none passes, the argmin residual (ties → smallest
            /// candidate).</summary>
            public readonly int suggested;

            /// <summary>Whether <see cref="suggested"/> passed the threshold.
            /// When false the caller should report the argmin + residual and
            /// leave the user's grid untouched — never silently apply.</summary>
            public readonly bool suggestedWithinThreshold;

            /// <summary>Full residual table, in <see cref="SuggestCandidates"/>
            /// order, so the caller can report every candidate honestly.</summary>
            public readonly IReadOnlyList<SubdivisionCandidate> candidates;

            public SubdivisionSuggestion(
                bool hasNotes, int suggested, bool suggestedWithinThreshold,
                IReadOnlyList<SubdivisionCandidate> candidates)
            {
                this.hasNotes = hasNotes;
                this.suggested = suggested;
                this.suggestedWithinThreshold = suggestedWithinThreshold;
                this.candidates = candidates ?? Array.Empty<SubdivisionCandidate>();
            }
        }

        /// <summary>
        /// IMPORT-QOL-1 item 1 — probe <see cref="SuggestCandidates"/> and
        /// measure how well each explains the file's note ONSETS and ENDS,
        /// using Import's exact time math (same tpqn → grid-beat conversion,
        /// same channel filter) so residuals are comparable with what Import
        /// will actually do. Pure and read-only; never called by Import — the
        /// user's grid remains authoritative (the editor's "Suggest" button
        /// reports the table and, on that explicit press, may set the slider).
        /// <c>options.subdivisions</c> and <c>options.measures</c> are ignored.
        /// </summary>
        public static SubdivisionSuggestion SuggestSubdivisions(MidiFile file, Options options)
        {
            if (file == null || !(file.TimeDivision is TicksPerQuarterNoteTimeDivision tpqDiv))
                return new SubdivisionSuggestion(false, 0, false,
                    Array.Empty<SubdivisionCandidate>());
            double tpqn = tpqDiv.TicksPerQuarterNote;
            double gridBeatsPerQuarter =
                TimeSignatureProperties[options.timeSignature].BeatUnit / 4.0;

            var timesBeats = new List<double>();
            foreach (var n in file.GetNotes())
            {
                if (options.channel >= 0 && (int)n.Channel != options.channel) continue;
                timesBeats.Add((n.Time / tpqn) * gridBeatsPerQuarter);
                timesBeats.Add(((n.Time + n.Length) / tpqn) * gridBeatsPerQuarter);
            }
            if (timesBeats.Count == 0)
                return new SubdivisionSuggestion(false, 0, false,
                    Array.Empty<SubdivisionCandidate>());

            var candidates = new List<SubdivisionCandidate>(SuggestCandidates.Length);
            int firstPassing = -1;
            int argminIndex = 0;
            double argminErr = double.MaxValue;
            for (int i = 0; i < SuggestCandidates.Length; i++)
            {
                int s = SuggestCandidates[i];
                double maxErr = 0.0;
                foreach (double t in timesBeats)
                {
                    double steps = t * s;
                    double err = Math.Abs(
                        steps - Math.Round(steps, MidpointRounding.AwayFromZero)) / s;
                    if (err > maxErr) maxErr = err;
                }
                var c = new SubdivisionCandidate(s, maxErr);
                candidates.Add(c);
                if (firstPassing < 0 && c.withinThreshold) firstPassing = s;
                if (maxErr < argminErr) { argminErr = maxErr; argminIndex = i; }
            }

            bool passed = firstPassing >= 0;
            int suggested = passed ? firstPassing : SuggestCandidates[argminIndex];
            return new SubdivisionSuggestion(true, suggested, passed, candidates);
        }

        // -------------------------------------------------------------------
        // Pitch-class-set matching (M3-D5)
        // -------------------------------------------------------------------

        /// <summary>All v1 qualities in enum order (the final, stable tie-break).</summary>
        private static readonly ChordQuality[] QualityOrder =
            (ChordQuality[])Enum.GetValues(typeof(ChordQuality));

        private static readonly Dictionary<ChordQuality, int> TemplateMasks = BuildTemplateMasks();

        private static Dictionary<ChordQuality, int> BuildTemplateMasks()
        {
            // Templates come from the package's own interval authority; intervals
            // ≥ 12 (the ninths' 14) fold onto their pitch class, so matching is a
            // pure mod-12 set comparison. Per root, the v1 alphabet has no pc-set
            // collisions (guarded by tests).
            var d = new Dictionary<ChordQuality, int>();
            foreach (ChordQuality q in Enum.GetValues(typeof(ChordQuality)))
            {
                int m = 0;
                foreach (int iv in GetIntervalsForQuality(q))
                    m |= 1 << (((iv % 12) + 12) % 12);
                d[q] = m;
            }
            return d;
        }

        private static int TemplateMask(ChordQuality q) => TemplateMasks[q];

        /// <summary>Rotate a 12-bit pc mask so that pitch class <paramref name="root"/> maps to bit 0.</summary>
        private static int Rotate(int mask, int root)
            => ((mask >> root) | (mask << (12 - root))) & 0xFFF;

        /// <summary>Inverse of <see cref="Rotate"/> (root-relative mask back to absolute pcs).</summary>
        private static int RotateBack(int mask, int root)
            => ((mask << root) | (mask >> (12 - root))) & 0xFFF;

        private static bool TryExactMatch(int segMask, int rootPc, out ChordQuality quality)
        {
            int rel = Rotate(segMask, rootPc);
            foreach (ChordQuality q in QualityOrder)
            {
                if (TemplateMasks[q] == rel)
                {
                    quality = q;
                    return true;
                }
            }
            quality = default;
            return false;
        }

        private enum MatchOutcome { Exact, ExactAmbiguous, Reduced, Unmatched }

        /// <summary>
        /// The M3-D5 identification cascade, shared verbatim by
        /// <see cref="Import"/> and <see cref="DescribeChordTimeline"/> so the
        /// diagnostic can never drift from the import decision.
        /// On <see cref="MatchOutcome.ExactAmbiguous"/>, <paramref name="exactCandidates"/>
        /// holds every exact candidate sorted winner-first; otherwise it is null.
        /// On <see cref="MatchOutcome.Unmatched"/>, rootPc/quality are meaningless.
        /// </summary>
        private static MatchOutcome MatchSegment(
            int segMask, int bassPc, int keyRootPc, int[] cumPc, ChordQualityResolver resolver,
            out int rootPc, out ChordQuality quality,
            out List<(int rootPc, ChordQuality q)> exactCandidates)
        {
            exactCandidates = null;

            // (1) Exact match with the bass as root.
            if (TryExactMatch(segMask, bassPc, out quality))
            {
                rootPc = bassPc;
                return MatchOutcome.Exact;
            }

            // (2)/(3) Exact match over all member roots (covers inversions).
            var exact = new List<(int rootPc, ChordQuality q)>();
            for (int r = 0; r < 12; r++)
            {
                if ((segMask & (1 << r)) == 0) continue;
                if (TryExactMatch(segMask, r, out var q2))
                    exact.Add((r, q2));
            }

            if (exact.Count >= 1)
            {
                if (exact.Count > 1)
                {
                    exact.Sort((a, b) => CompareExactCandidates(
                        a, b, keyRootPc, cumPc, resolver));
                    exactCandidates = exact;
                    rootPc = exact[0].rootPc;
                    quality = exact[0].q;
                    return MatchOutcome.ExactAmbiguous;
                }
                rootPc = exact[0].rootPc;
                quality = exact[0].q;
                return MatchOutcome.Exact;
            }

            // (4) Reduction: largest template fully contained in the set.
            var reduced = new List<(int rootPc, ChordQuality q, int size)>();
            for (int r = 0; r < 12; r++)
            {
                if ((segMask & (1 << r)) == 0) continue;
                int rel = Rotate(segMask, r);
                foreach (ChordQuality q3 in QualityOrder)
                {
                    int tm = TemplateMask(q3);
                    if ((tm & rel) == tm)
                        reduced.Add((r, q3, PopCount(tm)));
                }
            }

            if (reduced.Count == 0)
            {
                rootPc = bassPc;
                quality = default;
                return MatchOutcome.Unmatched;      // (5)
            }

            reduced.Sort((a, b) => CompareReductionCandidates(
                a, b, bassPc, keyRootPc, cumPc, resolver));
            rootPc = reduced[0].rootPc;
            quality = reduced[0].q;
            return MatchOutcome.Reduced;
        }

        /// <summary>Exact-multi tie-break (M3-D5 step 3): diatonic-in-key first,
        /// then fewer template voices, then lowest root pc, then enum order.</summary>
        private static int CompareExactCandidates(
            (int rootPc, ChordQuality q) a, (int rootPc, ChordQuality q) b,
            int keyRootPc, int[] cumPc, ChordQualityResolver resolver)
        {
            int c = DiatonicRank(a.rootPc, a.q, keyRootPc, cumPc, resolver)
                .CompareTo(DiatonicRank(b.rootPc, b.q, keyRootPc, cumPc, resolver));
            if (c != 0) return c;
            c = PopCount(TemplateMasks[a.q]).CompareTo(PopCount(TemplateMasks[b.q]));
            if (c != 0) return c;
            c = a.rootPc.CompareTo(b.rootPc);
            if (c != 0) return c;
            return ((int)a.q).CompareTo((int)b.q);
        }

        /// <summary>Reduction tie-break (M3-D5 step 4): largest template first,
        /// then diatonic, then the bass root, then lowest root pc, then enum order.</summary>
        private static int CompareReductionCandidates(
            (int rootPc, ChordQuality q, int size) a, (int rootPc, ChordQuality q, int size) b,
            int bassPc, int keyRootPc, int[] cumPc, ChordQualityResolver resolver)
        {
            int c = b.size.CompareTo(a.size);                       // larger template first
            if (c != 0) return c;
            c = DiatonicRank(a.rootPc, a.q, keyRootPc, cumPc, resolver)
                .CompareTo(DiatonicRank(b.rootPc, b.q, keyRootPc, cumPc, resolver));
            if (c != 0) return c;
            c = (a.rootPc == bassPc ? 0 : 1).CompareTo(b.rootPc == bassPc ? 0 : 1);
            if (c != 0) return c;
            c = a.rootPc.CompareTo(b.rootPc);
            if (c != 0) return c;
            return ((int)a.q).CompareTo((int)b.q);
        }

        /// <summary>0 when (root, quality) reads as diatonic in the user key, else 1.</summary>
        private static int DiatonicRank(
            int rootPc, ChordQuality q, int keyRootPc, int[] cumPc, ChordQualityResolver resolver)
        {
            int rel = ((rootPc - keyRootPc) % 12 + 12) % 12;
            if (!TryResolveDegreeAccidental(rel, cumPc, out int deg, out int acc))
                return 1;
            return (acc == 0 && resolver.IsChordDiatonic((ScaleDegree)deg, q)) ? 0 : 1;
        }

        // -------------------------------------------------------------------
        // Degree + accidental (M3-D2=A / D2b)
        // -------------------------------------------------------------------

        /// <summary>Resolve a key-relative pitch class to (degree index, accidental).
        /// Natural first; else FLAT preferred (the degree above, lowered — D2b);
        /// else sharp. Cum pcs are distinct in 7-tone modes, so each branch has at
        /// most one hit and the result is deterministic. Returns false only for
        /// scales where no ±1 spelling exists (impossible in the v1 modes).</summary>
        private static bool TryResolveDegreeAccidental(
            int relPc, int[] cumPc, out int degreeIndex, out int accidental)
        {
            for (int d = 0; d < cumPc.Length; d++)
                if (cumPc[d] == relPc) { degreeIndex = d; accidental = 0; return true; }
            for (int d = 0; d < cumPc.Length; d++)
                if (cumPc[d] == (relPc + 1) % 12) { degreeIndex = d; accidental = -1; return true; }
            for (int d = 0; d < cumPc.Length; d++)
                if (cumPc[d] == ((relPc - 1) % 12 + 12) % 12) { degreeIndex = d; accidental = +1; return true; }
            degreeIndex = 0; accidental = 0;
            return false;
        }

        private static void NearestDegree(int relPc, int[] cumPc, out int degreeIndex)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int d = 0; d < cumPc.Length; d++)
            {
                int down = ((relPc - cumPc[d]) % 12 + 12) % 12;
                int up = ((cumPc[d] - relPc) % 12 + 12) % 12;
                int dist = Math.Min(down, up);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            degreeIndex = best;
        }

        // -------------------------------------------------------------------
        // Diagnostic timeline (display-only; smoke/QA aid — NOT import contract)
        // -------------------------------------------------------------------

        /// <summary>
        /// Display-only diagnostic: one paste-ready text block listing, per
        /// segment AND per silence, the quantized location, duration in
        /// measures, sounding pitch-class set, bass, the actual pitches WITH
        /// OCTAVES (so inversions/voicings are visible), and the importer's
        /// verdict — produced with the same quantization formulas, the same
        /// segmentation, and the shared <see cref="MatchSegment"/> cascade that
        /// <see cref="Import"/> uses. Run it with identical Options on the
        /// SOURCE .mid and on a RENDERED .mid to diff harmony, boundaries and
        /// voicings line by line. Emits no warnings and writes nothing.
        /// </summary>
        public static string DescribeChordTimeline(MidiFile file, Options options)
        {
            if (file == null || !(file.TimeDivision is TicksPerQuarterNoteTimeDivision tpqDiv))
                return "[ChordTimeline] Cannot analyze: file is null or uses SMPTE time division.";
            double tpqn = tpqDiv.TicksPerQuarterNote;

            int subdivisions = Math.Max(1, options.subdivisions);
            var tsProps = TimeSignatureProperties[options.timeSignature];
            int stepsPerMeasure = tsProps.BeatsPerMeasure * subdivisions;
            double gridBeatsPerQuarter = tsProps.BeatUnit / 4.0;

            var filtered = new List<Note>();
            foreach (var n in file.GetNotes())
            {
                if (options.channel >= 0 && (int)n.Channel != options.channel) continue;
                filtered.Add(n);
            }
            if (filtered.Count == 0)
                return "[ChordTimeline] No notes after the channel filter.";

            // Quantize with Import's exact formulas (round away-from-zero, clamp,
            // one-step floor).
            var quantized = new List<(int s, int e, int pitch, int vel)>(filtered.Count);
            foreach (var n in filtered)
            {
                int s = (int)Math.Round((n.Time / tpqn) * gridBeatsPerQuarter * subdivisions,
                                        MidpointRounding.AwayFromZero);
                if (s < 0) s = 0;
                int e = (int)Math.Round(((n.Time + n.Length) / tpqn) * gridBeatsPerQuarter * subdivisions,
                                        MidpointRounding.AwayFromZero);
                if (e <= s) e = s + 1;
                quantized.Add((s, e, n.NoteNumber, Math.Min(127, Math.Max(1, (int)n.Velocity))));
            }

            int measures = options.measures > 0
                ? options.measures
                : Math.Min(MaxDerivedMeasures,
                    Math.Max(1, (int)Math.Ceiling(
                        quantized.Max(q => q.e) / (double)stepsPerMeasure)));
            int totalSteps = measures * stepsPerMeasure;
            quantized.RemoveAll(q => q.s >= totalSteps);
            for (int i = 0; i < quantized.Count; i++)
                if (quantized[i].e > totalSteps)
                    quantized[i] = (quantized[i].s, totalSteps, quantized[i].pitch, quantized[i].vel);
            if (quantized.Count == 0)
                return "[ChordTimeline] All notes fell outside the measure range.";

            var maskPerStep = new int[totalSteps];
            foreach (var q in quantized)
            {
                int bit = 1 << (((q.pitch % 12) + 12) % 12);
                for (int s = q.s; s < q.e; s++)
                    maskPerStep[s] |= bit;
            }

            // Key tables + resolver, same seams as Import.
            var scale = GetScaleFromTonality(options.tonality, options.rootNote);
            var intervals = scale.Intervals.ToList();
            var cumPc = new int[intervals.Count];
            int cumAcc = 0;
            for (int i = 1; i < intervals.Count; i++)
            {
                cumAcc += intervals[i - 1].HalfSteps;
                cumPc[i] = ((cumAcc % 12) + 12) % 12;
            }
            int keyRootPc = (int)options.rootNote;
            var resolver = new ChordQualityResolver(options.tonality, AutoChordQualityMode.None);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"[ChordTimeline] key={options.rootNote} {options.tonality} | " +
                $"grid={tsProps.BeatsPerMeasure}/{tsProps.BeatUnit} x sub {subdivisions} " +
                $"= {stepsPerMeasure} steps/measure | measures={measures}" +
                $"{(options.measures > 0 ? " (explicit)" : " (derived)")} | " +
                $"notes={filtered.Count} | loc format m<measure>.<beat>.<step-in-beat>");

            int runStart = 0;
            int runMask = maskPerStep[0];
            for (int s = 1; s <= totalSteps; s++)
            {
                int m = (s < totalSteps) ? maskPerStep[s] : ~runMask; // sentinel forces final flush
                if (m == runMask) continue;
                AppendTimelineRun(sb, runStart, s, runMask, quantized,
                    keyRootPc, cumPc, resolver, stepsPerMeasure, subdivisions);
                runStart = s;
                runMask = m;
            }
            return sb.ToString();
        }

        private static void AppendTimelineRun(
            System.Text.StringBuilder sb, int start, int end, int mask,
            List<(int s, int e, int pitch, int vel)> quantized,
            int keyRootPc, int[] cumPc, ChordQualityResolver resolver,
            int stepsPerMeasure, int subdivisions)
        {
            double lenMeasures = (end - start) / (double)stepsPerMeasure;
            string loc = StepLoc(start, stepsPerMeasure, subdivisions);
            string len = lenMeasures.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture) + "m";

            if (mask == 0)
            {
                sb.AppendLine($"{loc,-10} {len,-7} rest");
                return;
            }

            var pitches = new SortedSet<int>();
            foreach (var q in quantized)
                if (q.s < end && q.e > start) pitches.Add(q.pitch);
            int bass = pitches.Min;
            string noteList = string.Join(" ", pitches.Select(PitchName));

            string verdict;
            int pcCount = PopCount(mask);
            if (pcCount < MinChordPitchClasses)
            {
                verdict = $"below-threshold ({pcCount} pitch class(es); no chord)";
            }
            else
            {
                int bassPc = ((bass % 12) + 12) % 12;
                var outcome = MatchSegment(mask, bassPc, keyRootPc, cumPc, resolver,
                    out int rootPc, out ChordQuality quality, out var candidates);
                if (outcome == MatchOutcome.Unmatched)
                {
                    verdict = "unmatched (no v1 chord contained)";
                }
                else
                {
                    int rel = ((rootPc - keyRootPc) % 12 + 12) % 12;
                    TryResolveDegreeAccidental(rel, cumPc, out int deg, out int accidental);
                    bool dia = accidental == 0 &&
                               resolver.IsChordDiatonic((ScaleDegree)deg, quality);
                    verdict = $"{RomanLabel((ScaleDegree)deg, accidental, quality, 1.0)} " +
                              $"({PcName(rootPc)} {quality})" +
                              (dia ? "" : " borrowed") +
                              (outcome == MatchOutcome.ExactAmbiguous
                                  ? " AMBIG[" + string.Join(" / ",
                                        candidates.Select(c => $"{PcName(c.rootPc)} {c.q}")) + "]"
                                  : "") +
                              (outcome == MatchOutcome.Reduced ? " REDUCED" : "");
                }
            }

            sb.AppendLine(
                $"{loc,-10} {len,-7} pcs={PcSetToString(mask),-18} " +
                $"bass={PitchName(bass),-4} notes=[{noteList}]  -> {verdict}");
        }

        /// <summary>1-based "m2.3.1" = measure 2, beat 3, first step of the beat.</summary>
        private static string StepLoc(int step, int stepsPerMeasure, int subdivisions)
        {
            int measure = step / stepsPerMeasure + 1;
            int rem = step % stepsPerMeasure;
            int beat = rem / Math.Max(1, subdivisions) + 1;
            int sub = rem % Math.Max(1, subdivisions) + 1;
            return $"m{measure}.{beat}.{sub}";
        }

        /// <summary>MIDI pitch → "C4"-style name (middle C = 60 = C4).</summary>
        private static string PitchName(int pitch)
            => PcNames[((pitch % 12) + 12) % 12] + (pitch / 12 - 1);

        // -------------------------------------------------------------------
        // Formatting helpers (warnings + Roman summary; display-only)
        // -------------------------------------------------------------------

        private static int PopCount(int m)
        {
            int c = 0;
            while (m != 0) { c += m & 1; m >>= 1; }
            return c;
        }

        private static readonly string[] PcNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static string PcName(int pc) => PcNames[((pc % 12) + 12) % 12];

        private static string PcSetToString(int mask)
        {
            var parts = new List<string>(4);
            for (int i = 0; i < 12; i++)
                if ((mask & (1 << i)) != 0) parts.Add(PcNames[i]);
            return "{" + string.Join(",", parts) + "}";
        }

        private static string Loc(int step, int stepsPerMeasure, int subdivisions)
        {
            int measure = step / stepsPerMeasure + 1;
            int beat = (step % stepsPerMeasure) / Math.Max(1, subdivisions) + 1;
            return $"Measure {measure}, beat {beat}";
        }

        private static readonly string[] RomanNumerals =
            { "I", "II", "III", "IV", "V", "VI", "VII" };

        /// <summary>Display-only Roman label: accidental prefix, casing by triad
        /// family (lowercase for minor-ish), compact quality suffix, duration in
        /// measures when != 1. Not guaranteed to round-trip through the parser.</summary>
        private static string RomanLabel(
            ScaleDegree degree, int accidental, ChordQuality quality, double durationMeasures)
        {
            string prefix = accidental < 0 ? "♭" : accidental > 0 ? "♯" : "";
            string numeral = RomanNumerals[Math.Min((int)degree, RomanNumerals.Length - 1)];

            bool minorish = quality is ChordQuality.Minor or ChordQuality.Minor7
                or ChordQuality.Minor6 or ChordQuality.Minor9
                or ChordQuality.Diminished or ChordQuality.Diminished7
                or ChordQuality.HalfDiminished7;
            if (minorish) numeral = numeral.ToLowerInvariant();

            string suffix = quality switch
            {
                ChordQuality.Major => "",
                ChordQuality.Minor => "",
                ChordQuality.Diminished => "°",
                ChordQuality.Augmented => "+",
                ChordQuality.Major7 => "maj7",
                ChordQuality.Minor7 => "7",
                ChordQuality.Dominant7 => "7",
                ChordQuality.HalfDiminished7 => "ø7",
                ChordQuality.Diminished7 => "°7",
                ChordQuality.Sus2 => "sus2",
                ChordQuality.Sus4 => "sus4",
                ChordQuality.Major6 => "6",
                ChordQuality.Minor6 => "6",
                ChordQuality.Dominant7sus4 => "7sus4",
                ChordQuality.Dominant9 => "9",
                ChordQuality.Major9 => "maj9",
                ChordQuality.Minor9 => "9",
                _ => quality.ToString(),
            };

            string label = prefix + numeral + suffix;
            if (Math.Abs(durationMeasures - 1.0) > 0.0001)
                label += $" ({durationMeasures:g3})";
            return label;
        }
    }
}
#endif
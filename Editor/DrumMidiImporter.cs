#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function importer: a standard MIDI file → the canonical
    /// <see cref="DrumPatternData"/> grid shape (lanes of <see cref="DrumPatternData.StepState"/>).
    /// Phase M1 deliverable per <c>Roadmap_MIDI_Import.md</c>; consumed by the
    /// Drum Pattern Editor's "MIDI File Import" panel.
    /// </summary>
    /// <remarks>
    /// <para>Same mold as <see cref="DrumPatternEditorImporter"/>: no Unity-API
    /// calls, no asset mutation, no editor-window state — the window owns applying
    /// the returned <see cref="Result"/> to the working copy (Apply/Save As remains
    /// the only asset write path). EditMode-testable against in-memory
    /// DryWetMidi files.</para>
    ///
    /// <para><b>Grid semantics.</b> The caller supplies the target
    /// <see cref="TimeSignature"/> and subdivisions (the editor's Timing controls).
    /// Grid-beat conversion is beat-unit aware, matching the runtime's
    /// <c>GetBeatSpan</c> semantics: in X/8 meters one grid beat is an eighth note,
    /// so <c>gridBeats = quarterNotes × beatUnit / 4</c>.</para>
    ///
    /// <para><b>Instrument mapping.</b> Note number → <see cref="GeneralMidiPercussion"/>
    /// via a reverse map built from DryWetMidi's own GM authority
    /// (<c>AsSevenBitNumber</c>), never a hardcoded offset. This is the single seam
    /// to touch if the installed DryWetMidi version renames that extension.</para>
    ///
    /// <para><b>No silent fallback.</b> Every lossy step — off-grid snap beyond
    /// <see cref="SnapWarnThresholdSteps"/>, same-lane/same-step collision, dropped
    /// out-of-range note, unmapped note number — emits an
    /// <see cref="ImportWarning"/>. Note durations are intentionally ignored:
    /// the drum grid is trigger-based.</para>
    /// </remarks>
    public static class DrumMidiImporter
    {
        /// <summary>GM drum channel as a 0-based MIDI channel index (MIDI "channel 10").</summary>
        public const int GmDrumChannel = 9;

        /// <summary>Snap error (in step units) above which an off-grid warning is emitted.</summary>
        public const double SnapWarnThresholdSteps = 0.25;

        /// <summary>Cap for content-derived measure counts (pathological-file guard).</summary>
        public const int MaxDerivedMeasures = 64;

        /// <summary>Max per-note detail lines per warning kind before aggregating.</summary>
        public const int MaxDetailedWarnings = 8;

        // -------------------------------------------------------------------
        // Note number → GeneralMidiPercussion (built from DryWetMidi's GM tables)
        // -------------------------------------------------------------------

        private static readonly Dictionary<int, GeneralMidiPercussion> NoteToPercussion =
            BuildNoteToPercussion();

        private static Dictionary<int, GeneralMidiPercussion> BuildNoteToPercussion()
        {
            var map = new Dictionary<int, GeneralMidiPercussion>();
            foreach (GeneralMidiPercussion p in Enum.GetValues(typeof(GeneralMidiPercussion)))
            {
                // DryWetMidi is the GM note-number authority. If the installed
                // version names this differently (e.g. GetNoteNumber), fix HERE only.
                int n = p.AsSevenBitNumber();
                if (!map.ContainsKey(n)) map[n] = p;
            }
            return map;
        }

        // -------------------------------------------------------------------
        // Options / warnings / result
        // -------------------------------------------------------------------

        /// <summary>Caller-supplied import parameters (from the editor's UI).</summary>
        public struct Options
        {
            /// <summary>Target meter; drives beats-per-measure and the grid beat unit.</summary>
            public TimeSignature timeSignature;

            /// <summary>Steps per grid beat (the editor clamps 1–4).</summary>
            public int subdivisions;

            /// <summary>
            /// Declared measure count. &lt;= 0 = derive from content (capped at
            /// <see cref="MaxDerivedMeasures"/>); &gt; 0 = fixed, notes beyond are
            /// dropped with a warning.
            /// </summary>
            public int measures;

            /// <summary>0-based MIDI channel filter; -1 = accept all channels.
            /// Default usage passes <see cref="GmDrumChannel"/>.</summary>
            public int channel;
        }

        public enum ImportMode { Failed, Full }

        public enum ImportWarningKind
        {
            UnsupportedTimeDivision,
            NoNotesFound,
            UnmappedNoteNumber,
            OffGridSnap,
            StepCollision,
            NotesBeyondRange,
            MeasuresCapped,
        }

        /// <summary>Mirrors <see cref="DrumPatternEditorImporter.ImportWarning"/>'s
        /// ToString shape so the window's warning list renders both uniformly.</summary>
        public readonly struct ImportWarning
        {
            public readonly ImportWarningKind kind;

            /// <summary>Result lane index this warning relates to, or -1 if not lane-specific.</summary>
            public readonly int laneIndex;

            public readonly string detail;

            public ImportWarning(ImportWarningKind kind, string detail, int laneIndex = -1)
            {
                this.kind = kind;
                this.laneIndex = laneIndex;
                this.detail = detail;
            }

            public override string ToString()
            {
                string loc = laneIndex >= 0 ? $"lane {laneIndex}" : "file";
                return $"[{kind}] {loc}: {detail}";
            }
        }

        /// <summary>One resolved lane: instrument + modal default velocity + a full-length step list.</summary>
        public readonly struct LaneResult
        {
            public readonly GeneralMidiPercussion instrument;
            public readonly int defaultVelocity;

            /// <summary>Length == total steps. Active steps at the lane default use the
            /// velocity-0 sentinel (canonical <see cref="DrumPatternData.StepState"/> compression).</summary>
            public readonly IReadOnlyList<DrumPatternData.StepState> steps;

            public LaneResult(
                GeneralMidiPercussion instrument,
                int defaultVelocity,
                IReadOnlyList<DrumPatternData.StepState> steps)
            {
                this.instrument = instrument;
                this.defaultVelocity = defaultVelocity;
                this.steps = steps;
            }
        }

        /// <summary>
        /// Outcome. On <see cref="ImportMode.Full"/>, the grid parameters echo the
        /// options (measures resolved) and <see cref="lanes"/> are ordered by GM
        /// note number ascending. On <see cref="ImportMode.Failed"/>, inspect
        /// <see cref="warnings"/>.
        /// </summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly int subdivisions;

            public readonly IReadOnlyList<LaneResult> lanes;
            public readonly IReadOnlyList<ImportWarning> warnings;

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                int subdivisions,
                IReadOnlyList<LaneResult> lanes,
                IReadOnlyList<ImportWarning> warnings)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.lanes = lanes ?? Array.Empty<LaneResult>();
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
            }
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
                return new Result(ImportMode.Failed, ts, 0, subdivisions, null, warnings);
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
                      $"{allNotes.Count()} note(s) total — try disabling the drum-channel filter."
                    : "the file contains no notes.";
                warnings.Add(new ImportWarning(ImportWarningKind.NoNotesFound, hint));
                return new Result(ImportMode.Failed, ts, 0, subdivisions, null, warnings);
            }

            // ---- 3. Grid math (beat-unit aware). ----
            var tsProps = TimeSignatureProperties[ts];
            int beatsPerMeasure = tsProps.BeatsPerMeasure;
            int beatUnit = tsProps.BeatUnit;
            int stepsPerMeasure = beatsPerMeasure * subdivisions;
            double gridBeatsPerQuarter = beatUnit / 4.0;

            // ---- 4. Map + quantize each note into (percussion, step, velocity) hits. ----
            var hits = new List<(GeneralMidiPercussion perc, int step, int velocity)>();
            var unmappedCounts = new Dictionary<int, int>();
            int offGridDetailCount = 0;
            int offGridTotal = 0;
            int maxStep = -1;

            foreach (var n in filtered)
            {
                int noteNumber = n.NoteNumber;
                if (!NoteToPercussion.TryGetValue(noteNumber, out var perc))
                {
                    unmappedCounts.TryGetValue(noteNumber, out int c);
                    unmappedCounts[noteNumber] = c + 1;
                    continue;
                }

                double quarterNotes = n.Time / tpqn;
                double gridBeats = quarterNotes * gridBeatsPerQuarter;
                double rawStep = gridBeats * subdivisions;
                int step = (int)Math.Round(rawStep, MidpointRounding.AwayFromZero);
                double err = Math.Abs(rawStep - step);

                if (err > SnapWarnThresholdSteps)
                {
                    offGridTotal++;
                    if (offGridDetailCount < MaxDetailedWarnings)
                    {
                        offGridDetailCount++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.OffGridSnap,
                            $"{perc} at tick {n.Time} is {err:0.##} steps off the grid; snapped to step {step}."));
                    }
                }

                if (step < 0) step = 0;
                int velocity = Math.Min(127, Math.Max(1, (int)n.Velocity));
                hits.Add((perc, step, velocity));
                if (step > maxStep) maxStep = step;
            }

            if (offGridTotal > offGridDetailCount)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.OffGridSnap,
                    $"{offGridTotal - offGridDetailCount} further off-grid note(s) snapped (details omitted)."));

            foreach (var kv in unmappedCounts.OrderBy(k => k.Key))
                warnings.Add(new ImportWarning(
                    ImportWarningKind.UnmappedNoteNumber,
                    $"{kv.Value} note(s) on number {kv.Key} have no GeneralMidiPercussion mapping; skipped."));

            if (hits.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NoNotesFound,
                    "No note mapped to a GM percussion instrument; nothing to import."));
                return new Result(ImportMode.Failed, ts, 0, subdivisions, null, warnings);
            }

            // ---- 5. Resolve measure count. ----
            int measures;
            if (options.measures > 0)
            {
                measures = options.measures;
            }
            else
            {
                measures = Math.Max(1,
                    (int)Math.Ceiling((maxStep + 1) / (double)stepsPerMeasure));
                if (measures > MaxDerivedMeasures)
                {
                    warnings.Add(new ImportWarning(
                        ImportWarningKind.MeasuresCapped,
                        $"Content implies {measures} measures; capped at {MaxDerivedMeasures}. " +
                        "Notes beyond the cap are dropped."));
                    measures = MaxDerivedMeasures;
                }
            }
            int totalSteps = measures * stepsPerMeasure;

            // ---- 6. Drop out-of-range hits (explicit measures or cap). ----
            int dropped = hits.RemoveAll(h => h.step >= totalSteps);
            if (dropped > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NotesBeyondRange,
                    $"{dropped} note(s) fall beyond the {measures}-measure range; dropped."));

            if (hits.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NoNotesFound,
                    "All mapped notes fell outside the measure range; nothing to import."));
                return new Result(ImportMode.Failed, ts, measures, subdivisions, null, warnings);
            }

            // ---- 7. Build lanes (ordered by GM note number ascending). ----
            var byPerc = hits
                .GroupBy(h => h.perc)
                .OrderBy(g => g.Key.AsSevenBitNumber())
                .ToList();

            var lanes = new List<LaneResult>(byPerc.Count);
            for (int laneIndex = 0; laneIndex < byPerc.Count; laneIndex++)
            {
                var group = byPerc[laneIndex];

                // Collision resolution first: same step keeps the higher velocity.
                var kept = new Dictionary<int, int>(); // step → velocity
                int collisions = 0;
                foreach (var h in group)
                {
                    if (kept.TryGetValue(h.step, out int existing))
                    {
                        collisions++;
                        if (h.velocity > existing) kept[h.step] = h.velocity;
                    }
                    else
                    {
                        kept[h.step] = h.velocity;
                    }
                }
                if (collisions > 0)
                    warnings.Add(new ImportWarning(
                        ImportWarningKind.StepCollision,
                        $"{collisions} same-step hit(s) on {group.Key}; the higher velocity was kept.",
                        laneIndex));

                // Modal default velocity over the kept steps (tie → lower velocity,
                // for determinism).
                int defaultVelocity = kept.Values
                    .GroupBy(v => v)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .First().Key;

                // Full-length step list; default-velocity steps use the sentinel.
                var steps = new List<DrumPatternData.StepState>(totalSteps);
                for (int s = 0; s < totalSteps; s++)
                {
                    if (kept.TryGetValue(s, out int v))
                        steps.Add(DrumPatternData.StepState.On(v == defaultVelocity ? 0 : v));
                    else
                        steps.Add(DrumPatternData.StepState.Off);
                }

                lanes.Add(new LaneResult(group.Key, defaultVelocity, steps));
            }

            return new Result(ImportMode.Full, ts, measures, subdivisions, lanes, warnings);
        }
    }
}
#endif
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using NoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure-function importer: a standard MIDI file → the canonical
    /// <see cref="MelodyPatternData"/> note shape
    /// (<see cref="MelodyPatternData.MelodyNoteEvent"/> list, beat-absolute timing).
    /// Phase M2 deliverable per <c>Roadmap_MIDI_Import.md</c> (supersedes
    /// <c>Roadmap_Melody_Authoring_MVP.md</c> Phase D1); consumed by the
    /// Melody Pattern Editor's "MIDI File Import" panel.
    /// </summary>
    /// <remarks>
    /// <para>Same mold as <see cref="DrumMidiImporter"/>: no Unity-API calls in the
    /// parse, no asset mutation, no editor-window state — the window owns applying
    /// the returned <see cref="Result"/> to the working copy (Apply/Save As remains
    /// the only asset write path). EditMode-testable against in-memory
    /// DryWetMidi files. Only ticks-per-quarter-note files are supported (SMPTE
    /// is a hard fail). Grid-beat conversion is beat-unit aware:
    /// <c>gridBeats = quarterNotes × beatUnit / 4</c>.</para>
    ///
    /// <para><b>Pitch mapping (D-MIDI1=A, D-MIDI2=A, M2-D1=A, M2-D6=A).</b>
    /// The caller supplies root (<see cref="NoteName"/>) + <see cref="Tonality"/>;
    /// absolute pitch resolves to (<see cref="ScaleDegree"/>, scale octave) against
    /// the package interval tables (via <c>GetScaleFromTonality</c>). Chromatic
    /// notes snap to the nearest diatonic degree with a per-note warning; on an
    /// equidistant tie the note snaps DOWN in pitch. Note: in all seven modes every
    /// chromatic pitch class sits exactly one semitone from a scale tone on each
    /// side, so the tie rule is the operative rule — chromatic notes always snap
    /// one semitone down. The general nearest-degree search is kept for
    /// forward-compatibility with gapped scales.</para>
    ///
    /// <para><b>Reference octave (M2-D2=A).</b> <c>octaveOffset</c> is relative to a
    /// runtime reference (the instrument's mid register), unknown at import time.
    /// The importer auto-centers: the MODAL scale octave across the imported notes
    /// (tie → lower) becomes offset 0, mirroring M1's modal-default-velocity idiom.
    /// The chosen reference and offset span are echoed on the <see cref="Result"/>.</para>
    ///
    /// <para><b>Monophonization (M2-D4=A).</b> After quantization: notes sharing a
    /// start keep the highest pitch (warning); a note overlapping the next note's
    /// start is truncated at that start (warning). Durations are quantized to the
    /// subdivision grid with a one-step floor (M2-D5=A).</para>
    ///
    /// <para><b>No silent fallback.</b> Every lossy step emits an
    /// <see cref="ImportWarning"/> with the M1 shape <c>[Kind] loc: detail</c>,
    /// detailed up to <see cref="MaxDetailedWarnings"/> then aggregated.</para>
    /// </remarks>
    public static class MelodyMidiImporter
    {
        /// <summary>Snap error (in step units) above which an off-grid warning is emitted.</summary>
        public const double SnapWarnThresholdSteps = 0.25;

        /// <summary>Cap for content-derived measure counts (pathological-file guard).</summary>
        public const int MaxDerivedMeasures = 64;

        /// <summary>Max per-note detail lines per warning kind before aggregating.</summary>
        public const int MaxDetailedWarnings = 8;

        // -------------------------------------------------------------------
        // Options / warnings / result
        // -------------------------------------------------------------------

        /// <summary>Caller-supplied import parameters (from the editor's UI).</summary>
        public struct Options
        {
            /// <summary>Key root (D-MIDI1=A / M2-D1=A: user-specified, DryWetMidi
            /// <see cref="NoteName"/> — the same type the runtime resolution seam uses).</summary>
            public NoteName rootNote;

            /// <summary>Mode; selects the package interval table for pitch → degree.</summary>
            public Tonality tonality;

            /// <summary>Target meter; drives beats-per-measure and the grid beat unit.</summary>
            public TimeSignature timeSignature;

            /// <summary>Steps per grid beat (the melody editor clamps 1–8).</summary>
            public int subdivisions;

            /// <summary>
            /// Declared measure count. &lt;= 0 = derive from content — covering the
            /// LAST NOTE END, since melody notes have duration (capped at
            /// <see cref="MaxDerivedMeasures"/>); &gt; 0 = fixed: notes starting
            /// beyond are dropped, notes ending beyond are clipped, with warnings.
            /// </summary>
            public int measures;

            /// <summary>0-based MIDI channel filter; -1 = accept all channels
            /// (M2-D3=A default; a merge across channels warns).</summary>
            public int channel;
        }

        public enum ImportMode { Failed, Full }

        public enum ImportWarningKind
        {
            UnsupportedTimeDivision,
            NoNotesFound,
            ChannelsMerged,
            ChromaticSnapped,
            OffGridSnap,
            DurationSnapped,
            PolyphonyReduced,
            OverlapTruncated,
            NotesBeyondRange,
            DurationClipped,
            MeasuresCapped,
        }

        /// <summary>Mirrors <see cref="DrumMidiImporter.ImportWarning"/>'s ToString
        /// shape (<c>[Kind] loc: detail</c>) so the window's warning list renders
        /// both importers uniformly. Melody has no lanes; loc is always "file".</summary>
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
        /// Outcome. On <see cref="ImportMode.Full"/>, <see cref="notes"/> is the
        /// monophonic, grid-quantized note list ordered by startBeat, and
        /// <see cref="referenceOctave"/> / offset span document the M2-D2=A
        /// auto-centering. On <see cref="ImportMode.Failed"/>, inspect
        /// <see cref="warnings"/>.
        /// </summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly int subdivisions;

            /// <summary>Absolute scale octave mapped to octaveOffset 0
            /// (modal across imported notes, tie → lower). Meaningful on Full.</summary>
            public readonly int referenceOctave;

            /// <summary>Smallest octaveOffset in <see cref="notes"/> (0 when empty).</summary>
            public readonly int minOctaveOffset;

            /// <summary>Largest octaveOffset in <see cref="notes"/> (0 when empty).</summary>
            public readonly int maxOctaveOffset;

            public readonly IReadOnlyList<MelodyPatternData.MelodyNoteEvent> notes;
            public readonly IReadOnlyList<ImportWarning> warnings;

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                int subdivisions,
                int referenceOctave,
                int minOctaveOffset,
                int maxOctaveOffset,
                IReadOnlyList<MelodyPatternData.MelodyNoteEvent> notes,
                IReadOnlyList<ImportWarning> warnings)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.subdivisions = subdivisions;
                this.referenceOctave = referenceOctave;
                this.minOctaveOffset = minOctaveOffset;
                this.maxOctaveOffset = maxOctaveOffset;
                this.notes = notes ?? Array.Empty<MelodyPatternData.MelodyNoteEvent>();
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
            }

            internal static Result Fail(
                TimeSignature ts, int measures, int subdivisions, List<ImportWarning> warnings)
                => new Result(ImportMode.Failed, ts, measures, subdivisions,
                    0, 0, 0, null, warnings);
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

            // M2-D3=A: merging channels is legal but never silent.
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
                    "If this mixes parts, re-import with a channel filter."));
            }

            // ---- 3. Grid math (beat-unit aware) + scale tables. ----
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
            var cum = new int[degreeCount];
            for (int i = 1; i < degreeCount; i++)
                cum[i] = cum[i - 1] + intervals[i - 1].HalfSteps;
            int rootPc = (int)options.rootNote;                   // DryWetMidi NoteName: C=0..B=11

            // ---- 4. Per note: quantize timing, map pitch → (degree, scale octave). ----
            var mapped = new List<(int startStep, int durSteps, int degree, int scaleOct,
                                   int snappedPitch, int velocity)>(filtered.Count);

            int offGridDetail = 0, offGridTotal = 0;
            int durSnapDetail = 0, durSnapTotal = 0;
            int chromDetail = 0, chromTotal = 0;

            foreach (var n in filtered)
            {
                // -- Timing: onset --
                double startGridBeats = (n.Time / tpqn) * gridBeatsPerQuarter;
                double rawStartSteps = startGridBeats * subdivisions;
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

                // -- Timing: duration (M2-D5=A: quantized, one-step floor) --
                double durGridBeats = (n.Length / tpqn) * gridBeatsPerQuarter;
                double rawDurSteps = durGridBeats * subdivisions;
                int durSteps = (int)Math.Round(rawDurSteps, MidpointRounding.AwayFromZero);
                double durErr = Math.Abs(rawDurSteps - durSteps);
                bool floored = durSteps < 1;
                if (floored) durSteps = 1;

                if (durErr > SnapWarnThresholdSteps || floored)
                {
                    durSnapTotal++;
                    if (durSnapDetail < MaxDetailedWarnings)
                    {
                        durSnapDetail++;
                        string why = floored
                            ? $"raised to the one-step floor ({durSteps} step)"
                            : $"snapped to {durSteps} step(s) ({durErr:0.##} steps of error)";
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.DurationSnapped,
                            $"Note {n.NoteNumber} at tick {n.Time}: duration {why}."));
                    }
                }

                // -- Pitch: nearest degree; tie → snap DOWN (M2-D6=A) --
                int pitch = n.NoteNumber;
                int rel = ((pitch - rootPc) % 12 + 12) % 12;

                int bestDegree = 0, bestDist = int.MaxValue;
                bool bestDown = true;
                for (int d = 0; d < degreeCount; d++)
                {
                    int off = ((cum[d] % 12) + 12) % 12;
                    int distDown = ((rel - off) % 12 + 12) % 12;
                    int distUp = ((off - rel) % 12 + 12) % 12;

                    if (distDown < bestDist || (distDown == bestDist && !bestDown))
                    { bestDist = distDown; bestDegree = d; bestDown = true; }
                    if (distUp < bestDist)
                    { bestDist = distUp; bestDegree = d; bestDown = false; }
                }

                int snappedPitch = bestDown ? pitch - bestDist : pitch + bestDist;

                if (bestDist > 0)
                {
                    chromTotal++;
                    if (chromDetail < MaxDetailedWarnings)
                    {
                        chromDetail++;
                        warnings.Add(new ImportWarning(
                            ImportWarningKind.ChromaticSnapped,
                            $"Note {pitch} at tick {n.Time} is not in {options.rootNote} {options.tonality}; " +
                            $"snapped {(bestDown ? "down" : "up")} {bestDist} semitone(s) to degree " +
                            $"{(ScaleDegree)bestDegree}."));
                    }
                }

                // snappedPitch == 12·(scaleOct+1) + rootPc + cum[bestDegree], exactly divisible.
                int scaleOct = (snappedPitch - rootPc - cum[bestDegree]) / 12 - 1;

                int velocity = Math.Min(127, Math.Max(1, (int)n.Velocity));
                mapped.Add((startStep, durSteps, bestDegree, scaleOct, snappedPitch, velocity));
            }

            if (offGridTotal > offGridDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.OffGridSnap,
                    $"{offGridTotal - offGridDetail} further off-grid note(s) snapped (details omitted)."));
            if (durSnapTotal > durSnapDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.DurationSnapped,
                    $"{durSnapTotal - durSnapDetail} further duration(s) adjusted (details omitted)."));
            if (chromTotal > chromDetail)
                warnings.Add(new ImportWarning(ImportWarningKind.ChromaticSnapped,
                    $"{chromTotal - chromDetail} further chromatic note(s) snapped (details omitted)."));

            // ---- 5. Resolve measure count (content-derived covers the last note END). ----
            int measures;
            int maxEndStep = mapped.Max(m => m.startStep + m.durSteps);
            if (options.measures > 0)
            {
                measures = options.measures;
            }
            else
            {
                measures = Math.Max(1,
                    (int)Math.Ceiling(maxEndStep / (double)stepsPerMeasure));
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

            // ---- 6. Range handling: drop late starts, clip overhanging durations. ----
            int droppedBeyond = mapped.RemoveAll(m => m.startStep >= totalSteps);
            if (droppedBeyond > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NotesBeyondRange,
                    $"{droppedBeyond} note(s) start beyond the {measures}-measure range; dropped."));

            int clipped = 0;
            for (int i = 0; i < mapped.Count; i++)
            {
                var m = mapped[i];
                if (m.startStep + m.durSteps > totalSteps)
                {
                    clipped++;
                    mapped[i] = (m.startStep, totalSteps - m.startStep,
                                 m.degree, m.scaleOct, m.snappedPitch, m.velocity);
                }
            }
            if (clipped > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.DurationClipped,
                    $"{clipped} note(s) extended past the {measures}-measure range; clipped to the pattern end."));

            if (mapped.Count == 0)
            {
                warnings.Add(new ImportWarning(
                    ImportWarningKind.NoNotesFound,
                    "All notes fell outside the measure range; nothing to import."));
                return Result.Fail(ts, measures, subdivisions, warnings);
            }

            // ---- 7. Monophonize (M2-D4=A). Deterministic order: start asc, then
            // pitch desc (highest wins), then velocity desc, then duration desc. ----
            mapped.Sort((a, b) =>
            {
                int c = a.startStep.CompareTo(b.startStep);
                if (c != 0) return c;
                c = b.snappedPitch.CompareTo(a.snappedPitch);
                if (c != 0) return c;
                c = b.velocity.CompareTo(a.velocity);
                if (c != 0) return c;
                return b.durSteps.CompareTo(a.durSteps);
            });

            var mono = new List<(int startStep, int durSteps, int degree, int scaleOct,
                                 int snappedPitch, int velocity)>(mapped.Count);
            int simultaneous = 0;
            foreach (var m in mapped)
            {
                if (mono.Count > 0 && mono[mono.Count - 1].startStep == m.startStep)
                {
                    simultaneous++;   // same quantized start: the first (highest pitch) already won
                    continue;
                }
                mono.Add(m);
            }
            if (simultaneous > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.PolyphonyReduced,
                    $"{simultaneous} simultaneous note(s) removed; the highest pitch was kept at each position."));

            int truncated = 0;
            for (int i = 0; i < mono.Count - 1; i++)
            {
                var cur = mono[i];
                int nextStart = mono[i + 1].startStep;
                if (cur.startStep + cur.durSteps > nextStart)
                {
                    truncated++;
                    mono[i] = (cur.startStep, nextStart - cur.startStep,
                               cur.degree, cur.scaleOct, cur.snappedPitch, cur.velocity);
                }
            }
            if (truncated > 0)
                warnings.Add(new ImportWarning(
                    ImportWarningKind.OverlapTruncated,
                    $"{truncated} overlapping note(s) truncated at the next note's start (monophonic melody)."));

            // ---- 8. Reference octave (M2-D2=A): modal scale octave, tie → lower. ----
            int referenceOctave = mono
                .GroupBy(m => m.scaleOct)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First().Key;

            // ---- 9. Emit MelodyNoteEvents (beat-absolute timing). ----
            var notes = new List<MelodyPatternData.MelodyNoteEvent>(mono.Count);
            int minOffset = int.MaxValue, maxOffset = int.MinValue;
            foreach (var m in mono)
            {
                int offset = m.scaleOct - referenceOctave;
                if (offset < minOffset) minOffset = offset;
                if (offset > maxOffset) maxOffset = offset;

                notes.Add(MelodyPatternData.MelodyNoteEvent.Create(
                    (ScaleDegree)m.degree,
                    m.startStep / (float)subdivisions,
                    m.durSteps / (float)subdivisions,
                    offset,
                    m.velocity));
            }

            return new Result(ImportMode.Full, ts, measures, subdivisions,
                referenceOctave, minOffset, maxOffset, notes, warnings);
        }
    }
}
#endif
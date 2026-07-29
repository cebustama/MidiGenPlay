using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// RUNTIME-REQUALITY + HARMONY-PURE-1 — the shared-progression harmony
    /// publication transform. Diatonic re-resolution of chord qualities for
    /// opt-in progressions (ChordProgressionData.qualityRenderPolicy ==
    /// DiatonicToPart*), the opt-in COLOR TABLE on top of it, and the
    /// per-event SECONDARY DOMINANT primitive (SECDOM-1).
    ///
    /// WHY data-level: backing, bass and melody each compute chord pitch
    /// classes independently from the SHARED progression's per-event
    /// quality (GetChordNoteNames(degreeRoot, e.quality) at their own
    /// sites). Re-resolving anywhere but the shared data itself would make
    /// the consumers diverge, so this transform runs on the runtime clone
    /// at the two publication boundaries:
    ///   1. ChordTrackComposer step 2c (after step 2b, so the FINAL part
    ///      tonality is used — including any tonality-filter alignment);
    ///   2. SongOrchestrator.TrySeedDefaultProgression (the
    ///      MGP-ALWTTT-BASS-SOLO-1 backing-less path, where no backing
    ///      composer exists to do it).
    /// The bass's raw TrackParameters.Pattern fallback (no backing composed
    /// first) deliberately stays outside — same recorded hazard family as
    /// the Bass SSoT §1 normalization-order note.
    ///
    /// PIPELINE ORDER (contract, pinned by tests): the caller reprojects
    /// TS/subdivision FIRST (ChordTrackComposer), then this transform runs
    ///   A. core requality  (policy-gated; D-RQ-* decisions below)
    ///   B. color table     (policy + useColorTable gated; D-CT-* below) —
    ///      runs on the POST-core-remap quality, because rules like
    ///      ii(dim) -> iv react to the diminished the remap PRODUCES;
    ///   C. secondary dominants (per-event opt-in; D-SD-* below) — ALWAYS
    ///      active regardless of policy and tonality (the event field is
    ///      the opt-in; no existing asset carries it), validated against
    ///      the post-A/B effective view.
    /// All passes are computed into one effective view and materialized in
    /// a single clone-if-changed step.
    ///
    /// Decision surface:
    /// - D-RQ-SURF=A   opt-in lives on the asset; AsAuthored (default) is a
    ///                 guaranteed no-op for passes A/B => every existing
    ///                 asset byte-identical (pass C needs the per-event
    ///                 field, so the guarantee holds there too).
    /// - D-RQ-BORROW=A only events with isDiatonic == true re-resolve;
    ///                 borrowed chords keep authored quality + accidental.
    /// - D-RQ-MAP=A    only the core modal alphabet re-maps, preserving
    ///                 "size": the four triad qualities -> diatonic triad of
    ///                 (part tonality, degree); the five seventh qualities ->
    ///                 diatonic seventh. Sus2/Sus4/6ths/9ths pass through
    ///                 unchanged in pass A (no clean modal reading; color is
    ///                 authored intent) — pass B may re-color them, but only
    ///                 behind its own opt-in.
    /// - D-RQ-FUNC=A / D-RQ-FUNC-SCOPE=A (REQUALITY-FUNC amendment, lab Q2):
    ///                 under DiatonicToPartFunctional, a Dominant-degree event
    ///                 authored Major or Dominant7 whose diatonic re-resolution
    ///                 would differ KEEPS its authored quality and is marked
    ///                 borrowed (isDiatonic=false) — the harmonic-minor
    ///                 practice of surgically preserving the leading tone in
    ///                 the dominant. All other degrees re-resolve exactly as
    ///                 plain DiatonicToPart. Idempotent: the flipped event is
    ///                 borrowed on re-entry and therefore skipped.
    /// - D-RQ-LOCRIAN=A Locrian as target tonality skips passes A and B for
    ///                 BOTH opt-in policies: the tonic triad itself is
    ///                 diminished and every functional reading collapses (lab
    ///                 Q2 contraexample). Pass C (secdom) still runs — some
    ///                 Locrian degrees do carry major/minor triads.
    /// - D-RQ-DET      pure function of (progression, tonality): zero rng
    ///                 draws, no stream perturbation. Clone-if-changed: the
    ///                 asset instance is never mutated (no silent writes);
    ///                 when nothing would change, the SAME instance returns
    ///                 (reference identity is the cheap no-op signal).
    ///
    /// HARMONY-PURE-1 additions:
    /// - D-CT-GATE=A   the color table is gated by asset bool useColorTable
    ///                 (default false) AND a DiatonicToPart* policy, so
    ///                 assets already opted into requality keep their exact
    ///                 pre-B1 render. Rules: sixths (Aeolian/Phrygian:
    ///                 Major6/Minor6 -> Minor7; Dorian: Major6 -> Minor6),
    ///                 Phrygian Sus2 -> Sus4, ninths on minorized degrees
    ///                 (Dominant9/Major9 -> Minor9) with the FUNC exception
    ///                 (Dominant9 on V under Functional keeps quality, marked
    ///                 borrowed — mirrors D-RQ-FUNC).
    /// - D-CT-DIM=A    ii(dim) -> iv degree substitution when the post-remap
    ///                 quality on the Supertonic is diminished-family AND the
    ///                 event is LONG (lengthSteps >= ColorDiminishedMinBeats
    ///                 beats) or ACCENTED (starts on a measure downbeat).
    ///                 Substitution: degree = Subdominant, accidental = 0,
    ///                 size-preserving diatonic quality, isDiatonic = true.
    ///                 Applies to the post-remap state whether or not the
    ///                 remap changed it (a sustained authored ii-dim under
    ///                 the color table substitutes too); idempotent because
    ///                 the result is no longer diminished. Only ii is
    ///                 substituted (vii-dim is out of scope by decision).
    /// - D-SD-ENC=A / D-SD-OWN=A (see ChordProgressionData.ChordEvent):
    ///                 secondary dominants resolve at render as quality
    ///                 Dominant7 on the root a perfect 5th above the target
    ///                 degree's root IN THE CURRENT MODE, expressed as
    ///                 (degree, computed accidental), isDiatonic = false.
    ///                 Validity (else authored values render untouched):
    ///                 target diatonic triad is Major or Minor; the next
    ///                 event by startStep (wrapping — turnarounds are legal)
    ///                 is the target degree with accidental 0 and not itself
    ///                 a secondary dominant; duration <= target's duration.
    ///                 For VALID targets the computed accidental is always 0:
    ///                 the one degree with a diminished 5th above it is
    ///                 exactly the degree carrying the diatonic diminished
    ///                 triad, which validity already excludes. The [-1, +1]
    ///                 guard stays as pure defense.
    /// </summary>
    public static class ChordProgressionRequality
    {
        /// <summary>
        /// D-CT-DIM=A threshold: an event is "long" when it spans at least
        /// this many beats (lengthSteps >= this * subdivisions).
        /// </summary>
        public const int ColorDiminishedMinBeats = 2;

        /// <summary>
        /// Returns <paramref name="prog"/> unchanged (same reference) when
        /// nothing would change; otherwise returns a fresh runtime clone
        /// (name-preserving) with the pipeline (core requality -> color
        /// table -> secondary dominants) applied for
        /// <paramref name="tonality"/>. Never mutates the input. Pure — zero
        /// rng draws.
        /// </summary>
        public static ChordProgressionData ApplyDiatonicRequality(
            ChordProgressionData prog, Tonality tonality)
        {
            if (prog == null)
                return null;
            if (prog.events == null || prog.events.Count == 0)
                return prog;

            var policy = prog.qualityRenderPolicy;
            bool functional = policy ==
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional;
            bool requality =
                (policy == ChordProgressionData.QualityRenderPolicy.DiatonicToPart
                 || functional)
                && tonality != Tonality.Locrian; // D-RQ-LOCRIAN=A
            bool color = requality && prog.useColorTable; // D-CT-GATE=A

            int n = prog.events.Count;

            // Effective (post-pipeline) view, initialized to authored values.
            var effDegree = new ScaleDegree[n];
            var effAcc = new int[n];
            var effQuality = new ChordQuality[n];
            var effDiatonic = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var e = prog.events[i];
                if (e == null) continue;
                effDegree[i] = e.degree;
                effAcc[i] = e.degreeAccidental;
                effQuality[i] = e.quality;
                effDiatonic[i] = e.isDiatonic;
            }

            // Grid info for the D-CT-DIM accent/length tests.
            int beatsPerMeasure =
                TimeSignatureProperties.TryGetValue(prog.TimeSignature, out var tsInfo)
                    ? tsInfo.BeatsPerMeasure : 4;
            int subdivisions = Mathf.Max(1, prog.subdivisions);
            int stepsPerMeasure = Mathf.Max(1, beatsPerMeasure * subdivisions);

            // ---------------- Passes A (core) + B (color) -----------------
            if (requality)
            {
                for (int i = 0; i < n; i++)
                {
                    var e = prog.events[i];
                    if (e == null)
                        continue;
                    if (e.hasAppliedTarget)
                        continue; // SECDOM owns this event (pass C)
                    if (!e.isDiatonic)
                        continue; // D-RQ-BORROW=A

                    // ---- Pass A: core requality --------------------------
                    // REQUALITY-FUNC (D-RQ-FUNC-SCOPE=A): the dominant
                    // exception. A V authored Major/Dominant7 keeps its
                    // authored quality when the diatonic reading would
                    // differ; it becomes a borrowed chord on the clone
                    // (harmonic-minor practice). Size is preserved (Major is
                    // NOT promoted to Dominant7).
                    bool protectedDominant = false;
                    if (functional && e.degree == ScaleDegree.Dominant &&
                        (e.quality == ChordQuality.Major ||
                         e.quality == ChordQuality.Dominant7))
                    {
                        if (TryMapCoreQuality(e.quality, tonality, e.degree,
                                out var dq) && dq != e.quality)
                        {
                            effDiatonic[i] = false;
                        }
                        protectedDominant = true; // never remap / re-color
                    }
                    else if (TryMapCoreQuality(e.quality, tonality, e.degree,
                                 out var q))
                    {
                        // D-RQ-MAP=A. Re-resolved events are diatonic to the
                        // part BY CONSTRUCTION.
                        effQuality[i] = q;
                        effDiatonic[i] = true;
                    }

                    // ---- Pass B: color table -----------------------------
                    if (!color || protectedDominant || !effDiatonic[i])
                        continue;

                    // FUNC ninth exception (mirrors D-RQ-FUNC): a Dominant9
                    // on a minorized V under Functional keeps its authored
                    // quality and is marked borrowed instead of dropping to
                    // Minor9.
                    bool minorized =
                        GetDiatonicTriadQuality(tonality, effDegree[i]) ==
                        ChordQuality.Minor;
                    if (functional && minorized &&
                        effDegree[i] == ScaleDegree.Dominant &&
                        effQuality[i] == ChordQuality.Dominant9)
                    {
                        effDiatonic[i] = false;
                    }
                    else if (TryMapColorQuality(effQuality[i], tonality,
                                 effDegree[i], out var cq))
                    {
                        effQuality[i] = cq;
                    }

                    // D-CT-DIM=A: ii(dim) -> iv on long/accented events.
                    if (effDiatonic[i] &&
                        effDegree[i] == ScaleDegree.Supertonic &&
                        IsDiminishedFamily(effQuality[i]))
                    {
                        bool longEvent = e.lengthSteps >=
                            ColorDiminishedMinBeats * subdivisions;
                        int startInMeasure =
                            ((e.startStep % stepsPerMeasure) + stepsPerMeasure)
                            % stepsPerMeasure;
                        bool accented = startInMeasure == 0;

                        if (longEvent || accented)
                        {
                            bool seventh =
                                effQuality[i] != ChordQuality.Diminished;
                            effDegree[i] = ScaleDegree.Subdominant;
                            effAcc[i] = 0;
                            effQuality[i] = seventh
                                ? GetDiatonicSeventhQuality(
                                    tonality, ScaleDegree.Subdominant)
                                : GetDiatonicTriadQuality(
                                    tonality, ScaleDegree.Subdominant);
                            effDiatonic[i] = true;
                        }
                    }
                }
            }

            // ---------------- Pass C: secondary dominants -----------------
            // Always active; the per-event field is the opt-in (D-SD-OWN=A).
            ResolveSecondaryDominants(
                prog, tonality, effDegree, effAcc, effQuality, effDiatonic);

            // ---------------- Clone-if-changed ----------------------------
            bool changed = false;
            for (int i = 0; i < n && !changed; i++)
            {
                var e = prog.events[i];
                if (e == null) continue;
                changed = effDegree[i] != e.degree
                       || effAcc[i] != e.degreeAccidental
                       || effQuality[i] != e.quality
                       || effDiatonic[i] != e.isDiatonic;
            }
            if (!changed)
                return prog; // no-op: same reference, zero clones

            // Instantiate deep-copies the serialized event list; keep the
            // source name for readback identity.
            var clone = Object.Instantiate(prog);
            clone.name = prog.name;
            for (int i = 0; i < clone.events.Count && i < n; i++)
            {
                var ce = clone.events[i];
                if (ce == null) continue;
                ce.degree = effDegree[i];
                ce.degreeAccidental = effAcc[i];
                ce.quality = effQuality[i];
                ce.isDiatonic = effDiatonic[i];
            }
            return clone;
        }

        /// <summary>
        /// D-RQ-MAP=A core-alphabet mapping, size-preserving. Returns false
        /// (and echoes the authored quality) for qualities outside the core
        /// modal alphabet — Sus2/Sus4/Major6/Minor6/Dominant7sus4 and the
        /// ninths pass through as authored color. Public test seam (house
        /// pattern: pure seams are public, see SongOrchestrator).
        /// </summary>
        public static bool TryMapCoreQuality(
            ChordQuality authored, Tonality tonality, ScaleDegree degree,
            out ChordQuality mapped)
        {
            switch (authored)
            {
                case ChordQuality.Major:
                case ChordQuality.Minor:
                case ChordQuality.Diminished:
                case ChordQuality.Augmented:
                    mapped = GetDiatonicTriadQuality(tonality, degree);
                    return true;

                case ChordQuality.Major7:
                case ChordQuality.Minor7:
                case ChordQuality.Dominant7:
                case ChordQuality.HalfDiminished7:
                case ChordQuality.Diminished7:
                    mapped = GetDiatonicSeventhQuality(tonality, degree);
                    return true;

                default:
                    mapped = authored;
                    return false;
            }
        }

        /// <summary>
        /// D-CT-GATE=A color-table mapping (sixths, sus, ninths) WITHOUT the
        /// FUNC ninth exception and WITHOUT the ii(dim)->iv substitution —
        /// both live in the pipeline because they need event/policy context.
        /// Returns false (echoing the input) when no color rule applies.
        /// Public test seam.
        /// </summary>
        public static bool TryMapColorQuality(
            ChordQuality quality, Tonality tonality, ScaleDegree degree,
            out ChordQuality mapped)
        {
            mapped = quality;

            // Sixths: mode-level rules from the musical lab.
            if (tonality == Tonality.Aeolian || tonality == Tonality.Phrygian)
            {
                if (quality == ChordQuality.Major6 ||
                    quality == ChordQuality.Minor6)
                {
                    mapped = ChordQuality.Minor7;
                    return true;
                }
            }
            else if (tonality == Tonality.Dorian)
            {
                if (quality == ChordQuality.Major6)
                {
                    mapped = ChordQuality.Minor6;
                    return true;
                }
            }

            // Sus: Phrygian's b2 kills the M2 color.
            if (tonality == Tonality.Phrygian && quality == ChordQuality.Sus2)
            {
                mapped = ChordQuality.Sus4;
                return true;
            }

            // Ninths on minorized degrees.
            if (GetDiatonicTriadQuality(tonality, degree) == ChordQuality.Minor)
            {
                if (quality == ChordQuality.Dominant9 ||
                    quality == ChordQuality.Major9)
                {
                    mapped = ChordQuality.Minor9;
                    return true;
                }
            }

            return false;
        }

        /// <summary>True for the diminished-family qualities.</summary>
        public static bool IsDiminishedFamily(ChordQuality q)
            => q == ChordQuality.Diminished
            || q == ChordQuality.HalfDiminished7
            || q == ChordQuality.Diminished7;

        /// <summary>
        /// SECDOM-1 (D-SD-OWN=A) resolution over the effective view. Public
        /// entry is the pipeline; this stays private because it mutates the
        /// working arrays. Validity failures leave the event untouched.
        /// </summary>
        private static void ResolveSecondaryDominants(
            ChordProgressionData prog, Tonality tonality,
            ScaleDegree[] effDegree, int[] effAcc,
            ChordQuality[] effQuality, bool[] effDiatonic)
        {
            int n = prog.events.Count;

            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var e = prog.events[i];
                if (e != null && e.hasAppliedTarget) { any = true; break; }
            }
            if (!any)
                return;

            // Order indices by startStep for the "immediately before the
            // target" rule (wrapping: the last event's next is the first —
            // turnaround secondary dominants are legal).
            var order = Enumerable.Range(0, n)
                .Where(i => prog.events[i] != null)
                .OrderBy(i => prog.events[i].startStep)
                .ToList();
            if (order.Count < 2)
                return; // no distinct next event to target

            int[] offsets = GetDegreeSemitoneOffsets(tonality);

            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                var e = prog.events[i];
                if (!e.hasAppliedTarget)
                    continue;

                int targetIdx = Mathf.Clamp((int)e.appliedTarget, 0, 6);
                var target = (ScaleDegree)targetIdx;

                // Target validity: diatonic triad in the CURRENT tonality is
                // major or minor.
                var targetTriad = GetDiatonicTriadQuality(tonality, target);
                if (targetTriad != ChordQuality.Major &&
                    targetTriad != ChordQuality.Minor)
                    continue;

                // Position: the next event (by startStep, wrapping) must BE
                // the target — checked against the effective view so earlier
                // passes are honored — with no accidental, and must not be a
                // secondary dominant itself.
                int j = order[(k + 1) % order.Count];
                if (j == i)
                    continue;
                var nextEvent = prog.events[j];
                if (nextEvent.hasAppliedTarget)
                    continue;
                if (effDegree[j] != target || effAcc[j] != 0)
                    continue;

                // Duration: no longer than the target.
                if (e.lengthSteps > nextEvent.lengthSteps)
                    continue;

                // Resolution: root = P5 above the target degree's root in
                // the current mode, expressed as (degree, accidental).
                int desiredPc = (offsets[targetIdx] + 7) % 12;
                int sdDegIdx = (targetIdx + 4) % 7;
                int diff = ((desiredPc - offsets[sdDegIdx]) % 12 + 12) % 12;
                if (diff > 6) diff -= 12;
                if (diff < -1 || diff > 1)
                    continue; // defensive; unreachable within diatonic modes

                effDegree[i] = (ScaleDegree)sdDegIdx;
                effAcc[i] = diff;
                effQuality[i] = ChordQuality.Dominant7;
                effDiatonic[i] = false;
            }
        }
    }
}
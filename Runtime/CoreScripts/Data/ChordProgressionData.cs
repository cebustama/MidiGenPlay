using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Chord Progression")]
    public class ChordProgressionData : PatternDataSO
    {
        [Header("Harmonic Grid")]
        [Tooltip("How many timing steps per beat this progression uses.")]
        public int subdivisions = 1;

        [System.Serializable]
        public class ChordEvent
        {
            public int startStep;    // 0..(measures*beatsPerMeasure*subdivisions-1)
            public int lengthSteps;  // >= 1
            public ScaleDegree degree;
            public ChordQuality quality;
            public int velocity;    // 0..127

            [Tooltip("True if this chord’s quality matches the diatonic harmony " +
             "for the reference tonality; false if it is a borrowed/non-diatonic chord.")]
            public bool isDiatonic = true;

            [Range(-1, 1)]
            [Tooltip("Accidental on the scale degree root: -1 = flat (♭), 0 = natural, +1 = sharp (♯).")]
            public int degreeAccidental = 0;

            // ---- SECDOM-1 (HARMONY-PURE-1) -------------------------------
            // Secondary-dominant primitive. The FIELD is the opt-in: no
            // existing asset has it, so the zero-impact guarantee holds by
            // construction and resolution runs regardless of
            // qualityRenderPolicy (even AsAuthored).
            //
            // D-SD-ENC=A  bool + ScaleDegree pair (Unity can't serialize
            //             nullables; a -1 sentinel int would be inspector-
            //             hostile).
            // D-SD-OWN=A  when hasAppliedTarget is true, render IGNORES the
            //             authored degree/degreeAccidental/quality and
            //             rewrites the event on the runtime clone: root = a
            //             perfect 5th above the target degree's root IN THE
            //             CURRENT MODE (accidental computed), quality =
            //             Dominant7, isDiatonic = false. Authored values are
            //             documentation/fallback if the flag is turned off.
            // Validity (checked at render; invalid => authored values render
            // untouched): target's diatonic triad in the current tonality is
            // Major or Minor; the NEXT event (by startStep, wrapping) has
            // degree == appliedTarget with no accidental; this event's
            // lengthSteps <= the target event's lengthSteps.
            [Tooltip("SECDOM-1. If enabled, this event renders as the " +
                     "secondary dominant (V7/x) of the target degree: root a " +
                     "perfect 5th above the target's root in the current " +
                     "mode, quality Dominant7, marked borrowed. Authored " +
                     "degree/quality are ignored while enabled. The event " +
                     "must sit immediately before the target and last no " +
                     "longer than it.")]
            public bool hasAppliedTarget = false;

            [Tooltip("SECDOM-1. Target degree for the secondary dominant " +
                     "(only used when Has Applied Target is on). Valid " +
                     "targets are degrees whose diatonic triad in the " +
                     "render tonality is major or minor.")]
            public ScaleDegree appliedTarget = ScaleDegree.Tonic;
        }

        [Tooltip("Events in step units (startStep/lengthSteps).")]
        public List<ChordEvent> events = new List<ChordEvent>();

        /// <summary>
        /// RUNTIME-REQUALITY (D-RQ-SURF=A). How this progression's chord
        /// qualities render when the Part tonality differs from the authored
        /// intent. Append-only; values serialized, never renumbered.
        /// </summary>
        public enum QualityRenderPolicy
        {
            AsAuthored = 0,     // qualities render exactly as stored (legacy default)
            DiatonicToPart = 1, // diatonic events re-resolve to the Part tonality
            // REQUALITY-FUNC (D-RQ-FUNC=A): DiatonicToPart PLUS the
            // common-practice dominant exception — a Dominant-degree event
            // authored Major/Dominant7 keeps its authored quality (and is
            // marked borrowed) instead of losing its leading tone in modes
            // whose diatonic v is not major. "Functional" = common-practice
            // cadential reading; plain DiatonicToPart = pure modal reading.
            DiatonicToPartFunctional = 2,
        }

        [Header("Render Policy")]
        [Tooltip("RUNTIME-REQUALITY. AsAuthored (default): chord qualities " +
                 "render exactly as stored — every pre-existing asset " +
                 "deserializes into this and is byte-identical. DiatonicToPart: " +
                 "at render time, events marked isDiatonic re-resolve their " +
                 "quality to the diatonic chord of the PART's tonality on the " +
                 "same degree (I–IV–V in a minor part renders i–iv–v), " +
                 "preserving triad-vs-seventh size (D-RQ-MAP=A: only the four " +
                 "triad and five seventh qualities re-map; Sus/6th/9th colors " +
                 "pass through). Borrowed chords (isDiatonic=false) always " +
                 "keep their authored quality and accidental (D-RQ-BORROW=A). " +
                 "Applied to the runtime clone only — the asset is never " +
                 "mutated. Pure and deterministic: zero rng draws. " +
                 "DiatonicToPartFunctional: same as DiatonicToPart, plus the " +
                 "common-practice dominant exception (REQUALITY-FUNC): a V " +
                 "authored Major or Dominant7 KEEPS its authored quality — " +
                 "marked as borrowed (isDiatonic=false) — in modes whose " +
                 "diatonic v loses the leading tone (harmonic-minor practice: " +
                 "the surgical raise of the dominant's third). Pick Functional " +
                 "for cadence-driven material, plain DiatonicToPart for pure " +
                 "modal color. Locrian is a documented no-op for both " +
                 "(degenerate tonic).")]
        public QualityRenderPolicy qualityRenderPolicy = QualityRenderPolicy.AsAuthored;

        /// <summary>
        /// REQUALITY-2 (D-CT-GATE=A). Opt-in for the musical-lab COLOR TABLE,
        /// orthogonal to the policy so assets already opted into
        /// DiatonicToPart/Functional keep their exact pre-B1 render (the
        /// zero-impact guarantee is by construction, not by audit). Only
        /// effective when qualityRenderPolicy != AsAuthored. Rules (applied to
        /// diatonic events, AFTER the core remap, on the runtime clone only):
        ///  - Sixths: Aeolian/Phrygian part => Major6/Minor6 -> Minor7 (no
        ///    natural 6th to color with); Dorian part => Major6 -> Minor6
        ///    (Dorian's own color keeps the 6th, fixes the third).
        ///  - Sus: Phrygian part => Sus2 -> Sus4 (the b2 kills the M2 color).
        ///  - Ninths: on minorized degrees (diatonic triad of the part
        ///    tonality is minor) Dominant9/Major9 -> Minor9; EXCEPT a
        ///    Dominant9 on V under the Functional policy, which keeps its
        ///    authored quality and is marked borrowed (same practice as the
        ///    core dominant exception).
        ///  - Degree substitution ii(dim) -> iv (D-CT-DIM=A): when the core
        ///    remap leaves a diminished-family quality on the Supertonic of a
        ///    LONG (>= 2 beats) or ACCENTED (starts on a measure downbeat)
        ///    diatonic event, the event re-renders as the Subdominant with
        ///    the size-preserving diatonic quality (accidental reset to 0).
        ///    Short, unaccented passing ii(dim) is kept — common practice.
        /// </summary>
        [Tooltip("REQUALITY-2 color table (opt-in; needs a DiatonicToPart* " +
                 "policy). Adds the musical-lab color rules on top of the " +
                 "core remap: sixths re-colored per mode (6->m7 in " +
                 "Aeolian/Phrygian, 6->m6 in Dorian), sus2->sus4 in " +
                 "Phrygian, 9/Maj9->m9 on minorized degrees (V9 protected " +
                 "under Functional), and ii(dim)->iv substitution on long or " +
                 "accented events. Applied to the runtime clone only; " +
                 "existing opted-in assets are untouched unless this is " +
                 "explicitly enabled.")]
        public bool useColorTable = false;

        /// <summary>
        /// CADENCE-META (D-CAD-AUTH=A). Hand-authored cadence classification
        /// of this progression. Pure metadata: the package stores it, the
        /// consuming game's replace/reskin gate reads it. None (default) is
        /// the no-op value every pre-existing asset deserializes into.
        /// Append-only; values serialized, never renumbered.
        /// </summary>
        public enum CadenceType
        {
            None = 0,
            Authentic = 1,
            Plagal = 2,
            Half = 3,
            Modal = 4,
        }

        [Header("Cadence Metadata")]
        [Tooltip("CADENCE-META. Hand-authored cadence class of this " +
                 "progression (metadata only — runtime composers ignore it; " +
                 "consuming games may gate replace/reskin logic on it). " +
                 "Leave None if unclassified.")]
        public CadenceType cadence = CadenceType.None;

        [Header("Tonality Filter")]
        [Tooltip("If empty, this progression can be used in any tonality. " +
             "Otherwise, it’s restricted to these modes.")]
        public List<Tonality> tonalities = new();

        [Header("Authoring")]
        [Tooltip("Original Roman progression string used to create this asset, " +
             "e.g. \"I – V – vi – IV\" or \"i (2) – iv (1) – v (1)\".")]
        [TextArea(1, 3)]
        public string originalInput;

        [Tooltip("Optional song references that use this progression or something close " +
                 "to it. Useful for designers/composers as listening references.")]
        public List<string> songReferences = new();

        public int TotalSteps(int beatsPerMeasure)
            => Mathf.Max(0, Measures) * Mathf.Max(1, beatsPerMeasure) * Mathf.Max(1, subdivisions);

        /// Return an "anchor" mask: true at each chord start
        public bool[] BuildAnchorMask(int beatsPerMeasure)
        {
            int total = TotalSteps(beatsPerMeasure);
            var mask = new bool[total];
            foreach (var e in events)
            {
                int s = Mathf.Clamp(e.startStep, 0, total - 1);
                mask[s] = true;
            }
            return mask;
        }

        /// Rebuild 'events' from an anchor mask and a parallel degree/quality list
        public void RebuildFromAnchors(bool[] anchors,
            IReadOnlyList<(ScaleDegree deg, ChordQuality q)> id, int defaultVelocity = 64)
        {
            events.Clear();
            if (anchors == null || anchors.Length == 0) return;

            int total = anchors.Length;

            // Collect start steps
            var starts = new List<int>();
            for (int i = 0; i < total; i++) if (anchors[i]) starts.Add(i);
            if (starts.Count == 0) return;

            for (int i = 0; i < starts.Count; i++)
            {
                int start = starts[i];
                int end = (i + 1 < starts.Count) ? starts[i + 1] : total;
                int length = Mathf.Max(1, end - start);

                var (deg, qual) = (i < id.Count) ? id[i] : (ScaleDegree.Tonic, ChordQuality.Major);
                events.Add(new ChordEvent
                {
                    startStep = start,
                    lengthSteps = length,
                    degree = deg,
                    quality = qual,
                    velocity = defaultVelocity,
                    degreeAccidental = 0
                });
            }
        }

        /// Finds the chord event active at an absolute tick within the part.
        /// Returns null if no events exist.
        public ChordEvent FindChordEventAt(
    TempoMap tempoMap,
    MusicTheory.MusicTheory.TimeSignature timeSignature,
    long absoluteTicks)
        {
            if (events == null || events.Count == 0)
                return null;

            var tsInfo = TimeSignatureProperties[timeSignature];
            int beatsPerMeasure = tsInfo.BeatsPerMeasure;

            int totalSteps = TotalSteps(beatsPerMeasure);
            if (totalSteps <= 0)
                return events[0];

            // ticks → beats → steps (beat-unit aware)
            long ticksPerBeat = TimeConverter.ConvertFrom(GetBeatSpan(timeSignature), tempoMap);
            if (ticksPerBeat <= 0) return events[0];

            double beats = absoluteTicks / (double)ticksPerBeat;
            int step = (int)System.Math.Floor(beats * System.Math.Max(1, subdivisions));

            // Wrap inside progression length for repeating progressions
            step %= totalSteps;
            if (step < 0) step += totalSteps;

            // Find event whose [start, start+length) covers 'step'
            // If gaps exist, fall back to the nearest preceding start.
            ChordEvent best = null;

            foreach (var e in events.OrderBy(ev => ev.startStep))
            {
                if (step < e.startStep)
                    break;

                // Covers region?
                if (step >= e.startStep && step < e.startStep + e.lengthSteps)
                    return e;

                best = e;
            }

            return best ?? events.OrderBy(ev => ev.startStep).Last();
        }

        /// <summary>
        /// Rebuilds DisplayName from the original input string and basic metadata.
        /// This is meant to be called from editor tools after modifying the asset.
        /// </summary>
        public void UpdateDisplayNameAuto()
        {
            // Base: original Roman string or asset name
            string baseLabel = string.IsNullOrWhiteSpace(originalInput)
                ? name
                : originalInput.Replace("\n", " ").Trim();

            string tsShort = TimeSignature.ToString(); // e.g. FourFour
            string tonalityShort = (tonalities != null && tonalities.Count > 0)
                ? string.Join("-", tonalities.Select(ShortCodeForTonality))
                : "Any";

            // Example: "I–V–vi–IV [4 bars, FourFour, sub x1, I-M]"
            DisplayName = $"{baseLabel} [{Measures} bars, {tsShort}, " +
                $"sub x{subdivisions}, {tonalityShort}]";
        }

        private static string ShortCodeForTonality(Tonality t) => t switch
        {
            Tonality.Ionian => "I",
            Tonality.Dorian => "D",
            Tonality.Phrygian => "Ph",
            Tonality.Lydian => "L",
            Tonality.Mixolydian => "M",
            Tonality.Aeolian => "Ae",
            Tonality.Locrian => "Lo",
            _ => t.ToString()
        };
    }
}
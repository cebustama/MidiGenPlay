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
        }

        [Tooltip("Events in step units (startStep/lengthSteps).")]
        public List<ChordEvent> events = new List<ChordEvent>();

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
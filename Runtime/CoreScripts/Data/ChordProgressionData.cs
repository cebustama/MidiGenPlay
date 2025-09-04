using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Chord Progression")]
    public class ChordProgressionData : PatternDataSO
    {
        public List<MusicTheory.Tonality> tonalities;

        [Range(1, 8)]
        // steps per beat (1=quarters, 2=eighths, 4=sixteenths, ...)
        public int subdivisions = 1; 

        // List of chords with their timing details
        [System.Serializable]
        public class ChordEvent
        {
            public int startStep;    // 0..(measures*beatsPerMeasure*subdivisions-1)
            public int lengthSteps;  // >= 1
            public ScaleDegree degree;
            // TODO define a MidiGenPlay enum
            public ChordQuality quality;    
            public int velocity;    // 0..127
        }

        public List<ChordEvent> events = new List<ChordEvent>();  // List of chord data

        public int TotalSteps(int beatsPerMeasure)
            => Mathf.Max(0, measures) * Mathf.Max(1, beatsPerMeasure) * Mathf.Max(1, subdivisions);

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
                    velocity = defaultVelocity
                });
            }
        }
    }
}
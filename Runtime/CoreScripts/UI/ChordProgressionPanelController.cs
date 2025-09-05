using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

// WIP

namespace MidiGenPlay.UI
{
    public class ChordProgressionPanelController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PatternGrid grid;
        [SerializeField] private ChordLabelOverlay labels;
        [SerializeField] private ChordEventPanel popup;

        [Header("Data")]
        [SerializeField] private ChordProgressionData progression;
        [SerializeField] private Tonality tonality = Tonality.Ionian;
        public void SetTonality(Tonality t) 
        {
            tonality = t;
            labels.SetTonality(t);
        } 

        // Working cache — mirrors progression.events for quick queries
        private readonly List<ChordProgressionData.ChordEvent> events = new();

        private int beatsPerMeasure => grid.BeatsPerMeasure;

        void Awake()
        {
            if (grid != null)
            {
                grid.OnCellClicked += HandleCellClicked;
                grid.OnCellToggled += HandleCellToggled; // keep toggles as anchors view
            }

            if (popup != null)
                popup.Confirmed += HandlePopupConfirmed;
        }

        public void Bind(ChordProgressionData data)
        {
            progression = data;
            events.Clear();
            if (progression != null && progression.events != null)
                events.AddRange(progression.events);

            // paint anchors according to current events
            PaintAnchorsFromEvents();
            labels?.Refresh(progression.events);
        }

        private void PaintAnchorsFromEvents()
        {
            // clear all
            for (int s = 0; s < grid.Steps; s++)
                grid.SetCell(0, s, false);

            foreach (var e in events)
            {
                if (e.startStep >= 0 && e.startStep < grid.Steps)
                    grid.SetCell(0, e.startStep, true);
            }

            labels?.Refresh(events);
        }

        private void HandleCellToggled(int row, int step, bool value)
        {
            if (row != 0) return;

            if (value)
            {
                if (FindEventCovering(step) < 0)
                {
                    int len = DefaultLenOneMeasureFrom(step);
                    InsertEvent(step, len, ScaleDegree.Tonic, ChordQuality.Major, 64);
                    popup?.Show(step, len, ScaleDegree.Tonic, ChordQuality.Major, 64, grid.Steps, tonality);
                }
            }
            else
            {
                int idx = FindEventStarting(step);
                if (idx >= 0)
                {
                    events.RemoveAt(idx);
                    ApplyEventsToAsset();
                    PaintAnchorsFromEvents();
                }
            }
        }

        private void HandleCellClicked(int row, int step)
        {
            if (row != 0) return;

            int idx = FindEventCovering(step);
            if (idx >= 0)
            {
                var e = events[idx];
                popup.Show(e.startStep, e.lengthSteps, e.degree, e.quality, e.velocity, grid.Steps, tonality);
            }
            else
            {
                int defaultLen = DefaultLenOneMeasureFrom(step);
                var defaultDegree = ScaleDegree.Tonic;
                var defaultQuality = GetSuggestedQuality(tonality, defaultDegree, preferSeventh: false);

                popup.Show(step, defaultLen, defaultDegree, defaultQuality, 64, grid.Steps, tonality);
            }
        }

        // Helper: remainder of current measure, at least 1, at most full measure.
        // If you want “always exactly one bar”, return stepsPerMeasure instead.
        private int DefaultLenOneMeasureFrom(int step)
        {
            int stepsPerMeasure = grid.BeatsPerMeasure * grid.Subdivisions;
            int currentBarStart = (step / stepsPerMeasure) * stepsPerMeasure;
            int currentBarEnd = currentBarStart + stepsPerMeasure;
            int remaining = Mathf.Clamp(currentBarEnd - step, 1, stepsPerMeasure);
            return remaining;
        }

        private void HandlePopupConfirmed(int start, int length, ScaleDegree deg, ChordQuality qual, int vel)
        {
            Debug.Log("<color=cyan>CHORD EVENT CONFIRMED</color>");

            // Enforce bounds
            start = Mathf.Clamp(start, 0, grid.Steps - 1);
            length = Mathf.Max(1, Mathf.Min(length, grid.Steps - start));

            // Remove any event that overlaps the [start, start+length)
            RemoveOverlaps(start, start + length);

            // If an event already starts here, update it; otherwise insert new
            int idx = FindEventStarting(start);
            if (idx >= 0)
            {
                var e = events[idx];
                e.lengthSteps = length;
                e.degree = deg;
                e.quality = qual;
                e.velocity = vel;
                events[idx] = e;
            }
            else
            {
                InsertEvent(start, length, deg, qual, vel);
            }

            ApplyEventsToAsset();
            PaintAnchorsFromEvents();
        }

        // ---- helpers ----

        private int FindEventCovering(int step)
        {
            for (int i = 0; i < events.Count; i++)
            {
                int s = events[i].startStep;
                int e = s + events[i].lengthSteps;
                if (step >= s && step < e) return i;
            }
            return -1;
        }

        private int FindEventStarting(int step)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i].startStep == step) return i;
            return -1;
        }

        private void InsertEvent(int start, int length, ScaleDegree deg, ChordQuality qual, int vel)
        {
            var ce = new ChordProgressionData.ChordEvent
            {
                startStep = start,
                lengthSteps = length,
                degree = deg,
                quality = qual,
                velocity = Mathf.Clamp(vel, 0, 127)
            };
            events.Add(ce);
            events.Sort((a, b) => a.startStep.CompareTo(b.startStep));
        }

        private void RemoveOverlaps(int start, int endExclusive)
        {
            // Remove any completely covered or partially overlapping events
            for (int i = events.Count - 1; i >= 0; i--)
            {
                int s = events[i].startStep;
                int e = s + events[i].lengthSteps;
                if (e > start && s < endExclusive)
                    events.RemoveAt(i);
            }
        }

        private int NextFreeRun(int fromStep)
        {
            // length to next anchor or end
            int next = grid.Steps;
            foreach (var ev in events)
            {
                if (ev.startStep > fromStep)
                {
                    next = ev.startStep;
                    break;
                }
            }
            return Mathf.Max(1, next - fromStep);
        }

        private void ApplyEventsToAsset()
        {
            // Always redraw UI from the working list
            labels?.Refresh(events);

            if (progression == null) return;

            progression.events.Clear();
            progression.events.AddRange(events);
            progression.subdivisions = grid.Subdivisions;
            progression.measures = grid.Measures;
        }

    }
}
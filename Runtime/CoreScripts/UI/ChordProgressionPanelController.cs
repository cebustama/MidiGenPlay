using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using UnityEngine;

// WIP

namespace MidiGenPlay.UI
{
    public class ChordProgressionPanelController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PatternGrid grid;
        [SerializeField] private ChordEventPanel popup;

        [Header("Data")]
        [SerializeField] private ChordProgressionData progression;

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
        }

        private void HandleCellToggled(int row, int step, bool value)
        {
            // Optional: if a user toggles anchors directly without popup.
            if (row != 0) return;

            if (value)
            {
                // Add a 1-step default event at this position if no event covers it
                if (FindEventCovering(step) < 0)
                {
                    InsertEvent(step, 1, ScaleDegree.Tonic, ChordQuality.Major, 64);
                    popup?.Show(step, 1, ScaleDegree.Tonic, ChordQuality.Major, 64, grid.Steps);
                }
                else
                {
                    // clicking an existing anchor will be handled via cell click to edit
                }
            }
            else
            {
                // If they turn off an anchor, remove that event (and extend previous if needed? Keep simple: just remove.)
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
                popup.Show(e.startStep, e.lengthSteps, e.degree, e.quality, e.velocity, grid.Steps);
            }
            else
            {
                // Create a new default event from here to the next anchor or bar end
                int defaultLen = NextFreeRun(step);
                popup.Show(step, defaultLen, ScaleDegree.Tonic, ChordQuality.Major, 64, grid.Steps);
            }
        }

        private void HandlePopupConfirmed(int start, int length, ScaleDegree deg, ChordQuality qual, int vel)
        {
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
            if (progression == null) return;
            progression.events.Clear();
            progression.events.AddRange(events);
            progression.subdivisions = grid.Subdivisions;
            progression.measures = grid.Measures;
        }
    }
}
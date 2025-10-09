using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Authoring asset for drum patterns.
    /// - Multi-lane grid: one lane per GeneralMidiPercussion instrument.
    /// - Steps are booleans (on/off) sized by measures * beatsPerMeasure * subdivisions.
    /// - Designed for runtime-first editing: bind a deep clone to the panel, save later.
    /// </summary>
    [CreateAssetMenu(menuName = "MidiGenPlay/Drum Pattern")]
    public class DrumPatternData : PatternDataSO
    {
        // -----------------------------
        // Grid / Signature
        // -----------------------------
        [Min(1)] public int beatsPerMeasure = 4;
        [Min(1)] public int subdivisions = 1; // steps per beat (1=quarter grid in 4/4 if tempo grid uses beats)

        public int StepsPerMeasure => Mathf.Max(1, beatsPerMeasure * subdivisions);
        public int TotalSteps => Mathf.Max(1, measures * StepsPerMeasure);

        // -----------------------------
        // Lanes
        // -----------------------------
        [Serializable]
        public class Lane
        {
            public GeneralMidiPercussion instrument = GeneralMidiPercussion.ClosedHiHat;
            [Range(1, 127)] public int defaultVelocity = 100;

            /// <summary>Boolean on/off steps (length == TotalSteps).</summary>
            public List<bool> steps = new List<bool>();
        }

        public List<Lane> lanes = new List<Lane>();

        // -----------------------------
        // Runtime-friendly helpers
        // -----------------------------

        /// <summary>
        /// Ensure lane step arrays match TotalSteps; create at least one lane.
        /// Call after any signature change.
        /// </summary>
        public void EnsureSizes()
        {
            if (lanes == null) lanes = new List<Lane>();
            if (lanes.Count == 0) lanes.Add(new Lane());

            int steps = TotalSteps;
            foreach (var l in lanes)
            {
                if (l.steps == null) l.steps = new List<bool>(steps);
                // grow
                while (l.steps.Count < steps) l.steps.Add(false);
                // shrink
                if (l.steps.Count > steps) l.steps.RemoveRange(steps, l.steps.Count - steps);
                l.defaultVelocity = Mathf.Clamp(l.defaultVelocity, 1, 127);
            }
        }

        /// <summary>Create one default lane if there are none.</summary>
        public void InitializeIfEmpty()
        {
            if (lanes == null) lanes = new List<Lane>();
            if (lanes.Count == 0) lanes.Add(new Lane());
            EnsureSizes();
        }

        /// <summary>Set signature and resize all lanes safely.</summary>
        public void SetSignature(int beatsPerMeasure, int measures, int subdivisions = 1)
        {
            this.beatsPerMeasure = Mathf.Max(1, beatsPerMeasure);
            this.measures = Mathf.Max(1, measures);
            this.subdivisions = Mathf.Max(1, subdivisions);
            EnsureSizes();
        }

        /// <summary>Clear all hits in all lanes.</summary>
        public void ClearAll()
        {
            foreach (var l in lanes)
                for (int i = 0; i < l.steps.Count; i++) l.steps[i] = false;
        }

        /// <summary>Return active step indices for a lane.</summary>
        public List<int> GetActiveSteps(int laneIndex)
        {
            var result = new List<int>();
            if (laneIndex < 0 || laneIndex >= (lanes?.Count ?? 0)) return result;
            var l = lanes[laneIndex];
            for (int s = 0; s < l.steps.Count; s++) if (l.steps[s]) result.Add(s);
            return result;
        }

        /// <summary>
        /// Compact snapshot for generation: (instrument, velocity, stepIndices[]) per lane.
        /// </summary>
        public (GeneralMidiPercussion instrument, int velocity, List<int> stepIndices)[] SnapshotAsIndices()
        {
            var list = new List<(GeneralMidiPercussion, int, List<int>)>(lanes.Count);
            for (int i = 0; i < lanes.Count; i++)
            {
                var l = lanes[i];
                list.Add((l.instrument, l.defaultVelocity, GetActiveSteps(i)));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deep clone to a new ScriptableObject for runtime editing (UI binds to this).
        /// </summary>
        public DrumPatternData DeepCloneRuntime()
        {
            var clone = CreateInstance<DrumPatternData>();
            clone.name = name + " (Runtime)";

            clone.measures = measures;
            clone.beatsPerMeasure = beatsPerMeasure;
            clone.subdivisions = subdivisions;

            clone.lanes = new List<Lane>(lanes.Count);
            foreach (var l in lanes)
            {
                var nl = new Lane
                {
                    instrument = l.instrument,
                    defaultVelocity = l.defaultVelocity,
                    steps = new List<bool>(l.steps) // deep copy
                };
                clone.lanes.Add(nl);
            }

            clone.EnsureSizes();
            return clone;
        }

        // -----------------------------
        // Legacy (optional) — kept for backward compatibility with
        // your older PianoRoll-based assets. Not used by the new grid.
        // -----------------------------
        [Header("Legacy (PianoRoll-based) — optional / ignored by new grid")]
        public MusicalTimeSpan noteLength = MusicalTimeSpan.Eighth; // Default note duration
        public int velocity = 80;      // Default MIDI velocity

        [System.Serializable]
        public class DrumMapping
        {
            public string drumSymbol;   // Symbol used in PianoRoll notation (e.g., 'x' for HiHat)
            public GeneralMidiPercussion drumNote; // Mapped percussion instrument
        }

        public List<DrumMapping> drumMappings = new List<DrumMapping>();

        [TextArea(5, 10)]
        public string pianoRollPattern; // The full PianoRoll text pattern
    }
}
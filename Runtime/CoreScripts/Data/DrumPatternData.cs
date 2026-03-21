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
    /// - Steps are StepState (active + velocity) sized by measures * beatsPerMeasure * subdivisions.
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
        public int TotalSteps => Mathf.Max(1, Measures * StepsPerMeasure);

        // -----------------------------
        // Step model
        // -----------------------------

        /// <summary>
        /// Per-step state. Replaces the legacy List&lt;bool&gt; model.
        /// velocity == 0 is the sentinel meaning "defer to lane defaultVelocity".
        /// velocity 1–127 is an explicit per-step override.
        /// </summary>
        [Serializable]
        public struct StepState
        {
            public bool active;

            /// <summary>
            /// 0 = defer to lane defaultVelocity.
            /// 1–127 = explicit per-step velocity override.
            /// </summary>
            [Range(0, 127)] public int velocity;

            public static StepState Off => new StepState { active = false, velocity = 0 };
            public static StepState On(int vel = 0) =>
                new StepState { active = true, velocity = Mathf.Clamp(vel, 0, 127) };

            /// <summary>
            /// Resolve effective velocity given the lane default.
            /// Returns the per-step velocity if non-zero, otherwise the lane default.
            /// </summary>
            public int ResolveVelocity(int laneDefault) =>
                velocity > 0 ? velocity : Mathf.Clamp(laneDefault, 1, 127);
        }

        // -----------------------------
        // Lanes
        // -----------------------------
        [Serializable]
        public class Lane
        {
            public GeneralMidiPercussion instrument = GeneralMidiPercussion.ClosedHiHat;
            [Range(1, 127)] public int defaultVelocity = 100;

            /// <summary>Per-step state (length == TotalSteps).</summary>
            public List<StepState> steps = new List<StepState>();
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

            int total = TotalSteps;
            foreach (var l in lanes)
            {
                if (l.steps == null) l.steps = new List<StepState>(total);
                // grow
                while (l.steps.Count < total) l.steps.Add(StepState.Off);
                // shrink
                if (l.steps.Count > total) l.steps.RemoveRange(total, l.steps.Count - total);
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
            this.Measures = Mathf.Max(1, measures);
            this.subdivisions = Mathf.Max(1, subdivisions);
            EnsureSizes();
        }

        /// <summary>Clear all hits in all lanes (preserves velocity values, sets active = false).</summary>
        public void ClearAll()
        {
            foreach (var l in lanes)
                for (int i = 0; i < l.steps.Count; i++)
                    l.steps[i] = StepState.Off;
        }

        /// <summary>Return active step indices for a lane.</summary>
        public List<int> GetActiveSteps(int laneIndex)
        {
            var result = new List<int>();
            if (laneIndex < 0 || laneIndex >= (lanes?.Count ?? 0)) return result;
            var l = lanes[laneIndex];
            for (int s = 0; s < l.steps.Count; s++)
                if (l.steps[s].active) result.Add(s);
            return result;
        }

        /// <summary>
        /// Compact snapshot for generation: (instrument, velocity, stepIndices[]) per lane.
        /// velocity is the lane defaultVelocity — used by existing runtime callers.
        /// Per-step velocity is resolved in SnapshotAsStepVelocities.
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
        /// Per-step velocity snapshot for generation: (instrument, steps[(index, resolvedVelocity)]) per lane.
        /// Resolved velocity = per-step override if non-zero, else lane defaultVelocity.
        /// Use this when runtime needs per-step velocity fidelity.
        /// </summary>
        public (GeneralMidiPercussion instrument, List<(int stepIndex, int velocity)> steps)[]
            SnapshotAsStepVelocities()
        {
            var list =
                new List<(GeneralMidiPercussion, List<(int, int)>)>(lanes.Count);

            for (int i = 0; i < lanes.Count; i++)
            {
                var l = lanes[i];
                var hits = new List<(int, int)>(l.steps.Count);
                for (int s = 0; s < l.steps.Count; s++)
                {
                    var step = l.steps[s];
                    if (step.active)
                        hits.Add((s, step.ResolveVelocity(l.defaultVelocity)));
                }
                list.Add((l.instrument, hits));
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

            clone.Measures = Measures;
            clone.beatsPerMeasure = beatsPerMeasure;
            clone.subdivisions = subdivisions;
            clone.TimeSignature = TimeSignature;

            clone.lanes = new List<Lane>(lanes.Count);
            foreach (var l in lanes)
            {
                var nl = new Lane
                {
                    instrument = l.instrument,
                    defaultVelocity = l.defaultVelocity,
                    steps = new List<StepState>(l.steps) // deep copy (struct list)
                };
                clone.lanes.Add(nl);
            }

            clone.EnsureSizes();
            return clone;
        }

        // -----------------------------
        // Legacy (optional) — kept for backward compatibility with
        // older PianoRoll-based assets. Not used by the new grid.
        // -----------------------------
        [Header("Legacy (PianoRoll-based) — optional / ignored by new grid")]
        public MusicalTimeSpan noteLength = MusicalTimeSpan.Eighth;
        public int velocity = 80;

        [System.Serializable]
        public class DrumMapping
        {
            public string drumSymbol;
            public GeneralMidiPercussion drumNote;
        }

        public List<DrumMapping> drumMappings = new List<DrumMapping>();

        [TextArea(5, 10)]
        public string pianoRollPattern;
    }
}

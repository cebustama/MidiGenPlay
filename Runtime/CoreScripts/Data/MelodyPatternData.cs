using System;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    /// <summary>
    /// Authoring asset for melody patterns (Roadmap_Melody_Authoring_MVP, Phase 1).
    ///
    /// Canonical, DETERMINISTIC per-note model: each note is a single diatonic
    /// <see cref="ScaleDegree"/> + octave offset + beat-relative timing + velocity.
    /// Absolute MIDI pitch is NOT stored; degree -> pitch is resolved at runtime
    /// (Phase 4 ComposeFromPattern) against the active Part tonality / root.
    ///
    /// Replaces the legacy probabilistic model (the old MelodyNoteData with
    /// List&lt;ScaleDegree&gt; possibleDegrees, which was resolved by a random degree +
    /// random octave draw at generation). That model's only consumer,
    /// MidiGenerator.GenerateMelodyTrackWithPattern, was removed in the same change
    /// (decision M-3); MelodyPatternsList is shape-agnostic and is unaffected.
    /// Probabilistic / weighted note events are deferred to Phase D2.
    ///
    /// Mirrors the DrumPatternData authoring conventions: inherits PatternDataSO
    /// (DisplayName / TimeSignature / Measures), carries an explicit beatsPerMeasure
    /// + an editor-grid subdivisions resolution, and supports deep-clone-for-runtime
    /// editing (bind a clone to the wizard; persist on Apply/Save).
    /// </summary>
    [CreateAssetMenu(menuName = "MidiGenPlay/Melody Pattern")]
    public class MelodyPatternData : PatternDataSO
    {
        // -----------------------------
        // Grid / Signature
        // -----------------------------
        [Min(1)] public int beatsPerMeasure = 4;

        /// <summary>
        /// Editor grid resolution (steps per beat). Note timing is stored in beats
        /// (startBeat / durationBeats); subdivisions only quantizes the ladder UI.
        /// </summary>
        [Min(1)] public int subdivisions = 4;

        public int StepsPerMeasure => Mathf.Max(1, beatsPerMeasure * subdivisions);
        public int TotalSteps => Mathf.Max(1, Measures * StepsPerMeasure);
        public float TotalBeats => Mathf.Max(1f, Measures * beatsPerMeasure);

        // -----------------------------
        // Note model
        // -----------------------------

        /// <summary>
        /// One deterministic melody note. Pitch is a diatonic degree + octave offset,
        /// resolved to absolute MIDI at runtime against the active tonality / root.
        /// </summary>
        [Serializable]
        public struct MelodyNoteEvent
        {
            [Tooltip("Diatonic scale degree (I..VII). Resolved to pitch at runtime.")]
            public ScaleDegree degree;

            [Tooltip("Octave offset from the pattern reference octave (0 = reference, +1 up, -1 down).")]
            public int octaveOffset;

            [Tooltip("Start position in beats from pattern start.")]
            [Min(0f)] public float startBeat;

            [Tooltip("Note length in beats.")]
            [Min(0f)] public float durationBeats;

            [Tooltip("MIDI velocity 1..127.")]
            [Range(1, 127)] public int velocity;

            public static MelodyNoteEvent Create(
                ScaleDegree degree, float startBeat, float durationBeats,
                int octaveOffset = 0, int velocity = 100) =>
                new MelodyNoteEvent
                {
                    degree = degree,
                    octaveOffset = octaveOffset,
                    startBeat = Mathf.Max(0f, startBeat),
                    durationBeats = Mathf.Max(0f, durationBeats),
                    velocity = Mathf.Clamp(velocity, 1, 127)
                };
        }

        /// <summary>The authored note sequence. Sparse — only sounding notes are stored.</summary>
        public List<MelodyNoteEvent> notes = new List<MelodyNoteEvent>();

        // -----------------------------
        // Lifecycle helpers
        // -----------------------------

        /// <summary>Set signature / length. Notes are preserved (no resampling here).</summary>
        public void SetSignature(int beatsPerMeasure, int measures, int subdivisions = 4)
        {
            this.beatsPerMeasure = Mathf.Max(1, beatsPerMeasure);
            this.Measures = Mathf.Max(1, measures);
            this.subdivisions = Mathf.Max(1, subdivisions);
        }

        /// <summary>Remove all notes (keeps signature).</summary>
        public void ClearAll() => notes?.Clear();

        /// <summary>Ensure the notes list is non-null.</summary>
        public void InitializeIfEmpty()
        {
            if (notes == null) notes = new List<MelodyNoteEvent>();
        }

        /// <summary>
        /// Notes ordered by start time, then degree, then octave — a deterministic
        /// read order for rendering. Does not mutate the stored list.
        /// </summary>
        public List<MelodyNoteEvent> SnapshotOrdered()
        {
            var copy = new List<MelodyNoteEvent>(notes ?? new List<MelodyNoteEvent>());
            copy.Sort((a, b) =>
            {
                int c = a.startBeat.CompareTo(b.startBeat);
                if (c != 0) return c;
                c = ((int)a.degree).CompareTo((int)b.degree);
                if (c != 0) return c;
                return a.octaveOffset.CompareTo(b.octaveOffset);
            });
            return copy;
        }

        /// <summary>Deep clone to a new ScriptableObject for runtime editing (UI binds to this).</summary>
        public MelodyPatternData DeepCloneRuntime()
        {
            var clone = CreateInstance<MelodyPatternData>();
            clone.name = name + " (Runtime)";

            clone.DisplayName = DisplayName;
            clone.TimeSignature = TimeSignature;
            clone.Measures = Measures;
            clone.beatsPerMeasure = beatsPerMeasure;
            clone.subdivisions = subdivisions;

            clone.notes = new List<MelodyNoteEvent>(notes ?? new List<MelodyNoteEvent>());
            return clone;
        }
    }
}
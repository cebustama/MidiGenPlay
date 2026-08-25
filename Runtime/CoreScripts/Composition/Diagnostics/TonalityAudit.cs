using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition.Diagnostics
{
    /// <summary>
    /// MGP-TONALITY-1 Task 2 — log-only tonal audit at note emission.
    ///
    /// One call per EMITTED note (melody slot, bass segment note, backing
    /// voicing note). Classifies the note's pitch class against the part's
    /// scale and the current chord event's pitch classes:
    ///
    /// - <see cref="Verdict.InScale"/> — diatonic; silent.
    /// - <see cref="Verdict.ChordToneChromatic"/> — NOT in the scale but a
    ///   member of the sounding chord (borrowed/altered quality). Legitimate
    ///   harmony, not a melody bug (D-TON1=A): counted always, logged only
    ///   when the caller's generator logging is on.
    /// - <see cref="Verdict.OutOfScaleAndChord"/> — in neither set. The red
    ///   warning the batch asks for: logged unconditionally while
    ///   <see cref="Enabled"/> (D-TON3=A), with track, bar, beat, note,
    ///   expected tonality, origin and source (phrase archetype / pattern /
    ///   figure).
    ///
    /// CONTRACT (diagnostic batch constraint): this class is pure
    /// observation. It draws no RNG, mutates no composer state, and its
    /// presence or toggles can never change generated output. Runtime-safe:
    /// no editor-only APIs. The rhythm track is exempt by design (D-TON2=A:
    /// percussion carries no tonal content).
    ///
    /// Counters exist for the Task 3 matrix runner: reset per cell, snapshot
    /// after generation. Keys: "{track}|{verdict}" and, for violations only,
    /// "{track}|{verdict}|{origin}".
    /// </summary>
    public static class TonalityAudit
    {
        public enum Verdict
        {
            InScale = 0,
            ChordToneChromatic = 1,
            OutOfScaleAndChord = 2
        }

        /// <summary>Master switch. False = hard no-op (no logs, no counters),
        /// so shipping code can disable the audit wholesale.</summary>
        public static bool Enabled = true;

        /// <summary>Matrix-runner switch: keep counting but silence ALL logs
        /// (a full cartesian sweep would otherwise flood the console).</summary>
        public static bool SuppressLogs = false;

        private static readonly Dictionary<string, int> _counters
            = new Dictionary<string, int>();

        /// <summary>Total red-tier violations since the last reset (all tracks).</summary>
        public static int RedCount { get; private set; }

        public static void ResetCounters()
        {
            _counters.Clear();
            RedCount = 0;
        }

        /// <summary>Copy of the counters (safe for the matrix runner to store per cell).</summary>
        public static Dictionary<string, int> SnapshotCounters()
            => new Dictionary<string, int>(_counters);

        /// <summary>
        /// Audit one emitted note. Returns the verdict (callers ignore it today;
        /// the matrix runner reads counters instead).
        /// </summary>
        /// <param name="track">"Melody" / "Bass" / "Backing".</param>
        /// <param name="note">The note about to be emitted (null = no-op).</param>
        /// <param name="scalePcs">The part's 7 diatonic pitch classes
        /// (degree order, index 0 = tonic).</param>
        /// <param name="chordPcs">Pitch classes of the sounding chord event, AS
        /// THE CALLING COMPOSER COMPUTES THEM (melody/bass are accidental-blind
        /// today, backing is accidental-aware — F-TON-ACC-1; the audit measures
        /// what each composer believes, it does not correct it). Null when no
        /// harmonic context exists (e.g. authored melody patterns).</param>
        /// <param name="whenBeats">Onset in part beats (beat unit = GetBeatSpan).</param>
        /// <param name="beatsPerBar">Part beats per bar.</param>
        /// <param name="tonality">Part tonality (for the message).</param>
        /// <param name="root">Part root (for the message).</param>
        /// <param name="originTag">Which mechanism produced the pitch:
        /// "strategy", "strategy+contour", "strategy+motif", "interval-directive",
        /// "authored-pattern", "bass-segment", "backing-grid",
        /// "backing-progression".</param>
        /// <param name="sourceDetail">Phrase archetype / pattern asset /
        /// articulation figure name (may be null).</param>
        /// <param name="infoLoggingEnabled">Caller's logGenerator flag; gates
        /// the yellow tier only. The red tier ignores it (D-TON3=A).</param>
        public static Verdict Check(
            string track,
            DryWetMidiNote note,
            NoteName[] scalePcs,
            NoteName[] chordPcs,
            double whenBeats,
            int beatsPerBar,
            Tonality tonality,
            NoteName root,
            string originTag,
            string sourceDetail,
            bool infoLoggingEnabled)
        {
            if (!Enabled || note == null || scalePcs == null || scalePcs.Length == 0)
                return Verdict.InScale;

            var pc = note.NoteName;

            bool inScale = IndexOf(scalePcs, pc) >= 0;
            if (inScale)
            {
                Count($"{track}|InScale");
                return Verdict.InScale;
            }

            bool inChord = chordPcs != null && IndexOf(chordPcs, pc) >= 0;
            int bar = beatsPerBar > 0
                ? (int)Math.Floor(whenBeats / beatsPerBar) : 0;
            double beatInBar = beatsPerBar > 0
                ? whenBeats - bar * (double)beatsPerBar : whenBeats;

            if (inChord)
            {
                Count($"{track}|ChordToneChromatic");
                Count($"{track}|ChordToneChromatic|{originTag}");

                if (!SuppressLogs && infoLoggingEnabled)
                {
                    Debug.Log(
                        $"<color=yellow>[TonalityAudit] CHORD-TONE-CHROMATIC | " +
                        $"track={track} bar={bar} beat={beatInBar:0.##} " +
                        $"note={note} tonality={tonality}({root}) " +
                        $"scale=[{Join(scalePcs)}] chord=[{Join(chordPcs)}] " +
                        $"origin={originTag} source={sourceDetail ?? "-"}" +
                        $"</color>");
                }
                return Verdict.ChordToneChromatic;
            }

            // Red tier — the warning MGP-TONALITY-1 Task 2 asks for.
            Count($"{track}|OutOfScaleAndChord");
            Count($"{track}|OutOfScaleAndChord|{originTag}");
            RedCount++;

            if (!SuppressLogs)
            {
                Debug.LogWarning(
                    $"<color=red>[TonalityAudit] OUT-OF-KEY | " +
                    $"track={track} bar={bar} beat={beatInBar:0.##} " +
                    $"note={note} tonality={tonality}({root}) " +
                    $"scale=[{Join(scalePcs)}] " +
                    $"chord=[{(chordPcs != null ? Join(chordPcs) : "-")}] " +
                    $"origin={originTag} source={sourceDetail ?? "-"}" +
                    $"</color>");
            }
            return Verdict.OutOfScaleAndChord;
        }

        // Array.IndexOf without LINQ allocation, per-note hot path.
        private static int IndexOf(NoteName[] arr, NoteName pc)
        {
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == pc) return i;
            return -1;
        }

        private static string Join(NoteName[] arr)
            => string.Join(" ", arr.Select(n => n.ToString()));

        private static void Count(string key)
        {
            _counters.TryGetValue(key, out var v);
            _counters[key] = v + 1;
        }
    }
}
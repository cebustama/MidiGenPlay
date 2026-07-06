using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Tier-1 chord articulator (CA-T1). Stateless, RNG-free, deterministic.
    ///
    /// Structure: <see cref="PlanHits"/> is the pure planning seam — it maps
    /// (expression, event window, meter, base velocity) to a list of
    /// <see cref="Hit"/> values with no DryWetMIDI emission involved, and is the
    /// unit-test surface (internal, via Runtime/AssemblyInfo InternalsVisibleTo).
    /// <see cref="Emit"/> is a thin translator from hits to PatternBuilder calls.
    ///
    /// Invariants enforced here:
    /// - Block plan/emission is exactly the legacy pair: one chord hit at the
    ///   event onset, full event duration, velocity Clamp(base, 0, 127).
    /// - All non-Block hit velocities are Clamp(round(base * factor), 1, 127)
    ///   (min 1: velocity-0 note-on is note-off semantics).
    /// - Accent curve is a pure function of absolute beat position within the
    ///   Part meter: downbeat ×1.00, other on-beat ×0.85, off-beat ×0.80.
    /// - Never silent: figures that cannot fit the event degrade to the Block
    ///   plan for that event.
    /// - No hit ever overshoots the event window [startBeats, startBeats+durBeats).
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md §8.
    /// </summary>
    public sealed class ChordArticulator : IChordArticulator
    {
        /// <summary>One planned articulation hit, in beats (Part meter).</summary>
        public readonly struct Hit
        {
            /// <summary>Absolute onset in beats from part start.</summary>
            public readonly double StartBeats;
            /// <summary>Hit length in beats; never overshoots the event end.</summary>
            public readonly double DurBeats;
            /// <summary>Final MIDI velocity (already curved and clamped).</summary>
            public readonly int Velocity;
            /// <summary>-1 = full chord (voicer order); otherwise an index into
            /// the direction-sorted voicing (arpeggio figures).</summary>
            public readonly int NoteIndex;

            public Hit(double startBeats, double durBeats, int velocity, int noteIndex)
            {
                StartBeats = startBeats;
                DurBeats = durBeats;
                Velocity = velocity;
                NoteIndex = noteIndex;
            }
        }

        // Position comparisons: onsets are rationals (step / stepsPerBeat) produced
        // by a single division, so meter-grid positions land exactly; the epsilon
        // only guards accumulated Multiply/Ceiling edge noise. Pure => deterministic.
        private const double Eps = 1e-6;

        // Figure constants (beats).
        internal const double StaccatoDurBeats = 0.5;
        internal const double OffbeatDurBeats = 0.5;

        // SD-5=A velocity curve factors.
        internal const double AccentDownbeat = 1.00;
        internal const double AccentOnBeat = 0.85;
        internal const double AccentOffBeat = 0.80;

        public void Emit(
            PatternBuilder pb,
            IReadOnlyList<Note> playable,
            double startBeats,
            double durBeats,
            MusicalTimeSpan beatSpan,
            int beatsPerBar,
            int baseVelocity,
            int stepsPerBeat,
            ChordExpressionType expression,
            ArpeggioRate arpeggioRate)
        {
            int noteCount = playable != null ? playable.Count : 0;
            var hits = PlanHits(expression, arpeggioRate, startBeats, durBeats,
                                beatsPerBar, noteCount, baseVelocity);

            // Arpeggio hits index into a pitch-sorted copy; chord hits (including
            // Block and any degraded event) always use the voicer's order verbatim.
            IReadOnlyList<Note> sorted = null;
            if (noteCount > 0 &&
                (expression == ChordExpressionType.ArpeggioUp ||
                 expression == ChordExpressionType.ArpeggioDown))
            {
                var s = playable.OrderBy(n => n.NoteNumber).ToList(); // stable
                if (expression == ChordExpressionType.ArpeggioDown) s.Reverse();
                sorted = s;
            }

            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                pb.MoveToTime(beatSpan.Multiply(h.StartBeats));
                var length = beatSpan.Multiply(h.DurBeats);
                var vel = (SevenBitNumber)h.Velocity;

                if (h.NoteIndex < 0)
                    pb.Chord(playable, length, vel);
                else
                    pb.Note(sorted[h.NoteIndex], length, vel);
            }
        }

        /// <summary>
        /// Pure planning seam: maps one progression event to its articulation
        /// hits. No emission, no state, no RNG. Test surface for CA-T1.
        /// </summary>
        public static IReadOnlyList<Hit> PlanHits(
            ChordExpressionType expression,
            ArpeggioRate arpeggioRate,
            double startBeats,
            double durBeats,
            int beatsPerBar,
            int noteCount,
            int baseVelocity)
        {
            double end = startBeats + Math.Max(0.0, durBeats);

            switch (expression)
            {
                case ChordExpressionType.PerBeat:
                    return OnBeatPlan(startBeats, durBeats, end, beatsPerBar,
                                      baseVelocity, staccato: false);

                case ChordExpressionType.Staccato:
                    return OnBeatPlan(startBeats, durBeats, end, beatsPerBar,
                                      baseVelocity, staccato: true);

                case ChordExpressionType.Offbeat:
                    return OffbeatPlan(startBeats, durBeats, end, beatsPerBar,
                                       baseVelocity);

                case ChordExpressionType.ArpeggioUp:
                case ChordExpressionType.ArpeggioDown:
                    return ArpeggioPlan(startBeats, durBeats, end, beatsPerBar,
                                        baseVelocity, noteCount, arpeggioRate);

                case ChordExpressionType.Block:
                default:
                    return BlockPlan(startBeats, durBeats, baseVelocity);
            }
        }

        /// <summary>Legacy emission: one full-length chord hit at the onset,
        /// velocity Clamp(base, 0, 127) — exactly the pre-CA-T1 pair.</summary>
        private static IReadOnlyList<Hit> BlockPlan(
            double startBeats, double durBeats, int baseVelocity)
        {
            return new[]
            {
                new Hit(startBeats, durBeats, Mathf.Clamp(baseVelocity, 0, 127), -1)
            };
        }

        /// <summary>
        /// PerBeat / Staccato: chord re-struck on every meter-anchored integer
        /// beat inside the event; if the event starts off the beat grid, an
        /// extra hit sounds at the onset (a chord change must always be heard
        /// at its onset). PerBeat is legato to the next hit / event end;
        /// Staccato caps each hit at <see cref="StaccatoDurBeats"/>.
        /// </summary>
        private static IReadOnlyList<Hit> OnBeatPlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity, bool staccato)
        {
            var positions = new List<double>();

            double firstBeat = Math.Ceiling(startBeats - Eps);
            if (firstBeat > startBeats + Eps)
                positions.Add(startBeats); // off-grid onset hit

            for (double p = firstBeat; p < end - Eps; p += 1.0)
                positions.Add(p);

            if (positions.Count == 0)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            var hits = new List<Hit>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                double pos = positions[i];
                double boundary = (i + 1 < positions.Count) ? positions[i + 1] : end;
                double dur = boundary - pos;
                if (staccato) dur = Math.Min(StaccatoDurBeats, dur);

                hits.Add(new Hit(pos, dur,
                    CurvedVelocity(pos, beatsPerBar, baseVelocity), -1));
            }
            return hits;
        }

        /// <summary>
        /// Offbeat (ska/reggae upstroke): short chord hits at every beat+0.5
        /// inside the event. The only figure that can plan zero hits, in which
        /// case it degrades to Block (never-silent invariant).
        /// </summary>
        private static IReadOnlyList<Hit> OffbeatPlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity)
        {
            double p = Math.Floor(startBeats + Eps) + 0.5;
            while (p < startBeats - Eps) p += 1.0;

            var hits = new List<Hit>();
            for (; p < end - Eps; p += 1.0)
            {
                double dur = Math.Min(OffbeatDurBeats, end - p);
                hits.Add(new Hit(p, dur,
                    CurvedVelocity(p, beatsPerBar, baseVelocity), -1));
            }

            if (hits.Count == 0)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            return hits;
        }

        /// <summary>
        /// ArpeggioUp/Down: single notes at a fixed meter-based rate, anchored
        /// at the event onset (an arpeggio begins when its chord begins),
        /// cycling through the direction-sorted voicing. Each note is legato to
        /// the next hit; the final note is truncated to the event end. Events
        /// shorter than one full hit degrade to Block.
        /// </summary>
        private static IReadOnlyList<Hit> ArpeggioPlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity, int noteCount, ArpeggioRate rate)
        {
            double interval = ArpeggioIntervalBeats(rate);

            if (noteCount <= 0 || durBeats < interval - Eps)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            var hits = new List<Hit>();
            for (int k = 0; ; k++)
            {
                double t = startBeats + k * interval;
                if (t >= end - Eps) break;

                double dur = Math.Min(interval, end - t);
                hits.Add(new Hit(t, dur,
                    CurvedVelocity(t, beatsPerBar, baseVelocity),
                    k % noteCount));
            }
            return hits;
        }

        /// <summary>One arpeggio hit length in beats for the given rate.</summary>
        public static double ArpeggioIntervalBeats(ArpeggioRate rate)
        {
            switch (rate)
            {
                case ArpeggioRate.PerBeat: return 1.0;
                case ArpeggioRate.Sixteenth: return 0.25;
                case ArpeggioRate.Eighth:
                default: return 0.5;
            }
        }

        /// <summary>
        /// SD-5=A velocity model: multiplicative accent curve over the authored
        /// per-event base velocity, as a pure function of absolute beat position
        /// within the Part meter. Downbeat ×1.00, other on-beat ×0.85,
        /// off-beat ×0.80; round away-from-zero; clamp 1..127.
        /// </summary>
        internal static int CurvedVelocity(double posBeats, int beatsPerBar, int baseVelocity)
        {
            double nearest = Math.Round(posBeats);
            bool onBeat = Math.Abs(posBeats - nearest) < Eps;

            double factor;
            if (onBeat)
            {
                long beatIndex = (long)nearest;
                bool downbeat = beatsPerBar > 0 && (beatIndex % beatsPerBar) == 0;
                factor = downbeat ? AccentDownbeat : AccentOnBeat;
            }
            else
            {
                factor = AccentOffBeat;
            }

            int v = (int)Math.Round(baseVelocity * factor, MidpointRounding.AwayFromZero);
            return Mathf.Clamp(v, 1, 127);
        }
    }
}
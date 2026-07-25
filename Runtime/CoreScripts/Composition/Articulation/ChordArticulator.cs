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
    /// Structure: <see cref="PlanHits"/> is the pure planning seam � it maps
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
    ///   Part meter: downbeat �1.00, other on-beat �0.85, off-beat �0.80.
    /// - Never silent: figures that cannot fit the event degrade to the Block
    ///   plan for that event.
    /// - No hit ever overshoots the event window [startBeats, startBeats+durBeats).
    /// - CA-V1: when a jitter with Amount > 0 is supplied, every planned hit's
    ///   velocity is offset by a pure (seed, event, hit) mix and clamped 1..127
    ///   — including Block, whose legacy 0..127 clamp applies only with jitter
    ///   off (the default). Timing, hit count and note indices are never
    ///   touched. Amount == 0 returns the planned list by reference: identity is
    ///   structural, not empirical.
    /// - CA-T2-BOSSA: Hit.NoteIndex selection vocabulary is -1 = full chord,
    ///   -2 = upper voices (strictly above the lowest pitch), &gt;= 0 = one note
    ///   of the direction-sorted voicing. Emission is exact-match on the
    ///   sentinels; an undefined negative degrades to the full chord. Pure
    ///   selection: no pitch value is ever created or altered here.
    /// - CA-T2-BOSSA-V2: the v1 register split is renamed BassUpperSplit
    ///   (OD-BOSSA-7=A; value 9 intact) and Bossa (= 10) is the authentic
    ///   1-bar comping template with TEMPLATE-supplied accent tiers
    ///   (D-FEEL-ACCENT=A — the surdo weight on beat 2, a documented
    ///   per-figure exception to the position-derived curve above). Both are
    ///   selection figures on the same closed NoteIndex vocabulary.
    ///
    /// See runtime/SSoT_Composer_Backing_Track.md �8.
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
            /// <summary>-1 = full chord (voicer order); -2 = the upper voices
            /// (all notes strictly above the voicing's lowest pitch, CA-T2-BOSSA
            /// — see <see cref="UpperVoicesIndex"/>); otherwise an index into
            /// the direction-sorted voicing (arpeggio figures; the
            /// register-selective figures BassUpperSplit and Bossa use index
            /// 0 of the ascending sort = the lowest note). Any other negative
            /// value is undefined and emits the full chord (never silent).</summary>
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

        /// <summary>CA-T2-BOSSA (D-BOSSA-SEL=A): Hit.NoteIndex sentinel for
        /// "all voicing notes strictly above the lowest pitch". -1 remains
        /// "full chord"; both are matched EXACTLY at emission.</summary>
        internal const int UpperVoicesIndex = -2;

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
            ArpeggioRate arpeggioRate,
            VelocityJitter jitter = default)
        {
            int noteCount = playable != null ? playable.Count : 0;
            var hits = PlanHits(expression, arpeggioRate, startBeats, durBeats,
                                beatsPerBar, noteCount, baseVelocity, jitter);

            // Arpeggio and register-selective (BassUpperSplit/Bossa) hits
            // index into a pitch-sorted copy; chord hits (including Block and
            // any degraded event) always use the voicer's order verbatim. The
            // register-selective figures sort ASCENDING (index 0 = lowest).
            IReadOnlyList<Note> sorted = null;
            if (noteCount > 0 &&
                (expression == ChordExpressionType.ArpeggioUp ||
                 expression == ChordExpressionType.ArpeggioDown ||
                 expression == ChordExpressionType.BassUpperSplit ||
                 expression == ChordExpressionType.Bossa))
            {
                var s = playable.OrderBy(n => n.NoteNumber).ToList(); // stable
                if (expression == ChordExpressionType.ArpeggioDown) s.Reverse();
                sorted = s;
            }

            // CA-T2-BOSSA (D-BOSSA-SEL=A / OD-BOSSA-2=A; shared by both
            // register-selective figures since CA-T2-BOSSA-V2): the -2 subset is
            // materialized ONCE per event as all notes STRICTLY above the
            // lowest pitch (pitch ties with the bass are excluded, so a
            // duplicated low pitch is never re-struck on the offbeat). A
            // degenerate voicing (every note the same pitch) falls back to the
            // full playable list — never silent. Pure selection over the given
            // notes; no pitch is created or altered.
            IReadOnlyList<Note> uppers = null;
            if (sorted != null &&
                (expression == ChordExpressionType.BassUpperSplit ||
                 expression == ChordExpressionType.Bossa))
            {
                int lowest = sorted[0].NoteNumber;
                var u = playable.Where(n => n.NoteNumber > lowest).ToList();
                uppers = u.Count > 0 ? (IReadOnlyList<Note>)u : playable;
            }

            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                pb.MoveToTime(beatSpan.Multiply(h.StartBeats));
                var length = beatSpan.Multiply(h.DurBeats);
                var vel = (SevenBitNumber)h.Velocity;

                // Exact-match sentinel translation (CA-T2-BOSSA): a -2 that
                // arrives without a Bossa uppers subset, or any undefined
                // negative, degrades to the full chord (never silent) instead
                // of being swallowed by a blanket `< 0` branch — the BASS-WALK-1
                // verification lesson applied at the seam itself.
                if (h.NoteIndex == UpperVoicesIndex && uppers != null)
                    pb.Chord(uppers, length, vel);
                else if (h.NoteIndex < 0)
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
            int baseVelocity,
            VelocityJitter jitter = default)
        {
            // CA-V1: the figure math is untouched; the jitter is a post-pass over
            // the planned hits, so no figure branch had to learn about it.
            return ApplyJitter(
                PlanCore(expression, arpeggioRate, startBeats, durBeats,
                         beatsPerBar, noteCount, baseVelocity),
                jitter);
        }

        /// <summary>Pre-CA-V1 planning body, verbatim: the figure switch and all
        /// degrade rules. Pure, RNG-free, jitter-unaware.</summary>
        private static IReadOnlyList<Hit> PlanCore(
            ChordExpressionType expression,
            ArpeggioRate arpeggioRate,
            double startBeats,
            double durBeats,
            int beatsPerBar,
            int noteCount,
            int baseVelocity)
        {
            // ARTIC-1 + CA-T2 defensive degrade. Random is a selection-policy
            // sentinel; PowerChord's rhythm is a plain sustain — neither is a
            // rhythm this switch renders, so a leak degrades to Block (never
            // silent, still RNG-free). Chugging IS rendered here (chord pulse);
            // its pitch reshape was applied upstream by IChordReshaper, so this
            // only re-strikes the given voicing (§8 pitch-preserving holds).
            if (expression == ChordExpressionType.Random ||
                expression == ChordExpressionType.PowerChord)
                expression = ChordExpressionType.Block;

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

                case ChordExpressionType.Chugging:
                    return ChordPulsePlan(startBeats, durBeats, end, beatsPerBar,
                                          baseVelocity, arpeggioRate);

                case ChordExpressionType.BassUpperSplit:
                    return BassUpperSplitPlan(startBeats, durBeats, end,
                                              beatsPerBar, baseVelocity,
                                              noteCount);

                case ChordExpressionType.Bossa:
                    return BossaTemplatePlan(startBeats, durBeats, end,
                                             beatsPerBar, baseVelocity,
                                             noteCount);

                case ChordExpressionType.Block:
                default:
                    return BlockPlan(startBeats, durBeats, baseVelocity);
            }
        }

        /// <summary>Legacy emission: one full-length chord hit at the onset,
        /// velocity Clamp(base, 0, 127) � exactly the pre-CA-T1 pair.</summary>
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

        /// <summary>
        /// CA-T2 Chugging: the full voicing (reshaped to a power chord upstream)
        /// re-struck at the arpeggio rate, anchored at the event onset. Every hit
        /// is a full chord (NoteIndex = -1) — pitch-preserving, so §8 holds. Same
        /// timing model as <see cref="ArpeggioPlan"/> without note cycling. Events
        /// shorter than one hit degrade to Block.
        /// </summary>
        private static IReadOnlyList<Hit> ChordPulsePlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity, ArpeggioRate rate)
        {
            double interval = ArpeggioIntervalBeats(rate);
            if (durBeats < interval - Eps)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            var hits = new List<Hit>();
            for (int k = 0; ; k++)
            {
                double t = startBeats + k * interval;
                if (t >= end - Eps) break;
                double dur = System.Math.Min(interval, end - t);
                hits.Add(new Hit(t, dur,
                    CurvedVelocity(t, beatsPerBar, baseVelocity), -1)); // -1 = full chord
            }
            return hits;
        }

        /// <summary>
        /// CA-T2-BOSSA (register-selective split, D-BOSSA-RHYTHM=A fixed
        /// template). Renamed BossaPlan → BassUpperSplitPlan by CA-T2-BOSSA-V2
        /// (OD-BOSSA-7=A): the regular alternation below is a register split,
        /// not the bossa comping rhythm — see <see cref="BossaTemplatePlan"/>
        /// for the authentic figure. Behavior UNCHANGED by the rename.
        ///
        /// - LOW role (Hit.NoteIndex = 0 into the ascending sort = lowest note):
        ///   struck at the event onset (a chord change must always be heard at
        ///   its onset — same principle as <see cref="OnBeatPlan"/>) and at
        ///   every bar downbeat strictly inside the event (OD-BOSSA-3=A), each
        ///   hit legato to the next low hit or the event end.
        /// - UPPER role (<see cref="UpperVoicesIndex"/> = -2, all notes strictly
        ///   above the lowest pitch): short hits on every offbeat (beat + 0.5)
        ///   inside the event — exactly <see cref="OffbeatPlan"/>'s grid and
        ///   <see cref="OffbeatDurBeats"/> length. arpeggioRate is ignored.
        ///
        /// Degrades to Block when the voicing is mono/empty (noteCount &lt;= 1:
        /// there is no register to split) or when no offbeat fits the event
        /// (OD-BOSSA-4=A: a bass-only sustain would be a drastic register
        /// change — F-WALK-REG; mirror of OffbeatPlan's empty-plan degrade).
        /// Hits are returned in chronological order (stable index → jitter
        /// mapping under CA-V1's per-hit indexing); at an onset that lands on
        /// an offbeat both roles strike, low first (deterministic tie-break —
        /// the full chord is heard at the change). Pure, RNG-free, stateless.
        /// </summary>
        private static IReadOnlyList<Hit> BassUpperSplitPlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity, int noteCount)
        {
            if (noteCount <= 1)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade: mono

            // Upper-voice offbeats: OffbeatPlan's grid, verbatim.
            double p = Math.Floor(startBeats + Eps) + 0.5;
            while (p < startBeats - Eps) p += 1.0;

            var upperStarts = new List<double>();
            for (; p < end - Eps; p += 1.0)
                upperStarts.Add(p);

            if (upperStarts.Count == 0)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade: no split

            // Low-note grid: onset + bar downbeats strictly inside the event.
            // Bar downbeats are integer multiples of beatsPerBar in absolute
            // beats — the same meter convention as CurvedVelocity.
            var lowStarts = new List<double> { startBeats };
            if (beatsPerBar > 0)
            {
                double firstBar =
                    (Math.Floor((startBeats + Eps) / beatsPerBar) + 1) * beatsPerBar;
                for (double b = firstBar; b < end - Eps; b += beatsPerBar)
                    lowStarts.Add(b);
            }

            // Chronological merge; ties resolve low-before-upper.
            var hits = new List<Hit>(lowStarts.Count + upperStarts.Count);
            int li = 0, ui = 0;
            while (li < lowStarts.Count || ui < upperStarts.Count)
            {
                bool takeLow = li < lowStarts.Count &&
                               (ui >= upperStarts.Count ||
                                lowStarts[li] <= upperStarts[ui] + Eps);
                if (takeLow)
                {
                    double pos = lowStarts[li];
                    double boundary = (li + 1 < lowStarts.Count) ? lowStarts[li + 1] : end;
                    hits.Add(new Hit(pos, boundary - pos,
                        CurvedVelocity(pos, beatsPerBar, baseVelocity), 0));
                    li++;
                }
                else
                {
                    double pos = upperStarts[ui];
                    hits.Add(new Hit(pos, Math.Min(OffbeatDurBeats, end - pos),
                        CurvedVelocity(pos, beatsPerBar, baseVelocity),
                        UpperVoicesIndex));
                    ui++;
                }
            }
            return hits;
        }

        // ------------------------------------------------------------------
        // CA-T2-BOSSA-V2 — the authentic bossa comping template
        // ------------------------------------------------------------------

        // D-FEEL-ACCENT=A: template-supplied accent tiers. They REUSE the SD-5
        // factor VALUES — what changes is who assigns them: the template row,
        // not the beat position. The surdo weight sits on beat 2 (strong),
        // NOT on the downbeat (medium) — the deliberate inversion of
        // CurvedVelocity's curve that makes the figure read as bossa
        // (spec §0.3/§6.6). Documented as a per-figure exception to §8.3.
        internal const double BossaTierStrong = AccentDownbeat; // ×1.00
        internal const double BossaTierMedium = AccentOnBeat;   // ×0.85
        internal const double BossaTierWeak = AccentOffBeat;    // ×0.80

        /// <summary>One row of the fixed Bossa template: cycle-relative onset,
        /// nominal duration, role (low anchor vs upper voices) and accent tier
        /// factor. Rows are declared chronologically with low-before-upper at
        /// position ties, so plan generation needs no sort.</summary>
        private readonly struct BossaRow
        {
            public readonly double Pos;    // beats from cycle start
            public readonly double Dur;    // nominal; clipped at cycle/event end
            public readonly bool Low;      // true = LOW (index 0); false = UPPERS (-2)
            public readonly double Factor; // accent tier (D-FEEL-ACCENT=A)

            public BossaRow(double pos, double dur, bool low, double factor)
            { Pos = pos; Dur = dur; Low = low; Factor = factor; }
        }

        /// <summary>CA-T2-BOSSA-V2 (D-FEEL-SCOPE=A): the lab spec's
        /// `basico_solo` 1-bar pattern, verbatim — the recognizability
        /// threshold (spec §6.6) says this template alone already reads as
        /// bossa. The 2.5 row is the syncopation: it sustains to the cycle
        /// end and there is deliberately NO attack on beat 3.</summary>
        private static readonly BossaRow[] BossaTemplate =
        {
            //          pos  dur  low    tier
            new BossaRow(0.0, 2.0, true,  BossaTierMedium),
            new BossaRow(0.0, 1.0, false, BossaTierMedium),
            new BossaRow(1.0, 1.5, false, BossaTierWeak),
            new BossaRow(2.0, 2.0, true,  BossaTierStrong), // surdo
            new BossaRow(2.5, 1.5, false, BossaTierStrong), // syncopation
        };

        /// <summary>
        /// CA-T2-BOSSA-V2: the authentic bossa comping figure (see
        /// <see cref="ChordExpressionType.Bossa"/>). Pure, RNG-free, stateless.
        ///
        /// The template cycle is one bar (beatsPerBar beats) anchored at
        /// absolute beat 0 — i.e. the Part start (spec §6.1 blesses this
        /// anchor; no phrase detection in v1). Cycle position is derived from
        /// the ABSOLUTE beat position, the same meter convention as
        /// <see cref="CurvedVelocity"/>, so a chord change mid-cycle INHERITS
        /// the phase and never resets it (spec §6.2).
        ///
        /// Rules, in order:
        /// - noteCount &lt;= 1 → Block (no register to split); beatsPerBar
        ///   &lt;= 0 → Block (no bar to cycle on).
        /// - Rows at/after the bar length are dropped (meter clip: 2/4 keeps
        ///   only the first half of the template); each kept row's duration is
        ///   clipped at the cycle end and then at the event end (D-FEEL-TIE=A:
        ///   no hit ever overshoots the event window; the next cycle
        ///   re-attacks at 0.0, so truncating at the boundary is perceptually
        ///   legato-to-the-next-attack).
        /// - If the window contains no UPPERS attack, the whole event degrades
        ///   to Block (mirror of <see cref="BassUpperSplitPlan"/> /
        ///   OD-BOSSA-4: a bass-only fragment would be a silent register
        ///   shift, F-WALK-REG).
        /// - If no kept row lands on the event onset, a LOW hit is prepended
        ///   at the onset (medium tier), legato to the first template attack —
        ///   a chord change must always be heard at its onset (same principle
        ///   as <see cref="OnBeatPlan"/>; low role: register-safe and it does
        ///   not disturb the uppers syncopation).
        ///
        /// Hits are chronological with low-before-upper at position ties, by
        /// template construction (no sort).
        /// </summary>
        private static IReadOnlyList<Hit> BossaTemplatePlan(
            double startBeats, double durBeats, double end, int beatsPerBar,
            int baseVelocity, int noteCount)
        {
            if (noteCount <= 1 || beatsPerBar <= 0)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            double cycle = beatsPerBar;
            long firstCycle = (long)Math.Floor((startBeats + Eps) / cycle);

            var hits = new List<Hit>();
            bool anyUppers = false;

            for (long k = firstCycle; k * cycle < end - Eps; k++)
            {
                double c = k * cycle;
                for (int r = 0; r < BossaTemplate.Length; r++)
                {
                    var row = BossaTemplate[r];
                    if (row.Pos >= cycle - Eps) continue; // meter clip
                    double t = c + row.Pos;
                    if (t < startBeats - Eps || t >= end - Eps) continue;

                    double dur = Math.Min(row.Dur, cycle - row.Pos); // cycle end
                    dur = Math.Min(dur, end - t);                    // event end
                    hits.Add(new Hit(t, dur,
                        TierVelocity(row.Factor, baseVelocity),
                        row.Low ? 0 : UpperVoicesIndex));
                    if (!row.Low) anyUppers = true;
                }
            }

            if (!anyUppers)
                return BlockPlan(startBeats, durBeats, baseVelocity); // degrade

            if (hits[0].StartBeats > startBeats + Eps)
                hits.Insert(0, new Hit(startBeats,
                    hits[0].StartBeats - startBeats,
                    TierVelocity(BossaTierMedium, baseVelocity), 0));

            return hits;
        }

        /// <summary>D-FEEL-ACCENT=A: velocity for a template-supplied accent
        /// tier — <see cref="CurvedVelocity"/>'s arithmetic (round
        /// away-from-zero, clamp 1..127) with the factor chosen by the
        /// template row instead of the beat position.</summary>
        internal static int TierVelocity(double factor, int baseVelocity)
        {
            int v = (int)Math.Round(baseVelocity * factor, MidpointRounding.AwayFromZero);
            return Mathf.Clamp(v, 1, 127);
        }

        /// <summary>One arpeggio hit length in beats for the given rate.</summary>
        public static double ArpeggioIntervalBeats(ArpeggioRate rate)
        {
            switch (rate)
            {
                case ArpeggioRate.PerBeat: return 1.0;
                case ArpeggioRate.Sixteenth: return 0.25;
                // CA-V1: a leaked Random sentinel is resolved composer-side and
                // must never reach here; if it does, Eighth (never silent).
                case ArpeggioRate.Random:
                case ArpeggioRate.Eighth:
                default: return 0.5;
            }
        }

        /// <summary>
        /// BASS-WALK-1: exposes the arpeggio degrade predicate (ArpeggioPlan /
        /// ChordPulsePlan: events shorter than one hit degrade to Block) so
        /// consumers that must stay monophonic can avoid handing a multi-note
        /// playable to a plan that will degrade. Pure, RNG-free, stateless.
        /// </summary>
        public static bool ArpeggioFits(double durBeats, ArpeggioRate rate)
            => durBeats >= ArpeggioIntervalBeats(rate) - Eps;

        /// <summary>
        /// CA-V1 (D-V1-JIT-SCOPE=A): offsets every planned hit's velocity by the
        /// pure per-hit jitter delta and clamps 1..127 (a jittered velocity 0
        /// would be note-off semantics). Applies to ALL figures including Block,
        /// which is what makes "humanize a block render" expressible.
        ///
        /// Amount == 0 returns the input list BY REFERENCE — the pre-CA-V1
        /// bit-identity is structural, not an empirical property to re-verify.
        /// Indexing by hit position (not by stream consumption) is what keeps the
        /// jitter immune to draw-order coupling across events.
        /// </summary>
        private static IReadOnlyList<Hit> ApplyJitter(
            IReadOnlyList<Hit> hits, VelocityJitter jitter)
        {
            if (jitter.IsOff || hits == null || hits.Count == 0)
                return hits;

            var jittered = new Hit[hits.Count];
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                jittered[i] = new Hit(
                    h.StartBeats, h.DurBeats,
                    Mathf.Clamp(h.Velocity + jitter.DeltaFor(i), 1, 127),
                    h.NoteIndex);
            }
            return jittered;
        }

        /// <summary>
        /// SD-5=A velocity model: multiplicative accent curve over the authored
        /// per-event base velocity, as a pure function of absolute beat position
        /// within the Part meter. Downbeat �1.00, other on-beat �0.85,
        /// off-beat �0.80; round away-from-zero; clamp 1..127.
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
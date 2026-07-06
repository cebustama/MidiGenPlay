#if UNITY_EDITOR
// CA-T1 — Tier-1 chord articulation engine tests.
//
// Targets the internal pure planning seam via Runtime/AssemblyInfo.cs:
//
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]
//
//   1. ChordArticulator.PlanHits — figure math, accent curve, degrade rules,
//      meter authority (all pure; no Unity assets, mirrors the seam approach
//      of ChordTrackComposer_InversionPinTests).
//   2. ChordArticulator.Emit — Block bit-identity against the legacy
//      MoveToTime+Chord pair at MIDI-byte level, and arpeggio note ordering
//      at MIDI-note level (PatternBuilder only, no MIDIInstrumentSO fixture).
//
// Decisions covered: SD-1=A (6-member taxonomy), SD-3=A (RNG-free pure curve),
// SD-4=B (eighth default rate, configurable; onset-anchored cycling; Block
// degrade for too-short events), SD-5=A (multiplicative curve 1.00/0.85/0.80,
// round away-from-zero, clamp 1..127; Block keeps legacy clamp 0..127).
// See runtime/SSoT_Composer_Backing_Track.md §8.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay.Composition;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_ArticulationTests
    {
        private const double Tol = 1e-9;

        private static IReadOnlyList<ChordArticulator.Hit> Plan(
            ChordExpressionType expr,
            double start, double dur,
            int beatsPerBar = 4,
            int noteCount = 3,
            int baseVelocity = 100,
            ArpeggioRate rate = ArpeggioRate.Eighth) =>
            ChordArticulator.PlanHits(expr, rate, start, dur, beatsPerBar,
                                      noteCount, baseVelocity);

        private static IReadOnlyList<DwmNote> CMajorVoicerOrder() => new List<DwmNote>
        {
            // Deliberately NOT pitch-sorted: voicer order must be preserved for
            // chord hits and re-sorted only for arpeggio hits.
            DwmNote.Get(DwmNoteName.G, 4),
            DwmNote.Get(DwmNoteName.C, 4),
            DwmNote.Get(DwmNoteName.E, 4),
        };

        private static byte[] Bytes(PatternBuilder pb)
        {
            var file = pb.Build().ToFile(TempoMap.Create(Tempo.FromBeatsPerMinute(120)));
            using (var ms = new System.IO.MemoryStream())
            {
                file.Write(ms);
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------
        // Block — legacy plan and MIDI-byte bit-identity (the batch contract)
        // ------------------------------------------------------------------

        [Test]
        public void Block_Plan_IsSingleFullLengthChordHit_WithLegacyVelocityClamp()
        {
            var hits = Plan(ChordExpressionType.Block, start: 1.5, dur: 2.5);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].StartBeats, Is.EqualTo(1.5).Within(Tol));
            Assert.That(hits[0].DurBeats, Is.EqualTo(2.5).Within(Tol));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1), "Block emits the full chord.");
            Assert.That(hits[0].Velocity, Is.EqualTo(100));

            // Legacy clamp is 0..127 (NOT 1..127): base 0 stays 0, base 200 caps.
            Assert.That(Plan(ChordExpressionType.Block, 0, 1, baseVelocity: 0)[0].Velocity,
                Is.EqualTo(0));
            Assert.That(Plan(ChordExpressionType.Block, 0, 1, baseVelocity: 200)[0].Velocity,
                Is.EqualTo(127));
        }

        [Test]
        public void Block_Emit_IsByteIdenticalToLegacyMoveToTimeChordPair()
        {
            var playable = CMajorVoicerOrder();
            var beatSpan = MusicalTimeSpan.Quarter;
            double startBeats = 2.0, durBeats = 4.0;
            int velocity = 96;

            // Legacy pair, verbatim from the pre-CA-T1 emission sites.
            var legacy = new PatternBuilder();
            legacy.MoveToTime(beatSpan.Multiply(startBeats));
            legacy.Chord(playable, beatSpan.Multiply(durBeats),
                (SevenBitNumber)UnityEngine.Mathf.Clamp(velocity, 0, 127));

            // Articulated Block.
            var articulated = new PatternBuilder();
            new ChordArticulator().Emit(articulated, playable, startBeats, durBeats,
                beatSpan, beatsPerBar: 4, baseVelocity: velocity, stepsPerBeat: 4,
                ChordExpressionType.Block, ArpeggioRate.Eighth);

            Assert.That(Bytes(articulated), Is.EqualTo(Bytes(legacy)),
                "Block through the articulator must be bit-identical to the legacy pair.");
        }

        // ------------------------------------------------------------------
        // PerBeat / Staccato — meter-anchored on-beat grid
        // ------------------------------------------------------------------

        [Test]
        public void PerBeat_WholeBar44_FourLegatoHits_DownbeatAccented()
        {
            var hits = Plan(ChordExpressionType.PerBeat, start: 0, dur: 4);

            Assert.That(hits.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(hits[i].StartBeats, Is.EqualTo((double)i).Within(Tol));
                Assert.That(hits[i].DurBeats, Is.EqualTo(1.0).Within(Tol), "legato to next hit");
                Assert.That(hits[i].NoteIndex, Is.EqualTo(-1));
            }
            Assert.That(hits[0].Velocity, Is.EqualTo(100), "downbeat ×1.00");
            Assert.That(hits[1].Velocity, Is.EqualTo(85), "on-beat ×0.85");
            Assert.That(hits[3].Velocity, Is.EqualTo(85));
        }

        [Test]
        public void PerBeat_OffGridOnset_GetsOnsetHitThenMeterGrid()
        {
            // Event starts at 0.75, ends at 2.0: onset hit at 0.75 (a chord
            // change must always sound at its onset), then meter beat 1.
            var hits = Plan(ChordExpressionType.PerBeat, start: 0.75, dur: 1.25);

            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].StartBeats, Is.EqualTo(0.75).Within(Tol));
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.25).Within(Tol));
            Assert.That(hits[0].Velocity, Is.EqualTo(80), "off-beat onset ×0.80");
            Assert.That(hits[1].StartBeats, Is.EqualTo(1.0).Within(Tol));
            Assert.That(hits[1].DurBeats, Is.EqualTo(1.0).Within(Tol));
            Assert.That(hits[1].Velocity, Is.EqualTo(85));
        }

        [Test]
        public void Staccato_SameGridAsPerBeat_HitsCappedAtHalfBeat()
        {
            var hits = Plan(ChordExpressionType.Staccato, start: 0, dur: 2);

            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits.Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.0, 1.0 }).Within(Tol));
            Assert.That(hits.All(h => System.Math.Abs(h.DurBeats - 0.5) < Tol), Is.True);
        }

        // ------------------------------------------------------------------
        // Offbeat — upstrokes + the only empty-plan degrade
        // ------------------------------------------------------------------

        [Test]
        public void Offbeat_WholeBar44_FourUpstrokes_AllOffbeatVelocity()
        {
            var hits = Plan(ChordExpressionType.Offbeat, start: 0, dur: 4);

            Assert.That(hits.Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.5, 1.5, 2.5, 3.5 }).Within(Tol));
            Assert.That(hits.All(h => System.Math.Abs(h.DurBeats - 0.5) < Tol), Is.True);
            Assert.That(hits.All(h => h.Velocity == 80), Is.True, "off-beat ×0.80");
            Assert.That(hits.All(h => h.NoteIndex == -1), Is.True, "upstroke = full chord");
        }

        [Test]
        public void Offbeat_EventTooShortForAnyOffbeat_DegradesToBlock()
        {
            // [0, 0.5): the first offbeat (0.5) is outside the window.
            var hits = Plan(ChordExpressionType.Offbeat, start: 0, dur: 0.5,
                            baseVelocity: 0);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].StartBeats, Is.EqualTo(0.0).Within(Tol));
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.5).Within(Tol));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
            Assert.That(hits[0].Velocity, Is.EqualTo(0),
                "degraded event is true Block: legacy 0..127 clamp, no curve");
        }

        [Test]
        public void Offbeat_FinalHitTruncatedToEventBoundary()
        {
            // Event ends at 1.75: the 1.5 upstroke is cut to 0.25.
            var hits = Plan(ChordExpressionType.Offbeat, start: 0, dur: 1.75);

            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[1].StartBeats, Is.EqualTo(1.5).Within(Tol));
            Assert.That(hits[1].DurBeats, Is.EqualTo(0.25).Within(Tol),
                "no hit ever overshoots the event window");
        }

        // ------------------------------------------------------------------
        // Arpeggio — onset-anchored cycling, rate, truncation, degrades
        // ------------------------------------------------------------------

        [Test]
        public void ArpeggioUp_EighthRate_CyclesVoicingIndicesFromOnset()
        {
            var hits = Plan(ChordExpressionType.ArpeggioUp, start: 0, dur: 2,
                            noteCount: 3);

            Assert.That(hits.Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.0, 0.5, 1.0, 1.5 }).Within(Tol));
            Assert.That(hits.Select(h => h.NoteIndex),
                Is.EqualTo(new[] { 0, 1, 2, 0 }), "cycles through the voicing");
            Assert.That(hits.Select(h => h.Velocity),
                Is.EqualTo(new[] { 100, 80, 85, 80 }),
                "downbeat / off-beat / on-beat / off-beat curve");
        }

        [Test]
        public void Arpeggio_FinalNoteTruncated_AndRatesMapToBeats()
        {
            var hits = Plan(ChordExpressionType.ArpeggioUp, start: 0, dur: 1.75);
            Assert.That(hits.Last().DurBeats, Is.EqualTo(0.25).Within(Tol));

            Assert.That(ChordArticulator.ArpeggioIntervalBeats(ArpeggioRate.PerBeat),
                Is.EqualTo(1.0).Within(Tol));
            Assert.That(ChordArticulator.ArpeggioIntervalBeats(ArpeggioRate.Eighth),
                Is.EqualTo(0.5).Within(Tol));
            Assert.That(ChordArticulator.ArpeggioIntervalBeats(ArpeggioRate.Sixteenth),
                Is.EqualTo(0.25).Within(Tol));
        }

        [Test]
        public void Arpeggio_EventShorterThanOneHit_DegradesToBlock()
        {
            var hits = Plan(ChordExpressionType.ArpeggioUp, start: 0, dur: 0.25,
                            rate: ArpeggioRate.Eighth);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1), "degraded to full chord");
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.25).Within(Tol));
        }

        [Test]
        public void Arpeggio_EmptyVoicing_DegradesToBlock()
        {
            var hits = Plan(ChordExpressionType.ArpeggioUp, start: 0, dur: 4,
                            noteCount: 0);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Arpeggio_Emit_SortsByPitch_UpAscending_DownDescending()
        {
            var playable = CMajorVoicerOrder(); // G4, C4, E4 in voicer order
            var beatSpan = MusicalTimeSpan.Quarter;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(120));

            System.Func<ChordExpressionType, int[]> emittedPitches = expr =>
            {
                var pb = new PatternBuilder();
                new ChordArticulator().Emit(pb, playable, 0, 1.5, beatSpan,
                    beatsPerBar: 4, baseVelocity: 100, stepsPerBeat: 4,
                    expr, ArpeggioRate.Eighth);
                return pb.Build().ToFile(tempoMap).GetNotes()
                    .OrderBy(n => n.Time)
                    .Select(n => (int)n.NoteNumber).ToArray();
            };

            int c4 = DwmNote.Get(DwmNoteName.C, 4).NoteNumber;
            int e4 = DwmNote.Get(DwmNoteName.E, 4).NoteNumber;
            int g4 = DwmNote.Get(DwmNoteName.G, 4).NoteNumber;

            Assert.That(emittedPitches(ChordExpressionType.ArpeggioUp),
                Is.EqualTo(new[] { c4, e4, g4 }), "Up = voicing sorted low→high");
            Assert.That(emittedPitches(ChordExpressionType.ArpeggioDown),
                Is.EqualTo(new[] { g4, e4, c4 }), "Down = voicing sorted high→low");
        }

        // ------------------------------------------------------------------
        // Meter authority + velocity clamp + determinism
        // ------------------------------------------------------------------

        [Test]
        public void MeterAuthority_SevenEight_DownbeatOnlyAtBarMultiplesOfSeven()
        {
            // 7 beats per bar: two bars => downbeats at 0 and 7 only.
            var hits = Plan(ChordExpressionType.PerBeat, start: 0, dur: 14,
                            beatsPerBar: 7);

            Assert.That(hits.Count, Is.EqualTo(14));
            for (int i = 0; i < 14; i++)
            {
                int expected = (i % 7 == 0) ? 100 : 85;
                Assert.That(hits[i].Velocity, Is.EqualTo(expected),
                    $"beat {i}: accent must follow the Part meter, not 4/4");
            }
        }

        [Test]
        public void NonBlockVelocity_NeverZero_ClampsToOne()
        {
            // base 0 with any curve factor rounds to 0 → must clamp to 1
            // (velocity-0 note-on is note-off semantics).
            var hits = Plan(ChordExpressionType.PerBeat, start: 0, dur: 2,
                            baseVelocity: 0);
            Assert.That(hits.All(h => h.Velocity == 1), Is.True);

            // High base still caps at 127 even on the downbeat.
            var loud = Plan(ChordExpressionType.PerBeat, start: 0, dur: 1,
                            baseVelocity: 127);
            Assert.That(loud[0].Velocity, Is.EqualTo(127));
        }

        [Test]
        public void PlanHits_IsDeterministic_SameInputsSamePlan()
        {
            foreach (var expr in new[]
            {
                ChordExpressionType.Block, ChordExpressionType.PerBeat,
                ChordExpressionType.Offbeat, ChordExpressionType.Staccato,
                ChordExpressionType.ArpeggioUp, ChordExpressionType.ArpeggioDown,
            })
            {
                var a = Plan(expr, start: 0.75, dur: 3.5, beatsPerBar: 3);
                var b = Plan(expr, start: 0.75, dur: 3.5, beatsPerBar: 3);

                Assert.That(a.Count, Is.EqualTo(b.Count), expr.ToString());
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(a[i].StartBeats, Is.EqualTo(b[i].StartBeats), expr.ToString());
                    Assert.That(a[i].DurBeats, Is.EqualTo(b[i].DurBeats), expr.ToString());
                    Assert.That(a[i].Velocity, Is.EqualTo(b[i].Velocity), expr.ToString());
                    Assert.That(a[i].NoteIndex, Is.EqualTo(b[i].NoteIndex), expr.ToString());
                }
            }
        }
    }
}
#endif
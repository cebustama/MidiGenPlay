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

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

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
                ChordExpressionType.BassUpperSplit, ChordExpressionType.Bossa,
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

        // ---------------- CA-T2: reshape (pitch) ----------------

        private static Note N(int midi) => Note.Get((SevenBitNumber)midi);

        [Test]
        public void Reshape_PowerChord_DropsThird_KeepsRootFifthOctave()
        {
            var reshaper = new ChordReshaper();
            // C major triad voiced C4(60) E4(64) G4(67); root pc = C.
            var voiced = new List<Note> { N(60), N(64), N(67) };
            var pcs = new[] { NoteName.C, NoteName.E, NoteName.G };

            var outv = reshaper.Reshape(voiced, pcs, ChordExpressionType.PowerChord)
                               .Select(n => (int)n.NoteNumber).OrderBy(x => x).ToList();

            Assert.That(outv, Is.EquivalentTo(new[] { 60, 67, 72 })); // C, G, C(oct); no E
        }

        [Test]
        public void Reshape_NonTier2_IsIdentity()
        {
            var reshaper = new ChordReshaper();
            var voiced = new List<Note> { N(60), N(64), N(67) };
            var pcs = new[] { NoteName.C, NoteName.E, NoteName.G };
            foreach (var expr in new[] {
                ChordExpressionType.Block, ChordExpressionType.PerBeat,
                ChordExpressionType.ArpeggioUp, ChordExpressionType.Random })
                Assert.That(reshaper.Reshape(voiced, pcs, expr), Is.SameAs(voiced));
        }

        // ---------------- CA-T2: chugging pulse (rhythm) ----------------

        [Test]
        public void Chugging_PulsesFullChord_AtArpeggioRate()
        {
            // 2-beat event, Eighth rate => 4 full-chord hits, all NoteIndex = -1.
            var hits = ChordArticulator.PlanHits(
                ChordExpressionType.Chugging, ArpeggioRate.Eighth,
                startBeats: 0, durBeats: 2, beatsPerBar: 4, noteCount: 2, baseVelocity: 100);

            Assert.That(hits.Count, Is.EqualTo(4));
            Assert.That(hits.All(h => h.NoteIndex == -1), Is.True, "chug = full chord, not arpeggiated");
        }

        [Test]
        public void PowerChord_DegradesToBlock_InArticulator()
        {
            var hits = ChordArticulator.PlanHits(
                ChordExpressionType.PowerChord, ArpeggioRate.Eighth,
                0, 4, 4, 3, 90);
            Assert.That(hits.Count, Is.EqualTo(1));      // Block plan
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
        }

        // ------------------------------------------------------------------
        // CA-T2-BOSSA — register-selective split (low on the bar downbeat,
        // uppers on the offbeats), renamed Bossa → BassUpperSplit by
        // CA-T2-BOSSA-V2 (OD-BOSSA-7=A; value 9 intact, behavior UNCHANGED —
        // these tests were renamed, not rewritten). D-BOSSA-SEL=A:
        // NoteIndex 0 = lowest note of the ascending sort, -2 = upper-voices
        // sentinel. Emit-level tests read the RESULTING MIDI (BASS-WALK-1
        // verification lesson): pitches are asserted on GetNotes() output,
        // never on pre-emission variables.
        // ------------------------------------------------------------------

        [Test]
        public void BassUpperSplit_WholeBar44_LowSustainedOnDownbeat_UppersOnOffbeats()
        {
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 4);

            Assert.That(hits.Count, Is.EqualTo(5), "1 low + 4 upper hits");

            // Low: onset/downbeat, legato across the whole event, ×1.00.
            Assert.That(hits[0].StartBeats, Is.EqualTo(0.0).Within(Tol));
            Assert.That(hits[0].DurBeats, Is.EqualTo(4.0).Within(Tol));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(0), "lowest note of ascending sort");
            Assert.That(hits[0].Velocity, Is.EqualTo(100), "downbeat ×1.00");

            // Uppers: OffbeatPlan's grid and length, ×0.80, sentinel -2.
            Assert.That(hits.Skip(1).Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.5, 1.5, 2.5, 3.5 }).Within(Tol));
            Assert.That(hits.Skip(1).All(h =>
                System.Math.Abs(h.DurBeats - 0.5) < Tol), Is.True);
            Assert.That(hits.Skip(1).All(h => h.NoteIndex == -2), Is.True,
                "upper-voices sentinel");
            Assert.That(hits.Skip(1).All(h => h.Velocity == 80), Is.True,
                "off-beat ×0.80");
        }

        [Test]
        public void BassUpperSplit_MultiBarEvent_LowRestruckOnEachBarDownbeat()
        {
            // Two 4/4 bars: low hits at 0 and 4, each legato to the next
            // low hit / event end; uppers on all eight offbeats.
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 8);

            var lows = hits.Where(h => h.NoteIndex == 0).ToList();
            Assert.That(lows.Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.0, 4.0 }).Within(Tol));
            Assert.That(lows.All(h => System.Math.Abs(h.DurBeats - 4.0) < Tol),
                Is.True, "legato to next low hit / event end");
            Assert.That(lows.All(h => h.Velocity == 100), Is.True, "bar downbeats ×1.00");

            Assert.That(hits.Count(h => h.NoteIndex == -2), Is.EqualTo(8));
        }

        [Test]
        public void BassUpperSplit_OffGridOnset_LowAtOnset_NoInteriorBarDownbeat()
        {
            // [0.75, 4.0): the chord change must sound at its onset; the next
            // bar downbeat (4.0) is outside, so a single low hit spans the event.
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0.75, dur: 3.25);

            var lows = hits.Where(h => h.NoteIndex == 0).ToList();
            Assert.That(lows.Count, Is.EqualTo(1));
            Assert.That(lows[0].StartBeats, Is.EqualTo(0.75).Within(Tol));
            Assert.That(lows[0].DurBeats, Is.EqualTo(3.25).Within(Tol));
            Assert.That(lows[0].Velocity, Is.EqualTo(80), "off-beat onset ×0.80");

            Assert.That(hits.Where(h => h.NoteIndex == -2).Select(h => h.StartBeats),
                Is.EqualTo(new[] { 1.5, 2.5, 3.5 }).Within(Tol),
                "offbeats before the onset are skipped");
        }

        [Test]
        public void BassUpperSplit_MeterAuthority_ThreeFour_LowOnBarMultiplesOfThree()
        {
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 6,
                            beatsPerBar: 3);
            Assert.That(hits.Where(h => h.NoteIndex == 0).Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.0, 3.0 }).Within(Tol),
                "bar downbeats follow the Part meter, not 4/4");
        }

        [Test]
        public void BassUpperSplit_MonoVoicing_DegradesToBlock()
        {
            // 1-note voicing (bass path): no register to split — true Block,
            // including the legacy 0..127 clamp.
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 4,
                            noteCount: 1, baseVelocity: 0);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
            Assert.That(hits[0].Velocity, Is.EqualTo(0), "legacy Block clamp, no curve");
        }

        [Test]
        public void BassUpperSplit_EmptyVoicing_DegradesToBlock()
        {
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 4,
                            noteCount: 0);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
        }

        [Test]
        public void BassUpperSplit_NoOffbeatFits_DegradesToBlock()
        {
            // [0, 0.4): the first offbeat (0.5) is outside — a bass-only
            // sustain would be a drastic register change (F-WALK-REG), so the
            // whole event degrades (mirror of OffbeatPlan's empty-plan rule).
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 0.4);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.4).Within(Tol));
        }

        [Test]
        public void BassUpperSplit_NoHitOvershootsEventWindow()
        {
            // Event ends at 3.7: the 3.5 upper hit is cut to 0.2; the low hit
            // ends exactly at the event end.
            var hits = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 3.7);
            foreach (var h in hits)
                Assert.That(h.StartBeats + h.DurBeats,
                    Is.LessThanOrEqualTo(3.7 + Tol));
            Assert.That(hits.Last().DurBeats, Is.EqualTo(0.2).Within(Tol));
        }

        [Test]
        public void BassUpperSplit_Emit_DownbeatIsLowestPitch_OffbeatsExcludeLowest()
        {
            // THE resulting-MIDI probe (BASS-WALK-1 lesson): assert on the
            // pitches Emit actually produced, grouped by emitted note time —
            // not on any pre-emission variable.
            var playable = CMajorVoicerOrder(); // G4, C4, E4 — lowest is C4
            var beatSpan = MusicalTimeSpan.Quarter;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(120));

            var pb = new PatternBuilder();
            new ChordArticulator().Emit(pb, playable, 0, 4, beatSpan,
                beatsPerBar: 4, baseVelocity: 100, stepsPerBeat: 4,
                ChordExpressionType.BassUpperSplit, ArpeggioRate.Eighth);

            var groups = pb.Build().ToFile(tempoMap).GetNotes()
                .GroupBy(n => n.Time)
                .OrderBy(g => g.Key)
                .Select(g => g.Select(n => (int)n.NoteNumber)
                              .OrderBy(x => x).ToArray())
                .ToArray();

            int c4 = DwmNote.Get(DwmNoteName.C, 4).NoteNumber;
            int e4 = DwmNote.Get(DwmNoteName.E, 4).NoteNumber;
            int g4 = DwmNote.Get(DwmNoteName.G, 4).NoteNumber;

            Assert.That(groups.Length, Is.EqualTo(5), "1 low onset + 4 offbeats");
            Assert.That(groups[0], Is.EqualTo(new[] { c4 }),
                "downbeat = exactly the lowest voiced pitch");
            for (int i = 1; i < groups.Length; i++)
                Assert.That(groups[i], Is.EqualTo(new[] { e4, g4 }),
                    $"offbeat group {i} = exactly the non-lowest pitches");
        }

        [Test]
        public void BassUpperSplit_Emit_ArpeggioRateIsIgnored()
        {
            // D-BOSSA-RHYTHM=A: the fixed template must not react to the rate.
            var playable = CMajorVoicerOrder();
            var beatSpan = MusicalTimeSpan.Quarter;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(120));

            System.Func<ArpeggioRate, byte[]> render = rate =>
            {
                var pb = new PatternBuilder();
                new ChordArticulator().Emit(pb, playable, 0, 4, beatSpan,
                    beatsPerBar: 4, baseVelocity: 100, stepsPerBeat: 4,
                    ChordExpressionType.BassUpperSplit, rate);
                return Bytes(pb);
            };

            Assert.That(render(ArpeggioRate.Sixteenth),
                Is.EqualTo(render(ArpeggioRate.PerBeat)));
        }

        [Test]
        public void BassUpperSplit_Jitter_OffsetsVelocitiesOnly_TimingAndSelectionUntouched()
        {
            // CA-V1 composes: the jitter is a post-pass; the figure's timing and
            // NoteIndex vocabulary must be untouched, velocities clamped 1..127.
            var jitter = new VelocityJitter(20, seed: 12345);
            var plain = Plan(ChordExpressionType.BassUpperSplit, start: 0, dur: 4);
            var jittered = ChordArticulator.PlanHits(
                ChordExpressionType.BassUpperSplit, ArpeggioRate.Eighth,
                0, 4, 4, 3, 100, jitter);

            Assert.That(jittered.Count, Is.EqualTo(plain.Count));
            for (int i = 0; i < plain.Count; i++)
            {
                Assert.That(jittered[i].StartBeats,
                    Is.EqualTo(plain[i].StartBeats).Within(Tol));
                Assert.That(jittered[i].DurBeats,
                    Is.EqualTo(plain[i].DurBeats).Within(Tol));
                Assert.That(jittered[i].NoteIndex, Is.EqualTo(plain[i].NoteIndex));
                Assert.That(jittered[i].Velocity, Is.InRange(1, 127));
            }
        }

        // ------------------------------------------------------------------
        // CA-T2-BOSSA-V2 — Bossa = 10, the AUTHENTIC 1-bar comping template
        // (lab spec `basico_solo`, D-FEEL-SCOPE=A). Template rows
        // (cycle-relative): LOW 0.0×2.0 medium · UPPERS 0.0×1.0 medium ·
        // UPPERS 1.0×1.5 weak · LOW 2.0×2.0 STRONG (surdo) ·
        // UPPERS 2.5×1.5 STRONG (syncopation — no attack on beat 3).
        // Accents are template tiers (D-FEEL-ACCENT=A), NOT the position
        // curve: strong 100 / medium 85 / weak 80 at base 100.
        // ------------------------------------------------------------------

        [Test]
        public void Bossa_WholeBar44_TemplateRowsExact()
        {
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4);

            Assert.That(hits.Count, Is.EqualTo(5), "the 5 template rows");

            var expected = new (double pos, double dur, int idx, int vel)[]
            {
                (0.0, 2.0,  0,  85), // LOW medium
                (0.0, 1.0, -2,  85), // UPPERS medium
                (1.0, 1.5, -2,  80), // UPPERS weak
                (2.0, 2.0,  0, 100), // LOW strong — surdo
                (2.5, 1.5, -2, 100), // UPPERS strong — syncopation
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(hits[i].StartBeats,
                    Is.EqualTo(expected[i].pos).Within(Tol), $"row {i} pos");
                Assert.That(hits[i].DurBeats,
                    Is.EqualTo(expected[i].dur).Within(Tol), $"row {i} dur");
                Assert.That(hits[i].NoteIndex,
                    Is.EqualTo(expected[i].idx), $"row {i} role");
                Assert.That(hits[i].Velocity,
                    Is.EqualTo(expected[i].vel), $"row {i} accent tier");
            }
        }

        [Test]
        public void Bossa_SurdoAccent_Beat2StrongerThanDownbeat()
        {
            // Spec §0.3/§6.6 requirement 1 — THE identity-bearing feature:
            // the weight sits on beat 2, not on the downbeat. If this test
            // regresses, the figure stops being bossa by way of dynamics.
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4);

            var lowDownbeat = hits.Single(h =>
                h.NoteIndex == 0 && System.Math.Abs(h.StartBeats - 0.0) < Tol);
            var lowSurdo = hits.Single(h =>
                h.NoteIndex == 0 && System.Math.Abs(h.StartBeats - 2.0) < Tol);

            Assert.That(lowSurdo.Velocity, Is.GreaterThan(lowDownbeat.Velocity),
                "surdo inversion: beat 2 must outweigh the downbeat");
            Assert.That(lowSurdo.Velocity, Is.EqualTo(100), "strong ×1.00");
            Assert.That(lowDownbeat.Velocity, Is.EqualTo(85), "medium ×0.85");
        }

        [Test]
        public void Bossa_NoAttackOnBeat3_SyncopationSustains()
        {
            // The syncopation's whole point (spec §1 row 3): the 2.5 attack
            // sounds THROUGH beat 3 — beat 3 passes with no new attack.
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4);

            Assert.That(hits.Any(h =>
                System.Math.Abs(h.StartBeats - 3.0) < Tol), Is.False,
                "no template attack on beat 3");
            var sync = hits.Single(h =>
                System.Math.Abs(h.StartBeats - 2.5) < Tol);
            Assert.That(sync.StartBeats + sync.DurBeats,
                Is.EqualTo(4.0).Within(Tol), "sustains to the cycle end");
        }

        [Test]
        public void Bossa_TwoBarEvent_TwoIdenticalCycles()
        {
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 8);

            Assert.That(hits.Count, Is.EqualTo(10), "5 rows × 2 cycles");
            for (int i = 0; i < 5; i++)
            {
                Assert.That(hits[i + 5].StartBeats,
                    Is.EqualTo(hits[i].StartBeats + 4.0).Within(Tol),
                    "cycle 2 = cycle 1 shifted one bar");
                Assert.That(hits[i + 5].DurBeats,
                    Is.EqualTo(hits[i].DurBeats).Within(Tol));
                Assert.That(hits[i + 5].NoteIndex, Is.EqualTo(hits[i].NoteIndex));
                Assert.That(hits[i + 5].Velocity, Is.EqualTo(hits[i].Velocity));
            }
        }

        [Test]
        public void Bossa_MidCycleOnset_InheritsPhase_NeverResets()
        {
            // Spec §6.2: a chord change mid-cycle continues the cycle — it
            // does NOT restart the template at the new onset. An event over
            // the second half of the bar gets the surdo half: rows 2.0 and
            // 2.5, at their CYCLE positions and tiers.
            var hits = Plan(ChordExpressionType.Bossa, start: 2, dur: 2);

            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].StartBeats, Is.EqualTo(2.0).Within(Tol));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(0));
            Assert.That(hits[0].Velocity, Is.EqualTo(100), "surdo strong kept");
            Assert.That(hits[1].StartBeats, Is.EqualTo(2.5).Within(Tol));
            Assert.That(hits[1].NoteIndex, Is.EqualTo(-2));
        }

        [Test]
        public void Bossa_OnsetWithoutTemplateAttack_GetsLowOnsetHit()
        {
            // A chord change must always be heard at its onset: an onset that
            // lands between template rows is given a LOW hit (medium tier),
            // legato to the first template attack.
            var hits = Plan(ChordExpressionType.Bossa, start: 1.5, dur: 2.5);

            Assert.That(hits[0].StartBeats, Is.EqualTo(1.5).Within(Tol));
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.5).Within(Tol),
                "legato to the 2.0 template row");
            Assert.That(hits[0].NoteIndex, Is.EqualTo(0), "low role: register-safe");
            Assert.That(hits[0].Velocity, Is.EqualTo(85), "medium tier");

            Assert.That(hits.Skip(1).Select(h => h.StartBeats),
                Is.EqualTo(new[] { 2.0, 2.5 }).Within(Tol),
                "template rows keep their cycle positions");
        }

        [Test]
        public void Bossa_MeterClip_ThreeFour_RowsSurviveDursTruncate()
        {
            // 3/4: cycle = 3 beats. All rows sit below 3.0 and survive; the
            // 2.0 LOW and 2.5 UPPERS durations truncate at the cycle end.
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 6,
                            beatsPerBar: 3);

            Assert.That(hits.Count, Is.EqualTo(10), "5 rows × 2 cycles of 3");
            Assert.That(hits.Where(h => h.NoteIndex == 0)
                            .Select(h => h.StartBeats),
                Is.EqualTo(new[] { 0.0, 2.0, 3.0, 5.0 }).Within(Tol));

            var surdo = hits.Single(h =>
                h.NoteIndex == 0 && System.Math.Abs(h.StartBeats - 2.0) < Tol);
            Assert.That(surdo.DurBeats, Is.EqualTo(1.0).Within(Tol),
                "clipped at the 3-beat cycle end");
            var sync = hits.Single(h =>
                System.Math.Abs(h.StartBeats - 2.5) < Tol);
            Assert.That(sync.DurBeats, Is.EqualTo(0.5).Within(Tol));
        }

        [Test]
        public void Bossa_MeterClip_TwoFour_SecondHalfRowsDropped()
        {
            // 2/4: rows at 2.0 and 2.5 fall outside the bar and are dropped;
            // the surviving half still contains an UPPERS attack, so the
            // figure renders (deterministically degraded, never silent).
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4,
                            beatsPerBar: 2);

            Assert.That(hits.Count, Is.EqualTo(6), "3 surviving rows × 2 cycles");
            Assert.That(hits.All(h => (h.StartBeats % 2.0) < 1.0 + Tol),
                Is.True, "no row at cycle positions 2.0/2.5");
            Assert.That(hits[0].DurBeats, Is.EqualTo(2.0).Within(Tol),
                "LOW nominal 2.0 fits the 2-beat cycle exactly");
        }

        [Test]
        public void Bossa_MonoVoicing_DegradesToBlock()
        {
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4,
                            noteCount: 1, baseVelocity: 0);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
            Assert.That(hits[0].Velocity, Is.EqualTo(0), "legacy Block clamp");
        }

        [Test]
        public void Bossa_EmptyVoicing_DegradesToBlock()
        {
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 4,
                            noteCount: 0);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Bossa_WindowWithoutUppersAttack_DegradesToBlock()
        {
            // [3.0, 3.4): no template row falls inside — a low-only fallback
            // would be a bass-only fragment (silent register shift,
            // F-WALK-REG), so the event degrades whole (mirror of
            // BassUpperSplit's OD-BOSSA-4 rule).
            var hits = Plan(ChordExpressionType.Bossa, start: 3, dur: 0.4);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].NoteIndex, Is.EqualTo(-1));
            Assert.That(hits[0].DurBeats, Is.EqualTo(0.4).Within(Tol));
        }

        [Test]
        public void Bossa_NoHitOvershootsEventWindow()
        {
            // Event ends at 3.7 (D-FEEL-TIE=A): the surdo LOW (2.0×2.0) cuts
            // to 1.7 and the syncopation (2.5×1.5) cuts to 1.2.
            var hits = Plan(ChordExpressionType.Bossa, start: 0, dur: 3.7);
            foreach (var h in hits)
                Assert.That(h.StartBeats + h.DurBeats,
                    Is.LessThanOrEqualTo(3.7 + Tol));
            Assert.That(hits.Last().DurBeats, Is.EqualTo(1.2).Within(Tol));
        }

        [Test]
        public void Bossa_Emit_LowIsLowestPitch_UppersExcludeLowest()
        {
            // THE resulting-MIDI probe (BASS-WALK-1 lesson): assert on the
            // pitches Emit actually produced, grouped by emitted note time.
            // At 0.0 BOTH roles attack (low-first tie-break), so the first
            // time-group is the full pitch set.
            var playable = CMajorVoicerOrder(); // G4, C4, E4 — lowest is C4
            var beatSpan = MusicalTimeSpan.Quarter;
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(120));

            var pb = new PatternBuilder();
            new ChordArticulator().Emit(pb, playable, 0, 4, beatSpan,
                beatsPerBar: 4, baseVelocity: 100, stepsPerBeat: 4,
                ChordExpressionType.Bossa, ArpeggioRate.Eighth);

            var groups = pb.Build().ToFile(tempoMap).GetNotes()
                .GroupBy(n => n.Time)
                .OrderBy(g => g.Key)
                .Select(g => g.Select(n => (int)n.NoteNumber)
                              .OrderBy(x => x).ToArray())
                .ToArray();

            int c4 = DwmNote.Get(DwmNoteName.C, 4).NoteNumber;
            int e4 = DwmNote.Get(DwmNoteName.E, 4).NoteNumber;
            int g4 = DwmNote.Get(DwmNoteName.G, 4).NoteNumber;

            Assert.That(groups.Length, Is.EqualTo(4),
                "attack times 0.0, 1.0, 2.0, 2.5 — and NOTHING on beat 3");
            Assert.That(groups[0], Is.EqualTo(new[] { c4, e4, g4 }),
                "0.0: low + uppers together = the full pitch set");
            Assert.That(groups[1], Is.EqualTo(new[] { e4, g4 }),
                "1.0: exactly the non-lowest pitches");
            Assert.That(groups[2], Is.EqualTo(new[] { c4 }),
                "2.0 (surdo): exactly the lowest voiced pitch");
            Assert.That(groups[3], Is.EqualTo(new[] { e4, g4 }),
                "2.5 (syncopation): exactly the non-lowest pitches");
        }

        [Test]
        public void Bossa_Emit_ArpeggioRateIsIgnored()
        {
            // The fixed template must not react to the rate (byte-level).
            var playable = CMajorVoicerOrder();

            System.Func<ArpeggioRate, byte[]> render = rate =>
            {
                var pb = new PatternBuilder();
                new ChordArticulator().Emit(pb, playable, 0, 4,
                    MusicalTimeSpan.Quarter, beatsPerBar: 4,
                    baseVelocity: 100, stepsPerBeat: 4,
                    ChordExpressionType.Bossa, rate);
                return Bytes(pb);
            };

            Assert.That(render(ArpeggioRate.Sixteenth),
                Is.EqualTo(render(ArpeggioRate.PerBeat)));
        }

        [Test]
        public void Bossa_Jitter_OffsetsVelocitiesOnly_TimingAndSelectionUntouched()
        {
            // CA-V1 composes: the jitter is a post-pass; the template's
            // timing and NoteIndex vocabulary must be untouched.
            var jitter = new VelocityJitter(20, seed: 12345);
            var plain = Plan(ChordExpressionType.Bossa, start: 0, dur: 4);
            var jittered = ChordArticulator.PlanHits(
                ChordExpressionType.Bossa, ArpeggioRate.Eighth,
                0, 4, 4, 3, 100, jitter);

            Assert.That(jittered.Count, Is.EqualTo(plain.Count));
            for (int i = 0; i < plain.Count; i++)
            {
                Assert.That(jittered[i].StartBeats,
                    Is.EqualTo(plain[i].StartBeats).Within(Tol));
                Assert.That(jittered[i].DurBeats,
                    Is.EqualTo(plain[i].DurBeats).Within(Tol));
                Assert.That(jittered[i].NoteIndex, Is.EqualTo(plain[i].NoteIndex));
                Assert.That(jittered[i].Velocity, Is.InRange(1, 127));
            }
        }
    }
}
#endif
#if UNITY_EDITOR
// MGP-ARTIC-RATE-1 — EditMode regression tests for the §8.4 both-sites
// guarantee at ARGUMENT granularity.
//
// Why these tests exist (F-ARTIC-RATE-GRID-1): the whole CA-V1 suite
// (ChordTrackComposer_RandomArticulationTests, ..._VelocityJitterTests,
// ..._ArticulationTests) sits at the ROLLER and PlanHits seams, both of which
// were always correct. The defect lived in ChordTrackComposer's GRID emission
// site, which resolved the wrong sentinel, never resolved the rate sentinel and
// never scoped the jitter — so every seam-level test stayed green while the
// authored figure was silently replaced at render time.
//
// These tests therefore drive the GRID path end-to-end (composer.Compose with
// an authored progression on TrackParameters.Pattern + a BackingCardConfigSO
// on TrackParameters.Style) and assert on EMITTED notes, never on
// pre-emission variables — the BASS-WALK-1 verification lesson.
//
// Contract pinned (runtime/SSoT_Composer_Backing_Track.md §8, §8.5, §8.7):
//   Q1 concrete figure × concrete rate  — baseline
//   Q2 concrete figure × Random rate    — MUST be note-identical to Q1
//                                         (ArpeggioRate is inert for figures
//                                         that do not consume it, §8) and MUST
//                                         report null resolvedFigures (§8.5 R4)
//   Q3 Random figure   × concrete rate  — baseline figure sequence
//   Q4 Random figure   × Random rate    — MUST reproduce Q3's figure sequence
//                                         exactly (D-V1-RATE-STREAM=A) while
//                                         the arpeggio rates actually vary
//
// Shared fixtures: Dbg1Fixtures (SongOrchestratorKeyingTests.cs, same assembly).

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_ArticRateIndependenceTests
    {
        private const int Channel = 3;
        private const int Bpm = 120;
        private const int Seed = 11;

        // ------------------------------------------------------------------
        // Harness
        // ------------------------------------------------------------------

        private static BackingCardConfigSO Card(
            ChordExpressionType expr, ArpeggioRate rate,
            float rerollChance = 1f, int jitter = 0)
        {
            var c = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            c.name = $"Card_{expr}_{rate}";
            c.chordExpression = expr;
            c.arpeggioRate = rate;
            c.randomRerollChance = rerollChance;
            c.velocityJitter = jitter;
            return c;
        }

        /// <summary>Four-bar progression: enough events that a per-event
        /// hijack or a per-event rate roll is observable, and long enough
        /// windows that no figure degrades to Block for want of room.</summary>
        private static ChordProgressionData Prog()
            => Dbg1Fixtures.Progression("ArticRateProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Submediant, ChordQuality.Minor),
                (ScaleDegree.Subdominant, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

        /// <summary>One GRID-path render. Returns the emitted notes and the
        /// DBG-1 readback, which carries the resolved figure history.</summary>
        private static (List<(long tick, int note, int vel, long len)> notes,
                        ResolvedTrackChoice readback)
            RenderGrid(BackingCardConfigSO card)
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var composer = new ChordTrackComposer(settings, voicer: null);

            // Fresh Part per render: Compose consumes/clears part transients.
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                                   pattern: Prog(), style: card));

            ResolvedTrackChoice readback = null;
            var ctx = new MidiGenerator.GenContext
            {
                Settings = settings,
                rng = new System.Random(Seed),
                trackSeed = Seed,
                ReportResolved = r => readback = r,
            };

            var file = composer.Compose(part, part.Tracks[0], Bpm, Channel, ctx);
            return (Notes(file), readback);
        }

        private static List<(long tick, int note, int vel, long len)> Notes(MidiFile file)
            => file.GetNotes()
                   .Select(n => ((long)n.Time, (int)n.NoteNumber,
                                 (int)n.Velocity, (long)n.Length))
                   .OrderBy(n => n.Item1).ThenBy(n => n.Item2)
                   .ToList();

        // ------------------------------------------------------------------
        // Q1 vs Q2 — the reported defect. A concrete figure must be immune to
        // the rate knob (§8: "Ignored by all other expressions").
        // ------------------------------------------------------------------

        private static readonly ChordExpressionType[] RateInertFigures =
        {
            ChordExpressionType.Block,
            ChordExpressionType.PerBeat,
            ChordExpressionType.Offbeat,
            ChordExpressionType.Staccato,
            ChordExpressionType.BassUpperSplit,
            ChordExpressionType.Bossa,
        };

        [Test]
        public void ConcreteFigure_IsByteIdenticalUnderEveryRate(
            [ValueSource(nameof(RateInertFigures))] ChordExpressionType figure)
        {
            var baseline = RenderGrid(Card(figure, ArpeggioRate.PerBeat)).notes;

            foreach (var rate in new[] { ArpeggioRate.Eighth,
                                         ArpeggioRate.Sixteenth,
                                         ArpeggioRate.Random })
            {
                var probe = RenderGrid(Card(figure, rate)).notes;
                Assert.That(probe, Is.EqualTo(baseline),
                    $"{figure} must render identically at rate={rate}: " +
                    "ArpeggioRate is inert for figures that do not consume it " +
                    "(§8). A difference means the rate sentinel is reaching a " +
                    "figure-resolution branch it must not reach.");
            }
        }

        [Test]
        public void ConcreteFigure_WithRandomRate_ReportsNoResolvedFigures()
        {
            // §8.5 (CA-V1 clarification R4): a rate-only random render builds a
            // roller whose FIGURE history stays empty, so the readback reports
            // null. A non-null list here is the hijack, visible without audio.
            var readback = RenderGrid(
                Card(ChordExpressionType.Offbeat, ArpeggioRate.Random)).readback;

            Assert.That(readback, Is.Not.Null, "DBG-1 readback must be reported.");
            Assert.That(readback.resolvedFigures, Is.Null,
                "A fixed figure consumes zero figure draws (§8.5): the roller's " +
                "figure history must stay empty and report null.");
        }

        // ------------------------------------------------------------------
        // Q3 vs Q4 — stream orthogonality, at the composer rather than at the
        // roller (D-V1-RATE-STREAM=A).
        // ------------------------------------------------------------------

        [Test]
        public void RandomFigure_SequenceIsUnaffectedByTheRateKnob()
        {
            var fixedRate = RenderGrid(
                Card(ChordExpressionType.Random, ArpeggioRate.Eighth)).readback;
            var randomRate = RenderGrid(
                Card(ChordExpressionType.Random, ArpeggioRate.Random)).readback;

            Assert.That(fixedRate.resolvedFigures, Is.Not.Null.And.Not.Empty,
                "Random figure must report its roll history.");
            Assert.That(randomRate.resolvedFigures,
                        Is.EqualTo(fixedRate.resolvedFigures),
                "Toggling the rate sentinel must not shift a single figure roll: " +
                "the two axes draw from separate substreams (§8.5).");
        }

        // ------------------------------------------------------------------
        // The rate roll must actually be WIRED at the grid site — not merely
        // inert. Without this, a fix that simply drops the sentinel on the
        // floor would pass every test above.
        // ------------------------------------------------------------------

        [Test]
        public void ArpeggioFigure_ActuallyConsumesTheRateRoll()
        {
            var eighth = RenderGrid(
                Card(ChordExpressionType.ArpeggioUp, ArpeggioRate.Eighth)).notes;
            var rolled = RenderGrid(
                Card(ChordExpressionType.ArpeggioUp, ArpeggioRate.Random)).notes;

            Assert.That(rolled, Is.Not.EqualTo(eighth),
                "A rolled rate must reach the articulator. Identity with the " +
                "Eighth render means the sentinel leaked and degraded there " +
                "(§8.5 Degrade) instead of being resolved composer-side.");
        }

        // ------------------------------------------------------------------
        // §8.7 — the jitter axis must reach the grid site too.
        // ------------------------------------------------------------------

        [Test]
        public void VelocityJitter_IsAppliedOnTheGridPath()
        {
            var dry = RenderGrid(
                Card(ChordExpressionType.PerBeat, ArpeggioRate.Eighth,
                     jitter: 0)).notes;
            var wet = RenderGrid(
                Card(ChordExpressionType.PerBeat, ArpeggioRate.Eighth,
                     jitter: 16)).notes;

            Assert.That(wet.Select(n => n.tick).ToList(),
                        Is.EqualTo(dry.Select(n => n.tick).ToList()),
                "Jitter never touches timing (§8.7).");
            Assert.That(wet.Select(n => n.note).ToList(),
                        Is.EqualTo(dry.Select(n => n.note).ToList()),
                "Jitter never touches note indices (§8.7).");
            Assert.That(wet.Select(n => n.vel).ToList(),
                        Is.Not.EqualTo(dry.Select(n => n.vel).ToList()),
                "velocityJitter > 0 must change velocities on the grid path: " +
                "the composer scopes it per event and passes it to Emit (§8.7).");
        }

        [Test]
        public void GridRender_IsDeterministicUnderAPinnedSeed()
        {
            // The fix must not weaken SEED-1: same card, same seed => same bytes.
            var a = RenderGrid(Card(ChordExpressionType.Random, ArpeggioRate.Random,
                                    jitter: 8)).notes;
            var b = RenderGrid(Card(ChordExpressionType.Random, ArpeggioRate.Random,
                                    jitter: 8)).notes;
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
#endif
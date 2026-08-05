#if UNITY_EDITOR
// MGP-ALWTTT-BASS-BEND-1, step 3 — the composer-side legato bend pins,
// written AFTER steps 1 / 2a / 2b landed green. This suite photographs the
// three new pure seams and the new emission behaviour (carrier note + step
// pitch bend, D-BEND-GEST=A) at render-byte level.
//
// PIN SCOPE — read before editing:
//
// * BuildLegatoCarrierMap (D-BEND-GEST=A): the pure coalescing pass. The
//   all-(-1) map for legato-free plans IS the structural byte-identity
//   argument — if that pin moves, the SLAPFIG-2 canary
//   (GhostVocabulary_Render_IsDeterministic, in
//   BassTrackComposer_SelfPocketVocabularyTests) is the next thing to check.
// * ResolveLegatoGroupEndBeats: the declared carrier-gate law change —
//   identity without tails, last tail's end with them. A following
//   NON-legato hit is not a tail.
// * ResolveLegatoDeltaSemitones (D-BEND-DEG=A): degrees anchored to the
//   SCALE — the tonality decides each step's semitone size; whole-tone
//   fallback (offset * 2) for null/empty scales and off-scale pitch
//   classes (the silent-by-design deviation, on record in the seam doc).
// * Render pins: determinism, PitchBendEvent presence, the D-BEND-RESET=A
//   closing invariant (last bend = center), the no-attack law (fewer
//   note-ons than the slap-substituted pattern), and the anti-no-op pin —
//   a legato pattern MUST change the bytes vs the tail-dropped pattern.
//   The Ghost/GhostPop byte canary itself stays in the vocabulary suite.
//
// No orphan RENDER pin on purpose: the orphan law (index 0 => -1 =>
// attacked note + one warning per Compose) is pinned structurally at the
// carrier-map seam; a render pin would only add warning noise.

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_LegatoBendTests
    {
        // ------------------------------------------------------------------
        // Shorthand
        // ------------------------------------------------------------------

        private const BasslineCardConfigSO.SelfPocketStep Slap =
            BasslineCardConfigSO.SelfPocketStep.Slap;
        private const BasslineCardConfigSO.SelfPocketStep Pop =
            BasslineCardConfigSO.SelfPocketStep.Pop;
        private const BasslineCardConfigSO.SelfPocketStep Rest =
            BasslineCardConfigSO.SelfPocketStep.Rest;
        private const BasslineCardConfigSO.SelfPocketStep Ghost =
            BasslineCardConfigSO.SelfPocketStep.Ghost;
        private const BasslineCardConfigSO.SelfPocketStep GhostPop =
            BasslineCardConfigSO.SelfPocketStep.GhostPop;
        private const BasslineCardConfigSO.SelfPocketStep HammerOn =
            BasslineCardConfigSO.SelfPocketStep.HammerOn;
        private const BasslineCardConfigSO.SelfPocketStep PullOff =
            BasslineCardConfigSO.SelfPocketStep.PullOff;

        private const BasslineCardConfigSO.SelfPocketSubdivision Beat =
            BasslineCardConfigSO.SelfPocketSubdivision.Beat;
        private const BasslineCardConfigSO.SelfPocketSubdivision HalfBeat =
            BasslineCardConfigSO.SelfPocketSubdivision.HalfBeat;

        /// <summary>Plans through the REAL production path
        /// (BuildSelfPocketPlan), so the carrier map is exercised over the
        /// same PocketHit lists the emission loop consumes.</summary>
        private static List<BassTrackComposer.PocketHit> Plan(
            double start, double len,
            BasslineCardConfigSO.SelfPocketSubdivision sub,
            params BasslineCardConfigSO.SelfPocketStep[] pattern)
            => BassTrackComposer.BuildSelfPocketPlan(
                start, len, sub, pattern, eventVelocity: 100);

        private static int[] Map(IReadOnlyList<BassTrackComposer.PocketHit> p)
            => BassTrackComposer.BuildLegatoCarrierMap(p);

        // C major in the composer's own scaleNames shape (NoteName = pitch
        // class; the composer builds this array from GetNotesFromScale).
        private static readonly NoteName[] CMajor =
        {
            NoteName.C, NoteName.D, NoteName.E, NoteName.F,
            NoteName.G, NoteName.A, NoteName.B,
        };

        private static int Delta(int from, int degrees, NoteName[] scale)
            => BassTrackComposer.ResolveLegatoDeltaSemitones(
                from, degrees, scale);

        // MIDI anchors (pitch class is all the seam reads; 60 = a C).
        private const int C = 60, Db = 61, D = 62, E = 64, F = 65, B = 71;

        // ------------------------------------------------------------------
        // BuildLegatoCarrierMap (D-BEND-GEST=A / D-BEND-ANCHOR=A)
        // ------------------------------------------------------------------

        [Test]
        public void CarrierMap_NoLegatoClasses_IsAllMinusOne()
        {
            // The structural byte-identity pin: a plan without legato
            // classes maps to all -1, so the emission loop is line-for-line
            // the SLAPFIG-2 loop and the writer never runs.
            var plan = Plan(0.0, 4.0, Beat, Slap, Ghost, Pop, GhostPop);
            Assert.That(plan.Count, Is.EqualTo(4), "fixture guard");
            Assert.That(Map(plan), Is.EqualTo(new[] { -1, -1, -1, -1 }));
        }

        [Test]
        public void CarrierMap_LegatoAfterNote_PointsToIt()
        {
            var plan = Plan(0.0, 2.0, Beat, Slap, HammerOn);
            Assert.That(Map(plan), Is.EqualTo(new[] { -1, 0 }));
        }

        [Test]
        public void CarrierMap_Chain_CollapsesOntoTheRootCarrier()
        {
            // Both tails point to the SAME root carrier — the chain is one
            // group with one gate stretch and one reset (D-BEND-RESET=A
            // coalescing feeds off this shape).
            var plan = Plan(0.0, 3.0, Beat, Slap, HammerOn, PullOff);
            Assert.That(Map(plan), Is.EqualTo(new[] { -1, 0, 0 }));
        }

        [Test]
        public void CarrierMap_LegatoAtIndexZero_IsOrphan()
        {
            // Nothing sounds before it => -1 => the emission branch
            // degrades it to an attacked note (warn once per Compose).
            var plan = Plan(0.0, 2.0, Beat, HammerOn, Slap);
            Assert.That(Map(plan), Is.EqualTo(new[] { -1, -1 }));
        }

        [Test]
        public void CarrierMap_LegatoAfterOrphan_PointsToTheOrphan()
        {
            // The orphan DID emit its own note-on, so it is a legitimate
            // carrier for the tail that follows it.
            var plan = Plan(0.0, 2.0, Beat, HammerOn, PullOff);
            Assert.That(Map(plan), Is.EqualTo(new[] { -1, 0 }));
        }

        // ------------------------------------------------------------------
        // ResolveLegatoGroupEndBeats (the declared carrier-gate law change)
        // ------------------------------------------------------------------

        [Test]
        public void GroupEnd_NoTail_IsTheHitsOwnPlannedEnd()
        {
            // Identity case — and a following NON-legato hit (the Ghost) is
            // not a tail, so the Slap's group is just itself.
            var plan = Plan(0.0, 2.0, Beat, Slap, Ghost);
            var map = Map(plan);
            Assert.That(map, Is.EqualTo(new[] { -1, -1 }), "fixture guard");

            Assert.That(
                BassTrackComposer.ResolveLegatoGroupEndBeats(plan, map, 0),
                Is.EqualTo(plan[0].startBeats + plan[0].lenBeats));
            Assert.That(
                BassTrackComposer.ResolveLegatoGroupEndBeats(plan, map, 1),
                Is.EqualTo(plan[1].startBeats + plan[1].lenBeats),
                "identity holds for the ghost's click gate too");
        }

        [Test]
        public void GroupEnd_WithTails_IsTheLastTailsEnd()
        {
            var plan = Plan(0.0, 3.0, Beat, Slap, HammerOn, PullOff);
            var map = Map(plan);
            double end =
                BassTrackComposer.ResolveLegatoGroupEndBeats(plan, map, 0);

            Assert.That(end,
                Is.EqualTo(plan[2].startBeats + plan[2].lenBeats),
                "the group ends where its LAST tail ends");
            Assert.That(end,
                Is.GreaterThan(plan[0].startBeats + plan[0].lenBeats),
                "the carrier's gate stretches — the declared law change");
        }

        // ------------------------------------------------------------------
        // ResolveLegatoDeltaSemitones (D-BEND-DEG=A)
        // ------------------------------------------------------------------

        [Test]
        public void Delta_MajorScale_TheTonalityDecidesTheStepSize()
        {
            // THE reason for D-BEND-DEG=A: +1 degree is a whole step from
            // the tonic but a HALF step from the 3rd — the interval follows
            // the key instead of fighting it.
            Assert.That(Delta(C, +1, CMajor), Is.EqualTo(2), "C -> D");
            Assert.That(Delta(E, +1, CMajor), Is.EqualTo(1), "E -> F");
        }

        [Test]
        public void Delta_MinusOneDegree_IsSymmetric()
        {
            Assert.That(Delta(D, -1, CMajor), Is.EqualTo(-2), "D -> C");
            Assert.That(Delta(F, -1, CMajor), Is.EqualTo(-1), "F -> E");
        }

        [Test]
        public void Delta_OctaveCrossings_WalkThroughTheWrap()
        {
            Assert.That(Delta(B, +1, CMajor), Is.EqualTo(+1),
                "B -> C above: the pc walk wraps upward");
            Assert.That(Delta(C, -1, CMajor), Is.EqualTo(-1),
                "C -> B below: and downward");
        }

        [Test]
        public void Delta_MultiDegreeOffsets_AccumulateScaleSteps()
        {
            Assert.That(Delta(C, +2, CMajor), Is.EqualTo(4), "C -> E");
            Assert.That(Delta(E, -2, CMajor), Is.EqualTo(-4), "E -> C");
            Assert.That(Delta(C, +7, CMajor), Is.EqualTo(12),
                "a full scale walk is exactly one octave");
        }

        [Test]
        public void Delta_NullOrEmptyScale_FallsBackToWholeTones()
        {
            Assert.That(Delta(C, +1, null), Is.EqualTo(2));
            Assert.That(Delta(C, -3, null), Is.EqualTo(-6));
            Assert.That(Delta(C, +1, new NoteName[0]), Is.EqualTo(2));
        }

        [Test]
        public void Delta_OffScalePitchClass_FallsBackToWholeTones()
        {
            // C# is not a C-major member (borrowed / requalified chord
            // tone): offset * 2, silent by design — the recorded deviation
            // from warn-max (a data-dependent per-hit condition, not a
            // config degrade).
            Assert.That(Delta(Db, +1, CMajor), Is.EqualTo(2));
            Assert.That(Delta(Db, -2, CMajor), Is.EqualTo(-4));
        }

        [Test]
        public void Delta_ZeroOffset_IsZero()
        {
            Assert.That(Delta(C, 0, CMajor), Is.EqualTo(0));
            Assert.That(Delta(C, 0, null), Is.EqualTo(0),
                "the zero fast path precedes the null-scale fallback");
        }

        // ------------------------------------------------------------------
        // Render pins — the new emission, at byte level
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO Card(
            string name,
            params BasslineCardConfigSO.SelfPocketStep[] pattern)
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.name = name;
            c.pocketMode = BasslineCardConfigSO.PocketCouplingMode.SelfPocket;
            c.selfPocketSubdivision = HalfBeat;
            c.selfPocketPattern =
                new List<BasslineCardConfigSO.SelfPocketStep>(pattern);
            return c;
        }

        /// <summary>[Slap, HammerOn, Rest, Rest] on the HalfBeat grid over
        /// the 2-event fixture progression: each chord-event window plans a
        /// carrier Slap and ONE hammered tail — two gestures per render, no
        /// orphans (so no warnings).</summary>
        private static BasslineCardConfigSO LegatoCard()
            => Card("LegatoBendCard", Slap, HammerOn, Rest, Rest);

        /// <summary>The same figure with the tail ATTACKED instead of
        /// hammered — the "what BEND-1 replaced" reference render.</summary>
        private static BasslineCardConfigSO SlapSubstitutedCard()
            => Card("SlapSubstitutedCard", Slap, Slap, Rest, Rest);

        /// <summary>The same figure with the tail DROPPED — the reference
        /// for the anti-no-op pin (if the tail emitted neither a note nor
        /// a gesture nor a gate stretch, the renders would collide).</summary>
        private static BasslineCardConfigSO TailDroppedCard()
            => Card("TailDroppedCard", Slap, Rest, Rest, Rest);

        private static MidiFile RenderBassStem(
            BasslineCardConfigSO card, int seed)
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("LegatoBendProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                    pattern: prog, style: card));
            var render = Dbg1Fixtures.Render(orch, part, null, seed);
            render.stemsByMusician.TryGetValue(
                new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Bassline),
                out var stem);
            Assert.That(stem, Is.Not.Null);
            return stem;
        }

        // Absolute-tick flattening — the PitchBendWriterTests idiom,
        // duplicated locally (house style: fixtures shared, helpers local).
        private static List<(long tick, MidiEvent ev)> Flatten(MidiFile file)
        {
            var chunk = file.GetTrackChunks().First();
            var list = new List<(long, MidiEvent)>();
            long acc = 0;
            foreach (var ev in chunk.Events)
            {
                acc += ev.DeltaTime;
                list.Add((acc, ev));
            }
            return list;
        }

        private static List<(long tick, ushort value)> Bends(MidiFile file)
            => Flatten(file)
                .Where(t => t.ev is PitchBendEvent)
                .Select(t => (t.tick, ((PitchBendEvent)t.ev).PitchValue))
                .ToList();

        private static int NoteOnCount(MidiFile file)
            => Flatten(file)
                .Count(t => t.ev is NoteOnEvent n && n.Velocity > 0);

        [Test]
        public void Render_WithLegato_IsDeterministic()
        {
            var h1 = Dbg1Fixtures.Fnv(RenderBassStem(LegatoCard(), seed: 7));
            var h2 = Dbg1Fixtures.Fnv(RenderBassStem(LegatoCard(), seed: 7));
            Assert.That(h1, Is.EqualTo(h2),
                "same seed + same legato pattern => same bytes — gestures " +
                "included (BuildLegatoCarrierMap and the writer are pure; " +
                "zero new ctx.rng draws)");
        }

        [Test]
        public void Render_WithLegato_EmitsBends_AndClosesAtCenter()
        {
            var stem = RenderBassStem(LegatoCard(), seed: 7);
            var bends = Bends(stem);

            Assert.That(bends, Is.Not.Empty,
                "the hammered tails must reach the bytes as PitchBendEvents");
            Assert.That(bends.Last().value,
                Is.EqualTo(PitchBendWriter.Center),
                "D-BEND-RESET=A closing invariant: the channel is never " +
                "left detuned past its last gesture");

            // ForceAllChannel runs AFTER the writer (D-BEND-EMIT=B order):
            // bends carry the same channel as the notes, for free.
            var noteChannel = Flatten(stem)
                .Select(t => t.ev).OfType<NoteOnEvent>().First().Channel;
            Assert.That(Flatten(stem).Select(t => t.ev)
                    .OfType<PitchBendEvent>()
                    .All(e => e.Channel == noteChannel),
                Is.True);
        }

        [Test]
        public void Render_LegatoTails_DoNotAttack_FewerNoteOns()
        {
            // The no-attack law, empirically: the SAME figure with the tail
            // hammered emits strictly fewer note-ons than with it slapped.
            int legato = NoteOnCount(RenderBassStem(LegatoCard(), seed: 7));
            int attacked = NoteOnCount(
                RenderBassStem(SlapSubstitutedCard(), seed: 7));

            Assert.That(legato, Is.LessThan(attacked));
            Assert.That((legato, attacked), Is.EqualTo((2, 4)),
                "pinned fixture arithmetic: 2 chord events x (1 carrier vs " +
                "carrier + attacked tail)");
        }

        [Test]
        public void Render_LegatoPattern_ChangesTheBytes_NeverASilentNoOp()
        {
            // The anti-no-op pin (the canary's counterpart): dropping the
            // tail outright must NOT produce the same bytes as hammering
            // it. If the two ever collide, the gesture (bend + gate
            // stretch) silently vanished from the render. The Ghost/GhostPop
            // byte-identity canary itself lives in
            // BassTrackComposer_SelfPocketVocabularyTests and must stay
            // green alongside this suite.
            Assert.That(
                Dbg1Fixtures.Fnv(RenderBassStem(LegatoCard(), seed: 7)),
                Is.Not.EqualTo(
                    Dbg1Fixtures.Fnv(RenderBassStem(TailDroppedCard(), seed: 7))));
        }
    }
}
#endif
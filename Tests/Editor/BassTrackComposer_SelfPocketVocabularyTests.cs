#if UNITY_EDITOR
// MGP-ALWTTT-BASS-BEND-1, step 1 (D-PIN-FIRST=A) — the EditMode pins
// SLAPFIG-2 / 2b shipped without. Written BEFORE the bend work touches the
// emission branch, so the confirmed musical gain (Ghost / GhostPop) and the
// per-class laws are photographed while they are still the accepted baseline.
//
// PIN SCOPE — read before editing:
//
// * Ghost / GhostPop are pinned down to RENDER BYTES (orchestrator gates).
//   BEND-1's invariant says they must not change; these pins enforce it.
// * HammerOn / PullOff are pinned at the SEAM level ONLY (velocity law, gate
//   ceiling, ResolveOffsetNote, plan carriage). Their render bytes are
//   deliberately NOT pinned: BEND-1 exists to change that emission (carrier
//   note + step pitch bend, D-BEND-GEST=A). Pinning bytes scheduled for
//   demolition would just be churn.
// * hammerOffsetSemitones / pullOffsetSemitones defaults are pinned HERE as
//   the SLAPFIG-2 baseline; the D-BEND-DEG=A rename (semitones -> degrees)
//   will update these pins deliberately in the same diff that renames the
//   fields. A pin update inside the renaming batch documents intent; a
//   silent survival of the old pin would be drift.
//
// Laws covered (SLAPFIG-2 / 2b, doc diffs = de-facto governed text for
// §3.7.2 / §3.7.3):
// - D-SF2-VEL=B  : Slap/Pop = event velocity + additive boost (v1 verbatim);
//                  Ghost/GhostPop/HammerOn/PullOff = event velocity × class
//                  factor, no boosts; clamp 1..127; defensive fall-through
//                  to the slap law.
// - D-SF2-GATE=B : ghost classes take tuning.ghostGateBeats; every other
//                  class keeps PocketMaxGateBeats. min(gap, window, ceiling)
//                  law unchanged.
// - D-SF2-PITCH=A: the plan stays PITCH-FREE; ResolveOffsetNote is the pure
//                  call-site law for the offset classes (ceiling fold -12,
//                  floor fold +12, clamp last).
// - D-SF2B-TUNE=A: numbers live on the card; SelfPocketTuning.FromCard is a
//                  verbatim copy, FromCard(null) == Default.
// - Enum append-only: Ghost=3, GhostPop=4, HammerOn=5, PullOff=6,
//                  QuarterBeat=2.

using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_SelfPocketVocabularyTests
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
        private const BasslineCardConfigSO.SelfPocketSubdivision QuarterBeat =
            BasslineCardConfigSO.SelfPocketSubdivision.QuarterBeat;

        private static readonly BassTrackComposer.SelfPocketTuning Tun =
            BassTrackComposer.SelfPocketTuning.Default;

        private static int Vel(
            BasslineCardConfigSO.SelfPocketStep step, int eventVelocity,
            int slapBoost = 0, int popBoost = 0)
            => BassTrackComposer.ResolveSelfPocketVelocity(
                step, eventVelocity, slapBoost, popBoost, Tun);

        private static List<BassTrackComposer.PocketHit> Plan(
            double start, double len,
            BasslineCardConfigSO.SelfPocketSubdivision sub,
            BasslineCardConfigSO.SelfPocketStep[] pattern,
            int vel = 100, int slapBoost = 0, int popBoost = 0,
            BassTrackComposer.SelfPocketTuning? tuning = null)
            => BassTrackComposer.BuildSelfPocketPlan(
                start, len, sub, pattern, vel, slapBoost, popBoost, tuning);

        // ------------------------------------------------------------------
        // Enum + card surface (append-only serialization pins)
        // ------------------------------------------------------------------

        [Test]
        public void Enums_Slapfig2Members_ValuesArePinnedAppendOnly()
        {
            Assert.That((int)Ghost, Is.EqualTo(3));
            Assert.That((int)GhostPop, Is.EqualTo(4));
            Assert.That((int)HammerOn, Is.EqualTo(5));
            Assert.That((int)PullOff, Is.EqualTo(6));
            Assert.That((int)QuarterBeat, Is.EqualTo(2));
        }

        [Test]
        public void Card_Slapfig2Fields_DefaultsArePinned()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();

            // BEND-1 (D-BEND-DEG=A): the SLAPFIG-2 semitone fields were
            // renamed to SCALE-DEGREE offsets in this batch, exactly as the
            // previous version of this pin announced. [FormerlySerializedAs]
            // covers the (empty) asset surface; defaults are the scale
            // neighbours.
            Assert.That(card.hammerOffsetDegrees, Is.EqualTo(1));
            Assert.That(card.pullOffsetDegrees, Is.EqualTo(-1));

            // SLAPFIG-2b tuning defaults (card is the number's home,
            // D-SF2B-TUNE=A).
            Assert.That(card.ghostVelocityFactor, Is.EqualTo(0.60f));
            Assert.That(card.ghostPopVelocityFactor, Is.EqualTo(0.50f));
            Assert.That(card.hammerOnVelocityFactor, Is.EqualTo(0.60f));
            Assert.That(card.pullOffVelocityFactor, Is.EqualTo(0.55f));
            Assert.That(card.ghostGateBeats, Is.EqualTo(0.10f));
        }

        [Test]
        public void Tuning_FromCard_IsAVerbatimCopy_AndNullFallsBackToDefault()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            card.ghostVelocityFactor = 0.42f;
            card.ghostPopVelocityFactor = 0.33f;
            card.hammerOnVelocityFactor = 0.77f;
            card.pullOffVelocityFactor = 0.88f;
            card.ghostGateBeats = 0.25f;

            var t = BassTrackComposer.SelfPocketTuning.FromCard(card);
            Assert.That(t.ghost, Is.EqualTo(0.42f));
            Assert.That(t.ghostPop, Is.EqualTo(0.33f));
            Assert.That(t.hammerOn, Is.EqualTo(0.77f));
            Assert.That(t.pullOff, Is.EqualTo(0.88f));
            Assert.That(t.ghostGateBeats,
                Is.EqualTo((double)card.ghostGateBeats));

            var d = BassTrackComposer.SelfPocketTuning.FromCard(null);
            Assert.That(d.ghost, Is.EqualTo(Tun.ghost));
            Assert.That(d.ghostPop, Is.EqualTo(Tun.ghostPop));
            Assert.That(d.hammerOn, Is.EqualTo(Tun.hammerOn));
            Assert.That(d.pullOff, Is.EqualTo(Tun.pullOff));
            Assert.That(d.ghostGateBeats, Is.EqualTo(Tun.ghostGateBeats));
        }

        // ------------------------------------------------------------------
        // PocketHit constructors — class carriage + pop-domain derivation
        // ------------------------------------------------------------------

        [Test]
        public void PocketHit_BoolCtor_MapsOntoSlapAndPop()
        {
            var s = new BassTrackComposer.PocketHit(0, 1, 100, pop: false);
            var p = new BassTrackComposer.PocketHit(0, 1, 100, pop: true);
            Assert.That(s.articulation, Is.EqualTo(Slap));
            Assert.That(s.pop, Is.False);
            Assert.That(p.articulation, Is.EqualTo(Pop));
            Assert.That(p.pop, Is.True);
        }

        [Test]
        public void PocketHit_ClassCtor_PopFlagIsThePitchDomain()
        {
            // Pop domain = Pop and GhostPop (sound +12-folded at the call
            // site); everything else sounds in the selected-note domain.
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, Pop).pop,
                Is.True);
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, GhostPop).pop,
                Is.True);
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, Slap).pop,
                Is.False);
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, Ghost).pop,
                Is.False);
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, HammerOn).pop,
                Is.False);
            Assert.That(new BassTrackComposer.PocketHit(0, 1, 100, PullOff).pop,
                Is.False);
        }

        // ------------------------------------------------------------------
        // ResolveSelfPocketVelocity — the D-SF2-VEL=B two-law split
        // ------------------------------------------------------------------

        [Test]
        public void Velocity_SlapAndPop_KeepTheV1AdditiveBoostLawVerbatim()
        {
            Assert.That(Vel(Slap, 90, slapBoost: -10, popBoost: 25),
                Is.EqualTo(80));
            Assert.That(Vel(Pop, 90, slapBoost: -10, popBoost: 25),
                Is.EqualTo(115));
            // Clamps.
            Assert.That(Vel(Slap, 120, slapBoost: 64), Is.EqualTo(127));
            Assert.That(Vel(Pop, 10, popBoost: -64), Is.EqualTo(1));
        }

        [Test]
        public void Velocity_NewClasses_AreAFactorOfEventVelocity_NoBoosts()
        {
            // Defaults: ghost .60, ghostPop .50, hammer .60, pull .55.
            Assert.That(Vel(Ghost, 100), Is.EqualTo(60));
            Assert.That(Vel(GhostPop, 100), Is.EqualTo(50));
            Assert.That(Vel(HammerOn, 100), Is.EqualTo(60));
            Assert.That(Vel(PullOff, 100), Is.EqualTo(55));

            // Boost-immunity: the gig's (+64,+64) saturation finding — hot
            // boosts must NOT drag the quiet classes along.
            Assert.That(Vel(Ghost, 100, slapBoost: 64, popBoost: 64),
                Is.EqualTo(60));
            Assert.That(Vel(GhostPop, 100, slapBoost: 64, popBoost: 64),
                Is.EqualTo(50));
            Assert.That(Vel(HammerOn, 100, slapBoost: 64, popBoost: 64),
                Is.EqualTo(60));
            Assert.That(Vel(PullOff, 100, slapBoost: 64, popBoost: 64),
                Is.EqualTo(55));
        }

        [Test]
        public void Velocity_FactorLaw_ClampsLowTo1()
        {
            // round(1 * .5) = 1 already, but a tiny event velocity with a
            // small factor must never emit velocity 0 (note-off in disguise).
            Assert.That(Vel(GhostPop, 1), Is.GreaterThanOrEqualTo(1));
            Assert.That(Vel(Ghost, 1), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Velocity_UnknownOrRest_FallsThroughToTheSlapLaw()
        {
            // Defensive fall-through pin: Rest is never planned, but the law
            // says any non-matched member takes the slap branch.
            Assert.That(Vel(Rest, 90, slapBoost: 7), Is.EqualTo(97));
            Assert.That(
                Vel((BasslineCardConfigSO.SelfPocketStep)99, 90, slapBoost: 7),
                Is.EqualTo(97));
        }

        // ------------------------------------------------------------------
        // ResolveSelfPocketGateCeiling — D-SF2-GATE=B
        // ------------------------------------------------------------------

        [Test]
        public void GateCeiling_GhostClassesTakeTheClickCeiling_OthersKeepPocket()
        {
            double G(BasslineCardConfigSO.SelfPocketStep s)
                => BassTrackComposer.ResolveSelfPocketGateCeiling(s, Tun);

            Assert.That(G(Ghost), Is.EqualTo(Tun.ghostGateBeats));
            Assert.That(G(GhostPop), Is.EqualTo(Tun.ghostGateBeats));

            Assert.That(G(Slap),
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
            Assert.That(G(Pop),
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
            Assert.That(G(HammerOn),
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
            Assert.That(G(PullOff),
                Is.EqualTo(BassTrackComposer.PocketMaxGateBeats));
        }

        // ------------------------------------------------------------------
        // ResolveOffsetNote — D-SF2-PITCH=A pure pitch law
        // ------------------------------------------------------------------

        [Test]
        public void OffsetNote_NoFoldNeeded_IsSelectedPlusOffset()
        {
            // NOTE: NoteNumber is a SevenBitNumber (DryWetMidi struct), not
            // an int. NUnit's Is.EqualTo falls back to Equals for non-builtin
            // numeric types, so an uncast comparison fails with the baffling
            // "Expected: 26 But was: 26". Every actual is cast to int here.
            int baseN = NoteTheory.Get(NoteName.C, 2).NoteNumber;
            Assert.That((int)BassTrackComposer.ResolveOffsetNote(
                    NoteName.C, 2, +2, ceiling: 127).NoteNumber,
                Is.EqualTo(baseN + 2));
            Assert.That((int)BassTrackComposer.ResolveOffsetNote(
                    NoteName.C, 2, -2, ceiling: 127).NoteNumber,
                Is.EqualTo(baseN - 2));
        }

        [Test]
        public void OffsetNote_AboveCeiling_FoldsDownByOctaves()
        {
            int baseN = NoteTheory.Get(NoteName.C, 2).NoteNumber;
            // Ceiling right AT the selected note: +2 lands above, one -12
            // fold brings it under.
            var n = BassTrackComposer.ResolveOffsetNote(
                NoteName.C, 2, +2, ceiling: baseN);
            Assert.That((int)n.NoteNumber, Is.EqualTo(baseN + 2 - 12),
                "the interval's pitch class survives; the register folds");
        }

        [Test]
        public void OffsetNote_BelowMidiFloor_FoldsUpNeverClampDistorts()
        {
            // C-1 is MIDI 0 in DryWetMidi's octave convention. A -2 offset
            // from the floor folds UP an octave (pitch class preserved)
            // rather than clamping to 0 (which would distort the interval).
            int floorC = NoteTheory.Get(NoteName.C, -1).NoteNumber;
            Assert.That(floorC, Is.EqualTo(0), "convention guard");

            var n = BassTrackComposer.ResolveOffsetNote(
                NoteName.C, -1, -2, ceiling: 127);
            Assert.That((int)n.NoteNumber, Is.EqualTo(0 - 2 + 12));
        }

        // ------------------------------------------------------------------
        // BuildSelfPocketPlan — the SLAPFIG-2/2b surface
        // ------------------------------------------------------------------

        [Test]
        public void Plan_CarriesTheArticulationClass()
        {
            var plan = Plan(0.0, 4.0, Beat,
                new[] { Slap, Ghost, HammerOn, GhostPop });

            Assert.That(plan.Select(h => h.articulation),
                Is.EqualTo(new[] { Slap, Ghost, HammerOn, GhostPop }));
            Assert.That(plan.Select(h => h.pop),
                Is.EqualTo(new[] { false, false, false, true }),
                "pop flag derives from the class's pitch domain");
        }

        [Test]
        public void Plan_QuarterBeat_IsASixteenthGrid()
        {
            var plan = Plan(0.0, 1.0, QuarterBeat, new[] { Slap });

            Assert.That(plan.Select(h => h.startBeats),
                Is.EqualTo(new[] { 0.0, 0.25, 0.5, 0.75 }));
            Assert.That(plan.All(h => h.lenBeats == 0.25), Is.True,
                "gap to next planned hit (0.25) beats the 0.5 ceiling");
        }

        [Test]
        public void Plan_GhostClasses_TakeTheClickGateInThePlan()
        {
            // [Ghost] on the Beat grid over [0,2): gaps are 1.0, window is
            // ample — the 0.10 ghost ceiling must win (D-SF2-GATE=B).
            var plan = Plan(0.0, 2.0, Beat, new[] { Ghost });
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan.All(
                h => h.lenBeats == Tun.ghostGateBeats), Is.True);

            // Mixed pattern: only the ghost hits shrink.
            var mixed = Plan(0.0, 2.0, Beat, new[] { Slap, GhostPop });
            Assert.That(mixed[0].lenBeats, Is.EqualTo(0.5),
                "Slap keeps the POCKET-1 ceiling");
            Assert.That(mixed[1].lenBeats, Is.EqualTo(Tun.ghostGateBeats),
                "GhostPop takes the click ceiling");
        }

        [Test]
        public void Plan_V1SlapPopPatterns_AreTuningInvariant()
        {
            // Byte-identity anchor of D-SF2-VEL=B / D-SF2-GATE=B: a pattern
            // that only uses the v1 classes must produce the SAME plan with
            // any tuning (the tuning only feeds the new classes).
            var exotic = new BassTrackComposer.SelfPocketTuning(
                0.1f, 0.1f, 0.9f, 0.9f, 0.9);

            var a = Plan(0.0, 4.0, HalfBeat, new[] { Slap, Pop, Rest },
                vel: 96, slapBoost: 5, popBoost: -5);
            var b = Plan(0.0, 4.0, HalfBeat, new[] { Slap, Pop, Rest },
                vel: 96, slapBoost: 5, popBoost: -5, tuning: exotic);

            Assert.That(
                a.Select(h => (h.startBeats, h.lenBeats, h.velocity,
                               h.articulation)),
                Is.EqualTo(
                b.Select(h => (h.startBeats, h.lenBeats, h.velocity,
                               h.articulation))));
        }

        [Test]
        public void Plan_NewClassVelocities_UseTheFactorLawInsideThePlan()
        {
            var plan = Plan(0.0, 4.0, Beat,
                new[] { Ghost, GhostPop, HammerOn, PullOff },
                vel: 100, slapBoost: 64, popBoost: 64);

            Assert.That(plan.Select(h => h.velocity),
                Is.EqualTo(new[] { 60, 50, 60, 55 }),
                "factors of the EVENT velocity; boosts never leak in");
        }

        // ------------------------------------------------------------------
        // Orchestrator gates — Ghost/GhostPop render bytes (the BEND-1
        // "do not touch" invariant, photographed)
        // ------------------------------------------------------------------

        private static BasslineCardConfigSO GhostCard()
        {
            var c = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            c.name = "GhostVocabCard";
            c.pocketMode = BasslineCardConfigSO.PocketCouplingMode.SelfPocket;
            c.selfPocketSubdivision = HalfBeat;
            c.selfPocketPattern =
                new List<BasslineCardConfigSO.SelfPocketStep>
                    { Slap, Ghost, Pop, GhostPop };
            return c;
        }

        private static ulong RenderFnv(BasslineCardConfigSO card, int seed)
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("GhostVocabProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Bassline, inst,
                    pattern: prog, style: card));
            return Dbg1Fixtures.Fnv(
                Dbg1Fixtures.Render(orch, part, null, seed).merged);
        }

        [Test]
        public void GhostVocabulary_Render_IsDeterministic()
        {
            var h1 = RenderFnv(GhostCard(), seed: 7);
            var h2 = RenderFnv(GhostCard(), seed: 7);
            Assert.That(h1, Is.EqualTo(h2),
                "same seed + same ghost vocabulary => same bytes. This hash " +
                "is BEND-1's canary: Ghost/GhostPop emission must survive " +
                "the batch byte-identical.");
        }

        [Test]
        public void GhostVocabulary_ChangesTheRender_VsV1SlapPop()
        {
            var v1 = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            v1.name = "V1Card";
            v1.pocketMode = BasslineCardConfigSO.PocketCouplingMode.SelfPocket;
            v1.selfPocketSubdivision = HalfBeat;
            v1.selfPocketPattern =
                new List<BasslineCardConfigSO.SelfPocketStep>
                    { Slap, Slap, Pop, Pop };

            Assert.That(RenderFnv(GhostCard(), seed: 7),
                Is.Not.EqualTo(RenderFnv(v1, seed: 7)),
                "the ghost classes are audible in the bytes — the confirmed " +
                "musical gain is not a no-op");
        }

        // NOTE (deliberate absence): no render-byte pin for HammerOn /
        // PullOff. Their emission is the surface BEND-1 replaces (carrier
        // note + step pitch bend); the seam pins above are their protection
        // until the new emission lands with its own byte pins.
    }
}
#endif
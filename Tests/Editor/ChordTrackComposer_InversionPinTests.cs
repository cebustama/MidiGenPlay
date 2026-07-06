#if UNITY_EDITOR
// CQ-A1-OBJ2 — per-chord inversion voicing hint (pin) tests.
//
// Targets two internal seams via Runtime/AssemblyInfo.cs:
//
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]
//
//   1. BasicVoiceLeadingVoicer.GeneratePcCandidates — the pin's enforcement
//      site (D0=A pin semantics; D2b=a out-of-range value => unset), tested
//      without any MIDIInstrumentSO fixture (mirrors the
//      TryDirectionalFirstChordCore seam approach of
//      ChordTrackComposer_DirectionalFirstChordTests).
//   2. ChordTrackComposer.ResolveInversionPin — the composer-side per-position
//      resolver (D2a=a sticky-per-position: no repeat parameter by design).
//
// D3 precedence (the §6 directional modulation hint wins on the render's very
// first chord) is structural in both render loops: when
// TryDirectionalFirstChord(Core) yields a voicing, VoiceChord — and therefore
// the pin — is never invoked for that chord. The combined-hint test below
// asserts both halves of that guarantee at the seam level.
//
// Decisions covered: D0=A (pin, not bias), D1=A (inversion index, not bass
// pitch-class), D2=A (per-chord scope), D2a=a (sticky-per-position),
// D2b=a (out-of-range value => unset, never clamped), D3=A (§6 wins on the
// render's first chord). See runtime/SSoT_Composer_Backing_Track.md §7.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using UnityEngine;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_InversionPinTests
    {
        // C major triad (root position): C E G
        private static NoteName[] CMajorPcs() => new[]
        {
            NoteName.C, NoteName.E, NoteName.G
        };

        // C7 (root position): C E G Bb
        private static NoteName[] CSeventhPcs() => new[]
        {
            NoteName.C, NoteName.E, NoteName.G, NoteName.ASharp
        };

        private static VoiceLeadingConfig Cfg(bool inversions = true, bool drop2 = true)
        {
            var cfg = ScriptableObject.CreateInstance<VoiceLeadingConfig>();
            cfg.useInversions = inversions;
            cfg.useDrop2 = drop2;
            return cfg;
        }

        private static List<NoteName[]> Candidates(
            NoteName[] pcs, VoiceLeadingConfig cfg, int? pin = null) =>
            BasicVoiceLeadingVoicer.GeneratePcCandidates(pcs, cfg, pin).ToList();

        private static void AssertPcs(NoteName[] actual, params NoteName[] expected) =>
            Assert.That(actual, Is.EqualTo(expected));

        // ------------------------------------------------------------------
        // Unset pin — bit-identical baseline (mirrors §6.3 / §7.2)
        // ------------------------------------------------------------------

        [Test]
        public void Unset_Triad_YieldsBaselineCandidateSet()
        {
            // Pre-pin behavior: root, 1st inversion, 2nd inversion, drop-2.
            var cands = Candidates(CMajorPcs(), Cfg());

            Assert.That(cands.Count, Is.EqualTo(4));
            AssertPcs(cands[0], NoteName.C, NoteName.E, NoteName.G);   // root
            AssertPcs(cands[1], NoteName.E, NoteName.G, NoteName.C);   // 1st inv
            AssertPcs(cands[2], NoteName.G, NoteName.C, NoteName.E);   // 2nd inv
            AssertPcs(cands[3], NoteName.E, NoteName.C, NoteName.G);   // drop-2
        }

        [Test]
        public void Unset_NullPin_IsIdenticalToOmittedArgument()
        {
            var cfg = Cfg();
            var withNull = Candidates(CMajorPcs(), cfg, null);
            var omitted = BasicVoiceLeadingVoicer
                .GeneratePcCandidates(CMajorPcs(), cfg).ToList();

            Assert.That(withNull.Count, Is.EqualTo(omitted.Count));
            for (int i = 0; i < withNull.Count; i++)
                Assert.That(withNull[i], Is.EqualTo(omitted[i]));
        }

        [Test]
        public void Unset_UseInversionsFalse_RootOnly()
        {
            var cands = Candidates(CMajorPcs(), Cfg(inversions: false, drop2: false));

            Assert.That(cands.Count, Is.EqualTo(1));
            AssertPcs(cands[0], NoteName.C, NoteName.E, NoteName.G);
        }

        // ------------------------------------------------------------------
        // Valid pin — exactly one candidate, the requested rotation (D0=A)
        // ------------------------------------------------------------------

        [Test]
        public void Pinned_FirstInversion_YieldsExactlyThatRotation()
        {
            var cands = Candidates(CMajorPcs(), Cfg(), pin: 1);

            Assert.That(cands.Count, Is.EqualTo(1),
                "A valid pin must suppress all other candidates (pin, not bias).");
            AssertPcs(cands[0], NoteName.E, NoteName.G, NoteName.C);
        }

        [Test]
        public void Pinned_SecondInversion_YieldsExactlyThatRotation()
        {
            var cands = Candidates(CMajorPcs(), Cfg(), pin: 2);

            Assert.That(cands.Count, Is.EqualTo(1));
            AssertPcs(cands[0], NoteName.G, NoteName.C, NoteName.E);
        }

        [Test]
        public void Pinned_Zero_ForcesRootPosition_NotEquivalentToUnset()
        {
            // Pinning 0 is a meaningful pin: root position only, no drop-2,
            // no inversions offered to the scorer.
            var cands = Candidates(CMajorPcs(), Cfg(), pin: 0);

            Assert.That(cands.Count, Is.EqualTo(1));
            AssertPcs(cands[0], NoteName.C, NoteName.E, NoteName.G);
        }

        [Test]
        public void Pinned_ThirdInversion_OnSeventhChord()
        {
            var cands = Candidates(CSeventhPcs(), Cfg(), pin: 3);

            Assert.That(cands.Count, Is.EqualTo(1));
            AssertPcs(cands[0],
                NoteName.ASharp, NoteName.C, NoteName.E, NoteName.G);
        }

        [Test]
        public void Pinned_OverridesUseInversionsToggle()
        {
            // An explicit per-chord pin outranks the candidate-set toggles:
            // the requested inversion is honored even with useInversions off.
            var cands = Candidates(
                CMajorPcs(), Cfg(inversions: false, drop2: false), pin: 2);

            Assert.That(cands.Count, Is.EqualTo(1));
            AssertPcs(cands[0], NoteName.G, NoteName.C, NoteName.E);
        }

        // ------------------------------------------------------------------
        // Out-of-range pin value — unset, never clamped (D2b=a)
        // ------------------------------------------------------------------

        [Test]
        public void OutOfRange_High_TreatedAsUnset()
        {
            // Index 5 on a triad: falls through to the full baseline set.
            var baseline = Candidates(CMajorPcs(), Cfg());
            var cands = Candidates(CMajorPcs(), Cfg(), pin: 5);

            Assert.That(cands.Count, Is.EqualTo(baseline.Count));
            for (int i = 0; i < cands.Count; i++)
                Assert.That(cands[i], Is.EqualTo(baseline[i]));
        }

        [Test]
        public void OutOfRange_Negative_TreatedAsUnset_NotClampedToRoot()
        {
            // A negative value must NOT clamp to a forced root position —
            // that would turn garbage input into a meaningful pin.
            var cands = Candidates(CMajorPcs(), Cfg(), pin: -1);

            Assert.That(cands.Count, Is.EqualTo(4),
                "Negative pin must behave as unset (full candidate set), not as pin=0.");
        }

        [Test]
        public void ArityBoundary_PinEqualToArity_TreatedAsUnset()
        {
            // pin == pcs.Length is the first out-of-range value.
            var cands = Candidates(CMajorPcs(), Cfg(), pin: 3);

            Assert.That(cands.Count, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------
        // ResolveInversionPin — composer-side per-position resolver
        // ------------------------------------------------------------------

        [Test]
        public void Resolve_NullList_ReturnsNull()
        {
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(null, 0), Is.Null);
        }

        [Test]
        public void Resolve_PositionBeyondList_ReturnsNull()
        {
            var hints = new int?[] { 1 };
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(hints, 1), Is.Null);
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(hints, 99), Is.Null);
        }

        [Test]
        public void Resolve_NegativePosition_ReturnsNull()
        {
            var hints = new int?[] { 1 };
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(hints, -1), Is.Null);
        }

        [Test]
        public void Resolve_NullEntry_ReturnsNull()
        {
            var hints = new int?[] { null, 2 };
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(hints, 0), Is.Null);
            Assert.That(
                ChordTrackComposer.ResolveInversionPin(hints, 1), Is.EqualTo(2));
        }

        [Test]
        public void Resolve_D2a_StickyPerPosition_SameResolutionOnEveryRepeat()
        {
            // D2a=a: the resolver takes no repeat parameter by design. Both
            // render loops reset eventIndex to 0 on every pattern repeat, so
            // the same position resolves to the same pin on repeat 0, 1, 2, ...
            // — the pin recurs with the pattern instead of firing once.
            var hints = new int?[] { null, 1, null, 2 };

            for (int repeat = 0; repeat < 3; repeat++)
            {
                Assert.That(ChordTrackComposer.ResolveInversionPin(hints, 1),
                    Is.EqualTo(1), $"repeat {repeat}, position 1");
                Assert.That(ChordTrackComposer.ResolveInversionPin(hints, 3),
                    Is.EqualTo(2), $"repeat {repeat}, position 3");
                Assert.That(ChordTrackComposer.ResolveInversionPin(hints, 0),
                    Is.Null, $"repeat {repeat}, position 0");
            }
        }

        // ------------------------------------------------------------------
        // D3 — combined-hint precedence on the render's first chord
        // ------------------------------------------------------------------

        [Test]
        public void D3_ActiveDirectionalHint_ProducesFirstChord_SoPinIsNeverConsulted()
        {
            // Scenario: both hints target chord 1 — a directional Up hint AND
            // an inversion pin at position 0. The loops' structure is:
            //
            //     playable = TryDirectionalFirstChord(...);
            //     if (playable == null) playable = VoiceChord(..., pin);
            //
            // so a non-null directional result means the pin branch is
            // unreachable for that chord. This test asserts both halves:
            // (1) the directional hint is genuinely active (non-null voicing,
            //     root-position stack — NOT the pinned inversion), and
            // (2) the pin at position 0 is genuinely set (it would have
            //     produced a different, single-candidate rotation).
            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: CMajorPcs(),
                firstChordRoot: NoteName.C,
                minOct: 1,
                maxOct: 5,
                hint: ModulationOctaveHint.Up,
                previousRoot: NoteName.G,
                previousFirstChordPitch: null,
                settings: null);

            Assert.That(voicing, Is.Not.Null,
                "Directional hint must win the first chord (D3).");
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.C),
                "§6 realizes a root-position stack; the pinned 1st inversion is ignored.");

            var hints = new int?[] { 1 };
            Assert.That(ChordTrackComposer.ResolveInversionPin(hints, 0),
                Is.EqualTo(1),
                "The pin was genuinely active — it is skipped only because §6 produced the chord.");

            var pinned = Candidates(CMajorPcs(), Cfg(), pin: 1);
            Assert.That(pinned.Single()[0], Is.EqualTo(NoteName.E),
                "Had the pin been consulted, the chord would have started on E (1st inversion).");
        }

        [Test]
        public void D3_AutoHint_ShortCircuits_SoPinPathRuns()
        {
            // With the default Auto hint the directional helper yields null,
            // the `if (playable == null)` guard falls through, and the pin
            // path runs — i.e. D3 carves out exactly one chord, nothing more.
            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: CMajorPcs(),
                firstChordRoot: NoteName.C,
                minOct: 1,
                maxOct: 5,
                hint: ModulationOctaveHint.Auto,
                previousRoot: NoteName.G,
                previousFirstChordPitch: null,
                settings: null);

            Assert.That(voicing, Is.Null,
                "Auto must short-circuit so the (pinned) voicer path runs for chord 1.");
        }
    }
}
#endif
#if UNITY_EDITOR
// PERC-FALLBACK-1 — PercussionNoteResolver / PercussionFallbackTable tests.
//
// Covers the agreed resolution order (D-PF2): exact kit mapping → first mapped
// family substitute in fixed table order → None (default) / GmStandard
// (opt-in), plus the determinism invariant (same kit + same input ⇒ same
// resolution, repeatedly).
//
// Fixture note: Dbg1Fixtures.Kit() (SongOrchestratorKeyingTests.cs, same
// assembly) creates a MIDIPercussionInstrumentSO with the SO's built-in
// default mappings — exactly the 8-member "Brush Kit" surface that triggered
// the batch: AcousticBassDrum, AcousticSnare, ClosedHiHat, OpenHiHat,
// CrashCymbal1, RideCymbal1, LowTom, HighTom. The M1-import members
// (BassDrum1, LowFloorTom, HighFloorTom, PedalHiHat, HiMidTom) are all
// unmapped on it, which makes it the canonical substitution fixture.

using NUnit.Framework;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay.Composition;

namespace MidiGenPlay.Tests.Editor
{
    public class PercussionNoteResolverTests
    {
        private static MIDIPercussionInstrumentSO DefaultKit() => Dbg1Fixtures.Kit();

        private static Note KitNote(
            MIDIPercussionInstrumentSO kit, GeneralMidiPercussion p)
        {
            Assert.IsTrue(kit.TryGetMappedNote(p, out var n),
                $"fixture kit unexpectedly lacks {p}");
            return n;
        }

        // ------------------------------------------------------------------
        // (1) Exact
        // ------------------------------------------------------------------

        [Test]
        public void Exact_MappedMember_ResolvesExact()
        {
            var kit = DefaultKit();

            bool ok = PercussionNoteResolver.TryResolve(
                kit, GeneralMidiPercussion.AcousticBassDrum, allowGmStandard: false,
                out var note, out var res, out var resolvedAs);

            Assert.IsTrue(ok);
            Assert.AreEqual(PercussionNoteResolver.Resolution.Exact, res);
            Assert.AreEqual(GeneralMidiPercussion.AcousticBassDrum, resolvedAs);
            Assert.AreEqual(
                KitNote(kit, GeneralMidiPercussion.AcousticBassDrum).NoteNumber,
                note.NoteNumber);
        }

        // ------------------------------------------------------------------
        // (2) Family substitution — the four M1 cases from the rehydration
        // ------------------------------------------------------------------

        [TestCase(GeneralMidiPercussion.BassDrum1, GeneralMidiPercussion.AcousticBassDrum)]
        [TestCase(GeneralMidiPercussion.LowFloorTom, GeneralMidiPercussion.LowTom)]
        [TestCase(GeneralMidiPercussion.PedalHiHat, GeneralMidiPercussion.ClosedHiHat)]
        [TestCase(GeneralMidiPercussion.HiMidTom, GeneralMidiPercussion.HighTom)]
        public void Substitution_UnmappedMember_ResolvesToFamilySubstitute(
            GeneralMidiPercussion requested, GeneralMidiPercussion expected)
        {
            var kit = DefaultKit();

            bool ok = PercussionNoteResolver.TryResolve(
                kit, requested, allowGmStandard: false,
                out var note, out var res, out var resolvedAs);

            Assert.IsTrue(ok, $"{requested} should resolve on the default kit");
            Assert.AreEqual(PercussionNoteResolver.Resolution.Substituted, res);
            Assert.AreEqual(expected, resolvedAs);
            Assert.AreEqual(KitNote(kit, expected).NoteNumber, note.NoteNumber);
        }

        [Test]
        public void Substitution_FirstMappedSubstituteWins_FixedTableOrder()
        {
            // LowFloorTom's table is [HighFloorTom, LowTom, ...] (D-PF4=A).
            // On the default kit HighFloorTom is unmapped → LowTom wins (test
            // above). Mapping HighFloorTom must flip the result to the
            // higher-priority entry — proving order comes from the table, not
            // from kit mapping order.
            var kit = DefaultKit();
            kit.percussionMappings.Add(new MIDIPercussionInstrumentSO.PercussionMapping
            {
                percussionType = GeneralMidiPercussion.HighFloorTom,
                noteName = NoteName.G,
                octave = 2,
            });

            PercussionNoteResolver.TryResolve(
                kit, GeneralMidiPercussion.LowFloorTom, allowGmStandard: false,
                out var note, out var res, out var resolvedAs);

            Assert.AreEqual(PercussionNoteResolver.Resolution.Substituted, res);
            Assert.AreEqual(GeneralMidiPercussion.HighFloorTom, resolvedAs);
            Assert.AreEqual(
                KitNote(kit, GeneralMidiPercussion.HighFloorTom).NoteNumber,
                note.NoteNumber);
        }

        // ------------------------------------------------------------------
        // (3) None (default) / GmStandard (opt-in)
        // ------------------------------------------------------------------

        [TestCase(GeneralMidiPercussion.Cowbell)]  // singleton: empty family
        [TestCase(GeneralMidiPercussion.HiBongo)]  // family exists, none mapped
        public void None_FamilyUnmapped_AndGmStandardOff_ReturnsFalse(
            GeneralMidiPercussion requested)
        {
            var kit = DefaultKit();

            bool ok = PercussionNoteResolver.TryResolve(
                kit, requested, allowGmStandard: false,
                out var note, out var res, out _);

            Assert.IsFalse(ok);
            Assert.AreEqual(PercussionNoteResolver.Resolution.None, res);
            Assert.IsNull(note);
        }

        [TestCase(GeneralMidiPercussion.Cowbell)]
        [TestCase(GeneralMidiPercussion.HiBongo)]
        public void GmStandard_FamilyUnmapped_AndGmStandardOn_EmitsGmNote(
            GeneralMidiPercussion requested)
        {
            var kit = DefaultKit();

            bool ok = PercussionNoteResolver.TryResolve(
                kit, requested, allowGmStandard: true,
                out var note, out var res, out var resolvedAs);

            Assert.IsTrue(ok);
            Assert.AreEqual(PercussionNoteResolver.Resolution.GmStandard, res);
            Assert.AreEqual(requested, resolvedAs);
            // DryWetMidi is the GM note-number authority (same seam rule as
            // DrumMidiImporter).
            Assert.AreEqual((int)requested.AsSevenBitNumber(), (int)note.NoteNumber);
        }

        [Test]
        public void GmStandard_DoesNotShadowSubstitution()
        {
            // allowGmStandard is a LAST resort: a mapped family substitute
            // must still win over the GM-standard escape hatch.
            var kit = DefaultKit();

            PercussionNoteResolver.TryResolve(
                kit, GeneralMidiPercussion.BassDrum1, allowGmStandard: true,
                out _, out var res, out var resolvedAs);

            Assert.AreEqual(PercussionNoteResolver.Resolution.Substituted, res);
            Assert.AreEqual(GeneralMidiPercussion.AcousticBassDrum, resolvedAs);
        }

        [Test]
        public void None_NullKit_AndGmStandardOff_ReturnsFalse()
        {
            bool ok = PercussionNoteResolver.TryResolve(
                null, GeneralMidiPercussion.AcousticBassDrum, allowGmStandard: false,
                out var note, out var res, out _);

            Assert.IsFalse(ok);
            Assert.AreEqual(PercussionNoteResolver.Resolution.None, res);
            Assert.IsNull(note);
        }

        // ------------------------------------------------------------------
        // (4) Determinism — same kit + same input ⇒ same resolution, always
        // ------------------------------------------------------------------

        [Test]
        public void Determinism_RepeatedResolution_IsIdentical()
        {
            var kit = DefaultKit();
            var probes = new[]
            {
                GeneralMidiPercussion.AcousticBassDrum, // Exact
                GeneralMidiPercussion.BassDrum1,        // Substituted
                GeneralMidiPercussion.LowFloorTom,      // Substituted
                GeneralMidiPercussion.HiBongo,          // None
            };

            foreach (var probe in probes)
            {
                bool ok0 = PercussionNoteResolver.TryResolve(
                    kit, probe, allowGmStandard: false,
                    out var n0, out var r0, out var a0);

                for (int i = 0; i < 100; i++)
                {
                    bool ok = PercussionNoteResolver.TryResolve(
                        kit, probe, allowGmStandard: false,
                        out var n, out var r, out var a);

                    Assert.AreEqual(ok0, ok, $"{probe}: ok drifted at i={i}");
                    Assert.AreEqual(r0, r, $"{probe}: resolution drifted at i={i}");
                    Assert.AreEqual(a0, a, $"{probe}: resolvedAs drifted at i={i}");
                    if (ok0)
                        Assert.AreEqual(n0.NoteNumber, n.NoteNumber,
                            $"{probe}: note drifted at i={i}");
                }
            }
        }
    }
}
#endif
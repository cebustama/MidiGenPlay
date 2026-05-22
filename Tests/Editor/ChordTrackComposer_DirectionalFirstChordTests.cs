#if UNITY_EDITOR
// MGP-ALWTTT-MOD-DIR-1.1 — directional first-chord helper tests.
//
// Targets the internal seam ChordTrackComposer.TryDirectionalFirstChordCore so
// the test does not need MIDIInstrumentSO / SongConfig fixtures. Visibility is
// granted by Runtime/AssemblyInfo.cs:
//
//     [assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]
//
// Covers SM-DIR-1 (failing scenario before the fix), the symmetric Down case,
// the SM-DIR-3 regression baseline (cold-start centerOct anchor preserved),
// the range-clamp fallback, and the Auto short-circuit.

using NUnit.Framework;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_DirectionalFirstChordTests
    {
        // Reed-Organ-like wide range: Yamaha octaves 2..6  =>  scientific 1..5.
        // Reproduces the SM-DIR-1 failure scenario.
        private const int MinOctReedOrganLike = 1;
        private const int MaxOctReedOrganLike = 5;

        // Triad pitch classes (root-position) — sufficient for the helper's
        // root-position stack builder; the tests only inspect the root pitch.
        private static NoteName[] GSharpMajorPcs() => new[]
        {
            NoteName.GSharp,   // root
            NoteName.C,        // major third  (G# major: G#, C(=B#), D#)
            NoteName.DSharp    // perfect fifth
        };

        private static NoteName[] CSharpMajorPcs() => new[]
        {
            NoteName.CSharp, NoteName.F, NoteName.GSharp
        };

        // ------------------------------------------------------------------
        // Cold-start (no remembered pitch) — bit-identical to pre-1.1 behavior.
        // This is the SM-DIR-3 regression baseline.
        // ------------------------------------------------------------------

        [Test]
        public void ColdStart_Up_FromCSharp_UsesCenterOctAnchor()
        {
            // centerOct = (1 + 5) / 2 = 3   =>   prevPitch = MidiPitch(C#, 3) = 49
            // First octave o with MidiPitch(G#, o) > 49 is o = 3  (G#3 = 56).
            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: GSharpMajorPcs(),
                firstChordRoot: NoteName.GSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Up,
                previousRoot: NoteName.CSharp,
                previousFirstChordPitch: null,    // cold start
                settings: null);

            Assert.That(voicing, Is.Not.Null);
            int rootPitch = MidiPitchOf(voicing[0]);
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.GSharp));
            Assert.That(rootPitch, Is.EqualTo(56),
                "Cold-start anchor must match pre-1.1 centerOct behavior (SM-DIR-3 regression).");
        }

        [Test]
        public void ColdStart_Down_FromGSharp_UsesCenterOctAnchor()
        {
            // centerOct = 3   =>   prevPitch = MidiPitch(G#, 3) = 56
            // Highest o with MidiPitch(C#, o) < 56 is o = 3 (C#3 = 49).
            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: CSharpMajorPcs(),
                firstChordRoot: NoteName.CSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Down,
                previousRoot: NoteName.GSharp,
                previousFirstChordPitch: null,
                settings: null);

            Assert.That(voicing, Is.Not.Null);
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.CSharp));
            Assert.That(MidiPitchOf(voicing[0]), Is.EqualTo(49));
        }

        // ------------------------------------------------------------------
        // Remembered-pitch anchor — the MGP-ALWTTT-MOD-DIR-1.1 fix.
        // Reproduces SM-DIR-1: prior render placed first chord at C#5 (MIDI 73);
        // hint=Up to G#; expected root pitch strictly > 73, which is G#5 = 80.
        // Pre-1.1 returned G#3 (56) here — the bug.
        // ------------------------------------------------------------------

        [Test]
        public void Remembered_Up_FromCSharp5_ToGSharp_LandsStrictlyAbove()
        {
            const int prevPitch = 73; // C#5 in scientific = the SM-DIR-1 prior chord

            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: GSharpMajorPcs(),
                firstChordRoot: NoteName.GSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Up,
                previousRoot: NoteName.CSharp,
                previousFirstChordPitch: prevPitch,
                settings: null);

            Assert.That(voicing, Is.Not.Null);
            int rootPitch = MidiPitchOf(voicing[0]);
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.GSharp));
            Assert.That(rootPitch, Is.GreaterThan(prevPitch),
                "MGP-ALWTTT-MOD-DIR-1.1 closure criterion: new root pitch must be " +
                "strictly greater than the previous loop's first-chord root pitch.");
            Assert.That(rootPitch, Is.EqualTo(80),
                "Expected landing is G#5 (MIDI 80) — the lowest G# strictly above MIDI 73.");
        }

        [Test]
        public void Remembered_Down_FromCSharp3_ToGSharp_LandsStrictlyBelow()
        {
            // Symmetric Down case. Previous chord at C#3 = MIDI 49; Down to G#.
            // Highest o with MidiPitch(G#, o) < 49 is o = 2 (G#2 = 44).
            const int prevPitch = 49;

            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: GSharpMajorPcs(),
                firstChordRoot: NoteName.GSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Down,
                previousRoot: NoteName.CSharp,
                previousFirstChordPitch: prevPitch,
                settings: null);

            Assert.That(voicing, Is.Not.Null);
            int rootPitch = MidiPitchOf(voicing[0]);
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.GSharp));
            Assert.That(rootPitch, Is.LessThan(prevPitch));
            Assert.That(rootPitch, Is.EqualTo(44));
        }

        // ------------------------------------------------------------------
        // Range-limit fallback (§6.1) still works with remembered anchor.
        // ------------------------------------------------------------------

        [Test]
        public void Remembered_Up_BeyondTopOfRange_ClampsToMaxOct()
        {
            // Previous chord pinned to top of range (G#5 = MIDI 80). New chord
            // also G#: no octave above can satisfy strict ">", so the helper
            // clamps to maxOct (= 5). The closure-criterion warning fires when
            // logGenerator is on (not asserted here — settings is null).
            const int prevPitch = 80;

            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: GSharpMajorPcs(),
                firstChordRoot: NoteName.GSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Up,
                previousRoot: NoteName.GSharp,
                previousFirstChordPitch: prevPitch,
                settings: null);

            Assert.That(voicing, Is.Not.Null);
            Assert.That(voicing[0].NoteName, Is.EqualTo(NoteName.GSharp));
            Assert.That(voicing[0].Octave, Is.EqualTo(MaxOctReedOrganLike),
                "Clamp fallback must land on maxOct when no strict-above octave exists.");
        }

        // ------------------------------------------------------------------
        // Auto short-circuit — Core returns null and the standard voicer
        // remains in control (SM-DIR-3 regression depends on this).
        // ------------------------------------------------------------------

        [Test]
        public void Auto_ReturnsNull_LeavingVoicerInControl()
        {
            var voicing = ChordTrackComposer.TryDirectionalFirstChordCore(
                firstChordPcs: GSharpMajorPcs(),
                firstChordRoot: NoteName.GSharp,
                minOct: MinOctReedOrganLike,
                maxOct: MaxOctReedOrganLike,
                hint: ModulationOctaveHint.Auto,
                previousRoot: NoteName.CSharp,
                previousFirstChordPitch: 73,
                settings: null);

            Assert.That(voicing, Is.Null);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static int MidiPitchOf(DryWetMidiNote n) =>
            ChordTrackComposer.MidiPitch(n.NoteName, n.Octave);
    }
}
#endif
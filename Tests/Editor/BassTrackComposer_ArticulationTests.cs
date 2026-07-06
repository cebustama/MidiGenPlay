#if UNITY_EDITOR
// CA-F2 — monophonic bass consumer of the Tier-1 articulation engine.
//
// Mirrors the CA-T1 test discipline: behavior is pinned at pure seams
// (PatternBuilder-level Emit + the internal ResolveArticulation resolve seam,
// via Runtime/AssemblyInfo InternalsVisibleTo("MidiGenPlay.Tests.Editor")).
// No MIDIInstrumentSO / full-composer fixture: the composer's note-selection
// loop (and its per-event ctx.rng draw sequence) is deliberately untouched by
// CA-F2, and the emission swap is a single unconditional Emit call — the same
// structural argument that carried CA-T1's dual-site guarantee.
//
// Decisions covered:
//   SD-F2-1=A  — 1-note voicing through IChordArticulator.Emit; the GATE test
//                is Block_MonoEmit_IsByteIdenticalToLegacyMoveToTimeNotePair.
//                If it fails in Unity, take the recorded contingency (an
//                EmitMono translator sharing PlanHits) and amend the drafted
//                CA-F2 doc diffs BEFORE applying them.
//   SD-F2-2=A  — figures over the selected note; arpeggios = repeated-note
//                pulse (Up == Down for a 1-note voicing).
//   SD-F2-3=B  — meter authority: Block bit-identity holds per beat span; the
//                eighth-based output (6/8 part) intentionally differs from the
//                legacy Quarter-based emission (deliberate sync fix, pinned).
//   SD-F2-4=A / SD-F2-5=A — BasslineCardConfigSO in the Style slot; any other
//                bundle (incl. BackingCardConfigSO) is ignored => Block.
//
// See runtime/SSoT_Composer_Bass_Track.md and
// runtime/SSoT_Composer_Backing_Track.md §8 (engine contract).

using System.Linq;
using NUnit.Framework;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using UnityEngine;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_ArticulationTests
    {
        private static readonly TempoMap Map =
            TempoMap.Create(Tempo.FromBeatsPerMinute(120));

        private static DwmNote BassNote() => DwmNote.Get(DwmNoteName.E, 2);

        private static byte[] Bytes(PatternBuilder pb)
        {
            var file = pb.Build().ToFile(Map);
            using (var ms = new System.IO.MemoryStream())
            {
                file.Write(ms);
                return ms.ToArray();
            }
        }

        /// <summary>The pre-CA-F2 bass emission pair, verbatim (raw velocity
        /// cast, no clamp), parameterized on the time base.</summary>
        private static PatternBuilder LegacyPair(
            MusicalTimeSpan span, DwmNote note,
            double startBeats, double lenBeats, int velocity)
        {
            var pb = new PatternBuilder();
            pb.MoveToTime(span.Multiply(startBeats));
            pb.Note(note, span.Multiply(lenBeats), (SevenBitNumber)velocity);
            return pb;
        }

        private static PatternBuilder Articulated(
            ChordExpressionType expr, MusicalTimeSpan span, DwmNote note,
            double startBeats, double lenBeats, int velocity,
            int beatsPerBar = 4, ArpeggioRate rate = ArpeggioRate.Eighth)
        {
            var pb = new PatternBuilder();
            new ChordArticulator().Emit(pb, new[] { note }, startBeats, lenBeats,
                span, beatsPerBar, velocity, stepsPerBeat: 4, expr, rate);
            return pb;
        }

        // ------------------------------------------------------------------
        // SD-F2-1 GATE — 1-note Block through Emit == legacy MoveToTime+Note
        // ------------------------------------------------------------------

        [Test]
        public void Block_MonoEmit_IsByteIdenticalToLegacyMoveToTimeNotePair()
        {
            var note = BassNote();
            var legacy = LegacyPair(MusicalTimeSpan.Quarter, note,
                startBeats: 2.0, lenBeats: 4.0, velocity: 96);
            var art = Articulated(ChordExpressionType.Block, MusicalTimeSpan.Quarter,
                note, startBeats: 2.0, lenBeats: 4.0, velocity: 96);

            Assert.That(Bytes(art), Is.EqualTo(Bytes(legacy)),
                "SD-F2-1=A gate: a 1-note Block chord through the articulator " +
                "must be bit-identical to the legacy pb.Note pair. If this " +
                "fails, take the EmitMono contingency and amend the CA-F2 doc " +
                "diffs before applying.");
        }

        // ------------------------------------------------------------------
        // SD-F2-3=B — bit-identity holds per beat span; the 6/8 fix is real
        // ------------------------------------------------------------------

        [Test]
        public void Block_MonoEmit_BitIdentityHoldsPerBeatSpan_EighthDiffersFromLegacyQuarter()
        {
            var note = BassNote();

            // On the Part beat span (Eighth, i.e. a 6/8 part) the articulated
            // Block equals the legacy-shaped pair ON THAT SPAN...
            var legacyEighth = LegacyPair(MusicalTimeSpan.Eighth, note, 2.0, 4.0, 96);
            var artEighth = Articulated(ChordExpressionType.Block,
                MusicalTimeSpan.Eighth, note, 2.0, 4.0, 96, beatsPerBar: 6);
            Assert.That(Bytes(artEighth), Is.EqualTo(Bytes(legacyEighth)),
                "Block bit-identity is per beat span (meter authority).");

            // ...and intentionally differs from what the pre-CA-F2 bass emitted
            // (unconditional Quarter): the recorded SD-F2-3=B sync fix.
            var legacyQuarter = LegacyPair(MusicalTimeSpan.Quarter, note, 2.0, 4.0, 96);
            Assert.That(Bytes(artEighth), Is.Not.EqualTo(Bytes(legacyQuarter)),
                "In beat-unit != 4 meters the new output deliberately deviates " +
                "from the legacy Quarter-based emission (deliberate sync fix; " +
                "bit-identity is only claimed for beat-unit == 4 meters).");
        }

        // ------------------------------------------------------------------
        // SD-F2-4=A / SD-F2-5=A — card resolution (internal resolve seam)
        // ------------------------------------------------------------------

        [Test]
        public void ResolveArticulation_NoCard_DefaultsToBlockEighth()
        {
            var expectedDefault =
                (ChordExpressionType.Block, ArpeggioRate.Eighth);

            Assert.That(BassTrackComposer.ResolveArticulation(null),
                Is.EqualTo(expectedDefault), "null TrackConfig");

            var noParams = new SongConfig.PartConfig.TrackConfig();
            Assert.That(BassTrackComposer.ResolveArticulation(noParams),
                Is.EqualTo(expectedDefault), "null Parameters");

            var noStyle = new SongConfig.PartConfig.TrackConfig
            {
                Parameters = new TrackParameters()
            };
            Assert.That(BassTrackComposer.ResolveArticulation(noStyle),
                Is.EqualTo(expectedDefault), "null Style slot");
        }

        [Test]
        public void ResolveArticulation_BasslineCard_SelectsPersistentCardValues()
        {
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            try
            {
                // Field defaults on a fresh card must match the unset defaults.
                Assert.That(card.chordExpression, Is.EqualTo(ChordExpressionType.Block));
                Assert.That(card.arpeggioRate, Is.EqualTo(ArpeggioRate.Eighth));

                card.chordExpression = ChordExpressionType.Offbeat;
                card.arpeggioRate = ArpeggioRate.Sixteenth;

                var cfg = new SongConfig.PartConfig.TrackConfig
                {
                    Parameters = new TrackParameters { Style = card }
                };

                Assert.That(BassTrackComposer.ResolveArticulation(cfg),
                    Is.EqualTo((ChordExpressionType.Offbeat, ArpeggioRate.Sixteenth)),
                    "D-EXP1=A: persistent card values drive the whole render.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(card);
            }
        }

        [Test]
        public void ResolveArticulation_BackingCardOnBassSlot_IsIgnored_BassIsIndependent()
        {
            var backing = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            try
            {
                backing.chordExpression = ChordExpressionType.ArpeggioUp;
                backing.arpeggioRate = ArpeggioRate.Sixteenth;

                var cfg = new SongConfig.PartConfig.TrackConfig
                {
                    Parameters = new TrackParameters { Style = backing }
                };

                Assert.That(BassTrackComposer.ResolveArticulation(cfg),
                    Is.EqualTo((ChordExpressionType.Block, ArpeggioRate.Eighth)),
                    "SD-F2-5=A: the bass never inherits or adopts the backing " +
                    "card's expression — an unset bass stays bit-identical " +
                    "regardless of the backing selection.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(backing);
            }
        }

        // ------------------------------------------------------------------
        // SD-F2-2=A — monophonic figure semantics at MIDI level
        // ------------------------------------------------------------------

        [Test]
        public void Arpeggio_OneNoteVoicing_IsRepeatedNotePulse_UpEqualsDown()
        {
            var note = BassNote();
            var up = Articulated(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, note, 0, 2.0, 100);

            var notes = up.Build().ToFile(Map).GetNotes()
                .OrderBy(n => n.Time).ToList();

            Assert.That(notes.Count, Is.EqualTo(4), "eighth rate over 2 beats");
            Assert.That(notes.All(n => n.NoteNumber == note.NoteNumber), Is.True,
                "a 1-note voicing cycles the same pitch: repeated-note pulse");

            var expectedTimes = new[] { 0.0, 0.5, 1.0, 1.5 }
                .Select(b => TimeConverter.ConvertFrom(
                    MusicalTimeSpan.Quarter.Multiply(b), Map)).ToArray();
            Assert.That(notes.Select(n => n.Time), Is.EqualTo(expectedTimes));

            Assert.That(notes.Select(n => (int)n.Velocity),
                Is.EqualTo(new[] { 100, 80, 85, 80 }),
                "downbeat / off-beat / on-beat / off-beat accent curve");

            var down = Articulated(ChordExpressionType.ArpeggioDown,
                MusicalTimeSpan.Quarter, note, 0, 2.0, 100);
            Assert.That(Bytes(down), Is.EqualTo(Bytes(up)),
                "Up and Down are indistinguishable on a 1-note voicing");
        }

        [Test]
        public void Offbeat_OneNoteVoicing_UpstrokeStabs()
        {
            var note = BassNote();
            var pb = Articulated(ChordExpressionType.Offbeat,
                MusicalTimeSpan.Quarter, note, 0, 4.0, 100);

            var notes = pb.Build().ToFile(Map).GetNotes()
                .OrderBy(n => n.Time).ToList();

            var expectedTimes = new[] { 0.5, 1.5, 2.5, 3.5 }
                .Select(b => TimeConverter.ConvertFrom(
                    MusicalTimeSpan.Quarter.Multiply(b), Map)).ToArray();
            Assert.That(notes.Select(n => n.Time), Is.EqualTo(expectedTimes));

            Assert.That(notes.All(n => (int)n.Velocity == 80), Is.True,
                "all upstrokes off-beat ×0.80");

            long stabTicks = TimeConverter.ConvertFrom(
                MusicalTimeSpan.Quarter.Multiply(0.5), Map);
            Assert.That(notes.All(n => n.Length == stabTicks), Is.True,
                "short (0.5-beat) stabs");
        }

        // ------------------------------------------------------------------
        // Never-silent — unfittable figure emits the exact legacy pair
        // ------------------------------------------------------------------

        [Test]
        public void UnfittableFigure_OneNote_DegradesToLegacyBlockPair()
        {
            var note = BassNote();
            // [0, 0.5): the first offbeat (0.5) is outside the window.
            var degraded = Articulated(ChordExpressionType.Offbeat,
                MusicalTimeSpan.Quarter, note, 0, 0.5, 96);
            var legacy = LegacyPair(MusicalTimeSpan.Quarter, note, 0, 0.5, 96);

            Assert.That(Bytes(degraded), Is.EqualTo(Bytes(legacy)),
                "degrade is a TRUE Block: byte-identical legacy emission");
        }

        // ------------------------------------------------------------------
        // Determinism — RNG-free engine on a monophonic line
        // ------------------------------------------------------------------

        [Test]
        public void MonoEmit_IsDeterministic_SameInputsSameBytes_AllExpressions()
        {
            var note = BassNote();
            foreach (var expr in new[]
            {
                ChordExpressionType.Block, ChordExpressionType.PerBeat,
                ChordExpressionType.Offbeat, ChordExpressionType.Staccato,
                ChordExpressionType.ArpeggioUp, ChordExpressionType.ArpeggioDown,
            })
            {
                var a = Articulated(expr, MusicalTimeSpan.Quarter, note,
                    0.75, 3.5, 90, beatsPerBar: 3);
                var b = Articulated(expr, MusicalTimeSpan.Quarter, note,
                    0.75, 3.5, 90, beatsPerBar: 3);

                Assert.That(Bytes(a), Is.EqualTo(Bytes(b)), expr.ToString());
            }
        }
    }
}
#endif
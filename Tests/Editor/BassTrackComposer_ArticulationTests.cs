#if UNITY_EDITOR
// CA-F2 - monophonic bass consumer of the Tier-1 articulation engine.
//
// Mirrors the CA-T1 test discipline: behavior is pinned at pure seams
// (PatternBuilder-level Emit + the internal ResolveArticulation resolve seam,
// via Runtime/AssemblyInfo InternalsVisibleTo("MidiGenPlay.Tests.Editor")).
// No MIDIInstrumentSO / full-composer fixture: the composer's note-selection
// loop (and its per-event ctx.rng draw sequence) is deliberately untouched by
// CA-F2, and the emission swap is a single unconditional Emit call - the same
// structural argument that carried CA-T1's dual-site guarantee.
//
// Decisions covered:
//   SD-F2-1=A  - 1-note voicing through IChordArticulator.Emit; the GATE test
//                is Block_MonoEmit_IsByteIdenticalToLegacyMoveToTimeNotePair.
//                If it fails in Unity, take the recorded contingency (an
//                EmitMono translator sharing PlanHits) and amend the drafted
//                CA-F2 doc diffs BEFORE applying them.
//   SD-F2-2=A  - figures over the selected note; arpeggios = repeated-note
//                pulse (Up == Down for a 1-note voicing).
//   SD-F2-3=B  - meter authority: Block bit-identity holds per beat span; the
//                eighth-based output (6/8 part) intentionally differs from the
//                legacy Quarter-based emission (deliberate sync fix, pinned).
//   SD-F2-4=A / SD-F2-5=A - BasslineCardConfigSO in the Style slot; any other
//                bundle (incl. BackingCardConfigSO) is ignored => Block.
//
// BASS-WALK-1 (this batch) adds the chord-tone walk suite at the bottom:
//   D-WALK-HOME=A   - the walk is built bass-side as a 3-note playable handed
//                     to the SAME Emit; the engine's k % noteCount cycling does
//                     the walking. No articulator figure was added.
//   D-WALK-RNG=A    - zero new rng draws: 3rd/5th are derived deterministically
//                     from chordPcs and stacked above the ALREADY-DRAWN root
//                     octave. The section-2 draw contract (1 draw root mode /
//                     2 chord-tone mode, in that order) is structurally intact
//                     - the walk branch runs after both draws and reads no rng.
//                     Pinned indirectly by BuildWalkVoicing purity + root
//                     anchoring; the full-loop claim stays structural, per this
//                     file's standing no-composer-fixture argument.
//   D-WALK-SURF=A   - opt-in via the bass-only BassArpeggioToneMode enum on the
//                     card; ChordExpressionType is untouched, so nothing leaks
//                     into the shared engine or the backing's SS8.5 pool.
//   D-WALK-TONES    - triad only (root/3rd/5th); a 7th in chordPcs is dropped.
//   D-WALK-FIT=A    - ChordArticulator.ArpeggioFits is the exposed degrade
//                     predicate; the bass consults it so a too-short event
//                     never hands a 3-note playable to a plan that degrades to
//                     Block (which would emit a CHORD on a bass line). The
//                     equivalence test lives here, with the consumer that
//                     depends on it.
//
// See runtime/SSoT_Composer_Bass_Track.md and
// runtime/SSoT_Composer_Backing_Track.md SS8 (engine contract).

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.Composition.BasslineCardConfigSO;
using DwmNote = Melanchall.DryWetMidi.MusicTheory.Note;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class BassTrackComposer_ArticulationTests
    {
        private static readonly TempoMap Map =
            TempoMap.Create(Tempo.FromBeatsPerMinute(120));

        private static DwmNote BassNote() => DwmNote.Get(DwmNoteName.E, 2);

        // BASS-WALK-1 fixtures. C major = no wrap (C < E < G as pitch classes);
        // A minor = the wrapping case (C and E are BELOW A in pc order, so the
        // stacker must lift them an octave).
        private static readonly DwmNoteName[] CMajorPcs =
            { DwmNoteName.C, DwmNoteName.E, DwmNoteName.G };
        private static readonly DwmNoteName[] AMinorPcs =
            { DwmNoteName.A, DwmNoteName.C, DwmNoteName.E };
        private static readonly DwmNoteName[] CMaj7Pcs =
            { DwmNoteName.C, DwmNoteName.E, DwmNoteName.G, DwmNoteName.B };

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

        /// <summary>BASS-WALK-1: the same Emit seam over a multi-note playable
        /// (what the bass hands it when the walk branch is taken).</summary>
        private static PatternBuilder ArticulatedVoicing(
            ChordExpressionType expr, MusicalTimeSpan span, DwmNote[] playable,
            double startBeats, double lenBeats, int velocity,
            int beatsPerBar = 4, ArpeggioRate rate = ArpeggioRate.Eighth)
        {
            var pb = new PatternBuilder();
            new ChordArticulator().Emit(pb, playable, startBeats, lenBeats,
                span, beatsPerBar, velocity, stepsPerBeat: 4, expr, rate);
            return pb;
        }

        private static int[] PitchSequence(PatternBuilder pb) =>
            pb.Build().ToFile(Map).GetNotes()
              .OrderBy(n => n.Time).Select(n => (int)n.NoteNumber).ToArray();

        // ------------------------------------------------------------------
        // SD-F2-1 GATE - 1-note Block through Emit == legacy MoveToTime+Note
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
        // SD-F2-3=B - bit-identity holds per beat span; the 6/8 fix is real
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
        // SD-F2-4=A / SD-F2-5=A - card resolution (internal resolve seam)
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
                    "card's expression - an unset bass stays bit-identical " +
                    "regardless of the backing selection.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(backing);
            }
        }

        // ------------------------------------------------------------------
        // SD-F2-2=A - monophonic figure semantics at MIDI level
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
                "all upstrokes off-beat x0.80");

            long stabTicks = TimeConverter.ConvertFrom(
                MusicalTimeSpan.Quarter.Multiply(0.5), Map);
            Assert.That(notes.All(n => n.Length == stabTicks), Is.True,
                "short (0.5-beat) stabs");
        }

        // ------------------------------------------------------------------
        // Never-silent - unfittable figure emits the exact legacy pair
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
        // Determinism - RNG-free engine on a monophonic line
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

        // ---------- CA-V1: the bass roll (D6 lifted) ----------

        [Test]
        public void ArticulationSubstreams_DifferBetweenBackingAndBass()
        {
            // The bass roll is safe to wire precisely because trackSeed already
            // folds in the role: same part, same base seed, different sequence.
            int backing = SongOrchestrator.ResolveTrackSeedPart(
                1234, 0, TrackRole.Backing, "m1");
            int bass = SongOrchestrator.ResolveTrackSeedPart(
                1234, 0, TrackRole.Bassline, "m1");

            Assert.AreNotEqual(backing, bass);
            Assert.AreNotEqual(
                SongOrchestrator.ResolveArticulationSeed(backing),
                SongOrchestrator.ResolveArticulationSeed(bass));
            Assert.AreNotEqual(
                SongOrchestrator.ResolveVelocityJitterSeed(backing),
                SongOrchestrator.ResolveVelocityJitterSeed(bass));
        }

        [Test]
        public void ResolveArticulation_PassesTheRandomSentinelsThrough()
        {
            // CA-V1: the composer must SEE Random (to roll it); it is no longer
            // expected to reach the articulator and degrade to Block.
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            try
            {
                card.chordExpression = ChordExpressionType.Random;
                card.arpeggioRate = ArpeggioRate.Random;

                var cfg = new SongConfig.PartConfig.TrackConfig
                {
                    Parameters = new TrackParameters { Style = card }
                };

                Assert.That(BassTrackComposer.ResolveArticulation(cfg),
                    Is.EqualTo((ChordExpressionType.Random, ArpeggioRate.Random)));
                Assert.That(card.velocityJitter, Is.EqualTo(0),
                    "CA-V1 jitter must default to off on a fresh card.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(card);
            }
        }

        // ==================================================================
        // BASS-WALK-1 - chord-tone walk (D-WALK-*)
        // ==================================================================

        // ---------- the voicing builder (pitch selection, no rng) ----------

        [Test]
        public void BuildWalkVoicing_StacksRootThirdFifth_StrictlyAscending()
        {
            var v = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2);

            Assert.That(v.Length, Is.EqualTo(3), "triad: root/3rd/5th");
            Assert.That(v.Select(n => n.NoteName),
                Is.EqualTo(new[] { DwmNoteName.C, DwmNoteName.E, DwmNoteName.G }),
                "pitch classes keep chordPcs order (root first)");

            for (int i = 1; i < v.Length; i++)
                Assert.That((int)v[i].NoteNumber, Is.GreaterThan((int)v[i - 1].NoteNumber),
                    "the stack is strictly ascending, so Emit's pitch sort is a " +
                    "no-op and the walk reads root -> 3rd -> 5th");
        }

        [Test]
        public void BuildWalkVoicing_WrappingTones_AreLiftedAboveTheRoot()
        {
            // A minor: C and E are BELOW A in pitch-class order. Naive same-octane
            // placement would put the "3rd" under the root and invert the walk.
            var v = BassTrackComposer.BuildWalkVoicing(AMinorPcs, 2);

            Assert.That(v.Select(n => (int)n.NoteNumber), Is.EqualTo(new[]
            {
                (int)DwmNote.Get(DwmNoteName.A, 2).NoteNumber,
                (int)DwmNote.Get(DwmNoteName.C, 3).NoteNumber,
                (int)DwmNote.Get(DwmNoteName.E, 3).NoteNumber,
            }), "wrapping tones are lifted exactly one octave, once");
        }

        [Test]
        public void BuildWalkVoicing_IsRootAnchoredToTheDrawnOctave_AndPure()
        {
            // D-WALK-RNG=A / D-WALK-ANCHOR: the walk does NOT re-pick a register.
            // It stacks on top of the octave the selection loop already drew, so
            // no rng draw is added and the drawn octave still governs the line.
            foreach (int oct in new[] { 1, 2, 3 })
            {
                var v = BassTrackComposer.BuildWalkVoicing(CMajorPcs, oct);
                Assert.That((int)v[0].NoteNumber,
                    Is.EqualTo((int)DwmNote.Get(CMajorPcs[0], oct).NoteNumber),
                    "voicing[0] IS the drawn root note, verbatim");

                // Purity: no state, no rng - repeated calls are identical.
                var again = BassTrackComposer.BuildWalkVoicing(CMajorPcs, oct);
                Assert.That(again.Select(n => (int)n.NoteNumber),
                    Is.EqualTo(v.Select(n => (int)n.NoteNumber)));
            }
        }

        [Test]
        public void BuildWalkVoicing_SeventhChord_TakesTheTriadOnly()
        {
            // D-WALK-TONES: v1 walks root/3rd/5th; a 7th in the chord alphabet is
            // deliberately dropped (recorded extension, not a defect).
            var v = BassTrackComposer.BuildWalkVoicing(CMaj7Pcs, 2);

            Assert.That(v.Length, Is.EqualTo(3));
            Assert.That(v.Any(n => n.NoteName == DwmNoteName.B), Is.False,
                "the 7th is out of scope for the v1 walk");
        }

        // ---------- the walk through the shared engine ----------

        [Test]
        public void Walk_ArpeggioUp_CyclesRootThirdFifth()
        {
            var triad = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2);
            var up = ArticulatedVoicing(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, triad, 0, 2.0, 100);

            var pitches = PitchSequence(up);

            Assert.That(pitches.Length, Is.EqualTo(4), "eighth rate over 2 beats");
            Assert.That(pitches, Is.EqualTo(new[]
            {
                (int)triad[0].NoteNumber, (int)triad[1].NoteNumber,
                (int)triad[2].NoteNumber, (int)triad[0].NoteNumber,
            }), "k % noteCount cycling walks root -> 3rd -> 5th -> root");

            // The line stays monophonic: one note per hit, no stacking.
            var notes = up.Build().ToFile(Map).GetNotes().ToList();
            Assert.That(notes.Select(n => n.Time).Distinct().Count(),
                Is.EqualTo(notes.Count), "no two notes share an onset");

            // The accent curve is untouched by the walk (it is pitch selection
            // only - the engine's velocity model is the same one CA-T1 pinned).
            Assert.That(notes.OrderBy(n => n.Time).Select(n => (int)n.Velocity),
                Is.EqualTo(new[] { 100, 80, 85, 80 }));
        }

        [Test]
        public void Walk_UpAndDown_AreDistinguishable_UnlikeTheRepeatedNotePulse()
        {
            // THIS is the SS3.3 pool-bias fix: with three notes in the playable,
            // ArpeggioUp and ArpeggioDown stop being the same figure, so the
            // uniform Random pool no longer double-weights one sound.
            var triad = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2);

            var up = ArticulatedVoicing(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, triad, 0, 2.0, 100);
            var down = ArticulatedVoicing(ChordExpressionType.ArpeggioDown,
                MusicalTimeSpan.Quarter, triad, 0, 2.0, 100);

            Assert.That(PitchSequence(down), Is.Not.EqualTo(PitchSequence(up)),
                "walk mode makes Up and Down genuinely different figures");
            Assert.That(PitchSequence(down).First(),
                Is.EqualTo((int)triad[2].NoteNumber),
                "Down starts from the top of the stack (engine sort order)");

            // Contrast: the same two figures on the legacy 1-note playable are
            // still byte-identical (SD-F2-2=A holds where walk is off).
            var monoUp = Articulated(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, BassNote(), 0, 2.0, 100);
            var monoDown = Articulated(ChordExpressionType.ArpeggioDown,
                MusicalTimeSpan.Quarter, BassNote(), 0, 2.0, 100);
            Assert.That(Bytes(monoDown), Is.EqualTo(Bytes(monoUp)));
        }

        [Test]
        public void Walk_IsDeterministic_SameInputsSameBytes()
        {
            var triad = BassTrackComposer.BuildWalkVoicing(AMinorPcs, 2);
            foreach (var expr in new[]
            {
                ChordExpressionType.ArpeggioUp, ChordExpressionType.ArpeggioDown,
            })
                foreach (var rate in new[]
                {
                ArpeggioRate.PerBeat, ArpeggioRate.Eighth, ArpeggioRate.Sixteenth,
            })
                {
                    var a = ArticulatedVoicing(expr, MusicalTimeSpan.Quarter, triad,
                        0.75, 3.5, 90, beatsPerBar: 3, rate: rate);
                    var b = ArticulatedVoicing(expr, MusicalTimeSpan.Quarter, triad,
                        0.75, 3.5, 90, beatsPerBar: 3, rate: rate);

                    Assert.That(Bytes(a), Is.EqualTo(Bytes(b)), $"{expr}/{rate}");
                }
        }

        // ---------- D-WALK-FIT: the mono guard ----------

        [Test]
        public void ArpeggioFits_MatchesTheEngineDegradeBoundary()
        {
            // The guard is only sound if the predicate agrees with the plan it
            // predicts. Degrade == a single full-chord hit (NoteIndex -1);
            // a fitting arpeggio always indexes a note (NoteIndex >= 0).
            foreach (var rate in new[]
            {
                ArpeggioRate.PerBeat, ArpeggioRate.Eighth, ArpeggioRate.Sixteenth,
            })
            {
                double interval = ChordArticulator.ArpeggioIntervalBeats(rate);

                foreach (double dur in new[]
                {
                    interval * 0.25, interval * 0.9, interval,
                    interval * 1.5, interval * 4.0,
                })
                {
                    var hits = ChordArticulator.PlanHits(
                        ChordExpressionType.ArpeggioUp, rate,
                        startBeats: 0, durBeats: dur, beatsPerBar: 4,
                        noteCount: 3, baseVelocity: 100);

                    bool enginePlanned = hits[0].NoteIndex >= 0;

                    Assert.That(ChordArticulator.ArpeggioFits(dur, rate),
                        Is.EqualTo(enginePlanned),
                        $"predicate/plan disagreement at rate={rate} dur={dur} " +
                        "- the bass walk guard would leak a chord or suppress a " +
                        "valid walk. Re-sync ArpeggioFits with ArpeggioPlan.");
                }
            }
        }

        [Test]
        public void Walk_TooShortEvent_GuardKeepsTheLineMonophonic()
        {
            const double shortDur = 0.25; // shorter than one eighth
            Assert.That(ChordArticulator.ArpeggioFits(shortDur, ArpeggioRate.Eighth),
                Is.False, "precondition: this event degrades");

            // What the guard makes the bass do: fall back to the 1-note playable,
            // which degrades to a TRUE legacy Block.
            var guarded = ArticulatedVoicing(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, new[] { BassNote() }, 0, shortDur, 96);
            var legacy = LegacyPair(MusicalTimeSpan.Quarter, BassNote(),
                0, shortDur, 96);
            Assert.That(Bytes(guarded), Is.EqualTo(Bytes(legacy)));

            // What the guard PREVENTS: the same degrade over a triad playable is
            // a three-note chord - polyphony on a bass line.
            var triad = BassTrackComposer.BuildWalkVoicing(CMajorPcs, 2);
            var unguarded = ArticulatedVoicing(ChordExpressionType.ArpeggioUp,
                MusicalTimeSpan.Quarter, triad, 0, shortDur, 96);
            var stacked = unguarded.Build().ToFile(Map).GetNotes().ToList();
            Assert.That(stacked.Count, Is.EqualTo(3),
                "documents the hazard the D-WALK-FIT guard exists for: without " +
                "it, a too-short event emits a chord, not a bass note");
        }

        // ---------- D-WALK-SURF: the opt-in surface ----------

        [Test]
        public void ArpeggioToneMode_DefaultsToRepeatedNote_OnAFreshCard()
        {
            // The default is what makes BASS-WALK-1 a no-op for existing content:
            // the walk branch is gated on this enum, so pre-batch bit-identity is
            // structural (the branch is not entered), not an empirical claim.
            var card = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
            try
            {
                Assert.That(card.arpeggioToneMode,
                    Is.EqualTo(BassArpeggioToneMode.RepeatedNote));
                Assert.That((int)BassArpeggioToneMode.RepeatedNote, Is.EqualTo(0),
                    "append-only enum: RepeatedNote must stay 0 (serialized)");
                Assert.That((int)BassArpeggioToneMode.ChordToneWalk, Is.EqualTo(1));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(card);
            }
        }

        [Test]
        public void ArpeggioToneMode_IsBassOnly_ChordExpressionTypeIsUntouched()
        {
            // D-WALK-SURF=A: BASS-WALK-1's opt-in is the bass-only
            // BassArpeggioToneMode enum — that batch added NO ChordExpressionType
            // member, so nothing leaked into the shared engine or the backing
            // card's §8.5 Random pool. Pinning the Tier-2 tail is the cheap way
            // to catch an accidental append during a later batch: this assertion
            // must only ever be updated BY a governed Tier-2 batch that
            // deliberately appends, never to make a red suite go green.
            //   9 = BassUpperSplit, appended by CA-T2-BOSSA as `Bossa` and
            //       RENAMED by CA-T2-BOSSA-V2 (OD-BOSSA-7=A/-7a=A; value 9
            //       intact — enums serialize by VALUE, so no asset changed).
            //  10 = Bossa, the AUTHENTIC 1-bar comping template appended by
            //       CA-T2-BOSSA-V2. This tripwire fired on both deliberate
            //       edits, which is what it is for (OD-BOSSA-6=A).
            Assert.That((int)ChordExpressionType.Random, Is.EqualTo(6));
            Assert.That((int)ChordExpressionType.PowerChord, Is.EqualTo(7));
            Assert.That((int)ChordExpressionType.Chugging, Is.EqualTo(8));
            Assert.That((int)ChordExpressionType.BassUpperSplit, Is.EqualTo(9));
            Assert.That((int)ChordExpressionType.Bossa, Is.EqualTo(10));
            Assert.That(System.Enum.GetValues(typeof(ChordExpressionType)).Length,
                Is.EqualTo(11),
                "an append here must be a governed Tier-2 batch, not an accident");

            // §8.5 pool exclusion, by the same mechanism as PowerChord/Chugging
            // (D-T2-POOL=A′): Tier-2 members sit at or above the sentinel, so the
            // uniform roll pool can never reach them.
            Assert.That((int)ChordExpressionType.BassUpperSplit,
                Is.GreaterThanOrEqualTo(RandomArticulationRoller.ConcretePoolSize),
                "BassUpperSplit must stay out of the Random roll pool");
            Assert.That((int)ChordExpressionType.Bossa,
                Is.GreaterThanOrEqualTo(RandomArticulationRoller.ConcretePoolSize),
                "Bossa must stay out of the Random roll pool");
        }
    }
}
#endif
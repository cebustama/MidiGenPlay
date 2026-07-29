#if UNITY_EDITOR
// MGP-ALWTTT-BASS-POCKET-1 — rhythm-side onset publication (D-PKT-SRC=B).
//
// Pins the publication seam RhythmTrackComposer.ExtractResolvedOnsets (a pure
// mirror of ComposeFromGrid's step→beat math with the three contract deltas:
// truncation to the part end, audibility filter over the kit, deterministic
// (beat, instrument) ordering), the semantic-lane rule under PERC-FALLBACK-1
// substitution, the StepState velocity-sentinel resolution, and — at the
// Compose level — that the GRID path publishes through the ctx sink while the
// PROCEDURAL path publishes nothing (the consumer's degrade trigger).
// Also pins the orchestrator's first-publisher channel helpers (internal
// static seams, same idiom as the seed-derivation seams).
//
// See runtime/SSoT_Composer_Rhythm_Track.md (onset publication section, this
// batch) and runtime/SSoT_Runtime_Generation_Orchestration.md §5.

using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    public class RhythmTrackComposer_OnsetPublicationTests
    {
        // ------------------------------------------------------------------
        // Fixture helpers
        // ------------------------------------------------------------------

        /// <summary>Kit whose ONLY mapping is the acoustic kick — snare and
        /// hats resolve to None (their fallback families are absent too), so
        /// audibility filtering is observable.</summary>
        private static MIDIPercussionInstrumentSO KickOnlyKit()
        {
            var k = ScriptableObject.CreateInstance<MIDIPercussionInstrumentSO>();
            k.name = "KickOnlyKit";
            k.percussionMappings.Clear();
            k.percussionMappings.Add(new MIDIPercussionInstrumentSO.PercussionMapping
            {
                percussionType = GeneralMidiPercussion.AcousticBassDrum,
                noteName = Melanchall.DryWetMidi.MusicTheory.NoteName.C,
                octave = 1,
            });
            return k;
        }

        private static DrumPatternData Pattern(
            int measures, int subdivisions,
            params (GeneralMidiPercussion inst, int defVel, int[] steps, int[] vels)[] lanes)
        {
            var d = ScriptableObject.CreateInstance<DrumPatternData>();
            d.name = "OnsetPattern";
            d.DisplayName = d.name;
            d.TimeSignature = TimeSignature.FourFour;
            d.beatsPerMeasure = 4;
            d.subdivisions = subdivisions;
            d.Measures = measures;
            d.lanes = new List<DrumPatternData.Lane>();

            int total = measures * 4 * subdivisions;
            foreach (var (inst, defVel, steps, vels) in lanes)
            {
                var lane = new DrumPatternData.Lane
                {
                    instrument = inst,
                    defaultVelocity = defVel,
                    steps = new List<DrumPatternData.StepState>(),
                };
                for (int s = 0; s < total; s++)
                {
                    int i = System.Array.IndexOf(steps, s);
                    lane.steps.Add(i >= 0
                        ? DrumPatternData.StepState.On(vels != null ? vels[i] : 0)
                        : DrumPatternData.StepState.Off);
                }
                d.lanes.Add(lane);
            }
            return d;
        }

        // ------------------------------------------------------------------
        // ExtractResolvedOnsets — math
        // ------------------------------------------------------------------

        [Test]
        public void Extract_BeatsMirrorGridMath()
        {
            // 1 measure of 4/4, 4 steps/beat: kick on steps 0 and 8 => beats
            // 0.0 and 2.0; an offbeat kick on step 6 => beat 1.5.
            var kit = Dbg1Fixtures.Kit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.AcousticBassDrum, 110,
                 new[] { 0, 6, 8 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Select(o => o.beat),
                Is.EqualTo(new[] { 0.0, 1.5, 2.0 }));
            Assert.That(onsets.All(o =>
                o.instrument == GeneralMidiPercussion.AcousticBassDrum));
        }

        [Test]
        public void Extract_RepeatsThePatternAcrossThePart()
        {
            // 1-measure pattern over a 2-measure part: kick at beat 0 repeats
            // at beat 4 (steps re-offset per repeat, as ComposeFromGrid does).
            var kit = Dbg1Fixtures.Kit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.AcousticBassDrum, 110,
                 new[] { 0 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 2);

            Assert.That(onsets.Select(o => o.beat), Is.EqualTo(new[] { 0.0, 4.0 }));
        }

        [Test]
        public void Extract_TruncatesAtThePartEnd()
        {
            // 2-measure pattern, 1-measure part: the measure-2 kick (step 16,
            // beat 4.0) lies beyond the part and must NOT be published, even
            // though ComposeFromGrid's ceil-repeat would emit it.
            var kit = Dbg1Fixtures.Kit();
            var pat = Pattern(2, 4,
                (GeneralMidiPercussion.AcousticBassDrum, 110,
                 new[] { 0, 16 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Select(o => o.beat), Is.EqualTo(new[] { 0.0 }));
        }

        // ------------------------------------------------------------------
        // ExtractResolvedOnsets — audibility, semantics, velocity
        // ------------------------------------------------------------------

        [Test]
        public void Extract_UnresolvedLanesAreExcluded()
        {
            // Kick-only kit: the snare lane resolves to None => not published.
            var kit = KickOnlyKit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.AcousticBassDrum, 110, new[] { 0 }, null),
                (GeneralMidiPercussion.AcousticSnare, 100, new[] { 4 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Count, Is.EqualTo(1));
            Assert.That(onsets[0].instrument,
                Is.EqualTo(GeneralMidiPercussion.AcousticBassDrum));
        }

        [Test]
        public void Extract_PublishesSemanticLane_UnderFamilySubstitution()
        {
            // Kick-only kit + a BassDrum1 lane: PERC-FALLBACK-1 substitutes to
            // the mapped AcousticBassDrum (audible), but the PUBLISHED
            // instrument stays the authored semantic lane — consumers classify
            // on it, immune to what concrete note sounds.
            var kit = KickOnlyKit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.BassDrum1, 110, new[] { 0 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Count, Is.EqualTo(1));
            Assert.That(onsets[0].instrument,
                Is.EqualTo(GeneralMidiPercussion.BassDrum1));
        }

        [Test]
        public void Extract_VelocityFollowsTheStepSentinelRule()
        {
            // step velocity 0 => lane default; explicit velocity passes
            // through; both clamped 1..127 (same values ComposeFromGrid emits).
            var kit = Dbg1Fixtures.Kit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.AcousticBassDrum, 110,
                 new[] { 0, 4 }, new[] { 0, 37 }));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Select(o => o.velocity), Is.EqualTo(new[] { 110, 37 }));
        }

        [Test]
        public void Extract_SortedByBeatThenInstrument()
        {
            var kit = Dbg1Fixtures.Kit();
            var pat = Pattern(1, 4,
                (GeneralMidiPercussion.AcousticSnare, 100, new[] { 0, 4 }, null),
                (GeneralMidiPercussion.AcousticBassDrum, 110, new[] { 0, 8 }, null));

            var onsets = RhythmTrackComposer.ExtractResolvedOnsets(
                kit, pat, TimeSignature.FourFour, partMeasures: 1);

            Assert.That(onsets.Select(o => (o.beat, o.instrument)),
                Is.EqualTo(new[]
                {
                    (0.0, GeneralMidiPercussion.AcousticBassDrum), // 35 < 38
                    (0.0, GeneralMidiPercussion.AcousticSnare),
                    (1.0, GeneralMidiPercussion.AcousticSnare),
                    (2.0, GeneralMidiPercussion.AcousticBassDrum),
                }));
        }

        // ------------------------------------------------------------------
        // Compose-level publication (grid publishes, procedural doesn't)
        // ------------------------------------------------------------------

        [Test]
        public void Compose_GridPathPublishes_ProceduralPathDoesNot()
        {
            var settings = Dbg1Fixtures.Settings();
            var composer = new RhythmTrackComposer(settings);
            var kit = Dbg1Fixtures.Kit();
            var part = Dbg1Fixtures.Part(); // meter/tonality only

            List<MidiGenerator.RhythmOnset> captured = null;
            string capturedMus = null;
            var ctx = new MidiGenerator.GenContext
            {
                Settings = settings,
                rng = new System.Random(7),
                SetRhythmOnsetsForPartMusician = (p, mus, onsets) =>
                {
                    captured = onsets;
                    capturedMus = mus;
                },
            };

            // GRID path: pattern present => publishes.
            var gridCfg = new SongConfig.PartConfig.TrackConfig
            {
                Role = TrackRole.Rhythm,
                MusicianId = "drummer",
                PercussionInstrument = kit,
                Parameters = new TrackParameters
                {
                    Pattern = Dbg1Fixtures.DrumPattern("PubDrums"),
                },
            };
            composer.Compose(part, gridCfg, bpm: 120, channel: 9, ctx);

            Assert.That(captured, Is.Not.Null.And.Not.Empty,
                "grid path must publish the resolved onsets");
            Assert.That(capturedMus, Is.EqualTo("drummer"));
            Assert.That(captured.Any(o =>
                o.instrument == GeneralMidiPercussion.AcousticBassDrum));

            // PROCEDURAL path: no pattern => publishes nothing (v1 scope).
            captured = null;
            var procCfg = new SongConfig.PartConfig.TrackConfig
            {
                Role = TrackRole.Rhythm,
                MusicianId = "drummer",
                PercussionInstrument = kit,
                Parameters = new TrackParameters(),
            };
            composer.Compose(part, procCfg, bpm: 120, channel: 9, ctx);

            Assert.That(captured, Is.Null,
                "procedural path must NOT publish in v1 — the consumer degrade trigger");
        }

        // ------------------------------------------------------------------
        // Orchestrator channel helpers — first-publisher semantics
        // ------------------------------------------------------------------

        [Test]
        public void Channel_FirstNonEmptyPublicationWins_EmptyIgnored()
        {
            var store = new Dictionary<SongConfig.PartConfig,
                List<(string, List<MidiGenerator.RhythmOnset>)>>();
            var set = SongOrchestrator.CreateSetRhythmOnsetsForPartMusician(
                store, settings: null);
            var get = SongOrchestrator.CreateGetRhythmOnsetsForPart(store);

            var part = Dbg1Fixtures.Part();

            Assert.That(get(part), Is.Null, "nothing published yet");

            var empty = new List<MidiGenerator.RhythmOnset>();
            set(part, "ghost", empty); // ignored: publishing nothing must be
                                       // indistinguishable from not publishing
            Assert.That(get(part), Is.Null);

            var a = new List<MidiGenerator.RhythmOnset>
            {
                new MidiGenerator.RhythmOnset
                {
                    instrument = GeneralMidiPercussion.AcousticBassDrum,
                    beat = 0, velocity = 110,
                },
            };
            var b = new List<MidiGenerator.RhythmOnset>
            {
                new MidiGenerator.RhythmOnset
                {
                    instrument = GeneralMidiPercussion.AcousticSnare,
                    beat = 1, velocity = 90,
                },
            };
            set(part, "drummerA", a);
            set(part, "drummerB", b);

            Assert.That(get(part), Is.SameAs(a),
                "first publisher (publication order) wins");

            // Re-publication by the same musician replaces IN PLACE — it does
            // not lose its first-publisher slot.
            var a2 = new List<MidiGenerator.RhythmOnset>(a);
            set(part, "drummerA", a2);
            Assert.That(get(part), Is.SameAs(a2));
        }
    }
}
#endif
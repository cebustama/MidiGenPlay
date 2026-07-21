#if UNITY_EDITOR
// MGP-MIX-1 — consumer-side mix gain (D-MIX-1..5).
//
// Covers, per the batch DoD:
//   - Identity gates: null map == empty map == entry-less render == re-run
//     with the same seed => bit-identical to the pre-seam output (the 1.1.0
//     guarantee). FNV-golden idiom over serialized bytes (Dbg1Fixtures.Fnv).
//   - Application: an entry emits exactly ONE CC7 on THAT track's channel,
//     value = clamp(round(volume01 * gain * 100), 0, 127); other tracks'
//     stems stay byte-identical.
//   - Composition law: volume01 (package-side nominal) multiplies the
//     consumer gain; gain=0 or volume01=0 => CC7=0 with note events intact.
//   - Clamp: gain large enough => 127 (volume01 is authoring-clamped 0..1,
//     so headroom above identity comes only from gain > 1).
//   - Rhythm exclusion (D-MIX-4=A): entry => warn + ignore, no CC7, output
//     bit-identical, no readback entry.
//   - Readback (D-MIX-5=A): appliedCc7ByTrack contains exactly the melodic
//     keys that had an entry, with the emitted value.
//   - Determinism: same seed + same map twice => identical bytes; the gain
//     path touches no RNG (a gained render differs from baseline ONLY by the
//     inserted CC7 events).

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class SongOrchestrator_MixGainTests
    {
        private const string Mus = Dbg1Fixtures.Musician;

        private static readonly MusicianTrackKey BackingKey =
            new MusicianTrackKey(Mus, TrackRole.Backing);
        private static readonly MusicianTrackKey BassKey =
            new MusicianTrackKey(Mus, TrackRole.Bassline);
        private static readonly MusicianTrackKey RhythmKey =
            new MusicianTrackKey(Mus, TrackRole.Rhythm);

        // ------------------------------------------------------------------
        // Local harness
        // ------------------------------------------------------------------

        private static SongConfig.PartConfig BackingBassPart(MIDIInstrumentSO inst)
            => Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    pattern: Dbg1Fixtures.Progression("ProgA",
                        (ScaleDegree.Tonic, ChordQuality.Major),
                        (ScaleDegree.Dominant, ChordQuality.Major))),
                Dbg1Fixtures.Track(TrackRole.Bassline, inst));

        /// <summary>Render with the MIX-1 surface. Mirrors Dbg1Fixtures.Render
        /// but forwards mixGains (the fixture predates the seam).</summary>
        private static PartRender MixRender(
            SongOrchestrator orch,
            SongConfig.PartConfig part,
            IReadOnlyDictionary<MusicianTrackKey, float> mixGains,
            int seed = 7)
        {
            var roles = part.Tracks.Select(t => t.Role).ToList();
            return orch.GenerateSinglePart(
                part, roles, partIndex: 0, bpmOverride: 120,
                instrumentOverrides: null, seedOverride: seed,
                patternOverrides: null, mixGains: mixGains);
        }

        /// <summary>All CC7 (channel-volume) events in a file, as
        /// (channel, value) pairs.</summary>
        private static List<(int channel, int value)> Cc7Events(MidiFile file)
        {
            return file.GetTrackChunks()
                .SelectMany(c => c.Events)
                .OfType<ControlChangeEvent>()
                .Where(e => (int)e.ControlNumber == 7)
                .Select(e => ((int)e.Channel, (int)e.ControlValue))
                .ToList();
        }

        // ------------------------------------------------------------------
        // Identity gates — the 1.1.0 bit-identity guarantee
        // ------------------------------------------------------------------

        [Test]
        public void NoGain_NullMap_EmptyMap_AndRerun_AreBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            ulong RenderHash(IReadOnlyDictionary<MusicianTrackKey, float> gains)
                => Dbg1Fixtures.Fnv(
                    MixRender(orch, BackingBassPart(inst), gains, seed: 7).merged);

            var baseline = RenderHash(null);
            var emptyMap = RenderHash(new Dictionary<MusicianTrackKey, float>());
            var rerun = RenderHash(null);

            Assert.That(emptyMap, Is.EqualTo(baseline),
                "An empty mixGains map must be bit-identical to no map (per-entry emission gate).");
            Assert.That(rerun, Is.EqualTo(baseline),
                "Same inputs + same seed must stay bit-identical (determinism invariant).");
        }

        [Test]
        public void NoGain_EmitsNoCc7AndNoReadback()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var render = MixRender(orch, BackingBassPart(inst), null, seed: 7);

            Assert.That(Cc7Events(render.merged), Is.Empty,
                "The pre-seam pipeline emits no CC7; the seam must add none without entries.");
            Assert.That(render.appliedCc7ByTrack, Is.Empty);
        }

        // ------------------------------------------------------------------
        // Application + composition law (D-MIX-1 / D-MIX-3)
        // ------------------------------------------------------------------

        [Test]
        public void GainEntry_EmitsOneCc7OnThatTracksChannel_OthersUntouched()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument(); // volume01 defaults to 1.0
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var baseline = MixRender(orch, BackingBassPart(inst), null, seed: 7);
            var render = MixRender(orch, BackingBassPart(inst),
                new Dictionary<MusicianTrackKey, float> { [BackingKey] = 0.5f },
                seed: 7);

            // Exactly one CC7 in the whole render: round(1.0 * 0.5 * 100) = 50.
            var cc7 = Cc7Events(render.merged);
            Assert.That(cc7.Count, Is.EqualTo(1),
                "Exactly one CC7 per entried track, none elsewhere.");
            Assert.That(cc7[0].value, Is.EqualTo(50),
                "effective = volume01 * gain, identity-scaled to 100.");

            // It sits on the Backing stem's channel; the Bassline stem is
            // byte-identical to baseline (the gain leaks to no other track).
            Assert.That(Cc7Events(render.stemsByMusician[BackingKey]).Count,
                Is.EqualTo(1));
            Assert.That(Cc7Events(render.stemsByMusician[BassKey]), Is.Empty);
            Assert.That(Dbg1Fixtures.Fnv(render.stemsByMusician[BassKey]),
                Is.EqualTo(Dbg1Fixtures.Fnv(baseline.stemsByMusician[BassKey])),
                "A gain on one track must leave every other track's bytes untouched.");

            // Readback (D-MIX-5=A).
            Assert.That(render.appliedCc7ByTrack.Keys,
                Is.EquivalentTo(new[] { BackingKey }));
            Assert.That(render.appliedCc7ByTrack[BackingKey], Is.EqualTo(50));
        }

        [Test]
        public void Volume01_ComposesMultiplicativelyWithGain()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            inst.volume01 = 0.5f; // package-side nominal level
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var render = MixRender(orch, BackingBassPart(inst),
                new Dictionary<MusicianTrackKey, float> { [BackingKey] = 0.5f },
                seed: 7);

            // round(0.5 * 0.5 * 100) = 25.
            Assert.That(render.appliedCc7ByTrack[BackingKey], Is.EqualTo(25));
        }

        [Test]
        public void GainClampsTo127_AndZeroMutesWithoutRemovingNotes()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var loud = MixRender(orch, BackingBassPart(inst),
                new Dictionary<MusicianTrackKey, float> { [BackingKey] = 2.0f },
                seed: 7);
            Assert.That(loud.appliedCc7ByTrack[BackingKey], Is.EqualTo(127),
                "volume01(1.0) * gain(2.0) * 100 = 200 must clamp to 127.");

            var muted = MixRender(orch, BackingBassPart(inst),
                new Dictionary<MusicianTrackKey, float> { [BackingKey] = 0f },
                seed: 7);
            Assert.That(muted.appliedCc7ByTrack[BackingKey], Is.EqualTo(0));
            Assert.That(
                muted.stemsByMusician[BackingKey].GetTrackChunks()
                    .SelectMany(c => c.Events).OfType<NoteOnEvent>().Any(),
                Is.True,
                "CC7=0 mutes at playback; the note events must remain in the bytes.");
        }

        // ------------------------------------------------------------------
        // Rhythm exclusion (D-MIX-4=A)
        // ------------------------------------------------------------------

        [Test]
        public void RhythmEntry_WarnsIgnores_BitIdentical_NoReadback()
        {
            var settings = Dbg1Fixtures.Settings();
            var kit = Dbg1Fixtures.Kit();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);

            SongConfig.PartConfig RhythmPart() => Dbg1Fixtures.Part(
                new SongConfig.PartConfig.TrackConfig
                {
                    Role = TrackRole.Rhythm,
                    MusicianId = Mus,
                    PercussionInstrument = kit,
                    Parameters = new TrackParameters
                    {
                        Pattern = Dbg1Fixtures.DrumPattern("DrumA"),
                    },
                });

            var baseline = MixRender(orch, RhythmPart(), null, seed: 7);

            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[MixGain\] Rhythm entries are ignored in v1"));

            var render = MixRender(orch, RhythmPart(),
                new Dictionary<MusicianTrackKey, float> { [RhythmKey] = 0.5f },
                seed: 7);

            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)),
                "A Rhythm entry must fall through to the exact baseline output.");
            Assert.That(Cc7Events(render.merged), Is.Empty);
            Assert.That(render.appliedCc7ByTrack, Is.Empty);
        }

        // ------------------------------------------------------------------
        // Determinism (SEED-1 invariant intact)
        // ------------------------------------------------------------------

        [Test]
        public void SameSeedSameMap_TwoRenders_AreBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var gains = new Dictionary<MusicianTrackKey, float>
            {
                [BackingKey] = 0.7f,
                [BassKey] = 1.2f,
            };

            var a = Dbg1Fixtures.Fnv(
                MixRender(orch, BackingBassPart(inst), gains, seed: 7).merged);
            var b = Dbg1Fixtures.Fnv(
                MixRender(orch, BackingBassPart(inst), gains, seed: 7).merged);

            Assert.That(b, Is.EqualTo(a),
                "The gain path is pure data: same seed + same map => same bytes.");
        }

        [Test]
        public void GainPath_TouchesNoRng_OnlyDeltaIsTheCc7Events()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var baseline = MixRender(orch, BackingBassPart(inst), null, seed: 7);
            var gained = MixRender(orch, BackingBassPart(inst),
                new Dictionary<MusicianTrackKey, float> { [BackingKey] = 0.5f },
                seed: 7);

            // Strip every CC7 from the gained render; what remains must be
            // note-for-note identical to baseline — proving the seam consumed
            // no randomness and displaced no draw.
            List<(long time, string ev)> NotesOf(MidiFile f) =>
                f.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                 .Where(te => te.Event is NoteOnEvent || te.Event is NoteOffEvent)
                 .Select(te => (te.Time, te.Event.ToString()))
                 .OrderBy(t => t.Item1).ThenBy(t => t.Item2)
                 .ToList();

            Assert.That(NotesOf(gained.merged),
                Is.EqualTo(NotesOf(baseline.merged)),
                "With the CC7 set aside, a gained render must be note-identical to baseline.");
        }
    }
}
#endif
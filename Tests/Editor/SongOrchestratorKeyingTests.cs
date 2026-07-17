#if UNITY_EDITOR
// MGP-ALWTTT-DBG-1 — (musicianId, TrackRole) keying tests.
//
// Covers the D-DBG1=A breaking re-key of PartRender.stemsByMusician /
// melInstByMusician / instrumentOverrides and the ID-1=A tag extension
// ("mus:{id}:{role}"):
//   1. FormatMusicianTag / TryParseMusicianTag — pure round-trip seam
//      (internal via Runtime/AssemblyInfo.cs InternalsVisibleTo, the same
//      idiom as SongOrchestratorSeedTests).
//   2. Integration: ONE musician owning TWO roles (Backing + Bassline, the
//      BASS-1 collision case) renders to TWO distinct stem entries, two
//      melInst entries and two readback entries — the exact loss the string
//      key caused.
//
// Fixture note: MIDIInstrumentSO / MidiGenPlayConfig are constructed with
// ScriptableObject.CreateInstance and public-field setup (octaveMin/octaveMax/
// BankName/PatchIndex, defaultSeed) — the same surface the runtime composers
// read.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    // ----------------------------------------------------------------------
    // Shared DBG-1 fixtures (also used by PatternOverrideAndReadbackTests and
    // ChordMarkerParityTests — same assembly).
    // ----------------------------------------------------------------------
    internal static class Dbg1Fixtures
    {
        public const string Musician = "bob";

        public static MidiGenPlayConfig Settings(int seed = 12345)
        {
            var s = ScriptableObject.CreateInstance<MidiGenPlayConfig>();
            s.defaultSeed = seed;
            return s;
        }

        public static MIDIInstrumentSO Instrument(string assetName = "TestInst")
        {
            var i = ScriptableObject.CreateInstance<MIDIInstrumentSO>();
            i.name = assetName;
            i.octaveMin = 3;
            i.octaveMax = 5;
            i.BankName = "0";
            i.PatchIndex = 0;
            return i;
        }

        /// <summary>4/4, 1 measure, 4 steps/beat; chords split the bar evenly.</summary>
        public static ChordProgressionData Progression(
            string assetName,
            params (ScaleDegree deg, ChordQuality q)[] chords)
        {
            var p = ScriptableObject.CreateInstance<ChordProgressionData>();
            p.name = assetName;
            p.DisplayName = assetName;
            p.TimeSignature = TimeSignature.FourFour;
            p.Measures = 1;
            p.subdivisions = 4;
            p.events = new List<ChordProgressionData.ChordEvent>();

            int stepsPerMeasure = 4 * 4;
            int len = Mathf.Max(1, stepsPerMeasure / Mathf.Max(1, chords.Length));
            for (int i = 0; i < chords.Length; i++)
            {
                p.events.Add(new ChordProgressionData.ChordEvent
                {
                    degree = chords[i].deg,
                    quality = chords[i].q,
                    degreeAccidental = 0,
                    startStep = i * len,
                    lengthSteps = len,
                    velocity = 96,
                });
            }
            return p;
        }

        public static MelodyPatternData MelodyPattern(
            string assetName, params ScaleDegree[] quarterNotes)
        {
            var m = ScriptableObject.CreateInstance<MelodyPatternData>();
            m.name = assetName;
            m.DisplayName = assetName;
            m.TimeSignature = TimeSignature.FourFour;
            m.Measures = 1;
            m.beatsPerMeasure = 4;
            m.subdivisions = 4;
            m.notes = new List<MelodyPatternData.MelodyNoteEvent>();
            for (int i = 0; i < quarterNotes.Length; i++)
                m.notes.Add(MelodyPatternData.MelodyNoteEvent.Create(
                    quarterNotes[i], startBeat: i, durationBeats: 1f));
            return m;
        }

        /// <summary>Percussion kit using the SO's built-in default GM mappings
        /// (AcousticBassDrum, ClosedHiHat, etc. are all present), so the fixture
        /// needs no manual mapping setup. Bank/patch come from the
        /// MIDIInstrumentSO base and default fine (StampBankAndPatch tolerates a
        /// non-numeric bank).</summary>
        public static MIDIPercussionInstrumentSO Kit(string assetName = "TestKit")
        {
            var k = ScriptableObject.CreateInstance<MIDIPercussionInstrumentSO>();
            k.name = assetName;
            return k;
        }

        /// <summary>4/4, 1 measure, 4 steps/beat (16 steps). Two lanes:
        /// AcousticBassDrum on beats 1 and 3, ClosedHiHat on every step — enough
        /// hits that the grid path (hasGrid) runs and the render differs between
        /// distinct patterns.</summary>
        public static DrumPatternData DrumPattern(
            string assetName, bool denseKick = false)
        {
            var d = ScriptableObject.CreateInstance<DrumPatternData>();
            d.name = assetName;
            d.DisplayName = assetName;
            d.TimeSignature = TimeSignature.FourFour;
            d.beatsPerMeasure = 4;
            d.subdivisions = 4;
            d.Measures = 1;
            d.lanes = new List<DrumPatternData.Lane>();

            int total = 4 * 4;
            var kick = new DrumPatternData.Lane
            {
                instrument = GeneralMidiPercussion.AcousticBassDrum,
                defaultVelocity = 110,
                steps = new List<DrumPatternData.StepState>(),
            };
            var hat = new DrumPatternData.Lane
            {
                instrument = GeneralMidiPercussion.ClosedHiHat,
                defaultVelocity = 80,
                steps = new List<DrumPatternData.StepState>(),
            };
            for (int s = 0; s < total; s++)
            {
                // denseKick flips the kick placement so two patterns render
                // audibly (and byte-) differently.
                bool kickHit = denseKick ? (s % 4 == 2) : (s % 8 == 0);
                kick.steps.Add(kickHit
                    ? DrumPatternData.StepState.On() : DrumPatternData.StepState.Off);
                hat.steps.Add(DrumPatternData.StepState.On());
            }
            d.lanes.Add(kick);
            d.lanes.Add(hat);
            return d;
        }

        /// <summary>Orchestrator that additionally wires the Rhythm factory, for
        /// the drum-override render test.</summary>
        public static SongOrchestrator OrchestratorWithRhythm(MidiGenPlayConfig settings)
        {
            var melodicLeading = ScriptableObject.CreateInstance<MelodicLeadingConfig>();
            var factories = new Dictionary<TrackRole, ITrackComposerFactory>
            {
                [TrackRole.Rhythm] = new RhythmTrackComposerFactory(settings),
                [TrackRole.Backing] = new ChordTrackComposerFactory(settings, null),
                [TrackRole.Bassline] = new BassTrackComposerFactory(settings, randomChordTone: false),
                [TrackRole.Melody] = new MelodyTrackComposerFactory(
                    settings, melodicLeading, new ScaleFlowMelodyStrategy()),
            };
            return new SongOrchestrator(settings, factories, voicer: null);
        }

        public static SongConfig.PartConfig.TrackConfig Track(
            TrackRole role, MIDIInstrumentSO inst,
            PatternDataSO pattern = null, TrackStyleBundleSO style = null,
            string musicianId = Musician)
        {
            return new SongConfig.PartConfig.TrackConfig
            {
                Role = role,
                MusicianId = musicianId,
                Instrument = inst,
                Parameters = new TrackParameters { Pattern = pattern, Style = style },
            };
        }

        public static SongConfig.PartConfig Part(
            params SongConfig.PartConfig.TrackConfig[] tracks)
        {
            return new SongConfig.PartConfig
            {
                Name = "DBG1-Part",
                Tonality = Tonality.Ionian,
                RootNote = DwmNoteName.C,
                TimeSignature = TimeSignature.FourFour,
                Measures = 1,
                TempoRange = TempoRange.Moderate,
                Tracks = new List<SongConfig.PartConfig.TrackConfig>(tracks),
            };
        }

        /// <summary>Orchestrator wired with the Backing / Bassline / Melody
        /// factories these tests exercise (no Rhythm — a percussion-kit
        /// fixture needs MIDIPercussionInstrumentSO mapping setup).</summary>
        public static SongOrchestrator Orchestrator(MidiGenPlayConfig settings)
        {
            var melodicLeading = ScriptableObject.CreateInstance<MelodicLeadingConfig>();
            var factories = new Dictionary<TrackRole, ITrackComposerFactory>
            {
                [TrackRole.Backing] = new ChordTrackComposerFactory(settings, null),
                [TrackRole.Bassline] = new BassTrackComposerFactory(settings, randomChordTone: false),
                [TrackRole.Melody] = new MelodyTrackComposerFactory(
                    settings, melodicLeading, new ScaleFlowMelodyStrategy()),
            };
            return new SongOrchestrator(settings, factories, voicer: null);
        }

        public static PartRender Render(
            SongOrchestrator orch,
            SongConfig.PartConfig part,
            IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> overrides = null,
            int seed = 7)
        {
            var roles = part.Tracks.Select(t => t.Role).ToList();
            return orch.GenerateSinglePart(
                part, roles, partIndex: 0, bpmOverride: 120,
                instrumentOverrides: null, seedOverride: seed,
                patternOverrides: overrides);
        }

        /// <summary>FNV-1a 64 over the serialized MidiFile bytes — the golden
        /// idiom of SongOrchestratorSeedTests, applied to whole renders.</summary>
        public static ulong Fnv(MidiFile file)
        {
            using var ms = new System.IO.MemoryStream();
            file.Write(ms);
            var bytes = ms.ToArray();
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < bytes.Length; i++)
                {
                    h ^= bytes[i];
                    h *= 1099511628211UL;
                }
                return h;
            }
        }
    }

    public class SongOrchestratorKeyingTests
    {
        // ------------------------------------------------------------------
        // Tag round-trip (ID-1=A, pure seam)
        // ------------------------------------------------------------------

        [Test]
        public void Tag_Format_CarriesIdAndRole()
        {
            Assert.That(
                SongOrchestrator.FormatMusicianTag("bob", TrackRole.Backing),
                Is.EqualTo("mus:bob:Backing"));
        }

        [Test]
        public void Tag_Parse_RoundTrips()
        {
            var text = SongOrchestrator.FormatMusicianTag("bob", TrackRole.Bassline);
            Assert.That(
                SongOrchestrator.TryParseMusicianTag(text, out var id, out var role),
                Is.True);
            Assert.That(id, Is.EqualTo("bob"));
            Assert.That(role, Is.EqualTo(TrackRole.Bassline));
        }

        [Test]
        public void Tag_Parse_IdContainingColon_LastSegmentIsRole()
        {
            var text = SongOrchestrator.FormatMusicianTag("guild:bob", TrackRole.Melody);
            Assert.That(
                SongOrchestrator.TryParseMusicianTag(text, out var id, out var role),
                Is.True);
            Assert.That(id, Is.EqualTo("guild:bob"));
            Assert.That(role, Is.EqualTo(TrackRole.Melody));
        }

        [Test]
        public void Tag_Parse_LegacyFormatWithoutRole_Fails()
        {
            // Pre-batch tag shape: skipped, never mis-keyed.
            Assert.That(
                SongOrchestrator.TryParseMusicianTag("mus:bob", out _, out _),
                Is.False);
        }

        [Test]
        public void Tag_Parse_UnknownRole_Fails()
        {
            Assert.That(
                SongOrchestrator.TryParseMusicianTag("mus:bob:NotARole", out _, out _),
                Is.False);
        }

        [Test]
        public void Tag_Parse_NonMusText_Fails()
        {
            Assert.That(
                SongOrchestrator.TryParseMusicianTag("chd:0:I:C:1:Major", out _, out _),
                Is.False);
            Assert.That(
                SongOrchestrator.TryParseMusicianTag(null, out _, out _),
                Is.False);
        }

        // ------------------------------------------------------------------
        // Integration: one musician, two roles (the BASS-1 collision)
        // ------------------------------------------------------------------

        [Test]
        public void Render_SameMusicianTwoRoles_AllSurfacesKeyedDistinctly()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var prog = Dbg1Fixtures.Progression("ProgA",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst, pattern: prog),
                Dbg1Fixtures.Track(TrackRole.Bassline, inst));

            var render = Dbg1Fixtures.Render(
                Dbg1Fixtures.Orchestrator(settings), part);

            var backingKey = new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Backing);
            var bassKey = new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Bassline);

            // Stems: two entries, distinct keys — the string key collapsed these.
            Assert.That(render.stemsByMusician.Count, Is.EqualTo(2),
                "One musician in two roles must yield two stems.");
            Assert.That(render.stemsByMusician.ContainsKey(backingKey), Is.True);
            Assert.That(render.stemsByMusician.ContainsKey(bassKey), Is.True);

            // Instrument report: keyed per (musician, role).
            Assert.That(render.melInstByMusician.ContainsKey(backingKey), Is.True);
            Assert.That(render.melInstByMusician.ContainsKey(bassKey), Is.True);

            // Ask A readback: one entry per reporting role.
            Assert.That(render.resolvedByTrack.ContainsKey(backingKey), Is.True);
            Assert.That(render.resolvedByTrack.ContainsKey(bassKey), Is.True);

            // Identity stamped authoritatively by the orchestrator sink.
            Assert.That(render.resolvedByTrack[backingKey].musicianId,
                Is.EqualTo(Dbg1Fixtures.Musician));
            Assert.That(render.resolvedByTrack[backingKey].role,
                Is.EqualTo(TrackRole.Backing));
            Assert.That(render.resolvedByTrack[bassKey].role,
                Is.EqualTo(TrackRole.Bassline));
        }
    }
}
#endif
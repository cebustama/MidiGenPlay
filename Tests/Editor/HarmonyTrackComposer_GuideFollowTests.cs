#if UNITY_EDITOR
// MGP-ALWTTT-HARMONY-1 — guide-follow tests for HarmonyTrackComposer.
//
// Targets the named seams HarmonyTrackComposer.ResolveHarmonyNotesCore (byte-identical
// to ComposeHarmonyFromMelody's render loop) and HarmonyTrackComposer.ResolveGuideMelody,
// so the tests need no SongConfig / MidiFile fixtures — the same idiom as
// MelodyTrackComposer_PatternDeterminismTests.
//
// Both seams are declared PUBLIC, per the convention on record for EditMode test seams
// (runtime/SSoT_Runtime_Generation_Orchestration.md §5.6; see also the F-IVT-STALE note in
// Runtime/AssemblyInfo.cs — the InternalsVisibleTo directive there is inert, so `internal`
// seams do NOT compile from this assembly). Mirrors MelodyTrackComposer.ResolvePatternNotesCore
// / ResolvedMelodyNote, ChordTrackComposer.TryDirectionalFirstChordCore and
// BassTrackComposer.ResolveArticulation.
//
// Covers (the subset of audit item 8 that makes items 1 and 2 observable):
//  - item 1 / F-HARM-1: in 6/8 a guide beat is an EIGHTH; the chord looked up at guide
//    beat 3 must be the bar-1 chord. The legacy MusicalTimeSpan.Quarter conversion mapped
//    beat 3 to eighth-step 6 (bar 2) — that is the failure this pins.
//  - item 2 / F-HARM-3: canonical ChordProgressionData.FindChordEventAt, including its
//    defined wrap (guide beat 12 over a 12-step progression => step 0).
//  - item 2 / F-HARM-2: degreeAccidental is applied to the degree root (bIII in C Ionian
//    yields Eb-major chord tones, not E-major).
//  - D-H1-5a=B: own musician's melody is preferred over the first cached melody.
//  - Determinism: repeat-call sequence equality (the strategies are RNG-free).
//
// Chord choice: I (C E G) vs ii (D F A) share no pitch class, so chord-tone MEMBERSHIP of
// the harmony note discriminates the two lookups regardless of strategy tie-breaks.
// Melody D5 is used because it is a ii chord tone (excluded as unison by
// minDistanceFromMelody=3) and not an I chord tone.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Tests.Editor
{
    public class HarmonyTrackComposer_GuideFollowTests
    {
        private const Tonality Key = Tonality.Ionian;
        private const NoteName Root = NoteName.C;

        private static readonly NoteName[] I_Tones = { NoteName.C, NoteName.E, NoteName.G };
        private static readonly NoteName[] ii_Tones = { NoteName.D, NoteName.F, NoteName.A };

        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created)
                if (so != null) Object.DestroyImmediate(so);
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        // ---- fixtures ----

        private static ChordProgressionData.ChordEvent Ev(
            ScaleDegree degree, ChordQuality quality, int startStep, int lengthSteps,
            int accidental = 0) =>
            new ChordProgressionData.ChordEvent
            {
                degree = degree,
                quality = quality,
                startStep = startStep,
                lengthSteps = lengthSteps,
                degreeAccidental = accidental
            };

        private ChordProgressionData Prog(TimeSignature ts, int measures,
            params ChordProgressionData.ChordEvent[] events)
        {
            var p = Make<ChordProgressionData>();
            p.TimeSignature = ts;
            p.Measures = measures;
            p.subdivisions = 1; // 1 step per Part beat
            p.events = events.ToList();
            return p;
        }

        private MIDIInstrumentSO Inst()
        {
            var i = Make<MIDIInstrumentSO>();
            i.octaveMin = 3;
            i.octaveMax = 6;
            return i;
        }

        private HarmonicLeadingConfig Cfg()
        {
            // Defaults: relation=NearestDifferentChordTone, min=3, max=14.
            return Make<HarmonicLeadingConfig>();
        }

        private static MidiGenerator.GuideNote G(NoteName n, int oct, double startBeats, double durBeats = 1.0) =>
            new MidiGenerator.GuideNote
            {
                startBeats = startBeats,
                durBeats = durBeats,
                note = DryWetMidiNote.Get(n, oct)
            };

        private static TempoMap Tempo120() => TempoMap.Create(Tempo.FromBeatsPerMinute(120));

        private List<HarmonyTrackComposer.ResolvedHarmonyNote> Resolve(
            IReadOnlyList<MidiGenerator.GuideNote> guide, ChordProgressionData prog, TimeSignature ts) =>
            HarmonyTrackComposer.ResolveHarmonyNotesCore(
                guide, prog, Key, Root, ts, Tempo120(),
                new NearestChordToneHarmonyStrategy(), Cfg(), Inst(),
                new System.Random(1234));

        private static void AssertHarmonyAtBeat(
            List<HarmonyTrackComposer.ResolvedHarmonyNote> r, double beat, NoteName[] chordTones, string why)
        {
            var hits = r.Where(n => System.Math.Abs(n.WhenBeats - beat) < 1e-9).ToList();
            Assert.That(hits.Count, Is.EqualTo(1), $"expected exactly one harmony note at beat {beat} ({why})");
            Assert.That(chordTones, Does.Contain(hits[0].Note.NoteName),
                $"harmony at beat {beat} must be a chord tone of the chord sounding there ({why}); " +
                $"got {hits[0].Note}");
        }

        // ---- item 1 / F-HARM-1 + item 2 / F-HARM-3: 6/8 beat unit + canonical lookup ----

        [Test]
        public void SixEight_GuideBeatsAreEighths_ChordLookupFollowsPartBeatUnit_AndWraps()
        {
            // 2 bars of 6/8, 1 step per eighth: I on steps 0..5, ii on steps 6..11.
            var prog = Prog(TimeSignature.SixEight, measures: 2,
                Ev(ScaleDegree.Tonic, ChordQuality.Major, 0, 6),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, 6, 6));

            var guide = new List<MidiGenerator.GuideNote>
            {
                G(NoteName.D, 5, 0),   // bar 1, 1st eighth  -> I
                G(NoteName.D, 5, 3),   // bar 1, 4th eighth  -> I  (legacy Quarter mapped this to step 6 = ii)
                G(NoteName.D, 5, 6),   // bar 2, 1st eighth  -> ii
                G(NoteName.D, 5, 7),   // bar 2, 2nd eighth  -> ii
                G(NoteName.D, 5, 12),  // past the progression: canonical wrap => step 0 => I
            };

            var r = Resolve(guide, prog, TimeSignature.SixEight);

            Assert.That(r.Count, Is.EqualTo(5), "every guide note has a viable chord tone within [3,14] semis");
            AssertHarmonyAtBeat(r, 0, I_Tones, "bar 1");
            AssertHarmonyAtBeat(r, 3, I_Tones, "bar 1, would be ii under the legacy quarter conversion");
            AssertHarmonyAtBeat(r, 6, ii_Tones, "bar 2");
            AssertHarmonyAtBeat(r, 7, ii_Tones, "bar 2");
            AssertHarmonyAtBeat(r, 12, I_Tones, "wrap to step 0 (legacy lookup had no wrap)");

            // Timing is echoed 1:1 in Part beat units (the render loop converts with BeatsToSpan).
            Assert.That(r.Select(n => n.WhenBeats), Is.EqualTo(new[] { 0.0, 3.0, 6.0, 7.0, 12.0 }));
        }

        [Test]
        public void FourFour_GuideBeatsAreQuarters_ChordLookupUnchanged()
        {
            // 2 bars of 4/4: I on steps 0..3, ii on steps 4..7. Sanity companion to the 6/8 pin:
            // in a beat-unit==4 meter BeatsToSpan is a structural identity with Quarter.
            var prog = Prog(TimeSignature.FourFour, measures: 2,
                Ev(ScaleDegree.Tonic, ChordQuality.Major, 0, 4),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, 4, 4));

            var guide = new List<MidiGenerator.GuideNote>
            {
                G(NoteName.D, 5, 3),
                G(NoteName.D, 5, 4),
            };

            var r = Resolve(guide, prog, TimeSignature.FourFour);

            Assert.That(r.Count, Is.EqualTo(2));
            AssertHarmonyAtBeat(r, 3, I_Tones, "bar 1");
            AssertHarmonyAtBeat(r, 4, ii_Tones, "bar 2");
        }

        // ---- item 2 / F-HARM-2: accidental parity ----

        [Test]
        public void Accidental_IsAppliedToDegreeRoot_bIII_YieldsEbMajorTones()
        {
            // C Ionian, bIII (Mediant with degreeAccidental -1, Major) = Eb major = {D#, G, A#}.
            // Without the accidental the chord would be E major = {E, G#, B}: disjoint set.
            var prog = Prog(TimeSignature.FourFour, measures: 1,
                Ev(ScaleDegree.Mediant, ChordQuality.Major, 0, 4, accidental: -1));

            var guide = new List<MidiGenerator.GuideNote> { G(NoteName.G, 5, 0) };

            var r = Resolve(guide, prog, TimeSignature.FourFour);

            Assert.That(r.Count, Is.EqualTo(1));
            var eb = new[] { NoteName.DSharp, NoteName.G, NoteName.ASharp };
            var e = new[] { NoteName.E, NoteName.GSharp, NoteName.B };
            Assert.That(eb, Does.Contain(r[0].Note.NoteName), $"expected an Eb-major tone, got {r[0].Note}");
            // Has.No.Member, not Does.Not.Contain: on ConstraintExpression, Contain resolves
            // to the SUBSTRING overload, so a NoteName argument does not compile (CS1503).
            // The positive form above works because static Does.Contain(object) exists.
            Assert.That(e, Has.No.Member(r[0].Note.NoteName), "must not build against the un-accented degree");
        }

        [Test]
        public void Accidental_Zero_IsIdentity()
        {
            var prog = Prog(TimeSignature.FourFour, measures: 1,
                Ev(ScaleDegree.Mediant, ChordQuality.Major, 0, 4, accidental: 0));

            var guide = new List<MidiGenerator.GuideNote> { G(NoteName.G, 5, 0) };

            var r = Resolve(guide, prog, TimeSignature.FourFour);

            Assert.That(r.Count, Is.EqualTo(1));
            var e = new[] { NoteName.E, NoteName.GSharp, NoteName.B };
            Assert.That(e, Does.Contain(r[0].Note.NoteName), $"expected an E-major tone, got {r[0].Note}");
        }

        // ---- determinism ----

        [Test]
        public void ResolveHarmonyNotesCore_IsDeterministic_AcrossRepeatCalls()
        {
            var prog = Prog(TimeSignature.SixEight, measures: 2,
                Ev(ScaleDegree.Tonic, ChordQuality.Major, 0, 6),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, 6, 6));

            var guide = new List<MidiGenerator.GuideNote>
            {
                G(NoteName.E, 5, 0), G(NoteName.D, 5, 1.5, 0.5), G(NoteName.C, 5, 3),
                G(NoteName.B, 4, 6), G(NoteName.G, 5, 9),
            };

            var a = Resolve(guide, prog, TimeSignature.SixEight);
            var b = Resolve(guide, prog, TimeSignature.SixEight);

            Assert.That(Project(a), Is.EqualTo(Project(b)), "the resolution seam must be RNG-free / byte-stable");
        }

        private static IEnumerable<(double, double, int)> Project(List<HarmonyTrackComposer.ResolvedHarmonyNote> r) =>
            r.Select(n => (n.WhenBeats, n.DurBeats, (int)(byte)n.Note.NoteNumber)).ToList();

        // ---- D-H1-5a=B: guide-melody target resolution ----

        private static MidiGenerator.GenContext CtxWith(Dictionary<string, List<MidiGenerator.GuideNote>> cache)
        {
            return new MidiGenerator.GenContext
            {
                GetMelodyForPartMusician = (p, id) =>
                    (!string.IsNullOrEmpty(id) && cache.TryGetValue(id, out var l)) ? l : null,
                GetFirstMelodyMusicianIdForPart = (p) =>
                    cache.FirstOrDefault(kv => kv.Value != null && kv.Value.Count > 0).Key,
            };
        }

        [Test]
        public void ResolveGuideMelody_PrefersOwnMusician_OverFirstCached()
        {
            var other = new List<MidiGenerator.GuideNote> { G(NoteName.C, 5, 0) };
            var own = new List<MidiGenerator.GuideNote> { G(NoteName.E, 5, 0) };
            var cache = new Dictionary<string, List<MidiGenerator.GuideNote>>
            {
                { "other", other }, // inserted FIRST — the legacy pick
                { "zig",   own   },
            };
            var part = new SongConfig.PartConfig { Name = "P" };

            var got = HarmonyTrackComposer.ResolveGuideMelody(CtxWith(cache), part, "zig", out var target);

            Assert.That(target, Is.EqualTo("zig"));
            Assert.That(got, Is.SameAs(own));
        }

        [Test]
        public void ResolveGuideMelody_FallsBackToFirstCached_WhenOwnAbsentOrNull()
        {
            var other = new List<MidiGenerator.GuideNote> { G(NoteName.C, 5, 0) };
            var cache = new Dictionary<string, List<MidiGenerator.GuideNote>> { { "other", other } };
            var part = new SongConfig.PartConfig { Name = "P" };

            var got1 = HarmonyTrackComposer.ResolveGuideMelody(CtxWith(cache), part, "zig", out var t1);
            Assert.That(t1, Is.EqualTo("other"));
            Assert.That(got1, Is.SameAs(other));

            var got2 = HarmonyTrackComposer.ResolveGuideMelody(CtxWith(cache), part, null, out var t2);
            Assert.That(t2, Is.EqualTo("other"));
            Assert.That(got2, Is.SameAs(other));
        }

        [Test]
        public void ResolveGuideMelody_ReturnsNull_WhenNothingCached()
        {
            var cache = new Dictionary<string, List<MidiGenerator.GuideNote>>();
            var part = new SongConfig.PartConfig { Name = "P" };

            var got = HarmonyTrackComposer.ResolveGuideMelody(CtxWith(cache), part, "zig", out var target);

            Assert.That(got, Is.Null);
            Assert.That(target, Is.Null);
        }
    }
}
#endif
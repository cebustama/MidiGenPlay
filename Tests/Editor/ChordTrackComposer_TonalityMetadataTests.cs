#if UNITY_EDITOR
// B2 TONFILTER-1 — tonalities-as-metadata pins (D-B2-1=C, D-B2-2=B).
//
// Pre-B2, ChordTrackComposer step 2b treated ChordProgressionData.tonalities
// as a veto: an asset whose list excluded the part's tonality REVERTED
// part.Tonality (consuming one ctx.rng draw) before RUNTIME-REQUALITY could
// adapt it. Post-B2 the field is descriptive metadata and the part's tonality
// is card authority. These tests pin the three faces of that contract:
//   1. No revert: part.Tonality survives a mismatched progression untouched,
//      and the readback flags the mismatch (AsAuthored only).
//   2. Render-inert metadata: mismatched vs empty tonalities produce
//      byte-identical stems — the field affects no draw and no note.
//   3. REQUALITY now reachable: under a mismatch, DiatonicToPart produces a
//      different render than AsAuthored — impossible pre-B2, where the revert
//      made requality a no-op against the reverted (reference) tonality.
//
// Fixtures: Dbg1Fixtures (same assembly; SongOrchestratorKeyingTests.cs).
// logGenerator stays off, so the gated warning never fires here — the
// readback flag is the testable surface (D-B2-2=B).

using System.Collections.Generic;
using NUnit.Framework;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;
using DwmNoteName = Melanchall.DryWetMidi.MusicTheory.NoteName;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordTrackComposer_TonalityMetadataTests
    {
        private static readonly MusicianTrackKey BackingKey =
            new MusicianTrackKey(Dbg1Fixtures.Musician, TrackRole.Backing);

        /// <summary>I–V in the reference (Ionian) reading; under Aeolian
        /// DiatonicToPart both re-resolve to minor, so requality visibly
        /// changes pitch content.</summary>
        private static ChordProgressionData MismatchedProgression(
            string name,
            ChordProgressionData.QualityRenderPolicy policy =
                ChordProgressionData.QualityRenderPolicy.AsAuthored)
        {
            var p = Dbg1Fixtures.Progression(name,
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            p.tonalities = new List<Tonality> { Tonality.Ionian };
            p.qualityRenderPolicy = policy;
            return p;
        }

        private static SongConfig.PartConfig AeolianBackingPart(
            ChordProgressionData prog)
        {
            var part = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing,
                    Dbg1Fixtures.Instrument(), pattern: prog));
            part.Tonality = Tonality.Aeolian;
            part.RootNote = DwmNoteName.A;
            return part;
        }

        // ------------------------------------------------------------------
        // 1. No revert + readback flag
        // ------------------------------------------------------------------

        [Test]
        public void Mismatch_AsAuthored_PartTonalityNotReverted_AndFlagged()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var part = AeolianBackingPart(MismatchedProgression("Mismatch"));

            var render = Dbg1Fixtures.Render(orch, part);

            Assert.That(part.Tonality, Is.EqualTo(Tonality.Aeolian),
                "TONFILTER-1: the progression's tonalities metadata must " +
                "never revert the part's (card-authoritative) tonality.");
            Assert.That(render.resolvedByTrack.ContainsKey(BackingKey), Is.True);
            Assert.That(render.resolvedByTrack[BackingKey].tonalityMismatch,
                Is.True,
                "AsAuthored render in a foreign tonality must be signalled " +
                "instead of failing silently (D-B2-2=B).");
        }

        [Test]
        public void CompatibleTonalities_NoFlag()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);
            var prog = MismatchedProgression("Compatible");
            prog.tonalities = new List<Tonality> { Tonality.Aeolian };
            var part = AeolianBackingPart(prog);

            var render = Dbg1Fixtures.Render(orch, part);

            Assert.That(render.resolvedByTrack[BackingKey].tonalityMismatch,
                Is.False);
        }

        // ------------------------------------------------------------------
        // 2. Metadata is render-inert (no draw, no note depends on it)
        // ------------------------------------------------------------------

        [Test]
        public void MismatchedVsEmptyTonalities_ByteIdenticalRender()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var withMeta = AeolianBackingPart(MismatchedProgression("Meta"));
            var renderA = Dbg1Fixtures.Render(orch, withMeta);

            var bare = MismatchedProgression("Meta"); // same name: identity parity
            bare.tonalities = new List<Tonality>();   // metadata stripped
            var without = AeolianBackingPart(bare);
            var renderB = Dbg1Fixtures.Render(orch, without);

            Assert.That(Dbg1Fixtures.Fnv(renderA.merged),
                Is.EqualTo(Dbg1Fixtures.Fnv(renderB.merged)),
                "tonalities is descriptive metadata: it must consume no rng " +
                "draw and influence no rendered byte (pre-B2 the mismatch " +
                "case drew once and reverted the part).");
            Assert.That(renderB.resolvedByTrack[BackingKey].tonalityMismatch,
                Is.False, "Empty tonalities can never mismatch.");
        }

        // ------------------------------------------------------------------
        // 3. REQUALITY reachable under mismatch (the pre-B2 impossibility)
        // ------------------------------------------------------------------

        [Test]
        public void Mismatch_DiatonicToPart_AdaptsAndDiffersFromAsAuthored_NoFlag()
        {
            var settings = Dbg1Fixtures.Settings();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var asAuthored = AeolianBackingPart(MismatchedProgression("RQ"));
            var renderA = Dbg1Fixtures.Render(orch, asAuthored);

            var adapted = AeolianBackingPart(MismatchedProgression("RQ",
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart));
            var renderB = Dbg1Fixtures.Render(orch, adapted);

            Assert.That(Dbg1Fixtures.Fnv(renderA.merged),
                Is.Not.EqualTo(Dbg1Fixtures.Fnv(renderB.merged)),
                "With the revert gone, DiatonicToPart must re-resolve " +
                "qualities against the CARD's tonality (I/V Major → minor " +
                "in Aeolian) — pre-B2 the revert made requality a no-op.");
            Assert.That(renderB.resolvedByTrack[BackingKey].tonalityMismatch,
                Is.False,
                "Requality opt-in IS the adaptation; no mismatch to report.");
        }
    }
}
#endif
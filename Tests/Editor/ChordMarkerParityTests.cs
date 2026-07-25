#if UNITY_EDITOR
// MGP-ALWTTT-DBG — "chd:" marker contract promotion: parity test.
//
// The "chd:{channel}:{roman}:{symbol}:{deg}:{quality}" text marker (one per
// chord EVENT, not per articulation hit) is promoted from debug output to a
// governed contract in runtime/SSoT_Composer_Backing_Track.md. This test pins
// the contract's core property: the two emission sites — the grid loop inside
// ChordTrackComposer.Compose and RenderFromProgression (the procedural render
// path) — stamp IDENTICAL marker sequences for the same progression, meter,
// tempo and channel. Voicings may differ between the paths (RNG-dependent);
// the markers must not.
//
// Also pins the accidental alignment applied in this batch: both sites now
// prefix the roman numeral ("b"/"#") and transpose the degree root when
// degreeAccidental != 0 (previously grid-site-only; guarded so accidental==0
// output — every procedural progression today — is bit-identical).
//
// Drives RenderFromProgression directly via its internal accessor
// (Runtime/AssemblyInfo.cs InternalsVisibleTo — the established test-seam
// idiom).

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordMarkerParityTests
    {
        private const int Channel = 3;
        private const int Bpm = 120;

        private static List<(long tick, string text)> ChdMarkers(MidiFile file)
            => file.GetTrackChunks()
                   .SelectMany(c => c.GetTimedEvents())
                   .Where(te => te.Event is TextEvent tx &&
                                tx.Text != null && tx.Text.StartsWith("chd:"))
                   .Select(te => (te.Time, ((TextEvent)te.Event).Text))
                   .OrderBy(m => m.Time).ThenBy(m => m.Text)
                   .ToList();

        private static MidiGenerator.GenContext Ctx(MidiGenPlayConfig settings, int seed)
            => new MidiGenerator.GenContext
            {
                Settings = settings,
                rng = new System.Random(seed),
                trackSeed = seed,
            };

        private static (MidiFile gridFile, MidiFile progFile) RenderBothSites(
            ChordProgressionData prog)
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var composer = new ChordTrackComposer(settings, voicer: null);

            // Site 1 — grid loop inside Compose (authored progression via
            // TrackParameters.Pattern).
            var gridPart = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst, pattern: prog));
            var gridCfg = gridPart.Tracks[0];
            var gridFile = composer.Compose(
                gridPart, gridCfg, Bpm, Channel, Ctx(settings, seed: 11));

            // Site 2 — RenderFromProgression (the procedural render path),
            // driven directly with the SAME progression. Fresh part: Compose
            // consumes/clears the part transients.
            var progPart = Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst, pattern: prog));
            var progFile = composer.RenderFromProgression(
                inst, Bpm, progPart, prog, Channel, Ctx(settings, seed: 22),
                vlOverride: null,
                modulationHint: ModulationOctaveHint.Auto,
                previousRoot: null,
                inversionHints: null,
                chordExpression: ChordExpressionType.Block,
                arpeggioRate: ArpeggioRate.Eighth,
                articRoller: null,
                velocityJitter: default);

            return (gridFile, progFile);
        }

        [Test]
        public void ChdMarkers_GridSite_And_RenderFromProgression_AreIdentical()
        {
            var prog = Dbg1Fixtures.Progression("ParityProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Submediant, ChordQuality.Minor),
                (ScaleDegree.Subdominant, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            var (gridFile, progFile) = RenderBothSites(prog);

            var gridMarkers = ChdMarkers(gridFile);
            var progMarkers = ChdMarkers(progFile);

            Assert.That(gridMarkers.Count, Is.EqualTo(prog.events.Count),
                "One chd: marker per chord EVENT (not per articulation hit).");
            Assert.That(progMarkers, Is.EqualTo(gridMarkers),
                "Both emission sites must stamp identical (tick, text) marker sequences.");
        }

        [Test]
        public void ChdMarkers_CarryChannel_Roman_Degree_Quality()
        {
            var prog = Dbg1Fixtures.Progression("FormatProg",
                (ScaleDegree.Tonic, ChordQuality.Major));

            var (gridFile, _) = RenderBothSites(prog);
            var marker = ChdMarkers(gridFile).Single();

            var fields = marker.text.Split(':');
            Assert.That(fields.Length, Is.EqualTo(6),
                "Contract shape: chd:{channel}:{roman}:{symbol}:{deg}:{quality}");
            Assert.That(fields[0], Is.EqualTo("chd"));
            Assert.That(fields[1], Is.EqualTo(Channel.ToString()));
            Assert.That(fields[4], Is.EqualTo("1"), "deg is 1-based.");
            Assert.That(fields[5], Is.EqualTo(ChordQuality.Major.ToString()));
        }

        [Test]
        public void ChdMarkers_AccidentalPrefix_ParityAcrossBothSites()
        {
            var prog = Dbg1Fixtures.Progression("AccidentalProg",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Mediant, ChordQuality.Major));
            // Flatten the second chord's degree (bIII).
            var e = prog.events[1];
            e.degreeAccidental = -1;
            prog.events[1] = e;

            var (gridFile, progFile) = RenderBothSites(prog);

            var gridMarkers = ChdMarkers(gridFile);
            var progMarkers = ChdMarkers(progFile);

            Assert.That(gridMarkers[1].text.Split(':')[2],
                Does.StartWith("b"),
                "Grid site prefixes the roman with the accidental.");
            Assert.That(progMarkers, Is.EqualTo(gridMarkers),
                "Accidental handling must be identical at both sites (batch fix).");
        }
    }
}
#endif
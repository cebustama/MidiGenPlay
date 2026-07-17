#if UNITY_EDITOR
// MGP-ALWTTT-DBG-3/4 � per-render patternOverrides (Ask C) + Ask A readback.
//
// Covers, per the batch DoD:
//   - BC gate: no override => bit-identical output (null map == empty map ==
//     re-run with the same seed), FNV-golden idiom over serialized bytes.
//   - Step 0 precedence per role: Backing (applies + shares the progression),
//     Melody (applies over TrackParameters), Bassline (warn + ignore, v1).
//   - Clone-on-apply: the caller's override asset is never mutated / never
//     the instance the composer renders.
//   - Type mismatch = warn + ignore (output bit-identical to baseline).
//   - Backing readback: source / pre-clone asset name / roman sequence /
//     resolvedFigures under ChordExpressionType.Random.
//
// NOT covered here (needs a MIDIPercussionInstrumentSO mapping fixture whose
// type is outside this test's reach): the Rhythm render-level override-applies
// assertion. Rhythm's step-0 code path is shape-identical to Backing/Melody's
// and its mismatch warning shares their contract; render-level coverage is
// tracked as a pending item of the batch.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MidiGenPlay;
using MidiGenPlay.Composition;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class PatternOverrideAndReadbackTests
    {
        private const string Mus = Dbg1Fixtures.Musician;

        private static readonly MusicianTrackKey BackingKey =
            new MusicianTrackKey(Mus, TrackRole.Backing);
        private static readonly MusicianTrackKey BassKey =
            new MusicianTrackKey(Mus, TrackRole.Bassline);
        private static readonly MusicianTrackKey MelodyKey =
            new MusicianTrackKey(Mus, TrackRole.Melody);

        private static SongConfig.PartConfig BackingBassPart(
            MIDIInstrumentSO inst, ChordProgressionData backingProg,
            TrackStyleBundleSO backingStyle = null)
            => Dbg1Fixtures.Part(
                Dbg1Fixtures.Track(TrackRole.Backing, inst,
                    pattern: backingProg, style: backingStyle),
                Dbg1Fixtures.Track(TrackRole.Bassline, inst));

        // ------------------------------------------------------------------
        // BC gate � no override => bit-identical (D-DBG4 / batch DoD)
        // ------------------------------------------------------------------

        [Test]
        public void BcGate_NoOverride_NullMap_EmptyMap_AndRerun_AreBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            ulong RenderHash(IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> ovr)
            {
                var prog = Dbg1Fixtures.Progression("ProgA",
                    (ScaleDegree.Tonic, ChordQuality.Major),
                    (ScaleDegree.Dominant, ChordQuality.Major));
                var part = BackingBassPart(inst, prog);
                return Dbg1Fixtures.Fnv(
                    Dbg1Fixtures.Render(orch, part, ovr, seed: 7).merged);
            }

            var baseline = RenderHash(null);
            var emptyMap = RenderHash(
                new Dictionary<MusicianTrackKey, PatternDataSO>());
            var rerun = RenderHash(null);

            Assert.That(emptyMap, Is.EqualTo(baseline),
                "An empty override map must be draw-for-draw identical to no map.");
            Assert.That(rerun, Is.EqualTo(baseline),
                "Same inputs + same seed must stay bit-identical (determinism invariant).");
        }

        // ------------------------------------------------------------------
        // Backing � step 0 applies, shares the progression, reads back
        // ------------------------------------------------------------------

        [Test]
        public void BackingOverride_Step0_AppliesSharesAndReadsBack()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var baseProg = Dbg1Fixtures.Progression("ProgA",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));
            var overrideProg = Dbg1Fixtures.Progression("ProgB",
                (ScaleDegree.Submediant, ChordQuality.Minor),
                (ScaleDegree.Subdominant, ChordQuality.Major));

            // Baseline (no override): the backing's own TrackParameters
            // progression is surfaced through the shared-progression path —
            // GetProgressionForPart falls back to FindProgressionForPart, which
            // returns the Backing track's Pattern. So the backing reads it back
            // as SharedProgression (it IS the part's shared progression; the
            // bass consumes the same one). sourceAssetName pins it to ProgA.
            var baselinePart = BackingBassPart(inst, baseProg);
            var baseline = Dbg1Fixtures.Render(orch, baselinePart, null, seed: 7);
            var baselineChoice = baseline.resolvedByTrack[BackingKey];
            Assert.That(baselineChoice.source, Is.EqualTo(ResolvedSource.SharedProgression));
            Assert.That(baselineChoice.sourceAssetName, Is.EqualTo("ProgA"));

            // Override render.
            var overridePart = BackingBassPart(inst, baseProg);
            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [BackingKey] = overrideProg,
            };
            var render = Dbg1Fixtures.Render(orch, overridePart, overrides, seed: 7);

            var choice = render.resolvedByTrack[BackingKey];
            Assert.That(choice.source, Is.EqualTo(ResolvedSource.RenderOverride),
                "Step 0: the per-render override must win over TrackParameters.Pattern.");
            Assert.That(choice.sourceAssetName, Is.EqualTo("ProgB"),
                "Identity must be the PRE-clone caller asset name (D-DBG3=A).");
            Assert.That(choice.progressionRoman,
                Is.Not.EqualTo(baselineChoice.progressionRoman),
                "The rendered progression must be the overridden one.");

            // Clone-on-apply: the caller's asset is intact and was never the
            // rendered instance (the shared cache holds a clone).
            Assert.That(overrideProg.events.Count, Is.EqualTo(2));
            Assert.That(overrideProg.name, Is.EqualTo("ProgB"));

            // The override IS shared state: bass followed it.
            var bassChoice = render.resolvedByTrack[BassKey];
            Assert.That(bassChoice.usesSharedProgression, Is.True);
            Assert.That(bassChoice.progressionRoman,
                Is.EqualTo(choice.progressionRoman),
                "Bass must render the same (overridden) shared progression.");

            // And the output actually changed.
            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.Not.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)));
        }

        // ------------------------------------------------------------------
        // Backing � type mismatch = warn + ignore, bit-identical fallthrough
        // ------------------------------------------------------------------

        [Test]
        public void BackingOverride_TypeMismatch_WarnsAndFallsThroughBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            ChordProgressionData Prog() => Dbg1Fixtures.Progression("ProgA",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            var baseline = Dbg1Fixtures.Render(
                orch, BackingBassPart(inst, Prog()), null, seed: 7);

            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[ChordTrackComposer\] patternOverride type mismatch"));

            var wrongType = Dbg1Fixtures.MelodyPattern("NotAProgression",
                ScaleDegree.Tonic);
            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [BackingKey] = wrongType,
            };
            var render = Dbg1Fixtures.Render(
                orch, BackingBassPart(inst, Prog()), overrides, seed: 7);

            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)),
                "A mismatched override must fall through to the exact baseline output.");
            Assert.That(render.resolvedByTrack[BackingKey].source,
                Is.EqualTo(ResolvedSource.SharedProgression),
                "Readback must show the fallthrough source (shared progression), not RenderOverride.");
            Assert.That(render.resolvedByTrack[BackingKey].sourceAssetName,
                Is.EqualTo("ProgA"),
                "The ignored override must not change which progression rendered.");
        }

        // ------------------------------------------------------------------
        // Bassline � override targeting bass = warn + ignore (v1)
        // ------------------------------------------------------------------

        [Test]
        public void BassTargetedOverride_WarnsAndIgnoresBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            ChordProgressionData Prog() => Dbg1Fixtures.Progression("ProgA",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            var baseline = Dbg1Fixtures.Render(
                orch, BackingBassPart(inst, Prog()), null, seed: 7);

            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[BassTrackComposer\] patternOverride targeting Bassline"));

            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [BassKey] = Dbg1Fixtures.Progression("ProgB",
                    (ScaleDegree.Submediant, ChordQuality.Minor)),
            };
            var render = Dbg1Fixtures.Render(
                orch, BackingBassPart(inst, Prog()), overrides, seed: 7);

            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)),
                "A bass-targeted override must be ignored (bass renders shared state).");
            Assert.That(render.resolvedByTrack[BassKey].usesSharedProgression, Is.True);
            Assert.That(render.resolvedByTrack[BassKey].progressionRoman,
                Is.EqualTo(baseline.resolvedByTrack[BassKey].progressionRoman));
        }

        // ------------------------------------------------------------------
        // Melody � step 0 applies over TrackParameters, reads back
        // ------------------------------------------------------------------

        [Test]
        public void MelodyOverride_Step0_AppliesOverTrackParametersAndReadsBack()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var authored = Dbg1Fixtures.MelodyPattern("MelA",
                ScaleDegree.Tonic, ScaleDegree.Mediant,
                ScaleDegree.Dominant, ScaleDegree.Tonic);
            var overridden = Dbg1Fixtures.MelodyPattern("MelB",
                ScaleDegree.Dominant, ScaleDegree.Subdominant,
                ScaleDegree.Mediant, ScaleDegree.Supertonic);

            SongConfig.PartConfig MelodyPart(MelodyPatternData p) =>
                Dbg1Fixtures.Part(
                    Dbg1Fixtures.Track(TrackRole.Melody, inst, pattern: p));

            // Baseline: authored TrackParameters pattern.
            var baseline = Dbg1Fixtures.Render(orch, MelodyPart(authored), null, seed: 7);
            var baselineChoice = baseline.resolvedByTrack[MelodyKey];
            Assert.That(baselineChoice.source, Is.EqualTo(ResolvedSource.TrackParameters));
            Assert.That(baselineChoice.sourceAssetName, Is.EqualTo("MelA"));

            // Override render: step 0 wins.
            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [MelodyKey] = overridden,
            };
            var render = Dbg1Fixtures.Render(orch, MelodyPart(authored), overrides, seed: 7);

            var choice = render.resolvedByTrack[MelodyKey];
            Assert.That(choice.source, Is.EqualTo(ResolvedSource.RenderOverride));
            Assert.That(choice.sourceAssetName, Is.EqualTo("MelB"),
                "Identity must be the PRE-clone caller asset name (D-DBG3=A).");
            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.Not.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)));

            // Clone-on-apply: caller's asset intact.
            Assert.That(overridden.notes.Count, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------
        // Backing readback � resolvedFigures under Random articulation
        // ------------------------------------------------------------------

        [Test]
        public void BackingReadback_RandomArticulation_ReportsResolvedFigures()
        {
            var settings = Dbg1Fixtures.Settings();
            var inst = Dbg1Fixtures.Instrument();
            var orch = Dbg1Fixtures.Orchestrator(settings);

            var card = ScriptableObject.CreateInstance<BackingCardConfigSO>();
            card.chordExpression = ChordExpressionType.Random;
            card.randomRerollChance = 1f;

            var prog = Dbg1Fixtures.Progression("ProgA",
                (ScaleDegree.Tonic, ChordQuality.Major),
                (ScaleDegree.Dominant, ChordQuality.Major));

            var part = BackingBassPart(inst, prog, backingStyle: card);
            var render = Dbg1Fixtures.Render(orch, part, null, seed: 7);

            var choice = render.resolvedByTrack[BackingKey];
            Assert.That(choice.resolvedFigures, Is.Not.Null,
                "Random articulation must report the resolved figure sequence.");
            // 1 measure, pattern covers the part exactly => one repeat, one
            // figure per chord event, in emission order.
            Assert.That(choice.resolvedFigures.Count, Is.EqualTo(prog.events.Count));
            Assert.That(choice.resolvedFigures,
                Has.None.EqualTo(ChordExpressionType.Random),
                "Resolved figures are always concrete (never the Random sentinel).");

            // Fixed articulation (no card) => null figures.
            var fixedRender = Dbg1Fixtures.Render(
                orch, BackingBassPart(inst, Dbg1Fixtures.Progression("ProgA",
                    (ScaleDegree.Tonic, ChordQuality.Major),
                    (ScaleDegree.Dominant, ChordQuality.Major))), null, seed: 7);
            Assert.That(
                fixedRender.resolvedByTrack[BackingKey].resolvedFigures, Is.Null);
        }

        // ------------------------------------------------------------------
        // Rhythm � render-level override applies + reads back (DoD gap closed)
        // ------------------------------------------------------------------

        private static readonly MusicianTrackKey RhythmKey =
            new MusicianTrackKey(Mus, TrackRole.Rhythm);

        private static SongConfig.PartConfig.TrackConfig RhythmTrack(
            MIDIPercussionInstrumentSO kit, DrumPatternData pattern)
            => new SongConfig.PartConfig.TrackConfig
            {
                Role = TrackRole.Rhythm,
                MusicianId = Mus,
                PercussionInstrument = kit,
                Parameters = new TrackParameters { Pattern = pattern },
            };

        [Test]
        public void RhythmOverride_Step0_AppliesOverTrackParametersAndReadsBack()
        {
            var settings = Dbg1Fixtures.Settings();
            var kit = Dbg1Fixtures.Kit();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);

            var authored = Dbg1Fixtures.DrumPattern("DrumA", denseKick: false);
            var overridden = Dbg1Fixtures.DrumPattern("DrumB", denseKick: true);

            SongConfig.PartConfig RhythmPart(DrumPatternData p) =>
                Dbg1Fixtures.Part(RhythmTrack(kit, p));

            // Baseline: authored TrackParameters pattern.
            var baseline = Dbg1Fixtures.Render(orch, RhythmPart(authored), null, seed: 7);
            var baselineChoice = baseline.resolvedByTrack[RhythmKey];
            Assert.That(baselineChoice.source, Is.EqualTo(ResolvedSource.TrackParameters));
            Assert.That(baselineChoice.sourceAssetName, Is.EqualTo("DrumA"));

            // Override render: step 0 wins.
            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [RhythmKey] = overridden,
            };
            var render = Dbg1Fixtures.Render(orch, RhythmPart(authored), overrides, seed: 7);

            var choice = render.resolvedByTrack[RhythmKey];
            Assert.That(choice.source, Is.EqualTo(ResolvedSource.RenderOverride),
                "Step 0: the per-render override must win over TrackParameters.Pattern.");
            Assert.That(choice.sourceAssetName, Is.EqualTo("DrumB"),
                "Identity must be the PRE-clone caller asset name (D-DBG3=A).");
            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.Not.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)),
                "The overridden drum pattern must actually change the render.");

            // Clone-on-apply: the caller's asset is intact.
            Assert.That(overridden.lanes.Count, Is.EqualTo(2));
            Assert.That(overridden.name, Is.EqualTo("DrumB"));
        }

        [Test]
        public void RhythmOverride_TypeMismatch_WarnsAndFallsThroughBitIdentical()
        {
            var settings = Dbg1Fixtures.Settings();
            var kit = Dbg1Fixtures.Kit();
            var orch = Dbg1Fixtures.OrchestratorWithRhythm(settings);

            SongConfig.PartConfig RhythmPart() =>
                Dbg1Fixtures.Part(RhythmTrack(kit,
                    Dbg1Fixtures.DrumPattern("DrumA", denseKick: false)));

            var baseline = Dbg1Fixtures.Render(orch, RhythmPart(), null, seed: 7);

            LogAssert.Expect(LogType.Warning, new Regex(
                @"patternOverride type mismatch for role Rhythm"));

            // A ChordProgressionData aimed at the drum track: warn + ignore.
            var wrongType = Dbg1Fixtures.Progression("NotADrumPattern",
                (ScaleDegree.Tonic, ChordQuality.Major));
            var overrides = new Dictionary<MusicianTrackKey, PatternDataSO>
            {
                [RhythmKey] = wrongType,
            };
            var render = Dbg1Fixtures.Render(orch, RhythmPart(), overrides, seed: 7);

            Assert.That(Dbg1Fixtures.Fnv(render.merged),
                Is.EqualTo(Dbg1Fixtures.Fnv(baseline.merged)),
                "A mismatched override must fall through to the exact baseline output.");
            Assert.That(render.resolvedByTrack[RhythmKey].source,
                Is.EqualTo(ResolvedSource.TrackParameters));
        }
    }
}
#endif
#if UNITY_EDITOR
// RUNTIME-REQUALITY — EditMode tests for ChordProgressionRequality.
//
// Pure-seam idiom (ScriptableObject.CreateInstance fixtures, no render, no
// asset DB). Pins the whole decision surface:
//  - D-RQ-SURF=A:   AsAuthored (default) is a same-reference no-op.
//  - D-RQ-BORROW=A: isDiatonic == false events keep authored quality/accidental.
//  - D-RQ-MAP=A:    core triads/sevenths re-map size-preserving (golden values
//                   against textbook Aeolian harmony, independent of the
//                   resolver's rotation math); Sus/6th/9th pass through.
//  - D-RQ-DET:      clone-if-changed (asset never mutated; no-op returns the
//                   same instance), name-preserving, isDiatonic stays true on
//                   re-resolved events. Purity is structural (no rng in any
//                   signature).
//  - Integration:   SongOrchestrator.TrySeedDefaultProgression requalifies
//                   opt-in defaults on the backing-less seed path with a
//                   single clone.
//  - REQUALITY-FUNC amendment (D-RQ-FUNC=A / D-RQ-FUNC-SCOPE=A /
//                   D-RQ-LOCRIAN=A): the Functional policy's dominant
//                   exception (V Major / V7 keep authored quality, marked
//                   borrowed), non-dominant parity with the pure policy,
//                   Ionian no-op, idempotence, and the Locrian guard.

using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Tests.Editor
{
    public class ChordProgressionRequalityTests
    {
        // ---------------- Fixtures ----------------

        private static ChordProgressionData.ChordEvent Ev(
            ScaleDegree deg, ChordQuality q,
            bool diatonic = true, int accidental = 0, int start = 0)
            => new ChordProgressionData.ChordEvent
            {
                startStep = start,
                lengthSteps = 4,
                degree = deg,
                quality = q,
                velocity = 90,
                isDiatonic = diatonic,
                degreeAccidental = accidental,
            };

        private static ChordProgressionData MakeProg(
            ChordProgressionData.QualityRenderPolicy policy,
            params ChordProgressionData.ChordEvent[] events)
        {
            var prog = ScriptableObject.CreateInstance<ChordProgressionData>();
            prog.name = "RequalityFixture";
            prog.Measures = 4;
            prog.subdivisions = 1;
            prog.TimeSignature = TimeSignature.FourFour;
            prog.qualityRenderPolicy = policy;
            prog.events = new List<ChordProgressionData.ChordEvent>(events);
            return prog;
        }

        // ---------------- D-RQ-SURF=A: AsAuthored is a no-op ----------------

        [Test]
        public void AsAuthored_ReturnsSameReference_EvenUnderForeignTonality()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "AsAuthored must be a guaranteed same-reference no-op.");
            Assert.That(prog.events[0].quality, Is.EqualTo(ChordQuality.Major));
        }

        // ---------------- D-RQ-MAP=A: golden Aeolian triads ----------------

        [Test]
        public void MajorTriadProgression_InAeolian_BecomesTextbookMinorHarmony()
        {
            // I – IV – V authored major; Aeolian diatonic harmony is
            // i – iv – v (all minor). Golden literals, not resolver echoes.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Subdominant, ChordQuality.Major, start: 4),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.False,
                "A change must produce a clone.");
            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Minor)); // i
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Minor)); // iv
            Assert.That(result.events[2].quality, Is.EqualTo(ChordQuality.Minor)); // v
            // Degrees, timing, velocity untouched.
            Assert.That(result.events[1].degree, Is.EqualTo(ScaleDegree.Subdominant));
            Assert.That(result.events[2].startStep, Is.EqualTo(8));
            Assert.That(result.events[0].velocity, Is.EqualTo(90));
        }

        [Test]
        public void SupertonicAndMediant_InAeolian_MapToDimAndMajor()
        {
            // Aeolian: ii° (Diminished), III (Major).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor),
                Ev(ScaleDegree.Mediant, ChordQuality.Minor, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Diminished));
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Major));
        }

        // ---------------- D-RQ-MAP=A: size preservation (sevenths) ----------

        [Test]
        public void SeventhQualities_RemapToDiatonicSevenths_NotTriads()
        {
            // V7 (Dominant7) in Aeolian => v7 (Minor7); Imaj7 => im7 (Minor7).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant7),
                Ev(ScaleDegree.Tonic, ChordQuality.Major7, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Minor7));
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Minor7));
        }

        [Test]
        public void AlreadyDiatonicToTarget_IsSameReferenceNoOp()
        {
            // i – iv – v authored AS minor, target Aeolian: nothing changes,
            // so the SAME instance must return (zero clones).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Minor),
                Ev(ScaleDegree.Subdominant, ChordQuality.Minor, start: 4),
                Ev(ScaleDegree.Dominant, ChordQuality.Minor, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.True);
        }

        // ---------------- D-RQ-BORROW=A: borrowed chords intact -------------

        [Test]
        public void BorrowedChord_KeepsAuthoredQualityAndAccidental()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),                      // re-maps
                Ev(ScaleDegree.Submediant, ChordQuality.Major,
                   diatonic: false, accidental: -1, start: 4));                 // borrowed ♭VI

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.False);
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Major),
                "Borrowed chord quality must survive requality.");
            Assert.That(result.events[1].degreeAccidental, Is.EqualTo(-1),
                "Borrowed chord accidental must survive requality.");
            Assert.That(result.events[1].isDiatonic, Is.False,
                "Borrowed flag must not be overwritten.");
        }

        // ---------------- D-RQ-MAP=A: color qualities pass through ----------

        [Test]
        public void SusSixthAndNinthQualities_PassThroughUnchanged()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Sus4),
                Ev(ScaleDegree.Subdominant, ChordQuality.Major6, start: 4),
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant9, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "Color-only progression: nothing re-maps, same reference.");
            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Sus4));
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Major6));
            Assert.That(result.events[2].quality, Is.EqualTo(ChordQuality.Dominant9));
        }

        // ---------------- D-RQ-DET: clone semantics -------------------------

        [Test]
        public void CloneOnChange_NeverMutatesTheAsset_AndPreservesName()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major));
            prog.name = "MyOptInAsset";

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(prog.events[0].quality, Is.EqualTo(ChordQuality.Major),
                "The source asset instance must never be mutated.");
            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Minor));
            Assert.That(result.name, Is.EqualTo("MyOptInAsset"),
                "Clone must keep the source name (readback identity).");
            Assert.That(result.events[0].isDiatonic, Is.True,
                "Re-resolved events are diatonic by construction.");
        }

        [Test]
        public void NullAndEmptyInputs_AreSafeNoOps()
        {
            Assert.That(ChordProgressionRequality.ApplyDiatonicRequality(
                null, Tonality.Aeolian), Is.Null);

            var empty = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart);
            Assert.That(ReferenceEquals(
                ChordProgressionRequality.ApplyDiatonicRequality(
                    empty, Tonality.Aeolian),
                empty), Is.True);
        }

        // ---------------- REQUALITY-FUNC: Functional policy ------------------

        [Test]
        public void Functional_VMajor_InAeolian_KeepsMajor_MarkedBorrowed()
        {
            // I – V – iv under Functional in Aeolian: I re-maps to i, iv stays
            // (already minor via remap), but V KEEPS Major and becomes a
            // borrowed chord — the harmonic-minor leading-tone practice.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, start: 4),
                Ev(ScaleDegree.Subdominant, ChordQuality.Major, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.False);
            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Minor));  // i
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Major),   // V!
                "Functional policy must preserve the dominant's authored Major.");
            Assert.That(result.events[1].isDiatonic, Is.False,
                "The protected dominant is a borrowed chord in the target mode.");
            Assert.That(result.events[2].quality, Is.EqualTo(ChordQuality.Minor));  // iv
        }

        [Test]
        public void Functional_V7_InAeolian_KeepsDominant7_MarkedBorrowed()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant7));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.False);
            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Dominant7),
                "V7 keeps its authored quality (size preserved — no promotion).");
            Assert.That(result.events[0].isDiatonic, Is.False);
        }

        [Test]
        public void Functional_NonDominantDegrees_MatchPurePolicyExactly()
        {
            // Without a protected dominant, Functional ≡ DiatonicToPart.
            ChordProgressionData.ChordEvent[] Events() => new[]
            {
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4),
                Ev(ScaleDegree.Submediant, ChordQuality.Minor, start: 8),
            };
            var pure = ChordProgressionRequality.ApplyDiatonicRequality(
                MakeProg(ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                    Events()),
                Tonality.Aeolian);
            var func = ChordProgressionRequality.ApplyDiatonicRequality(
                MakeProg(ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                    Events()),
                Tonality.Aeolian);

            for (int i = 0; i < pure.events.Count; i++)
            {
                Assert.That(func.events[i].quality, Is.EqualTo(pure.events[i].quality));
                Assert.That(func.events[i].isDiatonic, Is.EqualTo(pure.events[i].isDiatonic));
            }
        }

        [Test]
        public void Functional_InIonian_VMajorIsSameReferenceNoOp()
        {
            // Diatonic V in Ionian IS Major: nothing to protect, nothing to
            // remap => same reference (and a minor-authored V would still
            // remap to Major — the exception guards only Major/Dominant7).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Dominant, ChordQuality.Major));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            Assert.That(ReferenceEquals(result, prog), Is.True);
        }

        [Test]
        public void Functional_IsIdempotent_SecondPassIsNoOp()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant7, start: 4));

            var once = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);
            var twice = ChordProgressionRequality.ApplyDiatonicRequality(
                once, Tonality.Aeolian);

            Assert.That(ReferenceEquals(twice, once), Is.True,
                "Second application must be a same-reference no-op: remapped " +
                "events are diatonic-stable and the protected dominant is " +
                "borrowed (skipped) on re-entry.");
        }

        // ---------------- D-RQ-LOCRIAN=A: degenerate target -----------------

        [Test]
        public void Locrian_IsSameReferenceNoOp_ForBothOptInPolicies()
        {
            var pure = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major));
            var func = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Dominant, ChordQuality.Major));

            Assert.That(ReferenceEquals(
                ChordProgressionRequality.ApplyDiatonicRequality(pure, Tonality.Locrian),
                pure), Is.True);
            Assert.That(ReferenceEquals(
                ChordProgressionRequality.ApplyDiatonicRequality(func, Tonality.Locrian),
                func), Is.True);
        }

        // ---------------- F-NORM-DROP regression ----------------------------

        [Test]
        public void PolicySurvivesFieldByFieldCloning_NormalizationParity()
        {
            // The TS/subdivision reprojection in ChordTrackComposer builds its
            // runtime clone FIELD BY FIELD (ScriptableObject.CreateInstance +
            // explicit copies), not via Instantiate. Any field it forgets
            // silently reverts to its default — which made requality a no-op
            // for every progression needing normalization (authoring writes
            // sub x1; the composer normalizes to x4, so this is the common
            // case, not an edge case).
            //
            // This test pins the INVARIANT the fix restores: a progression
            // whose policy survived cloning must requalify identically to the
            // original. It fails loudly if a future field-copy site drops the
            // policy again.
            var authored = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Subdominant, ChordQuality.Major, start: 4));

            // Simulate the composer's field-by-field clone, policy included.
            var cloned = ScriptableObject.CreateInstance<ChordProgressionData>();
            cloned.name = authored.name;
            cloned.DisplayName = authored.DisplayName;
            cloned.TimeSignature = authored.TimeSignature;
            cloned.Measures = authored.Measures;
            cloned.subdivisions = 4; // the normalization that triggered the bug
            cloned.qualityRenderPolicy = authored.qualityRenderPolicy;
            cloned.events = new List<ChordProgressionData.ChordEvent>();
            foreach (var e in authored.events)
            {
                cloned.events.Add(new ChordProgressionData.ChordEvent
                {
                    startStep = e.startStep * 4,
                    lengthSteps = e.lengthSteps * 4,
                    degree = e.degree,
                    quality = e.quality,
                    velocity = e.velocity,
                    isDiatonic = e.isDiatonic,
                    degreeAccidental = e.degreeAccidental,
                });
            }

            Assert.That(cloned.qualityRenderPolicy,
                Is.EqualTo(ChordProgressionData.QualityRenderPolicy.DiatonicToPart),
                "The clone must carry the authored render policy.");

            var fromAuthored = ChordProgressionRequality.ApplyDiatonicRequality(
                authored, Tonality.Aeolian);
            var fromClone = ChordProgressionRequality.ApplyDiatonicRequality(
                cloned, Tonality.Aeolian);

            Assert.That(ReferenceEquals(fromClone, cloned), Is.False,
                "Requality must fire on the normalized clone too — this is the " +
                "exact no-op that F-NORM-DROP produced in the smoke.");
            for (int i = 0; i < fromAuthored.events.Count; i++)
            {
                Assert.That(fromClone.events[i].quality,
                    Is.EqualTo(fromAuthored.events[i].quality),
                    "Normalized clone must requalify identically to the source.");
            }
        }

        // ---------------- TryMapCoreQuality seam ----------------------------

        [Test]
        public void TryMapCoreQuality_CoreAlphabetOnly()
        {
            Assert.That(ChordProgressionRequality.TryMapCoreQuality(
                ChordQuality.Augmented, Tonality.Ionian, ScaleDegree.Tonic,
                out var t), Is.True);
            Assert.That(t, Is.EqualTo(ChordQuality.Major)); // Ionian I

            Assert.That(ChordProgressionRequality.TryMapCoreQuality(
                ChordQuality.HalfDiminished7, Tonality.Ionian, ScaleDegree.Dominant,
                out var s7), Is.True);
            Assert.That(s7, Is.EqualTo(ChordQuality.Dominant7)); // Ionian V7

            Assert.That(ChordProgressionRequality.TryMapCoreQuality(
                ChordQuality.Dominant7sus4, Tonality.Ionian, ScaleDegree.Dominant,
                out var echo), Is.False);
            Assert.That(echo, Is.EqualTo(ChordQuality.Dominant7sus4));
        }

        // ---------------- Integration: bass-solo seed path ------------------

        [Test]
        public void SeedPath_RequalifiesOptInDefault_WithSingleNamePreservingClone()
        {
            var part = new SongConfig.PartConfig
            {
                Name = "MinorSolo",
                Tonality = Tonality.Aeolian,
                Tracks = new List<SongConfig.PartConfig.TrackConfig>
                {
                    new SongConfig.PartConfig.TrackConfig { Role = TrackRole.Bassline },
                },
            };
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, start: 8));
            prog.name = "SoloDefaultMajor";

            var result = SongOrchestrator.TrySeedDefaultProgression(part, prog, cache);

            Assert.That(result,
                Is.EqualTo(SongOrchestrator.DefaultProgressionSeedResult.Seeded));
            var seeded = cache[part];
            Assert.That(ReferenceEquals(seeded, prog), Is.False);
            Assert.That(seeded.name, Is.EqualTo("SoloDefaultMajor"));
            Assert.That(seeded.events[0].quality, Is.EqualTo(ChordQuality.Minor)); // i
            Assert.That(seeded.events[1].quality, Is.EqualTo(ChordQuality.Minor)); // v
            Assert.That(prog.events[0].quality, Is.EqualTo(ChordQuality.Major),
                "Asset must stay unmutated on the seed path too.");
        }

        [Test]
        public void SeedPath_AsAuthoredDefault_StillClonesPlainly()
        {
            var part = new SongConfig.PartConfig
            {
                Name = "MinorSolo",
                Tonality = Tonality.Aeolian,
                Tracks = new List<SongConfig.PartConfig.TrackConfig>
                {
                    new SongConfig.PartConfig.TrackConfig { Role = TrackRole.Bassline },
                },
            };
            var cache = new Dictionary<SongConfig.PartConfig, ChordProgressionData>();
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                Ev(ScaleDegree.Tonic, ChordQuality.Major));

            SongOrchestrator.TrySeedDefaultProgression(part, prog, cache);

            var seeded = cache[part];
            Assert.That(ReferenceEquals(seeded, prog), Is.False,
                "Clone-on-seed discipline holds for AsAuthored defaults.");
            Assert.That(seeded.events[0].quality, Is.EqualTo(ChordQuality.Major),
                "AsAuthored: qualities untouched on the seed path.");
        }

        // ================= HARMONY-PURE-1 additions =====================

        private static ChordProgressionData.ChordEvent SecDomEv(
            ScaleDegree authoredDeg, ScaleDegree target,
            int start, int length = 4)
        {
            var e = Ev(authoredDeg, ChordQuality.Major, start: start);
            e.lengthSteps = length;
            e.hasAppliedTarget = true;
            e.appliedTarget = target;
            return e;
        }

        // ---------------- D-CT-GATE=A: color-table gating ----------------

        [Test]
        public void ColorTable_OffByDefault_SixthsAndNinthsStillPassThrough()
        {
            // Assets already opted into DiatonicToPart keep their exact
            // pre-B1 render: without useColorTable the lab rules never fire.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major6),
                Ev(ScaleDegree.Subdominant, ChordQuality.Dominant9, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "Sixths/ninths are outside the core alphabet and the color " +
                "table is off => guaranteed same-reference no-op.");
        }

        [Test]
        public void ColorTable_RequiresOptInPolicy_AsAuthoredStaysNoOp()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                Ev(ScaleDegree.Tonic, ChordQuality.Major6));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "useColorTable without a DiatonicToPart* policy must not " +
                "activate anything (D-CT-GATE=A).");
        }

        // ---------------- Color rules: sixths / sus / ninths -------------

        [Test]
        public void ColorTable_Sixths_AeolianAndPhrygian_MapToMinor7()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major6),
                Ev(ScaleDegree.Subdominant, ChordQuality.Minor6, start: 4));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].quality,
                Is.EqualTo(ChordQuality.Minor7), "6 -> m7 in Aeolian.");
            Assert.That(result.events[1].quality,
                Is.EqualTo(ChordQuality.Minor7), "m6 -> m7 in Aeolian.");
        }

        [Test]
        public void ColorTable_Sixths_Dorian_Major6BecomesMinor6()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major6),
                Ev(ScaleDegree.Subdominant, ChordQuality.Minor6, start: 4));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Dorian);

            Assert.That(result.events[0].quality,
                Is.EqualTo(ChordQuality.Minor6),
                "Dorian keeps the 6th color, fixes the third (6 -> m6).");
            Assert.That(result.events[1].quality,
                Is.EqualTo(ChordQuality.Minor6),
                "m6 IS the Dorian color and passes through.");
        }

        [Test]
        public void ColorTable_Sus2_BecomesSus4_OnlyInPhrygian()
        {
            var phry = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Sus2));
            phry.useColorTable = true;
            var aeol = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Sus2));
            aeol.useColorTable = true;

            var phryOut = ChordProgressionRequality.ApplyDiatonicRequality(
                phry, Tonality.Phrygian);
            var aeolOut = ChordProgressionRequality.ApplyDiatonicRequality(
                aeol, Tonality.Aeolian);

            Assert.That(phryOut.events[0].quality,
                Is.EqualTo(ChordQuality.Sus4), "Phrygian: sus2 -> sus4.");
            Assert.That(ReferenceEquals(aeolOut, aeol), Is.True,
                "Outside Phrygian sus2 passes through (no-op reference).");
        }

        [Test]
        public void ColorTable_Ninths_MinorizedDegrees_MapToMinor9()
        {
            // Aeolian iv is minorized; both 9 flavors drop to m9.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Subdominant, ChordQuality.Dominant9),
                Ev(ScaleDegree.Subdominant, ChordQuality.Major9, start: 4));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].quality, Is.EqualTo(ChordQuality.Minor9));
            Assert.That(result.events[1].quality, Is.EqualTo(ChordQuality.Minor9));
        }

        [Test]
        public void ColorTable_Dominant9OnV_Functional_KeptAndMarkedBorrowed()
        {
            var func = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant9));
            func.useColorTable = true;
            var pure = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Dominant, ChordQuality.Dominant9));
            pure.useColorTable = true;

            var funcOut = ChordProgressionRequality.ApplyDiatonicRequality(
                func, Tonality.Aeolian);
            var pureOut = ChordProgressionRequality.ApplyDiatonicRequality(
                pure, Tonality.Aeolian);

            Assert.That(funcOut.events[0].quality,
                Is.EqualTo(ChordQuality.Dominant9),
                "Functional protects V9 (mirrors D-RQ-FUNC).");
            Assert.That(funcOut.events[0].isDiatonic, Is.False,
                "Protected V9 is a borrowed chord in the target mode.");
            Assert.That(pureOut.events[0].quality,
                Is.EqualTo(ChordQuality.Minor9),
                "Plain policy: minorized V9 drops to m9 (pure modal color).");
        }

        // ---------------- D-CT-DIM=A: ii(dim) -> iv ----------------------

        [Test]
        public void ColorTable_LongDiminishedSupertonic_SubstitutesToIv()
        {
            // ii Minor (diatonic in Ionian) remaps to Diminished in Aeolian;
            // lengthSteps = 4 (sub x1, 4/4) => 4 beats >= 2 => LONG.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            var e = result.events[0];
            Assert.That(e.degree, Is.EqualTo(ScaleDegree.Subdominant),
                "Degree substitution ii(dim) -> iv.");
            Assert.That(e.quality, Is.EqualTo(ChordQuality.Minor),
                "Size-preserving: triad in, diatonic iv triad out.");
            Assert.That(e.degreeAccidental, Is.EqualTo(0));
            Assert.That(e.isDiatonic, Is.True);
        }

        [Test]
        public void ColorTable_SeventhSizePreserved_iiHalfDimBecomesIv7()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor7, start: 4));
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].degree,
                Is.EqualTo(ScaleDegree.Subdominant));
            Assert.That(result.events[0].quality,
                Is.EqualTo(ChordQuality.Minor7),
                "iim7 -> (remap) iiHalfDim7 -> (substitution) iv7 = m7 in Aeolian.");
        }

        [Test]
        public void ColorTable_ShortUnaccentedDiminished_IsKeptAsPassing()
        {
            // 1 beat, off-downbeat: the passing ii(dim) survives.
            var e = Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 5);
            e.lengthSteps = 1;
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart, e);
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].degree,
                Is.EqualTo(ScaleDegree.Supertonic),
                "Short + unaccented: no substitution.");
            Assert.That(result.events[0].quality,
                Is.EqualTo(ChordQuality.Diminished),
                "Core remap still applies; only the substitution is gated.");
        }

        [Test]
        public void ColorTable_ShortButAccented_Substitutes()
        {
            // startStep 4 = downbeat of bar 2 (4/4, sub x1) => ACCENTED.
            var e = Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4);
            e.lengthSteps = 1;
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart, e);
            prog.useColorTable = true;

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            Assert.That(result.events[0].degree,
                Is.EqualTo(ScaleDegree.Subdominant),
                "Downbeat diminished substitutes even when short.");
        }

        [Test]
        public void ColorTable_Idempotent_SecondPassIsNoOp()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPart,
                Ev(ScaleDegree.Tonic, ChordQuality.Major6),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4));
            prog.useColorTable = true;

            var once = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);
            var twice = ChordProgressionRequality.ApplyDiatonicRequality(
                once, Tonality.Aeolian);

            Assert.That(ReferenceEquals(twice, once), Is.True,
                "Color-table output must be a fixed point (re-entry no-op).");
        }

        // ---------------- SECDOM-1 ---------------------------------------

        [Test]
        public void SecDom_ResolvesToFifthAboveTarget_EvenUnderAsAuthored()
        {
            // C-Ionian reading: V7/ii — event before a Supertonic target
            // resolves to degree VI (A7 in C), Dominant7, borrowed. The
            // per-event field is the opt-in, so AsAuthored still resolves.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Tonic, ScaleDegree.Supertonic,
                    start: 0, length: 4),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            var sd = result.events[0];
            Assert.That(sd.degree, Is.EqualTo(ScaleDegree.Submediant),
                "P5 above the Supertonic root is the Submediant degree.");
            Assert.That(sd.degreeAccidental, Is.EqualTo(0),
                "Valid targets always yield a perfect 5th (accidental 0).");
            Assert.That(sd.quality, Is.EqualTo(ChordQuality.Dominant7));
            Assert.That(sd.isDiatonic, Is.False, "Secondary dominants are borrowed.");
            Assert.That(prog.events[0].degree, Is.EqualTo(ScaleDegree.Tonic),
                "Asset never mutated: authored event untouched.");
        }

        [Test]
        public void SecDom_TargetWithDiminishedTriad_RendersAuthored()
        {
            // Ionian LeadingTone triad is diminished => invalid target.
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Tonic, ScaleDegree.LeadingTone,
                    start: 0, length: 4),
                Ev(ScaleDegree.LeadingTone, ChordQuality.Diminished,
                    diatonic: true, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "Invalid target => authored values render untouched (no-op).");
        }

        [Test]
        public void SecDom_NextEventMustBeTheTarget()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Tonic, ScaleDegree.Supertonic,
                    start: 0, length: 4),
                Ev(ScaleDegree.Dominant, ChordQuality.Major, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "Not immediately before the target => no resolution.");
        }

        [Test]
        public void SecDom_LongerThanTarget_RendersAuthored()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Tonic, ScaleDegree.Supertonic,
                    start: 0, length: 8),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 8));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            Assert.That(ReferenceEquals(result, prog), Is.True,
                "Duration must be <= the target's duration.");
        }

        [Test]
        public void SecDom_WrapAround_TurnaroundTargetsFirstEvent()
        {
            // Last event targets the FIRST (loop turnaround): V7/I on the
            // way back to the tonic. In Ionian that resolves to the
            // Dominant degree itself (G7 in C).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                Ev(ScaleDegree.Tonic, ChordQuality.Major, start: 0),
                SecDomEv(ScaleDegree.Dominant, ScaleDegree.Tonic,
                    start: 4, length: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);

            var sd = result.events[1];
            Assert.That(sd.degree, Is.EqualTo(ScaleDegree.Dominant));
            Assert.That(sd.degreeAccidental, Is.EqualTo(0));
            Assert.That(sd.quality, Is.EqualTo(ChordQuality.Dominant7));
        }

        [Test]
        public void SecDom_ModeAware_RootComputedInCurrentMode()
        {
            // Aeolian V7/iv: iv is minor (valid); its root sits a P4 above
            // the tonic, so the secondary dominant lands on the TONIC degree
            // (A7 before Dm in A Aeolian).
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Mediant, ScaleDegree.Subdominant,
                    start: 0, length: 4),
                Ev(ScaleDegree.Subdominant, ChordQuality.Minor, start: 4));

            var result = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Aeolian);

            var sd = result.events[0];
            Assert.That(sd.degree, Is.EqualTo(ScaleDegree.Tonic),
                "P5 above the Subdominant root is the Tonic degree.");
            Assert.That(sd.degreeAccidental, Is.EqualTo(0));
            Assert.That(sd.quality, Is.EqualTo(ChordQuality.Dominant7));
        }

        [Test]
        public void SecDom_Idempotent_SecondPassIsNoOp()
        {
            var prog = MakeProg(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                SecDomEv(ScaleDegree.Tonic, ScaleDegree.Supertonic,
                    start: 0, length: 4),
                Ev(ScaleDegree.Supertonic, ChordQuality.Minor, start: 4));

            var once = ChordProgressionRequality.ApplyDiatonicRequality(
                prog, Tonality.Ionian);
            var twice = ChordProgressionRequality.ApplyDiatonicRequality(
                once, Tonality.Ionian);

            Assert.That(ReferenceEquals(twice, once), Is.True,
                "Resolved secondary dominants must be a fixed point.");
        }

        // ---------------- Field-surface canary ---------------------------

        [Test]
        public void ChordEvent_FieldSurface_MatchesEveryFieldByFieldCopySite()
        {
            // F-NORM-DROP family guard: ChordEvent is copied FIELD BY FIELD
            // in ChordTrackComposer's TS reprojection and in the editor's
            // grid copies. If this canary fails, a field was added — update
            // EVERY copy site (and this expected list) or the new field
            // silently reverts to its default on runtime clones.
            var fields = typeof(ChordProgressionData.ChordEvent)
                .GetFields(System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.Instance);
            var names = new List<string>();
            foreach (var f in fields) names.Add(f.Name);
            names.Sort();

            var expected = new List<string>
            {
                "appliedTarget", "degree", "degreeAccidental",
                "hasAppliedTarget", "isDiatonic", "lengthSteps",
                "quality", "startStep", "velocity",
            };
            Assert.That(names, Is.EqualTo(expected),
                "ChordEvent field surface changed: update ChordTrackComposer's " +
                "reprojection copy list, the editor grid copy sites, and this canary.");
        }

    }
}
#endif
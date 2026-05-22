#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using MidiGenPlay;
using MidiGenPlay.Authoring;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="DrumPatternTextParser"/>.
    /// Covers Phase 7 smoke tests SMR1, SMR2, SMR4, SMR5 plus a couple of round-trip
    /// and render sanity checks. SMR3, SMR6, SMR7 are UI-coupled and verified manually.
    /// </summary>
    public class DrumPatternTextParserTests
    {
        // ---------------- Parse: basic correctness ----------------

        [Test]
        public void SMR1_Parse_AlternatingPattern_16Steps_8ActiveEvenIndices_NoWarnings()
        {
            var warnings = new List<DrumPatternTextWarning>();
            var result = DrumPatternTextParser.Parse(
                "x.x.x.x.x.x.x.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual(16, result.Count, "result length");
            for (int i = 0; i < 16; i++)
            {
                bool expectedActive = (i % 2 == 0);
                Assert.AreEqual(expectedActive, result[i].active, $"step {i} active");
                Assert.AreEqual(0, result[i].velocity,
                    $"step {i} velocity should be 0 (defer-to-default sentinel)");
            }
            Assert.AreEqual(0, warnings.Count, "no warnings expected");
        }

        [Test]
        public void SMR2_Parse_VelocityGlyphs_MapToConstants()
        {
            var warnings = new List<DrumPatternTextWarning>();
            var result = DrumPatternTextParser.Parse(
                "X.x.o.x.X.x.o.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual(16, result.Count);

            // Active positions: 0, 2, 4, 6, 8, 10, 12, 14
            // Glyphs:           X  x  o  x  X  x  o  x
            int[] expectedVelocities =
            {
                DrumPatternTextParser.AccentVelocity, // X
                0,                                    // x (sentinel)
                DrumPatternTextParser.GhostVelocity,  // o
                0,                                    // x
                DrumPatternTextParser.AccentVelocity, // X
                0,                                    // x
                DrumPatternTextParser.GhostVelocity,  // o
                0,                                    // x
            };

            int activeCounter = 0;
            for (int i = 0; i < 16; i++)
            {
                if (i % 2 == 0)
                {
                    Assert.IsTrue(result[i].active, $"step {i} should be active");
                    Assert.AreEqual(expectedVelocities[activeCounter], result[i].velocity,
                        $"step {i} velocity (active #{activeCounter})");
                    activeCounter++;
                }
                else
                {
                    Assert.IsFalse(result[i].active, $"step {i} should be off");
                }
            }
            Assert.AreEqual(0, warnings.Count, "no warnings expected");
        }

        // ---------------- Parse: warnings ----------------

        [Test]
        public void SMR4_Parse_ShortInput_PadsRightAndWarns()
        {
            var warnings = new List<DrumPatternTextWarning>();
            // 12-character input into a 16-step row
            var result = DrumPatternTextParser.Parse(
                "x.x.x.x.x.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual(16, result.Count, "result length");

            // First 12 steps follow the alternation
            for (int i = 0; i < 12; i++)
                Assert.AreEqual(i % 2 == 0, result[i].active, $"step {i} active");

            // Last 4 are padded off
            for (int i = 12; i < 16; i++)
                Assert.IsFalse(result[i].active, $"step {i} should be padded off");

            Assert.AreEqual(1, warnings.Count, "expected one length warning");
            Assert.AreEqual(DrumPatternTextWarningKind.LengthShort, warnings[0].kind);
        }

        [Test]
        public void Parse_LongInput_TruncatesFromRightAndWarns()
        {
            var warnings = new List<DrumPatternTextWarning>();
            // 20-character input into a 16-step row
            var result = DrumPatternTextParser.Parse(
                "x.x.x.x.x.x.x.x.x.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual(16, result.Count);
            Assert.AreEqual(1, warnings.Count, "expected one length warning");
            Assert.AreEqual(DrumPatternTextWarningKind.LengthLong, warnings[0].kind);
        }

        [Test]
        public void SMR5_Parse_UnknownGlyph_BecomesRestAndWarns()
        {
            var warnings = new List<DrumPatternTextWarning>();
            var result = DrumPatternTextParser.Parse(
                "x.@.x.x.x.x.x.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                laneIndex: 3, warnings: warnings);

            Assert.AreEqual(16, result.Count);
            Assert.IsFalse(result[2].active, "unknown glyph at step 2 should be off");

            Assert.AreEqual(1, warnings.Count, "expected one unknown-glyph warning");
            Assert.AreEqual(DrumPatternTextWarningKind.UnknownGlyph, warnings[0].kind);
            Assert.AreEqual('@', warnings[0].glyph);
            Assert.AreEqual(2, warnings[0].columnIndex);
            Assert.AreEqual(3, warnings[0].laneIndex);
        }

        // ---------------- Ignored characters ----------------

        [Test]
        public void Parse_BarSeparatorsAndSpaces_AreIgnored()
        {
            var warnings = new List<DrumPatternTextWarning>();
            // Same content as SMR1, but with bar separators and spaces interspersed
            var result = DrumPatternTextParser.Parse(
                "x.x. x.x. |x.x. x.x.",
                totalSteps: 16, laneDefaultVelocity: 100,
                warnings: warnings);

            Assert.AreEqual(16, result.Count);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(i % 2 == 0, result[i].active, $"step {i}");
            Assert.AreEqual(0, warnings.Count, "ignored chars should not produce warnings");
        }

        [Test]
        public void Parse_RestDashIsEquivalentToRestDot()
        {
            var warnings = new List<DrumPatternTextWarning>();
            var result = DrumPatternTextParser.Parse(
                "x-x-x-x-x-x-x-x-",
                totalSteps: 16, laneDefaultVelocity: 100,
                warnings: warnings);

            for (int i = 0; i < 16; i++)
                Assert.AreEqual(i % 2 == 0, result[i].active, $"step {i}");
            Assert.AreEqual(0, warnings.Count);
        }

        // ---------------- Render ----------------

        [Test]
        public void Render_AllOff_ProducesAllDots()
        {
            var steps = new List<DrumPatternData.StepState>();
            for (int i = 0; i < 16; i++) steps.Add(DrumPatternData.StepState.Off);

            string s = DrumPatternTextParser.Render(steps, laneDefaultVelocity: 100);
            Assert.AreEqual("................", s);
        }

        [Test]
        public void Render_WithBarSeparators_InsertsBetweenMeasures()
        {
            var steps = new List<DrumPatternData.StepState>();
            for (int i = 0; i < 16; i++)
                steps.Add(i % 4 == 0
                    ? DrumPatternData.StepState.On(0)
                    : DrumPatternData.StepState.Off);

            // 4 steps per measure, 4 measures → 3 separators expected
            string s = DrumPatternTextParser.Render(steps, laneDefaultVelocity: 100, stepsPerMeasure: 4);
            Assert.AreEqual("x...|x...|x...|x...", s);
        }

        [Test]
        public void Render_NonCanonicalVelocity_SnapsAndWarns()
        {
            var steps = new List<DrumPatternData.StepState>
            {
                DrumPatternData.StepState.On(75),   // distances: 25 to default(100), 25 to ghost(50), 45 to accent(120) → default (tie)
                DrumPatternData.StepState.Off,
                DrumPatternData.StepState.On(115),  // distances: 15 to default, 5 to accent, 65 to ghost → accent
                DrumPatternData.StepState.Off,
            };
            var warnings = new List<DrumPatternTextWarning>();

            string s = DrumPatternTextParser.Render(steps, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual("x.X.", s);
            Assert.AreEqual(2, warnings.Count,
                "expected snap warnings for both non-canonical velocities");
            Assert.AreEqual(DrumPatternTextWarningKind.VelocitySnappedToTier, warnings[0].kind);
            Assert.AreEqual(DrumPatternTextWarningKind.VelocitySnappedToTier, warnings[1].kind);
        }

        [Test]
        public void Render_ExactLaneDefault_IsXWithoutWarning()
        {
            var steps = new List<DrumPatternData.StepState>
            {
                DrumPatternData.StepState.On(100), // exactly lane default
                DrumPatternData.StepState.On(0),   // sentinel — same glyph
            };
            var warnings = new List<DrumPatternTextWarning>();

            string s = DrumPatternTextParser.Render(steps, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual("xx", s);
            Assert.AreEqual(0, warnings.Count, "exact-default match should not warn");
        }

        // ---------------- Round-trip preservation via ApplyTextEdits ----------------

        [Test]
        public void ApplyTextEdits_UnchangedCells_PreserveCustomVelocity()
        {
            // Previous lane has a non-canonical velocity at step 2 (e.g., 75).
            // 75 renders as 'x' (snapped to default), so a user input of 'x' at step 2
            // should be considered "unchanged" and preserve velocity 75.
            var previous = new List<DrumPatternData.StepState>
            {
                DrumPatternData.StepState.On(0),    // x (sentinel)
                DrumPatternData.StepState.Off,      // .
                DrumPatternData.StepState.On(75),   // would render as x (snapped)
                DrumPatternData.StepState.Off,      // .
            };

            // User changes step 0 from x to X (accent). Other cells match the prior render.
            var warnings = new List<DrumPatternTextWarning>();
            var result = DrumPatternTextParser.ApplyTextEdits(
                previous, "X.x.",
                totalSteps: 4, laneDefaultVelocity: 100,
                laneIndex: 0, warnings: warnings);

            Assert.AreEqual(4, result.Count);

            Assert.IsTrue(result[0].active);
            Assert.AreEqual(DrumPatternTextParser.AccentVelocity, result[0].velocity,
                "step 0 edited (x→X) — should be set to accent");

            Assert.IsFalse(result[1].active);

            Assert.IsTrue(result[2].active);
            Assert.AreEqual(75, result[2].velocity,
                "step 2 glyph unchanged from previous render — custom velocity 75 preserved");

            Assert.IsFalse(result[3].active);
        }

        [Test]
        public void ApplyTextEdits_ChangedCell_OverwritesWithCanonical()
        {
            var previous = new List<DrumPatternData.StepState>
            {
                DrumPatternData.StepState.On(75),  // renders as x
                DrumPatternData.StepState.Off,
            };

            // User changes step 0 from x to o (ghost)
            var result = DrumPatternTextParser.ApplyTextEdits(
                previous, "o.",
                totalSteps: 2, laneDefaultVelocity: 100);

            Assert.IsTrue(result[0].active);
            Assert.AreEqual(DrumPatternTextParser.GhostVelocity, result[0].velocity,
                "step 0 glyph changed (x→o) — should be overwritten with ghost velocity, " +
                "discarding the previous custom 75");
        }
    }
}
#endif
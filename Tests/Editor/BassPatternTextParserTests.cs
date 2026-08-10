#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using MidiGenPlay.Authoring;
using SelfPocketStep = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketStep;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="BassPatternTextParser"/>
    /// (MGP-BASSCARD-WIZARD-1). Pins:
    /// - the D1=A glyph map, both directions, including case sensitivity
    /// - the shared drum-DSL laws (ignored chars, UnknownGlyph → rest)
    /// - the declared divergences: free length (D13), exact round-trip
    ///   identity with no per-cell diff (D11), warning-free render
    /// - cross-DSL guard: the drum alphabet is unknown here
    /// </summary>
    public class BassPatternTextParserTests
    {
        // ---------------- Parse: glyph map ----------------

        [Test]
        public void Parse_AllSevenClasses_MapToEnumMembers_NoWarnings()
        {
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse("SP.gGHL", "body", warnings);

            var expected = new[]
            {
                SelfPocketStep.Slap,
                SelfPocketStep.Pop,
                SelfPocketStep.Rest,
                SelfPocketStep.Ghost,
                SelfPocketStep.GhostPop,
                SelfPocketStep.HammerOn,
                SelfPocketStep.PullOff,
            };

            Assert.AreEqual(expected.Length, result.Count, "result length");
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], result[i], $"step {i}");
            Assert.AreEqual(0, warnings.Count, "no warnings expected");
        }

        [Test]
        public void Parse_DashAndDot_AreBothRest()
        {
            var result = BassPatternTextParser.Parse("S-S.S", "body");
            Assert.AreEqual(SelfPocketStep.Rest, result[1], "dash rest");
            Assert.AreEqual(SelfPocketStep.Rest, result[3], "dot rest");
        }

        [Test]
        public void Parse_CaseIsSignificant_LowerGIsGhost_UpperGIsGhostPop()
        {
            var result = BassPatternTextParser.Parse("gG", "body");
            Assert.AreEqual(SelfPocketStep.Ghost, result[0]);
            Assert.AreEqual(SelfPocketStep.GhostPop, result[1]);
        }

        // ---------------- Parse: ignored characters ----------------

        [Test]
        public void Parse_WhitespaceAndBarSeparators_AreIgnored()
        {
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse(
                "S . . g | P .\tg .\n S . g . | P . g .", "body", warnings);

            Assert.AreEqual(16, result.Count, "16 parseable glyphs");
            Assert.AreEqual(0, warnings.Count, "separators and whitespace never warn");
            Assert.AreEqual(SelfPocketStep.Slap, result[0]);
            Assert.AreEqual(SelfPocketStep.Ghost, result[3]);
            Assert.AreEqual(SelfPocketStep.Pop, result[4]);
        }

        // ---------------- Parse: degradation laws ----------------

        [Test]
        public void Parse_UnknownGlyph_BecomesRest_WithLocatedWarning()
        {
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse("S?P", "bar 3 / variant 1", warnings);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(SelfPocketStep.Rest, result[1], "unknown degrades to rest");
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(BassPatternTextWarningKind.UnknownGlyph, warnings[0].kind);
            Assert.AreEqual(1, warnings[0].stepIndex, "cleaned-input step index");
            Assert.AreEqual('?', warnings[0].glyph);
            Assert.AreEqual("bar 3 / variant 1", warnings[0].bufferLabel);
        }

        [Test]
        public void Parse_DrumAlphabet_IsUnknownHere()
        {
            // Cross-DSL guard: x/X/o are drum glyphs; the two DSLs share law,
            // not alphabet. Muscle-memory input must warn, not silently pass.
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse("xXo", "body", warnings);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(3, warnings.Count, "one UnknownGlyph per drum glyph");
            foreach (var w in warnings)
                Assert.AreEqual(BassPatternTextWarningKind.UnknownGlyph, w.kind);
            foreach (var s in result)
                Assert.AreEqual(SelfPocketStep.Rest, s);
        }

        [Test]
        public void Parse_LowercaseAliasesOfKnownGlyphs_AreUnknown()
        {
            // 's', 'p', 'h', 'l' are NOT accepted spellings (D1=A is strict).
            var warnings = new List<BassPatternTextWarning>();
            BassPatternTextParser.Parse("sphl", "body", warnings);
            Assert.AreEqual(4, warnings.Count);
        }

        [Test]
        public void Parse_EmptyOrWhitespaceOnly_YieldsEmptyList_WithEmptyPatternWarning()
        {
            foreach (var input in new[] { null, "", "   ", "||", " | \t " })
            {
                var warnings = new List<BassPatternTextWarning>();
                var result = BassPatternTextParser.Parse(input, "body", warnings);

                Assert.AreEqual(0, result.Count, $"input '{input}' parses empty");
                Assert.AreEqual(1, warnings.Count, $"input '{input}' warns once");
                Assert.AreEqual(BassPatternTextWarningKind.EmptyPattern, warnings[0].kind);
                Assert.AreEqual(-1, warnings[0].stepIndex);
            }
        }

        // ---------------- D13: length is content ----------------

        [Test]
        public void Parse_LengthIsFree_NoPaddingNoTruncation()
        {
            // 5 glyphs → 5 steps; there is no totalSteps in the bass DSL.
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse("SP.gH", "body", warnings);

            Assert.AreEqual(5, result.Count);
            Assert.AreEqual(0, warnings.Count, "no length warnings exist in this parser");
        }

        // ---------------- Render ----------------

        [Test]
        public void Render_CanonicalGlyphs_RestRendersDot()
        {
            var steps = new List<SelfPocketStep>
            {
                SelfPocketStep.Slap, SelfPocketStep.Rest, SelfPocketStep.Rest,
                SelfPocketStep.Ghost, SelfPocketStep.Pop, SelfPocketStep.Rest,
                SelfPocketStep.GhostPop, SelfPocketStep.PullOff,
            };

            Assert.AreEqual("S..gP.GL", BassPatternTextParser.Render(steps));
        }

        [Test]
        public void Render_StepsPerBar_InsertsBarSeparators()
        {
            var steps = new List<SelfPocketStep>();
            for (int i = 0; i < 16; i++)
                steps.Add(i % 2 == 0 ? SelfPocketStep.Slap : SelfPocketStep.Pop);

            string text = BassPatternTextParser.Render(steps, stepsPerBar: 8);
            Assert.AreEqual("SPSPSPSP|SPSPSPSP", text);
        }

        [Test]
        public void Render_NullOrEmpty_YieldsEmptyString()
        {
            Assert.AreEqual(string.Empty, BassPatternTextParser.Render(null));
            Assert.AreEqual(string.Empty,
                BassPatternTextParser.Render(new List<SelfPocketStep>()));
        }

        // ---------------- D11: round-trip identity ----------------

        [Test]
        public void RoundTrip_PatternToTextToPattern_IsExactIdentity()
        {
            // Every enum member present, non-divisor length on purpose.
            var original = new List<SelfPocketStep>
            {
                SelfPocketStep.Slap, SelfPocketStep.Rest, SelfPocketStep.Ghost,
                SelfPocketStep.Pop, SelfPocketStep.HammerOn, SelfPocketStep.PullOff,
                SelfPocketStep.GhostPop, SelfPocketStep.Rest, SelfPocketStep.Slap,
            };

            string text = BassPatternTextParser.Render(original);
            var warnings = new List<BassPatternTextWarning>();
            var reparsed = BassPatternTextParser.Parse(text, "body", warnings);

            Assert.AreEqual(0, warnings.Count, "canonical text never warns");
            CollectionAssert.AreEqual(original, reparsed, "exact identity — no tiers, no snap");
        }

        [Test]
        public void RoundTrip_WithBarSeparators_IsExactIdentity()
        {
            var original = new List<SelfPocketStep>();
            var members = (SelfPocketStep[])System.Enum.GetValues(typeof(SelfPocketStep));
            for (int i = 0; i < 32; i++)
                original.Add(members[i % members.Length]);

            string text = BassPatternTextParser.Render(original, stepsPerBar: 16);
            StringAssert.Contains("|", text, "separator present at 16-step bars");

            var reparsed = BassPatternTextParser.Parse(text, "body");
            CollectionAssert.AreEqual(original, reparsed, "separators are ignored on re-parse");
        }

        // ---------------- Preset-spec sanity (governed-alphabet spelling) ----------------

        [Test]
        public void Parse_AeroplaneBody_UnderGovernedAlphabet_ParsesCleanTo16()
        {
            // PhrasePresets_Bass_Spec §1 body, respelled to D1=A
            // (draft '·' → '.', draft 'gp' → 'G'; here the body has no GhostPop
            // so only the rest spelling changes).
            var warnings = new List<BassPatternTextWarning>();
            var result = BassPatternTextParser.Parse(
                "S..gP.g.S.g.P.g.", "body", warnings);

            Assert.AreEqual(16, result.Count, "one 4/4 QuarterBeat bar");
            Assert.AreEqual(0, warnings.Count);
        }
    }
}
#endif
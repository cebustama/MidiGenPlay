#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for <see cref="DrumPatternEditorImporter"/> — the pure
    /// "setup card + DSL block" parser (D-L8 / D-L2.6). Covers the roadmap DoD
    /// cases: happy-path full block, malformed setup card (DSL-only fallback),
    /// alias resolution, alias-not-found (no silent fallback), and lane-count
    /// mismatch.
    /// </summary>
    public class DrumPatternEditorImporterTests
    {
        // -----------------------------
        // Helpers
        // -----------------------------

        /// <summary>
        /// Stub alias resolver covering the handful of short names the tests use.
        /// Mirrors the L1 LaneShortNames conventions; the real
        /// LaneAliasDictionary supplies the full map at runtime.
        /// </summary>
        private static GeneralMidiPercussion? StubAlias(string token)
        {
            switch (token)
            {
                case "BD": return GeneralMidiPercussion.BassDrum1;
                case "SN": return GeneralMidiPercussion.AcousticSnare;
                case "HHc": return GeneralMidiPercussion.ClosedHiHat;
                case "HHo": return GeneralMidiPercussion.OpenHiHat;
                default: return null;
            }
        }

        private static bool HasWarning(
            DrumPatternEditorImporter.Result r,
            DrumPatternEditorImporter.ImportWarningKind kind)
        {
            foreach (var w in r.warnings)
                if (w.kind == kind) return true;
            return false;
        }

        private const string FullCanonicalPayload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 2
- Subdivisions: 4
- Lanes (in this order):
  1. BassDrum1 (GM 36) — default velocity 100
  2. AcousticSnare (GM 38) — default velocity 110
  3. ClosedHiHat (GM 42) — default velocity 80
  4. OpenHiHat (GM 46) — default velocity 90

**DSL (switch to Text mode, paste one line per lane):**

```
x..x..x...x.....
....x.......x...
xxxxxxxxxxxxxxxx
....X.......X...
```";

        // -----------------------------
        // 1. Happy-path full block
        // -----------------------------

        [Test]
        public void Parse_FullCanonicalBlock_ReturnsFullWithGridAndLanes()
        {
            var r = DrumPatternEditorImporter.Parse(FullCanonicalPayload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode, "mode");
            Assert.AreEqual(TimeSignature.FourFour, r.timeSignature, "time signature");
            Assert.AreEqual(2, r.measures, "measures");
            Assert.AreEqual(4, r.subdivisions, "subdivisions");

            Assert.AreEqual(4, r.lanes.Count, "lane count");
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, r.lanes[0].instrument);
            Assert.AreEqual(100, r.lanes[0].defaultVelocity);
            Assert.AreEqual(GeneralMidiPercussion.AcousticSnare, r.lanes[1].instrument);
            Assert.AreEqual(110, r.lanes[1].defaultVelocity);
            Assert.AreEqual(GeneralMidiPercussion.ClosedHiHat, r.lanes[2].instrument);
            Assert.AreEqual(GeneralMidiPercussion.OpenHiHat, r.lanes[3].instrument);

            Assert.AreEqual(4, r.dslLines.Count, "dsl line count");
            Assert.AreEqual("x..x..x...x.....", r.dslLines[0]);
            Assert.AreEqual("....X.......X...", r.dslLines[3]);

            // No warnings on the clean path.
            Assert.AreEqual(0, r.warnings.Count, "no warnings expected on happy path");
        }

        [Test]
        public void Parse_FullBlock_ExactEnumNamesResolveWithoutAliasResolver()
        {
            // Enum names must resolve even when no alias resolver is supplied.
            var r = DrumPatternEditorImporter.Parse(FullCanonicalPayload, aliasResolver: null);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(4, r.lanes.Count);
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, r.lanes[0].instrument);
        }

        // -----------------------------
        // 2. Malformed setup card → DSL-only fallback
        // -----------------------------

        [Test]
        public void Parse_GarbledSetupCard_FallsBackToDslOnly()
        {
            // Setup card present in spirit but the mechanical fields are unreadable.
            const string payload =
@"Here's a beat, configure it yourself:

(no proper setup fields here)

```
x..x..x...x.....
....x.......x...
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.DslOnly, r.mode, "mode");
            Assert.AreEqual(2, r.dslLines.Count, "dsl lines still extracted");
            Assert.IsTrue(
                HasWarning(r, DrumPatternEditorImporter.ImportWarningKind.MissingOrGarbledSetupCard),
                "should warn about garbled/missing setup card");
        }

        [Test]
        public void Parse_NoFencedBlock_ReturnsFailed()
        {
            const string payload =
@"**Setup (configure in Grid mode):**
- Time signature: FourFour
- Measures: 2
- Subdivisions: 4
(no DSL block at all)";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Failed, r.mode, "mode");
            Assert.AreEqual(0, r.dslLines.Count, "no dsl lines");
            Assert.IsTrue(
                HasWarning(r, DrumPatternEditorImporter.ImportWarningKind.MissingDslBlock),
                "should warn about missing DSL block");
        }

        // -----------------------------
        // 3. Alias resolution
        // -----------------------------

        [Test]
        public void Parse_LanesUseShortNameAliases_ResolveViaResolver()
        {
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes (in this order):
  1. BD (GM 36) — default velocity 100
  2. SN (GM 38) — default velocity 110
  3. HHc (GM 42) — default velocity 80

```
x..x..x...x.....
....x.......x...
xxxxxxxxxxxxxxxx
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode, "mode");
            Assert.AreEqual(3, r.lanes.Count, "lane count");
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, r.lanes[0].instrument, "BD → BassDrum1");
            Assert.AreEqual(GeneralMidiPercussion.AcousticSnare, r.lanes[1].instrument, "SN → AcousticSnare");
            Assert.AreEqual(GeneralMidiPercussion.ClosedHiHat, r.lanes[2].instrument, "HHc → ClosedHiHat");
            Assert.AreEqual(0, r.warnings.Count, "clean alias resolution → no warnings");
        }

        // -----------------------------
        // 4. Alias not found → warn, no silent fallback
        // -----------------------------

        [Test]
        public void Parse_UnknownAlias_WarnsAndDoesNotSilentlyFallBack()
        {
            // "ZZ" resolves to neither an enum name nor the stub aliases.
            // The UnknownInstrument warning must be present (the failure is not
            // silent). Here the resolved lane count (2) no longer matches the 3
            // DSL lines, so the importer additionally degrades to DSL-only — that
            // is expected. The guarantee under test is the WARNING, not the mode.
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes (in this order):
  1. BD (GM 36) — default velocity 100
  2. ZZ (GM 99) — default velocity 110
  3. HHc (GM 42) — default velocity 80

```
x..x..x...x.....
....x.......x...
xxxxxxxxxxxxxxxx
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.IsTrue(
                HasWarning(r, DrumPatternEditorImporter.ImportWarningKind.UnknownInstrument),
                "unknown instrument must produce a warning (no silent fallback)");

            // Whatever lanes are reported, none may be a fabricated stand-in for ZZ.
            foreach (var lane in r.lanes)
                Assert.AreNotEqual("ZZ", lane.instrument.ToString(),
                    "unknown token must never be silently replaced by a default");
        }

        [Test]
        public void Parse_UnknownAlias_ResolvedLanesSurvive_WhenCountStillMatches()
        {
            // Two resolvable lanes + one unknown; the DSL block has exactly two
            // lines, so after omitting the unknown lane the count matches (2 == 2)
            // and the import succeeds as Full. Confirms the resolvable lanes are
            // retained and the unknown one is dropped (warned), not substituted.
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes (in this order):
  1. BD (GM 36) — default velocity 100
  2. ZZ (GM 99) — default velocity 110
  3. HHc (GM 42) — default velocity 80

```
x..x..x...x.....
xxxxxxxxxxxxxxxx
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.IsTrue(
                HasWarning(r, DrumPatternEditorImporter.ImportWarningKind.UnknownInstrument),
                "unknown instrument must still warn");
            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode,
                "with 2 resolvable lanes and 2 DSL lines, the import is Full");
            Assert.AreEqual(2, r.lanes.Count, "only the two resolvable lanes survive");
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, r.lanes[0].instrument);
            Assert.AreEqual(GeneralMidiPercussion.ClosedHiHat, r.lanes[1].instrument);
        }

        // -----------------------------
        // 5. Lane-count mismatch
        // -----------------------------

        [Test]
        public void Parse_LaneCountMismatch_DegradesToDslOnlyWithWarning()
        {
            // 4 lanes in the card, only 3 DSL lines.
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 2
- Subdivisions: 4
- Lanes (in this order):
  1. BassDrum1 (GM 36) — default velocity 100
  2. AcousticSnare (GM 38) — default velocity 110
  3. ClosedHiHat (GM 42) — default velocity 80
  4. OpenHiHat (GM 46) — default velocity 90

```
x..x..x...x.....
....x.......x...
xxxxxxxxxxxxxxxx
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.DslOnly, r.mode, "mode");
            Assert.AreEqual(3, r.dslLines.Count, "dsl lines preserved");
            Assert.IsTrue(
                HasWarning(r, DrumPatternEditorImporter.ImportWarningKind.LaneCountMismatch),
                "should warn about lane-count mismatch");
        }

        // -----------------------------
        // Robustness extras
        // -----------------------------

        [Test]
        public void Parse_HyphenSeparatorAndNoGmAnnotation_StillParses()
        {
            // ASCII hyphen instead of em-dash; no "(GM NN)" annotation.
            const string payload =
@"Setup:
- Time signature: ThreeFour
- Measures: 1
- Subdivisions: 2
- Lanes:
  1. BassDrum1 - default velocity 100
  2. AcousticSnare - default velocity 110

```
x...x.
..x..x
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode, "mode");
            Assert.AreEqual(TimeSignature.ThreeFour, r.timeSignature);
            Assert.AreEqual(2, r.lanes.Count);
            Assert.AreEqual(100, r.lanes[0].defaultVelocity);
        }

        [Test]
        public void Parse_LanguageTaggedFence_ExtractsDsl()
        {
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 1
- Subdivisions: 2
- Lanes (in this order):
  1. BassDrum1 (GM 36) — default velocity 100

```text
x...x...
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(1, r.dslLines.Count);
            Assert.AreEqual("x...x...", r.dslLines[0]);
        }

        [Test]
        public void Parse_OuterFenceWrapped_BareDslAfterLabel_ParsesAsFull()
        {
            // Real-world copy hazard: the whole response is wrapped in ONE outer
            // code fence, and the DSL glyph lines are bare text after the
            // "**DSL ...**" label (no inner fence). Glyph-line detection must
            // still isolate the four DSL lines and the setup card.
            const string payload =
@"```
**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 2
- Subdivisions: 4
- Lanes (in this order):
  1. BassDrum1 (GM 36) — default velocity 100
  2. AcousticSnare (GM 38) — default velocity 110
  3. ClosedHiHat (GM 42) — default velocity 80
  4. OpenHiHat (GM 46) — default velocity 90

**DSL (switch to Text mode, paste one line per lane):**
x..x..x...x.....x..x..x...x.....
....x.......x.......x.......x...
xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
....X.......X.......X.......X...
```";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode, "mode");
            Assert.AreEqual(TimeSignature.FourFour, r.timeSignature);
            Assert.AreEqual(2, r.measures);
            Assert.AreEqual(4, r.subdivisions);
            Assert.AreEqual(4, r.lanes.Count, "all four lanes resolved");
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, r.lanes[0].instrument);
            Assert.AreEqual(GeneralMidiPercussion.OpenHiHat, r.lanes[3].instrument);
            Assert.AreEqual(4, r.dslLines.Count, "four bare DSL lines extracted");
            Assert.AreEqual("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", r.dslLines[2]);
        }

        [Test]
        public void Parse_BareDslNoFenceAtAll_ParsesAsFull()
        {
            // No fences anywhere; DSL is bare lines after the label.
            const string payload =
@"**Setup (configure in Grid mode):**

- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes (in this order):
  1. BassDrum1 (GM 36) — default velocity 100
  2. AcousticSnare (GM 38) — default velocity 110

DSL:
x..x..x...x.....
....x.......x...";

            var r = DrumPatternEditorImporter.Parse(payload, StubAlias);

            Assert.AreEqual(DrumPatternEditorImporter.ImportMode.Full, r.mode);
            Assert.AreEqual(2, r.lanes.Count);
            Assert.AreEqual(2, r.dslLines.Count);
        }
    }
}
#endif
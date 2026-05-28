#if UNITY_EDITOR
using Melanchall.DryWetMidi.Standards;
using NUnit.Framework;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the synchronous translation path of
    /// <see cref="DrumPatternLLMResponseHandler"/> (<c>FromPayload</c>, the
    /// clipboard-Import route). The async <c>GenerateAsync</c> path is exercised
    /// end-to-end at L1 and via the L3 smoke tests (SMR-L4/L5), since
    /// <c>PromptExecutionHelper</c> is a non-injectable LLM Core static.
    /// </summary>
    public class DrumPatternLLMResponseHandlerTests
    {
        private static GeneralMidiPercussion? Alias(string token) =>
            LaneAliasDictionary.TryResolve(token);

        private const string FullPayload =
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
....X.......X...
```";

        [Test]
        public void FromPayload_FullBlock_ProducesFullOutcome()
        {
            var o = DrumPatternLLMResponseHandler.FromPayload(FullPayload, Alias);

            Assert.AreEqual(DrumPatternLLMResponseHandler.OutcomeKind.Full, o.kind);
            Assert.IsTrue(o.Success);
            Assert.AreEqual(TimeSignature.FourFour, o.timeSignature);
            Assert.AreEqual(2, o.measures);
            Assert.AreEqual(4, o.subdivisions);
            Assert.AreEqual(4, o.lanes.Count);
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, o.lanes[0].instrument);
            Assert.AreEqual(4, o.dslLines.Count);
            Assert.AreEqual("x..x..x...x.....", o.dslLines[0]);
            // Clipboard import → no token usage.
            Assert.AreEqual(0, o.inputTokens);
            Assert.AreEqual(0, o.outputTokens);
        }

        [Test]
        public void FromPayload_AliasesResolveThroughRealDictionary()
        {
            const string payload =
@"Setup:
- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes:
  1. BD (GM 36) — default velocity 100
  2. SN (GM 38) — default velocity 110

```
x...x...x...x...
....x.......x...
```";

            var o = DrumPatternLLMResponseHandler.FromPayload(payload, Alias);

            Assert.AreEqual(DrumPatternLLMResponseHandler.OutcomeKind.Full, o.kind);
            Assert.AreEqual(GeneralMidiPercussion.BassDrum1, o.lanes[0].instrument);
            Assert.AreEqual(GeneralMidiPercussion.AcousticSnare, o.lanes[1].instrument);
        }

        [Test]
        public void FromPayload_GarbledCard_ProducesDslOnlyOutcome()
        {
            const string payload =
@"just a beat:

```
x..x..x...x.....
....x.......x...
```";

            var o = DrumPatternLLMResponseHandler.FromPayload(payload, Alias);

            Assert.AreEqual(DrumPatternLLMResponseHandler.OutcomeKind.DslOnly, o.kind);
            Assert.IsTrue(o.Success, "DSL-only is still a usable (successful) outcome");
            Assert.AreEqual(2, o.dslLines.Count);
            Assert.Greater(o.displayWarnings.Count, 0, "should carry a warning about the missing card");
        }

        [Test]
        public void FromPayload_NoDslBlock_ProducesFailedOutcome()
        {
            const string payload =
@"**Setup (configure in Grid mode):**
- Time signature: FourFour
- Measures: 2
- Subdivisions: 4
(no DSL block)";

            var o = DrumPatternLLMResponseHandler.FromPayload(payload, Alias);

            Assert.AreEqual(DrumPatternLLMResponseHandler.OutcomeKind.Failed, o.kind);
            Assert.IsFalse(o.Success);
            Assert.AreEqual(0, o.dslLines.Count);
            Assert.Greater(o.displayWarnings.Count, 0);
        }

        [Test]
        public void FromPayload_UnknownInstrument_SurfacesWarning()
        {
            // Two resolvable + one unknown, DSL has two lines → Full after omission,
            // but the unknown-instrument warning must surface in displayWarnings.
            const string payload =
@"Setup:
- Time signature: FourFour
- Measures: 1
- Subdivisions: 4
- Lanes:
  1. BD (GM 36) — default velocity 100
  2. ZZ (GM 99) — default velocity 110
  3. HHc (GM 42) — default velocity 80

```
x...x...x...x...
xxxxxxxxxxxxxxxx
```";

            var o = DrumPatternLLMResponseHandler.FromPayload(payload, Alias);

            Assert.AreEqual(DrumPatternLLMResponseHandler.OutcomeKind.Full, o.kind);
            bool hasUnknown = false;
            foreach (var w in o.displayWarnings)
                if (w.Contains("UnknownInstrument")) hasUnknown = true;
            Assert.IsTrue(hasUnknown, "unknown-instrument warning must reach displayWarnings");
        }
    }
}
#endif
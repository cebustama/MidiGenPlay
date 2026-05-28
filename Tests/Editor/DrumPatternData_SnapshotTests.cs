#if UNITY_EDITOR
// Locks the SnapshotAsStepVelocities contract: per-step velocity resolves to
// override-if-nonzero, else lane default. This is the runtime velocity path
// into generated MIDI since the 2026-05-23 ComposeFromGrid switch
// (changelog-ssot.md), so the contract is load-bearing and any regression
// here corrupts every grid-authored rhythm track.

using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using NUnit.Framework;
using UnityEngine;

namespace MidiGenPlay.Tests.Editor
{
    public class DrumPatternData_SnapshotTests
    {
        [Test]
        public void SnapshotAsStepVelocities_MixedLane_ResolvesPerStep()
        {
            var data = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                data.subdivisions = 4;
                data.Measures = 1;
                // 4 beats * 4 subdivisions = 16 steps (4/4 default beatsPerMeasure
                // via DrumPatternData defaults; only step count matters here).

                var lane = new DrumPatternData.Lane
                {
                    instrument = GeneralMidiPercussion.AcousticBassDrum,
                    defaultVelocity = 100,
                };
                for (int i = 0; i < 16; i++)
                    lane.steps.Add(DrumPatternData.StepState.Off);

                // Step 0: sentinel (velocity=0) → resolves to lane default 100
                lane.steps[0] = DrumPatternData.StepState.On(0);
                // Step 4: explicit accent 120
                lane.steps[4] = DrumPatternData.StepState.On(120);
                // Step 8: explicit ghost 50
                lane.steps[8] = DrumPatternData.StepState.On(50);
                // Step 12: explicit non-canonical 73
                lane.steps[12] = DrumPatternData.StepState.On(73);
                data.lanes.Add(lane);

                var snap = data.SnapshotAsStepVelocities();
                Assert.That(snap.Length, Is.EqualTo(1));
                var steps = snap[0].steps;
                Assert.That(steps.Count, Is.EqualTo(4),
                    "exactly 4 active hits expected; off-steps must not be emitted.");

                Assert.That(steps[0].stepIndex, Is.EqualTo(0));
                Assert.That(steps[0].velocity, Is.EqualTo(100),
                    "sentinel velocity 0 must resolve to lane defaultVelocity (100).");

                Assert.That(steps[1].stepIndex, Is.EqualTo(4));
                Assert.That(steps[1].velocity, Is.EqualTo(120),
                    "explicit accent must pass through unchanged.");

                Assert.That(steps[2].stepIndex, Is.EqualTo(8));
                Assert.That(steps[2].velocity, Is.EqualTo(50),
                    "explicit ghost must pass through unchanged.");

                Assert.That(steps[3].stepIndex, Is.EqualTo(12));
                Assert.That(steps[3].velocity, Is.EqualTo(73),
                    "explicit non-canonical velocity must pass through unchanged " +
                    "(per-cell-diff round-trip relies on this).");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void SnapshotAsStepVelocities_AllOffLane_ReturnsEmptySteps()
        {
            var data = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                data.subdivisions = 4;
                data.Measures = 1;

                var lane = new DrumPatternData.Lane
                {
                    instrument = GeneralMidiPercussion.ClosedHiHat,
                    defaultVelocity = 100,
                };
                for (int i = 0; i < 16; i++)
                    lane.steps.Add(DrumPatternData.StepState.Off);
                data.lanes.Add(lane);

                var snap = data.SnapshotAsStepVelocities();
                Assert.That(snap.Length, Is.EqualTo(1));
                Assert.That(snap[0].steps, Is.Empty,
                    "all-off lane must produce zero hits.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void SnapshotAsStepVelocities_MultipleLanes_AreIndependent()
        {
            var data = ScriptableObject.CreateInstance<DrumPatternData>();
            try
            {
                data.subdivisions = 4;
                data.Measures = 1;

                var kick = new DrumPatternData.Lane
                {
                    instrument = GeneralMidiPercussion.AcousticBassDrum,
                    defaultVelocity = 110,
                };
                var hat = new DrumPatternData.Lane
                {
                    instrument = GeneralMidiPercussion.ClosedHiHat,
                    defaultVelocity = 90,
                };
                for (int i = 0; i < 16; i++)
                {
                    kick.steps.Add(DrumPatternData.StepState.Off);
                    hat.steps.Add(DrumPatternData.StepState.Off);
                }
                // Kick on beats 0 and 8 with sentinel → defaults to 110
                kick.steps[0] = DrumPatternData.StepState.On(0);
                kick.steps[8] = DrumPatternData.StepState.On(0);
                // Hat on every 2nd step with explicit accent 120
                for (int i = 0; i < 16; i += 2)
                    hat.steps[i] = DrumPatternData.StepState.On(120);

                data.lanes.Add(kick);
                data.lanes.Add(hat);

                var snap = data.SnapshotAsStepVelocities();
                Assert.That(snap.Length, Is.EqualTo(2));

                Assert.That(snap[0].instrument, Is.EqualTo(GeneralMidiPercussion.AcousticBassDrum));
                Assert.That(snap[0].steps.Count, Is.EqualTo(2));
                Assert.That(snap[0].steps[0].velocity, Is.EqualTo(110),
                    "kick lane sentinel must resolve to kick.defaultVelocity (110), " +
                    "not the hat lane's defaultVelocity (90).");
                Assert.That(snap[0].steps[1].velocity, Is.EqualTo(110));

                Assert.That(snap[1].instrument, Is.EqualTo(GeneralMidiPercussion.ClosedHiHat));
                Assert.That(snap[1].steps.Count, Is.EqualTo(8));
                foreach (var (_, v) in snap[1].steps)
                    Assert.That(v, Is.EqualTo(120),
                        "hat lane explicit accent must pass through on every hit.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
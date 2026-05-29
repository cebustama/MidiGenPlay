#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using MidiGenPlay.Authoring;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the chord LLM editor wiring (D-L4.7 = A). The IMGUI
    /// panel and async button handlers are not unit-testable in EditMode and are
    /// covered by the manual smoke-test checklist; this suite pins the two pure
    /// seams the wiring depends on:
    /// <list type="bullet">
    ///   <item><description><see cref="ChordLLMFieldPlan.From"/> — the
    ///   outcome→field mapping (the wiring's most error-prone seam).</description></item>
    ///   <item><description><see cref="ChordProgressionEditorWindow.ResolveBeatsPerMeasure"/>
    ///   — time-signature → beats lookup with fallback.</description></item>
    /// </list>
    /// </summary>
    public class ChordProgressionEditorWindowWiringTests
    {
        private static ChordProgressionLLMResponseHandler.Outcome MakeOutcome(
            ChordProgressionLLMResponseHandler.OutcomeKind kind,
            TimeSignature ts = TimeSignature.FourFour,
            int measures = 4,
            float defaultDuration = 1f,
            Tonality tonality = Tonality.Ionian,
            string progression = "ii7 – V7 – Imaj7 – vi7")
        {
            return new ChordProgressionLLMResponseHandler.Outcome(
                kind, ts, measures, defaultDuration, tonality, progression,
                new List<string>(), 0, 0);
        }

        // -----------------------------
        // ChordLLMFieldPlan.From
        // -----------------------------

        [Test]
        public void Plan_Full_SetsAllFields_AndPreviews()
        {
            var plan = ChordLLMFieldPlan.From(MakeOutcome(
                ChordProgressionLLMResponseHandler.OutcomeKind.Full,
                ts: TimeSignature.ThreeFour, defaultDuration: 2f, tonality: Tonality.Dorian,
                progression: "i – iv – v"));

            Assert.IsTrue(plan.ApplyFields);
            Assert.IsTrue(plan.SetSetupFields);
            Assert.AreEqual(TimeSignature.ThreeFour, plan.TimeSignature);
            Assert.AreEqual(Tonality.Dorian, plan.ReferenceTonality);
            Assert.IsTrue(plan.SetDefaultDuration);
            Assert.AreEqual(2f, plan.DefaultDurationMeasures);
            Assert.AreEqual("i – iv – v", plan.Progression);
            Assert.IsTrue(plan.RunPreview);
            Assert.IsFalse(plan.StatusIsError);
        }

        [Test]
        public void Plan_Full_WithZeroDefaultDuration_DoesNotOverwriteDuration()
        {
            // Guards the exact "applied TS but forgot/!mishandled duration" bug class.
            var plan = ChordLLMFieldPlan.From(MakeOutcome(
                ChordProgressionLLMResponseHandler.OutcomeKind.Full,
                defaultDuration: 0f));

            Assert.IsTrue(plan.SetSetupFields, "Setup fields still apply on Full.");
            Assert.IsFalse(plan.SetDefaultDuration,
                "A non-positive default duration must NOT overwrite the window's value.");
        }

        [Test]
        public void Plan_ProgressionOnly_SetsProgression_ButNotSetupFields()
        {
            var plan = ChordLLMFieldPlan.From(MakeOutcome(
                ChordProgressionLLMResponseHandler.OutcomeKind.ProgressionOnly,
                progression: "I – V – vi – IV"));

            Assert.IsTrue(plan.ApplyFields);
            Assert.IsFalse(plan.SetSetupFields,
                "ProgressionOnly must not touch time signature / tonality.");
            Assert.IsFalse(plan.SetDefaultDuration);
            Assert.AreEqual("I – V – vi – IV", plan.Progression);
            Assert.IsTrue(plan.RunPreview);
            Assert.IsFalse(plan.StatusIsError);
        }

        [Test]
        public void Plan_Failed_AppliesNothing_AndFlagsError()
        {
            var plan = ChordLLMFieldPlan.From(MakeOutcome(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed,
                progression: ""));

            Assert.IsFalse(plan.ApplyFields, "Failed must leave all fields unchanged.");
            Assert.IsFalse(plan.SetSetupFields);
            Assert.IsFalse(plan.SetDefaultDuration);
            Assert.IsFalse(plan.RunPreview, "No preview on failure.");
            Assert.IsTrue(plan.StatusIsError);
        }

        [Test]
        public void Plan_Failed_DoesNotCarryProgression()
        {
            // Even if a Failed outcome happens to carry a progression string (e.g.
            // the D-L4.5 guard fired on an otherwise-parseable string), the plan
            // must not apply it.
            var blocked = MakeOutcome(
                ChordProgressionLLMResponseHandler.OutcomeKind.Failed,
                progression: "ii7 – V13 – I");
            var plan = ChordLLMFieldPlan.From(blocked);

            Assert.IsFalse(plan.ApplyFields);
            Assert.AreEqual("", plan.Progression);
        }

        // -----------------------------
        // ResolveBeatsPerMeasure
        // -----------------------------

        [Test]
        public void Beats_FourFour_Is4()
            => Assert.AreEqual(4, ChordProgressionEditorWindow.ResolveBeatsPerMeasure(TimeSignature.FourFour));

        [Test]
        public void Beats_ThreeFour_Is3()
            => Assert.AreEqual(3, ChordProgressionEditorWindow.ResolveBeatsPerMeasure(TimeSignature.ThreeFour));

        [Test]
        public void Beats_AllDefinedTimeSignatures_ReturnPositive()
        {
            foreach (TimeSignature ts in System.Enum.GetValues(typeof(TimeSignature)))
                Assert.Greater(
                    ChordProgressionEditorWindow.ResolveBeatsPerMeasure(ts), 0,
                    $"Beats for {ts} must be positive (fallback covers unmapped values).");
        }
    }
}
#endif
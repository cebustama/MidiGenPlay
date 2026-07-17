#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using MidiGenPlay.Composition;
using ChordQuality = MidiGenPlay.MusicTheory.MusicTheory.ChordQuality;
using ScaleDegree = MidiGenPlay.MusicTheory.MusicTheory.ScaleDegree;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// MGP-ALWTTT-DBG-4 (Ask D) EditMode tests for
    /// <see cref="ChordProgressionRuntimeImporter"/>: the runtime-safe
    /// payload/Roman → <see cref="ChordProgressionData"/> builder.
    ///
    /// Covers the batch DoD: happy-path payload, bare-Roman context path,
    /// degrade-not-fail guard (out-of-alphabet suffix = hard fail, never a
    /// silent downgrade), quantization hard-fail, never-persisted instance,
    /// and field-for-field parity with the editor importer path (which now
    /// delegates here — the test pins the delegation against future drift).
    /// </summary>
    public class ChordProgressionRuntimeImporterTests
    {
        private const string CanonicalPayload =
            "**Setup (Roman mode):**\n\n" +
            "- Time signature: FourFour\n" +
            "- Measures (total): 4\n" +
            "- Default duration (measures): 1\n" +
            "- Reference tonality: Ionian\n\n" +
            "```\n" +
            "ii7 – V7 – Imaj7 – vi7\n" +
            "```\n";

        private static void DestroyBuilt(ChordProgressionData data)
        {
            if (data != null) Object.DestroyImmediate(data);
        }

        // -------------------------------------------------------------------
        // Happy path: full payload
        // -------------------------------------------------------------------

        [Test]
        public void TryParsePayload_CanonicalPayload_BuildsProgression()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParsePayload(
                CanonicalPayload, out var data, out var warnings);

            try
            {
                Assert.IsTrue(ok, "Canonical payload should build. Warnings: " +
                    string.Join(" | ", warnings));
                Assert.IsNotNull(data);

                Assert.AreEqual(TimeSignature.FourFour, data.TimeSignature);
                Assert.AreEqual(4, data.Measures);
                Assert.AreEqual(1, data.subdivisions,
                    "Whole-measure durations in 4/4 need no sub-beat grid.");
                Assert.AreEqual("ii7 – V7 – Imaj7 – vi7", data.originalInput);

                Assert.AreEqual(4, data.events.Count);
                // Explicit suffixes win and are literal in the shared grammar:
                // a bare "7" is Dominant7 regardless of Roman case (so "ii7"
                // is Supertonic + Dominant7; minor-seventh requires "m7").
                Assert.AreEqual(ScaleDegree.Supertonic, data.events[0].degree);
                Assert.AreEqual(ChordQuality.Dominant7, data.events[0].quality);
                Assert.AreEqual(ChordQuality.Dominant7, data.events[1].quality);
                Assert.AreEqual(ChordQuality.Major7, data.events[2].quality);
                Assert.AreEqual(ChordQuality.Dominant7, data.events[3].quality);

                // Contiguous whole-measure grid: 4 beats * sub 1 per measure.
                Assert.AreEqual(0, data.events[0].startStep);
                Assert.AreEqual(4, data.events[0].lengthSteps);
                Assert.AreEqual(4, data.events[1].startStep);
                Assert.AreEqual(12, data.events[3].startStep);

                // Reference tonality becomes the single tonality filter entry.
                Assert.AreEqual(1, data.tonalities.Count);
                Assert.AreEqual(Tonality.Ionian, data.tonalities[0]);
            }
            finally { DestroyBuilt(data); }
        }

        [Test]
        public void TryParsePayload_ProgressionOnly_IsHardFail_WithGuidance()
        {
            // Fenced block but no setup card → editor path degrades to
            // ProgressionOnly; the runtime builder cannot invent TS/measures,
            // so this must be a hard, explained failure.
            const string bare = "```\nI – V – vi – IV\n```\n";

            bool ok = ChordProgressionRuntimeImporter.TryParsePayload(
                bare, out var data, out var warnings);

            Assert.IsFalse(ok);
            Assert.IsNull(data);
            Assert.IsTrue(warnings.Exists(w => w.Contains("TryParseRoman")),
                "The failure should point the caller at the explicit-context API.");
        }

        // -------------------------------------------------------------------
        // Bare Roman with explicit context
        // -------------------------------------------------------------------

        [Test]
        public void TryParseRoman_BareString_InfersDiatonicTriads()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParseRoman(
                "I – V – vi – IV",
                TimeSignature.FourFour,
                measures: 4,
                defaultDurationMeasures: 1f,
                referenceTonality: Tonality.Ionian,
                out var data, out var warnings);

            try
            {
                Assert.IsTrue(ok, string.Join(" | ", warnings));
                Assert.AreEqual(4, data.events.Count);

                // No suffixes → diatonic-triad inference in Ionian:
                // I Major, V Major, vi Minor, IV Major — all diatonic.
                Assert.AreEqual(ChordQuality.Major, data.events[0].quality);
                Assert.AreEqual(ChordQuality.Major, data.events[1].quality);
                Assert.AreEqual(ChordQuality.Minor, data.events[2].quality);
                Assert.AreEqual(ChordQuality.Major, data.events[3].quality);
                Assert.IsTrue(data.events.TrueForAll(e => e.isDiatonic));

                Assert.AreEqual(ScaleDegree.Submediant, data.events[2].degree);
                Assert.AreEqual(96, data.events[0].velocity, "Editor default velocity.");
            }
            finally { DestroyBuilt(data); }
        }

        [Test]
        public void TryParseRoman_RestsAdvanceTimeWithoutEvents()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParseRoman(
                "I (1) – S (1) – V (2)",
                TimeSignature.FourFour, 4, 1f, Tonality.Ionian,
                out var data, out var warnings);

            try
            {
                Assert.IsTrue(ok, string.Join(" | ", warnings));
                Assert.AreEqual(2, data.events.Count, "The rest creates no event.");
                Assert.AreEqual(0, data.events[0].startStep);
                Assert.AreEqual(8, data.events[1].startStep,
                    "The rest span (steps 4..7) is skipped, not collapsed.");
                Assert.AreEqual(8, data.events[1].lengthSteps);
            }
            finally { DestroyBuilt(data); }
        }

        [Test]
        public void TryParseRoman_MeasuresMismatch_WarnsAndDerivedWins()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParseRoman(
                "I – V – vi – IV", // durations imply 4 measures
                TimeSignature.FourFour,
                measures: 8,       // declared, wrong
                defaultDurationMeasures: 1f,
                referenceTonality: Tonality.Ionian,
                out var data, out var warnings);

            try
            {
                Assert.IsTrue(ok);
                Assert.AreEqual(4, data.Measures, "Durations define the grid.");
                Assert.IsTrue(warnings.Exists(w => w.Contains("differ")),
                    "Mismatch must be surfaced, not silent.");
            }
            finally { DestroyBuilt(data); }
        }

        // -------------------------------------------------------------------
        // D-L4.5 degrade-not-fail guard
        // -------------------------------------------------------------------

        [Test]
        public void TryParseRoman_OutOfAlphabetSuffix_HardFails_NoSilentDowngrade()
        {
            // V13 is outside the v1/v2 alphabet: the shared parser would only
            // warn and downgrade it to diatonic quality. The runtime API must
            // block it, exactly like the editor response handler's guard.
            bool ok = ChordProgressionRuntimeImporter.TryParseRoman(
                "I – V13 – IV",
                TimeSignature.FourFour, 3, 1f, Tonality.Ionian,
                out var data, out var warnings);

            Assert.IsFalse(ok);
            Assert.IsNull(data, "Nothing may be applied on a blocked token.");
            Assert.IsTrue(warnings.Exists(w => w.Contains("V13")),
                "The offending token must be named in the warning.");
        }

        [Test]
        public void TryParsePayload_OutOfAlphabetSuffix_HardFails()
        {
            string payload = CanonicalPayload.Replace("Imaj7", "Iadd11");

            bool ok = ChordProgressionRuntimeImporter.TryParsePayload(
                payload, out var data, out var warnings);

            Assert.IsFalse(ok);
            Assert.IsNull(data);
            Assert.IsTrue(warnings.Exists(w => w.Contains("Iadd11")));
        }

        // -------------------------------------------------------------------
        // Quantization hard-fail
        // -------------------------------------------------------------------

        [Test]
        public void TryParseRoman_UnquantizableDurations_HardFailWithError()
        {
            // 0.37 measures maps to no integer step count for any
            // subdivisions in 1..8 in 4/4 — same hard failure the editor
            // surfaces as its Quantization Error dialog.
            bool ok = ChordProgressionRuntimeImporter.TryParseRoman(
                "I (0.37) – V (0.63)",
                TimeSignature.FourFour, 1, 1f, Tonality.Ionian,
                out var data, out var warnings);

            Assert.IsFalse(ok);
            Assert.IsNull(data);
            Assert.IsTrue(warnings.Exists(w => w.Contains("Quantization")));
        }

        // -------------------------------------------------------------------
        // Never persisted
        // -------------------------------------------------------------------

        [Test]
        public void TryParseRoman_BuiltInstance_IsNeverPersistable()
        {
            ChordProgressionRuntimeImporter.TryParseRoman(
                "I – IV", TimeSignature.FourFour, 2, 1f, Tonality.Ionian,
                out var data, out _);

            try
            {
                Assert.AreEqual(HideFlags.DontSave, data.hideFlags,
                    "The authoring no-silent-writes invariant is enforced in code.");
                Assert.IsFalse(UnityEditor.AssetDatabase.Contains(data),
                    "The instance must not exist in the asset database.");
                Assert.IsTrue(data.name.StartsWith("Runtime: "),
                    "Readback identity (Ask A, by-name) must be stamped.");
            }
            finally { DestroyBuilt(data); }
        }

        // -------------------------------------------------------------------
        // Editor-path parity (delegation pin)
        // -------------------------------------------------------------------

        [Test]
        public void EditorImporter_And_RuntimeImporter_ParsePayloadInParity()
        {
            var editor = ChordProgressionEditorImporter.Parse(CanonicalPayload);
            var runtime = ChordProgressionRuntimeImporter.ParsePayload(CanonicalPayload);

            Assert.AreEqual((int)editor.mode, (int)runtime.mode);
            Assert.AreEqual(editor.timeSignature, runtime.timeSignature);
            Assert.AreEqual(editor.measures, runtime.measures);
            Assert.AreEqual(editor.defaultDurationMeasures, runtime.defaultDurationMeasures);
            Assert.AreEqual(editor.referenceTonality, runtime.referenceTonality);
            Assert.AreEqual(editor.progression, runtime.progression);
            Assert.AreEqual(editor.warnings.Count, runtime.warnings.Count);
        }

        [Test]
        public void GuardParity_HandlerDelegatesToRuntimeScan()
        {
            // The handler's internal guard is a forwarder; both surfaces must
            // flag the same token.
            bool runtimeHit = ChordProgressionRuntimeImporter.TryFindForbiddenToken(
                "I – V13 – IV", out string runtimeToken);
            bool handlerHit = ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                "I – V13 – IV", out string handlerToken);

            Assert.IsTrue(runtimeHit);
            Assert.IsTrue(handlerHit);
            Assert.AreEqual(runtimeToken, handlerToken);
        }
    }
}
#endif
#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using MidiGenPlay.Composition;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// CPE-META-2 (D3=A, D-M2-3=A) EditMode tests for the optional-metadata
    /// extension of the payload grammar: presence-gated parsing of
    /// "Quality render policy" / "Use color table" / "Cadence" /
    /// "Allowed tonalities", backward compatibility of metadata-free payloads,
    /// present-but-invalid handling (warn + ignore, mode never degraded), the
    /// editor-mirror pass-through, the Outcome pass-through, and metadata
    /// stamping on the runtime-built instance.
    /// </summary>
    public class ChordProgressionImport_MetadataTests
    {
        // Pre-CPE-META-2 canonical payload — must keep parsing IDENTICALLY.
        private const string LegacyPayload =
            "**Setup (Roman mode):**\n\n" +
            "- Time signature: FourFour\n" +
            "- Measures (total): 4\n" +
            "- Default duration (measures): 1\n" +
            "- Reference tonality: Ionian\n\n" +
            "```\n" +
            "ii7 – V7 – Imaj7 – vi7\n" +
            "```\n";

        private const string MetadataPayload =
            "**Setup (Roman mode):**\n\n" +
            "- Time signature: FourFour\n" +
            "- Measures (total): 4\n" +
            "- Default duration (measures): 1\n" +
            "- Reference tonality: Aeolian\n" +
            "- Quality render policy: DiatonicToPartFunctional\n" +
            "- Use color table: true\n" +
            "- Cadence: Authentic\n" +
            "- Allowed tonalities: Aeolian, Dorian\n\n" +
            "```\n" +
            "i – iv – V7 – i\n" +
            "```\n";

        // -------------------------------------------------------------------
        // Backward compatibility
        // -------------------------------------------------------------------

        [Test]
        public void ParsePayload_LegacyPayload_DeclaresNoMetadata()
        {
            var r = ChordProgressionRuntimeImporter.ParsePayload(LegacyPayload);

            Assert.AreEqual(
                ChordProgressionRuntimeImporter.ImportMode.Full, r.mode);
            Assert.IsFalse(r.hasQualityRenderPolicy);
            Assert.IsFalse(r.hasUseColorTable);
            Assert.IsFalse(r.hasCadence);
            Assert.IsFalse(r.hasAllowedTonalities);
            // No metadata warnings either — absence is silent.
            foreach (var w in r.warnings)
                Assert.AreNotEqual(
                    ChordProgressionRuntimeImporter.ImportWarningKind.InvalidMetadataField,
                    w.kind);
        }

        // -------------------------------------------------------------------
        // Declared metadata parses (runtime + editor mirror)
        // -------------------------------------------------------------------

        [Test]
        public void ParsePayload_DeclaredMetadata_AllFieldsParsed()
        {
            var r = ChordProgressionRuntimeImporter.ParsePayload(MetadataPayload);

            Assert.AreEqual(
                ChordProgressionRuntimeImporter.ImportMode.Full, r.mode);

            Assert.IsTrue(r.hasQualityRenderPolicy);
            Assert.AreEqual(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                r.qualityRenderPolicy);

            Assert.IsTrue(r.hasUseColorTable);
            Assert.IsTrue(r.useColorTable);

            Assert.IsTrue(r.hasCadence);
            Assert.AreEqual(
                ChordProgressionData.CadenceType.Authentic, r.cadence);

            Assert.IsTrue(r.hasAllowedTonalities);
            CollectionAssert.AreEquivalent(
                new List<Tonality> { Tonality.Aeolian, Tonality.Dorian },
                new List<Tonality>(r.allowedTonalities));
        }

        [Test]
        public void ParsePayload_DeclaredMetadata_IsCrlfSafe()
        {
            string crlf = MetadataPayload.Replace("\n", "\r\n");
            var r = ChordProgressionRuntimeImporter.ParsePayload(crlf);

            Assert.AreEqual(
                ChordProgressionRuntimeImporter.ImportMode.Full, r.mode);
            Assert.IsTrue(r.hasQualityRenderPolicy);
            Assert.IsTrue(r.hasCadence);
            Assert.IsTrue(r.hasAllowedTonalities);
        }

        [Test]
        public void EditorForwarder_MirrorsMetadataFields()
        {
            var r = ChordProgressionEditorImporter.Parse(MetadataPayload);

            Assert.AreEqual(
                ChordProgressionEditorImporter.ImportMode.Full, r.mode);
            Assert.IsTrue(r.hasQualityRenderPolicy);
            Assert.AreEqual(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                r.qualityRenderPolicy);
            Assert.IsTrue(r.hasUseColorTable && r.useColorTable);
            Assert.AreEqual(ChordProgressionData.CadenceType.Authentic, r.cadence);
            Assert.IsTrue(r.hasAllowedTonalities);
        }

        // -------------------------------------------------------------------
        // Present-but-invalid: warn + ignore, never degrade
        // -------------------------------------------------------------------

        [Test]
        public void ParsePayload_InvalidMetadataValue_WarnsAndIgnores_StaysFull()
        {
            string payload = LegacyPayload.Replace(
                "- Reference tonality: Ionian\n",
                "- Reference tonality: Ionian\n" +
                "- Cadence: SuperCadence\n" +
                "- Allowed tonalities: Ionian, NotAMode\n");

            var r = ChordProgressionRuntimeImporter.ParsePayload(payload);

            // Mode is NOT degraded — metadata are not load-bearing.
            Assert.AreEqual(
                ChordProgressionRuntimeImporter.ImportMode.Full, r.mode);
            Assert.IsFalse(r.hasCadence);
            Assert.IsFalse(r.hasAllowedTonalities);

            int metaWarnings = 0;
            foreach (var w in r.warnings)
                if (w.kind == ChordProgressionRuntimeImporter
                        .ImportWarningKind.InvalidMetadataField)
                    metaWarnings++;
            Assert.AreEqual(2, metaWarnings,
                "One InvalidMetadataField warning per bad field.");
        }

        // -------------------------------------------------------------------
        // D-M2-3=A — runtime-built instance is stamped
        // -------------------------------------------------------------------

        [Test]
        public void TryParsePayload_StampsMetadataOnRuntimeInstance()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParsePayload(
                MetadataPayload, out ChordProgressionData data, out _);

            Assert.IsTrue(ok);
            Assert.IsNotNull(data);
            Assert.AreEqual(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                data.qualityRenderPolicy);
            Assert.IsTrue(data.useColorTable);
            Assert.AreEqual(
                ChordProgressionData.CadenceType.Authentic, data.cadence);
            // Declared list replaces the TONFILTER-1 single-entry provenance.
            CollectionAssert.AreEquivalent(
                new List<Tonality> { Tonality.Aeolian, Tonality.Dorian },
                data.tonalities);
        }

        [Test]
        public void TryParsePayload_LegacyPayload_InstanceKeepsDefaults()
        {
            bool ok = ChordProgressionRuntimeImporter.TryParsePayload(
                LegacyPayload, out ChordProgressionData data, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(
                ChordProgressionData.QualityRenderPolicy.AsAuthored,
                data.qualityRenderPolicy);
            Assert.IsFalse(data.useColorTable);
            Assert.AreEqual(ChordProgressionData.CadenceType.None, data.cadence);
            // TONFILTER-1 provenance default: exactly the reference tonality.
            CollectionAssert.AreEquivalent(
                new List<Tonality> { Tonality.Ionian }, data.tonalities);
        }

        // -------------------------------------------------------------------
        // Outcome + FieldPlan pass-through (the window staging seam)
        // -------------------------------------------------------------------

        [Test]
        public void FromPayload_Full_CarriesMetadata_AndPlanStagesIt()
        {
            var outcome =
                ChordProgressionLLMResponseHandler.FromPayload(MetadataPayload);

            Assert.AreEqual(
                ChordProgressionLLMResponseHandler.OutcomeKind.Full, outcome.kind);
            Assert.IsTrue(outcome.hasQualityRenderPolicy);
            Assert.IsTrue(outcome.hasUseColorTable);
            Assert.IsTrue(outcome.hasCadence);
            Assert.IsTrue(outcome.hasAllowedTonalities);

            var plan = ChordLLMFieldPlan.From(outcome);
            Assert.IsTrue(plan.ApplyFields);
            Assert.IsTrue(plan.SetQualityRenderPolicy);
            Assert.AreEqual(
                ChordProgressionData.QualityRenderPolicy.DiatonicToPartFunctional,
                plan.QualityRenderPolicy);
            Assert.IsTrue(plan.SetUseColorTable && plan.UseColorTable);
            Assert.IsTrue(plan.SetCadence);
            Assert.AreEqual(
                ChordProgressionData.CadenceType.Authentic, plan.Cadence);
            Assert.IsTrue(plan.SetAllowedTonalities);
        }

        [Test]
        public void FromPayload_Legacy_PlanStagesNothing()
        {
            var outcome =
                ChordProgressionLLMResponseHandler.FromPayload(LegacyPayload);
            var plan = ChordLLMFieldPlan.From(outcome);

            Assert.IsTrue(plan.ApplyFields);
            Assert.IsFalse(plan.SetQualityRenderPolicy);
            Assert.IsFalse(plan.SetUseColorTable);
            Assert.IsFalse(plan.SetCadence);
            Assert.IsFalse(plan.SetAllowedTonalities);
        }
    }
}
#endif
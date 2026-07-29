#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MidiGenPlay.Composition;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// MGP-ALWTTT-DBG-4 (E-5=A): compatibility forwarder. The setup-card +
    /// fenced-Roman grammar this type used to implement was RELOCATED, verbatim,
    /// to the runtime assembly as
    /// <see cref="ChordProgressionRuntimeImporter"/> (it was pure regex — no
    /// editor API — so it belongs where both the editor and the Ask D runtime
    /// path can share it). This symbol is preserved so existing callers
    /// (<c>ChordProgressionLLMResponseHandler</c>, the editor window, tests)
    /// keep compiling; every call delegates, so there is exactly ONE grammar
    /// and it cannot drift.
    /// </summary>
    /// <remarks>
    /// The nested types mirror the runtime shapes 1:1 (same member order) so
    /// conversion is a mechanical enum cast + field copy. Do not add logic
    /// here — new grammar behavior goes in the runtime type.
    /// </remarks>
    public static class ChordProgressionEditorImporter
    {
        // -------------------------------------------------------------------
        // Mirrored result + warning types (editor-facing compat surface)
        // -------------------------------------------------------------------

        /// <summary>Mirror of <see cref="ChordProgressionRuntimeImporter.ImportMode"/>.</summary>
        public enum ImportMode
        {
            Failed = (int)ChordProgressionRuntimeImporter.ImportMode.Failed,
            ProgressionOnly = (int)ChordProgressionRuntimeImporter.ImportMode.ProgressionOnly,
            Full = (int)ChordProgressionRuntimeImporter.ImportMode.Full,
        }

        /// <summary>Mirror of <see cref="ChordProgressionRuntimeImporter.ImportWarningKind"/>.</summary>
        public enum ImportWarningKind
        {
            MissingProgressionBlock =
                (int)ChordProgressionRuntimeImporter.ImportWarningKind.MissingProgressionBlock,
            MissingOrGarbledSetupCard =
                (int)ChordProgressionRuntimeImporter.ImportWarningKind.MissingOrGarbledSetupCard,
            MissingSetupField =
                (int)ChordProgressionRuntimeImporter.ImportWarningKind.MissingSetupField,
            // CPE-META-2 (D3=A): optional metadata field present but invalid
            // (ignored, mode never degraded). Mirror; append-only.
            InvalidMetadataField =
                (int)ChordProgressionRuntimeImporter.ImportWarningKind.InvalidMetadataField,
        }

        /// <summary>One importer-side warning (mirror).</summary>
        public readonly struct ImportWarning
        {
            public readonly ImportWarningKind kind;
            public readonly string detail;

            public ImportWarning(ImportWarningKind kind, string detail)
            {
                this.kind = kind;
                this.detail = detail;
            }

            public override string ToString() => $"[{kind}] {detail}";
        }

        /// <summary>Outcome of an import (mirror of the runtime PayloadResult).</summary>
        public readonly struct Result
        {
            public readonly ImportMode mode;

            public readonly TimeSignature timeSignature;
            public readonly int measures;
            public readonly float defaultDurationMeasures;
            public readonly Tonality referenceTonality;

            public readonly string progression;

            // -- CPE-META-2 (D3=A): mirrored optional metadata (mode == Full
            //    only). Presence flags gate application; absent != default.
            public readonly bool hasQualityRenderPolicy;
            public readonly ChordProgressionData.QualityRenderPolicy qualityRenderPolicy;
            public readonly bool hasUseColorTable;
            public readonly bool useColorTable;
            public readonly bool hasCadence;
            public readonly ChordProgressionData.CadenceType cadence;
            public readonly bool hasAllowedTonalities;
            public readonly IReadOnlyList<Tonality> allowedTonalities;

            public readonly IReadOnlyList<ImportWarning> warnings;

            /// <summary>Pre-CPE-META-2 ctor (compat): no metadata declared.</summary>
            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                float defaultDurationMeasures,
                Tonality referenceTonality,
                string progression,
                IReadOnlyList<ImportWarning> warnings)
                : this(mode, timeSignature, measures, defaultDurationMeasures,
                       referenceTonality, progression, warnings,
                       false, default, false, false, false, default, false, null)
            {
            }

            public Result(
                ImportMode mode,
                TimeSignature timeSignature,
                int measures,
                float defaultDurationMeasures,
                Tonality referenceTonality,
                string progression,
                IReadOnlyList<ImportWarning> warnings,
                bool hasQualityRenderPolicy,
                ChordProgressionData.QualityRenderPolicy qualityRenderPolicy,
                bool hasUseColorTable,
                bool useColorTable,
                bool hasCadence,
                ChordProgressionData.CadenceType cadence,
                bool hasAllowedTonalities,
                IReadOnlyList<Tonality> allowedTonalities)
            {
                this.mode = mode;
                this.timeSignature = timeSignature;
                this.measures = measures;
                this.defaultDurationMeasures = defaultDurationMeasures;
                this.referenceTonality = referenceTonality;
                this.progression = progression ?? string.Empty;
                this.warnings = warnings ?? Array.Empty<ImportWarning>();
                this.hasQualityRenderPolicy = hasQualityRenderPolicy;
                this.qualityRenderPolicy = qualityRenderPolicy;
                this.hasUseColorTable = hasUseColorTable;
                this.useColorTable = useColorTable;
                this.hasCadence = hasCadence;
                this.cadence = cadence;
                this.hasAllowedTonalities = hasAllowedTonalities;
                this.allowedTonalities = allowedTonalities ?? Array.Empty<Tonality>();
            }
        }

        // -------------------------------------------------------------------
        // Public API — pure delegation
        // -------------------------------------------------------------------

        /// <summary>
        /// Parse a full "setup card + progression block" payload. Delegates to
        /// <see cref="ChordProgressionRuntimeImporter.ParsePayload"/> — the one
        /// grammar (D-DBG4=A).
        /// </summary>
        public static Result Parse(string payload)
        {
            var r = ChordProgressionRuntimeImporter.ParsePayload(payload);

            var warnings = new List<ImportWarning>(r.warnings.Count);
            for (int i = 0; i < r.warnings.Count; i++)
            {
                var w = r.warnings[i];
                warnings.Add(new ImportWarning((ImportWarningKind)w.kind, w.detail));
            }

            return new Result(
                (ImportMode)r.mode,
                r.timeSignature,
                r.measures,
                r.defaultDurationMeasures,
                r.referenceTonality,
                // Preserve legacy nuance: on Failed the old importer returned
                // null progression (struct ctor normalizes to string.Empty).
                r.mode == ChordProgressionRuntimeImporter.ImportMode.Failed
                    ? null : r.progression,
                warnings,
                // CPE-META-2: mechanical mirror of the metadata fields.
                r.hasQualityRenderPolicy, r.qualityRenderPolicy,
                r.hasUseColorTable, r.useColorTable,
                r.hasCadence, r.cadence,
                r.hasAllowedTonalities, r.allowedTonalities);
        }

        /// <summary>
        /// Compat re-export for tests/tools that used the internal extraction
        /// helper. Delegates to the relocated implementation.
        /// </summary>
        internal static string ExtractProgression(string payload)
            => ChordProgressionRuntimeImporter.ExtractProgression(payload);
    }
}
#endif
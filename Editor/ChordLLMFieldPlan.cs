#if UNITY_EDITOR
using System.Collections.Generic;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using Tonality = MidiGenPlay.MusicTheory.MusicTheory.Tonality;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// Pure translation of a <see cref="ChordProgressionLLMResponseHandler.Outcome"/>
    /// into the concrete field changes the editor window should apply, plus
    /// whether a preview should run and what status to show. Extracted from
    /// <c>ChordProgressionEditorWindow.ApplyOutcome</c> (D-L4.7 = A) so the
    /// outcome→field mapping — the wiring's most error-prone seam — is unit
    /// testable without IMGUI, async, or a live window.
    /// </summary>
    /// <remarks>
    /// The window stays a thin applier: it reads the flags here and assigns its
    /// own serialized fields, then (if <see cref="RunPreview"/>) calls its
    /// existing <c>ParseAndPreview(onlyPreview: true)</c>. No write logic lives
    /// here; this only decides <i>what</i> the window should set.
    /// </remarks>
    public readonly struct ChordLLMFieldPlan
    {
        /// <summary>True when the outcome carried usable content (not Failed).</summary>
        public readonly bool ApplyFields;

        /// <summary>True when the window should set time signature / tonality from the outcome (Full only).</summary>
        public readonly bool SetSetupFields;

        public readonly TimeSignature TimeSignature;
        public readonly Tonality ReferenceTonality;

        /// <summary>True when a positive default-duration value should be written.</summary>
        public readonly bool SetDefaultDuration;
        public readonly float DefaultDurationMeasures;

        /// <summary>The Roman string to write to progressionInput (empty if !ApplyFields).</summary>
        public readonly string Progression;

        // -- CPE-META-2 (D3=A, D-M2-1=A): metadata the window should STAGE as
        //    pending (policy / color table / cadence — the D2=C direct-bound
        //    trio, consumed one-shot by the next Apply/Save) or apply to its
        //    tonality toggles (allowed tonalities — mirror state, rides the
        //    existing apply route). Presence flags gate everything.
        public readonly bool SetQualityRenderPolicy;
        public readonly ChordProgressionData.QualityRenderPolicy QualityRenderPolicy;
        public readonly bool SetUseColorTable;
        public readonly bool UseColorTable;
        public readonly bool SetCadence;
        public readonly ChordProgressionData.CadenceType Cadence;
        public readonly bool SetAllowedTonalities;
        public readonly IReadOnlyList<Tonality> AllowedTonalities;

        /// <summary>True when the window should run its preview after applying fields.</summary>
        public readonly bool RunPreview;

        /// <summary>Status line to display.</summary>
        public readonly string StatusMessage;

        /// <summary>True when the status should render as an error/warning.</summary>
        public readonly bool StatusIsError;

        private ChordLLMFieldPlan(
            bool applyFields, bool setSetupFields,
            TimeSignature ts, Tonality tonality,
            bool setDefaultDuration, float defaultDuration,
            string progression, bool runPreview,
            string statusMessage, bool statusIsError,
            bool setQualityRenderPolicy = false,
            ChordProgressionData.QualityRenderPolicy qualityRenderPolicy = default,
            bool setUseColorTable = false,
            bool useColorTable = false,
            bool setCadence = false,
            ChordProgressionData.CadenceType cadence = default,
            bool setAllowedTonalities = false,
            IReadOnlyList<Tonality> allowedTonalities = null)
        {
            ApplyFields = applyFields;
            SetSetupFields = setSetupFields;
            TimeSignature = ts;
            ReferenceTonality = tonality;
            SetDefaultDuration = setDefaultDuration;
            DefaultDurationMeasures = defaultDuration;
            Progression = progression ?? string.Empty;
            RunPreview = runPreview;
            StatusMessage = statusMessage ?? string.Empty;
            StatusIsError = statusIsError;
            SetQualityRenderPolicy = setQualityRenderPolicy;
            QualityRenderPolicy = qualityRenderPolicy;
            SetUseColorTable = setUseColorTable;
            UseColorTable = useColorTable;
            SetCadence = setCadence;
            Cadence = cadence;
            SetAllowedTonalities = setAllowedTonalities;
            AllowedTonalities = allowedTonalities ?? System.Array.Empty<Tonality>();
        }

        /// <summary>
        /// Decide the field plan for a given outcome. Pure; no side effects.
        /// </summary>
        public static ChordLLMFieldPlan From(ChordProgressionLLMResponseHandler.Outcome outcome)
        {
            switch (outcome.kind)
            {
                case ChordProgressionLLMResponseHandler.OutcomeKind.Full:
                    return new ChordLLMFieldPlan(
                        applyFields: true,
                        setSetupFields: true,
                        ts: outcome.timeSignature,
                        tonality: outcome.referenceTonality,
                        setDefaultDuration: outcome.defaultDurationMeasures > 0f,
                        defaultDuration: outcome.defaultDurationMeasures,
                        progression: outcome.progression,
                        runPreview: true,
                        statusMessage: HasMetadata(outcome)
                            ? "Generated and previewed. Metadata staged — written by the next " +
                              "Apply/Save (see the Asset Metadata section)."
                            : "Generated and previewed. Press \"Apply To Target Asset\" to write.",
                        statusIsError: false,
                        setQualityRenderPolicy: outcome.hasQualityRenderPolicy,
                        qualityRenderPolicy: outcome.qualityRenderPolicy,
                        setUseColorTable: outcome.hasUseColorTable,
                        useColorTable: outcome.useColorTable,
                        setCadence: outcome.hasCadence,
                        cadence: outcome.cadence,
                        setAllowedTonalities: outcome.hasAllowedTonalities,
                        allowedTonalities: outcome.allowedTonalities);

                case ChordProgressionLLMResponseHandler.OutcomeKind.ProgressionOnly:
                    return new ChordLLMFieldPlan(
                        applyFields: true,
                        setSetupFields: false,
                        ts: default,
                        tonality: default,
                        setDefaultDuration: false,
                        defaultDuration: 0f,
                        progression: outcome.progression,
                        runPreview: true,
                        statusMessage: "Imported progression only — check time signature / measures, then preview.",
                        statusIsError: false);

                default: // Failed (includes the D-L4.5 token block)
                    return new ChordLLMFieldPlan(
                        applyFields: false,
                        setSetupFields: false,
                        ts: default,
                        tonality: default,
                        setDefaultDuration: false,
                        defaultDuration: 0f,
                        progression: string.Empty,
                        runPreview: false,
                        statusMessage: "Generation/import failed; see warnings. Fields unchanged.",
                        statusIsError: true);
            }
        }

        /// <summary>True when the outcome carries any declared metadata field.</summary>
        private static bool HasMetadata(ChordProgressionLLMResponseHandler.Outcome o)
            => o.hasQualityRenderPolicy || o.hasUseColorTable ||
               o.hasCadence || o.hasAllowedTonalities;
    }
}
#endif
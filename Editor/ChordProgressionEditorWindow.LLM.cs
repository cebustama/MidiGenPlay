#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using BCS.LLM.Core.Clients;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// <summary>
/// LLM-assisted generation panel for <see cref="ChordProgressionEditorWindow"/>
/// (Batch L4). Partial-class extension so the wiring lives apart from the
/// hand-authoring window body. Mirrors the drum surface:
/// <list type="bullet">
///   <item><description>default + override client (an <c>LLMClientData</c> asset; default loaded if the field is null);</description></item>
///   <item><description>genre / sub-style / free-text inputs + pre-network cost cap;</description></item>
///   <item><description>Generate / Regenerate / Import-from-clipboard, all async and non-blocking;</description></item>
///   <item><description>the outcome is applied by writing the window's existing fields
///   (<c>progressionInput</c>, <c>timeSignature</c>, <c>defaultDurationMeasures</c>,
///   <c>referenceTonality</c>) and calling the existing
///   <c>ParseAndPreview(onlyPreview: true)</c> — i.e. the SAME path a human edit
///   takes. No new write/apply logic; no silent asset writes (the user still
///   presses "Apply To Target Asset").</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>Async discipline (load-bearing).</b> Button handlers are
/// <c>async void</c> and <c>await</c> the response handler; we never call
/// <c>.Result</c> / <c>.Wait()</c> / <c>.GetAwaiter().GetResult()</c>, which
/// would deadlock the editor main thread. The continuation resumes on the main
/// thread, where field writes + Repaint are valid.</para>
///
/// <para><b>D-L4.5.</b> The handler blocks an out-of-alphabet chord token rather
/// than letting the parser silently downgrade it; such a result arrives as
/// <c>OutcomeKind.Failed</c> and we surface its warnings without touching fields.</para>
/// </remarks>
public partial class ChordProgressionEditorWindow
{
    // -- LLM panel state --
    // IMPORT-QOL-1 item 4: collapsed by default; state still persists across
    // domain reloads via window serialization.
    [SerializeField] private bool llmFoldout = false;
    [SerializeField] private ChordGenreVocabularySO llmVocabulary;
    [SerializeField] private LLMClientData llmClientOverride; // null → default client
    [SerializeField] private string llmGenreName = "";
    [SerializeField] private string llmSubStyleCue = "";
    [SerializeField][TextArea(2, 4)] private string llmFreeText = "";
    [SerializeField] private int llmMaxCharBudget = 8000; // 0 = no cap
    [SerializeField] private int llmTargetMeasures = 4;

    // Transient (not serialized): in-flight + last-run reporting.
    [NonSerialized] private bool llmGenerating;
    [NonSerialized] private string llmStatus = "";
    [NonSerialized] private string llmLastWarnings = "";
    [NonSerialized] private int llmLastInputTokens;
    [NonSerialized] private int llmLastOutputTokens;

    /// <summary>
    /// Resource path for the default vocabulary asset, mirroring the drum
    /// surface's "Default ... Genres" Resources convention.
    /// </summary>
    private const string DefaultChordVocabResource = "Default Chord Genres";

    /// <summary>
    /// Draw the LLM panel. Call this from the window's OnGUI, after the action
    /// buttons block and before EndScrollView, e.g.:
    /// <code>
    ///     EditorGUILayout.Space();
    ///     DrawLLMPanel();
    /// </code>
    /// Only meaningful in RomanString mode (the LLM produces a Roman string); in
    /// Grid mode the panel still applies by writing progressionInput, which the
    /// existing Grid/Roman round-trip already treats as the source of truth.
    /// </summary>
    private void DrawLLMPanel()
    {
        llmFoldout = EditorGUILayout.Foldout(llmFoldout, "LLM Generation (beta)", true);
        if (!llmFoldout) return;

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.HelpBox(
                "Generates a Roman-numeral progression and fills the fields above, " +
                "then previews it. Nothing is written to the asset until you press " +
                "\"Apply To Target Asset\".",
                MessageType.Info);

            llmVocabulary = (ChordGenreVocabularySO)EditorGUILayout.ObjectField(
                new GUIContent("Vocabulary", "ChordGenreVocabularySO. If empty, the default is loaded from Resources."),
                llmVocabulary, typeof(ChordGenreVocabularySO), false);

            llmClientOverride = (LLMClientData)EditorGUILayout.ObjectField(
                new GUIContent("Client (override)", "LLMClientData asset. If empty, the project default client is used."),
                llmClientOverride, typeof(LLMClientData), false);

            llmGenreName = EditorGUILayout.TextField(
                new GUIContent("Genre", "Must match a genre in the vocabulary (e.g. \"jazz\")."),
                llmGenreName);
            llmSubStyleCue = EditorGUILayout.TextField(
                new GUIContent("Sub-style (optional)", "A sub-style cue under the genre (e.g. \"modal jazz\")."),
                llmSubStyleCue);
            llmTargetMeasures = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Target measures", "Total length; chord durations must sum to this."),
                llmTargetMeasures));
            llmFreeText = EditorGUILayout.TextField(
                new GUIContent("Direction (optional)", "Free-text mood / target chords."),
                llmFreeText);
            llmMaxCharBudget = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Max prompt chars", "Pre-network cost cap. 0 = no cap."),
                llmMaxCharBudget));

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(llmGenerating))
                {
                    if (GUILayout.Button("Generate"))
                        GenerateChordsAsync(); // async void; fire-and-forget by design

                    if (GUILayout.Button("Regenerate"))
                        GenerateChordsAsync();

                    if (GUILayout.Button("Import from clipboard"))
                        ImportFromClipboard();
                }
            }

            if (llmGenerating)
                EditorGUILayout.LabelField("Working… (editor stays responsive)");

            if (!string.IsNullOrEmpty(llmStatus))
                EditorGUILayout.LabelField(llmStatus);

            if (llmLastInputTokens > 0 || llmLastOutputTokens > 0)
                EditorGUILayout.LabelField(
                    $"Tokens — in: {llmLastInputTokens}, out: {llmLastOutputTokens}");

            if (!string.IsNullOrEmpty(llmLastWarnings))
                EditorGUILayout.HelpBox(llmLastWarnings, MessageType.Warning);
        }
    }

    // -------------------------------------------------------------------
    // Generate (async, non-blocking)
    // -------------------------------------------------------------------

    private async void GenerateChordsAsync()
    {
        if (llmGenerating) return;

        var vocab = ResolveVocabulary();
        if (vocab == null)
        {
            SetStatus("No vocabulary: assign a ChordGenreVocabularySO or add a default to Resources.", isError: true);
            return;
        }

        ILLMClient client = ResolveClient(out string clientError);
        if (client == null)
        {
            SetStatus(clientError, isError: true);
            return;
        }

        int beatsPerMeasure = ResolveBeatsPerMeasure(timeSignature);

        var input = new ChordProgressionLLMPromptBuilder.Input(
            genreName: llmGenreName,
            subStyleCueName: string.IsNullOrWhiteSpace(llmSubStyleCue) ? null : llmSubStyleCue,
            timeSignature: timeSignature,
            beatsPerMeasure: beatsPerMeasure,
            measures: llmTargetMeasures,
            defaultDurationMeasures: defaultDurationMeasures,
            userFreeText: string.IsNullOrWhiteSpace(llmFreeText) ? null : llmFreeText,
            maxCharBudget: llmMaxCharBudget);

        llmGenerating = true;
        SetStatus("Generating…");
        Repaint();

        ChordProgressionLLMResponseHandler.Outcome outcome;
        try
        {
            // inferTriadFromCaseWhenNoSuffix mirrors the window's autoDiatonicMode:
            // anything other than None implies case/diatonic inference is welcome.
            bool inferFromCase = autoDiatonicMode != AutoDiatonicMode.None;
            outcome = await ChordProgressionLLMResponseHandler.GenerateAsync(
                client, vocab, input, inferTriadFromCaseWhenNoSuffix: inferFromCase);
        }
        catch (Exception ex)
        {
            llmGenerating = false;
            SetStatus($"Generation threw: {ex.GetType().Name}: {ex.Message}", isError: true);
            Repaint();
            return;
        }

        llmGenerating = false;
        ApplyOutcome(outcome);
        Repaint();
    }

    private void ImportFromClipboard()
    {
        string payload = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(payload))
        {
            SetStatus("Clipboard is empty.", isError: true);
            return;
        }

        var outcome = ChordProgressionLLMResponseHandler.FromPayload(payload);
        ApplyOutcome(outcome);
        Repaint();
    }

    // -------------------------------------------------------------------
    // Apply outcome via the EXISTING field → ParseAndPreview path
    // -------------------------------------------------------------------

    private void ApplyOutcome(ChordProgressionLLMResponseHandler.Outcome outcome)
    {
        llmLastInputTokens = outcome.inputTokens;
        llmLastOutputTokens = outcome.outputTokens;
        llmLastWarnings = outcome.displayWarnings != null && outcome.displayWarnings.Count > 0
            ? string.Join("\n", outcome.displayWarnings)
            : "";

        // Pure decision (D-L4.7 = A): what to set, whether to preview, status.
        var plan = ChordLLMFieldPlan.From(outcome);

        if (plan.ApplyFields)
        {
            if (plan.SetSetupFields)
            {
                timeSignature = plan.TimeSignature;
                referenceTonality = plan.ReferenceTonality;
            }
            if (plan.SetDefaultDuration)
                defaultDurationMeasures = plan.DefaultDurationMeasures;

            progressionInput = plan.Progression;
            inputMode = InputMode.RomanString;

            if (plan.RunPreview)
                ParseAndPreview(onlyPreview: true);
        }

        SetStatus(plan.StatusMessage, isError: plan.StatusIsError);
    }

    // -------------------------------------------------------------------
    // Resolution helpers
    // -------------------------------------------------------------------

    private ChordGenreVocabularySO ResolveVocabulary()
    {
        if (llmVocabulary != null) return llmVocabulary;
        llmVocabulary = Resources.Load<ChordGenreVocabularySO>(DefaultChordVocabResource);
        return llmVocabulary;
    }

    private ILLMClient ResolveClient(out string error)
    {
        error = null;
        LLMClientData data = llmClientOverride;

        if (data == null)
        {
            // Default: the first LLMClientData found in the project. Mirrors the
            // console harness's "select a client data asset" contract, but auto-
            // resolved so the panel works without an explicit assignment.
            string[] guids = AssetDatabase.FindAssets("t:LLMClientData");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                data = AssetDatabase.LoadAssetAtPath<LLMClientData>(path);
            }
        }

        if (data == null)
        {
            error = "No LLMClientData asset assigned or found in the project. " +
                    "Assign one in the Client (override) field.";
            return null;
        }

        var client = LLMClientFactory.CreateClient(data);
        if (client == null)
        {
            error = $"LLMClientFactory returned null for '{data.name}'. " +
                    "Check the provider is supported and the API key is configured.";
            return null;
        }
        return client;
    }

    internal static int ResolveBeatsPerMeasure(TimeSignature ts)
    {
        if (TimeSignatureProperties != null &&
            TimeSignatureProperties.TryGetValue(ts, out var props) &&
            props.BeatsPerMeasure > 0)
            return props.BeatsPerMeasure;
        return 4; // defensive default
    }

    private void SetStatus(string message, bool isError = false)
    {
        llmStatus = (isError ? "⚠ " : "") + message;
    }

    // -------------------------------------------------------------------
    // Create new progression (clean-slate working copy)
    // -------------------------------------------------------------------

    /// <summary>
    /// Detach from the current target asset and reset the working copy to a
    /// clean default state, so Generate / Apply / Save-As start from scratch and
    /// cannot touch the previously-targeted asset. Mirrors the drum tool's
    /// "create new" affordance. Asks for confirmation when there is content that
    /// would be discarded (the tool has no unsaved-edits flag, so it cannot tell
    /// whether the current working copy was saved — it asks rather than assume).
    /// </summary>
    private void NewProgression()
    {
        bool hasContent =
            targetAsset != null || !string.IsNullOrWhiteSpace(progressionInput);

        if (hasContent)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Create new progression",
                "This clears the current working copy and detaches from the target " +
                "asset. Any changes not saved to an asset will be lost.\n\n" +
                "The target asset itself is not modified or deleted.",
                "Create new", "Cancel");
            if (!proceed) return;
        }

        // Detaching the target reuses the window's existing clean-slate path,
        // which blanks progressionInput and all preview state.
        targetAsset = null;
        OnTargetAssetChanged();

        // Restore coherent authoring defaults for a fresh progression.
        inputMode = InputMode.RomanString;
        timeSignature = TimeSignature.FourFour;
        referenceTonality = Tonality.Ionian;
        defaultDurationMeasures = 1f;

        // Clear LLM panel transients so stale warnings/tokens don't linger.
        llmStatus = "";
        llmLastWarnings = "";
        llmLastInputTokens = 0;
        llmLastOutputTokens = 0;

        GUI.FocusControl(null);
        Repaint();
    }
}
#endif
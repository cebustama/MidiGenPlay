#if UNITY_EDITOR
using BCS.LLM.Core.Clients;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Authoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// <summary>
/// Package-owned Unity Editor window for authoring DrumPatternData assets.
///
/// Workflow:
///   1. Assign a target DrumPatternData asset (or leave empty to start a new pattern).
///   2. Edit time signature, measures, subdivisions, lanes and steps.
///   3. Apply To Asset (overwrite) or Save As New Asset.
///
/// Does NOT require a runtime scene.
/// Does NOT depend on RhythmPatternPanelController.
///
/// Phase 6: each lane row has a [T]/[V] mode toggle (Grid mode only).
///   Trigger mode  — boolean step buttons (green = active, dark = inactive).
///   Velocity mode — per-step int fields; 0 = defer to lane defaultVelocity.
///                   Setting a field > 0 activates the step with an explicit velocity.
///                   Setting a field to 0 deactivates the step (velocity 0 = sentinel/off).
///
/// Phase 7: whole-window Grid / Text tabbed toggle.
///   Grid  mode — current authoring surface (Phase 5 / Phase 6 controls).
///   Text  mode — one drum-machine glyph string per lane; parse on tab-switch and on Apply.
///                Per-cell diff preserves non-canonical per-step velocities for cells whose
///                typed glyph is unchanged from the rendered text.
///                Syntax authority: authoring/SSoT_Authoring_Rhythm_Patterns.md §3A.x.
/// </summary>
public class DrumPatternEditorWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string MenuPath = "MidiGenPlay/Drum Pattern Editor...";
    private const string DefaultSaveFolder = "Assets/Resources/ScriptableObjects/Patterns/Drums";

    private const float LaneHeaderWidth = 172f;
    private const float ViewModeButtonW = 22f;   // [T] / [V]
    private const float VelocityLabelW = 16f;
    private const float VelocityFieldW = 34f;
    private const float StepSize = 28f;
    private const float RowHeight = 26f;
    private const float RowSpacing = 2f;

    // Phase 7 — text mode
    private const float TextRowLabelW = 190f;     // lane name + velocity readout

    // L2 — LLM-assisted generation
    private const string DefaultLlmClientResourcePath =
        "ScriptableObjects/LLM/AnthropicClientData";   // Resources.Load path (no extension)
    private const string VocabularyResourcePath =
        "ScriptableObjects/Vocabularies/Default Rhythm Genres"; // Resources.Load path

    private enum InputMode
    {
        Grid,
        Text,
    }

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var w = GetWindow<DrumPatternEditorWindow>("Drum Pattern Editor");
        w.minSize = new Vector2(560f, 400f);
    }

    // -------------------------------------------------------------------------
    // Serialised editor state (survives domain reload when asset is assigned)
    // -------------------------------------------------------------------------

    [SerializeField] private DrumPatternData targetAsset;
    [SerializeField] private TimeSignature editTimeSignature = TimeSignature.FourFour;
    [SerializeField] private int editMeasures = 2;
    [SerializeField] private int editSubdivisions = 2;  // steps per beat

    /// <summary>Phase 7 — current authoring input mode. Survives domain reload within a session.</summary>
    [SerializeField] private InputMode _inputMode = InputMode.Grid;

    /// <summary>
    /// Phase 7 — text-mode authoring buffer. One string per working lane.
    /// Serialised so that session-level edits survive domain reload, but NEVER written into
    /// the asset (text is a view; the asset's per-step state is canonical). On asset rebind
    /// or new pattern this array is cleared; it is re-rendered on entry into text mode.
    /// </summary>
    [SerializeField] private string[] _textRows = Array.Empty<string>();

    // -------------------------------------------------------------------------
    // L2 — LLM-assisted generation state (serialised UI selections)
    // -------------------------------------------------------------------------

    /// <summary>Genre vocabulary source for the Generate dropdown (D-L2.5).</summary>
    [SerializeField] private RhythmGenreVocabularySO _vocabulary;

    /// <summary>
    /// Optional per-window client override (D-L2.1 = B). When null, the window
    /// falls back to the project-default Anthropic client loaded from Resources.
    /// </summary>
    [SerializeField] private LLMClientData _clientOverride;

    /// <summary>Index into the flattened genre list for the dropdown.</summary>
    [SerializeField] private int _selectedGenreIndex;

    /// <summary>Optional free-text direction appended to the prompt (D-L2.5).</summary>
    [SerializeField] private string _userDirection = string.Empty;

    /// <summary>Whether the LLM panel foldout is expanded.</summary>
    [SerializeField] private bool _llmPanelExpanded = true;

    /// <summary>
    /// L3 (D-L3.1 = A) — maximum total prompt character budget passed into
    /// <see cref="DrumPatternLLMPromptBuilder.Input.maxCharBudget"/>. 0 = no
    /// enforcement. When &gt; 0 and the assembled prompt exceeds it, the builder
    /// fails the call and the reason is surfaced through <see cref="_llmWarnings"/>
    /// (SMR-L3). Default 4000 mirrors the D-L4 soft cap.
    /// </summary>
    [SerializeField] private int _maxCharBudget = 4000;

    // -------------------------------------------------------------------------
    // Non-serialised working state
    // -------------------------------------------------------------------------

    private DrumPatternData _working;
    private DrumPatternData _lastBound;

    /// <summary>
    /// Rows whose view mode is currently Velocity (Grid mode only).
    /// Not serialised: resets on domain reload (acceptable — mode is authoring UI state, not asset truth).
    /// </summary>
    private readonly HashSet<int> _velocityModeRows = new HashSet<int>();

    /// <summary>
    /// Phase 7 — warnings emitted by the most recent parse or render. Cleared and rebuilt on
    /// every parse/render call. Displayed in the warning panel at the bottom of text mode.
    /// </summary>
    private readonly List<DrumPatternTextWarning> _warnings = new List<DrumPatternTextWarning>();

    /// <summary>
    /// L2 — human-readable warning/info lines from the most recent LLM
    /// generation or clipboard import. Rendered in the same warning panel as
    /// parser warnings. Cleared on a new generate/import cycle.
    /// </summary>
    private readonly List<string> _llmWarnings = new List<string>();

    /// <summary>L2 — true while an async LLM call is in flight (disables Generate, D-L2.3).</summary>
    private bool _isGenerating;

    /// <summary>L2 — cached last-generate parameters for the Regenerate button (D-L2.4 = A).</summary>
    private bool _hasLastGenerateInput;
    private string _lastGenreName;
    private string _lastSubStyleCueName;
    private string _lastUserDirection;

    private Vector2 _mainScroll;
    private Vector2 _gridScroll;
    private Vector2 _warningsScroll;

    private GUIStyle _stepOnStyle;
    private GUIStyle _stepOffStyle;
    private bool _stylesBuilt;
    private bool _pendingRebuild;

    // X position of the first step cell, measured during Repaint from the actual lane rows.
    // Used to align column headers without guessing control widths and spacings.
    private float _firstStepX = -1f;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        if (targetAsset != null && (_working == null || _lastBound != targetAsset))
            BindAsset(targetAsset);

        // L2 — lazy-load the default vocabulary so the genre dropdown is
        // populated without the user wiring it manually (D-L2.5).
        if (_vocabulary == null)
            _vocabulary = Resources.Load<RhythmGenreVocabularySO>(VocabularyResourcePath);
    }

    private void OnGUI()
    {
        EnsureStyles();

        _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

        DrawHeader();
        EditorGUILayout.Space(4f);
        DrawTimingControls();
        EditorGUILayout.Space(4f);
        DrawLLMPanel();
        EditorGUILayout.Space(4f);
        DrawLanesAndGrid();
        EditorGUILayout.Space(6f);
        DrawActionButtons();

        EditorGUILayout.EndScrollView();

        if (_pendingRebuild)
        {
            _pendingRebuild = false;
            ApplySignatureToWorking();
        }
    }

    // -------------------------------------------------------------------------
    // Header
    // -------------------------------------------------------------------------

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Drum Pattern Editor", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newAsset = (DrumPatternData)EditorGUILayout.ObjectField(
            new GUIContent("Target Asset",
                "DrumPatternData to edit. Leave empty to author a new pattern."),
            targetAsset, typeof(DrumPatternData), false);
        if (EditorGUI.EndChangeCheck() && newAsset != _lastBound)
        {
            targetAsset = newAsset;
            BindAsset(targetAsset);
        }

        if (_working == null)
        {
            EditorGUILayout.HelpBox(
                "No pattern loaded. Assign a Target Asset or press 'New Pattern'.",
                MessageType.Info);
        }
        else
        {
            string src = targetAsset != null
                ? AssetDatabase.GetAssetPath(targetAsset)
                : "unsaved new pattern";
            EditorGUILayout.LabelField($"Editing: {src}", EditorStyles.miniLabel);
        }
    }

    // -------------------------------------------------------------------------
    // Timing controls
    // -------------------------------------------------------------------------

    private void DrawTimingControls()
    {
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        var newTs = (TimeSignature)EditorGUILayout.EnumPopup(
            new GUIContent("Time Signature",
                "Sets the meter. Beats per measure is derived from this."),
            editTimeSignature);

        int newMeasures = Mathf.Max(1,
            EditorGUILayout.IntField(
                new GUIContent("Measures", "Number of bars in this pattern."),
                editMeasures));

        int newSubs = Mathf.Clamp(
            EditorGUILayout.IntSlider(
                new GUIContent("Subdivisions",
                    "Steps per beat. 1 = quarter grid, 2 = eighth, 4 = sixteenth."),
                editSubdivisions, 1, 4),
            1, 4);

        if (EditorGUI.EndChangeCheck())
        {
            editTimeSignature = newTs;
            editMeasures = newMeasures;
            editSubdivisions = newSubs;
            _pendingRebuild = true;
            Repaint();
        }

        if (_working != null)
        {
            int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
            int total = editMeasures * bpm * editSubdivisions;
            EditorGUILayout.LabelField(
                $"{editMeasures} bars × {bpm} beats × {editSubdivisions} subdivisions = {total} steps",
                EditorStyles.miniLabel);
        }
    }

    // -------------------------------------------------------------------------
    // L2 — LLM-assisted generation panel
    // -------------------------------------------------------------------------

    private void DrawLLMPanel()
    {
        _llmPanelExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _llmPanelExpanded, "LLM-Assisted Generation");

        if (_llmPanelExpanded)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // -- Vocabulary + client sources --
                _vocabulary = (RhythmGenreVocabularySO)EditorGUILayout.ObjectField(
                    new GUIContent("Genre Vocabulary",
                        "RhythmGenreVocabularySO providing the genre list. " +
                        "Defaults to Resources/.../Default Rhythm Genres."),
                    _vocabulary, typeof(RhythmGenreVocabularySO), false);

                _clientOverride = (LLMClientData)EditorGUILayout.ObjectField(
                    new GUIContent("LLM Client (override)",
                        "Optional. Leave empty to use the project-default Anthropic " +
                        "client from Resources/ScriptableObjects/LLM."),
                    _clientOverride, typeof(LLMClientData), false);

                // -- Cost cap (D-L3.1 = A) --
                _maxCharBudget = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("Max prompt chars (budget)",
                        "Maximum total prompt character count (system + user). " +
                        "0 disables the cap. When exceeded, generation is refused " +
                        "before any LLM call and the reason is shown below."),
                    _maxCharBudget));

                // -- Genre dropdown (D-L2.5) --
                string[] genreNames = GetGenreNames();
                if (genreNames.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No genres available. Assign a vocabulary asset (or run " +
                        "MidiGenPlay → Authoring → Create Default Rhythm Genres Asset).",
                        MessageType.Warning);
                }
                else
                {
                    _selectedGenreIndex = Mathf.Clamp(_selectedGenreIndex, 0, genreNames.Length - 1);
                    _selectedGenreIndex = EditorGUILayout.Popup(
                        new GUIContent("Genre",
                            "Mechanical parameters (meter, measures, subdivisions) " +
                            "come from the Timing controls above."),
                        _selectedGenreIndex, genreNames);
                }

                // -- Optional free-text direction --
                EditorGUILayout.LabelField(
                    new GUIContent("Additional direction (optional)",
                        "Free-text style cues passed verbatim to the LLM."));
                _userDirection = EditorGUILayout.TextArea(
                    _userDirection ?? string.Empty, GUILayout.MinHeight(36f));

                EditorGUILayout.Space(2f);

                // -- Action buttons (Generate / Regenerate / Import) --
                using (new EditorGUILayout.HorizontalScope())
                {
                    // During an in-flight call, disable Generate and show status (D-L2.3 = A).
                    GUI.enabled = !_isGenerating && _working != null
                                  && _vocabulary != null && genreNames.Length > 0;
                    if (GUILayout.Button(_isGenerating ? "Generating…" : "Generate"))
                        OnGenerateClicked(regenerate: false);

                    GUI.enabled = !_isGenerating && _working != null && _hasLastGenerateInput;
                    if (GUILayout.Button(
                        new GUIContent("Regenerate",
                            "Re-run the last generation with the same genre and direction."),
                        GUILayout.Width(110f)))
                        OnGenerateClicked(regenerate: true);

                    GUI.enabled = !_isGenerating && _working != null;
                    if (GUILayout.Button(
                        new GUIContent("Import from Clipboard",
                            "Parse a 'setup card + DSL block' payload from the clipboard."),
                        GUILayout.Width(170f)))
                        OnImportFromClipboard();

                    GUI.enabled = true;
                }

                if (_isGenerating)
                    EditorGUILayout.LabelField(
                        "Contacting the LLM… the editor stays responsive.",
                        EditorStyles.miniLabel);

                // L2 — LLM / import feedback, co-located with the controls so it
                // is visible in both Grid and Text modes.
                if (_llmWarnings.Count > 0)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField(
                        $"Generation / import notes ({_llmWarnings.Count})",
                        EditorStyles.boldLabel);
                    for (int i = 0; i < _llmWarnings.Count; i++)
                        EditorGUILayout.LabelField(_llmWarnings[i], EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private string[] GetGenreNames()
    {
        if (_vocabulary?.genres == null) return Array.Empty<string>();
        return _vocabulary.genres
            .Where(g => g != null && !string.IsNullOrWhiteSpace(g.genreName))
            .Select(g => g.genreName)
            .ToArray();
    }

    // -------------------------------------------------------------------------
    // L2 — Generate (async; never blocks the main thread, D-L2.3)
    // -------------------------------------------------------------------------

    private async void OnGenerateClicked(bool regenerate)
    {
        if (_isGenerating || _working == null) return;

        // Resolve genre + direction (fresh from UI, or cached for Regenerate).
        string genreName, cueName, direction;
        if (regenerate && _hasLastGenerateInput)
        {
            genreName = _lastGenreName;
            cueName = _lastSubStyleCueName;
            direction = _lastUserDirection;
        }
        else
        {
            var names = GetGenreNames();
            if (names.Length == 0) return;
            genreName = names[Mathf.Clamp(_selectedGenreIndex, 0, names.Length - 1)];
            cueName = null; // sub-style cue selection is a later enhancement; v1 uses genre + free-text
            direction = _userDirection;
        }

        // Resolve the LLM client (override → project default).
        ILLMClient client = ResolveClient(out string clientError);
        if (client == null)
        {
            _llmWarnings.Clear();
            _llmWarnings.Add(clientError);
            Repaint();
            return;
        }

        // Build the prompt input from the genre's lane composition + grid params.
        if (!TryBuildPromptInput(genreName, cueName, direction, out var input, out string inputError))
        {
            _llmWarnings.Clear();
            _llmWarnings.Add(inputError);
            Repaint();
            return;
        }

        // Cache for Regenerate.
        _lastGenreName = genreName;
        _lastSubStyleCueName = cueName;
        _lastUserDirection = direction;
        _hasLastGenerateInput = true;

        _isGenerating = true;
        _llmWarnings.Clear();
        Repaint();

        DrumPatternLLMResponseHandler.Outcome outcome;
        try
        {
            outcome = await DrumPatternLLMResponseHandler.GenerateAsync(
                client, _vocabulary, input, LaneAliasDictionary.TryResolve);
        }
        catch (Exception ex)
        {
            _isGenerating = false;
            _llmWarnings.Add($"Generation failed: {ex.GetType().Name}: {ex.Message}");
            Repaint();
            return;
        }

        // Back on the main thread (await continuation): apply the outcome.
        _isGenerating = false;
        ApplyOutcome(outcome);
        Repaint();
    }

    private void OnImportFromClipboard()
    {
        if (_working == null) return;

        string payload = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(payload))
        {
            _llmWarnings.Clear();
            _llmWarnings.Add("Clipboard is empty — nothing to import.");
            Repaint();
            return;
        }

        _llmWarnings.Clear();
        var outcome = DrumPatternLLMResponseHandler.FromPayload(
            payload, LaneAliasDictionary.TryResolve);
        ApplyOutcome(outcome);
        Repaint();
    }

    // -------------------------------------------------------------------------
    // L2 — Apply an outcome (Full → grid config + rows; DslOnly → rows only)
    // -------------------------------------------------------------------------

    private void ApplyOutcome(DrumPatternLLMResponseHandler.Outcome outcome)
    {
        // Surface all warning/info lines in the panel (same shape as parser warnings).
        _llmWarnings.Clear();
        foreach (var w in outcome.displayWarnings)
            _llmWarnings.Add(w);

        if (!outcome.Success)
            return; // Failed: nothing applied; existing rows/grid preserved.

        if (outcome.kind == DrumPatternLLMResponseHandler.OutcomeKind.Full)
        {
            ConfigureGridFromOutcome(outcome);
        }

        // Both Full and DslOnly: write DSL into the text rows and switch to Text mode
        // so the user sees the result and Apply commits it (D-L2.2 = A — reuse Phase 7).
        WriteDslIntoTextRows(outcome.dslLines);
        _inputMode = InputMode.Text;
    }

    /// <summary>
    /// Apply the setup-card grid configuration: signature, measures, subdivisions,
    /// and lane composition (instruments + default velocities). Mirrors the
    /// structural path of <see cref="ApplySignatureToWorking"/> + lane rebuild.
    /// </summary>
    private void ConfigureGridFromOutcome(DrumPatternLLMResponseHandler.Outcome outcome)
    {
        if (_working == null) return;

        // Sync the editor's timing controls so the readout matches.
        editTimeSignature = outcome.timeSignature;
        editMeasures = Mathf.Max(1, outcome.measures);
        editSubdivisions = Mathf.Clamp(outcome.subdivisions, 1, 4);

        int beats = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;

        // Rebuild lanes from the outcome's composition.
        _working.lanes = new List<DrumPatternData.Lane>(outcome.lanes.Count);
        foreach (var laneInfo in outcome.lanes)
        {
            _working.lanes.Add(new DrumPatternData.Lane
            {
                instrument = laneInfo.instrument,
                defaultVelocity = Mathf.Clamp(laneInfo.defaultVelocity, 1, 127),
                steps = new List<DrumPatternData.StepState>(),
            });
        }
        if (_working.lanes.Count == 0)
            _working.lanes.Add(new DrumPatternData.Lane());

        // Size step lists to the new signature (creates Off steps).
        _working.SetSignature(beats, editMeasures, editSubdivisions);
        _working.TimeSignature = editTimeSignature;
        _velocityModeRows.Clear();
        _firstStepX = -1f;
    }

    /// <summary>
    /// Write the DSL glyph lines into <c>_textRows</c>, sized to the current
    /// working lane count. Extra lines are ignored; missing lines become empty
    /// (the parser right-pads with rests on commit).
    /// </summary>
    private void WriteDslIntoTextRows(IReadOnlyList<string> dslLines)
    {
        if (_working == null) return;

        int laneCount = _working.lanes.Count;
        _textRows = new string[laneCount];
        for (int i = 0; i < laneCount; i++)
            _textRows[i] = (dslLines != null && i < dslLines.Count) ? dslLines[i] : string.Empty;
    }

    // -------------------------------------------------------------------------
    // L2 — Client + prompt-input resolution
    // -------------------------------------------------------------------------

    private ILLMClient ResolveClient(out string error)
    {
        error = null;
        LLMClientData data = _clientOverride;
        if (data == null)
            data = Resources.Load<LLMClientData>(DefaultLlmClientResourcePath);

        if (data == null)
        {
            error = "No LLM client available. Assign an override, or place an " +
                    "AnthropicClientData at Resources/" + DefaultLlmClientResourcePath + ".";
            return null;
        }

        var client = LLMClientFactory.CreateClient(data);
        if (client == null)
        {
            error = $"LLMClientFactory returned null for '{data.name}'. Check the " +
                    "provider configuration and API key.";
            return null;
        }
        return client;
    }

    private bool TryBuildPromptInput(
        string genreName, string cueName, string direction,
        out DrumPatternLLMPromptBuilder.Input input, out string error)
    {
        input = default;
        error = null;

        GenreEntry genre = _vocabulary?.genres?
            .FirstOrDefault(g => g != null &&
                string.Equals(g.genreName, genreName, StringComparison.OrdinalIgnoreCase));
        if (genre == null)
        {
            error = $"Genre '{genreName}' not found in the vocabulary.";
            return false;
        }
        if (genre.defaultLaneComposition == null || genre.defaultLaneComposition.Count == 0)
        {
            error = $"Genre '{genreName}' has no default lane composition.";
            return false;
        }

        int beats = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;

        input = new DrumPatternLLMPromptBuilder.Input(
            genreName: genreName,
            subStyleCueName: cueName,
            timeSignature: editTimeSignature,
            beatsPerMeasure: beats,
            measures: Mathf.Max(1, editMeasures),
            subdivisions: Mathf.Clamp(editSubdivisions, 1, 4),
            laneComposition: genre.defaultLaneComposition,
            userFreeText: string.IsNullOrWhiteSpace(direction) ? null : direction.Trim(),
            maxCharBudget: Mathf.Max(0, _maxCharBudget));
        return true;
    }

    // -------------------------------------------------------------------------
    // Lanes + grid (mode-aware dispatch)
    // -------------------------------------------------------------------------

    private void DrawLanesAndGrid()
    {
        if (_working == null) return;

        EditorGUILayout.LabelField("Lanes & Steps", EditorStyles.boldLabel);

        // Phase 7 — whole-window Grid / Text tab toggle (mirrors ChordProgressionEditorWindow).
        EditorGUI.BeginChangeCheck();
        var newMode = (InputMode)GUILayout.Toolbar(
            (int)_inputMode,
            new[] { "Grid", "Text" });
        if (EditorGUI.EndChangeCheck() && newMode != _inputMode)
        {
            OnInputModeChange(newMode);
        }

        EditorGUILayout.Space(2f);

        if (_inputMode == InputMode.Grid)
            DrawGridMode();
        else
            DrawTextMode();
    }

    private void DrawGridMode()
    {
        int totalSteps = _working.TotalSteps;
        int stepsPerMeasure = _working.beatsPerMeasure * _working.subdivisions;

        _gridScroll = EditorGUILayout.BeginScrollView(
            _gridScroll, GUILayout.MaxHeight(420f));

        DrawColumnHeaders(totalSteps, stepsPerMeasure);

        for (int r = 0; r < _working.lanes.Count; r++)
            DrawLaneRow(r, totalSteps, stepsPerMeasure);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(2f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Lane", GUILayout.Width(72)))
                AddLane();

            GUI.enabled = _working.lanes.Count > 1;
            if (GUILayout.Button("− Last", GUILayout.Width(60)))
                RemoveLastLane();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear All Steps", GUILayout.Width(114)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Steps",
                    "Remove all active steps from every lane?",
                    "Clear", "Cancel"))
                {
                    _working.ClearAll();
                    Repaint();
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Text mode (Phase 7)
    // -------------------------------------------------------------------------

    private void DrawTextMode()
    {
        // Compact glyph legend. The authoritative syntax lives in
        // authoring/SSoT_Authoring_Rhythm_Patterns.md §3A.x.
        EditorGUILayout.HelpBox(
            "Glyphs:  . or -  rest    x  hit (lane default)    X  accent (120)    o  ghost (50)\n" +
            "Ignored: spaces, |\n" +
            "Length:  short rows pad with rests; long rows truncate. Warnings shown below.",
            MessageType.Info);

        EnsureTextRowsArraySize();

        _gridScroll = EditorGUILayout.BeginScrollView(
            _gridScroll, GUILayout.MaxHeight(420f));

        for (int r = 0; r < _working.lanes.Count; r++)
            DrawTextLaneRow(r);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(2f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Lane", GUILayout.Width(72)))
                AddLane();

            GUI.enabled = _working.lanes.Count > 1;
            if (GUILayout.Button("− Last", GUILayout.Width(60)))
                RemoveLastLane();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Re-render from grid", GUILayout.Width(150)))
                RenderWorkingIntoText(); // discards any unparsed text edits in favor of current asset state
        }

        DrawWarningPanel();
    }

    /// <summary>One row in text mode: lane label + single TextField + remove button.</summary>
    private void DrawTextLaneRow(int rowIndex)
    {
        var lane = _working.lanes[rowIndex];

        using (new EditorGUILayout.HorizontalScope())
        {
            // [T/V] placeholder (disabled in text mode) — keeps vertical alignment with grid mode.
            GUI.enabled = false;
            GUILayout.Button(
                new GUIContent("·", "Row view mode does not apply in Text mode"),
                GUILayout.Width(ViewModeButtonW),
                GUILayout.Height(RowHeight));
            GUI.enabled = true;

            // Read-only lane label: instrument name + default velocity readout.
            string label = $"{lane.instrument} (v{lane.defaultVelocity})";
            GUILayout.Label(
                new GUIContent(label,
                    "Change instrument or default velocity in Grid mode."),
                GUILayout.Width(TextRowLabelW),
                GUILayout.Height(RowHeight));

            GUILayout.Space(4f);

            // The text field — full remaining width.
            string current = (rowIndex < _textRows.Length) ? _textRows[rowIndex] ?? "" : "";
            string updated = EditorGUILayout.TextField(
                current,
                GUILayout.Height(RowHeight),
                GUILayout.ExpandWidth(true));
            if (!ReferenceEquals(current, updated) && current != updated)
            {
                _textRows[rowIndex] = updated;
            }

            // Remove lane (allowed in either mode).
            if (GUILayout.Button("✕", GUILayout.Width(22f), GUILayout.Height(RowHeight)))
                RemoveLane(rowIndex);
        }

        GUILayout.Space(RowSpacing);
    }

    /// <summary>Bottom panel listing warnings from the most recent parse/render cycle.</summary>
    private void DrawWarningPanel()
    {
        if (_warnings.Count == 0)
        {
            EditorGUILayout.LabelField("No warnings.", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField($"Warnings ({_warnings.Count})", EditorStyles.boldLabel);

        _warningsScroll = EditorGUILayout.BeginScrollView(
            _warningsScroll, GUILayout.MaxHeight(110f));
        for (int i = 0; i < _warnings.Count; i++)
        {
            var w = _warnings[i];
            EditorGUILayout.LabelField(w.ToString(), EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Grid mode lane drawing (unchanged from Phase 6)
    // -------------------------------------------------------------------------

    private void DrawColumnHeaders(int totalSteps, int stepsPerMeasure)
    {
        // Reserve a row of the correct height so the layout flow has the right spacing.
        Rect headerRow = GUILayoutUtility.GetRect(0f, RowHeight,
            GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight));

        // _firstStepX is captured from the reserved GUILayoutUtility.GetRect in DrawTriggerSteps/
        // DrawVelocitySteps — written on every event pass, so it is valid by the time we draw here.
        // Skip only until the first lane row has been processed (first frame after open or rebind).
        if (_firstStepX < 0f || Event.current.type != EventType.Repaint)
            return;

        float cellX = _firstStepX;
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                cellX += 3f + 1f; // measure gap (Space 3f) + inter-cell Space(1f)
            else if (s > 0)
                cellX += 1f; // inter-cell gap between steps

            bool isBarStart = s % stepsPerMeasure == 0;
            int posInMeasure = s % stepsPerMeasure;
            bool isBeatStart = !isBarStart && posInMeasure % _working.subdivisions == 0;

            if (isBarStart || isBeatStart)
            {
                string label = isBarStart ? $"|{s / stepsPerMeasure + 1}" : "·";
                var style = isBarStart ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel;
                GUI.Label(new Rect(cellX, headerRow.y, StepSize - 1f, headerRow.height), label, style);
            }

            cellX += StepSize - 1f;
        }
    }

    private void DrawLaneRow(int rowIndex, int totalSteps, int stepsPerMeasure)
    {
        var lane = _working.lanes[rowIndex];

        // Guard against signature changes mid-draw
        while (lane.steps.Count < totalSteps)
            lane.steps.Add(DrumPatternData.StepState.Off);

        bool isVelocityMode = _velocityModeRows.Contains(rowIndex);

        using (new EditorGUILayout.HorizontalScope())
        {
            // ---- View mode toggle [T] / [V] ----
            string modeLabel = isVelocityMode ? "V" : "T";
            var modeTooltip = isVelocityMode
                ? "Velocity mode — click to switch to Trigger mode"
                : "Trigger mode — click to switch to Velocity mode";

            if (GUILayout.Button(
                new GUIContent(modeLabel, modeTooltip),
                GUILayout.Width(ViewModeButtonW),
                GUILayout.Height(RowHeight)))
            {
                if (isVelocityMode)
                    _velocityModeRows.Remove(rowIndex);
                else
                    _velocityModeRows.Add(rowIndex);
                Repaint();
                return; // avoid mid-frame state inconsistency
            }

            // ---- Instrument selector ----
            var allNames = Enum.GetNames(typeof(GeneralMidiPercussion));
            var allValues = (GeneralMidiPercussion[])Enum.GetValues(typeof(GeneralMidiPercussion));
            int curIdx = Array.IndexOf(allValues, lane.instrument);
            if (curIdx < 0) curIdx = 0;

            int newIdx = EditorGUILayout.Popup(
                curIdx, allNames, GUILayout.Width(LaneHeaderWidth));
            if (newIdx != curIdx)
                lane.instrument = allValues[newIdx];

            // ---- Default velocity (compact) ----
            GUILayout.Label("v", GUILayout.Width(VelocityLabelW));
            int newVel = EditorGUILayout.IntField(
                lane.defaultVelocity, GUILayout.Width(VelocityFieldW));
            lane.defaultVelocity = Mathf.Clamp(newVel, 1, 127);

            GUILayout.Space(4f);

            // ---- Steps ----
            if (isVelocityMode)
                DrawVelocitySteps(lane, rowIndex, totalSteps, stepsPerMeasure);
            else
                DrawTriggerSteps(lane, totalSteps, stepsPerMeasure);

            // ---- Per-row remove ----
            if (GUILayout.Button("✕", GUILayout.Width(22f), GUILayout.Height(RowHeight)))
                RemoveLane(rowIndex);
        }

        GUILayout.Space(RowSpacing);
    }

    /// <summary>
    /// Trigger view: green toggle = active, dark = inactive.
    /// Toggling preserves existing per-step velocity.
    /// </summary>
    private void DrawTriggerSteps(
        DrumPatternData.Lane lane, int totalSteps, int stepsPerMeasure)
    {
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                GUILayout.Space(3f);
            else if (s > 0)
                GUILayout.Space(1f); // 1px gap between cells (matches pre-fix visual)

            // Reserve the rect before drawing so we have a guaranteed pixel position.
            // GUILayout.Toggle's GetLastRect() is unreliable; GetRect() before the draw is exact.
            Rect cellRect = GUILayoutUtility.GetRect(
                StepSize - 1f, RowHeight,
                GUILayout.Width(StepSize - 1f),
                GUILayout.Height(RowHeight));

            // Capture the X of the very first step cell so DrawColumnHeaders
            // can anchor its markers to the exact same pixel position.
            // Must be Repaint-only: during Layout pass GetRect returns the group origin, not the
            // accumulated intra-group position, so the value would be wrong.
            if (s == 0 && Event.current.type == EventType.Repaint)
                _firstStepX = cellRect.x;

            var step = lane.steps[s];
            bool cur = step.active;
            bool toggled = EditorGUI.Toggle(
                cellRect, cur,
                cur ? _stepOnStyle : _stepOffStyle);

            if (toggled != cur)
            {
                lane.steps[s] = toggled
                    ? DrumPatternData.StepState.On(step.velocity)
                    : DrumPatternData.StepState.Off;
                Repaint();
            }
        }
    }

    /// <summary>
    /// Velocity view: int field per step showing per-step velocity (0 = use lane default / inactive).
    /// Setting a field > 0 activates the step with that explicit velocity.
    /// Setting a field to 0 deactivates the step.
    /// A [clr] button at row end resets all per-step velocities to 0 (defer to default),
    /// while keeping activation state intact.
    /// </summary>
    private void DrawVelocitySteps(
        DrumPatternData.Lane lane, int rowIndex, int totalSteps, int stepsPerMeasure)
    {
        for (int s = 0; s < totalSteps; s++)
        {
            if (s > 0 && s % stepsPerMeasure == 0)
                GUILayout.Space(3f);
            else if (s > 0)
                GUILayout.Space(1f); // 1px gap between cells (matches trigger mode visual)

            var step = lane.steps[s];

            // Inactive steps show lane.defaultVelocity as a hint of what will be activated.
            // Writing 0 explicitly deactivates. Writing any value > 0 activates.
            int displayValue = step.active
                ? (step.velocity > 0 ? step.velocity : lane.defaultVelocity)
                : lane.defaultVelocity;

            bool wasActive = step.active;

            // Reserve the rect before drawing so we have a guaranteed pixel position.
            // EditorGUILayout.IntField's GetLastRect() is unreliable; GetRect() before draw is exact.
            Rect cellRect = GUILayoutUtility.GetRect(
                StepSize - 1f, RowHeight,
                GUILayout.Width(StepSize - 1f),
                GUILayout.Height(RowHeight));

            // Capture X anchor from first cell (covers the case where row 0 is in velocity mode).
            // Must be Repaint-only: Layout pass GetRect returns group origin, not intra-group position.
            if (s == 0 && Event.current.type == EventType.Repaint)
                _firstStepX = cellRect.x;

            // Tint active steps amber, inactive steps muted grey.
            // GUI.backgroundColor push/pop avoids passing a custom GUIStyle to IntField,
            // which causes IMGUI layout desync between Layout and Repaint passes.
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = step.active
                ? new Color(0.85f, 0.60f, 0.15f, 1f)
                : new Color(0.45f, 0.45f, 0.45f, 1f);

            int newValue = EditorGUI.IntField(cellRect, displayValue);

            GUI.backgroundColor = prevBg;

            newValue = Mathf.Clamp(newValue, 0, 127);

            if (newValue != displayValue)
            {
                if (newValue == 0)
                {
                    // 0 = deactivate
                    lane.steps[s] = DrumPatternData.StepState.Off;
                }
                else
                {
                    // > 0 = activate with explicit velocity
                    // Store as explicit override only if it differs from lane default,
                    // otherwise store 0 sentinel (defer) to keep assets clean.
                    int storeVel = (newValue == lane.defaultVelocity) ? 0 : newValue;
                    lane.steps[s] = DrumPatternData.StepState.On(storeVel);
                }
                Repaint();
            }
        }

        // [clr] button: reset all per-step velocities to 0 (defer to default),
        // leaving active/inactive state unchanged.
        if (GUILayout.Button(
            new GUIContent("clr", "Reset all per-step velocities to 0 (defer to lane default). Active steps remain active."),
            GUILayout.Width(28f),
            GUILayout.Height(RowHeight)))
        {
            for (int s = 0; s < lane.steps.Count; s++)
            {
                var st = lane.steps[s];
                if (st.velocity != 0)
                    lane.steps[s] = new DrumPatternData.StepState { active = st.active, velocity = 0 };
            }
            Repaint();
        }
    }

    // -------------------------------------------------------------------------
    // Action buttons
    // -------------------------------------------------------------------------

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Pattern"))
                CreateNewPattern();

            GUI.enabled = targetAsset != null && _working != null;
            if (GUILayout.Button("Apply To Asset"))
                ApplyToAsset();
            GUI.enabled = true;

            GUI.enabled = _working != null;
            if (GUILayout.Button("Save As New Asset…"))
                SaveAsNewAsset();
            GUI.enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Bind / new
    // -------------------------------------------------------------------------

    private void BindAsset(DrumPatternData asset)
    {
        _lastBound = asset;
        _velocityModeRows.Clear(); // reset row view modes on rebind
        _firstStepX = -1f;
        _warnings.Clear();

        if (asset == null)
        {
            _working = null;
            _textRows = Array.Empty<string>();
            Repaint();
            return;
        }

        _working = asset.DeepCloneRuntime();
        _working.InitializeIfEmpty();

        // Sync editor controls from asset
        editTimeSignature = _working.TimeSignature;
        editMeasures = Mathf.Max(1, _working.Measures);
        editSubdivisions = Mathf.Max(1, _working.subdivisions);

        // Refresh text-mode buffer if it's currently visible. Otherwise lazy-render on next entry.
        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();
        else
            _textRows = Array.Empty<string>();

        Repaint();
    }

    private void CreateNewPattern()
    {
        targetAsset = null;
        _lastBound = null;
        _velocityModeRows.Clear();
        _firstStepX = -1f;
        _warnings.Clear();

        _working = ScriptableObject.CreateInstance<DrumPatternData>();
        _working.name = "New Drum Pattern (unsaved)";

        int bpm = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        _working.SetSignature(bpm, editMeasures, editSubdivisions);
        _working.TimeSignature = editTimeSignature;
        _working.InitializeIfEmpty();

        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();
        else
            _textRows = Array.Empty<string>();

        Repaint();
    }

    // -------------------------------------------------------------------------
    // Input-mode transitions (Phase 7)
    // -------------------------------------------------------------------------

    private void OnInputModeChange(InputMode newMode)
    {
        if (newMode == _inputMode) return;

        if (newMode == InputMode.Text)
        {
            // Grid → Text: render current working copy into the text buffer.
            _inputMode = InputMode.Text;
            RenderWorkingIntoText();
        }
        else
        {
            // Text → Grid: commit current text buffer into the working copy via per-cell diff.
            CommitTextToWorking();
            _inputMode = InputMode.Grid;
            // Warnings stay visible after switching back to grid for one frame in case the
            // commit produced parse warnings the designer should see. Cleared on next text entry.
        }

        Repaint();
    }

    /// <summary>
    /// Parse <c>_textRows</c> into the working copy. Per-cell diff via <see cref="DrumPatternTextParser.ApplyTextEdits"/>
    /// preserves cells whose typed glyph matches the previous render (preserving custom velocities).
    /// Warnings are appended to <c>_warnings</c>.
    /// </summary>
    private void CommitTextToWorking()
    {
        if (_working == null) return;
        if (_textRows == null) return;

        int total = _working.TotalSteps;
        _warnings.Clear();

        int laneCount = Mathf.Min(_working.lanes.Count, _textRows.Length);
        for (int i = 0; i < laneCount; i++)
        {
            var lane = _working.lanes[i];
            var updated = DrumPatternTextParser.ApplyTextEdits(
                previous: lane.steps,
                input: _textRows[i] ?? string.Empty,
                totalSteps: total,
                laneDefaultVelocity: lane.defaultVelocity,
                laneIndex: i,
                warnings: _warnings);

            lane.steps.Clear();
            lane.steps.AddRange(updated);
        }
    }

    /// <summary>
    /// Render the working copy into <c>_textRows</c> for the text view.
    /// Replaces any prior <c>_warnings</c> contents (warnings reflect the latest cycle only).
    /// </summary>
    private void RenderWorkingIntoText()
    {
        if (_working == null)
        {
            _textRows = Array.Empty<string>();
            _warnings.Clear();
            return;
        }

        int laneCount = _working.lanes.Count;
        if (_textRows == null || _textRows.Length != laneCount)
            _textRows = new string[laneCount];

        int spm = Mathf.Max(1, _working.beatsPerMeasure * _working.subdivisions);
        _warnings.Clear();

        for (int i = 0; i < laneCount; i++)
        {
            var lane = _working.lanes[i];
            _textRows[i] = DrumPatternTextParser.Render(
                steps: lane.steps,
                laneDefaultVelocity: lane.defaultVelocity,
                laneIndex: i,
                warnings: _warnings,
                stepsPerMeasure: spm);
        }
    }

    private void EnsureTextRowsArraySize()
    {
        if (_working == null) return;
        int laneCount = _working.lanes.Count;
        if (_textRows == null || _textRows.Length != laneCount)
        {
            // Lane count drifted (e.g. after AddLane/RemoveLane in text mode); re-render.
            RenderWorkingIntoText();
        }
    }

    // -------------------------------------------------------------------------
    // Signature apply (deferred one frame to avoid mid-draw resize)
    // -------------------------------------------------------------------------

    private void ApplySignatureToWorking()
    {
        if (_working == null)
        {
            CreateNewPattern();
            return;
        }

        // Phase 7: commit any pending text edits before structural change so the typed
        // content survives the resize. CommitTextToWorking is a no-op outside text mode
        // (it only runs if _textRows is non-empty and the working copy is alive).
        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        // --- Capture old dimensions before any mutation ---
        int oldMeasures = _working.Measures;
        int oldBeats = _working.beatsPerMeasure;
        int oldSubs = _working.subdivisions;
        int oldTotalSteps = Mathf.Max(1, oldMeasures * oldBeats * oldSubs);

        int newBeats = TimeSignatureProperties[editTimeSignature].BeatsPerMeasure;
        int newMeasures = editMeasures;
        int newSubs = editSubdivisions;
        int newTotalSteps = Mathf.Max(1, newMeasures * newBeats * newSubs);

        // --- Remap each lane before SetSignature resizes the lists ---
        if (_working.lanes != null)
        {
            foreach (var lane in _working.lanes)
            {
                var remapped = new List<DrumPatternData.StepState>(newTotalSteps);
                for (int i = 0; i < newTotalSteps; i++)
                    remapped.Add(DrumPatternData.StepState.Off);

                for (int s = 0; s < lane.steps.Count && s < oldTotalSteps; s++)
                {
                    var step = lane.steps[s];
                    if (!step.active) continue;

                    float frac = s / (float)oldTotalSteps;
                    int newIndex = Mathf.RoundToInt(frac * newTotalSteps);
                    newIndex = Mathf.Clamp(newIndex, 0, newTotalSteps - 1);

                    // Collision: keep the step with higher resolved velocity.
                    var existing = remapped[newIndex];
                    if (!existing.active ||
                        step.ResolveVelocity(lane.defaultVelocity) >
                        existing.ResolveVelocity(lane.defaultVelocity))
                    {
                        remapped[newIndex] = step;
                    }
                }

                lane.steps = remapped;
            }
        }

        // --- Now apply signature; EnsureSizes will be a no-op (list already correct size) ---
        _working.SetSignature(newBeats, newMeasures, newSubs);
        _working.TimeSignature = editTimeSignature;
        _firstStepX = -1f;

        // Phase 7: re-render text from the resized working copy.
        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();

        Repaint();
    }

    // -------------------------------------------------------------------------
    // Lane management
    // -------------------------------------------------------------------------

    private void AddLane()
    {
        if (_working == null) return;
        _working.lanes ??= new List<DrumPatternData.Lane>();

        // Phase 7: commit text first so the user's in-flight typing isn't lost.
        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        _working.lanes.Add(new DrumPatternData.Lane
        {
            instrument = GuessNextInstrument(),
            defaultVelocity = 100,
            steps = new List<DrumPatternData.StepState>(
                new DrumPatternData.StepState[_working.TotalSteps])
        });

        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();

        Repaint();
    }

    private void RemoveLastLane()
    {
        if (_working?.lanes == null || _working.lanes.Count == 0) return;

        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        int last = _working.lanes.Count - 1;
        _velocityModeRows.Remove(last);
        _working.lanes.RemoveAt(last);

        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();

        Repaint();
    }

    private void RemoveLane(int index)
    {
        if (_working?.lanes == null) return;
        if (index < 0 || index >= _working.lanes.Count) return;

        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        _velocityModeRows.Remove(index);
        // Remap velocity-mode rows above the removed index
        var above = new List<int>();
        foreach (var r in _velocityModeRows)
            if (r > index) above.Add(r);
        foreach (var r in above)
        {
            _velocityModeRows.Remove(r);
            _velocityModeRows.Add(r - 1);
        }
        _working.lanes.RemoveAt(index);

        if (_inputMode == InputMode.Text)
            RenderWorkingIntoText();

        Repaint();
    }

    private static readonly GeneralMidiPercussion[] _commonPerc =
    {
        GeneralMidiPercussion.BassDrum1,
        GeneralMidiPercussion.AcousticSnare,
        GeneralMidiPercussion.ClosedHiHat,
        GeneralMidiPercussion.OpenHiHat,
        GeneralMidiPercussion.HandClap,
        GeneralMidiPercussion.LowTom,
        GeneralMidiPercussion.HighTom,
        GeneralMidiPercussion.CrashCymbal1,
        GeneralMidiPercussion.RideCymbal1,
    };

    private GeneralMidiPercussion GuessNextInstrument()
    {
        if (_working?.lanes == null || _working.lanes.Count == 0)
            return GeneralMidiPercussion.BassDrum1;

        var used = new HashSet<GeneralMidiPercussion>();
        foreach (var l in _working.lanes) used.Add(l.instrument);

        foreach (var cand in _commonPerc)
            if (!used.Contains(cand)) return cand;

        return _commonPerc[_working.lanes.Count % _commonPerc.Length];
    }

    // -------------------------------------------------------------------------
    // Persistence
    // -------------------------------------------------------------------------

    private void ApplyToAsset()
    {
        if (targetAsset == null || _working == null) return;

        // Phase 7: parse-from-text first so text-mode edits land in the asset.
        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        Undo.RecordObject(targetAsset, "Drum Pattern Editor: Apply");
        CopyWorkingInto(targetAsset);
        EditorUtility.SetDirty(targetAsset);
        AssetDatabase.SaveAssets();

        BindAsset(targetAsset);
        Debug.Log($"[DrumPatternEditor] Applied to {AssetDatabase.GetAssetPath(targetAsset)}");
    }

    private void SaveAsNewAsset()
    {
        if (_working == null) return;

        // Phase 7: parse-from-text first so text-mode edits land in the asset.
        if (_inputMode == InputMode.Text)
            CommitTextToWorking();

        Directory.CreateDirectory(DefaultSaveFolder);
        AssetDatabase.Refresh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Drum Pattern As…",
            BuildDefaultName(),
            "asset",
            "Choose where to save the new drum pattern asset.",
            DefaultSaveFolder);

        if (string.IsNullOrEmpty(path)) return;

        var newAsset = ScriptableObject.CreateInstance<DrumPatternData>();
        AssetDatabase.CreateAsset(newAsset, path);

        Undo.RecordObject(newAsset, "Drum Pattern Editor: Save As New");
        CopyWorkingInto(newAsset, Path.GetFileNameWithoutExtension(path));
        EditorUtility.SetDirty(newAsset);
        AssetDatabase.SaveAssets();

        targetAsset = newAsset;
        BindAsset(targetAsset);
        Debug.Log($"[DrumPatternEditor] Saved new asset at {path}");
    }

    private void CopyWorkingInto(DrumPatternData dst, string nameOverride = null)
    {
        dst.DisplayName = string.IsNullOrEmpty(nameOverride)
                                ? _working.DisplayName
                                : nameOverride;
        dst.TimeSignature = _working.TimeSignature;
        dst.Measures = _working.Measures;
        dst.beatsPerMeasure = _working.beatsPerMeasure;
        dst.subdivisions = _working.subdivisions;

        dst.lanes ??= new List<DrumPatternData.Lane>();
        dst.lanes.Clear();
        foreach (var l in _working.lanes)
        {
            dst.lanes.Add(new DrumPatternData.Lane
            {
                instrument = l.instrument,
                defaultVelocity = l.defaultVelocity,
                steps = new List<DrumPatternData.StepState>(l.steps)
            });
        }
        dst.EnsureSizes();
    }

    private string BuildDefaultName()
    {
        if (_working == null) return "NewDrumPattern";

        string ts = $"{_working.beatsPerMeasure}-{_working.subdivisions}";
        string ins = (_working.lanes == null || _working.lanes.Count == 0)
            ? "empty"
            : string.Join("", _working.lanes.ConvertAll(l => InstrumentAbbrev(l.instrument)));
        return $"Drum_{ts}_{_working.Measures}m_{ins}";
    }

    private static string InstrumentAbbrev(GeneralMidiPercussion gmp)
    {
        switch (gmp)
        {
            case GeneralMidiPercussion.BassDrum1: return "BD";
            case GeneralMidiPercussion.AcousticSnare:
            case GeneralMidiPercussion.ElectricSnare: return "SN";
            case GeneralMidiPercussion.ClosedHiHat: return "CH";
            case GeneralMidiPercussion.OpenHiHat: return "OH";
            case GeneralMidiPercussion.HandClap: return "CL";
            case GeneralMidiPercussion.LowTom: return "LT";
            case GeneralMidiPercussion.HighTom: return "HT";
            case GeneralMidiPercussion.CrashCymbal1: return "CR";
            case GeneralMidiPercussion.RideCymbal1: return "RI";
            default:
                var s = gmp.ToString();
                return s.Length >= 2 ? s.Substring(0, 2).ToUpper() : s.ToUpper();
        }
    }

    // -------------------------------------------------------------------------
    // Styles
    // -------------------------------------------------------------------------

    private void EnsureStyles()
    {
        if (_stylesBuilt) return;

        _stepOnStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(2, 2, new Color(0.25f, 0.85f, 0.35f, 1f)) },
            hover = { background = MakeTex(2, 2, new Color(0.35f, 1.00f, 0.45f, 1f)) },
            active = { background = MakeTex(2, 2, new Color(0.15f, 0.65f, 0.25f, 1f)) },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _stepOffStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.18f, 1f)) },
            hover = { background = MakeTex(2, 2, new Color(0.28f, 0.28f, 0.28f, 1f)) },
            active = { background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.12f, 1f)) },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _stylesBuilt = true;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var t = new Texture2D(w, h);
        t.SetPixels(pix);
        t.Apply();
        return t;
    }
}
#endif
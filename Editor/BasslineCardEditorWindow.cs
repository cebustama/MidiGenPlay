#if UNITY_EDITOR
using MidiGenPlay;
using MidiGenPlay.Authoring;
using MidiGenPlay.Composition;
using MidiGenPlay.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using SelfPocketStep = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketStep;
using SelfPocketSubdivision = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketSubdivision;
using SelfPocketBarSubstitution = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketBarSubstitution;
using SelfPocketPatternVariant = MidiGenPlay.Composition.BasslineCardConfigSO.SelfPocketPatternVariant;

/// <summary>
/// Package-owned Unity Editor window for authoring <see cref="BasslineCardConfigSO"/>
/// assets (MGP-BASSCARD-WIZARD-1).
///
/// Workflow (the validated package loop — normalize → preview → apply/save):
///   1. Assign a target card asset (or New Card to start fresh).
///   2. Edit card fields; author the SelfPocket body and PHRASE-1 substitution
///      table as text (BassPatternTextParser DSL: S P . - g G H L).
///   3. Validate &amp; Preview parses all buffers into the working copy and
///      surfaces warnings, advisories and the phrase plan.
///   4. Apply To Asset (overwrite) or Save As New Asset.
///
/// Contract notes:
///   - Whole-card editing over a working copy (D3=C): every field of the card
///     is edited here, but only the pattern surfaces get bespoke text UX; the
///     rest draw with default property drawers over the clone. One editing
///     surface, one Apply — no inspector round-trips mid-session.
///   - The asset is NEVER mutated until Apply / Save As (no silent writes).
///   - Text buffers are one-per-variant (D4=A): UI structure mirrors the
///     serialized structure; warnings locate (buffer, step) honestly.
///   - The preview meter (D5=A) is editor-only state, never serialized: the
///     card has no meter — the Part does. It seeds bar separators on render
///     and drives ADVISORY checks that mirror the runtime's own warnings
///     (non-divisor re-phase, SD-PH-1 table defects). The window never
///     blocks a save: runtime law degrades locally, authoring mirrors it.
///   - Preview is a plan preview (D6=C): phrase timeline + per-buffer class
///     counts. Audible preview stays in Composition Smoke (button below).
///   - This window owns ZERO runtime semantics: it writes enums the composer
///     already interprets. Authority: runtime/SSoT_Composer_Bass_Track.md
///     §3.7.x; PHRASE-1 semantics per its drafted §3.7.4.
/// </summary>
public class BasslineCardEditorWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string MenuPath = "MidiGenPlay/Bassline Card Editor...";
    private const string UndoApply = "Bassline Card Editor: Apply";
    private const string UndoSaveAsNew = "Bassline Card Editor: Save As New";

    /// <summary>Fields drawn bespoke below — excluded from the default-drawer pass.</summary>
    private static readonly string[] BespokeFields =
    {
        "m_Script",
        "selfPocketPattern",
        "selfPocketPhraseLengthBars",
        "selfPocketBarSubstitutions",
        "selfPocketVariantSelection",
    };

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var w = GetWindow<BasslineCardEditorWindow>("Bassline Card Editor");
        w.minSize = new Vector2(520f, 480f);
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    [SerializeField] private BasslineCardConfigSO targetAsset;

    // D12: existing PATTERN-PERSIST-1 store, new typeFolder. Save root:
    // Assets/Resources/ScriptableObjects/Patterns/Basslines. Cards under
    // Patterns/ is a recorded cosmetic misnomer — chosen over generalizing
    // the shared store's hardcoded root (zero changes to shared code).
    private readonly TrackPatternConfigStoreResources<BasslineCardConfigSO> _cardStore =
        new TrackPatternConfigStoreResources<BasslineCardConfigSO>("Basslines");

    private BasslineCardConfigSO _working;      // deep clone; DontSave
    private BasslineCardConfigSO _lastBound;
    private SerializedObject _workingSO;

    // Text buffers — editor UI state, never serialized into the asset.
    // Survive domain reload within the session; re-rendered from the asset on
    // rebind (drum-window policy: unsaved text is lost on rebind, documented).
    [SerializeField] private string _bodyBuffer = string.Empty;

    [Serializable]
    private class SubEntryBuffers
    {
        public int barIndex;
        public List<string> variants = new List<string>();
    }

    [SerializeField] private List<SubEntryBuffers> _subBuffers = new List<SubEntryBuffers>();

    // D5=A: editor-only preview meter. NOT serialized to the asset — the card
    // is meter-agnostic; this exists only for readability separators and
    // advisory divisor checks.
    [SerializeField] private TimeSignature _previewMeter = TimeSignature.FourFour;

    [SerializeField] private bool _showBrowse;
    [SerializeField] private bool _showCardFields = true;

    private readonly List<BassPatternTextWarning> _warnings = new List<BassPatternTextWarning>();
    private readonly List<string> _advisories = new List<string>();
    private string _planPreview = string.Empty;
    private Vector2 _scroll;

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------

    private void OnGUI()
    {
        // Rebind after domain reload (the DontSave clone dies; the asset ref survives).
        if (targetAsset != null && (_working == null || _lastBound != targetAsset))
            BindAsset(targetAsset);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawBindSection();

        if (_working == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a BasslineCardConfigSO or press New Card.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        DrawCardFieldsSection();
        EditorGUILayout.Space();
        DrawPatternSection();
        EditorGUILayout.Space();
        DrawPhraseSection();
        EditorGUILayout.Space();
        DrawValidateAndPreview();
        DrawWarningsPanel();
        EditorGUILayout.Space();
        DrawPersistenceSection();

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Bind / New / Browse
    // -------------------------------------------------------------------------

    private void DrawBindSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var picked = (BasslineCardConfigSO)EditorGUILayout.ObjectField(
                "Target Card", targetAsset, typeof(BasslineCardConfigSO), false);
            if (picked != targetAsset)
            {
                targetAsset = picked;
                BindAsset(targetAsset);
            }

            if (GUILayout.Button("New Card", GUILayout.Width(80)))
                CreateNewCard();
        }

        _showBrowse = EditorGUILayout.Foldout(_showBrowse, "Browse Saved Cards", true);
        if (_showBrowse)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Refresh List"))
                    _cardStore.Refresh();

                var saved = _cardStore.GetAll();
                if (saved.Count == 0)
                {
                    EditorGUILayout.LabelField(
                        $"No saved cards under {_cardStore.AssetsSaveRootPath}.");
                }
                else
                {
                    foreach (var a in saved)
                    {
                        if (a == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(a, typeof(BasslineCardConfigSO), false);
                            using (new EditorGUI.DisabledScope(a == targetAsset))
                            {
                                if (GUILayout.Button("Load", GUILayout.Width(52)))
                                {
                                    targetAsset = a;
                                    BindAsset(targetAsset);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void BindAsset(BasslineCardConfigSO asset)
    {
        _lastBound = asset;
        _warnings.Clear();
        _advisories.Clear();
        _planPreview = string.Empty;

        if (asset == null)
        {
            _working = null;
            _workingSO = null;
            _bodyBuffer = string.Empty;
            _subBuffers.Clear();
            Repaint();
            return;
        }

        _working = Instantiate(asset);
        _working.name = asset.name;
        _working.hideFlags = HideFlags.DontSave;
        _workingSO = new SerializedObject(_working);

        RenderWorkingIntoBuffers();
        Repaint();
    }

    private void CreateNewCard()
    {
        targetAsset = null;
        _lastBound = null;
        _warnings.Clear();
        _advisories.Clear();
        _planPreview = string.Empty;

        _working = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
        _working.name = "New Bassline Card";
        _working.hideFlags = HideFlags.DontSave;
        // CreateInstance does not invoke Reset(); tag the role explicitly,
        // matching what the Create-menu path would have done.
        _working.appliesTo = TrackRole.Bassline;
        _workingSO = new SerializedObject(_working);

        RenderWorkingIntoBuffers();
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Card fields (D3=C: default drawers over the working copy)
    // -------------------------------------------------------------------------

    private void DrawCardFieldsSection()
    {
        _showCardFields = EditorGUILayout.Foldout(
            _showCardFields, "Card Fields (articulation, pocket, tuning)", true);
        if (!_showCardFields) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _workingSO.Update();
            var prop = _workingSO.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (BespokeFields.Contains(prop.name)) continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            _workingSO.ApplyModifiedProperties();
        }
    }

    // -------------------------------------------------------------------------
    // Pattern section (body)
    // -------------------------------------------------------------------------

    private void DrawPatternSection()
    {
        EditorGUILayout.LabelField("Self Pocket Pattern (text)", EditorStyles.boldLabel);

        // Usability legend, not authority (drum-window HelpBox discipline).
        EditorGUILayout.HelpBox(
            "S slap · P pop · . or - rest · g ghost · G ghost-pop · H hammer-on · " +
            "L pull-off · | and whitespace ignored. Case matters. Length is free " +
            "(the composer cycles the pattern).",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            _previewMeter = (TimeSignature)EditorGUILayout.EnumPopup(
                new GUIContent("Preview Meter",
                    "Editor-only, never saved to the card. The card has no meter — " +
                    "the Part does. Used for | placement on render and advisory " +
                    "bar-divisor checks only."),
                _previewMeter);
            EditorGUILayout.LabelField(
                $"{StepsPerPreviewBar()} steps/bar at this meter + subdivision",
                EditorStyles.miniLabel, GUILayout.Width(230));
        }

        _bodyBuffer = EditorGUILayout.TextField("Body", _bodyBuffer);
    }

    // -------------------------------------------------------------------------
    // Phrase section (PHRASE-1 surface)
    // -------------------------------------------------------------------------

    private void DrawPhraseSection()
    {
        EditorGUILayout.LabelField("Phrase (PHRASE-1)", EditorStyles.boldLabel);

        _working.selfPocketPhraseLengthBars = Mathf.Max(1,
            EditorGUILayout.IntField("Phrase Length (bars)", _working.selfPocketPhraseLengthBars));

        _working.selfPocketVariantSelection =
            (BasslineCardConfigSO.SelfPocketVariantSelection)EditorGUILayout.EnumPopup(
                "Variant Selection", _working.selfPocketVariantSelection);

        if (_subBuffers.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Substitution table is EMPTY — phrase machinery OFF (the single " +
                "gate, D-PH-BYTE=A). The body cycles bar-blind, byte-identical to v1.",
                MessageType.None);
        }

        int removeEntry = -1;
        for (int i = 0; i < _subBuffers.Count; i++)
        {
            var entry = _subBuffers[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.barIndex = EditorGUILayout.IntField(
                        new GUIContent("Slot (bar index)",
                            "0-based slot within the phrase. length-1 is the " +
                            "phrase-closing fill slot."),
                        entry.barIndex);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        removeEntry = i;
                }

                int removeVariant = -1;
                for (int v = 0; v < entry.variants.Count; v++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        entry.variants[v] = EditorGUILayout.TextField(
                            $"Variant {v}", entry.variants[v]);
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                            removeVariant = v;
                    }
                }
                if (removeVariant >= 0)
                    entry.variants.RemoveAt(removeVariant);

                if (GUILayout.Button("+ Variant", GUILayout.Width(90)))
                    entry.variants.Add(string.Empty);
            }
        }
        if (removeEntry >= 0)
            _subBuffers.RemoveAt(removeEntry);

        if (GUILayout.Button("+ Substitution", GUILayout.Width(120)))
        {
            _subBuffers.Add(new SubEntryBuffers
            {
                // Canonical authoring: the phrase-closing fill slot.
                barIndex = Mathf.Max(0, _working.selfPocketPhraseLengthBars - 1),
                variants = new List<string> { string.Empty },
            });
        }
    }

    // -------------------------------------------------------------------------
    // Validate & preview (D6=C)
    // -------------------------------------------------------------------------

    private void DrawValidateAndPreview()
    {
        if (GUILayout.Button("Validate & Preview"))
            CommitBuffersToWorking();

        if (!string.IsNullOrEmpty(_planPreview))
            EditorGUILayout.HelpBox(_planPreview, MessageType.None);
    }

    private void DrawWarningsPanel()
    {
        foreach (var w in _warnings)
            EditorGUILayout.HelpBox(w.ToString(), MessageType.Warning);
        foreach (var a in _advisories)
            EditorGUILayout.HelpBox(a, MessageType.Info);
    }

    // -------------------------------------------------------------------------
    // Buffers ↔ working copy
    // -------------------------------------------------------------------------

    private int StepsPerBeat(SelfPocketSubdivision sub)
    {
        switch (sub)
        {
            case SelfPocketSubdivision.HalfBeat: return 2;
            case SelfPocketSubdivision.QuarterBeat: return 4;
            default: return 1;
        }
    }

    private int StepsPerPreviewBar()
    {
        int beats = GetTimeSignatureDetails(_previewMeter).BeatsPerMeasure;
        return beats * StepsPerBeat(_working.selfPocketSubdivision);
    }

    private void RenderWorkingIntoBuffers()
    {
        int spb = StepsPerPreviewBar();
        _bodyBuffer = BassPatternTextParser.Render(_working.selfPocketPattern, spb);

        _subBuffers.Clear();
        if (_working.selfPocketBarSubstitutions != null)
        {
            foreach (var sub in _working.selfPocketBarSubstitutions)
            {
                var entry = new SubEntryBuffers { barIndex = sub.barIndex };
                if (sub.variants != null)
                    foreach (var variant in sub.variants)
                        entry.variants.Add(BassPatternTextParser.Render(variant?.steps, spb));
                _subBuffers.Add(entry);
            }
        }
    }

    /// <summary>
    /// Parse every text buffer into the working copy, then refresh warnings,
    /// advisories and the plan preview. Called by Validate &amp; Preview and —
    /// unconditionally — before Apply / Save As (text is the pattern surface;
    /// there is no tab to switch out of).
    /// </summary>
    private void CommitBuffersToWorking()
    {
        _warnings.Clear();
        _advisories.Clear();

        _working.selfPocketPattern =
            BassPatternTextParser.Parse(_bodyBuffer, "body", _warnings);

        _working.selfPocketBarSubstitutions =
            new List<SelfPocketBarSubstitution>(_subBuffers.Count);
        foreach (var entry in _subBuffers)
        {
            var sub = new SelfPocketBarSubstitution { barIndex = entry.barIndex };
            for (int v = 0; v < entry.variants.Count; v++)
            {
                sub.variants.Add(new SelfPocketPatternVariant
                {
                    steps = BassPatternTextParser.Parse(
                        entry.variants[v], $"bar {entry.barIndex} / variant {v}", _warnings),
                });
            }
            _working.selfPocketBarSubstitutions.Add(sub);
        }

        ComputeAdvisories();
        _planPreview = BuildPlanPreview();
        Repaint();
    }

    /// <summary>
    /// ADVISORY checks mirroring runtime law — same conditions, same outcomes
    /// stated, never blocking. The window pre-warns what the composer would
    /// warn (non-divisor re-phase; SD-PH-1 local degradation) so the author
    /// hears about it before the render does. It adds no law of its own.
    /// </summary>
    private void ComputeAdvisories()
    {
        if (_working.pocketMode != BasslineCardConfigSO.PocketCouplingMode.SelfPocket)
            _advisories.Add(
                $"pocketMode is {_working.pocketMode}: the pattern and phrase " +
                "surfaces are inert at render until it is SelfPocket.");

        int spb = StepsPerPreviewBar();
        int len = _working.selfPocketPhraseLengthBars;

        CheckDivisor("body", _working.selfPocketPattern?.Count ?? 0, spb);

        var seenSlots = new HashSet<int>();
        foreach (var sub in _working.selfPocketBarSubstitutions)
        {
            if (!seenSlots.Add(sub.barIndex))
                _advisories.Add(
                    $"Duplicate slot {sub.barIndex}: at render the LAST entry " +
                    "wins and the runtime warns (SD-PH-1).");
            if (sub.barIndex < 0 || sub.barIndex >= len)
                _advisories.Add(
                    $"Slot {sub.barIndex} is outside 0..{len - 1}: inert at " +
                    "render, warned (SD-PH-1).");
            if (sub.variants.Count == 0)
                _advisories.Add(
                    $"Slot {sub.barIndex} has no variants: entry inert at " +
                    "render, warned (SD-PH-1).");

            for (int v = 0; v < sub.variants.Count; v++)
            {
                int vLen = sub.variants[v].steps?.Count ?? 0;
                if (vLen == 0)
                    _advisories.Add(
                        $"bar {sub.barIndex} / variant {v} is empty: dropped at " +
                        "render, warned (SD-PH-1). An all-rest variant " +
                        "('....') is the legal way to author a silent bar.");
                else
                    CheckDivisor($"bar {sub.barIndex} / variant {v}", vLen, spb);
            }
        }
    }

    private void CheckDivisor(string label, int patternLength, int stepsPerBar)
    {
        if (patternLength <= 0) return;
        if (stepsPerBar % patternLength != 0)
            _advisories.Add(
                $"{label}: length {patternLength} does not divide {stepsPerBar} " +
                $"steps/bar under the preview meter — with the phrase active the " +
                "pattern restarts every bar and the runtime warns once " +
                "(D-PH-INDEX=A). Advisory only: the Part's real meter decides.");
    }

    private string BuildPlanPreview()
    {
        var sb = new StringBuilder();
        sb.Append("Phrase plan (").Append(_working.selfPocketPhraseLengthBars)
          .Append(" bars, ").Append(_working.selfPocketVariantSelection).Append("):\n");

        var bySlot = new Dictionary<int, int>(); // slot -> variant count (LAST wins, SD-PH-1)
        foreach (var sub in _working.selfPocketBarSubstitutions)
            bySlot[sub.barIndex] = sub.variants.Count(v => (v.steps?.Count ?? 0) > 0);

        for (int slot = 0; slot < _working.selfPocketPhraseLengthBars; slot++)
        {
            sb.Append("  slot ").Append(slot).Append(": ");
            if (bySlot.TryGetValue(slot, out int count) && count > 0)
                sb.Append(count).Append(count == 1 ? " variant" : " variants");
            else
                sb.Append("body");
            sb.Append('\n');
        }

        sb.Append("Body classes: ").Append(ClassCounts(_working.selfPocketPattern));
        return sb.ToString();
    }

    private static string ClassCounts(IReadOnlyList<SelfPocketStep> steps)
    {
        if (steps == null || steps.Count == 0) return "(empty)";
        var counts = new Dictionary<SelfPocketStep, int>();
        foreach (var s in steps)
            counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
        return string.Join(" · ", counts
            .OrderBy(kv => (int)kv.Key)
            .Select(kv => $"{BassPatternTextParser.StepToGlyph(kv.Key)}×{kv.Value}"));
    }

    // -------------------------------------------------------------------------
    // Persistence (normalize → preview → apply/save; no silent writes)
    // -------------------------------------------------------------------------

    private void DrawPersistenceSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = targetAsset != null && _working != null;
            if (GUILayout.Button("Apply To Asset"))
                ApplyToAsset();

            GUI.enabled = _working != null;
            if (GUILayout.Button("Save As New Asset"))
                SaveAsNewAsset();
            GUI.enabled = true;
        }

        // D6=C convenience: audible preview lives where the part context lives.
        if (GUILayout.Button("Open Composition Smoke (audible preview)"))
            GetWindow<MidiGenPlay.EditorTools.CompositionSmokeWindow>();
    }

    private void ApplyToAsset()
    {
        if (targetAsset == null || _working == null) return;

        CommitBuffersToWorking();

        Undo.RecordObject(targetAsset, UndoApply);
        CopyWorkingInto(targetAsset);
        // PATTERN-PERSIST-1 — store owns SetDirty + SaveAssets + cache refresh.
        _cardStore.Save(targetAsset);

        BindAsset(targetAsset);
        Debug.Log($"[BasslineCardEditor] Applied to {AssetDatabase.GetAssetPath(targetAsset)}");
    }

    private void SaveAsNewAsset()
    {
        if (_working == null) return;

        CommitBuffersToWorking();

        // Ensure the canonical root exists so the dialog can default into it.
        Directory.CreateDirectory(_cardStore.AssetsSaveRootPath);
        AssetDatabase.Refresh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Bassline Card As…",
            string.IsNullOrEmpty(_working.name) ? "Bassline Card" : _working.name,
            "asset",
            "Choose where to save the new bassline card asset.",
            _cardStore.AssetsSaveRootPath);

        if (string.IsNullOrEmpty(path)) return;

        // PATTERN-PERSIST-1 / D6=C — window keeps the naming dialog; the store
        // owns the AssetDatabase write. Create at the chosen path, populate
        // under Undo, then Save() to flush field edits + refresh the cache.
        var newAsset = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
        _cardStore.PersistNewAtPath(newAsset, path);

        Undo.RecordObject(newAsset, UndoSaveAsNew);
        CopyWorkingInto(newAsset, Path.GetFileNameWithoutExtension(path));
        _cardStore.Save(newAsset);

        targetAsset = newAsset;
        BindAsset(targetAsset);
        Debug.Log($"[BasslineCardEditor] Saved new asset at {path}");
    }

    /// <summary>
    /// Whole-card copy via CopySerialized, name preserved. DELIBERATE deviation
    /// from the drum window's field-by-field CopyWorkingInto: the drum copies
    /// selectively because DrumPatternData mixes data with runtime helper
    /// state; the card is plain serialized data edited whole (D3=C), and a
    /// field-by-field copy would silently drift every time the card grows a
    /// field. CopySerialized copies every serialized field — present and
    /// future — in one law.
    /// </summary>
    private void CopyWorkingInto(BasslineCardConfigSO dst, string nameOverride = null)
    {
        string keepName = string.IsNullOrEmpty(nameOverride) ? dst.name : nameOverride;
        EditorUtility.CopySerialized(_working, dst);
        dst.name = keepName;
        dst.hideFlags = HideFlags.None; // never inherit the clone's DontSave
    }
}
#endif
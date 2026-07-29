#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MidiGenPlay; // MIDIInstrumentSO / InstrumentType live in this namespace

/// <summary>
/// Catalogue + management window for the MIDI instrument assets
/// (<c>MIDIInstrumentSO</c> and <c>MIDIPercussionInstrumentSO</c>).
///
/// INST-WIZ-1. Category rationale (D-W1=A): unlike the pattern catalogue
/// wizards (strictly read-only: discover -> filter -> inspect -> select), this
/// window also MANAGES assets — but editing is done by embedding the asset's
/// own inspector for exactly ONE target at a time, which:
///   - reuses the existing custom property drawers (soundfont/bank/patch
///     dropdowns) without duplicating them, and
///   - structurally sidesteps the known multi-object-editing bug where
///     selecting several instrument assets propagates one asset's values to
///     all of them (root cause lives in the drawers; not fixed here).
/// Create / Duplicate / Delete / Rename are AssetDatabase operations on whole
/// assets, not field mutations, so the normalize -> preview -> apply pipeline
/// of the pattern editors is deliberately NOT replicated (flat config data,
/// nothing derived to preview).
///
/// Export All dumps every visible serialized property of every listed asset
/// to CSV (file and/or clipboard), independent of this window knowing the
/// field names — the export is complete by construction.
/// </summary>
public sealed class MidiInstrumentCatalogueWizard : EditorWindow
{
    // ------------------------------------------------------------------ setup

    private const string PackageInstrumentsFolder =
        "Packages/com.claudiobustamante.midigenplay/Runtime/Resources/ScriptableObjects/MIDI Instruments";

    /// <summary>Optional project-local root (mirrors the repository's two-root
    /// model). Left empty by default; edit in the window toolbar.</summary>
    private string _extraScanFolder = "";

    private enum ViewMode { All, MelodicOnly, PercussionOnly }

    private enum SortMode { Name, Type, PatchIndex, Path }

    private sealed class Row
    {
        public MIDIInstrumentSO asset;
        public string path;
        public bool isPercussion;
        public string displayName;   // InstrumentName if found, else asset name
        public string typeLabel;     // instrument-type enum display, or "—"
        public string soundFont;     // soundfont label, or "—"
        public string bankName;
        public string patchName;
        public int patchIndex = -1;
        public int octaveMin = int.MinValue;
        public int octaveMax = int.MinValue;
        public float volume01 = float.NaN;
        public string searchBlob;    // lower-cased "prop=value" dump for search
        /// <summary>displayName -> stringified value, insertion-ordered.</summary>
        public List<KeyValuePair<string, string>> allProps = new();
    }

    private readonly List<Row> _rows = new();
    private ViewMode _viewMode = ViewMode.All;
    private SortMode _sortMode = SortMode.Name;
    private string _search = "";
    private string _typeFilter = "";      // empty = all
    private string[] _typeOptions = { "(all types)" };
    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private Row _selected;
    private UnityEditor.Editor _detailEditor;
    private string _renameField = "";
    private string _lastStatus = "";

    [MenuItem("MidiGenPlay/MIDI Instrument Catalogue Wizard...")]
    private static void Open()
    {
        var w = GetWindow<MidiInstrumentCatalogueWizard>("MIDI Instruments");
        w.minSize = new Vector2(980, 480);
        w.Rescan();
    }

    private void OnEnable()
    {
        if (_rows.Count == 0) Rescan();
    }

    private void OnDisable()
    {
        if (_detailEditor != null) DestroyImmediate(_detailEditor);
    }

    // ------------------------------------------------------------------ scan

    private void Rescan()
    {
        _rows.Clear();
        var folders = new List<string>();
        if (AssetDatabase.IsValidFolder(PackageInstrumentsFolder))
            folders.Add(PackageInstrumentsFolder);
        if (!string.IsNullOrWhiteSpace(_extraScanFolder) &&
            AssetDatabase.IsValidFolder(_extraScanFolder))
            folders.Add(_extraScanFolder);

        // Fall back to a global search if the hardcoded package folder moved.
        string[] guids = folders.Count > 0
            ? AssetDatabase.FindAssets("t:MIDIInstrumentSO", folders.ToArray())
            : AssetDatabase.FindAssets("t:MIDIInstrumentSO");

        foreach (var guid in guids.Distinct())
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<MIDIInstrumentSO>(path);
            if (asset == null) continue;
            _rows.Add(BuildRow(asset, path));
        }

        RebuildTypeOptions();
        SortRows();
        if (_selected != null)
            _selected = _rows.FirstOrDefault(r => r.asset == _selected.asset);
        _lastStatus = $"Scanned {_rows.Count} instrument asset(s).";
        Repaint();
    }

    private Row BuildRow(MIDIInstrumentSO asset, string path)
    {
        var row = new Row
        {
            asset = asset,
            path = path,
            isPercussion = asset is MIDIPercussionInstrumentSO,
            displayName = asset.name,
        };

        var so = new SerializedObject(asset);
        var blob = new StringBuilder();
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;
            string value = Stringify(it);
            row.allProps.Add(new KeyValuePair<string, string>(it.displayName, value));
            blob.Append(it.displayName).Append('=').Append(value).Append('\n');

            // Known-name columns (confirmed field names in the codebase).
            switch (it.name)
            {
                case "InstrumentName":
                    if (!string.IsNullOrEmpty(it.stringValue)) row.displayName = it.stringValue;
                    break;
                case "BankName": row.bankName = it.stringValue; break;
                case "PatchName": row.patchName = it.stringValue; break;
                case "PatchIndex": row.patchIndex = it.intValue; break;
                case "octaveMin": row.octaveMin = it.intValue; break;
                case "octaveMax": row.octaveMax = it.intValue; break;
                case "volume01": row.volume01 = it.floatValue; break;
            }

            // Unknown-name columns, resolved defensively (D-W1 note): the
            // instrument-type and soundfont field names are not confirmed in
            // the seam set, so match by name candidates first, then by
            // display-name fragment. Purely cosmetic columns — the embedded
            // inspector and the export never depend on this resolution.
            if (row.typeLabel == null &&
                it.propertyType == SerializedPropertyType.Enum &&
                (IsAnyName(it, "InstrumentType", "instrumentType", "type", "Type") ||
                 it.displayName.IndexOf("Instrument Type", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                row.typeLabel = EnumLabel(it);
            }
            if (row.soundFont == null &&
                (IsAnyName(it, "SelectedSoundFont", "selectedSoundFont", "soundFont", "SoundFont") ||
                 it.displayName.IndexOf("Sound Font", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                row.soundFont = Stringify(it);
            }
        }

        row.typeLabel ??= row.isPercussion ? "Percussion" : "—";
        row.soundFont ??= "—";
        blob.Append(path).Append('\n').Append(row.isPercussion ? "percussion" : "melodic");
        row.searchBlob = blob.ToString().ToLowerInvariant();
        return row;
    }

    private static bool IsAnyName(SerializedProperty p, params string[] names)
        => names.Any(n => string.Equals(p.name, n, StringComparison.Ordinal));

    private static string EnumLabel(SerializedProperty p)
    {
        var names = p.enumDisplayNames;
        int i = p.enumValueIndex;
        return (names != null && i >= 0 && i < names.Length) ? names[i] : p.intValue.ToString();
    }

    private static string Stringify(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.String: return p.stringValue ?? "";
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Float:
                return p.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
            case SerializedPropertyType.Enum: return EnumLabel(p);
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue != null ? p.objectReferenceValue.name : "(none)";
            default:
                if (p.isArray) return $"[{p.arraySize} item(s)]";
                return p.hasVisibleChildren ? "(complex)" : "";
        }
    }

    private void RebuildTypeOptions()
    {
        var types = _rows.Select(r => r.typeLabel)
                         .Where(t => !string.IsNullOrEmpty(t) && t != "—")
                         .Distinct().OrderBy(t => t).ToList();
        types.Insert(0, "(all types)");
        _typeOptions = types.ToArray();
        if (!_typeOptions.Contains(_typeFilter)) _typeFilter = "";
    }

    private void SortRows()
    {
        switch (_sortMode)
        {
            case SortMode.Name:
                _rows.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase)); break;
            case SortMode.Type:
                _rows.Sort((a, b) =>
                {
                    int c = string.Compare(a.typeLabel, b.typeLabel, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case SortMode.PatchIndex:
                _rows.Sort((a, b) =>
                {
                    int c = a.patchIndex.CompareTo(b.patchIndex);
                    return c != 0 ? c : string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case SortMode.Path:
                _rows.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase)); break;
        }
    }

    private IEnumerable<Row> FilteredRows()
    {
        string q = _search?.Trim().ToLowerInvariant() ?? "";
        foreach (var r in _rows)
        {
            if (_viewMode == ViewMode.MelodicOnly && r.isPercussion) continue;
            if (_viewMode == ViewMode.PercussionOnly && !r.isPercussion) continue;
            if (!string.IsNullOrEmpty(_typeFilter) && r.typeLabel != _typeFilter) continue;
            if (q.Length > 0 && !r.searchBlob.Contains(q)) continue;
            yield return r;
        }
    }

    // ------------------------------------------------------------------ GUI

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawDetail();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(_lastStatus))
            EditorGUILayout.HelpBox(_lastStatus, MessageType.None);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60))) Rescan();

        _viewMode = (ViewMode)EditorGUILayout.EnumPopup(_viewMode, EditorStyles.toolbarPopup, GUILayout.Width(110));

        int typeIdx = Mathf.Max(0, Array.IndexOf(_typeOptions, string.IsNullOrEmpty(_typeFilter) ? "(all types)" : _typeFilter));
        int newTypeIdx = EditorGUILayout.Popup(typeIdx, _typeOptions, EditorStyles.toolbarPopup, GUILayout.Width(120));
        _typeFilter = newTypeIdx <= 0 ? "" : _typeOptions[newTypeIdx];

        var newSort = (SortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarPopup, GUILayout.Width(90));
        if (newSort != _sortMode) { _sortMode = newSort; SortRows(); }

        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("New Melodic", EditorStyles.toolbarButton, GUILayout.Width(85)))
            CreateAsset(percussion: false);
        if (GUILayout.Button("New Percussion", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CreateAsset(percussion: true);
        if (GUILayout.Button("Export CSV (file)", EditorStyles.toolbarButton, GUILayout.Width(110)))
            ExportCsv(toClipboard: false);
        if (GUILayout.Button("Export CSV (clipboard)", EditorStyles.toolbarButton, GUILayout.Width(140)))
            ExportCsv(toClipboard: true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Extra scan folder:", GUILayout.Width(105));
        _extraScanFolder = EditorGUILayout.TextField(_extraScanFolder);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        var filtered = FilteredRows().ToList();
        EditorGUILayout.BeginVertical(GUILayout.Width(520));
        EditorGUILayout.LabelField($"{filtered.Count} / {_rows.Count} instruments", EditorStyles.miniBoldLabel);

        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(150));
        GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(85));
        GUILayout.Label("SF", EditorStyles.miniBoldLabel, GUILayout.Width(60));
        GUILayout.Label("Bank", EditorStyles.miniBoldLabel, GUILayout.Width(40));
        GUILayout.Label("Patch", EditorStyles.miniBoldLabel, GUILayout.Width(45));
        GUILayout.Label("Oct", EditorStyles.miniBoldLabel, GUILayout.Width(45));
        GUILayout.Label("Vol01", EditorStyles.miniBoldLabel, GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var r in filtered)
        {
            bool isSel = r == _selected;
            var style = isSel ? EditorStyles.helpBox : EditorStyles.textField;
            EditorGUILayout.BeginHorizontal(style);
            if (GUILayout.Button(r.displayName + (r.isPercussion ? "  [perc]" : ""),
                                 isSel ? EditorStyles.boldLabel : EditorStyles.label,
                                 GUILayout.Width(150)))
            {
                Select(r);
            }
            GUILayout.Label(r.typeLabel, GUILayout.Width(85));
            GUILayout.Label(r.soundFont, GUILayout.Width(60));
            GUILayout.Label(r.bankName ?? "", GUILayout.Width(40));
            GUILayout.Label(r.patchIndex >= 0 ? r.patchIndex.ToString() : "—", GUILayout.Width(45));
            GUILayout.Label(r.octaveMin != int.MinValue ? $"{r.octaveMin}-{r.octaveMax}" : "—", GUILayout.Width(45));
            GUILayout.Label(float.IsNaN(r.volume01) ? "—" : r.volume01.ToString("0.##"), GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void Select(Row r)
    {
        _selected = r;
        _renameField = r.asset != null ? r.asset.name : "";
        if (_detailEditor != null) { DestroyImmediate(_detailEditor); _detailEditor = null; }
        if (r.asset != null)
        {
            // SINGLE target by construction — never hand the editor a
            // multi-object array (the known copy-across bug lives there).
            _detailEditor = UnityEditor.Editor.CreateEditor(r.asset);
            EditorGUIUtility.PingObject(r.asset);
        }
        GUI.FocusControl(null);
    }

    private void DrawDetail()
    {
        EditorGUILayout.BeginVertical();
        if (_selected == null || _selected.asset == null)
        {
            EditorGUILayout.HelpBox("Select an instrument on the left to inspect / edit it here (one at a time).", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField(_selected.path, EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        _renameField = EditorGUILayout.TextField("Asset name", _renameField);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_renameField) || _renameField == _selected.asset.name))
        {
            if (GUILayout.Button("Rename", GUILayout.Width(70)))
            {
                string err = AssetDatabase.RenameAsset(_selected.path, _renameField.Trim());
                _lastStatus = string.IsNullOrEmpty(err)
                    ? $"Renamed to '{_renameField.Trim()}'."
                    : $"Rename failed: {err} (immutable package?)";
                Rescan();
            }
        }
        if (GUILayout.Button("Duplicate", GUILayout.Width(75))) DuplicateSelected();
        if (GUILayout.Button("Delete", GUILayout.Width(60))) DeleteSelected();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
        if (_detailEditor != null)
        {
            EditorGUI.BeginChangeCheck();
            _detailEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_selected.asset);
                // Refresh the cached row so list/search/export reflect the edit.
                int i = _rows.IndexOf(_selected);
                if (i >= 0)
                {
                    _rows[i] = BuildRow(_selected.asset, _selected.path);
                    _selected = _rows[i];
                    RebuildTypeOptions();
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // -------------------------------------------------------------- commands

    private void CreateAsset(bool percussion)
    {
        string folder = _selected != null
            ? Path.GetDirectoryName(_selected.path)?.Replace('\\', '/')
            : PackageInstrumentsFolder;
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            folder = PackageInstrumentsFolder;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            _lastStatus = $"Create failed: folder not found ('{folder}').";
            return;
        }

        ScriptableObject inst = percussion
            ? ScriptableObject.CreateInstance<MIDIPercussionInstrumentSO>()
            : ScriptableObject.CreateInstance<MIDIInstrumentSO>();
        string basePath = $"{folder}/New {(percussion ? "Percussion " : "")}MIDI Instrument.asset";
        string path = AssetDatabase.GenerateUniqueAssetPath(basePath);
        try
        {
            AssetDatabase.CreateAsset(inst, path);
            AssetDatabase.SaveAssets();
            _lastStatus = $"Created '{path}'.";
        }
        catch (Exception e)
        {
            _lastStatus = $"Create failed: {e.Message} (immutable package?)";
            DestroyImmediate(inst);
            return;
        }
        Rescan();
        Select(_rows.FirstOrDefault(r => r.path == path));
    }

    private void DuplicateSelected()
    {
        if (_selected == null) return;
        string dst = AssetDatabase.GenerateUniqueAssetPath(_selected.path);
        if (AssetDatabase.CopyAsset(_selected.path, dst))
        {
            _lastStatus = $"Duplicated to '{dst}'.";
            Rescan();
            Select(_rows.FirstOrDefault(r => r.path == dst));
        }
        else _lastStatus = "Duplicate failed (immutable package?).";
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        if (!EditorUtility.DisplayDialog("Delete instrument asset",
                $"Delete '{_selected.path}'?\nThis cannot be undone.", "Delete", "Cancel"))
            return;
        if (AssetDatabase.DeleteAsset(_selected.path))
        {
            _lastStatus = $"Deleted '{_selected.path}'.";
            _selected = null;
            if (_detailEditor != null) { DestroyImmediate(_detailEditor); _detailEditor = null; }
            Rescan();
        }
        else _lastStatus = "Delete failed (immutable package?).";
    }

    // ---------------------------------------------------------------- export

    /// <summary>
    /// CSV over the CURRENT filter. Columns = assetPath, className, then the
    /// union of every visible serialized property display name across the
    /// exported assets (stable first-seen order). Values are stringified as in
    /// the list view; lists are summarized as "[n item(s)]".
    /// </summary>
    private void ExportCsv(bool toClipboard)
    {
        var rows = FilteredRows().ToList();
        if (rows.Count == 0) { _lastStatus = "Nothing to export (filter matches 0 assets)."; return; }

        var columns = new List<string>();
        foreach (var r in rows)
            foreach (var kv in r.allProps)
                if (!columns.Contains(kv.Key)) columns.Add(kv.Key);

        var sb = new StringBuilder();
        sb.Append("assetPath,className");
        foreach (var c in columns) sb.Append(',').Append(Csv(c));
        sb.Append('\n');
        foreach (var r in rows)
        {
            sb.Append(Csv(r.path)).Append(',').Append(Csv(r.asset.GetType().Name));
            foreach (var c in columns)
            {
                string v = r.allProps.FirstOrDefault(kv => kv.Key == c).Value ?? "";
                sb.Append(',').Append(Csv(v));
            }
            sb.Append('\n');
        }

        if (toClipboard)
        {
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _lastStatus = $"Copied CSV for {rows.Count} asset(s) to the clipboard.";
        }
        else
        {
            string file = EditorUtility.SaveFilePanel("Export instrument catalogue CSV",
                "", "midi_instrument_catalogue.csv", "csv");
            if (string.IsNullOrEmpty(file)) return;
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            _lastStatus = $"Exported {rows.Count} asset(s) to '{file}'.";
        }
    }

    private static string Csv(string s)
    {
        s ??= "";
        bool needsQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        if (!needsQuote) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
#endif
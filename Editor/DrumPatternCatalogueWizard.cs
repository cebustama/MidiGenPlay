#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Standards;
using UnityEditor;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

/// <summary>
/// Read-only catalogue browser for drum-pattern assets and palettes.
/// Scans configured folders, lets designers filter by structural/derived metadata
/// (time signature, measures, subdivisions, instruments used, active-step density),
/// and selecting a row pings/selects the asset in the Project/Inspector.
///
/// Structural mirror of <c>ChordProgressionCatalogueWizard</c>. Catalogue tools own
/// discover -> filter -> inspect -> select; they never mutate assets and never
/// duplicate the authoring (normalize -> preview -> apply/save) pipeline.
/// </summary>
public sealed class DrumPatternCatalogueWizard : EditorWindow
{
    // Default scan roots point at the live Patterns/ tree (not the older chord-style
    // top-level folder). Palettes folder is created on first palette save by the editor.
    private const string DefaultPatternsFolder = "Assets/Resources/ScriptableObjects/Patterns/Drums";
    private const string DefaultPalettesFolder = "Assets/Resources/ScriptableObjects/Patterns/Drums/Palettes";

    private enum AssetViewMode
    {
        All,
        PatternsOnly,
        PalettesOnly
    }

    private enum SortMode
    {
        Name,
        Path,
        Measures,
        TimeSignature,
        EntryCount,
        StepDensity
    }

    private sealed class PatternRow
    {
        public DrumPatternData asset;
        public string path;
        public string displayName;
        public string searchBlob;
        public HashSet<GeneralMidiPercussion> instruments;
        public int laneCount;
        public int activeSteps;
        public int totalStepCapacity;   // TotalSteps * laneCount
        public float density;           // activeSteps / totalStepCapacity (0..1)
    }

    private sealed class PaletteRow
    {
        public DrumPatternPaletteSO asset;
        public string path;
        public string displayName;
        public string searchBlob;
        public int entryCount;
        public List<PatternRow> patternRows;
        public HashSet<TimeSignature> timeSignatures;
        public HashSet<GeneralMidiPercussion> instruments;
        public int minMeasures;
        public int maxMeasures;
        public int minSubdivisions;
        public int maxSubdivisions;
        public float maxDensity;
    }

    [MenuItem("MidiGenPlay/Drum Pattern Catalogue Wizard...")]
    public static void Open()
    {
        GetWindow<DrumPatternCatalogueWizard>("Drum Pattern Catalogue");
    }

    [SerializeField] private List<string> scanFolders = new();
    [SerializeField] private string searchText = "";
    [SerializeField] private AssetViewMode assetViewMode = AssetViewMode.All;
    [SerializeField] private SortMode sortMode = SortMode.Name;
    [SerializeField] private bool sortDescending = false;

    [SerializeField] private bool filterByTimeSignature = false;
    [SerializeField] private TimeSignature timeSignatureFilter = TimeSignature.FourFour;

    [SerializeField] private int minMeasures = 0;
    [SerializeField] private int maxMeasures = 0;
    [SerializeField] private int minSubdivisions = 0;
    [SerializeField] private int maxSubdivisions = 0;

    [SerializeField] private bool filterByInstrument = false;
    [SerializeField] private GeneralMidiPercussion instrumentFilter = GeneralMidiPercussion.AcousticBassDrum;

    [SerializeField] private bool showPatternResults = true;
    [SerializeField] private bool showPaletteResults = true;
    [SerializeField] private bool showFilters = true;
    [SerializeField] private bool showFolders = false;

    [SerializeField] private Vector2 mainScroll;
    [SerializeField] private Vector2 patternScroll;
    [SerializeField] private Vector2 paletteScroll;

    private readonly List<PatternRow> allPatterns = new();
    private readonly List<PaletteRow> allPalettes = new();
    private List<PatternRow> filteredPatterns = new();
    private List<PaletteRow> filteredPalettes = new();

    private GUIStyle wrapMiniLabel;
    private GUIStyle headerStyle;
    private bool hasScanned;
    private string scanStatus = "Click Refresh to scan the configured folders.";

    private void OnEnable()
    {
        if (scanFolders == null || scanFolders.Count == 0)
            scanFolders = new List<string> { DefaultPatternsFolder, DefaultPalettesFolder };

        EnsureStyles();

        if (!hasScanned)
            RefreshCatalogue();
    }

    private void EnsureStyles()
    {
        wrapMiniLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            richText = true
        };

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };
    }

    private void OnGUI()
    {
        EnsureStyles();

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        DrawHeader();
        EditorGUILayout.Space(4);
        DrawFoldersSection();
        EditorGUILayout.Space(4);
        DrawFiltersSection();
        EditorGUILayout.Space(8);
        DrawResultsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Drum Pattern Catalogue Wizard", headerStyle);
            EditorGUILayout.LabelField(
                "Read-only browser for DrumPatternData and DrumPatternPaletteSO assets.",
                wrapMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                assetViewMode = (AssetViewMode)EditorGUILayout.EnumPopup(
                    new GUIContent("View", "Choose whether to show patterns, palettes, or both."),
                    assetViewMode);

                sortMode = (SortMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Sort", "Sort the visible results by a simple key."),
                    sortMode);

                sortDescending = EditorGUILayout.ToggleLeft("Desc", sortDescending, GUILayout.Width(55));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                    RefreshCatalogue();
            }

            EditorGUILayout.HelpBox(scanStatus, MessageType.Info);
        }
    }

    private void DrawFoldersSection()
    {
        showFolders = EditorGUILayout.BeginFoldoutHeaderGroup(showFolders, "Scan Folders");
        if (!showFolders)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Scanned recursively with AssetDatabase.FindAssets(...). " +
                "If no valid folders are listed, the whole project is scanned.",
                wrapMiniLabel);

            int removeIndex = -1;
            for (int i = 0; i < scanFolders.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    scanFolders[i] = EditorGUILayout.TextField(scanFolders[i]);

                    if (GUILayout.Button("...", GUILayout.Width(30)))
                    {
                        string picked = EditorUtility.OpenFolderPanel("Select Scan Folder", "Assets", "");
                        if (!string.IsNullOrEmpty(picked))
                        {
                            if (picked.StartsWith(Application.dataPath))
                                scanFolders[i] = "Assets" + picked.Substring(Application.dataPath.Length);
                            else
                                EditorUtility.DisplayDialog(
                                    "Outside Assets",
                                    "Pick a folder inside the project's Assets directory.",
                                    "OK");
                        }
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24)))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0 && removeIndex < scanFolders.Count)
                scanFolders.RemoveAt(removeIndex);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Folder"))
                    scanFolders.Add("Assets/");

                if (GUILayout.Button("Reset Defaults"))
                    scanFolders = new List<string> { DefaultPatternsFolder, DefaultPalettesFolder };
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawFiltersSection()
    {
        showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Filters");
        if (!showFilters)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();

            searchText = EditorGUILayout.TextField(
                new GUIContent("Search", "Matches asset name, display name, path, and palette notes."),
                searchText);

            using (new EditorGUILayout.HorizontalScope())
            {
                filterByTimeSignature = EditorGUILayout.ToggleLeft("Filter by TS", filterByTimeSignature, GUILayout.Width(95));
                using (new EditorGUI.DisabledScope(!filterByTimeSignature))
                    timeSignatureFilter = (TimeSignature)EditorGUILayout.EnumPopup(timeSignatureFilter);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                minMeasures = EditorGUILayout.IntField(new GUIContent("Min Measures", "0 = ignore."), minMeasures);
                maxMeasures = EditorGUILayout.IntField(new GUIContent("Max Measures", "0 = ignore."), maxMeasures);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                minSubdivisions = EditorGUILayout.IntField(new GUIContent("Min Subdivisions", "0 = ignore."), minSubdivisions);
                maxSubdivisions = EditorGUILayout.IntField(new GUIContent("Max Subdivisions", "0 = ignore."), maxSubdivisions);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                filterByInstrument = EditorGUILayout.ToggleLeft("Filter by Instrument", filterByInstrument, GUILayout.Width(140));
                using (new EditorGUI.DisabledScope(!filterByInstrument))
                    instrumentFilter = (GeneralMidiPercussion)EditorGUILayout.EnumPopup(instrumentFilter);
            }

            if (EditorGUI.EndChangeCheck())
                ApplyFilters();

            if (GUILayout.Button("Clear Filters"))
                ClearFilters();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawResultsSection()
    {
        int patCount = filteredPatterns?.Count ?? 0;
        int palCount = filteredPalettes?.Count ?? 0;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"Results: {patCount} pattern(s), {palCount} palette(s)",
                headerStyle);

            if (assetViewMode == AssetViewMode.All || assetViewMode == AssetViewMode.PatternsOnly)
            {
                showPatternResults = EditorGUILayout.BeginFoldoutHeaderGroup(
                    showPatternResults, $"DrumPatternData ({patCount})");

                if (showPatternResults)
                {
                    patternScroll = EditorGUILayout.BeginScrollView(patternScroll, GUILayout.MinHeight(180));
                    if (patCount == 0)
                        EditorGUILayout.HelpBox("No pattern assets matched the current filters.", MessageType.None);
                    else
                        foreach (var row in filteredPatterns)
                            DrawPatternRow(row);
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            if (assetViewMode == AssetViewMode.All || assetViewMode == AssetViewMode.PalettesOnly)
            {
                showPaletteResults = EditorGUILayout.BeginFoldoutHeaderGroup(
                    showPaletteResults, $"DrumPatternPaletteSO ({palCount})");

                if (showPaletteResults)
                {
                    paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.MinHeight(180));
                    if (palCount == 0)
                        EditorGUILayout.HelpBox("No palette assets matched the current filters.", MessageType.None);
                    else
                        foreach (var row in filteredPalettes)
                            DrawPaletteRow(row);
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
    }

    private void DrawPatternRow(PatternRow row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(row.displayName, EditorStyles.linkLabel))
                    SelectAsset(row.asset);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(row.asset.TimeSignature.ToString(), GUILayout.Width(90));
                EditorGUILayout.LabelField($"{row.asset.Measures} bars", GUILayout.Width(55));
                EditorGUILayout.LabelField($"sub x{Mathf.Max(1, row.asset.subdivisions)}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{row.density * 100f:0}% dense", GUILayout.Width(75));
            }

            EditorGUILayout.LabelField($"Path: {row.path}", wrapMiniLabel);
            EditorGUILayout.LabelField(
                $"Lanes: {row.laneCount} | Active steps: {row.activeSteps}/{row.totalStepCapacity} | " +
                $"Instruments: {FormatInstruments(row.instruments)}",
                wrapMiniLabel);
        }
    }

    private void DrawPaletteRow(PaletteRow row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(row.displayName, EditorStyles.linkLabel))
                    SelectAsset(row.asset);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"entries {row.entryCount}", GUILayout.Width(65));
                EditorGUILayout.LabelField($"TS {FormatTimeSignatureSet(row.timeSignatures)}", GUILayout.Width(150));
            }

            EditorGUILayout.LabelField($"Path: {row.path}", wrapMiniLabel);
            EditorGUILayout.LabelField(
                $"Measures: {FormatRange(row.minMeasures, row.maxMeasures)} | " +
                $"Subdivisions: {FormatRange(row.minSubdivisions, row.maxSubdivisions)} | " +
                $"Max density: {row.maxDensity * 100f:0}%",
                wrapMiniLabel);

            EditorGUILayout.LabelField($"Instruments: {FormatInstruments(row.instruments)}", wrapMiniLabel);

            if (!string.IsNullOrWhiteSpace(row.asset.paletteNotes))
                EditorGUILayout.LabelField($"Notes: {row.asset.paletteNotes}", wrapMiniLabel);
        }
    }

    // -------------------------------------------------------------------------
    // Scan
    // -------------------------------------------------------------------------

    private void RefreshCatalogue()
    {
        allPatterns.Clear();
        allPalettes.Clear();

        var validFolders = GetValidFolders();

        string[] patternGuids = validFolders.Count > 0
            ? AssetDatabase.FindAssets("t:DrumPatternData", validFolders.ToArray())
            : AssetDatabase.FindAssets("t:DrumPatternData");

        foreach (string guid in patternGuids.Distinct())
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<DrumPatternData>(path);
            if (asset == null)
                continue;
            allPatterns.Add(BuildPatternRow(asset, path));
        }

        string[] paletteGuids = validFolders.Count > 0
            ? AssetDatabase.FindAssets("t:DrumPatternPaletteSO", validFolders.ToArray())
            : AssetDatabase.FindAssets("t:DrumPatternPaletteSO");

        foreach (string guid in paletteGuids.Distinct())
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<DrumPatternPaletteSO>(path);
            if (asset == null)
                continue;
            allPalettes.Add(BuildPaletteRow(asset, path));
        }

        hasScanned = true;
        scanStatus = $"Scanned {allPatterns.Count} pattern(s) and {allPalettes.Count} palette(s).";
        ApplyFilters();
        Repaint();
    }

    private List<string> GetValidFolders()
    {
        var valid = new List<string>();
        if (scanFolders == null)
            return valid;

        foreach (var raw in scanFolders)
        {
            string folder = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(folder))
                continue;
            if (AssetDatabase.IsValidFolder(folder))
                valid.Add(folder);
        }

        return valid.Distinct().ToList();
    }

    private PatternRow BuildPatternRow(DrumPatternData asset, string path)
    {
        var instruments = new HashSet<GeneralMidiPercussion>();
        int active = 0;
        int laneCount = asset.lanes?.Count ?? 0;

        if (asset.lanes != null)
        {
            foreach (var lane in asset.lanes)
            {
                if (lane == null) continue;
                instruments.Add(lane.instrument);
                if (lane.steps == null) continue;
                foreach (var s in lane.steps)
                    if (s.active) active++;
            }
        }

        int capacity = Mathf.Max(1, asset.TotalSteps) * Mathf.Max(1, laneCount);
        float density = capacity > 0 ? (float)active / capacity : 0f;

        string display = !string.IsNullOrWhiteSpace(asset.DisplayName) ? asset.DisplayName : asset.name;

        return new PatternRow
        {
            asset = asset,
            path = path,
            displayName = display,
            searchBlob = BuildSearchBlob(display, asset.name, path, asset.DisplayName),
            instruments = instruments,
            laneCount = laneCount,
            activeSteps = active,
            totalStepCapacity = capacity,
            density = density
        };
    }

    private PaletteRow BuildPaletteRow(DrumPatternPaletteSO asset, string path)
    {
        var patternRows = new List<PatternRow>();
        var timeSignatures = new HashSet<TimeSignature>();
        var instruments = new HashSet<GeneralMidiPercussion>();

        int minMeasures = int.MaxValue, maxMeasures = 0;
        int minSub = int.MaxValue, maxSub = 0;
        float maxDensity = 0f;

        if (asset.entries != null)
        {
            foreach (var entry in asset.entries)
            {
                if (entry?.pattern == null) continue;
                string progPath = AssetDatabase.GetAssetPath(entry.pattern);
                var pr = BuildPatternRow(entry.pattern, progPath);
                patternRows.Add(pr);

                timeSignatures.Add(entry.pattern.TimeSignature);
                foreach (var inst in pr.instruments) instruments.Add(inst);

                int m = Mathf.Max(1, entry.pattern.Measures);
                minMeasures = Mathf.Min(minMeasures, m);
                maxMeasures = Mathf.Max(maxMeasures, m);

                int sub = Mathf.Max(1, entry.pattern.subdivisions);
                minSub = Mathf.Min(minSub, sub);
                maxSub = Mathf.Max(maxSub, sub);

                maxDensity = Mathf.Max(maxDensity, pr.density);
            }
        }

        if (patternRows.Count == 0)
        {
            minMeasures = 0; maxMeasures = 0; minSub = 0; maxSub = 0;
        }

        string display = !string.IsNullOrWhiteSpace(asset.paletteDisplayName)
            ? asset.paletteDisplayName
            : asset.name;

        return new PaletteRow
        {
            asset = asset,
            path = path,
            displayName = display,
            searchBlob = BuildSearchBlob(display, asset.name, path, asset.paletteNotes),
            entryCount = asset.entries?.Count ?? 0,
            patternRows = patternRows,
            timeSignatures = timeSignatures,
            instruments = instruments,
            minMeasures = minMeasures,
            maxMeasures = maxMeasures,
            minSubdivisions = minSub,
            maxSubdivisions = maxSub,
            maxDensity = maxDensity
        };
    }

    private static string BuildSearchBlob(params string[] parts)
        => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();

    // -------------------------------------------------------------------------
    // Filter / sort
    // -------------------------------------------------------------------------

    private void ApplyFilters()
    {
        filteredPatterns = SortPatterns(allPatterns.Where(MatchesPatternFilters)).ToList();
        filteredPalettes = SortPalettes(allPalettes.Where(MatchesPaletteFilters)).ToList();
    }

    private IEnumerable<PatternRow> SortPatterns(IEnumerable<PatternRow> query)
    {
        Func<PatternRow, object> key = sortMode switch
        {
            SortMode.Path => row => row.path,
            SortMode.Measures => row => row.asset.Measures,
            SortMode.TimeSignature => row => row.asset.TimeSignature,
            SortMode.StepDensity => row => row.density,
            _ => row => row.displayName
        };
        return sortDescending ? query.OrderByDescending(key) : query.OrderBy(key);
    }

    private IEnumerable<PaletteRow> SortPalettes(IEnumerable<PaletteRow> query)
    {
        Func<PaletteRow, object> key = sortMode switch
        {
            SortMode.Path => row => row.path,
            SortMode.Measures => row => row.maxMeasures,
            SortMode.TimeSignature => row => row.timeSignatures.Count > 0
                ? row.timeSignatures.OrderBy(ts => ts.ToString()).First()
                : 0,
            SortMode.EntryCount => row => row.entryCount,
            SortMode.StepDensity => row => row.maxDensity,
            _ => row => row.displayName
        };
        return sortDescending ? query.OrderByDescending(key) : query.OrderBy(key);
    }

    private bool MatchesPatternFilters(PatternRow row)
    {
        if (row == null || row.asset == null)
            return false;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string s = searchText.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(row.searchBlob) || !row.searchBlob.Contains(s))
                return false;
        }

        if (filterByTimeSignature && row.asset.TimeSignature != timeSignatureFilter)
            return false;

        if (minMeasures > 0 && row.asset.Measures < minMeasures) return false;
        if (maxMeasures > 0 && row.asset.Measures > maxMeasures) return false;

        int sub = Mathf.Max(1, row.asset.subdivisions);
        if (minSubdivisions > 0 && sub < minSubdivisions) return false;
        if (maxSubdivisions > 0 && sub > maxSubdivisions) return false;

        if (filterByInstrument && !row.instruments.Contains(instrumentFilter))
            return false;

        return true;
    }

    private bool MatchesPaletteFilters(PaletteRow row)
    {
        if (row == null || row.asset == null)
            return false;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string s = searchText.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(row.searchBlob) || !row.searchBlob.Contains(s))
                return false;
        }

        if (row.patternRows == null || row.patternRows.Count == 0)
        {
            // Empty palettes only survive when no structural filters are active.
            return !filterByTimeSignature
                && minMeasures <= 0
                && maxMeasures <= 0
                && minSubdivisions <= 0
                && maxSubdivisions <= 0
                && !filterByInstrument;
        }

        return row.patternRows.Any(MatchesPatternFilters);
    }

    private void ClearFilters()
    {
        searchText = string.Empty;
        filterByTimeSignature = false;
        minMeasures = 0;
        maxMeasures = 0;
        minSubdivisions = 0;
        maxSubdivisions = 0;
        filterByInstrument = false;
        ApplyFilters();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void SelectAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static string FormatInstruments(IEnumerable<GeneralMidiPercussion> instruments)
    {
        if (instruments == null)
            return "-";
        var list = instruments.Select(i => i.ToString()).OrderBy(s => s).ToList();
        return list.Count == 0 ? "-" : string.Join(", ", list);
    }

    private static string FormatTimeSignatureSet(IEnumerable<TimeSignature> set)
    {
        if (set == null)
            return "-";
        var list = set.Select(ts => ts.ToString()).OrderBy(s => s).ToList();
        return list.Count == 0 ? "-" : string.Join(", ", list);
    }

    private static string FormatRange(int min, int max)
    {
        if (min <= 0 && max <= 0)
            return "-";
        return min == max ? min.ToString() : $"{min}–{max}";
    }
}
#endif
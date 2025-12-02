using Melanchall.DryWetMidi.Standards; // GeneralMidiPercussion
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.UI
{
    /// <summary>
    /// Minimal controller for drum pattern authoring.
    /// - Edits a runtime clone of DrumPatternData (runtime-first editing).
    /// - Paints a multi-row PatternGrid; click/toggle = hit on/off.
    /// - Save overwrites original asset; SaveAs creates a new local asset.
    /// </summary>
    public class RhythmPatternPanelController : MonoBehaviour
    {
        private static readonly GeneralMidiPercussion[] CommonPerc =
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
        private int _nextCommonIndex = 0;

        [Header("References")]
        [SerializeField] private ScrollRect headersScroll;
        [SerializeField] private float rowHeight = 56f;
        [SerializeField] private RectTransform percussionElementList;
        [SerializeField] private RectTransform buttonsRoot; // +/- container
        [SerializeField] private RhythmRowHeader rowHeaderPrefab;
        [SerializeField] private Button addLaneButton;
        [SerializeField] private Button removeLaneButton;
        [SerializeField] private PatternGrid grid;
        
        private ScrollRect gridScroll;
        private bool _syncingScroll;

        [Header("Data (debug/preview)")]
        [SerializeField] private DrumPatternData patternPreview;

        private DrumPatternData originalAsset;
        private DrumPatternData runtime;

        private readonly List<RhythmRowHeader> headers = new();

        public DrumPatternData GetRuntime() => runtime;
        public DrumPatternData GetOriginalAsset() => originalAsset;

        private UnityEngine.Events.UnityAction<Vector2> _gridScrollHandler;
        private UnityEngine.Events.UnityAction<Vector2> _headersScrollHandler;

        public event Action PatternChanged;

        // --- Lifecycle ---
        private void Awake()
        {
            if (grid != null)
            {
                grid.OnCellToggled += HandleCellToggled;
                grid.OnCellClicked += HandleCellClicked;
                grid.OnRebuilt += SyncHeaderHeightsToGrid;
            }
            if (addLaneButton != null) addLaneButton.onClick.AddListener(AddLaneFromCommonList);
            if (removeLaneButton != null) removeLaneButton.onClick.AddListener(RemoveLastLaneUI);
        }

        private void Start()
        {
            gridScroll = grid != null ? grid.GetComponent<ScrollRect>() : null;

            if (gridScroll != null && headersScroll != null)
            {
                _gridScrollHandler = v =>
                {
                    if (_syncingScroll) return;
                    _syncingScroll = true;
                    headersScroll.verticalNormalizedPosition = v.y;
                    _syncingScroll = false;
                };
                gridScroll.onValueChanged.AddListener(_gridScrollHandler);

                _headersScrollHandler = v =>
                {
                    if (_syncingScroll) return;
                    _syncingScroll = true;
                    gridScroll.verticalNormalizedPosition = v.y;
                    _syncingScroll = false;
                };
                headersScroll.onValueChanged.AddListener(_headersScrollHandler);
            }
        }

        private void HandleCellClicked(int row, int step)
        {
            Debug.Log($"[RhythmGrid] Click r={row} s={step} (was {grid.GetCell(row, step)})");
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.OnCellToggled -= HandleCellToggled;
                grid.OnCellClicked -= HandleCellClicked;
                grid.OnRebuilt -= SyncHeaderHeightsToGrid;
            }

            if (addLaneButton != null) 
                addLaneButton.onClick.RemoveListener(AddLaneFromCommonList);
            if (removeLaneButton != null) 
                removeLaneButton.onClick.RemoveListener(RemoveLastLaneUI);

            if (gridScroll != null && _gridScrollHandler != null)
                gridScroll.onValueChanged.RemoveListener(_gridScrollHandler);
            if (headersScroll != null && _headersScrollHandler != null)
                headersScroll.onValueChanged.RemoveListener(_headersScrollHandler);
        }

        // --- Public API ---

        /// <summary>Bind to an existing asset. A deep runtime clone is created and edited.</summary>
        public void Bind(DrumPatternData data)
        {
            originalAsset = data;
            runtime = data != null ? data.DeepCloneRuntime()
                                   : ScriptableObject.CreateInstance<DrumPatternData>();
            runtime.InitializeIfEmpty();

            // ensure grid is sized to runtime’s own signature
            SetSignature(runtime.beatsPerMeasure, runtime.Measures, runtime.subdivisions);

            patternPreview = runtime;
            RebuildGridFromRuntime();
        }

        /// <summary>Create a fresh runtime pattern (not tied to an asset yet).</summary>
        public void CreateNewRuntime(TimeSignature ts, int measures, int subdivisions = 1)
        {
            originalAsset = null;
            runtime = ScriptableObject.CreateInstance<DrumPatternData>();
            SetSignature(ts, measures, subdivisions);

            patternPreview = runtime;
            RebuildGridFromRuntime();
        }

        public void SetSignature(TimeSignature ts, int measures, int subdivisions = 1)
        {
            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            SetSignature(beats, measures, subdivisions);
        }

        /// <summary>Change time signature / measures and keep the runtime safe.</summary>
        public void SetSignature(int beatsPerMeasure, int measures, int subdivisions = 1)
        {
            if (runtime == null) return;
            runtime.SetSignature(beatsPerMeasure, measures, subdivisions);
            RebuildGridFromRuntime();

            PatternChanged?.Invoke();
        }

        /// <summary>Save current runtime contents back into the original asset (overwrite).</summary>
        public void SaveRuntimeIntoAsset()
        {
#if UNITY_EDITOR
            if (originalAsset == null || runtime == null) return;
            CopyRuntimeInto(originalAsset);                       // <-- use helper
            UnityEditor.EditorUtility.SetDirty(originalAsset);
            UnityEditor.AssetDatabase.SaveAssets();
            Bind(originalAsset);
#endif
        }

        /// <summary>
        /// Save runtime as a NEW asset (always to local folder). Returns the created asset.
        /// Pass a folder from settings (e.g., settings.GetDrumWriteFolder()).
        /// </summary>
        public DrumPatternData SaveRuntimeAsNewAsset(string folderPath = null)
        {
#if UNITY_EDITOR
            if (runtime == null) return null;
            string folder = string.IsNullOrWhiteSpace(folderPath)
                ? "Assets/Resources/ScriptableObjects/Patterns/Drums"
                : folderPath;
            System.IO.Directory.CreateDirectory(folder);
            UnityEditor.AssetDatabase.Refresh();

            string name = SanitizeFileName(BuildKeyName());
            string candidate = $"{folder}/{name}.asset";
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(candidate);

            var asset = ScriptableObject.CreateInstance<DrumPatternData>();
            CopyRuntimeInto(asset, name);                         // <-- use helper

            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorUtility.SetDirty(asset);

            Bind(asset);
            Debug.Log($"[RhythmPanel] Created new DrumPattern asset at {path}");
            return asset;
#else
    return null;
#endif
        }

        // --- Grid <-> Runtime ---

        private void RebuildGridFromRuntime()
        {
            if (grid == null || runtime == null) return;

            runtime.EnsureSizes();

            int rows = Mathf.Max(1, runtime.lanes?.Count ?? 0);
            grid.Build(
                rows: rows,
                measures: runtime.Measures,
                beatsPerMeasure: runtime.beatsPerMeasure,
                subdivisions: runtime.subdivisions,
                initialState: (r, s) =>
                {
                    if (runtime.lanes == null || r >= runtime.lanes.Count) return false;
                    var lane = runtime.lanes[r];
                    if (lane.steps == null || s >= lane.steps.Count) return false;
                    return lane.steps[s];
                }
            );

            if (rowHeight > 0f) grid.SetCellHeight(rowHeight);
            grid.SetToggleReceivesClicks(true);
            grid.SetOverlayEnabled(true);

            RebuildHeaders();
        }

        private void HandleCellToggled(int row, int step, bool on)
        {
            Debug.Log($"[RhythmGrid] TOGGLED r={row} s={step} -> {(on ? "On" : "Off")}");   
            if (runtime == null || runtime.lanes == null) return;
            if (row < 0 || row >= runtime.lanes.Count) return;

            var lane = runtime.lanes[row];
            while (lane.steps.Count <= step) lane.steps.Add(false);
            lane.steps[step] = on;

            PatternChanged?.Invoke();
        }

        // --- Lane utilities (for step 3 headers / buttons) ---

        public void AddLane(
            GeneralMidiPercussion instrument = GeneralMidiPercussion.ClosedHiHat, 
            int velocity = 100)
        {
            if (runtime == null) return;
            runtime.lanes ??= new List<DrumPatternData.Lane>();
            var l = new DrumPatternData.Lane
            {
                instrument = instrument,
                defaultVelocity = Mathf.Clamp(velocity, 1, 127),
                steps = new List<bool>(new bool[runtime.TotalSteps])
            };
            runtime.lanes.Add(l);
            RebuildGridFromRuntime();

            PatternChanged?.Invoke();
        }

        private void AddLaneFromCommonList()
        {
            if (runtime == null) { AddLane(); return; }

            // pick the next instrument not already in use (fallback to cycle)
            var used = new HashSet<GeneralMidiPercussion>();
            foreach (var l in runtime.lanes) used.Add(l.instrument);

            GeneralMidiPercussion pick = GeneralMidiPercussion.ClosedHiHat;

            for (int i = 0; i < CommonPerc.Length; i++)
            {
                int idx = (_nextCommonIndex + i) % CommonPerc.Length;
                var cand = CommonPerc[idx];
                if (!used.Contains(cand)) { pick = cand; _nextCommonIndex = idx + 1; break; }
                if (i == CommonPerc.Length - 1) { pick = CommonPerc[_nextCommonIndex % CommonPerc.Length]; _nextCommonIndex++; }
            }

            AddLane(pick, velocity: 80);
        }

        public void RemoveLane(int row)
        {
            if (runtime == null || runtime.lanes == null) return;
            if (row < 0 || row >= runtime.lanes.Count) return;
            runtime.lanes.RemoveAt(row);
            RebuildGridFromRuntime();

            PatternChanged?.Invoke();
        }

        public void SetLaneInstrument(int row, GeneralMidiPercussion instr)
        {
            if (runtime == null || runtime.lanes == null) return;
            if (row < 0 || row >= runtime.lanes.Count) return;
            runtime.lanes[row].instrument = instr;

            PatternChanged?.Invoke();
        }

        public void SetLaneVelocity(int row, int velocity)
        {
            if (runtime == null || runtime.lanes == null) return;
            if (row < 0 || row >= runtime.lanes.Count) return;
            runtime.lanes[row].defaultVelocity = Mathf.Clamp(velocity, 1, 127);

            PatternChanged?.Invoke();
        }

        public (GeneralMidiPercussion instrument, int velocity, List<int> steps)[] SnapshotAsIndices()
        {
            if (runtime == null) return Array.Empty<(GeneralMidiPercussion, int, List<int>)>();
            var list = new List<(GeneralMidiPercussion, int, List<int>)>(runtime.lanes.Count);
            for (int i = 0; i < runtime.lanes.Count; i++)
            {
                var l = runtime.lanes[i];
                var onIdx = new List<int>(l.steps.Count);
                for (int s = 0; s < l.steps.Count; s++) if (l.steps[s]) onIdx.Add(s);
                list.Add((l.instrument, l.defaultVelocity, onIdx));
            }
            return list.ToArray();
        }

        public void AddLaneUI() { AddLane(); }
        public void RemoveLastLaneUI()
        {
            if (runtime?.lanes?.Count > 0) RemoveLane(runtime.lanes.Count - 1);
        }

        // --- helpers ---
        private static string SanitizeFileName(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private string BuildKeyName()
        {
            // ex: Drum_4-4_4m_HatSnareKick
            string ts = $"{runtime.beatsPerMeasure}-{runtime.subdivisions}";
            string ins = (runtime.lanes == null || runtime.lanes.Count == 0)
                ? "none"
                : string.Join("", runtime.lanes.ConvertAll(l => Abbrev(l.instrument)));

            return $"Drum_{ts}_{runtime.Measures}m_{ins}";
        }

        private static string Abbrev(GeneralMidiPercussion gmp)
        {
            // very short tags for file names
            switch (gmp)
            {
                case GeneralMidiPercussion.ClosedHiHat: return "CH";
                case GeneralMidiPercussion.OpenHiHat: return "OH";
                case GeneralMidiPercussion.AcousticSnare:
                case GeneralMidiPercussion.ElectricSnare: return "SN";
                case GeneralMidiPercussion.BassDrum1: return "BD";
                case GeneralMidiPercussion.LowTom: return "LT";
                case GeneralMidiPercussion.HighTom: return "HT";
                default: return gmp.ToString().Replace(" ", "").Substring(0, Math.Min(3, gmp.ToString().Length));
            }
        }

        private void CopyRuntimeInto(DrumPatternData dst, string displayNameOverride = null)
        {
            dst.DisplayName = string.IsNullOrEmpty(displayNameOverride) ? runtime.DisplayName : displayNameOverride;
            dst.Measures = runtime.Measures;
            dst.beatsPerMeasure = runtime.beatsPerMeasure;
            dst.subdivisions = runtime.subdivisions;

            dst.lanes ??= new List<DrumPatternData.Lane>();
            dst.lanes.Clear();
            foreach (var l in runtime.lanes)
            {
                dst.lanes.Add(new DrumPatternData.Lane
                {
                    instrument = l.instrument,
                    defaultVelocity = l.defaultVelocity,
                    steps = new List<bool>(l.steps)
                });
            }
            dst.EnsureSizes();
        }

        // Headers
        private void RebuildHeaders()
        {
            if (percussionElementList == null || runtime == null) return;

            // destroy old
            foreach (var h in headers) Destroy(h.gameObject);
            headers.Clear();

            for (int r = 0; r < runtime.lanes.Count; r++)
            {
                var h = Instantiate(rowHeaderPrefab, percussionElementList);
                h.Bind(r, runtime.lanes[r].instrument);
                int row = r;
                h.InstrumentChanged += (i, instr) => SetLaneInstrument(i, instr);
                h.RemoveClicked += RemoveLane;
                headers.Add(h);

                // Size the header to match row height
                var rt = h.GetComponent<RectTransform>();
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);

                var le = h.GetComponent<LayoutElement>();
                if (le == null) le = h.gameObject.AddComponent<LayoutElement>();
                le.minHeight = rowHeight;
                le.preferredHeight = rowHeight;
                le.flexibleHeight = 0f;
            }

            // Only order it if it's actually inside the list
            if (buttonsRoot != null && buttonsRoot.parent == percussionElementList)
            {
                buttonsRoot.SetAsLastSibling();
            }

            ResizeHeadersContentToRows();     // <— size content vertically
            SnapHeadersToTop();               // <— put viewport at the top
        }
        private void ResizeHeadersContentToRows()
        {
            if (percussionElementList == null) return;
            float h = rowHeight > 0f ? rowHeight : grid.CellHeight;
            int rows = headers.Count + (buttonsRoot ? 1 : 0);

            var vlg = percussionElementList.GetComponent<VerticalLayoutGroup>();
            float padTop = vlg ? vlg.padding.top : 0f;
            float padBot = vlg ? vlg.padding.bottom : 0f;
            float spacing = vlg ? vlg.spacing : 0f;

            float totalH = padTop + padBot + rows * h + Mathf.Max(0, rows - 1) * spacing;
            percussionElementList.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalH);
        }

        private void SnapHeadersToTop()
        {
            if (headersScroll != null)
                headersScroll.verticalNormalizedPosition = 1f;
            // ensure content anchored to top-left
            var rt = percussionElementList;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0f);
        }


        private void SyncHeaderHeightsToGrid()
        {
            if (percussionElementList == null) return;
            var h = rowHeight > 0f ? rowHeight : grid.CellHeight;
            for (int i = 0; i < headers.Count; i++)
            {
                var rt = headers[i].GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(percussionElementList);
        }
    }
}

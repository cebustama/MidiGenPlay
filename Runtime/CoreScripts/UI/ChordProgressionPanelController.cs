using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

// WIP

namespace MidiGenPlay.UI
{
    public class ChordProgressionPanelController : MonoBehaviour
    {
        const string DefaultAssetsFolder = "Assets/MidiGenPlay/ChordProgressions";
        const string DefaultPackageFolder = "Packages/MidiGenPlay/Runtime/Resources/ScriptableObjects/Patterns/Chords";

        [Header("Refs")]
        [SerializeField] private PatternGrid grid;
        [SerializeField] private ChordLabelOverlay labels;
        [SerializeField] private ChordEventPanel popup;

        [Header("Data")]
        [SerializeField] private ChordProgressionData progression;
        [SerializeField] private Tonality tonality = Tonality.Ionian;


        private ChordProgressionData originalAsset;   // the loaded SO
        private ChordProgressionData runtime;         // runtime clone we actually edit

        public ChordProgressionData GetRuntime() => runtime;
        public ChordProgressionData GetOriginalAsset() => originalAsset;

        // Working cache — mirrors progression.events for quick queries
        private readonly List<ChordProgressionData.ChordEvent> events = new();

        private int beatsPerMeasure => grid.BeatsPerMeasure;

        void Awake()
        {
            if (grid != null)
            {
                grid.OnCellClicked += HandleCellClicked;
                grid.OnCellToggled += HandleCellToggled; // keep toggles as anchors view
            }

            if (popup != null)
                popup.Confirmed += HandlePopupConfirmed;
        }

        public void SetTonality(Tonality t)
        {
            tonality = t;
            labels.SetTonality(t);
        }

        public void Bind(ChordProgressionData data)
        {
            originalAsset = data;
            runtime = DeepClone(data);
            progression = runtime;

            events.Clear();
            if (runtime != null && runtime.events != null)
                events.AddRange(runtime.events);

            PaintAnchorsFromEvents();
            labels?.Refresh(events);
        }

        public void SaveRuntimeIntoAsset()
        {
            if (originalAsset == null || runtime == null) return;

            originalAsset.displayName = runtime.displayName;
            originalAsset.timeSignature = runtime.timeSignature;
            originalAsset.measures = runtime.measures;
            originalAsset.subdivisions = runtime.subdivisions;

            originalAsset.tonalities = new(originalAsset.tonalities ?? new());
            originalAsset.tonalities.Clear();
            if (runtime.tonalities != null) originalAsset.tonalities.AddRange(runtime.tonalities);

            originalAsset.events.Clear();
            foreach (var e in runtime.events)
                originalAsset.events.Add(new ChordProgressionData.ChordEvent
                {
                    startStep = e.startStep,
                    lengthSteps = e.lengthSteps,
                    degree = e.degree,
                    quality = e.quality,
                    velocity = e.velocity
                });

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(originalAsset);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        public void CreateNewRuntime(
            Tonality t, TimeSignature ts, int measures, int subdivisions = 1)
        {
            tonality = t;

            originalAsset = null; // this is a new pattern, not tied to an asset yet
            runtime = ScriptableObject.CreateInstance<ChordProgressionData>();
            runtime.displayName = "Untitled";
            runtime.timeSignature = ts;
            runtime.measures = Mathf.Max(1, measures);
            runtime.subdivisions = Mathf.Max(1, subdivisions);
            runtime.tonalities = new List<Tonality> { t };
            runtime.events = new List<ChordProgressionData.ChordEvent>();

            progression = runtime; // keep legacy field in sync
            events.Clear();

            PaintAnchorsFromEvents();
            labels?.SetTonality(t);
            labels?.Refresh(events);
        }

        // Save runtime as a brand-new asset (Editor only).
        // Returns the created asset (or null on failure).
        public ChordProgressionData SaveRuntimeAsNewAsset(string folderPath = null)
        {
#if UNITY_EDITOR
            if (runtime == null) return null;

            // 1) Resolve preferred folder (package first, then Assets),
            // allowing caller override.
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(folderPath)) candidates.Add(folderPath);
            candidates.Add(DefaultPackageFolder);
            candidates.Add(DefaultAssetsFolder);

            // 2) Prepare the name
            string name = string.IsNullOrWhiteSpace(runtime.displayName) ? 
                BuildKeyName() : runtime.displayName;
            name = SanitizeFileName(name);

            // 3) Try to create the asset in the first folder that works
            foreach (var folder in candidates)
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;

                // Ensure folder exists on disk if it's under Assets/
                if (folder.StartsWith("Assets/"))
                    System.IO.Directory.CreateDirectory(folder);

                UnityEditor.AssetDatabase.Refresh();

                string candidate = $"{folder}/{name}.asset";
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(candidate);
                if (string.IsNullOrEmpty(path)) path = candidate;

                try
                {
                    // Build a fresh ScriptableObject to own on disk
                    var asset = ScriptableObject.CreateInstance<ChordProgressionData>();
                    asset.displayName = runtime.displayName;
                    asset.timeSignature = runtime.timeSignature;
                    asset.measures = runtime.measures;
                    asset.subdivisions = runtime.subdivisions;
                    asset.tonalities = new List<Tonality>(runtime.tonalities ?? new());
                    asset.events = 
                        new List<ChordProgressionData.ChordEvent>(runtime.events ?? new());

                    UnityEditor.AssetDatabase.CreateAsset(asset, path);
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.EditorUtility.SetDirty(asset);

                    // Rebind so the panel keeps editing a runtime clone of the new asset
                    Bind(asset);
                    Debug.Log($"[ChordPanel] Created new ChordProgression asset at {path}");
                    return asset;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ChordPanel] Could not create asset at '{path}'. " +
                        $"Will try next folder. ({ex.Message})");
                }
            }

            Debug.LogError("[ChordPanel] Failed to create ChordProgression asset " +
                "in all candidate folders.");
            return null;
#else
    return null;
#endif
        }

        static string SanitizeFileName(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        // Compute a key-ish default name from contents
        private string BuildKeyName()
        {
            var src = runtime ?? progression;
            string ts = (src?.timeSignature.ToString() ?? "TS").Replace(" ", "");
            string ton = tonality.ToString();
            string ev = (events.Count == 0) ? "none"
                       : string.Join("-", events.Select(e => $"{e.startStep}:{e.lengthSteps}:{(int)e.degree}:{(int)e.quality}"));
            int m = src?.measures ?? 1;
            return $"Prog_{ton}_{ts}_{m}m_{ev}";
        }

        private void PaintAnchorsFromEvents()
        {
            // clear all
            for (int s = 0; s < grid.Steps; s++)
                grid.SetCell(0, s, false);

            foreach (var e in events)
            {
                if (e.startStep >= 0 && e.startStep < grid.Steps)
                    grid.SetCell(0, e.startStep, true);
            }

            labels?.Refresh(events);
        }

        private void HandleCellToggled(int row, int step, bool value)
        {
            if (row != 0) return;

            if (value)
            {
                if (FindEventCovering(step) < 0)
                {
                    int len = DefaultLenOneMeasureFrom(step);
                    InsertEvent(step, len, ScaleDegree.Tonic, ChordQuality.Major, 64);
                    popup?.Show(step, len, ScaleDegree.Tonic, ChordQuality.Major, 64, grid.Steps, tonality);
                }
            }
            else
            {
                int idx = FindEventStarting(step);
                if (idx >= 0)
                {
                    events.RemoveAt(idx);
                    ApplyEventsToRuntime();
                    PaintAnchorsFromEvents();
                }
            }
        }

        private void HandleCellClicked(int row, int step)
        {
            if (row != 0) return;

            int idx = FindEventCovering(step);
            if (idx >= 0)
            {
                var e = events[idx];
                popup.Show(e.startStep, e.lengthSteps, e.degree, e.quality, e.velocity, grid.Steps, tonality);
            }
            else
            {
                int defaultLen = DefaultLenOneMeasureFrom(step);
                var defaultDegree = ScaleDegree.Tonic;
                var defaultQuality = GetSuggestedQuality(tonality, defaultDegree, preferSeventh: false);

                popup.Show(step, defaultLen, defaultDegree, defaultQuality, 64, grid.Steps, tonality);
            }
        }

        // Helper: remainder of current measure, at least 1, at most full measure.
        // If you want “always exactly one bar”, return stepsPerMeasure instead.
        private int DefaultLenOneMeasureFrom(int step)
        {
            int stepsPerMeasure = grid.BeatsPerMeasure * grid.Subdivisions;
            int currentBarStart = (step / stepsPerMeasure) * stepsPerMeasure;
            int currentBarEnd = currentBarStart + stepsPerMeasure;
            int remaining = Mathf.Clamp(currentBarEnd - step, 1, stepsPerMeasure);
            return remaining;
        }

        private void HandlePopupConfirmed(int start, int length, ScaleDegree deg, ChordQuality qual, int vel)
        {
            Debug.Log("<color=cyan>CHORD EVENT CONFIRMED</color>");

            // Enforce bounds
            start = Mathf.Clamp(start, 0, grid.Steps - 1);
            length = Mathf.Max(1, Mathf.Min(length, grid.Steps - start));

            // Remove any event that overlaps the [start, start+length)
            RemoveOverlaps(start, start + length);

            // If an event already starts here, update it; otherwise insert new
            int idx = FindEventStarting(start);
            if (idx >= 0)
            {
                var e = events[idx];
                e.lengthSteps = length;
                e.degree = deg;
                e.quality = qual;
                e.velocity = vel;
                events[idx] = e;
            }
            else
            {
                InsertEvent(start, length, deg, qual, vel);
            }

            ApplyEventsToRuntime();
            PaintAnchorsFromEvents();
        }

        // ---- helpers ----

        private int FindEventCovering(int step)
        {
            for (int i = 0; i < events.Count; i++)
            {
                int s = events[i].startStep;
                int e = s + events[i].lengthSteps;
                if (step >= s && step < e) return i;
            }
            return -1;
        }

        private int FindEventStarting(int step)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i].startStep == step) return i;
            return -1;
        }

        private void InsertEvent(int start, int length, ScaleDegree deg, ChordQuality qual, int vel)
        {
            var ce = new ChordProgressionData.ChordEvent
            {
                startStep = start,
                lengthSteps = length,
                degree = deg,
                quality = qual,
                velocity = Mathf.Clamp(vel, 0, 127)
            };
            events.Add(ce);
            events.Sort((a, b) => a.startStep.CompareTo(b.startStep));
        }

        private void RemoveOverlaps(int start, int endExclusive)
        {
            // Remove any completely covered or partially overlapping events
            for (int i = events.Count - 1; i >= 0; i--)
            {
                int s = events[i].startStep;
                int e = s + events[i].lengthSteps;
                if (e > start && s < endExclusive)
                    events.RemoveAt(i);
            }
        }

        private int NextFreeRun(int fromStep)
        {
            // length to next anchor or end
            int next = grid.Steps;
            foreach (var ev in events)
            {
                if (ev.startStep > fromStep)
                {
                    next = ev.startStep;
                    break;
                }
            }
            return Mathf.Max(1, next - fromStep);
        }

        private void ApplyEventsToRuntime()
        {
            labels?.Refresh(events);
            if (runtime == null) return;

            runtime.events.Clear();
            runtime.events.AddRange(events);
            runtime.subdivisions = grid.Subdivisions;
            runtime.measures = grid.Measures;
        }

        private static ChordProgressionData DeepClone(ChordProgressionData src)
        {
            if (src == null) return null;
            var clone = ScriptableObject.CreateInstance<ChordProgressionData>();
            clone.displayName = src.displayName;
            clone.timeSignature = src.timeSignature;
            clone.measures = src.measures;
            clone.subdivisions = src.subdivisions;

            clone.tonalities = new List<Tonality>();
            if (src.tonalities != null) clone.tonalities.AddRange(src.tonalities);

            clone.events = new List<ChordProgressionData.ChordEvent>(src.events?.Count ?? 0);
            if (src.events != null)
                foreach (var e in src.events)
                    clone.events.Add(new ChordProgressionData.ChordEvent
                    {
                        startStep = e.startStep,
                        lengthSteps = e.lengthSteps,
                        degree = e.degree,
                        quality = e.quality,
                        velocity = e.velocity
                    });
            return clone;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MidiGenPlay;
using MidiGenPlay.Composition;

namespace MidiGenPlay.EditorTools
{
    /// <summary>
    /// MGP-TONALITY-2: thin UI over <see cref="TonalityMatrixRunner"/>.
    /// Assign the SAME SmokeSetupSO the Composition Smoke window uses (it
    /// supplies the config, instruments-per-role template rows, root,
    /// measures and BPM), list the progressions to sweep (>= 1 diatonic and
    /// >= 1 carrying degreeAccidental != 0), set the seed, run.
    ///
    /// "Re-run cell (verbose)" replays ONE cell with audit logs on and the
    /// config's own logGenerator untouched — the drill-down / reproduction
    /// path (same axes + same seed => same output).
    /// </summary>
    public class TonalityMatrixWindow : EditorWindow
    {
        private SmokeSetupSO _setup;
        private readonly List<ChordProgressionData> _progressions = new();
        private int _seed = 12345;
        private int _rerunCellIndex;
        private Vector2 _scroll;
        private string _lastReportLine;

        [MenuItem("Tools/MidiGenPlay/Tonality Matrix")]
        public static void Open() =>
            GetWindow<TonalityMatrixWindow>("Tonality Matrix");

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Regression matrix over the composition smoke path. The audit " +
                "stays log-only; composers are not modified; nothing is " +
                "written to any asset. Output: CSV + markdown under " +
                "persistentDataPath/TonalityMatrix.", MessageType.Info);

            _setup = (SmokeSetupSO)EditorGUILayout.ObjectField(
                new GUIContent("Smoke setup",
                    "Same SmokeSetupSO the Composition Smoke window uses. " +
                    "Needs template rows for Backing, Bassline and Melody " +
                    "(instruments; the Melody row's style/pattern pass " +
                    "through — a procedural melody row is recommended)."),
                _setup, typeof(SmokeSetupSO), false);

            EditorGUILayout.LabelField("Progressions to sweep", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < _progressions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _progressions[i] = (ChordProgressionData)EditorGUILayout.ObjectField(
                    _progressions[i], typeof(ChordProgressionData), false);
                if (GUILayout.Button("−", GUILayout.Width(24))) remove = i;
                EditorGUILayout.EndHorizontal();
            }
            if (remove >= 0) _progressions.RemoveAt(remove);
            if (GUILayout.Button("+ Add progression")) _progressions.Add(null);

            _seed = EditorGUILayout.IntField(
                new GUIContent("Seed (all cells)",
                    "D-TON2-SEED=A: one seed for every cell, recorded per row."),
                _seed);

            var inputs = BuildInputs();
            List<string> notes = null;
            List<TonalityMatrixRunner.CellSpec> cells = null;
            if (inputs != null)
            {
                cells = TonalityMatrixRunner.BuildCells(inputs, out notes);
                EditorGUILayout.LabelField($"Planned cells: {cells.Count}");
                foreach (var n in notes)
                    EditorGUILayout.HelpBox(n, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(inputs == null || cells == null || cells.Count == 0))
            {
                if (GUILayout.Button("Run full sweep"))
                    RunSweep(inputs, cells, notes);

                EditorGUILayout.BeginHorizontal();
                _rerunCellIndex = EditorGUILayout.IntField("Cell index", _rerunCellIndex);
                if (GUILayout.Button("Re-run cell (verbose)", GUILayout.Width(160)))
                    RerunCell(inputs, cells, _rerunCellIndex);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(_lastReportLine))
                EditorGUILayout.HelpBox(_lastReportLine, MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private TonalityMatrixRunner.MatrixInputs BuildInputs()
        {
            if (_setup == null || _setup.config == null) return null;
            var progs = _progressions.Where(p => p != null).ToList();
            if (progs.Count == 0) return null;
            return new TonalityMatrixRunner.MatrixInputs
            {
                setup = _setup,
                progressions = progs,
                seed = _seed,
            };
        }

        private void RunSweep(
            TonalityMatrixRunner.MatrixInputs inputs,
            List<TonalityMatrixRunner.CellSpec> cells,
            List<string> notes)
        {
            bool cancelled = false;
            List<TonalityMatrixRunner.CellResult> results;
            try
            {
                results = TonalityMatrixRunner.RunSweep(
                    inputs, cells,
                    progress: (i, total, spec) =>
                    {
                        cancelled = EditorUtility.DisplayCancelableProgressBar(
                            "Tonality matrix",
                            $"Cell {i + 1}/{total}: {spec}",
                            total > 0 ? (float)i / total : 0f);
                    },
                    cancelRequested: () => cancelled);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var (csv, md) = TonalityMatrixRunner.WriteReports(inputs, cells, results, notes);
            int failed = results.Count(r => r.error != null);
            int residual = results.Where(r => r.error == null).Sum(r => r.ResidualReds);
            _lastReportLine =
                $"Cells run: {results.Count}/{cells.Count}" +
                (cancelled ? " (CANCELLED)" : "") +
                $" · failed: {failed} · residual reds (non-approach): {residual}\n" +
                $"CSV: {csv}\nSummary: {md}";
            Debug.Log($"<color=lime>[TonalityMatrix]</color> {_lastReportLine}");
            EditorUtility.RevealInFinder(md);
        }

        private void RerunCell(
            TonalityMatrixRunner.MatrixInputs inputs,
            List<TonalityMatrixRunner.CellSpec> cells,
            int index)
        {
            var spec = cells.FirstOrDefault(c => c.index == index);
            if (spec == null)
            {
                _lastReportLine = $"No cell with index {index} (0..{cells.Count - 1}).";
                return;
            }
            var r = TonalityMatrixRunner.RunCell(inputs, spec, verbose: true);
            _lastReportLine = r.error != null
                ? $"Cell {index} FAILED: {r.error}"
                : $"Cell {index}: residualReds={r.ResidualReds} " +
                  $"walkApproachInferred={r.walkApproachInferred} " +
                  $"(full evidence in the console — audit logs were not suppressed).";
            Debug.Log($"<color=lime>[TonalityMatrix]</color> {spec} → {_lastReportLine}");
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
    /// <summary>
    /// Time Signature-aware, scrollable grid used by rhythm/chords/melody editors.
    /// - X = steps (measures × beats × subdivisions)
    /// - Y = rows (instrument lanes, or 1 for chords)
    /// - Emits cell toggle events; role-specific controllers map these to data.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class PatternGrid : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RectTransform content;       // child of ScrollRect
        [SerializeField] private GridLayoutGroup layout;      // on 'content'
        [SerializeField] private PatternGridCell cellPrefab;

        [Header("Visuals")]
        [SerializeField] private Color barAccent = new(1f, 1f, 1f, 0.04f);
        [SerializeField] private Color beatAccent = new(1f, 1f, 1f, 0.08f);

        public int Rows { get; private set; } = 0;
        public int Steps { get; private set; } = 0;           // total columns
        public int Measures { get; private set; } = 0;
        public int BeatsPerMeasure { get; private set; } = 4;
        public int Subdivisions { get; private set; } = 1;

        // row-major store: cells[row][step]
        private readonly List<List<PatternGridCell>> grid = new();

        public event Action<int, int, bool> OnCellToggled;      // (row, step, value)

        public void Build(int rows, int measures, int beatsPerMeasure, int subdivisions = 1,
                          Func<int, int, bool> initialState = null)
        {
            Clear();  // destroy previous

            Rows = rows;
            Measures = measures;
            BeatsPerMeasure = Mathf.Max(1, beatsPerMeasure);
            Subdivisions = Mathf.Max(1, subdivisions);
            Steps = Measures * BeatsPerMeasure * Subdivisions;

            // GridLayout cell sizing can be set in prefab/UI; here we only control column count
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Steps;

            for (int r = 0; r < Rows; r++)
            {
                // Obtain row
                var row = new List<PatternGridCell>(Steps);
                for (int s = 0; s < Steps; s++)
                {
                    var cell = Instantiate(cellPrefab, content);
                    bool startOn = initialState?.Invoke(r, s) ?? false;

                    // Accent bars/beat lines (very light tint in bg image)
                    Color? accent = null;
                    bool isBar = (s % (BeatsPerMeasure * Subdivisions)) == 0;
                    bool isBeat = (s % Subdivisions) == 0;
                    if (isBar) accent = barAccent;
                    else if (isBeat) accent = beatAccent;

                    cell.Initialize(r, s, startOn, accent);
                    cell.Toggled += HandleToggled;
                    row.Add(cell);
                }
                grid.Add(row);
            }
        }

        public void Clear()
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            grid.Clear();
            Rows = Steps = Measures = BeatsPerMeasure = Subdivisions = 0;
        }

        private void HandleToggled(PatternGridCell cell, bool value)
        {
            OnCellToggled?.Invoke(cell.Row, cell.Step, value);
        }

        public void SetRowLabelArea(float width)
        {
            // Optional: if you add a left label column, adjust padding here.
            var p = layout.padding;
            p.left = Mathf.RoundToInt(width);
            layout.padding = p;
        }

        public void SetCell(int row, int step, bool value)
        {
            if (row < 0 || row >= Rows) return;
            if (step < 0 || step >= Steps) return;
            grid[row][step].SetActive(value);
        }

        public bool GetCell(int row, int step)
        {
            if (row < 0 || row >= Rows) return false;
            if (step < 0 || step >= Steps) return false;
            return grid[row][step].IsActive;
        }

        /// <summary>Export a plain bool matrix [row, step] for controllers to serialize.</summary>
        public bool[,] Snapshot()
        {
            var data = new bool[Rows, Steps];
            for (int r = 0; r < Rows; r++)
                for (int s = 0; s < Steps; s++)
                    data[r, s] = grid[r][s].IsActive;
            return data;
        }

        /// <summary>Bulk import a bool matrix (size mismatch is clamped).</summary>
        public void LoadFrom(bool[,] state)
        {
            int rows = Mathf.Min(Rows, state.GetLength(0));
            int steps = Mathf.Min(Steps, state.GetLength(1));
            for (int r = 0; r < rows; r++)
                for (int s = 0; s < steps; s++)
                    grid[r][s].SetActive(state[r, s]);
        }
    }
}



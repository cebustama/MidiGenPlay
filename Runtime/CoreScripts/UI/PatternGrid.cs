using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
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

        [Header("Auto-fit")]
        [Tooltip("If on, cell width will be computed to exactly fill the content width for all columns; if off, horizontal scrolling may be needed.")]
        [SerializeField] private bool fitToContentWidth = true;

        [Tooltip("If on, cell height will be computed to fill content height across all rows.")]
        [SerializeField] private bool fitToContentHeight = true;

        [Tooltip("Minimum cell width when auto-fitting.")]
        [SerializeField] private float minCellWidth = 12f;
        [Tooltip("Maximum cell width when auto-fitting (0 = unlimited).")]
        [SerializeField] private float maxCellWidth = 0f;

        [Tooltip("Minimum cell height when auto-fitting.")]
        [SerializeField] private float minCellHeight = 16f;
        [Tooltip("Maximum cell height when auto-fitting (0 = unlimited).")]
        [SerializeField] private float maxCellHeight = 0f;

        public int Rows { get; private set; } = 0;
        public int Steps { get; private set; } = 0;           // total columns
        public int Measures { get; private set; } = 0;
        public int BeatsPerMeasure { get; private set; } = 4;
        public int Subdivisions { get; private set; } = 1;

        private readonly List<List<PatternGridCell>> grid = new();

        public event Action<int, int, bool> OnCellToggled;

        public void Build(int rows, int measures, int beatsPerMeasure, int subdivisions = 1,
                          Func<int, int, bool> initialState = null)
        {
            Clear();

            Rows = Mathf.Max(1, rows);
            Measures = Mathf.Max(1, measures);
            BeatsPerMeasure = Mathf.Max(1, beatsPerMeasure);
            Subdivisions = Mathf.Max(1, subdivisions);
            Steps = Measures * BeatsPerMeasure * Subdivisions;

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Steps;

            for (int r = 0; r < Rows; r++)
            {
                var row = new List<PatternGridCell>(Steps);
                for (int s = 0; s < Steps; s++)
                {
                    var cell = Instantiate(cellPrefab, content);
                    bool startOn = initialState?.Invoke(r, s) ?? false;

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

            RecomputeCellSize();
        }

        public void Clear()
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            grid.Clear();
            Rows = Steps = Measures = BeatsPerMeasure = Subdivisions = 0;
        }

        private void HandleToggled(PatternGridCell cell, bool value)
            => OnCellToggled?.Invoke(cell.Row, cell.Step, value);

        public void SetRowLabelArea(float width)
        {
            var p = layout.padding;
            p.left = Mathf.RoundToInt(width);
            layout.padding = p;
            RecomputeCellSize();
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

        public bool[,] Snapshot()
        {
            var data = new bool[Rows, Steps];
            for (int r = 0; r < Rows; r++)
                for (int s = 0; s < Steps; s++)
                    data[r, s] = grid[r][s].IsActive;
            return data;
        }

        public void LoadFrom(bool[,] state)
        {
            int rows = Mathf.Min(Rows, state.GetLength(0));
            int steps = Mathf.Min(Steps, state.GetLength(1));
            for (int r = 0; r < rows; r++)
                for (int s = 0; s < steps; s++)
                    grid[r][s].SetActive(state[r, s]);
        }

        // --- Responsive sizing ---

        private void OnRectTransformDimensionsChange()
        {
            // When parent/viewport resizes, recompute
            RecomputeCellSize();
        }

        private void RecomputeCellSize()
        {
            if (layout == null || content == null || Steps <= 0 || Rows <= 0) return;

            // Ensure layout is up-to-date
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            var padding = layout.padding;
            var spacing = layout.spacing;

            float contentWidth = content.rect.width;
            float contentHeight = content.rect.height;

            // Effective space after padding
            float innerW = Mathf.Max(0f, contentWidth - padding.left - padding.right);
            float innerH = Mathf.Max(0f, contentHeight - padding.top - padding.bottom);

            // Horizontal sizing
            float cellW;
            if (fitToContentWidth)
            {
                float totalSpacingX = spacing.x * Mathf.Max(0, Steps - 1);
                cellW = (innerW - totalSpacingX) / Steps;
                if (maxCellWidth > 0f) cellW = Mathf.Min(cellW, maxCellWidth);
                cellW = Mathf.Max(cellW, minCellWidth);
            }
            else
            {
                // keep whatever prefab has; still clamp
                cellW = Mathf.Max(layout.cellSize.x, minCellWidth);
                if (maxCellWidth > 0f) cellW = Mathf.Min(cellW, maxCellWidth);
            }

            // Vertical sizing
            float cellH;
            if (fitToContentHeight)
            {
                float totalSpacingY = spacing.y * Mathf.Max(0, Rows - 1);
                cellH = (innerH - totalSpacingY) / Rows;
                if (maxCellHeight > 0f) cellH = Mathf.Min(cellH, maxCellHeight);
                cellH = Mathf.Max(cellH, minCellHeight);
            }
            else
            {
                cellH = Mathf.Max(layout.cellSize.y, minCellHeight);
                if (maxCellHeight > 0f) cellH = Mathf.Min(cellH, maxCellHeight);
            }

            layout.cellSize = new Vector2(cellW, cellH);

            // If we’re fitting to width/height, make content size match viewport so no overflow
            var sr = GetComponent<ScrollRect>();
            if (sr != null)
            {
                bool horizScrollNeeded = !fitToContentWidth;
                bool vertScrollNeeded = !fitToContentHeight;

                sr.horizontal = horizScrollNeeded;
                sr.vertical = vertScrollNeeded;
            }
        }
    }
}

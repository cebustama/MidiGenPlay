using System;
using System.Collections;
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
        [SerializeField] private bool toggleReceivesClicks = true;
        [SerializeField] private bool overlayEnabled = true;

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
        public event Action<int, int> OnCellClicked;

        public event Action OnRebuilt;   // raised after Build() and RecomputeCellSize()

        public float CellWidth => layout != null ? layout.cellSize.x : 0f;
        public float CellHeight => layout != null ? layout.cellSize.y : 0f;
        public Vector2 Spacing => layout != null ? layout.spacing : Vector2.zero;
        public RectOffset Padding => layout != null ? layout.padding : new RectOffset();
        public RectTransform ContentRect => content;

        bool _pendingRebuild;

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
                    cell.SetToggleReceivesClicks(toggleReceivesClicks);
                    cell.SetOverlayEnabled(overlayEnabled);
                    cell.Clicked += _ => OnCellClicked?.Invoke(cell.Row, cell.Step);
                    cell.Toggled += HandleToggled;
                    row.Add(cell);
                }
                grid.Add(row);
            }

            RecomputeCellSize();
            EnsureTopAnchoring();
            SnapToTop();
        }

        public void Clear()
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            grid.Clear();
            Rows = Steps = Measures = BeatsPerMeasure = Subdivisions = 0;
        }

        private void HandleToggled(PatternGridCell cell, bool value)
            => OnCellToggled?.Invoke(cell.Row, cell.Step, value);

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

        void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || _pendingRebuild) return;
            _pendingRebuild = true;
            StartCoroutine(Co_RebuildNextFrame());
        }

        IEnumerator Co_RebuildNextFrame()
        {
            yield return null; // wait one frame
            _pendingRebuild = false;
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

            OnRebuilt?.Invoke();
            EnsureTopAnchoring();
            SnapToTop();
        }

        public float StepToLocalX(int step)
        {
            // left padding + (cellW + spacingX) * step
            return Padding.left + (CellWidth + Spacing.x) * step;
        }

        public void SetFitToContent(bool width, bool height)
        {
            fitToContentWidth = width;
            fitToContentHeight = height;
            RecomputeCellSize();
        }

        // Lock row height to a specific value (and allow vertical scrolling if needed)
        public void SetCellHeight(float height)
        {
            fitToContentHeight = false; // important: we’ll control height manually

            float h = Mathf.Max(minCellHeight, height);
            if (maxCellHeight > 0f) h = Mathf.Min(h, maxCellHeight);

            var size = layout.cellSize;
            size.y = h;
            layout.cellSize = size;

            // Ensure content is tall enough for scroll
            float totalH = layout.padding.top + layout.padding.bottom
                           + h * Rows + layout.spacing.y * Mathf.Max(0, Rows - 1);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalH);

            OnRebuilt?.Invoke();
            EnsureTopAnchoring();
            SnapToTop();
        }

        private void EnsureTopAnchoring()
        {
            if (content == null) return;

            if (fitToContentHeight)
            {
                // Stretch vertically to the viewport (Chord grid scenario)
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 0.5f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
            }
            else
            {
                // Manual row height: anchor to TOP (Rhythm grid with headers)
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.anchoredPosition = new Vector2(0f, 0f);
            }

            if (layout != null) layout.childAlignment = TextAnchor.UpperLeft;
        }

        private void SnapToTop()
        {
            // Only meaningful when rows don’t fill the viewport (manual-height mode)
            if (fitToContentHeight) return;

            var sr = GetComponent<ScrollRect>();
            if (sr != null) sr.verticalNormalizedPosition = 1f; // top
            if (content != null)
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        public void UseAutoHeight()
        {
            fitToContentHeight = true;
            RecomputeCellSize();
        }

        public void SetToggleReceivesClicks(bool enabled)
        {
            toggleReceivesClicks = enabled;
            foreach (var row in grid)
                foreach (var cell in row)
                    cell.SetToggleReceivesClicks(enabled);
        }

        public void SetOverlayEnabled(bool enabled)
        {
            overlayEnabled = enabled;
            foreach (var row in grid)
                foreach (var cell in row)
                    cell.SetOverlayEnabled(enabled);
        }
    }
}

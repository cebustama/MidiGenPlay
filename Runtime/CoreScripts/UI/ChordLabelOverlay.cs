using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.UI
{
    /// Renders one label block per ChordEvent: a background Image spanning the event width
    /// and a centered TMP roman numeral over it.
    public class ChordLabelOverlay : MonoBehaviour
    {
        [SerializeField] private Tonality tonality = Tonality.Ionian;
        [SerializeField] private bool preferSeventhForLabels = false;
        [SerializeField] private bool preferModeSuggestionWhenUnspecified = true;

        [Header("Wiring")]
        [SerializeField] private PatternGrid grid;
        [SerializeField] private RectTransform overlayContainer; // should align with grid.ContentRect
        [SerializeField] private RectTransform labelPrefab;      // root with child 'Background' (Image) and child 'Text (TMP)'

        [Header("Palette")]
        [SerializeField] private Color defaultBg = new(1f, 1f, 1f, 1f);

        [Tooltip("Per-degree background colors in ScaleDegree enum order (Tonic..LeadingTone).")]
        [SerializeField] private Color[] degreeBgColors = Array.Empty<Color>();

        // pooled items
        private readonly List<Item> pool = new();
        private IReadOnlyList<ChordProgressionData.ChordEvent> lastEvents;

        private struct Item
        {
            public RectTransform root;
            public Image bg;
            public TextMeshProUGUI txt;
        }

        private void Awake()
        {
            if (!overlayContainer) overlayContainer = (RectTransform)transform;

            if (grid)
            {
                // match overlay rect to grid content, and re-render after size changes
                grid.OnRebuilt += () =>
                {
                    MatchOverlayToGridContent();
                    if (lastEvents != null) Refresh(lastEvents);
                };
            }
        }

        private void OnEnable() => MatchOverlayToGridContent();

        public void SetTonality(Tonality t)
        {
            Debug.Log("Setting overlay tonality to " + t.ToString());
            tonality = t;
            if (lastEvents != null) Refresh(lastEvents);
        }

        private void MatchOverlayToGridContent()
        {
            if (!grid || !overlayContainer || !grid.ContentRect) return;
            var content = grid.ContentRect;

            overlayContainer.anchorMin = content.anchorMin;
            overlayContainer.anchorMax = content.anchorMax;
            overlayContainer.pivot = content.pivot;
            overlayContainer.position = content.position;
            overlayContainer.sizeDelta = content.sizeDelta;
        }

        public void Refresh(IReadOnlyList<ChordProgressionData.ChordEvent> events)
        {
            if (CanvasUpdateRegistry.IsRebuildingGraphics() ||
                CanvasUpdateRegistry.IsRebuildingLayout())
            {
                StartCoroutine(Co_RefreshEndOfFrame(events));
                return;
            }

            DoRefresh(events);
        }

        private Color PickBg(ScaleDegree deg)
        {
            int idx = (int)deg;
            if (degreeBgColors != null && idx >= 0 && idx < degreeBgColors.Length)
            {
                var c = degreeBgColors[idx];
                if (c.a > 0f) return c;
            }
            return defaultBg;
        }

        private void DoRefresh(IReadOnlyList<ChordProgressionData.ChordEvent> events)
        {
            lastEvents = events;

            // disable all existing
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].root) pool[i].root.gameObject.SetActive(false);

            if (grid == null || overlayContainer == null || events == null) return;

            // ensure pool size
            while (pool.Count < events.Count)
            {
                var root = Instantiate(labelPrefab, overlayContainer);
                // expect background and text as children:
                var bg = root.GetComponentInChildren<Image>(includeInactive: true);
                var txt = root.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

                // absolute positioning (no layouts)
                root.anchorMin = new Vector2(0, 0.5f);
                root.anchorMax = new Vector2(0, 0.5f);
                root.pivot = new Vector2(0, 0.5f);

                if (txt != null)
                {
                    txt.enableAutoSizing = true;
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.raycastTarget = false;
                }

                pool.Add(new Item { root = root, bg = bg, txt = txt });
            }

            float cw = grid.CellWidth;
            float sx = grid.Spacing.x;
            float height = overlayContainer.rect.height;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                int start = Mathf.Clamp(e.startStep, 0, Mathf.Max(0, grid.Steps - 1));
                float x = grid.StepToLocalX(start);
                float w = Mathf.Max(1f, e.lengthSteps) * (cw + sx) - sx;

                var item = pool[i];
                var rt = item.root;

                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(w, height);
                rt.gameObject.SetActive(true);

                // background color
                if (item.bg) item.bg.color = PickBg(e.degree);

                // label text
                if (item.txt)
                {
                    string rn;
                    if (preferModeSuggestionWhenUnspecified)
                    {
                        // If you consider all events “specified”, you can always call the mode-based overload instead.
                        rn = ToRomanRich(e.degree, e.quality); // explicit quality drives suffix/case
                    }
                    else
                    {
                        rn = ToRomanRich(e.degree, tonality, preferSeventhForLabels);
                    }

                    item.txt.text = rn;
                    item.txt.color = Color.black; // or keep your font material color
                }
            }
        }

        IEnumerator Co_RefreshEndOfFrame(IReadOnlyList<ChordProgressionData.ChordEvent> evs)
        {
            yield return null; // end of frame
            DoRefresh(evs);
        }
    }
}

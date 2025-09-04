using Melanchall.DryWetMidi.MusicTheory;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MidiGenPlay.UI
{
    /// Renders one TMP label per ChordEvent, sized to its step-length.
    public class ChordLabelOverlay : MonoBehaviour
    {
        [SerializeField] private PatternGrid grid;
        [SerializeField] private RectTransform overlayContainer; // this RectTransform (stretch X)
        [SerializeField] private TextMeshProUGUI labelPrefab;

        private readonly List<TextMeshProUGUI> pool = new();

        private IReadOnlyList<ChordProgressionData.ChordEvent> lastEvents;

        private void Awake()
        {
            if (!overlayContainer) overlayContainer = (RectTransform)transform;
            if (grid) grid.OnRebuilt += () => Refresh(lastEvents); // keep size aligned
        }

        public void Refresh(IReadOnlyList<ChordProgressionData.ChordEvent> events)
        {
            Debug.Log("<color=green>REFRESHING OVERLAY!</color>");

            lastEvents = events;

            // clear pool
            for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false);

            if (grid == null || events == null) return;

            int needed = events.Count;
            while (pool.Count < needed)
            {
                var lbl = Instantiate(labelPrefab, overlayContainer);
                var rt = lbl.rectTransform;
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                pool.Add(lbl);
            }

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                var rt = pool[i].rectTransform;

                float x = grid.StepToLocalX(Mathf.Clamp(e.startStep, 0, grid.Steps - 1));
                float w = Mathf.Max(1f, e.lengthSteps) * (grid.CellWidth + grid.Spacing.x) - grid.Spacing.x;

                // position inside overlay
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(w, overlayContainer.rect.height);

                var label = pool[i];
                label.text = ToRoman(e.degree, e.quality);
                label.color = PickColor(e.degree);
                label.enableAutoSizing = true;
                label.alignment = TextAlignmentOptions.Center;
                label.gameObject.SetActive(true);
            }
        }

        private static string ToRoman(ScaleDegree deg, ChordQuality q)
        {
            // roman base
            // TODO: Depending on Mode
            string baseNum = deg switch
            {
                ScaleDegree.Tonic => "I",
                ScaleDegree.Supertonic => "II",
                ScaleDegree.Mediant => "III",
                ScaleDegree.Subdominant => "IV",
                ScaleDegree.Dominant => "V",
                ScaleDegree.Submediant => "VI",
                ScaleDegree.LeadingTone => "VII",
                _ => "?"
            };

            /*
            bool minorish = q is ChordQuality.Minor or ChordQuality.Minor7 or ChordQuality.Diminished;
            string rn = minorish ? baseNum.ToLower() : baseNum;

            // simple suffixes — extend as you like
            return q switch
            {
                ChordQuality.Dominant7 => rn + "7",
                ChordQuality.Major7 => rn + "Δ7",
                ChordQuality.Minor7 => rn + "7",
                ChordQuality.Diminished => rn + "°",
                ChordQuality.Susp4 => rn + "sus4",
                _ => rn
            };*/
            return baseNum;
        }

        // TODO
        private Color PickColor(ScaleDegree deg)
        {
            return deg switch
            {
                //ScaleDegree.I => tonicColor,
                //ScaleDegree.V => dominantColor,
                _ => Color.white
            };
        }
    }
}

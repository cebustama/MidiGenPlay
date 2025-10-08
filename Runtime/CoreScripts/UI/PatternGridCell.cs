using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
    /// <summary>A single interactive cell in the PatternGrid.</summary>
    public class PatternGridCell : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Toggle toggle; // optional, can be null for label-only cells
        [SerializeField] private Image bg;  // to draw accents/highlights

        [SerializeField] private Color activeOverlay = new(0f, 1f, 0f, 0.35f);
        [SerializeField] private Color inactiveOverlay = new(0f, 0f, 0f, 0f);

        public int Row { get; private set; }
        public int Step { get; private set; }   // 0..(totalSteps-1)
        public event Action<PatternGridCell, bool> Toggled;
        public event Action<PatternGridCell> Clicked;

        private bool overlayEnabled = true;

        public void Initialize(int row, int step, bool initial, Color? accent = null)
        {
            Row = row;
            Step = step;

            if (accent.HasValue && bg != null) bg.color = accent.Value;

            if (toggle != null)
            {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.isOn = initial;
                ApplyVisual(initial);
                toggle.onValueChanged.AddListener(v =>
                {
                    ApplyVisual(v);
                    Toggled?.Invoke(this, v);
                });
            }
        }

        public void SetActive(bool v)
        {
            if (toggle == null) return;
            if (toggle.isOn == v) return;
            toggle.SetIsOnWithoutNotify(v);
            ApplyVisual(v);
        }

        public bool IsActive => toggle != null && toggle.isOn;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private void ApplyVisual(bool on)
        {
            if (toggle != null && toggle.targetGraphic != null)
                toggle.targetGraphic.color = (overlayEnabled && on) ? 
                    activeOverlay : inactiveOverlay;
        }

        public void SetToggleReceivesClicks(bool enabled)
        {
            if (toggle == null) return;
            toggle.interactable = enabled;
            if (toggle.targetGraphic != null)
                (toggle.targetGraphic as Graphic).raycastTarget = enabled;
        }

        public void SetOverlayEnabled(bool enabled)
        {
            overlayEnabled = enabled;
            ApplyVisual(IsActive);
        }

    }

}
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
    /// <summary>A single interactive cell in the PatternGrid.</summary>
    public class PatternGridCell : MonoBehaviour
    {
        [SerializeField] private Toggle toggle; // optional, can be null for label-only cells
        [SerializeField] private Image bg;  // to draw accents/highlights

        public int Row { get; private set; }
        public int Step { get; private set; }   // 0..(totalSteps-1)
        public event Action<PatternGridCell, bool> Toggled;

        public void Initialize(int row, int step, bool initial, Color? accent = null)
        {
            Row = row;
            Step = step;

            if (accent.HasValue && bg != null) bg.color = accent.Value;

            if (toggle != null)
            {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.isOn = initial;
                toggle.onValueChanged.AddListener(v => Toggled?.Invoke(this, v));
            }
        }

        public void SetActive(bool v)
        {
            if (toggle == null) return;
            if (toggle.isOn == v) return;
            toggle.SetIsOnWithoutNotify(v);
        }

        public bool IsActive => toggle != null && toggle.isOn;
    }

}
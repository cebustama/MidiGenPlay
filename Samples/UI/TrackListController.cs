using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay
{
    public class TrackListController : MonoBehaviour
    {
        [SerializeField] private Transform trackTabContainer;
        [SerializeField] private TrackTabButton trackTabPrefab;
        [SerializeField] private Button addTrackButton;

        private readonly List<TrackTabButton> trackTabButtons = new();
        private int selectedIndex = -1;

        public event Action<int> OnTrackSelected;
        public event Action OnAddTrackClicked;
        public event Action<int> OnRemoveTrackClicked;

        private void Awake()
        {
            addTrackButton.onClick.AddListener(() => OnAddTrackClicked?.Invoke());
        }

        public void SetTrackTabs(int count, int selected)
        {
            // Clean existing tabs
            foreach (var btn in trackTabButtons)
                Destroy(btn.gameObject);
            trackTabButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                var tab = Instantiate(trackTabPrefab, trackTabContainer);
                if (tab == null)
                {
                    Debug.LogError("TrackTabButton component missing on prefab.");
                    continue;
                }

                int index = i; // closure capture
                tab.Initialize(index, $"Track {i + 1}");

                tab.OnLeftClicked += idx =>
                {
                    SelectTab(idx);
                    OnTrackSelected?.Invoke(idx);
                };

                tab.OnRightClicked += idx => OnRemoveTrackClicked?.Invoke(idx);

                tab.gameObject.SetActive(true);
                trackTabButtons.Add(tab);
            }

            addTrackButton.transform.SetAsLastSibling();
            SelectTab(selected);
        }

        public void SelectTab(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < trackTabButtons.Count; i++)
                trackTabButtons[i].SetActiveVisual(i == index);
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay
{
    public class PartListController : MonoBehaviour
    {
        [SerializeField] private Transform partTabContainer;
        [SerializeField] private GameObject partTabPrefab;
        [SerializeField] private Button addPartButton;

        private readonly List<Button> partTabButtons = new();
        private int selectedIndex = -1;

        public event Action<int> OnPartSelected;
        public event Action OnPartAddClicked;
        public event Action<int> OnPartRemoveClicked;

        private void Awake()
        {
            addPartButton.onClick.AddListener(() => OnPartAddClicked?.Invoke());
        }

        public void SetPartTabs(int count, int selected)
        {
            // Destroy and recreate buttons
            foreach (var btn in partTabButtons)
                Destroy(btn.gameObject);
            partTabButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                var tabObj = Instantiate(partTabPrefab, partTabContainer);

                var tabButton = tabObj.GetComponent<PartTabButton>();
                if (tabButton == null)
                {
                    Debug.LogError("PartTabButton component missing on partTabPrefab.");
                    continue;
                }

                int index = i; // avoid closure capture
                tabButton.Initialize(index, $"Part {i + 1}");
                tabButton.OnLeftClicked += idx =>
                {
                    SelectTab(idx);
                    OnPartSelected?.Invoke(idx);
                };

                tabButton.OnRightClicked += idx =>
                {
                    OnPartRemoveClicked?.Invoke(idx);
                };
                tabButton.gameObject.SetActive(true);

                partTabButtons.Add(tabButton.GetComponent<Button>());
            }

            SelectTab(selected);
        }

        private void SelectTab(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < partTabButtons.Count; i++)
            {
                partTabButtons[i].interactable = (i != index);
            }
        }
    }
}
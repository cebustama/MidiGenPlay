using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A tab that represents one Track inside GenerateMidiSongPanel.
/// Left-click → select, Right-click → remove (bubble up via events).
/// </summary>
public class TrackTabButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image background;

    public int Index { get; private set; }
    public event Action<int> OnLeftClicked;
    public event Action<int> OnRightClicked;

    public void Initialize(int index, string labelText)
    {
        Index = index;
        if (label != null)
            label.text = labelText;
    }

    public void SetActiveVisual(bool isActive)
    {
        if (background != null)
            background.color = isActive ? Color.white : Color.gray;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            OnLeftClicked?.Invoke(Index);
        else if (eventData.button == PointerEventData.InputButton.Right)
            OnRightClicked?.Invoke(Index);
    }
}

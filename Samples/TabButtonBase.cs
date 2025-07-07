using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidiGenPlay
{
    /// <summary>
    /// A generic tab‐button that carries an index and forwards
    /// left‐ and right‐clicks to abstract handlers.
    /// </summary>
    /// <typeparam name="TPanel">The type of the panel that owns these tabs.</typeparam>
    [RequireComponent(typeof(Button))]
    public abstract class TabButtonBase<TPanel> : MonoBehaviour, IPointerClickHandler
        where TPanel : MonoBehaviour
    {
        public int Index { get; private set; }
        protected TPanel Panel { get; private set; }

        Button _button;
        TMP_Text _label;

        /// <summary>
        /// Call immediately after Instantiate().
        /// </summary>
        public void Initialize(int index, TPanel panel, string labelText)
        {
            Index = index;
            Panel = panel;
            _label = GetComponentInChildren<TMP_Text>();
            _label.text = labelText;

            _button = GetComponent<Button>();
        }

        void Awake()
        {
            _button = GetComponent<Button>();
            _label = GetComponentInChildren<TMP_Text>();
        }

        /// <summary>
        /// Unity calls this on any mouse‐click.  We dispatch
        /// based on which button was pressed.
        /// </summary>
        public void OnPointerClick(PointerEventData evt)
        {
            if (evt.button == PointerEventData.InputButton.Left)
                OnLeftClick();
            else if (evt.button == PointerEventData.InputButton.Right)
                OnRightClick();
        }

        /// <summary>
        /// Called when the user left‐clicks this tab.
        /// </summary>
        protected abstract void OnLeftClick();

        /// <summary>
        /// Called when the user right‐clicks this tab.
        /// </summary>
        protected abstract void OnRightClick();

        /// <summary>
        /// Handy visual helper to show which tab is active.
        /// </summary>
        public void SetActiveVisual(bool isActive)
        {
            if (_button == null) _button = GetComponent<Button>();
            //Debug.Log(gameObject.name + " " + isActive);
            _button.image.color = isActive ? Color.white : Color.gray;
        }
    }
}
using Melanchall.DryWetMidi.Standards;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
    public class RhythmRowHeader : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown instrumentDropdown;
        [SerializeField] private Button removeButton;

        public event Action<int, GeneralMidiPercussion> InstrumentChanged;
        public event Action<int> RemoveClicked;

        private int rowIndex;

        void Awake()
        {
            // Fill once with enum names
            instrumentDropdown.ClearOptions();
            var names = Enum.GetNames(typeof(GeneralMidiPercussion));

            foreach (var n in names)
                instrumentDropdown.options.Add(new TMP_Dropdown.OptionData(n));

            instrumentDropdown.onValueChanged.AddListener(i =>
            {
                var value = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion),
                                                              instrumentDropdown.options[i].text);
                InstrumentChanged?.Invoke(rowIndex, value);
            });

            if (removeButton != null) 
                removeButton.onClick.AddListener(() => RemoveClicked?.Invoke(rowIndex));
        }

        public void Bind(int row, GeneralMidiPercussion current)
        {
            rowIndex = row;
            var name = current.ToString();
            int idx = instrumentDropdown.options.FindIndex(o => o.text == name);
            instrumentDropdown.SetValueWithoutNotify(idx >= 0 ? idx : 0);
            instrumentDropdown.RefreshShownValue();
        }
    }
}
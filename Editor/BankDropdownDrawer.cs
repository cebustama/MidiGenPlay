using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace MidiGenPlay
{
    [CustomPropertyDrawer(typeof(BankDropdownAttribute))]
    public class BankDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var midiInstrument = property.serializedObject.targetObject as MIDIInstrumentSO;
            if (midiInstrument == null)
            {
                EditorGUI.LabelField(position, label.text, "MIDI Instrument not found.");
                return;
            }

            List<string> banks = new List<string> { "No SoundFont Selected" };
            if (!string.IsNullOrEmpty(midiInstrument.SelectedSoundFont))
            {
                banks = SoundFontUtility.GetBanksForSoundFont(midiInstrument.SelectedSoundFont);
            }

            int currentIndex = banks.IndexOf(property.stringValue);
            if (currentIndex == -1 && banks.Count > 0) // If bank is invalid, reset to first valid bank
            {
                property.stringValue = banks[0];
                currentIndex = 0;
                midiInstrument.PatchName = ""; // Reset patchName because the bank changed
            }

            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, banks.ToArray());

            if (selectedIndex != currentIndex) // Detects change in Bank
            {
                property.stringValue = banks[selectedIndex];
                midiInstrument.PatchName = ""; // Reset patchName when Bank changes
                EditorUtility.SetDirty(midiInstrument); // Mark as dirty for saving
            }
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiGenPlay
{
    [CustomPropertyDrawer(typeof(SoundFontDropdownAttribute))]
    public class SoundFontDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var midiInstrument = property.serializedObject.targetObject as MIDIInstrumentSO;
            if (midiInstrument == null)
            {
                EditorGUI.LabelField(position, label.text, "MIDI Instrument not found.");
                return;
            }

            List<string> soundFontNames = SoundFontUtility.GetSoundFontNames();
            int currentIndex = Mathf.Max(soundFontNames.IndexOf(property.stringValue), 0);
            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, soundFontNames.ToArray());

            if (selectedIndex != currentIndex) // Detects change in SoundFont
            {
                // Update the selected SoundFont and reset dependent fields
                property.stringValue = soundFontNames[selectedIndex];
                midiInstrument.BankName = ""; // Reset bankName when SoundFont changes
                midiInstrument.PatchName = ""; // Reset patchName when SoundFont changes

                // Apply modified properties to the ScriptableObject
                EditorUtility.SetDirty(midiInstrument);
            }
        }
    }
}
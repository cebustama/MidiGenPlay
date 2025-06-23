using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace MidiGenPlay
{
    [CustomPropertyDrawer(typeof(PatchDropdownAttribute))]
    public class PatchDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var midiInstrument = property.serializedObject.targetObject as MIDIInstrumentSO;
            if (midiInstrument == null)
            {
                EditorGUI.LabelField(position, label.text, "MIDI Instrument not found.");
                return;
            }

            List<PatchData> patchesData = new List<PatchData>();
            if (!string.IsNullOrEmpty(midiInstrument.SelectedSoundFont) && !string.IsNullOrEmpty(midiInstrument.BankName))
            {
                int bankNumber;
                if (int.TryParse(midiInstrument.BankName, out bankNumber))
                {
                    patchesData = SoundFontUtility.GetPatchesDataForBank(midiInstrument.SelectedSoundFont, bankNumber);
                }
            }

            // Extract only patch names
            string[] patchNames = new string[patchesData.Count];
            for (int i = 0; i < patchesData.Count; i++)
            {
                patchNames[i] = patchesData[i].patchName;
            }

            // Find the selected index based on the current patch name
            int selectedIndex = Mathf.Max(patchesData.FindIndex(p => p.patchName == property.stringValue), 0);
            selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, patchNames);

            // Update the selected patch name and patch index in the MIDIInstrumentSO
            if (selectedIndex >= 0 && selectedIndex < patchesData.Count)
            {
                property.stringValue = patchesData[selectedIndex].patchName;
                midiInstrument.SetPatchIndex(patchesData[selectedIndex].patchNumber); // Store patch number
            }
        }
    }
}

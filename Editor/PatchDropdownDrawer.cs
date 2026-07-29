using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// INST-WIZ-1 / D-W2=B rewrite. This drawer was the main multi-edit
    /// corruptor: it wrote property.stringValue UNCONDITIONALLY on every
    /// repaint (a SerializedProperty write applies to ALL selected targets,
    /// so the first asset's patch stamped every selected asset just by
    /// drawing the inspector) and wrote PatchIndex by direct method call on
    /// the FIRST target only. Now:
    /// - Writes happen only on real user interaction (BeginChangeCheck).
    /// - The patch list needs a unique (SoundFont, Bank) pair; a mixed
    ///   selection disables the popup instead of guessing.
    /// - PatchName AND PatchIndex are written via SerializedProperty, so the
    ///   name/index pair stays coherent on every selected target, with undo.
    /// - An out-of-list current value renders as no selection; it is never
    ///   silently "repaired".
    /// </summary>
    [CustomPropertyDrawer(typeof(PatchDropdownAttribute))]
    public class PatchDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var so = property.serializedObject;
            var sfProp = so.FindProperty("SelectedSoundFont");
            var bankProp = so.FindProperty("BankName");
            if (sfProp == null || bankProp == null)
            {
                EditorGUI.LabelField(position, label.text, "SoundFont/Bank fields not found.");
                EditorGUI.EndProperty();
                return;
            }
            if (sfProp.hasMultipleDifferentValues || bankProp.hasMultipleDifferentValues)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(position, label.text, "— (mixed SoundFont/Bank)");
                EditorGUI.EndProperty();
                return;
            }
            if (string.IsNullOrEmpty(sfProp.stringValue) ||
                string.IsNullOrEmpty(bankProp.stringValue) ||
                !int.TryParse(bankProp.stringValue.Trim(), out int bankNumber))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(position, label.text, "Select a SoundFont and Bank first.");
                EditorGUI.EndProperty();
                return;
            }

            List<PatchData> patchesData =
                SoundFontUtility.GetPatchesDataForBank(sfProp.stringValue, bankNumber);
            if (patchesData == null || patchesData.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "No patches for this bank.");
                EditorGUI.EndProperty();
                return;
            }

            string[] patchNames = new string[patchesData.Count];
            for (int i = 0; i < patchesData.Count; i++)
                patchNames[i] = patchesData[i].patchName;

            int currentIndex = patchesData.FindIndex(p => p.patchName == property.stringValue); // -1 = unset

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, patchNames);
            bool userChanged = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (userChanged && selectedIndex >= 0 && selectedIndex < patchesData.Count)
            {
                property.stringValue = patchesData[selectedIndex].patchName;
                var idx = so.FindProperty("PatchIndex");
                if (idx != null) idx.intValue = patchesData[selectedIndex].patchNumber;
            }

            EditorGUI.EndProperty();
        }
    }
}
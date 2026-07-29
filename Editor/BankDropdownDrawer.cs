using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// INST-WIZ-1 / D-W2=B rewrite. Multi-edit-safe and silent-write-free.
    /// The old drawer auto-"repaired" an out-of-list value on every repaint
    /// (silent asset write) and reset PatchName by direct field access (first
    /// target only). Now:
    /// - The bank list is derived from SelectedSoundFont; if the selection
    ///   spans DIFFERENT soundfonts, the list is ambiguous and the popup is
    ///   disabled instead of guessing.
    /// - An out-of-list current value simply renders as no selection (-1);
    ///   nothing is written until the user picks.
    /// - On a real pick, BankName is written via this property and PatchName
    ///   is reset via a sibling property — all targets, undoable.
    /// </summary>
    [CustomPropertyDrawer(typeof(BankDropdownAttribute))]
    public class BankDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var so = property.serializedObject;
            var sfProp = so.FindProperty("SelectedSoundFont");
            if (sfProp == null)
            {
                EditorGUI.LabelField(position, label.text, "SelectedSoundFont not found.");
                EditorGUI.EndProperty();
                return;
            }
            if (sfProp.hasMultipleDifferentValues)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(position, label.text, "— (mixed SoundFonts)");
                EditorGUI.EndProperty();
                return;
            }
            if (string.IsNullOrEmpty(sfProp.stringValue))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(position, label.text, "No SoundFont selected.");
                EditorGUI.EndProperty();
                return;
            }

            List<string> banks = SoundFontUtility.GetBanksForSoundFont(sfProp.stringValue);
            if (banks == null || banks.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "No banks for this SoundFont.");
                EditorGUI.EndProperty();
                return;
            }

            int currentIndex = banks.IndexOf(property.stringValue); // -1 = unset/out of list

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, banks.ToArray());
            bool userChanged = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (userChanged && selectedIndex >= 0 && selectedIndex < banks.Count)
            {
                property.stringValue = banks[selectedIndex];
                var patch = so.FindProperty("PatchName");
                if (patch != null) patch.stringValue = ""; // bank changed => patch invalid
            }

            EditorGUI.EndProperty();
        }
    }
}
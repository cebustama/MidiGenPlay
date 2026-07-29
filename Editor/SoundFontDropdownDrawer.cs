using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// INST-WIZ-1 / D-W2=B rewrite. Multi-edit-safe and silent-write-free:
    /// - Writes ONLY on actual user interaction (BeginChangeCheck), never on
    ///   repaint. The old drawer treated "current value not in list" (-1 vs
    ///   Max(...,0)) as a change and rewrote assets just by being drawn.
    /// - All writes go through SerializedProperty (this property + the
    ///   dependent BankName/PatchName resets), so they apply consistently to
    ///   every selected target and are undoable. The old drawer mixed
    ///   property writes (all targets) with direct field writes (first
    ///   target only), which is what scrambled multi-selections.
    /// - Mixed values across a multi-selection render as showMixedValue.
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundFontDropdownAttribute))]
    public class SoundFontDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            List<string> soundFontNames = SoundFontUtility.GetSoundFontNames();
            if (soundFontNames == null || soundFontNames.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "No SoundFonts cached.");
                EditorGUI.EndProperty();
                return;
            }

            int currentIndex = soundFontNames.IndexOf(property.stringValue); // -1 if unset

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, soundFontNames.ToArray());
            bool userChanged = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (userChanged && selectedIndex >= 0 && selectedIndex < soundFontNames.Count)
            {
                property.stringValue = soundFontNames[selectedIndex];
                // Dependent resets via sibling properties => applied to ALL
                // selected targets, with undo, on ApplyModifiedProperties.
                var so = property.serializedObject;
                var bank = so.FindProperty("BankName");
                if (bank != null) bank.stringValue = "";
                var patch = so.FindProperty("PatchName");
                if (patch != null) patch.stringValue = "";
            }

            EditorGUI.EndProperty();
        }
    }
}
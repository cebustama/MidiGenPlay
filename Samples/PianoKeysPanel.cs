using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay
{
    public class PianoKeysPanel : MonoBehaviour
    {
        [SerializeField] private List<GameObject> octavePanels;

        /// <summary>
        /// Activates piano octaves based on the given min/max inclusive range.
        /// </summary>
        public void SetInteractableRange(int octaveMin, int octaveMax)
        {
            for (int i = 0; i < octavePanels.Count; i++)
            {
                int octaveNumber = i + 1; // octavePanels[0] is octave 1
                bool withinRange = octaveNumber >= octaveMin && octaveNumber <= octaveMax;

                // Enable or disable each button inside the octave
                var buttons = octavePanels[i].GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                    button.interactable = withinRange;
            }
        }
    }
}
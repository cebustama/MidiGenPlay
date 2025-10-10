using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.MusicTheory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;

public class PartSettingsPanelController : MonoBehaviour
{
    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown tonalityDropdown;
    [SerializeField] private TMP_Dropdown rootNoteDropdown;
    [SerializeField] private TMP_Dropdown tempoRangeDropdown;
    [SerializeField] private TMP_Dropdown timeSignatureDropdown;
    [SerializeField] private TMP_InputField measuresInput;

    private SongConfig.PartConfig boundPart;

    public void Bind(SongConfig.PartConfig part)
    {
        boundPart = part;

        // Populate and assign UI from data
        PopulateDropdown(tonalityDropdown, 
            System.Enum.GetNames(typeof(Tonality)), (int)part.Tonality);
        PopulateDropdown(rootNoteDropdown, 
            System.Enum.GetNames(typeof(NoteName)), (int)part.RootNote);
        PopulateDropdown(tempoRangeDropdown, 
            System.Enum.GetNames(typeof(TempoRange)), (int)part.TempoRange);
        PopulateDropdown(timeSignatureDropdown, 
            System.Enum.GetNames(typeof(TimeSignature)), (int)part.TimeSignature);

        measuresInput.text = part.Measures.ToString();

        tonalityDropdown.onValueChanged.AddListener(
            idx => part.Tonality = (Tonality)idx);
        rootNoteDropdown.onValueChanged.AddListener(
            idx => part.RootNote = (NoteName)idx);
        tempoRangeDropdown.onValueChanged.AddListener(
            idx => part.TempoRange = (TempoRange)idx);
        timeSignatureDropdown.onValueChanged.AddListener(
            idx => part.TimeSignature = (TimeSignature)idx);
        
        measuresInput.onEndEdit.AddListener(str => 
        { 
            if (int.TryParse(str, out var m)) 
                part.Measures = m; 
        });
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, string[] options, int selected)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
        dropdown.SetValueWithoutNotify(selected);
    }
}

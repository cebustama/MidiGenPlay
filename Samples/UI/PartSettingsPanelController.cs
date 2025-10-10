using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;
using MidiGenPlay.MusicTheory;
using TMPro;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

public class PartSettingsPanelController : MonoBehaviour
{
    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown tonalityDropdown;
    [SerializeField] private TMP_Dropdown rootNoteDropdown;
    [SerializeField] private TMP_Dropdown tempoRangeDropdown;
    [SerializeField] private TMP_Dropdown timeSignatureDropdown;
    [SerializeField] private TMP_Dropdown measuresDropdown;

    private static readonly int[] AllowedMeasures = { 2, 4, 8, 16, 32 };

    private SongConfig.PartConfig boundPart;

    // Expose change events so other systems can react
    public event System.Action<Tonality> OnTonalityChanged;
    public event System.Action<NoteName> OnRootChanged;
    public event System.Action<TempoRange> OnTempoRangeChanged;
    public event System.Action<TimeSignature> OnTimeSignatureChanged;
    public event System.Action<int> OnMeasuresChanged;

    public void Bind(SongConfig.PartConfig part)
    {
        // Clear old listeners to avoid stacking
        UnsubscribeAll();

        boundPart = part;

        PopulateDropdown(tonalityDropdown, 
            System.Enum.GetNames(typeof(Tonality)), (int)part.Tonality);
        PopulateDropdown(rootNoteDropdown, 
            System.Enum.GetNames(typeof(NoteName)), (int)part.RootNote);
        PopulateDropdown(tempoRangeDropdown, 
            System.Enum.GetNames(typeof(TempoRange)), (int)part.TempoRange);
        PopulateDropdown(timeSignatureDropdown, 
            System.Enum.GetNames(typeof(TimeSignature)), (int)part.TimeSignature);

        var labels = System.Array.ConvertAll(AllowedMeasures, m => m.ToString());
        measuresDropdown.ClearOptions();
        measuresDropdown.AddOptions(new System.Collections.Generic.List<string>(labels));

        int idx = System.Array.IndexOf(AllowedMeasures, part.Measures);
        if (idx < 0)
        {
            // Fallback: choose the closest allowed value
            int closest = AllowedMeasures[0];
            int minDiff = int.MaxValue;
            foreach (var m in AllowedMeasures)
            {
                int d = Mathf.Abs(m - part.Measures);
                if (d < minDiff) { minDiff = d; closest = m; }
            }
            idx = System.Array.IndexOf(AllowedMeasures, closest);
            // Write back the normalized value
            boundPart.Measures = closest;
        }
        measuresDropdown.SetValueWithoutNotify(idx);
        measuresDropdown.RefreshShownValue();

        tonalityDropdown.onValueChanged.AddListener(idx => {
            var val = (Tonality)idx; boundPart.Tonality = val; OnTonalityChanged?.Invoke(val);
        });
        rootNoteDropdown.onValueChanged.AddListener(idx => {
            var val = (NoteName)idx; boundPart.RootNote = val; OnRootChanged?.Invoke(val);
        });
        tempoRangeDropdown.onValueChanged.AddListener(idx => {
            var val = (TempoRange)idx; boundPart.TempoRange = val; OnTempoRangeChanged?.Invoke(val);
        });
        timeSignatureDropdown.onValueChanged.AddListener(idx => {
            var val = (TimeSignature)idx; boundPart.TimeSignature = val; OnTimeSignatureChanged?.Invoke(val);
        });
        measuresDropdown.onValueChanged.AddListener(i => 
        { 
            var m = AllowedMeasures[Mathf.Clamp(i, 0, AllowedMeasures.Length - 1)]; 
            boundPart.Measures = m; 
            OnMeasuresChanged?.Invoke(m); 
        });
    }

    public (TimeSignature ts, int beats, int measures, int subdivisions) GetSignatureSnapshot()
    {
        var ts = boundPart?.TimeSignature ?? TimeSignature.FourFour;
        int beats = MusicTheory.GetTimeSignatureDetails(ts).BeatsPerMeasure;
        int measures = boundPart?.Measures ?? 4;
        int subdivisions = 1;
        return (ts, beats, measures, subdivisions);
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, string[] options, int selected)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
        dropdown.SetValueWithoutNotify(selected);
        dropdown.RefreshShownValue();
    }

    private void UnsubscribeAll()
    {
        tonalityDropdown.onValueChanged.RemoveAllListeners();
        rootNoteDropdown.onValueChanged.RemoveAllListeners();
        tempoRangeDropdown.onValueChanged.RemoveAllListeners();
        timeSignatureDropdown.onValueChanged.RemoveAllListeners();
        measuresDropdown.onValueChanged.RemoveAllListeners();
    }

    private void OnDisable() => UnsubscribeAll();
}

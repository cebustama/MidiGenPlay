using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.UI
{
    public class BackingRoleUIController : ITrackRoleUIController
    {
        public TrackRole Role => TrackRole.Backing;

        private readonly GameObject chordPanel;
        private readonly TMP_Dropdown chordDropdown;
        private readonly TMP_Dropdown melodicInstrumentDropdown;
        private readonly Transform melodicInstrumentGroup;
        private readonly PianoKeysPanel piano;
        private readonly IPatternRepository patternRepo;
        private readonly IList<MIDIInstrumentSO> melodicInstruments;

        private List<ChordProgressionData> patterns = new();

        private ChordProgressionData currentSO;
        private int beatsPerMeasure;
        private int measures;

        public BackingRoleUIController(
            GameObject chordPanel,
            TMP_Dropdown chordDropdown,
            TMP_Dropdown melodicInstrumentDropdown,
            Transform melodicInstrumentGroup,
            PianoKeysPanel piano,
            IList<MIDIInstrumentSO> melodicInstruments,
            IPatternRepository patternRepo)
        {
            this.chordPanel = chordPanel;
            this.chordDropdown = chordDropdown;
            this.melodicInstrumentDropdown = melodicInstrumentDropdown;
            this.melodicInstrumentGroup = melodicInstrumentGroup;
            this.piano = piano;
            this.melodicInstruments = melodicInstruments;
            this.patternRepo = patternRepo;
        }

        public void RefreshPatterns(TimeSignature ts)
        {
            patterns = patternRepo.GetChordProgressions(ts).ToList();
            chordDropdown.ClearOptions();
            chordDropdown.AddOptions(patterns.Select(p => p.displayName).ToList());
            if (patterns.Count > 0) chordDropdown.value = 0;
        }

        public void LoadIntoUI(SongConfig.PartConfig.TrackConfig cfg)
        {
            int i = Mathf.Clamp(melodicInstruments.IndexOf(cfg.Instrument), 0, Mathf.Max(0, melodicInstruments.Count - 1));
            melodicInstrumentDropdown.SetValueWithoutNotify(i);
            melodicInstrumentDropdown.RefreshShownValue();

            if (melodicInstruments.Count > 0)
            {
                var inst = melodicInstruments[melodicInstrumentDropdown.value];
                piano.SetInteractableRange(inst.octaveMin, inst.octaveMax);
            }

            int p = patterns.IndexOf(cfg.Parameters.Pattern as ChordProgressionData);
            if (p < 0 && patterns.Count > 0) p = 0;
            if (p >= 0)
            {
                chordDropdown.SetValueWithoutNotify(p);
                chordDropdown.RefreshShownValue();
            }
        }

        public void SaveFromUI(SongConfig.PartConfig.TrackConfig cfg)
        {
            if (melodicInstruments.Count > 0)
                cfg.Instrument = melodicInstruments[Mathf.Clamp(melodicInstrumentDropdown.value, 0, melodicInstruments.Count - 1)];
            if (patterns.Count > 0)
                cfg.Parameters.Pattern = patterns[Mathf.Clamp(chordDropdown.value, 0, patterns.Count - 1)];
        }

        public void Activate(SongConfig.PartConfig.TrackConfig currentCfg)
        {
            chordPanel.SetActive(true);
        }

        public void Deactivate()
        {
            chordPanel.SetActive(false);
        }
    }
}
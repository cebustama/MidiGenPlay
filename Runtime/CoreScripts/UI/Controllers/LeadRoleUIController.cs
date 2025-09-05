using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.UI
{
    public class LeadRoleUIController : ITrackRoleUIController
    {
        public TrackRole Role => TrackRole.Lead;

        private readonly GameObject melodyPanel;
        private readonly TMP_Dropdown melodyDropdown;
        private readonly TMP_Dropdown melodicInstrumentDropdown;
        private readonly Transform melodicInstrumentGroup;
        private readonly PianoKeysPanel piano;
        private readonly IPatternRepository patternRepo;
        private readonly IList<MIDIInstrumentSO> melodicInstruments;

        private List<MelodyPatternData> patterns = new();

        public LeadRoleUIController(
            GameObject melodyPanel,
            TMP_Dropdown melodyDropdown,
            TMP_Dropdown melodicInstrumentDropdown,
            Transform melodicInstrumentGroup,
            PianoKeysPanel piano,
            IList<MIDIInstrumentSO> melodicInstruments,
            IPatternRepository patternRepo)
        {
            this.melodyPanel = melodyPanel;
            this.melodyDropdown = melodyDropdown;
            this.melodicInstrumentDropdown = melodicInstrumentDropdown;
            this.melodicInstrumentGroup = melodicInstrumentGroup;
            this.piano = piano;
            this.melodicInstruments = melodicInstruments;
            this.patternRepo = patternRepo;
        }

        public void RefreshPatterns(TimeSignature ts)
        {
            patterns = patternRepo.GetMelodyPatterns(ts).ToList();
            melodyDropdown.ClearOptions();
            melodyDropdown.AddOptions(patterns.Select(p => p.displayName).ToList());
            if (patterns.Count > 0) melodyDropdown.value = 0;
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

            int p = patterns.IndexOf(cfg.Parameters.Pattern as MelodyPatternData);
            if (p < 0 && patterns.Count > 0) p = 0;
            if (p >= 0)
            {
                melodyDropdown.SetValueWithoutNotify(p);
                melodyDropdown.RefreshShownValue();
            }
        }

        public void SaveFromUI(SongConfig.PartConfig.TrackConfig cfg)
        {
            if (melodicInstruments.Count > 0)
                cfg.Instrument = melodicInstruments[Mathf.Clamp(melodicInstrumentDropdown.value, 0, melodicInstruments.Count - 1)];
            if (patterns.Count > 0)
                cfg.Parameters.Pattern = patterns[Mathf.Clamp(melodyDropdown.value, 0, patterns.Count - 1)];
        }

        public void Activate(SongConfig.PartConfig.TrackConfig currentCfg)
        {
            melodyPanel.SetActive(true);
            melodicInstrumentGroup.gameObject.SetActive(true);
            if (melodicInstruments.Count > 0)
            {
                var inst = melodicInstruments[Mathf.Clamp(melodicInstrumentDropdown.value, 0, melodicInstruments.Count - 1)];
                piano.gameObject.SetActive(true);
                piano.SetInteractableRange(inst.octaveMin, inst.octaveMax);
            }
        }

        public void Deactivate()
        {
            melodyPanel.SetActive(false);
            melodicInstrumentGroup.gameObject.SetActive(false);
            piano.gameObject.SetActive(false);
        }
    }
}
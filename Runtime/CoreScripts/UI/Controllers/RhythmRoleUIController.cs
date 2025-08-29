using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MidiGenPlay.MusicTheory;

namespace MidiGenPlay.UI
{
    public class RhythmRoleUIController : ITrackRoleUIController
    {
        public TrackRole Role => TrackRole.Rhythm;

        private readonly GameObject drumPanel;
        private readonly TMP_Dropdown drumPatternDropdown;
        private readonly TMP_Dropdown percInstrumentDropdown;
        // parent GameObject to toggle (dropdown parent)
        private readonly Transform percInstrumentGroup; 
        private readonly IPatternRepository patternRepo;

        private readonly IList<MIDIPercussionInstrumentSO> percInstruments;

        // Local cache of filtered patterns for current time signature
        private List<DrumPatternData> patterns = new();

        public RhythmRoleUIController(
            GameObject drumPanel,
            TMP_Dropdown drumPatternDropdown,
            TMP_Dropdown percInstrumentDropdown,
            Transform percInstrumentGroup,
            IList<MIDIPercussionInstrumentSO> percInstruments,
            IPatternRepository patternRepo)
        {
            this.drumPanel = drumPanel;
            this.drumPatternDropdown = drumPatternDropdown;
            this.percInstrumentDropdown = percInstrumentDropdown;
            this.percInstrumentGroup = percInstrumentGroup;
            this.percInstruments = percInstruments;
            this.patternRepo = patternRepo;
        }

        public void RefreshPatterns(TimeSignature ts)
        {
            patterns = patternRepo.GetDrumPatterns(ts).ToList();
            drumPatternDropdown.ClearOptions();
            drumPatternDropdown.AddOptions(patterns.Select(p => p.displayName).ToList());
            if (patterns.Count > 0) drumPatternDropdown.value = 0;
        }

        public void LoadIntoUI(SongConfig.PartConfig.TrackConfig cfg)
        {
            // instrument
            int i = Mathf.Clamp(
                percInstruments.IndexOf(
                    cfg.PercussionInstrument), 0, Mathf.Max(0, percInstruments.Count - 1));
            percInstrumentDropdown.SetValueWithoutNotify(i);
            percInstrumentDropdown.RefreshShownValue();

            // pattern (may not exist if TS changed)
            int p = patterns.IndexOf(cfg.Parameters.Pattern as DrumPatternData);
            if (p < 0 && patterns.Count > 0) p = 0;
            if (p >= 0)
            {
                drumPatternDropdown.SetValueWithoutNotify(p);
                drumPatternDropdown.RefreshShownValue();
            }
        }

        public void SaveFromUI(SongConfig.PartConfig.TrackConfig cfg)
        {
            if (percInstruments.Count > 0)
                cfg.PercussionInstrument = 
                    percInstruments[
                        Mathf.Clamp(
                            percInstrumentDropdown.value, 0, percInstruments.Count - 1)];

            if (patterns.Count > 0)
                cfg.Parameters.Pattern = 
                    patterns[Mathf.Clamp(drumPatternDropdown.value, 0, patterns.Count - 1)];
        }

        public void Activate(SongConfig.PartConfig.TrackConfig currentCfg)
        {
            drumPanel.SetActive(true);
            percInstrumentGroup.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            drumPanel.SetActive(false);
            percInstrumentGroup.gameObject.SetActive(false);
        }
    }
}
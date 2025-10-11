using MidiGenPlay.Interfaces;
using MidiGenPlay.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;
using static PlasticPipe.Server.MonitorStats;
// Short aliases
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using TrackConfig = MidiGenPlay.SongConfig.PartConfig.TrackConfig;

namespace MidiGenPlay
{
    /// <summary>
    /// Owns all per-track UI and behavior:
    /// - Role dropdown + instrument groups (melodic/percussion)
    /// - Per-role pattern dropdowns/panels (chords, rhythm, melody)
    /// - Piano keys range feedback for melodic roles
    /// - Pattern binding: asset -> runtime clone in editor panel
    ///
    /// Extensibility:
    /// - New roles: register a new ITrackRoleUIController in BuildRoleControllers.
    /// - New per-role panels: serialize them and pass into the role controller.
    /// - Replace repositories or generators by injecting via Initialize(...).
    /// </summary>
    public class TrackDetailsPanel : MonoBehaviour
    {
        [Header("Role & Instruments")]
        [SerializeField] private TMP_Dropdown trackRoleDropdown;
        [SerializeField] private TMP_Dropdown melodicInstrumentDropdown;
        [SerializeField] private TMP_Dropdown percInstrumentDropdown;

        [Tooltip("Parent group for melodic instrument UI (toggle visibility).")]
        [SerializeField] private GameObject melodicInstrumentGroup;
        [Tooltip("Parent group for percussion instrument UI (toggle visibility).")]
        [SerializeField] private GameObject percInstrumentGroup;

        [Header("Per-Role Panels & Dropdowns")]
        // Rhythm
        [SerializeField] private GameObject drumSettingsPanel;
        [SerializeField] private TMP_Dropdown drumPatternDropdown;
        [SerializeField] private RhythmPatternPanelController rhythmPatternPanel;
        // Backing (Chords)
        [SerializeField] private GameObject chordSettingsPanel;
        [SerializeField] private TMP_Dropdown chordProgressionDropdown;
        [SerializeField] private ChordProgressionPanelController chordProgressionPanel;
        // Melody-ish (Lead/Melody/Harmony/Bassline share this for now)
        [SerializeField] private GameObject melodySettingsPanel;
        [SerializeField] private TMP_Dropdown melodyPatternDropdown;

        [Header("Other UI")]
        [SerializeField] private PianoKeysPanel pianoKeysPanel;

        [Header("Optional New/Save (Editor)")]
        [SerializeField] private Button saveChordButton;
        [SerializeField] private Button newChordButton;
        [SerializeField] private Button saveRhythmButton;
        [SerializeField] private Button newRhythmButton;

        // Injected services/data
        private MidiGenPlayConfig settings;
        private IPatternRepository patternRepo;
        private List<MIDIInstrumentSO> melodicInstruments = new();
        private List<MIDIPercussionInstrumentSO> percInstruments = new();

        // Role controllers
        private Dictionary<TrackRole, ITrackRoleUIController> roleControllers;

        // Current context
        private SongConfig.PartConfig boundPart;
        private TrackConfig boundTrack;
        private TimeSignature currentTS = TimeSignature.FourFour;

        public event Action<TrackRole> OnRoleChanged;

        #region Initialization
        public void Initialize(
            MidiGenPlayConfig config,
            List<MIDIInstrumentSO> melodic,
            List<MIDIPercussionInstrumentSO> percussion,
            IPatternRepository patterns)
        {
            settings = config;
            melodicInstruments = melodic ?? new List<MIDIInstrumentSO>();
            percInstruments = percussion ?? new List<MIDIPercussionInstrumentSO>();
            patternRepo = patterns;

            PopulateDropdownFromEnum<TrackRole>(trackRoleDropdown);
            PopulateInstruments();
            BuildRoleControllers();
            SubscribeUI();
        }

        private void PopulateInstruments()
        {
            melodicInstrumentDropdown.ClearOptions();
            melodicInstrumentDropdown.AddOptions(
                melodicInstruments.Select(i => i.InstrumentName).ToList());

            percInstrumentDropdown.ClearOptions();
            percInstrumentDropdown.AddOptions(
                percInstruments.Select(i => i.InstrumentName).ToList());
        }

        private void BuildRoleControllers()
        {
            // NOTE: controllers encapsulate per-role dropdowns/panels and instrument group
            roleControllers = new Dictionary<TrackRole, ITrackRoleUIController>
            {
                {
                    TrackRole.Rhythm,
                    new RhythmRoleUIController(
                        drumSettingsPanel,
                        drumPatternDropdown,
                        percInstrumentDropdown,
                        percInstrumentGroup != null ? percInstrumentGroup.transform : null,
                        percInstruments,
                        patternRepo
                    )
                },
                {
                    TrackRole.Backing,
                    new BackingRoleUIController(
                        chordSettingsPanel,
                        chordProgressionDropdown,
                        melodicInstrumentDropdown,
                        melodicInstrumentGroup != null ? melodicInstrumentGroup.transform : null,
                        pianoKeysPanel,
                        melodicInstruments,
                        patternRepo
                    )
                },
                {
                    TrackRole.Lead,
                    new LeadRoleUIController(
                        melodySettingsPanel,
                        melodyPatternDropdown,
                        melodicInstrumentDropdown,
                        melodicInstrumentGroup != null ? melodicInstrumentGroup.transform : null,
                        pianoKeysPanel,
                        melodicInstruments,
                        patternRepo
                    )
                }
            };

            // Temporary aliases that share the "Lead" controller (can diverge later)
            roleControllers[TrackRole.Melody] = roleControllers[TrackRole.Lead];
            roleControllers[TrackRole.Bassline] = roleControllers[TrackRole.Lead];
            roleControllers[TrackRole.Harmony] = roleControllers[TrackRole.Lead];

            RefreshPatterns(currentTS);
        }
        #endregion

        #region Binding
        /// <summary>Bind to a specific track within a part and load its UI.</summary>
        public void BindTrack(SongConfig.PartConfig part, TrackConfig track, TimeSignature ts)
        {
            boundPart = part;
            boundTrack = track;
            currentTS = ts;

            // Ensure pattern lists reflect this part's signature
            RefreshPatterns(ts);

            // Reflect track state into UI
            trackRoleDropdown.SetValueWithoutNotify((int)track.Role);
            DeactivateAllRoles();

            // Push data into UI, then pull dropdown selections back into cfg (to sync Parameters.Pattern)
            roleControllers[track.Role].LoadIntoUI(track);
            roleControllers[track.Role].SaveFromUI(track);

            // Ensure runtime binding where needed (chords/drums)
            BindRuntimePatternIfNeeded(track);

            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            if (track.Role == TrackRole.Rhythm)
                rhythmPatternPanel?.SetSignature(beats, part.Measures, 1);
            if (track.Role == TrackRole.Backing)
                chordProgressionPanel?.SetSignature(ts, part.Measures, 1);

            // Activate correct panel, toggle instrument groups, update piano range if melodic
            if (!roleControllers.TryGetValue(track.Role, out var controller))
            {
                Debug.LogWarning($"No controller for role {track.Role}");
                return;
            }
            controller.Activate(track);
            UpdateInstrumentGroupsForRole(track.Role, track);
        }

        /// <summary>Persist current UI values back into the given TrackConfig.</summary>
        public void SaveInto(TrackConfig cfg)
        {
            if (cfg == null) return;
            cfg.Role = (TrackRole)trackRoleDropdown.value;
            roleControllers[cfg.Role].SaveFromUI(cfg);
            BindRuntimePatternIfNeeded(cfg);
        }
        #endregion

        #region External updates (from Part panel)
        public void OnPartSignatureChanged(TimeSignature ts, int measures)
        {
            currentTS = ts;
            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            rhythmPatternPanel?.SetSignature(ts, measures, 1);
            chordProgressionPanel?.SetSignature(ts, measures, 1);
            RefreshPatterns(ts);
        }

        public void SetTonality(Tonality t)
        {
            chordProgressionPanel?.SetTonality(t);
        }
        #endregion

        #region UI events
        private void SubscribeUI()
        {
            melodicInstrumentDropdown.onValueChanged.AddListener(_ =>
            {
                if (boundTrack == null) return;
                if (boundTrack.Role == TrackRole.Rhythm) return;

                boundTrack.Instrument = melodicInstruments[Mathf.Clamp(
                    melodicInstrumentDropdown.value, 0, Mathf.Max(0, melodicInstruments.Count - 1))];

                if (boundTrack.Instrument != null)
                    pianoKeysPanel?.SetInteractableRange(boundTrack.Instrument.octaveMin, boundTrack.Instrument.octaveMax);

                SaveInto(boundTrack);
            });

            percInstrumentDropdown.onValueChanged.AddListener(_ =>
            {
                if (boundTrack == null) return;
                if (boundTrack.Role != TrackRole.Rhythm) return;

                boundTrack.PercussionInstrument = percInstruments[Mathf.Clamp(
                    percInstrumentDropdown.value, 0, Mathf.Max(0, percInstruments.Count - 1))];

                SaveInto(boundTrack);
            });

            trackRoleDropdown.onValueChanged.AddListener(_ => HandleRoleChanged());

            // Pattern dropdowns → bind editor panels and point cfg to runtime clone
            drumPatternDropdown.onValueChanged.AddListener(_ =>
            {
                if (boundTrack == null) return;
                SaveInto(boundTrack);

                if (boundTrack.Parameters?.Pattern is DrumPatternData asset)
                {
                    rhythmPatternPanel.Bind(asset);
                    boundTrack.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                }
            });

            chordProgressionDropdown.onValueChanged.AddListener(_ =>
            {
                if (boundTrack == null) return;
                SaveInto(boundTrack);

                if (boundTrack.Parameters?.Pattern is ChordProgressionData asset)
                {
                    chordProgressionPanel.Bind(asset);
                    boundTrack.Parameters.Pattern = chordProgressionPanel.GetRuntime();
                }
            });

            melodyPatternDropdown.onValueChanged.AddListener(_ =>
            {
                if (boundTrack == null) return;
                SaveInto(boundTrack);
            });

#if UNITY_EDITOR
            if (saveChordButton) saveChordButton.onClick.AddListener(SaveChordClicked);
            if (newChordButton) newChordButton.onClick.AddListener(NewChordClicked);
            if (saveRhythmButton) saveRhythmButton.onClick.AddListener(SaveRhythmClicked);
            if (newRhythmButton) newRhythmButton.onClick.AddListener(NewRhythmClicked);
#endif
        }

        private void HandleRoleChanged()
        {
            if (boundTrack == null) return;

            var role = (TrackRole)trackRoleDropdown.value;
            boundTrack.Role = role;

            DeactivateAllRoles();

            if (!roleControllers.TryGetValue(boundTrack.Role, out var controller))
            {
                Debug.LogWarning($"No controller for role {boundTrack.Role}");
                return;
            }

            controller.Activate(boundTrack);
            controller.LoadIntoUI(boundTrack);
            controller.SaveFromUI(boundTrack);

            BindRuntimePatternIfNeeded(boundTrack);
            UpdateInstrumentGroupsForRole(role, boundTrack);

            OnRoleChanged?.Invoke(role);
        }
        #endregion

        #region Helpers
        public void RefreshPatterns(TimeSignature ts)
        {
            foreach (var c in roleControllers.Values)
                c.RefreshPatterns(ts);
        }

        private void DeactivateAllRoles()
        {
            foreach (var c in roleControllers.Values) c.Deactivate();
        }

        private void UpdateInstrumentGroupsForRole(TrackRole role, TrackConfig cfg)
        {
            bool rhythm = role == TrackRole.Rhythm;
            if (percInstrumentGroup) percInstrumentGroup.SetActive(rhythm);
            if (melodicInstrumentGroup) melodicInstrumentGroup.SetActive(!rhythm);
            if (pianoKeysPanel) pianoKeysPanel.gameObject.SetActive(!rhythm);

            if (!rhythm && cfg?.Instrument != null)
                pianoKeysPanel?.SetInteractableRange(cfg.Instrument.octaveMin, cfg.Instrument.octaveMax);
        }

        private void BindRuntimePatternIfNeeded(TrackConfig cfg)
        {
            if (cfg == null || cfg.Parameters == null) return;

            if (cfg.Role == TrackRole.Backing && cfg.Parameters.Pattern is ChordProgressionData ch)
            {
                if (chordProgressionPanel.GetOriginalAsset() != ch)
                    chordProgressionPanel.Bind(ch);
                cfg.Parameters.Pattern = chordProgressionPanel.GetRuntime();
            }
            else if (cfg.Role == TrackRole.Rhythm && cfg.Parameters.Pattern is DrumPatternData dr)
            {
                if (rhythmPatternPanel.GetOriginalAsset() != dr)
                    rhythmPatternPanel.Bind(dr);
                cfg.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
            }
        }

        private void PopulateDropdownFromEnum<T>(TMP_Dropdown dropdown) where T : Enum
        {
            dropdown.ClearOptions();
            var names = Enum.GetNames(typeof(T))
                            .Select(n => new TMP_Dropdown.OptionData(n))
                            .ToList();
            dropdown.AddOptions(names);
            dropdown.RefreshShownValue();
        }
        #endregion

        #region Editor Save/New (kept here to keep runtime panel self-contained)
#if UNITY_EDITOR
        private void SaveChordClicked()
        {
            ChordProgressionData result = null;

            if (chordProgressionPanel.GetOriginalAsset() == null)
            {
                var folder = settings.GetChordWriteFolder();
                result = chordProgressionPanel.SaveRuntimeAsNewAsset(folder);
            }
            else
            {
                chordProgressionPanel.SaveRuntimeIntoAsset();
                result = chordProgressionPanel.GetOriginalAsset();
            }

            patternRepo.Refresh();
            RefreshPatterns(currentTS);
            SelectChordDropdownForAsset(result);
            Debug.Log("<b>[TrackDetails]</b> Progression saved.");
        }

        private void NewChordClicked()
        {
            if (boundPart == null) return;
            chordProgressionPanel.CreateNewRuntime(
                boundPart.Tonality, boundPart.TimeSignature, boundPart.Measures, subdivisions: 1);

            if (boundTrack != null && boundTrack.Role == TrackRole.Backing)
                boundTrack.Parameters.Pattern = chordProgressionPanel.GetRuntime();
        }

        private void SaveRhythmClicked()
        {
            DrumPatternData result = null;

            if (rhythmPatternPanel.GetOriginalAsset() == null)
            {
                var folder = settings.GetDrumWriteFolder();
                result = rhythmPatternPanel.SaveRuntimeAsNewAsset(folder);
            }
            else
            {
                rhythmPatternPanel.SaveRuntimeIntoAsset();
                result = rhythmPatternPanel.GetOriginalAsset();
            }

            patternRepo.Refresh();
            RefreshPatterns(currentTS);
            SelectRhythmDropdownForAsset(result);
            Debug.Log("<b>[TrackDetails]</b> Drum pattern saved.");
        }

        private void NewRhythmClicked()
        {
            if (boundPart == null) return;
            var beats = GetTimeSignatureDetails(boundPart.TimeSignature).BeatsPerMeasure;
            rhythmPatternPanel.CreateNewRuntime(boundPart.TimeSignature, boundPart.Measures, subdivisions: 1);

            if (boundTrack != null && boundTrack.Role == TrackRole.Rhythm)
                boundTrack.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
        }

        private void SelectChordDropdownForAsset(ChordProgressionData asset)
        {
            if (!asset) return;
            var disp = string.IsNullOrEmpty(asset.displayName) ? asset.name : asset.displayName;

            int idx = chordProgressionDropdown.options.FindIndex(o => o.text == disp || o.text == asset.name);
            if (idx < 0) { Debug.LogWarning($"[TrackDetails] Could not find '{disp}' in chord dropdown."); return; }

            chordProgressionDropdown.value = idx;
            chordProgressionDropdown.RefreshShownValue();

            if (boundTrack != null && boundTrack.Role == TrackRole.Backing)
            {
                chordProgressionPanel.Bind(asset);
                boundTrack.Parameters.Pattern = chordProgressionPanel.GetRuntime();
            }
        }

        private void SelectRhythmDropdownForAsset(DrumPatternData asset)
        {
            if (!asset) return;
            var disp = string.IsNullOrEmpty(asset.displayName) ? asset.name : asset.displayName;

            int idx = drumPatternDropdown.options.FindIndex(o => o.text == disp || o.text == asset.name);
            if (idx < 0) { Debug.LogWarning($"[TrackDetails] Could not find '{disp}' in drum dropdown."); return; }

            drumPatternDropdown.value = idx;
            drumPatternDropdown.RefreshShownValue();

            if (boundTrack != null && boundTrack.Role == TrackRole.Rhythm)
            {
                rhythmPatternPanel.Bind(asset);
                boundTrack.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
            }
        }
#endif
        #endregion

        private void OnDestroy()
        {
            melodicInstrumentDropdown.onValueChanged.RemoveAllListeners();
            percInstrumentDropdown.onValueChanged.RemoveAllListeners();
            trackRoleDropdown.onValueChanged.RemoveAllListeners();
            drumPatternDropdown.onValueChanged.RemoveAllListeners();
            chordProgressionDropdown.onValueChanged.RemoveAllListeners();
            melodyPatternDropdown.onValueChanged.RemoveAllListeners();
        }
    }
}

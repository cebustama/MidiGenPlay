using MidiGenPlay.Interfaces;
using MidiGenPlay.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;
using TrackConfig = MidiGenPlay.SongConfig.PartConfig.TrackConfig;

namespace MidiGenPlay
{
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

        // Domain
        private ISongConfigManager manager;
        private PartIdx currentPartIdx = new PartIdx(-1);
        private TrackIdx currentTrackIdx = new TrackIdx(-1);

        // Role controllers (UI-only helpers)
        private Dictionary<TrackRole, ITrackRoleUIController> roleControllers;

        // Current context (for convenience)
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

        public void SetManager(ISongConfigManager m) => manager = m;

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

            // Map bound references to indices for manager calls
            UpdateIndicesFromRefs(part, track);

            RefreshPatterns(ts);

            // Reflect track state into UI
            trackRoleDropdown.SetValueWithoutNotify((int)track.Role);
            DeactivateAllRoles();

            roleControllers[track.Role].LoadIntoUI(track);
            roleControllers[track.Role].SaveFromUI(track); // keep UI + cfg aligned

            BindRuntimePatternIfNeeded(track);

            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            if (track.Role == TrackRole.Rhythm)
                rhythmPatternPanel?.SetSignature(beats, part.Measures, 1);
            if (track.Role == TrackRole.Backing)
                chordProgressionPanel?.SetSignature(ts, part.Measures, 1);

            if (!roleControllers.TryGetValue(track.Role, out var controller))
            {
                Debug.LogWarning($"No controller for role {track.Role}");
                return;
            }
            controller.Activate(track);
            UpdateInstrumentGroupsForRole(track.Role, track);
        }

        /// <summary>Persist current UI values via the manager (keeps manager authoritative).</summary>
        public void SaveInto(TrackConfig _)
        {
            if (!ValidIndices()) return;

            var role = (TrackRole)trackRoleDropdown.value;
            manager.SetTrackRole(currentPartIdx, currentTrackIdx, role);

            if (role == TrackRole.Rhythm)
            {
                var pIdx = Mathf.Clamp(percInstrumentDropdown.value, 0, Mathf.Max(0, percInstruments.Count - 1));
                var perc = percInstruments.Count > 0 ? percInstruments[pIdx] : null;
                manager.SetPercInstrument(currentPartIdx, currentTrackIdx, perc);

                // Ensure runtime clone is the one we set on the model
                if (boundTrack?.Parameters?.Pattern is DrumPatternData dAsset)
                {
                    rhythmPatternPanel.Bind(dAsset);
                    manager.SetTrackPattern(currentPartIdx, currentTrackIdx, rhythmPatternPanel.GetRuntime());
                }
            }
            else
            {
                var mIdx = Mathf.Clamp(melodicInstrumentDropdown.value, 0, Mathf.Max(0, melodicInstruments.Count - 1));
                var inst = melodicInstruments.Count > 0 ? melodicInstruments[mIdx] : null;
                manager.SetMelodicInstrument(currentPartIdx, currentTrackIdx, inst);

                if (inst != null)
                    pianoKeysPanel?.SetInteractableRange(inst.octaveMin, inst.octaveMax);

                if (boundTrack?.Parameters?.Pattern is ChordProgressionData cAsset)
                {
                    chordProgressionPanel.Bind(cAsset);
                    manager.SetTrackPattern(currentPartIdx, currentTrackIdx, chordProgressionPanel.GetRuntime());
                }
                else if (boundTrack?.Parameters?.Pattern != null)
                {
                    // Melody patterns etc.
                    manager.SetTrackPattern(currentPartIdx, currentTrackIdx, boundTrack.Parameters.Pattern);
                }
            }
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
                if (!ValidIndices()) return;
                if (boundTrack == null || boundTrack.Role == TrackRole.Rhythm) return;

                var idx = Mathf.Clamp(melodicInstrumentDropdown.value, 0, Mathf.Max(0, melodicInstruments.Count - 1));
                var inst = melodicInstruments.Count > 0 ? melodicInstruments[idx] : null;
                manager?.SetMelodicInstrument(currentPartIdx, currentTrackIdx, inst);

                if (inst != null)
                    pianoKeysPanel?.SetInteractableRange(inst.octaveMin, inst.octaveMax);
            });

            percInstrumentDropdown.onValueChanged.AddListener(_ =>
            {
                if (!ValidIndices()) return;
                if (boundTrack == null || boundTrack.Role != TrackRole.Rhythm) return;

                var idx = Mathf.Clamp(percInstrumentDropdown.value, 0, Mathf.Max(0, percInstruments.Count - 1));
                var inst = percInstruments.Count > 0 ? percInstruments[idx] : null;
                manager?.SetPercInstrument(currentPartIdx, currentTrackIdx, inst);
            });

            trackRoleDropdown.onValueChanged.AddListener(_ => HandleRoleChanged());

            // Pattern dropdowns → bind editor panels and push runtime clone via manager
            drumPatternDropdown.onValueChanged.AddListener(_ =>
            {
                if (!ValidIndices() || boundTrack == null) return;

                if (boundTrack.Parameters?.Pattern is DrumPatternData asset)
                {
                    rhythmPatternPanel.Bind(asset);
                    manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, rhythmPatternPanel.GetRuntime());
                }
            });

            chordProgressionDropdown.onValueChanged.AddListener(_ =>
            {
                if (!ValidIndices() || boundTrack == null) return;

                if (boundTrack.Parameters?.Pattern is ChordProgressionData asset)
                {
                    chordProgressionPanel.Bind(asset);
                    manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, chordProgressionPanel.GetRuntime());
                }
            });

            melodyPatternDropdown.onValueChanged.AddListener(_ =>
            {
                if (!ValidIndices() || boundTrack == null) return;
                if (boundTrack.Parameters?.Pattern != null)
                    manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, boundTrack.Parameters.Pattern);
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
            if (!ValidIndices() || boundTrack == null) return;

            var role = (TrackRole)trackRoleDropdown.value;
            manager?.SetTrackRole(currentPartIdx, currentTrackIdx, role);

            DeactivateAllRoles();

            if (!roleControllers.TryGetValue(role, out var controller))
            {
                Debug.LogWarning($"No controller for role {role}");
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
                // Keep UI clone current; manager gets it on SaveInto or dropdown change
            }
            else if (cfg.Role == TrackRole.Rhythm && cfg.Parameters.Pattern is DrumPatternData dr)
            {
                if (rhythmPatternPanel.GetOriginalAsset() != dr)
                    rhythmPatternPanel.Bind(dr);
            }
        }

        private void UpdateIndicesFromRefs(SongConfig.PartConfig part, TrackConfig track)
        {
            if (manager == null || manager.Song?.Parts == null) { currentPartIdx = new PartIdx(-1); currentTrackIdx = new TrackIdx(-1); return; }

            int p = manager.Song.Parts.IndexOf(part);
            if (p < 0) { currentPartIdx = new PartIdx(-1); currentTrackIdx = new TrackIdx(-1); return; }

            currentPartIdx = new PartIdx(p);

            var tracks = manager.Song.Parts[p].Tracks;
            int t = (tracks != null) ? tracks.IndexOf(track) : -1;
            currentTrackIdx = new TrackIdx(t);
        }

        private bool ValidIndices() => manager != null && currentPartIdx.Value >= 0 && currentTrackIdx.Value >= 0;

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

        #region Editor Save/New
#if UNITY_EDITOR
        private void SaveChordClicked()
        {
            if (!ValidIndices()) return;

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

            if (result != null)
                manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, chordProgressionPanel.GetRuntime());

            Debug.Log("<b>[TrackDetails]</b> Progression saved.");
        }

        private void NewChordClicked()
        {
            if (!ValidIndices() || boundPart == null) return;

            chordProgressionPanel.CreateNewRuntime(boundPart.Tonality, boundPart.TimeSignature, boundPart.Measures, subdivisions: 1);
            manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, chordProgressionPanel.GetRuntime());
        }

        private void SaveRhythmClicked()
        {
            if (!ValidIndices()) return;

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

            if (result != null)
                manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, rhythmPatternPanel.GetRuntime());

            Debug.Log("<b>[TrackDetails]</b> Drum pattern saved.");
        }

        private void NewRhythmClicked()
        {
            if (!ValidIndices() || boundPart == null) return;

            rhythmPatternPanel.CreateNewRuntime(boundPart.TimeSignature, boundPart.Measures, subdivisions: 1);
            manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, rhythmPatternPanel.GetRuntime());
        }

        private void SelectChordDropdownForAsset(ChordProgressionData asset)
        {
            if (!asset) return;
            var disp = string.IsNullOrEmpty(asset.DisplayName) ? asset.name : asset.DisplayName;

            int idx = chordProgressionDropdown.options.FindIndex(o => o.text == disp || o.text == asset.name);
            if (idx < 0) { Debug.LogWarning($"[TrackDetails] Could not find '{disp}' in chord dropdown."); return; }

            chordProgressionDropdown.value = idx;
            chordProgressionDropdown.RefreshShownValue();

            manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, chordProgressionPanel.GetRuntime());
        }

        private void SelectRhythmDropdownForAsset(DrumPatternData asset)
        {
            if (!asset) return;
            var disp = string.IsNullOrEmpty(asset.DisplayName) ? asset.name : asset.DisplayName;

            int idx = drumPatternDropdown.options.FindIndex(o => o.text == disp || o.text == asset.name);
            if (idx < 0) { Debug.LogWarning($"[TrackDetails] Could not find '{disp}' in drum dropdown."); return; }

            drumPatternDropdown.value = idx;
            drumPatternDropdown.RefreshShownValue();

            manager?.SetTrackPattern(currentPartIdx, currentTrackIdx, rhythmPatternPanel.GetRuntime());
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

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Interfaces;
using MidiGenPlay.Services;
using MidiGenPlay.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;
using static MidiGenPlay.SongConfig.PartConfig;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay
{
    public class GenerateMidiSongPanel : MonoBehaviour
    {
        [Header("Midi Player Reference")]
        [Tooltip("Any component on this GameObject that implements IPlayMidi")]
        [SerializeField] private MonoBehaviour midiPlayerAdapter = null;

        [Header("MidiGenPlay Settings")]
        [SerializeField] private MidiGenPlayConfig settings;

        [SerializeField] private PartListController partListController;
        [SerializeField] private PartSettingsPanelController partSettingsPanel;

        [Header("Track Tabs")]
        [SerializeField] private Transform trackTabContainer;
        [SerializeField] private TrackTabButton trackTabButtonPrefab;

        [Header("Track Settings UI")]
        [SerializeField] private TMP_Dropdown melodicInstrumentDropdown;
        [SerializeField] private TMP_Dropdown percInstrumentDropdown;
        [SerializeField] private TMP_Dropdown trackRoleDropdown;

        [Header("Per-Role Panels")]
        // Percussion
        [SerializeField] private GameObject drumSettingsPanel;
        [SerializeField] private TMP_Dropdown drumPatternDropdown;
        [SerializeField] private RhythmPatternPanelController rhythmPatternPanel;
        // Chords
        [SerializeField] private GameObject chordSettingsPanel;
        [SerializeField] private TMP_Dropdown chordProgressionDropdown;
        [SerializeField] private ChordProgressionPanelController chordProgressionPanel;
        [SerializeField] private PatternGrid chordPatternGrid;
        // Melodies
        [SerializeField] private GameObject melodySettingsPanel;
        [SerializeField] private TMP_Dropdown melodyPatternDropdown;
        // Piano Keys
        [SerializeField] private PianoKeysPanel pianoKeysPanel;

        [Header("Controls")]
        [SerializeField] private Button newTrackButton;
        [SerializeField] private Button generateButton;
        [SerializeField] private Toggle useMetronomeToggle;
        [SerializeField] private Toggle loopToggle;
        // TODO: Move to ChordProgression-specific component
        [SerializeField] private Button saveChordButton;
        [SerializeField] private Button newChordButton;
        // TODO: Rhythm specific component
        [SerializeField] private Button saveRhythmButton;
        [SerializeField] private Button newRhythmButton;

        [Header("Defaults")]
        [SerializeField] private TrackRole defaultTrackRole = TrackRole.Backing;

        [Header("Config I/O")]
        [SerializeField] private Button saveConfigButton;
        [SerializeField] private TMP_Dropdown loadConfigDropdown;

        [Header("Input Text")]
        [SerializeField] private TMP_InputField sequenceInputField;

        
        private SongConfig songConfig = new SongConfig();
        private SongConfig.PartConfig part => songConfig.Parts[activePart];
        private List<SongConfig.PartConfig.TrackConfig> tracks => part.Tracks;

        private List<TrackTabButton> trackTabs = new List<TrackTabButton>();
        private int activePart = -1;
        private int activeTrack = -1;

        // loaded scriptables:
        private List<MIDIInstrumentSO> melodicInstruments;
        private List<MIDIPercussionInstrumentSO> percInstruments;
        private List<SongConfigSO> availableConfigs = new List<SongConfigSO>();

        private IPlayMidi midiPlayer => midiPlayerAdapter as IPlayMidi;
        private MidiGenerator midiGenerator;
        private MidiFile lastSong;

        // Services
        private IInstrumentRepository instrumentRepo;
        private IPatternRepository patternRepo;

        private ISequenceSerializer sequenceSerializer;
        private IMidiPlayback midiPlayback;
        private ISongConfigStore configStore;

        private Dictionary<TrackRole, ITrackRoleUIController> roleControllers;

        private void Awake()
        {
            if (midiPlayer == null)
            {
                Debug.LogError($"{nameof(midiPlayerAdapter)} must implement IPlayMidi");
                return;
            }

            midiPlayback = new MidiPlayback(midiPlayer);
            midiPlayer.OnSongEnded += HandleSongEnded;

            // Load MGP system settings
            if (settings == null) 
                settings = MidiGenPlayConfig.FindInResources() ?? 
                    ScriptableObject.CreateInstance<MidiGenPlayConfig>();

            songConfig = new SongConfig();
            songConfig.Parts = new List<SongConfig.PartConfig>();
            configStore = new SongConfigStoreResources();

            instrumentRepo = new InstrumentRepositoryResources(settings);
            patternRepo = new PatternRepositoryResources(settings);
            sequenceSerializer = new SequenceSerializer();

            PopulateAllDropdowns();
            BuildUIControllers();
            SubscribeUIChanges();

            // Subscribe parts
            partListController.OnPartSelected += OnPartSelected;
            partListController.OnPartAddClicked += OnAddPart;
            partListController.OnPartRemoveClicked += OnRemovePartClicked;

            partSettingsPanel.OnTimeSignatureChanged += HandleTimeSignatureChanged;
            partSettingsPanel.OnMeasuresChanged += HandleMeasuresChanged;
            partSettingsPanel.OnTonalityChanged += t => chordProgressionPanel.SetTonality(t);

            RefreshPartTabs();


            // TODO: Wire buttons method
            newTrackButton.onClick.AddListener(AddNewTrack);

            midiGenerator = new MidiGenerator(settings);

            generateButton.onClick.AddListener(OnGenerateAndPlay);
            
            if (saveChordButton != null)
            {
                saveChordButton.onClick.AddListener(() =>
                {
#if UNITY_EDITOR
                    ChordProgressionData createdOrOverwritten = null;

                    if (chordProgressionPanel.GetOriginalAsset() == null)
                    {
                        var targetFolder = settings.GetChordWriteFolder();
                        createdOrOverwritten = 
                            chordProgressionPanel.SaveRuntimeAsNewAsset(targetFolder);
                    }
                    else
                    {
                        chordProgressionPanel.SaveRuntimeIntoAsset();
                        createdOrOverwritten = chordProgressionPanel.GetOriginalAsset();
                    }

                    patternRepo.Refresh();
                    var ts = songConfig.Parts[activePart].TimeSignature;
                    FilterAndRefreshPatternLists(ts);

                    if (createdOrOverwritten != null)
                        SelectChordDropdownForAsset(createdOrOverwritten);
#endif
                    Debug.Log("<b>[UI]</b> Progression saved.");
                });
            }

            if (newChordButton != null)
            {
                newChordButton.onClick.AddListener(() =>
                {
                    var p = songConfig.Parts[activePart];
                    chordProgressionPanel.CreateNewRuntime(
                        p.Tonality, p.TimeSignature, p.Measures, subdivisions: 1);

                    // Point the active backing track to the runtime object so
                    // "Generate" uses it
                    if (activeTrack >= 0 && tracks[activeTrack].Role == TrackRole.Backing)
                    {
                        var cfg = tracks[activeTrack];
                        cfg.Parameters.Pattern = chordProgressionPanel.GetRuntime();
                        Debug.Log("[UI] New runtime progression created and assigned to the " +
                            "Backing track.");
                    }
                });
            }

            if (saveRhythmButton != null)
            {
                saveRhythmButton.onClick.AddListener(() =>
                {
#if UNITY_EDITOR
                    DrumPatternData createdOrOverwritten = null;

                    if (rhythmPatternPanel.GetOriginalAsset() == null)
                    {
                        var targetFolder = settings.GetDrumWriteFolder();
                        createdOrOverwritten = rhythmPatternPanel.SaveRuntimeAsNewAsset(targetFolder);
                    }
                    else
                    {
                        rhythmPatternPanel.SaveRuntimeIntoAsset();
                        createdOrOverwritten = rhythmPatternPanel.GetOriginalAsset();
                    }

                    patternRepo.Refresh();
                    var ts = songConfig.Parts[activePart].TimeSignature;
                    FilterAndRefreshPatternLists(ts);

                    if (createdOrOverwritten != null)
                        SelectRhythmDropdownForAsset(createdOrOverwritten);
#endif
                    Debug.Log("<b>[UI]</b> Drum pattern saved.");
                });
            }

            if (newRhythmButton != null)
            {
                newRhythmButton.onClick.AddListener(() =>
                {
                    var p = songConfig.Parts[activePart];
                    var beats = GetTimeSignatureDetails(p.TimeSignature).BeatsPerMeasure;
                    rhythmPatternPanel.CreateNewRuntime(
                        p.TimeSignature, p.Measures, subdivisions: 1);

                    // Point the active Rhythm track to the panel's runtime so Generate uses it
                    if (activeTrack >= 0 && tracks[activeTrack].Role == TrackRole.Rhythm)
                    {
                        var cfg = tracks[activeTrack];
                        cfg.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                        Debug.Log("[UI] New runtime drum pattern created and assigned to the Rhythm track.");
                    }
                });
            }


#if UNITY_EDITOR
            saveConfigButton.onClick.AddListener(OnSaveConfigClicked);
#endif
            OnAddPart();

            if (settings.defaultSeed != 0)
                UnityEngine.Random.InitState(settings.defaultSeed);
        }

        private void OnDestroy()
        {
            if (midiPlayer != null) midiPlayer.OnSongEnded -= HandleSongEnded;
        }

        private void PopulateAllDropdowns()
        {
            PopulateLoadConfigDropdown();
            PopulateInstruments();

            // Track Roles
            PopulateDropdownFromEnum<TrackRole>(trackRoleDropdown);

            patternRepo.Refresh();
        }

        private void BuildUIControllers()
        {
            // Build role controllers
            roleControllers = new Dictionary<TrackRole, ITrackRoleUIController>
            {
                {
                    TrackRole.Rhythm,
                    new RhythmRoleUIController(
                        drumSettingsPanel,
                        drumPatternDropdown,
                        percInstrumentDropdown,
                        percInstrumentDropdown.transform.parent, // group to toggle
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
                        melodicInstrumentDropdown.transform.parent, // group to toggle
                        pianoKeysPanel,
                        melodicInstruments,
                        patternRepo,
                        chordPatternGrid
                    )
                },
                {
                    TrackRole.Lead,
                    new LeadRoleUIController(
                        melodySettingsPanel,
                        melodyPatternDropdown,
                        melodicInstrumentDropdown,
                        melodicInstrumentDropdown.transform.parent, // group to toggle
                        pianoKeysPanel,
                        melodicInstruments,
                        patternRepo
                    )
                }
            };

            // TEMP
            roleControllers[TrackRole.Melody] = roleControllers[TrackRole.Lead];
            roleControllers[TrackRole.Bassline] = roleControllers[TrackRole.Lead];
            roleControllers[TrackRole.Harmony] = roleControllers[TrackRole.Lead];

            // Initial pattern lists per role based on current TS
            var ts = (songConfig.Parts.Count > 0) ? 
                songConfig.Parts[
                    Mathf.Clamp(activePart, 0, songConfig.Parts.Count - 1)].TimeSignature
                : TimeSignature.FourFour;

            foreach (var c in roleControllers.Values) c.RefreshPatterns(ts);
        }

        private void PopulateInstruments()
        {
            instrumentRepo.Refresh();

            melodicInstruments = instrumentRepo.GetMelodicInstruments().ToList();
            percInstruments = instrumentRepo.GetPercussionInstruments().ToList();

            melodicInstrumentDropdown.ClearOptions();
            melodicInstrumentDropdown.AddOptions(
                melodicInstruments.Select(i => i.InstrumentName).ToList());

            percInstrumentDropdown.ClearOptions();
            percInstrumentDropdown.AddOptions(
                percInstruments.Select(i => i.InstrumentName).ToList());
        }

        private void PopulateLoadConfigDropdown()
        {
            loadConfigDropdown.ClearOptions();

            configStore.Refresh();
            availableConfigs = configStore.GetAll().ToList();

            var optionNames = new List<string> { "-" };
            optionNames.AddRange(availableConfigs.Select(so => so.name));

            loadConfigDropdown.AddOptions(optionNames
                .Select(n => new TMP_Dropdown.OptionData(n))
                .ToList());
            loadConfigDropdown.RefreshShownValue();

            // Make sure we don't double-subscribe if this gets called again
            loadConfigDropdown.onValueChanged.RemoveAllListeners();
            loadConfigDropdown.onValueChanged.AddListener(OnLoadConfigDropdownChanged);
        }

        private void OnLoadConfigDropdownChanged(int selectedIndex)
        {
            // 0 means “–” → reset to a brand new song
            if (selectedIndex == 0)
            {
                // clear data
                songConfig = new SongConfig
                {
                    Parts = new List<SongConfig.PartConfig>(),
                    Structure = new List<SongConfig.PartSequenceEntry>()
                };

                sequenceInputField.SetTextWithoutNotify(string.Empty);

                // start fresh
                OnAddPart();
                return;
            }

            // otherwise load the chosen SO
            var so = availableConfigs[selectedIndex - 1];
            LoadSongConfigSO(so);
        }

        private void LoadSongConfigSO(SongConfigSO so)
        {
            // Use the store to produce a deep runtime clone from the asset
            songConfig = configStore.CloneFromAsset(so);

            // repopulate the sequence input
            sequenceInputField.SetTextWithoutNotify(
                sequenceSerializer.Serialize(songConfig.Structure)
            );

            partListController.SetPartTabs(songConfig.Parts.Count, 0);
            OnPartSelected(0);
        }

        private void SubscribeUIChanges()
        {
            // whenever the user tweaks one of these, write it back into tracks[activeTrack]
            percInstrumentDropdown.onValueChanged.AddListener(_ =>
            {
                if (activeTrack >= 0 && tracks[activeTrack].Role == TrackRole.Rhythm)
                    SaveTrack(activeTrack);
            });
            melodicInstrumentDropdown.onValueChanged.AddListener(_ =>
            {
                if (activeTrack >= 0 && tracks[activeTrack].Role != TrackRole.Rhythm)
                {
                    SaveTrack(activeTrack);
                    var instrument = melodicInstruments[melodicInstrumentDropdown.value];
                    pianoKeysPanel.SetInteractableRange(instrument.octaveMin, instrument.octaveMax);
                }
            });

            trackRoleDropdown.onValueChanged.AddListener(_ => {
                //SaveTrack(activeTrack);
                OnRoleChanged();
            });

            // Patterns
            drumPatternDropdown.onValueChanged.AddListener(_ => 
            {
                SaveTrack(activeTrack);

                var cfg = tracks[activeTrack];
                var asset = cfg.Parameters?.Pattern as DrumPatternData;
                if (asset != null)
                {
                    rhythmPatternPanel.Bind(asset);
                    cfg.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                }
            });

            chordProgressionDropdown.onValueChanged.AddListener(_ =>
            {
                SaveTrack(activeTrack);

                // Bind panel to the asset currently on the config, then put runtime back into config
                var cfg = tracks[activeTrack];
                var asset = cfg?.Parameters?.Pattern as ChordProgressionData;
                if (asset != null)
                {
                    chordProgressionPanel.Bind(asset);
                    cfg.Parameters.Pattern = chordProgressionPanel.GetRuntime();
                    Debug.Log($"[UI] Bound panel to asset '{asset.displayName}' (id {asset.GetInstanceID()}); " +
                              $"config now points to runtime clone.");
                }
            });

            melodyPatternDropdown.onValueChanged.AddListener(_ => SaveTrack(activeTrack));
        }

        /// <summary>
        /// Clears & fills a TMP_Dropdown with all names from enum T.
        /// </summary>
        private void PopulateDropdownFromEnum<T>(TMP_Dropdown dropdown) where T : Enum
        {
            dropdown.ClearOptions();
            var names = Enum.GetNames(typeof(T))
                            .Select(n => new TMP_Dropdown.OptionData(n))
                            .ToList();
            dropdown.AddOptions(names);
            dropdown.RefreshShownValue();
        }

        #region Parts

        private void SavePart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;
            Debug.Log($"<color=white>Saving part {idx}</color>");
            if (activeTrack >= 0) SaveTrack(activeTrack);
        }

        private void LoadPart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;

            Debug.Log($"<color=green> Loading part {idx}.</color>");

            ResetTracks();

            // create one tab per TrackConfig in this part (if any)
            for (int i = 0; i < tracks.Count; i++)
            {
                var tab = Instantiate(trackTabButtonPrefab, trackTabContainer);
                tab.gameObject.SetActive(true);
                tab.Initialize(i, this, $"Track {i + 1}");

                trackTabs.Add(tab);

                // Keep the “+” button at the end
                int plusIndex = newTrackButton.transform.GetSiblingIndex();
                tab.transform.SetSiblingIndex(plusIndex);
            }

            // if there was at least one track, select it
            if (tracks.Count > 0)
                SelectTrack(0);

            RebuildGridsForCurrentPart();
        }

        private void ResetTracks()
        {
            Debug.Log("<color=red>Resetting tracks.</color>");

            for (int i = trackTabs.Count - 1; i >= 0; i--)
            {
                Destroy(trackTabs[i].gameObject);
            }

            trackTabs.Clear();

            activeTrack = -1;

            newTrackButton.transform.SetAsLastSibling();
        }

        public void RemovePart(int index)
        {

        }

        
        #endregion

        #region Tracks
        private void AddNewTrack()
        {
            Debug.Log("<color=cyan>Adding new track.</color>");
            // 1) save the current UI into its TrackConfig
            if (activeTrack >= 0)
                SaveTrack(activeTrack);

            // 2) create a new blank config and tab
            var newConfig = new SongConfig.PartConfig.TrackConfig
            {
                Instrument = melodicInstruments[0],
                PercussionInstrument = percInstruments[0],
                Role = defaultTrackRole,
                Parameters = new TrackParameters()
            };

            if (part.Tracks == null) part.Tracks = new List<SongConfig.PartConfig.TrackConfig>();

            tracks.Add(newConfig);
            int newIndex = tracks.Count - 1;

            var tab = Instantiate(trackTabButtonPrefab, trackTabContainer);
            tab.gameObject.SetActive(true);
            tab.Initialize(newIndex, this, $"Track {newIndex + 1}");
            
            trackTabs.Add(tab);

            // Place this new tab just before the “+” button
            int plusIndex = newTrackButton.transform.GetSiblingIndex();
            tab.transform.SetSiblingIndex(plusIndex);
            newTrackButton.transform.SetAsLastSibling();

            // TODO: Remove track button

            SelectTrack(newIndex);
        }

        public void SelectTrack(int index)
        {
            Debug.Log($"<color=lime>Selecting track {index}</color>");
            if (index < 0 || index >= tracks.Count) return;

            // save outgoing
            if (activeTrack >= 0)
                SaveTrack(activeTrack);

            activeTrack = index;
            LoadTrack(activeTrack);

            // Highlight active tab
            for (int i = 0; i < trackTabs.Count; i++)
            {
                trackTabs[i].SetActiveVisual(i == index);
            }

            OnRoleChanged();
        }

        public void RemoveTrack(int index)
        {
            Destroy(trackTabs[index].gameObject);
            trackTabs.RemoveAt(index);
            tracks.RemoveAt(index);

            // Re-label remaining tabs
            for (int i = 0; i < trackTabs.Count; i++)
            {
                trackTabs[i].GetComponentInChildren<TMP_Text>().text = $"Track {i + 1}";
            }

            newTrackButton.transform.SetAsLastSibling();

            // Choose valid index
            if (tracks.Count > 0) SelectTrack(Mathf.Clamp(index, 0, tracks.Count - 1));
            else activeTrack = -1;
        }

        private void SaveTrack(int index)
        {
            Debug.Log($"<color=yellow>Saving track {index}</color>");

            var cfg = tracks[index];
            cfg.Role = (TrackRole)trackRoleDropdown.value;

            // 1) Pull current dropdown selections into cfg
            roleControllers[cfg.Role].SaveFromUI(cfg);

            // 2) Ensure the track keeps a RUNTIME clone, not the asset
            BindPanel(cfg);
        }

        private void BindPanel(TrackConfig cfg)
        {
            switch (cfg.Role)
            {
                case TrackRole.Backing:
                    var pat = cfg.Parameters?.Pattern as ChordProgressionData;
                    if (pat == null)
                    {
                        Debug.LogWarning("[UI->Config] Backing track has NULL ChordProgressionData.");
                    }
                    else
                    {
                        // Re-bind if needed
                        if (chordProgressionPanel.GetOriginalAsset() != pat)
                            chordProgressionPanel.Bind(pat);

                        cfg.Parameters.Pattern = chordProgressionPanel.GetRuntime();
                    }
                    break;
                case TrackRole.Rhythm:
                    var rPat = cfg.Parameters?.Pattern as DrumPatternData;
                    if (rPat == null)
                    {
                        Debug.LogWarning("[UI->Config] Rhythm track has NULL DrumPatternData.");
                    }
                    else
                    {
                        if (rhythmPatternPanel.GetOriginalAsset() != rPat)
                            rhythmPatternPanel.Bind(rPat);

                        cfg.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                    }
                    break;
                case TrackRole.Lead:
                    // TODO
                    break;
                default:
                    break;
            }
        }

        private void LoadTrack(int index)
        {
            Debug.Log($"<color=teal>Loading track {index}</color>");
            var cfg = tracks[index];
            trackRoleDropdown.SetValueWithoutNotify((int)cfg.Role);

            roleControllers[TrackRole.Rhythm].Deactivate();
            roleControllers[TrackRole.Backing].Deactivate();
            roleControllers[TrackRole.Lead].Deactivate();

            var ts = songConfig.Parts[activePart].TimeSignature;
            roleControllers[cfg.Role].RefreshPatterns(ts);

            // Push cfg → UI
            roleControllers[cfg.Role].LoadIntoUI(cfg);
            // Pull the dropdown's current choice into cfg.Parameters.Pattern
            roleControllers[cfg.Role].SaveFromUI(cfg);

            // Ensure current dropdown selection becomes the config pattern,
            // and bind the panel to it immediately on first load
            if (cfg.Role == TrackRole.Backing)
            {
                var asset = cfg.Parameters?.Pattern as ChordProgressionData;
                if (asset != null)
                {
                    chordProgressionPanel.Bind(asset);
                    cfg.Parameters.Pattern = chordProgressionPanel.GetRuntime();
                    //Debug.Log($"[UI] (Initial bind) '{asset.displayName}' → runtime clone bound to panel.");
                }
            }
            else if (cfg.Role == TrackRole.Rhythm)
            {
                var asset = cfg.Parameters?.Pattern as DrumPatternData;
                if (asset != null)
                {
                    rhythmPatternPanel.Bind(asset);
                    cfg.Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                    //Debug.Log($"[UI] (Initial bind) '{asset.displayName}' → runtime clone bound to panel.");
                }
            }

            // Show the right panel + instrument group and set piano range if melodic
            roleControllers[cfg.Role].Activate(cfg);
        }
        #endregion

        private void OnRoleChanged()
        {
            var role = (TrackRole)trackRoleDropdown.value;

            roleControllers[TrackRole.Rhythm].Deactivate();
            roleControllers[TrackRole.Backing].Deactivate();
            roleControllers[TrackRole.Lead].Deactivate();

            if (activeTrack >= 0 && activeTrack < tracks.Count)
            {
                var cfg = tracks[activeTrack];
                cfg.Role = role;
                pianoKeysPanel.gameObject.SetActive(role != TrackRole.Rhythm);

                // Show panel + correct instrument group; set piano range for melodic
                roleControllers[role].Activate(cfg);
                // Ensure UI matches cfg (esp. first-time selection)
                roleControllers[role].LoadIntoUI(cfg);
                // Persist immediately so subsequent actions operate on consistent state
                roleControllers[role].SaveFromUI(cfg);

                BindPanel(cfg);
            }
        }

        private void FilterAndRefreshPatternLists(TimeSignature ts)
        {
            foreach (var c in roleControllers.Values)
                c.RefreshPatterns(ts);
        }

        private void OnGenerateAndPlay()
        {
            SaveTrack(activeTrack);
            SavePart(activePart);
            UpdateStructureFromInput();

            var backing = songConfig.Parts.SelectMany(p => p.Tracks)
                  .FirstOrDefault(t => t.Role == TrackRole.Backing);
            var patObj = backing?.Parameters?.Pattern;
            Debug.Log($"[Generate] Backing Pattern object type={patObj?.GetType().Name} " +
                      $"name={(patObj as ChordProgressionData)?.displayName} " +
                      $"id={((patObj as ChordProgressionData)?.GetInstanceID().ToString() ?? "-")}");

            var fullSong = midiGenerator.GenerateSong(songConfig);
            lastSong = fullSong;

            var metroVol = (useMetronomeToggle != null && useMetronomeToggle.isOn)
                ? Mathf.Clamp(settings.metronomeChannelVolume, 0, 127)
                : 0;

            MidiGenerator.ApplyChannelVolume(
                fullSong, MidiGenerator.MetronomeChannel, metroVol);

            /*foreach (var chunk in fullSong.GetTrackChunks())
                Debug.Log($"Chunk has {chunk.Events.Count} events; last event at " +
                    $"{chunk.GetTimedEvents().Max(e => e.Time)} ticks");*/

            midiPlayback.Play(fullSong);
        }

        private void UpdateStructureFromInput()
        {
            if (sequenceSerializer.TryParse(sequenceInputField.text,
                                            songConfig.Parts?.Count ?? 0,
                                            out var parsed,
                                            out var warnings))
            {
                songConfig.Structure = parsed;
            }
            else
            {
                // empty or invalid → clear structure
                songConfig.Structure = new List<SongConfig.PartSequenceEntry>();
            }

            // surface warnings
            foreach (var w in warnings)
                Debug.LogWarning(w);
        }

        private void RebuildChordGridForCurrentPart()
        {
            if (chordPatternGrid == null || activePart < 0) return;
            var p = songConfig.Parts[activePart];
            int beats = GetTimeSignatureDetails(p.TimeSignature).BeatsPerMeasure;
            int measures = p.Measures;

            chordPatternGrid.Build(rows: 1, measures: measures, beatsPerMeasure: beats, subdivisions: 1, initialState: null);
            chordPatternGrid.UseAutoHeight();
            chordPatternGrid.SetFitToContent(width: true, height: true);
        }

        private void RebuildGridsForCurrentPart()
        {
            RebuildChordGridForCurrentPart();
            //RebuildRhythmGridForCurrentPart();
        }

#if UNITY_EDITOR
        private void OnSaveConfigClicked()
        {
            // Ensure Structure is in sync with the input field
            UpdateStructureFromInput();  // (uses your SequenceSerializer)

            // Delegate the asset save (dialog + AssetDatabase) to the store
            configStore.SaveNewAsset(songConfig);

            // Refresh the dropdown so the new asset appears immediately
            PopulateLoadConfigDropdown();
        }
#endif

        private void SelectChordDropdownForAsset(ChordProgressionData asset)
        {
            if (asset == null) return;

            // Try to match by displayName, then by asset.name
            var disp = string.IsNullOrEmpty(asset.displayName) ? asset.name : asset.displayName;

            int idx = -1;
            for (int i = 0; i < chordProgressionDropdown.options.Count; i++)
            {
                var txt = chordProgressionDropdown.options[i].text;
                if (txt == disp || txt == asset.name) { idx = i; break; }
            }

            if (idx >= 0)
            {
                chordProgressionDropdown.value = idx;
                chordProgressionDropdown.RefreshShownValue();

                // Ensure the Backing track uses the panel's runtime clone of this asset
                if (activeTrack >= 0 && tracks[activeTrack].Role == TrackRole.Backing)
                {
                    // Bind to the asset (creates a fresh runtime clone)
                    chordProgressionPanel.Bind(asset);
                    tracks[activeTrack].Parameters.Pattern = chordProgressionPanel.GetRuntime();
                }
                Debug.Log($"[UI] Selected chord pattern '{disp}' in dropdown.");
            }
            else
            {
                Debug.LogWarning($"[UI] Could not find '{disp}' in chord pattern dropdown after refresh.");
            }
        }

        private void SelectRhythmDropdownForAsset(DrumPatternData asset)
        {
            if (asset == null) return;

            var disp = string.IsNullOrEmpty(asset.displayName) ? asset.name : asset.displayName;

            int idx = -1;
            for (int i = 0; i < drumPatternDropdown.options.Count; i++)
            {
                var txt = drumPatternDropdown.options[i].text;
                if (txt == disp || txt == asset.name) { idx = i; break; }
            }

            if (idx >= 0)
            {
                drumPatternDropdown.value = idx;
                drumPatternDropdown.RefreshShownValue();

                // Ensure the active Rhythm track uses the panel's runtime clone
                if (activeTrack >= 0 && tracks[activeTrack].Role == TrackRole.Rhythm)
                {
                    rhythmPatternPanel.Bind(asset);
                    tracks[activeTrack].Parameters.Pattern = rhythmPatternPanel.GetRuntime();
                }

                Debug.Log($"[UI] Selected drum pattern '{disp}' in dropdown.");
            }
            else
            {
                Debug.LogWarning($"[UI] Could not find '{disp}' in drum pattern dropdown after refresh.");
            }
        }


        private void HandleSongEnded()
        {
            if (loopToggle != null && loopToggle.isOn && lastSong != null)
            {
                if (settings != null && settings.logUI)
                    Debug.Log("[Loop] Song ended → restarting from beginning.");

                midiPlayback.Play(lastSong);
            }
        }

        #region Helpers
        private void RefreshPartTabs()
        {
            int partCount = songConfig.Parts.Count;
            partListController.SetPartTabs(partCount, activePart);
        }

        private void OnAddPart()
        {
            Debug.Log("<color=orange>Adding new part.</color>");
            if (activePart >= 0) SavePart(activePart);

            var newPart = new SongConfig.PartConfig
            {
                Name = $"Part {songConfig.Parts.Count + 1}",
                Tonality = Tonality.Ionian,
                RootNote = NoteName.C,
                TempoRange = TempoRange.Fast,
                TimeSignature = TimeSignature.FourFour,
                Measures = 4
            };

            songConfig.Parts.Add(newPart);
            activePart = songConfig.Parts.Count - 1;

            partListController.SetPartTabs(songConfig.Parts.Count, activePart);
            partListController.SelectTab(activePart);

            partSettingsPanel.Bind(newPart);

            ResetTracks();
            AddNewTrack();
            FilterAndRefreshPatternLists(newPart.TimeSignature);
            RebuildGridsForCurrentPart();
        }

        private void OnPartSelected(int partIndex)
        {
            Debug.Log($"<color=green>Selecting part {partIndex}.</color>");
            if (activePart >= 0) SavePart(activePart);

            ResetTracks();

            activePart = partIndex;
            partListController.SelectTab(partIndex);

            var p = songConfig.Parts[partIndex];
            partSettingsPanel.Bind(p);
            FilterAndRefreshPatternLists(p.TimeSignature);

            LoadPart(activePart);
            RebuildGridsForCurrentPart();
        }

        private void OnRemovePartClicked(int index)
        {
            if (songConfig.Parts.Count <= 1)
            {
                Debug.LogWarning("Cannot remove the last remaining part.");
                return;
            }

            Debug.Log($"<color=red>Removing part {index}.</color>");
            songConfig.Parts.RemoveAt(index);

            // Re-select a safe index
            activePart = Mathf.Clamp(activePart, 0, songConfig.Parts.Count - 1);
            partListController.SetPartTabs(songConfig.Parts.Count, activePart);

            // keep settings UI in sync with new selection
            var p = songConfig.Parts[activePart];
            partSettingsPanel.Bind(p);
            FilterAndRefreshPatternLists(p.TimeSignature);

            // Rebuild UI and track state
            ResetTracks();
            LoadPart(activePart);
            RebuildGridsForCurrentPart();
        }

        private void HandleTimeSignatureChanged(TimeSignature ts)
        {
            var p = songConfig.Parts[activePart];
            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            rhythmPatternPanel?.SetSignature(beats, p.Measures, 1);
            FilterAndRefreshPatternLists(ts);
            RebuildGridsForCurrentPart();
        }

        private void HandleMeasuresChanged(int measures)
        {
            var ts = songConfig.Parts[activePart].TimeSignature;
            var beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;
            rhythmPatternPanel?.SetSignature(beats, measures, 1);
            RebuildGridsForCurrentPart();
        }
        #endregion
    }
}
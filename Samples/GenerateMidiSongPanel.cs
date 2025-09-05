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
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay
{
    public class GenerateMidiSongPanel : MonoBehaviour
    {
        [Header("Midi Player Reference")]
        [Tooltip("Any component on this GameObject that implements IPlayMidi")]
        [SerializeField] private MonoBehaviour midiPlayerAdapter = null;

        [Header("Part Tabs")]
        [SerializeField] private Transform partTabContainer;
        [SerializeField] private PartTabButton partTabButtonPrefab;

        [Header("Song Parts")]
        [SerializeField] private TMP_Dropdown tonalityDropdown;
        [SerializeField] private TMP_Dropdown rootNoteDropdown;
        [SerializeField] private TMP_Dropdown tempoRangeDropdown;
        [SerializeField] private TMP_Dropdown timeSignatureDropdown;
        [SerializeField] private TMP_Dropdown measuresDropdown;

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
        [SerializeField] private Button newPartButton;
        [SerializeField] private Button newTrackButton;
        [SerializeField] private Button generateButton;

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
            else
            {
                midiPlayback = new MidiPlayback(midiPlayer);
            }

            songConfig = new SongConfig();
            songConfig.Parts = new List<SongConfig.PartConfig>();
            configStore = new SongConfigStoreResources();

            instrumentRepo = new InstrumentRepositoryResources();
            patternRepo = new PatternRepositoryResources();
            sequenceSerializer = new SequenceSerializer();

            PopulateAllDropdowns();
            BuildUIControllers();
            SubscribeUIChanges();

            newPartButton.onClick.AddListener(AddNewPart);
            newTrackButton.onClick.AddListener(AddNewTrack);

            midiGenerator = new MidiGenerator();

            generateButton.onClick.AddListener(OnGenerateAndPlay);
#if UNITY_EDITOR
            saveConfigButton.onClick.AddListener(OnSaveConfigClicked);
#endif
            AddNewPart();
        }

        private void PopulateAllDropdowns()
        {
            PopulateLoadConfigDropdown();

            PopulateDropdownFromEnum<Tonality>(tonalityDropdown);
            tonalityDropdown.onValueChanged.AddListener(v =>
            {
                chordProgressionPanel.SetTonality((Tonality)v);
            });
            tonalityDropdown.value = (int)Tonality.Ionian;

            PopulateDropdownFromEnum<NoteName>(rootNoteDropdown);
            PopulateDropdownFromEnum<TempoRange>(tempoRangeDropdown);
            tempoRangeDropdown.value = (int)TempoRange.Fast;
            tempoRangeDropdown.RefreshShownValue();
            PopulateDropdownFromEnum<TimeSignature>(timeSignatureDropdown);

            var measuresOptions = new List<string> { "1", "2", "4", "8" };
            measuresDropdown.ClearOptions();
            measuresDropdown.AddOptions(measuresOptions);
            measuresDropdown.value = measuresOptions.IndexOf("4");
            measuresDropdown.RefreshShownValue();

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

            // Initial pattern lists per role based on current TS
            var ts = (TimeSignature)timeSignatureDropdown.value;
            foreach (var c in roleControllers.Values) c.RefreshPatterns(ts);

            FilterAndRefreshPatternLists((TimeSignature)timeSignatureDropdown.value);
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
                // clear UI
                ClearAllPartTabs();
                sequenceInputField.SetTextWithoutNotify(string.Empty);

                // start fresh
                AddNewPart();
                return;
            }

            // otherwise load the chosen SO
            var so = availableConfigs[selectedIndex - 1];
            LoadSongConfigSO(so);
        }


        private void ClearAllPartTabs()
        {
            // Leave the “+” button; destroy everything else under partTabContainer
            for (int i = partTabContainer.childCount - 2; i >= 1; i--)
            {
                Destroy(partTabContainer.GetChild(i).gameObject);
            }

            // Also clear tracks UI
            ResetTracks();
        }

        private void LoadSongConfigSO(SongConfigSO so)
        {
            // Use the store to produce a deep runtime clone from the asset
            songConfig = configStore.CloneFromAsset(so);

            // 2) repopulate the sequence input
            sequenceInputField.SetTextWithoutNotify(
                sequenceSerializer.Serialize(songConfig.Structure)
            );

            // 3) rebuild the Part tabs
            ClearAllPartTabs();

            for (int i = 0; i < songConfig.Parts.Count; i++)
            {
                var pd = songConfig.Parts[i];
                var tab = Instantiate(partTabButtonPrefab, partTabContainer);
                tab.gameObject.SetActive(true);
                tab.Initialize(i, this, pd.Name);

                if (i == 0) tab.SetActiveVisual(true);
            }

            newPartButton.transform.SetAsLastSibling();

            // 4) finally load the very first part into the UI
            SelectPart(0);
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

            timeSignatureDropdown.onValueChanged.AddListener(idx =>
            {
                var newTs = (TimeSignature)idx;
                FilterAndRefreshPatternLists(newTs);
                RebuildChordGridForCurrentPart();
            });

            // TODO: All grids
            measuresDropdown.onValueChanged.AddListener(_ => RebuildChordGridForCurrentPart());

            // Patterns
            drumPatternDropdown.onValueChanged.AddListener(_ => SaveTrack(activeTrack));

            chordProgressionDropdown.onValueChanged.AddListener(_ =>
            {
                SaveTrack(activeTrack);
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
        private void AddNewPart()
        {
            Debug.Log("<color=orange>Adding new part.</color>");

            // save outgoing part
            if (activePart >= 0) SavePart(activePart);

            // create & name
            var newPart = new SongConfig.PartConfig
            {
                Name = $"Part {songConfig.Parts.Count + 1}"
            };
            songConfig.Parts.Add(newPart);

            // instantiate its tab
            var tab = Instantiate(partTabButtonPrefab, partTabContainer);
            tab.gameObject.SetActive(true);
            int newIdx = songConfig.Parts.Count - 1;
            tab.Initialize(newIdx, this, newPart.Name);

            // Place this new tab just before the “+” button
            int plusIndex = newPartButton.transform.GetSiblingIndex();
            tab.transform.SetSiblingIndex(plusIndex);
            newPartButton.transform.SetAsLastSibling();

            // immediately select it
            SelectPart(newIdx);

            // Start with a single track
            // TODO: Clone the same instruments as previous part
            if (activeTrack >= 0)
            {
                ResetTracks();
            }
            //activeTrack = -1;
            AddNewTrack();

            RebuildChordGridForCurrentPart();
        }

        private void SavePart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;

            Debug.Log($"<color=white>Saving part {idx}</color>");

            part.Tonality = (Tonality)tonalityDropdown.value;
            part.RootNote = (NoteName)rootNoteDropdown.value;
            part.TempoRange = (TempoRange)tempoRangeDropdown.value;
            part.TimeSignature = (TimeSignature)timeSignatureDropdown.value;

            if (int.TryParse(measuresDropdown.options[measuresDropdown.value].text, out int m))
                part.Measures = m;

            // snapshot currently-selected track before we blow away UI
            if (activeTrack >= 0) SaveTrack(activeTrack);
        }

        private void LoadPart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;

            Debug.Log($"<color=green> Loading part {idx}.</color>");

            // Set each dropdown to the enum’s underlying int, then refresh
            tonalityDropdown.value = (int)part.Tonality;
            tonalityDropdown.RefreshShownValue();

            rootNoteDropdown.value = (int)part.RootNote;
            rootNoteDropdown.RefreshShownValue();

            tempoRangeDropdown.value = (int)part.TempoRange;
            tempoRangeDropdown.RefreshShownValue();

            timeSignatureDropdown.value = (int)part.TimeSignature;
            timeSignatureDropdown.RefreshShownValue();

            // Find the measures option whose text matches part.Measures
            string target = part.Measures.ToString();
            for (int i = 0; i < measuresDropdown.options.Count; i++)
            {
                if (measuresDropdown.options[i].text == target)
                {
                    measuresDropdown.value = i;
                    break;
                }
            }
            measuresDropdown.RefreshShownValue();

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

            RebuildChordGridForCurrentPart();
        }

        public void SelectPart(int index)
        {
            Debug.Log($"<color=green>Selecting part {index}.</color>");

            if (activePart >= 0) SavePart(activePart);

            activePart = index;
            if (part != null && part.Tracks != null)
                LoadPart(activePart);

            // highlight tab visuals…
            for (int i = 1; i < partTabContainer.childCount - 1; i++)
            {
                partTabContainer.GetChild(i)
                    .GetComponent<PartTabButton>()
                    .SetActiveVisual((i - 1) == index);
            }

            RebuildChordGridForCurrentPart();
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
                Role = TrackRole.Rhythm,
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
            Debug.Log(part);
            Debug.Log(tracks);

            var cfg = tracks[index];
            cfg.Role = (TrackRole)trackRoleDropdown.value;

            roleControllers[cfg.Role].SaveFromUI(cfg);
        }

        private void LoadTrack(int index)
        {
            Debug.Log($"<color=teal>Loading track {index}</color>");
            var cfg = tracks[index];
            trackRoleDropdown.SetValueWithoutNotify((int)cfg.Role);

            roleControllers[TrackRole.Rhythm].Deactivate();
            roleControllers[TrackRole.Backing].Deactivate();
            roleControllers[TrackRole.Lead].Deactivate();

            // Ensure pattern dropdowns reflect current TS (if user changed TS before selecting track)
            var ts = (TimeSignature)timeSignatureDropdown.value;
            roleControllers[cfg.Role].RefreshPatterns(ts);

            // Push cfg → UI
            roleControllers[cfg.Role].LoadIntoUI(cfg);

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

                // Optional: ensure UI matches cfg (esp. first-time selection)
                roleControllers[role].LoadIntoUI(cfg);

                // Persist immediately so subsequent actions operate on consistent state
                roleControllers[role].SaveFromUI(cfg);
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

            var fullSong = new MidiFile();

            UpdateStructureFromInput();

            fullSong = midiGenerator.GenerateSong(songConfig);
            foreach (var chunk in fullSong.GetTrackChunks())
                Debug.Log($"Chunk has {chunk.Events.Count} events; last event at " +
                    $"{chunk.GetTimedEvents().Max(e => e.Time)} ticks");

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
            if (chordPatternGrid == null) return;

            var ts = (TimeSignature)timeSignatureDropdown.value;
            int beats = GetTimeSignatureDetails(ts).BeatsPerMeasure;

            // Measures from dropdown
            int measures = int.Parse(measuresDropdown.options[measuresDropdown.value].text);

            Debug.Log($"<color=white>Rebuilding PatternGrid for {1} rows, {measures} measures, {beats} beats per measure");

            // For now, subdivisions=1, rows=1 (chords)
            chordPatternGrid.Build(
                rows: 1,
                measures: measures,
                beatsPerMeasure: beats,
                subdivisions: 1,
                initialState: null // all off
            );
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
    }
}
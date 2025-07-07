using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory;

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
        [SerializeField] private GameObject drumSettingsPanel;
        [SerializeField] private TMP_Dropdown drumPatternDropdown;
        [SerializeField] private PianoKeysPanel pianoKeysPanel;
        [SerializeField] private GameObject chordSettingsPanel;
        [SerializeField] private TMP_Dropdown chordProgressionDropdown;
        [SerializeField] private GameObject melodySettingsPanel;
        [SerializeField] private TMP_Dropdown melodyPatternDropdown;

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

        private List<DrumPatternData> allDrumPatterns;
        private List<ChordProgressionData> allChordProgressions;
        private List<MelodyPatternData> allMelodyPatterns;
        private List<DrumPatternData> drumPatterns;
        private List<ChordProgressionData> chordProgressions;
        private List<MelodyPatternData> melodyPatterns;

        private IPlayMidi midiPlayer => midiPlayerAdapter as IPlayMidi;
        private MidiGenerator midiGenerator;

        private void Awake()
        {
            if (midiPlayer == null)
                Debug.LogError($"{nameof(midiPlayerAdapter)} must implement IPlayMidi");

            songConfig = new SongConfig();
            songConfig.Parts = new List<SongConfig.PartConfig>();

            PopulateAllDropdowns();
            SubscribeUIChanges();

            newPartButton.onClick.AddListener(AddNewPart);
            newTrackButton.onClick.AddListener(AddNewTrack);

            midiGenerator = new MidiGenerator();

            generateButton.onClick.AddListener(OnGenerateAndPlay);

            saveConfigButton.onClick.AddListener(OnSaveConfigClicked);

            AddNewPart();
        }

        private void PopulateAllDropdowns()
        {
            PopulateLoadConfigDropdown();

            PopulateDropdownFromEnum<Tonality>(tonalityDropdown);
            PopulateDropdownFromEnum<NoteName>(rootNoteDropdown);
            PopulateDropdownFromEnum<TempoRange>(tempoRangeDropdown);
            tempoRangeDropdown.value = (int)TempoRange.Fast;
            tempoRangeDropdown.RefreshShownValue();
            PopulateDropdownFromEnum<MusicTheory.TimeSignature>(timeSignatureDropdown);

            var measuresOptions = new List<string> { "1", "2", "4", "8" };
            measuresDropdown.ClearOptions();
            measuresDropdown.AddOptions(measuresOptions);
            measuresDropdown.value = measuresOptions.IndexOf("4");
            measuresDropdown.RefreshShownValue();

            PopulateInstruments();

            // Track Roles
            PopulateDropdownFromEnum<TrackRole>(trackRoleDropdown);

            // Patterns
            allDrumPatterns = Resources
                .LoadAll<DrumPatternData>("ScriptableObjects/Patterns/Drums")
                .ToList();
            allChordProgressions = Resources
                .LoadAll<ChordProgressionData>("ScriptableObjects/Patterns/Chords")
                .ToList();
            allMelodyPatterns = Resources
                .LoadAll<MelodyPatternData>("ScriptableObjects/Patterns/Melodies")
                .ToList();

            FilterAndRefreshPatternLists((MusicTheory.TimeSignature)timeSignatureDropdown.value);
        }

        private void PopulateInstruments()
        {
            // load everything under your “MIDI Instruments” Resources folder
            var all = Resources
                .LoadAll<MIDIInstrumentSO>("ScriptableObjects/MIDI Instruments")
                .ToList();

            // pick out the percussion subclass…
            percInstruments = all
                .OfType<MIDIPercussionInstrumentSO>()
                .ToList();

            // …and everything else is “melodic”
            melodicInstruments = all
                .Where(i => !(i is MIDIPercussionInstrumentSO))
                .ToList();

            melodicInstrumentDropdown.ClearOptions();
            melodicInstrumentDropdown.AddOptions(
                melodicInstruments.Select(i => i.InstrumentName).ToList()
            );

            percInstrumentDropdown.ClearOptions();
            percInstrumentDropdown.AddOptions(
                percInstruments.Select(i => i.InstrumentName).ToList()
            );
        }

        private void PopulateLoadConfigDropdown()
        {
            // 1) clear old options
            loadConfigDropdown.ClearOptions();

            // 2) load all SongConfigSO assets in Resources/ScriptableObjects/Song Configs
            availableConfigs = Resources
                .LoadAll<SongConfigSO>("ScriptableObjects/Song Configs")
                .ToList();

            // 3) build a list of display names, starting with "-"    
            var optionNames = new List<string> { "-" };
            optionNames.AddRange(availableConfigs.Select(so => so.name));

            // 4) add them to the dropdown
            loadConfigDropdown.AddOptions(optionNames
                .Select(n => new TMP_Dropdown.OptionData(n))
                .ToList());
            loadConfigDropdown.RefreshShownValue();

            // 5) hook the change callback
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
            // 1) copy the SO’s data into our runtime config
            songConfig.Parts = so.Config.Parts
                .Select(p => new SongConfig.PartConfig
                {
                    Name = p.Name,
                    Tonality = p.Tonality,
                    RootNote = p.RootNote,
                    TempoRange = p.TempoRange,
                    TimeSignature = p.TimeSignature,
                    Measures = p.Measures,
                    Tracks = p.Tracks
                        .Select(t => new SongConfig.PartConfig.TrackConfig
                        {
                            Instrument = t.Instrument,
                            PercussionInstrument = t.PercussionInstrument,
                            Role = t.Role,
                            Parameters = new TrackParameters
                            {
                                Pattern = t.Parameters.Pattern
                            }
                        })
                        .ToList()
                })
                .ToList();

            songConfig.Structure = so.Config.Structure
                .Select(e => new SongConfig.PartSequenceEntry
                {
                    PartIndex = e.PartIndex,
                    RepeatCount = e.RepeatCount
                })
                .ToList();

            // 2) repopulate the sequence input
            sequenceInputField.SetTextWithoutNotify(SerializeStructure());

            // 3) rebuild the Part tabs
            ClearAllPartTabs();

            for (int i = 0; i < songConfig.Parts.Count; i++)
            {
                var pd = songConfig.Parts[i];
                var tab = Instantiate(partTabButtonPrefab, partTabContainer);
                tab.gameObject.SetActive(true);
                tab.Initialize(i, this, pd.Name);

                // Set first as active
                if (i == 0) tab.SetActiveVisual(true);
            }

            // make sure your “+” button is still last
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
                var newTs = (MusicTheory.TimeSignature)idx;
                FilterAndRefreshPatternLists(newTs);
            });

            // Patterns
            drumPatternDropdown.onValueChanged.AddListener(_ => SaveTrack(activeTrack));
            chordProgressionDropdown.onValueChanged.AddListener(_ => SaveTrack(activeTrack));
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
        }

        private void SavePart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;

            Debug.Log($"<color=white>Saving part {idx}</color>");

            part.Tonality = (Tonality)tonalityDropdown.value;
            part.RootNote = (NoteName)rootNoteDropdown.value;
            part.TempoRange = (TempoRange)tempoRangeDropdown.value;
            part.TimeSignature = (MusicTheory.TimeSignature)timeSignatureDropdown.value;

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

            switch (cfg.Role)
            {
                case TrackRole.Rhythm:
                    cfg.Parameters.Pattern = drumPatterns[drumPatternDropdown.value];
                    cfg.PercussionInstrument = percInstruments[percInstrumentDropdown.value];
                    break;
                case TrackRole.Backing:
                    cfg.Parameters.Pattern =
                        chordProgressions[chordProgressionDropdown.value];
                    cfg.Instrument = melodicInstruments[melodicInstrumentDropdown.value];
                    break;
                case TrackRole.Lead:
                    cfg.Parameters.Pattern =
                        melodyPatterns[melodyPatternDropdown.value];
                    cfg.Instrument = melodicInstruments[melodicInstrumentDropdown.value];
                    break;
            }
        }

        private void LoadTrack(int index)
        {
            Debug.Log($"<color=teal>Loading track {index}</color>");
            var cfg = tracks[index];
            trackRoleDropdown.SetValueWithoutNotify((int)cfg.Role);

            switch (cfg.Role)
            {
                case TrackRole.Rhythm:
                    percInstrumentDropdown.SetValueWithoutNotify(
                        percInstruments.IndexOf(cfg.PercussionInstrument)
                    );
                    percInstrumentDropdown.RefreshShownValue();

                    //Debug.Log(((DrumTrackParameters)cfg.Parameters).SelectedPattern.patternName);
                    drumPatternDropdown.SetValueWithoutNotify(
                        drumPatterns.IndexOf((DrumPatternData)cfg.Parameters.Pattern)
                    );
                    drumPatternDropdown.RefreshShownValue();

                    percInstrumentDropdown.transform.parent.gameObject.SetActive(true);
                    melodicInstrumentDropdown.transform.parent.gameObject.SetActive(false);
                    break;
                case TrackRole.Backing:
                    melodicInstrumentDropdown.SetValueWithoutNotify(
                        melodicInstruments.IndexOf(cfg.Instrument)
                    );
                    chordProgressionDropdown.SetValueWithoutNotify(
                        chordProgressions.IndexOf(
                            (ChordProgressionData)cfg.Parameters.Pattern
                    ));

                    percInstrumentDropdown.transform.parent.gameObject.SetActive(false);
                    melodicInstrumentDropdown.transform.parent.gameObject.SetActive(true);
                    break;
                case TrackRole.Lead:
                    melodicInstrumentDropdown.SetValueWithoutNotify(
                        melodicInstruments.IndexOf(cfg.Instrument)
                    );
                    melodyPatternDropdown.SetValueWithoutNotify(
                        melodyPatterns.IndexOf(
                            (MelodyPatternData)cfg.Parameters.Pattern
                    ));

                    percInstrumentDropdown.transform.parent.gameObject.SetActive(false);
                    melodicInstrumentDropdown.transform.parent.gameObject.SetActive(true);
                    break;
            }
        }
        #endregion

        private void OnRoleChanged()
        {
            // persist current UI
            //SaveTrack(activeTrack);

            var role = (TrackRole)trackRoleDropdown.value;
            // show only the matching panel
            drumSettingsPanel.SetActive(role == TrackRole.Rhythm);
            chordSettingsPanel.SetActive(role == TrackRole.Backing);
            melodySettingsPanel.SetActive(role == TrackRole.Lead);
            pianoKeysPanel.gameObject.SetActive(role != TrackRole.Rhythm);

            // activate the corresponding dropdown
            percInstrumentDropdown.transform.parent.gameObject.SetActive(role == TrackRole.Rhythm);
            melodicInstrumentDropdown.transform.parent.gameObject.SetActive(role != TrackRole.Rhythm);

            // load the correct instrument for this track
            var cfg = tracks[activeTrack];
            if (role == TrackRole.Rhythm)
            {
                // find existing percussion kit index, default to 0
                int idx = percInstruments.IndexOf(cfg.PercussionInstrument);
                idx = Mathf.Clamp(idx, 0, percInstruments.Count - 1);
                percInstrumentDropdown.SetValueWithoutNotify(idx);
                percInstrumentDropdown.RefreshShownValue();
            }
            else
            {
                int idx = melodicInstruments.IndexOf(cfg.Instrument);
                idx = Mathf.Clamp(idx, 0, melodicInstruments.Count - 1);
                melodicInstrumentDropdown.SetValueWithoutNotify(idx);
                melodicInstrumentDropdown.RefreshShownValue();

                var instrument = melodicInstruments[melodicInstrumentDropdown.value];
                pianoKeysPanel.SetInteractableRange(instrument.octaveMin, instrument.octaveMax);
            }

            // save the newly‐selected (first) instrument
            SaveTrack(activeTrack);
        }

        private void FilterAndRefreshPatternLists(MusicTheory.TimeSignature ts)
        {
            // 1) pick only the patterns that match this TS…
            drumPatterns = allDrumPatterns
                .Where(p => p.timeSignature == ts)
                .ToList();
            chordProgressions = allChordProgressions
                .Where(p => p.timeSignature == ts)
                .ToList();
            melodyPatterns = allMelodyPatterns
                .Where(p => p.timeSignature == ts)
                .ToList();

            // 2) re-populate each dropdown
            drumPatternDropdown.ClearOptions();
            drumPatternDropdown.AddOptions(drumPatterns
                .Select(p => p.displayName)
                .ToList());
            chordProgressionDropdown.ClearOptions();
            chordProgressionDropdown.AddOptions(chordProgressions
                .Select(p => p.displayName)
                .ToList());
            melodyPatternDropdown.ClearOptions();
            melodyPatternDropdown.AddOptions(melodyPatterns
                .Select(p => p.displayName)
                .ToList());

            // 3) reset to the first element if there is one
            if (drumPatterns.Count > 0) drumPatternDropdown.value = 0;
            if (chordProgressions.Count > 0) chordProgressionDropdown.value = 0;
            if (melodyPatterns.Count > 0) melodyPatternDropdown.value = 0;
        }

        private void OnGenerateAndPlay()
        {
            SaveTrack(activeTrack);
            SavePart(activePart);

            var fullSong = new MidiFile();

            ParseSequence();

            fullSong = midiGenerator.GenerateSong(songConfig);
            foreach (var chunk in fullSong.GetTrackChunks())
                Debug.Log($"Chunk has {chunk.Events.Count} events; last event at " +
                    $"{chunk.GetTimedEvents().Max(e => e.Time)} ticks");

            Play(fullSong);
        }

        private void ParseSequence()
        {
            // start fresh
            songConfig.Structure = new List<SongConfig.PartSequenceEntry>();

            string raw = sequenceInputField.text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning("Sequence input is empty. Nothing to parse.");
                return;
            }

            // split on commas
            var tokens = raw.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (!int.TryParse(t, out int partNumber))
                {
                    Debug.LogWarning($"Invalid sequence entry '{t}' at position {i}. Skipping.");
                    continue;
                }

                // convert 1-based to 0-based index
                int idx = partNumber - 1;

                // clamp if out of range
                if (idx < 0 || idx >= songConfig.Parts.Count)
                {
                    int clampedNumber = Mathf.Clamp(partNumber, 1, songConfig.Parts.Count);
                    Debug.LogWarning(
                        $"Part number {partNumber} is out of range. " +
                        $"Using part {clampedNumber} instead."
                    );
                    idx = clampedNumber - 1;
                }

                // add to structure (always 1 repeat)
                songConfig.Structure.Add(new SongConfig.PartSequenceEntry
                {
                    PartIndex = idx,
                    RepeatCount = 1
                });
            }
        }

        /// <summary>
        /// e.g. if Structure = [ {PartIndex=0}, {PartIndex=2}, {PartIndex=0} ],
        /// returns "1,3,1"
        /// </summary>
        private string SerializeStructure()
        {
            return string.Join(",",
                songConfig.Structure
                          .Select(e => (e.PartIndex + 1).ToString()));
        }

        private void Play(MidiFile midi)
        {
            // Convert MidiFile → byte[] 
            byte[] data;
            using (var ms = new System.IO.MemoryStream())
            {
                midi.Write(ms);
                data = ms.ToArray();
            }

            midiPlayer.Stop();      // stop any existing playback
            midiPlayer.Play(data);  // start the new song
        }

#if UNITY_EDITOR
        private void OnSaveConfigClicked()
        {
            // Prompt for asset path & name
            string path = UnityEditor.EditorUtility
                .SaveFilePanelInProject(
                    "Save SongConfig",
                    "NewSongConfig",
                    "asset",
                    "Choose a location in your project"
                );
            if (string.IsNullOrEmpty(path)) return;

            // Create and populate a new SO
            var so = ScriptableObject.CreateInstance<SongConfigSO>();
            so.Config = new SongConfig();
            so.Config.Parts = new List<SongConfig.PartConfig>();

            so.Config.Parts = songConfig.Parts
                .Select(p => new SongConfig.PartConfig
                {
                    Name = p.Name,
                    Tonality = p.Tonality,
                    RootNote = p.RootNote,
                    TempoRange = p.TempoRange,
                    TimeSignature = p.TimeSignature,
                    Measures = p.Measures,
                    Tracks = p.Tracks.Select(t => t).ToList()
                })
                .ToList();

            so.Config.Structure = new List<SongConfig.PartSequenceEntry>();

            ParseSequence();

            so.Config.Structure = songConfig.Structure
                .Select(e => new SongConfig.PartSequenceEntry
                {
                    PartIndex = e.PartIndex,
                    RepeatCount = e.RepeatCount
                })
                .ToList();

            // Save it into your project
            UnityEditor.AssetDatabase.CreateAsset(so, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
        }
#endif
    }
}
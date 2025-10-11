using Melanchall.DryWetMidi.Core;
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
        [SerializeField] private TrackListController trackListController;
        [SerializeField] private TrackDetailsPanel trackDetailsPanel;

        [Header("Controls")]
        [SerializeField] private Button generateButton;
        [SerializeField] private Toggle useMetronomeToggle;
        [SerializeField] private Toggle loopToggle;

        [Header("Defaults")]
        [SerializeField] private TrackRole defaultTrackRole = TrackRole.Backing;

        [Header("Config I/O")]
        [SerializeField] private Button saveConfigButton;
        [SerializeField] private TMP_Dropdown loadConfigDropdown;

        [Header("Input Text")]
        [SerializeField] private TMP_InputField sequenceInputField;
        
        private SongConfig songConfig = new SongConfig();
        private SongConfig.PartConfig part => songConfig.Parts[activePart];
        private List<TrackConfig> tracks => part.Tracks;
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

        private Action<Tonality> _tonalityHandler;

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

            instrumentRepo.Refresh();
            melodicInstruments = instrumentRepo.GetMelodicInstruments().ToList();
            percInstruments = instrumentRepo.GetPercussionInstruments().ToList();
            patternRepo.Refresh();

            trackDetailsPanel.Initialize(
                settings, melodicInstruments, percInstruments, patternRepo);

            PopulateLoadConfigDropdown();
            SubscribeControllers();
            RefreshPartTabs();

            midiGenerator = new MidiGenerator(settings);
            generateButton.onClick.AddListener(OnGenerateAndPlay);

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

            UnsubscribeControllers();
        }

        private void SubscribeControllers()
        {
            // Parts
            partListController.OnPartSelected += OnPartSelected;
            partListController.OnPartAddClicked += OnAddPart;
            partListController.OnPartRemoveClicked += OnRemovePartClicked;

            partSettingsPanel.OnTimeSignatureChanged += HandleTimeSignatureChanged;
            partSettingsPanel.OnMeasuresChanged += HandleMeasuresChanged;
            _tonalityHandler = t => trackDetailsPanel.SetTonality(t);
            partSettingsPanel.OnTonalityChanged += _tonalityHandler;

            // Tracks
            trackListController.OnAddTrackClicked += AddNewTrack;
            trackListController.OnTrackSelected += SelectTrack;
            trackListController.OnRemoveTrackClicked += RemoveTrack;
        }

        private void UnsubscribeControllers()
        {
            // Parts
            partListController.OnPartSelected -= OnPartSelected;
            partListController.OnPartAddClicked -= OnAddPart;
            partListController.OnPartRemoveClicked -= OnRemovePartClicked;

            partSettingsPanel.OnTimeSignatureChanged -= HandleTimeSignatureChanged;
            partSettingsPanel.OnMeasuresChanged -= HandleMeasuresChanged;
            partSettingsPanel.OnTonalityChanged -= _tonalityHandler;

            // Tracks
            trackListController.OnAddTrackClicked -= AddNewTrack;
            trackListController.OnTrackSelected -= SelectTrack;
            trackListController.OnRemoveTrackClicked -= RemoveTrack;
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

            // build track tabs for this part
            trackListController.SetTrackTabs(tracks.Count, tracks.Count > 0 ? 0 : -1);

            // if there was at least one track, select it
            if (tracks.Count > 0)
                SelectTrack(0);

            var p = songConfig.Parts[activePart];
            trackDetailsPanel.OnPartSignatureChanged(p.TimeSignature, p.Measures);
        }
        #endregion

        #region Tracks
        private void AddNewTrack()
        {
            Debug.Log("<color=cyan>Adding new track.</color>");
            // 1) save the current UI into its TrackConfig
            if (activeTrack >= 0)
                SaveTrack(activeTrack);

            var defaultMelodic = (melodicInstruments != null && melodicInstruments.Count > 0)
                ? melodicInstruments[0] : null;
            var defaultPerc = (percInstruments != null && percInstruments.Count > 0)
                ? percInstruments[0] : null;

            var newConfig = new SongConfig.PartConfig.TrackConfig
            {
                Instrument = defaultMelodic,
                PercussionInstrument = defaultPerc,
                Role = defaultTrackRole,
                Parameters = new TrackParameters()
            };

            if (part.Tracks == null) part.Tracks = new List<SongConfig.PartConfig.TrackConfig>();

            tracks.Add(newConfig);
            int newIndex = tracks.Count - 1;

            trackListController.SetTrackTabs(tracks.Count, newIndex);

            SelectTrack(newIndex);
        }

        public void SelectTrack(int index)
        {
            Debug.Log($"<color=lime>Selecting track {index}</color>");
            if (index < 0 || index >= tracks.Count) return;

            // save outgoing only if it still exists
            if (activeTrack >= 0 && activeTrack < tracks.Count)
                SaveTrack(activeTrack);

            activeTrack = index;
            LoadTrack(activeTrack);
            trackListController.SelectTab(index);
        }

        public void RemoveTrack(int index)
        {
            var wasActive = (index == activeTrack);
            tracks.RemoveAt(index);

            if (wasActive || activeTrack >= tracks.Count)
                activeTrack = -1;

            var next = (tracks.Count == 0) ? -1 : Mathf.Clamp(index, 0, tracks.Count - 1);
            trackListController.SetTrackTabs(tracks.Count, next);

            if (next >= 0) SelectTrack(next);
            else activeTrack = -1;
        }

        private void SaveTrack(int index)
        {
            if (index < 0 || index >= tracks.Count) return;
            trackDetailsPanel.SaveInto(tracks[index]);
        }

        private void ResetTracks()
        {
            Debug.Log("<color=red>Resetting tracks.</color>");

            trackListController.SetTrackTabs(0, -1);
            activeTrack = -1;
        }

        private void LoadTrack(int index)
        {
            var cfg = tracks[index];
            var ts = songConfig.Parts[activePart].TimeSignature;
            trackDetailsPanel.BindTrack(part, cfg, ts);
        }
        #endregion

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
            trackDetailsPanel.OnPartSignatureChanged(newPart.TimeSignature, newPart.Measures);
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

            LoadPart(activePart);
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

            // Rebuild UI and track state
            ResetTracks();
            LoadPart(activePart);
        }

        private void HandleTimeSignatureChanged(TimeSignature ts)
        {
            var p = songConfig.Parts[activePart];
            trackDetailsPanel.OnPartSignatureChanged(ts, p.Measures);
        }

        private void HandleMeasuresChanged(int measures)
        {
            var ts = songConfig.Parts[activePart].TimeSignature;
            trackDetailsPanel.OnPartSignatureChanged(ts, measures);
        }
        #endregion
    }
}
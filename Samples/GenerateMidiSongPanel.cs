using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
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

        // Domain manager (single source of truth)
        private ISongConfigManager manager;
        private SongConfig songConfig => manager?.Song;
        private int activePart => manager?.ActivePart.Value ?? -1;
        private int activeTrack => manager?.ActiveTrack.Value ?? -1;
        private SongConfig.PartConfig part => 
            (activePart >= 0 && activePart < (songConfig?.Parts?.Count ?? 0)) ? 
                songConfig.Parts[activePart] : null;

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

            // Create manager and subscribe to its events (manager -> UI)
            manager = new SongConfigManager(
                settings, instrumentRepo, patternRepo, sequenceSerializer, configStore);

            trackDetailsPanel.SetManager(manager);

            SubscribeManagerEvents();

            midiGenerator = new MidiGenerator(settings, new BasicVoiceLeadingVoicer());
            generateButton.onClick.AddListener(OnGenerateAndPlay);

#if UNITY_EDITOR
            saveConfigButton.onClick.AddListener(OnSaveConfigClicked);
#endif
            // Start with a first part & a first track via manager
            manager.AddPart();
            manager.AddTrack(defaultTrackRole);

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
            partListController.OnPartSelected += i => manager.SelectPart(new PartIdx(i));
            partListController.OnPartAddClicked += () => manager.AddPart();
            partListController.OnPartRemoveClicked += i => manager.RemovePart(new PartIdx(i));

            partSettingsPanel.OnTimeSignatureChanged += HandleTimeSignatureChanged;
            partSettingsPanel.OnMeasuresChanged += HandleMeasuresChanged;
            _tonalityHandler = t => trackDetailsPanel.SetTonality(t);
            partSettingsPanel.OnTonalityChanged += _tonalityHandler;

            // Tracks
            trackListController.OnAddTrackClicked += () => manager.AddTrack(defaultTrackRole);
            trackListController.OnTrackSelected += i => manager.SelectTrack(new TrackIdx(i));
            trackListController.OnRemoveTrackClicked += 
                i => manager.RemoveTrack(new TrackIdx(i));
        }

        private void UnsubscribeControllers()
        {
            if (partSettingsPanel != null)
            {
                partSettingsPanel.OnTimeSignatureChanged -= HandleTimeSignatureChanged;
                partSettingsPanel.OnMeasuresChanged -= HandleMeasuresChanged;
                if (_tonalityHandler != null)
                    partSettingsPanel.OnTonalityChanged -= _tonalityHandler;
            }
        }

        private void SubscribeManagerEvents()
        {
            // Part lifecycle
            manager.PartAdded += (_, e) =>
            {
                partListController.SetPartTabs(manager.Song.Parts.Count, e.Part.Value);
                partListController.SelectTab(e.Part.Value);

                var p = manager.Song.Parts[e.Part.Value];
                partSettingsPanel.Bind(p);
                trackListController.SetTrackTabs(p.Tracks?.Count ?? 0, (p.Tracks?.Count ?? 0) > 0 ? 0 : -1);
                trackDetailsPanel.OnPartSignatureChanged(p.TimeSignature, p.Measures);
            };

            manager.PartRemoved += (_, e) =>
            {
                int count = manager.Song.Parts?.Count ?? 0;

                int sel = manager.ActivePart.Value;
                if (sel < 0 && count > 0) sel = 0;
                sel = Mathf.Clamp(sel, 0, Mathf.Max(0, count - 1));

                partListController.SetPartTabs(count, sel);
            };

            manager.ActivePartChanged += (_, e) =>
            {
                partListController.SelectTab(e.Part.Value);
                var p = manager.Song.Parts[e.Part.Value];
                partSettingsPanel.Bind(p);
                var tCount = p.Tracks?.Count ?? 0;
                trackListController.SetTrackTabs(tCount, tCount > 0 ? 0 : -1);

                // Auto-select track 0 if it exists so details bind immediately
                if (tCount > 0) manager.SelectTrack(new TrackIdx(0));
            };

            // Track lifecycle
            manager.TrackAdded += (_, e) =>
            {
                var p = manager.Song.Parts[e.Part.Value];
                trackListController.SetTrackTabs(p.Tracks.Count, e.Track.Value);
                trackListController.SelectTab(e.Track.Value);
                trackDetailsPanel.BindTrack(p, p.Tracks[e.Track.Value], p.TimeSignature);
            };

            manager.TrackRemoved += (_, e) =>
            {
                var p = manager.Song.Parts[e.Part.Value];
                int count = p.Tracks?.Count ?? 0;

                int sel = manager.ActiveTrack.Value;
                if (sel < 0 && count > 0) sel = 0;
                sel = Mathf.Clamp(sel, 0, Mathf.Max(0, count - 1));

                trackListController.SetTrackTabs(count, sel);
            };

            manager.ActiveTrackChanged += (_, e) =>
            {
                var p = manager.Song.Parts[e.Part.Value];
                var t = p.Tracks[e.Track.Value];
                trackListController.SelectTab(e.Track.Value);
                trackDetailsPanel.BindTrack(p, t, p.TimeSignature);
            };

            // Structure (keeps input field synced when changed programmatically)
            manager.StructureChanged += (_, __) =>
            {
                sequenceInputField.SetTextWithoutNotify(manager.SerializeStructure());
            };
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
                manager.ReplaceSong(new SongConfig
                {
                    Parts = new List<SongConfig.PartConfig>(),
                    Structure = new List<SongConfig.PartSequenceEntry>()
                });

                sequenceInputField.SetTextWithoutNotify(string.Empty);

                // Seed a fresh part/track so UI has something to bind to
                manager.AddPart();
                manager.AddTrack(defaultTrackRole);
                return;
            }

            // Otherwise load the chosen ScriptableObject
            var so = availableConfigs[selectedIndex - 1];
            LoadSongConfigSO(so);
        }

        private void LoadSongConfigSO(SongConfigSO so)
        {
            // Use the store to produce a deep runtime clone from the asset
            var clone = configStore.CloneFromAsset(so);

            // Replace the current song in the manager
            manager.ReplaceSong(clone);

            // Repopulate the structure input text
            sequenceInputField.SetTextWithoutNotify(
                sequenceSerializer.Serialize(clone.Structure));

            // Select first part/track if present
            if (clone.Parts != null && clone.Parts.Count > 0)
            {
                manager.SelectPart(new PartIdx(0));
                if (clone.Parts[0].Tracks != null && clone.Parts[0].Tracks.Count > 0)
                    manager.SelectTrack(new TrackIdx(0));
            }
        }

        private void SavePart(int idx)
        {
            if (idx < 0 || idx >= songConfig.Parts.Count) return;
            Debug.Log($"<color=white>Saving part {idx}</color>");
            if (activeTrack >= 0) SaveActiveTrack();
        }

        private void SaveActiveTrack()
        {
            if (activePart < 0 || activeTrack < 0) return;
            var p = songConfig.Parts[activePart];
            if (p.Tracks == null || activeTrack >= p.Tracks.Count) return;
            trackDetailsPanel.SaveInto(p.Tracks[activeTrack]);
        }

        private void OnGenerateAndPlay()
        {
            if (activeTrack >= 0) SaveActiveTrack();
            if (activePart >= 0) SavePart(activePart);
            UpdateStructureFromInput(); // now writes through manager

            // Your existing metronome toggles / channel volume logic can stay
            var backing = songConfig.Parts
                .SelectMany(p => p.Tracks)
                .FirstOrDefault(t => t.Role == TrackRole.Backing);

            foreach (var (p, pIdx) in manager.Song.Parts.Select((pp, ii) => (pp, ii)))
            {
                var tracksStr = (p.Tracks ?? new List<SongConfig.PartConfig.TrackConfig>())
                    .Select((t, ti) =>
                        $"[t{ti}:{t.Role} inst={t.Instrument?.InstrumentName ?? "-"} perc={t.PercussionInstrument?.InstrumentName ?? "-"} patt={(t.Parameters?.Pattern ? t.Parameters.Pattern.name : "-")}]")
                    .Aggregate("", (acc, s) => acc + " " + s);

                Debug.Log($"[Gen] p{pIdx} {p.Tonality}/{p.RootNote} {p.TimeSignature}x{p.Measures} :: {tracksStr}");
            }

            var fullSong = midiGenerator.GenerateSong(manager.Song);
            lastSong = fullSong;

            // Apply (or mute) metronome channel volume per toggle
            var metroVol = (useMetronomeToggle != null && useMetronomeToggle.isOn)
                ? Mathf.Clamp(settings.metronomeChannelVolume, 0, 127)
                : 0;
            MidiGenerator.ApplyChannelVolume(fullSong, MidiGenerator.MetronomeChannel, metroVol);

            midiPlayback.Stop();
            midiPlayback.Play(fullSong);
        }

        private void UpdateStructureFromInput()
        {
            if (manager.TrySetStructureFromString(sequenceInputField.text, out var warnings))
            {
                // ok
            }
            if (warnings != null)
                foreach (var w in warnings) Debug.LogWarning(w);
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

        private void HandleTimeSignatureChanged(TimeSignature ts)
        {
            if (activePart < 0 || activePart >= songConfig.Parts.Count) return;
            var p = songConfig.Parts[activePart];
            trackDetailsPanel.OnPartSignatureChanged(ts, p.Measures);
        }

        private void HandleMeasuresChanged(int measures)
        {
            if (activePart < 0 || activePart >= songConfig.Parts.Count) return;
            var ts = songConfig.Parts[activePart].TimeSignature;
            trackDetailsPanel.OnPartSignatureChanged(ts, measures);
        }
    }
}
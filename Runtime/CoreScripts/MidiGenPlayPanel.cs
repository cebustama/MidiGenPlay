using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.UI;
using Melanchall.DryWetMidi.MusicTheory;
using MidiPlayerTK;
using System.Collections;
using System.IO;

using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;

namespace MidiGenPlay
{
    public class MidiGenPlayPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Dropdown instrumentDropdown;
        [SerializeField] private TMP_Dropdown roleDropdown;
        [SerializeField] private TMP_Dropdown tonalityDropdown;
        [SerializeField] private TMP_Dropdown rootNoteDropdown;
        [SerializeField] private TMP_Dropdown tempoDropdown;
        [SerializeField] private TMP_Dropdown timeSignatureDropdown;
        [SerializeField] private Toggle metronomeToggle;
        [SerializeField] private Button generateAndPlayButton;

        [Header("Backing Settings")]
        [SerializeField] private MIDIInstrumentSO backingTrackInstrument;
        [SerializeField] private ChordProgressionData backingProgressionData;
        [Header("Percussion Settings")]
        [SerializeField] private MIDIPercussionInstrumentSO percussionInstrument;
        [SerializeField] private DrumPatternData drumPatternData;
        [Header("Lead Settings")]
        [SerializeField] private MIDIInstrumentSO leadInstrument;
        [SerializeField] private MelodyPatternData leadMelodyData;


        private const string InstrumentsFolderPath = "ScriptableObjects/MIDI Instruments";

        private MidiGenerator midiGenerator;
        private MidiFilePlayer midiFilePlayer;

        private void Awake()
        {
            generateAndPlayButton.onClick.AddListener(GenerateAndPlaySong);

            midiFilePlayer = GetComponentInChildren<MidiFilePlayer>();
            midiFilePlayer.OnEventNotesMidi.AddListener(NotesToPlay);
        }

        private void Start()
        {
            PopulateInstrumentDropdown();
            PopulateDropdownWithEnum<TrackRole>(roleDropdown);
            PopulateDropdownWithEnum<MusicTheory.Tonality>(tonalityDropdown);
            PopulateDropdownWithEnum<NoteName>(rootNoteDropdown);
            PopulateDropdownWithEnum<MusicTheory.TempoRange>(tempoDropdown);
            PopulateDropdownWithEnum<MusicTheory.TimeSignature>(timeSignatureDropdown);
        }

        private void PopulateDropdownWithEnum<T>(TMP_Dropdown dropdown) where T : Enum
        {
            dropdown.ClearOptions();
            List<string> options = new List<string> { "Any" }; // Add "Any" option at the beginning
            options.AddRange(Enum.GetNames(typeof(T)).ToList());
            dropdown.AddOptions(options);
        }

        private void PopulateInstrumentDropdown()
        {
            instrumentDropdown.ClearOptions();

            var instruments = Resources.LoadAll<MIDIInstrumentSO>(InstrumentsFolderPath);
            List<string> options = new List<string> { "Any" };
            options.AddRange(instruments.Select(instrument => instrument.name).ToList());

            instrumentDropdown.AddOptions(options);
        }

        private void GenerateAndPlaySong()
        {
            // Retrieve values or choose random if "Any" is selected
            var instrument = GetDropdownValueOrRandom(instrumentDropdown, LoadInstruments());
            var role = GetDropdownValueOrRandomEnum<TrackRole>(roleDropdown);
            var tonality = GetDropdownValueOrRandomEnum<MusicTheory.Tonality>(tonalityDropdown);
            tonality = MusicTheory.GetRandomTonalityByWeight();
            var rootNote = GetDropdownValueOrRandomEnum<NoteName>(rootNoteDropdown);
            var tempoRange = GetDropdownValueOrRandomEnum<MusicTheory.TempoRange>(tempoDropdown);
            var timeSignature = GetDropdownValueOrRandomEnum<MusicTheory.TimeSignature>(timeSignatureDropdown);
            bool metronome = metronomeToggle.isOn;

            Debug.Log($"Instrument: {instrument?.name ?? "Random"}");
            Debug.Log($"Role: {role}");
            Debug.Log($"Tonality: {tonality}");
            Debug.Log($"Root Note: {rootNote}");
            Debug.Log($"Tempo: {tempoRange}");
            Debug.Log($"Time Signature: {timeSignature}");
            Debug.Log($"Metronome: {metronome}");

            int bpm = MusicTheory.GetBPMFromRange(tempoRange, MusicTheory.TempoRule.MultiplesOfTen);

            midiGenerator = new MidiGenerator();
            int measures = 4;

            // Generate Backing Track
            var backingTrackMidiFile = midiGenerator.GenerateChordProgressionMidiTrackFile(
                instrument,
                role,
                tonality,
                rootNote,
                bpm,
                timeSignature,
                measures,
                channel: 0,
                backingProgressionData);

            // Generate Drum Track
            var rhythmTrackMidiFile = midiGenerator.GenerateRhythmTrackWithPattern(
                percussionInstrument,
                drumPatternData,
                bpm,
                timeSignature,
                measures,
                channel: 1);

            var melodyTrackMidiFile = midiGenerator.GenerateMelodyTrackWithPattern(
                instrument, 
                leadMelodyData, 
                tonality, 
                rootNote, 
                bpm, 
                timeSignature,
                measures,
                channel: 2
            );

            // 🟣 3️⃣ Merge Tracks
            var finalMidiFile = MergeMidiFiles(backingTrackMidiFile, rhythmTrackMidiFile);
            finalMidiFile = melodyTrackMidiFile;

            // 🔴 4️⃣ Add Metronome if enabled
            if (metronome)
            {
                var metronomeFile = midiGenerator.GenerateMetronomeTrackFile(
                    timeSignature, 
                    bpm, 
                    measures
                );

                finalMidiFile = MergeMidiFiles(finalMidiFile, metronomeFile);
            }

            // 🔥 5️⃣ Play the generated song
            PlaySong(finalMidiFile);
        }


        private MidiFile MergeMidiFiles(params MidiFile[] midiFiles)
        {
            var finalMidiFile = new MidiFile();

            foreach (var midiFile in midiFiles)
            {
                foreach (var trackChunk in midiFile.GetTrackChunks())
                {
                    finalMidiFile.Chunks.Add(trackChunk.Clone());
                }
            }

            return finalMidiFile;
        }

        private MIDIInstrumentSO GetDropdownValueOrRandom(TMP_Dropdown dropdown, List<MIDIInstrumentSO> instruments)
        {
            string selectedValue = dropdown.options[dropdown.value].text;
            if (selectedValue == "Any")
            {
                return instruments[UnityEngine.Random.Range(0, instruments.Count)];
            }
            return instruments.FirstOrDefault(i => i.name == selectedValue);
        }

        private T GetDropdownValueOrRandomEnum<T>(TMP_Dropdown dropdown) where T : Enum
        {
            string selectedValue = dropdown.options[dropdown.value].text;
            if (selectedValue == "Any")
            {
                Array values = Enum.GetValues(typeof(T));
                return (T)values.GetValue(UnityEngine.Random.Range(0, values.Length));
            }
            return (T)Enum.Parse(typeof(T), selectedValue);
        }

        private List<MIDIInstrumentSO> LoadInstruments()
        {
            return Resources.LoadAll<MIDIInstrumentSO>(InstrumentsFolderPath).ToList();
        }

        private void PlaySong(MidiFile midiFile)
        {
            StartCoroutine(DelayedPlay(0, midiFile));
        }

        private IEnumerator DelayedPlay(float delay, MidiFile midiFile)
        {
            // Convert the MidiFile to byte array (if not already in that format)
            byte[] midiData;
            using (var memoryStream = new MemoryStream())
            {
                midiFile.Write(memoryStream);
                midiData = memoryStream.ToArray();
            }

            // Stop any previous playback to avoid conflicts
            midiFilePlayer.MPTK_Stop();

            // Wait for the specified delay
            yield return new WaitForSeconds(delay);

            Debug.Log("<color=red>PLAYING SONG...</color>");

            // Start playback by directly setting and playing the MIDI data
            midiFilePlayer.MPTK_Play(midiData);
        }

        public void NotesToPlay(List<MPTKEvent> mptkEvents)
        {
            Debug.Log("Received " + mptkEvents.Count + " MIDI Events");
            // Loop on each MIDI events
            foreach (MPTKEvent mptkEvent in mptkEvents)
            {
                // Log if event is a note on
                if (mptkEvent.Command == MPTKCommand.NoteOn)
                    Debug.Log($"Note on Time:{mptkEvent.RealTime} millisecond  Note:{mptkEvent.Value}  Duration:{mptkEvent.Duration} millisecond  Velocity:{mptkEvent.Velocity}");

                //Uncomment to display all MIDI events
                Debug.Log(mptkEvent.ToString());
            }
        }
    }
}

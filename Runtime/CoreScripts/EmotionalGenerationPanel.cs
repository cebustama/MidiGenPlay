using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay;

using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;
using Melanchall.DryWetMidi.Core;
using MidiPlayerTK;
using System.IO;

public class EmotionalGenerationPanel : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private int measures = 16;
    [SerializeField] private bool useBackingTrack = true;
    [SerializeField] private bool usePercussionTrack = true;
    [SerializeField] private bool useMelodyTrack = true;

    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Dropdown emotionDropdown;
    [SerializeField] private Button generateAndPlayButton;
    [SerializeField] private Button quitButton;

    [Header("Emotions Musical Data")]
    [SerializeField] private EmotionMusicalData fearMusicalData;
    [SerializeField] private EmotionMusicalData braveryMusicalData;
    [SerializeField] private EmotionMusicalData sadnessMusicalData;
    [SerializeField] private EmotionMusicalData joyMusicalData;
    [SerializeField] private EmotionMusicalData calmMusicalData;
    [SerializeField] private EmotionMusicalData wrathMusicalData;
    [SerializeField] private EmotionMusicalData confidenceMusicalData;
    [SerializeField] private EmotionMusicalData insecurityMusicalData;
    [SerializeField] private EmotionMusicalData hopeMusicalData;
    [SerializeField] private EmotionMusicalData despairMusicalData;

    private MidiGenerator midiGenerator;
    private MidiFilePlayer midiFilePlayer;

    private void Awake()
    {
        midiFilePlayer = GetComponentInChildren<MidiFilePlayer>();

        PopulateDropdownWithEnum<EmotionMusicalData.MusicalEmotion>(emotionDropdown);
        generateAndPlayButton.onClick.AddListener(GenerateAndPlaySong);
        quitButton.onClick.AddListener(() => { Application.Quit(); });

        emotionDropdown.onValueChanged.AddListener(delegate { SetBackgroundColor(); });
    }

    private void GenerateAndPlaySong()
    {
        Debug.Log("<color=cyan>Generating Song Based on Emotion...</color>");

        // Retrieve Selected Emotion
        string selectedEmotion = emotionDropdown.options[emotionDropdown.value].text;

        if (selectedEmotion == "Any")
        {
            // Randomly pick an emotion
            Array emotions = Enum.GetValues(typeof(EmotionMusicalData.MusicalEmotion));
            var randomEmotion = (EmotionMusicalData.MusicalEmotion)
                emotions.GetValue(UnityEngine.Random.Range(0, emotions.Length));

            Debug.Log($"Random Emotion Selected: <b>{randomEmotion}</b>");

            // Update dropdown selection
            emotionDropdown.value = emotionDropdown.options.FindIndex(
                option => option.text == randomEmotion.ToString()
            );

            // Update background color
            SetBackgroundColor();

            selectedEmotion = randomEmotion.ToString();
        }

        EmotionMusicalData selectedEmotionData = GetEmotionDataByName(selectedEmotion);

        // Randomly Select Tonality, Tempo, and Time Signature
        var tonality = selectedEmotionData.possibleTonalities[
            UnityEngine.Random.Range(0, selectedEmotionData.possibleTonalities.Count)];

        var bpm = MusicTheory.GetBPMFromRange(
            selectedEmotionData.possibleTempoRanges[
                UnityEngine.Random.Range(0, selectedEmotionData.possibleTempoRanges.Count)],
            MusicTheory.TempoRule.MultiplesOfTen);

        var timeSignature = selectedEmotionData.possibleTimeSignatures[
            UnityEngine.Random.Range(0, selectedEmotionData.possibleTimeSignatures.Count)];

        // Randomly Select Instruments
        var percussionInstrument = selectedEmotionData.possiblePercussionInstruments
            .Where(i => i is MIDIPercussionInstrumentSO)
            .Cast<MIDIPercussionInstrumentSO>()
            .OrderBy(_ => UnityEngine.Random.value)
            .FirstOrDefault();

        var backingInstrument = selectedEmotionData.possibleBackingInstruments
            .Where(i => i is MIDIInstrumentSO)
            .Cast<MIDIInstrumentSO>()
            .OrderBy(_ => UnityEngine.Random.value)
            .FirstOrDefault();

        var leadInstrument = selectedEmotionData.possibleLeadInstruments
            .Where(i => i is MIDIInstrumentSO)
            .Cast<MIDIInstrumentSO>()
            .OrderBy(_ => UnityEngine.Random.value)
            .FirstOrDefault();

        // Select Progressions and Patterns
        var chordProgression = selectedEmotionData.chordProgressionsList
            .GetRandomProgressionByTimeSignatureAndTonality(timeSignature, tonality);

        var drumPattern = selectedEmotionData.drumPatternsList
            .GetRandomPatternByTimeSignature(timeSignature);

        var melodyPattern = selectedEmotionData.melodyPatternsList
            .GetRandomPatternByTimeSignature(timeSignature);

        // Debug Logging
        Debug.Log($"Tonality: {tonality}");
        Debug.Log($"Tempo: {bpm} BPM");
        Debug.Log($"Time Signature: {timeSignature}");
        Debug.Log($"Percussion Instrument: {percussionInstrument?.InstrumentName ?? "None"}");
        Debug.Log($"Backing Instrument: {backingInstrument?.InstrumentName ?? "None"}");
        Debug.Log($"Lead Instrument: {leadInstrument?.InstrumentName ?? "None"}");
        Debug.Log($"Chord Progression: {chordProgression?.progressionName ?? "None"}");
        Debug.Log($"Drum Pattern: {drumPattern?.patternName ?? "None"}");
        Debug.Log($"Melody Pattern: {melodyPattern?.melodyName ?? "None"}");

        // Initialize MIDI Generator
        MidiGenerator midiGenerator = new MidiGenerator();
        int measures = this.measures;
        NoteName rootNote = NoteName.C;
        var finalMidiFile = new MidiFile();

        // Generate Tracks Based on User Selection
        if (usePercussionTrack && percussionInstrument != null && drumPattern != null)
        {
            Debug.Log("<color=green>Generating Percussion Track...</color>");
            var rhythmTrackMidiFile = midiGenerator.GenerateRhythmTrackWithPattern(
                percussionInstrument,
                drumPattern,
                bpm,
                timeSignature,
                measures,
                channel: 0);

            MergeIntoFinalMidi(finalMidiFile, rhythmTrackMidiFile);
        }

        if (useBackingTrack && backingInstrument != null && chordProgression != null)
        {
            Debug.Log("<color=green>Generating Backing Track...</color>");
            var backingTrackMidiFile = midiGenerator.GenerateChordProgressionMidiTrackFile(
                backingInstrument,
                TrackRole.Backing,
                tonality,
                rootNote,
                bpm,
                timeSignature,
                measures,
                channel: 1,
                chordProgression);

            MergeIntoFinalMidi(finalMidiFile, backingTrackMidiFile);
        }

        if (useMelodyTrack && leadInstrument != null && melodyPattern != null)
        {
            Debug.Log("<color=green>Generating Melody Track...</color>");
            var melodyTrackMidiFile = midiGenerator.GenerateMelodyTrackWithPattern(
                leadInstrument,
                melodyPattern,
                tonality,
                rootNote,
                bpm,
                timeSignature,
                measures,
                channel: 2);

            MergeIntoFinalMidi(finalMidiFile, melodyTrackMidiFile);
        }

        // Play the final MIDI file
        PlaySong(finalMidiFile);
    }

    /// <summary>
    /// Merges an individual track into the final MIDI file.
    /// </summary>
    private void MergeIntoFinalMidi(MidiFile finalMidiFile, MidiFile trackMidiFile)
    {
        foreach (var trackChunk in trackMidiFile.GetTrackChunks())
        {
            finalMidiFile.Chunks.Add(trackChunk.Clone());
        }
    }

    /// <summary>
    /// Retrieves EmotionMusicalData based on the selected emotion name.
    /// </summary>
    private EmotionMusicalData GetEmotionDataByName(string emotionName)
    {
        if (emotionName == "Any")
        {
            // Randomly pick an emotion
            Array emotions = Enum.GetValues(typeof(EmotionMusicalData.MusicalEmotion));
            var randomEmotion = (EmotionMusicalData.MusicalEmotion)
                emotions.GetValue(UnityEngine.Random.Range(0, emotions.Length));

            Debug.Log($"Random Emotion Selected: <b>{randomEmotion}</b>");
            return GetEmotionData(randomEmotion);
        }

        // Parse the selected emotion
        EmotionMusicalData.MusicalEmotion parsedEmotion =
            (EmotionMusicalData.MusicalEmotion)Enum.Parse(
                typeof(EmotionMusicalData.MusicalEmotion), emotionName
            );

        return GetEmotionData(parsedEmotion);
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

    private void PlaySong(MidiFile midiFile)
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

        Debug.Log("<color=red>PLAYING SONG...</color>");

        // Start playback by directly setting and playing the MIDI data
        midiFilePlayer.MPTK_Play(midiData);
    }

    private EmotionMusicalData GetEmotionData(EmotionMusicalData.MusicalEmotion emotion)
    {
        return emotion switch
        {
            EmotionMusicalData.MusicalEmotion.Fear => fearMusicalData,
            EmotionMusicalData.MusicalEmotion.Bravery => braveryMusicalData,
            EmotionMusicalData.MusicalEmotion.Sadness => sadnessMusicalData,
            EmotionMusicalData.MusicalEmotion.Joy => joyMusicalData,
            EmotionMusicalData.MusicalEmotion.Calm => calmMusicalData,
            EmotionMusicalData.MusicalEmotion.Wrath => wrathMusicalData,
            EmotionMusicalData.MusicalEmotion.Confidence => confidenceMusicalData,
            EmotionMusicalData.MusicalEmotion.Insecurity => insecurityMusicalData,
            EmotionMusicalData.MusicalEmotion.Hope => hopeMusicalData,
            EmotionMusicalData.MusicalEmotion.Despair => despairMusicalData,
            _ => throw new ArgumentException($"Emotion {emotion} not mapped!")
        };
    }

    private void PopulateDropdownWithEnum<T>(TMP_Dropdown dropdown) where T : Enum
    {
        dropdown.ClearOptions();
        List<string> options = new List<string> { "Any" };
        options.AddRange(Enum.GetNames(typeof(T)).ToList());
        dropdown.AddOptions(options);
    }

    private void SetBackgroundColor()
    {
        string selectedEmotion = emotionDropdown.options[emotionDropdown.value].text;

        if (selectedEmotion == "Any")
        {
            backgroundImage.color = Color.gray; // Neutral color for "Any"
            return;
        }

        EmotionMusicalData.MusicalEmotion parsedEmotion =
            (EmotionMusicalData.MusicalEmotion)Enum.Parse(
                typeof(EmotionMusicalData.MusicalEmotion), selectedEmotion
            );

        EmotionMusicalData selectedEmotionData = GetEmotionData(parsedEmotion);

        if (selectedEmotionData != null)
        {
            backgroundImage.color = selectedEmotionData.emotionColor;
        }
    }
}

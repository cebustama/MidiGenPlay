using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiGenPlay; // Ensure access to MusicTheory namespace

[CreateAssetMenu(menuName = "MidiGenPlay/Emotion Musical Data")]
public class EmotionMusicalData : ScriptableObject
{
    public enum MusicalEmotion
    {
        Fear,
        Bravery,
        Sadness,
        Joy,
        Calm,
        Wrath,
        Confidence,
        Insecurity,
        Hope,
        Despair
    }

    [Header("Emotion")]
    public MusicalEmotion musicalEmotion; // The emotion associated with this data

    [Header("Color")]
    public Color emotionColor;

    [Header("Instrument Selection")]
    public List<MIDIInstrumentSO> possiblePercussionInstruments;
    public List<MIDIInstrumentSO> possibleBackingInstruments;
    public List<MIDIInstrumentSO> possibleLeadInstruments;

    [Header("Tonality & Scale")]
    public List<MusicTheory.Tonality> possibleTonalities; // Possible tonalities (Major, Minor, Modes)

    [Header("Rhythm & Tempo")]
    public List<MusicTheory.TempoRange> possibleTempoRanges; // Possible tempo ranges
    public List<MusicTheory.TimeSignature> possibleTimeSignatures; // Possible time signatures

    [Header("Harmonic & Rhythmic Structure")]
    public ChordProgressionsList chordProgressionsList;
    public DrumPatternsList drumPatternsList;
    public MelodyPatternsList melodyPatternsList;
}

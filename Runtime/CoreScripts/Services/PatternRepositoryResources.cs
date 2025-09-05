using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Services
{
    public class PatternRepositoryResources : IPatternRepository
    {
        private const string DRUMS_PATH = "ScriptableObjects/Patterns/Drums";
        private const string CHORDS_PATH = "ScriptableObjects/Patterns/Chords";
        private const string MELODIES_PATH = "ScriptableObjects/Patterns/Melodies";

        private List<DrumPatternData> allDrums = new();
        private List<ChordProgressionData> allChords = new();
        private List<MelodyPatternData> allMelodies = new();

        public void Refresh()
        {
            allDrums = Resources.LoadAll<DrumPatternData>(DRUMS_PATH).ToList();
            allChords = Resources.LoadAll<ChordProgressionData>(CHORDS_PATH).ToList();
            allMelodies = Resources.LoadAll<MelodyPatternData>(MELODIES_PATH).ToList();
        }

        public IReadOnlyList<DrumPatternData> GetAllDrumPatterns() => allDrums;
        public IReadOnlyList<ChordProgressionData> GetAllChordProgressions() => allChords;
        public IReadOnlyList<MelodyPatternData> GetAllMelodyPatterns() => allMelodies;

        public IReadOnlyList<DrumPatternData> GetDrumPatterns(TimeSignature ts)
            => allDrums.Where(p => p.timeSignature == ts).ToList();

        public IReadOnlyList<ChordProgressionData> GetChordProgressions(TimeSignature ts)
            => allChords.Where(p => p.timeSignature == ts).ToList();

        public IReadOnlyList<MelodyPatternData> GetMelodyPatterns(TimeSignature ts)
            => allMelodies.Where(p => p.timeSignature == ts).ToList();
    }

}
using System.Collections.Generic;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Interfaces
{
    public interface IPatternRepository
    {
        // One call to (re)load everything
        void Refresh();

        // Unfiltered
        IReadOnlyList<DrumPatternData> GetAllDrumPatterns();
        IReadOnlyList<ChordProgressionData> GetAllChordProgressions();
        IReadOnlyList<MelodyPatternData> GetAllMelodyPatterns();

        // Filtered by Time Signature
        IReadOnlyList<DrumPatternData> GetDrumPatterns(TimeSignature ts);
        IReadOnlyList<ChordProgressionData> GetChordProgressions(TimeSignature ts);
        IReadOnlyList<MelodyPatternData> GetMelodyPatterns(TimeSignature ts);

        string GetChordWriteFolder();
    }
}
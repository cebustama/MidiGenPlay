using MidiGenPlay.UI;
using UnityEngine;

namespace MidiGenPlay.Interfaces
{
    public interface IChordGridAdapter
    {
        // Build grid size + initial state from ScriptableObject
        void BindToGrid(
            PatternGrid grid, ChordProgressionData data, int beatsPerMeasure, int measures);

        // Write back grid state to ScriptableObject
        void WriteBack(PatternGrid grid, ChordProgressionData data);

        // Optional: step subdivisions used by this pattern type (usually 1 for chords)
        int Subdivisions { get; }
    }
}
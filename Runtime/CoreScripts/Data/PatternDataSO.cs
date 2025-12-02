using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    public abstract class PatternDataSO : ScriptableObject
    {
        public string DisplayName;
        public TimeSignature TimeSignature;
        [Min(1)] public int Measures;
    }
}
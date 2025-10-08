using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    public abstract class PatternDataSO : ScriptableObject
    {
        public string displayName;
        public TimeSignature timeSignature;
        [Min(1)] public int measures;
    }
}
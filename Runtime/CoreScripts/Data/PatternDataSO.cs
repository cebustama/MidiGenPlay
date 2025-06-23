using UnityEngine;

namespace MidiGenPlay
{
    public abstract class PatternDataSO : ScriptableObject
    {
        public string displayName;
        public MusicTheory.TimeSignature timeSignature;
        public int measures;
    }
}
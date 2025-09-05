using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    public abstract class PatternDataSO : ScriptableObject
    {
        public string displayName;
        public TimeSignature timeSignature;
        public int measures;
    }
}
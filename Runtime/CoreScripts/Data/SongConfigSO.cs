using UnityEngine;

namespace MidiGenPlay
{
    [CreateAssetMenu(menuName = "MidiGenPlay/Song Config", fileName = "NewSongConfig")]
    public class SongConfigSO : ScriptableObject
    {
        public SongConfig Config;
    }
}
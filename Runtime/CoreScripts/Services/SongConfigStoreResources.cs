using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MidiGenPlay.Services
{
    /// <summary>
    /// Resources-backed store for SongConfigSO assets.
    /// Editor-only Save uses AssetDatabase; list/refresh works at runtime too.
    /// </summary>
    public class SongConfigStoreResources : ISongConfigStore
    {
        // TODO: Configurable per-project, maybe add a config window?
        private const string SONG_CONFIGS_PATH = "ScriptableObjects/Song Configs";

        private List<SongConfigSO> all = new();

        public void Refresh()
        {
            all = Resources.LoadAll<SongConfigSO>(SONG_CONFIGS_PATH).ToList();
        }

        public IReadOnlyList<SongConfigSO> GetAll() => all;

        /// Returns a deep runtime clone of the SongConfig stored inside the asset.
        public SongConfig CloneFromAsset(SongConfigSO asset)
        {
            if (asset == null || asset.Config == null)
                return new SongConfig
                {
                    Parts = new List<SongConfig.PartConfig>(),
                    Structure = new List<SongConfig.PartSequenceEntry>()
                };
            return Clone(asset.Config);
        }

#if UNITY_EDITOR
        public void SaveNewAsset(SongConfig runtimeConfig)
        {
            // Ask user for location/name
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Save SongConfig",
                "NewSongConfig",
                "asset",
                "Choose a location in your project");

            if (string.IsNullOrEmpty(path)) return;

            // Create SO and deep-copy the runtime config into it
            var so = ScriptableObject.CreateInstance<SongConfigSO>();
            so.Config = Clone(runtimeConfig);

            UnityEditor.AssetDatabase.CreateAsset(so, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
        }
#endif

        // Deep copy that mirrors your existing save/load logic in the panel.
        internal static SongConfig Clone(SongConfig src)
        {
            var dst = new SongConfig
            {
                Parts = src.Parts?.Select(p => new SongConfig.PartConfig
                {
                    Name = p.Name,
                    Tonality = p.Tonality,
                    RootNote = p.RootNote,
                    TempoRange = p.TempoRange,
                    TimeSignature = p.TimeSignature,
                    Measures = p.Measures,
                    Tracks = p.Tracks?.Select(t => new SongConfig.PartConfig.TrackConfig
                    {
                        Instrument = t.Instrument,
                        PercussionInstrument = t.PercussionInstrument,
                        Role = t.Role,
                        Parameters = new TrackParameters
                        {
                            Pattern = t.Parameters?.Pattern
                        }
                    }).ToList() ?? new List<SongConfig.PartConfig.TrackConfig>()
                }).ToList() ?? new List<SongConfig.PartConfig>(),
                Structure = src.Structure?.Select(e => new SongConfig.PartSequenceEntry
                {
                    PartIndex = e.PartIndex,
                    RepeatCount = e.RepeatCount
                }).ToList() ?? new List<SongConfig.PartSequenceEntry>()
            };
            return dst;
        }
    }
}
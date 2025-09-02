using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Interfaces
{
    public interface ISongConfigStore
    {
        /// (Re)load list of configs (eg from Resources)
        void Refresh();

        /// Return the cached list loaded by Refresh().
        IReadOnlyList<SongConfigSO> GetAll();

        /// Create a deep runtime clone from a SongConfigSO asset.
        SongConfig CloneFromAsset(SongConfigSO asset);

        /// Save the given runtime config as a new SongConfigSO asset.
        /// (UI prompt + AssetDatabase) — available only in the Editor.
#if UNITY_EDITOR
        void SaveNewAsset(SongConfig runtimeConfig);
#endif
    }
}


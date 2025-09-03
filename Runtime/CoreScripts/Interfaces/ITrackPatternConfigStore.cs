using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Interfaces
{
    /// <summary>
    /// Read/Write store for user-authored pattern ScriptableObjects.
    /// - Loads from Resources (or any provider the impl chooses)
    /// - Creates brand-new assets
    /// - Saves (overwrite) or "Save As New"
    /// - No UI concerns — pure persistence & discovery
    /// </summary>
    public interface ITrackPatternConfigStore<T> where T : ScriptableObject
    {
        /// <summary>Re-scan the source (e.g., Resources) and refresh the cache.</summary>
        void Refresh();

        /// <summary>All known pattern assets (cached after Refresh).</summary>
        IReadOnlyList<T> GetAll();

        /// <summary>
        /// Optional filter helper. Implementations may no-op if not relevant.
        /// </summary>
        IReadOnlyList<T> GetWhere(Func<T, bool> predicate);

#if UNITY_EDITOR
        /// <summary>
        /// Create a brand-new in-memory ScriptableObject (not yet saved).
        /// Caller is expected to fill its fields before Save*().
        /// </summary>
        T CreateNewInProject(string defaultName = null);

        /// <summary>
        /// Save changes to an existing asset on disk (overwrite).
        /// If the asset is not yet saved, throws unless allowCreateIfNew is true.
        /// </summary>
        void Save(T asset, bool allowCreateIfNew = false);

        /// <summary>
        /// Save a copy of the given asset as a new asset on disk under a generated or provided name.
        /// Returns the newly-created persisted instance.
        /// </summary>
        T SaveAsNew(T source, string name = null);

        /// <summary>Delete the given asset from disk (editor only).</summary>
        void Delete(T asset);
#endif
    }
}
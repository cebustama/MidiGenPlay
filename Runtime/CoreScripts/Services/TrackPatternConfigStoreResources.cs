using MidiGenPlay.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MidiGenPlay.Services
{
    /// <summary>
    /// Generic Resources-backed pattern store. Works for Chords/Drums/Melody SOs.
    /// </summary>
    public class TrackPatternConfigStoreResources<T> :
        ITrackPatternConfigStore<T> where T : ScriptableObject
    {
        private readonly string resourcesLoadPath;   // e.g., "ScriptableObjects/Patterns/Chords"
        private readonly string assetsSaveRootPath;  // e.g., "Assets/Resources/ScriptableObjects/Patterns/Chords"
        private readonly Func<T, bool> defaultPredicate; // optional

        private readonly List<T> cache = new();

        /// <param name="typeFolder">Subfolder like "Chords", "Drums", "Melodies".</param>
        /// <param name="defaultPredicate">Optional common filter (e.g., TS match).</param>
        public TrackPatternConfigStoreResources(
            string typeFolder, Func<T, bool> defaultPredicate = null)
        {
            resourcesLoadPath = $"ScriptableObjects/Patterns/{typeFolder}";
            assetsSaveRootPath = Path.Combine("Assets", "Resources", resourcesLoadPath);
            this.defaultPredicate = defaultPredicate;
        }

        /// <summary>
        /// Project-relative save root for this store's assets, e.g.
        /// "Assets/Resources/ScriptableObjects/Patterns/Drums". Editor windows use this
        /// to seed save dialogs and folder scans so the old per-window hardcoded
        /// DefaultSaveFolder constants can be removed (PATTERN-PERSIST-1 / D4): the store
        /// is the single source of "where this pattern type lives". Pure string, no
        /// editor APIs — intentionally not #if UNITY_EDITOR-guarded.
        /// </summary>
        public string AssetsSaveRootPath => assetsSaveRootPath;

        public void Refresh()
        {
            cache.Clear();
            var loaded = Resources.LoadAll<T>(resourcesLoadPath);
            cache.AddRange(loaded);
        }

        public IReadOnlyList<T> GetAll() => cache;

        public IReadOnlyList<T> GetWhere(Func<T, bool> predicate)
        {
            if (predicate == null && defaultPredicate == null) return cache;
            var p = predicate ?? defaultPredicate;
            return cache.Where(p).ToList();
        }

#if UNITY_EDITOR
        public T CreateNewInProject(string defaultName = null)
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.name = string.IsNullOrWhiteSpace(defaultName) ? typeof(T).Name : defaultName.Trim();
            return instance;
        }

        public void Save(T asset, bool allowCreateIfNew = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            EnsureFolders();

            string path = AssetDatabase.GetAssetPath(asset);
            bool isNew = string.IsNullOrEmpty(path);

            if (isNew && !allowCreateIfNew)
                throw new InvalidOperationException(
                    $"Asset {asset.name} isn't saved yet. Use SaveAsNew() or set allowCreateIfNew=true.");

            if (isNew)
            {
                string uniquePath = GetUniqueAssetPath(asset.name);
                AssetDatabase.CreateAsset(asset, uniquePath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            Refresh();
        }

        public T SaveAsNew(T source, string name = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            EnsureFolders();

            // We duplicate a *serialized* copy to avoid linking to the original in memory.
            var clone = ScriptableObject.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            clone.name = string.IsNullOrWhiteSpace(name) ? MakeCopyName(source.name) : name.Trim();

            string uniquePath = GetUniqueAssetPath(clone.name);
            AssetDatabase.CreateAsset(clone, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();
            return clone;
        }

        /// <summary>
        /// PATTERN-PERSIST-1 / D6=C. Persist an in-memory instance as a NEW asset at an
        /// explicit, caller-chosen project path (e.g. one returned by
        /// EditorUtility.SaveFilePanelInProject). Unlike SaveAsNew, the path is NOT
        /// auto-generated: the editor window keeps ownership of the interactive naming
        /// dialog while the store owns the AssetDatabase write. This is create-only — if
        /// the caller populates <paramref name="instance"/> under its own Undo scope
        /// AFTER this call, it must follow with Save(instance) to flush those field edits
        /// and refresh the cache. <paramref name="projectPath"/>'s folder must already
        /// exist (SaveFilePanelInProject only returns paths inside existing folders).
        /// </summary>
        public void PersistNewAtPath(T instance, string projectPath)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(projectPath))
                throw new ArgumentException("A project-relative path is required.", nameof(projectPath));

            AssetDatabase.CreateAsset(instance, projectPath);
        }

        public void Delete(T asset)
        {
            if (asset == null) return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"Delete skipped: {asset.name} is not a persisted asset.");
                return;
            }

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            Refresh();
        }

        // --- helpers ---

        private void EnsureFolders()
        {
            // Ensure Assets/Resources/ScriptableObjects/Patterns/<TypeFolder> exists
            var parts = assetsSaveRootPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string accum = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = Path.Combine(accum, parts[i]);
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(accum, parts[i]);
                }
                accum = next;
            }
        }

        private string GetUniqueAssetPath(string baseName)
        {
            string file = $"{SanitizeFileName(baseName)}.asset";
            string path = Path.Combine(assetsSaveRootPath, file).Replace("\\", "/");
            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

        private static string MakeCopyName(string original)
        {
            return string.IsNullOrWhiteSpace(original) ? "New" : $"{original} Copy";
        }

        private static string SanitizeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "New" : s;
        }
#endif
    }
}
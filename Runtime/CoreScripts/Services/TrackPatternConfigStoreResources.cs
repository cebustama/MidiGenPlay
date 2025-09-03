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
        private readonly string resourcesLoadPath;   // e.g., "MidiGenPlay/Patterns/Chords"
        private readonly string assetsSaveRootPath;  // e.g., "Assets/Resources/MidiGenPlay/Patterns/Chords"
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
            // Ensure Assets/Resources/MidiGenPlay/Patterns/<TypeFolder> exists
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
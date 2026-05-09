using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Services
{
    public class PatternRepositoryResources : IPatternRepository
    {
        // Package paths (resource-relative)
        private const string PKG_DRUMS = "ScriptableObjects/Patterns/Drums";
        private const string PKG_CHORDS = "ScriptableObjects/Patterns/Chords";
        private const string PKG_MELODIES = "ScriptableObjects/Patterns/Melodies";

        private readonly MidiGenPlayConfig cfg;

        private List<DrumPatternData> drums = new();
        private List<ChordProgressionData> chords = new();
        private List<MelodyPatternData> melodies = new();

        public PatternRepositoryResources(MidiGenPlayConfig settings = null)
        {
            cfg = settings ?? MidiGenPlayConfig.FindInResources()
                          ?? ScriptableObject.CreateInstance<MidiGenPlayConfig>();
            Refresh();
        }

        public void Refresh()
        {
            // --- DRUMS ---
            int pkgC, locC;
            drums = LoadBoth<DrumPatternData>(
                PKG_DRUMS, cfg.ResourcesDrumsPath, out pkgC, out locC);
            // --- CHORDS ---
            int pkgC2, locC2;
            chords = LoadBoth<ChordProgressionData>(
                PKG_CHORDS, cfg.ResourcesChordsPath, out pkgC2, out locC2);
            // --- MELODIES ---
            int pkgC3, locC3;
            melodies = LoadBoth<MelodyPatternData>(
                PKG_MELODIES, cfg.ResourcesMelodiesPath, out pkgC3, out locC3);

            if (cfg.logRepository)
                Debug.Log($"[PatternRepo] Loaded " +
                          $"Drums: pkg={pkgC}, local={locC}, total={drums.Count} | " +
                          $"Chords: pkg={pkgC2}, local={locC2}, total={chords.Count} | " +
                          $"Melodies: pkg={pkgC3}, local={locC3}, total={melodies.Count}");
        }

        private static List<T> LoadBoth<T>(
            string pkgPath, string localPath, out int pkgCount, out int localCount)
            where T : UnityEngine.Object
        {
            var result = new List<T>();
            var seen = new HashSet<T>();

            // Package
            var pkg = Resources.LoadAll<T>(pkgPath) ?? System.Array.Empty<T>();
            foreach (var x in pkg) if (x && seen.Add(x)) result.Add(x);
            pkgCount = pkg.Length;

            // Local (skip if same path to avoid double load)
            localCount = 0;
            if (!string.Equals(pkgPath, localPath, System.StringComparison.Ordinal))
            {
                var loc = Resources.LoadAll<T>(localPath) ?? System.Array.Empty<T>();
                foreach (var x in loc) { if (x && seen.Add(x)) result.Add(x); }
                localCount = loc.Length;
            }

            return result;
        }

        public IReadOnlyList<DrumPatternData> GetAllDrumPatterns() => drums;
        public IReadOnlyList<ChordProgressionData> GetAllChordProgressions() => chords;
        public IReadOnlyList<MelodyPatternData> GetAllMelodyPatterns() => melodies;

        public IReadOnlyList<DrumPatternData> GetDrumPatterns(TimeSignature ts)
            => drums.Where(p => p.TimeSignature == ts).ToList();
        public IReadOnlyList<ChordProgressionData> GetChordProgressions(TimeSignature ts)
            => chords.Where(p => p.TimeSignature == ts).ToList();
        public IReadOnlyList<MelodyPatternData> GetMelodyPatterns(TimeSignature ts)
            => melodies.Where(p => p.TimeSignature == ts).ToList();

        // Always return LOCAL write folder (Assets/Resources/...) in Editor.
        // Player builds: returns null. The path is only meaningful for editor-side
        // asset authoring (AssetDatabase writes); no runtime caller exists per the
        // cross-project bridge SSoT, which classifies this as authoring-tool surface.
        public string GetChordWriteFolder()
        {
#if UNITY_EDITOR
            return cfg.GetChordWriteFolder();
#else
            return null;
#endif
        }
    }

}
using MidiGenPlay.Composition;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

[CreateAssetMenu(fileName = "MidiGenPlayConfig", menuName = "MidiGenPlay/Config")]
public class MidiGenPlayConfig : ScriptableObject
{
    // --- PACKAGE (read-only) resource roots: DO NOT CHANGE ---
    private const string pckgPatternsRoot = "ScriptableObjects/Patterns";
    private const string pckgInstrumentsRoot = "ScriptableObjects/MIDI Instruments";

    // Public accessors for package paths (Resources.LoadAll needs resource-relative paths)
    public string PackageChordsPath => $"{pckgPatternsRoot}/Chords";
    public string PackageDrumsPath => $"{pckgPatternsRoot}/Drums";
    public string PackageMelodiesPath => $"{pckgPatternsRoot}/Melodies";
    public string PackageInstrumentsPath => pckgInstrumentsRoot;

    [Header("Resources (LOCAL project)")]
    [Tooltip("Root under *Assets/Resources/* used by Resources.LoadAll for patterns in THIS project.")]
    public string resourcesPatternsRoot = "ScriptableObjects/Patterns"; // local resource root
    [Tooltip("Path under *Assets/Resources/* for instruments in THIS project.")]
    public string resourcesInstrumentsPath = "ScriptableObjects/MIDI Instruments";
    [Tooltip("Create directories if missing when saving assets.")]
    public bool autoCreateFolders = true;

    [Header("Log toggles")]
    public bool logUI = true;
    public bool logRepository = false;
    public bool logGenerator = true;
    public bool logMidiMusicManager = true;

    [Header("Dev / QA")]
    public bool debugDumpMidi = false;

    [Header("Defaults / fallbacks")]
    public Tonality defaultTonality = Tonality.Ionian;
    public TimeSignature defaultTimeSignature = TimeSignature.FourFour;
    public int defaultMeasures = 4;
    public int defaultChordSubdivisions = 1;
    [Tooltip("If true, the chord popup suggests diatonic 7ths when appropriate.")]
    public bool preferSeventhInUI = false;

    [Header("Playback")]
    [Tooltip("If non-zero, Random.InitState(defaultSeed) can be used for deterministic generation.")]
    public int defaultSeed = 0;
    [Range(0, 127)] public int metronomeChannelVolume = 110;

    [Header("Chord Labels / Sync")]
    [Tooltip("Ventana en ticks para empatar etiquetas chd: con NoteOn (±valor).")]
    public int chordLabelTickTolerance = 2;

    [Header("Chord Progressions")]
    public ChordProgressionLibrarySO progressionLibrary;

    [Header("Voice Leadings")]
    public VoiceLeadingConfig voiceLeading;
    public MelodicLeadingConfig melodicLeading;
    public HarmonicLeadingConfig harmonicLeading;

    [Header("Tonality / Modal Profiles")]
    public TonalityProfileSO[] tonalityProfiles;

    // --------- LOCAL resources helper properties ----------
    public string ResourcesChordsPath => $"{resourcesPatternsRoot}/Chords";
    public string ResourcesDrumsPath => $"{resourcesPatternsRoot}/Drums";
    public string ResourcesMelodiesPath => $"{resourcesPatternsRoot}/Melodies";

#if UNITY_EDITOR
    // --------- Write-path helpers (Editor) ----------
    // ALWAYS write to LOCAL Assets/Resources/* (never to Packages)
    public string GetChordWriteFolder() => ResolveWriteFolder("Chords");
    public string GetDrumWriteFolder() => ResolveWriteFolder("Drums");
    public string GetMelodyWriteFolder() => ResolveWriteFolder("Melodies");

    private string ResolveWriteFolder(string leaf)
    {
        // "Assets/Resources/" + local configured root + leaf
        string localRoot = string.IsNullOrWhiteSpace(resourcesPatternsRoot)
            ? "Assets/Resources/ScriptableObjects/Patterns"
            : $"Assets/Resources/{resourcesPatternsRoot.TrimStart('/', '\\')}";

        string finalPath = CombineSafe(localRoot, leaf);
        if (autoCreateFolders) System.IO.Directory.CreateDirectory(finalPath);
        return finalPath;
    }

    private static string CombineSafe(string root, string child)
    {
        if (string.IsNullOrEmpty(root)) return null;
        root = root.TrimEnd('/', '\\');
        return $"{root}/{child}";
    }

    /// <summary>
    /// Return the TonalityProfileSO that matches a given Tonality,
    /// or null if we don't have one.
    /// </summary>
    public TonalityProfileSO GetProfileForTonality(Tonality ton)
    {
        if (tonalityProfiles == null) return null;
        for (int i = 0; i < tonalityProfiles.Length; i++)
        {
            var p = tonalityProfiles[i];
            if (p != null && p.tonality == ton)
                return p;
        }
        return null;
    }
#endif

    // --------- Discovery ----------
    public static MidiGenPlayConfig FindInResources(string name = "MidiGenPlayConfig")
        => Resources.Load<MidiGenPlayConfig>(name);
}

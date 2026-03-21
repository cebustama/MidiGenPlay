> ABSORBED SOURCE — preserved during governance migration. Use current SSoTs in `runtime/` and `authoring/` as the active authorities.

# MidiGenPlay — Runtime Architecture & API Guide (Event‑Driven)

> Version: post–SongConfigManager refactor (event‑driven), Unity runtime–friendly.

This document summarizes the **updated classes, responsibilities, and main methods** in the MidiGenPlay system after introducing an event‑driven **Domain Layer**. It focuses on:

- `SongConfig` (data model)  
- `ISongConfigManager` / `SongConfigManager` (domain state & events)  
- `GenerateMidiSongPanel` (scene composer & playback entry)  
- `TrackDetailsPanel` (per‑track UI, now event‑driven)  
- Referenced service interfaces (repositories, serialization, IO, playback)  
- Event ordering, extension points, and practical examples


---

## 1) High‑Level Overview

### Goals
- **Single Source of Truth:** The `SongConfigManager` owns runtime song state (parts, tracks, structure, selection).
- **Event‑Driven UI:** Views (panels) **never mutate the model directly**; they call the manager, which raises events.
- **Runtime Friendly:** No editor‑only code in domain logic; editor actions isolated in UI/controllers.
- **Composable Services:** Repositories, serialization, IO, and playback accessed via interfaces.

### Main Flow
1. Scene creates repositories, serializer, store, and the **SongConfigManager**.  
2. UI subscribes to **manager events** (`PartAdded/Removed`, `ActivePartChanged`, `TrackAdded/Removed`, `ActiveTrackChanged`, `StructureChanged`, etc.) to update tabs and bind panels.  
3. UI actions (dropdowns, add/remove buttons, pattern edits) **call manager methods** (`SetTrackRole`, `SetMelodicInstrument`, `SetTrackPattern`, …).  
4. **Generate & Play**: build `MidiFile` from `manager.Song`, optionally set metronome channel volume, and send to playback.


---

## 2) Data Model — `SongConfig`

```csharp
public class SongConfig
{
    public List<PartConfig> Parts;
    public List<PartSequenceEntry> Structure;

    // Optional channel annotations
    public List<string>     ChannelMusicianOrder = new();
    public List<TrackRole>  ChannelRoles         = new();

    [Serializable]
    public class PartConfig
    {
        public string Name;
        public List<TrackConfig> Tracks;

        public Tonality      Tonality;
        public NoteName      RootNote;
        public TempoRange    TempoRange;
        public TimeSignature TimeSignature;
        public int           Measures;

        [Serializable]
        public class TrackConfig
        {
            public MIDIInstrumentSO             Instrument;
            public MIDIPercussionInstrumentSO   PercussionInstrument;
            public TrackRole                    Role;
            public TrackParameters              Parameters;
            public string                       MusicianId;
        }
    }

    [Serializable] public class PartSequenceEntry { public int PartIndex; public int RepeatCount; }
}

[Serializable] public class TrackParameters { public PatternDataSO Pattern; }
```

**Notes**
- **Parts** hold **Tracks**, tonality, signature, and measure count.  
- **TrackConfig.Parameters.Pattern** can be a chord progression, drum pattern, or other pattern type.  
- `Structure` is a sequence of `{ PartIndex, RepeatCount }` entries.


---

## 3) Domain Layer — `ISongConfigManager` & `SongConfigManager`

### Index Helpers & Event Payloads
```csharp
public readonly struct PartIdx  { public readonly int Value; public PartIdx(int v){Value=v;} }
public readonly struct TrackIdx { public readonly int Value; public TrackIdx(int v){Value=v;} }

public sealed class PartChangedEventArgs  : EventArgs { public PartIdx  Part  { get; } ... }
public sealed class TrackChangedEventArgs : EventArgs { public PartIdx  Part  { get; } public TrackIdx Track { get; } ... }
```

### Interface
```csharp
public interface ISongConfigManager
{
    SongConfig Song { get; }
    PartIdx    ActivePart  { get; }
    TrackIdx   ActiveTrack { get; }

    // Selection
    void SelectPart(PartIdx p);
    void SelectTrack(TrackIdx t);

    // Parts
    PartIdx AddPart(SongConfig.PartConfig template = null);
    void    RemovePart(PartIdx p);

    // Tracks (within ActivePart)
    TrackIdx AddTrack(TrackRole defaultRole = TrackRole.Backing);
    void     RemoveTrack(TrackIdx t);

    // Mutators
    void SetPartSignature(PartIdx p, TimeSignature ts, int measures);
    void SetPartTonality(PartIdx p, Tonality tonality, NoteName root);
    void SetTrackRole(PartIdx p, TrackIdx t, TrackRole role);
    void SetMelodicInstrument(PartIdx p, TrackIdx t, MIDIInstrumentSO inst);
    void SetPercInstrument(PartIdx p, TrackIdx t, MIDIPercussionInstrumentSO inst);
    void SetTrackPattern(PartIdx p, TrackIdx t, PatternDataSO patternAssetOrRuntime);

    // Structure
    bool   TrySetStructureFromString(string input, out List<string> warnings);
    string SerializeStructure();

    // Replace entire runtime song (used by Load/Reset flows)
    void ReplaceSong(SongConfig newSong);

    // Events
    event EventHandler                                 SongReplaced;
    event EventHandler<PartChangedEventArgs>           PartAdded;
    event EventHandler<PartChangedEventArgs>           PartRemoved;
    event EventHandler<PartChangedEventArgs>           PartUpdated;
    event EventHandler<PartChangedEventArgs>           ActivePartChanged;
    event EventHandler<TrackChangedEventArgs>          TrackAdded;
    event EventHandler<TrackChangedEventArgs>          TrackRemoved;
    event EventHandler<TrackChangedEventArgs>          ActiveTrackChanged;
    event EventHandler<TrackChangedEventArgs>          TrackUpdated;
    event EventHandler                                 StructureChanged;
}
```

### Implementation Highlights
- **Selection**: `SelectPart` resets `ActiveTrack`.  
- **Add/Remove**: Emit `PartAdded/Removed` or `TrackAdded/Removed`, then set/raise **active** selection (`ActivePartChanged` / `ActiveTrackChanged`).  
- **Mutators**: Change model then emit `PartUpdated` or `TrackUpdated`.  
- **Structure**: Use injected `ISequenceSerializer` for `TryParse/Serialize`.  
- **ReplaceSong**: Swaps the entire model (used for **Load** and **Reset** flows) and raises `SongReplaced` then `ActivePartChanged`.  
- **Invariants**: Validates indices; track operations require an active part.

> **Event ordering tip:** During `RemovePart` / `RemoveTrack`, do **not** index into `Parts[oldIndex]` in `...Removed` handlers. Wait for the subsequent `ActivePartChanged` / `ActiveTrackChanged` (which point to a **valid** selection).


---

## 4) Scene Composer — `GenerateMidiSongPanel`

**Purpose**: Orchestrates UI wiring, manager creation, load/save flows, generation, and playback.

### Key Serialized Fields (Inspector)
- Settings: `MidiGenPlayConfig`  
- UI: `PartListController`, `PartSettingsPanelController`, `TrackListController`, `TrackDetailsPanel`  
- Controls: `generateButton`, `useMetronomeToggle`, `loopToggle`  
- Config I/O: `saveConfigButton`, `loadConfigDropdown`  
- `sequenceInputField` (structure text)

### Important Methods
- **`Awake()`**  
  - Instantiates repositories (`IInstrumentRepository`, `IPatternRepository`), `ISequenceSerializer`, `ISongConfigStore`, and `SongConfigManager`.  
  - Subscribes **UI → Manager** (button/listeners) and **Manager → UI** (event handlers).  
  - Seeds first part/track.  
  - Hooks playback adapter: `IPlayMidi.OnSongEnded → HandleSongEnded`.

- **`SubscribeManagerEvents()`**  
  - `PartAdded/Removed/ActivePartChanged` update tabs and bind the part settings.  
  - `TrackAdded/Removed/ActiveTrackChanged` update track tabs and call `trackDetailsPanel.BindTrack(...)`.  
  - `StructureChanged` syncs the text field.

- **`PopulateLoadConfigDropdown()`** & **`OnLoadConfigDropdownChanged(int)`**  
  - Option 0 is reset to a fresh song (`ReplaceSong` + seed part/track).  
  - Else: `configStore.CloneFromAsset(...) → manager.ReplaceSong(...)` then select first part/track.

- **`OnGenerateAndPlay()`**  
  - `SaveActiveTrack()` and `SavePart(activePart)` to persist edit UI.  
  - `UpdateStructureFromInput()` → `manager.TrySetStructureFromString(...)`.  
  - `midiGenerator.GenerateSong(manager.Song)` → `MidiFile`.  
  - **Metronome volume**: `MidiGenerator.ApplyChannelVolume(fullSong, MetronomeChannel, metroVol)` per toggle.  
  - `midiPlayback.Stop()` & `midiPlayback.Play(fullSong)`.

- **`HandleSongEnded()`**  
  - If loop toggle on, replay `lastSong` via `IMidiPlayback`.

**Why event‑driven here?**  
The panel doesn’t touch lists/indices directly. It updates UI only upon **manager events**, avoiding out‑of‑range errors during removals and centralizing logic.


---

## 5) Per‑Track View — `TrackDetailsPanel` (Event‑Driven)

**Purpose**: Shows/edits a single track’s role, instruments, and patterns. **All mutations** go through the **manager**.

### Lifecycle
- **`Initialize(settings, melodicInstruments, percInstruments, patternRepo)`**  
  Populates dropdowns, builds role controllers, subscribes UI.
- **`SetManager(ISongConfigManager)`**  
  Provide manager reference for mutations.
- **`BindTrack(part, track, timeSignature)`**  
  - Map bound refs to indices (`PartIdx`, `TrackIdx`) for manager calls.  
  - Refresh pattern lists, set role dropdown, activate proper role controller panel, and bind runtime pattern UI panels.
- **`SaveInto(TrackConfig)`**  
  - Push current UI selections through manager (`SetTrackRole`, `SetMelodicInstrument`/`SetPercInstrument`, `SetTrackPattern`).

### UI Event Handlers (examples)
- **Role** → `HandleRoleChanged()` → `manager.SetTrackRole(...)`, activates proper role UI, binds runtime pattern, and updates instrument groups (melodic vs percussion).  
- **Melodic instrument dropdown** → `manager.SetMelodicInstrument(...)` (+ update piano key range).  
- **Perc instrument dropdown** → `manager.SetPercInstrument(...)`.  
- **Pattern dropdowns** → bind editor panel & `manager.SetTrackPattern(..., panel.GetRuntime())`.

### Editor Buttons (optional)
- **Save/New Chord Progression** and **Save/New Drum Pattern**  
  - Save into asset or create new asset via role panel helpers.  
  - `patternRepo.Refresh()` then refresh dropdowns, select the new asset, and push the **runtime clone** through the manager.

**Why event‑driven here?**  
- Centralizes validation/constraints inside **manager** (e.g., percussion role expects a percussion instrument).  
- Enables multiple observers (e.g., a mixer window) to react to `TrackUpdated` without coupling to this panel.


---

## 6) Referenced Service Interfaces (Summary)

- **`IInstrumentRepository`**: `Refresh()`, `IEnumerable<MIDIInstrumentSO> GetMelodicInstruments()`, `IEnumerable<MIDIPercussionInstrumentSO> GetPercussionInstruments()`  
- **`IPatternRepository`**: `Refresh()`, listing/providing chord/drum patterns; filters by time signature in role controllers.  
- **`ISequenceSerializer`**: `bool TryParse(string, int partCount, out List<PartSequenceEntry>, out List<string> warnings)`, `string Serialize(List<PartSequenceEntry>)`.  
- **`ISongConfigStore`**: `Refresh()`, `IEnumerable<SongConfigSO> GetAll()`, `SongConfig CloneFromAsset(SongConfigSO)`, `void SaveNewAsset(SongConfig runtime)`.  
- **`IMidiPlayback`**: `Play(MidiFile)`, `Stop()`.  
- **`IPlayMidi` (adapter component)**: exposes `event Action OnSongEnded` that the panel subscribes to.

> Implementations used: `InstrumentRepositoryResources`, `PatternRepositoryResources`, `SequenceSerializer`, `SongConfigStoreResources`, `MidiPlayback` (wraps `IPlayMidi`).


---

## 7) Event Ordering & Safe UI Patterns

**Removal pattern**  
- Manager does: remove → raise `...Removed` → compute next → `Select...` → raise `Active...Changed`.  
- **UI rule:** In `...Removed` handlers, **don’t index into lists**—just refresh tab count/selection. Rebind UI in the subsequent `Active...Changed` handler where indices are valid.

**Structure changes**  
- Write structure via `TrySetStructureFromString` (surface warnings).  
- Read structure via `SerializeStructure()` (used to keep text input synced).


---

## 8) Typical Scenarios (Examples)

### A) Fresh Startup
1. Create manager and repositories in `Awake()`.
2. `manager.AddPart()` → **PartAdded** → tabs update; **ActivePartChanged** → part binds.  
3. `manager.AddTrack()` → **TrackAdded** → track tab & **BindTrack** call.

### B) Load a SongConfig asset
1. Dropdown → `OnLoadConfigDropdownChanged(index)`  
2. `configStore.CloneFromAsset(...)` → `manager.ReplaceSong(clone)`  
3. `ActivePartChanged` (0) → tabs/UI bind; optionally `ActiveTrackChanged` (0).

### C) Edit Track
- Change role, instrument, or pattern → panel calls `manager.Set*` → **TrackUpdated** (observers refresh).

### D) Generate & Play
- `midiGenerator.GenerateSong(manager.Song)` → `MidiFile`
- Apply metronome volume (0 or configured) to metronome channel.
- `midiPlayback.Play(file)`; adapter `OnSongEnded` replays if loop toggle is on.


---

## 9) Extension Points

- **New TrackRole**: add a new role UI controller and attach it to the roleControllers map; implement binding and saving; manager doesn’t need changes (just persists role & patterns).  
- **Custom validation**: override or extend manager mutators to normalize/validate (e.g., enforce drum map).  
- **Secondary views**: subscribe to `TrackUpdated`/`PartUpdated` to reflect changes elsewhere (mixer, HUD, analytics).  
- **Alternate repositories**: provide different implementations (Addressables, remote DB) without UI changes.  
- **Playback controller**: extract generation/playback into a small component if you want to reuse it elsewhere.


---

## 10) Public API Cheat‑Sheet

### SongConfigManager (selected)
- `AddPart() / RemovePart(p)`  
- `AddTrack() / RemoveTrack(t)`  
- `SelectPart(p) / SelectTrack(t)`  
- `SetPartSignature(p, ts, measures)`, `SetPartTonality(p, tonality, root)`  
- `SetTrackRole(p, t, role)`, `SetMelodicInstrument(p, t, inst)`, `SetPercInstrument(p, t, inst)`, `SetTrackPattern(p, t, pattern)`  
- `TrySetStructureFromString(text, out warnings)`, `SerializeStructure()`  
- `ReplaceSong(song)`

### GenerateMidiSongPanel (selected)
- `OnLoadConfigDropdownChanged(index)` → load/reset via manager  
- `OnGenerateAndPlay()` → generate, set metro volume, play  
- `HandleSongEnded()` → looping via `IPlayMidi.OnSongEnded`

### TrackDetailsPanel (selected)
- `Initialize(settings, melodic, perc, patternRepo)`  
- `SetManager(manager)`  
- `BindTrack(part, track, timeSignature)`  
- `SaveInto(track)` (push current UI back to manager)  
- `OnPartSignatureChanged(ts, measures)` / `SetTonality(tonality)`


---

## 11) Practical Notes & Pitfalls

- **Do not** index into collections during **Removed** events. Wait for `Active...Changed`.  
- Keep **pattern panels** in sync by rebinding when assets change; push **runtime clones** to the manager.  
- When saving/creating assets in the Editor, `patternRepo.Refresh()` then refresh the dropdowns and set selection.  
- Maintain clear separation: UI drives **intent**, Manager owns **state**, Services provide **data/external IO**.


---

## 12) Quick Wiring Reference (Scene)

```csharp
// Awake():
instrumentRepo   = new InstrumentRepositoryResources(settings);
patternRepo      = new PatternRepositoryResources(settings);
sequenceSerializer = new SequenceSerializer();
configStore        = new SongConfigStoreResources();

manager = new SongConfigManager(settings, instrumentRepo, patternRepo, sequenceSerializer, configStore);

trackDetailsPanel.Initialize(settings, instrumentRepo.GetMelodicInstruments().ToList(), instrumentRepo.GetPercussionInstruments().ToList(), patternRepo);
trackDetailsPanel.SetManager(manager);

// Subscribe UI -> manager & manager -> UI, then seed:
manager.AddPart();
manager.AddTrack(defaultRole);
```


---

**End of document.**

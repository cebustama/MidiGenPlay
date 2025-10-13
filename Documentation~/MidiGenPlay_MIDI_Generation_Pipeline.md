
# MidiGenPlay — MIDI Generation Pipeline (Refactored)

_Last updated: 2025-10-13_

This document describes the refactored MIDI generation pipeline used by **MidiGenPlay** for ALWTTT. It presents the global picture and breaks it down into components, their responsibilities, and how they interact at runtime.

---

## 1) Big‑picture overview

```
[UI / Domain Layer]
  └─ builds a SongConfig (parts, tracks, patterns, tonality, etc.)

[MidiGenerator]  ← facade
  ├─ Composer Registry: TrackRole → ITrackComposer
  └─ SongOrchestrator (timeline & assembly)
        ├─ PASS 1: compose all tracks except Harmony
        ├─ PASS 2: compose Harmony (reads Melody/Lead)
        ├─ Stamp meta (tempo, time signature, markers, chord tags)
        ├─ Apply channel/bank/patch (if not already stamped by composer)
        ├─ Trim to part length, offset to absolute song time
        ├─ Merge all tracks, add "All Sound Off" at boundaries
        └─ (optional) Metronome per repetition

[ITrackComposer] per role
  ├─ MelodyComposerMinimal      → musical notes (Pattern/MidiFile)
  ├─ HarmonyComposerMinimal     → harmony notes from melody & chords
  ├─ ChordTrackComposer         → voiced chords (with IChordVoicer)
  ├─ BassTrackComposer          → sustained roots / chord tones
  └─ RhythmTrackComposer        → drums from grid or legacy piano roll

[DryWetMIDI]
  ├─ PatternBuilder → Pattern → MidiFile
  └─ TrackChunk / TimedEvents utilities

[Playback]
  └─ IMidiPlayback (e.g., MPTK) plays the MidiFile
```

---

## 2) Core components

### 2.1 MidiGenerator (facade)
- **Purpose**: Single entry point from game systems (UI/Domain). Owns the composer registry and delegates final song creation to the `SongOrchestrator`.
- **Key responsibilities**
  - Hold `Dictionary<TrackRole, ITrackComposer>` (the “plugin point”).
  - Construct and pass a `GenContext` per part repetition.
  - Expose `GenerateSong(SongConfig)` as the public API.
- **Inputs**: `SongConfig`, global configs (e.g., `MidiGenPlayConfig`), `IChordVoicer`, melody/harmony strategies.
- **Output**: A complete `MidiFile` ready for playback.

### 2.2 SongOrchestrator (timeline & assembly)
- **Purpose**: Coordinates parts/repetitions, meta events, calling composers, and final assembly. **Does not contain note‑level composition**.
- **Key responsibilities**
  - **Timeline**: iterate `song.Structure`, compute absolute cursor ticks, part duration, and per‑repetition tempo/time signature.
  - **Meta**: insert `TimeSignatureEvent`, `SetTempoEvent`, DAW-friendly markers (`TextEvent`, `MarkerEvent`).
  - **Metronome** (optional): per repetition on a fixed channel.
  - **Two‑phase composition**: all roles except Harmony → then Harmony (which consumes the produced Melody/Lead).
  - **Assembly**: trim files to part length, tag musician, offset to absolute ticks, and merge into the master `MidiFile`.
  - **Safety**: add `All Sound Off` at exact part ends; optional logging/inspection hooks.
- **Collaborators**
  - `ITrackComposer` per role.
  - `GenContext` (RNG, chord progression lookup, melody accessors, chord‑event lookup, voicer preset, etc.).
  - DryWetMIDI (`PatternBuilder`, `TempoMap`, `TrackChunk`, `TimedEvent`).

### 2.3 ITrackComposer & concrete composers
- **Contract**: 
  ```csharp
  public interface ITrackComposer {
      MidiFile Compose(
          SongConfig.PartConfig part,
          SongConfig.PartConfig.TrackConfig cfg,
          int bpm,
          int channel,
          MidiGenerator.GenContext ctx);
  }
  ```
- **Concrete implementations**
  - **ChordTrackComposer**: Repeats the `ChordProgressionData`, voices chords via `IChordVoicer` (`BasicVoiceLeadingVoicer` by default), stamps chord tags, sets bank/patch, and forces channel.
  - **MelodyComposerMinimal**: Melody from progression using an `IMelodyStrategy` + `MelodicLeadingConfig` (note density & placement rules; nearest‑chord‑tone strategy by default).
  - **HarmonyComposerMinimal**: Harmony from the generated melody using `IHarmonyStrategy` + `HarmonicLeadingConfig` (e.g., nearest different chord tone).
  - **BassTrackComposer**: Simple root (or random chord tone) per chord event in a low register.
  - **RhythmTrackComposer**: Drums from grid (`DrumPatternData.lanes`) or legacy piano‑roll; stamps kit bank/patch; forces channel 9 by channel map.

> Each composer focuses on **what notes** to write. Orchestrator focuses on **when and where** to place them in the global timeline.

### 2.4 Strategies & voicing (pluggable)
- **IMelodyStrategy** → e.g., `NearestChordToneMelodyStrategy` (bias stepwise motion, clamp range, note density / length mode via `MelodicLeadingConfig`).
- **IHarmonyStrategy** → e.g., `NearestDifferentChordToneHarmonyStrategy` (distance clamp, relation to melody via `HarmonicLeadingConfig`).
- **IChordVoicer** → `BasicVoiceLeadingVoicer` (inversions, optional drop‑2, spacing constraints, register drift control via `VoiceLeadingConfig`).

### 2.5 Configuration data (SO‑friendly)
- **MidiGenPlayConfig**: toggles (logging), shared presets (`VoiceLeadingConfig`, `MelodicLeadingConfig`, `HarmonicLeadingConfig`), default seed.
- **VoiceLeadingConfig**: tunable chord‑voicing rules (candidate set, spacing, register drift, debug scoring).
- **MelodicLeadingConfig / HarmonicLeadingConfig**: melody/harmony density, placement, interval/motion limits.
- **Instrument data**: `MIDIInstrumentSO`, `MIDIPercussionInstrumentSO` (bank/patch, playable range, GM percussion mapping).
- **Patterns**: `ChordProgressionData`, `DrumPatternData` (and optionally `MelodyPatternData`).

### 2.6 GenContext (per repetition)
Holds utility delegates/state needed during composition:
- `rng` (deterministic when seeded), `ChordVoicer`, and a voicing preset.
- `DefaultMelodicInstrument` (optional fallback).
- Delegates for: `GetTrackForRole`, `ExtractMonophonicNotes`, `FindChordEventAt`, `GetProgressionForPart`.

---

## 3) Orchestration lifecycle (per structure entry)

1. **Compute context**
   - Determine BPM from part tempo range, build a per‑part `TempoMap` and time‑signature.
   - Build a **channel map** (Rhythm → ch 9, others assigned sequentially).
2. **Stamp part markers**
   - `TextEvent part:…` and a `MarkerEvent` at the part start tick.
3. **Metronome (optional)**
   - Compose a simple tic/tac pattern; assign metronome bank/patch/channel; merge.
4. **Pass 1: compose non‑Harmony roles**
   - For each track (except Harmony): `composer.Compose(…)` → trim → tag musician → offset to the part start → merge.
   - Cache produced tracks by role in a local map.
5. **Pass 2: compose Harmony**
   - Harmony composer retrieves Melody/Lead via `ctx.GetTrackForRole` and chooses harmony notes accordingly; then trim/tag/offset/merge.
6. **Boundary event**
   - Insert `All Sound Off` at the exact end tick to avoid sustain bleed.
7. **Advance cursor**
   - Move the timeline cursor by the part’s duration and continue with repetitions/next parts.

---

## 4) Interactions (who talks to whom)

- **UI/Domain → MidiGenerator**: requests `GenerateSong(songConfig)`.
- **MidiGenerator → SongOrchestrator**: delegates the build for the whole song.
- **SongOrchestrator → ITrackComposer**: calls per track with the computed BPM, channel, and `GenContext`.
- **Composers → DryWetMIDI**: use `PatternBuilder`/`Pattern`/`TempoMap` to produce a `MidiFile`.
- **SongOrchestrator → DryWetMIDI**: applies meta events, merges, offsets, and boundaries in the master file.
- **Playback**: separate service consumes the final `MidiFile`.

---

## 5) Extensibility map

| Goal | Add/Change | No edits required in |
|------|------------|----------------------|
| New musical role | Implement `ITrackComposer` and register for the new `TrackRole` | `SongOrchestrator`, existing composers |
| New melody style | Implement `IMelodyStrategy` (optionally a ScriptableObject) | Orchestrator, other composers |
| New harmony logic | Implement `IHarmonyStrategy` | Orchestrator, other composers |
| Different chord voicing | Implement `IChordVoicer` and set in config/context | Composers that don’t use chords |
| New pattern source | Implement/extend data providers (`ChordProgressionData`/`DrumPatternData` or an interface wrapper) | Orchestrator, unrelated composers |
| Alternate metronome | Replace metronome helper in orchestrator | Composers |

---

## 6) Real‑time usage notes

- **Determinism**: seed the RNG per repetition via `GenContext` for repeatable runs.
- **Performance**: cache scale/tempo data per part; avoid excess allocations in hot paths; reuse composers.
- **Hot‑swapping**: strategies and voicers can be swapped at runtime (SO assets or code), without touching the orchestrator.

---

## 7) How to include **all** interfaces & data in the docs

To generate the **best** documentation, we can include every public interface, config, and data type. Two options:

1) **Upload remaining files** here and I’ll regenerate this doc with an API Reference appendix. Helpful files include:
   - Interfaces: `ITrackComposer`, `IMelodyStrategy`, `IHarmonyStrategy`, `IChordVoicer`, `IMidiPlayback`.
   - Data: `SongConfig`, `MIDIInstrumentSO`, `MIDIPercussionInstrumentSO`, `ChordProgressionData`, `DrumPatternData`, `MelodyPatternData` (if present).
   - Configs: `MelodicLeadingConfig`, `HarmonicLeadingConfig`, `MidiGenPlayConfig`.
   - Utilities: any timing helpers, pattern repositories, etc.

2) **Auto‑extract (scripted)**: I can produce a small script that scans `.cs` files and appends a type/member summary to this markdown (namespaces, public classes/interfaces, public methods/fields). Drop your sources in a `/Docs/src` folder and run the script to refresh the **API Reference** section automatically.

> Either way, once those files are available, I’ll append an **Appendix: API Reference** with signatures for quick lookup.

---

## 8) Minimal reference (signatures)

```csharp
public interface ISongOrchestrator {
    MidiFile GenerateSong(SongConfig song);
}

public interface ITrackComposer {
    MidiFile Compose(
        SongConfig.PartConfig part,
        SongConfig.PartConfig.TrackConfig cfg,
        int bpm,
        int channel,
        MidiGenerator.GenContext ctx);
}

// GenContext (abridged)
public class GenContext {
    public System.Random rng;
    public IChordVoicer ChordVoicer;
    public VoiceLeadingConfig chordVoicingPreset;
    public MIDIInstrumentSO DefaultMelodicInstrument;

    public System.Func<SongConfig.PartConfig, TrackRole, MidiFile> GetTrackForRole;
    public System.Func<MidiFile, List<Melanchall.DryWetMidi.Interaction.Note>> ExtractMonophonicNotes;
    public System.Func<ChordProgressionData, TempoMap, MusicTheory.MusicTheory.TimeSignature, long, ChordProgressionData.ChordEvent> FindChordEventAt;
    public System.Func<SongConfig.PartConfig, ChordProgressionData> GetProgressionForPart;
}
```

---

### Credits & dependencies
- **DryWetMIDI** (Melanchall): Patterns, TempoMap, TrackChunk/TimedEvents, MIDI writing.
- **Unity**: ScriptableObjects for configuration/strategies; MonoBehaviours in UI layer.

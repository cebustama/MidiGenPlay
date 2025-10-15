
# Deep Research Prompt — Refactor & Integrate **MidiMusicManager** (ALWTTT ⇄ MidiGenPlay)

_Last updated: 2025‑10‑14_

## 0) What this research is about
We need a **runtime‑ready, SOLID** refactor and integration plan for **MidiMusicManager** (the ALWTTT ⇄ MidiGenPlay interface), so that:
- **Musicians** in the band play **Tracks** with distinct **personalities** (strategies, configs, instruments, preferred tonalities, mistake profiles).
- The **deckbuilder card system** can **modify songs** flexibly at runtime (intros, solos, singalong, tempo changes, alternate tracks, audible mistakes, humanization, highlighting/mixing, etc.).

Assume the composition stack has been refactored with **SongOrchestrator** + per‑role **ITrackComposer** modules and pluggable strategies (melody/harmony/voicing).

---

## 1) Baseline — how it currently works (summarize)
- Playback pipeline: `SongData` → `SongConfig` (band lineup & patterns) → `MidiGenerator.GenerateSong` → `MidiMusicManager` (cache, play/stop, channel mask/volumes, metronome, markers, beat & chord listeners).
- Orchestration: **SongOrchestrator** stamps tempo/time‑signature & markers, runs two passes (Harmony after Melody), merges tracks, adds “All Sound Off”, optional metronome.
- UI path (already SOLID): editor/runtime panel that uses the same generator stack and services.

Deliver a concise description of the **data flow**, **events**, **timing/markers**, **channel ownership tagging**, and **caching** in `MidiMusicManager` today.

---

## 2) Integration goals (what we want next)

### 2.1 Musician “personality”
Represent per‑musician **composition strategies** (melody/harmony/bass/rhythm), **voicing configs**, **preferred tonalities & ranges**, **instrument prefs**, and a **mistake profile** (frequency, severity, type). Cleanly inject these into composers via `GenContext` or per‑track params.

### 2.2 Cards (deckbuilder) — song mutations & post‑processing
Cards must apply **non‑destructive, stackable** modifications to the current or next song:

- **Intro**: prepend a new Part for any instrument.
- **Solo**: add a new Part where **all tracks stay the same** except the soloist, which is **re‑composed** (styles: *emotional*, *facemelting*, *virtuoso/shredding*, control over notes/beat density, range, aggression).
- **Mistakes**: audible mapping of game logic—melodic instruments occasionally play ±1 semitone “wrong notes”; rhythm instruments go slightly off‑tempo/on wrong subdivisions.
- **Singalong**: musician improvises a short phrase; **audience** repeats it 1–2 times using a different **soundfont/patch** (call‑and‑response, Wembley vibe).
- **Solo piece**: complete standalone feature Part (e.g., Bonham/Hendrix) with others muted.
- **Tempo change**: double/halve (or otherwise scale) BPM of the next song (or section).
- **Alternate track**: re‑compose one musician’s track; others unchanged.
- **Mixing**: precise per‑track volume; ability to “highlight” a musician (duck others or accentuate via velocity/patch/EQ proxy).
- **Humanization**: slight timing offsets, note length variance, and velocities per performance (seeded, deterministic).

### 2.3 Architecture shape
- Keep **SongOrchestrator** as timeline owner.
- Introduce an **Arrangement mutation layer** before orchestration and a **MIDI post‑processing layer** after composition:
  - `IArrangementMutator` (edits `SongConfig`/Arrangement: insert parts, replace one track, tempo scale).
  - `ITrackMutation` (localized per‑track changes: solo, fills, alternates).
  - `IMidiPostProcessor` (humanize, mistakes, audience duplication/patch swaps, highlight/mix).
  - `IMusicianPersonality` (strategies + preferences + mistake profile).
  - `IMixController` (channel volumes, ducking/highlight).

Refactor **MidiMusicManager** into a thin **director/facade** that queues mutators (from cards & personality), invokes orchestration, then runs post‑processors and handles playback/mixing.

### 2.4 Runtime API (proposal to evaluate)
- **Arrangement & cards**
  - `ApplyCards(IEnumerable<CardData> cards)` → builds a mutation pipeline
  - `InsertPart(PartSpec spec)` / `AddIntro(InstrumentRef, measures, style)`
  - `AppendSoloPart(MusicianId id, SoloStyle style, int measures)`
  - `ReplaceTrack(int partIdx, MusicianId id, StrategyOverride? strategy)`
  - `SetTempoScale(float factor)` / `ScheduleNextSongTempoScale(float factor)`
- **Playback & mix**
  - `Play(SongData data | SongConfig cfg)` / `Stop()`
  - `SetChannelVolume(int channel, float linear0to1)`
  - `Highlight(MusicianId id, HighlightMode mode)`
  - `EnableHumanization(HumanizeOptions opts)`
  - `EnableMistakes(MusicianId id, MistakeProfile profile, Scope scope)`

- Determinism: seed RNG per song/part (+ per mutator/post‑processor).

---

## 3) Deliverables (what to produce)
1. **Current state summary** of `MidiMusicManager`: responsibilities, event/data flow, timing/markers, channel ownership, caching.
2. **Refactor plan** for `MidiMusicManager`:
   - Components & interfaces (diagram or structured text).
   - Event/data flow for normal playback and for card‑driven mutations.
3. **Card → music mapping** catalog for every card type above:
   - When it applies (pre‑ or post‑orchestration), how it transforms Arrangement or MIDI, which strategies/configs it touches.
4. **Public API** proposal for `MidiMusicManager` to support game code and the card system (signatures & brief semantics).
5. **Mutation pipeline examples** (pseudo/real C#): Add Intro, Append Solo, Singalong, Mistakes (melodic & rhythm), Alternate Track, Tempo Scale, Highlight.
6. **Post‑processing passes**: humanization, mistake injection, audience duplication, mix/highlight.
7. **Testing plan**: golden‑master MIDI diffs for each mutation; property tests for determinism; basic perf targets.
8. **Integration notes**: trigger points inside ALWTTT (card resolution), mapping `CardData` to effects, routing **MusicianPersonality** into composers, maintaining existing beat/chord/part listeners.

---

## 4) Codebase to read (attached)
**Please review these 10 files for optimal understanding:**
1. `MidiMusicManager.cs` — ALWTTT ⇄ MidiGenPlay interface & playback hub.
2. `MidiGenerator.cs` — facade/registry entry point for generation.
3. `SongOrchestrator.cs` — timeline owner (tempo/TS/markers, merge, passes).
4. `SongConfig.cs` — song data model used by generation.
5. `SongData.cs` — builds `SongConfig` from game data/band lineup.
6. `CardData.cs` — card structures & effect descriptors.
7. `ChordProgressionData.cs` — harmony pattern source.
8. `DrumPatternData.cs` — rhythm pattern source.
9. `VoiceLeadingConfig.cs` — chord voicing preset (for IChordVoicer).
10. `HarmonicLeadingConfig.cs` — harmony rule preset.

> If available, the following will further improve the research: `MelodicLeadingConfig.cs`, `MIDIInstrumentSO.cs`, `MIDIPercussionInstrumentSO.cs`, `ITrackComposer.cs`, `IMelodyStrategy.cs`, `IHarmonyStrategy.cs`, `IChordVoicer.cs`, `NearestChordToneMelodyStrategy.cs`, `NearestDifferentChordToneHarmonyStrategy.cs`.

---

## 5) Constraints & preferences
- Runtime‑safe (no editor‑only code in game path).
- SOLID separation: **arrangement mutators**, **composers**, **orchestrator**, **post‑processors**, **mix**.
- Deterministic by seed; allow per‑card/per‑musician seeds.
- Keep `MidiMusicManager` slim; push heavy logic into injectible services.
- Work with existing beat/chord/part listeners & channel masking/volume controls.

---

## 6) Acceptance criteria
- From the same base song, we can apply cards to:
  - Prepend intro; append solo part (only soloist recomposed);
  - Generate singalong (call→audience x2 with different soundfont);
  - Inject controlled mistakes; compose an alternate track for one musician;
  - Scale tempo; highlight a musician; adjust per‑channel volumes;
  - Enable humanization (timing/length/velocity jitter) deterministically.
- All changes are audible and still compatible with existing listeners.
- **No changes** to `SongOrchestrator` are required for new cards beyond registering mutators/post‑processors.

---

### Notes for researcher
Focus on **how** to structure `MidiMusicManager` as a **director** that orchestrates:
- pre‑orchestration **Arrangement mutations**,
- orchestration via **SongOrchestrator**,
- post‑orchestration **MIDI post‑processing**,
- and **playback/mix** control—while remaining testable, composable, and deterministic.

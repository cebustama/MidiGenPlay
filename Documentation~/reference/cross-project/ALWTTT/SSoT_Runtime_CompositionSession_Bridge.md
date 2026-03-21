> CROSS-PROJECT REFERENCE ONLY — preserved for consumer-project context.
> DO NOT UPDATE THIS FILE AS MIDI GEN PLAY PACKAGE AUTHORITY.
> Primary game-owned authority lives in: `ALWTTT Docs/runtime/SSoT_Runtime_CompositionSession_Integration.md`.

# SSoT — Runtime Live Composition Bridge (ALWTTT × MidiGenPlay)

**Status:** draft (implementation-aligned)  
**Generated:** 2026-03-04  
**Scope:** Runtime bridge for **live composition**: playing **Composition Cards** mutates the **SongCompositionUI model**, which is built into **SongConfig**, rendered to MIDI, cached, and looped.

> Authoring model (cards + bundles + musical modifiers vs gameplay effects) lives in:
> `SSoT_CompositionCards_TrackStyleBundles.md`

---

## 0) The key separation at runtime (MUST HOLD)

When a Composition card is played, two independent pipelines run:

### A) Musical pipeline (composition model)
- Source: `CompositionCardPayload.modifierEffects : List<PartEffect>`
- Executor: `SongCompositionUI.ApplyEffectToModel(...)`
- Output: mutations to `SongModel/PartEntry/TrackEntry`
- Result: affects the next `SongConfig` build and the generated MIDI

### B) Gameplay pipeline (combat/economy/status)
- Source: `CardPayload.effects : List<CardEffectSpec>`
- Executor (in this session): `CompositionSession.ApplyStatusActionsFromCard(...)`
- Output: statuses / gameplay state changes
- Result: does **not** change SongConfig directly

---

## 1) Musical modifiers: concrete semantics (code-backed)

**Files:**
- `PartEffect.cs` (+ `ApplyTiming`, `EffectScope`)
- `TempoEffect.cs`
- `MeterEffect.cs`
- `TonalityEffect.cs`
- `ModulationEffect.cs`
- `InstrumentEffect.cs`
- `SongCompositionUI.cs`

### 1.1 Timing resolution (current implementation)
In `SongCompositionUI.ApplyEffectToModel(...)`:

- `Immediate` → apply to `partIndex`
- `OnNextLoop` → apply to `partIndex` *(model changes now; audible depends on render/cache invalidation)*
- `OnNextPartStart` → apply to `partIndex + 1` *(auto-ensures part exists)*

> Note: timing is not directly used to decide cache invalidation targets yet; invalidation happens at card-level in `CompositionSession`.

### 1.2 Scope resolution (current implementation)
- `InstrumentEffect` currently **only supports** `scope == TrackOnly` and requires a musician target.
- Other effects do not branch on `scope` in current code; they are effectively treated as “part-level” changes.

### 1.3 Effect-by-effect mapping (what actually gets written)

#### TempoEffect
- Mode `Range`:
  - `PartEntry.tempoRangeOverride = fx.tempoRange`
  - `PartEntry.absoluteBpmOverride = null`
  - `PartEntry.tempo = fx.tempoRange.ToString()`
- Mode `AbsoluteBpm`:
  - `PartEntry.absoluteBpmOverride = fx.absoluteBpm`
  - `PartEntry.tempoRangeOverride = TempoRange.Fast`
  - `PartEntry.tempo = "<bpm> BPM"`
- Mode `ScaleFactor`:
  - `PartEntry.tempoScale *= fx.tempoScale`
  - `PartEntry.tempo = "×<scale>"`

**Known issue:** In the tempo branch, UI binding uses `partUIs[partIndex]` even if timing resolved to `idx = partIndex + 1`. It likely should bind `partUIs[idx]`.

#### MeterEffect
- `PartEntry.timeSignature = fx.timeSignature`

#### TonalityEffect
- Sets `PartEntry.tonality` based on mode:
  - `Explicit` uses `fx.tonality`
  - random modes call `GetRandomAnyTonality`, `GetRandomMajorishTonality`, `GetRandomMinorishTonality`

#### ModulationEffect
- Writes:
  - `PartEntry.rootNote = newRoot`
  - `PartEntry.hasExplicitRootNote = true`
- Modes:
  - `AbsoluteKey`: direct set from `fx.absoluteRoot`
  - `IntervalWithinScale`: chooses note from scale at `fx.targetDegree`
  - `RandomAny`: random `NoteName`
  - `RandomWithinScale`: random note inside current scale (optionally excluding tonic)

#### InstrumentEffect (TrackOnly)
- Finds the target musician track in the part:
  - `TrackEntry.musicianId == target.CharacterId`
- Clears previous overrides, then sets one of:
  - `TrackEntry.overrideMelodicInstrument`
  - `TrackEntry.overridePercussionInstrument`
  - `TrackEntry.hasOverrideInstrumentType=true` + `overrideInstrumentType`

#### DensityEffect / FeelEffect
- Present as assets but not handled in current `ApplyEffectToModel` → no mutation yet.

---

## 2) Card-level cache invalidation (how musical changes become audible)

**Files:**
- `CompositionSession.cs`
- `CompositionCardClassifier.cs`

After applying the card to the model:
- `affectsSound = CompositionCardClassifier.AffectsSound(compPayload)`
  - true if **any modifierEffects exist**
  - OR Track-primary
  - OR certain part actions (intro/bridge/solo/outro/final)

If loop is running and affectsSound:
- invalidate cache for:
  - NextPart zone → draft part index (`ui.Model.CurrentPartIndex`)
  - CurrentPart zone → `_currentPartIndex`
- choose preserve flags:
  - keepTempo = false only for `TempoEffect` cards
  - keepInstruments = false for instrument cards (currently includes all Track-primary cards)

**Open design question:** invalidation currently does not look at `ApplyTiming` or `EffectScope`. If a card’s effects are `OnNextPartStart`, you may want to invalidate `partIndex+1`, not the currently playing part.

---

## 3) Repositories & selection rules (now closed)

This section makes the “instrument/pattern selection” part fully explicit.

### 3.1 Instrument repository (Resources + package)
**Files:** `IInstrumentRepository.cs`, `InstrumentRepositoryResources.cs`

- The repository loads **MIDIInstrumentSO** assets from **two Resources roots**:
  - `cfg.PackageInstrumentsPath` (package)
  - `cfg.resourcesInstrumentsPath` (local project root)
- It de-duplicates loaded objects and then partitions:
  - **Percussion**: `MIDIPercussionInstrumentSO`
  - **Melodic**: all other `MIDIInstrumentSO`

**Implication:** content can ship default instruments in the package, while allowing project-local overrides/additions under a configurable Resources root.

### 3.2 Pattern repository (Drums/Chords/Melodies)
**Files:** `IPatternRepository.cs`, `PatternRepositoryResources.cs`

- Loads patterns from package constants:
  - Drums: `ScriptableObjects/Patterns/Drums`
  - Chords: `ScriptableObjects/Patterns/Chords`
  - Melodies: `ScriptableObjects/Patterns/Melodies`
- And from project-local roots configured in `MidiGenPlayConfig`:
  - `cfg.ResourcesDrumsPath`
  - `cfg.ResourcesChordsPath`
  - `cfg.ResourcesMelodiesPath`
- Provides both:
  - **unfiltered** pools (GetAll*)
  - **filtered** pools by `TimeSignature` (Get*Patterns(TimeSignature ts))
- Provides canonical local write folder for chord assets:
  - `GetChordWriteFolder()` → `cfg.GetChordWriteFolder()`

**Implication:** authoring tools (like chord progression authoring) should treat `GetChordWriteFolder()` as the canonical “save here” root for locally-authored chord progressions.

### 3.3 Musician-permitted melodic instruments (filtering)
**File:** `InstrumentRules.cs`

`InstrumentRules.GetPermittedMelodic(musician, role, repo)` filters by the musician profile:
- If musician/profile is missing → return all melodic instruments.
- Chooses **primary** and **secondary** instrument-type lists depending on track role:
  - Backing/Bassline: primary = `profile.backingInstruments`, secondary = `profile.leadInstruments`
  - Melody/Harmony: primary = `profile.leadInstruments`, secondary = `profile.backingInstruments`
- Filtering rule:
  - if primary yields none → try secondary
  - if still none → fall back to all melodic instruments

A debug helper exists:
- `GetPermittedMelodicAllRoles(musician, repo)` returns union across roles (Backing/Bassline/Melody/Harmony).

---

## 4) UI model → SongConfig mapping (closed)

### 4.1 SongConfigBuilder selection order (in detail)
**Files:** `CompositionSession.cs`, `SongConfigBuilder.cs`, `InstrumentRules.cs`

`CompositionSession.BuildSongConfigFromUI()` constructs fresh repos and passes a delegate:
- `getPermittedMelodic: (mus, role) => InstrumentRules.GetPermittedMelodic(mus, role, instruments)`

Inside `SongConfigBuilder.FromUI(...)`, per track:

**Base pick (role-dependent):**
- `Rhythm`:
  - `percInst = instruments.GetPercussionInstruments().OrderBy(rng).FirstOrDefault()`
  - creates a minimal `RhythmRecipe` (HatDensity = From_Style, HatMode = Fixed)
- `Backing`:
  - `candidates = getPermittedMelodic(musician, Backing)` (or all melodic)
  - `melInst = random(candidates)`
  - creates an empty `BackingRecipe`
- `Bassline / Melody / Harmony`:
  - `melInst = random(getPermittedMelodic(...))`

**Then apply overrides in this order (wins later):**
1) Explicit override melodic instrument (`TrackEntry.overrideMelodicInstrument`)
2) Explicit override percussion instrument (`TrackEntry.overridePercussionInstrument`)
3) Override by instrument type (`TrackEntry.hasOverrideInstrumentType`):
   - chooses a random melodic instrument with that `InstrumentType`
   - **TODO in code:** does not consider musician allowed instruments yet
4) Pinned instrument override from part cache:
   - if `ctx.TryGetPartCache(partIndex, out partCache)` and `partCache.resolvedMelInstByMusician[musicianId]` exists → it wins

**TrackParameters:**
- `Style = trModel.styleBundle` (this is the *card-authored* bundle)
- `RhythmRecipe = recipe` (only for Rhythm role right now)
- Legacy fallbacks are still filled (strategy ids + leading overrides from persistent gameplay data)

### 4.2 About patterns in SongConfigBuilder
`SongConfigBuilder` refreshes the pattern repository but does not currently assign:
- `TrackParameters.Pattern`
- chord/melody pattern selection
This implies that pattern choice happens later (or inside orchestrator/generator) based on:
- `TrackParameters.Style`
- `TrackParameters.RhythmRecipe / BackingRecipe`
- or default repositories.

---

## 5) Rendering + playback + caching (recap)
(unchanged, but now connected to repositories)

**File:** `MidiMusicManager.cs`

- `MidiMusicManager` also owns its own repositories:
  - `instrumentRepo = new InstrumentRepositoryResources(settings)`
  - `patternRepo = new PatternRepositoryResources(settings)`
- On first use, `EnsureRegistriesLoaded()` refreshes repos and stores read-only pools:
  - `MelodicInstruments`, `PercussionInstruments`
  - `DrumPatterns`, `ChordPatterns`, `MelodyPatterns`

Render for jam:
- `RenderSinglePart(fullCfg, partIndex, bpmOverride, instrumentOverrides)`:
  - stamps channels based on `SongConfig.ChannelRoles + ChannelMusicianOrder`
  - resolves BPM (explicit/range + scale)
  - delegates generation to `generator.Orchestrator.GenerateSinglePart(...)`
  - returns merged bytes + stems + duration + resolved bpm + pinned melodic instruments

---

## 6) Remaining missing attachments (optional)
To close “generation internals” (how Style/Recipes turn into MIDI notes):
- `MidiGenerator` + `SongOrchestrator` + track composer classes
- Any strategy registries used by `HarmonyStrategyId`, `MelodyStrategyId`

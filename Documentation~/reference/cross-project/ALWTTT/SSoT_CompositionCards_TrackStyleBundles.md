> CROSS-PROJECT REFERENCE ONLY — preserved for consumer-project context.
> DO NOT UPDATE THIS FILE AS MIDI GEN PLAY PACKAGE AUTHORITY.
> Primary game-owned authority lives in: `ALWTTT Docs/systems/SSoT_Card_System.md + Docs/runtime/SSoT_Runtime_CompositionSession_Integration.md`.

# SSoT — Composition Cards & TrackStyleBundles (ALWTTT × MidiGenPlay)

**Status:** draft (implementation-aligned)  
**Generated:** 2026-03-07  
**Scope:** Defines the **authoring model** for **Composition Cards** in ALWTTT and their role-specific **TrackStyleBundles** in MidiGenPlay.

> **Key separation (do not mix):**
> - **Musical modifier effects** = `CompositionCardPayload.modifierEffects : List<PartEffect>` (changes the composition model)
> - **Gameplay effects** = `CardPayload.effects : List<CardEffectSpec>` (combat/status/economy actions when the card is played)

Runtime execution details (session loop, cache invalidation, render/playback) live in:  
**`SSoT_Runtime_CompositionSession_Bridge.md`**

---

## 0) Glossary (minimal)

- **Composition card**: a `CardDefinition` whose `payload` is `CompositionCardPayload`.
- **Track card**: `CompositionCardPayload.primaryKind == Track` → modifies tracks for a part (role + bundle).
- **Part card**: `primaryKind == Part` → structural action on parts (create/mark intro/solo/etc.).
- **Musical modifier**: a `PartEffect` asset inside `modifierEffects` that changes tempo/meter/tonality/etc.
- **Gameplay effect**: a `CardEffectSpec` entry inside `effects` that changes gameplay state (draw, statuses, etc.).
- **TrackStyleBundleSO**: role-specific authored parameters for MidiGenPlay generation.

---

## 1) CompositionCardPayload — authoritative authoring surface

**Files:**  
- `CompositionCardPayload.cs`  
- `TrackActionDescriptor.cs`  
- `PartActionDescriptor.cs` / `PartActionKind.cs`  
- `PartEffect.cs` (+ subclasses)  
- `CardPayload.cs` (effects list)

### 1.1 Fields (what you author)

- `primaryKind : CardPrimaryKind`
- `trackAction : TrackActionDescriptor`
  - `role : TrackRole`
  - `styleBundle : TrackStyleBundleSO` (optional but expected for content)
- `partAction : PartActionDescriptor`
  - `action : PartActionKind` (CreatePart, MarkIntro, MarkSolo, …)
  - `customLabel : string`
  - `musicianId : string` (solo tie-in)
- `modifierEffects : List<PartEffect>`  ✅ **MUSICAL**
- inherited `effects : List<CardEffectSpec>` ✅ **GAMEPLAY**

### 1.2 Authoritative meaning (normative)

#### A) Musical modifier effects (`modifierEffects`)
- **Purpose:** mutate the **composition model** (SongModel / PartEntry / TrackEntry) so that the next render produces different music.
- **Type:** `PartEffect : ScriptableObject` and subclasses (`TempoEffect`, `MeterEffect`, etc.).
- **Execution:** by `SongCompositionUI.ApplyEffectToModel(...)` at runtime.

#### B) Gameplay effects (`effects`)
- **Purpose:** execute **gameplay actions** when the card is played.
- **Type:** `CardEffectSpec` polymorphic entries (SerializeReference list).
- **Execution:** by the gameplay/session host (`CompositionSession.ApplyStatusActionsFromCard(...)` currently handles `ApplyStatusEffectSpec`).

**Rule:** Musical modifiers must never be encoded as gameplay effects, and gameplay effects must never be interpreted as musical changes.

---

## 2) Part actions (structure layer)

### 2.1 PartActionKind
**File:** `PartActionKind.cs`

- `CreatePart`
- `MarkIntro`
- `MarkSolo`
- `MarkOutro`
- `MarkBridge`
- `MarkFinal`
- `Custom`

### 2.2 PartActionDescriptor
**File:** `PartActionDescriptor.cs`

- `action`
- `customLabel` (optional label for created/marked part)
- `musicianId` (optional target when marking solo)

**Normative intent:**
- Part actions change **structure semantics** (labels/markers and “final-ness”) and may drive UI/feedback and sound invalidation.

---

## 3) Musical modifier effects (PartEffect)

### 3.1 Base class + enums
**File:** `PartEffect.cs`

- `EffectScope`
  - `TrackOnly`
  - `CurrentPart`
  - `NextPart`
  - `WholeSong`
- `ApplyTiming`
  - `Immediate`
  - `OnNextLoop`
  - `OnNextPartStart`

Defaults:
- `scope = CurrentPart`
- `timing = OnNextLoop`

Each effect must implement:
- `GetLabel()` short UI label for cards/inspector.

### 3.2 Implemented effect assets (currently in project)

| Effect Type | What it means (authoring intent) |
|---|---|
| `TempoEffect` | tempo policy: range / absolute bpm / scale factor |
| `MeterEffect` | time signature override |
| `TonalityEffect` | mode selection: explicit or random families |
| `ModulationEffect` | change root note (key center) by absolute or scale logic |
| `InstrumentEffect` | instrument override/bias for a specific musician/track |
| `DensityEffect` | generic density/sparsity control (currently not consumed by runtime) |
| `FeelEffect` | generic feel/swing/laidback control (currently not consumed by runtime) |

> **Important:** `DensityEffect` and `FeelEffect` exist as assets but (in current runtime code) are not handled in `ApplyEffectToModel`, so they do not change the model yet.

---

## 4) Track roles + TrackStyleBundles (canonical)

This section merges the **type taxonomy** (what each TrackRole means in ALWTTT) with the **authoring bundle surface**
(the `TrackStyleBundleSO` subclasses that drive MidiGenPlay generation).

**Rule:** this doc defines *what assets exist and what they mean*.
The exact **composer precedence rules** and rendering internals live in the per-role composer SSoT docs.

### 4.1 Role map (at a glance)

| TrackRole | Musical meaning (in a part) | Primary authored assets | Runtime generator |
|---|---|---|---|
| **Backing** | harmonic support: chords / comping | `BackingCardConfigSO`, `ChordProgressionData` / `ChordProgressionPaletteSO`, `VoiceLeadingConfig` | `ChordTrackComposer` → see `SSoT_Composer_BackingChordTrack_v2.md` |
| **Rhythm** | drum kit groove / hits | `RhythmCardConfigSO`, `DrumPatternData` (+ optional `RhythmRecipe`) | `RhythmTrackComposer` (composer SSoT pending) |
| **Bassline** | bass pattern supporting harmony | (TBD) (likely pattern + strategy bundle) | `BassTrackComposerFactory` (composer SSoT pending) |
| **Melody / Lead** | melodic line / lead instrument | (TBD) pattern + melodic strategy/leading | `MelodyTrackComposerFactory` (composer SSoT pending) |
| **Harmony** | additional melodic support (counterlines / pads / chord tones) | (TBD) pattern + harmonic strategy/leading | `HarmonyTrackComposerFactory` (composer SSoT pending) |

> Note: `TrackRole.Lead` currently reuses Melody behavior at generator level.

---

### 4.2 TrackStyleBundleSO (base class)

**`TrackStyleBundleSO : ScriptableObject`**
- `appliesTo : TrackRole` — a declarative “intended role” tag for inspector sanity.
- Concrete bundles derive from this:
  - `BackingCardConfigSO`
  - `RhythmCardConfigSO`
  - (future) `BasslineCardConfigSO`, `MelodyCardConfigSO`, `HarmonyCardConfigSO`

---

### 4.3 Backing bundle (implemented & production-ready)

**`BackingCardConfigSO : TrackStyleBundleSO`** *(MidiGenPlay/TrackConfigs/BackingCardConfig)*
- `voiceLeadingOverride : VoiceLeadingConfig` *(optional)*
- `progressionOverride : ChordProgressionData` *(optional)*
- `progressionPalette : ChordProgressionPaletteSO` *(optional)*

**Meaning**
- If `progressionOverride` is set, it is the strongest authored harmonic override and wins before palette/library/procedural resolution.
- If `progressionOverride` is null and `progressionPalette` is set, the Backing composer should resolve a progression from the palette using the TS-aware picker defined in the Backing composer SSoT.
- If neither override is provided, the generator may fall back to cached/library/procedural progression selection.
- Any resolved progression must be cloned for runtime use; project assets must never be mutated in place.

**Selection semantics (authoring-facing)**
- `progressionOverride` = direct single authored progression.
- `progressionPalette` = authored pool of candidate progressions.
  - TS-aware picker uses **Tier A / Tier B / Tier C** semantics:
    - **Tier A**: exact `Part.TimeSignature` match (optional; can be disabled per palette)
    - **Tier B**: ranked fallback heuristic if no exact match is used
    - **Tier C**: raw palette weighted pick only if TS-aware candidate scoring cannot produce a result
- Runtime normalization still happens after selection; palette selection chooses the **best source progression**, not necessarily the final rendered grid.

**Implementation note (important for current behavior)**
- In the current TS-aware path, palette entries are introspected and sanitized before Tier scoring.
- This means a candidate can still participate in Tier A / Tier B even if its authored palette weight is `0`, because the TS-aware selector treats extracted candidates as valid authored options and uses weights as soft roulette bias rather than as a strict enabled/disabled gate.
- If you need to force fallback behavior for testing, use the palette-level `preferExactTsMatches` toggle instead of relying on weight `0` to suppress an exact TS candidate.

**Where it is consumed**
- Composer pipeline details: `SSoT_Composer_BackingChordTrack_v2.md`

#### 4.3.1 `ChordProgressionPaletteSO` (authoring surface)

**`ChordProgressionPaletteSO : ScriptableObject`**
- `paletteDisplayName : string` *(optional human label; asset name is fallback)*
- `paletteNotes : string` *(optional authoring notes)*
- `preferExactTsMatches : bool = true`
- `entries : List<WeightedEntry>`
  - `WeightedEntry.progression : ChordProgressionData`
  - `WeightedEntry.weight : float`

**Normative meaning**
- A palette is an authored pool of harmonic candidates intended to be reused by multiple Backing cards.
- `preferExactTsMatches = true` means the TS-aware picker should try Tier A exact TS first.
- `preferExactTsMatches = false` means the TS-aware picker should intentionally skip Tier A and begin from Tier B fallback scoring. This exists primarily to improve testing/validation and to let authors force adaptation scenarios.
- The palette's native `PickRandomProgression(...)` remains the legacy weighted picker; the TS-aware card path may use the palette data differently from the legacy picker.

---

### 4.4 Rhythm bundle (implemented, but MVP wiring incomplete)

**`RhythmCardConfigSO : TrackStyleBundleSO`** *(MidiGenPlay/TrackConfigs/RhythmCardConfig)*
- `patternOverride : DrumPatternData` *(optional)*
- `recipeOverride : RhythmRecipe` *(optional)*
- `styleIdOverride : string` *(optional)*

Additional hooks currently present on the bundle (but not yet wired into generation everywhere):
- phrasing: `fillEveryNMeasures`, `lastMeasuresAsFill`
- feel: `kickDensity`, `snareGhostNoteChance`, `hatSubdivisionBias`

**Meaning**
- If `patternOverride` exists, the Rhythm composer should render the explicit pattern (grid or legacy).
- If no pattern exists, the Rhythm composer may choose a procedural style using recipe + styleId overrides.

**Where it is consumed**
- Current implementation: `RhythmTrackComposer` (composer SSoT pending)

---

### 4.5 Placeholders (Bassline / Melody / Harmony)

These roles exist in card taxonomy but their authoring surface is still evolving.
When they become “real”, they should follow the same structure:
- a role-specific `TrackStyleBundleSO` subclass
- optional `PatternDataSO` asset(s)
- a dedicated composer SSoT doc describing precedence + rendering

Do **not** document composer internals in this file—keep it strictly type/bundle-level.



## 5) Authoring UX in CardEditorWindow

### 5.1 What exists today
CardEditorWindow shows:
- Track Action (role + styleBundle)
- Part Action (action + customLabel + musicianId)
- Modifier Effects (list of PartEffect assets)
- Effects (New) (list of CardEffectSpec gameplay effects)

### 5.2 Recommended labeling (to reduce confusion)
In UI and docs, use these names consistently:
- **Modifier Effects (Musical)** = `CompositionCardPayload.modifierEffects`
- **Gameplay Effects** = `CardPayload.effects`

> This matches your intent: “modifier effects are musical; effects are gameplay”.

---

## 6) Missing attachments for authoring SSoT completeness
(Only if you want the authoring doc to also define the full gameplay effects catalog)
- `CardEffectSpec` base + concrete gameplay effect specs (Draw, Discard, ApplyStatus, etc.)
- Any “CardEffect execution engine” used in gig combat

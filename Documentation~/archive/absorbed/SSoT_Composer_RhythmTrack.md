> ABSORBED SOURCE — preserved during governance migration. Use current SSoTs in `runtime/` and `authoring/` as the active authorities.

# SSoT — Composer Pipeline: Rhythm (Drum Track) — RhythmTrackComposer (ALWTTT × MidiGenPlay)

**Status:** draft (implementation-aligned)  
**Generated:** 2026-03-05  
**Scope:** End-to-end **musical** pipeline for generating a **Rhythm / Drum** track (MIDI) using **`RhythmTrackComposer`**, from **Composition card authoring assets** through **SongConfig → Orchestrator → MIDI bytes**.

> This doc covers the **Rhythm track musical generation** only.  
> It does **not** re-document gameplay effects or UI session-loop logic beyond what’s needed to explain how the composer is fed.

---

## 0) Key invariants (assumed contracts)

1) **Meter & measures are Part-owned.**  
   `RhythmTrackComposer` does **not** choose time signature or measures; it consumes:
   - `part.TimeSignature` (typically set via `MeterEffect` → `SongCompositionUI.PartEntry.timeSignature`)
   - `part.Measures` (from the PartEntry; defaulted by builder if missing)

2) **Option A is non-negotiable.**  
   Runtime must respect `RhythmCardConfigSO` as the primary authoring surface:
   - `patternOverride` (DrumPatternData) has priority
   - `recipeOverride` and `styleIdOverride` influence procedural selection
   - other feel/phrasing fields are present (some may still be TODO depending on branch)

3) **Deterministic generation is required.**  
   Any randomness used for style selection must be driven by **`ctx.rng`** (seeded by `SongOrchestrator`), not `UnityEngine.Random`.

4) **Beat-unit aware timing is required (6/8 must not “stretch”).**  
   Any beat-based scheduling must respect the Part time signature’s beat unit (Quarter vs Eighth, etc.).  
   Practical implication: composer + orchestrator must agree on tick math, otherwise you get “half-loop silence”.

---

## 1) Inputs (what the composer depends on)

### 1.1 From the live composition model → SongConfig
Runtime bridge (simplified):

**Card payload** (`TrackActionDescriptor.styleBundle`) → **UI model** (`SongCompositionUI.TrackEntry.styleBundle`) →  
**SongConfigBuilder** sets:

- `SongConfig.PartConfig.TimeSignature = PartEntry.timeSignature`
- `SongConfig.PartConfig.Measures = PartEntry.measures` (fallback to default if <= 0)
- `SongConfig.PartConfig.TrackConfig.Parameters.Style = TrackEntry.styleBundle`
- `SongConfig.PartConfig.TrackConfig.PercussionInstrument` (resolved kit)
- `SongConfig.PartConfig.TrackConfig.Parameters.RhythmRecipe` (may be set, but card override wins)

### 1.2 Rhythm authoring assets

#### RhythmCardConfigSO (TrackStyleBundleSO)
Primary bundle attached to a Rhythm Composition Card (Option A).

Fields:
- **Pattern / Style Selection**
  - `patternOverride : DrumPatternData`  
    If set, composer uses this pattern (grid or legacy) and **does not** select a procedural style.
  - `recipeOverride : RhythmRecipe`  
    Higher-level “how to play” hints for procedural styles (density/feel/fills). (Current recipe is minimal.)
  - `styleIdOverride : string`  
    Forces a specific rhythm style **within the current meter** (e.g. `shuffle_6_8_default` for 6/8).

- **Phrasing / Feel (MVP hooks)**
  - `fillEveryNMeasures`, `lastMeasuresAsFill`
  - `kickDensity`, `snareGhostNoteChance`, `hatSubdivisionBias`  
  (These exist as authoring intent; whether they are applied depends on the current implementation phase.)

#### DrumPatternData (PatternDataSO)
Authored drum patterns.

- Grid signature:
  - `beatsPerMeasure` (authoring-side; may be validated against Part meter)
  - `subdivisions` = steps per beat
  - `Measures` (pattern length in measures)
- Lanes:
  - `instrument : GeneralMidiPercussion`
  - `defaultVelocity`
  - `steps : List<bool>` of length `Measures * beatsPerMeasure * subdivisions`
- Helpers:
  - `EnsureSizes()`, `SetSignature(...)`, `SnapshotAsIndices()`, `DeepCloneRuntime()`
- Legacy (optional):
  - `pianoRollPattern`, `drumMappings`, plus note length/velocity defaults

### 1.3 Generation services / runtime stack
- `SongOrchestrator` calls the composer via role factories:
  - `TrackRole.Rhythm` → `RhythmTrackComposerFactory` → `RhythmTrackComposer`
- `MidiMusicManager` (or runtime equivalent) ultimately serializes `MidiFile` → bytes.
- Determinism:
  - `SongOrchestrator` seeds `ctx.rng` from `settings.defaultSeed` (per part/repetition).

---

## 2) End-to-end flow (authoring → MIDI bytes)

### 2.1 Authoring (design-time)
- Designers create:
  - `RhythmCardConfigSO` (style bundle) for a Rhythm card
  - optionally `DrumPatternData` (patternOverride)
  - optionally a `MeterEffect` card to switch the Part to 6/8, 3/4, etc.
- Designers may also rely on procedural styles (no patternOverride).

### 2.2 Runtime bridge (card → SongConfig)
- Card is played; UI composition model updates the current Part:
  - sets/keeps `PartEntry.timeSignature` (via MeterEffect)
  - sets/keeps `PartEntry.measures`
  - assigns `TrackEntry.styleBundle = RhythmCardConfigSO`
- `SongConfigBuilder.FromUI(...)` generates `SongConfig`, copying Part meter + measures.

### 2.3 Composer consumption point (where authored assets become sound)
`RhythmTrackComposer.Compose(part, cfg, bpm, channel, ctx)`:

1) Resolve kit:
- `kit = cfg.PercussionInstrument` (MIDIPercussionInstrumentSO)

2) Resolve authoring bundle (Option A):
- `styleBundle = cfg.Parameters.Style`
- `cardCfg = styleBundle as RhythmCardConfigSO`

3) Resolve pattern:
- `data = cardCfg?.patternOverride ?? (cfg.Parameters.Pattern as DrumPatternData)`

4) Resolve recipe:
- `recipe = cardCfg?.recipeOverride ?? cfg.Parameters.RhythmRecipe`

5) Choose modality:
- If `data == null` and `kit != null` → **Procedural(no pattern)**
- Else if `data != null` and has non-empty `lanes[].steps` → **Pattern(Grid)**
- Else → **Pattern(Legacy)** (piano-roll path)

6) Output:
- `MidiFile` for this track only (single channel)
- Bank/Patch stamped for kits; channel forced

---

## 3) Composer precedence rules (critical, load-bearing)

### 3.1 Pattern vs procedural precedence (Option A)
1) `RhythmCardConfigSO.patternOverride` (wins)
2) fallback: `TrackParameters.Pattern as DrumPatternData`
3) else procedural styles (with recipe + styleId overrides)

### 3.2 Procedural style selection precedence
When `data == null`:

1) `RhythmCardConfigSO.styleIdOverride` (if non-empty):
   - `RhythmStyleRegistry.ChooseById(part.TimeSignature, styleIdOverride)`
2) else:
   - `RhythmStyleRegistry.Choose(part.TimeSignature, recipe, rngCallback)`
   - recipe may include `RhythmStyleId` hint
3) if no style found:
   - fallback: internal `ComposeProcedural(...)`

### 3.3 Determinism contract
- Style selection must be driven by `ctx.rng` (seeded upstream).
- Any additional randomness introduced later (fills/feel) must also use `ctx.rng`.

---

## 4) Modalities (how each mode works)

## 4.1 Procedural(no pattern)
Trigger:
- `patternOverride == null` AND `TrackParameters.Pattern` is null

Flow:
1) Register styles: `RhythmStyleRegistry.RegisterDefaults()`
2) Resolve style by meter + overrides
3) Generate:
   - `style.Compose(...)` OR fallback `ComposeProcedural(...)`
4) Post-process:
   - `StampBankAndPatch(file, kit, channel)`
   - `ForceAllChannel(file, channel)`

Notes:
- `part.Measures` controls how long the file is (in measures).
- **Meter correctness requires beat-unit aware scheduling** inside the chosen style or fallback composer.

## 4.2 Pattern(Grid)
Trigger:
- `DrumPatternData.lanes` exists and has step data

Interpretation:
- Meter source is the **Part** (`part.TimeSignature`) for beats-per-measure and beat unit.
- Grid resolution source is the pattern (`data.subdivisions`).

Scheduling model (conceptual):
- `stepsPerBeat = data.subdivisions`
- `beatSpan = GetBeatSpan(part.TimeSignature)`
- each boolean step at index `s` lands at:
  - `beatsFromStart = s / stepsPerBeat`
  - `when = beatSpan * beatsFromStart`
- Pattern repeats to fill `part.Measures` (by repeating its total steps)

Kit mapping:
- Each lane uses `GeneralMidiPercussion` → `kit.TryGetMappedNote(...)`
- Velocity per lane is applied per note.

## 4.3 Pattern(Legacy / PianoRoll)
Trigger:
- Pattern exists but grid lanes are absent/empty, so the legacy `pianoRollPattern` is used.

High-level:
- Rewrites piano-roll lines by mapping `{symbols}` → concrete MIDI notes via `drumMappings`.
- Repeats the piano roll to fill `part.Measures`.

Important limitation:
- Legacy piano-roll timing may still behave “quarter-grid-like” depending on implementation.
- If Phase 2 is applied fully, measure offsets should respect beat unit (6/8 should not double).

---

## 5) Meter & loop length (where things can break)

### 5.1 Where meter comes from
- Meter is chosen at the **Part** level:
  - `MeterEffect` updates `SongCompositionUI.PartEntry.timeSignature`
  - `SongConfigBuilder` copies it into `SongConfig.PartConfig.TimeSignature`
  - Composer uses `part.TimeSignature`

### 5.2 Where measures come from
- `part.Measures` comes from `PartEntry.measures` (builder may default to 8 if <= 0).
- Composer always uses `part.Measures` for output length in procedural/styled generation.

### 5.3 The “half-loop” 6/8 bug (what happened and why)
Symptom:
- Rhythm sounded correct in 6/8 but only covered half of the loop.

Cause:
- Composer became beat-unit aware (Eighth in 6/8), but `SongOrchestrator` still computed `partTicks`
  using `MusicalTimeSpan.Quarter`, making the loop length ~2× longer.

Fix (required invariant):
- Orchestrator must compute `ticksPerBeat` using the Part time signature’s beat unit span
  (and ideally metronome note lengths too).

---

## 6) Observability & verification checklist (practical DoD)

### 6.1 Phase0 snapshot logging (what to look for)
`RhythmTrackComposer` logs a “Phase0 snapshot” with:
- chosen path: `Procedural(no pattern)`, `Pattern(Grid)`, `Pattern(Legacy)` or missing inputs
- `chosenStyleId` (procedural only)
- Part: `meas`, `bpm`, `ts` (and channel)
- Track: role + musician + kit
- StyleBundle type + patternOverride/recipeOverride presence
- Resolved recipe source
- Phrasing/feel fields reported (even if not yet applied)

### 6.2 Manual verification cases
1) **4/4 procedural**: `styleIdOverride="rock_backbeat_4_4"`
2) **6/8 procedural**: apply `MeterEffect` 6/8 + `styleIdOverride="shuffle_6_8_default"`
3) **Grid pattern**: assign `patternOverride` with lanes; confirm repeat to fill `part.Measures`
4) **Legacy pattern**: ensure offsets and loop length behave as expected (document limitations if not)

---

## 7) Known gaps / edge cases (documented, not hand-waved)

1) **Phase 3 pending:** `DrumPatternData.beatsPerMeasure` vs Part meter mismatch can silently misalign
   unless explicitly validated/resampled.
2) **Recipe is minimal today:** `RhythmRecipe` currently only includes a style hint + hat density enums.
   Most “feel” controls live on `RhythmCardConfigSO` and may still be TODO to apply.
3) **Legacy piano-roll semantics:** may remain quarter-grid-based unless fully updated for beat unit.

---

## 8) Code references (authoritative implementations)

- Composer:
  - `RhythmTrackComposer.cs`
- Authoring:
  - `RhythmCardConfigSO.cs`
  - `DrumPatternData.cs`
- Styles:
  - `RhythmStyleRegistry.cs`
  - `RhythmStyles.cs`  
    Example style ids:
    - `rock_backbeat_4_4` (4/4)
    - `shuffle_6_8_default` (6/8)
    - `waltz_ride_backbeat` (3/4)
    - `backbeat_5_4_3plus2` (5/4)
- Meter effects:
  - `MeterEffect.cs` (applies to PartEntry.timeSignature)
- Runtime length / loop math:
  - `SongOrchestrator.cs` (must be beat-unit aware for `partTicks`)
  - `SongConfigBuilder.cs` (copies Part meter/measures into SongConfig)

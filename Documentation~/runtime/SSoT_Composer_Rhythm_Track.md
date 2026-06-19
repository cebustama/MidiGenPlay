# SSoT — Composer Rhythm Track

## Scope

This document is the primary authority for MidiGenPlay rhythm/drum runtime behavior.
It covers the active runtime path centered on:

- `RhythmTrackComposer`
- `RhythmCardConfigSO` as the current concrete rhythm style bundle consumed by runtime
- `DrumPatternData` as the authored pattern asset consumed by runtime
- procedural style selection via `RhythmStyleRegistry`
- runtime meter normalization for grid-authored drum patterns

It does **not** define future editor-window UX in detail. That lives in:

- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
- `authoring/SSoT_Authoring_Tools.md`
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`

## 1. Current active runtime truth

The active rhythm runtime path is the `SongConfig` / `SongOrchestrator` / `RhythmTrackComposer` stack.
This is the package authority for current rhythm generation.

The older `MIDISong` / `MIDIGeneratorManager` branch still exists in the repository, but it is not the primary runtime truth for current rhythm generation.
It should be treated as legacy/reference material unless explicitly re-promoted.

## 2. Current inputs and resolution surface

The current rhythm composer resolves its inputs from two package-facing surfaces:

1. track parameters on the active `SongConfig` track
2. a concrete rhythm style bundle currently consumed through `TrackParameters.Style`

The code-backed inputs of documentary importance are:

- `TrackParameters.Style`
- `TrackParameters.Pattern`
- `TrackParameters.RhythmRecipe`
- `RhythmCardConfigSO.patternOverride`
- `RhythmCardConfigSO.patternPalette`
- `RhythmCardConfigSO.recipeOverride`
- `RhythmCardConfigSO.styleIdOverride`
- `RhythmCardConfigSO.fillEveryNMeasures`
- `RhythmCardConfigSO.lastMeasuresAsFill`
- `RhythmCardConfigSO.kickDensity`
- `RhythmCardConfigSO.snareGhostNoteChance`
- `RhythmCardConfigSO.hatSubdivisionBias`

### Current practical precedence

The current practical resolution visible in code is:

1. `RhythmCardConfigSO.patternOverride`
2. `RhythmCardConfigSO.patternPalette` (weighted, seeded, **TS-aware** pick — clone-on-pick)
3. `TrackParameters.Pattern` as `DrumPatternData`
4. `RhythmCardConfigSO.recipeOverride`
5. `TrackParameters.RhythmRecipe`
6. `RhythmCardConfigSO.styleIdOverride` when procedural style selection is needed
7. fallback procedural style selection through `RhythmStyleRegistry`

Steps 1–2 are resolved inside the TS-aware
`RhythmCardConfigSO.PickPatternOverride(rng, timeSignature, settings, verbose)`, which
`RhythmTrackComposer.Compose` calls with the Part time signature: `patternOverride` wins
if set; else a TS-aware weighted pick from `patternPalette` through the shared
`PatternFinder` / `PaletteSelector`; else null → fall through to step 3. A legacy
`PickPatternOverride(System.Random)` overload is retained for callers without a TS.

This precedence describes the current runtime path. If it changes, this document must be updated.

## 3. Current runtime paths

`RhythmTrackComposer` currently exposes three meaningful runtime paths:

### A. Procedural (no pattern)
Used when no `DrumPatternData` is resolved and a valid percussion kit is present.

Behavioral characteristics:
- style is chosen deterministically
- `ctx.rng` is preferred over unrelated global randomness
- `RhythmStyleRegistry` remains the current style source
- `styleIdOverride` can force a specific style when present

### B. Pattern (grid)
Used when `DrumPatternData` has lane/step content.

Behavioral characteristics:
- authored asset stores lane/step data as `List<StepState>` per lane
  (`StepState` carries `bool active` and `int velocity`; velocity 0 = defer to lane default)
- **current runtime consumption**: `ComposeFromGrid` calls
  `SnapshotAsStepVelocities()`, which returns
  `(instrument, (stepIndex, resolvedVelocity)[])` per lane. Per-step velocity
  reaches generated MIDI; the sentinel rule (velocity 0 → lane default) is
  resolved by `StepState.ResolveVelocity` at snapshot time
- the final per-note velocity is clamped to `[1..127]` before emission, so
  a lane with `defaultVelocity == 0` floors to 1 rather than producing
  inaudible silent hits
- authored grid patterns may be normalized to the current Part meter at runtime
  (normalization operates on a runtime clone; authored assets are not mutated)

### C. Pattern (legacy)
Used when the pattern does not resolve as the new lane/grid structure and runtime falls back to legacy `pianoRollPattern` content.

This path still exists for compatibility, but it should not be treated as the forward authoring target.

### D. Palette consumption contract (PCE; TS-aware as of CE-F1)

When a card carries a `patternPalette` and no `patternOverride`,
`RhythmTrackComposer.Compose` resolves its working pattern through the TS-aware
`RhythmCardConfigSO.PickPatternOverride(pickRng, part.TimeSignature, settings, verbose)`:

- `pickRng` is `ctx.rng`. When `ctx.rng` is null *and* a palette is present, the composer
  logs a warning and falls back to `System.Random(settings.defaultSeed)` so output stays
  reproducible.
- The palette pick goes through the shared `PatternFinder` / `PaletteSelector`
  (Tier A exact-TS -> Tier B fitness heuristic -> Tier C raw-weights), keyed on each
  pattern's `TimeSignature`. Exactly one `rng.NextDouble()` is consumed per pick, so the
  determinism invariant holds.
- Tier B's density term uses the drum-specific **capped foundational-onset density**: kick
  onsets per bar (GM 35/36; else the lowest-note lane) capped at the meter's natural
  grouping count, so busy grooves are not penalized — only under-articulation of the meter
  (D-F1.5).
- The pick clones on selection (`ScriptableObject.Instantiate`), so the project asset is
  never mutated. It then flows into the existing grid path, including
  `NormalizeGridPatternForPartIfNeeded`, which produces a fresh meter-correct clone; no
  additional deep clone is required.

This shares one selector with the backing composer's
`BackingCardConfigSO.PickProgressionOverride` seam. Both palettes'
`preferExact*TimeSignatureMatches` toggles are consumed in that single selector, so the
PCE-era TS-toggle asymmetry no longer exists.

## 4. Determinism contract

Rhythm generation must be deterministic under the orchestration seed/RNG context.

Current code-backed truth:
- `SongOrchestrator` seeds `ctx.rng` deterministically
- `RhythmTrackComposer` uses that seeded RNG path for style choice
- palette selection (`PickPatternOverride`) draws from the same `ctx.rng` and consumes one `NextDouble()` per pick through the shared `PaletteSelector`; same seed => same pattern picked
- same seed should produce stable style selection and stable MIDI output, assuming unchanged inputs

Rhythm runtime should not silently depend on unrelated global randomness for style selection.

## 5. Meter authority contract

Rhythm runtime follows the package-wide rule:

- **Part.TimeSignature is authoritative**
- when a grid-authored `DrumPatternData` signature differs from the current Part, runtime adapts using normalized bar-time semantics
- adaptation occurs on a runtime clone
- authored assets are not silently mutated during runtime normalization

This is already part of current runtime truth and should not be described as future work.

## 6. Current implemented state vs deferred semantics

### Implemented and code-backed today

The following are already part of current runtime truth:

- deterministic style selection through seeded RNG
- beat-unit-aware timing
- runtime support for procedural, grid, and legacy pattern paths
- runtime normalization of grid-authored patterns to the active Part meter
- `StepState`-aware normalization in `NormalizeGridPatternForPartIfNeeded`
  (compile-fixed in Phase 6; no behavioral change to generated MIDI)
- per-step velocity in generated MIDI: `ComposeFromGrid` consumes
  `SnapshotAsStepVelocities()` as of 2026-05-23 (changelog-ssot.md). The
  `SnapshotAsIndices()` API remains available as a default-velocity-only
  view but is no longer called by any runtime composer.

### Present in the input surface but **not yet fully closed semantically**

The following controls already exist on `RhythmCardConfigSO`, but should **not** be documented as fully honored in runtime yet:

- `fillEveryNMeasures`
- `lastMeasuresAsFill`
- `kickDensity`
- `snareGhostNoteChance`
- `hatSubdivisionBias`

These fields are real package-facing inputs, but their full musical meaning is still an active implementation area.

## 7. Current package sequencing

The current package sequencing is:

1. ~~consolidate rhythm authoring and the dedicated authoring toolchain~~ — done (Phases 4–6)
2. ~~text/DSL authoring mode~~ — done (Phase 7)
3. ~~per-step velocity consumption in `ComposeFromGrid`~~ — done (2026-05-23)
4. persistence/repository cleanup for rhythm tools (Phase 8)
5. phrasing / feel semantics as a later runtime layer (Phase 9)

## 8. Boundary with authoring docs

This document defines how runtime consumes rhythm inputs.
It does **not** define the full authoring UX.

Authoring authority lives in:
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
- `authoring/SSoT_Authoring_Tools.md`

Planning authority for the next rhythm editor milestone lives in:
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`

## 9. Boundary with cross-project docs

Cross-project docs may describe how ALWTTT chooses or injects rhythm bundles, but they do not override this runtime contract.

## 10. Update triggers

Update this SSoT when:

- rhythm path precedence changes
- determinism rules change
- meter normalization changes
- `RhythmCardConfigSO` runtime meaning changes
- phrasing/feel fields become semantically closed in runtime
- the future rhythm editor changes authored data contracts consumed by runtime

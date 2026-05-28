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
2. `TrackParameters.Pattern` as `DrumPatternData`
3. `RhythmCardConfigSO.recipeOverride`
4. `TrackParameters.RhythmRecipe`
5. `RhythmCardConfigSO.styleIdOverride` when procedural style selection is needed
6. fallback procedural style selection through `RhythmStyleRegistry`

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

## 4. Determinism contract

Rhythm generation must be deterministic under the orchestration seed/RNG context.

Current code-backed truth:
- `SongOrchestrator` seeds `ctx.rng` deterministically
- `RhythmTrackComposer` uses that seeded RNG path for style choice
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

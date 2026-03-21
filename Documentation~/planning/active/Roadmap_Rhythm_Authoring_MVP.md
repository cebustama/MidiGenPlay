# Roadmap — Rhythm Authoring MVP

> Active MidiGenPlay package planning.
> This roadmap is grounded in the current codebase and intentionally separates **what already exists**, **what is next**, and **what remains later**.

## Purpose

Bring Rhythm up to the same end-to-end clarity already emerging in Backing and Chord authoring:

- deterministic runtime behavior
- meter-correct timing
- clear authoring/runtime contracts
- a production-usable rhythm authoring toolchain
- explicit separation between current MVP tooling and the future dedicated editor

## Current code-backed baseline

### Already true today

The codebase already supports:

- deterministic rhythm style selection through seeded orchestration RNG
- beat-unit-aware timing
- runtime support for procedural rhythm generation
- runtime support for grid-authored `DrumPatternData`
- legacy compatibility for piano-roll-style patterns
- runtime normalization of grid-authored patterns to the active Part meter
- a dedicated package-owned rhythm authoring entry point (`DrumPatternEditorWindow`)
- row-local trigger and velocity editing per lane in `DrumPatternEditorWindow`
- `StepState`-based persisted model: `bool active` + `int velocity` per step

### Important correction to earlier planning language

The package is **not** starting rhythm authoring from zero.
What already exists is a mature `DrumPatternEditorWindow` with trigger and velocity editing,
built on top of the earlier runtime-first MVP panel.

## Current milestone sequencing

The active package sequencing is now:

1. ~~consolidate rhythm authoring and the dedicated authoring UX~~ — done (Phases 4–5)
2. ~~row-local velocity view and data-model extension~~ — done (Phase 6)
3. text/DSL mode for rhythm authoring (Phase 7, next active)
4. persistence/repository cleanup (Phase 8)
5. phrasing / feel knobs as organic variation layer (Phase 9)

This is a deliberate reprioritization.

## Milestone map

## Phase 0–3 — Runtime foundation

### Status
Completed.

### Closed outcomes
- style selection is deterministic
- timing is beat-unit-aware
- `DrumPatternData` can be adapted to the active Part meter through normalized bar-time semantics

### Why this matters
These phases establish that the runtime foundation is already strong enough to justify pushing authoring tooling first.

---

## Phase 4 — Document and lock the current rhythm MVP

### Status
Completed.

### Closed outcomes
- `runtime/SSoT_Composer_Rhythm_Track.md` refined
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` refined
- `authoring/SSoT_Authoring_Tools.md` refined
- `CURRENT_STATE.md` updated
- docs clearly state that a rhythm panel already existed
- docs clearly state what was current truth vs future target
- docs no longer implied that phrasing/feel completion must happen before editor work

---

## Phase 5 — Dedicated Rhythm Authoring Tool v2

### Status
Completed.

### Closed outcomes
- `DrumPatternEditorWindow` exists as a dedicated scene-independent package authoring entry point
- a designer can author and persist a drum pattern without relying on runtime-only scene wiring
- normalize/apply/save contract is explicit and documented
- `RhythmPatternPanelController` reclassified as secondary tool (not deprecated)

---

## Phase 6 — Trigger View + Row-Local Velocity View

### Status
Completed.

### Closed outcomes
- `DrumPatternData.Lane.steps` promoted from `List<bool>` to `List<StepState>`
  (`StepState { bool active; int velocity; }` — velocity 0 is sentinel for lane default)
- `StepState.ResolveVelocity(int laneDefault)` encapsulates effective velocity resolution
- `SnapshotAsIndices()` return signature unchanged — existing runtime callers unaffected
- `SnapshotAsStepVelocities()` added as per-step-velocity-aware forward snapshot
- `DrumPatternEditorWindow` extended with `[T]`/`[V]` row mode toggle
  - Trigger mode: boolean step buttons, preserves velocity on toggle-off
  - Velocity mode: per-step int fields, `[clr]` resets overrides to 0
  - Row view mode is editor UI state only, not persisted in asset
- `RhythmTrackComposer.NormalizeGridPatternForPartIfNeeded` updated for `StepState` (compile-fix, no behavioral change)
- `RhythmPatternPanelController` updated for `StepState` (compile-fix, no behavioral change)
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` updated — `StepState` is now canonical persisted model
- `authoring/SSoT_Authoring_Tools.md` updated — Phase 6 promoted to current truth
- `runtime/SSoT_Composer_Rhythm_Track.md` updated — asset-truth vs runtime-consumption gap documented

### Known open item (not blocking Phase 7)
`ComposeFromGrid` still calls `SnapshotAsIndices()` — per-step velocity is not yet consumed
by runtime generation. Switching to `SnapshotAsStepVelocities()` is a deferred decision.

### Migration note
Existing `.asset` files serialized with the old `List<bool>` model will deserialize
lane step arrays as empty lists on first load. Assets must be re-authored or migrated
manually via `DrumPatternEditorWindow`.

---

## Phase 7 — Text / DSL Mode (next active)

### Goal
Add fast textual rhythm sketching without discarding the canonical grid/lane model.

### Why this is later than the dedicated editor baseline
The most urgent gap is the dedicated package-owned editor and its normalized data flow.
Text mode is valuable, but it should not block the first clean editor release.

### Target capabilities
- one compact textual input per lane/row
- parse -> normalize -> preview -> apply
- round-trip with grid editing where feasible

### Policy constraint
The first text mode should still target the same global timing model as the current asset.
True polymeter or row-local cycle metadata should remain a later explicit decision.

### Definition of done
- text input can generate valid normalized rhythm data
- switching between text and grid does not silently corrupt assets

---

## Phase 8 — Persistence and repository cleanup

### Goal
Align rhythm authoring tools with package-owned persistence abstractions instead of ad hoc direct save paths.

### Current issue
`DrumPatternEditorWindow` saves directly through editor-side asset operations with a hardcoded default folder.
The codebase already contains repository/store abstractions that should be used instead.

### Target
- package-owned write/read path is explicit
- rhythm tools use the same persistence philosophy as the rest of the package
- folder and repository behavior are no longer hidden inside tool-specific code

### Definition of done
- rhythm tool persistence is routed through an explicit package-sanctioned path
- doc and code agree on where patterns come from and how they are saved

---

## Phase 9 — Phrasing / feel semantics as organic variation layer

### Goal
Implement the currently exposed but semantically incomplete rhythm variation knobs **after** authoring tooling is in a healthier place.

### Fields involved
- `fillEveryNMeasures`
- `lastMeasuresAsFill`
- `kickDensity`
- `snareGhostNoteChance`
- `hatSubdivisionBias`

### Design intent
These controls should act as an organic variation layer on top of:

- procedural rhythm generation
- and, where appropriate later, patterns authored through the package tools

### Why this comes later now
Right now the package benefits more from strong authoring tools than from prematurely deepening variation semantics on top of an incomplete editor workflow.

### Definition of done
- phrasing/feel controls have documented audible effect
- deterministic behavior is preserved
- docs no longer describe these fields as only partially honored

---

## Phase 10 — Validation and regression coverage

### Goal
Make rhythm generation and authoring harder to regress silently.

### Suggested checks
- deterministic generation checks
- timing correctness checks in meters such as 6/8
- grid normalization checks
- pattern override end-to-end validation
- editor normalize/apply/save sanity checks

### Definition of done
- at least one regression-catching test or repeatable validation harness exists for rhythm determinism and timing

---

## Immediate next steps

1. Decide whether to implement Phase 7 (text/DSL mode) or proceed directly to Phase 8 (persistence cleanup)
2. Decide and schedule the `ComposeFromGrid` → `SnapshotAsStepVelocities` runtime switch (deferred from Phase 6)
3. Confirm existing `.asset` files are re-authored or accepted as requiring manual migration

## Related authorities

- `CURRENT_STATE.md`
- `runtime/SSoT_Composer_Rhythm_Track.md`
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
- `authoring/SSoT_Authoring_Tools.md`

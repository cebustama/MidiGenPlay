# CURRENT_STATE

## Active now

1. Decide whether to implement Phase 7 (text/DSL mode) or proceed directly to Phase 8 (persistence cleanup)
2. Decide and schedule the `ComposeFromGrid` → `SnapshotAsStepVelocities` runtime switch (deferred from Phase 6 — not blocking authoring work)
3. Confirm existing `.asset` files are re-authored or accepted as requiring manual migration (see migration note in `SSoT_Authoring_Rhythm_Patterns.md` Section 2)
4. Confirm no other consumers of `lane.steps` as `List<bool>` exist outside the four updated files

## Just completed

- Implemented Phase 6: `StepState` struct replaces `List<bool>` in `DrumPatternData.Lane`
  - `StepState { bool active; int velocity; }` — velocity 0 is the sentinel (defer to lane default)
  - `StepState.ResolveVelocity(int laneDefault)` encapsulates effective velocity resolution
  - `SnapshotAsIndices()` return signature unchanged — existing runtime callers unaffected
  - `SnapshotAsStepVelocities()` added as the per-step-velocity-aware forward snapshot
  - `DeepCloneRuntime`, `EnsureSizes`, `ClearAll`, `GetActiveSteps` all updated
- Updated `DrumPatternEditorWindow` with row-local velocity view
  - `[T]` / `[V]` toggle per lane row
  - Trigger mode: existing boolean step buttons, preserves step velocity on toggle-off
  - Velocity mode: per-step int fields, `[clr]` button to reset overrides to 0
  - `_velocityModeRows` is editor UI state only, not persisted in asset
- Compile-fix applied to `RhythmTrackComposer.NormalizeGridPatternForPartIfNeeded`
  - `List<bool>` → `List<StepState>`, step reads updated to `.active`, velocity preserved during normalization
  - No behavioral change to runtime generation
- Compile-fix applied to `RhythmPatternPanelController`
  - `lane.steps[s]` bool reads/writes → `StepState` equivalents
  - Toggle-off preserves existing per-step velocity
  - No behavioral change
- Updated `SSoT_Authoring_Rhythm_Patterns.md` — `StepState` is now canonical persisted model
- Updated `SSoT_Authoring_Tools.md` — Phase 6 promoted to current truth, stale planning sections removed
- Updated `SSoT_Composer_Rhythm_Track.md` — asset-truth vs runtime-consumption gap documented in Section 3B
- Updated `Roadmap_Rhythm_Authoring_MVP.md` — Phase 6 marked closed, Phase 7 promoted to next active

## Next

1. Phase 7: text/DSL mode for rhythm authoring (optional for first dedicated editor release)
2. Phase 8: route `DrumPatternEditorWindow` save paths through package store/repository abstractions
3. Runtime decision: update `ComposeFromGrid` to call `SnapshotAsStepVelocities` when per-step velocity fidelity is wanted in generated MIDI (runtime composer change, not an authoring change)
4. Resume phrasing / feel runtime completion only after Phase 8 is done (Phase 9)
5. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to legacy/reference status

## Blocked / not implemented yet

- Per-step velocity in generated MIDI: `ComposeFromGrid` still uses `SnapshotAsIndices` (lane default velocity); consuming `SnapshotAsStepVelocities` is a deferred runtime change
- Text/DSL mode for rhythm authoring (Phase 7)
- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists in the repository

## Docs to update next

- `runtime/SSoT_Composer_Rhythm_Track.md` — when `ComposeFromGrid` is updated to consume per-step velocity
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — when Phase 7 work begins or is skipped

## Working rule

If the next technical change touches rhythm generation, rhythm authoring, or the rhythm editor:

- update the primary rhythm SSoT first
- then update `CURRENT_STATE.md` if active focus or reality changed
- then update `planning/active/Roadmap_Rhythm_Authoring_MVP.md` if the next-step sequence changed
- then update `changelog-ssot.md` if semantics or authority changed
- then update `runtime/SSoT_Composer_Rhythm_Track.md` if runtime generation behavior changed

# changelog-ssot

## 2026-03-20 — Phase 6 complete: StepState data model and row-local velocity view

### Data model change
- `DrumPatternData.Lane.steps` promoted from `List<bool>` to `List<StepState>`
- `StepState { bool active; int velocity; }` is the new canonical per-step representation
- Sentinel contract: `velocity == 0` means defer to lane `defaultVelocity`; `1–127` is an explicit per-step override
- `StepState.ResolveVelocity(int laneDefault)` encapsulates effective velocity resolution
- `StepState.Off` and `StepState.On(int vel)` are the canonical construction helpers

### New API
- `DrumPatternData.SnapshotAsStepVelocities()` — per-step-velocity-aware snapshot for future runtime consumption
- `DrumPatternData.SnapshotAsIndices()` return signature **unchanged** — existing runtime callers unaffected

### Editor update
- `DrumPatternEditorWindow` gains per-row `[T]`/`[V]` mode toggle
  - Trigger mode: boolean step buttons (behavior unchanged from Phase 5)
  - Velocity mode: per-step int fields; 0 = deactivate; >0 = activate with explicit velocity; `[clr]` resets overrides
  - Row view mode is editor UI state only; not persisted in asset

### Compile-fixes (no behavioral change)
- `RhythmTrackComposer.NormalizeGridPatternForPartIfNeeded`: `List<bool>` → `List<StepState>`, step reads updated to `.active`, per-step velocity preserved during normalization
- `RhythmPatternPanelController`: `lane.steps[s]` bool reads/writes → `StepState` equivalents; toggle-off preserves existing step velocity

### Migration note
- Existing `DrumPatternData` `.asset` files serialized with `List<bool>` steps will have empty lane step arrays after this change. Assets require re-authoring via `DrumPatternEditorWindow` or manual migration. This is an accepted consequence of the data-model promotion.

### Authority changes
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` Section 2 rewritten: `StepState` is now canonical persisted truth; migration note added
- Section 4: per-step velocity removed from "not yet true" list; runtime per-step velocity consumption remains deferred
- Section 5: Phase 6 velocity view documented as implemented
- Section 8: Phase 6 marked complete in sequencing

### Not changed
- `runtime/SSoT_Composer_Rhythm_Track.md`: `ComposeFromGrid` still uses `SnapshotAsIndices`; per-step velocity in generated MIDI is a deferred runtime change
- `coverage-matrix.md`: primary home for rhythm authoring was already `authoring/SSoT_Authoring_Rhythm_Patterns.md`

---

## 2026-03-20 — Phase 5 complete: DrumPatternEditorWindow promoted to primary rhythm authoring tool

### Added
- `DrumPatternEditorWindow.cs` — dedicated package-owned Unity Editor window for rhythm pattern authoring
  - scene-independent (no runtime MonoBehaviour wiring)
  - follows `ChordProgressionEditorWindow` architectural pattern
  - `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
  - explicit working copy / apply / save-as contract
  - lane management, instrument selection, step toggle grid, safe normalize/rebuild

### Authority changes
- `DrumPatternEditorWindow` is now the primary package-owned rhythm authoring entry point
- `RhythmPatternPanelController` reclassified as secondary / legacy runtime-scene panel
  - not deprecated; still valid for scene-embedded editing flows
  - no longer documented as the primary tool

### Modified
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
  - Section 3 rewritten: `DrumPatternEditorWindow` as 3A (primary), `RhythmPatternPanelController` as 3B (secondary)
  - Section 4: removed "no dedicated `DrumPatternEditorWindow`" from "not true yet" list
  - Section 8: updated sequencing to reflect Phase 5 completion
  - Added explicit normalize/apply/save contract documentation
- `authoring/SSoT_Authoring_Tools.md`
  - Section 3A: `DrumPatternEditorWindow` added alongside `ChordProgressionEditorWindow` as Category A tool
  - Section 3B: `RhythmPatternPanelController` reclassified as legacy runtime-scene MVP
  - Section 5: "current truth" updated to reflect `DrumPatternEditorWindow` capabilities
  - Section 9: sequencing updated to reflect Phases 4–5 as done
- `CURRENT_STATE.md`
  - Phase 5 moved from Blocked to Just Completed
  - Next steps updated to Phase 6 data-model decision as immediate priority

### Notes
- Per-step velocity remains outside current persisted truth; data-model decision is the Phase 6 gate
- `DefaultSaveFolder` hardcoded in `DrumPatternEditorWindow`; Phase 8 will route through package store abstractions
- `coverage-matrix.md` does not require update: primary home for rhythm authoring tooling was already `authoring/SSoT_Authoring_Tools.md`

---

## 2026-03-18 — Rhythm semantic refinement against codebase

### Clarified
- The active rhythm runtime truth is the `SongConfig` / `SongOrchestrator` / `RhythmTrackComposer` stack, not the older `MIDISong` / `MIDIGeneratorManager` branch.
- Rhythm runtime already supports deterministic procedural generation, grid-authored `DrumPatternData`, legacy compatibility, and Part-meter normalization.
- MidiGenPlay already has a real rhythm authoring MVP through `RhythmPatternPanelController` + `PatternGrid` + `RhythmRowHeader`; grid authoring is not merely future intent.

### Re-sequenced
- Rhythm planning now explicitly prioritizes dedicated authoring/tool consolidation before closing phrasing / feel semantics.
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` now treats phrasing / feel knobs as a later organic-variation milestone rather than a blocker before authoring work.

### Added planning clarity
- Captured the future rhythm-editor UI target as a row-based trigger grid plus row-local velocity-edit view.
- Explicitly documented that per-step velocity is not part of current persisted package truth and would require a canonical data-model extension before promotion.

### Authority adjustment
- `coverage-matrix.md` now distinguishes between current rhythm authoring truth and the still-planned rhythm editor interaction model.

## 2026-03-18 — Documentation governance migration bootstrap

### Added
- Root governance spine:
  - `README.md`
  - `SSoT_INDEX.md`
  - `SSoT_CONTRACTS.md`
  - `coverage-matrix.md`
  - `CURRENT_STATE.md`
  - `changelog-ssot.md`
- New `runtime/` SSoTs
- New `authoring/` SSoTs
- New folder READMEs for `runtime/`, `authoring/`, `reference/`, `planning/`, `research/`, and `archive/`

### Reclassified
- Moved ALWTTT-specific documents under `reference/cross-project/ALWTTT/`
- Reclassified `ALWTTT_MidiGenPlay_Rhythm_MVP_Roadmap.md` as active package planning and renamed it to `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- Reclassified research prompts/design notes under `research/`
- Reclassified legacy package docs as absorbed source material under `archive/absorbed/`
- Reclassified `CardData_Redesign.md` as historical under `archive/historical/`

### Authority changes
- Package authority is now split by responsibility instead of being mixed in root legacy docs
- Cross-project integration docs no longer compete with package truth
- Rhythm authoring is now treated as an immediate package priority in `CURRENT_STATE.md`

### Notes
This change is a documentation governance migration and folder restructuring pass.
It preserves source material rather than deleting it.

## 2026-03-19 — Hardening micro-pass for cross-project ALWTTT references

### Modified
- `runtime/SSoT_Runtime_Song_Model_and_Config.md`
- `reference/cross-project/ALWTTT/README.md`
- `reference/cross-project/ALWTTT/SSoT_Runtime_CompositionSession_Bridge.md`
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
- `reference/cross-project/ALWTTT/SSoT_CompositionSystem_INDEX.md`

### Key hardening decisions
- clarified that `SongConfig` / `SongConfigManager` are the **package-side runtime truth after handoff**, not a replacement for a consumer project's game-side editable/session truth
- hardened the status of ALWTTT cross-project docs as **reference only**, not package authority
- made the primary-home rule more explicit to reduce documentary drift between MidiGenPlay and ALWTTT

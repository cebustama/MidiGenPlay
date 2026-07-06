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
- whole-window Grid / Text tab toggle in `DrumPatternEditorWindow` (Phase 7)
- `StepState`-based persisted model: `bool active` + `int velocity` per step

### Important correction to earlier planning language

The package is **not** starting rhythm authoring from zero.
What already exists is a mature `DrumPatternEditorWindow` with trigger, velocity, and text editing,
built on top of the earlier runtime-first MVP panel.

## Current milestone sequencing

The active package sequencing is now:

1. ~~consolidate rhythm authoring and the dedicated authoring UX~~ — done (Phases 4–5)
2. ~~row-local velocity view and data-model extension~~ — done (Phase 6)
3. ~~text/DSL mode for rhythm authoring~~ — done (Phase 7)
4. ~~persistence/repository cleanup~~ — done (Phase 8, closed 2026-07-05; widened to Chord + Melody)
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

### Known open item (not blocking Phase 7 or 8)
`ComposeFromGrid` still calls `SnapshotAsIndices()` — per-step velocity is not yet consumed
by runtime generation. Switching to `SnapshotAsStepVelocities()` is an independent
runtime micro-batch that can run before or after Phase 8.

### Migration note
Existing `.asset` files serialized with the old `List<bool>` model will deserialize
lane step arrays as empty lists on first load. Assets must be re-authored or migrated
manually via `DrumPatternEditorWindow`.

---

## Phase 7 — Text / DSL Mode

### Status
Completed.

### Closed outcomes
- `Editor/DrumPatternTextParser.cs` and `Editor/DrumPatternTextWarning.cs` added
  in namespace `MidiGenPlay.Authoring` — pure-function parser, renderer, and
  per-cell-diff `ApplyTextEdits` over a single lane's `List<StepState>`
- `DrumPatternEditorWindow` extended with whole-window Grid / Text tab toolbar
  (D-T3 = B), mirroring `ChordProgressionEditorWindow`
- Text-mode lane row: disabled `[T]/[V]` placeholder, read-only
  "Instrument (vNNN)" label, single-line text field per lane, per-row `✕`
- HelpBox glyph legend at the top of the text pane; warning panel below the
  lane rows
- Parse on tab-switch (Text → Grid) and on Apply / SaveAs (D-T4)
- Per-cell diff preserves non-canonical per-step velocity for cells whose
  typed glyph matches the lane's prior render — text is a view, asset is
  canonical
- 3-tier glyph alphabet (v1): `.` and `-` = rest, `x` = lane default
  (sentinel), `X` = accent (120), `o` = ghost (50); `|` and whitespace
  ignored (D-T1, D-T2)
- Short input right-pads with rests; long input right-truncates; both emit a
  warning (D-T7)
- Unknown glyphs become rests with an `UnknownGlyph` warning (D-T6)
- Non-canonical render velocities snap to the nearest tier with a
  `VelocitySnappedToTier` warning; the asset's per-step velocity remains
  canonical until the user types a different glyph in that cell
- Text rows not persisted into asset; `[SerializeField] _inputMode` and
  `[SerializeField] _textRows` survive domain reload within the session only
  (D-T8)
- First test assembly in the package: `Tests/Editor/MidiGenPlay.Tests.Editor`
  with 13 EditMode tests covering SMR1, SMR2, SMR4, SMR5 plus render snap,
  bar-separators, ignored-char handling, dash equivalence, per-cell-diff
  round-trip preservation
- Three manual smoke tests verified: SMR3 grid↔text round-trip, SMR6
  signature change re-renders text, SMR7 SaveAs preserves text-mode edits
- `package.json` gains `"testables": ["MidiGenPlay.Tests.Editor"]`
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` updated — new §3A subsection
  "Text mode (Phase 7)" plus §4 / §5 / §8 updates
- `authoring/SSoT_Authoring_Tools.md` updated — text-mode capability added,
  §5 / §6 updated

### Final D-T9 amendment
The parser placement was initially chosen as `Editor/Authoring/` with namespace
`MidiGenPlay.Editor.Authoring`. Two corrections during implementation:

1. The package's existing `Editor/` directory is flat; no `Authoring/`
   subfolder exists. Files placed directly in `Editor/`.
2. The namespace `MidiGenPlay.Editor.*` shadowed `UnityEditor.Editor` inside
   `namespace MidiGenPlay { … }` and broke `SoundFontCacheSOEditor` with
   CS0118. Renamed to `MidiGenPlay.Authoring` (a peer to
   `MidiGenPlay.Composition` and `MidiGenPlay.MusicTheory`).

The renamed namespace is the canonical home; the editor-only nature is
encoded by `#if UNITY_EDITOR` and the asmdef's `includePlatforms: ["Editor"]`.

### Testing-process knowledge captured
Phase 7 introduced the first test assembly in the package. The canonical asmdef
shape and the `testables` handshake are documented in
`reference/package/Tests_Authoring_HowTo.md`. The how-to also documents the
`defineConstraints: ["UNITY_INCLUDE_TESTS"]` gotcha (silently blocks
compilation in Unity 2022.3 local-package contexts; resolved by leaving the
constraint empty).

---

## Phase 8 — Persistence and repository cleanup (Completed 2026-07-05)

### Status
Completed — batch **PATTERN-PERSIST-1**. Scope was widened at batch open (D2)
beyond rhythm to also cover the Chord and Melody editors; recorded in
`CURRENT_STATE.md` + `changelog-ssot.md` (no separate Chord/Melody roadmap entries —
this was persistence plumbing, not new track capability).

### Closed outcomes
- All three pattern editors (`DrumPatternEditorWindow`,
  `ChordProgressionEditorWindow`, `MelodyPatternEditorWindow`) persist through the
  shared, previously-unused generic store `TrackPatternConfigStoreResources<T>`
  instead of ad-hoc `AssetDatabase` calls with per-window hardcoded folders.
- The store gained a public `AssetsSaveRootPath` accessor and an editor-only
  `PersistNewAtPath(instance, path)` method (dialog-preserving write).
- Drum's save root unchanged (`.../Patterns/Drums`); Chord gained a real default
  folder for the first time (`.../Patterns/Chords`, across all four of its save
  sites); Melody realigned from singular `.../Patterns/Melody` to plural
  `.../Patterns/Melodies` (matches the repository read root + shipped assets).
- Additive canonical-root "Browse Saved Patterns" list added to each editor.
- Runtime read path (`IPatternRepository` / `PatternRepositoryResources`) unchanged;
  determinism invariant untouched (no composer / RNG surface changed).

### Decisions resolved at batch open
- **D1**: route through the store `TrackPatternConfigStoreResources<T>`, **not**
  `IPatternRepository` — the roadmap/tools docs previously named the repository, but
  the store was the correct, already-editor-safe write path. Repository not extended.
- **D2**: Chord + Melody in scope alongside Drum (explicit widening).
- **D3**: browse affordance is additive (alongside the existing `ObjectField`) and
  canonical-root-only (`Resources.LoadAll`, not a full-project scan).
- **D4**: per-window hardcoded `DefaultSaveFolder` constants removed; the store is
  the single source of the save root.
- **D5**: Melody store key `"Melodies"` (plural) — realigns editor writes to the
  runtime read root (no stray `/Melody` assets existed, so no migration).
- **D6 = C**: the editor keeps its interactive Save dialog; the store owns the write
  via `PersistNewAtPath` (the only option satisfying both "preserve the dialog" and
  "route through the store").

### Definition of done — met
- rhythm (and, by widening, chord + melody) tool persistence routed through the
  store ✓
- doc and code agree on where patterns come from and how they are saved ✓
- `authoring/SSoT_Authoring_Tools.md` "current limitations" hardcoded-folder bullets
  removed (drum + melody); §6 flipped to a closed persistence note ✓
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` §4 store-backed-persistence line
  moved to "What is already true" (reworded to name the store) ✓

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

### Note (post Phase 7)
The package now has a test-assembly seam (`MidiGenPlay.Tests.Editor`) and a
documented how-to for adding new tests. Phase 10 can build on that seam
without re-litigating the asmdef shape or `testables` handshake.

---

## Immediate next steps

1. Confirm existing `.asset` files are re-authored or accepted as requiring
   manual migration (Phase 6 carry-over)
2. Phase 9 (phrasing / feel semantics) is the remaining rhythm-authoring phase;
   per `CURRENT_STATE.md` it resumes now that Phase 8 is closed.

_(2026-07-05: the former item 1 — "surface and confirm Phase 8 open decisions" —
was removed; Phase 8 closed via batch PATTERN-PERSIST-1, with decisions D1–D6
recorded in the Phase 8 section above.)_

_(2026-07-05 drift fix: this list previously carried a third item — "decide
and schedule the `ComposeFromGrid` → `SnapshotAsStepVelocities` runtime
micro-batch." That work is already closed; see `CURRENT_STATE.md` and
`runtime/SSoT_Composer_Rhythm_Track.md` §6 ("per-step velocity in generated
MIDI... as of 2026-05-23"). Removed as stale; no roadmap-authority change —
roadmaps are not implementation truth, this is a housekeeping correction.)_

## Related authorities

- `CURRENT_STATE.md`
- `runtime/SSoT_Composer_Rhythm_Track.md`
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
- `authoring/SSoT_Authoring_Tools.md`
- `reference/package/Tests_Authoring_HowTo.md`

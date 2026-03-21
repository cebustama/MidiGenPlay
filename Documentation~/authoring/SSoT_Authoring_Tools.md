# SSoT — Authoring Tools

## Scope

This document defines package-owned conventions for authoring tools in MidiGenPlay.
It does not attempt to freeze every UI detail.
It defines:

- what counts as a package-owned authoring tool
- what the validated authoring-tool pattern is
- which package tools are already mature vs partial
- how current tools relate to future editor targets

## 1. Tooling principles

Package-owned authoring tools should be:

- data-driven
- explicit about the package-owned asset they produce or edit
- clear about preview/runtime-clone state vs saved asset state
- reusable across projects when possible
- documented separately from game-specific session tooling

## 2. Preferred package authoring loop

The validated package pattern is:

1. input or edit authoring data
2. normalize / parse / validate
3. preview the result on normalized data
4. apply or save into a package-owned asset
5. let runtime consume that asset later

This pattern is demonstrated by both `ChordProgressionEditorWindow` and
`DrumPatternEditorWindow` and should remain the target shape for future package tools.

## 3. Current tool categories

### A. Mature package-owned editor windows

Two tools currently hold Category A status as dedicated, scene-independent package
authoring entry points:

#### `ChordProgressionEditorWindow`

The original package reference implementation. Demonstrates:

- dedicated editor-window UX
- target asset binding with working copy isolation
- multiple authoring modes (Roman string and grid)
- normalize / preview / apply / save flow
- separation between authoring input and persisted structure

#### `DrumPatternEditorWindow`

The dedicated rhythm authoring entry point, implemented in Phase 5 and extended in Phase 6.
Follows the same architectural pattern as `ChordProgressionEditorWindow`, adapted for
row-based drum pattern editing.

Capabilities:

- opens via `MidiGenPlay / Drum Pattern Editor...`
- scene-independent: no runtime MonoBehaviour wiring required
- `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
- deep-clone / working-copy isolation: asset is never mutated until Apply or Save As
- lane management and instrument selection per lane
- default velocity per lane via inline int field
- step toggle grid with visual bar boundaries
- row-local `[T]` / `[V]` mode toggle per lane:
  - **Trigger mode** (`[T]`, default): boolean step on/off, preserves per-step velocity on toggle-off
  - **Velocity mode** (`[V]`): per-step int fields; `[clr]` resets overrides to 0 (defer to lane default)
- safe signature normalize/rebuild (resizes lane step arrays without data loss where possible)
- Apply To Asset and Save As New Asset flows

Current limitations (known, not blocking):

- save path uses a hardcoded default folder (Phase 8 will route through package store abstractions)
- style textures created on first draw are not explicitly cleaned up on window close (low risk for editor windows)
- unsaved new patterns are lost on domain reload if no asset is assigned
- row view mode (`[T]`/`[V]`) is editor UI state only — not persisted in the asset, resets on domain reload or asset rebind

### B. Legacy runtime-scene MVP panel

`RhythmPatternPanelController` + `PatternGrid` + `PatternGridCell` + `RhythmRowHeader`
is the behavioral predecessor to `DrumPatternEditorWindow`.

It is:

- runtime-first and scene-embedded
- grid-based and asset-backed
- still valid for runtime-scene-embedded editing flows
- **not** the primary package authoring entry point
- updated to compile against the `StepState` model (Phase 6 compile-fix); exposes trigger-mode editing only

It is not deprecated. It is reclassified as a secondary tool.
Deprecation should only happen after `DrumPatternEditorWindow` is validated in production use.

### C. Supporting package editor infrastructure

The package also contains smaller editor/tooling components such as:

- dropdown drawers
- asset-specific custom editors
- repository/store abstractions for pattern/config assets

These are reusable building blocks available for future editor work, including Phase 8
persistence cleanup.

## 4. Package-owned vs cross-project-owned tools

A tool belongs in MidiGenPlay package docs when it authors or edits package-owned musical assets.

Examples:
- chord progression assets
- drum pattern assets
- future package-owned melody/composition tooling

A tool belongs in cross-project reference when it exists mainly to support game-specific
runtime/session workflows.

Examples:
- composition session/gameplay bridge UIs
- game-specific card runtime tools
- live gameplay-facing managers such as `MidiMusicManager`

## 5. Current rhythm tooling truth

`DrumPatternEditorWindow` is the primary rhythm authoring entry point.
It currently supports:

- lane/instrument editing
- step on/off editing (trigger mode)
- row-local velocity editing (velocity mode) with `[T]`/`[V]` toggle per lane
- `TimeSignature`-driven signature control
- measure and subdivision control
- safe normalize/rebuild on signature change
- apply and save-as flows for `DrumPatternData`

The persisted data model is `List<StepState>` per lane, where each `StepState` carries
`bool active` and `int velocity` (0 = defer to lane default).
See `authoring/SSoT_Authoring_Rhythm_Patterns.md` for the full data-model contract.

**Asset-truth vs runtime-consumption gap**: the asset now carries per-step velocity,
but the current runtime (`ComposeFromGrid`) still consumes via `SnapshotAsIndices`,
which returns lane default velocity for all active steps. Closing this gap is a
deferred runtime decision (see `runtime/SSoT_Composer_Rhythm_Track.md` Section 3B).

## 6. Next rhythm tooling target (Phase 7)

Text / DSL mode for rhythm authoring — fast textual lane sketching that parses into
the canonical grid/lane model without discarding it.
This is planning, not current truth. See `planning/active/Roadmap_Rhythm_Authoring_MVP.md`.

## 7. Data-model note

The persisted rhythm step model was promoted from `List<bool>` to `List<StepState>`
in Phase 6. Existing `.asset` files serialized with the old model will deserialize
lane step arrays as empty lists on first load and must be re-authored or migrated
manually via `DrumPatternEditorWindow`.

## 8. Relationship to runtime docs

Tool docs explain how assets are authored and edited.
Runtime SSoTs explain how resulting assets are consumed.

Do not duplicate runtime behavior here unless it changes the meaning of the authored asset.

## 9. Current sequencing principle

For the rhythm subsystem, current package sequencing is:

1. ~~authoring clarity and editor/tool consolidation~~ — done (Phases 4–5)
2. ~~row-local velocity view and data-model extension~~ — done (Phase 6)
3. text/DSL mode for rhythm authoring (Phase 7, optional for first dedicated editor release)
4. persistence/repository cleanup (Phase 8)
5. phrasing / feel semantic enrichment (Phase 9)

## 10. Update triggers

Update this SSoT when:

- the package adopts a new authoring-tool pattern
- a new package-owned editor window becomes canonical
- `DrumPatternEditorWindow` capabilities or known limitations change
- the save/preview/normalize model changes
- `RhythmPatternPanelController` is formally deprecated
- the runtime consumption of per-step velocity is closed (update asset-truth vs runtime-consumption gap note)

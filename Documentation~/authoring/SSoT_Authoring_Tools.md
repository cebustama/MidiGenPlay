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

Three tools currently hold Category A status as dedicated, scene-independent package
authoring entry points (the third, the Melody Pattern Editor, currently authors
pattern data that is not yet runtime-consumed — see its entry for scope):

#### `ChordProgressionEditorWindow`

The original package reference implementation. Demonstrates:

- dedicated editor-window UX
- target asset binding with working copy isolation
- multiple authoring modes (Roman string and grid)
- normalize / preview / apply / save flow
- separation between authoring input and persisted structure

#### `DrumPatternEditorWindow`

The dedicated rhythm authoring entry point, implemented in Phase 5 and extended in
Phases 6 and 7. Follows the same architectural pattern as
`ChordProgressionEditorWindow`, adapted for row-based drum pattern editing.

Capabilities:

- opens via `MidiGenPlay / Drum Pattern Editor...`
- scene-independent: no runtime MonoBehaviour wiring required
- `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
- deep-clone / working-copy isolation: asset is never mutated until Apply or Save As
- lane management and instrument selection per lane
- default velocity per lane via inline int field
- step toggle grid with visual bar boundaries
- row-local `[T]` / `[V]` mode toggle per lane (Grid mode only):
  - **Trigger mode** (`[T]`, default): boolean step on/off, preserves per-step velocity on toggle-off
  - **Velocity mode** (`[V]`): per-step int fields; `[clr]` resets overrides to 0 (defer to lane default)
- whole-window **Grid / Text** tab toggle at the top of the lane area (Phase 7):
  - **Grid** (default): the row-based authoring surface described above
  - **Text**: one drum-machine glyph string per lane; parsed on tab-switch and on
    Apply / SaveAs; per-cell diff preserves non-canonical per-step velocity for
    cells whose typed glyph hasn't changed
  - Syntax authority: `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A
    "Text mode (Phase 7)"
- **LLM-assisted generation** (Batches L1–L3): prompt-to-pattern Generate /
  Regenerate, clipboard Import of an LLM-shaped setup-card + DSL payload, lane-alias
  resolution, and a pre-network prompt cost cap. Async and non-blocking; output is
  committed to the working copy and applied through the existing Apply/Save flow.
  - Contract + architecture authority: `authoring/SSoT_Authoring_LLM_Generation.md`
- safe signature normalize/rebuild (resizes lane step arrays without data loss where possible)
- Apply To Asset and Save As New Asset flows

Current limitations (known, not blocking):

- style textures created on first draw are not explicitly cleaned up on window close (low risk for editor windows)
- unsaved new patterns are lost on domain reload if no asset is assigned
- row view mode (`[T]`/`[V]`) is editor UI state only — not persisted in the asset, resets on domain reload or asset rebind
- text-mode input buffer (`_textRows`) is editor UI state only — not persisted in
  the asset; survives domain reload within the session, cleared on asset rebind
- text-mode is lossy on render when per-step velocities fall outside the three
  glyph tiers (default / accent / ghost); the asset value remains canonical
  until the user explicitly types a different glyph in that cell

#### `MelodyPatternEditorWindow`

The dedicated melody authoring entry point, implemented in Phase 2 of
`planning/active/Roadmap_Melody_Authoring_MVP.md` (closed 2026-06-16). Follows the
same architectural pattern as the chord and drum editors, adapted for a
scale-degree "ladder" note grid.

Capabilities:

- opens via `MidiGenPlay / Melody Pattern Editor...`
- scene-independent: no runtime MonoBehaviour wiring required
- `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
- deep-clone / working-copy isolation: asset is never mutated until Apply or Save As
- ladder grid: Y = 7 diatonic scale-degree rows (I–VII) × octave bands, X = time steps
- click to place / select notes; right-click to delete; a per-note selection
  inspector for degree, octave offset, start step, length (steps), and velocity
- configurable visible octave window that auto-fits to cover all notes on load;
  notes outside the window or beyond the current measure count are preserved (not
  deleted) and surfaced as a hidden-note count
- explicit Normalize (snap notes to the current subdivision grid)
- Apply To Asset and Save As New Asset flows
- grid authoring semantics authority:
  `authoring/SSoT_Authoring_Melody_Composition.md` §5 ("Grid authoring semantics (Phase 2)")

Status / scope (Phase 2):

- this window authors `MelodyPatternData` only and makes **no runtime changes**;
  its output is **not yet consumed at runtime** — the `MelodyTrackComposer`
  pattern-override path (`ComposeFromPattern`) is Phase 4 (see §3.D and
  `runtime/SSoT_Composer_Melody_Track.md`)
- there is **no generation-parameters section / generator** yet — the top section
  of the wizard is Phase 3 (see §3.D)
- there is **no text/DSL mode** (a rhythm/chord-only feature); the analogous melody
  import path is MIDI-file → scale-degree, deferred to Phase D1

Current limitations (known, not blocking):

- the grid fits to the window width, so cells shrink at high step counts
- unsaved new patterns are lost on domain reload if no asset is assigned
- the visible octave window and the current selection are editor UI state only —
  not persisted in the asset
- drag-to-resize notes is deferred to Phase 5 polish; duration is edited via the
  inspector "Length (steps)" field

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
- pure-function authoring helpers (e.g. `MidiGenPlay.Authoring.DrumPatternTextParser`)

These are reusable building blocks available for future editor work, including Phase 8
persistence cleanup.

### D. Melody Pattern Editor — remaining planned phases

The **Melody Pattern Editor** has landed its editor-window shell and scale-degree
"ladder" note grid (Phase 2 of
`planning/active/Roadmap_Melody_Authoring_MVP.md`, closed 2026-06-16) and is now a
Category-A tool — see its entry in §3.A. Two parts of the planned wizard remain
**not yet implemented**:

- **Generation-parameters section + simplified generator (Phase 3).** The top
  section of the wizard surfacing `MelodyGenerationParamsSO` (Tier-1 params:
  scale/tonality, GM instrument hint, density, octave range, rhythmic style) plus a
  "Generate" button driving an editor-only simplified generator into the working
  copy. Contract: `authoring/SSoT_Authoring_Melody_Composition.md` §5
  (`MelodyGenerationParamsSO` is a generation-time aid only, never read at runtime).
- **Runtime consumption (Phase 4).** A `MelodyTrackComposer.ComposeFromPattern`
  branch that consumes an authored `MelodyPatternData` at runtime (resolving scale
  degrees to absolute pitch against the active tonality/root), analogous to the
  rhythm `ComposeFromGrid` path. Until this lands, authored melody patterns are not
  consumed by any composer. Authority: `runtime/SSoT_Composer_Melody_Track.md`.

> Asset-reset caveat (mirrors §7): the Phase-1 `MelodyPatternData` redesign
> changed the serialized note shape, so pre-existing melody `.assets` deserialize
> their note data as empty on first load and must be re-authored via the wizard.

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
- whole-window Grid / Text tab toggle with per-lane text DSL authoring (Phase 7)
- `TimeSignature`-driven signature control
- measure and subdivision control
- safe normalize/rebuild on signature change
- apply and save-as flows for `DrumPatternData`

The persisted data model is `List<StepState>` per lane, where each `StepState` carries
`bool active` and `int velocity` (0 = defer to lane default).
See `authoring/SSoT_Authoring_Rhythm_Patterns.md` for the full data-model contract.

**Asset-truth vs runtime-consumption alignment**: the asset carries per-step velocity,
and runtime consumption matches it. `ComposeFromGrid` calls `SnapshotAsStepVelocities()`,
so per-step velocity reaches generated MIDI; `SnapshotAsIndices()` remains available as a
default-velocity-only view but is no longer called by any runtime composer (closed
2026-05-23; see `runtime/SSoT_Composer_Rhythm_Track.md` §3B / §6).

_(2026-07-05 correction: this note previously described the gap as still open —
"the current runtime (`ComposeFromGrid`) still consumes via `SnapshotAsIndices`." That
was stale. The runtime SSoT and `CURRENT_STATE.md` both show the gap closed 2026-05-23.
Documentation-only fix; no runtime behavior changed by this edit.)_

## 6. Pattern-asset persistence (Phase 8 — closed 2026-07-05, PATTERN-PERSIST-1)

All three package pattern editors — `DrumPatternEditorWindow`,
`ChordProgressionEditorWindow`, and `MelodyPatternEditorWindow` — persist through
the shared generic store `TrackPatternConfigStoreResources<T>`
(`Runtime/CoreScripts/Services/`) rather than ad-hoc `AssetDatabase` calls with
per-window hardcoded folders. Each editor instantiates the store with its type
folder (`"Drums"` / `"Chords"` / `"Melodies"`), which resolves the canonical save
root `Assets/Resources/ScriptableObjects/Patterns/<TypeFolder>`:

- Drum's save root is unchanged (`.../Patterns/Drums`).
- Chord gained a real default save folder for the first time (`.../Patterns/Chords`);
  previously its Save dialogs passed no default folder at all.
- Melody's write folder realigned from a singular `.../Patterns/Melody` to the plural
  `.../Patterns/Melodies` that `PatternRepositoryResources` reads and the shipped
  assets live in.

Division of responsibility: the editor window keeps ownership of the interactive
`SaveFilePanelInProject` naming dialog and its `Undo.RecordObject` calls; the store
owns the `AssetDatabase` write. For a new asset the window passes the dialog-chosen
path to the store's editor-only `PersistNewAtPath(instance, path)` (so the dialog is
preserved *and* the write routes through the store); in-place applies call `Save`.
`IPatternRepository` / `PatternRepositoryResources` remain the runtime **read** path
and were not extended. Each editor also exposes an additive, canonical-root "Browse
Saved Patterns" list backed by the store's `GetAll()` / `Refresh()`.

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
3. ~~text/DSL mode for rhythm authoring~~ — done (Phase 7)
4. persistence/repository cleanup (Phase 8, next active)
5. phrasing / feel semantic enrichment (Phase 9)

## 10. Update triggers

Update this SSoT when:

- the package adopts a new authoring-tool pattern
- a new package-owned editor window becomes canonical
- `DrumPatternEditorWindow` capabilities or known limitations change
- the save/preview/normalize model changes
- `RhythmPatternPanelController` is formally deprecated
- the runtime consumption of per-step velocity is closed (update asset-truth vs runtime-consumption gap note)
- the package adds a new test assembly (cross-reference
  `reference/package/Tests_Authoring_HowTo.md`)

# SSoT — Authoring Rhythm Patterns

## Scope

This document is the primary authority for package-owned rhythm pattern authoring.
It covers:

- `DrumPatternData`
- current rhythm pattern lane/grid semantics
- the current dedicated package-owned authoring window and the legacy panel it supersedes
- legacy textual compatibility only where it still affects persisted assets or runtime fallback
- the boundary between current authoring truth and future editor-window planning

It does **not** define every future UI detail as current package truth.
Planned UI targets belong secondarily to:

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- `authoring/SSoT_Authoring_Tools.md`

## 1. Current authoring mental model

Rhythm authoring currently produces reusable package-owned drum pattern assets that can be consumed by runtime generation.

The current canonical persisted model is:

- one global timing grid per pattern
- a fixed number of measures
- one beats-per-measure value for the asset, derived from `TimeSignature`
- one subdivision value for the asset
- multiple lanes, each mapped to a percussion instrument
- `StepState` step activation per lane (see Section 2)
- one default velocity per lane, used when a step carries no explicit velocity override

That model is package truth today.

## 2. Canonical persisted asset

`DrumPatternData` is the current package-owned canonical persisted rhythm asset.

### Code-backed semantics visible today

`DrumPatternData` currently stores:

- `Measures` (inherited from `PatternDataSO`)
- `TimeSignature` (inherited from `PatternDataSO`)
- `beatsPerMeasure` (derived from `TimeSignature` at authoring time)
- `subdivisions`
- `lanes`
- lane instrument mapping (`GeneralMidiPercussion`)
- lane `defaultVelocity`
- lane `steps` as `List<StepState>`
- legacy compatibility fields such as `pianoRollPattern`

### StepState model

`StepState` is the current canonical per-step representation:

```
[Serializable]
struct StepState {
    bool active;
    int  velocity;   // 0 = defer to lane defaultVelocity; 1–127 = explicit override
}
```

Sentinel contract: `velocity == 0` means "use lane `defaultVelocity`". This is the
canonical way to represent a step that is active at the lane's default loudness without
bloating the asset with redundant velocity data. Setting `velocity` to 0 on an inactive
step is equivalent to `StepState.Off`.

Effective velocity resolution for any active step:

```
effectiveVelocity = (step.velocity > 0) ? step.velocity : lane.defaultVelocity
```

This is implemented in `StepState.ResolveVelocity(int laneDefault)`.

### Migration note for existing assets

The `List<bool>` model used before Phase 6 is **not** Unity-serialization-compatible
with `List<StepState>`. Existing `.asset` files serialized with the old model will
deserialize lane step arrays as empty lists on first load. This is a known,
accepted consequence of the data-model promotion. Assets must be re-authored or
migrated manually via `DrumPatternEditorWindow` before they carry valid step data.

## 3. Current authoring tools

### 3A. Dedicated package-owned editor window (primary)

The primary rhythm authoring entry point is `DrumPatternEditorWindow`.

It is a Unity Editor window (no runtime scene required) that follows the validated
package authoring pattern established by `ChordProgressionEditorWindow`.

#### Capabilities

- opens via `MidiGenPlay / Drum Pattern Editor...`
- accepts a `DrumPatternData` target asset via an object field
- on bind: deep-clones the asset into a working copy; edits never touch the asset directly
- timing controls: `TimeSignature` enum (drives `beatsPerMeasure`), `Measures`, `Subdivisions`
- lane management: add lane, remove lane per row, remove last lane
- instrument selection per lane via `GeneralMidiPercussion` popup
- default velocity per lane via inline int field
- safe normalize/rebuild: signature changes resize lane step arrays without data loss where possible
- **Apply To Asset**: overwrites target asset with working copy, re-binds on completion
- **Save As New Asset**: saves working copy to a new `.asset` file, points window at result
- **New Pattern**: creates an unsaved working copy not tied to any asset

#### Row-local velocity view (Phase 6)

Each lane row has a `[T]` / `[V]` mode toggle button at its left edge:

- **Trigger mode** (`[T]`, default): step buttons behave as boolean toggles.
  Active steps shown in green, inactive in dark. Toggling off a step preserves its
  existing per-step velocity value so it is not lost if the step is re-activated.
- **Velocity mode** (`[V]`): each step cell shows an integer field.
  - Value displayed: the explicit per-step velocity if non-zero; otherwise the lane
    default velocity (as a visual hint of what will play).
  - Setting a field to a value > 0 activates the step with that explicit velocity.
    If the entered value equals `defaultVelocity`, the stored override is set to 0
    (sentinel) to keep assets clean.
  - Setting a field to 0 deactivates the step (`StepState.Off`).
  - A `[clr]` button at the row end resets all per-step velocity overrides to 0
    (defer to lane default) without changing which steps are active or inactive.

Row view mode is editor-only UI state and is not persisted in the asset.
It resets to Trigger mode on domain reload or asset rebind.

#### Normalize / apply / save contract

1. All edits are made to the working copy (`_working`), a deep clone of the target asset.
2. The target asset is never mutated until the designer explicitly presses Apply or Save As.
3. Apply overwrites the target asset in place and re-binds.
4. Save As creates a new asset at a designer-chosen path and re-binds to it.
5. Signature changes are applied deferred (one frame) to avoid mid-draw resize artifacts.

### 3B. Legacy runtime-scene panel (secondary, not deprecated)

`RhythmPatternPanelController` + `PatternGrid` + `PatternGridCell` + `RhythmRowHeader`
remains a valid authoring path for runtime-scene-embedded editing flows.

It is no longer the primary package-owned authoring entry point, but it is not deprecated.
It continues to serve use cases where the editor is embedded in a runtime scene UI.

Key distinction from `DrumPatternEditorWindow`:

- it requires scene wiring and a MonoBehaviour lifecycle
- it saves directly through `UnityEditor` calls inside `#if UNITY_EDITOR` guards, not through a dedicated editor-window flow
- it is the behavioral MVP that `DrumPatternEditorWindow` was built from
- it exposes only trigger-mode editing (no velocity view)

## 4. Current boundaries and limitations

### What is already true

- `DrumPatternEditorWindow` is the primary scene-independent authoring entry point.
- The working copy / apply / save-as contract is explicit and documented above.
- `TimeSignature` drives `beatsPerMeasure`; they are not set independently.
- Per-step velocity is now canonical persisted truth via `StepState`.
- Row-local velocity view is implemented in `DrumPatternEditorWindow`.
- The current runtime handoff is clear: `DrumPatternData` → `RhythmTrackComposer`.
- Runtime may normalize the authored pattern to the Part meter, but authoring still defines the musical content.
- `SnapshotAsIndices()` returns lane `defaultVelocity` per lane for existing runtime callers (behavior unchanged).
- `SnapshotAsStepVelocities()` is available as the per-step-velocity-aware forward snapshot.

### What is **not** true yet

The following are **not** current persisted truth:

- row-local "velocity edit mode" stored canonically in the asset (it is editor UI state only)
- mandatory text-mode / row DSL authoring
- true per-row polymeter persisted in the runtime asset model
- store-backed persistence routed through package repository abstractions (Phase 8)
- `ComposeFromGrid` in `RhythmTrackComposer` consuming per-step velocity
  (it currently uses `SnapshotAsIndices` with lane `defaultVelocity`; upgrading to
  `SnapshotAsStepVelocities` is a future runtime change)

## 5. Planned UI target (not current persisted truth)

The Phase 6 velocity view is now implemented. No further Phase 6 work is open.

The next planned authoring milestone is Phase 7 (text/DSL mode) and Phase 8
(package store/repository persistence integration). See the roadmap for details.

## 6. Relationship to `RhythmCardConfigSO`

`RhythmCardConfigSO` is currently the concrete rhythm-oriented style bundle used by runtime.
Its current authoring-relevant fields include:

- `patternOverride`
- `recipeOverride`
- `styleIdOverride`
- phrasing / feel fields such as fill cadence and density controls

This is a valid current package-facing surface, even if the naming still reflects card-originated history.

The canonical persisted rhythm pattern asset remains `DrumPatternData`.

## 7. Runtime handoff

Runtime consumption, precedence and meter normalization rules live in:

- `runtime/SSoT_Composer_Rhythm_Track.md`

This document defines the authored asset side.
Runtime docs define how that asset is interpreted and adapted during generation.

Note: `RhythmTrackComposer.ComposeFromGrid` currently calls `SnapshotAsIndices()`
and uses lane `defaultVelocity` uniformly per lane. The new `SnapshotAsStepVelocities()`
method is available for a future runtime update that consumes per-step velocity.
That update is out of scope for Phase 6.

## 8. Current package priority

Phase 5 and Phase 6 are now complete.
The current package sequencing is:

1. ~~consolidate rhythm authoring UX and contracts~~ — done (Phase 4)
2. ~~build the dedicated rhythm editor/tooling milestone~~ — done (Phase 5)
3. ~~implement row-local velocity view and required data-model extension~~ — done (Phase 6)
4. text/DSL mode for row authoring (Phase 7, optional before larger vision)
5. package store/repository persistence integration (Phase 8)
6. only afterwards close phrasing / feel semantics as a later variation layer (Phase 9)

See also:

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- `CURRENT_STATE.md`

## 9. Update triggers

Update this SSoT when:

- `DrumPatternData` semantics change
- `StepState` model or sentinel contract changes
- `DrumPatternEditorWindow` capabilities or contract change
- `SnapshotAsStepVelocities` is consumed by runtime (update Section 4 and Section 7)
- rhythm bundle authoring fields change in a way that alters pattern authoring meaning
- grid/text compatibility rules change
- row-local cycle / polymeter policy is promoted from planning into package truth
- `RhythmPatternPanelController` is formally deprecated

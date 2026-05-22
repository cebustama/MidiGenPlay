# SSoT — Runtime Song Model and Config

## Scope

This document is the primary authority for the package-owned runtime song model:

- `SongConfig`
- `PartConfig`
- `TrackConfig`
- `PartSequenceEntry`
- `TrackParameters`
- `RhythmRecipe`
- `BackingRecipe`
- `SongConfigManager`

## 1. Mental model

MidiGenPlay runtime song state is represented by a `SongConfig` object.

A song contains:

- `Parts`
- `Structure` entries that refer to parts by index and number of repetitions

Each `PartConfig` contains:

- musical identity (`Name`, `Tonality`, `RootNote`)
- time identity (`TimeSignature`, `Measures`)
- a list of `TrackConfig`

Each `TrackConfig` contains:

- `Role`
- performer/channel/instrument identity
- `TrackParameters`

## 1.1 Transient one-shot composer hints on `PartConfig`

`PartConfig` carries a small set of transient, one-shot composer hints in
addition to its serialized song-state fields. These hints are:

- not serialized,
- not part of persisted song state,
- written by upstream effects (such as a `PartEffect` in a consuming project)
  immediately before a render,
- consumed by the relevant composer on entry and cleared in the same call so
  they apply to exactly one render.

The canonical example is the directional modulation hint consumed by
`ChordTrackComposer`:

- `PartConfig.PreviousRootNote : NoteName?`
- `PartConfig.ModulationOctaveHint : MidiGenPlay.Composition.ModulationOctaveHint`

Composer-side behavior is defined in `runtime/SSoT_Composer_Backing_Track.md §6`.

Determinism contract: because these transients are inputs visible at the moment
of composer entry, the deterministic-under-seed contract is preserved. A
consumer that writes the transients deterministically before each render sees
deterministic output; a consumer that omits them sees default behavior.

## 2. `TrackParameters` is the runtime extension surface

`TrackParameters` is the package-owned cross-role input surface for extra generation data.

Current important fields:

- `Pattern` — asset/runtime pattern input
- `RhythmRecipe` — rhythm-specific procedural configuration
- `BackingRecipe` — backing-specific configuration
- `Style` — a `TrackStyleBundleSO`-derived bundle for role-specific authoring/runtime inputs

This is a package contract. External projects may provide concrete bundles, but that does not change the package-level meaning of `TrackParameters`.

## 3. `SongConfigManager` responsibilities

`SongConfigManager` is the single runtime-friendly owner of mutable **package-side** song configuration state.

Its responsibilities include:

- owning the live `Song`
- tracking active part and active track
- creating/removing parts and tracks
- mutating part signature, tempo-related inputs, and track parameters
- parsing/replacing structure
- exposing events so UI/runtime callers react to state changes instead of mutating the model directly

This means UI panels should talk to the manager, not mutate `SongConfig` directly.


## 3.1 Package-side runtime truth after handoff
`SongConfig` + `SongConfigManager` are the package-side runtime truth **after a caller has built and handed off song input into MidiGenPlay**.

This must not be misread as replacing a caller project's own game-side editable/session truth.
In ALWTTT, for example, the editable/session truth before handoff lives on the game side (`SongCompositionUI` + `CompositionSession`), while MidiGenPlay becomes authoritative only for the package-side runtime song representation after the handoff.

## 4. Part/track state invariants

- `SongConfig` is the current **package-side** runtime source of truth for composition inputs after handoff/build.
- Tracks exist under parts; they are not global.
- `TrackParameters` may be null in legacy or partially configured states, but runtime-facing code should treat it as the location for extensible per-track inputs.
- Structure references parts by index, not by embedded copies.

## 5. Recipes vs patterns vs style bundles

These concepts are related but distinct:

- `Pattern` — direct authored rhythmic/melodic/harmonic pattern data asset
- `RhythmRecipe` / `BackingRecipe` — procedural or style selection hints/config
- `Style` — role-specific bundle that can carry richer authoring/runtime inputs

Do not collapse them into one concept in documentation.

## 6. Documentation boundary notes

This document intentionally does not define:

- ALWTTT gameplay card semantics,
- composition session bridge behavior,
- live playback cache semantics in `MidiMusicManager`.

Those live elsewhere or in cross-project reference.

## 7. Update triggers

Update this SSoT when any of the following change:

- `SongConfig` field semantics
- `TrackParameters` meaning
- `SongConfigManager` responsibilities or event model
- runtime ownership of part/track state
- transient one-shot composer hints on `PartConfig` (add, remove, or change semantics)

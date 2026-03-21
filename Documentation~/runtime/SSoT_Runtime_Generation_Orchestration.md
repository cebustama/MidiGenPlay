# SSoT — Runtime Generation Orchestration

## Scope

This document is the primary authority for MidiGenPlay generation and orchestration flow:

- `MidiGenerator`
- `SongOrchestrator`
- `ComposerFactories`
- `GenContext`
- composer registration and render flow

## 1. High-level flow

The package builds MIDI through a runtime pipeline roughly shaped like this:

1. A configured `SongConfig` exists.
2. `MidiGenerator` prepares role-specific composer factories.
3. `SongOrchestrator` walks the song structure and parts.
4. For each track, an `ITrackComposer` is chosen and executed.
5. Generated track files are merged with metadata and optional metronome.
6. The resulting `MidiFile` is returned to the caller.

## 2. `MidiGenerator` responsibilities

`MidiGenerator` is the high-level entry point that:

- registers composer factories by `TrackRole`,
- builds the orchestration context,
- exposes helper methods for generation,
- and centralizes preflight/debug visibility.

It is the package-facing entry, not the per-track implementation surface.

## 3. `SongOrchestrator` responsibilities

`SongOrchestrator` is the runtime coordinator.

Its responsibilities include:

- stamping tempo and time signature metadata
- iterating parts/structure
- deciding track channels
- calling the appropriate track composer
- merging per-track MIDI output
- optionally adding metronome output
- preserving the part meter as the authoritative time basis

## 4. `ComposerFactories` responsibilities

`ComposerFactories` resolve which concrete composer to instantiate for a track role.

This keeps role-selection logic separate from orchestration and from composer-specific implementation details.

## 5. `GenContext`

`GenContext` carries generation-scoped inputs such as:

- tempo map and timing helpers
- RNG/seeded context
- helper delegates/services used by composers
- cross-composer coordination inputs

This context should be treated as orchestration-owned runtime state, not as a gameplay/session bridge.

## 6. Meter and timing rule

For package runtime rendering, the **Part time signature is authoritative**.

That rule matters for:

- metronome generation
- loop sizing
- normalization of authored data
- chord/rhythm pattern adaptation

## 7. What this document does not define

This SSoT does not define:

- detailed backing/rhythm/melody composer internals,
- ALWTTT composition-session bridge logic,
- `MidiMusicManager` playback/cache semantics.

See the relevant composer SSoTs or cross-project reference docs.

## 8. Update triggers

Update this SSoT when:

- orchestration stages change,
- composer registration or selection changes,
- `GenContext` meaning changes,
- meter/loop/render contracts change.

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

### 5.1 Seed threading (caller-supplied seed) — MGP-ALWTTT-SEED-1

Both render entry points accept an optional caller-supplied seed:

- `GenerateSong(SongConfig song, int? seedOverride = null)`
- `GenerateSinglePart(part, rolesForChannels, partIndex, bpmOverride, instrumentOverrides, int? seedOverride = null)`

The contract:

- **Base-seed resolution happens exactly once per render call:**
  `baseSeed = seedOverride ?? settings.defaultSeed`.
- **Every derived seed comes from the base seed** — the per-part / per-repetition
  `GenContext.rng` and every per-track RNG (including the RNG stream that drives
  palette selection through the shared `PaletteSelector`). There is no seed site
  in the orchestrator that bypasses the base seed.
- **Seed policy is host-side.** When the seed changes and what it derives from
  (per-song, per-render, per-anything) is the caller's decision. The package
  never invents per-render entropy on its own; it only consumes what it is
  given. (Determinism invariant: same inputs + same seed => same outputs.)
- **Backward compatibility is bit-exact.** When no seed is supplied, all derived
  seed strings and arithmetic are identical to the pre-SEED-1 behavior
  (`defaultSeed`-anchored). This is guarded by golden-value regression tests in
  `Tests/Editor/SongOrchestratorSeedTests.cs` (FNV-1a goldens captured against
  the pre-batch seed-string formats).
- **Derivation seams are internal, testable functions** on `SongOrchestrator`:
  `ResolveBaseSeed`, `ResolveRepContextSeed` (`(base + partIndex*397) ^ rep`),
  `ResolvePartContextSeed` (`base + partIndex*397`), `ResolveTrackSeedSong`,
  `ResolveTrackSeedPart` (both FNV-1a over the legacy seed-string formats).
- Consumer projects reach this surface through their render bridge (e.g.
  `MidiMusicManager.RenderSinglePart` forwards `seedOverride`); the bridge is a
  pure pass-through and holds no seed policy either.

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
- meter/loop/render contracts change,
- the seed-threading contract (§5.1) changes: new seed sites, a change to base-seed
  resolution, or any move of seed policy into the package.

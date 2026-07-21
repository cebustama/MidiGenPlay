# SSoT_CONTRACTS

This document defines cross-cutting documentation and architecture contracts for MidiGenPlay.

## 1. Package boundary contract

MidiGenPlay package truth covers:

- song model and part/track configuration,
- track parameters and package-owned style bundle abstractions,
- generation orchestration,
- role-specific composers,
- package-owned authoring assets and authoring tools.

It does not automatically cover:

- ALWTTT gameplay semantics,
- live composition session gameplay bridge,
- `MidiMusicManager` behavior on the game side,
- card economy/combat/status systems,
- legacy `CardData` redesign material.

## 2. Runtime vs authoring vs integration contract

### Runtime
Describes what the package executes and how generated MIDI is produced.

### Authoring
Describes how package-owned assets and editor tooling define inputs consumed by runtime.

### Integration
Describes how an external project injects or consumes package data. Integration docs are secondary unless explicitly promoted.

## 3. Song model contract

`SongConfig` is the runtime song model authority.

Key invariants:

- a song is composed of `Parts`,
- parts own meter, tonality, root and track lists,
- tracks own role, channel/instrument identity and `TrackParameters`,
- `TrackParameters` are the role-agnostic extension point for package data injection.

## 4. TrackParameters contract

`TrackParameters` is the package-owned extension surface for per-track inputs.

Current cross-role fields of documentary importance:

- `Pattern`
- `RhythmRecipe`
- `BackingRecipe`
- `Style`

The `Style` field is package-owned and points to `TrackStyleBundleSO` or a derived bundle.

Concrete game-specific bundles may be consumed by the package, but they do not redefine package theory.

## 5. Meter authority contract

For runtime normalization and rendering, the **Part time signature is authoritative** unless a future promoted contract explicitly says otherwise.

This rule is especially important for:

- `ChordTrackComposer`
- `RhythmTrackComposer`
- authoring assets that may be reused across signatures

## 6. No silent promotion contract

The following may not become authoritative by accident:

- roadmap files
- research prompts
- cross-project docs
- archive docs

Promotion requires an explicit change in `coverage-matrix.md` and `changelog-ssot.md`.

## 7. Preservation contract

Migration prefers:

- reclassification,
- absorption,
- archive with superseded markers,
- and explicit redirects

over deletion.

## 8. Consumer mix gain contract (MGP-MIX-1)

- No entry ⇒ no event: a render with a null/empty `mixGains` map, or a track
  without an entry, is byte-identical to the pre-MIX-1 render.
- Same seed + same map ⇒ same bytes. The gain path consumes no randomness and
  displaces no draw (stripped-CC7 note-identity is test-pinned).
- Law: `cc7 = clamp(round(volume01 × gain × 100), 0, 127)`; gain 0 or
  volume01 0 mutes without removing note events.
- Keying: `MusicianTrackKey (musicianId, TrackRole)`; Rhythm entries
  warn+ignore in v1.
- `volume01` stays package-side; consumer gain is per-render data, never a
  `SongConfig` field, never an asset override.

## 9. Update completion contract

A technical change is not complete until:

- the primary SSoT is updated,
- `CURRENT_STATE.md` is updated if focus/reality changed,
- `changelog-ssot.md` is updated if meaning/authority changed,
- and `coverage-matrix.md` is updated if the primary home changed.

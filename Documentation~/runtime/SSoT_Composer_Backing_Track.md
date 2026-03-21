# SSoT — Composer Backing Track

## Scope

This document is the primary authority for MidiGenPlay backing/chord track runtime behavior:

- `ChordTrackComposer`
- package-owned backing style/bundle inputs
- use of `ChordProgressionData`
- progression selection and normalization at runtime

## 1. Inputs

Backing track generation may be influenced by:

- `TrackParameters.Style` when it resolves to a backing-oriented bundle
- `TrackParameters.BackingRecipe`
- direct progression overrides
- progression palettes / weighted selection
- part tonality/root/time signature

## 2. Runtime behavior

`ChordTrackComposer` is responsible for turning the selected harmonic source into MIDI chord events.

Important documented behavior:

- can use an explicit progression override,
- can select from palette-driven options,
- can fall back when exact options are absent,
- uses cache/shared selection semantics where appropriate,
- and normalizes or adapts progression timing against the **Part** meter.

## 3. Meter normalization contract

Backing runtime follows the package-wide rule:

- **Part meter is authoritative**
- progression assets are not silently treated as more authoritative than the current part
- adaptation should preserve meaningful bar-relative position rather than mutating authoring assets globally

## 4. Boundary with authoring

This SSoT defines runtime behavior only.

The authoring-side meaning of progression assets, palettes, Roman strings, grid editing and rests is defined in:

- `authoring/SSoT_Authoring_Chord_Progressions.md`

## 5. Boundary with cross-project cards

A concrete backing bundle may be injected by a consuming game, but the package-level truth is:

- runtime consumes backing-oriented data through package-owned inputs and bundle abstractions,
- game-specific card semantics do not redefine backing composer theory.

## 6. Update triggers

Update this SSoT when:

- progression selection rules change,
- fallback behavior changes,
- time-signature normalization changes,
- backing bundle input precedence changes.

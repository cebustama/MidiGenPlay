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
- can select from palette-driven options via the shared, TS-aware `PaletteSelector` / `ProgressionFinder` (Tier A exact-TS → B heuristic → C raw-weights; reflection removed in CE-F1),
- can fall back when exact options are absent,
- uses cache/shared selection semantics where appropriate,
- treats duplicate progression references within a single palette as independent weighted entries (no de-duplication; CE-F1 delta vs. the prior reflection path),
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

## 6. Directional modulation hint (one-shot transient)

The composer honors a one-shot directional hint for the first chord of a render
when the consuming project (typically a `PartEffect` such as ALWTTT's
`ModulationEffect`) sets two transient fields on `PartConfig` before the part
is rendered:

- `PartConfig.ModulationOctaveHint : MidiGenPlay.Composition.ModulationOctaveHint`
- `PartConfig.PreviousRootNote : NoteName?`

Default behavior:

- `ModulationOctaveHint.Auto` (the enum default) plus `PreviousRootNote == null`
  preserves prior behavior bit-identically.
- The composer captures both fields at the start of `Compose` and clears them
  immediately so the hint is consumed exactly once per render, regardless of
  which internal render path runs.

When the hint is `Up` or `Down` and a previous root is provided, the composer
overrides the first chord of the render as follows:

1. The first chord is realized as an ascending root-position stack — inversions
   and Drop-2 are skipped for this chord only.
2. The root octave is chosen as the lowest octave whose root pitch is strictly
   above the previous root (`Up`) or the highest octave whose root pitch is
   strictly below (`Down`). The previous root is anchored at the instrument's
   central octave for the comparison.
3. Chords 2..N continue normal voice leading from the constrained first chord.
   The directional bias is not propagated.

### 6.1 Range-limit fallback

If no octave within the instrument's playable range satisfies the strict
direction, the composer clamps the first-chord root octave to the boundary on
the requested side (top for `Up`, bottom for `Down`) and emits a warning when
`MidiGenPlayConfig.logGenerator` is enabled. The pitch class still changes, so
the modulation is still audible; the directional "lift" is weakened.

### 6.2 Edge case — degree=1 with `Up` or `Down`

If a modulation resolves to the same root note as the previous tonic (for
example `targetDegree == Tonic`) while a non-`Auto` hint is set, the composer
treats this as a deliberate request to bump the register: `Up` lands the first
chord one octave above the previous root anchor (clamped per §6.1); `Down`
lands one octave below. This is preferred over a no-op so that an authored
"Up" request always produces audible motion in the requested direction.

### 6.3 Determinism

The hint is part of the input set. Given the same `SongConfig` (including the
transients at the moment of `Compose` entry) and the same seed, the output is
deterministic. Because the transients are cleared on entry, a second call with
no new modulation request behaves as `Auto` automatically.

### 6.4 Boundary

The two transient fields are package-defined and live on `PartConfig`
(`runtime/SSoT_Runtime_Song_Model_and_Config.md §1.1`). The enum
`ModulationOctaveHint` is package-owned
(`Runtime/CoreScripts/Composition/Data/ModulationOctaveHint.cs`). Consumers
(such as an ALWTTT `ModulationEffect`) write to the transients; the package
consumes them. No consumer is required to use this surface.

## 7. Update triggers

Update this SSoT when:

- progression selection rules change,
- fallback behavior changes,
- time-signature normalization changes,
- backing bundle input precedence changes.

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

## 7. Per-chord inversion hint (voicing pin)

The composer honors a per-chord inversion pin when a consumer sets one
transient field on `PartConfig` before the part is rendered:

- `PartConfig.ChordInversionHints : IReadOnlyList<int?>`

The list is index-aligned to the rendered progression's events. Each entry pins
the voicing of the chord at that event position:

- A `null` entry, a list shorter than the event count, or no list at all means
  that chord is unset: the voicer generates and scores candidates exactly as
  before.
- A value `k` in `0..arity-1` (arity = the chord's voice count) means the voicer
  realizes exactly rotation `k` of the chord's pitch classes (`0` = root
  position, `1` = first inversion, …). Register and spacing remain the voicer's
  responsibility (target-octave choice, `RealizeNear`, and the range clamp are
  unchanged).
- A value outside `0..arity-1` is treated as unset (a safe no-op). It is never
  clamped, so a garbage value cannot silently force root position (D2b = a).

Semantics (batch CQ-A1-OBJ2; decisions D0–D3 plus D2a/D2b):

- **Pin, not bias (D0 = A).** A valid pin yields exactly one candidate — the
  requested rotation — suppressing all alternatives including Drop-2, and
  outranking the `useInversions` / `useDrop2` candidate-set toggles. Pinning
  `0` is therefore *not* equivalent to leaving the chord unset: it forces root
  position.
- **Inversion index, not bass pitch-class (D1 = A).** No figured-bass or slash
  notation enters the Roman DSL or any asset grammar; the pin lives in the
  input set only.
- **Per-chord scope (D2 = A), sticky-per-position (D2a = a).** The pin applies
  at its event position on **every pattern repeat** within the render. It
  addresses the progression's content, which recurs with the pattern — unlike
  the §6 modulation hint, which describes the one-time transition into the
  render.
- **§6 wins on the render's first chord (D3 = A).** When a directional
  modulation hint is active and produces the render's very first chord
  (repeat 0, event 0), the inversion pin is ignored for that one chord only.
  On later repeats, position 0's pin applies normally. This precedence is
  structural in both render loops: when §6 yields the first chord, the voicer
  is never invoked for it.
- The pin applies only on the voice-leading path (`enableVoiceLeading == true`
  with a voicer present). The simple-stack fallback (`RealizeChordSimple`)
  ignores it.

### 7.1 Lifecycle

The field follows the §6 transient lifecycle exactly: `Compose` snapshots it on
entry and clears it immediately, so it is consumed by exactly one render
regardless of which internal render path runs. All chord render paths (inline
card progression, library-selected, and fully procedural) normalize to
progression events, so the index alignment is well-defined everywhere.

### 7.2 Determinism

Default-unset is bit-identical to prior behavior (mirrors §6.3). The pin itself
is RNG-free: a pinned chord yields exactly one candidate, and non-pinned chords
see an unchanged candidate set and scoring order. The hint is part of the input
set: the same `SongConfig` (including the transients at the moment of `Compose`
entry) plus the same seed produces the same output.

### 7.3 Boundary

`PartConfig.ChordInversionHints` is package-defined and lives with the other
transient hints (`runtime/SSoT_Runtime_Song_Model_and_Config.md §1.1`). The pin
is enforced in the voicing layer
(`BasicVoiceLeadingVoicer.GeneratePcCandidates`); `IChordVoicer.VoiceChord`
carries it as an optional trailing parameter (`int? forcedInversion = null`),
so existing callers and alternative voicer implementations compile unchanged.
Consumers write the transient; the package consumes it. No consumer is
required to use this surface.

## 8. Update triggers

Update this SSoT when:

- progression selection rules change,
- fallback behavior changes,
- time-signature normalization changes,
- backing bundle input precedence changes,
- directional modulation hint semantics change (§6),
- per-chord inversion pin semantics change (§7).

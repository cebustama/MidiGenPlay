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

## 2.1 Chord marker contract (`chd:`) — MGP-ALWTTT-DBG

The backing composer stamps one `chd:` text marker **per chord event** (not per
articulation hit) on the rendered track:

    chd:{channel}:{roman}:{symbol}:{deg}:{quality}

- `channel` — the track's MIDI channel (0..15).
- `roman` — the roman numeral (`ToRomanRich(degree, quality)`), accidental-
  prefixed `b` / `#` when `degreeAccidental` is set.
- `symbol` — the concrete chord symbol (`GetChordSymbol`).
- `deg` — 1-based scale degree.
- `quality` — `ChordQuality.ToString()`.

**Both-emission-sites parity.** The two render paths — the grid loop in
`Compose` and `RenderFromProgression` (the procedural render path) — stamp
identical `(tick, text)` marker sequences for the same progression, meter, tempo
and channel. Voicings may differ between paths (RNG-dependent); the markers must
not. `RenderFromProgression` now applies the same accidental handling as the
grid site (root transpose + roman prefix), **guarded on `degreeAccidental != 0`**
so accidental-free progressions (every procedural progression today) stay
MIDI-byte bit-identical. Guarded by
`Tests/Editor/ChordMarkerParityTests.cs`.

The marker is a stable readback/DAW-display contract: field order and the
one-marker-per-event rule must not change without updating this section.

## 2.2 Runtime progression construction and chord-palette enumeration (MGP-ALWTTT-DBG-4+2)

**Runtime progression construction (Ask D, MGP-ALWTTT-DBG-4).** Consumers may
build a `ChordProgressionData` at runtime from a Roman string via
`ChordProgressionRuntimeImporter.TryParseRoman(roman, ts, measures,
defaultDurationMeasures, referenceTonality, out data, out warnings)` (or
`TryParsePayload` for the full setup-card shape; grammar authority:
`authoring/SSoT_Authoring_Chord_Progressions.md` §4.1–4.2). The built instance
is never persisted (`HideFlags.DontSave`); carries `originalInput`, a single
`tonalities` entry (the reference tonality), and a `name` stamped
`"Runtime: <roman>"` so Ask A readback (by pre-clone asset name, D-DBG3=A)
stays meaningful; and is a `PatternDataSO`, so it feeds the Ask C
`patternOverrides` map directly (precedence step 0; clone-on-apply remains the
composer's job, unchanged). Quantization failure and out-of-alphabet suffixes
are hard failures with explanatory warnings — never silent fallbacks. A
declared `measures` that mismatches the durations produces a warning and the
duration-derived value wins (durations define the grid, as in the editor).

**Runtime chord-palette enumeration (Ask B, MGP-ALWTTT-DBG-2).**
`ChordProgressionPaletteSO` assets are enumerable at runtime with
`new TrackPatternConfigStoreResources<ChordProgressionPaletteSO>("Chords")`
→ `Refresh()` / `GetAll()` (`Resources.LoadAll` is recursive and
type-filtered). Canonical folder:
`Resources/ScriptableObjects/Patterns/Chords/Palettes` (mirror of the drum
palettes' folder). Migration note: the shipped `Test Palette.asset` predates
this contract and lived under the legacy
`Resources/ScriptableObjects/Chord Progressions/Palettes`; it must sit under
the canonical folder to be enumerable (GUID references survive the move).
Display metadata per palette: `GetDisplayName()`, entry count, and per-entry
progression metadata (name / `DisplayName` / `originalInput`, `TimeSignature`,
`Measures`, `subdivisions`, `tonalities`).

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

### 7.4 Start-register determinism (VL-DET-1)

The voicer chooses the **first chord's** starting register in
`BasicVoiceLeadingVoicer.TargetOctave`; every later chord steers near the previous
voicing's average octave and is deterministic given the first. Under a random
`VoiceLeadingConfig.StartRegisterMode` (`RandomAroundCenter`,
`Uniform01AroundCenter`), that first-chord draw uses the **part's deterministic RNG**
— `ctx.rng`, threaded in through the optional trailing `System.Random rng` parameter
of `IChordVoicer.VoiceChord` — **not** the global `UnityEngine.Random`. This keeps
those modes reproducible and inside the SEED-1 "sole per-render entropy" contract
(§ Runtime Generation Orchestration): the package never self-generates per-render
entropy, including in the voicer.

Guarantees:
- **Non-random start modes** never reach the random branches; `rng` is not consumed
  and their output is **bit-identical** to pre-VL-DET-1 behavior.
- **Random start modes** draw from the seeded stream, so two renders with the same
  seed and specs (e.g. the editor smoke window and the runtime runner) are
  **byte-identical**.
- A null `rng` (composer callers always pass `ctx?.rng`; only direct test/tooling
  calls omit it) retains the legacy global-RNG path.

`TargetOctave` returns early for all chords after the first, so at most one draw per
track is taken from `ctx.rng`. The per-chord inversion pin path remains RNG-free
(§7.2); this subsection governs only the start-register selection.

### 7.5 Interaction with Tier-2 reshaping (CA-T2, D-T2-PIN=A)

Tier-2 voicing reshapes (§8.6) run AFTER the voicer, so an inversion pin is
applied to the full voiced chord first and the reshape then acts on the pinned
result. Where the reshape removes the pinned voice — a power chord has no third,
so a pin selecting the third's rotation has nothing to invert — the pin is a
no-op on that chord. Where the pin moved the root or fifth, the reshaped power
chord inherits that placement via the already-inverted bass. The pin path itself
stays RNG-free and voicer-owned (§7.2/§7.3); reshaping owns no pin semantics.

## 8. Chord expression / articulation (Tier 1)

CA-T1 adds a post-voicing articulation layer so the same voiced progression
can be rendered with different rhythmic figures. The **articulator** is
strictly Tier 1: rhythm/velocity applied OVER the already-voiced chord. The
articulator never reshapes the voicing and is orthogonal to inversions/Drop-2
(§7) and to the transient hints (§6/§7). **Tier 2** (voicing-*reshaping*
figures) is now built as a SEPARATE pre-articulation seam — see §8.6; it does
its pitch mutation before the articulator runs, so the articulator's
pitch-preserving contract is unaffected.

### 8.1 Selection surface and lifecycle

Selection is a PERSISTENT authored field on the backing card surface
(D-EXP1=A):

- `BackingCardConfigSO.chordExpression : ChordExpressionType` (default `Block`)
- `BackingCardConfigSO.arpeggioRate : ArpeggioRate` (default `Eighth`;
  consumed only by the arpeggio figures)

The field applies to the whole render. It is NOT a transient one-shot hint:
the §6/§7 snapshot-and-clear lifecycle does not apply, and nothing is written
to `PartConfig`. A track with no backing card bundle renders `Block`.

### 8.2 Taxonomy (v1, SD-1=A)

`ChordExpressionType { Block, PerBeat, Offbeat, Staccato, ArpeggioUp,
ArpeggioDown }` (`Runtime/CoreScripts/Composition/Data/ChordExpressionType.cs`).
Member values are serialized in assets and must never be renumbered; Tier 2
extends additively.

- **Block** — one chord struck at the event onset, sustained the full event
  length. Legacy behavior; the bit-identical default (mirrors the §6/§7
  default-unset discipline).
- **PerBeat** — chord re-struck on every meter-anchored integer beat inside
  the event, each hit legato to the next hit / event end. An event starting
  off the beat grid additionally sounds at its onset (a chord change is
  always audible at its onset).
- **Staccato** — the PerBeat grid with each hit capped at 0.5 beat.
- **Offbeat** — ska/reggae upstroke: short (≤0.5 beat) full-chord hits at
  every beat+0.5 inside the event.
- **ArpeggioUp / ArpeggioDown** — voicing notes played one at a time at the
  card's `ArpeggioRate` (`PerBeat`=1, `Eighth`=0.5 (default), `Sixteenth`=0.25
  beats per note), anchored at the event onset, cycling through the
  pitch-sorted voicing (ascending / descending), each note legato to the next
  hit; the final note truncates to the event end. Chord hits always use the
  voicer's note order verbatim; only arpeggio note hits use the sorted copy.

Degrade rule (never-silent invariant): a figure that cannot fit the event —
Offbeat with no offbeat inside the window; an arpeggio on an event shorter
than one hit or on an empty voicing — degrades to a true Block for that
event (legacy emission, including the legacy velocity clamp). No hit ever
overshoots its event window.

### 8.3 Velocity model (SD-3=A, SD-5=A)

Velocity/timing are pure functions of absolute beat position within the Part
meter — the articulator is RNG-free and never consumes `ctx.rng` (consuming
the shared stream would perturb every downstream rng consumer in the same
render). Per hit: `Clamp(round(e.velocity × factor, away-from-zero), 1, 127)`
with factor ×1.00 on bar downbeats, ×0.85 on other integer beats, ×0.80 off
the beat. Block keeps the legacy `Clamp(e.velocity, 0, 127)` untouched.
Seeded jitter is a deferred opt-in extension (would fork a child rng, not
tap `ctx.rng`).

### 8.4 Seam and structural both-sites guarantee

`IChordArticulator` / `ChordArticulator`
(`Composition/Interfaces/IChordArticulator.cs`,
`Composition/Articulation/ChordArticulator.cs`). The composer's BOTH chord
emission sites (grid path in `Compose`; `RenderFromProgression`, which also
serves `ComposeProcedural`) replace the legacy `MoveToTime`+`Chord` pair with
the SAME single unconditional `Emit(...)` call — there is no per-site branch
that can diverge. `Block` inside `Emit` reproduces the legacy pair verbatim;
byte-level identity is pinned by
`Tests/Editor/ChordTrackComposer_ArticulationTests.cs`.

`ChordArticulator.PlanHits` is the internal pure planning seam (the test
surface); `Emit` is a thin PatternBuilder translator. All figure math builds
on the Part-derived `beatSpan`/`beatsPerBar` (meter authority, §5 of
`SSoT_CONTRACTS.md`), never on asset values. The selection is threaded as
parameters through `ComposeProcedural` → `RenderFromProgression` (MOD-DIR-1
pattern); `ITrackComposer` is unchanged. Runtime-only; no editor APIs.
Per-event `chd:` markers are unaffected (one marker per event, not per hit).

The engine is reused by the monophonic bass consumer (CA-F2):
`BassTrackComposer` invokes the same `Emit` seam at its single emission site
with a 1-note voicing. Consumer semantics (card surface, monophonic figure
meaning, the recorded bass meter deviation) live in
`runtime/SSoT_Composer_Bass_Track.md`; this section owns only the engine
contract.

### 8.5 Random selection policy (MGP-ALWTTT-ARTIC-1)

`ChordExpressionType.Random` (member value `6`, appended; values 0..5 are
serialized and unchanged) is a selection-policy SENTINEL, not a figure. It is
resolved composer-side, per chord event, by `RandomArticulationRoller`
(`Runtime/CoreScripts/Composition/Articulation/RandomArticulationRoller.cs`);
the articulator NEVER receives `Random` — if it leaks (e.g. a bassline card
selects it before the bass roll is wired), `ChordArticulator.PlanHits`
degrades it to `Block` (never silent). SD-3=A stands: the articulator itself
remains RNG-free.

**Stream.** The roll draws from a DEDICATED stream seeded by
`SongOrchestrator.ResolveArticulationSeed(ctx.trackSeed)` (FNV-1a over
`"{trackSeed}|artic"`), fully derived from the SEED-1 base seed
(`seedOverride ?? defaultSeed`) and independent of `ctx.rng` — voicing and
progression draws are untouched, so toggling a card Fixed<->Random changes
articulation only, never voicings. Same seed => identical roll sequence =>
bit-identical render (the consumer held-loop replay guarantee). Composers
invoked outside `GenerateOne` see `trackSeed = 0` — still deterministic.

**Granularity (SD-1=A).** One knob, `BackingCardConfigSO.randomRerollChance`
(float 0..1, default 1, clamped): the first chord event always rolls a
figure; every subsequent event draws one gate (`NextDouble`), re-rolling iff
gate < chance. `1` = fresh roll per chord event; `0` = one figure for the
whole render (per-loop variety then comes from the host's per-render
`seedOverride`, §5.1 of the orchestration SSoT); intermediates = per-chord
change probability. Draw discipline is fixed and documented: first event =
1 figure draw; each later event = 1 gate draw + 1 conditional figure draw.
Per-chord rolling cannot fight voice-leading: articulation is post-voicing
(§8 orthogonality).

**Pool (D4=A / SD-2=A; CA-T2 D-T2-POOL=A′).** Default: uniform over exactly the
concrete members with value < `Random` (the six Tier-1 figures, `Block`
included). Tier-2 members (appended after `Random`, value ≥ 7) do NOT enter the
pool AND are NOT admissible via `randomFigureWeights` in v1: `BuildWeightTable`
ignores any entry whose figure value is ≥ `ConcretePoolSize` (== `Random`), so
the roller stays Tier-1-only by construction (the roll selects an articulation
*rhythm*; Tier-2 *reshaping* lives in a different seam, §8.6). Admitting Tier-2
into the roll is deferred. Optional per-card weighted pool
`BackingCardConfigSO.randomFigureWeights : List<ChordExpressionWeight>`
(struct `{figure, weight}` in `ChordExpressionType.cs`): entries DEFINE the
pool (unlisted figures excluded); weight <= 0 excludes; duplicate figures
sum; `Random` entries are ignored; a degenerate list falls back to the
uniform pool with a one-time construction warning (never silent). Figure
picks use one `NextDouble` over the cumulative table (one-draw-per-pick
idiom).

Rolled arpeggio figures consume the card's fixed `arpeggioRate` (D5); the
randomized-rate wish remains deferred on the CA roadmap. Per-event `chd:`
markers are unaffected. Test surface:
`Tests/Editor/ChordTrackComposer_RandomArticulationTests.cs` (seed-seam
goldens, roller determinism/variance in the SEED-1 idiom, knob semantics,
weight-table rules, `PlanHits(Random)==PlanHits(Block)`).

The roller's resolved figure history (`RandomArticulationRoller.History`,
observability-only, no extra draws) is snapshotted into the backing track's
`ResolvedTrackChoice.resolvedFigures` for the Ask A readback (MGP-ALWTTT-DBG-1);
fixed articulation reports null figures.

### 8.6 Tier-2 voicing-reshaping figures (CA-T2)

Tier-2 figures mutate PITCH (not just rhythm), so they cannot live in the
pitch-preserving articulator (§8.3/§8.4). A dedicated pre-articulation seam
`IChordReshaper` / `ChordReshaper`
(`Composition/Interfaces/IChordReshaper.cs`,
`Composition/Articulation/ChordReshaper.cs`) runs between `VoiceChord` and the
articulator's `Emit` at BOTH emission sites, transforming the voiced note list.
Non-Tier-2 expressions (all Tier-1 figures, `Block`, the `Random` sentinel) are
identity — the input voicing is returned unchanged, so every CA-T1 path stays
byte-identical (D-T2-SEAM=B: voicer owns register/inversions/Drop-2, articulator
owns rhythm/velocity, reshaper owns the pitch reduction, each doing exactly one
job).

**Members (D-T2-SCOPE=A: power chord + chugging ship in v1).**
`ChordExpressionType` gains `PowerChord = 7` and `Chugging = 8` (appended after
`Random`; values 0..6 serialized and unchanged; never renumbered).

- **PowerChord** — drops the chord's third to leave root + perfect fifth
  (+ octave), anchored at the root pitch at or below the voicing's bass (so the
  reduction stays in the chord's register regardless of the voicer's inversion
  choice). Rhythm is `Block` (one sustained hit).
- **Chugging** — the same power-chord reshape, re-struck at the card's
  `ArpeggioRate` (D-T2-RHYTHM=A: `arpeggioRate` is overloaded as the chug pulse
  rate — no new field). The pulse is emitted by the articulator's
  pitch-preserving `ChordPulsePlan` (full-chord hits at the arpeggio interval,
  anchored at the event onset; events shorter than one hit degrade to `Block`).

**Composition contract.** The reshaper mutates pitch; the articulator still owns
rhythm. The selected expression is passed straight to `Emit`: the articulator
degrades a leaked `PowerChord` (and `Random`) to `Block` and renders `Chugging`
as the chord pulse, so the articulator never emits a Tier-2 *pitch* while still
carrying the Tier-2 *rhythm* where one exists. Both emission sites keep the
SINGLE unconditional `Emit` (the reshape is a transform on the emitted voicing,
not a per-site branch); `lastVoicing` and the first-chord pitch stash keep the
full harmonic voicing, so voice-leading continuity is unaffected. Reshapes are
deterministic and RNG-free (same discipline as Tier-1). Tier-2 members are
excluded from the §8.5 Random pool (D-T2-POOL=A′).

**Precedence vs §7.** See §7.5 — the reshape runs after the inversion pin.

**Deferred.** The bossa bass/upper split figure is spun out of this batch: it
needs register-selective emission (bass on 1, upper voices off the beat) that
the pitch-preserving Tier-1 articulator does not express; it is tracked on the
CA roadmap.

Test surface: `Tests/Editor/ChordTrackComposer_ArticulationTests.cs` (reshape
drops the third / identity on non-Tier-2 expressions; `ChordPulsePlan`
full-chord pulse count; `PlanHits(PowerChord) == PlanHits(Block)`).

## 9. Update triggers

Update this SSoT when:

- progression selection rules change,
- fallback behavior changes,
- time-signature normalization changes,
- backing bundle input precedence changes,
- directional modulation hint semantics change (§6),
- per-chord inversion pin semantics change (§7),
- chord expression / articulation semantics change (§8),
- Tier-2 voicing-reshaping semantics change (§8.6): the reshaper seam contract,
  the power-chord/chugging reductions, the `arpeggioRate` chug overload, or the
  §7.5 reshape-vs-pin precedence,
- the Random selection policy changes (§8.5): pool rule, draw discipline,
  knob semantics, the articulation-substream derivation, or Tier-2 pool
  admissibility (D-T2-POOL),
- the voicer's start-register RNG source or determinism changes (VL-DET-1: random
  `StartRegisterMode` draws from the part's seeded `ctx.rng` via
  `IChordVoicer.VoiceChord`'s trailing `rng`, not global `UnityEngine.Random`;
  non-random modes and a null `rng` stay bit-identical to legacy),
- the `chd:` marker contract (field order, one-per-event, both-sites parity, or
  accidental handling) changes (MGP-ALWTTT-DBG, §2.1);
- the Random-articulation readback (`resolvedFigures`) meaning changes
  (MGP-ALWTTT-DBG-1, §8.5).

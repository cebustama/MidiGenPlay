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

**Scheduling and the host default (MGP-ALWTTT-BASS-ORDER-1).** The Backing
composer now runs in a dedicated PASS 0, before every harmony consumer,
whatever its position in the track list
(`SSoT_Runtime_Generation_Orchestration.md` §5.7). Two consequences for this
composer, neither of which changes its own resolution logic:

- Its publication is always visible to Bassline / Melody / Harmony. The
  "backing composed first" precondition attached to TS normalization and
  runtime re-qualification is now structurally guaranteed for the shared
  channel.
- A card carrying NO harmony source (no `progressionOverride`, no palette with
  a valid entry, no authored `Pattern`, no per-render override) no longer
  suppresses a host-supplied `defaultProgression`. Such a card reaches step 2
  of its own precedence with the default already in the shared cache and
  consumes it — instead of going procedural. Articulation-only backing cards
  are therefore a supported shape.

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

**Semantics of `tonalities` (TONFILTER-1, D-B2-1=C).**
`ChordProgressionData.tonalities` is DESCRIPTIVE metadata: it records which
modes the progression was authored or imported for (the runtime importer
writes the reference tonality of the Roman parse as its single entry —
provenance, not a filter). The runtime does NOT consult it to decide or to
revert the part's tonality: the tonality is the card's authority. The
supported adaptation for use outside the reference tonality is
`qualityRenderPolicy` (RUNTIME-REQUALITY, §3). When an `AsAuthored`
progression renders in a foreign tonality the composer signals it on
`ResolvedTrackChoice.tonalityMismatch` and, gated by `logGenerator`, in a
warning — it never fails silently and never spends a draw.

**Exception on record — F-B2-LIBRARY.** The legacy procedural template path
`ChordTrackComposer.PickTemplateForPart` — reachable only from
`BuildProceduralProgression` (`ChordTrackComposer.cs:985`) when
`ctx.Settings.progressionLibrary != null` — still DISCARDS library entries whose
allowed list (`entry.compatibleTonalities` when non-empty, otherwise
`progression.tonalities`) excludes the part's tonality
(`ChordTrackComposer.cs:1626–1635`), which B2 left unchanged on purpose
(`planning/active/Roadmap_Composition_Expressivity.md` §B2 outcome: "the legacy
`PickTemplateForPart` is unchanged"), so the "does not filter" contract above
holds for the override, palette and runtime-importer paths, and retiring the
library filter is a RUNTIME candidate — it changes renders — not a
documentation one.

## 2.3 AdoptProgressionTonality (MGP-MEL-1 P4, D3=C / D4=A)

**Surface.** Card-level opt-in `BackingCardConfigSO.adoptProgressionTonality`,
default OFF — with it off, behaviour is byte-identical to pre-batch.

**When it fires.** At step 2a* of the resolution chain — after resolution,
before TONFILTER-1's 2b and requality's 2c — when the resolved progression's
`tonalities` do NOT contain `part.Tonality`. The part then adopts
`tonalities[0]`. Deterministic, zero rng draws; the root is unchanged.

**Guard.** Adoption requires `tonalities.Count > 0`. An entry with an empty
`tonalities` list silently does not adopt. This is the most likely authoring
failure on this path.

**Visibility to consumers.** PASS-0 ordering
(`SSoT_Runtime_Generation_Orchestration.md` §5.7) guarantees bass, melody and
harmony see the adopted tonality.

**Precedence (D4=A).** Compose-time adoption WINS over any pre-render
tonality, including a host `TonalityEffect`. Combining both on one card is an
authoring error that the HOST must validate: the composer cannot distinguish a
default tonality from an effect-pinned one.

**Readback.** `ResolvedTrackChoice.tonalityAdopted` / `.adoptedTonality`,
mutually exclusive with `tonalityMismatch` by construction.

**Interaction with requality.** An adopted render then requalifies (2c)
against the ADOPTED tonality, which makes `DiatonicToPart` on the asset a
near-no-op there.

**Lifetime — and a COMMITTED consumer surface.** The tonality is mutated IN
PLACE on the `PartConfig`. It persists after the card that caused it is gone,
until something else changes it. Restoring a base tonality is HOST policy, not
package behaviour — this is by design, not a leak.

Since MGP-TRIAGE-ALWTTT-R3 this is a COMMITTED surface, not an implementation
detail: a consumer may read `PartConfig.Tonality` after `GenerateSinglePart`
returns and rely on it reflecting any adoption that occurred. A refactor that
composed against an internal copy, or reverted the mutation on exit, would
silently hand consumers the pre-adoption mode and make them generate against the
wrong scale. See `SSoT_CONTRACTS.md` §12.

**Preferred read path.** Consumers should nonetheless prefer the readback:
`PartRender.resolvedByTrack[{musicianId, Backing}].tonalityAdopted` /
`.adoptedTonality`. It is explicit, testable, per-track, and it distinguishes
"adopted to X" from "was already X" — which reading the mutated field cannot.
The in-place mutation is the compatibility guarantee; the readback is the
supported interface.

## 3. Meter normalization contract

Backing runtime follows the package-wide rule:

- **Part meter is authoritative**
- progression assets are not silently treated as more authoritative than the current part
- adaptation should preserve meaningful bar-relative position rather than mutating authoring assets globally

**Application sites and field-copy hazard (RUNTIME-REQUALITY).** Two data-level
transforms run on the runtime clone of a progression, in this order:

1. TS/subdivision reprojection (this section);
2. `ChordProgressionRequality.ApplyDiatonicRequality` against the part's FINAL
   tonality — the PART's tonality, which is card authority and is never
   reverted by the asset's `tonalities` metadata (TONFILTER-1), so the mode
   the listener actually gets is the one the qualities are resolved to.

**The second transform (requality boundary).** It applies the full harmonic
publication pipeline (`ChordProgressionRequality.ApplyDiatonicRequality`, same
entry point): **A** core requality (policy-driven; unchanged) → **B** the color
table (policy + `useColorTable`; REQUALITY-2) → **C** secondary dominants
(per-event opt-in; SECDOM-1, active under ANY policy). Contract order, pinned
by tests: TS/subdivision reprojection FIRST, then A→B→C, materialized in a
single clone-if-changed. The reprojection's field-copy list includes
`useColorTable` and `cadence` (asset) and `hasAppliedTarget` /
`appliedTarget` (event) — hazard F-NORM-DROP, verified in smoke with
`sub x1 → x4`.

Both happen inside the same clone/publication step, so the don't-overwrite
publication guard still compares against the ORIGINAL asset and every
shared-channel consumer (bass, melody) sees the transformed data. The second
site is `SongOrchestrator.TrySeedDefaultProgression` (the backing-less path,
§5.5 of the Orchestration SSoT).

> **Field-copy hazard (F-NORM-DROP, found in verification).** The reprojection
> does NOT clone with `Instantiate`; it builds a fresh `ChordProgressionData`
> and copies fields ONE BY ONE. Any field omitted there silently reverts to its
> default on the runtime clone. `qualityRenderPolicy` was initially omitted,
> which made requality inert for every progression that needs normalization —
> i.e. nearly all of them, since authoring writes `sub x1` and the composer
> normalizes to `x4`. **Any new `ChordProgressionData` field must be added to
> that copy list.** Pinned by
> `ChordProgressionRequalityTests.PolicySurvivesFieldByFieldCloning_NormalizationParity`.
> **The copy list includes `UnityEngine.Object.name`** — see §3.1. It is not a
> serialized field, which is exactly why it was missed for four batches.

### 3.1 Clone identity contract (MGP-TRIAGE-ALWTTT-R3, E3)

**Invariant.** Every runtime clone of a `ChordProgressionData` that reaches the
shared progression channel carries the PRE-CLONE asset name. Consequently
`PartRender.sharedProgressionData.name == PartRender.sharedProgressionAssetName`
on every precedence step: `RenderOverride`, `CardOverride`, `CardPalette`,
`TrackParameters`, `Procedural`, `HostDefault`.

**What was broken.** `NormalizeProgressionForPartIfNeeded` builds its clone from
`ScriptableObject.CreateInstance`, which leaves `.name` EMPTY, and never copied
it. That clone is published to the shared cache and snapshotted onto
`sharedProgressionData`, so consumers received a nameless object. The remaining
`Instantiate` sites (card override, card palette, per-render override, library
template) had the milder form of the same defect: a `(Clone)` suffix instead of
the asset name.

**Not source-specific.** ALWTTT observed it on `CardPalette` and left
`CardOverride` unverified. The distinction does not exist: the loser is
normalization, not the source, and normalization fires on nearly every render
(`sub x1` authored, `x4` wanted). The already-correct sites — `SeedDefaultCore`,
`ApplyDiatonicRequality`, the P7 snapshot — all followed the no-`(Clone)`
convention already; these four had simply been missed.

**Not the same thing as `sharedProgressionAssetName`.** That readback field was
correct throughout on every source. The reported name and the clone's object
identity are separate surfaces; only the latter was broken.

**Readback taxonomy note (found while pinning this).**
`ResolvedSource.TrackParameters` is effectively UNREACHABLE for Backing through
the orchestrator. Step 2 asks `ctx.GetProgressionForPart` first, and the
orchestrator wires that delegate with an authored fallback
(`SongOrchestrator.FindProgressionForPart`) returning the first Backing track's
`Parameters.Pattern`. A Backing track carrying its own authored progression is
therefore always served by the cache delegate and reports
`SharedProgression`; the `else if (cfg.Parameters?.Pattern is
ChordProgressionData)` branch below is dead on that path. It remains reachable
for a SECOND Backing track in the same part (the fallback returns only the
first one's Pattern) and for composers driven with a null
`GetProgressionForPart`. Behaviour is correct; only the taxonomy is
misleading, since a host reading the readback cannot distinguish "authored on
my own track" from "another track published it". NOT changed here — changing
either the fallback or the enum is a runtime decision, not a documentation one.

Test surface: `Tests/Editor/ChordProgression_CloneIdentityTests.cs` (one test
per REACHABLE precedence step, plus the impose-a-published-clone round trip
that mirrors the host's JAM-1 carry).

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

The register-selective figures (`BassUpperSplit`, `Bossa` — §8.6) sit at the
END of this chain and add no rule of their own: "the bass" they anchor on is
simply the lowest note as it stands after the voicer, after the pin, and after
any reshape (D-BOSSA-BASSNOTE=A). A `Down` pin therefore makes them anchor on
the INVERTED bass — that is the defined behaviour, not a conflict. Selection
owns no pin semantics either.

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
the beat. Block keeps the legacy `Clamp(e.velocity, 0, 127)` — but only while
the CA-V1 jitter is off, which is the default; see §8.7 for the jittered
clamp. Seeded jitter is DELIVERED (CA-V1) and, per D-V1-JIT-SRC=A, it did NOT
fork a child rng: it is a pure mix over (seed, event index, hit index), so
this section's "RNG-free" claim holds verbatim after CA-V1 rather than being
weakened by it.

**Per-figure exception (CA-T2-BOSSA-V2, D-FEEL-ACCENT=A).** The authentic
`Bossa` template (§8.6) supplies its OWN accent tier per template row, reusing
the three factor VALUES above (strong ×1.00 / medium ×0.85 / weak ×0.80) but
assigning them by row rather than by beat position — the surdo weight sits on
beat 2, not the downbeat, which the position curve cannot express and which
the genre requires. Still a pure function of (figure, template row, base
velocity); still RNG-free; the CA-V1 jitter post-pass composes unchanged.
Every other figure keeps the position-derived curve.

### 8.4 Seam and structural both-sites guarantee

`IChordArticulator` / `ChordArticulator`
(`Composition/Interfaces/IChordArticulator.cs`,
`Composition/Articulation/ChordArticulator.cs`). The composer's BOTH chord
emission sites (grid path in `Compose`; `RenderFromProgression`, which also
serves `ComposeProcedural`) replace the legacy `MoveToTime`+`Chord` pair with
the SAME single unconditional `Emit(...)` call. `Block` inside `Emit`
reproduces the legacy pair verbatim; byte-level identity is pinned by
`Tests/Editor/ChordTrackComposer_ArticulationTests.cs`.

**The guarantee is about the ARGUMENTS, not only the call (MGP-ARTIC-RATE-1).**
Both sites must resolve the same per-event values — effective figure, effective
rate, per-event jitter — before the shared call. Identical call shape with
divergent arguments is the failure mode this section previously did NOT
exclude, and it shipped: the grid site kept ARTIC-1's figure gate
(`articRoller != null`) after CA-V1 widened roller construction to fire on
EITHER sentinel, so a card with a concrete figure and `arpeggioRate = Random`
had its authored figure silently replaced by a roll; the same site never
resolved the rate sentinel and never passed the jitter (F-ARTIC-RATE-GRID-1,
-2, -3). Each sentinel must degrade or resolve ONLY its own field: the two
resolutions are independent ternaries over independent substreams, never one
shared "is there a roller" test.

Per-event resolution at BOTH sites is:
```
effectiveExpression = roller != null && chordExpression == Random
                        ? roller.NextFigure() : chordExpression
effectiveRate       = roller != null && arpeggioRate    == Random
                        ? roller.NextRate()   : arpeggioRate
```
with `velocityJitter.ForEvent(eventIndex)` as `Emit`'s trailing argument.

**Verification rule (the load-bearing lesson).** Seam-level tests cannot pin
this. The roller and `PlanHits` were correct throughout; the defect lived only
in what the composer handed them, so the entire CA-V1 suite stayed green while
authored figures were being discarded at render time. Any contract asserting
cross-site equivalence must be pinned by a test that drives EACH SITE
end-to-end and asserts on EMITTED notes — the BASS-WALK-1 verification lesson
raised from the `Hit.NoteIndex` seam to the composer.
`Tests/Editor/ChordTrackComposer_ArticRateIndependenceTests.cs` does this for
the grid site; `Tests/Editor/ChordMarkerParityTests.cs` covers the `chd:`
marker half across both.

**Defensive assertion (D-MGP-ARTIC-2=B).** Both sites emit ONE warning per
render — never per event — if either sentinel is present with no roller to
resolve it, i.e. exactly the state §8.5 declares impossible. This is an
assertion on the roller gate, not a degrade path. The per-event figure degrades
of §8.2 stay silent by design: "never-silent" there means *never produces
silence*, not *always warns*.

`ChordArticulator.PlanHits` is the internal pure planning seam (the test
surface); `Emit` is a thin PatternBuilder translator. `ArpeggioFits(durBeats,
rate)` exposes the arpeggio degrade predicate as a pure static (BASS-WALK-1,
D-WALK-FIT=A) so consumers that must stay monophonic can avoid handing a
multi-note voicing to a plan that will degrade to `Block`; it is a read-only
view of the existing rule, not a new one.

**Selection vocabulary (`Hit.NoteIndex`).** A planned hit names WHICH of the
notes handed to `Emit` sound; it never names a pitch. The vocabulary is closed
and translated by EXACT match:

- `-1` — the full chord, in the voicer's order verbatim.
- `-2` — the UPPER VOICES: every note strictly above the lowest pitch of the
  voicing (CA-T2-BOSSA, D-BOSSA-SEL=A). A degenerate voicing in which no note
  is strictly higher falls back to the full chord (never silent).
- `>= 0` — one note of the direction-sorted voicing (ascending for
  `ArpeggioUp` and for the register-selective figures `BassUpperSplit` and
  `Bossa`, descending for `ArpeggioDown`).

Any other negative value is undefined and emits the full chord. The exact-match
translation is deliberate: a blanket `NoteIndex < 0` test would silently render
a subset sentinel as a full chord — green plan tests, wrong MIDI. This is the
BASS-WALK-1 verification lesson applied at the seam, and every probe for a
selection figure asserts on the EMITTED notes, not on pre-emission variables.

Extending this vocabulary does not weaken §8's pitch-preserving contract:
"pitch-preserving" means the articulator never alters a pitch VALUE, not that it
never chooses among the values it is given — it has selected single notes since
CA-T1. All figure math builds
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

**Rate axis (CA-V1, supersedes D5).** `ArpeggioRate.Random` (member value `3`,
appended; values 0..2 are serialized and unchanged) is the exact mirror of the
figure sentinel: a selection policy, not a rate, resolved composer-side by
`RandomArticulationRoller.NextRate()`. It is INDEPENDENT of the figure axis —
a fixed figure with a random rate consumes zero figure draws, and vice versa.

- **Stream (D-V1-RATE-STREAM=A).** Its own substream,
  `SongOrchestrator.ResolveArticulationRateSeed(ctx.trackSeed)` (FNV-1a over
  `"{trackSeed}|articrate"`). Toggling the rate sentinel therefore cannot shift
  a single figure roll — the same orthogonality the articulation stream has
  against `ctx.rng`, applied one level down. Test-pinned
  (`RateRoll_DoesNotPerturbTheFigureSequence`).
- **Granularity (D-V1-RATE-GRAN=A).** The SAME `randomRerollChance` value drives
  both axes; the draws come from separate streams. `0` = one rate for the whole
  render (per-pattern variety via the host's per-render `seedOverride`), `1` =
  a fresh rate per chord event, intermediates = per-chord change probability.
  Draw discipline mirrors the figure roll exactly: first event 1 pick, each
  later event 1 gate draw plus a conditional pick.
- **Pool (D-V1-RATE-POOL=A).** Uniform over exactly the concrete members with
  value < `Random` (`PerBeat`, `Eighth`, `Sixteenth`), one draw per pick. There
  is deliberately NO weight list for rates in v1; the figure-weight machinery is
  not duplicated.
- **Degrade.** A leaked `ArpeggioRate.Random` resolves to `Eighth` in
  `ChordArticulator.ArpeggioIntervalBeats` (never silent), the same defensive
  posture as the figure sentinel's Block-degrade.

The rate sentinel is consumed by the arpeggio figures AND by Tier-2 `Chugging`
(which overloads `arpeggioRate` as its pulse rate, §8.6) — no extra rule was
added for that overload.

Per-event `chd:` markers are unaffected by either axis (they are stamped
outside the `Emit` call, one per event). Test surface, in two layers:

- **Seam** — `Tests/Editor/ChordTrackComposer_RandomArticulationTests.cs`
  (seed-seam goldens, roller determinism/variance in the SEED-1 idiom, knob
  semantics, weight-table rules, rate-roll semantics + stream orthogonality,
  `PlanHits(Random)==PlanHits(Block)`).
- **Composer** — `Tests/Editor/ChordTrackComposer_ArticRateIndependenceTests.cs`
  (MGP-ARTIC-RATE-1). Drives the GRID emission site end-to-end with a real card
  and asserts on emitted notes: the 2×2 sentinel matrix (concrete/Random figure
  × concrete/Random rate), byte-identity of every rate-inert figure across all
  four rate values, `resolvedFigures == null` for a rate-only random render
  (the R4 clarification below, now test-pinned), figure-sequence invariance
  under the rate knob, and that a rolled rate actually REACHES the articulator
  rather than being dropped. The seam layer alone is insufficient and was
  green throughout F-ARTIC-RATE-GRID-1 (§8.4).

The roller's resolved figure history (`RandomArticulationRoller.History`,
observability-only, no extra draws) is snapshotted into the backing track's
`ResolvedTrackChoice.resolvedFigures` for the Ask A readback (MGP-ALWTTT-DBG-1);
fixed articulation reports null figures. **CA-V1 clarification (R4):** a
rate-only random render builds a roller whose FIGURE history stays empty; an
empty history reports `null` too, so "fixed articulation reports null figures"
remains literally true. The readback was deliberately NOT extended to rates or
to jitter — the rate history (`RateHistory`) is observability-only and feeds the
`logGenerator` trace, and the jitter is not a discrete choice (its seed is
derivable from `trackSeed`).

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

**Members.** `ChordExpressionType` gains `PowerChord = 7` and `Chugging = 8`
(D-T2-SCOPE=A: the two voicing-RESHAPING figures shipped in the CA-T2 batch),
`BassUpperSplit = 9` (CA-T2-BOSSA, the register-SELECTIVE figure that batch
deferred — shipped there as `Bossa` and RENAMED by CA-T2-BOSSA-V2,
OD-BOSSA-7=A/-7a=A, value intact) and `Bossa = 10` (CA-T2-BOSSA-V2, the
AUTHENTIC bossa comping template). All are appended after `Random`; values
0..9 are serialized and unchanged; none is ever renumbered. The rename is
name-only: enums serialize by VALUE, the member is never parsed or persisted
by NAME anywhere in the package (verified before renaming), so no authored
asset changed meaning.

Tier 2 therefore contains two different KINDS of figure, and the distinction is
what decides where each one lives:

- **Voicing-reshaping** (`PowerChord`, `Chugging`) — mutate pitch values, so
  they need the reshaper seam.
- **Register-selective** (`BassUpperSplit`, `Bossa`) — mutate nothing; they
  choose WHICH of the voiced notes sound WHEN, so they live entirely in the
  articulator's selection vocabulary (§8.4) and the reshaper is IDENTITY for
  them (by exclusion: any non-reshaping member is returned unchanged, so the
  CA-T2-BOSSA-V2 append required no reshaper edit).

- **PowerChord** — drops the chord's third to leave root + perfect fifth
  (+ octave), anchored at the root pitch at or below the voicing's bass (so the
  reduction stays in the chord's register regardless of the voicer's inversion
  choice). Rhythm is `Block` (one sustained hit).
- **Chugging** — the same power-chord reshape, re-struck at the card's
  `ArpeggioRate` (D-T2-RHYTHM=A: `arpeggioRate` is overloaded as the chug pulse
  rate — no new field). The pulse is emitted by the articulator's
  pitch-preserving `ChordPulsePlan` (full-chord hits at the arpeggio interval,
  anchored at the event onset; events shorter than one hit degrade to `Block`).
- **BassUpperSplit** — register-selective split (CA-T2-BOSSA, D-BOSSA-HOME=A;
  renamed from `Bossa` by CA-T2-BOSSA-V2 — the regular alternation below is a
  register split, not the bossa comping rhythm, F-BOSSA-FEEL). The
  voicing's LOWEST note anchors the event onset and each bar downbeat strictly
  inside the event, legato to the next low hit or the event end; the UPPER
  VOICES are struck short (≤0.5 beat) on every beat+0.5 inside the event —
  `Offbeat`'s grid and hit length exactly. `arpeggioRate` is IGNORED
  (D-BOSSA-RHYTHM=A: a fixed v1 template, not a rate-driven figure). The v1
  template is a REGULAR alternation — low on every bar downbeat, uppers on
  every offbeat — a register split, useful in its own right (a calm upstroke
  feel); the authentic bossa rhythm is the `Bossa` bullet below
  (F-BOSSA-FEEL → CA-T2-BOSSA-V2). The reshaper is identity: the split is pure
  selection via `Hit.NoteIndex` (`0` = lowest of the ascending sort, `-2` =
  uppers, §8.4), so no pitch is created or altered.
  **Which note is "the bass" (D-BOSSA-BASSNOTE=A):** the lowest note as it
  stands when the articulator receives it — i.e. AFTER the voicer, AFTER the §7
  inversion pin, AFTER any Tier-2 reshape. No new precedence rule; see §7.5.
  **Degrades to `Block`** for the whole event when the voicing has ≤1 note
  (nothing to split — this is what a bassline card selecting `Bossa` always
  hits) or when no offbeat fits the event: a bass-note-only sustain would be a
  drastic and unintended register change, so the event falls back to the full
  chord (never-silent invariant, and the register-safety rule below).
- **Bossa** — the AUTHENTIC bossa nova comping figure (CA-T2-BOSSA-V2,
  D-FEEL-HOME=A / D-FEEL-SCOPE=A): the lab spec's `basico_solo` 1-bar pattern
  as a fixed 5-row template over a bar-length cycle anchored at absolute
  beat 0 (cycle position = position mod `beatsPerBar`, the same absolute-beat
  convention as §8.3's downbeat test; a chord change mid-cycle INHERITS the
  phase and never resets it). Rows (cycle-relative `pos × dur`, role, tier):
  LOW `0.0×2.0` medium · UPPERS `0.0×1.0` medium · UPPERS `1.0×1.5` weak ·
  LOW `2.0×2.0` **strong** (the surdo weight — on beat 2, NOT the downbeat) ·
  UPPERS `2.5×1.5` **strong** (the syncopation, sustained to the cycle end;
  deliberately NO attack on beat 3). LOW = index 0 of the ascending sort,
  UPPERS = the `-2` sentinel — the same closed §8.4 vocabulary as
  `BassUpperSplit`; no `FULL` row in this template (where both roles attack at
  `0.0` they are two hits, low-first, with different durations).
  **Accent model (D-FEEL-ACCENT=A):** velocities are TEMPLATE-supplied tiers
  reusing the SD-5 factor values (strong ×1.00 / medium ×0.85 / weak ×0.80),
  a documented per-figure exception to §8.3's position-derived curve — see
  §8.3. `arpeggioRate` is IGNORED. **Meter (shorter than 4/4):** rows at or
  after the bar length are dropped and every duration clips at the cycle end
  (3/4 keeps all five rows with the `2.0`/`2.5` rows truncated; 2/4 keeps the
  first three). **Windowing (D-FEEL-TIE=A):** no hit ever overshoots the
  event window; the next cycle re-attacks at `0.0`, so truncating at the
  boundary is perceptually legato-to-the-next-attack. An onset that lands
  between template rows gets a LOW hit (medium tier) legato to the first
  template attack — a chord change must always be heard at its onset.
  **Degrades to `Block`** for the whole event when the voicing has ≤1 note,
  when `beatsPerBar ≤ 0` (no bar to cycle on), or when the window contains no
  UPPERS attack (a bass-only fragment would be a silent register shift —
  F-WALK-REG; mirror of `BassUpperSplit`'s OD-BOSSA-4 rule).
  D-BOSSA-BASSNOTE=A applies unchanged: "the lowest note" is the voicing as
  received, post-voicer, post-§7 pin, post-reshape (§7.5).

**Composition contract.** The reshaper mutates pitch; the articulator still owns
rhythm AND selection. The selected expression is passed straight to `Emit`: the
articulator degrades a leaked `PowerChord` (and `Random`) to `Block`, renders
`Chugging` as the chord pulse, and renders `BassUpperSplit` and `Bossa` as
their register-selective plans — so it never emits a Tier-2 *pitch* while
still carrying the Tier-2
*rhythm* or *selection* where one exists. Both emission sites keep the SINGLE
unconditional `Emit` (a reshape is a transform on the emitted voicing, a
selection is a value in the plan — neither is a per-site branch);
`lastVoicing` and the first-chord pitch stash keep the full harmonic voicing, so
voice-leading continuity is unaffected. Reshapes and selections are
deterministic and RNG-free (same discipline as Tier-1). All Tier-2 members are
excluded from the §8.5 Random pool (D-T2-POOL=A′).

**Why a register-selective figure did NOT get its own emit path.** The rejected
alternative was a reshaper-owned emission route. It would have broken two
invariants at once: the reshaper would stop being a pure list transform and
become an emitter, and the single unconditional `Emit` would gain a per-site
branch. Extending the articulator's selection vocabulary (§8.4) instead costs
one sentinel and leaves every composer file untouched (D-BOSSA-HOME=A).

**Register safety (F-WALK-REG).** A figure that changes WHICH notes sound
changes the effective register of the output even when it invents no pitch.
Both register-selective figures are subject to this by definition, so their
degrade rules are written to
avoid silent register shifts (an event that can only place the low note falls
back to the full chord rather than rendering bass-only), and its emitted pitch
set is by construction a SUBSET of the voicing handed to `Emit` — verifiable on
the rendered MIDI, which is where such changes must be checked.

**Precedence vs §7.** See §7.5 — the reshape runs after the inversion pin.

**Delivered (CA-T2-BOSSA → CA-T2-BOSSA-V2).** The bossa bass/upper split spun
out of CA-T2 shipped in CA-T2-BOSSA; on listening it turned out to be a
register split rather than the bossa comping rhythm (F-BOSSA-FEEL), so
CA-T2-BOSSA-V2 renamed it `BassUpperSplit` (value 9 intact) and delivered the
AUTHENTIC figure as `Bossa = 10` from a sourced rhythmic specification (the
lab spec's `basico_solo` pattern — reference material, not implementation
authority). Both earlier deferral premises are now settled by code:
register-selective emission needed only a wider selection vocabulary (§8.4),
and bar-cycle math needed only the absolute beat position `PlanHits` already
receives — no new input, no new seam.

**Deferred — refinements of the authentic figure**, each blocked on a decision
or an input, none on the seam (§8.4 already expresses LOW/UPPERS/FULL):

- **The 2-bar patterns** of the spec (`sincopado_2c_anticipacion`,
  `baja_densidad`, `clave_bossa_pedagogica`) and the alternating pattern 5.
  Bar PARITY is derivable from absolute position; the open question is the
  cycle's phase ANCHOR (the Part start is the lab-blessed v1 approximation;
  phrase detection is explicitly rejected for v1 — spec §6.1).
- **Harmony-carrying anticipation** (spec §6.3(b), `carries_next_harmony`):
  the cycle-final attack sounding the NEXT chord across the barline. Needs
  the planner to know the next chord — a contract change. The rhythmic attack
  itself is already in the template; the lab rates the harmonic carry
  "degradable with dignity" for v1.
- **LOW_ALT — root/fifth bass alternation** (spec §7.1, the one real
  vocabulary limitation): a second low role ("the second-lowest note" or an
  explicit fifth). The lab blesses root-only as documented real practice.
- **Muted ghost strokes** (spec §7.2): a pitchless percussive role.
  Dispensable without identity loss per the lab.

Recorded, not scheduled. Nothing here reopens a shipped contract.

Test surface: `Tests/Editor/ChordTrackComposer_ArticulationTests.cs` (reshape
drops the third / identity on non-Tier-2 expressions; `ChordPulsePlan`
full-chord pulse count; `PlanHits(PowerChord) == PlanHits(Block)`; for `BassUpperSplit`:
the low/upper grids in 4/4 and 3/4, the multi-bar re-strike, the off-grid onset,
all three degrades, no-overshoot, rate-independence at MIDI-byte level, jitter
composition, and — the load-bearing one — an EMITTED-MIDI probe asserting that
the downbeat carries exactly the lowest voiced pitch and each offbeat exactly
the non-lowest pitches). The `ChordExpressionType` member count and Tier-2 tail
are pinned in `Tests/Editor/BassTrackComposer_ArticulationTests.cs` as an
append tripwire: any future append must update it deliberately. For the
authentic `Bossa` template (CA-T2-BOSSA-V2), 15 further tests: the exact
5-row plan (positions, durations, roles AND tiers), the surdo inversion
(beat 2 louder than the downbeat — the test that fails if D-FEEL-ACCENT
regresses), no-attack-on-beat-3 with the syncopation sustained to the cycle
end, two identical cycles over a 2-bar event, mid-cycle onset phase
INHERITANCE (no reset), the low onset-fallback hit, meter clipping in 3/4 and
2/4, all four degrades, no-overshoot, byte-level rate independence, jitter
composition, and the emitted-MIDI probe (attack-time groups: full set at 0.0,
uppers at 1.0, lowest pitch alone at 2.0, uppers at 2.5 — and nothing on
beat 3). The tripwire now pins `BassUpperSplit = 9`, `Bossa = 10`, member
count 11, and pool exclusion for BOTH.

### 8.7 Seeded velocity jitter (CA-V1)

Opt-in per-hit velocity humanization, layered over the §8.3 accent curve.
Surface: `BackingCardConfigSO.velocityJitter : int` (`[Range(0,32)]`, default
`0`) and the same field on `BasslineCardConfigSO`; persistent card-level, whole
render, no snapshot-and-clear (D-EXP1=A, as everywhere in §8).

**Source (D-V1-JIT-SRC=A) — the load-bearing decision.** The jitter is a PURE
FUNCTION of `(seed, event index, hit index)`, not a draw from a stateful
stream, carried by the value type
`Runtime/CoreScripts/Composition/Data/VelocityJitter.cs`. Consequences, all
contract-level:

- The articulator stays pure and RNG-free. **SD-3=A is unchanged, not
  relaxed**: there is no draw order inside the articulator that could be
  perturbed, so the §8.4 both-sites guarantee and the bass's §2 draw contract
  are structurally safe rather than carefully avoided.
- Immune to draw-order coupling: changing an earlier event's figure, rate or
  hit count does not shift any later event's jitter.
- Integer-only mixing, so goldens are exactly pinnable across .NET versions —
  unlike `System.Random`, which is only runtime-stable (which is why the §8.5
  roller tests use the variance idiom and these do not).

**Seed.** `SongOrchestrator.ResolveVelocityJitterSeed(ctx.trackSeed)` (FNV-1a
over `"{trackSeed}|articvel"`), a third articulation substream alongside
`|artic` and `|articrate`. Because `trackSeed` already folds in role and
musicianId, backing and bass on the same part jitter independently by
construction.

**Application (D-V1-JIT-SCOPE=A, D-V1-JIT-SHAPE=A).** The composer builds one
render-level `VelocityJitter`, scopes it per chord event via `ForEvent`, and
passes it as `Emit`'s optional trailing parameter — the extension route the
CA-T1 seam contract recorded (the `IChordVoicer.VoiceChord` `forcedInversion`
precedent), so the signature was extended, not changed. `PlanHits` applies it
as a POST-PASS over the planned hits (`ApplyJitter`), indexed by hit position;
no figure branch knows about it. The offset is an integer uniform over
`[-n, +n]`.

- Applies to ALL figures **including `Block`** — humanizing a block render is
  the primary use case.
- Clamp: `1..127` on every jittered hit (a jittered velocity 0 would be
  note-off semantics). `Block`'s legacy `0..127` clamp therefore applies only
  with jitter off.
- Timing, hit count and note indices are never touched.
- `velocityJitter == 0` (default, and any absent card) returns the planned list
  BY REFERENCE: pre-CA-V1 bit-identity is **structural**, not an empirical
  property to re-verify.

**Orthogonality.** The jitter axis is independent of the §8.5 sentinels: it
works with a fixed figure and a fixed rate, and it is not part of the roll.
Same seed => same jitter => byte-identical render.

Test surface: `Tests/Editor/ChordTrackComposer_VelocityJitterTests.cs`
(substream goldens and mutual distinctness, exact jitter goldens, bound and
range coverage, event/hit fold asymmetry, default-jitter identity across every
enum member, golden velocities for `Block` and `PerBeat`, both clamps,
timing/note-index invariance, determinism, rate-sentinel degrade). That file
pins the jitter ENGINE at the `PlanHits` seam; that the composer actually
DELIVERS the jitter to `Emit` at each emission site is pinned separately by
`Tests/Editor/ChordTrackComposer_ArticRateIndependenceTests.cs`
(MGP-ARTIC-RATE-1 — the grid site omitted the trailing argument entirely from
CA-V1 until this batch, F-ARTIC-RATE-GRID-3, with every engine-level test
green).

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
- the arpeggio-rate roll changes (§8.5, CA-V1): the `ArpeggioRate.Random`
  sentinel, its dedicated `|articrate` substream, the shared-rerollChance
  granularity rule, the uniform rate pool, or the Eighth degrade,
- a NEW per-event value is threaded into `Emit` (§8.4): every emission site
  must resolve it, and the batch must add a site-level end-to-end pin, not only
  a seam pin (MGP-ARTIC-RATE-1),
- the seeded velocity jitter changes (§8.7): the pure-mix source (any move to a
  stateful stream would break SD-3=A and must be argued explicitly), the
  substream derivation, the jitter scope (`Block` included), the 1..127 clamp,
  the uniform distribution, or the zero-amount identity guarantee,
- the voicer's start-register RNG source or determinism changes (VL-DET-1: random
  `StartRegisterMode` draws from the part's seeded `ctx.rng` via
  `IChordVoicer.VoiceChord`'s trailing `rng`, not global `UnityEngine.Random`;
  non-random modes and a null `rng` stay bit-identical to legacy),
- the `chd:` marker contract (field order, one-per-event, both-sites parity, or
  accidental handling) changes (MGP-ALWTTT-DBG, §2.1);
- the Random-articulation readback (`resolvedFigures`) meaning changes
  (MGP-ALWTTT-DBG-1, §8.5).
- the F-B2-LIBRARY exception changes (§2.2): the `PickTemplateForPart` tonality
  filter is retired, gated, or extended to another selection path — any of which
  is a runtime change with an impact radius, not a wording change.

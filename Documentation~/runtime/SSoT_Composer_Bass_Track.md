# SSoT — Bass Track Composer

> Runtime authority for `BassTrackComposer` and its authoring surface. The
> Tier-1 articulation ENGINE contract lives in
> `runtime/SSoT_Composer_Backing_Track.md` §8 and is not duplicated here; this
> document owns the bass CONSUMER semantics (CA-F2, D-PRIO=A Feature 2).

## 0. Scope & governed surfaces

Governs:
- `Runtime/CoreScripts/Composition/Composers/BassTrackComposer.cs`
- `Runtime/CoreScripts/Composition/Data/BasslineCardConfigSO.cs`
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs`

The factory (`BassTrackComposerFactory` in `ComposerFactories.cs`) is
unchanged by CA-F2: it constructs the composer with a hardcoded
`randomChordTone: false` and reads nothing from the track config.
`ITrackComposer` is unchanged.

## 1. Progression consumption

The bass renders the shared progression:
`ctx.GetProgressionForPart(part) ?? cfg.Parameters.Pattern as
ChordProgressionData`. Null/empty progression => empty `MidiFile`.

On record (pre-existing, deliberately unchanged by CA-F2):
- **Single pass, no repeat-to-fill.** Unlike the backing composer, the bass
  renders each progression event once at its absolute step; it does not
  repeat the progression to cover the part length.
- **Normalization-order hazard.** The bass sees the TS-normalized runtime
  clone only if the backing track composed first (track-list order); otherwise
  it consumes the raw cached/authored progression.
- `degreeAccidental` is ignored (same recorded gap as the backing grid path).

**Per-render override (Ask C, D-DBG4=A).** A `patternOverride` targeting the
Bassline track is **warn + ignore** in v1: the bass owns no pattern channel (it
renders the shared progression), so honoring an override here would create a
second mutation path into shared state. To change the bass's harmony, override
the **Backing** track — its override becomes the shared progression by the
existing don't-overwrite discipline, and the bass picks it up via
`ctx.GetProgressionForPart`. The bass reports `usesSharedProgression` +
`progressionRoman` (grid-site roman formatting) for the Ask A readback
(MGP-ALWTTT-DBG-1).

## 2. Note selection & rng contract

Per event, in `prog.events.OrderBy(startStep)` order: the degree root is
looked up in the part scale; the pitch class is the chord root (default) or a
random chord tone (`randomChordTone` ctor flag); the octave is drawn from a
narrow low band around `inst.octaveMin`.

**Determinism contract:** the selection loop draws from the shared `ctx.rng`
stream — exactly ONE draw per event in root mode (octave), exactly TWO in
chord-tone mode (tone, then octave), in that order. This draw count and order
is part of the composer's determinism/bit-identity surface and must not be
perturbed by any layer added around it. The articulation engine is RNG-free
by contract (§8 of the Backing SSoT) and therefore cannot perturb it.

## 3. Articulation (CA-F2 — monophonic consumer of the shared engine)

### 3.1 Selection surface and lifecycle

Selection is a PERSISTENT authored field pair on the new bassline card
(SD-F2-4=A, honoring D-EXP1=A):

- `BasslineCardConfigSO.chordExpression : ChordExpressionType` (default `Block`)
- `BasslineCardConfigSO.arpeggioRate : ArpeggioRate` (default `Eighth`)

Resolved once at `Compose` entry from the track's `Parameters.Style` slot via
the internal test seam `BassTrackComposer.ResolveArticulation`. It applies to
the whole render; the §6/§7 snapshot-and-clear lifecycle does not apply, and
nothing is written to `PartConfig`.

**Independence (SD-F2-5=A):** the bass never inherits the backing card's
expression. Any non-bass bundle in the Style slot (including
`BackingCardConfigSO`) resolves to the defaults, so an unset bass track is
bit-identical regardless of the backing selection.

### 3.2 Seam and single-site guarantee (SD-F2-1=A)

The bass's SINGLE emission site invokes the SAME engine the backing composer
uses — one unconditional `IChordArticulator.Emit(...)` call with a 1-note
`playable` list, replacing the legacy `MoveToTime`+`Note` pair. `Block` (or
no card) is MIDI-byte bit-identical to the legacy pair: a 1-note
`pb.Chord` compiles to the same bytes as the legacy `pb.Note` (test-pinned,
the SD-F2-1 gate). Recorded contingency: if a DryWetMIDI change ever breaks
that equivalence, add a thin `EmitMono` translator on `ChordArticulator`
sharing `PlanHits` — the figure math is unaffected either way.

Velocity note: `Block` clamps 0..127 where the legacy raw cast threw on
out-of-range values — byte-identical for valid 0..127 data, strictly more
robust otherwise.

### 3.3 Monophonic figure semantics (SD-F2-2=A)

Figures apply over the per-event SELECTED note (the root, or the
`randomChordTone` tone). Consequences on a 1-note voicing:
- `ArpeggioUp` / `ArpeggioDown` are a repeated-note pulse at the card's rate
  and are indistinguishable from each other (test-pinned).
- `Offbeat` = short root upstroke stabs; `PerBeat`/`Staccato` = root pulse.
- All engine invariants hold unchanged: never-silent Block-degrade, no window
  overshoot, RNG-free pure accent curve.

A chord-tone walk interpretation (arpeggio cycling root/3rd/5th) is
explicitly NOT implemented; it is recorded as a candidate for the
seeded-variation batch (see the CA roadmap).

### 3.4 Meter authority and the recorded SD-F2-3=B deviation

CA-F2 derives `beatSpan`/`beatsPerBar` from the Part TS
(`GetBeatSpan(part.TimeSignature)` / `GetTimeSignatureDetails`), mirroring the
backing composer — meter authority per `SSoT_CONTRACTS.md` §5.

**Deviation on record:** the legacy bass emitted on
`MusicalTimeSpan.Quarter` unconditionally and was therefore desynced from the
backing track in every beat-unit ≠ 4 meter (e.g. 6/8). CA-F2's default
bit-identity claim is scoped to beat-unit == 4 meters; in others the output
deliberately changes (a sync FIX), pinned by
`Block_MonoEmit_BitIdentityHoldsPerBeatSpan_EighthDiffersFromLegacyQuarter`.

## 4. MIDI plumbing

Unchanged by CA-F2: channel forcing on all ChannelEvents; bank/patch stamping
(CC0/CC32 + ProgramChange) on the first chunk; `logGenerator` trace (now also
reports the resolved expression/rate).

## 5. Update triggers

Update this document when any of the following change:
- progression consumption or the repeat-to-fill behavior (§1);
- the note-selection rng draw count/order or register policy (§2);
- articulation consumption, card surface, or figure meaning for bass (§3);
- the SD-F2-3 meter deviation is resolved for legacy content (§3.4);
- a `BasslineCardConfigSO` gains fields beyond the articulation pair (§3.1);
- the per-render override policy (bass = warn + ignore) or the shared-progression
  readback changes (MGP-ALWTTT-DBG-1+3).

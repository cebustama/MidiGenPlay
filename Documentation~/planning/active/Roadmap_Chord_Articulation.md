# Roadmap — Chord Expression / Articulation arc (CA)

> Planning material. Not implementation authority; runtime truth lives in
> `runtime/SSoT_Composer_Backing_Track.md` §8.

## Scope
Add an articulation layer so the same chord progression renders with
different rhythmic interpretations, shared between the backing composer and
(later) a monophonic bass consumer. Distinct from
`Roadmap_Chord_Expressivity` (chord vocabulary/voicing) and
`Roadmap_Composition_Expressivity` (palette/card identity).

## Arc-level locked decisions
- **D-PRIO=A** — shared articulation engine first; bass is a later
  monophonic consumer of the same engine.
- **D-EXP1=A** — expression selection is a persistent field on the backing
  config surface (whole-render), not a transient hint, not a per-event field.
- **D-EXP2=Tier1** — v1 shipped rhythm/velocity articulation over the voiced
  chord; Tier 2 (voicing-reshaping) followed as a separate seam. CA-T2 shipped
  power chord + chugging; the bossa bass/upper split is spun out (see below).

## Batch CA-T1 — Tier-1 engine + backing consumer
**Status: DONE (2026-07-15, tests green 2026-07-15, smoke 2026-07-15).**
`IChordArticulator`/`ChordArticulator` (pure `PlanHits` seam), 6-figure
`ChordExpressionType` + `ArpeggioRate`, `BackingCardConfigSO.chordExpression`/
`.arpeggioRate`, identical dual-site wiring, 16 EditMode tests incl.
MIDI-byte Block bit-identity. Sub-decisions SD-1=A, SD-2=A, SD-3=A, SD-4=B,
SD-5=A locked (see changelog entry).

## Batch CA-F2 — monophonic bass consumer
**Status: DONE (2026-07-15, tests green 2026-07-15, smoke 2026-07-15).**
BassTrackComposer consumes the shared engine at its single emission site with
a 1-note voicing (SD-F2-1=A; EmitMono contingency on record).
`BasslineCardConfigSO.chordExpression`/`.arpeggioRate` (SD-F2-4=A, D-EXP1=A;
SD-F2-5=A independent of the backing card). Figures over the selected note
(SD-F2-2=A; arpeggio = repeated-note pulse; chord-tone walk deferred to
CA-V1). SD-F2-3=B: Part-meter time base adopted; legacy unconditional-Quarter
desync in beat-unit≠4 meters deliberately fixed (deviation on record in
`runtime/SSoT_Composer_Bass_Track.md`, test-pinned). New bass SSoT
(SD-F2-6=A). 9 EditMode tests incl. the mono Block byte-identity gate.

## Batch CA-T2 — Tier-2 voicing-reshaping figures
**Status: DONE (2026-07-16, tests green 2026-07-16).** Shipped **power chord**
and **chugging** via a new pre-articulation seam `IChordReshaper`/`ChordReshaper`
(D-T2-SEAM=B) that reshapes the voiced list between `VoiceChord` and `Emit` at
both emission sites — voicer owns register/inversions, articulator owns rhythm,
reshaper owns the pitch reduction. `ChordExpressionType.PowerChord = 7` (drop the
third → root+fifth+octave; `Block` rhythm) + `Chugging = 8` (same reshape
re-struck at `arpeggioRate` via the articulator's pitch-preserving
`ChordPulsePlan`; D-T2-RHYTHM=A overloads `arpeggioRate`). Tier-1/`Block`/`Random`
byte-identical (reshaper identity); the articulator degrades leaked
`PowerChord`/`Random` to `Block`. **D-T2-PIN=A** (reshape after the §7 pin,
backing §7.5) · **D-T2-POOL=A′** (Tier-2 out of the §8.5 Random pool, not
weight-admissible) · **D-T2-SCOPE=A** (power chord + chugging ship). Governed by
`runtime/SSoT_Composer_Backing_Track.md` §8.6.

### Spun out of CA-T2 — Tier-2 bossa bass/upper split
**Status: NEXT (deferred from CA-T2).** A register-selective figure (bass on 1,
upper voices off the beat) that the pitch-preserving Tier-1 articulator cannot
express (D-T2-SCOPE=A). Needs either a register-aware articulator figure or a
reshaper-owned emit path; must compose with the CA-T2 reshaper and §7 pins.

## Batch CA-V1 — seeded variation (opt-in)
**Status: PARTIALLY DELIVERED — part 1 done via MGP-ALWTTT-ARTIC-1.**
Original scope: seeded velocity jitter + randomized per-pattern / per-chord
arpeggio-rate variety (user wish recorded at SD-4). Requires an rng policy
that does NOT tap the shared ctx.rng stream (fork a child rng from the
seed); extends the seam via optional trailing parameter (IChordVoicer
forcedInversion precedent).

- **Batch MGP-ALWTTT-ARTIC-1 — Random selection policy (seeded variation,
  part 1). Status: DONE (2026-07-15).** Delivered the rng policy CA-T1 excluded:
  `Random` sentinel + `RandomArticulationRoller` + `ResolveArticulationSeed`
  substream + card knobs (rerollChance, figure weights). Cross-project ask
  from ALWTTT (DEMO-FIXES/DF-ARTIC).
- **Still pending (seeded variation, part 2):** seeded velocity jitter, and
  randomized arpeggio-rate variety (D5 kept the rate fixed) — trivial
  extension of the roller once wanted. Bass roll wiring (D6 shipped
  degrade-only) is a one-line rider on demand.

Candidate recorded at CA-F2: a chord-tone-walk bass interpretation of the
arpeggio figures (SD-F2-2 deferral).

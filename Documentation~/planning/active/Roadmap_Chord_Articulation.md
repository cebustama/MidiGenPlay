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

## Batch CA-V1 — seeded variation (opt-in)
**Status: DONE (2026-07-24; part 1 2026-07-15, part 2 2026-07-24).**

- **Part 1 — MGP-ALWTTT-ARTIC-1, Random selection policy. DONE (2026-07-15).**
  Delivered the rng policy CA-T1 excluded: `Random` sentinel +
  `RandomArticulationRoller` + `ResolveArticulationSeed` substream + card knobs
  (rerollChance, figure weights). Cross-project ask from ALWTTT
  (DEMO-FIXES/DF-ARTIC).
- **Part 2 — velocity jitter + rate variety + bass rider. DONE (2026-07-24).**
  Shipped `VelocityJitter` (new `Composition/Data/` value type),
  `ArpeggioRate.Random = 3` resolved by `RandomArticulationRoller.NextRate()`,
  two new substreams (`|articrate`, `|articvel`), `velocityJitter` on both
  cards, the full Random knob set on `BasslineCardConfigSO`, and the bass roll
  wiring. Governed by `runtime/SSoT_Composer_Backing_Track.md` §8.5 (rate) and
  the new §8.7 (jitter), plus `runtime/SSoT_Composer_Bass_Track.md` §3.5.

**The original framing was wrong on one point, deliberately.** This batch was
scoped as "fork a child rng from the seed". It did not: **D-V1-JIT-SRC=A** made
the jitter a PURE MIX over (seed, event, hit), so the articulator's RNG-free
contract (SD-3=A) survives CA-V1 intact instead of being relaxed, and the jitter
is immune to draw-order coupling. The seam extension DID follow the recorded
route (optional trailing parameter, `IChordVoicer.VoiceChord` precedent).

Decisions locked: **D-V1-JIT-SRC=A** (pure mix, not a stream) ·
**JIT-SCOPE=A** (all figures incl. Block; clamp 1..127 when active) ·
**JIT-SHAPE=A** (uniform integer) · **RATE-SEL=A** (`ArpeggioRate.Random`
sentinel) · **RATE-STREAM=A** (own substream) · **RATE-GRAN=A** (shared
`randomRerollChance`) · **RATE-POOL=A** (uniform, no weights) ·
**BASS=B** (full knob parity on the bassline card).
**D5** (fixed rate) and **D6** (bass degrade-only) are SUPERSEDED.
**R4**: the DBG-1 readback was deliberately not extended (rates and jitter stay
out of `resolvedFigures`; an empty figure history reports null).

## Arc closed — recorded candidates

**The CA arc is COMPLETE** as of CA-T2-BOSSA. Nothing remains scheduled.

Recorded candidates, each needing its own decision before it becomes a batch:
- ~~Authentic bossa template~~ — **DELIVERED by CA-T2-BOSSA-V2** as
  `Bossa = 10` (the spec arrived; scope: the 1-bar `basico_solo` pattern).
  Remaining refinements (2-bar patterns + phase anchor, harmony-carrying
  anticipation, LOW_ALT, ghost strokes) are recorded in Backing §8.6.
- ~~Rename `Bossa`~~ — **DELIVERED by CA-T2-BOSSA-V2** (OD-BOSSA-7=A/-7a=A:
  `BassUpperSplit`, value 9 intact).
- **Rhythm-driven backing accents** — instead of a fixed template, let the
  backing follow the accents of a rhythm-track pattern, so the comping locks to
  whatever groove is authored. Larger than a figure: it is a new input
  dependency between two composers and needs its own decision about who owns the
  accent source and what happens when no rhythm track exists.
- **Narrow the bass octave band** — Bass §2 samples `octaveMin-1..octaveMin+1`
  and IGNORES `octaveMax`. Changes every bass render, so it is not a rider on
  anything (F-WALK-REG, BASS-WALK-1).
- **Admit Tier-2 figures into the §8.5 Random pool** — deferred since CA-T2
  (D-T2-POOL=A′).
- **Move the `ChordExpressionType` tail pin** out of the bass test file into the
  chord articulation tests, where the enum lives (OD-BOSSA-6 alternative B;
  cosmetic, not blocking).

### Batch CA-T2-BOSSA-V2 — authentic bossa template + v1 member rename
**Status: DONE (2026-07-24, tests green 2026-07-24, smoke validated 2026-07-24).** Reopened
the closed CA arc on finding F-BOSSA-FEEL (not on a failure) and closed it
again. Two deliverables:

1. **OD-BOSSA-7=A / OD-BOSSA-7a=A** — `Bossa = 9` renamed `BassUpperSplit`
   (value intact; enums serialize by VALUE and the member is never
   parsed/persisted by NAME — verified by grep, surface exactly 4 files). The
   name `Bossa` is reclaimed for the authentic figure. `RegisterSplit` was
   rejected: it is the §8.6 CATEGORY name and the category has two members.
2. **`Bossa = 10`** — the authentic 1-bar comping template
   (lab spec `basico_solo`, D-FEEL-SCOPE=A: the recognizability threshold says
   this template alone reads as bossa; one pattern done well over four
   approximated). D-FEEL-HOME=A: a flat member on the SAME seam — `PlanHits`
   needed no new input (cycle position from absolute beats). D-FEEL-PHASE:
   moot for a 1-bar cycle. D-FEEL-TIE=A: no-overshoot stands; the
   harmony-carrying anticipation is a recorded future. D-FEEL-ACCENT=A: the
   surdo inversion via template-supplied tiers reusing the SD-5 factor values
   — a documented per-figure exception to §8.3.

Deferred (each needing its own decision, none blocked on the seam): the 2-bar
spec patterns + cycle phase anchoring · `carries_next_harmony` · LOW_ALT
root/fifth bass alternation (spec §7.1) · muted ghost strokes (spec §7.2).
Governed by `runtime/SSoT_Composer_Backing_Track.md` §8.3 (accent exception),
§8.4, §8.6, §7.5.

### Batch CA-T2-BOSSA — Tier-2 bossa bass/upper split
**Status: DONE (2026-07-24, tests green 2026-07-24, smoke validated 2026-07-24).** The last
item of the CA arc. `ChordExpressionType.Bossa = 9`: the voicing's lowest note
anchors the event onset and each interior bar downbeat, the upper voices strike
on every beat+0.5.

The deferral's premise did not survive contact with the code. The choice was
never "register-aware figure OR reshaper-owned emit path" — **D-BOSSA-HOME=A**
took the first and made it cheap by extending the articulator's EXISTING
selection vocabulary (`Hit.NoteIndex`) with one subset sentinel (`-2` = upper
voices, **D-BOSSA-SEL=A**), keeping the `Hit` struct shape. The reshaper-owned
route was rejected outright: it would have turned a pure list transform into an
emitter and broken the single unconditional `Emit`.

Decisions: **D-BOSSA-BASSNOTE=A** (the anchor note is simply the lowest note
after voicer + §7 pin + reshape — no new precedence rule; Backing §7.5) ·
**D-BOSSA-RHYTHM=A** (fixed v1 template reusing `Offbeat`'s grid; `arpeggioRate`
ignored) · **OD-BOSSA-1=A** (low role reuses the ascending sort at index 0 — no
third sentinel) · **OD-BOSSA-2=A** (uppers by strict `>` on pitch) ·
**OD-BOSSA-3=A** (onset + interior bar downbeats) · **OD-BOSSA-4=A** (no offbeat
fits ⇒ Block, to avoid a silent register shift) · **OD-BOSSA-6=A** (the enum
tripwire is updated, never deleted).

Scope of the change is the headline: **two runtime files** (`ChordArticulator.cs`,
`ChordExpressionType.cs`) and two test files. No composer, no reshaper, no card
surface, no orchestrator. Governed by `runtime/SSoT_Composer_Backing_Track.md`
§8.4 (selection vocabulary) and §8.6 (the figure).

**Finding F-BOSSA-FEEL, on record (post-smoke):** the v1 template is a REGISTER
SPLIT, not an authentic bossa rhythm — low on every bar downbeat, uppers on every
offbeat is a regular alternation that reads as a calm ska upstroke. Real bossa
comping alternates unevenly across a two-bar cycle and mixes in full-chord
attacks. The figure ships as-is and is useful; the label overreaches. **Open
decision OD-BOSSA-7 (member name)** and the authentic-template deferral are
described in `runtime/SSoT_Composer_Backing_Track.md` §8.6.

### Batch BASS-WALK-1 — chord-tone walk for the bass
**Status: DONE (2026-07-24, tests green 2026-07-24).** Promoted from the SD-F2-2
candidate. An opt-in pitch-selecting reading of the arpeggio figures on the
monophonic line: `BasslineCardConfigSO.arpeggioToneMode = ChordToneWalk` makes
the bass hand the SAME `Emit` a root-anchored root/3rd/5th triad, and the
engine's existing `k % noteCount` cycling walks it. **D-WALK-HOME=A** (built
bass-side; no engine figure, no seam change) · **D-WALK-RNG=A** (zero new
`ctx.rng` draws — the tones are derived, the register is the octave already
drawn, so the §2 draw contract is structurally intact) · **D-WALK-SURF=A**
(bass-only `BassArpeggioToneMode`; `ChordExpressionType` untouched, so nothing
enters the §8.5 pool) · **D-WALK-TONES** (triad only; 7th dropped) ·
**D-WALK-DIR** (engine sort order as-is) · **D-WALK-FIT=A** (new pure predicate
`ChordArticulator.ArpeggioFits` guards the degrade path so a short event can
never emit a chord on the bass) · **D-WALK-ANCHOR** (root-anchored even in the
unreachable `randomChordTone` mode; simplification on record). Also retires the
Bass §3.3 pool bias in walk mode: `ArpeggioUp` and `ArpeggioDown` become
genuinely different figures. Governed by `runtime/SSoT_Composer_Bass_Track.md`
§3.6.

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
- ~~**MGP-ALWTTT-BASS-POCKET-1 — rhythm-coupled bass ("pocket")**~~ —
  **SHIPPED 2026-07-25** as `BasslineCardConfigSO.pocketMode = SlapPocket`;
  see the batch record below. Every consumer acceptance criterion recorded here
  was met, and the degrade gate is pinned as BYTE-identity rather than
  approximate. The original candidate text is preserved verbatim for the
  record: ACCEPTED as a batch of its own on 2026-07-25 (ALWTTT R2
  cross-boundary consultation), **not scheduled**. Generalizes the earlier "rhythm-driven backing accents"
  candidate: instead of a fixed template, a track follows the accents/onsets of
  a rhythm-track pattern, so the line locks to whatever groove is authored. Still
  larger than a figure — it is a new input dependency between two composers.
  Consumer acceptance criteria, recorded verbatim from the ask:
  opt-in by config · no Rhythm track in the Part degrades silently to the
  uncoupled figure (warn at most, never error, never silence) · deterministic
  (same seed + config ⇒ same bytes) · `MusicianTrackKey` keying untouched · BC
  gate byte-identical with the coupling off.
  **Hash duty, declared in advance:** if the bass reads the resolved Rhythm
  pattern, that pattern becomes a hash-relevant input of the bass track and
  ALWTTT must extend `ComputeTrackInputsHashesForPart`. This is an obligation of
  the batch's handoff, not a decision to defer.
  Decisions to resolve first: who owns the accent source (the resolved
  `DrumPatternData`? which one, with several drummers? a channel published by
  `RhythmTrackComposer`?) — interacts with track composition order and the
  recorded Bass §1 normalization-order hazard · what couples (onsets, velocity
  accents, both; which lanes count) · where it lives (composer-side like
  BASS-WALK-1, most likely, since the articulator is RNG-free and
  pitch-preserving) · whether the pocket replaces or modulates the selected
  `ChordExpressionType`.
  **Determinism constraint that bounds all of the above:** Bass §2 fixes exactly
  ONE `ctx.rng` draw per event in root mode (TWO in chord-tone mode). Any new
  randomness must run on a seed-derived substream (the CA-V1 pattern), never on
  `ctx.rng`.
- **MGP-ALWTTT-BASS-WALK-2 — improvised walking bass** — **SHIPPED 2026-07-27
  as the WALK-2 phase of B3 — see B3 below for the outcome.** ABSORBED into
  **B3 — BASS-REG-1** (below), because its register consequence and the two
  F-WALK-REG instances are the same problem and must not be decided twice.
  Original candidate text: ACCEPTED as a candidate
  on 2026-07-25 (ALWTTT D-X1=B), **not scheduled**. BASS-WALK-1 shipped a fixed
  root → 3rd → 5th cycle; the ask is a line that reads as an *improvised* walking
  bass, with variation between bars. Decisions to resolve first: the note
  vocabulary (chord tones only, or chromatic/diatonic approach notes into the
  next chord root) · where it lives (composer-side like BASS-WALK-1, or a new
  figure) · the selection surface (a third `BassArpeggioToneMode` value, or a
  new field) · the variation source and its seeded substream — **`ctx.rng` is
  not available**, per the Bass §2 draw contract above · the register
  consequence (interacts with F-WALK-REG). BC gate: `RepeatedNote` and the
  shipped `ChordToneWalk` both byte-identical.
- **Narrow the bass octave band** — Bass §2 samples `octaveMin-1..octaveMin+1`
  and IGNORES `octaveMax`. Changes every bass render, so it is not a rider on
  anything (F-WALK-REG, BASS-WALK-1). **Now scheduled as B3 — BASS-REG-1**
  (below), which also absorbs WALK-2.
- **Track composition ORDER as a transversal contract** — raised at B0. POCKET-1
  introduced the package's first composer→composer data dependency and with it a
  real ordering requirement (Rhythm before Bassline). It is documented today in
  Bass §3.7 and Orchestration §5; whether it should be promoted to
  `SSoT_CONTRACTS.md` is undecided. Deliberately NOT promoted at B0.
- **F-IVT-STALE option (b)** — repair the `InternalsVisibleTo` assembly name and
  revert the public test seams to `internal`. B0 took option (a) (consecrate
  `public`; Orchestration §5.6). Needs the real test `.asmdef` name and a full
  suite re-run, so it belongs to a batch with code.
- ~~**MGP-ALWTTT-BASS-SOLO-1 — bass-without-backing progression sourcing**~~ —
  **SHIPPED 2026-07-26** as D-SOLO-SRC=A (the host supplies the default) /
  D-SOLO-SURF=A2 (a trailing optional `defaultProgression` on
  `GenerateSinglePart` that pre-seeds the shared cache). The preliminary
  host-side inclination recorded when the candidate was raised is what shipped.
  The bass gained no harmonic field: the shared-progression ownership contract
  is unchanged. Authority: `runtime/SSoT_Runtime_Generation_Orchestration.md`
  §5.5 and `runtime/SSoT_Composer_Bass_Track.md` §1. Not a CA-arc feature —
  recorded here only because the candidate was raised on this roadmap.
- **Admit Tier-2 figures into the §8.5 Random pool** — deferred since CA-T2
  (D-T2-POOL=A′).
- **Move the `ChordExpressionType` tail pin** out of the bass test file into the
  chord articulation tests, where the enum lives (OD-BOSSA-6 alternative B;
  cosmetic, not blocking).

### Batch MGP-ALWTTT-BASS-POCKET-1 — rhythm-coupled bass (SlapPocket)
**Status: DONE (2026-07-25; tests + 9 smoke gates green; documentation applied
at B0, 2026-07-26).** Opt-in per-event SUBSTITUTION of the bass figure by the
Rhythm track's published onsets.

Decisions locked: **D-PKT-SRC=B** — the source is a new `GenContext` onset
channel published by `RhythmTrackComposer` on the GRID path only, which makes
this the package's first composer→composer DATA dependency;
**D-PKT-WHAT=SlapPocket** (kick family → slap on the §2 selected note; snare
family → pop); **D-PKT-POP-PITCH=A** (pop = note + 12); **D-PKT-VEL=A** (the
DRUM step's velocity, which is what makes the line breathe with the drummer);
**D-PKT-GATE=A** (`min(gap, remaining window, 0.5 beat)`); **D-PKT-EXPR=A**
(a window without triggers renders the resolved figure exactly as decoupled, so
pocket and figures mix); **D-PKT-ORDER=A** (order-sensitive by design: Rhythm
before Bassline in `Part.Tracks`; the CONSUMER owns the degrade path);
**D-PKT-HOME=A** (composer-side, like BASS-WALK-1).

Load-bearing properties: **zero new `ctx.rng` draws** (the plan is a pure
function of published onsets and the event window, running after both §2 draws
— the same structural argument as D-WALK-RNG=A), and **degradation pinned as
BYTE-identity**, which holds structurally because the CA-V1 roller rolls per
event whether or not its result is used. Emission was restructured into a
segment list drained by ONE unconditional `Emit`; nothing entered
`ChordExpressionType`, so nothing entered the §8.5 pool.

Consumer hash duty discharged into
`reference/cross-project/ALWTTT/Handoff_MGP_POCKET.md`.
Governed by `runtime/SSoT_Composer_Bass_Track.md` §3.7,
`runtime/SSoT_Composer_Rhythm_Track.md` §3bis,
`runtime/SSoT_Runtime_Generation_Orchestration.md` §5.

Still open on record (not part of this batch): Accent mode (velocity-only
coupling), an explicit drummer-binding field, publication from the procedural
and legacy rhythm paths, and pop pitch = upper chord tone.

### Batch MGP-ALWTTT-BASS-POCKET-2 — pocket velocity and lanes
**Status: DONE (2026-07-25; 27 tests + 11 smoke gates green; documentation
applied at B0).**

Motivated by the POCKET-1 manual smoke: against a softly-authored Latin kit,
slaps and especially pops read weak, and the pattern's backbeat is `SideStick`,
which the v1 trigger families exclude on purpose.

Decisions resolved: **D-PKT-VEL2 = B** (additive per-class offsets on the card,
pre-clamp 1..127, default 0); **D-PKT-LANES2 = C** with **serialization C1**
(opt-in toggle plus two lane lists that replace the families; empty list
disables the class; a lane in both lists is a pop).

Alternatives on record as NOT taken: VEL2=C (float scale + curve) — deferred,
no content demanded it; VEL2=D (revert D-PKT-VEL=A) — rejected, would remove
the "breathes with the drummer" property that motivated the mode;
LANES2=B (fold `SideStick` into the snare family) — rejected, it hardcodes a
genre opinion into the package where the same result is a content decision.

RESOLVED in B3 — BASS-REG-1 (2026-07-27): the band was narrowed and both
ceiling instances capped. See B3 below.

### B3 — BASS-REG-1: bass register, in one batch (ABSORBS MGP-ALWTTT-BASS-WALK-2)
**Status: DONE (2026-07-27). Register phase: tests green 2026-07-27, parity
smoke waived by owner decision. WALK-2 phase: tests green 2026-07-27 (13 new),
smoke validated 2026-07-27.**
D-REG-0=A split the batch in two — the register decisions were prerequisites
for designing the improvised walk — and both halves are now shipped. B3 closes
the bass thread of the CA arc. This is the only bass batch with a real impact radius on the
register, so it takes every register question at once rather than letting two
batches each "not fix it".

Scope:
- Both instances of **F-WALK-REG**: the walk's upward stack (ceiling ~a fifth
  above the drawn root) and SlapPocket's uncapped pop (+12).
- Honour `MIDIInstrumentSO.octaveMax`, which the bass does not consult at all
  today (unlike the chord and melody composers, which use
  `octaveMin-1 .. octaveMax-1`).
- Narrow the §2 band `octaveMin-1 .. octaveMin+1`. This changes every bass
  render; it does NOT change the determinism surface (the octave draw keeps its
  count and order, only its range).
- The WALK-2 ask itself: a line that reads as an *improvised* walking bass, with
  variation between bars. Decisions still to resolve: note vocabulary (chord
  tones only vs chromatic/diatonic approach notes into the next root), where it
  lives (composer-side like BASS-WALK-1, or a new figure), the selection surface
  (a third `BassArpeggioToneMode` value, or a new field), and the variation
  source — **`ctx.rng` is not available**, per the Bass §2 draw contract, so it
  must be a seed-derived substream (the CA-V1 pattern).

Surface note for **D-W2-SURF**: `BasslineCardConfigSO` already carries a
"Pocket Coupling" header with SIX fields (`pocketMode` + the five POCKET-2
fields). Any new bass surface must account for that, not assume a bare card.

BC gate: `RepeatedNote`, the shipped `ChordToneWalk`, and `pocketMode = Off`
all byte-identical.

**Outcome of the register phase.** Three of the four scope bullets are done:
both F-WALK-REG instances capped, `octaveMax` honoured on both surfaces, the §2
band narrowed to two octaves. The fourth — the WALK-2 ask itself — remained,
with its four decisions open (vocabulary, home, selection surface, variation
substream), now designed against a bounded register.

**BC gate, reinterpreted (on record).** As written, the gate («`RepeatedNote`,
the shipped `ChordToneWalk`, and `pocketMode = Off` all byte-identical»)
contradicted the batch's own «narrow the band» bullet, which changes every bass
render by construction. Reading adopted: the gate binds the WALK-2 SURFACE
ADDITIONS — no new mode may perturb the existing ones — and does not bind the
register decisions D-REG-1..4, which are the batch's declared change. Anyone
reopening this should not read the shipped render change as a gate violation.

**Outcome of the WALK-2 phase (closes B3).** Decisions: **D-W2-VOCAB=B** —
chord tones on the middles plus a chromatic (±1) or whole-step (±2) approach
note into the NEXT event's root; the walk's signature move. **D-W2-LAST=A** —
the last event wraps its approach to the first event's root (loop-friendly).
**D-W2-HOME=A** — composer-side pitch planning (`BuildWalkLine`, pure seam)
over the ENGINE's own arpeggio grid (`PlanHits` called composer-side with
`noteCount: 1`), emitted as 1-note Block segments through the single
unconditional `Emit`; the engine and `ChordExpressionType` are untouched,
Backing §8 holds verbatim. **D-W2-SURF=A** —
`BassArpeggioToneMode.ImprovisedWalk = 2`, append-only; same activation gate as
WALK-1; `ArpeggioDown` biases contour ties downward. **D-W2-RNG=B** —
variation by pure `(ResolveWalkSeed(trackSeed), eventIndex, hitIndex)` mix, the
VelocityJitter idiom: no stream exists, so the always-roll discipline the CA-V1
roller needs is unnecessary here by construction. **D-W2-POCKET=A** — the §3.7
pocket substitution stands; pinned as byte-identity between both walk modes
under full pocket coverage. **D-W2-REG** — per-note −12 fold under the
D-REG-1=C ceiling; approach notes may dip below the band floor.

BC gate discharged: `RepeatedNote`, the shipped `ChordToneWalk` and
`pocketMode = Off` are byte-identical — structurally (the new branch hangs off
the new enum value; `walkSeed` is a pure hash read only there) and empirically
(the four pre-existing suites run unmodified; plus an explicit inertness gate
under non-arpeggio figures).

Alternatives on record as NOT taken: a new engine figure (breaks the §8
RNG-free contract or the seam signature); the WALK-1 "bigger playable +
cycling" route (Emit's pitch sort destroys a planned contour); a stateful walk
substream (demands always-roll discipline the pure mix obviates).

Governed by `runtime/SSoT_Composer_Bass_Track.md` §3.6bis and
`runtime/SSoT_Runtime_Generation_Orchestration.md` §5.1 (`ResolveWalkSeed`).

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

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
- `BasslineCardConfigSO.randomRerollChance : float` (`[Range(0,1)]`, default `1`)
  — CA-V1, D-V1-BASS=B
- `BasslineCardConfigSO.randomFigureWeights : List<ChordExpressionWeight>`
  (default empty) — CA-V1, D-V1-BASS=B
- `BasslineCardConfigSO.velocityJitter : int` (`[Range(0,32)]`, default `0`)
  — CA-V1
- `BasslineCardConfigSO.arpeggioToneMode : BassArpeggioToneMode`
  (default `RepeatedNote`) — BASS-WALK-1, D-WALK-SURF=A. Bass-only enum
  declared alongside the card; append-only (`RepeatedNote = 0`,
  `ChordToneWalk = 1`), never renumbered.

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

**The repeated-note pulse above is the DEFAULT reading, not the only one.**
BASS-WALK-1 shipped the chord-tone walk that SD-F2-2 deferred, as an opt-in
selected by `arpeggioToneMode = ChordToneWalk` (§3.1). See §3.6.

**Register-selective figures on a monophonic line.** The Tier-2
register-selective figures (`BassUpperSplit`, `Bossa` — Backing §8.6) split a
voicing into a low anchor and upper voices, which a 1-note bass voicing cannot
express. Selecting either on a bassline card therefore degrades to `Block` for
every event, by the articulator's own ≤1-note rule — no bass-side branch, no
silence, and no interaction with the walk (the walk builds its 3-note playable
only for the arpeggio figures). Nothing was added to the bass composer for
CA-T2-BOSSA or CA-T2-BOSSA-V2.

**Consequence for the CA-V1 roll (§3.5).** In `RepeatedNote` mode `ArpeggioUp`
and `ArpeggioDown` are indistinguishable on a 1-note voicing, so the default
uniform six-figure pool gives the repeated-note pulse effectively double weight
on the bass. This is a known musical bias, not a defect; `randomFigureWeights`
on the bassline card is the intended correction and the card tooltip says so.
**In `ChordToneWalk` mode the bias does not arise** — the two directions walk
genuinely different pitch sequences (§3.6, test-pinned), so the uniform pool is
already balanced.

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

### 3.5 Seeded variation (CA-V1 — supersedes D6)

**The ARTIC-1 `D6 = degrade-only` limitation is LIFTED.** The bass now resolves
both selection sentinels itself instead of leaking them to the articulator:

- `ChordExpressionType.Random` and `ArpeggioRate.Random` are rolled per chord
  event by the bass's own `RandomArticulationRoller`, constructed whenever
  either sentinel is selected. Engine semantics, pool rules, draw discipline and
  granularity are the Backing SSoT's §8.5 verbatim — this document adds no rule
  of its own.
- **Substream independence is structural.** The bass derives
  `ResolveArticulationSeed` / `ResolveArticulationRateSeed` /
  `ResolveVelocityJitterSeed` from the BASS `ctx.trackSeed`, and
  `ResolveTrackSeed*` already folds in role and musicianId — so backing and bass
  on the same part with the same base seed can never share a roll sequence.
  Test-pinned (`ArticulationSubstreams_DifferBetweenBackingAndBass`).
- **The §2 rng contract is untouched.** None of this reads or advances
  `ctx.rng`: the roll runs on seed-derived substreams and the jitter is a pure
  mix (Backing §8.7). The note-selection loop keeps its exact per-event draw
  count and order (1 root mode / 2 chord-tone mode), unchanged by CA-V1 as it
  was by CA-F2.
- `velocityJitter` behaves exactly as Backing §8.7 describes, over the
  monophonic hits.

**Verification note.** The bass roll has no unit-level end-to-end pin: the test
file works at pure seams (no `Compose` fixture with a `GenContext`), so the
tests cover substream separation and sentinel pass-through, and the end-to-end
exercise is the Bassline row of the composition smoke window (which exposes the
same knobs since CA-V1).

### 3.6 Chord-tone walk (BASS-WALK-1 — supersedes the SD-F2-2 deferral)

An OPT-IN pitch-selecting reading of the arpeggio figures, selected by
`arpeggioToneMode = ChordToneWalk` (D-WALK-SURF=A). Default `RepeatedNote`
keeps §3.3 semantics verbatim.

**Where it lives (D-WALK-HOME=A).** The walk is built on the BASS side, not in
the engine: when the resolved figure is `ArpeggioUp`/`ArpeggioDown` and walk
mode is on, the composer hands the SAME single unconditional `Emit` a 3-note
`playable` instead of a 1-note one, and the articulator's existing
`k % noteCount` cycling does the walking. No articulation figure was added, no
seam signature changed, and the engine remains pitch-preserving — it selects
among the notes it is given, exactly as it already did for the backing track.

**The voicing (D-WALK-TONES / D-WALK-ANCHOR).** `BassTrackComposer.BuildWalkVoicing`
takes the first three entries of the event's `chordPcs` (root, 3rd, 5th; a 7th
in the alphabet is deliberately dropped) and stacks them strictly ascending from
the root at the octave the selection loop ALREADY DREW — each tone lifted one
octave if it would otherwise fall at or below the previous note. Strict ascent
means the engine's pitch sort is a no-op, so `ArpeggioUp` reads root → 3rd → 5th
and `ArpeggioDown` reads 5th → 3rd → root (D-WALK-DIR: the engine's sort order
is accepted as-is, matching the backing track's meaning of the two figures).

**Determinism (D-WALK-RNG=A) — the load-bearing property.** The walk adds ZERO
rng draws. `BuildWalkVoicing` is a pure function of `(chordPcs, octave)`; the
3rd and 5th are derived from the chord, not drawn, and the register comes from
the octave draw that §2 already specifies. The branch runs AFTER both selection
draws and reads no rng, so the §2 contract (exactly ONE draw per event in root
mode, TWO in chord-tone mode, in that order) is intact **structurally**, not
merely empirically.

**Monophony guard (D-WALK-FIT=A).** An event shorter than one arpeggio hit
degrades to `Block` inside the engine — and `Block` over a 3-note playable would
emit a CHORD, breaking the monophonic line. The composer therefore consults
`ChordArticulator.ArpeggioFits(durBeats, rate)`, the engine's exposed degrade
predicate (Backing §8.4), and falls back to the 1-note playable when it returns
false. Predicate/plan agreement is test-pinned; if the engine's degrade rule ever
changes, that test is the drift detector.

**Interaction with §3.5.** Orthogonal. The walk is chosen per event from the
figure the roller already resolved; it changes which pitch each hit plays, never
which figure or rate was rolled, and the velocity jitter still applies per hit
over the walked notes.

**Simplification on record.** The walk is always root-anchored, including in the
`randomChordTone` constructor mode (unreachable today — `BassTrackComposerFactory`
hardcodes `randomChordTone: false`). In that mode the tone draw still executes,
preserving the §2 draw order literally, but its result governs only the
non-arpeggio figures. Revisit if the factory ever exposes the flag.

**Register consequence (F-WALK-REG, on record).** The walk anchors at the root
and stacks UPWARD, so the highest note of an event sits a fifth (6-8 semitones)
above the drawn root instead of on it. The §2 octave band is
`octaveMin-1 .. octaveMin+1` (three octaves, sampled per event, derived from
`MIDIInstrumentSO.octaveMin`; note that the bass IGNORES `octaveMax`, unlike the
chord and melody composers, which use `octaveMin-1 .. octaveMax-1`), so walk mode
raises the effective ceiling of the line from `octaveMin+1` to roughly a fifth
above it. With `octaveMin = 2` that is about MIDI 59 -> 67. This is a
CONSEQUENCE of D-WALK-ANCHOR, not a defect, but it was not anticipated when the
decision was recorded: authored content may need a lower `octaveMin` on the bass
instrument asset when walk mode is on. Narrowing the bass octave band itself
(the pre-existing three-octave spread) is recorded as a separate candidate; it
would change output for every bass render and must not be folded in silently.
Note it would NOT change the determinism surface: the octave draw would keep its
count and order, only its range.

Test surface: `Tests/Editor/BassTrackComposer_ArticulationTests.cs`
(voicing stacking incl. the wrapping case; root anchoring + purity; triad-only
truncation; Up ≠ Down under walk vs Up ≡ Down under `RepeatedNote`;
`ArpeggioFits` ↔ engine degrade equivalence; the monophony guard; enum defaults
and the "no new `ChordExpressionType` member" pin).

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
- the bassline card's field set changes (§3.1) — CA-V1 already extended it
  beyond the original articulation pair (reroll chance, figure weights, jitter);
- the bass's seeded-variation policy changes (§3.5): which sentinels the bass
  resolves itself, the substream derivation, or the D6-superseded claim that the
  bass no longer degrades `Random`;
- the per-render override policy (bass = warn + ignore) or the shared-progression
  readback changes (MGP-ALWTTT-DBG-1+3).
- the chord-tone walk changes (§3.6): the opt-in surface, the voicing
  construction (tone set, anchoring, stacking), the zero-new-draws property,
  or the `ArpeggioFits` monophony guard,

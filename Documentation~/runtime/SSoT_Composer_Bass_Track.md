# SSoT — Bass Track Composer

> Runtime authority for `BassTrackComposer` and its authoring surface. The
> Tier-1 articulation ENGINE contract lives in
> `runtime/SSoT_Composer_Backing_Track.md` §8 and is not duplicated here; this
> document owns the bass CONSUMER semantics (CA-F2, D-PRIO=A Feature 2).

## 0. Scope & governed surfaces

Governs:
- `Runtime/CoreScripts/Composition/Composers/BassTrackComposer.cs`
- `Runtime/CoreScripts/Composition/Data/BasslineCardConfigSO.cs`
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs` (CA-F2 — §3.1–§3.6)
- `Tests/Editor/BassTrackComposer_PocketTests.cs` (MGP-ALWTTT-BASS-POCKET-1/2 —
  §3.7 / §3.7.1)
- `Tests/Editor/BassTrackComposer_RegisterTests.cs` (B3 BASS-REG-1 — §2 band and
  ceiling, §3.6 walk fold, §3.7.1 pop fold)
- `Tests/Editor/BassTrackComposer_WalkImprovTests.cs` (B3 WALK-2 — §3.6bis)
- `Tests/Editor/BassTrackComposer_SelfPocketTests.cs` (MGP-ALWTTT-BASS-SLAPFIG-1
  — §3.7.2)
- `Tests/Editor/BassTrackComposer_SelfPocketVocabularyTests.cs`
  (MGP-ALWTTT-BASS-SLAPFIG-2/2b, written retroactively at BEND-1 step 1 —
  §3.7.3)
- `Tests/Editor/BassTrackComposer_LegatoBendTests.cs`
  (MGP-ALWTTT-BASS-BEND-1 — §3.7.3)
- `Runtime/CoreScripts/Composition/Articulation/PitchBendWriter.cs` and
  `Tests/Editor/PitchBendWriterTests.cs` (MGP-ALWTTT-BASS-BEND-1). Homed here
  because the bass is the writer's only in-package consumer today; the
  CONTRACT is `SSoT_CONTRACTS.md` §11 and that is the authority. If the melody
  composer adopts it, the home moves.

This list matches `ssot_manifest.yaml` exactly. The three test files added on
2026-07-28 were already cited by name in the body (§3.6, §3.6bis, §3.7) and were
missing here only from this header; per the bass SSoT's own convention (M-1=A)
this document lists its test files and its sibling SSoTs do not.

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
- **Normalization-order hazard — CLOSED for the shared progression
  (MGP-ALWTTT-BASS-ORDER-1).** The backing composer now always composes first
  (PASS 0), so the bass always sees the TS-normalized, re-qualified runtime
  clone when a Backing row exists. Two residues remain, both recorded and
  unchanged: (a) the bass's own `cfg.Parameters.Pattern` fallback receives
  NEITHER transform — it is private harmony, outside the shared channel; and
  (b) on a backing-less part the SOLO-1 seed path still seeds AS-IS
  (D-SOLO-NORM=A). Under an articulation-only Backing row the seeded default
  now DOES receive both transforms, because the Backing composer consumes it
  via its shared-cache step — a strict improvement over D-SOLO-NORM=A, on
  record.
- **`degreeAccidental` IS applied (MGP-TONALITY-1, D-TON10).** The degree root
  is `TransposeNoteName(scaleNames[(int)ce.degree], ce.degreeAccidental)`, and
  the improvised walk's next-root lookup (the approach-note target) applies the
  same transform to the next event. This is the shared chord-identity law of
  `SSoT_CONTRACTS.md` §13. Before MGP-TONALITY-1 the bass derived both the chord
  root and the walk's approach target from the degree alone, so on any
  accidental-bearing progression it harmonized against a chord a semitone away
  from the backing track's — a confirmed audible defect
  (`Prog_Min_Napolitana_bII`: backing `[ASharp D F]` against bass
  `[B DSharp FSharp]`, clashing on root, third and fifth at once). Sealed by the
  MGP-TONALITY-2 matrix at `beliefDiv == 0` across 476 cells.

**Order independence (MGP-ALWTTT-BASS-ORDER-1, D-ORD-MECH=A).** The bass no
longer depends on the Backing track's POSITION in the track list. The
orchestrator composes every Backing row in a dedicated PASS 0, before any
harmony consumer, and merges the per-track results afterwards in track-list
index order (`runtime/SSoT_Runtime_Generation_Orchestration.md` §5.7). The
shared progression is therefore always published by the time the bass reads
`ctx.GetProgressionForPart`, whatever the list order.

This closes **F-BASS-ORDER-1** (ALWTTT gig report, 2026-07-30): a Bassline row
placed BEFORE a Backing row whose harmony lived in its Style bundle (card
override / palette, invisible to `FindProgressionForPart`, which reads only
`Parameters.Pattern`) resolved to a null progression and rendered PERMANENT
SILENCE. Track-list order is a consumer-side identity concern (channels,
`ChannelRoles`, per-musician RNG streams) and consumers cannot reorder freely;
the fix is package-side scheduling, not a documentation caveat.

**Per-render override (Ask C, D-DBG4=A).** A `patternOverride` targeting the
Bassline track is **warn + ignore** in v1: the bass owns no pattern channel (it
renders the shared progression), so honoring an override here would create a
second mutation path into shared state. To change the bass's harmony, override
the **Backing** track — its override becomes the shared progression by the
existing don't-overwrite discipline, and the bass picks it up via
`ctx.GetProgressionForPart`. The bass reports `usesSharedProgression` +
`progressionRoman` (grid-site roman formatting) for the Ask A readback
(MGP-ALWTTT-DBG-1).

**Host-supplied default for backing-less parts (MGP-ALWTTT-BASS-SOLO-1,
D-SOLO-SRC=A / D-SOLO-SURF=A2).** The shared channel has exactly two
publishers — the backing composer (card override / palette / procedural) and
the authored fallback `SongOrchestrator.FindProgressionForPart`, which reads
the **Backing** track's `Pattern`. A part with a Bassline row and NO Backing
row therefore had no harmony source at all, and the bass rendered silence by
the null rule above. `GenerateSinglePart` now accepts a trailing optional
`ChordProgressionData defaultProgression`, pre-seeded into the per-render
shared-progression cache BEFORE the track loop, so every harmony consumer
(Bassline, Melody, Harmony) sees it via `ctx.GetProgressionForPart`.

The bass still owns no harmony: the parameter is a HOST channel into the
shared cache, not a bass surface. `BasslineCardConfigSO` gains no harmonic
field, and the D-DBG4=A warn+ignore on Bassline `patternOverride` is unchanged.

- **Guard (D-ORD-GUARD=A, supersedes D-SOLO-GUARD=A).** The guard is no longer
  "a Backing row exists" but "a Backing row carries a HARMONY SOURCE" — a
  static, draw-free sniff (`SongOrchestrator.BackingTrackCarriesHarmonySource`)
  over: per-render override of type `ChordProgressionData`, card
  `progressionOverride`, card palette with at least one valid entry
  (non-null progression AND weight > 0 — the exact `PickRandomProgression`
  filter), or authored `TrackParameters.Pattern`. A Backing row carrying NONE
  of these is ARTICULATION-ONLY (a future bossa / ska / power-chord bundle):
  the host default IS seeded, and the Backing composer consumes it through its
  own shared-cache step — one winner, no fork. When the sniff DOES find a
  source, the default is warn + ignore as before, because seeding under a
  real source would fork the render (the card-palette publish is guarded by
  "don't overwrite"). To impose harmony on a part whose Backing carries a
  source, use the per-render `patternOverride` on the Backing track
  (precedence step 0, imposes unconditionally).
- **Recorded edge (presence-based sniff).** A palette that looks valid to the
  sniff can still fail its TS-aware pick at compose time; the Backing then
  degrades to procedural and the suppressed default does NOT resurge. Not
  silence — a documented gap, matching pre-ORDER-1 "palette pick failed"
  semantics.
- **Normalization (D-SOLO-NORM=A).** The default is seeded AS-IS. TS
  normalization is the backing composer's site and does not run on this path,
  so this is a THIRD instance of the normalization-order hazard recorded above;
  hosts must author the default in the part's time signature.
- **Determinism (D-SOLO-DET).** Pure dictionary write: zero `ctx.rng` draws, no
  stream perturbation. A null default leaves the render byte-identical — pinned
  end-to-end by smoke gate 3 (seeded default ≡ same asset in the bass row's own
  `Pattern` slot, same seed, same notes).
- **Clone-on-seed.** The seeded value is a runtime clone (name-preserving, so
  the Ask-A readback stays meaningful), mirroring the override discipline: no
  runtime state points at the asset instance.

## 2. Note selection & rng contract

Per event, in `prog.events.OrderBy(startStep)` order: the degree root is
looked up in the part scale; the pitch class is the chord root (default) or a
random chord tone (`randomChordTone` ctor flag); the octave is drawn from a
narrow low band derived from BOTH declared instrument bounds (B3 BASS-REG-1).
The band is `octaveMin-1 .. min(octaveMin, octaveMax-1)` in DryWetMidi octaves
— two octaves — where the `-1` is the authored→DryWetMidi octave CONVERSION,
the same one behind the chord and melody composers'
`octaveMin-1 .. octaveMax-1`. In authored octaves the band therefore reads
`octaveMin .. octaveMin+1`, capped by the declared ceiling. A degenerate asset
(`octaveMax <= octaveMin`) collapses the band to one octave; it never inverts.
Resolved by the pure seam `BassTrackComposer.ResolveOctaveBand`.

**Determinism contract:** the selection loop draws from the shared `ctx.rng`
stream — exactly ONE draw per event in root mode (octave), exactly TWO in
chord-tone mode (tone, then octave), in that order. This draw count and order
is part of the composer's determinism/bit-identity surface and must not be
perturbed by any layer added around it. The articulation engine is RNG-free
by contract (§8 of the Backing SSoT) and therefore cannot perturb it.

**Register ceiling (D-REG-1=C).** `MIDIInstrumentSO.octaveMax` is a HARD
ceiling on everything the bass emits, not only on the draw: the ceiling is
`octaveMax * 12 + 11` (B at the top of the declared register, clamped to 127;
seam `ResolveRegisterCeiling`), and every structure built ABOVE the drawn note
— walk tops (§3.6), pops (§3.7) — is guaranteed to sit at or below it. The
ceiling wins over the band floor: low is safe on a bass. Downward folding
stops at the MIDI floor for every emission path EXCEPT the WALK-2 improvised
line, which since D-W2-FLOOR=B also folds UP under a floor one octave below
the §2 band (§3.6bis).

**Determinism note.** B3 changed the octave draw's RANGE, not its count or
order, so the §2 contract above is intact — but a given seed now selects a
different octave than pre-B3. Every bass render changed; this was the batch's
declared, decided outcome, not drift.

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
keeps §3.3 semantics verbatim. A third mode, `ImprovisedWalk`, plans a varying
line instead of the fixed cycle — see §3.6bis.

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

**Register (F-WALK-REG, first instance — RESOLVED in B3 BASS-REG-1).** The walk
anchors at the root and stacks UPWARD, so an event's highest note sits a fifth
(6-8 semitones) above the drawn root. Left uncapped, that raised the line's
ceiling above the §2 band and above the instrument's declared `octaveMax`,
which the bass did not consult at all. B3 closed both halves: the §2 band is
now ceiling-capped (§2), and `BuildWalkVoicing` gained a ceiling-aware overload
(D-REG-3=B) that transposes the WHOLE voicing down an octave while its top
exceeds the ceiling. A whole-voicing fold, never a per-note clamp: shape,
intervals, pitch-class order and strict ascent are preserved, so
D-WALK-ANCHOR's musical gesture survives the cap. The fold is pure and rng-free
— the D-WALK-RNG=A argument is untouched. The 2-argument
`BuildWalkVoicing(chordPcs, rootOct)` remains as a ceiling-free form (delegates
with `ceiling = int.MaxValue`) so the WALK-1 pins keep their meaning.

Authored-content note, now milder but still live: with a tight `octaveMax` the
walk folds rather than overshooting, which is audible as a register drop on the
affected events. Choosing `octaveMin`/`octaveMax` on the bass asset is a
content decision, not a package one.

Test surface: `Tests/Editor/BassTrackComposer_ArticulationTests.cs`
(voicing stacking incl. the wrapping case; root anchoring + purity; triad-only
truncation; Up ≠ Down under walk vs Up ≡ Down under `RepeatedNote`;
`ArpeggioFits` ↔ engine degrade equivalence; the monophony guard; enum defaults
and the "no new `ChordExpressionType` member" pin). B3 register behaviour lives
in `Tests/Editor/BassTrackComposer_RegisterTests.cs` (band, ceiling, pop fold,
walk fold, plus two orchestrator-level gates).

### 3.6bis Improvised walk (B3 WALK-2 — closes the WALK-2 ask)

An OPT-IN third reading of the arpeggio figures, selected by
`arpeggioToneMode = ImprovisedWalk` (D-W2-SURF=A; append-only value 2). Unlike
`ChordToneWalk`'s fixed root→3rd→5th cycle — identical bar to bar — this plans
a LINE that varies between bars: the "improvised walking bass" ask, delivered.

**Vocabulary (D-W2-VOCAB=B).** Per event, one pitch per arpeggio hit: hit 0
anchors the event root at the §2 drawn octave (the WALK-1 anchor); middle hits
are chord tones placed in the octave NEAREST the previous note (never
re-striking it) — usually the closest such tone, sometimes the 2nd/3rd
closest, with `ArpeggioDown` biasing equal-distance ties downward; the LAST hit
is a chromatic (±1) or whole-step (±2) approach note into the NEXT event's root
— the thing that makes a walk read as a walk. The next-root lookup mirrors the
selection loop's own degree lookup exactly (accidental-aware since
MGP-TONALITY-1 — the recorded accidental-blindness of this lookup no longer
holds, retired with D-TON10); the last event wraps to the first event's
root (D-W2-LAST=A, loop-friendly).

**Approach notes are expected out-of-key (audit note).** The improvised walk's
approach notes are deliberately chromatic and `TonalityAudit` will report them
as `OUT-OF-KEY`. That is correct: they sit on the last beat of a chord window
and resolve by semitone into the next root. It does NOT follow that every bass
red is an approach note. Under D-W2-LAST=A the walk emits exactly ONE approach
note per chord window, at the window's end, resolving by 1–2 semitones into the
next root; a red that is not at a window edge, or that does not resolve into
the next root, is a defect candidate — F-TON-WALK-DRIFT-1 presented exactly
this way. Judge bass reds by position and resolution, not by track alone.
A future refinement may tag them `origin=walk-approach` to separate intentional
chromaticism from defects in the audit counters; until then the MGP-TONALITY-2
runner infers the tag positionally rather than requiring it.

**Home (D-W2-HOME=A) — division of labor.** Composer-side, but the ENGINE still
owns rhythm and dynamics: the composer calls the public pure
`ChordArticulator.PlanHits(ArpeggioUp/Down, rate, …, noteCount: 1)` to obtain
the arpeggio grid with its accent curve and the event jitter (the returned
`NoteIndex` is ignored), and owns PITCHES only (`BuildWalkLine`, a pure static
seam). Each planned hit re-enters the single unconditional `Emit` as a 1-note
`Block` segment with jitter off: `BlockPlan` is a velocity passthrough (clamp
only, no accent curve), so the walk's dynamics are EXACTLY an arpeggio's, with
no double shaping. Nothing was added to the engine or to
`ChordExpressionType`; Backing §8 holds verbatim. The WALK-1 "hand the engine a
bigger playable" route was rejected for this mode: `Emit` pitch-sorts arpeggio
playables, which destroys a planned contour (WALK-1 survives only because its
stack is strictly ascending).

**Variation source (D-W2-RNG=B) — the load-bearing property.** ZERO rng draws,
and no stateful substream either: every choice is a PURE MIX of
`(walkSeed, eventIndex, hitIndex)` — the VelocityJitter idiom (lowbias32
avalanche, integer-only, runtime-stable, exactly pinnable) — where
`walkSeed = SongOrchestrator.ResolveWalkSeed(trackSeed)` (FNV-1a over
`"{trackSeed}|walk"`). Because no stream exists, no draw-count discipline is
needed: toggling pocket, event lengths, or any conditional branch cannot shift
a later event's line, BY CONSTRUCTION rather than by carefully maintained
always-roll discipline. The §2 `ctx.rng` contract is intact structurally (the
branch runs after both selection draws and reads no rng — the D-WALK-RNG=A
argument). Same seed ⇒ same line (the held-loop replay guarantee); a later
event over the same chord walks a different line (`eventIndex` is in the mix).

**Register (D-W2-REG, under D-REG-1=C; floor added by D-W2-FLOOR=B).** Every
planned note folds −12 while above the register ceiling AND +12 while below
the walk floor — the per-note adaptation of D-REG-3=B (the unit here is the
note; there is no voicing shape to preserve). Folding is octave-wise, so pitch
class, chord-tone membership and the approach interval are invariant under it.

The walk floor sits ONE OCTAVE BELOW the §2 band floor (`C` at `minOct`, minus
12; seam `FoldIntoRegister`). The octave of slack is deliberate: a single
approach note may still dip under the band — low is safe on a bass — while a
window's CUMULATIVE descent cannot leave the register. The CEILING WINS: the
up-fold never lifts a note above the ceiling, so a degenerate asset
(floor >= ceiling) degrades to the pre-existing ceiling-only behaviour rather
than oscillating. Under a tight ceiling — or, now, a tight band — a fold may
land on the previous pitch; the ceiling wins over variety.

**Why the floor exists (F-TON-WALK-DRIFT-1).** Middle-hit selection is
prev-relative only: each candidate is `NearestPitch(pc, prev)` with `prev`
excluded, and the resulting candidate set is asymmetric — from G1 over
[G A♯ D] the options are +3 and −5, weighted ≈0.55/0.45 by the mix, an
expected drift of ≈ −0.6 semitones per hit. With no lower bound the only stop
was the MIDI floor inside the fold, so a long window (24 hits in 6/8 over a
normalized progression) walked the line four octaves under the instrument and
bottomed out at note 0 — whose pitch class is C by construction, which is why
only Lydian surfaced it as OUT-OF-KEY. The floor CONTAINS this; it does not
remove the drift. See D-W2-DRIFT below.

**D-W2-DRIFT (open).** The selection asymmetry itself is unresolved. Removing
it (re-anchoring to the event root per bar, or symmetrizing the candidate set)
changes the walk's musical character and needs its own decision and a listening
pass. Deferred to a follow-up batch; until then the line still tends downward
and rests against the floor in long windows.

**Scope.** `BuildWalkVoicing` (ChordToneWalk, §3.6) is NOT affected and stays
byte-identical; the floor is a WALK-2 (`ImprovisedWalk`) law only. Callers that
pass no floor (`floor = int.MinValue`, the default) get the pre-fix
ceiling-only behaviour — which is what the WALK-2 unit tests exercise.

**Gating and interactions.** Same activation gate as §3.6 (resolved figure
`ArpeggioUp`/`ArpeggioDown`, at least 2 chord pcs, `ArpeggioFits` true);
anything else renders the 1-note playable, byte-identical to `RepeatedNote`
(test-pinned). Pocketed events bypass the walk — either mode — per §3.7
(D-W2-POCKET=A), pinned as byte-identity between both walk modes under full
pocket coverage. For the next-root lookahead the event enumeration is
materialized (`prog.events.OrderBy(startStep).ToList()`); OrderBy is stable, so
iteration order — and with it the §2 draw order — is identical to the previous
foreach.

Test surface: `Tests/Editor/BassTrackComposer_WalkImprovTests.cs`
(BuildWalkLine purity / anchor / vocabulary / approach / variation / ceiling —
every assertion is a property invariant under octave-wise folding, and each
calls BuildWalkLine WITHOUT the floor argument, so the suite pins the
ceiling-only path; a floor-specific test belongs with D-W2-DRIFT;
NearestPitch; the card surface pin; and four orchestrator-level gates in the
Dbg1Fixtures + FNV idiom).

### 3.7 SlapPocket coupling (MGP-ALWTTT-BASS-POCKET-1)

An OPT-IN, per-event SUBSTITUTION of the bass figure by the Rhythm track's
published onsets, emulating funk slap bass. Selected by
`BasslineCardConfigSO.pocketMode = SlapPocket` (default `Off`; bass-only
append-only enum `PocketCouplingMode { Off = 0, SlapPocket = 1 }`). The
slap/pop TIMBRE is the bass patch's job (e.g. GM Slap Bass on the
`MIDIInstrumentSO`); this mode shapes timing, register and dynamics only.

**Source (D-PKT-SRC=B).** The bass consumes
`ctx.GetRhythmOnsetsForPart(part)` — the onset channel the rhythm composer
publishes for its resolved GRID pattern (Rhythm SSoT §3bis; Orchestration
SSoT §5). Fetched ONCE at `Compose` entry; a null or empty result means "no
source". Multi-drummer: the channel returns the FIRST non-empty publication in
composition (track-list) order.

**What it renders (D-PKT-WHAT=SlapPocket).** Per chord event, if the event
window `[start, start+len)` contains kick/snare onsets, the figure is replaced
by:
- kick family (`AcousticBassDrum`, `BassDrum1`) → SLAP: the event's §2
  selected note;
- snare family (`AcousticSnare`, `ElectricSnare`) → POP: the same note one
  octave up (+12, D-PKT-POP-PITCH=A). Side stick is deliberately not a pop
  trigger in v1.
Velocity is the DRUM step's resolved velocity (D-PKT-VEL=A), not the chord
event's. Hit length is `min(gap to next hit, remaining window, 0.5 beat)`
(D-PKT-GATE=A, `PocketMaxGateBeats`). Same-beat collisions: pop wins over
slap outright (flag AND velocity); within one class the max velocity wins.
Classification uses the SEMANTIC lane (pre kit resolution), so
PERC-FALLBACK-1 substitutions cannot re-classify a hit.

**Per-event fallback (D-PKT-EXPR=A).** A window WITHOUT kick/snare onsets
renders the resolved figure exactly as decoupled — pocket and figures can mix
within one render. Pocketed events bypass the figure AND the walk — either walk
mode, D-W2-POCKET=A (`arpeggioToneMode` does not participate: pocket hits are
1-note `Block` segments). Pinned as byte-identity between `ChordToneWalk` and
`ImprovisedWalk` under full pocket coverage.

**Degradation contract.** No published source — no Rhythm track in the part,
the Rhythm track composes AFTER the bass (see the order hazard below), or the
rhythm resolved to a procedural/legacy path — degrades to the decoupled
figure with AT MOST one warning per `Compose`; never an error, never
silence. **Test-pinned as BYTE-identity:** pocket-on-without-source ≡
pocket-off (`PocketOn_WithoutAnyRhythmTrack_IsByteIdenticalToOff`). This
holds structurally: the CA-V1 roller rolls per event whether or not its
result is used, so source availability can never shift the roll stream.

**Order hazard (D-PKT-ORDER=A, companion to the §1 normalization hazard).**
The publication exists only if the Rhythm track composed first (track-list
order). Consumers must place Rhythm before Bassline in `Part.Tracks`; hosts
that add a Rhythm track after a bass-only render re-render the part (the
existing host-side re-render pattern) to obtain the pocketed bass.

**Determinism (the load-bearing property).** ZERO new `ctx.rng` draws: the
pocket branch runs AFTER both §2 selection draws and reads no rng — the plan
(`BuildPocketPlan`) is a pure function of (published onsets, event window),
same structural argument as D-WALK-RNG=A. The §2 contract (1 draw root mode /
2 chord-tone mode, in order) is intact structurally. Jitter: the event-scoped
jitter is refolded per pocket hit (`ForEvent` chaining, a pure avalanche);
the decoupled path keeps the pre-batch event jitter verbatim.

**Emission structure.** The per-event body now builds a SEGMENT list — one
segment (decoupled: the event span + resolved figure) or N `Block` segments
(pocketed: one per planned hit) — drained by ONE unconditional
`IChordArticulator.Emit` call site. This is the SD-F2-1 anti-divergence
discipline restructured over segments; the engine remains RNG-free and
pitch-preserving, nothing was added to `ChordExpressionType`.

**Hash duty (consumer invariant, on record).** When `pocketMode != Off`, the
resolved rhythm pattern is a hash-relevant INPUT of the bass track. ALWTTT
extends `ComputeTrackInputsHashesForPart` accordingly (the drummer's resolved
pattern identity is already available in its Rhythm-track Ask A readback).

Test surface: `Tests/Editor/BassTrackComposer_PocketTests.cs` (planner
windowing/classification/gate/dedupe/purity; card surface pins; the
orchestrator-level degrade gate, order-hazard degrade, and engaged-pocket
determinism) and `Tests/Editor/RhythmTrackComposer_OnsetPublicationTests.cs`
(publication side).

#### 3.7.1 Pocket shaping (MGP-ALWTTT-BASS-POCKET-2)

Two OPT-IN refinements of §3.7, both inert at their defaults. Every field
below lives inside the `pocketMode = SlapPocket` branch, so the
"pocket-on-without-source ≡ pocket-off" degrade guarantee of §3.7 is
structurally unaffected by them (test-pinned and smoke-pinned; see the
POCKET-2 verification record).

**Velocity shaping (D-PKT-VEL2 = B).** `pocketSlapBoost` and
`pocketPopBoost` (`int`, `[Range(-64, 64)]`, default `0`) are ADDITIVE
per-class offsets applied to the drum step's resolved velocity, clamped to
1..127:

    hitVelocity = Clamp(onsetVelocity + (pop ? popBoost : slapBoost), 1, 127)

Published onsets already arrive clamped to 1..127 (the publisher resolves the
`StepState.velocity == 0` sentinel to the lane's `defaultVelocity` and clamps),
so a boost of `0` is an EXACT identity — not an approximate one. This is what
makes the default path byte-identical to POCKET-1 on the same content.

The two classes are independent: a pop-only positive boost is the intended fix
for the common case where pops (one octave up, §3.7 D-PKT-POP-PITCH=A) read
weaker than slaps at equal drum velocity against a softly-authored kit. The
boost does NOT touch the drum pattern; the drums remain the author's.

*Ordering note.* The boost is applied at classification time, inside the plan
builder, and is observationally equivalent to applying it after the same-beat
dedupe: the offset is uniform within a class, so the intra-class max-velocity
rule is invariant under it, and the cross-class pop-wins rule is unconditional
(it never compares velocities). The boost therefore changes no POCKET-1 pin.
It is applied BEFORE the per-event `VelocityJitter` refold, which clamps
independently.

**Trigger lanes (D-PKT-LANES2 = C, serialization C1).**
`pocketCustomLanes` (`bool`, default `false`) plus two
`List<GeneralMidiPercussion>` fields, `pocketSlapLanes` and `pocketPopLanes`
(both default empty):

- Toggle OFF (default) — the built-in v1 families apply exactly: slap =
  `AcousticBassDrum`, `BassDrum1`; pop = `AcousticSnare`, `ElectricSnare`;
  `SideStick` deliberately excluded. Every asset serialized before POCKET-2
  deserializes into this state.
- Toggle ON — the two lists REPLACE the families outright. They do not extend
  them: a lane absent from the list does not fall back to its family.
- An EMPTY list with the toggle on DISABLES that trigger class, which is how a
  pop-only or slap-only pocket is expressed.
- A lane present in BOTH lists classifies as POP. The pop membership test runs
  first, consistent with the same-beat pop-wins rule.

Matching is on the SEMANTIC authored lane, exactly as in v1 — i.e. before
per-kit resolution, so it is immune to PERC-FALLBACK-1 substitutions. This is
load-bearing and field-verified: with a kit that maps `SideStick` onto
`AcousticSnare`, a `SideStick`-driven pocket fires only when `SideStick` is in
the list, and does not fire via the resolved snare when it is not.

The typical Latin case: adding `SideStick` to `pocketPopLanes` lets a rim-click
backbeat drive the pop, which the v1 families exclude by design.

**Register (F-WALK-REG, second instance — RESOLVED in B3 BASS-REG-1).** A pop
is the selected note + 12. Uncapped, that let a pocketed bass exceed the §2
band by an octave and exceed the instrument's declared ceiling. B3 caps it
(D-REG-2=B): the pop fires at +12 when that fits the ceiling and **folds back
onto the selected note** when it does not. The fold is PITCH ONLY — pop
IDENTITY is decided upstream in `BuildPocketPlan` and is untouched:
classification (including the custom lane lists), `pocketPopBoost`, the
same-beat pop-wins rule and the D-PKT-GATE=A length all stand. The planner
never sees the fold; it happens at the emission call site via the pure seam
`ResolvePopNote`, so the POCKET-1/2 test surface keeps its meaning verbatim. A
folded pop is therefore still a pop dynamically and rhythmically — it just
sounds at the slap's pitch.

Side effect on record: `ResolvePopNote` also refuses to build a note above
MIDI 127, closing a latent out-of-range `Note.Get` on extreme assets that
pre-B3 code could reach.

#### 3.7.2 SelfPocket — autonomous slap/pop figure (MGP-ALWTTT-BASS-SLAPFIG-1)

**Surface (D-SFIG-SURF=A).** `PocketCouplingMode.SelfPocket = 2` (append-only;
`Off = 0` and `SlapPocket = 1` unchanged). SelfPocket produces the slap/pop
GESTURE with NO Rhythm track and NO cross-track read: it never calls
`ctx.GetRhythmOnsetsForPart`, so it cannot wake the consumer-side publication
duty on the ALWTTT boundary (§8.4 there). The two coupled modes are mutually
exclusive by the enum; the per-event branch keys on which source field is
non-null, so `Off` keeps both null and the loop body stays draw-for-draw AND
value-for-value the decoupled path.

Rejected: a new `ChordExpressionType` member. The Tier-1 articulation engine is
pitch-preserving and rng-free; the pop's +12 (and its ceiling fold) is composer
domain, not engine domain.

**Hit source (D-SFIG-PAT=A).** Two card fields on `BasslineCardConfigSO`:
`selfPocketSubdivision` (`Beat = 0` → 1.0 beat, `HalfBeat = 1` → 0.5 beat,
`QuarterBeat = 2` → 0.25 beat, MGP-ALWTTT-BASS-SLAPFIG-2b D-SF2B-GRID=A) and
`selfPocketPattern`, a cycled `List<SelfPocketStep>` whose alphabet is defined
in §3.7.3, default `[Slap, Pop]`. Unknown future subdivision members fall
through to `Beat` rather than throwing.

`QuarterBeat` exists because the classic-funk ghost vocabulary is a
SIXTEENTH-note idiom: the two canonical figures (a dead sixteenth immediately
before the pop; two dead sixteenths between sounding notes) are inexpressible
on `Beat` or `HalfBeat`. In 4/4 a 16-step pattern on `QuarterBeat` is exactly
one bar, and a 16-step pattern is the shortest that can place an
end-of-bar anticipation without the figure rotating against the meter. Candidate hits sit at
multiples of the subdivision step in PART beats, **anchored to the meter**
(part beat 0), intersected with the chord-event window `[start, start + len)`
— inclusive start, exclusive end, the `BuildPocketPlan` convention. The step is
chosen by the ABSOLUTE grid index modulo the pattern length, so the figure
keeps phase across chord changes exactly as SlapPocket's absolute drum onsets
do; splitting a window at a chord boundary yields the same hits as one
whole-bar plan (test-pinned). `Rest` skips.

**Velocity (D-SFIG-VEL=A, extended by D-SF2-VEL=B).** Base is the chord
EVENT's authored velocity (vs the drum step's in SlapPocket). Two laws, keyed
by articulation class:

- `Slap` / `Pop`: base + the EXISTING `pocketSlapBoost` / `pocketPopBoost`,
  clamped 1..127 — the D-PKT-VEL2=B law over a different base, verbatim.
- `Ghost` / `GhostPop` / `HammerOn` / `PullOff`: `round(base × class factor)`,
  clamped 1..127. **No boosts.** The factors are authored per card
  (D-SF2B-TUNE=A) and default to the shipped tuning.

  *Reach note (BEND-1, D-BEND-GEST=A).* Since a legato hit no longer emits a
  note-on (§3.7.3), `hammerOnVelocityFactor` and `pullOffVelocityFactor` are
  consumed ONLY on the degraded ORPHAN path. A bent tail inherits the
  carrier's velocity — pitch bend is channel state and carries no dynamics of
  its own. This is a declared loss, recorded on the card tooltips; restoring
  per-tail dynamics would require a re-attack, which is the thing the batch
  removes.

The two laws differ on purpose. Additive boosts do not scale past two classes:
a hot card (the gig's `(+64, +64)` on events authored at 100) drives every
boosted class into the 127 clamp and the dynamic relief disappears. A
multiplicative factor preserves proportion under any event velocity, and a
factor of exactly 1.0 for `Slap`/`Pop` is what makes a v1-only pattern
byte-identical to SLAPFIG-1.

**Everything downstream is SlapPocket verbatim.** The planner emits the same
`PocketHit` list, consumed by the same emission branch: 1-note `Block`
segments, pop = selected note +12 through `ResolvePopNote` (D-REG-2=B ceiling
fold, pop identity untouched), the D-PKT-GATE=A length rule
`min(gap to next PLANNED hit, remaining window, PocketMaxGateBeats)`, and the
per-hit jitter refold.

**Planner.** `BuildSelfPocketPlan(eventStart, eventLen, subdivision, pattern,
eventVelocity, slapBoost, popBoost)` — public pure test seam, the
`BuildPocketPlan` / `BuildWalkVoicing` idiom. ZERO rng, zero state, zero
cross-track reads. It runs at the same per-event position as the SlapPocket
plan: AFTER both §2 selection draws, reading no rng — the POCKET-1 structural
argument verbatim.

**Degrade.** An empty or all-`Rest` pattern warns ONCE at entry and leaves
`selfPocketPattern` null, so the whole render takes the decoupled figure —
byte-identical to `Off` (test-pinned). Never an error, never silence. A window
shorter than one grid step yields an empty plan, which is the caller's
per-event fallback (identical to an empty SlapPocket plan).

**Timbre.** As SlapPocket: the slap/pop TIMBRE is the bass patch's
(GM Slap Bass 1/2 on the `MIDIInstrumentSO`). This mode shapes timing,
register and dynamics only.

**Vocabulary.** See §3.7.3.

**Deferred (post-SLAPFIG-2, not implemented).** Per-step velocity or accent
curves; swing/shuffle placement (doctrine fixed in §3.7.3, implementation
open); octave-jump variants beyond the single +12 pop and the alternate pop
intervals (+15/+16/+19); a `LeftHandSlap` class; compound macros
(open-hammer-pluck, machine-gun triplet, trill) which would require a step
that emits several events; bar-aware pattern selection and end-of-phrase
ornament substitution (BASS-PHRASE-1).

#### 3.7.3 SelfPocket articulation vocabulary and legato (MGP-ALWTTT-BASS-SLAPFIG-2 / BEND-1)

**Alphabet (D-SF2-VOCAB=C).** `SelfPocketStep` is append-only over the v1
`{Slap = 0, Pop = 1, Rest = 2}`:

| Member | Sounds as | Velocity | Gate ceiling | Status |
|---|---|---|---|---|
| `Slap = 0` | note on the selected pitch | base + `pocketSlapBoost` | `PocketMaxGateBeats` | v1 |
| `Pop = 1` | note, `ResolvePopNote` (+12, ceiling-folded) | base + `pocketPopBoost` | `PocketMaxGateBeats` | v1 |
| `Rest = 2` | — | — | — | v1 |
| `Ghost = 3` | note on the selected pitch | base × `ghostVelocityFactor` | `ghostGateBeats` | active |
| `GhostPop = 4` | note, `ResolvePopNote` | base × `ghostPopVelocityFactor` | `ghostGateBeats` | active |
| `HammerOn = 5` | **pitch bend on its carrier**, `+hammerOffsetDegrees` | carrier's (orphan: base × `hammerOnVelocityFactor`) | extends the carrier's gate | active (BEND-1) |
| `PullOff = 6` | **pitch bend on its carrier**, `+pullOffsetDegrees` | carrier's (orphan: base × `pullOffVelocityFactor`) | extends the carrier's gate | active (BEND-1) |

A pattern containing only v1 members renders byte-identical to SLAPFIG-1, and
a pattern without `HammerOn`/`PullOff` renders byte-identical to SLAPFIG-2:
the v1 members carry no factor and no per-class ceiling, and the legato pass
degenerates to a no-op (below). Both identities are structural, and the
second is pinned by a render-hash canary
(`GhostVocabulary_Render_IsDeterministic`).

**Two members were deliberately NOT created.** `Mute` is not a class: in MIDI
a muted note IS a ghost note — minimum-band velocity AND ultra-short gate, both
at once — and the source vocabulary treats ghost/dead/muted as synonyms. A
separate member would duplicate a class with no distinguishing law.
`LeftHandSlap` is deferred for the same reason at v1 parameters (its MIDI
profile is indistinguishable from `Ghost`); its rhythmic FUNCTION differs
(backbeat vs. fill), so it earns a slot once a law distinguishes it — e.g. a
fixed open-string pitch.

**Pitch of the sounding classes (D-SF2-PITCH=A).** The plan stays PITCH-FREE.
`PocketHit` carries an articulation CLASS; every sounding class's pitch is a
pure call-site law over the event's SELECTED note, with no rng:
`ResolvePopNote` for the pop domain (register fold intact) and
`ResolveOffsetNote` for the orphan legato case (folds `-12` while above the
register ceiling, `+12` if below the MIDI floor, hard-clamped only as a last
resort).

**Legato is a MODIFICATION of the previous note, not a note (D-BEND-GEST=A).**
This is the defining law of the class pair, and the reason SLAPFIG-2's
note-based emission was replaced: on a General MIDI patch any note-on
retriggers the sample's attack, which is precisely what a hammer-on removes.

- **Carrier.** For each hit, `BuildLegatoCarrierMap(plan)` returns the plan
  index of the nearest preceding hit that emits its OWN note-on, or `-1` when
  the hit emits its own note. Chains collapse onto the chain's ROOT carrier,
  so a `[Slap, HammerOn, PullOff]` group is one note with two bend points.
  The PLAN is never modified: `BuildSelfPocketPlan` and every SLAPFIG-2 pin
  stand byte-for-byte; the reinterpretation lives entirely in this pure
  coalescing pass. A plan with no legato classes maps to all `-1`, and the
  emission loop is then line-for-line the SLAPFIG-2 loop.
- **Gate (declared law change).** The carrier's gate extends through its
  legato tail — `ResolveLegatoGroupEndBeats` returns the end of the LAST tail,
  and the identity of the hit's own planned end when there is none. This
  overrides the §3.7.2 `min(gap, window, ceiling)` result for carriers with a
  tail, and only for them; a following NON-legato hit is not a tail.
- **Interval, in SCALE DEGREES (D-BEND-DEG=A).** Card fields
  `hammerOffsetDegrees` / `pullOffsetDegrees` (`int`, defaults `+1` / `-1`,
  `[FormerlySerializedAs]` over the SLAPFIG-2 semitone fields) are DEGREES of
  the PART scale, not semitones. `ResolveLegatoDeltaSemitones` walks the scale
  one degree at a time from the pitch the chain has reached, so the tonality
  decides each step's size: `+1` from the tonic of a major key is a whole
  step, `+1` from its third is a half step. Any `|offset|` works; octave
  crossings come for free.
  - *Anchoring (D-BEND-ANCHOR=A).* The interval is measured from the CARRIER's
    reached pitch — the note that is actually sounding — not from the event's
    selected note. This is structural: the chain state resets at every
    note-emitting hit.
  - *Off-scale fallback.* If the starting pitch class is not a scale member
    (a borrowed or re-qualified chord tone), the resolver falls back to whole
    tones, `offsetDegrees × 2`. **Silent by design** — a data-dependent
    per-hit condition, not a configuration degrade. Recorded deviation from
    the warn-max discipline.
- **Emission (D-BEND-EMIT=B / D-BEND-RESET=A / D-BEND-RANGE=A).** Tails
  accumulate an absolute detune and enqueue a STEP gesture; the composer
  applies the whole list as post-build surgery through `PitchBendWriter`
  (`SSoT_CONTRACTS.md` §11), between `pb.Build().ToFile(tempoMap)` and
  `ForceAllChannel` / `StampBankAndPatch`, so bends inherit the channel stamp
  and the bank/patch tick shift exactly as notes do. Ticks are converted with
  the same `beatSpan`/`tempoMap` the notes use, so a gesture can never drift
  from its carrier. An empty gesture list is a hard no-op.
- **Range degradation.** The GM default of ±2 semitones is ASSUMED; no RPN is
  emitted. A chained target beyond the range clamps with a warning — a shrunk
  interval, never a wrong direction. Verified behaviour, not a defect: a
  two-hammer chain over a whole-tone pair reaches `+4` and stops at `+2`. The
  writer's `rangeSemitones` parameter is the declared seam for the slide
  follow-up, which will need conditional RPN.
- **Orphan.** A legato hit that OPENS its chord-event window has nothing to
  bend from. It degrades to an ATTACKED note at the degree-resolved interval
  from the selected note — SLAPFIG-2 behaviour with the interval law upgraded
  — with one warning per `Compose` (latched). Authoring guidance: put a
  sounding step before a legato step in `selfPocketPattern`.

**Gate (D-SF2-GATE=B).** Unchanged law, per-class ceiling:
`min(gap to next PLANNED hit, remaining window, class ceiling)`. Ghost classes
take `ghostGateBeats` because a ghost is a click, not a short note; every
other class keeps `PocketMaxGateBeats`. The BEND-1 carrier extension above is
applied AFTER this rule, and only to a carrier that owns a tail.

**Tuning surface (D-SF2B-TUNE=A).** The per-class NUMBERS are card fields
(`ghostVelocityFactor`, `ghostPopVelocityFactor`, `hammerOnVelocityFactor`,
`pullOffVelocityFactor`, `ghostGateBeats`), carried into the planner as a
`SelfPocketTuning` value with a `Default` that mirrors the field defaults. The
LAWS do not move to the card: "factor of the event velocity, never an additive
boost" and "ghosts get a click ceiling" remain composer domain. This keeps the
byte-identity argument un-breakable from the inspector while letting the ear
set the constants — the shipped `ghostVelocityFactor` of 0.60 is a
tuned-by-ear value, raised from a research-derived 0.35 that read too quiet
through a GM slap patch, whose attack transient dominates the sample.

**Swing doctrine (D-SF2-SWING=A).** Not implemented. If swing/shuffle
placement is ever added it is a CARD field applied to the grid inside the
planner — never read from the Rhythm track's feel. Reading the drummer would
reintroduce a cross-track dependency, contradict the mode's defining autonomy,
break the `SelfPocket_IgnoresTheRhythmTrack_BassStemIsByteIdentical` pin, and
require an orchestrator pass under §10 of `SSoT_CONTRACTS.md`.

**Determinism.** Unchanged: zero new `ctx.rng` draws — the three BEND-1 seams
(`BuildLegatoCarrierMap`, `ResolveLegatoGroupEndBeats`,
`ResolveLegatoDeltaSemitones`) are pure static functions and the writer reads
no state. `ForEvent(k)` remains a pure per-index derivation, so skipping a
tail's note does not shift any other hit's jitter. Same seed → same bytes,
gestures included.

Test surface: `Tests/Editor/BassTrackComposer_SelfPocketVocabularyTests.cs`
(the SLAPFIG-2/2b vocabulary, plus the Ghost/GhostPop render canary),
`Tests/Editor/PitchBendWriterTests.cs` (the writer in isolation) and
`Tests/Editor/BassTrackComposer_LegatoBendTests.cs` (the three seams and the
legato render: determinism, bend presence, closing invariant, the fewer
note-ons law, and the anti-no-op pin).

#### 3.7.4 SelfPocket phrase — bar substitutions (MGP-ALWTTT-BASS-PHRASE-1)

The v1 cycled pattern is bar-blind: the figure is identical measure after
measure. PHRASE-1 makes the bar matter. The card gains:

- `selfPocketPhraseLengthBars` (default 4, `[Min(1)]`) — the phrase length
  in bars (D-PH-LEN=A: authored, fixed, purely modular over the part; a
  trailing partial phrase truncates — there is no part-end lookahead).
- `selfPocketBarSubstitutions` — a table of `{ barIndex, variants[] }`
  entries (D-PH-SURF=D). Each entry replaces ONE phrase slot's bar with one
  of its pattern variants; the canonical authoring is a single entry at
  slot `length-1`, the phrase-closing fill (the *Aeroplane* gesture).
  Unity cannot serialize `List<List<T>>`, hence the two wrapper classes
  (`SelfPocketBarSubstitution`, `SelfPocketPatternVariant`).
- `selfPocketVariantSelection` (default `SeededMix`) — how a slot with
  several variants picks one per phrase occurrence.

**The single ON/OFF gate is the table (D-PH-BYTE=A).** Null/empty table —
or a table where nothing survives validation — keeps every phrase field
inert and the planner on the SLAPFIG-2b path, byte-identical by
construction: the pre-PHRASE `BuildSelfPocketPlan` signature delegates to
the extended overload with a null table, and the null-table branch is the
v1 lookup verbatim (test-pinned plan-for-plan; the Ghost-vocabulary render
canary keeps watching the bytes).

**Anchoring and slot law (D-PH-ANCHOR=A).** Bar = `floor(part beat /
beatsPerBar + ε)` — METER absolute, part beat 0 anchored, the same footing
as the v1 grid. `beatsPerBar` is the TS table's integer `BeatsPerMeasure`
(7/8 ⇒ 7 part beats), though the math stays in doubles under the planner's
epsilon discipline. Slot = `bar % phraseLengthBars`; phraseIndex =
`bar / phraseLengthBars`. Chord-event windows never move the anchor: an
event slicing a phrase mid-bar sees exactly the bars the meter says
(test-pinned across split windows). Parts whose first chord event starts
late still count the phrase from part beat 0.

**Indexing (D-PH-INDEX=A).** With the phrase ACTIVE, EVERY effective
pattern — substituted variants AND the body on unsubstituted bars —
indexes from its bar start (`(g − barStart/step) % length`), cycling
within the bar. For a body whose length divides the bar's grid steps this
coincides with the v1 absolute indexing; for a non-divisor length,
enabling the phrase re-phases the body (restart every bar) — a declared,
opt-in change with no baseline to preserve. Pattern lengths that do not
divide the bar's steps warn once per Compose (informative, never fatal).

**Variant selection (D-PH-FILL=C / SD-PH-2=A / SD-PH-3=A).**
- `SeededMix` (default): `variant = floor(PhraseMix01(phraseSeed,
  phraseIndex, slot, salt=0) × count)` — a pure integer mix, the WalkMix01
  idiom deliberately DUPLICATED (lowbias32 avalanche verbatim; own fold
  constants `0xC2B2AE35` / `0x27D4EB2F` so the (phraseIndex, slot) matrix
  is asymmetric and no other seam's byte-identity radius grows). Exact
  goldens are test-pinned; moving either constant re-picks serialized
  cards' variants and is a declared render-affecting change.
- `RoundRobin`: `phraseIndex % count`, seed-independent — mechanical
  alternation for A/B auditioning.
- One variant short-circuits to index 0 under both laws.

The phrase seed is a dedicated derived substream key —
`StableHash32($"{trackSeed}|selfphrase")` — consumed ONLY as the mix key,
never as a stream: **zero new `ctx.rng` draws**, and no stream exists that
a toggle could shift. Recorded deviation: the derivation lives
composer-side (`BassTrackComposer.ResolvePhraseSeed`, calling the same
public `StableHash32`) rather than beside the `Resolve*` family in
`SongOrchestrator` — a batch-scoped choice to hold the touched file set to
the two verified-fresh files. Relocating it (same string, same hash) is a
no-render-change refactor candidate.

**Table validation (SD-PH-1=A).** `ResolvePhraseSubstitutions` is a pure
seam with LOCAL degradation: a duplicate `barIndex` keeps the LAST entry;
an out-of-range `barIndex` is inert; a variant with no steps is dropped;
an entry left with zero variants is inert. An all-Rest variant is LEGAL —
a silent break bar renders as absence. Every defect appends one message,
batched into ONE `LogWarning` per Compose. The one GLOBAL degrade is
`phraseLengthBars < 1` (a phrase of no bars addresses no slots): table
disabled, warn, decoupled v1 cycling continues.

**Scope (D-PH-SCOPE=A).** SelfPocket only. SlapPocket takes its grid from
the drummer's published onsets; a phrase there would fight the external
source. The planner stays a pure static function — zero rng, zero
cross-track reads — so the SLAPFIG-1 autonomy pin
(`SelfPocket_IgnoresTheRhythmTrack_BassStemIsByteIdentical`) and the
dormant ALWTTT §8.4 consumer hash duty are untouched.

**Authoring.** The body and every variant are authored as text in the
Bassline Card Editor (`authoring/SSoT_Authoring_Bass_Cards.md`,
MGP-BASSCARD-WIZARD-1); that document governs the DSL and the window, not
the semantics above.

**Deferred (recorded, NOT designed):**
- Adaptive phrase length — derived from the progression, or coupled to the
  rhythm track pocket-style (would wake cross-track questions; explicitly
  out of v1).
- Extending the phrase surface to SlapPocket.
- Part-end awareness (a fill on the part's final bar even mid-phrase).
- Partial-bar substitution (only the last N steps); authoring the tail
  inside the variant covers the need.

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
- the SlapPocket coupling changes (§3.7): the opt-in surface, the kick/snare
  families or the pop-wins rule, the velocity/gate/pop-pitch parameters, the
  degradation byte-identity claim, the segment-list emission structure, or
  the consumer hash duty.
- Any change to the pocket velocity offsets (`pocketSlapBoost`,
  `pocketPopBoost`), to their clamp point, or to their application order
  relative to dedupe or `VelocityJitter` (§3.7.1).
- Any change to the trigger-lane resolution: the `pocketCustomLanes` opt-in
  semantics, the replace-not-extend rule, the empty-list-disables rule, the
  both-lists-resolve-to-pop rule, or the built-in v1 family membership
  (§3.7.1).
- Any change to whether lane matching happens before or after per-kit
  resolution (§3.7.1) — this is the PERC-FALLBACK-1 interaction.
- the host-supplied default progression for backing-less parts changes (§1):
  the guard policy, the seeding site, the clone/normalization discipline, or the
  zero-draws property;
- the runtime re-qualification of chord qualities reaches the bass's own
  `TrackParameters.Pattern` fallback (today it does not — see §1).
- the improvised walk changes (§3.6bis, B3 WALK-2): the `ImprovisedWalk` opt-in
  surface, the D-W2-VOCAB=B vocabulary (anchor hit, nearest-octave chord tones,
  the approach-note last hit and its D-W2-LAST=A wrap), the D-W2-HOME=A division
  of labor (composer owns pitches via `BuildWalkLine`; the engine keeps rhythm
  and dynamics through `PlanHits(..., noteCount: 1)` and 1-note `Block`
  segments), the D-W2-RNG=B variation source (pure mix over `ResolveWalkSeed`,
  `eventIndex`, `hitIndex` — no stream, zero `ctx.rng` draws), or the
  D-W2-POCKET=A bypass.
- the register contract changes (B3 BASS-REG-1, D-REG-1=C): the two-octave
  ceiling-capped band or `ResolveOctaveBand` (§2), the `ResolveRegisterCeiling`
  definition (`octaveMax * 12 + 11`, clamped to 127), the WHOLE-voicing walk fold
  (D-REG-3=B, §3.6) or its per-note adaptation for the improvised walk
  (D-W2-REG, §3.6bis), or the pop fold-onto-the-selected-note (D-REG-2=B,
  §3.7.1) and the pitch-only scope that keeps the POCKET-1/2 test surface
  meaning verbatim.
- the order-independence contract (§1) changes: the PASS 0 scheduling of
  Backing, the deferred index-ordered merge, or the harmony-source sniff that
  gates the host default;
- the SelfPocket surface (§3.7.2) changes: the enum member, the cycled-pattern
  grid or its meter anchoring, the velocity base, the degrade rule, or the
  reuse of the SlapPocket emission pipeline downstream of the plan;
- the SelfPocket vocabulary (§3.7.3) changes: a `SelfPocketStep` member is
  added or its status changes, a per-class pitch / velocity / gate law
  changes, the tuning surface moves between card and composer, or the swing
  doctrine is implemented or revised;
- the legato law (§3.7.3, BEND-1) changes: the carrier rule or chain
  collapsing, the carrier gate extension, the scale-degree interval law
  (including its anchoring and its off-scale fallback), the orphan degrade,
  the post-build application point relative to `ForceAllChannel` /
  `StampBankAndPatch`, or the assumed ±2 semitone range / the decision to
  emit RPN.
- the SelfPocket PHRASE surface (§3.7.4) changes: a phrase field is added
  or re-lawed, the anchoring/slot/indexing law moves, a variant-selection
  law is added or its mix constants change (render-affecting for
  serialized cards — the PhraseMix01 goldens are the tripwire), or the
  single-gate OFF semantics (D-PH-BYTE=A) are widened.

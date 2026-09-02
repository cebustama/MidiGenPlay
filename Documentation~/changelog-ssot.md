# changelog-ssot

## 2026-09-01 — DOC-SWEEP-3: the five-batch documentation backlog applied

Documentation-only session, zero code, zero assets, zero test runs. Applied
every genuinely pending `*_doc_diffs.md` to its governed documents, in
dependency order: PHRASE-1 → BASSCARD-WIZARD-1 → TONALITY-1 → TONALITY-2 →
HARMONY-1, with the CURRENT_STATE entries placed newest-first per that file's
convention.

### Inventory correction — the backlog was five, not two

The session was briefed for TONALITY-1 and TONALITY-2 only. Verified against
the **governed documents themselves** (the accumulator is not authority — the
M-5 precedent and the DOC-SWEEP-1 blocker are both on record), three further
batches were also unapplied and were swept in the same pass:

- **MGP-ALWTTT-BASS-PHRASE-1** (2026-08-05). Bass SSoT had no §3.7.4; §3.7.2's
  deferred list still named BASS-PHRASE-1 as future work. Not among the five
  DOC-SWEEP-1 applied, and BASSCARD-WIZARD-1's own header (2026-08-07)
  independently records it as outstanding.
- **MGP-BASSCARD-WIZARD-1** (2026-08-07). Zero mentions of the window, the
  parser or the new SSoT in any governed document, the manifest or the matrix.
- **MGP-ALWTTT-HARMONY-1** (2026-09-01). Orchestration had no §5.9; the ALWTTT
  cards doc still read "composer SSoT pending".

Confirmed stale, NOT re-applied (DOC-SWEEP-1 and -2 applied them; M-5: stale
copy, not drift): `MGP-ALWTTT-BASS-ORDER-SLAPFIG-1`,
`MGP-ALWTTT-BASS-SLAPFIG-2` (applied in part), `MGP-ARTIC-RATE-1` (applied
except §B.2), `MGP-ALWTTT-BASS-BEND-1` doc and code diffs, `MGP-MEL-1b`.

### Sweep decisions (DOC-SWEEP-3)

- **D-S3-1 = C.** `PENDING_DOC_DIFFS.md` was not available to this session. The
  closure record lives here instead of in the accumulator. **The accumulator is
  therefore stale in a NEW way: it does not record this sweep.** Reconciling it
  is owed to the owner — see "Owed" below.
- **D-S3-2 = A.** All five pending diffs applied in one pass, including the two
  that change the authority model (a new governed SSoT, a manifest entry and an
  index entry). The narrower option was recommended and declined.
- **D-S3-3 = A for TONALITY-2 Diff 5, B for Diff 4.** Diff 4's target document
  was unnamed and `coverage-matrix.md` gives the answer as "none, by decision":
  **D-SMOKE-DOC-1=A** (IMPORT-QOL-1, 2026-07-24) leaves `CompositionSmokeWindow`
  intentionally ungoverned. Diff 5 ("Save as new…") is a two-button convenience
  on that window and was **SKIPPED** rather than silently reversing an accepted
  decision; it is recorded in CURRENT_STATE only. Diff 4 governs a different
  artefact — the matrix runner, whose `beliefDiv`-not-counters method is the
  evidence base for the parity claim — and landed in a NEW
  `authoring/SSoT_Authoring_Tools.md` §3.F, "Diagnostic and regression
  harnesses", which restates D-SMOKE-DOC-1=A as unchanged.
- **D-S3-4 = A.** TONALITY-2 Diffs 1–3 all cite **§3.6.1** of the Bass SSoT.
  No such section exists; the find-anchors resolve inside **§3.6bis**, which is
  what `coverage-matrix.md` rows 22–23 cite by name. Applied amended: every
  §3.6.1 reference rewritten to §3.6bis, including the forward reference inside
  Diff 2's own replacement text.
- **D-S3-5 = A.** TONALITY-2 Diff 7 says "TONALITY-1 first". Read as
  application order, not document order: `CURRENT_STATE.md` "Just completed" is
  reverse-chronological, so the entries land HARMONY-1 / TONALITY-2 /
  TONALITY-1 / R3. Same precedent as DOC-SWEEP-1's recorded placement deviation.
- **D-S3-6 = A.** Two governed claims were falsified by MGP-TONALITY-1 and were
  amended rather than left standing — the DOC-SWEEP-2 D-1=A precedent (a
  governed document asserting the opposite of its own primary SSoT is the drift
  class this process exists to prevent):
  1. `CURRENT_STATE.md`'s "Phrase-final breath" blocked bullet (present TWICE —
     a second verbatim double-write, distinct from the MEL-1b one; both copies
     edited identically, neither de-duplicated) claimed sustaining archetypes
     fill the whole span. `endRestFraction` ships.
  2. The `transposeScaleSteps` follow-up, which this session was instructed to
     carry forward as open, **is closed under a different name**:
     MGP-TONALITY-1 D-TON6=A shipped diatonic motif transposition as
     `RepeatLastNotesDirective.transposeMode = ScaleDegrees`. Carrying it
     forward would have written a false claim. The authoring hazard block in
     `authoring/SSoT_Authoring_Melody_Composition.md` was rewritten, not
     appended to, for the same reason.

### Further amendments beyond the diffs

- **BASSCARD-WIZARD-1 §2** supplied a bare bullet with no heading for a section
  whose entries are `####`-level. Applied as a `#### BasslineCardEditorWindow`
  entry under §3.A, and §3.A's "Three tools" count corrected to "Four tools" —
  which the diff did not mention and which would otherwise have contradicted
  the list directly beneath it.
- **BASSCARD-WIZARD-1 §3** proposed `authority: ssot` with unquoted `governs`
  paths. The live manifest schema is `authority_class: subsystem_ssot` with
  quoted paths and an `invariants` block. Applied in the live shape (the diff
  itself asked for this confirmation); nine invariants written from the new
  SSoT's own text. Manifest re-parsed clean.
- **BASSCARD-WIZARD-1 §7a** specified a blanket `·` → `.` substitution across
  `PhrasePresets_Bass_Spec.md`. That document also used `·` as a LEGEND
  SEPARATOR, which the blanket rule would have turned into rests. The pattern
  strings were substituted; the legend was rewritten by hand instead. The same
  header's claim that "no bass-card wizard exists" was false as of the batch
  that was editing it, and was corrected.
- **`SSoT_INDEX.md` pre-existing omission recorded and fixed.**
  `runtime/SSoT_Composer_Bass_Track.md` was absent from the "Primary runtime
  SSoTs" list although it has been registered in `ssot_manifest.yaml` and cited
  as primary by `coverage-matrix.md` since CA-F2 (2026-07-15). Both TONALITY
  diffs treat it as primary. Listed, with the omission noted in place. This is
  a list correction, not an authority change.

### Not applied

- **TONALITY-2 Diff 5** (Composition Smoke "Save as new…") — no governed home;
  D-SMOKE-DOC-1=A. Recorded in `CURRENT_STATE.md` only.
- **PHRASE-1 §5 and BASSCARD-WIZARD-1 §6** — both write accumulator entries.
  Void under D-S3-1=C.
- **PHRASE-1 §3** correctly asked for no contract change and got none. Its
  conditional suggestion — append `|selfphrase` to a substream inventory in the
  orchestration SSoT "if one exists" — was checked: no such inventory exists,
  so nothing was appended. The derivation and its recorded composer-side
  deviation live in Bass SSoT §3.7.4.

### Owed to the owner (not actionable in a documentation session)

- **`PENDING_DOC_DIFFS.md` reconciliation.** It still reads "Accumulator status:
  EMPTY" (false since PHRASE-1) and lists eight stale copies. Seven of those
  eight are not present in the working set at all. This sweep is unrecorded
  there.
- **`TonalityAudit.cs` stale comment.** The `chordPcs` XML doc still states that
  melody and bass are accidental-blind; D-TON10 retired that and
  `SSoT_CONTRACTS.md` §13 now says the opposite. Code comment, not a governed
  document — flagged for the next code batch, not edited here.

### Follow-ups carried forward, unscheduled

- **D-W2-DRIFT** (new, MGP-TONALITY-2) — the improvised walk's negative
  selection drift. Contained by the register floor, not cured. Bass SSoT
  §3.6bis.
- `AscendingClimbMelodyStrategy` hardcodes `octs = 2` for the final-slot cadence.
- `ResolvedSource.TrackParameters` is unreachable on the Backing path; both
  candidate fixes change what the readback reports, so both are runtime
  decisions.
- The `ssot_manifest.yaml` header entry for ARTIC-RATE-1 §B.2, recorded at
  DOC-SWEEP-2 D-3 as APPLIED EXCEPT §B.2. Not written here: doing so would
  re-apply part of a diff already swept, which this session is forbidden to do.
- MGP-MEL-2 "Phrase Form" — reduced to `RestPhraseSO` and A/B/A′ form with
  relatively-stored motif memory. Its other two members shipped (see D-S3-6).
- Tagging walk approach notes at the composer (`origin=walk-approach`); the
  matrix infers the tag positionally instead.
- HARMONY-1's deferred audit items 3, 5, 6, 7, the remainder of 8, and 9
  (a dedicated Harmony composer SSoT), plus the F-HARM-8 residual.
- Bassline card catalogue browser (BASSCARD D2); extracting `ComputeAdvisories`
  into a testable pure function.

---

## 2026-09-01 — MGP-ALWTTT-HARMONY-1: Harmony role, minimum subset

Code closed 2026-09-01 (EditMode green). Scope was set by the consumer, not the
package: ALWTTT's R6 finisher card needs Tier A, and asked for items 0/1/2/4 of
MGP-HARMONY-AUDIT (2026-08-31, verdict BOUNDED GAP) plus one new question, with
the remaining items explicitly deferred. The stated reason for the narrow scope
is worth preserving: items 1 and 2 change Harmony-track bytes, and **no existing
render contains a Harmony row**, so this was the cheapest window those fixes
would ever have.

### Fixed
- **F-HARM-1 — meter.** Guide-note beats are Part beat units (MEL-BEATUNIT-1);
  the composer multiplied `MusicalTimeSpan.Quarter`. In 6/8 the 4th eighth of
  bar 1 was emitted and looked up as the downbeat of bar 2. Now
  `MelodyTrackComposer.BeatsToSpan`, at both the emission site and the lookup
  site, so a note is harmonized against the chord sounding where it actually
  sounds. Byte-identical in beat-unit-4 meters.
- **F-HARM-2 — accidental parity.** `degreeAccidental` now transposes the degree
  root before chord-tone expansion, matching Melody/Bass/Backing. Secondary
  dominants and modal borrowing — surface the consumer has used since
  REQUALITY-2/B1 — were previously harmonized against the wrong chord.
- **F-HARM-3 — canonical lookup.** The composer's private `FindEventAtBeat`
  (`RoundToInt`, start-only, no length window, no wrap) is replaced by
  `ChordProgressionData.FindChordEventAt`.
- **F-HARM-4 — factory field.** `HarmonyTrackComposerFactory._settings` was
  never assigned; the composer ran with `settings = null` and every Harmony
  diagnostic was suppressed. Assigned, mirroring `MelodyTrackComposerFactory`.

### Decided
- **D-H1-STRAT = A.** No change to `HarmonyStrategyFactory`. The audit's unison
  worry (F-HARM-8) does not materialize: the effective strategy excludes any
  candidate closer than `minDistanceFromMelody`, whose range starts at 1 and
  defaults to 3, so the melody note can never be returned as its own harmony.
  The residual — `relation` inert, `NearestDifferentChordToneHarmonyStrategy`
  unreachable — is registered as deferred, not fixed.
- **D-H1-5a = B.** Harmony prefers its own musician's cached melody before the
  first-in-list fallback. The audit had reasoned about Harmony as harmonizing
  ANOTHER musician's melody; the consumer's actual case is the reverse
  (self-harmony), and under an exact-key lookup that case stops depending on
  track-list order entirely.
- **D-H1-5b = A.** Harmony still publishes its line to the melody cache under
  its own id. Assessed benign under self-harmony and written down as contract
  (§5.9) instead of left to be re-derived: PASS 2 is last, the write swaps a
  reference rather than mutating Melody's list, the Melody stem is already
  built, and the cache is per-repetition. One edge registered: a second Harmony
  track for the same musician would follow the first harmony.

### Documented
- `runtime/SSoT_Runtime_Generation_Orchestration.md` gains **§5.9**, the melody
  guide-note cache contract (lifetime, target resolution, write-back, stem
  isolation). §5.6's public-seam list gains the three HARMONY-1 seams; §5.7's
  PASS 2 line points at §5.9.
- `runtime/SSoT_Composer_Melody_Track.md` — the guide-note section asserted
  "There is no in-package consumer today", which was false and, being false,
  is why the hazard the same paragraph warned about went unnoticed in the one
  consumer that existed. Corrected, with the payload/channel split made explicit.

### Test surface
- New `Tests/Editor/HarmonyTrackComposer_GuideFollowTests.cs` (9 tests) against
  the public seams `HarmonyTrackComposer.ResolveHarmonyNotesCore` and
  `.ResolveGuideMelody`: 6/8 guide-follow with an explicit discriminator (beat 3
  must resolve to the bar-1 chord — the legacy quarter conversion sent it to
  bar 2), canonical wrap, a 4/4 companion, bIII accidental parity plus a
  zero-accidental identity control, repeat-call determinism, and three
  target-resolution cases. This is the subset of audit item 8 that makes items 1
  and 2 observable; the rest of item 8 is deferred.

### Deferred (registered, not forgotten)
Audit items 3 (card→composer bridge), 5 (configurable melody target), 6 (cache
contract: no self-publication / separate key / list-backed cache), 7 (velocity
policy — harmony velocity is still hardcoded 80), the remainder of 8, and 9 (a
dedicated Harmony composer SSoT). Plus the F-HARM-8 residual.

---

## 2026-09-01 — MGP-TONALITY-2: tonality regression matrix

Code closed on DoD; documentation applied at DOC-SWEEP-3 the same day.
Evidence base: sweep `tonality_matrix_20260901_172029` — 476/476 cells, 0
failures, `beliefDiv == 0` on every track of every cell, `residualReds == 0`;
anchor hashes `59193145 / 95CC33FD / C8A25142 / A61E8AB6 / 6B1133C3` unchanged;
`BassTrackComposer_WalkImprovTests` green.

### Added
- **Tonality regression matrix** (`Editor/TonalityMatrixRunner.cs` +
  `TonalityMatrixWindow.cs`): a 476-cell cartesian sweep over the smoke render
  path — tonality profiles × {4/4, 6/8} × progressions × the seven
  melody/bass/backing combinations × walk modes × backing figures. No runtime
  dependency; no composer modified; nothing marked dirty.
- **Canonical re-classification (D-TON2-PARITY=A)** as the parity detector. The
  audit counters alone CANNOT detect a chord-identity breach — a composer with
  a wrong chord belief judges its own wrong notes as in-chord and reports green,
  which is exactly how the pre-D-TON10 bass defect survived. `beliefDiv` is the
  only admissible source for a parity claim. Written into `SSoT_CONTRACTS.md`
  §13 as part of the contract it verifies.
- `CompositionSmokeWindow` gained "Save as new…" — editor-only convenience,
  delegating to the same `SaveToSetup` write path so the two buttons cannot
  drift; cancelling leaves the current assignment untouched. **Not documented in
  a governed SSoT** (D-SMOKE-DOC-1=A; DOC-SWEEP-3 D-S3-3).

### Fixed
- **F-TON-WALK-DRIFT-1.** `BuildWalkLine` had no register floor, and its
  prev-relative middle-hit selection has a negative expected drift (≈ −0.6
  semitones per hit from an asymmetric candidate set). Long windows walked the
  line four octaves under the instrument and bottomed out at MIDI 0. Contained
  by a two-sided octave-wise fold (**D-W2-FLOOR=B**), floor one octave below the
  §2 band; the ceiling still wins, so a degenerate asset degrades to the
  pre-existing behaviour rather than oscillating. `ChordToneWalk` is untouched
  and byte-identical; callers passing no floor get the old path, which is what
  the WALK-2 unit tests exercise. Bass SSoT §2 and §3.6bis.

### Opened
- **D-W2-DRIFT.** The selection asymmetry itself is unresolved. The floor
  contains the symptom; the line still tends downward and rests against the
  floor in long windows. Removing the asymmetry changes the walk's musical
  character and needs its own decision and a listening pass.

### Recorded blind spot
The matrix under-reports this class of defect by construction: the MIDI-floor
pitch class is C, diatonic in most profiles, so a line bottoming out at note 0
surfaces only where C is out of scale. F-TON-WALK-DRIFT-1 appeared in
Lydian/6/8/Backing cells alone.

---

## 2026-08-11 — MGP-TONALITY-1: tonal defect diagnosis and fixes

Four reported symptoms triaged against real assets. All code applied and
verified in Unity; documentation applied at DOC-SWEEP-3 (2026-09-01).

### Added
- **`TonalityAudit`** — a log-only diagnostics component, gated by
  `enableTonalityAudit` / `tonalityAuditShowInfo`. It classifies every emitted
  note InScale / ChordToneChromatic / OutOfScaleAndChord and names an origin.
  It never alters output.
- **Four phrase-archetype authoring fields**, all defaulting to legacy
  behaviour: `PhraseArchetypeSO.endRestFraction`, `.meterFitSlots`,
  `.allowTupletSubdivisions`, and `BurstThenHoldPhraseSO.restProbMid`.

### Fixed
- **D-TON10 — accidental awareness, and the batch's only render-affecting
  change.** `MelodyTrackComposer` (both paths) and `BassTrackComposer` (main
  selection AND the walk's approach target) resolved the degree root from
  `degree` alone while `ChordTrackComposer` had always applied
  `degreeAccidental`. On `Prog_Min_Napolitana_bII` the backing sounded
  `[ASharp D F]` against a bass playing `[B DSharp FSharp]` — a semitone clash
  on root, third and fifth simultaneously, reproduced in three tonalities and
  confirmed by ear. Fixed at every site; affects only accidental-bearing
  progressions. **Retires the recorded accidental-blindness of the walk's
  next-root lookup (D-W2-LAST).**
- **D-TON6=A — diatonic motif transposition.**
  `RepeatLastNotesDirective.transposeMode` gains `ScaleDegrees` beside the
  legacy `ChromaticSemitones`, which stays the serialized default for asset
  compatibility. Root cause of the "Showtime" out-of-key melody:
  `transposeSemitones = 2` in chromatic mode echoed motif `G5 B5 A5` at +2 and
  +4, emitting `C#6 D#6 C#6`. **This closes the standing `transposeScaleSteps`
  follow-up under a different name** — see DOC-SWEEP-3 D-S3-6.
- **D-TON7/D-TON8/D-TON9 — phrase materialization.** Phrases could not breathe
  (every archetype filled 100% of its span, spans tile contiguously) and slot
  counts produced off-grid onsets (9 slots over an 8-beat span, onsets at 0.89 /
  1.78 / 2.67). `endRestFraction` trims the phrase-end slot under a clamp of the
  greater of 1/8 beat and 25% of planned duration; `meterFitSlots` constrains
  the resulting slot DURATION — not the count — to a power of two in beats,
  with `allowTupletSubdivisions` admitting the triplet family.

### Committed
- **New `SSoT_CONTRACTS.md` §13 — chord identity.** The sounding chord is the
  triple (degree, degreeAccidental, quality), and every composer and every
  chord-naming component must resolve the root through `TransposeNoteName`. The
  defect survived two prior batches because no contract stated the rule; this is
  its general form.

### Determinism
- The snap in `meterFitSlots` applies to the RESULT of the slot-count draw,
  never in place of it, so toggling it cannot shift an RNG stream. **One
  recorded exception:** `restProbMid` gates its per-note roll on `> 0`, so
  raising it above zero shifts `BurstThenHoldPhraseSO`'s draw stream.
  Deliberate — an unconditional roll would have broken byte-identity for every
  pre-existing asset.
- D-TON10 changes bytes on accidental-bearing progressions only. The smoke
  anchor is unchanged.

### Closed as authoring, not engine
- **S4 — compound-meter drum density.** Not a defect. `SSoT_CONTRACTS.md` §5
  defines one beat as `GetBeatSpan(TimeSignature)`, an eighth in x/8, so a 6/8
  bar has six beats while the FELT pulse is the dotted quarter. A groove
  authored "one kick per beat" pulses at eighths. Documented in
  `authoring/SSoT_Authoring_Rhythm_Patterns.md` §2; the corrective `CS_*` asset
  work is ALWTTT's, not the package's.

---

## 2026-08-08 — MGP-TRIAGE-ALWTTT-R3: ALWTTT R3 evidence bundle triaged

Code closed 2026-08-08 (EditMode green); documentation applied in the same-day
pass MGP-DOC-SWEEP-2. Source: `MGP_Evidence_Bundle_from_ALWTTT_R3_2026-08-08.md`,
answered back through the boundary contract's open-asks channel. No evidence
label was promoted from "observed" to "confirmed" without a package-side repro.

### Fixed
- **E1 — gap F5 closed, and RECLASSIFIED from cosmetic to audible.**
  `SustainLeadInPhraseSO`'s pickup branch built three slots while hardcoding
  `totalSlotsInPhrase = 2, 2, 3`; all three now carry `3`. The prior "no render
  impact" justification rested on nothing consuming
  `PhraseState.TotalNotesInPhrase` — true, but the SLOT field has a second
  consumer, `MelodyTrackComposer.IsFinalSlotOfPart`
  (`slotIndexInPhrase == totalSlotsInPhrase - 1`), which a drifting denominator
  satisfies more than once. On the final chord span both the pickup grace note
  and the landing read as final, and `AscendingClimbMelodyStrategy`
  short-circuits every such slot to a tonic two octaves above the reference.
  Scope: AscendingClimb + a pickup SustainLeadIn phrase on the last chord span.
  Landed in `runtime/SSoT_Composer_Melody_Track.md`, which now also states the
  archetype bookkeeping obligations (constant denominator, dense indices,
  exactly one final slot). Pinned by
  `Tests/Editor/PhraseArchetype_SlotBookkeepingTests.cs`.
- **E3 — clone identity.** `NormalizeProgressionForPartIfNeeded` built its clone
  from `CreateInstance` field by field and never copied
  `UnityEngine.Object.name`, which `CreateInstance` leaves empty; the four
  `Instantiate` sites carried the milder `(Clone)`-suffix form. Same hazard
  class as the documented F-NORM-DROP; `.name` is not a serialized field, which
  is why it survived four batches. New invariant in
  `runtime/SSoT_Composer_Backing_Track.md` §3.1:
  `sharedProgressionData.name == sharedProgressionAssetName` on every precedence
  step. **The reported CardPalette scoping is void** — the loser is
  normalization, not the source, and normalization fires on nearly every render
  (`sub x1` authored, `x4` wanted). Pinned by
  `Tests/Editor/ChordProgression_CloneIdentityTests.cs`.

### Changed
- **E2 — `maxStepSemitones` is a PREFERENCE, not a bound.** No contract was
  violated; the log was misleading. `ComputeMotionWeight` multiplies an
  over-step candidate's weight by `0.01` rather than excluding it, and
  `AscendingClimb`'s no-candidate fallbacks abandon the limit deliberately.
  Separately, the logged step is measured on the EMITTED note — after strategy,
  after contour snap, after `ApplyIntervalDirective`. Log renamed `maxStep=` →
  `maxStepPref=` and `step=` → `emittedStep=`; `emittedStep > maxStepPref` is
  expected output. **No behaviour change.** MEL-1b's evidence line "all steps ≤
  `maxStepSemitones`" was a one-render observation and must not be cited as an
  invariant.

### Closed as intended
- **E4 — chromatic motif transposition.** `transposeSemitones` is chromatic and
  accumulates per cycle; a degree transposed out of the mode leaves the scale.
  Already specified in `runtime/SSoT_Composer_Melody_Track.md` and carrying an
  authoring-hazard callout in `authoring/SSoT_Authoring_Melody_Composition.md`,
  which now records the R3 sighting as a field data point. No code.

### Committed
- **PartConfig in-place mutation is now a contract, not an implementation
  detail.** New `SSoT_CONTRACTS.md` §12: `adoptProgressionTonality` assigns
  `part.Tonality` in place during compose, the mutation is visible after
  `GenerateSinglePart` returns and must remain so, and composing against an
  internal copy or reverting on exit is a breaking change requiring a
  boundary-record entry. Exactly one field on one opt-in path; no other composer
  may mutate the `PartConfig`. `runtime/SSoT_Composer_Backing_Track.md` §2.3
  names `ResolvedTrackChoice.tonalityAdopted` / `.adoptedTonality` as the
  PREFERRED read path — it distinguishes "adopted to X" from "was already X",
  which reading the mutated field cannot.

### Determinism
E1 CHANGES the melody rng draw sequence on affected renders: the slot that used
to short-circuit to the cadence now runs the normal ascending path and draws
from `PickWeightedRandom`. **Same seed ⇒ a different (and correct) melody** for
any AscendingClimb part using a pickup SustainLeadIn phrase. Same class as
MEL-1b's F1. Rhythm, backing and bass streams are untouched (rng is per track).
Any pinned-seed melody golden needs re-pinning.

### Consumer verification discharged
The live ALWTTT session of 2026-08-08 exercised **MGP-MEL-1b P4 and P7** in the
game: `adoptProgressionTonality` drove JAM-2 and `PartRender.sharedProgressionData`
drove JAM-1. Both left the `CURRENT_STATE.md` blocked list, where they had stood
since 2026-08-05.

### Open, recorded, not scheduled
Per DOC-SWEEP-1 decision D-1=C, follow-ups are recorded here and in
`CURRENT_STATE.md` only; no roadmap file is opened for unscoped work.
- **`transposeScaleSteps`** — the diatonic sibling of `transposeSemitones`. Now
  carries TWO data points: the MEL-1b hazard note and the R3 sighting. Still
  unscheduled, and still the correct answer to E4 rather than changing the
  chromatic behaviour.
- **`AscendingClimbMelodyStrategy` hardcodes `octs = 2`** for the final-slot
  cadence. Candidate for a style/leading parameter. E1 made the consequence of
  that hardcode audible; the hardcode itself is untouched.
- **`ResolvedSource.TrackParameters` is unreachable on the Backing path.**
  `GetProgressionForPart` is consulted first and the orchestrator wires it with
  an authored fallback (`SongOrchestrator.FindProgressionForPart`) returning the
  first Backing track's `Parameters.Pattern`, so a Backing track with its own
  authored progression always reports `SharedProgression`. Behaviour is correct;
  the taxonomy is misleading, and any host branch keyed on
  `sharedProgressionSource == TrackParameters` is dead. Either the fallback is
  consulted AFTER the track's own Pattern, or the enum member is documented as
  second-Backing-track-only. **Both change what the readback reports, so both
  are runtime decisions, not documentation ones.** Recorded in
  `runtime/SSoT_Composer_Backing_Track.md` §3.1, not scheduled.
- **A composer-level render gate for E1** (AscendingClimb + pickup SustainLeadIn
  on the last chord span, asserting ONE cadence). The archetype-level pin closes
  the defect; the render gate would pin the interaction. Deferred as
  disproportionate for a one-line data fix.

### Sweep decisions (MGP-DOC-SWEEP-2)
- **D-1=A.** `CURRENT_STATE.md`'s "Recorded gap F5" bullet said "No render
  impact today". §1 of this batch falsifies it. The diff's §7 did not mention
  the bullet; it was REMOVED rather than left standing, because leaving a
  governed document asserting the opposite of its own primary SSoT is the drift
  class this process exists to prevent. Recorded here as an amendment beyond the
  diff.
- **D-2=A.** `coverage-matrix.md` registers new test files in its closure-notes
  section, not in the table's "Secondary / supporting docs" column, which holds
  documents. The diff's "add under the melody/backing composer row" was applied
  in the file's own convention. **No primary-home flip; no row added.**
- **D-3.** `PENDING_DOC_DIFFS.md` entry 3 (MGP-ARTIC-RATE-1) was corrected to
  **APPLIED EXCEPT §B.2**, not to plain APPLIED — see that file for the
  evidence. Writing the missing §B.2 header entry now would be re-applying a
  diff already swept at DOC-SWEEP-1, which this batch is forbidden to do.

## 2026-08-07 — MGP-BASSCARD-WIZARD-1: bassline cards become authorable

Authoring-tools batch. **Zero runtime change** — `BassTrackComposer.cs` was not
touched and no render byte moved. Code closed 2026-08-07; documentation applied
at DOC-SWEEP-3 (2026-09-01).

### Added
- **`BasslineCardEditorWindow`** (`MidiGenPlay/Bassline Card Editor...`) —
  whole-card editing over a deep clone, with text-mode authoring for the
  SelfPocket body and the PHRASE-1 substitution table. Writes only on Apply /
  Save As, under `Undo`, through the shared Resources store
  (`typeFolder = "Basslines"`). Registered in
  `authoring/SSoT_Authoring_Tools.md` §3.A as the FOURTH Category-A tool.
- **A text DSL** (`S P . - g G H L`; `|` and whitespace ignored; unknown glyph →
  rest + warning) with `BassPatternTextParser` / `BassPatternTextWarning`.
  Motivation was arithmetic: a four-bar phrase with two variants is ~48 enum
  dropdowns.
- **New governed document `authoring/SSoT_Authoring_Bass_Cards.md`** (D14=A),
  registered in `ssot_manifest.yaml` and `SSoT_INDEX.md`. It owns the DSL, the
  window contract and the advisory set — and explicitly does NOT own the meaning
  of any articulation class, which stays runtime law in
  `runtime/SSoT_Composer_Bass_Track.md` §3.7.x.

### Declared divergences from the drum DSL
Deliberate, and not to be "completed" by symmetry: **no length policy** (a bass
pattern's length is content — the composer cycles it, and PHRASE-1 variants may
differ in length from the body); **lossless round-trip** (`SelfPocketStep`
carries no per-step velocity, so the glyph map is bijective and no per-cell diff
path may be added); **the warning locator is a label, not a lane index**.

### Verification
Smoke at seed 12345, G Ionian, over 8-bar and 16-bar backings: note and
legato-gesture counts match the authored text exactly (8 bars: 64 / 22; 16 bars:
130 / 39), zero parser and zero runtime warnings — which pins window → parser →
asset → composer as lossless. New suite
`Tests/Editor/BassPatternTextParserTests.cs` (15 pins).

### Also
`PhrasePresets_Bass_Spec.md` respelled to the governed alphabet and given a
fifth demonstration card ("Forget-Me-Nots groove", legato-heavy 16th funk —
an articulation skeleton, explicitly not a transcription; the DSL is pitch-blind
and the composer picks the notes).

---

## 2026-08-05 — DOC-SWEEP-1: the five-batch documentation backlog applied

Documentation-only session, zero code. Applied the full backlog of
drafted-but-unapplied `*_doc_diffs.md` files to their governed documents, in
dependency order: ORDER-1/SLAPFIG-1 → SLAPFIG-2 (selective) → ARTIC-RATE-1 →
BEND-1 → MEL-1b.

### Blocker resolved before applying
`PENDING_DOC_DIFFS.md` read **EMPTY / closed at B4** while five batches waited,
and two of them declared they stacked on POCKET-1 v2, POCKET-2 and
SOLO-1/RUNTIME-REQUALITY diffs whose application state the accumulator did not
record. Verified against the **governed documents themselves** — the
accumulator is not authority and had already produced one recorded stale-copy
incident (M-5): POCKET-1 is present as Bass SSoT §3.7, POCKET-2 as §3.7.1,
SOLO-1 as the §1 host-default block, RUNTIME-REQUALITY as Orchestration §5.5,
and `coverage-matrix.md` records all three as applied by B0 — DOC-CLOSE
(2026-07-26). The stack base was sound; the gap was bookkeeping.

### Accumulator reopened
`PENDING_DOC_DIFFS.md` was REOPENED with five entries before any document was
touched, and swept per entry as the pass proceeded, so an interruption would
have left the remaining backlog visible.

### Three diff statements contradicted the shipped code — the code won
- **ORDER-SLAPFIG-1 §5b bullet 1 SKIPPED.** It writes "SLAPFIG-2 … deferred to
  a dedicated batch" into `CURRENT_STATE.md`'s blocked list. SLAPFIG-2 shipped
  2026-08-03 and BEND-1 2026-08-05, both applied later in the same pass.
- **SLAPFIG-2 §3a field names corrected** to `hammerOffsetDegrees` /
  `pullOffsetDegrees` per BEND-1 §4a; the `*Semitones` names survive only as
  `[FormerlySerializedAs]` aliases.
- **BEND-1 §6a applied AMENDED** (sweep decision D-5): its replacement
  accumulator table listed four entries and predates MEL-1b. Written with five
  rows so it was not wrong on arrival.

### Supersessions honoured
`MGP-ALWTTT-BASS-SLAPFIG-2_doc_diffs.md` is marked **APPLIED IN PART**, never
plain APPLIED: its §1d, §1e, §2 and §5a are superseded by BEND-1 (D-DOC-SEQ=B)
and were skipped. **D-SF2-LEGATO=C is superseded** — `HammerOn` / `PullOff`
moved from RESERVED to ACTIVE. **D-SOLO-GUARD=A is superseded by
D-ORD-GUARD=A** at the orchestrator's own call site; the 3-parameter seam keeps
the original guard verbatim as the BC pin.

### Closure of the SLAPFIG-2 test-pin gap
SLAPFIG-2 shipped with its laws argued structurally and confirmed by ear, not
test-pinned, and named `BassTrackComposer_SelfPocketVocabularyTests` as its
precondition for closure. That suite exists (20 pins) and BEND-1's verification
header reports it green, so the caveat was NOT written into `CURRENT_STATE.md`
(sweep decision D-4).

### Sweep decisions
- **D-1 = C.** The MEL-2 follow-ups (`transposeScaleSteps`, "Phrase Form") are
  recorded here and in `CURRENT_STATE.md` only. No roadmap file is opened for
  an unscoped batch.
- **D-2 = A.** The per-mode diatonic triad table IS inlined in
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.7.3, explicitly marked
  DERIVED from `MusicTheory_Tonality.TonalityIntervals` and naming that source,
  so a future interval change cannot leave a silent stale copy.
- **D-3 = A.** Accumulator reopened first, swept per entry, closed at the end.
- **D-4 = A.** SLAPFIG-2 treated as closed; its "EditMode pins not yet
  written" caveat struck rather than written and later corrected.
- **D-5 = A.** BEND-1 §6a amended to five rows.

### Recorded placement deviation
SLAPFIG-2 §4a and BEND-1 §5b both target a "Bass baseline" section of
`CURRENT_STATE.md` that does not exist. Both entries were placed in
**Just completed**, newest-first, which is that file's existing convention.

### Not applied
`MGP-ALWTTT-BASS-BEND-1_code_diffs_BassTrackComposer.md` is a CODE diff whose
changes are already in the shipped `BassTrackComposer.cs`
(`BuildLegatoCarrierMap`, `ResolveLegatoGroupEndBeats`,
`ResolveLegatoDeltaSemitones`, the `PitchBendWriter.ApplyStepGestures` call
site), with `PitchBendWriter.cs` present. Verified 2026-08-05; it is a
historical record, not outstanding work.

### Follow-ups recorded, not scheduled
- **OD-ARTIC-RATE-1** — figure/rate/jitter resolution is now duplicated
  verbatim at three emission sites; extracting one shared pure helper is the
  structural fix. Raise it when the next per-event value is proposed.
- **MGP-MEL-2 "Phrase Form"** (proposed, NOT scoped): `RestPhraseSO`,
  `tailRestFraction`, `transposeScaleSteps`, and A/B/A′ phrase form with
  part-scoped motif memory stored RELATIVELY. Needs `EvenFlowPhraseSO`,
  `BurstThenHoldPhraseSO`, `SustainLeadInPhraseSO` to scope — the same three
  files that close F5.
- **F5** — `PhraseSlot.totalSlotsInPhrase` is not constant within a
  SustainLeadIn phrase. No render impact today.
- **Cards §4.5 reconciliation** — the ALWTTT bassline bundle subsection listed
  only `chordExpression` and `arpeggioRate`; four batches of card surface were
  already missing. The gap is now recorded in place rather than silently
  backfilled.

---

## 2026-08-05 — MGP-MEL-1b: procedural melody directive layer fixed and hardened

### Fixed
- **F1.** `RepeatLastNotesDirective` is now gated on `.enabled`, like the
  interval directive always was. Both are `[Serializable]` classes and always
  deserialize to an instance, so presence carried no intent; the ungated
  decorator short-circuited the strategy into a flat pitch. **This changes the
  melody rng draw sequence** — ScaleFlow now consumes the per-slot draws the
  broken decorator skipped, so the same seed yields a different (correct)
  melody. Any golden pinning procedural melody bytes must be re-pinned;
  SEED-1 rhythm / backing / bass streams are untouched (rng is per track).
- **F3 (D9).** `AscendingOnly` / `DescendingOnly` snap a violating pick to the
  nearest candidate of the SAME harmonic pool strictly above/below the phrase
  reference — scale-aware, never chromatic. With no candidate on the required
  side the inner pick is kept.

### Changed
- **F2 (D8=B).** `notesToRepeat` became a true N-note, phrase-scoped motif
  buffer replayed cyclically, with `transposeSemitones` added once per
  completed cycle. Rests never enter the motif. The transpose is CHROMATIC and
  ACCUMULATES per cycle — a recorded authoring hazard, not a defect.
- **P2.** Five reserved fields hidden (`MelodicStyleSO.swingAmount` /
  `.humanize`, `MelodicLeadingConfig.chancePassingNote` / `.voicingPreset`,
  `PhrasePaletteSO.allowCrossChordPhrases`), and
  `WeightedPhraseDirective.overrideStrategy` migrated to `useOverrideStrategy`
  + value. The old nullable never serialized, so no data migration exists or
  is needed.

### Added
- **P3.** A `logGenerator`-gated effective-leading line, one per render, which
  immediately exposed the P6 hazard live.
- **P6.1.** The procedural precedence table now has a documented home,
  `authoring/SSoT_Authoring_Melody_Composition.md` §4b. The hazard it records:
  a palette set inside a `MelodicLeadingConfig` used as `leadingOverride` is
  INERT when the card also carries `phrasePaletteOverride`.
- **P6.2.** A `logGenerator`-gated inert-config signal on the pattern path,
  mirroring the TONFILTER-1 signal idiom — a signal, never a degrade.
- **P4 (D3=C / D4=A).** `BackingCardConfigSO.adoptProgressionTonality`
  (default OFF) with readback `ResolvedTrackChoice.tonalityAdopted` /
  `.adoptedTonality`. Fires at step 2a* when the resolved progression's
  `tonalities` exclude the part's tonality; requires `tonalities.Count > 0`;
  mutates the `PartConfig` tonality IN PLACE, so restoring a base tonality is
  host policy, not package behaviour.
- **P7 (D6=B).** `PartRender.sharedProgressionData` — a name-preserving runtime
  clone of the shared-channel winner, taken post-normalization and
  post-requality, as the host's jam-continuity carry channel. Runtime instance,
  never written to disk.
- `Tests/Editor/ConstrainedMelodyStrategy_MotifTests.cs`.

### Not consumer-verified
**P4 and P7** are implemented and unit-tested but unexercised in the game; both
wait on host-side work (modal card, jam-continuity wiring).

---

## 2026-08-05 — MGP-ALWTTT-BASS-BEND-1: true legato via pitch bend, intervals in scale degrees

### Added
- `Runtime/.../Composition/Articulation/PitchBendWriter.cs` — the package's
  shared post-build pitch bend writer (pure, static, tick-domain). First
  non-note, non-CC emission in generated files; contract registered as
  `SSoT_CONTRACTS.md` §11.
- Three pure seams on `BassTrackComposer`: `BuildLegatoCarrierMap`,
  `ResolveLegatoGroupEndBeats`, `ResolveLegatoDeltaSemitones`.
- `Tests/Editor/PitchBendWriterTests.cs` and
  `Tests/Editor/BassTrackComposer_LegatoBendTests.cs`; plus
  `Tests/Editor/BassTrackComposer_SelfPocketVocabularyTests.cs`, written
  retroactively to close SLAPFIG-2's stated test-pin precondition.

### Changed
- **D-BEND-GEST=A.** A `HammerOn` / `PullOff` step no longer strikes a note.
  The nearest preceding sounding hit becomes its CARRIER, chains collapse onto
  the chain's root carrier, and the carrier's gate extends through its legato
  tail — a declared override of the §3.7.2 `min(gap, window, ceiling)` rule,
  and only for carriers that own a tail. The PLAN is never modified, so every
  SLAPFIG-2 pin stands byte-for-byte.
- **D-BEND-DEG=A.** `hammerOffsetSemitones` / `pullOffsetSemitones` became
  `hammerOffsetDegrees` / `pullOffsetDegrees` (`[FormerlySerializedAs]`,
  defaults +1 / −1): SCALE DEGREES of the part scale, so the tonality decides
  each step's size. **D-BEND-ANCHOR=A** measures the interval from the
  carrier's REACHED pitch, not the event's selected note.
- **D-SF2-LEGATO=C SUPERSEDED** — the pair moved from RESERVED to ACTIVE.

### Declared degradations
±2 semitone GM range assumed, no RPN emitted; a chained target beyond it clamps
with a warning (shrunk interval, never a wrong direction). An off-scale
starting pitch class falls back to whole tones **silently** — a data-dependent
per-hit condition, a recorded deviation from the warn-max discipline. An orphan
legato step (one that opens its chord-event window) degrades to an attacked
note, warned once per `Compose`. The two legato velocity factors now reach only
that orphan path: a bent tail inherits the carrier's velocity, since pitch bend
is channel state and carries no dynamics.

### Determinism
Zero new `ctx.rng` draws; all three seams are pure statics and the writer reads
no state. Renders without legato classes are byte-identical, pinned by a
render-hash canary.

---

## 2026-08-05 — MGP-ALWTTT-BASS-PHRASE-1: phrase-aware SelfPocket

Code closed 2026-08-05; documentation applied at DOC-SWEEP-3 (2026-09-01). The
diff sat unapplied across two documentation sweeps — DOC-SWEEP-1 (same day,
five other batches) and DOC-SWEEP-2 — because the accumulator did not carry it.

### Added
- **Bar substitutions on the SelfPocket figure.** The v1 cycled pattern is
  bar-blind; PHRASE-1 makes the bar matter. Three card fields:
  `selfPocketPhraseLengthBars` (D-PH-LEN=A), `selfPocketBarSubstitutions`
  (D-PH-SURF=D — a `{ barIndex, variants[] }` table, wrapper classes because
  Unity cannot serialize nested lists), and `selfPocketVariantSelection`
  (`SeededMix` default, or `RoundRobin`).
- Bass SSoT **§3.7.4** and one §5 update trigger.

### Contracts held
- **Single OFF gate (D-PH-BYTE=A).** An empty or fully-invalid table keeps every
  phrase field inert and the planner byte-identical, by delegation: the
  pre-PHRASE signature calls the extended overload with a null table and the
  null-table branch is the v1 lookup verbatim.
- **Zero new `ctx.rng` draws.** The phrase seed is a derived substream key,
  `StableHash32("{trackSeed}|selfphrase")`, consumed only as a mix key and never
  as a stream, so no toggle can shift a draw order. Recorded deviation: the
  derivation lives composer-side (`BassTrackComposer.ResolvePhraseSeed`) rather
  than beside the `Resolve*` family in `SongOrchestrator` — a batch-scoped
  choice to hold the touched file set to two verified-fresh files. Relocating it
  is a no-render-change refactor candidate.
- **Meter anchoring (D-PH-ANCHOR=A)** is absolute to part beat 0; chord-event
  windows never move the anchor, pinned across split windows.
- **Local degradation only (SD-PH-1=A)**, one batched `LogWarning` per Compose.
  The single GLOBAL degrade is `phraseLengthBars < 1`. An all-Rest variant is
  LEGAL — a silent break bar renders as absence.

### Declared behaviour change
**D-PH-INDEX=A.** With the phrase active, every effective pattern indexes from
its bar start. For a body length that divides the bar this matches v1 absolute
indexing; for a non-divisor length, enabling the phrase re-phases the body. Opt
in only, with no baseline to preserve.

### Scope
SelfPocket only (D-PH-SCOPE=A). SlapPocket takes its grid from the drummer's
published onsets and a phrase there would fight the external source. The planner
stays pure — zero rng, zero cross-track reads — so the SLAPFIG-1 autonomy pin
holds untouched.

### Test surface
`Tests/Editor/BassTrackComposer_PhraseTests.cs` — delegation, slot/anchor laws,
within-bar indexing, variant selection with exact `PhraseMix01` goldens (moving
either fold constant re-picks serialized cards' variants and is a declared
render-affecting change), SD-PH-1 table validation, render gates.

---

## 2026-08-03 — MGP-ALWTTT-BASS-SLAPFIG-2 (+2b): SelfPocket articulation vocabulary

### Added
- **D-SF2-VOCAB=C.** `SelfPocketStep` extended append-only with `Ghost = 3`,
  `GhostPop = 4`, `HammerOn = 5`, `PullOff = 6`. `Mute` deliberately NOT
  created (in MIDI a muted note IS a ghost note); `LeftHandSlap` deferred until
  a law distinguishes it.
- **D-SF2B-GRID=A.** `selfPocketSubdivision` extended with `QuarterBeat = 2`,
  because the classic-funk ghost vocabulary is a sixteenth-note idiom that
  `Beat` and `HalfBeat` cannot express.
- **D-SF2B-TUNE=A.** Per-class NUMBERS moved to `BasslineCardConfigSO`
  (`ghostVelocityFactor`, `ghostPopVelocityFactor`, `hammerOnVelocityFactor`,
  `pullOffVelocityFactor`, `ghostGateBeats`); the LAWS stayed in the composer,
  which keeps the byte-identity argument un-breakable from the inspector.

### Decisions
- **D-SF2-VEL=B.** Per-class velocity is a MULTIPLICATIVE factor of the chord
  event's velocity, not an additive boost. Additive boosts do not scale past
  two classes — a hot card's `(+64, +64)` clamps every boosted class to 127 and
  the dynamic relief disappears.
- **D-SF2-GATE=B.** Per-class gate ceiling: the ghost classes take
  `ghostGateBeats`, because a ghost is a click, not a short note.
- **D-SF2-PITCH=A.** The plan stays PITCH-FREE; each class's pitch is a pure
  call-site law over the event's selected note.
- **D-SF2-SWING=A.** Swing/shuffle placement, if ever added, is a CARD field
  applied inside the planner — never read from the Rhythm track's feel, which
  would reintroduce a cross-track dependency and require an orchestrator pass
  under `SSoT_CONTRACTS.md` §10.

Note: `ghostVelocityFactor` ships at 0.60, tuned by ear, raised from a
research-derived 0.35 that read too quiet through a GM slap patch whose attack
transient dominates the sample.

---

## 2026-08-03 — MGP-ARTIC-RATE-1: rate sentinel suppressed the authored figure

### Fixed
- `ChordTrackComposer.cs` grid emission site (`Compose`) resolved the
  articulation figure from `articRoller != null` rather than from
  `chordExpression == ChordExpressionType.Random`. Since CA-V1 widened roller
  construction to fire on EITHER sentinel, selecting `arpeggioRate = Random`
  created a roller and that roller then consumed the authored figure —
  `chordExpression = Offbeat` rendered as a per-event random figure, silently
  (F-ARTIC-RATE-GRID-1). Reported from ALWTTT (CTX-2b, Dev Mode articulation
  override), verified end-to-end consumer-side before filtering.
- The same site never resolved `ArpeggioRate.Random`, passing the sentinel raw
  to `Emit` where it degraded to `Eighth`: the rate roll was non-functional on
  the grid path even for `expression = Random` cards (F-ARTIC-RATE-GRID-2).
- The same site never passed `velocityJitter` to `Emit`, so §8.7's jitter was
  inert on the grid path (F-ARTIC-RATE-GRID-3).
- Root cause is single: CA-V1 aligned `RenderFromProgression` and
  `BassTrackComposer` and missed the grid site. Each sentinel now resolves only
  its own field, via independent ternaries over the independent `|artic` and
  `|articrate` substreams.

### Added
- `Tests/Editor/ChordTrackComposer_ArticRateIndependenceTests.cs` — the 2×2
  sentinel matrix driven END TO END through the grid site, asserting on emitted
  notes. Includes a positive pin that the rolled rate REACHES the articulator,
  so a fix that merely dropped the sentinel would fail.
- Once-per-render assertion warning at both emission sites when a sentinel is
  present with no roller (D-MGP-ARTIC-2=B).

### Decisions
- **D-MGP-ARTIC-1 = (C).** Neither the articulator's defensive guard degrading
  the whole articulation (option A) nor a resolution-order fault (option B):
  `ChordArticulator.PlanCore` was correct throughout and ignores `arpeggioRate`
  for every figure that does not consume it. The cause was a stale gate at
  `ChordTrackComposer.cs:658`.
- **D-MGP-ARTIC-2 = B.** One composer-side assertion warning per render, not
  per-event degrade warnings. The batch brief read §8's "never silent" as
  "always warns"; §8.2 says *never produces silence*. Recorded because the
  misreading is a natural one and will recur.
- **D-MGP-ARTIC-3 = A.** The jitter omission is fixed in the same batch rather
  than deferred: the field currently does nothing on that path, so there is no
  established sound to break.

### Meaning change
`SSoT_Composer_Backing_Track.md` §8.4's both-sites guarantee was stated at CALL
granularity ("the SAME single unconditional `Emit(...)` call"), which is true
and insufficient — the ARGUMENTS diverged. It is now stated at ARGUMENT
granularity, with the verification rule that any cross-site equivalence claim
must be pinned by a test driving EACH SITE end-to-end and asserting on emitted
notes. Every CA-V1 test sat at the roller / `PlanHits` seams, both always
correct, so the whole suite stayed green throughout the defect.

### Impact radius
Renders change, intentionally, for three card populations: concrete figure +
`rate = Random` (the authored figure is restored), `expression = Random` +
`rate = Random` (arpeggio rates now vary instead of being pinned to `Eighth`),
and any card with `velocityJitter > 0` on the grid path. Determinism under a
pinned seed is unchanged.

---

## 2026-07-31 — MGP-ALWTTT-BASS-ORDER-1 + SLAPFIG-1: order independence and the autonomous slap figure

### Fixed
- **F-BASS-ORDER-1.** A Bassline row placed BEFORE a Backing row whose harmony
  lived in its Style bundle (card override / palette — invisible to
  `FindProgressionForPart`, which reads only `Parameters.Pattern`) resolved to
  a null progression and rendered PERMANENT SILENCE. Track-list order is a
  consumer identity concern and consumers cannot reorder freely, so the fix is
  package-side scheduling, not a documentation caveat.

### Changed
- **D-ORD-MECH=A.** Both entry points compose in three passes: PASS 0 Backing
  (the shared-harmony publisher), PASS 1 everything except Backing and
  Harmony, PASS 2 Harmony. Merging is DEFERRED — each track parks in a slot
  indexed by track-list position and the slots merge in INDEX order after
  PASS 2, so chunk order follows the LIST while log order follows COMPOSE
  order. The asymmetry is intentional.
- **D-ORD-GUARD=A, superseding D-SOLO-GUARD=A.** The guard on the host default
  is no longer "a Backing row exists" but "a Backing row carries a HARMONY
  SOURCE" — a static, draw-free sniff
  (`SongOrchestrator.BackingTrackCarriesHarmonySource`). An articulation-only
  Backing row no longer suppresses the host default, and consumes it through
  its own shared-cache step, which also gives it TS normalization and requality
  — a strict improvement over the raw D-SOLO-NORM=A path. The 3-parameter
  `TrySeedDefaultProgression` seam keeps the original binary guard VERBATIM as
  the BC pin.
- The normalization-order hazard is CLOSED for the shared progression. Two
  residues remain on record: the bass's own `Parameters.Pattern` fallback
  (private harmony, outside the shared channel) and the backing-less SOLO-1
  seed path.

### Added
- **D-SFIG-SURF=A.** `PocketCouplingMode.SelfPocket = 2` — the slap/pop gesture
  with NO Rhythm track and NO cross-track read, so it cannot wake the
  consumer-side onset publication duty. Cycled, meter-anchored card pattern
  (D-SFIG-PAT=A) keyed on the ABSOLUTE grid index, so the figure keeps phase
  across chord changes; velocity based on the chord EVENT (D-SFIG-VEL=A). Zero
  rng, zero state, and everything downstream of the plan is SlapPocket
  verbatim. An empty or all-`Rest` pattern warns once and degrades
  byte-identically to `Off`.
- **D-ORD-RB.** `PartRender.sharedProgressionSource` +
  `sharedProgressionAssetName`, with `ResolvedSource.HostDefault = 7` appended.
  Composers never report `HostDefault` — it is an orchestrator-level statement
  about which source WON the shared channel, so hosts can stop keying render
  caches on the now-invalid "part has no Backing" proxy.
- **`SSoT_CONTRACTS.md` §10, track-list order contract.** No rendered output
  may depend on the ORDER of track-list entries, only on content and per-track
  keys; a new cross-track dependency needs a PASS, not a caveat. Recorded
  exception: `SlapPocket` still consumes published Rhythm onsets and degrades
  gracefully; `SelfPocket` is the order-free alternative.
- `Tests/Editor/SongOrchestrator_HarmonyOrderTests.cs`,
  `Tests/Editor/BassTrackComposer_SelfPocketTests.cs`.

### Recorded edge
A palette that looks valid to the presence-based sniff can still fail its
TS-aware pick at compose time; the Backing then degrades to procedural and the
suppressed default does NOT resurge. Not silence — a documented gap matching
pre-ORDER-1 "palette pick failed" semantics.

## 2026-07-29 — CPE-META-1 + CPE-META-2: chord asset metadata in the editor, the import payload and the LLM route

### Added — CPE-META-1 (asset metadata section)
- `Editor/ChordProgressionEditorWindow.cs` gains an "Asset Metadata
  (policy / cadence)" foldout between Allowed Tonalities and Song References:
  editable `qualityRenderPolicy`, `useColorTable` and `cadence`, plus read-only
  `DisplayName` and `originalInput` (the `[MIDI: …]` suffix stays legible as
  provenance). `useColorTable` is UI-gated under `AsAuthored` — previously a
  silent render-time no-op — and an ML-8b advisory (warn, never block) flags
  `cadence = Authentic` over a pure `DiatonicToPart` policy
  (`DiatonicToPartFunctional` is exempt: its dominant exception IS the
  cadential reading).
- The Grid tab's "Selected Chord Event" panel now edits `isDiatonic` and the
  SECDOM-1 pair (`hasAppliedTarget` / `appliedTarget`) through the existing
  working-copy + commit flow, with a non-blocking advisory mirroring the
  render-time validity rules (Reference Tonality as proxy for the triad test).
  This retires the "Grid-inspector UI is future QoL" note in
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3.

### Decisions — CPE-META-1
- **D1 = A + C.** Asset-level section AND per-event editing in the Grid tab, in
  one batch; no event list duplicated outside the grid (option B rejected).
- **D2 = C — direct binding** (the `songReferences` precedent): the section
  reads and writes `targetAsset.*` immediately, `Undo.RecordObject` + `SetDirty`
  per change. The Roman/Grid apply pipelines never read or write these fields
  (grep-verified: zero references before the batch), so apply-clobber is
  STRUCTURALLY IMPOSSIBLE and load-on-bind is automatic by construction.
- **D3 = B** (payload carries no metadata) and **D4 = out of scope** — both
  superseded the same day by CPE-META-2.
- Zero changes to `ChordProgressionData.cs`: pre-existing assets stay
  byte-identical by construction.

### Added — CPE-META-2 (import payload + LLM emission)
- Payload grammar extended with four OPTIONAL setup-card lines:
  `Quality render policy:`, `Use color table:`, `Cadence:`,
  `Allowed tonalities:` (comma-separated `Tonality` names). Absent = silent, so
  every pre-existing payload parses field-identically.
- New append-only warning kind `InvalidMetadataField`: a present-but-invalid
  value warns and the field is IGNORED. The tonality list is all-or-nothing —
  one bad name discards the list rather than silently narrowing the filter.
- Runtime stamping in `ChordProgressionRuntimeImporter.TryParsePayload`
  (Ask D path); `Editor/ChordProgressionEditorImporter.cs` mirrors it
  mechanically. `ChordProgressionLLMResponseHandler.Outcome` and
  `ChordLLMFieldPlan` carry the fields through; the window stages them.
- `ChordProgressionLLMPromptBuilder` asks for `Cadence` and
  `Allowed tonalities` (card lines + rules + self-check item 5).
- New `Tests/Editor/ChordProgressionImport_MetadataTests.cs`: legacy payloads
  declare nothing; declared metadata parses (LF + CRLF); invalid values
  warn-and-ignore without degrading `Full`; editor mirror and Outcome/FieldPlan
  pass-through; runtime stamping incl. the TONFILTER-1 replacement; legacy
  runtime instances keep defaults.

### Decisions — CPE-META-2
- **D-M2-1 = A — one-shot pending staging.** The D2=C trio is direct-bound
  asset state, so an import must neither write it silently nor park it in
  window mirror state (that would recreate the clobber D2=C removed). The
  import STAGES it in the window (serialized, survives domain reloads); a banner
  in the Asset Metadata section — visible with the foldout collapsed —
  announces exactly what will be written; the next Apply To Target Asset /
  Save As New Asset gesture (all four write sites) consumes it and clears the
  staging; a Discard button drops it. Re-applies after consumption never touch
  metadata, so the D2=C guarantee survives. `Allowed tonalities` is the
  exception: it is MIRROR state (the toggles) and rides the existing
  toggles→asset apply route.
- **D-M2-2 — card grammar.** Four optional lines; absent = silent;
  present-but-invalid = `InvalidMetadataField` + ignored, NEVER a mode
  degradation (metadata are not load-bearing mechanical fields).
- **D-M2-3 = A — one grammar, one behavior.** The runtime path stamps the same
  declared metadata on its in-memory instance; a declared tonality list
  REPLACES the TONFILTER-1 single-entry provenance default. Metadata-free
  payloads build byte-identical instances to before.
- **D-M2-4 = A — LLM emits descriptive fields only.** `Cadence` (classify what
  was emitted; `None` when unsure) and `Allowed tonalities` (must include the
  Reference tonality). `Quality render policy` and `Use color table` are
  render-semantics / lab-opt-in choices the model has no basis to make — not
  requested, but accepted by the importer when hand-written.

### Behavior
- `ImportMode` Full / ProgressionOnly / Failed semantics are UNTOUCHED: the
  extension is additive and presence-gated. Compat constructors on
  `PayloadResult` / `Result` / `Outcome` are preserved by delegation, so no
  external constructor caller breaks.
- No runtime render-semantics change: stamped metadata means exactly what it
  has always meant (`SSoT_Authoring_Chord_Progressions.md` §4.1 / §4.3).
- Editor + authoring surface only; no composer, no rng path, no determinism or
  golden implication; asset semantics unchanged.

### Documentation
- `authoring/SSoT_Authoring_Chord_Progressions.md` — §3 gains the subsection
  "Asset metadata authoring (CPE-META-1, CPE-META-2)"; §4.3 SECDOM-1 authoring
  note corrected (the grid-inspector UI shipped); §7 gains one update trigger
  for the setup-card metadata grammar.
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 gains the CPE-META-2
  paragraph (descriptive-fields-only rule, presence-gated parsing, staging
  pointer).
- `CURRENT_STATE.md` — both batches recorded under "Just completed".
- `coverage-matrix.md` — no primary-home change and no row added; a closure
  note was added by the governance sweep below. The metadata field semantics
  stay owned by `SSoT_Authoring_Chord_Progressions.md` §4.1 / §4.3 and the
  payload grammar stays with the same document's §3 / §4.2; §3 only gains tool
  surface and optional grammar.

### Verification
- CPE-META-1: in-editor exit criteria 1–7 PASS (2026-07-29).
- CPE-META-2: importer verified in editor, LLM route test green, EditMode suite
  `ChordProgressionImport_MetadataTests` green.

### Governance sweep (same session)
- `ssot_manifest.yaml` — the §4.3 opt-in-field invariant's SECDOM-1 tail
  ("Grid-inspector UI is future QoL") was FALSE once CPE-META-1 (D1=C) shipped
  that UI, and was REWRITTEN rather than appended to; a new invariant pins the
  CPE-META-1/CPE-META-2 surface (D2=C direct binding, the four optional card
  lines with absent=silent / invalid=`InvalidMetadataField`+ignored and no mode
  degradation, the all-or-nothing tonality list, the D-M2-1=A one-shot staging,
  D-M2-3=A runtime stamping, D-M2-4=A prompt scope).
- `ssot_manifest.yaml` `governs:` — `Editor/ChordProgressionEditorWindow.LLM.cs`
  registered under the chord authoring SSoT. **Pre-existing omission dating to
  L4 (2026-05-29)**, not created by these batches: the file is named in
  `SSoT_Authoring_LLM_Generation.md` §7 as "the `.LLM` partial" yet appeared in
  no `governs:`. Homed under the DOMAIN SSoT by the `.MidiImport` precedent;
  the LLM SSoT keeps the pattern stages.
- **All four PATH INFERRED flags cleared** (the package tree became available
  mid-session). Three confirmed as written —
  `Composition/Strategies/VoiceLeading.cs`,
  `Composition/ChordProgressionRequality.cs` (both dual-listed lines),
  `Composition/ModulationPlanner.cs`. **One corrected:**
  `MIDIPercussionInstrumentSO.cs` was listed under `Runtime/CoreScripts/Data/`
  and actually lives under `Runtime/CoreScripts/ScriptableObjects/`. The
  inference was wrong, which is why B0 and B4 both refused to clear these flags
  without a tree.
- **Filename correction:** the two window partials are DOT-separated on disk
  (`ChordProgressionEditorWindow.MidiImport.cs`,
  `ChordProgressionEditorWindow.LLM.cs`). The `.MidiImport` line had carried an
  underscore since M3 (2026-07-23). The underscore form is a PK-export
  artefact — PK filenames are not reliable as `governs:` paths.
- `Tests/Editor/ChordProgressionImport_MetadataTests.cs` stays UNLISTED in the
  manifest by M-1=A — this SSoT lists no test files. Recorded so the absence
  reads as a decision, not an omission.
- `SSoT_Authoring_LLM_Generation.md` invariants unchanged: the working-copy rule
  ("the asset mutates solely via the tool's explicit Apply/Save") is REINFORCED
  by the staging, not contradicted — staged metadata is consumed by that same
  gesture. All CPE-META-2 files were already registered.
- `coverage-matrix.md` — closure note added (no primary-home flip, no row
  added; every concept touched already had a row and none moved).

### Open — carried, not resolved here
- The `chord-progression-importer` skill's `references/roman_dsl_syntax.md`
  documents the importable payload shape and is now stale. WORKSHOP-side,
  outside package governance; regenerating it needs
  `ChordProgressionRuntimeImporter.cs`,
  `ChordProgressionLLMResponseHandler.cs` and
  `ChordProgressionLLMPromptBuilder.cs` in the working set. `SKILL.md` itself
  was updated.

## 2026-07-28 — B4 (DOC-CLOSE-2): drift-run corrections and the B-series completion sweep

### Changed
- Documentation-only batch, **zero code**. Applied the nine corrections derived
  from the 2026-07-28 drift run (10 findings over 6 governed documents). No
  finding was code drift: the register seams, `ResolveWalkSeed`, the A→B→C
  publication order, the `ChordProgressionData` field-by-field copy list and
  `PlanHits(..., noteCount: 1)` all verified clean. Everything corrected here is
  wording, registration or closure.
- `runtime/SSoT_Composer_Backing_Track.md` §2.2 and
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 — the F-B2-LIBRARY
  caveat (below), plus one update trigger each.
- `authoring/SSoT_Authoring_Chord_Progressions.md` — **new §4.6** for MOD-1 /
  `ModulationPlanner`.
- `runtime/SSoT_Composer_Bass_Track.md` — §0 governs list aligned with
  `ssot_manifest.yaml` (three test files added, all already cited in the body);
  §5 gained triggers for §3.6bis (WALK-2) and for the D-REG-2 / D-REG-3 folds.
- `CURRENT_STATE.md` — F-CS-SEQ resolved (below).
- `ssot_manifest.yaml` — `ModulationOctaveHint.cs` path corrected; the two
  `tonalities` invariants amended; the M-3 debt and the carried-open items
  cleared.

### Fixed
- **F-B2-LIBRARY — a documentation defect, not code drift.**
  `ChordTrackComposer.PickTemplateForPart` DOES filter by tonality: it discards
  library entries whose allowed list (`entry.compatibleTonalities` when
  non-empty, otherwise `progression.tonalities`) excludes the part's tonality,
  with a hard `continue` (`ChordTrackComposer.cs:1626–1635`), reachable from
  `BuildProceduralProgression` (`:985`) when
  `ctx.Settings.progressionLibrary != null`. **The code is intended:** B2 left
  that path out of scope on purpose and said so in
  `planning/active/Roadmap_Composition_Expressivity.md` §B2 ("the legacy
  `PickTemplateForPart` is unchanged"). What was wrong is that two SSoTs
  generalized to "does not filter selection" without the caveat, leaving the
  only record of the nuance in a roadmap — which is not authority. Both SSoTs
  now carry the exception; the code is untouched. Retiring the filter changes
  renders and is registered as an unscheduled RUNTIME candidate.
- **F-CS-SEQ.** `CURRENT_STATE.md` §Active now still framed B1 and B2 as "agreed
  next sequence, not yet opened" while listing B3 as CLOSED, contradicting its
  own §Just completed entries. Resolved by the owner: B1 and B2 are closed
  (2026-07-27), aligned with B3. The "also open, in no committed order" block
  (volume01 authoring, MGP-ALWTTT-BASSFILL-1) is unchanged — that work really is
  unopened.
- **M-3 debt cleared.** `ModulationPlanner.cs` was registered under the chord
  authoring SSoT's `governs:` on 2026-07-28 with no section describing it; §4.6
  now exists.
- **Manifest path.** `ModulationOctaveHint.cs` was listed as
  `Runtime/CoreScripts/Composition/` (PATH INFERRED) while
  `runtime/SSoT_Composer_Backing_Track.md` §6.4 gives
  `Runtime/CoreScripts/Composition/Data/`. The primary SSoT wins per
  `SSoT_INDEX.md`; the manifest was corrected and the flag removed.

### Decisions
- **D-B4-1 = A.** The F-B2-LIBRARY caveat is a single tagged sentence inside the
  existing sections, not a new subsection: it is a bounded exception, not a
  contract.
- **D-B4-2 = A.** §4.6 is a full but bounded section — output shape, D-MOD-OUT=A
  ranking with the FNV-1a seeded tiebreak, purity and zero draws, no in-package
  callers, and the load-bearing sentence: the planner returns a PLAN, not a
  progression; timing and placement are the game's decision.
- **Scope held.** Four items were raised and deliberately NOT opened: removing
  the `PickTemplateForPart` filter (runtime, changes renders); governing
  `ChordProgressionLibrarySO` and `TonalityProfileSO` (live on the procedural
  path, in no `governs:`, not readable this session); registering
  `changelog-ssot.md` in an authority class (mandatory by the §9 contract, yet
  governed by nothing); and the four remaining `PATH INFERRED` flags, which need
  the package tree — inferring a path is precisely the failure mode the auditor
  exists to catch.

### Behavior
- No runtime, composer, rng path or asset semantics touched. No render output
  changes; determinism surface and goldens untouched.

### Closure
- With the `changelog-ssot.md` and `coverage-matrix.md` sweeps below and in this
  entry, **B1 / B2 / B3 is complete by the `SSoT_CONTRACTS.md` §9 update
  completion contract** — primary SSoTs, `CURRENT_STATE.md`, `changelog-ssot.md`
  and `coverage-matrix.md` are all current.

---

## 2026-07-27 — B1 (HARMONY-PURE-1) / B2 (TONFILTER-1) / B3 (BASS-REG-1 + WALK-2)

> Retroactive entry, written at B4 (2026-07-28). The three batches closed and
> their documentation was applied over 8 governed documents on 2026-07-27, but
> the changelog was not swept at any of the three closures — which is why
> `ssot_manifest.yaml` recorded (M-4 = B) that the series was NOT closed by the
> §9 completion contract. This entry and the `coverage-matrix.md` sweep close it.

### Added — B1 (HARMONY-PURE-1)
- `ChordProgressionData.useColorTable` (bool, default `false`) — REQUALITY-2
  opt-in colour table over the render clone, effective only under a
  `DiatonicToPart*` policy and applied AFTER the core remap (D-CT-GATE=A):
  sixths by mode, `sus2` → `sus4` in Phrygian, ninths on minorized degrees with
  the Functional `V9` exception, and the `ii(dim)` → `iv` degree substitution on
  LONG or ACCENTED events (D-CT-DIM=A), size-preserving, `vii°` out of scope.
- `ChordProgressionData.cadence : CadenceType`
  (`{None=0, Authentic, Plagal, Half, Modal}`, append-only, default `None`) —
  CADENCE-META, manually authored (D-CAD-AUTH=A). Pure metadata: composers
  ignore it; consuming games may gate replace/reskin decisions on it.
- `ChordEvent.hasAppliedTarget : bool` + `appliedTarget : ScaleDegree` —
  SECDOM-1 (D-SD-ENC=A / D-SD-OWN=A). The event stores a RELATION, not a chord,
  so it survives transposition and mode change. Resolution runs at render time
  under ANY policy (`AsAuthored` included) and any tonality; an invalid target
  renders the authored event untouched and silently.
- `MidiGenPlay.Composition.ModulationPlanner` (MOD-1, D-MOD-OUT=A) — pure
  host-facing modulation planning primitive: functional dominant of the target,
  rank-ordered pivot candidates (subdominant-in-target band first, FNV-1a seeded
  tiebreak), common tones. Zero rng, zero composer edits; the host consumes it
  through `patternOverride`. Documented at B4 in
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.6.
- `Tests/Editor/ModulationPlannerTests.cs` (8),
  `Tests/Editor/ChordTrackComposer_TonalityMetadataTests.cs` (4),
  `Tests/Editor/BassTrackComposer_RegisterTests.cs`,
  `Tests/Editor/BassTrackComposer_WalkImprovTests.cs`.

### Changed — B1
- EDITOR-CASE-1 (D-EC-SEM=B): Roman case precedence is now explicit suffix >
  unambiguous case > auto. The override fires only when the case CONTRADICTS the
  diatonic family, so purely diatonic strings resolve exactly as before; mixed
  case is discarded with the only warning. **Parse-time only — saved assets do
  not change.**
- The publication pipeline's internal order is now a pinned contract:
  TS/subdivision reprojection FIRST, then A (core requality) → B (colour table)
  → C (secondary dominants), materialized in a SINGLE clone-if-changed.
- Zero impact radius VERIFIED by a byte-parity smoke; the `ChordEvent`
  field-surface reflection canary is live.

### Fixed — B1
- Two pre-existing editor F-NORM-DROP hazards: the grid's selection copy omitted
  `isDiatonic`, and its round-trip copy omitted `isDiatonic` AND
  `degreeAccidental` (accidentals were lost when saving from the grid). Both
  sites now copy all 9 fields.

### Changed — B2 (TONFILTER-1, D-B2-1=C / D-B2-2=B)
- `ChordProgressionData.tonalities` demoted to DESCRIPTIVE metadata. The
  tonality revert in step 2b of `ChordTrackComposer` and its conditional
  `ctx.rng` draw are **removed, not gated**: the PART's tonality is the card's
  authority. A foreign-tonality render signals on
  `ResolvedTrackChoice.tonalityMismatch` plus a `logGenerator`-gated warning —
  never silent, never a draw.
- Consequences on record: mismatched vs empty `tonalities` now produce
  byte-identical stems, and RUNTIME-REQUALITY became REACHABLE under a mismatch
  (pre-B2 the revert resolved qualities against the reverted reference tonality,
  making the opt-in policies a no-op exactly where they were most needed).
- The runtime importer contract is intact: it writes the reference tonality as
  provenance.
- **Not changed, on record (F-B2-LIBRARY):** the legacy `PickTemplateForPart`
  still filters library templates by tonality. Deliberate; documented at B4.
- Impact radius: only renders whose progression is tonality-incompatible. This is
  the one batch of the B-series that changes bytes.

### Changed — B3 (BASS-REG-1, D-REG-1=C / D-REG-2=B / D-REG-3=B)
- The bass now reads BOTH declared instrument bounds. The octave draw samples a
  two-octave ceiling-capped band (`ResolveOctaveBand`); a degenerate asset
  collapses it to one octave and never inverts. `MIDIInstrumentSO.octaveMax` is a
  HARD ceiling on everything the bass emits (`ResolveRegisterCeiling` =
  `octaveMax * 12 + 11`, clamped to 127): walk voicings fold as a WHOLE by −12
  (shape, intervals, pitch-class order and strict ascent preserved), pops fold
  onto the selected note. All folds pure and rng-free. Supersedes both recorded
  instances of F-WALK-REG.
- **Determinism:** the draw's RANGE changed, not its count or order, so the §2
  contract holds — but a given seed now selects a different octave and **every
  bass render changed**. Declared and decided, not drift.
- Side effect on record: `ResolvePopNote` also refuses to build a note above MIDI
  127, closing a latent out-of-range `Note.Get` on extreme assets.

### Added — B3 (WALK-2, D-W2-*)
- `arpeggioToneMode = ImprovisedWalk` (append-only value 2, D-W2-SURF=A): a THIRD
  opt-in reading of the arpeggio figures that plans a line VARYING bar to bar,
  unlike WALK-1's fixed root→3rd→5th cycle. Vocabulary D-W2-VOCAB=B: anchor hit
  at the drawn root, middle hits nearest-octave chord tones never re-striking the
  previous note, last hit a chromatic or whole-step approach into the next
  event's root (last event wraps, D-W2-LAST=A).
- Division of labor D-W2-HOME=A: the composer owns PITCHES only (pure static
  `BuildWalkLine`); the ENGINE keeps rhythm and dynamics via the public pure
  `ChordArticulator.PlanHits(..., noteCount: 1)`, each hit re-entering the same
  single unconditional `Emit` as a 1-note `Block` segment with jitter off.
  Nothing added to the engine or to `ChordExpressionType`; nothing entered the
  §8.5 pool.
- **The load-bearing property (D-W2-RNG=B):** zero `ctx.rng` draws AND no
  stateful substream. Every choice is a pure mix over `(walkSeed, eventIndex,
  hitIndex)` in the `VelocityJitter` idiom, `walkSeed =
  SongOrchestrator.ResolveWalkSeed(trackSeed)`. Because no stream exists, no
  draw-count discipline is needed: a conditional branch cannot shift a later
  event's line BY CONSTRUCTION.

### Documentation
- Applied 2026-07-27 over 8 governed documents. `ssot_manifest.yaml` was swept
  separately on 2026-07-28 (manifest-only remediation); `changelog-ssot.md` and
  `coverage-matrix.md` were swept at B4 (2026-07-28) — see the entry above.

---

## 2026-07-26 — B0 (DOC-CLOSE): batched application of three pending diff sets

### Changed
- Documentation-only batch. Applied, in order, the drafted-but-unapplied diffs of
  MGP-ALWTTT-BASS-POCKET-1 → MGP-ALWTTT-BASS-POCKET-2 →
  MGP-ALWTTT-BASS-SOLO-1 + RUNTIME-REQUALITY. See the three entries below for the
  semantics each one introduces; this entry records only the application itself
  and the decisions B0 had to take.
- `Runtime/AssemblyInfo.cs` — comment corrected (the only code touched, and only a
  comment).

### Decisions
- **F-IVT-STALE = (a).** The `InternalsVisibleTo("MidiGenPlay.Tests.Editor")`
  directive is INERT: no test in the package exercises internal access, and the two
  members its comment cited as "internal seams"
  (`ChordTrackComposer.TryDirectionalFirstChordCore`,
  `SongOrchestrator.ResolveTrackSeedPart`) are `public`, as are
  `BassTrackComposer.ResolveArticulation` and the three seams this batch group
  added. `public` is consecrated as the test-seam convention and recorded in
  `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.6; the directive is kept
  as an escape hatch. Option (b) — repair the assembly name and revert the new
  seams to `internal` — is code, needs the real test `.asmdef` name, and is
  registered as a candidate rather than taken in a doc-only batch.
- **coverage-matrix schema.** The SOLO-1 diff proposed `feature | tests | smoke`
  rows; that is not this file's schema. Translated into the existing
  `Concept | Primary authority | Secondary` shape. **No primary-home flip.**
- **`SSoT_CONTRACTS.md` deliberately unchanged.** No cross-cutting contract moved.
  "Track composition ORDER as a transversal contract" (raised by the first
  composer→composer dependency) is registered as a candidate, not promoted.
- **Numbering.** The new rhythm section is `§3bis`, not a renumbering of §4–§10:
  `§3D`/`§3E` are cited by name from `coverage-matrix.md`, `ssot_manifest.yaml`
  and sibling SSoTs.

### Fixed
- **Doc defect caught before it entered a governed document.** The POCKET-1 diff
  described `SongOrchestrator.CreateSetRhythmOnsetsForPartMusician` /
  `CreateGetRhythmOnsetsForPart` as "internal static test seams"; both are
  `public static`. Corrected on the way in — a fourth instance of F-IVT-STALE.

### Behavior
- No runtime, composer, rng path or asset semantics touched. No render output
  changes; determinism surface and goldens untouched.

---

## 2026-07-26 — MGP-ALWTTT-BASS-SOLO-1 + RUNTIME-REQUALITY: host-default progression channel + diatonic re-qualification

### Added
- `GenerateSinglePart` gains a trailing optional `ChordProgressionData
  defaultProgression` (declared on `ISongOrchestrator`), pre-seeded into the
  per-render shared-progression cache before the track loop, so a part with a
  Bassline row and NO Backing row is no longer silent (D-SOLO-SRC=A,
  D-SOLO-SURF=A2). Seam: public static
  `SongOrchestrator.TrySeedDefaultProgression` →
  `DefaultProgressionSeedResult { NotSupplied, Seeded, IgnoredBackingPresent }`.
  Contract in `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.5, consumer
  semantics in `runtime/SSoT_Composer_Bass_Track.md` §1.
- `ChordProgressionData.qualityRenderPolicy` (`AsAuthored = 0` default /
  `DiatonicToPart = 1` / `DiatonicToPartFunctional = 2`, append-only) plus the
  pure transform `ChordProgressionRequality.ApplyDiatonicRequality(prog,
  tonality)`. Grammar/semantic authority:
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.1; application sites:
  `runtime/SSoT_Composer_Backing_Track.md` §3.
- `Tests/Editor/SongOrchestrator_DefaultProgressionTests.cs` (6),
  `Tests/Editor/ChordProgressionRequalityTests.cs` (19).

### Changed
- `runtime/SSoT_Composer_Backing_Track.md` §3 now names the two data-level
  transforms that run on a progression's runtime clone and their order:
  TS/subdivision reprojection, then requality against the part's FINAL tonality
  (i.e. after the §2.2 tonality-filter alignment).
- The Bass SSoT's normalization-order hazard bullet now covers requality too, and
  records that the bass's own `cfg.Parameters.Pattern` fallback receives NEITHER
  transform — an unchanged, recorded gap.

### Fixed
- **F-NORM-DROP.** The TS/subdivision reprojection does not clone with
  `Instantiate`; it builds a fresh `ChordProgressionData` and copies fields ONE BY
  ONE, so any omitted field silently reverts to its default on the runtime clone.
  `qualityRenderPolicy` was initially omitted, which made requality inert for
  nearly every asset (authoring writes `sub x1`; the composer normalizes to `x4`).
  Fixed and regression-pinned
  (`ChordProgressionRequalityTests.PolicySurvivesFieldByFieldCloning_NormalizationParity`).
  **Any new `ChordProgressionData` field must be added to that copy list.**

### Behavior
- Both features are opt-in and byte-identical at their defaults. A null
  `defaultProgression` performs no seeding: zero rng draws, zero allocations —
  pinned end-to-end by smoke gate 3 (seeded default ≡ the same asset in the bass
  row's own `Pattern` slot, same seed, same notes). `AsAuthored` returns the same
  reference and no existing render changes by one byte.
- Guard on record (D-SOLO-GUARD=A): a default supplied to a part that HAS a
  Backing track is warn + ignore — seeding under it would fork the backing's own
  render from the shared channel. The warning names `patternOverride` on Backing
  as the supported alternative.
- Requality scope (D-RQ-BORROW=A / D-RQ-MAP=A / D-RQ-LOCRIAN=A): borrowed events
  untouched; core alphabet only and size-preserving (sus, 6ths and 9ths pass
  through; `Major` is never promoted to `Dominant7`); Locrian is a documented
  no-op. The Functional variant (D-RQ-FUNC=A / -FUNC-SCOPE=A) keeps an authored
  `Major`/`Dominant7` on the dominant degree and re-marks it borrowed, so the
  leading tone survives.

### Verification
- 25 new EditMode tests green; full pre-existing suite re-run green. 7 smoke gates
  + 1 optional pass. Gates 6 and 7 as originally specified are **void**: a Block
  bass plays roots only and the roots of I/IV/V are identical in C Ionian and C
  Aeolian, so the render could not distinguish "requality worked" from "requality
  never ran". Superseded by 6'/7' (verified through the backing, which plays full
  chords) and the optional 6b (bass walk, which exposes the third).

---

## 2026-07-25 — MGP-ALWTTT-BASS-POCKET-2: pocket velocity and trigger lanes

### Added
- `BasslineCardConfigSO` gains five authorable fields under **Pocket Coupling**,
  all inert at their defaults: `pocketSlapBoost` / `pocketPopBoost`
  (`int`, `[Range(-64,64)]`, default 0) and `pocketCustomLanes` (`bool`, default
  false) + `pocketSlapLanes` / `pocketPopLanes` (`List<GeneralMidiPercussion>`,
  default empty). Contract: `runtime/SSoT_Composer_Bass_Track.md` §3.7.1.

### Behavior
- **D-PKT-VEL2 = B.** Boosts are ADDITIVE per-class offsets over the drum step's
  resolved velocity, clamped 1..127. Published onsets already arrive clamped, so a
  boost of 0 is an EXACT identity. Applied at classification time — observationally
  equivalent to applying it after the same-beat dedupe (the offset is uniform
  within a class, and the pop-wins rule never compares velocities) — and BEFORE the
  per-event `VelocityJitter` refold, which clamps independently.
- **D-PKT-LANES2 = C, serialization C1.** With the toggle on, the two lists
  REPLACE the built-in families rather than extending them; an empty list DISABLES
  that trigger class; a lane in both lists resolves to POP. Matching stays on the
  SEMANTIC authored lane, so it is immune to PERC-FALLBACK-1 substitutions —
  field-verified against a kit mapping `SideStick` onto `AcousticSnare`.
- Every field lives inside the `pocketMode = SlapPocket` branch, so the POCKET-1
  degrade guarantee is structurally unaffected. Zero new `ctx.rng` draws; keying
  untouched; every pre-POCKET-2 asset deserializes into the v1 behaviour.

### Not taken (on record)
- VEL2=C (float scale + curve) — deferred, no content demanded it. VEL2=D (revert
  D-PKT-VEL=A) — rejected, it removes the "breathes with the drummer" property that
  motivated the mode. LANES2=B (fold `SideStick` into the snare family) — rejected,
  it hardcodes a genre opinion where the same result is a content decision.

### Verification
- 27 tests in `BassTrackComposer_PocketTests`, of which the 16 POCKET-1 tests run
  UNMODIFIED against the extended seam signature — the default-path identity pin at
  seam level. 11 smoke gates pass, including G3, the degrade gate under the most
  hostile shaping available (`+30/−30`, custom lanes on, both lists empty), which
  still produces a bass hash identical to `Off` in the same track order.

### Findings
- **F-WALK-REG, second instance.** A pop is the selected note + 12, uncapped, so a
  pocketed bass overshoots the §2 register band by an octave — a different
  mechanism from the walk's upward stacking. Two shipped bass features now exceed
  the band and the bass still ignores `octaveMax`. Deliberately not capped here;
  the cap belongs to the batch that narrows the band (B3 — BASS-REG-1).
- **Golden fragility.** Every POCKET-2 smoke hash is a function of the bass
  instrument's `octaveMin`, which was edited between capture sessions (`Slap Bass 1`
  2 → 1; the arithmetic is the detector, since the band is only three values wide).
  Every gate's internal A/B remains valid, but **these hashes are not a durable
  golden**: re-derive them if `octaveMin` changes, and never read a mismatch as a
  POCKET-2 regression without checking the instrument asset first. Cross-track-order
  comparison is separately invalid.

---

## 2026-07-25 — MGP-ALWTTT-BASS-POCKET-1: rhythm-coupled bass (SlapPocket)

### Added
- `runtime/SSoT_Composer_Rhythm_Track.md` **§3bis** — onset publication. On the
  GRID path only, `RhythmTrackComposer.Compose` publishes the resolved pattern's
  audible onsets via `ctx.SetRhythmOnsetsForPartMusician(part, cfg.MusicianId,
  onsets)`, after TS normalization and only when a sink is installed. Payload
  `MidiGenerator.RhythmOnset` (semantic lane, part-relative beat, resolved 1..127
  velocity) from the pure seam `ExtractResolvedOnsets`, with three deltas against
  the compose path: truncation at the part end, an audibility filter (only lanes
  that RESOLVE on the kit, published under their SEMANTIC name), and (beat,
  instrument) ordering.
- `GenContext` rhythm onset channel (`GetRhythmOnsetsForPart` /
  `SetRhythmOnsetsForPartMusician`), same mould as the progression and melody
  caches, wired identically in both entry points —
  `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.
- `runtime/SSoT_Composer_Bass_Track.md` **§3.7** — the SlapPocket coupling.
- `Tests/Editor/BassTrackComposer_PocketTests.cs`,
  `Tests/Editor/RhythmTrackComposer_OnsetPublicationTests.cs`.

### Behavior
- Opt-in via `BasslineCardConfigSO.pocketMode` (`PocketCouplingMode { Off = 0,
  SlapPocket = 1 }`, default `Off`). Per chord event, a window containing
  kick/snare onsets has its figure REPLACED: kick family → slap on the §2 selected
  note; snare family → pop at +12 (D-PKT-POP-PITCH=A; side stick deliberately
  excluded in v1). Velocity is the DRUM step's (D-PKT-VEL=A); hit length is
  `min(gap, remaining window, 0.5 beat)` (D-PKT-GATE=A). Same-beat: pop wins
  outright; within a class, max velocity wins. A window WITHOUT kick/snare onsets
  renders the resolved figure exactly as decoupled (D-PKT-EXPR=A), so pocket and
  figures mix within one render; pocketed events bypass both the figure and the
  walk.
- **D-PKT-SRC=B** makes this the package's **first composer→composer data
  dependency**. It is order-sensitive by design (**D-PKT-ORDER=A**: Rhythm must
  precede Bassline in `Part.Tracks`) and the CONSUMER owns the degrade path. The
  procedural and legacy rhythm paths publish nothing in v1 — scope on record, not
  an implied behaviour.
- **Degradation is BYTE-identity, test-pinned**
  (`PocketOn_WithoutAnyRhythmTrack_IsByteIdenticalToOff`): no source ⇒ the
  decoupled figure, at most one warning per `Compose`, never an error, never
  silence. This holds structurally because the CA-V1 roller rolls per event
  whether or not its result is used, so source availability can never shift the
  roll stream.
- **Zero new `ctx.rng` draws.** `BuildPocketPlan` is a pure function of (published
  onsets, event window) and runs after both §2 draws — the same structural
  argument as D-WALK-RNG=A. Classification uses the SEMANTIC lane, so
  PERC-FALLBACK-1 substitutions cannot re-classify a hit.

### Changed
- Bass emission restructured into a SEGMENT list (one decoupled segment, or N
  1-note `Block` segments when pocketed) drained by ONE unconditional
  `IChordArticulator.Emit` call — the SD-F2-1 anti-divergence discipline carried
  over segments. The engine stays RNG-free and pitch-preserving; nothing was added
  to `ChordExpressionType`.

### Consumer duty
- With `pocketMode != Off` the resolved rhythm pattern is a hash-relevant INPUT of
  the BASS track: ALWTTT extends `ComputeTrackInputsHashesForPart` (identity
  available in the Rhythm track's Ask A readback). Track order and the re-render
  pattern are handed over in
  `reference/cross-project/ALWTTT/Handoff_MGP_POCKET.md`.

### Open observation (not a defect)
- In the bass-only and bass-first renders the operator read the bass MIDI as
  spanning 4 bars rather than the part's 8. The logs contradict it: the part is
  `lenTicks=3072` and the bass reports `notes=4 lastTick=3073`, trimmed at 3072 —
  4 Block notes of 2 bars each covering the full part, and `BassTrackComposer`
  derives its spans from the progression alone. Undecided causes: a DAW
  display/import artefact (the captures show each track drawn twice), or the
  progression asset summing to fewer beats than assumed (which would contradict
  `lastTick`). Decisive check: the bar position of the last bass note-off.

---

## 2026-07-25 — INST-WIZ-1: MIDI instrument catalogue wizard + dropdown-drawer repair

### Added
- `Editor/MidiInstrumentCatalogueWizard.cs` — catalogue + management window for
  `MIDIInstrumentSO` / `MIDIPercussionInstrumentSO`: two-root scan, filters
  (melodic/percussion, `InstrumentType`, free text over every serialized
  property), four sort modes, single-target embedded-inspector editing,
  create / duplicate / rename / delete (delete confirmed), and CSV export of the
  filtered set to file or clipboard. Menu:
  `MidiGenPlay/MIDI Instrument Catalogue Wizard...`.
- `SSoT_Authoring_Tools` §3.E — catalogue tool variants (browse-only vs
  catalogue + management) and why the management variant does not duplicate the
  §2 normalize → preview → apply loop.

### Changed
- `Editor/SoundFontDropdownDrawer.cs`, `Editor/BankDropdownDrawer.cs`,
  `Editor/PatchDropdownDrawer.cs` — rewritten (D-W2=B). Writes gated behind
  `EditorGUI.BeginChangeCheck`; every write via `SerializedProperty`, including
  the dependent `BankName`/`PatchName` resets and the `PatchName`+`PatchIndex`
  pair; `showMixedValue` on mixed selections; popups disabled when the list
  source is ambiguous across the selection.
- `ssot_manifest.yaml` — the read-only catalogue invariant rewritten to cover
  both variants (it contradicted this batch as written); new drawer
  write-discipline invariant; `MidiInstrumentCatalogueWizard.cs` added to
  `SSoT_Authoring_Tools` governs.

### Fixed
- **Data loss on multi-selection.** `PatchDropdownDrawer` assigned
  `property.stringValue` on every repaint; a `SerializedProperty` write applies
  to all selected targets, so opening the inspector on several instruments
  copied the first one's patch onto all of them. The sibling drawers had the
  same shape via an index-normalisation bug (`Mathf.Max(idx, 0)` vs a `-1` "not
  found"), which additionally fired the dependent field resets.
- **Silent asset writes in single selection.** The same unconditional writes
  "repaired" out-of-list values on inspector open — a standing violation of the
  §1 "editor tools must not write to assets silently" invariant.
- **Incoherent name/index pairs.** `PatchIndex` was written by direct method
  call (first target only) while `PatchName` was written by property (all
  targets); both now go through `SerializedProperty`.

### Behavior
- Editor-only: no runtime file, composer, rng path or asset semantics touched.
  No render output changes; determinism surface and goldens untouched.
- Deliberate change on record: an out-of-list `BankName` no longer auto-corrects
  to the first bank when the inspector is drawn; it renders as no selection until
  the user chooses.

### Findings
- Full catalogue export (79 assets — 70 melodic in bank 000, 9 kits in bank 001):
  `PatchName` numeric prefix equals `PatchIndex` in every asset; no duplicate
  `(bank, patch)` pairs. The standing PatchName/PatchIndex hygiene candidate
  closes with **no findings**, and the ALWTTT Poly Synth / Warm Pad collision is
  confirmed a measurement artifact on the consumer side.
- `volume01 = 1.0` across all 70 melodic instruments confirmed from data — the
  flat baseline is unauthored, not deliberate. D-MIX-6 remains open and blocked
  on ALWTTT D-CSV-18.
- Cosmetic, not a defect: `Pizzicato Strings` is the only asset whose
  `PatchName` (`45 - Pizzicato`) does not repeat its `InstrumentName` in full.
- `ChordProgressionCatalogueWizard.cs` is absent from the `SSoT_Authoring_Tools`
  governs list — a pre-existing omission, recorded, not fixed here.

### Decisions locked
- **D-W1=A** — catalogue tools gain a management variant; editing is delegated
  to the asset's own inspector for exactly one target rather than duplicating
  the authoring normalize → preview → apply pipeline (instrument assets are flat
  config with nothing derived to preview).
- **D-W2=B** — drawers made multi-edit **correct**, not multi-edit **blocked**;
  the alternative (disable on `isEditingMultipleObjects`) was rejected because it
  would have left the single-selection silent-write bug in place.

### Not changed
- No instrument asset was edited by this batch. `MIDIInstrumentSO.cs` is
  untouched — including its now-obsolete
  `[Header("⚠️ WARNING: DON'T SELECT MULTIPLE INSTRUMENTS ⚠️")]`, which is a
  separate cosmetic decision (see the open item below).

## 2026-07-24 — CA-T2-BOSSA-V2: authentic bossa template + member rename (CA arc re-closed)

### Added
- `ChordExpressionType.Bossa = 10` — the AUTHENTIC bossa comping figure: the lab
  spec's `basico_solo` 1-bar template (LOW 0.0×2.0 medium · UPPERS 0.0×1.0
  medium · UPPERS 1.0×1.5 weak · LOW 2.0×2.0 strong SURDO · UPPERS 2.5×1.5
  strong SYNCOPATION, sustained to the cycle end, no attack on beat 3).
  Append-only after value 9; 0..9 serialized and unchanged.
- `ChordArticulator.BossaTemplatePlan` — pure planner: bar-length cycle from
  absolute beat position (mid-cycle chord changes inherit phase); meter
  clipping below 4/4; LOW onset-fallback hit; degrades to Block on ≤1-note
  voicings, beatsPerBar ≤ 0, or windows with no UPPERS attack.
- `ChordArticulator.TierVelocity` + `BossaTier*` constants — D-FEEL-ACCENT=A:
  template-supplied accent tiers reusing the SD-5 factor values (the surdo
  inversion §8.3 cannot express positionally).
- 15 tests in `Tests/Editor/ChordTrackComposer_ArticulationTests.cs`, incl. the
  surdo regression test and an emitted-MIDI attack-group probe.

### Changed
- `ChordExpressionType`: member 9 RENAMED `Bossa` → `BassUpperSplit`
  (OD-BOSSA-7=A/-7a=A). Name-only: value intact, enums serialize by value, the
  member is never parsed or persisted by name (verified). Surface: exactly 4
  files; the v1 tests were renamed, not rewritten.
- `ChordArticulator.Emit`: the sorted-copy and uppers-subset gates now cover
  both register-selective figures; sentinel translation unchanged.
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs` — tail tripwire updated
  deliberately (OD-BOSSA-6=A): BassUpperSplit=9, Bossa=10, count 11, BOTH
  pool-excluded.

### Behavior
- Every pre-batch path byte-identical: the rename changes no plan, and the new
  figure is reachable only by selecting value 10.
- `Bossa = 10` is excluded from the §8.5 Random pool by the existing
  `>= ConcretePoolSize` mechanism — no rule edited.
- §8.3 gains a documented per-figure accent exception (D-FEEL-ACCENT=A).

### Decisions locked
- OD-BOSSA-7=A, OD-BOSSA-7a=A (`BassUpperSplit`), D-FEEL-HOME=A,
  D-FEEL-SCOPE=A, D-FEEL-PHASE=moot, D-FEEL-TIE=A, D-FEEL-ACCENT=A,
  D-FEEL-DIFFS=A; OD-BOSSA-6=A reaffirmed.

### Not changed
- `ChordReshaper.cs` (identity by exclusion covers the new member), every
  composer, card surface, orchestrator, and seam signature.

### Deferred
- 2-bar spec patterns + cycle phase anchoring · harmony-carrying anticipation
  (`carries_next_harmony`) · LOW_ALT root/fifth alternation · muted ghost
  strokes. All recorded in Backing §8.6; none blocked on the seam.

### Arc
- The CA arc, closed by CA-T2-BOSSA, was REOPENED by finding F-BOSSA-FEEL and
  is CLOSED again.

## 2026-07-24 — CA-T2-BOSSA: register-selective bossa bass/upper split (CA arc closed)

### Added
- `ChordExpressionType.Bossa = 9` — Tier-2 register-SELECTIVE figure (append-only
  after `Chugging = 8`; values 0..8 serialized and unchanged).
- `ChordArticulator.BossaPlan` — pure planner: low note at the event onset and at
  every interior bar downbeat (legato to the next low hit / event end), upper
  voices on `Offbeat`'s grid at ≤0.5 beat.
- `ChordArticulator.UpperVoicesIndex = -2` — subset sentinel in the `Hit.NoteIndex`
  selection vocabulary; `Hit` struct shape unchanged.
- 11 tests in `Tests/Editor/ChordTrackComposer_ArticulationTests.cs`, including an
  emitted-MIDI probe (downbeat = lowest voiced pitch; offbeats = the non-lowest
  pitches) and a byte-level rate-independence test.

### Modified
- `SSoT_Composer_Backing_Track.md` §8.4 — the `Hit.NoteIndex` selection vocabulary
  is now documented and closed (`-1` full chord, `-2` uppers, `>= 0` sorted index),
  with the exact-match translation rule and its rationale.
- `SSoT_Composer_Backing_Track.md` §8.6 — Tier 2 now distinguishes
  voicing-RESHAPING from register-SELECTIVE members; `Bossa` documented; the
  "Deferred" bossa paragraph replaced by "Delivered" plus finding F-BOSSA-FEEL
  and the authentic-template deferral.
- `SSoT_Composer_Backing_Track.md` §7.5 — Bossa's anchor note named as the end of
  the voicer → pin → reshape chain (no new precedence rule).
- `SSoT_Composer_Bass_Track.md` §3.3 — records that `Bossa` on a bassline card
  degrades to `Block` (≤1-note voicing); no bass-side code changed.
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs` — the enum tail tripwire
  updated for the governed append and strengthened with a pool-exclusion assert.

### Behavior
- Default `Block` unchanged; every pre-batch path byte-identical (the new figure
  is reachable only by selecting it).
- `Emit` sentinel translation changed from a blanket `NoteIndex < 0` test to exact
  matching; behaviour for `-1` and `>= 0` is unchanged, undefined negatives now
  degrade to the full chord explicitly.
- Bossa's emitted pitch set is a SUBSET of the voicing handed to `Emit` — no pitch
  is created or altered.

### Decisions locked
- D-BOSSA-HOME=A, D-BOSSA-SEL=A, D-BOSSA-BASSNOTE=A, D-BOSSA-RHYTHM=A,
  OD-BOSSA-1=A, OD-BOSSA-2=A, OD-BOSSA-3=A, OD-BOSSA-4=A, OD-BOSSA-5 (`Bossa`),
  OD-BOSSA-6=A.

### Not changed
- `ChordTrackComposer.cs`, `BassTrackComposer.cs`, `ChordReshaper.cs`,
  `BackingCardConfigSO.cs`, `BasslineCardConfigSO.cs`, the orchestrator, and every
  seam signature. The reshaper's existing identity guard already covered `Bossa`.
- The §8.5 Random pool: `Bossa = 9` is excluded by the existing
  `>= ConcretePoolSize` mechanism, no rule edited (D-T2-POOL=A′).

### Findings
- **F-BOSSA-FEEL** (post-smoke): the v1 template is a REGISTER SPLIT, not an
  authentic bossa rhythm — low on every bar downbeat, uppers on every offbeat is
  a regular alternation reading as a calm ska upstroke. Correct and useful as
  shipped; the stylistic label overreaches. Corrects an earlier claim that the
  clave was blocked on "bar parity the articulator is not given": parity IS
  derivable from absolute beat position. The real gaps are cycle PHASE
  ANCHORING, full-chord attacks inside the template, and attacks that tie across
  a chord change.

### Open
- **OD-BOSSA-7** — member name. `Bossa = 9` names a feel it does not deliver.
  Rename to `BassUpperSplit` / `RegisterSplit` (value unchanged) and reserve
  `Bossa`, or keep and improve in place. Not decided.

### Deferred
- Authentic bossa template (blocked on a rhythmic specification, not on the seam).
- Rhythm-driven backing accents (new inter-composer input dependency).

### Arc
- The CA (Chord Articulation) arc is CLOSED: CA-T1, CA-F2, MGP-ALWTTT-ARTIC-1,
  CA-T2, CA-V1 (parts 1–2), BASS-WALK-1, CA-T2-BOSSA.

## 2026-07-24 — BASS-WALK-1: chord-tone walk for the monophonic bass

### Added
- `BassArpeggioToneMode { RepeatedNote = 0, ChordToneWalk = 1 }` — bass-only
  enum declared in `Runtime/CoreScripts/Composition/Data/BasslineCardConfigSO.cs`
  (D-WALK-SURF=A; append-only, never renumbered). `ChordExpressionType` is
  deliberately NOT extended, so nothing enters the shared engine or the backing
  card's §8.5 Random pool.
- `BasslineCardConfigSO.arpeggioToneMode` (default `RepeatedNote`).
- `BassTrackComposer.BuildWalkVoicing(NoteName[] chordPcs, int rootOct)` —
  internal static, pure, RNG-free. Returns the first `Min(3, chordPcs.Length)`
  tones stacked strictly ascending from the root at the drawn octave, each tone
  lifted one octave if it would fall at or below the previous note.
- `ChordArticulator.ArpeggioFits(double durBeats, ArpeggioRate rate)` — public
  static pure predicate exposing the existing arpeggio degrade rule
  (D-WALK-FIT=A). No behavior added; a read-only view of `ArpeggioPlan`'s
  condition.

### Modified
- `Runtime/CoreScripts/Composition/Composers/BassTrackComposer.cs` — resolves
  `arpeggioToneMode` alongside the CA-V1 knobs, and at the single emission site
  chooses the `playable` handed to the unchanged `Emit` call: the walk triad
  when (walk mode AND the resolved figure is `ArpeggioUp`/`ArpeggioDown` AND
  `chordPcs.Length >= 2` AND `ArpeggioFits`), else the 1-note voicing. The
  emission call itself is untouched — still ONE unconditional `Emit`.
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs` — 9 tests added
  (voicing stacking + wrapping case, root anchoring + purity, triad-only
  truncation, Up ≠ Down under walk vs Up ≡ Down under `RepeatedNote`, walk
  determinism across rates, `ArpeggioFits` ↔ degrade equivalence, the monophony
  guard incl. the hazard it prevents, enum defaults, and a pin that
  `ChordExpressionType` gained no member).

### Behavior
- `arpeggioToneMode = RepeatedNote` (default): unchanged output. Bit-identity is
  STRUCTURAL — the walk branch is gated on the enum.
- `ChordToneWalk` + `ArpeggioUp`: root → 3rd → 5th → root at the card rate.
  `ArpeggioDown`: 5th → 3rd → root. Non-arpeggio figures ignore the mode.
- Short events (shorter than one arpeggio hit) fall back to the 1-note playable
  and degrade to a true legacy `Block` — the line never becomes polyphonic.
- The §8.5/§3.5 Random roll is unaffected; in walk mode the documented
  monophonic pool bias no longer arises.
- Register rises in walk mode: the stack is upward from the root, so the top of
  each event sits a fifth above the drawn root (see findings).

### Findings
- **F-WALK-REG (on record, no fix in this batch).** Walk mode raises the bass
  line's effective ceiling by roughly a fifth, on top of a pre-existing
  three-octave sampled band (`octaveMin-1 .. octaveMin+1`). Two contributing
  facts, both pre-dating this batch and now documented in §3.6: the band is wide
  for a bass, and the bass IGNORES `MIDIInstrumentSO.octaveMax` (`maxOct` is
  hardcoded to `octaveMin + 1`), unlike the chord and melody composers which use
  `octaveMin-1 .. octaveMax-1`. Immediate mitigation is authoring-side (lower
  `octaveMin` on the bass instrument asset). Narrowing the band in code is a
  recorded candidate requiring its own decision: it changes every bass render,
  though NOT the determinism surface (the octave draw keeps its count and order,
  only its range).

### Decisions locked
D-WALK-HOME=A · D-WALK-RNG=A · D-WALK-SURF=A · D-WALK-TONES (triad) ·
D-WALK-DIR (engine sort order) · D-WALK-FIT=A · D-WALK-ANCHOR (root-anchored).
The SD-F2-2 deferral is RESOLVED.

### Not changed
- `ctx.rng`: no new draws, no reordering. The §2 bass draw contract (1 draw root
  mode / 2 chord-tone mode, in that order) holds by construction.
- The articulator: still pure, RNG-free, stateless; no new figure, no seam
  signature change, no enum renumbering.
- `IChordArticulator` / `IChordReshaper` / the factory / `ITrackComposer`.
- The DBG-1 readback: the tone mode is not reported (same discipline as CA-V1's
  R4).
- The smoke no-asset fallback (`SmokeEntry` / `SmokeRenderUtil`): the walk knob
  is reachable only through an authored `BasslineCardConfigSO` asset in the
  Style slot. Extending the fallback is an optional rider, not part of this
  batch's DoD.

## 2026-07-24 — CA-V1 part 2: seeded velocity jitter + arpeggio-rate variety

Closes the CA-V1 batch (part 1 was MGP-ALWTTT-ARTIC-1). Two opt-in variation
axes, both off by default; supersedes D5 (fixed rate) and D6 (bass
degrade-only).

### Added
- `Runtime/CoreScripts/Composition/Data/VelocityJitter.cs` — new value type.
  `Amount` + `Seed`, `ForEvent(eventIndex)` scoping, `DeltaFor(hitIndex)`
  uniform over [-n, +n]. **D-V1-JIT-SRC=A: a pure integer mix, not a stream** —
  the articulator stays RNG-free (SD-3=A intact), the jitter is immune to
  draw-order coupling, and goldens are exactly pinnable across .NET versions.
  Distinct fold constants for the event and hit axes (the matrix must not be
  symmetric). `MaxAmount = 64` defensive clamp.
- `SongOrchestrator.ResolveArticulationRateSeed` (FNV-1a `"{trackSeed}|articrate"`)
  and `ResolveVelocityJitterSeed` (`"{trackSeed}|articvel"`) — two new SEED-1
  substreams. The jitter one is consumed as a seed for a pure mix, never as a
  `System.Random`.
- `ChordExpressionType.cs` — `ArpeggioRate.Random = 3` (append; 0..2 serialized
  and unchanged), the exact mirror of `ChordExpressionType.Random = 6`.
- `RandomArticulationRoller` — second stream (`rateRng`, optional 4th ctor
  arg), `NextRate()` with the same draw discipline as `NextFigure()`,
  `ConcreteRatePoolSize`, `RateHistory`, trace extension.
- `Tests/Editor/ChordTrackComposer_VelocityJitterTests.cs` — 16 tests
  (substream goldens, exact jitter goldens, bound/coverage, fold asymmetry,
  default-jitter identity across every enum member, Block/PerBeat golden
  velocities, both clamps, timing invariance, determinism, rate degrade).
  Additions to `ChordTrackComposer_RandomArticulationTests.cs` (rate roll +
  **stream-orthogonality pin**) and `BassTrackComposer_ArticulationTests.cs`
  (role-based substream separation, sentinel pass-through).

### Modified
- `IChordArticulator.Emit` / `ChordArticulator.PlanHits` — optional trailing
  `VelocityJitter jitter = default` (the recorded extension route: the
  `IChordVoicer.VoiceChord` `forcedInversion` precedent, not a signature
  change). `PlanHits` split into the jitter wrapper + `PlanCore` (the figure
  switch, verbatim); jitter applied as a post-pass indexed by hit position, so
  no figure branch learned about it. `ArpeggioIntervalBeats` degrades a leaked
  `ArpeggioRate.Random` to `Eighth`.
- `ChordTrackComposer` — roller now built when EITHER sentinel is selected (both
  streams constructed); render-level `VelocityJitter` threaded through
  `ComposeProcedural` → `RenderFromProgression`; both emission sites resolve
  figure and rate independently and pass `jitter.ForEvent(eventIndex)`.
  **`SnapshotRolls` now reports `null` for an empty figure history** — without
  this, a rate-only random render would have silently broken the DBG-1 readback
  contract (R4).
- `BassTrackComposer` — D6 lifted: own roller + jitter + `eventIndex` counter.
  The note-selection loop and its `ctx.rng` draw count/order are untouched.
- `BackingCardConfigSO` (+`velocityJitter`); `BasslineCardConfigSO`
  (+`randomRerollChance`, `randomFigureWeights`, `velocityJitter` — D-V1-BASS=B
  parity, motivated by the monophonic pool bias where `ArpeggioUp` ≡
  `ArpeggioDown`).
- Smoke surface (R5, ungoverned dev infra per D-SMOKE-DOC-1=A):
  `SmokeEntry` (+`velocityJitter` + Clone), `SmokeRenderUtil.BuildEffectiveSpec`
  (+ param; bassline branch now also receives the Random knobs),
  `CompositionSmokeWindow` (knobs for both roles and both sentinels, jitter
  slider, **`RandomOnBasslineWarning` deleted** — it now asserts something
  false), `CompositionSmokeRunner` (call-site parity).
- `Tests/Editor/ChordMarkerParityTests.cs` — one argument
  (`velocityJitter: default`) at the `RenderFromProgression` seam call. Kept
  explicit rather than defaulting the parameter, so any future call site must
  decide (anti-divergence discipline).

### Behavior
- Default (`velocityJitter = 0`, no `Random` sentinels): byte-identical, and
  **structurally** so — `ApplyJitter` returns the input list by reference.
- Jitter on: every hit offset uniformly in [-n, +n] and clamped 1..127,
  `Block` included; `Block`'s legacy 0..127 clamp applies only with jitter off.
- Rate sentinel on: per-event rate roll on its own substream; enabling it does
  not shift a single figure roll (test-pinned).
- Bass with `Random`: rolls instead of degrading; its sequence differs from the
  backing's by construction (role is in `trackSeed`).

### Decisions locked
- D-V1-JIT-SRC=A · JIT-SCOPE=A · JIT-SHAPE=A · RATE-SEL=A · RATE-STREAM=A ·
  RATE-GRAN=A · RATE-POOL=A · BASS=B · R4 (readback unchanged) · R5 (smoke in
  scope). D5 and D6 SUPERSEDED.

### Not changed
- `ITrackComposer`; the figure math, accent curve and degrade rules; the §8.5
  figure pool and weight semantics; `ctx.rng` consumption anywhere; the DBG-1
  readback type; seed policy stays host-side (no new entropy site).
- Pre-existing, on record: bass single-pass rendering, normalization-order
  hazard, `degreeAccidental` ignored.

### Cross-project (ALWTTT side — tracked there)
- `ArpeggioRate.Random` and the new card fields are visible consumer surface;
  the boundary doc needs a note. Any consumer byte-identity baseline must assume
  `velocityJitter = 0`.

## 2026-07-24 — MEL-BEATUNIT-1: beat-unit-aware melody timing (finding F-1 resolved)

Runtime fix + tests + documentation. One invariant reworded; no contract redesigned,
no decision reversed.

### Changed — code
- **`Runtime/CoreScripts/Composition/Composers/MelodyTrackComposer.cs`**
  - New single conversion seam
    `internal static ITimeSpan BeatsToSpan(double beats, MusicalTimeSpan beatSpan)`,
    sited next to `MinNoteBeats`, carrying the deviation record in its XML doc.
  - `var beatSpan = GetBeatSpan(part.TimeSignature);` added beside the `beatsPerBar`
    each path already derived, at all three meter-derivation sites.
  - Six emission expressions moved off `MusicalTimeSpan.Quarter.Multiply(...)` onto the
    seam: `ComposeFromPattern`, the procedural `ComposeMelodyFromProgression`, and
    `ComposePerBeatMelody`.
  - `ComposePerBeatMelody` marked **unreachable** (its only call site is commented out in
    `Compose`) and corrected in lockstep so re-enabling it cannot reintroduce the desync.
    Deleting it is a separate, open question.
  - The Phase 4 header comment claiming "one beat = a quarter" corrected — it had become
    false with the change.

### Changed — tests
- **`Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs`** (8 → 12 tests, still no
  Unity fixtures): `BeatsToSpan_FourFour_IsBitIdenticalToLegacyQuarter` (the
  non-regression control), `BeatsToSpan_SixEight_IsHalfTheLegacyQuarterTicks` (the fix,
  mirroring the bass pin `Block_MonoEmit_BitIdentityHoldsPerBeatSpan_EighthDiffersFromLegacyQuarter`),
  `BeatSpan_AllTimeSignatures_MatchTheirBeatUnit` (all 8 meters), and
  `Resolve_SixEightPart_ResolutionSeamIsUnchanged` (the batch's upper boundary). A
  `using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;` alias was
  needed because `DryWetMidi.Interaction` declares its own `TimeSignature` — same pattern
  as `SongOrchestratorSeedTests`.

### Changed — docs
- `runtime/SSoT_Composer_Melody_Track.md` — §7 "Meter & looping" rewritten off the
  quarter assumption; new **§7.1** records the deviation in the shape Bass §3.4 uses;
  guide-note handoff now states the unit; §8 gains a trigger for the timing unit / the
  seam being bypassed. The F-1 characterization block is superseded by §7.1.
- `ssot_manifest.yaml` — the authored-pattern invariant no longer says beats are
  "quarter-mapped"; header changelog block added.
- `CURRENT_STATE.md`, `coverage-matrix.md` — closure recorded; the "smoke melody in 4/4"
  instruction withdrawn; `Next` renumbered.
- `SSoT_CONTRACTS.md` §5 — `BassTrackComposer` and `MelodyTrackComposer` added to the
  list of composers the meter-authority rule especially covers. Both already applied it;
  the list had simply not been updated after CA-F2.

### Decisions
- **Scope = all three call sites** (two live, one unreachable-but-corrected), over
  fixing only the live pair.
- **4/4 identity is structural, and pinned at the seam.** `GetBeatSpan(FourFour)`
  *returns* `MusicalTimeSpan.Quarter`, so the substitution cannot drift; the test asserts
  span identity and tick equality across a multiplier set rather than reaching for MIDI
  goldens (which would have required the `MIDIInstrumentSO` / `SongConfig` / `GenContext`
  fixtures this test file deliberately avoids).
- **No migration of authored X/8 patterns.** `MelodyPatternData` stores timing in beats
  and inherits `PatternDataSO.TimeSignature`; `MelodyMidiImporter` already writes
  `gridBeats = quarterNotes × beatUnit / 4`. The stored beats were always in the meter's
  beat unit — only the render misread them, so rescaling assets would double-correct
  correct data. Consequence on record: an author who hand-compensated for the old render
  must undo that compensation.
- **D-MEL5.1 = A stands**, with MEL-BEATUNIT-1 as a bounded exception. Beat unit and
  bar-alignment are independent axes; bar-time renormalization remains post-MVP.

### Notes
- Precision was checked, not assumed: `MusicalTimeSpan.Multiply(double)` rounds the
  *multiplier* to three decimals and scales numerator/denominator, so the beatSpan swap
  leaves relative precision untouched and halves the absolute tick error in X/8.
- No governed surface moved and no `governs:` entry changed. The test file remains
  unlisted in the manifest, as it was before this batch — flagged here rather than
  changed silently, since the bass SSoT does list its tests.
- Smoke (4/4 byte-identity control + 6/8 cross-track sync) is run by the maintainer in
  Unity; it is not evidenced by this entry.

## 2026-07-24 — MIDIIMP-SSOT-1: MIDI import promoted to its own SSoT (+ governance close-outs)

Documentation and governance only. No code, no contract change, no invariant
weakened.

### Added
- **`authoring/SSoT_Authoring_MIDI_Import.md`** — new primary SSoT for the
  cross-cutting MIDI-import pattern. Owns the shared contract only: pure-function
  importer in `Editor/`, working-copy-only apply, the window's Timing controls as
  meter authority, beat-unit-aware grid conversion, the `[Kind] loc: detail`
  warning shape with no silent fallback, ticks-per-quarter-only, ties-toward-lower
  determinism, measure derivation and the 64 cap. Also records the three documented
  losses (drum durations + kit-agnostic GM, melody absolute register, chord
  inversions/voicings) and what does not exist (export, meter/tempo import, key
  detection, bassline import, full chord recognition).
  - The three domain SSoTs keep their musical semantics and warning taxonomies
    **unchanged** — nothing was moved out of them. This is a shared home added, not
    a relocation, which is why no domain doc needed editing.
  - Promotion trigger is the one M1 wrote down: revisit if import becomes
    cross-domain. M3 made it three adopters. Precedent and shape follow
    `authoring/SSoT_Authoring_LLM_Generation.md` (primary since L3).

### Changed
- `coverage-matrix.md` — new row for MIDI file import (primary =
  the new SSoT), plus the six missing batch notes (M1, PERC-FALLBACK-1, M2, M3,
  IMPORT-QOL-1, MEL-DOCDRIFT-1) and this batch's **primary-home flip** note. The
  matrix had been silent since 2026-07-17.
- `SSoT_INDEX.md` — the new SSoT added to the primary authoring spine. While there:
  `authoring/SSoT_Authoring_LLM_Generation.md` had been missing from that list since
  it became primary in 2026-05-28; added with a dated note.
- `ssot_manifest.yaml` — new entry with six cross-cutting invariants; the three
  importer files dual-listed (domain SSoT + import SSoT), mirroring the
  `ChordProgressionRuntimeImporter.cs` / `PaletteSelection.cs` precedent.

### Resolved
- **`MIDIPercussionInstrumentSO` ownership** (open since PERC-FALLBACK-1 §7.5):
  **package-owned**. No separate SSoT — homed under
  `runtime/SSoT_Composer_Rhythm_Track.md`, which already owns the read-only
  consumption contract in §3E, and added to that entry's `governs:`. Its path in the
  manifest is **inferred and flagged inline**; verify against the package tree.
- **`planning/active/Roadmap_Melody_Authoring_MVP.md` archived.** MVP complete
  (Phases 1–5) and D1 superseded, so it moved to `planning/archive/` and was
  de-registered from `roadmaps:`. Consequence recorded in its header: D2–D4 are
  carried as records only, and resuming melody authoring opens a **new** roadmap
  rather than reopening this one.
- Residual D1 staleness in that roadmap's "Immediate next steps" (a line §8.4 did
  not reach) corrected before archiving.

### Recorded, not fixed — F-1 / MEL-BEATUNIT-1
`runtime/SSoT_Composer_Melody_Track.md` §7 "Meter & looping (D-MEL4.3)" gains a
characterization of the beat-unit desync: both melody paths place notes with
`MusicalTimeSpan.Quarter` regardless of meter, so a 6/8 Part renders melody at half
speed against the other tracks. Bounded by two properties that must be read
together — the error is a **uniform scaling** (contour and internal rhythm survive;
cross-track sync does not), and it is **not** an import defect (a hand-authored
pattern hits it identically, so it predates M2 and lives in the Phase 4 render
path). Operational note added: smoke melody with a **4/4** file, since `beatUnit = 4`
gives factor 1. The fix is its own pending batch, **MEL-BEATUNIT-1**.

### Not changed
- The three domain authoring SSoTs, all composers, all importers, all tests. No
  runtime or editor code.
- `SSoT_CONTRACTS.md` — no cross-boundary contract moved.

## 2026-07-24 — MEL-DOCDRIFT-1: melody-phase drift in the tools SSoT

Documentation-only. No code, no contract, no invariant change.

### Fixed
- `authoring/SSoT_Authoring_Tools.md` §3.A `MelodyPatternEditorWindow` — the
  "Status / scope (Phase 2)" block claimed Phase 3 (generation params +
  generator) and Phase 4 (runtime `ComposeFromPattern`) were **not yet
  implemented**. Both closed 2026-06-17 and are code-backed
  (`DrawGenerationParams` + `SimplifiedMelodyGenerator.Generate`;
  `MelodyTrackComposer.ComposeFromPattern` + `ResolvePatternNotesCore`,
  verified against the package tree). Block renamed and both bullets rewritten
  to point at the live contracts (`runtime/SSoT_Composer_Melody_Track.md` §7;
  melody authoring SSoT §5).
- Same section, Capabilities — the Phase-3 generation-parameters section and
  the "Generate" button were missing from the capability list entirely, an
  omission produced by the same staleness. Added with its contract pointer.
- Same section, Current limitations — "drag-to-resize notes is deferred to
  Phase 5 polish" was stale in a second way: Phase 5 closed 2026-06-22 under
  "Closure scope = A" (editor polish treated as satisfied by the Phase 2–3
  closures), so drag-to-resize is a standing limitation, not pending work.
  Reworded; the limitation itself is unchanged.
- `authoring/SSoT_Authoring_Tools.md` §3.D — retitled from "remaining planned
  phases" to "phase history" and rewritten as a completed record (Phases 2–5
  with closure dates and contract pointers), since it described Phases 3 and 4
  as unimplemented. The asset-reset caveat is retained verbatim. Deferred
  phases D2–D4 are noted as still recorded in the melody MVP roadmap; D1 is
  noted as superseded by Batch M2.

### Provenance
Drift surfaced (not fixed) while anchoring Batch M2 — see the parked note at the
top of `M2_Doc_Diffs.md`. M2 corrected only the one line it made outright false
(§8.2c, the "deferred to Phase D1" clause); the rest was deliberately deferred to
this batch rather than silently patched.

### Not changed
- `authoring/SSoT_Authoring_Melody_Composition.md` §7/§8 — already correct; they
  were the evidence the tools SSoT was the drifted document, not the melody one.
- `planning/active/Roadmap_Melody_Authoring_MVP.md` — Phase 3/4/5 statuses were
  already accurate (DONE with dates); D1 already marked superseded at §8.4.
- `ssot_manifest.yaml` governs / invariants — no governed surface moved. Header
  changelog note only.
- No runtime or editor code.

## 2026-07-24 — IMPORT-QOL-1: editor quality of life (chord import + smoke)

### Added
- `ChordMidiImporter.SuggestSubdivisions` (+ `SubdivisionCandidate` /
  `SubdivisionSuggestion`, `SuggestMaxErrorBeats = 0.03125` beats,
  `SuggestCandidates = {1,2,3,4,6,8}`): pure read-only probe of how well each
  candidate grid explains a file's note onsets and ends, sharing Import's time
  math and channel filter. Surfaced as a "Suggest…" button beside the panel's
  Grid Subdivisions slider — explicit press sets the slider to the smallest
  passing candidate and always reports the full residual table; no pass → the
  argmin is reported and the slider is untouched (D-QOL1-1..3).
- `ChordMidiImporter.Options.preserveReStrikes` (+ "Preserve Re-strikes"
  panel toggle, OFF by default = M3 behavior): restricts identity coalescing
  to contiguous regions, keeping gapped re-strikes of the same chord as
  separate events. Bounded amendment to M3-D5/M3-D6 (D-QOL1-4=A); with the
  default-false polarity, `default(Options)` and all 25 pre-existing tests
  keep the M3 semantics untouched.
- MIDI-import provenance: the grid-apply paths stamp `[MIDI: <file>]` into
  the asset's `originalInput` (suffix stripped on load into the Roman field;
  rebuilt per apply; cleared by target rebind or a Roman-path apply). The
  `DisplayName` growth is an accepted, documented cost (D-QOL1-5=B; corrects
  the batch premise — the grid path never left `originalInput` empty).
- `CompositionSmokeWindow`: pattern-measures advisory — a HelpBox listing
  patterns that declare MORE measures than the window (TS mismatch noted per
  line) plus an explicit "Fit to longest pattern" button that touches ONLY
  `partContext.measures`. Shorter patterns repeat silently (legitimate);
  nothing changes automatically (D-QOL1-7=A).
- `CompositionSmokeWindow`: MGP Config auto-assign on open when the field is
  empty — last manual selection restored by GUID from `EditorPrefs`, else
  `FindAssets("t:MidiGenPlayConfig")` only when exactly one exists (never
  guess among several).
- 8 new EditMode tests in `ChordMidiImporterTests` (25 → 33): both coalescing
  modes (gap merge / gap preserve / contiguous-always-merge, with per-strike
  velocities) and the suggestion (parsimony pick, triplet pick, no-pass
  argmin, channel-filter parity, empty file).

### Fixed
- `BuildRomanStringFromGrid` emitted durations with two decimals ("0.##"),
  truncating anything finer than a quarter measure (25/32 → "0.78"). The
  derived Roman string then failed `RhythmGridQuantizer`'s round-trip — every
  candidate subdivision landed ~0.04 steps off the 0.001 tolerance — so the
  Roman preview raised "Could not find a valid 'subdivisions' value for these
  durations" and the string persisted to `originalInput` was not re-parseable.
  Now six decimals (`DurationFormat`). Pre-existing since Grid mode; reachable
  in practice from M3 onward (imports fill the grid with sub-beat events) and
  hit on every rebind once IMPORT-QOL-1 made `originalInput` load through the
  preview. The ASSET was never wrong — grid events, measures and subdivisions
  are written from the grid directly; only the derived metadata string and its
  preview were affected (D-QOL1-8).

### Changed
- `ChordProgressionEditorWindow`: "Allowed Tonalities" foldout and the LLM
  panel foldout now default COLLAPSED; `showAllowedTonalities` is newly
  serialized so its state persists across domain reloads like the other
  foldouts (D-QOL1-6=B; already-open windows keep their serialized state).

### Decisions locked (D-QOL1-1..7)
- **D-QOL1-1** — suggestion residual measured as MAX error in grid beats over
  all onsets and ends; `SuggestMaxErrorBeats = 0.03125` (1/32 beat), chosen so
  even sub=8 (worst case 1/16 beat) is falsifiable; public, tunable constant.
- **D-QOL1-2** — the button sets the slider only on a passing suggestion and
  always reports the full residual table; no pass → report-only.
- **D-QOL1-3** — Suggest opens its own file panel (same pattern as Import and
  Analyze); path caching is out of scope.
- **D-QOL1-4=A** — flag polarity `preserveReStrikes`, default false = M3;
  registered as a bounded amendment to M3-D5/D6.
- **D-QOL1-5=B** — provenance as an `originalInput` suffix with strip-on-load;
  same rule in Apply-to-target and Save-as-new (one behavior, not two).
- **D-QOL1-6=B** — serialize `showAllowedTonalities`, default false.
- **D-QOL1-7=A** — smoke advisory only when a pattern EXCEEDS the window's
  measures; repetition of shorter patterns stays silent.
- **D-QOL1-8** — grid-derived Roman durations use six decimals so the string
  round-trips through the quantizer; scope addition, accepted in-batch because
  item 6 routes `originalInput` through the preview on every asset rebind.

### Not changed
- `ChordProgressionData` (no inversion field — still the documented M3
  limitation, future expressivity batch), all runtime composers, importers'
  M1/M2 behavior, `ssot_manifest.yaml` invariants, determinism and
  asset-write invariants. `CompositionSmokeWindow` remains intentionally
  ungoverned (D-SMOKE-DOC-1=A re-confirmed).

## 2026-07-23 — M3: MIDI file import (chord progressions)

### Added
- `ChordMidiImporter` (`Editor/`): pure-function MIDI → `ChordProgressionData.ChordEvent`
  list. Quantize-then-segment on sounding pitch-class sets (M3-D1); restricted
  deterministic matching cascade against the v1 quality templates via
  `GetIntervalsForQuality`, bass-root first, warned reduction, warned skip
  (M3-D5); degree + accidental relative to the user key, flat-preferred (M3-D2);
  fixed 3-pc chord threshold + channel filter (M3-D3); identical harmonic
  regions coalesce; velocity = rounded mean (M3-D6). 14 warning kinds, M1 shape.
- `ChordMidiImporterTests`: 25 EditMode tests over in-memory DryWetMidi files,
  including a template-uniqueness invariant guarding the exact-match cascade.
- `ChordProgressionEditorWindow_MidiImport.cs`: "MIDI File Import" panel partial
  (LLM-panel pattern) — key root/tonality/channel fields, a Grid Subdivisions
  slider (the same window field as Grid mode's Timing controls — surfaced here
  because it IS the import resolution: minimum chord duration = one step),
  warnings list, Roman summary readout; applies Full results to the GRID working
  state and switches the window to Grid mode. One-line OnGUI hook in
  `ChordProgressionEditorWindow.cs`.
- `ChordMidiImporter.DescribeChordTimeline` + panel "Analyze File (log)" button:
  read-only diagnostic emitting a paste-ready per-segment timeline (location,
  duration, pitch-class set, bass, exact pitches with octaves, importer verdict)
  to Console + clipboard. Shares the `MatchSegment` cascade with `Import` by
  construction, so the diagnostic cannot drift from the import decision. Not
  part of the import contract.

### Changed
- `ChordProgressionEditorWindow.cs`: OnGUI calls `DrawMidiImportPanel()` after
  `DrawLLMPanel()` (hook only; no behavior change elsewhere).

### Decisions locked (M3-D1..D6, `planning/active/Roadmap_MIDI_Import.md`)
- See the roadmap's Phase M3 "Locked decisions" block.

### Docs
- `SSoT_Authoring_Chord_Progressions.md`: §2 assisted-paths note, §3 MIDI import
  subsection, §7 trigger bullet. `SSoT_Authoring_Tools.md`: window capability
  bullet. Roadmap M3 CLOSED; roadmap COMPLETE → candidate for archive.

### Not changed
- `ChordProgressionData`, `RomanProgressionParser`, `ChordQualityResolver`,
  `ChordTrackComposer`, runtime importers/consumers: untouched. M3 is
  authoring-only; determinism and asset-write invariants unchanged.

## 2026-07-23 — M2: MIDI file import (melody)

### Added
- `Editor/MelodyMidiImporter.cs` — pure-function importer turning a standard MIDI
  file into the canonical `MelodyPatternData` note shape. User-specified key
  (root `NoteName` + `Tonality`, D-MIDI1=A / M2-D1=A) drives pitch → (degree,
  absolute scale octave) through `GetScaleFromTonality`; chromatic notes snap to
  the nearest degree, ties downward (D-MIDI2=A / M2-D6=A), with no data-model
  change. Reference octave auto-centered to the modal scale octave, ties lower
  (M2-D2=A), and reported. Beat-unit-aware quantization
  (`gridBeats = quarterNotes × beatUnit / 4`); timing written in absolute beats;
  duration preserved, quantized, one-step floor (M2-D5=A); content-derived
  measures cover the last note's **end** (cap 64), explicit measures drop late
  starts and clip overhangs. Monophonization: highest pitch wins on simultaneity,
  overlaps truncate at the next onset (M2-D4=A). Channel filter with a merge
  warning (M2-D3=A). Hard fails: SMPTE time division, no notes after the filter,
  no notes in range. Eleven warning kinds, no silent fallback.
- `Tests/Editor/MelodyMidiImporterTests.cs` — 20 EditMode tests over in-memory
  DryWetMidi files: diatonic mapping (degree/beat/duration/velocity), non-C root
  in Aeolian, chromatic snap ×2, reference octave (modal tie → lower; majority),
  simultaneity, overlap truncation, off-grid onset + on-grid no-warn, duration
  floor, end-based measure derivation, explicit-measure drop + clip, channel
  filter ×3, 6/8 beat-unit conversion, empty file, null file, and a determinism
  guard over warnings and notes. All green; additionally verified against a real
  `.mid` in-editor, including a render pass through `ComposeFromPattern`.

### Changed
- `Editor/MelodyPatternEditorWindow.cs` — new "MIDI File Import" foldout panel
  (Key Root + Tonality popups, 1-based MIDI channel field where 0 = all, an
  "Import MIDI File…" button, and a per-import warning list closing with the
  reference-octave / offset-span readout) plus `OnImportMidiFile` /
  `ApplyMidiImport`. Straight note-list replacement into the working copy — melody
  has no lanes and no Text mode, so M1's Grid-vs-Text apply dilemma does not
  arise. Asset writes still happen only through Apply / Save As.

### Decisions locked (M2-D1..D6, `planning/active/Roadmap_MIDI_Import.md`)
- M2-D1=A `NoteName` root matching the runtime seam · M2-D2=A auto-centered
  reference octave (modal, tie lower), reported not selectable · M2-D3=A channel
  filter + merge warning, track filtering deferred · M2-D4=A highest-pitch wins /
  overlap truncated · M2-D5=A quantized duration with one-step floor · M2-D6=A
  chromatic ties snap downward.

### Docs
- `authoring/SSoT_Authoring_Melody_Composition.md` — new §5 "MIDI file import
  (Batch M2)" (grid semantics, pitch→degree, reference octave, monophonization,
  duration, warning taxonomy); §8 trigger + closing-paragraph note.
- `authoring/SSoT_Authoring_Tools.md` — melody editor capability + limitations;
  the stale "deferred to Phase D1" clause corrected.
- `planning/active/Roadmap_MIDI_Import.md` — M2 CLOSED + M2-D1..D6 recorded.
- `planning/active/Roadmap_Melody_Authoring_MVP.md` — Phase D1 marked superseded.

### Not changed
- No runtime code. No change to `MelodyPatternData`, `MelodyTrackComposer`, the
  simplified generator, or any other composer.

## 2026-07-22 — PERC-FALLBACK-1: percussion note fallback resolver

Closes the M1 follow-up (render-time lane drop when the kit lacks the exact
`GeneralMidiPercussion` a lane requests). New Runtime
`PercussionFallbackTable` (static, fixed-order same-family substitutes per GM
member; D-PF4=A tom ordering) + `PercussionNoteResolver` (pure: exact →
first mapped family substitute → None default / GM-standard opt-in via
`AsSevenBitNumber()`, D-PF2). `RhythmTrackComposer` routes all six former
`TryGetMappedNote` call sites (procedural ×4, grid ×1, legacy ×1 — D-PF7=A)
through one seam, `TryResolveForCompose`, which owns the D-PF3 log
discipline (Exact silent; Substituted/GmStandard informational, gated by
`logGenerator`, D-PF5=B; None hard actionable warning). Kit SO untouched
(D-PF1=B, read-only). `allowGmStandard` wired false everywhere (D-PF6=B).
Deterministic by construction (no RNG, no dictionary order). New tests:
`PercussionNoteResolverTests` (exact; the four M1 substitution cases
BassDrum1→AcousticBassDrum, LowFloorTom→LowTom, PedalHiHat→ClosedHiHat,
HiMidTom→HighTom; table-order priority; None; GmStandard; null kit;
100× determinism guard). The M1 real-MIDI import case (Brush Kit) now
renders all lanes.

## 2026-07-20 — MGP-MIX-1: consumer-side mix gain (package 1.2.0)

Closes the seam D-BAG-3=A opened. `GenerateSinglePart` gains an optional
`mixGains : IReadOnlyDictionary<MusicianTrackKey, float>` (D-MIX-2=A). Entry ⇒
one CC7 on that track's channel, `clamp(round(volume01 × gain × 100), 0, 127)`
(D-MIX-1=A, D-MIX-3: multiplicative, per-entry emission gate, identity = GM
default 100). No entry ⇒ zero new events ⇒ bit-identical to the pre-MIX-1
render. Rhythm warn+ignore in v1 (shared ch9, D-MIX-4=A). Readback:
`PartRender.appliedCc7ByTrack` (D-MIX-5=A). Deterministic by construction (no
RNG/seed involvement). `MidiGenerator.ApplyChannelVolume` gains its first
package-side call site (was call-site-dead since 1.0.0; kept public,
unchanged). New tests: `SongOrchestrator_MixGainTests` (8). Handoff to ALWTTT
filed under `reference/cross-project/ALWTTT/Handoff_MGP_MIX_1.md`.
volume01 authoring of the 70 instruments: deliberately deferred to a later
version, post ALWTTT D-CSV-18 verdicts (D-MIX-6). GenerateSong: unchanged.
Interface note: trailing optional param is source-compatible for callers,
breaking for `ISongOrchestrator` test doubles.

### Version note
`package.json` goes **1.0.0 -> 1.2.0** in a single jump. The 1.1.0 bump that
MGP-BAGGAGE-1 planned was never materialized in `package.json`; 1.1.0 was never
published. Both batches therefore ship in **1.2.0**, and 1.1.0 is a version that
does not exist. Consumers pin 1.2.0. Any earlier statement pointing ALWTTT at
1.1.0 is superseded by this entry.

## 2026-07-20 — MGP-BAGGAGE-1: shipped-catalogue cleanup (ships in package 1.2.0)

Documentation/maintenance batch opened by an ALWTTT request (measured inventory
export, 218 assets with derived health flags). Disposition-only: no runtime
semantics, no code changes, no new contracts. Planned as 1.1.0; that bump was
never materialized, so this batch ships in 1.2.0 alongside MGP-MIX-1 (see the
version note above).

### Removed
- `Runtime/Resources/ScriptableObjects/Patterns/Chords/ChordProgression-Default{TwoFour,
  ThreeFour,FourFour,FiveFour,SixEight,SevenEight,NineEight,TwelveEight}.asset` (8) —
  six empty, two with `Measures=0` (unrenderable). Never authored: all eight
  serialized `TimeSignature=FourFour`, which is the enum's zero value, not an
  authored choice.
- `Runtime/Resources/ScriptableObjects/Patterns/Drums/DrumPattern-Default{…same 8…}.asset`
  (8) — seven with no lanes, one all-silent. Same TS=0 signature.
- `Runtime/Resources/ScriptableObjects/Patterns/Melodies/{BasicMelodyPattern 2..7,
  FourFourMelody1..3,ThreeFourMelody1..2,OrangePeelBass}.asset` (12) — all empty,
  single duplicate group.
- `Patterns/Chords/Palettes/Test Palette.asset` and
  `Patterns/Drums/Palettes/DrumPatternPalette.asset` (displayName "TestPalette") —
  test fixtures carrying production-looking type names.
- `ScriptableObjects/Melodic Style - Test 1.asset`, `Chord Progressions/Test
  Progression.asset` — same category, found package-side during the audit.

Thirty-two assets in total (8 + 8 + 12 + 2 + 2).

Rationale (D-BAG-1=A): none of the thirty-two is a runtime fallback, an editor
template or a test fixture. No governed SSoT declares them; no composer resolves a
"default per time signature" by name. Composers take patterns from explicit
references (`renderOverride ?? cardPattern ?? TrackParameters.Pattern`), never from
the repository. But `PatternRepositoryResources` publishes everything under
`Patterns/{Chords,Drums,Melodies}` through `GetAll*()` / `Get*(TimeSignature)`, so
`GetChordProgressions(FourFour)` could return an unplayable asset to any
consumer-side selector. Selection risk, not just baggage.

### Moved
- `Runtime/Resources/ScriptableObjects/Chord Progressions/` (the authored
  progressions + `_ChordProgressionLibrary.asset` + `Palettes/`) →
  `Samples/ExampleCatalogue/ChordProgressions/` (D-BAG-2=A). The now-empty
  `Runtime/Resources/ScriptableObjects/Chord Progressions/` root is deleted
  outright (D-BAG-2 follow-through): it is not a canonical enumeration root and
  an empty folder inside `Resources/` only invites refilling.

  This folder was a **second, older catalogue root**: not scanned by
  `PatternRepositoryResources` (which reads `Patterns/Chords`), and not scanned by
  `ChordProgressionCatalogueWizard` (which reads `Assets/Resources/...` — consumer
  side). Orphaned from both runtime and tooling. Moving it out of `Resources/`
  removes it from `Resources.LoadAll` while keeping the only authored example
  progressions the package ships. `package.json` declares no `samples` key, so the
  folder ships as ordinary package content. `MidiGenPlayConfig.progressionLibrary`
  is a by-reference field and is unaffected by the path change.

### Unchanged (deliberate)
- `Patterns/Chords/Palettes/` and `Patterns/Drums/Palettes/` are kept as EMPTY
  folders: they are the canonical enumeration roots pinned by MGP-ALWTTT-DBG-2.
- `_Chord Progressions List.asset`, `_Drum Patterns List.asset`,
  `_Melody Patterns List.asset` kept and emptied (D-BAG-4=A); the container types
  stay. Emptied contents verified 2026-07-21: no entries left pointing at retired
  assets.
- `volume01` stays a package-side authoring field (nominal per-instrument level).
  Currently 1.0 on all 70 melodic instruments — unauthored, not deliberately flat.
  Consumer-side mix balance is a separate seam, opened and closed as MGP-MIX-1
  (D-BAG-3=A).

### Corrected measurement
- The ALWTTT export flagged `Poly Synth` and `Warm Pad` as sharing soundfont/bank/
  patch. False positive: `Warm Pad` is patch 89, `Poly Synth` is patch 90, with
  `PatchName` and `PatchIndex` agreeing on both assets. `MIDIInstrumentSO.PatchIndex`
  is 0-based GM (89 = Pad 2 warm, 90 = Pad 3 polysynth). Both correctly authored; no
  package-side action.

### Version
- Planned as `package.json` 1.0.0 → 1.1.0. Minor bump, not a patch: removing content
  from `Resources/` breaks GUID references in downstream projects. Verified that no
  live ALWTTT content referenced any of the retired assets before removal. That bump
  was never materialized; the content ships in 1.2.0 (see the MGP-MIX-1 version
  note above).

## 2026-07-19 — M1: MIDI file import (drums)

### Added
- `Editor/DrumMidiImporter.cs` — pure-function importer turning a standard MIDI
  file into the canonical `DrumPatternData` grid shape. Note number →
  `GeneralMidiPercussion` via a reverse map built from DryWetMidi's own GM tables
  (never a hardcoded offset); beat-unit-aware quantization
  (`gridBeats = quarterNotes × beatUnit / 4`, so X/8 meters grid on eighths);
  content-derived measures (cap 64) or explicit measures with drop-and-warn;
  lane `defaultVelocity` = modal velocity (ties → lower, deterministic) with
  default-velocity steps written as the `velocity == 0` sentinel. Hard fails:
  SMPTE time division, no notes after the channel filter, no GM-mapped note.
  Warns (never silently): off-grid snap > 0.25 step (first 8 detailed, rest
  aggregated), same-lane/same-step collision (higher velocity kept), unmapped
  note number, notes beyond range, measure cap. Note durations are discarded
  (the drum grid is trigger-based).
- `Tests/Editor/DrumMidiImporterTests.cs` — 11 EditMode tests over in-memory
  DryWetMidi files: happy path (sentinel + explicit velocities, lane ordering),
  channel filter ×3 (exclude melodic, all-channels, no-drum-notes fail),
  off-grid snap + on-grid no-warn, collision, explicit-measure truncation,
  derived measures, 6/8 beat-unit conversion, empty file, null file. All green;
  additionally verified against a real GM drum `.mid` in-editor (100% OK).

### Changed
- `Editor/DrumPatternEditorWindow.cs` — new "MIDI File Import" foldout panel
  (drum-channel-only toggle, "Import MIDI File…" button, per-import warning
  list) plus `OnImportMidiFile` / `ApplyMidiImport`. Applies in **Grid** mode
  and clears the text buffer, deliberately: imported velocities are arbitrary
  1–127 values the three-tier glyph view would snap. Asset writes still happen
  only through Apply / Save As.

### Decisions locked (D-MIDI1..5, `planning/active/Roadmap_MIDI_Import.md`)
- D-MIDI1=A user-specified key for melody/chord import (no auto-detection) ·
  D-MIDI2=A chromatic notes snap to nearest degree + warn (no data-model change) ·
  D-MIDI3=A restricted v1 chord detection · D-MIDI4=A **bassline import out of
  scope** (no bassline pattern asset exists; `BassTrackComposer` ignores pattern
  overrides in v1) · D-MIDI5=A panel inside existing editor windows, no dedicated
  window.

### Follow-up spun out (not in this batch)
- **PERC-FALLBACK-1** — render-time robustness: `RhythmTrackComposer` drops a lane
  when the kit lacks the exact `GeneralMidiPercussion` an imported/authored pattern
  requests. A package-side `PercussionNoteResolver` will substitute within the GM
  family before dropping. Governed by `runtime/SSoT_Composer_Rhythm_Track.md`.

### Docs
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` — new §3A "MIDI file import
  (Batch M1)" (grid semantics, note→lane, velocity compression, Grid-mode apply
  rationale, warning taxonomy); §4 already-true + not-yet-true bullets; §9 trigger.
- `authoring/SSoT_Authoring_Tools.md` — drum editor capability + limitations; §10 trigger.
- New `planning/active/Roadmap_MIDI_Import.md` (M1 CLOSED, M2/M3 planned);
  registered in `ssot_manifest.yaml`.

### Not changed
- No runtime code. No change to `DrumPatternData`, the text parser, or any composer.

## 2026-07-17 — MGP-ALWTTT-DBG-4+2: composition-debug package half, completion

### Added
- `Runtime/CoreScripts/Composition/ChordProgressionRuntimeImporter.cs` — the
  Ask D runtime parser/builder: setup-card + fenced-Roman grammar (RELOCATED
  verbatim from the editor importer; pure regex, no editor API), the D-L4.5
  quality-suffix allowlist + `TryFindForbiddenToken` (relocated from the
  response handler; now the single canonical copy), and
  `TryParsePayload` / `TryParseRoman(roman, ts, measures,
  defaultDurationMeasures, referenceTonality, out data, out warnings)` —
  `RomanProgressionParser` → `RhythmGridQuantizer` → `ChordQualityResolver`,
  producing a never-persisted `ChordProgressionData` (`HideFlags.DontSave`;
  `name` = `"Runtime: <roman>"` for by-name readback, D-DBG3=A). Hard fails:
  out-of-alphabet suffix (no silent downgrade), quantization failure,
  payload-without-setup-card (`TryParsePayload` points at `TryParseRoman`).
  Non-fatal warn: declared-vs-derived measures mismatch (durations win).
- `Tests/Editor/ChordProgressionRuntimeImporterTests.cs` — 11 EditMode tests:
  payload happy path, ProgressionOnly hard-fail, bare-Roman diatonic
  inference, rests, measures mismatch, guard ×2 (V13 / Iadd11), unquantizable
  durations, never-persisted (`DontSave` + `!AssetDatabase.Contains` + name
  stamp), editor↔runtime payload parity, handler↔runtime guard parity.

### Changed
- `Editor/ChordProgressionEditorImporter.cs` — rewritten as a thin forwarder
  (E-5=A): public surface preserved (Result/ImportMode/warnings, `Parse`,
  internal `ExtractProgression`); all logic delegates to the runtime type. One
  grammar, no drift; existing callers/tests untouched.
- `Editor/ChordProgressionLLMResponseHandler.cs` — the private allowlist /
  token-split regex / scan removed; internal `TryFindForbiddenToken` now
  forwards to the runtime importer (same signature; V2 tests untouched).

### Contract (Ask B gaps, MGP-ALWTTT-DBG-2 — documented, zero new code)
- Runtime catalog enumeration: the three pattern domains stay on
  `IPatternRepository` / `PatternRepositoryResources`; palettes and phrase
  vocabulary enumerate via `TrackPatternConfigStoreResources<T>` over
  canonical Resources folders — Drums palettes `Patterns/Drums/Palettes` (no
  migration), Chord palettes `Patterns/Chords/Palettes` (migrate legacy
  `Chord Progressions/Palettes/Test Palette.asset`), Phrase vocabulary
  `Patterns/Phrases` (migrate the legacy `ScriptableObjects/Phrases/` folder;
  `PhraseArchetypeSO` is abstract — concrete archetypes load under it).
  Display metadata per domain documented in the respective SSoTs.
  `IPatternRepository` NOT extended (E-2=A / E-3=A).

### Decisions locked
- E-1=A (canonical chord-palette folder confirmed against the package tree) ·
  E-1b=A (declare `Patterns/Chords/Palettes`, migrate the one legacy asset) ·
  E-2=A + E-2b=A (store over `Patterns/Phrases`, migrate the folder) · E-3=A
  (documented contract, no new API) · E-4 surface confirmed (warnings as
  strings; guard inside the importer, pre-parse; `DontSave` in code;
  quantization hard-fail) · E-5=A (editor symbol kept as forwarder).
- Grammar pin: bare `7` suffix = literal `Dominant7` regardless of Roman case
  (`ii7` = Supertonic + Dominant7); minor sevenths require `m7`.

### Notes
- No regression surface: no composer, RNG, or asset touched; runtime gains no
  editor dependency (the relocated code was pure regex). Determinism intact.
- With this batch the composition-debug arc's PACKAGE half is complete
  (DBG-1+3 + DBG-4+2). The consumer half (Ask A/B/C/D wiring + the
  MusicianTrackKey migration, `TODO(BASS-1)`) is a single ALWTTT session,
  driven by the DBG-4+2 handoff document.

## 2026-07-17 — MGP-ALWTTT-DBG-1+3: composition-debug return contract (package half)

### Added
- `Runtime/CoreScripts/Composition/CompositionReadback.cs` — `MusicianTrackKey`
  (composite `(musicianId, TrackRole)` key), `ResolvedTrackChoice` (Ask A
  readback payload), `ResolvedSource` enum, `PatternPickInfo` (pick source
  out-info). Pure data; no runtime semantics.
- `PartRender.resolvedByTrack` (per-track Ask A readback).
- `GenContext.ReportResolved` (readback sink) + `GenContext.patternOverride`
  (per-render override channel), both swap/restored by `GenerateOne` like
  `ctx.rng`/`ctx.trackSeed`.
- `GenerateSinglePart` trailing `patternOverrides` parameter.
- Info-capturing overloads `PickPatternOverride(..., out PatternPickInfo, ...)`
  and `PickProgressionOverride(..., out PatternPickInfo, ...)` (old overloads
  delegate; draws identical).
- `PhrasePlanner.LastPlannedArchetypeName` (observability, no draws).
- Tests: `SongOrchestratorKeyingTests.cs`, `PatternOverrideAndReadbackTests.cs`,
  `ChordMarkerParityTests.cs`.

### Changed
- **BREAKING (D-DBG1=A):** `PartRender.stemsByMusician` / `melInstByMusician` /
  `percInstByMusician` and `GenerateSinglePart(instrumentOverrides)` re-keyed
  `string` → `MusicianTrackKey`. Consumers must key on `(musicianId, TrackRole)`.
- Track tag `mus:{id}` → `mus:{id}:{role}` (ID-1=A; internal format,
  `FormatMusicianTag`/`TryParseMusicianTag`).
- `RenderFromProgression` is now `public` (marker-parity test seam; `internal` +
  InternalsVisibleTo also suffices — repo shipped `public`) and applies
  the grid-site accidental handling (guarded; accidental-free output bit-identical).
- `chd:` marker promoted from debug output to a governed contract (§2.1 of the
  Backing SSoT).

### Notes
- Decisions: **D-DBG1..4 = A**; **ID-1=A** (tag carries role), **ID-2=A** (Harmony
  out of v1), **ID-3** (roller's existing `History` reused — no new roller state),
  **ID-4** (`PatternDataSO` base confirmed; Bassline override = warn+ignore).
- BC gate: no override + no seed ⇒ MIDI-byte bit-identical (FNV goldens);
  `ctx.rng` draw order unchanged.
- Consumer half (`MidiMusicManager` boundary flatten to `musicianId`,
  `TODO(BASS-1)`) is ALWTTT-side. Full consumer migration to the composite key is
  a separate ALWTTT batch.
- Open: Rhythm render-level override test (perc-kit fixture).

## 2026-07-16 — CA-T2: Tier-2 voicing-reshaping figures (power chord, chugging)

### Added
- `Runtime/CoreScripts/Composition/Interfaces/IChordReshaper.cs` — new
  pre-articulation reshaping seam (D-T2-SEAM=B). Deterministic, RNG-free;
  identity on all non-Tier-2 expressions; never null / never empty on a
  non-empty voicing.
- `Runtime/CoreScripts/Composition/Articulation/ChordReshaper.cs` — default
  reshaper. `PowerChord`/`Chugging` reduce the voicing to root + perfect fifth
  (+ octave), anchored at the root pitch at or below the voicing's bass; every
  other expression returns the input list unchanged.
- `ChordExpressionType.PowerChord = 7`, `ChordExpressionType.Chugging = 8`
  (append-only; values 0..6 serialized and unchanged; never renumber)
  (`Composition/Data/ChordExpressionType.cs`).
- `ChordArticulator.ChordPulsePlan` — pitch-preserving full-chord pulse at the
  arpeggio interval (`NoteIndex = -1` every hit), selected by `Chugging`; events
  shorter than one hit degrade to `Block`.

### Modified
- `Runtime/CoreScripts/Composition/Composers/ChordTrackComposer.cs` — a static
  `IChordReshaper _reshaper` field; at BOTH emission sites the voiced list is
  reshaped (`_reshaper.Reshape(playable, chordPcs, effectiveExpression)`) into a
  local just before the SAME single unconditional `Emit`, which still receives
  the selected expression. `lastVoicing`/the first-chord pitch stash keep the
  full harmonic voicing (reshape does not touch voice-leading continuity).
- `Runtime/CoreScripts/Composition/Articulation/ChordArticulator.cs` —
  `PlanHits` degrades a leaked `PowerChord` (as well as `Random`) to `Block` and
  gains the `Chugging` → `ChordPulsePlan` case. Articulator stays RNG-free and
  never mutates pitch.
- `Runtime/CoreScripts/Composition/Data/ChordExpressionType.cs` — `ArpeggioRate`
  doc notes it is overloaded as the `Chugging` pulse rate (D-T2-RHYTHM=A).
- `Runtime/CoreScripts/Composition/Data/BackingCardConfigSO.cs` — `arpeggioRate`
  tooltip notes the `Chugging` overload.
- `Tests/Editor/ChordTrackComposer_ArticulationTests.cs` — reshape drops the
  third / identity on non-Tier-2 expressions; `ChordPulsePlan` full-chord pulse
  count; `PlanHits(PowerChord) == PlanHits(Block)`.

### Behavior
- Any Tier-1 figure / `Block` / `Random`: reshaper is identity ⇒ output
  byte-identical to CA-T1/ARTIC-1 (the existing Block bit-identity tests remain
  the BC guard).
- `PowerChord`: sustained root+fifth(+octave). `Chugging`: that voicing pulsed at
  `arpeggioRate`. Deterministic; no RNG in the reshape path.

### Authority changes
- `runtime/SSoT_Composer_Backing_Track.md` — new **§8.6** (Tier-2
  voicing-reshaping figures) + new **§7.5** (reshape-vs-pin precedence); §8 intro
  reworded (Tier-2 is a separate seam, not the articulator); §8.5 pool rule
  corrected (Tier-2 not weight-admissible, D-T2-POOL=A′); §9 Update triggers
  extended.
- `ssot_manifest.yaml` — two `governs:` additions (`IChordReshaper.cs`,
  `ChordReshaper.cs`) + the CA-T2 Tier-2 invariant, both under
  `SSoT_Composer_Backing_Track`; **path fix**: `IChordVoicer.cs` corrected from
  `Runtime/CoreScripts/Interfaces/` to `Runtime/CoreScripts/Composition/Interfaces/`
  (was flagged inferred at CQ-A1-OBJ2; confirmed against the package tree).
- `coverage-matrix.md` — articulation row extended to name the Tier-2 reshaper.
- `planning/active/Roadmap_Chord_Articulation.md` — CA-T2 → DONE; bossa split
  spun out as a tracked follow-up.

### Decisions locked
- D-T2-SEAM=B (pre-articulation reshaper seam) · D-T2-SCOPE=A (power chord +
  chugging ship; bossa split deferred) · D-T2-PIN=A (reshape after the pin) ·
  D-T2-POOL=A′ (Tier-2 out of the Random roll, not weight-admissible) ·
  D-T2-RHYTHM=A (`arpeggioRate` overloaded as the chug pulse).

### Not changed
- `IChordArticulator` signature and its pitch-preserving / RNG-free contract
  (the `ChordPulsePlan` addition re-strikes the given voicing, never reshapes);
  `IChordVoicer`; `ITrackComposer`; the `RandomArticulationRoller` (its Tier-1
  pool is unchanged — Tier-2 is excluded by construction); seed policy.

## 2026-07-16 — BPM-DET-1: seeded GenerateSong tempo roll + live ExplicitBpm

### Fixed
- `Runtime/CoreScripts/Composition/SongOrchestrator.cs` — `GenerateSong` rolled
  the per-part tempo through `MusicTheory.GetBPMFromRange`, which picks from a
  fresh **unseeded** `new System.Random()`, so the same seed produced a different
  tempo each render (the last open SEED-1 gap, SMOKE-MT finding C1; VL-DET-1 had
  fixed only the voicer half). Tempo now resolves `bpmOverride ??
  PartConfig.ExplicitBpm ?? RollTempoBpm(ResolveTempoSeed(baseSeed, partIndex), …)`
  — a seeded, reproducible roll. `GenerateSinglePart` gains the same `ExplicitBpm`
  middle term.

### Added
- `SongOrchestrator.ResolveTempoSeed(baseSeed, partIndex)` — FNV-1a over
  `{baseSeed}|p={partIndex}|tempo` (D-BPM2=A dedicated substream; D-BPM2-KEY=A —
  keyed on part-occurrence, no `rep`, so all repeats of a part-occurrence and any
  two `Structure` entries reusing a part index share a tempo).
- `SongOrchestrator.RollTempoBpm(tempoSeed, range, rule)` — seeded pick over
  `GetValidBpms` (empty ⇒ 120, matching the empty-part fallback).
- `MusicTheory.GetValidBpms(range, rule)` — pure, RNG-free valid-BPM set
  (multiples-of-ten within the `TempoRange` band); the seeded roll draws from it.
- `Tests/Editor/SongOrchestratorSeedTests.cs` — `ResolveTempoSeed` string-format
  guard (omits `rep`), `RollTempoBpm` same-seed determinism + on-grid/in-band,
  distinct-seed variance.

### Changed
- `Runtime/CoreScripts/Data/SongConfig.cs` — **no code change**;
  `PartConfig.ExplicitBpm` (already present) flips from written-never-read to a
  live reader (D-BPM1=A).
- `runtime/SSoT_Runtime_Generation_Orchestration.md` — §5.1 derivation-seams list
  gains `ResolveTempoSeed`; new **§5.2 Tempo resolution (BPM-DET-1)**; §8 Update
  triggers extended.
- `ssot_manifest.yaml` — SEED-1 orchestration invariant extended with the tempo
  clause.

### Behavior
- `MusicTheory.GetBPMFromRange` is **left byte-identical** and stays off the
  render path (D-BPM3=B); its remaining callers (`ChordTrackComposer`, which
  reads only `BeatsPerMeasure`) are unaffected by construction.
- Any caller supplying `bpmOverride` (or a part with `ExplicitBpm` set) is
  MIDI-byte bit-identical to before (the golden path). Only the previously
  unseeded roll changes (unseeded → seeded); no pre-seed baseline exists, so
  determinism is asserted rather than a golden faked.

### Decisions locked
- D-BPM1=A (`bpmOverride ?? ExplicitBpm ?? seeded-roll`; `ExplicitBpm` live on
  both entries) · D-BPM2=A (dedicated `ResolveTempoSeed` substream) · D-BPM2-KEY=A
  (keyed on part-occurrence, no `rep`) · D-BPM3=B (seed policy in the
  orchestrator; `GetBPMFromRange` untouched, off the render path).

### Not changed
- `GetBPMFromRange` body/behavior; the `ChordTrackComposer`
  `GetTimeSignatureDetails` call sites; `ISongOrchestrator` /
  `GenerateSong` / `GenerateSinglePart` signatures (the seed surface and
  `bpmOverride` already existed); seed policy stays host-side (no new entropy
  site — the tempo roll is fully seed-derived).

## 2026-07-15 — VL-DET-1: seeded voicer start-register (deterministic random modes)

### Fixed
- `Composition/Strategies/VoiceLeading.cs` — `BasicVoiceLeadingVoicer.TargetOctave`'s
  random start-register modes (`RandomAroundCenter`, `Uniform01AroundCenter`) drew
  from the global, unseeded `UnityEngine.Random`, which `partSeed` never resets.
  That first-chord draw shifted the whole backing line's register, so two renders
  with the same seed (editor smoke window vs runtime runner) diverged and no two
  runs were reproducible — a hidden violation of the SEED-1 "package never
  self-generates per-render entropy" invariant. It now draws from the part's
  deterministic `ctx.rng`.

### Changed
- `Composition/Interfaces/IChordVoicer.cs` — `VoiceChord` gains an optional trailing
  `System.Random rng = null`; when supplied, the random start-register modes use it.
  Null preserves the legacy global-RNG path bit-identically; non-random start modes
  never consume it.
- `Composition/Composers/ChordTrackComposer.cs` — both `VoiceChord(...)` call sites
  pass `ctx?.rng`.
- `runtime/SSoT_Composer_Backing_Track.md` — new §7.4 *"Start-register determinism
  (VL-DET-1)"* + an Update-triggers bullet.
- `ssot_manifest.yaml` — VL-DET-1 invariant added to `SSoT_Composer_Backing_Track`.
  No `governs:` change (all three files already governed under it).

### Notes
- **Determinism:** non-random `StartRegisterMode` and a null `rng` are bit-identical
  to pre-fix; only the two random modes change (non-deterministic → seeded). At most
  one draw per track (`TargetOctave` returns early after the first chord). No test
  golden changes for the default/non-random presets.
- Brings the voicer into SEED-1 compliance. **Does not** resolve BPM-DET-1 (the
  separate unseeded BPM roll in `GenerateSong`) — that stays open (SMOKE-MT C1).
- Surfaced by the SMOKE-MT parity work; the config under test used
  `Uniform01AroundCenter`, which is why the backing track diverged.

## 2026-07-15 — MEL-NULL-1: phrase-planner null contract + missing-palette early-out

### Fixed
- `Composition/PhrasePlanner.cs` — `PlanPhraseSlotsForSpan` returns an **empty
  slot list** instead of `null` on its bail path, and coerces a null archetype
  `Build()` to empty. It previously returned null, which `MelodyTrackComposer`'s
  slot loop dereferenced — an NRE that aborted the **entire song render**.
- `Composition/Composers/MelodyTrackComposer.cs` — `ComposeMelodyFromProgression`
  checks the palette precondition before planning; on failure logs one error and
  returns an **empty melody track** so every other role still renders.

### Added
- `PhrasePlanner.HasUsablePalette(MelodicLeadingConfig)` — the single definition of
  a *usable* palette (leading present + `PhrasePaletteSO` present + ≥1 archetype;
  Unity `==`). The planner's bail was always broader than "no palette asset" (it
  also fires on zero archetypes), so two independent guards would have left the NRE
  reachable.

### Changed
- `runtime/SSoT_Composer_Melody_Track.md` — new §4 subsection + §8 trigger.
- `ssot_manifest.yaml` — MEL-NULL-1 invariant added. No `governs:` change.

### Notes
- Decision **MEL-NULL-1 = A + C**. Determinism unaffected; every usable-palette
  configuration is bit-identical to pre-fix. No golden changes. Distinct from the
  CA-arc melody log hotfix. Surfaced by the SMOKE-MT harness.

## 2026-07-15 — SMOKE-MT: multi-track composition smoke (editor + runtime)

### Added
- `Runtime/CoreScripts/Composition/Smoke/SmokeTrackSpec.cs` — `SmokePartContext`
  and `SmokeTrackSpec`. Plain serializable types; no editor dependency.
- `Runtime/CoreScripts/Composition/Smoke/SmokeSongConfigAssembler.cs` — static,
  editor-free `Assemble(ctx, specs) → SongConfig`. Validates distinct roles (the
  orchestrator's `producedByRole` cache is keyed by role) and a role-appropriate
  instrument. Owns no runtime semantics: it only populates public `SongConfig` /
  `TrackConfig` / `TrackParameters` fields per existing contracts.
- `Runtime/CoreScripts/Composition/Smoke/CompositionSmokeRunner.cs` — runtime
  `MonoBehaviour`; renders the same song as the window via `GenerateSinglePart`;
  exports to `Application.persistentDataPath/CompositionSmoke/` (D-SMOKE-RT-1=A);
  reads a shared `SmokeSetupSO`; seeded runner-only Root/BPM randomization
  (D-SMOKE-RT-4=A).
- `Runtime/CoreScripts/Composition/Smoke/SmokeRenderUtil.cs` — shared
  `BuildEffectiveSpec` (D-SMOKE-RT-2=B, lifted from the window),
  `StripMetronomeChunks` (D-SMOKE-RT-3=A, lifted), and `LogRenderFingerprint`
  (per-chunk parity diagnostic).
- `Runtime/CoreScripts/Composition/Smoke/SmokeEntry.cs` +
  `…/Smoke/SmokeSetupSO.cs` — the shared row type and single-source-of-truth
  setup asset (D-SMOKE-RT-5=A).

### Changed
- `Editor/CompositionSmokeWindow.cs` — single-track → multi-track; assembly
  delegated to `SmokeSongConfigAssembler`; optional metronome strip; soft type
  warnings. Menu label `MidiGenPlay/Smoke/Composition Smoke (multi-track → .mid)`.
  It now also delegates `BuildEffectiveSpec`/`StripMetronomeChunks` to
  `SmokeRenderUtil`, uses the shared `SmokeEntry`, logs a render fingerprint, and
  gains a `SmokeSetupSO` field with Save/Load round-trip.
- Render entry moved from `GenerateSong(song, seedOverride)` to
  `GenerateSinglePart(part, roles, partIndex: 0, bpmOverride, instrumentOverrides:
  null, seedOverride)`. `GenerateSong` never reads `PartConfig.ExplicitBpm` and
  resolves tempo via `GetBPMFromRange(part.TempoRange, MultiplesOfTen)`, an
  unseeded `System.Random` — so the BPM field was dead and tempo varied per render.
  Underlying orchestrator behavior unchanged; tracked as **BPM-DET-1** (open).

### Notes
- Decisions: **D-SMOKE-MT-1 = B**, **-2**, **-3**, **-4 = B** (Harmony out),
  **-5 = A**; **D-SMOKE-RT-1 = A**, **-2 = B**, **-3 = A**, **-4 = A**, **-5 = A**;
  **D-SMOKE-DOC-1 = A re-confirmed** (six Smoke files, incl. a MonoBehaviour + a
  referenceable ScriptableObject, intentionally ungoverned — they own no runtime
  semantics and no composer reads them).
- Smoke Tests 0–5 pass. Exports are **not** bit-comparable to pre-batch ones
  (per-track seeds now via `ResolveTrackSeedPart`; tempo no longer a random roll).
- **Parity PASSED**: window and runner produce byte-identical `.mid` under a
  shared setup + fixed seed, confirmed via `LogRenderFingerprint` chunk hashes
  (after resolving the two divergences below).
- The parity work surfaced two determinism issues, both fixed this arc:
  **MEL-NULL-1** (own entry) and **VL-DET-1** (voicer start-register drew from
  global `UnityEngine.Random`; own entry), plus a Unity serialization footgun
  (field initializers do not run for `[Serializable]` rows added via the
  inspector "+"; the runner's rows came up zero-valued — `randomRerollChance = 0`,
  `arpeggioRate = PerBeat`). D-SMOKE-RT-5's shared setup removes the class of
  drift the second issue caused.

## 2026-07-15 — MGP-ALWTTT-ARTIC-1: randomized chord articulation (Random selection policy)

### Added
- `Runtime/CoreScripts/Composition/Articulation/RandomArticulationRoller.cs`
  — composer-side roll policy for `ChordExpressionType.Random` (internal;
  test access via existing InternalsVisibleTo). Dedicated stream, SD-1
  rerollChance gate, SD-2 weighted pool with `BuildWeightTable` test seam,
  degenerate-list uniform fallback + one-time warning (never silent). It also
  carries an observability-only surface: `History` (resolved figures in
  emission order) and `DescribeRolls()` (one-line policy + roll trace) —
  logging/test hook; consumes no draws and has no semantic effect.
- `ChordExpressionType.Random = 6` (append-only; values 0..5 unchanged) and
  `ChordExpressionWeight { figure, weight }` [Serializable] struct
  (`Composition/Data/ChordExpressionType.cs`).
- `SongOrchestrator.ResolveArticulationSeed(trackSeed)` — new SEED-1-style
  derivation seam, FNV-1a over `"{trackSeed}|artic"`.
- `MidiGenerator.GenContext.trackSeed` — per-track seed int, swap/restored
  by `GenerateOne` beside `ctx.rng`.
- `Tests/Editor/ChordTrackComposer_RandomArticulationTests.cs` — 15 tests:
  ResolveArticulationSeed goldens (independent FNV-1a), same-seed sequence
  bit-repeatability (held-loop guarantee), distinct-seed variance (SEED-1
  idiom), chance 0/1/clamp semantics, never-returns-Random, D4 pool breadth,
  weight-table rules (entries-define-pool, exclusion, duplicate summing,
  Random-entry ignore, degenerate fallback), single-figure weighting,
  `PlanHits(Random) == PlanHits(Block)`.

### Modified
- `Runtime/CoreScripts/Composition/Composers/ChordTrackComposer.cs` — builds
  one `RandomArticulationRoller` at `Compose` entry when the backing card
  selects `Random` (seeded from `ResolveArticulationSeed(ctx.trackSeed)`),
  threads it (nullable) through `ComposeProcedural` → `RenderFromProgression`
  (MOD-DIR-1 pattern; `ITrackComposer` unchanged); at BOTH emission sites the
  effective figure is resolved just above the SAME single unconditional
  `Emit` call (`roller?.NextFigure() ?? fixed`). Null roller (any fixed
  figure) => CA-T1 behavior, bit-identical. Also gains a `logGenerator`-guarded
  ARTIC-1 roll trace at BOTH emission sites (`articRoller.DescribeRolls()`);
  logging only.
- `Runtime/CoreScripts/Composition/Articulation/ChordArticulator.cs` —
  `PlanHits` defensively degrades a leaked `Random` to `Block` (D6; covers
  bassline cards until the bass roll is wired). Articulator stays RNG-free.
- `Runtime/CoreScripts/Composition/SongOrchestrator.cs` — new
  `ResolveArticulationSeed` seam; `GenerateOne` gains an `int trackSeed`
  param (3 call sites) and swap/restores `ctx.trackSeed` beside `ctx.rng`.
- `Runtime/CoreScripts/Composition/MidiGenerator.cs` — `GenContext.trackSeed`.
- `Runtime/CoreScripts/Composition/Data/BackingCardConfigSO.cs` — new fields
  `randomRerollChance` (float, [Range(0,1)], default 1) and
  `randomFigureWeights` (`List<ChordExpressionWeight>`, default empty).
- `Editor/CompositionSmokeWindow.cs` — the D-SMOKE-MT-1=B in-memory fallback
  now also exposes `randomRerollChance` + `randomFigureWeights` (Backing rows
  with `chordExpression = Random`), and warns when a Bassline row selects
  `Random` (degrades to `Block`, D6). Editor-only; belongs to the SMOKE-MT
  arc's surface (see the SMOKE-MT entry, applied in the same pass).

### Behavior
- No card / any fixed figure: zero new draws, no roller, output unchanged
  (existing CA-T1 Block bit-identity tests remain the BC guard).
- `Random`: per-chord-event deterministic roll; per-loop variety via the
  host's per-render `seedOverride` (SEED-1); same seed => bit-identical
  render. Voicings never shift when toggling Fixed<->Random (dedicated
  stream).
- Bassline card selecting `Random` degrades to `Block` (v1, D6).

### Cross-project (ALWTTT side — tracked there)
- Consumer surface communicated for the adoption rider + boundary entry
  (SSoT_ALWTTT_MidiGenPlay_Boundary.md §8.x, SEED-1 pattern): see §8 of the
  ARTIC-1 diffs file / the batch close-out message.

### Decisions locked
- D1=A · D2=A · D3=A · D4=A · D5=fixed rate · D6=degrade-only ·
  SD-1=A · SD-2=A · SD-3=A.

### Not changed
- `IChordArticulator` signature and RNG-free contract; CA-T1 figures,
  velocity curve, degrade rules; `ITrackComposer`; seed policy remains
  host-side (no new entropy site); bass roll not wired (explicit follow-up).

## 2026-07-15 — CA-F2: monophonic bass articulation consumer

### Added
- `Runtime/CoreScripts/Composition/Data/BasslineCardConfigSO.cs` — new bundle
  (SD-F2-4=A): `chordExpression` (default `Block`) + `arpeggioRate` (default
  `Eighth`). Fills the TrackStyleBundles §4.1 Bassline TBD row. Persistent
  card-level selection (D-EXP1=A); independent of the backing card
  (SD-F2-5=A).
- `Tests/Editor/BassTrackComposer_ArticulationTests.cs` — 9 tests: the
  SD-F2-1 GATE (1-note Block through `Emit` byte-identical to the legacy
  `MoveToTime`+`Note` pair), per-beat-span bit-identity + the pinned 6/8
  deviation (SD-F2-3=B), `ResolveArticulation` defaults / card values /
  backing-card-ignored (SD-F2-4/5=A), monophonic figure semantics (arpeggio =
  repeated-note pulse, Up≡Down; offbeat stabs), never-silent degrade to the
  exact legacy pair, all-figure determinism.
- `Documentation~/runtime/SSoT_Composer_Bass_Track.md` — new SSoT (SD-F2-6=A;
  bass previously had none — "composer SSoT pending" gap closed).

### Modified
- `Runtime/CoreScripts/Composition/Composers/BassTrackComposer.cs` — the
  bass's SINGLE emission site replaces `MoveToTime`+`Note` with one
  unconditional `_articulator.Emit(...)` call carrying a 1-note voicing
  (SD-F2-1=A; same shared static engine instance pattern as the backing
  composer). New: card resolve at `Compose` entry via internal test seam
  `ResolveArticulation` (persistent, no snapshot-and-clear); Part-TS meter
  derivation (`GetTimeSignatureDetails` + `GetBeatSpan`, SD-F2-3=B). The
  note-selection loop — including its per-event ctx.rng draw count and order
  (1 draw root mode / 2 draws chord-tone mode) — is byte-for-byte untouched.
  `logGenerator` trace extended with the resolved expression/rate.

### Behavior
- Default (`Block` / no bassline card / non-bass bundle in the Style slot):
  MIDI-byte bit-identical to prior output in every beat-unit==4 meter
  (test-pinned), with the identical rng draw sequence.
- **Deviation (SD-F2-3=B, on record):** in beat-unit≠4 meters (e.g. 6/8) the
  bass now emits on the Part beat span instead of the legacy unconditional
  Quarter — a deliberate, test-pinned sync fix of a pre-existing
  meter-authority violation that desynced bass from backing.
- Velocity: `Block` clamps 0..127 where the legacy raw cast threw
  out-of-range — byte-identical for valid 0..127 data.
- Determinism preserved: the articulator is RNG-free; no draws added,
  removed, or reordered.

### Authority changes
- NEW `runtime/SSoT_Composer_Bass_Track.md` — bass consumer authority.
- `runtime/SSoT_Composer_Backing_Track.md` §8.4 closing paragraph — reworded
  to reference the now-implemented bass consumer (amends the CA-T1 draft).
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
  — §4.1 Bassline row filled; §4.2 list updated; new §4.5 Bassline bundle;
  placeholders renumbered to §4.6 (Melody/Harmony only).
- `coverage-matrix.md` — new bass composer row.
- `ssot_manifest.yaml` — bass SSoT registered with governs + invariants.

### Decisions locked
- SD-F2-1=A (reuse `IChordArticulator.Emit` with a 1-note voicing; EmitMono
  contingency on record) · SD-F2-2=A (figures over the selected note;
  chord-tone walk deferred) · SD-F2-3=B (Part-meter time base; deviation
  scoped and pinned) · SD-F2-4=A (`BasslineCardConfigSO`) · SD-F2-5=A (fully
  independent of the backing card) · SD-F2-6=A (dedicated bass SSoT).

### Not changed
- `BassTrackComposerFactory` (still hardcoded `randomChordTone: false`);
  `ITrackComposer`; the engine (`ChordArticulator`) and the backing composer.
- Pre-existing, on record: single-pass rendering (no repeat-to-fill);
  normalization-order hazard (bass may consume the un-normalized progression
  if it renders before the backing track); `degreeAccidental` ignored.

## 2026-07-15 — Hotfix: MelodyTrackComposer log NRE (log-only)
- `Composers/MelodyTrackComposer.cs` — null-safe `logGenerator` trace; no
  longer NREs when a melody track has no MelodyCardConfigSO in its Style slot.
  Log-only; no output/determinism/test impact. Surfaced while smoke-testing
  CA-F2 with `logGenerator` on. Distinct from MEL-NULL-1 (own entry).

## 2026-07-15 — CA-T1: Tier-1 chord articulation engine

### Added
- `Runtime/CoreScripts/Composition/Data/ChordExpressionType.cs` — new enums
  `ChordExpressionType { Block=0, PerBeat, Offbeat, Staccato, ArpeggioUp,
  ArpeggioDown }` (SD-1=A; values serialized, never renumber) and
  `ArpeggioRate { PerBeat, Eighth, Sixteenth }` (SD-4=B; Eighth default).
- `Runtime/CoreScripts/Composition/Interfaces/IChordArticulator.cs` — new
  post-voicing articulation seam. Contract: deterministic and RNG-free
  (SD-3=A); meter authority (Part beatSpan/beatsPerBar); Block = exact legacy
  pair; never silent (Block-degrade).
- `Runtime/CoreScripts/Composition/Articulation/ChordArticulator.cs` — new
  folder + implementation. Internal pure `PlanHits` planning seam (test
  surface, via existing InternalsVisibleTo) + thin `Emit` translator.
  SD-5=A velocity curve (×1.00 downbeat / ×0.85 on-beat / ×0.80 off-beat,
  round away-from-zero, clamp 1..127; Block keeps legacy clamp 0..127).
- `Tests/Editor/ChordTrackComposer_ArticulationTests.cs` — 16 tests: Block
  MIDI-byte bit-identity vs the legacy pair, per-figure plans (grids, legato/
  staccato durations, offbeat upstrokes, arpeggio cycling + pitch ordering at
  MIDI level), boundary truncation, all degrade rules, 7/8 meter-authority
  accents, velocity clamps, determinism.

### Modified
- `Runtime/CoreScripts/Composition/Composers/ChordTrackComposer.cs` — BOTH
  chord emission sites (grid path in `Compose`; `RenderFromProgression`)
  replace `MoveToTime`+`Chord` with the SAME single unconditional
  `_articulator.Emit(...)` call (structural anti-divergence). `Compose`
  resolves `chordExpression`/`arpeggioRate` from the backing card at entry
  (persistent, D-EXP1=A — NO snapshot-and-clear; §6/§7 lifecycle does not
  apply) and threads both through `ComposeProcedural` →
  `RenderFromProgression` (private signatures +2 params each, MOD-DIR-1
  pattern; `ITrackComposer` unchanged).
- `Runtime/CoreScripts/Composition/Data/BackingCardConfigSO.cs` — new fields
  `chordExpression` (default `Block`) and `arpeggioRate` (default `Eighth`).

### Behavior
- Default (`Block` / no card bundle): MIDI-byte bit-identical to prior
  output (test-pinned).
- Determinism preserved and hardened: the articulator never consumes
  `ctx.rng` (a draw would perturb every downstream consumer of the shared
  stream in the same render); velocity/timing are pure functions of beat
  position.
- Randomized arpeggio-rate variety (user wish at SD-4) explicitly deferred
  to the seeded-variation batch (requires an rng policy CA-T1 excludes).

### Authority changes
- `runtime/SSoT_Composer_Backing_Track.md` — new §8 "Chord expression /
  articulation (Tier 1)"; prior §8 "Update triggers" renumbered to §9 and
  extended.
- `coverage-matrix.md` — new articulation row.
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
  §4.3 — backing bundle field list extended (authoring surface only).

### Decisions locked
- D-PRIO=A (shared engine first; bass is a later monophonic consumer) ·
  D-EXP1=A (persistent backing-card field) · D-EXP2=Tier1 · SD-1=A (6-member
  taxonomy, no `Sustained` alias) · SD-2=A (`BackingCardConfigSO`) · SD-3=A
  (pure curve, no rng) · SD-4=B (eighth default, configurable rate;
  onset-anchored cycling; Block-degrade) · SD-5=A (multiplicative curve).

### Not changed
- `IChordVoicer` / voicing semantics; §6/§7 transient hints; `chd:` marker
  stamping (per event, not per hit); `ITrackComposer`.
- Pre-existing `degreeAccidental` grid-vs-`RenderFromProgression`
  inconsistency: still out of scope, still on record.

## 2026-07-05 — CQ-A1-OBJ2: per-chord inversion voicing hint (pin)

### Added
- `SongConfig.PartConfig.ChordInversionHints : IReadOnlyList<int?>` — new
  transient (`[NonSerialized]`) per-chord inversion pin, index-aligned to the
  rendered progression's events. Null entry / short list / out-of-range value
  = unset (safe no-op, never clamped — D2b = a). Sticky-per-position (D2a = a):
  applies at its event position on every pattern repeat within one render.
- `ChordTrackComposer.ResolveInversionPin(hints, eventIndex)` — internal static
  per-position resolver (the D2a test seam); threaded through both chord render
  loops (inline card-progression path in `Compose` and `RenderFromProgression`).
- `Tests/Editor/ChordTrackComposer_InversionPinTests.cs` — baseline
  candidate-set identity (unset = bit-identical), exact-rotation pins (triad +
  seventh, including pin `0` ≠ unset and pin-overrides-`useInversions`),
  out-of-range/negative no-ops, D2a sticky-per-position, and D3 combined-hint
  precedence at the seams.
- `runtime/SSoT_Composer_Backing_Track.md` **§7 — Per-chord inversion hint
  (voicing pin)** (behavior / lifecycle / determinism / boundary, mirroring
  §6's shape); previous §7 "Update triggers" renumbered to **§8** and extended
  with §6/§7 semantics-change triggers.

### Changed
- `IChordVoicer.VoiceChord` and `BasicVoiceLeadingVoicer.VoiceChord` gained an
  optional trailing `int? forcedInversion = null`; existing callers compile
  unchanged.
- `BasicVoiceLeadingVoicer.GeneratePcCandidates` — pin enforcement site
  (**D0 = A**: a valid pin yields exactly one candidate rotation, suppressing
  Drop-2 and outranking the `useInversions`/`useDrop2` toggles); visibility
  `private` → `internal` as the fixture-free test seam (mirrors
  `TryDirectionalFirstChordCore`).
- `ChordTrackComposer.Compose` — the transient snapshot-and-clear block now
  also consumes `ChordInversionHints` (exactly-once-per-render lifecycle,
  mirroring the §6 modulation hint).
- `runtime/SSoT_Runtime_Song_Model_and_Config.md §1.1` — registers the new
  transient alongside the modulation hint, pointing to backing-track §7.
- `planning/active/Roadmap_Chord_Expressivity.md` — "Chord inversions" flipped
  **DEFERRED → BUILT** with decisions D0–D3 + D2a/D2b recorded.

### Decisions
- D0 = A (pin, not bias) · D1 = A (inversion index, not bass pitch-class) ·
  D2 = A (per-chord scope) · **D2a = a** (sticky-per-position) · **D2b = a**
  (out-of-range value ⇒ unset, never clamped) · D3 = A (§6 wins the render's
  first chord; structural in both loops).

### Determinism
- Default-unset is bit-identical to prior output. The pin is RNG-free; non-
  pinned chords see an unchanged candidate set and scoring order.

## 2026-07-05 — PATTERN-PERSIST-1: pattern-asset persistence unification

### Added
- `TrackPatternConfigStoreResources<T>` — `public string AssetsSaveRootPath`
  (exposes the resolved `Assets/Resources/ScriptableObjects/Patterns/<TypeFolder>`
  save root; pure string, not `#if UNITY_EDITOR`-guarded) and editor-only
  `PersistNewAtPath(T instance, string projectPath)` (create-at-an-explicit-
  caller-chosen-path, so the editor window keeps its interactive
  `SaveFilePanelInProject` naming dialog while the store owns the `AssetDatabase`
  write — D6 = C).
- Additive "Browse Saved Patterns" foldout in all three pattern editors, reading
  `store.GetAll()` / `Refresh()` off each type's canonical Resources root (D3 = A,
  canonical-root-only). The existing `ObjectField` target picker is retained.

### Changed
- `DrumPatternEditorWindow` — `ApplyToAsset` / `SaveAsNewAsset` route writes through
  `TrackPatternConfigStoreResources<DrumPatternData>("Drums")`; the palette-scan
  folder list now reads `AssetsSaveRootPath`. Save root unchanged
  (`.../Patterns/Drums`, byte-identical to the removed constant).
- `ChordProgressionEditorWindow` — all four internal save sites (`ApplyToAsset`
  in-place + create branch, `ApplyGridToTarget` in-place, `SaveAsNewAsset` Roman +
  grid paths) route through `TrackPatternConfigStoreResources<ChordProgressionData>("Chords")`.
  The editor now passes a real default save folder (`.../Patterns/Chords`) for the
  first time — previously every `SaveFilePanelInProject` call passed no folder.
- `MelodyPatternEditorWindow` — `ApplyToAsset` / `SaveAsNewAsset` route through
  `TrackPatternConfigStoreResources<MelodyPatternData>("Melodies")`; the pattern
  write folder realigns from singular `.../Patterns/Melody` to plural
  `.../Patterns/Melodies` (D5 = A), matching `PatternRepositoryResources`' read root
  and the shipped assets. `CreateNewParamsAsset` (a `MelodyGenerationParamsSO` save,
  a different asset kind) is unchanged.
- `TrackPatternConfigStoreResources<T>` — corrected two stale inline comments that
  referenced `MidiGenPlay/Patterns/…` where the code resolves
  `ScriptableObjects/Patterns/…` (comment-only).

### Removed
- Per-window hardcoded `DefaultSaveFolder` constants from `DrumPatternEditorWindow`
  and `MelodyPatternEditorWindow` (the store is now the single source of the save
  root, D4). Palette / gen-params folder constants and the catalogue-wizard folder
  constants are a different asset kind and were intentionally left untouched.

### Docs
- `authoring/SSoT_Authoring_Tools.md` — removed the drum + melody "hardcoded default
  folder" limitation bullets; §6 retitled from a Phase-8 "next target" to a closed
  persistence note (all three editors persist via the store; `IPatternRepository`
  remains the read path; the naming dialog is preserved via `PersistNewAtPath`).
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` §4 — the store-backed-persistence
  line moved from "What is not true yet" to "What is already true," reworded to name
  the store rather than "repository abstractions."
- `authoring/SSoT_Authoring_Chord_Progressions.md` §3 — closure note (canonical
  `.../Patterns/Chords` default; all four save sites via the store).
- `authoring/SSoT_Authoring_Melody_Composition.md` — status-note line on the
  `/Melody`→`/Melodies` realignment.
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — Phase 8 → Completed; open
  decisions resolved (D1 store not repository; D2 chord+melody in scope; D3
  additive/canonical; D4 constants removed; D5 `/Melodies`; D6 = C dialog-preserving);
  widened-scope note recorded.
- `CURRENT_STATE.md` — Phase 8 moved to "Just completed"; "Next" list updated.
- `coverage-matrix.md` — closure note in "Notes on primary-home flips" (no concept →
  authority row changed; no primary-home flip; records the deferred governs decision).

### Governance
- The persistence Services layer (`TrackPatternConfigStoreResources.cs` /
  `ITrackPatternConfigStore.cs` + the sibling `PatternRepositoryResources.cs` /
  `IPatternRepository.cs`) was brought under `SSoT_Authoring_Tools.md` `governs:`
  (decision B), with a persistence-contract invariant added (editor owns the Save
  dialog + Undo; store owns the `AssetDatabase` write; repository is read-only).
  These Runtime/ files are governed by an authoring SSoT because Tools §6 documents
  the persistence mechanism.

## 2026-07-05 — MGP-ALWTTT-SEED-1: per-render seed threading

### Added
- `SongOrchestrator` — internal seed-derivation seams `ResolveBaseSeed`,
  `ResolveRepContextSeed` (`(base + partIndex*397) ^ rep`; original operator
  precedence preserved), `ResolvePartContextSeed`, `ResolveTrackSeedSong`,
  `ResolveTrackSeedPart`; `StableHash32` visibility flipped `private → internal`
  (test access via the existing `InternalsVisibleTo("MidiGenPlay.Tests.Editor")`).
- `Tests/Editor/SongOrchestratorSeedTests.cs` (new) — golden FNV-1a values
  captured against the **pre-batch** seed-string formats (the bit-identity
  guard), null-override == explicit-`defaultSeed` equivalence, the
  operator-precedence guard, distinct-seed ⇒ distinct-track-seed, and an
  end-to-end `PaletteSelector` variance + repeatability test mirroring the ALWTTT
  S5g acceptance (distinct seeds ⇒ ≥2 distinct picks over a 6-entry palette; same
  seed ⇒ same pick). No Unity/`SongConfig` fixtures (the internal-seam idiom,
  same as `MelodyTrackComposer_PatternDeterminismTests`).

### Changed
- `ISongOrchestrator` / `SongOrchestrator` — `GenerateSong(SongConfig song,
  int? seedOverride = null)` and `GenerateSinglePart(..., int? seedOverride =
  null)` (trailing optional parameters; the `bpmOverride` default was also added
  to the interface signature to match the implementation). All five seed sites
  now derive from `baseSeed = seedOverride ?? _settings.defaultSeed`, resolved
  once per render call.
- `runtime/SSoT_Runtime_Generation_Orchestration.md` — new **§5.1 Seed
  threading** contract (base-seed resolution once per render; every part/rep/track
  seed derives from it incl. the `PaletteSelector` RNG stream; host-side policy;
  bit-exact backward compat; the internal seams named); §8 gains a seed-contract
  update trigger.
- `runtime/SSoT_Composer_Rhythm_Track.md` §4 — determinism contract now names the
  base-seed source (`seedOverride ?? defaultSeed`); the
  one-`NextDouble()`-per-pick line is unchanged.

### Cross-project (ALWTTT side — tracked there; no package governs change)
- `MidiMusicManager.RenderSinglePart` gains a pass-through `int? seedOverride =
  null` forwarded to `GenerateSinglePart`, and ALWTTT owns the per-song seed
  policy. `MidiMusicManager` is out-of-tree from the package (as with
  ALWTTT-MOD-DIR-3 / CE-L1), so this is not a package changelog "Changed" item.
  This batch delivers only the package seed surface + the adoption note;
  host-side acceptance runs in ALWTTT S5g-b.

### Decisions
- **D1 (locked at open)** — the package accepts a caller-supplied seed; seed
  policy stays host-side; the package never invents per-render entropy.
- **D2 (locked at open)** — no seed supplied ⇒ bit-identical to the previous
  behavior (`defaultSeed` fallback).
- **D3 = A** — the seed surface is an optional trailing `int? seedOverride =
  null` on both `GenerateSinglePart` and `GenerateSong` (stateless, per-call).
  The GenContext-field and MidiGenerator-setter options were rejected: the
  context is constructed inside the render methods, and a setter is stateful /
  leak-prone.
- **D4 = declined** — Pick-chain exclusion not implemented. Clone-on-pick (drum:
  `Instantiate` inside the pick; chord: caller clones per CE-F1) means the host
  never holds a palette-entry-identical reference, so `excludeIfPossible` by
  reference would silently never match; and threading a previous-pick identity
  host → composer → picker requires a GenContext meaning change beyond this
  batch. ALWTTT ships probabilistic no-repeat (palettes ≥ 6). Revisit as its own
  batch with an entry-identity decision if needed.

### Not changed
- `PaletteSelection.cs` / `Tests/Editor/PaletteSelectorTests.cs` — untouched
  (D4 declined); the one-`NextDouble()`-per-`Pick` invariant is intact.
- No behavior change for any caller that does not pass a seed.
- No host-policy (per-song seed derivation) logic inside the package.

### Known intentional deltas
- The golden-value tests cover ASCII seed strings (all current role names and
  typical musician IDs); a non-ASCII musician ID still hashes correctly but is
  not golden-guarded.
- ALWTTT acceptance advice: pin three fixed distinct seeds for the "≥2 distinct
  PROG_PICKs over 3 songs" check to avoid a ~3% false-fail under a uniform
  6-entry palette.

## 2026-06-22 — Melody Authoring MVP Phase 5: polish, validation, documentation closure (MVP complete)

### Validated (no runtime change)
- `MelodyTrackComposer.ComposeFromPattern` edge cases, by code-trace against the shipping
  method — all correct and deterministic; no code change required (D-MEL5.1 = A):
  - empty pattern → silence, no crash (`MelodyPatternData.TotalBeats ≥ 1` and the
    `Math.Max(1.0, …)` loop floor prevent a zero divisor; the empty `SnapshotOrdered()` list
    emits nothing; `ToFile`/`StampBankAndPatch`/`ForceAllChannel` don't throw on zero notes;
    the guide-note cache call is null-guarded);
  - single-note pattern;
  - pattern shorter than the Part → tiles (`repeats = Ceiling(partBeats / loopBeats) ≥ 2`);
  - pattern longer than the Part → truncated by note onset (onsets `≥ partTotalBeats`
    dropped; a note whose onset is inside the Part rings to its authored duration);
  - `octaveOffset` at the band extremes → clamped to `[octaveMin-1, octaveMax-1]` (the same
    register band the shipping `ChooseMelodicRegister` uses), no out-of-range throw;
  - authored duration floored at `MinNoteBeats` (0.05); velocity clamped to 1–127.
- Determinism: the path consumes no RNG, `SnapshotOrdered()` is a pure sort over a fixed
  serialized list, and `ctx.rng` is untouched ⇒ same pattern + tonality/root + meter ⇒
  byte-identical MIDI.

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — §7 "Meter & looping" reworded: tiles-by-beats +
  warning is the **accepted MVP outcome** (D-MEL5.1 = A), the truncation clause now states
  it is by note onset, and full bar-time renormalization (with compound/odd-meter beat-unit
  timing) is labelled **post-MVP** rather than "deferred (Phase 5)". §7 "Determinism" gains
  a one-line Phase-5 validation note.
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note + §8 updated: all phases
  (1–5) closed, MVP complete; a Phase-5 paragraph records D-MEL5.1 = A. No §5/§7 semantic
  change.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 5 marked DONE with D-MEL5.1 = A and closure-scope
  = A recorded; sequencing + "Immediate next steps" re-pointed to MVP-complete.
- `coverage-matrix.md` — Notes section gains an MVP-closure entry (no row / primary-home
  change).
- `CURRENT_STATE.md` — "Active now" melody paragraph flipped to MVP-complete; a Phase-5
  "Just completed" entry prepended.
- `ssot_manifest.yaml` — maintenance log records the Phase-5/MVP close (no invariant change
  under A); the stale "(not yet built)" note on the authoring-melody governs comment fixed.

### Decisions
- **D-MEL5.1 = A** — meter-mismatch handling keeps tiles-by-beats + warning as the accepted
  MVP limitation; bar-time renormalization (and compound/odd-meter beat-unit timing across
  both melody paths) is post-MVP future work. No Phase-4 / INT1 contract change; no manifest
  invariant change.
- **Closure scope = A** — editor-side Phase-5 target work (round-trip hardening, wizard UX
  polish) deemed satisfied by the Phase 2–3 closures; MVP completion rests on runtime
  validation + documentation closure.

### Added (testability seam + determinism fixture — F-A)
- `MelodyTrackComposer.cs` — extracted the note-resolution loop of `ComposeFromPattern` into
  an `internal static ResolvePatternNotesCore(...)` (plus an `internal readonly struct
  ResolvedMelodyNote` and `MinNoteBeats` promoted to an `internal const`); `ComposeFromPattern`
  now calls the seam and renders/caches the returned sequence. The output is **byte-identical**
  (same notes, render order, timing, velocity, guide notes) — a testability refactor only:
  no contract change, SSoT §7 and the manifest invariants are untouched. Mirrors
  `ChordTrackComposer.TryDirectionalFirstChordCore`.
- `Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs` (new) — EditMode fixture with
  no Unity fixtures (targets the seam + `MelodyPatternData.SnapshotOrdered`, visible via the
  existing `InternalsVisibleTo`): `SnapshotOrdered` ordering + idempotence; seam determinism
  (repeat-call sequence equality), tiling (shorter-than-Part), onset truncation
  (longer-than-Part), octave clamp at the band extremes, duration floor + velocity clamp,
  empty → empty, and degree-Tonic → root pitch. (Resolves the F-A vs. F-B choice = **F-A**.)

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change.
- `SSoT_CONTRACTS.md`: no change.
- Runtime behavior: unchanged — the F-A seam extraction is byte-identical (same MIDI bytes / guide notes); SSoT §7 and the manifest invariants are untouched. (Edge-case validation itself required no code change; the seam was added solely for testability.)

## 2026-06-17 — Melody card-pattern integration (D-MEL-INT1, package half)

### Added (package)
- `MelodyCardConfigSO` — new `patternOverride` (`MelodyPatternData`) field under a "Pattern
  (optional authored override)" header. If set, the composer plays it verbatim and bypasses
  the procedural pipeline and the card's leading/palette/style overrides.

### Changed (package runtime)
- `MelodyTrackComposer.Compose` — the authored-melody dispatch now reads the melody card
  override first: `(cfg.Parameters?.Style as MelodyCardConfigSO)?.patternOverride ??
  (cfg.Parameters?.Pattern as MelodyPatternData)`. Card-wins, mirroring rhythm's
  `RhythmCardConfigSO.patternOverride > … > TrackParameters.Pattern`. Feeds the same
  `ComposeFromPattern` path; still RNG-free/deterministic. No new assembly coupling — the
  composer already referenced `MelodyCardConfigSO` (its leading/palette/style fields).

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — §7 "Integration surface & precedence" updated to
  the three-level precedence (card `patternOverride` > `TrackParameters.Pattern` > procedural);
  §3 notes the consumer card may carry a pattern; §8 trigger covers the precedence order.
- `ssot_manifest.yaml` — `SSoT_Composer_Melody_Track` gains the card-pattern precedence
  invariant; maintenance log records the INT1 package half.

### Decisions
- **D-MEL-INT1** Melody card-pattern seam (resolves roadmap D1, Option A): authored melodies
  reach the composer via `MelodyCardConfigSO.patternOverride` (card-wins) with
  `TrackParameters.Pattern` retained as a fallback. Chosen over feeding
  `SongConfigBuilder.FromUI` directly (Option B) because it mirrors the existing rhythm/chord
  card precedent, keeps interpretation package-side, and leaves `TrackParameters` untouched.

### Cross-project (ALWTTT half — tracked separately, not yet done)
- ALWTTT sets `patternOverride` on the melody card asset and folds the referenced
  `MelodyPatternData`'s GUID into `trackInputsHash` (the style-bundle GUID identifies the card
  asset, not its `patternOverride` contents, so authored melodies would otherwise be masked by
  the stem cache). Recorded ALWTTT-side in `SSoT_Runtime_CompositionSession_Integration.md`
  (semantic + integration). Plus a joint card-path smoke. Until then INT1 is package-half-only.

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change (the card carries the pattern).
- `SSoT_CONTRACTS.md`, `coverage-matrix.md`: no change.

## 2026-06-17 — Melody Authoring MVP Phase 4: runtime hookup (`ComposeFromPattern`)

### Added (package runtime)
- `Runtime/CoreScripts/Composition/Composers/MelodyTrackComposer.cs` — a `ComposeFromPattern`
  branch (no new fields beyond a local `MinNoteBeats` const). The dispatch, inserted in
  `Compose` after the instrument null-check and before progression resolution, returns
  `ComposeFromPattern(...)` when an authored `MelodyPatternData` is present (skipping the
  procedural pipeline); otherwise the procedural path is unchanged. `ComposeFromPattern`
  resolves each note's `(degree, octaveOffset)` via `GetNoteFromScale` against the active Part
  tonality/root (reference = the instrument's mid octave per `ChooseMelodicRegister`'s
  convention, clamped to the instrument range), maps beats to `MusicalTimeSpan.Quarter`
  exactly as the procedural path does, tiles the authored loop (`pattern.TotalBeats`) to the
  Part's total beats with final-loop truncation (a `beatsPerMeasure` mismatch logs a warning
  and tiles), clamps velocity, emits via `pb.Note(note, …)`, reuses the existing
  `StampBankAndPatch`/`ForceAllChannel`/`Inspect`, caches the line as `MidiGenerator.GuideNote`s
  via `ctx.SetMelodyForPartMusician`, and consumes no RNG. Analogous to `RhythmTrackComposer`'s
  `DrumPatternData` → `ComposeFromGrid`. Smoke-validated in-game.

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — new §7 "Pattern-override path
  (`ComposeFromPattern`)"; §1 + Scope note the two rendering paths; §8 update trigger added.
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to Phases-1–4 closed;
  §5 boundary updated; §7 "Runtime handoff" expanded; §8 update-triggers note updated.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 4 marked DONE with D-MEL4.1–4.4 recorded;
  sequencing + "Immediate next steps" re-pointed to Phase 5.
- `ssot_manifest.yaml` — `SSoT_Composer_Melody_Track` gains the authored-pattern override
  invariant; maintenance log records the Phase-4 close-out.

### Decisions
- **D-MEL4.1** Reuse `TrackParameters.Pattern`, dispatched via `as MelodyPatternData`. No new
  serialized field; mirrors the rhythm/chord composers; no song-model or contract change.
- **D-MEL4.2** Degree→pitch via `GetNoteFromScale` against Part tonality/root; reference
  register = the instrument's mid octave, `octaveOffset` on top, clamped to the instrument
  range; chord progression not consulted by the pattern path.
- **D-MEL4.3** Beats quarter-mapped; authored loop tiled to the Part with truncation;
  `beatsPerMeasure` mismatch warns + tiles; bar-time renormalization deferred to Phase 5.
- **D-MEL4.4** `ComposeFromPattern` populates the guide-note cache.

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change. `SSoT_CONTRACTS.md`, `coverage-matrix.md`: no change.

### Known intentional deltas
- Meter mismatch tiles by raw beats with a warning; alignment guaranteed only when meters
  match (Phase-5 decision). No automated tests (Phase-4 DoD: manual E2E only, passed).

## 2026-06-17 — Melody Authoring MVP Phase 3: generation-params UI + simplified generator

### Added (package editor)
- `Editor/SimplifiedMelodyGenerator.cs` — new editor-only (`#if UNITY_EDITOR`, namespace
  `MidiGenPlay.Authoring`) static generator. `Generate(MelodyPatternData,
  MelodyGenerationParamsSO, int seed)` maps the Tier-1 params into the caller's working copy:
  density → onsets/measure; rhythmic style → onset placement (Even = evenly spaced;
  Syncopated = pushed onto the off-beat; Burst = short consecutive-subdivision runs with
  gaps); octave range → per-note octave-offset bounds; scale → the seven diatonic degrees,
  drawn with a fixed stability bias (Tonic/Dominant/Mediant favoured). Velocity is a
  deterministic bar-downbeat > beat > off-beat shape. Does NOT invoke the procedural
  `MelodyTrackComposer` / `PhrasePlanner` pipeline (Phase D3) and has no runtime dependency.

### Changed (package runtime)
- `Runtime/CoreScripts/Composition/Data/MelodyGenerationParamsSO.cs` — two additive Tier-1
  fields: `int seed` (deterministic generation seed; closes the gap with the §5 description,
  which already listed a seed) and `GeneralMidiProgram instrumentHint` (DryWetMidi
  `Standards`, mirroring `DrumPatternData`'s `GeneralMidiPercussion`). Added the
  `Melanchall.DryWetMidi.Standards` using. Both additive; existing serialized `.assets`
  deserialize with defaults (seed 0, AcousticGrandPiano). `Normalize()` unchanged. The SO
  remains a generation-time aid only — never read at runtime.

### Changed (package editor)
- `Editor/MelodyPatternEditorWindow.cs` — new "Generation (simplified)" foldout under the
  header (`Header → Generation → Timing → Grid → Actions`): binds/creates a
  `MelodyGenerationParamsSO` (rendered via a cached `Editor` so its inspector incl. the new
  fields appears), a "Generate" button + a "Randomize Seed" button, and a `New Params Asset…`
  creator (new `DefaultParamsFolder` constant). `Generate` overwrites the working copy only
  (asset untouched until Apply/Save As) and auto-fits the octave window. Added an `OnDisable`
  to dispose the cached inspector editor. Scope doc-comment updated to Phase 3.

### Changed (governance docs)
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to
  Phases-1–3-closed; new §5 subsection "Generation parameters & simplified generator
  (Phase 3)" documents the determinism boundary (RNG-free onset placement;
  `System.Random(seed)` for pitch/octave), the seed field, the informational-only
  `instrumentHint`, the non-gating `tonalityHint` + stability bias, and the parameter→output
  mapping; the §5 params-SO paragraph reconciled to list the GM instrument hint and the
  stored seed; §8 update-triggers note updated (only Phase 4 remains).
- `Roadmap_Melody_Authoring_MVP.md` — Phase 3 marked DONE; D-MEL3.1–3.3 + the layout
  decision recorded; sequencing + "Immediate next steps" re-pointed to Phase 4.
- `ssot_manifest.yaml` — `SSoT_Authoring_Melody_Composition` governs gains
  `Editor/SimplifiedMelodyGenerator.cs` (Phase 3) and `Editor/MelodyPatternEditorWindow.cs`
  (closing the Phase-2 registration gap); inline phase-status comment + maintenance log
  updated; an editor-only-generator determinism invariant added.

### Decisions
- **D-MEL3.1** Seed is a stored `int` on the params SO; RNG is `System.Random(seed)`; onset
  placement is RNG-free (a new seed re-rolls pitch/octave over a fixed groove).
- **D-MEL3.2** GM instrument hint added as a Tier-1 control but informational-only for the
  MVP (no pattern field; not read at runtime).
- **D-MEL3.3** `tonalityHint` does not gate the degree set; all seven diatonic degrees,
  stability-weighted; mode-sensitive weighting deferred.

### Not changed
- `runtime/SSoT_Composer_Melody_Track.md`: no runtime change in Phase 3; the
  `ComposeFromPattern` consumption path is Phase 4.
- `coverage-matrix.md`: no change — the primary home for melody authoring is already
  `authoring/SSoT_Authoring_Melody_Composition.md` (no flip).
- `SSoT_CONTRACTS.md`: no new cross-cutting rule.

### Known intentional deltas
- `instrumentHint` and `tonalityHint` are carried but do not affect generated notes in the
  MVP (the pattern stores neither instrument nor pitch); both are documented as
  informational / future-use.
- No automated tests (Phase-3 DoD requires none); a small generator determinism fixture is
  offered as an optional follow-up.

## 2026-06-16 — Melody Authoring MVP Phase 1: deterministic MelodyPatternData

### Changed (package runtime)
- `Runtime/CoreScripts/Data/MelodyPatternData.cs` — replaced the legacy
  probabilistic model (`MelodyNoteData` with `List<ScaleDegree> possibleDegrees`,
  integer measure/beat timing) with a deterministic per-note model: a
  `[Serializable] struct MelodyNoteEvent` (one `ScaleDegree degree`,
  `int octaveOffset`, `float startBeat`, `float durationBeats`, `int velocity`)
  and a sparse `List<MelodyNoteEvent> notes`. Inherits `PatternDataSO`; adds
  explicit `beatsPerMeasure` + `subdivisions` (editor grid) and `SetSignature` /
  `ClearAll` / `InitializeIfEmpty` / `SnapshotOrdered` / `DeepCloneRuntime`
  helpers (mirrors `DrumPatternData`). Absolute pitch is not stored.
- `Runtime/CoreScripts/Composition/MidiGenerator.cs` — removed
  `GenerateMelodyTrackWithPattern` (sole consumer of the old shape;
  non-deterministic `UnityEngine.Random` degree+octave draw) and the two privates
  orphaned by it (`SetBankAndPatchEvents`, `SetChannel`) plus a now-dead
  `Melanchall.DryWetMidi.Composing` using (M-3, clean break).

### Added (package runtime)
- `Runtime/CoreScripts/Composition/Data/MelodyGenerationParamsSO.cs` — new
  ScriptableObject parameterizing the planned wizard's simplified generator:
  optional `MelodicLeadingConfig` / `PhrasePaletteSO` / `MelodicStyleSO`
  references + Tier-1 scalars (density, octave range, `MelodyRhythmicStyle` enum,
  `Tonality` hint) + a `Normalize()` clamp. Generation-time aid only; never read
  at runtime.

### Changed (governance docs)
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to
  Phase-1-closed; new §5 documents the canonical format + params SO; trailing
  sections renumbered (boundary/handoff/triggers → 6/7/8).
- `authoring/SSoT_Authoring_Tools.md` — new §3.D registers the melody wizard as
  the next planned Category-A tool (window is Phase 2/3) + asset-reset caveat.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 1 marked DONE; decisions
  M-3 + D-MEL1.1–1.5 recorded.
- `ssot_manifest.yaml` — `SSoT_Authoring_Melody_Composition` governs populated
  with the two new in-tree types; two invariants added; maintenance-log entry.

### Known intentional deltas
- Existing melody `.assets` (the twelve under `Resources/.../Patterns/Melodies/`)
  reset their note data on reimport — the new per-note shape is incompatible with
  the old serialized fields; disposable, to be re-authored via the wizard
  (D-MEL1.4).
- The note list/type were renamed (`melodyNotes` → `notes`, `MelodyNoteData` →
  `MelodyNoteEvent`); safe because no live code referenced the old names.
- The demoted `EmotionalGenerationPanel` / `MidiGenPlayPanel` keep their
  commented-out generation blocks untouched (D-MEL1.5); those already reference a
  long-gone `MidiGenerator` API.
- The authoring wizard (`EditorWindow`) and the `ComposeFromPattern` runtime
  branch are NOT in this change — they are Phases 2–4.

## 2026-06-16 — CQ-B1: chord quality alphabet v2 (Tier B — ninths)

### Added
- `MusicTheory.ChordQuality` enum: `Dominant9`, `Major9`, `Minor9` (append-only,
  ordinals 14–16; existing serialized `ChordEvent.quality` values unaffected).
  Realization intervals `{0,4,7,10,14}` / `{0,4,7,11,14}` / `{0,3,7,10,14}` (five
  voices, top interval a major ninth) and compact symbols `C9` / `Cmaj9` / `Cm9`
  across `GetIntervalsForQuality`, `GetChordSymbol`,
  `GetChordSymbolSpelledForDegree` (+ `ToRomanRich` display).
- `planning/active/Roadmap_Chord_Expressivity.md` — new arc roadmap for the chord
  vocabulary/voicing work (Tier A → Tier B → inversions), capturing D-CQA1.* and
  D-CQB1.*.

### Changed
- `RomanProgressionParser.TryParseQualitySuffix` — explicit-suffix cases `9`/`dom9`
  → Dominant9, `maj9`/`ma9` → Major9, `m9`/`min9` → Minor9. Explicit-only; no
  change to diatonic inference. Suffix outranks numeral case (`vi9` = dominant-
  ninth; minor-ninth is `vim9`).
- `ChordProgressionLLMResponseHandler.AllowedSuffixes` — `9`, `dom9`, `maj9`,
  `ma9`, `m9`, `min9` added to the D-L4.5 allowlist.
- `ChordProgressionLLMPromptBuilder` system prompt — alphabet gains the three
  ninths; `9`/`maj9`/`m9` removed from the Forbidden list (11/13/add9/6-9 remain
  forbidden); self-check reworded.
- `ChordProgressionEditorWindow` — `QualitySuffixForToken` maps the three ninths
  back to `9`/`maj9`/`m9`; `IsSeventhQuality` adds all three (they contain a real
  7th → 4 grid rows; the 9th has no grid row, a known delta).
- `ChordQualityResolver.GetTriadFamily` — `Dominant9` → Major, `Major9` → Major,
  `Minor9` → Minor.
- `Strategies/VoiceLeading.cs` (runtime voicer) — `GeneratePcCandidates` inversion
  loop uncapped from `i < 4` to `i < pcs.Length`. Zero regression for ≤4-voice
  chords (their length already bounds the loop); only adds the top inversion
  candidate for five-voice chords.

### Changed (package docs)
- `authoring/SSoT_Authoring_Chord_Progressions.md` §4.1 — alphabet now described
  in two tiers (Tier A ≤4 voices; Tier B five-voice ninths); grid-arity note
  generalized (ninths render 4 of 5 rows); the two five-voice voicer deltas recorded.

### Decisions
- **D-CQB1.1** Tier B explicit-only; no diatonic-template change.
- **D-CQB1.2** Ship ninths against the existing voicer + the single zero-regression
  inversion uncap. Do NOT generalize `Drop2` (would change existing seventh
  voicings) and do NOT touch the range clamp.
- **D-CQB1.3** `IsSeventhQuality` gains all three ninths (each has a real 7th).
- **D-CQB1.4** `GetTriadFamily`: Dominant9/Major9 → Major, Minor9 → Minor.
- **D-CQB1.5** Forbidden set now 11/13/add9/6-9; `9`/`maj9`/`m9` are allowed (the
  parser-rejection and guard tests were inverted accordingly — `9` moved from
  rejected to accepted).

### Known intentional deltas
- A ninth is five voices but the grid renders at most the four seventh-chord rows;
  the 9th itself gets no grid row (same family as the Tier A added-6th limitation).
  The Roman / LLM / import path stores and plays all five voices.
- Five-voice voicer nuances: `Drop2` is triad-oriented (effectively inert for
  ninths); a very tall five-voice stack near an instrument's range edge can have
  voices collapsed by the range clamp (pre-existing; also affects 4-voice chords at
  the edges). Neither affects ≤4-voice output.

## 2026-06-16 — CQ-A1: chord quality alphabet v2 (Tier A — sixths + 7sus4)

### Added
- `MusicTheory.ChordQuality` enum: `Major6`, `Minor6`, `Dominant7sus4`
  (append-only, ordinals 11–13; existing serialized `ChordEvent.quality` values
  unaffected). Realization intervals `{0,4,7,9}` / `{0,3,7,9}` / `{0,5,7,10}` and
  compact symbols `C6` / `Cm6` / `C7sus4` across `GetIntervalsForQuality`,
  `GetChordSymbol`, `GetChordSymbolSpelledForDegree` (+ `ToRomanRich` display).
- `Tests/Editor/` — five EditMode fixtures: `RomanProgressionParserTests`
  (new-suffix parse, suffix-outranks-case, extensions still warn-and-downgrade),
  `MusicTheory_ChordQualityTests` (intervals, symbols, append-only ordinals),
  `ChordProgressionLLMResponseHandler_V2Tests` (D-L4.5 guard: new suffixes pass,
  9/add9/6-9 blocked), `ChordProgressionEditorWindow_V2Tests` (suffix round-trip
  + grid arity), `ChordQualityResolver_V2Tests` (triad-family classification).

### Changed
- `RomanProgressionParser.TryParseQualitySuffix` — explicit-suffix cases `6` →
  Major6, `m6`/`min6` → Minor6, `7sus4` → Dominant7sus4 (whole-suffix match).
  Explicit-only: no change to diatonic inference; a bare degree still infers the
  diatonic triad/seventh. Suffix outranks numeral case (`vi6` = major-sixth;
  minor-sixth is `vim6`).
- `ChordProgressionLLMResponseHandler.AllowedSuffixes` — `6`, `m6`, `min6`,
  `7sus4` added to the D-L4.5 allowlist.
- `ChordProgressionLLMPromptBuilder` system prompt — alphabet gains the three
  qualities; `6` removed from the Forbidden list (`6/9` still forbidden); 9/11/13
  remain forbidden (Tier B deferred); self-check reworded.
- `ChordProgressionEditorWindow` — `QualitySuffixForToken` maps the three new
  qualities back to `6`/`m6`/`7sus4` (fixes Roman-rebuild data loss for the new
  qualities); `IsSeventhQuality` adds `Dominant7sus4` (Decision A). Both methods
  made `internal` for EditMode coverage.
- `ChordQualityResolver.GetTriadFamily` — `Major6` → Major, `Minor6` → Minor,
  `Dominant7sus4` → Suspended (previously fell through to `Other`, which
  mis-flagged the new qualities as borrowed for the `isDiatonic` metadata).

### Changed (package docs)
- `authoring/SSoT_Authoring_Chord_Progressions.md` — new §4.1 (chord quality
  alphabet): the `ChordQuality` enum is the canonical source of truth, mirrored
  in lockstep by the parser, prompt alphabet, editor round-trip, and handler
  allowlist; explicit-only; append-only; v2 additions and the known grid-arity
  limitation recorded. §7 update-triggers gains the alphabet.

### Decisions
- v2 scope split by voicer arity/interval: Tier A (≤4 voices, ≤10 semitones —
  Major6/Minor6/Dominant7sus4) shipped now; Tier B (Dominant9/Major9/Minor9 —
  5 voices, span >octave) deferred pending review of `Strategies/VoiceLeading.cs`.
- Inversions (Objective 2): recommended for the voicing layer (a per-chord hint
  following the §6 modulation-override precedent in
  `runtime/SSoT_Composer_Backing_Track.md`), NOT the Roman DSL — slash and
  figured-bass both collide with the grammar/guard. Recommendation only; not built.

### Known intentional deltas
- Grid authoring of `Major6`/`Minor6` renders three chord-tone rows (the added
  6th has no row) because they are 4-voice but not sevenths. The Roman / LLM /
  import path stores and realizes all four voices correctly.
- `Dominant7sus4` is classified Suspended (not Major), so `V7sus4` is flagged
  non-diatonic vs a major V — consistent with how sus2/sus4 are treated.

## 2026-06-11 — CE-L1: LLM card-author (third adopter, consumer-side)

### Added (ALWTTT project, not package)
- `Assets/Scripts/Cards/LLMAuthoring/` — editor-only asmdef pair
  (`ALWTTT.Cards.LLMAuthoring` + `.Tests`; refs MidiGenPlay.Runtime +
  BCS.LLM.Core.Runtime): `CardImportDtos` (+`PaletteIntentJson.requested`
  explicit-presence flag), `CardImportDtoParser` (hoisted from the window),
  `CardLLMVocabulary` (live-snapshot POCO, D-CE-L1.4),
  `CardPaletteDescriptorScanner`, `CardPaletteIntentResolver` (seeded,
  composes over the CE-F1 `PaletteSelector`), and the quartet
  `CardLLMPromptBuilder` / `CardLLMGenerator` / `CardLLMResponseHandler`
  (banned-asset-ref + out-of-alphabet guards) / `CardLLMFieldPlan`; consumer
  copy of `FakeLLMClient`; 77 tests total across both fixtures.
- `Assets/Scripts/Cards/Editor/LLM/CardLLMVocabularyBuilder.cs` —
  registry-driven status keys (both catalogues) with asset-scan fallback.
- `Assets/Scripts/Cards/Editor/CardEditorWindow.LLM.cs` — panel partial +
  `ApplyLlmPlanOnSave` Save hook (bundle creation + palette assignment,
  D-CE-L1.6).

### Changed (ALWTTT project)
- `CardEditorWindow.JsonImport.cs` — DTOs delegated to the shared parser;
  `modifierEffectNames` resolved all-or-nothing at staging; Save-step LLM hook;
  discard-time plan cleanup.

### Changed (package docs)
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 third-adopter row +
  consumer-side scope note; vocabulary-snapshot deviation recorded.
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
  — §5 LLM panel + §5.3 boundary rules.
- `Roadmap_Composition_Expressivity.md` — CE-L1 DONE + D-CE-L1.1..7 +
  telemetry (1572 in / 366 out); arc complete.
- `ssot_manifest.yaml` — two new invariants; maintenance log entry.

### Known intentional deltas
- The vocabulary is a per-generate snapshot, not an asset (deviation from the
  drum/chord Vocabulary-SO stage; rationale D-CE-L1.4).
- The generator does not parse the DTO (handler owns the single parse) —
  deliberate split difference from the chord twin to avoid double-parsing.
- The legacy "Create from JSON" box keeps path/guid loading and
  warn-and-default enum behavior; the guards apply to the LLM-panel route only
  (D-CE-L1.5).

## 2026-06-10 — CE-E1 + CE-F1: Card Editor clone/presets + shared palette selector

### Added
- `Runtime/CoreScripts/Composition/Data/PaletteSelection.cs` — shared, deterministic
  `PaletteSelector` (Tier A exact-TS -> B heuristic -> C raw-weights; one
  `rng.NextDouble()` per pick) over a neutral `TsFeatures` summary, plus typed
  `ProgressionFinder` / `PatternFinder` adapters. Generalizes for future melody/harmony.
- `Tests/Editor/PaletteSelectorTests.cs` — Tier A gating, Tier-A-skip, determinism,
  degenerate inputs, the B1-B6 heuristic ordering, grouping counts, chord
  `StartsPerBar`, the drum capped-onset-density cases, and kick-foundation extraction
  on a real `DrumPatternData`.
- `CardEditorWindow` (ALWTTT) — Clone Card action (deep-copies payload, clones the
  style bundle so the clone never shares the source palette) + New-Card preset buttons
  (Action / Composition / Rhythm / Backing / Melody / Harmony). [CE-E1]

### Changed
- `BackingCardConfigSO.PickProgressionOverride(rng, ts, settings, verbose)` now delegates
  to `ProgressionFinder`/`PaletteSelector`; the ~250-line reflection + Tier-helper block
  (`TryExtractPaletteCandidates`, `GetMemberValue*`, `ComputeTsHeuristicMultiplier`,
  `Roulette`, ...) is deleted. Legacy `(rng)` overload and both public signatures unchanged.
- `RhythmCardConfigSO` gained TS-aware `PickPatternOverride(rng, ts, settings, verbose)`
  delegating to `PatternFinder`/`PaletteSelector`; legacy `(rng)` overload retained. The
  drum side is now TS-aware (was deferred from PCE).
- `RhythmTrackComposer.Compose` now calls the TS-aware overload
  `PickPatternOverride(pickRng, part.TimeSignature, _settings, LogEnabled)`. Pick
  precedence unchanged (patternOverride > patternPalette > TrackParameters.Pattern).
- `runtime/SSoT_Composer_Rhythm_Track.md` §3D + precedence — drum pick now TS-aware via
  the shared selector; "not TS-aware / deferred to CE-F1" note retired.
- `runtime/SSoT_Composer_Backing_Track.md` §2 — shared-selector delegation + duplicate-
  reference delta noted.
- `planning/active/Roadmap_Composition_Expressivity.md` — CE-E1 and CE-F1 marked DONE;
  live/inert "correction" retired (resolved); "no clone affordance" line updated.
- `ssot_manifest.yaml` — flipped the TS-toggle-asymmetry invariant; added shared-selector
  + drum-density invariants; added `PaletteSelection.cs` to both composer governs.
- `coverage-matrix.md` — drum-palette row + PCE note updated for the resolved asymmetry.

### Corrected / resolved
- The PCE-era TS-toggle asymmetry (chord LIVE / drum INERT) is **resolved**: both palettes
  select through one `PaletteSelector`, so `preferExact*TimeSignatureMatches` is LIVE on
  both sides.

### Known intentional deltas
- Duplicate progression references in one chord palette are now independent weighted slots
  (the old reflection path de-duped via GroupBy-max-weight). For palettes without duplicate
  references, chord picks are byte-identical for the same seed.
- Tier C (raw-weights fallback) is an unreachable defensive guard under positive
  weights/multipliers — documented, not unit-tested.
- Drum vs chord density is asymmetric by design: drums cap foundational-onset density at the
  meter's grouping count (only under-articulation penalized); chords penalize both
  directions (D-F1.5).

## 2026-06-04 — PCE: drum-palette consumption + distinctness experiment

### Added
- `Resources/ScriptableObjects/Drums/` — 5 `DrumPatternData` assets
  (Four-on-the-Floor, Waltz-Pulse Lilt, Compound Swing, Odd-Meter Angular,
  Syncopated Pocket).
- `Resources/ScriptableObjects/Drums/Palettes/` — 5 `DrumPatternPaletteSO`
  assets (one pattern each, weight 1.0).
- `Documentation~/planning/active/Roadmap_Composition_Expressivity.md` — successor
  arc roadmap (CE-E1 / CE-F1 / CE-L1).

### Changed
- `RhythmCardConfigSO` — added `patternPalette : DrumPatternPaletteSO` and
  `PickPatternOverride(System.Random)`; priority patternOverride > patternPalette
  > null. Mirrors `BackingCardConfigSO.PickProgressionOverride(rng)`.
- `RhythmTrackComposer.Compose` — pattern resolution now calls
  `PickPatternOverride(pickRng)` seeded from `ctx.rng`, with a deterministic
  `defaultSeed` fallback when `ctx.rng` is null and a palette is present.
- `runtime/SSoT_Composer_Rhythm_Track.md` — added §3D palette consumption
  contract; precedence list now includes `patternPalette` at priority 2.
- `ssot_manifest.yaml` — flipped the "no runtime caller consumes it yet"
  invariant; added the TS-toggle-asymmetry invariant; registered the CE roadmap;
  moved the design doc to `reference/package/` (PROPOSAL → GOVERNED). Palette/
  pattern `.asset` instances are not added to `governs:` because `**/*.asset` is
  globally excluded and assets are not text-auditable.
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` —
  added §1.3 card→palette identity table (mirror of ALWTTT-owned assignment);
  updated §4.4 to add `patternPalette` and drop the stale "wiring incomplete /
  composer SSoT pending" notes.

### Corrected (carried from PCE findings)
- TS-toggle asymmetry: `preferExactTimeSignatureMatches` is LIVE on the chord
  palette (via `PickProgressionOverride`'s TS-aware overload) but INERT on the
  drum palette. The kickoff brief's "inert on both" was wrong. Unification = CE-F1.
- Palette asset path is `Resources/ScriptableObjects/Drums/Palettes/`, not the
  design doc §9's `Patterns/Drums/Palettes/`.

### Validated
- PCE §5 distinctness experiment: two 4/4 cards distinct by palette alone.
  Smoke pass: determinism PASS, consumption PASS, distinctness PASS.

## 2026-05-29 — Batch L4: chord editor LLM generalization (LLM Authoring MVP complete through L4)

### Added
- `Runtime/CoreScripts/Composition/Data/ChordGenreVocabularySO.cs` — chord
  analogue of `RhythmGenreVocabularySO`; `genres[]` + `TryResolve` +
  `ChordSubStyleCue`, with chord-domain members (characteristic Roman-string
  progressions, voicing hints, cadence cues, `measuresOverride`).
- `Editor/ChordProgressionLLMPromptBuilder.cs` — pure-function system+user
  prompt builder. DSL alphabet verified against `RomanProgressionParser`;
  forbids extended/slash chords; dot-decimal durations; exact-length
  reinforcement (D-L4.4).
- `Editor/ChordProgressionLLMGenerator.cs` — generator wrapper over LLM Core
  `PromptExecutionHelper` with injectable `ILLMClient`; extracts the fenced
  Roman block and parses via `RomanProgressionParser`.
- `Editor/ChordProgressionEditorImporter.cs` — pure-function importer for the
  setup-card + Roman-block payload (single progression string, no lanes/aliases);
  CRLF-safe; line-anchored setup-field parsing.
- `Editor/ChordProgressionLLMResponseHandler.cs` — async unify point for
  generate + import; carries the D-L4.5 token-allowlist guard.
- `Editor/ChordLLMFieldPlan.cs` — pure outcome→field decision extracted from the
  window wiring for testability (D-L4.7).
- `Editor/ChordGenreVocabularyBuilder.cs` — menu-item seeder writing
  `Default Chord Genres.asset` (v1 set: jazz, pop, blues, folk) with a build-time
  parser+guard self-check so no malformed anchor can ship (D-L4.8).
- `Editor/AssemblyInfo.cs` — `InternalsVisibleTo("MidiGenPlay.Tests.Editor")`
  for the Editor assembly (D-L4.6), enabling direct unit tests of editor-side
  internals (e.g. the chord guard helper).
- Editor wiring: `ChordProgressionEditorWindow.LLM.cs` partial — LLM panel
  (vocabulary + client-override fields, genre/sub-style/measures/free-text,
  cost cap, Generate/Regenerate/Import), async non-blocking; plus a
  "Create New Progression" working-copy reset affordance.
- Tests (`Tests/Editor/`): `ChordProgressionLLMPromptBuilderTests` (11),
  `ChordProgressionEditorImporterTests` (9),
  `ChordProgressionLLMGeneratorTests` (6, `FakeLLMClient`-driven),
  `ChordProgressionLLMResponseHandlerTests` (13, incl. guard),
  `ChordProgressionEditorWindowWiringTests` (8). 47 chord LLM tests; full
  EditMode suite green. Manual smoke tests CSMR-S1..S8 pass.

### Modified
- `Editor/ChordProgressionEditorWindow.cs` — class made `partial`; LLM panel +
  "Create New Progression" button calls added (the implementation lives in the
  `.LLM` partial). No change to the existing parse/apply pipeline; LLM outcomes
  route through the existing `ParseAndPreview`/`ApplyToAsset` path.
- `Editor/DrumPatternLLMPromptBuilder.cs` — D-L4.4 backport: one exact-length
  reinforcement sentence in the system prompt, keeping the two builders aligned.
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 now lists the chord adopter
  with its stage→artifact mapping; §3.3 gained the degrade-vs-fail enforcement
  nuance (parser warns-and-downgrades ⇒ guard moves to the response handler).
- `ssot_manifest.yaml` — chord LLM artifacts added to the LLM SSoT `governs`;
  new degrade-vs-fail invariant.
- `coverage-matrix.md` — LLM cross-cutting row now cites the chord Roman DSL
  authority; milestone-plan row retired to closed historical; L4 closure note.

### Authority / semantics
- Determinism invariant untouched: the chord asset remains the seam, consumed
  deterministically by `ChordTrackComposer`. No LLM call sits on a compose path.
- New contract clarification (not a new contract): "no silent fallback" is
  enforced at the response-handler layer when the domain parser degrades rather
  than rejects. Documented in `SSoT_Authoring_LLM_Generation.md` §3.3.
- The LLM Authoring MVP is complete through L4. `Roadmap_LLM_Authoring_MVP.md`
  §"Batch L4" promoted from deferred sketch to closed; the roadmap is now a
  closed historical record rather than active planning.

### Decisions locked
- D-L4.1 Roman-string output · D-L4.2 vocab SO confirmed against the prompt ·
  D-L4.3 copy-then-unify (shared generic deferred) · D-L4.4 exact-length
  reinforcement + drum backport · D-L4.5 handler-side token-allowlist guard ·
  D-L4.6 Editor `InternalsVisibleTo` · D-L4.7 pure `ChordLLMFieldPlan` + wiring
  tests · D-L4.8 vocabulary builder with self-check.

## 2026-05-22 — MGP-ALWTTT-MOD-DIR-1: directional modulation hint for ChordTrackComposer

### Added
- `Runtime/CoreScripts/Composition/Data/ModulationOctaveHint.cs` — new package
  enum `ModulationOctaveHint { Auto, Up, Down }`. `Auto` is the default and
  preserves prior behavior bit-identically.
- `SongConfig.PartConfig.PreviousRootNote : NoteName?` and
  `SongConfig.PartConfig.ModulationOctaveHint` — two `[NonSerialized]`,
  transient, one-shot composer hints. Not part of persisted song state.

### Modified
- `Runtime/CoreScripts/Composition/Composers/ChordTrackComposer.cs`
  - `Compose` now captures the two transients at entry and clears them
    immediately so the hint is consumed exactly once per render.
  - Two internal render sites (authored progression path inside `Compose`;
    procedural path via `ComposeProcedural` → `RenderFromProgression`) now
    invoke a shared directional-first-chord helper when the hint is set.
  - First chord under hint != `Auto` is realized as a root-position stack at
    the directional octave (`Up` = lowest octave strictly above the previous
    root; `Down` = highest strictly below). Inversions and Drop-2 are skipped
    for the first chord only. Chords 2..N continue normal voice leading.
  - Range-limit fallback (R-A): when no octave in the instrument range
    satisfies the strict direction, the composer clamps to the boundary
    octave on the requested side and emits a warning when
    `MidiGenPlayConfig.logGenerator` is enabled.
  - Private signature changes: `ComposeProcedural` and `RenderFromProgression`
    each gained two parameters (`ModulationOctaveHint`, `NoteName?`). No
    public/interface change; `ITrackComposer.Compose` is unchanged.

### Behavior
- Default (`Auto` + null previous root): bit-identical to prior output.
- Determinism preserved: transients are now part of the input set captured at
  `Compose` entry; same seed + same inputs ⇒ same MIDI.
- SMD5 edge case (modulation lands on the previous root with a non-`Auto`
  hint): composer bumps the first chord one octave above (`Up`) or below
  (`Down`) the previous root anchor so that the authored direction always
  produces audible motion. See `runtime/SSoT_Composer_Backing_Track.md §6.2`.

### Authority changes
- `runtime/SSoT_Composer_Backing_Track.md` — new §6 "Directional modulation
  hint (one-shot transient)"; prior §6 "Update triggers" renumbered to §7.
- `runtime/SSoT_Runtime_Song_Model_and_Config.md` — new §1.1 "Transient
  one-shot composer hints on `PartConfig`"; §7 "Update triggers" gains a
  bullet covering transient hints.

### Cross-project notes
- ALWTTT's `ModulationEffect` lives in `ALWTTT.Cards` (not in the package).
  ALWTTT-side adoption is a follow-up batch: add an `octaveHint` field on the
  `ModulationEffect` SO and write `PartConfig.PreviousRootNote` +
  `PartConfig.ModulationOctaveHint` in the effect's apply path before render.
  See the rehydration prompt produced at MGP-ALWTTT-MOD-DIR-1 closure.
- Smoke testing deferred to ALWTTT-side scene smoke per the batch decision
  (F3 = (c)). No package-side test harness was added.

### Not changed
- `IChordVoicer` interface and `BasicVoiceLeadingVoicer` semantics.
- `VoiceLeadingConfig` shape.
- Pre-existing inconsistency: the procedural-path render site applies
  `degreeAccidental` while `RenderFromProgression` does not. Out of scope
  for this batch; surfaced for the record.

---

## 2026-04-12 — ssot-drift-auditor remediation batch

### Deleted: arrangement mutator / post-processor / personality cluster

**Affected code (deleted):**
- `Runtime/CoreScripts/Composition/Mutators/AlternateTrackMutator.cs`
- `Runtime/CoreScripts/Composition/Mutators/IntroMutator.cs`
- `Runtime/CoreScripts/Composition/Mutators/OutroMutator.cs`
- `Runtime/CoreScripts/Composition/Mutators/SoloMutator.cs`
- `Runtime/CoreScripts/Interfaces/IArrangementMutator.cs`
- `Runtime/CoreScripts/Composition/Post Processors/HumanizationPostProcessor.cs`
- `Runtime/CoreScripts/Composition/Post Processors/TempoScalePostProcessor.cs`
- `Runtime/CoreScripts/Interfaces/IMidiPostProcessor.cs`
- `Runtime/CoreScripts/Interfaces/IMixController.cs` — **retained**
- `Runtime/CoreScripts/Interfaces/IMusicianPersonality.cs`
- `Runtime/CoreScripts/Composition/Personalities/NeutralPersonality.cs`

**Reason:** The entire mutator/post-processor/personality pipeline was implemented but never
governed by any package SSoT. The pipeline was not on the active roadmap and was explicitly
marked "unrouted legacy" in coverage-matrix.md. All references removed from `MidiMusicManager.cs`.
`IMixController` was retained — it is actively used for channel volume and highlight management.

**Governance changes:**
- `coverage-matrix.md` — removed row: "Arrangement mutator pipeline (`IArrangementMutator`, `AlternateTrackMutator`)"
- `ssot_manifest.yaml` — removed stale invariant referencing `IArrangementMutator`; cleaned governs of melody authoring SSoT entry

---

### Clarified: `SSoT_Authoring_Melody_Composition.md` scope boundary

**Change:** Added a status note to the top of the document making explicit that:
- The described authoring concepts (phrase palettes, `MelodicLeadingConfig`, `MelodicStyleSO`) are current implemented truth.
- `MelodyPatternData` canonical redesign, `MelodyGenerationParamsSO`, and the authoring wizard are **not yet documented here** — they are planned in `Roadmap_Melody_Authoring_MVP.md` Phase 1.

**Reason:** The doc had no "what is NOT true yet" section equivalent to `SSoT_Authoring_Rhythm_Patterns.md`.
This created an asymmetry that could mislead a reader into treating planning material as current truth.

**Authority unchanged:** The doc remains primary authority in `authoring/`. No promotion or demotion.

---

### Fixed: cross-project reference index link rot

**File:** `reference/cross-project/ALWTTT/SSoT_CompositionSystem_INDEX.md`

**Change:** Section 3 updated to use correct MidiGenPlay package doc names:
- `SSoT_Composer_BackingChordTrack_v1.md` → `Documentation~/runtime/SSoT_Composer_Backing_Track.md`
- `SSoT_Composer_RhythmTrack_v1.md` → `Documentation~/runtime/SSoT_Composer_Rhythm_Track.md`
- Bassline, Melody, Harmony entries clarified as "no package SSoT yet"

**Authority unchanged:** This file is and remains a cross-project reference, not package authority.

## 2026-03-20 — Phase 6 complete: StepState data model and row-local velocity view

### Data model change
- `DrumPatternData.Lane.steps` promoted from `List<bool>` to `List<StepState>`
- `StepState { bool active; int velocity; }` is the new canonical per-step representation
- Sentinel contract: `velocity == 0` means defer to lane `defaultVelocity`; `1–127` is an explicit per-step override
- `StepState.ResolveVelocity(int laneDefault)` encapsulates effective velocity resolution
- `StepState.Off` and `StepState.On(int vel)` are the canonical construction helpers

### New API
- `DrumPatternData.SnapshotAsStepVelocities()` — per-step-velocity-aware snapshot for future runtime consumption
- `DrumPatternData.SnapshotAsIndices()` return signature **unchanged** — existing runtime callers unaffected

### Editor update
- `DrumPatternEditorWindow` gains per-row `[T]`/`[V]` mode toggle
  - Trigger mode: boolean step buttons (behavior unchanged from Phase 5)
  - Velocity mode: per-step int fields; 0 = deactivate; >0 = activate with explicit velocity; `[clr]` resets overrides
  - Row view mode is editor UI state only; not persisted in asset

### Compile-fixes (no behavioral change)
- `RhythmTrackComposer.NormalizeGridPatternForPartIfNeeded`: `List<bool>` → `List<StepState>`, step reads updated to `.active`, per-step velocity preserved during normalization
- `RhythmPatternPanelController`: `lane.steps[s]` bool reads/writes → `StepState` equivalents; toggle-off preserves existing step velocity

### Migration note
- Existing `DrumPatternData` `.asset` files serialized with `List<bool>` steps will have empty lane step arrays after this change. Assets require re-authoring via `DrumPatternEditorWindow` or manual migration. This is an accepted consequence of the data-model promotion.

### Authority changes
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` Section 2 rewritten: `StepState` is now canonical persisted truth; migration note added
- Section 4: per-step velocity removed from "not yet true" list; runtime per-step velocity consumption remains deferred
- Section 5: Phase 6 velocity view documented as implemented
- Section 8: Phase 6 marked complete in sequencing

### Not changed
- `runtime/SSoT_Composer_Rhythm_Track.md`: `ComposeFromGrid` still uses `SnapshotAsIndices`; per-step velocity in generated MIDI is a deferred runtime change
- `coverage-matrix.md`: primary home for rhythm authoring was already `authoring/SSoT_Authoring_Rhythm_Patterns.md`

---

## 2026-03-20 — Phase 5 complete: DrumPatternEditorWindow promoted to primary rhythm authoring tool

### Added
- `DrumPatternEditorWindow.cs` — dedicated package-owned Unity Editor window for rhythm pattern authoring
  - scene-independent (no runtime MonoBehaviour wiring)
  - follows `ChordProgressionEditorWindow` architectural pattern
  - `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
  - explicit working copy / apply / save-as contract
  - lane management, instrument selection, step toggle grid, safe normalize/rebuild

### Authority changes
- `DrumPatternEditorWindow` is now the primary package-owned rhythm authoring entry point
- `RhythmPatternPanelController` reclassified as secondary / legacy runtime-scene panel
  - not deprecated; still valid for scene-embedded editing flows
  - no longer documented as the primary tool

### Modified
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
  - Section 3 rewritten: `DrumPatternEditorWindow` as 3A (primary), `RhythmPatternPanelController` as 3B (secondary)
  - Section 4: removed "no dedicated `DrumPatternEditorWindow`" from "not true yet" list
  - Section 8: updated sequencing to reflect Phase 5 completion
  - Added explicit normalize/apply/save contract documentation
- `authoring/SSoT_Authoring_Tools.md`
  - Section 3A: `DrumPatternEditorWindow` added alongside `ChordProgressionEditorWindow` as Category A tool
  - Section 3B: `RhythmPatternPanelController` reclassified as legacy runtime-scene MVP
  - Section 5: "current truth" updated to reflect `DrumPatternEditorWindow` capabilities
  - Section 9: sequencing updated to reflect Phases 4–5 as done
- `CURRENT_STATE.md`
  - Phase 5 moved from Blocked to Just Completed
  - Next steps updated to Phase 6 data-model decision as immediate priority

### Notes
- Per-step velocity remains outside current persisted truth; data-model decision is the Phase 6 gate
- `DefaultSaveFolder` hardcoded in `DrumPatternEditorWindow`; Phase 8 will route through package store abstractions
- `coverage-matrix.md` does not require update: primary home for rhythm authoring tooling was already `authoring/SSoT_Authoring_Tools.md`

---

## 2026-03-18 — Rhythm semantic refinement against codebase

### Clarified
- The active rhythm runtime truth is the `SongConfig` / `SongOrchestrator` / `RhythmTrackComposer` stack, not the older `MIDISong` / `MIDIGeneratorManager` branch.
- Rhythm runtime already supports deterministic procedural generation, grid-authored `DrumPatternData`, legacy compatibility, and Part-meter normalization.
- MidiGenPlay already has a real rhythm authoring MVP through `RhythmPatternPanelController` + `PatternGrid` + `RhythmRowHeader`; grid authoring is not merely future intent.

### Re-sequenced
- Rhythm planning now explicitly prioritizes dedicated authoring/tool consolidation before closing phrasing / feel semantics.
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` now treats phrasing / feel knobs as a later organic-variation milestone rather than a blocker before authoring work.

### Added planning clarity
- Captured the future rhythm-editor UI target as a row-based trigger grid plus row-local velocity-edit view.
- Explicitly documented that per-step velocity is not part of current persisted package truth and would require a canonical data-model extension before promotion.

### Authority adjustment
- `coverage-matrix.md` now distinguishes between current rhythm authoring truth and the still-planned rhythm editor interaction model.

## 2026-03-18 — Documentation governance migration bootstrap

### Added
- Root governance spine:
  - `README.md`
  - `SSoT_INDEX.md`
  - `SSoT_CONTRACTS.md`
  - `coverage-matrix.md`
  - `CURRENT_STATE.md`
  - `changelog-ssot.md`
- New `runtime/` SSoTs
- New `authoring/` SSoTs
- New folder READMEs for `runtime/`, `authoring/`, `reference/`, `planning/`, `research/`, and `archive/`

### Reclassified
- Moved ALWTTT-specific documents under `reference/cross-project/ALWTTT/`
- Reclassified `ALWTTT_MidiGenPlay_Rhythm_MVP_Roadmap.md` as active package planning and renamed it to `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- Reclassified research prompts/design notes under `research/`
- Reclassified legacy package docs as absorbed source material under `archive/absorbed/`
- Reclassified `CardData_Redesign.md` as historical under `archive/historical/`

### Authority changes
- Package authority is now split by responsibility instead of being mixed in root legacy docs
- Cross-project integration docs no longer compete with package truth
- Rhythm authoring is now treated as an immediate package priority in `CURRENT_STATE.md`

### Notes
This change is a documentation governance migration and folder restructuring pass.
It preserves source material rather than deleting it.

## 2026-03-19 — Hardening micro-pass for cross-project ALWTTT references

### Modified
- `runtime/SSoT_Runtime_Song_Model_and_Config.md`
- `reference/cross-project/ALWTTT/README.md`
- `reference/cross-project/ALWTTT/SSoT_Runtime_CompositionSession_Bridge.md`
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
- `reference/cross-project/ALWTTT/SSoT_CompositionSystem_INDEX.md`

### Key hardening decisions
- clarified that `SongConfig` / `SongConfigManager` are the **package-side runtime truth after handoff**, not a replacement for a consumer project's game-side editable/session truth
- hardened the status of ALWTTT cross-project docs as **reference only**, not package authority
- made the primary-home rule more explicit to reduce documentary drift between MidiGenPlay and ALWTTT

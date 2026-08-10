# PENDING_DOC_DIFFS

> **Accumulator status: EMPTY.**
> Closed at **DOC-SWEEP-2 (2026-08-08)**, reopened and swept in the same pass
> with one entry (MGP-TRIAGE-ALWTTT-R3). Previously closed at DOC-SWEEP-1
> (2026-08-05) with five entries. Both closure records are retained below as
> history, not as outstanding work.

## What this file is

A staging area for documentation diffs that have been DRAFTED but not yet
APPLIED to their governed documents. It exists so that a batch that closes on
code can defer its documentation sweep without losing it, and so that a later
documentation batch can apply the backlog in one pass and in order.

**Rule:** an entry lives here only while it is unapplied. Once applied, it is
marked APPLIED with its date and removed at the next accumulator close. An entry
that is still listed as pending while its target document already carries the
change is a STALE COPY, not drift — see the note below.

## Closure record — DOC-SWEEP-2 (2026-08-08)

Reopened and closed in one pass. Documentation only, zero code.

| # | Batch | File | Status |
|---|---|---|---|
| 1 | MGP-TRIAGE-ALWTTT-R3 | `MGP-TRIAGE-ALWTTT-R3_doc_diffs.md` | **APPLIED 2026-08-08** (DOC-SWEEP-2) — §1–§7 in file order; §7 applied amended, see notes |

The entry declared it depends on nothing and shares no anchor with any other
drafted batch. Verified: the stack was empty on arrival, and all six anchors of
§1–§6 matched their governed documents literally.

**Its own gate was discharged before applying.** The diff shipped marked
*"DRAFTED, NOT APPLIED — do not apply until the two new test files are green"*.
`PhraseArchetype_SlotBookkeepingTests.cs` and `ChordProgression_CloneIdentityTests.cs`
exist and the EditMode suite is green as of 2026-08-08, so the gate is
satisfied, not waived.

**§7 amendments, recorded rather than silent** (full text in `changelog-ssot.md`):

- **D-1=A.** `CURRENT_STATE.md`'s "Recorded gap F5" bullet asserted "No render
  impact today", which §1 of this very batch falsifies. §7 did not mention it.
  REMOVED rather than left standing — a governed document contradicting its own
  primary SSoT is the drift class this process exists to prevent.
- **D-2=A.** §7 asks for the two new test files to be added "under the melody
  composer row" and "under the backing composer row" of `coverage-matrix.md`.
  That file records test surfaces in its closure-notes section; the table's
  third column holds documents. Applied in the file's own convention. No
  primary-home flip, no row added.
- **Duplication observed, NOT repaired.** `CURRENT_STATE.md` carries a verbatim
  double-write from DOC-SWEEP-1: the MEL-1b "Just completed" entry appears
  twice, and so did the three blocked-list bullets §7 targets. Both copies were
  edited identically, so no information was lost and no anchor was silently
  resolved to one of two. **De-duplicating the region is unspecified structural
  surgery and was deliberately not attempted here.** Flagged for the owner.

### Correction to the DOC-SWEEP-1 record, verified against the sources

The entry-3 row below read *"§A done; §B/§C land at the governance sweep"*.
Re-derived against the governed files on 2026-08-08:

| ARTIC-RATE-1 part | Target | Evidence | Verdict |
|---|---|---|---|
| §B.1 — extend the CA-V1 "Seeded variation" invariant | `ssot_manifest.yaml` | the invariant carries the `MGP-ARTIC-RATE-1: the two sentinels resolve INDEPENDENTLY…` block with the F-ARTIC-RATE-GRID-1..3 record | **APPLIED** |
| §C — changelog entry | `changelog-ssot.md` | `## 2026-08-03 — MGP-ARTIC-RATE-1: rate sentinel suppressed the authored figure` | **APPLIED** |
| §B.2 — header log entry | `ssot_manifest.yaml` | **no 2026-08-03 entry exists**; the header log runs 2026-07-29 → 2026-08-05. The only trace is the DOC-SWEEP-1 entry recording that the batch was applied. §B.2's own content — no `governs:` change, `ChordTrackComposer_ArticRateIndependenceTests.cs` deliberately unlisted per the MEL-BEATUNIT-1 convention, the consumer-impact and handoff note — is nowhere in the manifest | **NOT APPLIED** |

The row is therefore corrected to **APPLIED EXCEPT §B.2**, not to plain
APPLIED. The missing entry was **not** written here: ARTIC-RATE-1 is one of the
five diffs swept at DOC-SWEEP-1, and re-applying a swept diff is outside this
batch's scope. Recorded as an open item for whoever next touches the manifest
header — it is bookkeeping, not divergence, since §B.1 and §C carry the meaning.

### Follow-ups this batch leaves open

Recorded here for visibility and in `changelog-ssot.md` as the authority record,
per DOC-SWEEP-1 decision D-1=C. **No roadmap file was opened** — none of these
is scoped.

| Item | State |
|---|---|
| `transposeScaleSteps` (diatonic motif transposition) | open, unscheduled; **two** data points now (MEL-1b hazard note + R3 sighting) |
| `AscendingClimbMelodyStrategy` hardcodes `octs = 2` for the final cadence | recorded candidate for a style/leading parameter; E1 made its consequence audible, the hardcode is untouched |
| `ResolvedSource.TrackParameters` unreachable on the Backing path | recorded in `runtime/SSoT_Composer_Backing_Track.md` §3.1, not scheduled; fixing it changes what the readback reports, so it is a RUNTIME decision |
| Composer-level render gate for E1 | deferred as disproportionate for a one-line data fix; the archetype pin closes the defect |
| `ssot_manifest.yaml` header entry for ARTIC-RATE-1 §B.2 | open bookkeeping, see the correction table above |

## Closure record — DOC-SWEEP-1 (2026-08-05)

No drafted-but-unapplied documentation diff is outstanding.

| # | Batch | File | Status |
|---|---|---|---|
| 1 | MGP-ALWTTT-BASS-ORDER-1 + SLAPFIG-1 | `MGP-ALWTTT-BASS-ORDER-SLAPFIG-1_doc_diffs.md` | **APPLIED 2026-08-05** (DOC-SWEEP-1) — one item skipped, see note |
| 2 | MGP-ALWTTT-BASS-SLAPFIG-2 (+2b) | `MGP-ALWTTT-BASS-SLAPFIG-2_doc_diffs.md` | **APPLIED IN PART 2026-08-05** (items 1a/1b/1c; 3a + 4a land amended at entry 4; §1d/§1e/§2/§5a superseded) |
| 3 | MGP-ARTIC-RATE-1 | `MGP-ARTIC-RATE-1_doc_diffs.md` | **APPLIED EXCEPT §B.2** — corrected 2026-08-08 at DOC-SWEEP-2 against the sources. §A, §B.1 and §C are in their governed documents; the §B.2 manifest header entry was never written. The old wording ("§B/§C land at the governance sweep") was wrong on both counts |
| 4 | MGP-ALWTTT-BASS-BEND-1 | `MGP-ALWTTT-BASS-BEND-1_doc_diffs.md` | **APPLIED 2026-08-05** (DOC-SWEEP-1) — §6a applied amended, see note below |
| 5 | MGP-MEL-1b | `MGP-MEL-1b_doc_diffs.md` (v2) | **APPLIED 2026-08-05** (DOC-SWEEP-1) |

**Application order is not cosmetic.** Entries 1 → 2 → 4 are one dependent
chain on `runtime/SSoT_Composer_Bass_Track.md` §3.7.x and MUST be applied in
that order. Entry 4 SUPERSEDES part of entry 2 (D-DOC-SEQ=B): entry 2's
§3.7.3, its §5 trigger, its `SSoT_CONTRACTS.md` no-op note and its
`PENDING_DOC_DIFFS.md` item are skipped. When entry 2 is swept it is marked
**APPLIED IN PART**, never plain APPLIED. Entry 3 is independent (backing
composer §8) and may be applied at any point in the pass. **Entry 5 is applied
LAST**: it shares `SSoT_Composer_Backing_Track.md` with entry 3 and
`SSoT_Runtime_Generation_Orchestration.md` with entry 1, and its own anchors
are additive sections that survive those earlier edits.

*Amendment note (D-5, DOC-SWEEP-1).* The replacement table drafted in
`MGP-ALWTTT-BASS-BEND-1_doc_diffs.md` §6a listed four entries; it predates
MGP-MEL-1b. The table above is that block amended to five rows and reordered
so the row numbers match application order. Applying §6a verbatim would have
written an accumulator that was wrong on arrival.

### Stack-base verification (DOC-SWEEP-1, 2026-08-05)

Entries 1 and 4 declare that they stack on POCKET-1 v2, POCKET-2 and
SOLO-1/RUNTIME-REQUALITY, whose application state this file did not record.
Verified **against the governed documents themselves**, which are authority
where this file is not:

| Prerequisite | Evidence | Verdict |
|---|---|---|
| MGP-ALWTTT-BASS-POCKET-1 | `runtime/SSoT_Composer_Bass_Track.md` §3.7 | APPLIED |
| MGP-ALWTTT-BASS-POCKET-2 | `runtime/SSoT_Composer_Bass_Track.md` §3.7.1 | APPLIED |
| MGP-ALWTTT-BASS-SOLO-1 | `runtime/SSoT_Composer_Bass_Track.md` §1, host-default block | APPLIED |
| RUNTIME-REQUALITY | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.5 | APPLIED |

Corroborated by `coverage-matrix.md`, which records the three batches as
applied in one pass by B0 — DOC-CLOSE (2026-07-26). The stack base is sound;
the gap was bookkeeping in this file, not divergence in the documents.

### Known stale copies (decision M-5 pattern, not drift)

Seven `*_doc_diffs.md` files still in circulation read "NOT applied" while
their target documents already carry the change: `CA-V1-part2`,
`BASS-WALK-1`, `CA-T2-BOSSA-V2`, `INST-WIZ-1`,
`MGP-ALWTTT-BASS-POCKET-1`, `MGP-ALWTTT-BASS-POCKET-2`,
`MGP-ALWTTT-BASS-SOLO-1_RUNTIME-REQUALITY`. Verified by presence of their text
in the governed SSoTs (2026-08-05). They are historical records; do not
re-apply them.

An eighth file is in the same category for **code**, not documentation:
`MGP-ALWTTT-BASS-BEND-1_code_diffs_BassTrackComposer.md`. Its changes are
already in the shipped `BassTrackComposer.cs` (`BuildLegatoCarrierMap`,
`ResolveLegatoGroupEndBeats`, `ResolveLegatoDeltaSemitones`, and the
`PitchBendWriter.ApplyStepGestures` call site), with `PitchBendWriter.cs`
present in `MidiGenPlay.Composition`. Verified 2026-08-05. It is a record of
what BEND-1 changed, not outstanding work.

## Earlier closure records

- **Entries 1–6 — APPLIED 2026-07-27.** These were the documentation diffs of
  **B1 (HARMONY-PURE-1)**, **B2 (TONFILTER-1)** and **B3 (BASS-REG-1 + WALK-2)**.
  They were applied over 8 governed documents on 2026-07-27; this accumulator was
  simply not swept at that closure, which is what made the copy in circulation
  read "NOT applied" while every target document already carried the change.
  Recorded as **decision M-5 (2026-07-28): stale copy, not drift.**
- **Manifest.** `ssot_manifest.yaml` was brought in line separately on
  2026-07-28 (manifest-only remediation session).
- **B4 — DOC-CLOSE-2 (2026-07-28)** applied its own nine corrections directly to
  their governed documents and deferred nothing, so it opened no accumulator of
  its own. Its record lives in `changelog-ssot.md` (entry 2026-07-28) and in the
  `ssot_manifest.yaml` header log, not here.
- With the `changelog-ssot.md` and `coverage-matrix.md` sweeps applied at B4,
  **B1 / B2 / B3 is complete by the `SSoT_CONTRACTS.md` §9 update completion
  contract.**

## Reading discipline

This file is **not authority**. It never defines package truth and it never
overrides a governed document. Authority order is `SSoT_INDEX.md`; the primary
SSoTs in `runtime/` and `authoring/` win. If this file and a governed document
disagree, the governed document is right and this file is stale.

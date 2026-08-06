# PENDING_DOC_DIFFS

> **Accumulator status: EMPTY.**
> Closed at DOC-SWEEP-1 (2026-08-05). Reopened earlier the same day with five
> entries — the previous "EMPTY / closed at B4" banner had been stale since
> MGP-ARTIC-RATE-1 — and swept entry by entry as the pass applied them. All
> five are now APPLIED (entry 2 APPLIED IN PART by design). The table below is
> retained as the closure record of that pass, not as outstanding work.

## What this file is

A staging area for documentation diffs that have been DRAFTED but not yet
APPLIED to their governed documents. It exists so that a batch that closes on
code can defer its documentation sweep without losing it, and so that a later
documentation batch can apply the backlog in one pass and in order.

**Rule:** an entry lives here only while it is unapplied. Once applied, it is
marked APPLIED with its date and removed at the next accumulator close. An entry
that is still listed as pending while its target document already carries the
change is a STALE COPY, not drift — see the note below.

## Closure record — DOC-SWEEP-1 (2026-08-05)

No drafted-but-unapplied documentation diff is outstanding.

| # | Batch | File | Status |
|---|---|---|---|
| 1 | MGP-ALWTTT-BASS-ORDER-1 + SLAPFIG-1 | `MGP-ALWTTT-BASS-ORDER-SLAPFIG-1_doc_diffs.md` | **APPLIED 2026-08-05** (DOC-SWEEP-1) — one item skipped, see note |
| 2 | MGP-ALWTTT-BASS-SLAPFIG-2 (+2b) | `MGP-ALWTTT-BASS-SLAPFIG-2_doc_diffs.md` | **APPLIED IN PART 2026-08-05** (items 1a/1b/1c; 3a + 4a land amended at entry 4; §1d/§1e/§2/§5a superseded) |
| 3 | MGP-ARTIC-RATE-1 | `MGP-ARTIC-RATE-1_doc_diffs.md` | **APPLIED 2026-08-05** (DOC-SWEEP-1) — §A done; §B/§C land at the governance sweep |
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

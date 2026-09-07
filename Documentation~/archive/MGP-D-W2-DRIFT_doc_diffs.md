# MGP-D-W2-DRIFT — proposed documentation diffs

**Status: APPLIED IN THE COPIES SHIPPED WITH THIS RECORD. Cleared to apply.**
**Batch:** MGP-D-W2-DRIFT — the improvised walk's downward drift, cured
(2026-09-02). Closed on DoD 1–5; DoD 3's residual
reds are root-caused to a MELODY defect (F-MEL-DEGRADE-1) outside this batch's
scope — see below.
**Depends on:** DOC-SWEEP-3 (2026-09-01), which applied the TONALITY-1 and
TONALITY-2 backlog and renamed Bass §3.6.1 → §3.6bis. Verified present before
these diffs were written; all anchors below resolve in §3.6bis.

---

## DoD 3 resolved — F-MEL-DEGRADE-1, and these diffs are CLEAR TO APPLY

`tonality_matrix_20260902_141144`: 476/476, 0 failures, `beliefDiv == 0`, and
**80 residual reds across 70 cells** where MGP-TONALITY-2 recorded 0. Root-caused
by re-running cell **#157** verbose — Ionian / 4-4 / `Prog_Min_Napolitana_bII` /
`tracks=M`, a MELODY-ONLY cell with no bass track:

    OUT-OF-KEY track=Melody bar=3 note=F5 chord=[GSharp C DSharp]
    origin=strategy+motif source=Phrase_Anthem_FlowDense

**F-MEL-DEGRADE-1.** `MelodicStyle_Showtime` is correctly authored to
`transposeMode = ScaleDegrees` — confirmed in the inspector — so this is not the
old S1. The defect is the degrade branch of
`ConstrainedMelodyStrategy.TransposeByScaleDegrees`: a motif note absent from
`degreeLookup` falls through to
`MelodyStrategyCommon.Transpose(src, degreeSteps)`, handing a DEGREE count to a
SEMITONE shift. Traced from the log: D#5 (chord tone of the Napolitan ♭II
`[G# C D#]`, `degree=-1` in G Ionian) + 2 ⇒ F5, neither scale tone nor chord
tone. Triggers on any progression with a chord chromatic to the part tonality.

The planned `WalkPullMax = 0.0` control was rendered unnecessary: a melody-only
cell cannot be affected by a bass-only change. **This batch is cleared on direct
evidence.** F-MEL-DEGRADE-1 is registered in `CURRENT_STATE.md` and the
changelog; its FIX and its Melody-SSoT documentation belong to a follow-up
melody batch and are deliberately not written here.

Not established, and left open: why TONALITY-2 recorded 0 residual reds on the
same progressions. Candidates are a different Melody template row in the swept
setup, or a different classification. The follow-up should settle it rather than
assume.

---

## Diff 1 — `runtime/SSoT_Composer_Bass_Track.md` §2, after the register-ceiling clause

INSERT a **Playable floor (D-LOWNOTE=A / D-LOWSCOPE=C)** block: `lowestNote`
semantics, note-granularity rationale (open E = 28 is not an octave boundary),
scope across every emission path, the sentinel, the ceiling-wins rule, and the
**F-W2-FLOOR-SUBINST-1** finding with its measured evidence (`Slap Bass 2` band
floor 24 versus open E 28; a real render held D#0 = 15 for three bars). Closes
with a **governance note**: `MIDIInstrumentSO.cs` is governed by no SSoT, this
section documents the bass's READING of the field only, and ownership of the
type is an open question this batch does not resolve.

## Diff 2 — `runtime/SSoT_Composer_Bass_Track.md` §3.6bis, the D-W2-DRIFT block

REPLACES the "D-W2-DRIFT (open)" paragraph and amends the two around it.

- The "Why the floor exists" paragraph keeps its narrative but gains a
  **correction**: the ≈ −0.6 st/hit figure was the first-step expectation from
  the ROOT, not the stationary drift (≈ −0.13), and its sign is
  chord-dependent. The defect was variance, not bias.
- New **D-W2-PULL** clause: the formula, the reason it acts on the thresholds
  and not the candidate order (with the measured 61.7% → 58.3% no-op result of
  the rejected re-ordering design), `WalkPullMax = 0.30` and its kill switch,
  and the calibration.
- New **limitation** paragraph on seventh chords (22.4% → 10.7% aggregate,
  versus 32.5% → 2.5% on triads alone).
- New **F-W2-DESCBIAS-1** and **F-W2-EVENTSEAM-1** clauses.
- **Scope** rewritten: `BuildWalkVoicing` is untouched *because it shares no
  helper* with `BuildWalkLine` — verified in source, not assumed — and the §2
  playable floor is called out as the one law that DOES cross both walk modes.

## Diff 3 — `runtime/SSoT_Composer_Bass_Track.md` §3.6bis, test surface

Amends the closing test-surface paragraph to register the three new pins and to
state explicitly that no previously pinned assertion was changed or re-pinned.

## Diff 4 — `CURRENT_STATE.md`

- New "Just completed" entry at the top of that section, per this file's
  newest-first convention.
- The TONALITY-2 bullet `**D-W2-DRIFT deferred**` is marked **superseded** in
  place rather than deleted, with a pointer to the resolution and the explicit
  DoD-5 verdict: **the floor STAYS** as defence in depth, because the pull is
  statistical and the fold is a hard guarantee.
- Records the correction of the TONALITY-2 handoff claim that the smoke bass
  row is `RepeatedNote` (it is `ChordToneWalk`).

## Diff 5 — `changelog-ssot.md`

New dated entry at the head. Carries the two baseline corrections, the reason
options A and D were rejected on listening evidence rather than taste, the
full evidence set, the F-MTX-MELODY-1 caveat with its undone control, the four
findings opened, and the sevenths limitation.

## Diff 6 — `coverage-matrix.md`

- Row "Bass register contract" extended with the playable floor.
- Row "Improvised walking bass" extended with D-W2-PULL and the two open
  findings.
- New closure note in the D-2=A convention registering the three pins, and
  recording a **coverage gap**: `lowestNote` has no test of its own, because
  every shipped asset sits at the sentinel and only the no-op path is
  exercised.

## Not changed, deliberately

- `ssot_manifest.yaml` — no governs list changes. `MIDIInstrumentSO.cs` was NOT
  added to the Bass SSoT's governs: it is read by the chord and melody
  composers too, and claiming it for the bass would be an authority grab. The
  precedent followed is §2's existing treatment of `octaveMin`/`octaveMax` —
  document the reading, not the type.
- `SSoT_INDEX.md` — no authority change.
- `SSoT_CONTRACTS.md` — the determinism contract is untouched; the pull adds no
  `ctx.rng` draw and no state.

---

## Registration block for `Documentation~/planning/PENDING_DOC_DIFFS.md`

Append verbatim:

    - **MGP-D-W2-DRIFT** (2026-09-02) — walk drift cured (D-W2-PULL) +
      `MIDIInstrumentSO.lowestNote` (D-LOWNOTE=A / D-LOWSCOPE=C).
      Touches: `runtime/SSoT_Composer_Bass_Track.md` §2 + §3.6bis,
      `CURRENT_STATE.md`, `changelog-ssot.md`, `coverage-matrix.md`.
      Matrix 476/476, 0 failures, `beliefDiv == 0`; 80 residual reds
      root-caused to **F-MEL-DEGRADE-1** (melody, cell #157 verbose) and
      handed on unfixed. CLEAR TO APPLY.

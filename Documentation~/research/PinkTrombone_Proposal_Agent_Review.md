# Pink Trombone Proposal — Agent Review and Accepted Decisions

**Type.** Agent review of `PinkTrombone_Performance_Backbone_Proposal.md` (same folder).
**Status.** Decisions accepted 2026-05-24. Not implementation authority. No SSoT promotion yet.
**Authority.** None. This document records decisions about a proposal; the proposal itself records the proposal. Both are research artifacts preserved for traceability.

---

## 1. What this document is

The Pink Trombone proposal arrived as a third-party technical evaluation suggesting we use an articulatory voice synthesizer as the **performance** (rendering) layer for melody-family tracks, while leaving the **composition** layer entirely unchanged. This document captures the agent review that followed, the four decisions surfaced by that review, and the user's accepted answers.

The proposal itself stays preserved verbatim alongside this file. Together they form the complete record of the evaluation as of 2026-05-24.

---

## 2. Agent assessment summary

The proposal's framing is sound:

- The composition vs performance split is correctly identified. Pink Trombone cannot serve as `IMelodyStrategy` or `PhraseArchetypeSO`. It targets the rendering boundary downstream of `MelodyTrackComposer`, preserving every invariant in `SSoT_Composer_Melody_Track.md` and `SSoT_Runtime_Generation_Orchestration.md`.
- The topology respects the §7.6 boundary discipline. The synth, voice profile SO, audio-thread wiring, and mixing concerns all live on the ALWTTT side. The package gains one interface and one struct.
- The proposal correctly identifies that `MelodyTrackComposer` step 7 currently crushes structured articulation metadata (`isAccent`, `isPhraseEnd`, `desiredContourDir`, phrase grouping) into a single 0–127 velocity number and then discards the rest. The sink interface is essentially "stop wasting this information."

Two areas were under-weighted in the original proposal:

- **Authority drift on `PerformanceSlotInfo`.** Once defined, the struct becomes a forever-contract subject to semver. The proposal does not address evolution policy.
- **Sink ordering and re-entrancy guarantees.** Not explicitly stated; consumers will need them.

---

## 3. Accepted decision bundle (locked 2026-05-24)

### D-PT-1 — Package surface

**Accepted: approve in concept, defer the schema until Phase A reports back.**

The new package interface `IPerformanceMetadataSink` and the struct `PerformanceSlotInfo` will exist. Their exact field list is not committed until Phase A has produced audible evidence that the integration is worth pursuing. This is more conservative than the proposal's recommendation (commit to fields now) and protects against schema drift if the not-started melody MVP Phases 1–5 surface emission needs we cannot anticipate today.

### D-PT-2 — Determinism scope

**Accepted: extend the invariant to the sink stream only, not to audio.**

The package promises: given the same inputs and seed, the sequence of `OnSlotRendered` calls and the contents of each `PerformanceSlotInfo` are bit-identical. Audio determinism is a property of the C# synth port plus the audio-thread wiring, both of which live in ALWTTT. The package never produces audio and will not claim that property.

### D-PT-3 — Modal-degree exposure

**Accepted: composer re-derives classification post-hoc.**

`MelodyTrackComposer` will re-derive `IsTonicDegree`, `IsCharacteristicDegree`, `IsChordTone` from the resolved note plus `TonalityProfileSO` and `degreeLookup` at slot emission time, using the existing `MelodyStrategyCommon` helpers. The `IMelodyStrategy` interface is **not** changed; existing strategies (`NearestChordToneMelodyStrategy`, `ScaleFlowMelodyStrategy`, `AscendingClimbMelodyStrategy`) are unaffected. The cost is ~10 lines of code duplication, avoided by `MelodyStrategyCommon` reuse.

When Phase B's schema is frozen (post-Phase-A), these three flags are candidates for inclusion in `PerformanceSlotInfo`. Doing it this way keeps modal-degree theory inside the package boundary rather than leaking it to ALWTTT.

### D-PT-4 — Sequencing

**Accepted: Phase A starts whenever; Phase B revisited after Phase 8 closes and Phase A reports musical results.**

Phase A is consumer-side only and does not block, nor get blocked by, any package roadmap work. It can begin at any time without coordinating with `Roadmap_Rhythm_Authoring_MVP.md` (Phase 8) or `Roadmap_Melody_Authoring_MVP.md` (Phases 1–5).

Phase B is registered on the melody roadmap as Phase D4 (deferred), with explicit gating: blocked on Phase A producing a positive verdict and sequenced after Phase 8 closes. It does not block the melody MVP and is not blocked by it.

---

## 4. Implications by phase

### Phase A — ALWTTT-side spike (not on the package roadmap)

No package change. ALWTTT builds a minimal `PinkTromboneVoicePlayer` that consumes the existing `MidiFile`, mapping only `Pitch` and `Velocity` per the proposal's §5 table. Output sounds articulate but flat (no phrase-aware articulation, no contour shaping, no modal-degree color — those need Phase B's sink). The deliverable is a listening test, not a feature.

Decision gate at end of Phase A: **does this sound good enough to be worth Phase B?** If yes → schedule Phase B per the D-PT-4 sequencing. If no → archive the proposal and this review, retain as evaluated-and-passed-on.

A separate rehydration prompt (`Phase_A_Pink_Trombone_Rehydration_Prompt.md`) defines Phase A scope, deliverables, and decision-surfacing for the ALWTTT side. That prompt is consumer-owned, not package-owned.

### Phase B — package work, deferred

Registered as Phase D4 in `Roadmap_Melody_Authoring_MVP.md`. Out of scope for this document; the roadmap entry is authoritative on the package side.

### Phases C and D — out of scope until B clears

Not registered anywhere. Re-evaluated only if Phase B lands and demonstrates Phase B's musical payoff was real.

---

## 5. Risks tracked at this evaluation tier

Not all risks from the proposal's §9 require active management at this stage. Tracked here:

| Risk | Status at 2026-05-24 |
|---|---|
| Authority drift on `PerformanceSlotInfo` (under-weighted in proposal) | Mitigated by D-PT-1: schema deferred until Phase A reports. Additive-only policy applies from Phase B onward. |
| Sink ordering / re-entrancy guarantees (under-weighted in proposal) | To be specified explicitly in the Phase B SSoT addendum. Provisional answer: invoked in deterministic slot order, on the orchestration thread, no re-entrancy. |
| CPU under chorus (proposal §9.2) | Phase A validates per-voice CPU. Multi-voice scenarios are Phase D scope; not active. |
| Polyphony / monophonic synth (proposal §9.1) | Architectural — Pink Trombone only ever covers `TrackRole.Melody` and `TrackRole.Lead`. Other roles continue through MPTK. Mixing complexity is Phase A's risk to validate. |
| Cross-phrase tract continuity (proposal §9.4) | Phase A may surface artifacts. Phase B's mapping refinements address them. Not actionable now. |
| Audio-determinism caveat re sample rate / block size (proposal §9.5) | Resolved by D-PT-2: package does not claim audio determinism. Caveat lives consumer-side. |
| Pattern-authoring roadmap interaction (proposal §9.7) | D-PT-1's deferral handles this: schema frozen only after Phase A reports, by which point pattern-path emission needs are clearer or still absent. |

---

## 6. What this document does and does not authorize

**Authorizes:**
- Preservation of the proposal and this review in `research/`.
- Registration of Phase B as Phase D4 (deferred) on `Roadmap_Melody_Authoring_MVP.md`.
- Phase A work on the ALWTTT side, with no package change required or expected.

**Does not authorize:**
- Any change to package code, package assets, or package SSoTs at this time.
- Any commitment to land Phase B; that decision waits on Phase A's verdict.
- Any addition of `IPerformanceMetadataSink` or `PerformanceSlotInfo` to the codebase.
- Any change to `CURRENT_STATE.md`, `changelog-ssot.md`, or `coverage-matrix.md`. This evaluation does not change semantics, authority, or interpretation of existing material.

---

## 7. Closing breadcrumb

If Phase A returns positive: open a Phase B rehydration prompt against this review and the proposal. The accepted decisions in §3 are inputs to that prompt; the field list for `PerformanceSlotInfo` and the SSoT addendum text are outputs.

If Phase A returns negative: this document and the proposal both remain in `research/` as preserved evaluation material. No archival or deletion action needed; they simply stop being relevant. The melody roadmap's Phase D4 entry would be amended to note the outcome.

If Phase A is never started: this evaluation is dormant but valid. The sequencing decision in D-PT-4 has no expiry.

---

## 8. Related material

- `PinkTrombone_Performance_Backbone_Proposal.md` (same folder) — the proposal under review.
- `Phase_A_Pink_Trombone_Rehydration_Prompt.md` — ALWTTT-side opening prompt for Phase A. Lives in the ALWTTT project, not this package.
- `planning/active/Roadmap_Melody_Authoring_MVP.md` Phase D4 — package-side registration of Phase B as deferred.
- `Documentation~/research/Deep_Research_Prompt_MidiMusicManager.md` — adjacent exploratory material, structurally similar governance status.
- `runtime/SSoT_Composer_Melody_Track.md` — primary package authority on melody composer behavior (unaffected by this evaluation).
- `runtime/SSoT_Runtime_Generation_Orchestration.md` — primary package authority on orchestration and determinism (unaffected by this evaluation).

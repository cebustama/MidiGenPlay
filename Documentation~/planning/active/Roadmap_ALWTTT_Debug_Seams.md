# Roadmap_ALWTTT_Debug_Seams — ALWTTT

**Status:** Planning artifact — **arc CLOSED 2026-07-17**. Planning-only; does not
define implementation truth. The governed SSoTs listed under §6 are authoritative
for everything this doc describes.
**Scope:** The ALWTTT consumer side of the **MGP-ALWTTT-DBG** arc — the debug/observability
seams ALWTTT opened over the MidiGenPlay composition boundary so that per-track
generation can be inspected and, in dev, overridden. Records what the arc was, which
batches closed it, the locked decisions, and the residuals parked at close.
**Does not own:** the package-side re-key and readback contract (MGP-ALWTTT-DBG-1 —
MidiGenPlay's own SSoTs); the ALWTTT runtime/cache invariants (Integration SSoT §8);
the Dev-Mode surface contract (`SSoT_Dev_Mode §18`).

---

## 1. Purpose of the arc

Before this arc, ALWTTT could hand a part to the package and hear the result, but had
no structured way to see **what the package resolved per track** (which palette, which
pattern, which chord timeline) or to **probe alternatives** during a playtest. Two things
blocked it: the package readback was keyed by `musicianId` alone (so a musician holding
two role-tracks collapsed to one entry — the BASS-1 constraint), and there was no
dev-facing override channel.

The arc closed both. The package re-keyed its readback and override maps by
`(musicianId, TrackRole)` and added a per-track `resolvedByTrack` snapshot plus a
trailing `patternOverrides` map (**MGP-ALWTTT-DBG-1**, package-side). ALWTTT then
adopted that composite key end-to-end and built the two-phase composition-debug tab on
top of it (**DBG-C1** read side, **DBG-C2** write side).

The arc's demo value is indirect: it does not add player-facing content. It makes the
composition pipeline **inspectable and counterfactually probeable**, which is what the
next tuning-facing work (composition-session validation, per-musician music variety) needs.

---

## 2. Arc batches

Newest-last (chronological within 2026-07-17). All consumer batches are ALWTTT-side;
the package batch is recorded for context only and is not governed here.

| Batch | Side | What it delivered | Status |
| --- | --- | --- | --- |
| MGP-ALWTTT-DBG-1 | MidiGenPlay (package) | Re-keyed `PartRender.stemsByMusician` / `melInstByMusician` and the `RenderSinglePart` override maps by `MusicianTrackKey = (musicianId, TrackRole)`; added `PartRender.resolvedByTrack`; added a trailing `patternOverrides` (step-0 precedence) map; promoted the `chd:` per-chord marker to governed contract. | closed (package) — referenced, not governed here |
| DBG-C1 | ALWTTT (consumer, read) | Adopted the composite key end-to-end (stem/bundle/part caches, `RenderSinglePart` signature, `PartCache`, `ComputeTrackInputsHashesForPart`, merged-rebuild ordering); retired the `FlattenInstrumentReport` + id→key shims and **all three BASS-1 carve-outs** (multi-track musicians cacheable again); added the `MidiMusicManager` read-only truth surface (`LastResolvedByTrack` + serial, `GetChordTimelineSnapshot()`); built `DevCompositionDebugTab` + `GenerationDebugFormatter` (two-phase intent/resolved log, `'*'` convention, Compact/Full, Copy fingerprint, seed pin, infinite composition-loop toggle). | **closed 2026-07-17** (ST-S1..S10 PASS) |
| DBG-C2 | ALWTTT (consumer, write) | Made `patternOverrides` LIVE: per-track override dropdowns (full registry, TS-filtered, off-band annotated), Roman free-text → `TryParseRoman` → never-persisted Backing override (verdict verbatim), R2a "re-render part now" (stamp-bump through the normal seeded loop). Overrides never cache-keyed (MMM stem/bundle bypass + `PartCache` stamp-invalidation); idle ⇒ byte-identical. Bassline/Harmony vetoed in-UI. A1 confirmed. | **closed 2026-07-17** (ST-C2-1..9 PASS) |

**Arc close condition met:** the consumer surface is complete (read + write), the BASS-1
package request is RESOLVED, and no further package ask is open. The write half consumed
only surfaces already recorded by DBG-C1; no MidiGenPlay file was touched in either
consumer batch.

---

## 3. What shipped (consumer surface, at arc close)

- **Composite `(musicianId, role)` keying** across the stem cache, bundle cache,
  `PartCache`, the `instrumentOverrides` argument, and `ComputeTrackInputsHashesForPart`.
  A musician holding two role-tracks now yields two independent cache identities.
- **Read-only render truth surface** on `MidiMusicManager`: `LastResolvedByTrack` /
  `LastPinnedByTrack` / `LastRenderSerial|PartIndex|Bpm|FromCache`, republished on every
  render (fresh + bundle replay), plus `GetChordTimelineSnapshot()` over the governed
  `chd:` contract. Production API; truth-only, never a gameplay input.
- **`#if ALWTTT_DEV` Composition tab** (`DevCompositionDebugTab` + `GenerationDebugFormatter`):
  two-phase intent/resolved per-track log, `'*'` resolved-only convention, Compact/Full,
  Copy fingerprint, `chd:` dump, seed pin (closed the `SSoT_Dev_Mode §8.7` debt), and the
  infinite composition-loop toggle (host hooks keep firing; CARD-UX-1 dev-exempt).
- **Interactive write half** (`#if ALWTTT_DEV`): per-track override dropdowns, Roman →
  Backing override (never persisted, verdict verbatim), R2a re-render. Overrides are never
  part of any cache key — MMM bypasses the stem/bundle caches when overrides are supplied
  (mirrors the Mod-DIR one-shot bypass) and `CompositionSession` stamp-invalidates
  `PartCache` on change; clearing restores byte-identical output.

**Backward-compatibility gate held throughout:** dev OFF (or all controls idle) ⇒
single-track output byte-identical to pre-arc; the only stem-key change is a deterministic
`:{role}` segment (ST-S1, ST-C2-7 PASS). Production builds carry none of the stamp/override
machinery.

---

## 4. Decisions ledger (locked at close — do not re-litigate)

- **D-C1-1 = A** — `patternOverrides` added to `RenderSinglePart` at C1 as an inert
  passthrough; made LIVE at C2.
- **D-C1(seed) = A** — seed field wired into the Composition tab (closed §8.7).
- **D2 = A** — infinite composition-loop keeps per-loop host hooks firing.
- **D3** — infinite-loop toggle lives in the Composition tab; CARD-UX-1 final-loop deny
  is dev-exempted under it.
- **D-C2-1 = A** — importer verdict surfaced verbatim; no ALWTTT-side reduction.
- **D-C2-2 = A** — override dropdowns from the full runtime registry, off-band annotated.
- **D-C2-3 = A** — R2a = stamp-bump re-render through the existing seeded loop (working
  path reused).
- **D-C2-4 = A** — overrides never cache-keyed; MMM bypass + `PartCache` stamp-invalidation.
- **A1 — CONFIRMED (2026-07-17)** against `Design_Composition_Debug_Tab_v0_1 §3.1`: the
  `'*'` resolved-only convention and role-adaptive line format match §3.1's intent; the
  per-field `'*'` placement is a faithful, stricter refinement of §3.1's illustrative
  whole-line sample. No code change to `GenerationDebugFormatter.IsResolvedOnly`.
- **Inherited unchanged:** D-DBG1..5, ID-1..4, E-1..E-5 (+E-1b/E-2b package asset moves,
  confirmed done).

---

## 5. Residuals parked at close

These do **not** gate arc close. They are recorded so the arc's tail is not lost.

- **DBG-OBS-1 (non-blocking).** The `RenderOverride` resolved line may omit
  `pattern=<asset>` if the package leaves `ResolvedTrackChoice.sourceAssetName`
  unpopulated on that source path (observed in ST-C2-1; the override was audible and
  correct). Localized fix if pursued: confirm the package readback for `RenderOverride`,
  and if intentionally unpopulated, have the formatter fall back to the override asset's
  `name` for that field. Override correctness is not affected. Home: `SSoT_Dev_Mode §18.8`.
- **Design-doc §4 card-injection R2a (reserved).** The design doc's §4 "debug-play any
  catalogue card's musical side" is a larger surface than the pattern-override R2a shipped
  here; it remains reserved under **M1.5 Phase 5** and is not built by this arc.

---

## 6. Governed homes (authoritative — this roadmap is not)

- Cache keying, carve-out retirement, read-only truth surface, override cache-bypass:
  `runtime/SSoT_Runtime_CompositionSession_Integration.md` §8 invariant 9 (+ §10/§11
  riders, invariant 11 dev exemption).
- BASS-1 package request → RESOLVED: `integrations/midigenplay/SSoT_ALWTTT_MidiGenPlay_Boundary.md` §4.3.
- Dev Composition tab (read + interactive) + seed pin + infinite-loop toggle:
  `systems/SSoT_Dev_Mode.md` §18 (§18.1–§18.9), §3, §6, §8.7, §9.13/§9.14.
- Operational state: `CURRENT_STATE.md` §1 (DBG-C1/DBG-C2 rows) + §2 (LIVE bullets).
- Semantic history: `changelog-ssot.md` (2026-07-17 DBG-C1 + DBG-C2 entries).

The `ssot_manifest.yaml` was **not** changed by this arc: its mirrored Integration-SSoT
invariants are the BASS-1 §11 track-identity (invariant 10) and the D4 bundle-less rule,
neither of which the arc altered; the inv-9 keying detail and the override cache-bypass
are not among the manifest's mirrored hard-invariants.

---

## 7. Closure statement

The **MGP-ALWTTT-DBG consumer arc is CLOSED.** ALWTTT owns an inspectable, dev-overrideable
composition seam over the MidiGenPlay boundary; the package owns the re-keyed readback and
the step-0 override contract. No consumer batch touched a MidiGenPlay file. The next
tuning-facing work (composition-session validation) can now read resolved-per-track truth
instead of inferring it.

# changelog-ssot

## 2026-05-28 — LLM Authoring Batch L3: smoke-test sign-off + governance close

Closes the LLM-Assisted Authoring MVP (Batches L1–L3). No runtime behavior
changed; the determinism invariant is untouched (LLM is authoring-side; the
asset is the seam).

### Added
- `Documentation~/authoring/SSoT_Authoring_LLM_Generation.md` — **new primary
  SSoT** for LLM-assisted authoring, written as a replicable pattern: the
  seven-stage pipeline (vocabulary SO → pure prompt builder → generator wrapper
  → pure importer → alias dictionary → async response handler → editor wiring),
  six contracts (asset-as-seam, non-blocking async, no silent fallback,
  CRLF-safe parsing, working-copy/apply, pre-network cost cap), and a
  failure-handling table. Registered in `ssot_manifest.yaml` under `ssots`.
- `Editor/FakeLLMClient.cs` (Tests) — deterministic `ILLMClient` test double
  for the SMR-L5 path (D-L3.2), with a `WasCalled` guard against vacuous passes.
- `Tests/Editor/DrumPatternLLMGeneratorTests.cs` — 6 EditMode tests over the
  generator async path (invalid glyphs → located parser warnings; no-fence
  fail; lane-count mismatch; valid-via-fake control; null-client guard). All
  payloads use CRLF to guard the L2 split regression.

### Modified
- `Editor/DrumPatternEditorWindow.cs` — cost-cap UI (D-L3.1): new per-window
  `Max prompt chars (budget)` field (default 4000, 0 = off) passed into
  `DrumPatternLLMPromptBuilder.Input.maxCharBudget`. The budget is checked
  pre-network in the builder; over-budget prompts are refused without spending
  and the reason surfaces through the existing warning panel (SMR-L3).
- `Documentation~/coverage-matrix.md` — "LLM-assisted authoring" primary home
  flipped from `Roadmap_LLM_Authoring_MVP.md` to the new SSoT; flip note moved
  to past tense.
- `Documentation~/authoring/SSoT_Authoring_Rhythm_Patterns.md` — new §3A
  subsection "LLM-assisted generation" pointing at the new SSoT for the
  contract and the roadmap for L1–L3 history.
- `Documentation~/authoring/SSoT_Authoring_Tools.md` — `DrumPatternEditorWindow`
  capability list gains "LLM-assisted generation" with authority pointer.
- `Documentation~/CURRENT_STATE.md` — LLM Authoring MVP marked completed; L4
  promoted to active; blocked/next lists updated.
- `Documentation~/planning/active/Roadmap_LLM_Authoring_MVP.md` — Batch L3 marked
  CLOSED with closure note; roadmap is now the secondary L1–L3 historical record.

### Authority
- LLM-assisted authoring is now SSoT-primary, not roadmap-primary. Per-tool DSL
  grammar and asset details remain in the relevant per-tool authoring SSoT; the
  new SSoT governs the cross-cutting pattern.

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

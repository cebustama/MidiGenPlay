# MidiGenPlay — Project Summary

<!--
Generated: 2026-05-26
Skill: project-summary-generator (v0.1, run from SKILL.md alone — references/ menu files not present)
Per-run overrides: none (both menus accepted as proposed)
Sources read in Phase 1: SSoT_INDEX.md, SSoT_CONTRACTS.md, CURRENT_STATE.md, coverage-matrix.md, ssot_manifest.yaml, changelog-ssot.md (most recent entry only)
-->

## Status snapshot

### Current focus
Two parallel in-construction items per `CURRENT_STATE.md` (as of 2026-05-24):
- **LLM-Assisted Authoring Batch L1** — surface and lock decisions D-L1 through D-L7 (`planning/active/Roadmap_LLM_Authoring_MVP.md`). Prioritized ahead of Phase 8 persistence for the next session sequence.
- **Workshop companion** — construction of catalog entry R1 (`rhythm-pattern-generator` skill) as Step 3.5 of the Claude Skills & Agents Workshop. Independent of the L1 session; either can run first.

### Last meaningful change
Runtime micro-batch closed: `ComposeFromGrid` now consumes `SnapshotAsStepVelocities` instead of `SnapshotAsIndices`. Per-step authored velocity in `DrumPatternData` reaches generated MIDI for every grid-authored rhythm track. Closes the deferred runtime gap carried since Phase 6; three EditMode tests lock the new contract (sentinel resolution, all-off lane, multi-lane independence).

### Recent decisions
- **SMD5 collapse-to-silence (Option 1).** When the modulation direction lands on the previous root and the requested octave is range-clamped, the composer collapses to silence rather than wrapping. Wrap-on-clamp (Option 2) rejected for inverting user musical intent. (`runtime/SSoT_Composer_Backing_Track.md §6.2`.)
- **Transients excluded from cache key.** `PartConfig` modulation transients (`PreviousRootNote`, `ModulationOctaveHint`) force `cacheEnabled = false` in the consumer's bundle cache. (`runtime/SSoT_Composer_Backing_Track.md §6.5`.)
- **Directional anchor source.** The directional first-chord anchor uses the actual previous first-chord root pitch (held per-track in `ChordTrackComposerFactory`), not the notional `centerOct`. Cold-start fallback to `centerOct` preserved.
- **D-T9 final placement.** Drum text/DSL parser lives in flat `Editor/` with namespace `MidiGenPlay.Authoring` — the initial `MidiGenPlay.Editor.Authoring` shadowed `UnityEditor.Editor` and broke `SoundFontCacheSOEditor`.

### Next move
Decided sequence per `CURRENT_STATE.md → Next`:
1. LLM-Assisted Authoring Batches L2 and L3 once L1 closes.
2. Phase 8 — route `DrumPatternEditorWindow` save paths through the existing package store/repository abstractions (`IPatternRepository` / `PatternRepositoryResources`).
3. Phase 9 — phrasing / feel runtime semantic completion.
4. Batch L4 — chord editor generalization (deferred until L3 closes).
5. Continued demotion of the legacy `MIDISong` / `MIDIGeneratorManager` branch to reference status.

### Open questions
Batch L1 is the work of locking decisions D-L1 through D-L7 in `planning/active/Roadmap_LLM_Authoring_MVP.md`. Those decisions are open-by-design until that batch closes. A new SSoT — `authoring/SSoT_Authoring_LLM_Generation.md` — is scheduled to be created at Batch L3 closure (not before; SSoTs document implemented truth, not planning).

### Blockers
- LLM-assisted authoring not yet implemented for either rhythm (Batches L1–L3) or chord progressions (Batch L4).
- Package store/repository persistence not yet wired into the rhythm tools (Phase 8).
- Phrasing / feel knob semantics incomplete (Phase 9).
- Legacy `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists in the repository.

## Inventory

### Purpose & boundary
MidiGenPlay is a Unity package providing runtime MIDI generation and editor-side authoring tooling. Package truth covers the song model, part/track configuration, track parameters, generation orchestration, role-specific composers, and package-owned authoring assets and tools. It explicitly does **not** cover ALWTTT gameplay semantics, the live composition session bridge, `MidiMusicManager` consumer behavior, card economy / combat / status systems, or legacy `CardData` redesign material. Two non-negotiable invariants: runtime code must not depend on editor-only APIs, and same inputs + same seed must produce same outputs (all composers deterministic).

### Subsystems
- **Song model & config** — `SongConfig`, `PartConfig`, `TrackParameters`; managed by `SongConfigManager`. Parts own meter, tonality, root, and track lists.
- **Generation orchestration** — `MidiGenerator` (factory-registration entry point) and `SongOrchestrator` (walks song structure, executes per-track `ITrackComposer` instances).
- **Backing / chord composer** — `ChordTrackComposer`, `BackingStyles`, `ChordProgressionPaletteSO`. Cloning required before runtime use; assets are never mutated in place.
- **Rhythm composer** — `RhythmTrackComposer`, `RhythmStyleRegistry`, `RhythmGridQuantizer`. Active runtime path: `SongConfig → SongOrchestrator → RhythmTrackComposer`. Legacy `MIDISong` branch is reference, not runtime truth.
- **Melody composer** — `MelodyTrackComposer` + `PhrasePlanner`, plus phrase palettes/archetypes (`BurstThenHold`, `EvenFlow`, `SustainLeadIn`) and per-strategy generation (`ScaleFlow`, `AscendingClimb`, `NearestChordTone`, `Constrained`).

### Governed documents
The doc spine is governed by `ssot_manifest.yaml` (11 authority-class SSoTs). Authority order: subsystem SSoTs in `runtime/` and `authoring/` → `SSoT_CONTRACTS.md` → `coverage-matrix.md` → `CURRENT_STATE.md` → reference → planning → research → archive. Runtime SSoTs cover song model & config, generation orchestration, and the three composer surfaces (backing, rhythm, melody). Authoring SSoTs cover chord progressions, rhythm patterns, melody composition, and the cross-cutting authoring-tools convention. No silent promotion: roadmaps, research, cross-project docs, and archive cannot become authoritative without an explicit edit to `coverage-matrix.md` and `changelog-ssot.md`.

### Authoring tools
Two package-owned editor windows, both governed by `authoring/SSoT_Authoring_Tools.md` under the `input/edit → normalize → preview → apply/save` convention with no silent asset writes:
- **`ChordProgressionEditorWindow`** — chord progression authoring (Roman numeral parsing via `RomanProgressionParser`, quality resolution via `ChordQualityResolver`).
- **`DrumPatternEditorWindow`** — drum pattern authoring with grid mode and (Phase 7) text/DSL mode. Three-tier glyph alphabet: `.` `-` = rest, `x` = lane default (sentinel), `X` = accent (120), `o` = ghost (50); `|` and whitespace ignored; short/long input warns. `DrumPatternTextParser` handles parse/render/per-cell-diff apply as a pure function.

### Tests
`Tests/Editor/MidiGenPlay.Tests.Editor` is the package's first test assembly, introduced in Phase 7. Current EditMode coverage:
- **Phase 7 text-mode parser** — 13 tests (SMR1 alternation, SMR2 glyph→velocity, SMR4 short-pad + warn, SMR5 unknown-glyph + warn, plus render snap, bar separators, ignored chars, dash equivalence, per-cell-diff round-trip).
- **Directional modulation** — 9 tests at the `Core` seam (SM-DIR-1 reproduction C#5 → G#5, Down symmetry, range-clamp fallback, Auto short-circuit, three SMD5 cases including at-top-boundary).
- **`SnapshotAsStepVelocities` contract** — 3 tests.

Canonical asmdef conventions documented in `reference/package/Tests_Authoring_HowTo.md`, including the `defineConstraints: ["UNITY_INCLUDE_TESTS"]` gotcha that silently blocks compilation in Unity 2022.3 local-package contexts.

### Cross-project surface
ALWTTT is the primary consumer. Integration is documented in `reference/cross-project/ALWTTT/` (four docs: melody authoring pipeline, composition cards / track style bundles, runtime composition session bridge, composition system index). The MGP-ALWTTT-MOD-DIR work is now closed end-to-end across both projects, including a new package-level invariant: `MidiMusicManager.RenderSinglePart` must disable its bundle cache when either `PartConfig` modulation transient is non-default. Six ALWTTT smoke tests pass (SM-DIR-1..6); SM-DIR-7 is deferred pending a narrow-range debug instrument. Package-side coverage already locks the contract via `Remembered_Up_BeyondTopOfRange_ClampsToMaxOct`.

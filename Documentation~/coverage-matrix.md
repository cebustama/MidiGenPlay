# coverage-matrix

| Concept / subsystem | Primary authority | Secondary / supporting docs |
|---|---|---|
| Documentation system scope and authority rules | `SSoT_INDEX.md` | `README.md`, `SSoT_CONTRACTS.md` |
| Cross-cutting documentation contracts | `SSoT_CONTRACTS.md` | `SSoT_INDEX.md` |
| Current active focus / next work / blockers | `CURRENT_STATE.md` | `planning/active/` |
| Runtime song model (`SongConfig`, parts, tracks, parameters) | `runtime/SSoT_Runtime_Song_Model_and_Config.md` | `archive/absorbed/251012 MidiGenPlay-Architecture.md` |
| Runtime manager semantics (`SongConfigManager`) | `runtime/SSoT_Runtime_Song_Model_and_Config.md` | `archive/absorbed/251012 MidiGenPlay-Architecture.md` |
| Orchestration and render flow (`MidiGenerator`, `SongOrchestrator`, composer selection) | `runtime/SSoT_Runtime_Generation_Orchestration.md` | `archive/absorbed/MidiGenPlay_MIDI_Generation_Pipeline.md` |
| Backing / chord composer behavior | `runtime/SSoT_Composer_Backing_Track.md` | `authoring/SSoT_Authoring_Chord_Progressions.md`, `archive/absorbed/SSoT_Composer_BackingChordTrack.md` |
| Chord expression / articulation (Tier-1 figures, `ChordExpressionType`, `IChordArticulator`/`ChordArticulator`; Tier-2 voicing-reshaping via `IChordReshaper`/`ChordReshaper`) | `runtime/SSoT_Composer_Backing_Track.md` §8 (Tier-1 §8.1–§8.5, selection vocabulary §8.4, Tier-2 reshaping + register-selective §8.6, seeded variation §8.5 rate + §8.7 jitter); bass consumer semantics incl. the chord-tone walk in `runtime/SSoT_Composer_Bass_Track.md` §3.3/§3.6 | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §4.3, `planning/active/Roadmap_Chord_Articulation.md` |
| Bass composer (progression consumption, note-selection rng contract, monophonic Tier-1 articulation via the shared engine, `BasslineCardConfigSO`) | `runtime/SSoT_Composer_Bass_Track.md` | `runtime/SSoT_Composer_Backing_Track.md` §8 (engine), `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §4.5, `planning/active/Roadmap_Chord_Articulation.md` |
| Rhythm / drum composer behavior | `runtime/SSoT_Composer_Rhythm_Track.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md`, `archive/absorbed/SSoT_Composer_RhythmTrack.md` |
| Melody composer behavior | `runtime/SSoT_Composer_Melody_Track.md` | `authoring/SSoT_Authoring_Melody_Composition.md`, `archive/absorbed/melody_pipeline.md` |
| Chord progression authoring assets and tool flow | `authoring/SSoT_Authoring_Chord_Progressions.md` | `authoring/SSoT_Authoring_Tools.md`, `archive/absorbed/ChordProgressionEditorWindow_Overview.md` |
| Rhythm pattern authoring assets and current tool flow | `authoring/SSoT_Authoring_Rhythm_Patterns.md` | `authoring/SSoT_Authoring_Tools.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md` |
| Drum pattern palettes (`DrumPatternPaletteSO`, weighted/deterministic pick; consumed by `RhythmTrackComposer` via the TS-aware `PickPatternOverride` → shared `PaletteSelector`/`PatternFinder`) | `runtime/SSoT_Composer_Rhythm_Track.md` §3D | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §1.3, `planning/active/Roadmap_Composition_Expressivity.md` |
| Palette selection policy (shared `PaletteSelector` Tier A/B/C; typed `ProgressionFinder`/`PatternFinder`; TS-aware; `PaletteSelection.cs`) | `runtime/SSoT_Composer_Rhythm_Track.md` §3D | `runtime/SSoT_Composer_Backing_Track.md` §2, `planning/active/Roadmap_Composition_Expressivity.md` (CE-F1) |
| Rhythm card musical identity (card → palette assignment, distinctness axis) | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §1.3 | `runtime/SSoT_Composer_Rhythm_Track.md` §3D, `planning/active/Roadmap_Composition_Expressivity.md` (PCE) |
| Drum pattern catalogue/browser tool (`DrumPatternCatalogueWizard`) | `authoring/SSoT_Authoring_Tools.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `archive/absorbed/SSoT_CompositionAuthoringTools.md` |
| Rhythm editor UI target / next milestone interaction model | `planning/active/Roadmap_Rhythm_Authoring_MVP.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `authoring/SSoT_Authoring_Tools.md` |
| Melody phrase planning / palette / leading / style authoring | `authoring/SSoT_Authoring_Melody_Composition.md` | `runtime/SSoT_Composer_Melody_Track.md`, `archive/absorbed/melody_pipeline.md` |
| Package-owned authoring tool conventions | `authoring/SSoT_Authoring_Tools.md` | `archive/absorbed/SSoT_CompositionAuthoringTools.md` |
| LLM-assisted authoring (cross-cutting pattern) | `authoring/SSoT_Authoring_LLM_Generation.md` | `authoring/SSoT_Authoring_Tools.md`, `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A (drum DSL), `authoring/SSoT_Authoring_Chord_Progressions.md` (chord Roman DSL), `planning/active/Roadmap_LLM_Authoring_MVP.md` (L1–L4 history); LLM Core package's `SSoT_Editor_Tooling_and_Wizard.md` (external, integration shape only) |
| MIDI file import (cross-cutting authoring pattern) | `authoring/SSoT_Authoring_MIDI_Import.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A (drums, M1), `authoring/SSoT_Authoring_Melody_Composition.md` §5 (melody, M2), `authoring/SSoT_Authoring_Chord_Progressions.md` §3 (chords, M3), `authoring/SSoT_Authoring_Tools.md` (panels), `planning/archive/Roadmap_MIDI_Import.md` (M1–M3 history, archived) |
| ALWTTT melody integration boundary | `reference/cross-project/ALWTTT/ALWTTT_Melody_Authoring_Pipeline.md` | `runtime/SSoT_Composer_Melody_Track.md` |
| ALWTTT composition cards / track style bundle usage | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` | package SSoTs in `runtime/` and `authoring/` |
| ALWTTT runtime composition session bridge | `reference/cross-project/ALWTTT/SSoT_Runtime_CompositionSession_Bridge.md` | `runtime/SSoT_Runtime_Generation_Orchestration.md` |
| Rhythm authoring immediate milestone plan | `planning/active/Roadmap_Rhythm_Authoring_MVP.md` | `CURRENT_STATE.md` |
| LLM-assisted authoring milestone plan (L1–L4 history) | `planning/active/Roadmap_LLM_Authoring_MVP.md` | `CURRENT_STATE.md` — MVP through L4 complete; roadmap retained as closed historical record |
| Documentation migration plan | `planning/active/Roadmap_Documentation_Migration.md` | `CURRENT_STATE.md` |
| Exploratory Jamplay runtime composition design | `research/MIDI_Jamplay_Runtime_Composition_Design-1.md` | none |
| Deep research prompt for MidiMusicManager | `research/Deep_Research_Prompt_MidiMusicManager.md` | `reference/cross-project/ALWTTT/` |
| Historical CardData redesign | `archive/historical/CardData_Redesign.md` | none |

## Notes on primary-home flips at batch closure

- **LLM-assisted authoring** flipped to SSoT-primary at Batch L3 closure (2026-05-28): `authoring/SSoT_Authoring_LLM_Generation.md` is now primary for the cross-cutting pattern; the roadmap is the secondary historical record of the L1–L3 closure. The "LLM-assisted authoring milestone plan" row stays roadmap-primary until L4 closure (chord editor generalization), at which point the milestone plan can be retired as no longer-active planning.

- **Batch L4 closed (2026-05-29):** the chord progression editor adopted the LLM pattern as the second adopter. `authoring/SSoT_Authoring_LLM_Generation.md` §7 now lists both adopters; §3.3 gained the degrade-vs-fail enforcement nuance (the chord parser warns-and-downgrades rather than rejecting, so the no-silent-fallback guard lives in the response handler, D-L4.5). The milestone plan's primary home stays the roadmap, now retained as a **closed historical record** (LLM Authoring MVP complete through L4) rather than active planning. Chord LLM artifacts registered in `ssot_manifest.yaml` under the LLM SSoT governs.

- **Batch L5 (L-PAL) closed (2026-05-29):** drum palettes + catalogue wizard. `DrumPatternPaletteSO` is primary-homed under `runtime/SSoT_Composer_Rhythm_Track.md` (D-PAL.5=B, mirroring `ChordProgressionPaletteSO` under the backing composer SSoT), with `authoring/SSoT_Authoring_Rhythm_Patterns.md` secondary — ahead of the planned composer-consumption phase. `DrumPatternCatalogueWizard` is primary-homed under `authoring/SSoT_Authoring_Tools.md` as a read-only catalogue/browser. Palettes are author-only for now (D-PAL.3); no runtime path consumes `PickRandomPattern` yet.

- **PCE closed (2026-06-04); CE-F1 closed (2026-06-10):** `RhythmTrackComposer` consumes drum palettes via `RhythmCardConfigSO.PickPatternOverride`, now TS-aware through the shared `PaletteSelector`. The TS-toggle asymmetry (chord live / drum inert) is **resolved** — both toggles are live in the one selector.

- **Melody Authoring MVP complete (Phase 5 closed 2026-06-22):** no primary-home flip — melody composer behavior stays primary under `runtime/SSoT_Composer_Melody_Track.md` (the `ComposeFromPattern` pattern-override path was added there at Phase 4) and melody authoring stays primary under `authoring/SSoT_Authoring_Melody_Composition.md`. Phase 5 was validation + documentation closure; meter-mismatch resolved as D-MEL5.1 = A (tiles-by-beats + warning is the documented MVP limitation; bar-time renormalization is post-MVP). Rows unchanged.

- **Batch CQ-A1-OBJ2 closed (2026-07-05):** per-chord inversion voicing hint
  (pin) built in the voicing layer. **No primary-home flip** — backing/chord
  composer behavior stays primary under `runtime/SSoT_Composer_Backing_Track.md`,
  which gained **§7** (per-chord inversion hint; update triggers renumbered to
  §8); the transient field itself is registered in
  `runtime/SSoT_Runtime_Song_Model_and_Config.md §1.1` alongside the modulation
  hint, with composer semantics deferred to backing-track §7 (same split as §6).
  The table above is unchanged. Governance note: the voicing layer
  (`Strategies/VoiceLeading.cs`, previously ungoverned) and
  `Interfaces/IChordVoicer.cs` are now listed under the backing-track SSoT
  `governs:` — §7 documents their pin contract (mirrors the PATTERN-PERSIST-1
  governs-B precedent of registering the files an SSoT section documents).

- **Batch PATTERN-PERSIST-1 closed (2026-07-05):** pattern-asset persistence unified — all three pattern editors (`DrumPatternEditorWindow`, `ChordProgressionEditorWindow`, `MelodyPatternEditorWindow`) now save and read through the shared `TrackPatternConfigStoreResources<T>` store instead of ad-hoc `AssetDatabase` calls with per-window hardcoded folders (Drum `/Drums` unchanged; Chord `/Chords` — first real default folder; Melody realigned singular `/Melody` → plural `/Melodies`). **No concept → authority mapping changed:** the pattern-authoring "tool flow" rows and "Package-owned authoring tool conventions" stay primary under the `authoring/` SSoTs, with `authoring/SSoT_Authoring_Tools.md` §6 now documenting the persistence mechanism; the table above is unchanged and there is **no primary-home flip**. Governance note: the persistence Services layer (`TrackPatternConfigStoreResources.cs` / `ITrackPatternConfigStore.cs`, and the sibling `PatternRepositoryResources.cs` / `IPatternRepository.cs`) is now listed under `authoring/SSoT_Authoring_Tools.md` `governs:` (decision B), with a persistence-contract invariant — Runtime/ files governed by an authoring SSoT because Tools §6 documents the persistence mechanism.

- **Batch BPM-DET-1 closed (2026-07-16):** the `GenerateSong` tempo roll is now seed-deterministic and `PartConfig.ExplicitBpm` is a live reader (`bpmOverride ?? ExplicitBpm ?? seeded-roll`; `ResolveTempoSeed`/`RollTempoBpm`; `GetBPMFromRange` left byte-identical and off the render path). **No primary-home flip** — orchestration/render flow stays primary under `runtime/SSoT_Runtime_Generation_Orchestration.md`, which gained **§5.2** (tempo resolution) alongside the §5.1 seed contract. The table above is unchanged.

- **Batch CA-T2 closed (2026-07-16):** Tier-2 voicing-reshaping figures (power chord, chugging) built as a separate pre-articulation seam. **No primary-home flip** — backing/chord composer behavior stays primary under `runtime/SSoT_Composer_Backing_Track.md`, which gained **§8.6** (Tier-2 reshaping) and **§7.5** (reshape-vs-pin precedence); the articulation row above is extended to name the reshaper. Governance note: `IChordReshaper.cs` + `ChordReshaper.cs` are listed under the backing-track SSoT `governs:` (§8.6 documents them), and the `IChordVoicer.cs` governs path was corrected to `Composition/Interfaces/` (a stale inferred path from CQ-A1-OBJ2, now confirmed against the package tree). The bossa bass/upper split figure is deferred to the CA roadmap.

- **Batch MGP-ALWTTT-DBG-4+2 closed (2026-07-17):** runtime Roman parser/builder + catalog-enumeration contract. **No primary-home flip** — the setup-card + Roman grammar stays primary under `authoring/SSoT_Authoring_Chord_Progressions.md`, which gained **§4.2** (runtime consumption; `ChordProgressionRuntimeImporter` is the relocated single code path, editor importer = forwarder); runtime consumption/enumeration contracts land in `runtime/SSoT_Composer_Backing_Track.md` **§2.2** (Ask D builder + chord-palette enumeration), `runtime/SSoT_Composer_Rhythm_Track.md` §3D addendum (drum-palette enumeration; repository = patterns, store = palettes), and `runtime/SSoT_Composer_Melody_Track.md` §4 addendum (phrase-vocabulary enumeration, E-2=A). Governance note: `ChordProgressionRuntimeImporter.cs` is dual-listed — under the authoring chord SSoT `governs:` (grammar authority) and the backing SSoT `governs:` (consumption contract) — intentional, mirroring the PaletteSelection.cs dual-listing precedent. New test row: `ChordProgressionRuntimeImporterTests.cs` (11 tests: payload/bare-Roman/guard×2/quantization/never-persisted/parity×2).

- **Batch M1 closed (2026-07-19):** MIDI file import for drums. **No primary-home
  flip at the time** — `Editor/DrumMidiImporter.cs` was homed under
  `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A, with the explicit note that
  import would be revisited as a cross-domain concept if M2/M3 landed. New test
  file: `Tests/Editor/DrumMidiImporterTests.cs` (11 tests).

- **Batch PERC-FALLBACK-1 closed (2026-07-22):** render-time percussion fallback.
  **No primary-home flip** — `PercussionFallbackTable` + `PercussionNoteResolver`
  are homed under `runtime/SSoT_Composer_Rhythm_Track.md` §3E (exact → fixed-order
  family substitute → mute+warn; GM-standard emission behind an opt-in wired off).
  New test file: `Tests/Editor/PercussionNoteResolverTests.cs`.

- **Batch M2 closed (2026-07-23):** MIDI file import for melody. **No primary-home
  flip at the time** — homed under `authoring/SSoT_Authoring_Melody_Composition.md`
  §5. Implements and supersedes `Roadmap_Melody_Authoring_MVP.md` Phase D1. New
  test file: `Tests/Editor/MelodyMidiImporterTests.cs` (20 tests).

- **Batch M3 closed (2026-07-23):** MIDI file import for chord progressions
  (restricted deterministic detection). Homed under
  `authoring/SSoT_Authoring_Chord_Progressions.md` §3. New test file:
  `Tests/Editor/ChordMidiImporterTests.cs` (25 tests, later 33). Closing M3
  completed the arc and made import a three-adopter cross-domain concept — the
  trigger M1 wrote down.

- **Batch IMPORT-QOL-1 closed (2026-07-24):** chord-import and smoke QoL. **No
  primary-home flip** — the sub-features live at the §3 level of the chord SSoT.
  `CompositionSmokeWindow` remains intentionally ungoverned (D-SMOKE-DOC-1=A).

- **Batch MEL-DOCDRIFT-1 closed (2026-07-24):** documentation-only correction of
  melody-phase staleness in `authoring/SSoT_Authoring_Tools.md` §3.A/§3.D. **No
  primary-home flip**; no governed surface moved.

- **Batch MIDIIMP-SSOT-1 closed (2026-07-24): PRIMARY-HOME FLIP.** MIDI file
  import became SSoT-primary: the new `authoring/SSoT_Authoring_MIDI_Import.md` is
  now primary for the **cross-cutting** contract (pure-function importer in
  `Editor/`, working-copy-only apply, window Timing controls as meter authority,
  beat-unit-aware conversion, the `[Kind] loc: detail` warning shape with no silent
  fallback, ticks-per-quarter-only, ties-toward-lower, measure derivation and cap).
  The three domain SSoTs keep their per-domain musical semantics and warning
  taxonomies unchanged — nothing was moved out of them, so this is an addition of a
  shared home, not a relocation. The three importer files are **dual-listed** in
  `ssot_manifest.yaml` (domain SSoT + import SSoT), mirroring the
  `ChordProgressionRuntimeImporter.cs` and `PaletteSelection.cs` dual-listing
  precedent. Same rationale as the L3 LLM flip: a replicable authoring pattern with
  more than one adopter needs one home or its copies drift.

- **Batch MIDIIMP-SSOT-1, second item:** `MIDIPercussionInstrumentSO` resolved as
  **package-owned** (open question left by PERC-FALLBACK-1 §7.5). No new SSoT — it
  is homed under `runtime/SSoT_Composer_Rhythm_Track.md`, which already owns the
  read-only consumption contract in §3E.

- **Batch MEL-BEATUNIT-1 closed (2026-07-24):** runtime fix, **no primary-home flip** and
  no governed surface moved. Melody timing became beat-unit aware through the new single
  seam `MelodyTrackComposer.BeatsToSpan`; the deviation is recorded in
  `runtime/SSoT_Composer_Melody_Track.md` §7.1, which is the primary authority for it,
  worded to match the bass precedent in `runtime/SSoT_Composer_Bass_Track.md` §3.4.
  `SSoT_CONTRACTS.md` §5 (meter authority) picked up `BassTrackComposer` and
  `MelodyTrackComposer`, which had been applying the rule without appearing in its list.
  With this, every composer and every importer resolves a beat through
  `MusicTheory.GetBeatSpan` — there is no remaining consumer that assumes a quarter.

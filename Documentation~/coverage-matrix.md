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
| Rhythm / drum composer behavior | `runtime/SSoT_Composer_Rhythm_Track.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md`, `archive/absorbed/SSoT_Composer_RhythmTrack.md` |
| Melody composer behavior | `runtime/SSoT_Composer_Melody_Track.md` | `authoring/SSoT_Authoring_Melody_Composition.md`, `archive/absorbed/melody_pipeline.md` |
| Chord progression authoring assets and tool flow | `authoring/SSoT_Authoring_Chord_Progressions.md` | `authoring/SSoT_Authoring_Tools.md`, `archive/absorbed/ChordProgressionEditorWindow_Overview.md` |
| Rhythm pattern authoring assets and current tool flow | `authoring/SSoT_Authoring_Rhythm_Patterns.md` | `authoring/SSoT_Authoring_Tools.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md` |
| Rhythm editor UI target / next milestone interaction model | `planning/active/Roadmap_Rhythm_Authoring_MVP.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `authoring/SSoT_Authoring_Tools.md` |
| Melody phrase planning / palette / leading / style authoring | `authoring/SSoT_Authoring_Melody_Composition.md` | `runtime/SSoT_Composer_Melody_Track.md`, `archive/absorbed/melody_pipeline.md` |
| Package-owned authoring tool conventions | `authoring/SSoT_Authoring_Tools.md` | `archive/absorbed/SSoT_CompositionAuthoringTools.md` |
| LLM-assisted authoring (cross-cutting pattern) | `authoring/SSoT_Authoring_LLM_Generation.md` | `authoring/SSoT_Authoring_Tools.md`, `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A (drum DSL), `authoring/SSoT_Authoring_Chord_Progressions.md` (chord Roman DSL), `planning/active/Roadmap_LLM_Authoring_MVP.md` (L1–L4 history); LLM Core package's `SSoT_Editor_Tooling_and_Wizard.md` (external, integration shape only) |
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

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
| Bass composer (progression consumption, note-selection rng contract, monophonic Tier-1 articulation via the shared engine, `BasslineCardConfigSO`; SlapPocket rhythm coupling §3.7 + shaping §3.7.1; SelfPocket autonomous figure §3.7.2; articulation vocabulary and legato §3.7.3) | `runtime/SSoT_Composer_Bass_Track.md` | `runtime/SSoT_Composer_Backing_Track.md` §8 (engine), `runtime/SSoT_Composer_Rhythm_Track.md` §3bis (onset source), `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §4.5, `reference/cross-project/ALWTTT/Handoff_MGP_POCKET.md`, `planning/active/Roadmap_Chord_Articulation.md` |
| Cross-composer rhythm onset channel (Rhythm publishes on the grid path; Bass consumes; first-publisher-wins by track-list order) | `runtime/SSoT_Composer_Rhythm_Track.md` §3bis | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5 (the `GenContext` channel), `runtime/SSoT_Composer_Bass_Track.md` §3.7 (the consumer + degrade contract) |
| Host-supplied default progression for backing-less parts (`GenerateSinglePart defaultProgression`, MGP-ALWTTT-BASS-SOLO-1) | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.5 | `runtime/SSoT_Composer_Bass_Track.md` §1, `runtime/SSoT_Composer_Backing_Track.md` §3 |
| Chord quality render policy (`ChordProgressionData.qualityRenderPolicy`; diatonic re-qualification at render time, RUNTIME-REQUALITY) | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.1 | `runtime/SSoT_Composer_Backing_Track.md` §3 (the two application sites + F-NORM-DROP), `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.5, `planning/active/Roadmap_Chord_Expressivity.md` |
| Modal colour table (`useColorTable`, REQUALITY-2; opt-in, policy-gated, applied after the core remap; the `ii(dim)` → `iv` substitution) | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 | `runtime/SSoT_Composer_Backing_Track.md` §3 (application order A→B→C), `planning/active/Roadmap_Chord_Expressivity.md` |
| Secondary dominants as a per-event relation (`hasAppliedTarget` + `appliedTarget`, SECDOM-1; resolved at render time under any policy and any tonality) | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 | `runtime/SSoT_Composer_Backing_Track.md` §3 (step C of the publication order) |
| Cadence as authored metadata (`ChordProgressionData.cadence : CadenceType`, CADENCE-META; composers ignore it, consuming games may gate on it) | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` (consumer use) |
| `tonalities` as descriptive metadata (B2 TONFILTER-1: no revert, no draw; the part's tonality is the card's authority) **and the F-B2-LIBRARY exception** (`PickTemplateForPart` still filters on the procedural library path) | `runtime/SSoT_Composer_Backing_Track.md` §2.2 | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 (authoring side of both), `planning/active/Roadmap_Composition_Expressivity.md` §B2 (the out-of-scope decision) |
| Modulation planning primitive (`ModulationPlanner`, MOD-1; pure host-facing plan — functional dominant, ranked pivots, common tones; D-MOD-OUT=A) | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.6 | `runtime/SSoT_Composer_Backing_Track.md` §6 (the distinct directional-hint transient), `runtime/SSoT_Runtime_Generation_Orchestration.md` §5 (the `patternOverride` surface the host consumes it through), `planning/active/Roadmap_Chord_Expressivity.md` |
| Bass register contract (B3 BASS-REG-1: ceiling-capped two-octave band `ResolveOctaveBand`; `octaveMax` as a hard ceiling `ResolveRegisterCeiling` over everything emitted; whole-voicing walk fold D-REG-3=B, per-note TWO-SIDED fold D-W2-REG + D-W2-FLOOR=B, pop fold D-REG-2=B) | `runtime/SSoT_Composer_Bass_Track.md` §2 | `runtime/SSoT_Composer_Bass_Track.md` §3.6 / §3.6bis / §3.7.1 (the three fold sites), `planning/active/Roadmap_Chord_Articulation.md` §B3 |
| Improvised walking bass (B3 WALK-2, `arpeggioToneMode = ImprovisedWalk`; composer owns pitches, engine owns rhythm/dynamics; variation as a pure mix over `ResolveWalkSeed`) | `runtime/SSoT_Composer_Bass_Track.md` §3.6bis | `runtime/SSoT_Composer_Backing_Track.md` §8 (the engine, `PlanHits`), `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.1 (`ResolveWalkSeed`), `planning/active/Roadmap_Chord_Articulation.md` §B3 |
| Track-list order independence (`SSoT_CONTRACTS.md` §10) and the composition-pass structure: PASS 0 Backing / PASS 1 / PASS 2 Harmony, deferred index-ordered merge, the harmony-source sniff `BackingTrackCarriesHarmonySource` and the `sharedProgressionSource` / `ResolvedSource.HostDefault` readback (MGP-ALWTTT-BASS-ORDER-1) | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.7 | `SSoT_CONTRACTS.md` §10 (the contract itself), `runtime/SSoT_Composer_Bass_Track.md` §1 (bass-side consequence), `runtime/SSoT_Composer_Backing_Track.md` §2 (PASS-0 consequence) |
| SelfPocket — the order-free autonomous slap/pop figure, its articulation vocabulary (`Ghost` / `GhostPop` / `HammerOn` / `PullOff`, `QuarterBeat`) and the per-class velocity-factor and gate-ceiling laws (MGP-ALWTTT-BASS-SLAPFIG-1 / -2 / -2b) | `runtime/SSoT_Composer_Bass_Track.md` §3.7.2–§3.7.3 | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §4.5 (authoring surface only), `SSoT_CONTRACTS.md` §10 (the swing doctrine's constraint) |
| Emitted MIDI event contract and pitch bend (`PitchBendWriter`; no-entry-no-event, same-tick ordering, the centre-reset closing invariant, single-channel scope, assumed ±2 semitone range, MGP-ALWTTT-BASS-BEND-1) | `SSoT_CONTRACTS.md` §11 | `runtime/SSoT_Composer_Bass_Track.md` §3.7.3 (the only consumer today, and the writer's manifest home), `runtime/SSoT_Composer_Melody_Track.md` §7 (documented unconsumed seam) |
| Shared-progression carry channel (`PartRender.sharedProgressionData`, MGP-MEL-1 P7) and the host jam-continuity recipe built on it | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.8 | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.7 (`sharedProgressionAssetName`, which its name preservation matches), `reference/cross-project/ALWTTT/SSoT_Runtime_CompositionSession_Bridge.md` (consumer side) |
| Tonality adoption from the resolved progression (`BackingCardConfigSO.adoptProgressionTonality`, MGP-MEL-1 P4; compose-time adoption wins over any pre-render tonality, `PartConfig` mutated in place) | `runtime/SSoT_Composer_Backing_Track.md` §2.3 | `authoring/SSoT_Authoring_Chord_Progressions.md` §4.7 (how to author a progression for adoption), `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.7 (PASS-0 visibility guarantee) |
| Procedural melody precedence and the directive layer (layer table, `.enabled` intent contract, motif buffer + chromatic transpose hazard, scale-aware contour snapping, P3 / P6.2 signals; MGP-MEL-1b) | `authoring/SSoT_Authoring_Melody_Composition.md` §4b | `runtime/SSoT_Composer_Melody_Track.md` §7.2 (composer-side law and the F1 rng-sequence change) |
| Bassline card authoring: the SelfPocket text DSL (`S P . - g G H L`), the editor window's working-copy/persistence contract, and its advisory set (MGP-BASSCARD-WIZARD-1) | `authoring/SSoT_Authoring_Bass_Cards.md` | `authoring/SSoT_Authoring_Tools.md` §3.A (registration), `runtime/SSoT_Composer_Bass_Track.md` §3.7.x (the articulation-class meaning it deliberately does NOT govern), `reference/package/PhrasePresets_Bass_Spec.md` |
| SelfPocket phrase — bar substitutions, meter anchoring, within-bar indexing, seeded/round-robin variant selection (MGP-ALWTTT-BASS-PHRASE-1) | `runtime/SSoT_Composer_Bass_Track.md` §3.7.4 | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §4.5 (card surface only), `authoring/SSoT_Authoring_Bass_Cards.md` (how the patterns are typed) |
| Chord identity — the (degree, degreeAccidental, quality) triple every composer and chord-naming component must resolve through `TransposeNoteName` (MGP-TONALITY-1, D-TON10) | `SSoT_CONTRACTS.md` §13 | `runtime/SSoT_Composer_Bass_Track.md` §1, `runtime/SSoT_Composer_Melody_Track.md` §7.2, `runtime/SSoT_Composer_Backing_Track.md` §3 (the adherents) |
| Melody guide-note cache — the PASS 1 → PASS 2 channel: lifetime, which melody Harmony follows (D-H1-5a=B), what it writes back (D-H1-5b=A), stem isolation (MGP-ALWTTT-HARMONY-1) | `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.9 | `runtime/SSoT_Composer_Melody_Track.md` "Guide-note handoff" (payload units only) |
| Diagnostic / regression harnesses that author nothing (the MGP-TONALITY-2 tonality matrix; `beliefDiv` canonical re-classification as the only admissible parity detector) | `authoring/SSoT_Authoring_Tools.md` §3.F | `SSoT_CONTRACTS.md` §13 (the contract it verifies), `runtime/SSoT_Composer_Bass_Track.md` §3.6bis (the defect it found) |
| Phrase archetype materialization — `endRestFraction`, `meterFitSlots`, `allowTupletSubdivisions`, the `restProbMid` RNG-neutrality exception (MGP-TONALITY-1) | `runtime/SSoT_Composer_Melody_Track.md` §4 | `authoring/SSoT_Authoring_Melody_Composition.md` §4b (the authoring side and the field defaults) |
| Rhythm / drum composer behavior | `runtime/SSoT_Composer_Rhythm_Track.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md`, `archive/absorbed/SSoT_Composer_RhythmTrack.md` |
| Melody composer behavior | `runtime/SSoT_Composer_Melody_Track.md` | `authoring/SSoT_Authoring_Melody_Composition.md`, `archive/absorbed/melody_pipeline.md` |
| Chord progression authoring assets and tool flow | `authoring/SSoT_Authoring_Chord_Progressions.md` | `authoring/SSoT_Authoring_Tools.md`, `archive/absorbed/ChordProgressionEditorWindow_Overview.md` |
| Rhythm pattern authoring assets and current tool flow | `authoring/SSoT_Authoring_Rhythm_Patterns.md` | `authoring/SSoT_Authoring_Tools.md`, `planning/active/Roadmap_Rhythm_Authoring_MVP.md` |
| Drum pattern palettes (`DrumPatternPaletteSO`, weighted/deterministic pick; consumed by `RhythmTrackComposer` via the TS-aware `PickPatternOverride` → shared `PaletteSelector`/`PatternFinder`) | `runtime/SSoT_Composer_Rhythm_Track.md` §3D | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §1.3, `planning/active/Roadmap_Composition_Expressivity.md` |
| Palette selection policy (shared `PaletteSelector` Tier A/B/C; typed `ProgressionFinder`/`PatternFinder`; TS-aware; `PaletteSelection.cs`) | `runtime/SSoT_Composer_Rhythm_Track.md` §3D | `runtime/SSoT_Composer_Backing_Track.md` §2, `planning/active/Roadmap_Composition_Expressivity.md` (CE-F1) |
| Rhythm card musical identity (card → palette assignment, distinctness axis) | `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §1.3 | `runtime/SSoT_Composer_Rhythm_Track.md` §3D, `planning/active/Roadmap_Composition_Expressivity.md` (PCE) |
| Drum pattern catalogue/browser tool (`DrumPatternCatalogueWizard`) | `authoring/SSoT_Authoring_Tools.md` | `authoring/SSoT_Authoring_Rhythm_Patterns.md`, `archive/absorbed/SSoT_CompositionAuthoringTools.md` |
| MIDI instrument catalogue + management tool (`MidiInstrumentCatalogueWizard`; catalogue variants, CSV export) and the `MIDIInstrumentSO` dropdown drawers' write discipline | `authoring/SSoT_Authoring_Tools.md` §3.E + §3.C | `runtime/SSoT_Runtime_Song_Model_and_Config.md` §3.2 (`volume01` meaning), `reference/cross-project/ALWTTT/Handoff_MGP_BAGGAGE_1.md` §4.1 (the patch-collision false positive this export settles) |
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

- **MGP-TRIAGE-ALWTTT-R3, applied at MGP-DOC-SWEEP-2 (2026-08-08) — no primary-home flip; no row added or relocated.** Melody composer behaviour stays primary under `runtime/SSoT_Composer_Melody_Track.md`, which closed gap F5 (reclassified cosmetic → audible) and gained the `maxStepSemitones`-is-a-preference statement; backing composer behaviour stays primary under `runtime/SSoT_Composer_Backing_Track.md`, which gained **§3.1** (clone identity contract, plus the `ResolvedSource.TrackParameters` reachability note) and whose §2.3 now marks the `PartConfig` in-place mutation as a committed consumer surface. One CROSS-CUTTING CONTRACT was added to `SSoT_CONTRACTS.md` and is primary there, not in any composer SSoT: **§12** PartConfig mutation. The authoring hazard for `transposeSemitones` stays primary under `authoring/SSoT_Authoring_Melody_Composition.md`, which gained the R3 field data point. **New test files:** `Tests/Editor/PhraseArchetype_SlotBookkeepingTests.cs` (archetype slot bookkeeping — all three phrase archetypes; subject governed under the melody composer SSoT) and `Tests/Editor/ChordProgression_CloneIdentityTests.cs` (one test per reachable precedence step plus the publish-then-impose round trip; subject governed under the backing composer SSoT). Registered here in the closure-notes convention this file uses for test surfaces, not in the table's *Secondary / supporting docs* column, which holds documents — sweep decision D-2=A.

- **DOC-SWEEP-1 (2026-08-05) — no primary-home flip; six rows ADDED, one extended.** The five-batch documentation backlog (ORDER-1/SLAPFIG-1, SLAPFIG-2, ARTIC-RATE-1, BEND-1, MEL-1b) was applied in one pass. Bass composer behaviour stays primary under `runtime/SSoT_Composer_Bass_Track.md`, which gained **§3.7.2** (SelfPocket) and **§3.7.3** (articulation vocabulary + legato); orchestration stays primary under `runtime/SSoT_Runtime_Generation_Orchestration.md`, which gained **§5.7** (composition passes and deferred merge) and **§5.8** (shared-progression carry channel); the backing composer gained **§2.3** (tonality adoption); melody authoring gained **§4b** (procedural precedence and recipes) and the melody composer **§7.2** (directive layer). Two CROSS-CUTTING CONTRACTS were added to `SSoT_CONTRACTS.md` and are primary there, not in any composer SSoT: **§10** track-list order and **§11** emitted MIDI events / pitch bend. `PitchBendWriter.cs` is *governed* under the Bass SSoT because the bass is its only in-package consumer today — the CONTRACT is §11 and that is the authority; if the melody composer adopts the writer, the governs home moves (the `ModulationPlanner` homing discipline). Rows added above for concepts that did not previously exist: order independence + passes, SelfPocket/vocabulary/legato, pitch bend, the carry channel, tonality adoption, and the procedural-melody directive layer. The Bass composer row was extended, not relocated.

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

- **Batch INST-WIZ-1 closed (2026-07-25):** MIDI instrument catalogue wizard + drawer repair. `MidiInstrumentCatalogueWizard` is primary-homed under `authoring/SSoT_Authoring_Tools.md` §3.E as the **catalogue + management** variant (D-W1=A) — the first catalogue tool that is not read-only, which is why the manifest's catalogue invariant was rewritten rather than left to drift. Editor-only; no runtime, composer or asset semantics touched. The catalogue export closed the standing `PatchName`/`PatchIndex` hygiene candidate with **no findings** across 79 assets. `ChordProgressionCatalogueWizard` remains absent from the `SSoT_Authoring_Tools` governs list — a pre-existing omission, recorded here, not fixed by this batch.

- **Batches MGP-ALWTTT-BASS-POCKET-1, -POCKET-2 and -SOLO-1 + RUNTIME-REQUALITY closed (2026-07-25/26); documentation applied in one pass by B0 — DOC-CLOSE (2026-07-26).** **No primary-home flip.** Bass composer behaviour stays primary under `runtime/SSoT_Composer_Bass_Track.md`, which gained **§3.7** (SlapPocket coupling) and **§3.7.1** (pocket shaping) plus a §1 paragraph for the host-supplied default; orchestration stays primary under `runtime/SSoT_Runtime_Generation_Orchestration.md`, which gained **§5.5** (host default progression) and **§5.6** (test-seam visibility convention, F-IVT-STALE) alongside the §5 onset-channel bullet; the chord-quality alphabet stays primary under `authoring/SSoT_Authoring_Chord_Progressions.md`, whose **§4.1** gained the render policy. Four rows were ADDED above for concepts that did not previously exist (the onset channel, the host-default channel, the render policy) and the Bass row was extended — additions, not relocations.
  Governance notes: (1) `runtime/SSoT_Composer_Rhythm_Track.md` gained a section numbered **§3bis** rather than renumbering §4–§10, because §3D/§3E are cited by name from this file, `ssot_manifest.yaml` and sibling SSoTs. (2) `Runtime/CoreScripts/Composition/ChordProgressionRequality.cs` is **dual-listed** in the manifest — under the authoring chord SSoT (policy semantics) and the backing SSoT (application sites) — mirroring the `ChordProgressionRuntimeImporter.cs` and `PaletteSelection.cs` precedent. (3) The SOLO-1 diff proposed `feature | tests | smoke` rows; that is not this file's schema, so the content was translated into the `Concept | Primary | Secondary` shape above and test/smoke provenance stays in each SSoT's own "Test surface" line. (4) `planning/active/Roadmap_Chord_Expressivity.md` was registered under `roadmaps:` in `ssot_manifest.yaml` at B0 — a pre-existing omission, fixed because B1 schedules work there.

- **Batches B1 (HARMONY-PURE-1), B2 (TONFILTER-1) and B3 (BASS-REG-1 + WALK-2) closed (2026-07-27); this matrix swept at B4 — DOC-CLOSE-2 (2026-07-28).** **No primary-home flip.** Seven rows were ADDED above for concepts that did not previously exist — the colour table, per-event secondary dominants, cadence metadata, `tonalities`-as-metadata (with F-B2-LIBRARY), the MOD-1 planner, the bass register contract and the improvised walk — and nothing was relocated. Homing notes: (1) `tonalities` semantics are primary-homed on the RUNTIME side (`runtime/SSoT_Composer_Backing_Track.md` §2.2) because the contract being asserted is a runtime one ("the runtime does not consult it"), with the authoring SSoT secondary — the mirror of the RUNTIME-REQUALITY split, where the policy's MEANING is authoring-primary and its APPLICATION SITES are runtime-secondary. (2) `ModulationPlanner` is homed under the chord AUTHORING SSoT despite living in `Runtime/`: it has ZERO in-package callers, so orchestration was rejected on evidence — `SongOrchestrator` never touches it — and it sits beside the rest of the B1 opt-in surface because it emits `ChordProgressionData`-shaped material. Precedent for an authoring SSoT governing `Runtime/` files: PATTERN-PERSIST-1 governs-B. If an in-package caller ever appears, the home moves. (3) The bass register contract is primary-homed at §2 (where the band and the ceiling are defined) rather than at any of its three fold sites, which are secondary — one contract, three consumers.

- **F-B2-LIBRARY, recorded at B4 (2026-07-28).** `ChordTrackComposer.PickTemplateForPart` still discards library entries whose allowed tonality list excludes the part's tonality. This is INTENDED code — B2 left the legacy procedural path out of scope and said so on `planning/active/Roadmap_Composition_Expressivity.md` §B2 — but the only record of the nuance lived in a roadmap, which `SSoT_CONTRACTS.md` §6 forbids treating as authority. Both SSoTs now carry the exception. Retiring the filter changes renders and is an unscheduled RUNTIME candidate, not a documentation one.

- **Standing governance gap, recorded at B4 (2026-07-28), NOT resolved:** `changelog-ssot.md` is cited by the `SSoT_CONTRACTS.md` §9 completion contract as a mandatory update target, yet it is registered in no authority class in `ssot_manifest.yaml`. Registering it is a governance decision with its own scope and was deliberately not taken in a documentation batch.

- **Five batches applied at DOC-SWEEP-3 (2026-09-01): PRIMARY-HOME FLIP (one).**
  `authoring/SSoT_Authoring_Bass_Cards.md` is NEW and now primary for bassline
  card authoring — the SelfPocket DSL and the editor window contract. Nothing
  was relocated into it: the articulation classes it types stay primary under
  `runtime/SSoT_Composer_Bass_Track.md` §3.7.x, and the new document says so in
  its own header. Registered in `ssot_manifest.yaml` (authority_class
  `subsystem_ssot`, four governs, nine invariants) and in `SSoT_INDEX.md`.
  **Six rows were ADDED above** for concepts that did not previously exist —
  bass card authoring, the SelfPocket phrase surface, the chord-identity
  contract, the melody guide-note cache, diagnostic harnesses, and phrase
  archetype materialization. One row was EXTENDED (bass register contract, now
  two-sided). No row was relocated.
  Homing notes:
  (1) **The chord-identity rule is homed on `SSoT_CONTRACTS.md` §13, not on any
  composer SSoT.** It is cross-cutting by construction — the defect it fixes
  existed precisely because four independent components each read `degree`
  alone and no single composer's document could have caught it. Same shape as
  §12 (PartConfig mutation), added at DOC-SWEEP-2.
  (2) **The tonality matrix is homed under `authoring/SSoT_Authoring_Tools.md`
  §3.F, a NEW category** for tools that author nothing. This widens that
  document's scope, taken as sweep decision D-S3-3=B. `CompositionSmokeWindow`
  remains intentionally ungoverned — **D-SMOKE-DOC-1=A is unchanged**, and
  MGP-TONALITY-2's proposed "Save as new…" clause was skipped rather than
  silently reversing it. §3.F restates that boundary in place.
  (3) The Harmony role still has **no composer SSoT** (HARMONY-1 audit item 9,
  deferred). Its contract is split between Orchestration §5.9 (the channel) and
  the Melody SSoT (payload units), and `CURRENT_STATE.md` "Docs to update next"
  registers the absence rather than papering over it.
  **New test files**, registered in this closure-notes convention (D-2=A):
  `Tests/Editor/BassTrackComposer_PhraseTests.cs`,
  `Tests/Editor/BassPatternTextParserTests.cs` (15 pins),
  `Tests/Editor/HarmonyTrackComposer_GuideFollowTests.cs` (9 tests).

- **Anchor amendment recorded at DOC-SWEEP-3 (D-S3-4=A).** `MGP-TONALITY-2`'s
  diffs cited Bass SSoT **§3.6.1**, which does not exist; the anchors resolve in
  **§3.6bis**, which rows 22–23 above already cite by name. Applied amended.
  This file was the tiebreaker, which is the §3-of-the-update-protocol case
  working as intended.

- **Pre-existing omission fixed at DOC-SWEEP-3, recorded here.**
  `runtime/SSoT_Composer_Bass_Track.md` was missing from `SSoT_INDEX.md`'s
  "Primary runtime SSoTs" list although registered in `ssot_manifest.yaml` and
  cited as primary by this file since CA-F2 (2026-07-15). A list omission, not
  an authority change; listed, with the omission noted in place.

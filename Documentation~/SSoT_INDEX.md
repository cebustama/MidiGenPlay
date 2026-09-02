# SSoT_INDEX

## Purpose

This file defines the documentation authority model for **MidiGenPlay**.

Its job is to answer:

- which documents are authoritative,
- which folders are secondary,
- what wins when documents disagree,
- and how package truth is separated from cross-project integration material.

## Authority order

When documents disagree, resolve conflicts in this order:

1. Relevant primary SSoT in `runtime/` or `authoring/`
2. `SSoT_CONTRACTS.md`
3. `coverage-matrix.md`
4. `CURRENT_STATE.md` for operational focus only
5. `reference/`
6. `planning/`
7. `research/`
8. `archive/`

## Primary documentation spine

Root governance docs:

- `README.md`
- `SSoT_INDEX.md`
- `SSoT_CONTRACTS.md`
- `coverage-matrix.md`
- `changelog-ssot.md`
- `CURRENT_STATE.md`

Primary runtime SSoTs:

- `runtime/SSoT_Runtime_Song_Model_and_Config.md`
- `runtime/SSoT_Runtime_Generation_Orchestration.md`
- `runtime/SSoT_Composer_Backing_Track.md`
- `runtime/SSoT_Composer_Rhythm_Track.md`
- `runtime/SSoT_Composer_Melody_Track.md`
- `runtime/SSoT_Composer_Bass_Track.md` (primary since CA-F2, 2026-07-15 —
  omitted from this list until DOC-SWEEP-3, 2026-09-01; it has been registered
  in `ssot_manifest.yaml` and cited as primary by `coverage-matrix.md`
  throughout, so this is a list omission, not an authority change)

Primary authoring SSoTs:

- `authoring/SSoT_Authoring_Chord_Progressions.md`
- `authoring/SSoT_Authoring_Rhythm_Patterns.md`
- `authoring/SSoT_Authoring_Melody_Composition.md`
- `authoring/SSoT_Authoring_Bass_Cards.md` — bassline card authoring: the
  SelfPocket text DSL, the editor window contract, and the advisory set.
  Authoring only; the articulation classes themselves are governed by
  `runtime/SSoT_Composer_Bass_Track.md` §3.7.x. (Primary since
  MGP-BASSCARD-WIZARD-1, applied 2026-09-01.)
- `authoring/SSoT_Authoring_Tools.md`
- `authoring/SSoT_Authoring_LLM_Generation.md` (cross-cutting; primary since Batch
  L3, 2026-05-28 — omitted from this list until 2026-07-24)
- `authoring/SSoT_Authoring_MIDI_Import.md` (cross-cutting; primary since
  MIDIIMP-SSOT-1, 2026-07-24)

## Folder roles

### `runtime/`
Authoritative docs for runtime model, orchestration, and composer behavior.

### `authoring/`
Authoritative docs for package-owned authoring assets, authoring pipelines, and editor-facing authoring semantics.

### `reference/`
Non-authoritative supporting docs. Includes package-adjacent notes and cross-project integration documents.

### `planning/`
Roadmaps and active plans. Planning is not implementation truth.

### `research/`
Exploratory design notes, research prompts, and speculative future work. Never authoritative for implemented behavior.

### `archive/`
Preserved historical docs, superseded docs, and absorbed source material.

## Explicit package boundary rules

The following are **not** primary package authority unless explicitly promoted later:

- ALWTTT gameplay/card runtime behavior
- `MidiMusicManager`-centric live game playback docs
- composition session bridge docs
- roadmaps
- research prompts
- legacy `CardData` redesign material

Cross-project documents may describe how ALWTTT consumes MidiGenPlay, but they do not define MidiGenPlay package truth.

## Reading order by task

### If changing runtime song/config state
Read:
- `runtime/SSoT_Runtime_Song_Model_and_Config.md`
- `SSoT_CONTRACTS.md`
- `CURRENT_STATE.md`

### If changing generation pipeline or orchestration
Read:
- `runtime/SSoT_Runtime_Generation_Orchestration.md`
- relevant composer SSoT
- `CURRENT_STATE.md`

### If changing authoring tools or authoring assets
Read:
- relevant doc in `authoring/`
- `runtime/` SSoT if runtime semantics also change
- `CURRENT_STATE.md`

### If touching ALWTTT integration
Read:
- primary package SSoT first
- then `reference/cross-project/ALWTTT/`
- never update cross-project docs as a substitute for package truth

## Local update protocol reminder

After every meaningful technical change:

1. Identify what concept actually changed.
2. Find its primary home in `coverage-matrix.md`.
3. Update that primary SSoT first.
4. Then:
   - update `CURRENT_STATE.md` if active focus or operational reality changed,
   - update `changelog-ssot.md` if semantics/authority/interpretation changed,
   - update `coverage-matrix.md` only if the primary home changed,
   - update reference docs only if workflow or usage changed.

A technical change is not complete until the required documentation updates are done.

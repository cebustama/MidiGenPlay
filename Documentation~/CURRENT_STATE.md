# CURRENT_STATE

## Active now

1. Apply Phase 7 documentation updates (six governed files) to close the batch formally
2. Decide and schedule the `ComposeFromGrid` → `SnapshotAsStepVelocities` runtime
   switch (deferred from Phase 6; not blocking authoring work)
3. Surface and confirm Phase 8 open decisions (persistence/repository cleanup
   for rhythm authoring tools)

## Just completed

- Closed Phase 7: text/DSL authoring mode for `DrumPatternEditorWindow`
  - `DrumPatternTextParser` (pure-function parse / render / per-cell-diff apply)
    and `DrumPatternTextWarning` types added in `Editor/`, namespace
    `MidiGenPlay.Authoring`
  - Whole-window Grid / Text tab toolbar in `DrumPatternEditorWindow`; text-mode
    lane row shows read-only "Instrument (vNNN)" + single text field + remove
  - Parse on tab-switch and on Apply; per-cell diff preserves non-canonical
    per-step velocity for cells whose typed glyph hasn't changed
  - 3-tier glyph alphabet (v1): `.` and `-` = rest, `x` = lane default
    (sentinel), `X` = accent (120), `o` = ghost (50); `|` and whitespace
    ignored; short input right-pads with rests, long input right-truncates;
    both length cases emit a warning
  - HelpBox legend at the top of the text pane; warning panel below the lanes
  - Asset model untouched: `DrumPatternData`, `StepState`, working-copy /
    apply / save-as contract unchanged
  - First test assembly in the package: `Tests/Editor/MidiGenPlay.Tests.Editor`
    with 13 EditMode tests covering SMR1 (alternation), SMR2 (glyph→velocity),
    SMR4 (short pad + warn), SMR5 (unknown glyph + warn), plus render snap,
    bar-separators, ignored chars, dash equivalence, per-cell-diff round-trip
  - Three manual smoke tests verified: SMR3 grid↔text round-trip, SMR6
    signature change re-renders text, SMR7 SaveAs preserves text-mode edits
  - D-T9 final placement: flat `Editor/` (matches package convention), namespace
    `MidiGenPlay.Authoring`. Initial namespace `MidiGenPlay.Editor.Authoring`
    shadowed `UnityEditor.Editor` and broke `SoundFontCacheSOEditor`; renamed
- Established package test-authoring conventions
  - `package.json` gains `"testables": ["MidiGenPlay.Tests.Editor"]`
  - Canonical asmdef shape captured in
    `reference/package/Tests_Authoring_HowTo.md` (new), including the
    `defineConstraints: ["UNITY_INCLUDE_TESTS"]` gotcha that silently blocks
    compilation in Unity 2022.3 local-package contexts
- Closed MGP-ALWTTT-MOD-DIR-1: directional modulation hint for `ChordTrackComposer`
  - Added `ModulationOctaveHint { Auto, Up, Down }` enum (default `Auto`)
  - Added two `[NonSerialized]` transient fields on `PartConfig`:
    `PreviousRootNote`, `ModulationOctaveHint`
  - `ChordTrackComposer.Compose` captures and clears the transients on entry;
    both internal render sites apply a shared directional first-chord override
  - Default behavior bit-identical to prior output; determinism preserved
  - Cross-project follow-up (ALWTTT side) tracked via rehydration prompt
    (`ALWTTT-MOD-DIR-2`)
- Implemented Phase 6: `StepState` struct replaces `List<bool>` in
  `DrumPatternData.Lane`
  - `StepState { bool active; int velocity; }` — velocity 0 is the sentinel
    (defer to lane default)
  - `StepState.ResolveVelocity(int laneDefault)` encapsulates effective
    velocity resolution
  - `SnapshotAsIndices()` return signature unchanged — existing runtime callers
    unaffected
  - `SnapshotAsStepVelocities()` added as the per-step-velocity-aware forward
    snapshot
  - `DrumPatternEditorWindow` extended with `[T]`/`[V]` row mode toggle

## Next

1. Phase 8: route `DrumPatternEditorWindow` save paths through package
   store/repository abstractions (`IPatternRepository` /
   `PatternRepositoryResources` already exist)
2. Runtime micro-batch: update `ComposeFromGrid` to call
   `SnapshotAsStepVelocities` so per-step velocity reaches generated MIDI
   (independent of authoring; can run before or after Phase 8)
3. Resume phrasing / feel runtime completion only after Phase 8 is done
   (Phase 9)
4. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to
   legacy/reference status

## Blocked / not implemented yet

- Per-step velocity in generated MIDI: `ComposeFromGrid` still uses
  `SnapshotAsIndices` (lane default velocity); consuming
  `SnapshotAsStepVelocities` is a deferred runtime change
- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists
  in the repository

## Docs to update next

- `runtime/SSoT_Composer_Rhythm_Track.md` — when `ComposeFromGrid` is updated
  to consume per-step velocity
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — when Phase 8 work begins
- `authoring/SSoT_Authoring_Tools.md` — when persistence routing changes
  in Phase 8

## Working rule

If the next technical change touches rhythm generation, rhythm authoring, or
the rhythm editor:

- update the primary rhythm SSoT first
- then update `CURRENT_STATE.md` if active focus or reality changed
- then update `planning/active/Roadmap_Rhythm_Authoring_MVP.md` if the
  next-step sequence changed
- then update `changelog-ssot.md` if semantics or authority changed
- then update `runtime/SSoT_Composer_Rhythm_Track.md` if runtime generation
  behavior changed

# CURRENT_STATE

## Active now

1. LLM-Assisted Authoring **Batch L4** — chord editor generalization. Apply the
   replicable pattern documented in `authoring/SSoT_Authoring_LLM_Generation.md`
   to `ChordProgressionEditorWindow` (vocabulary SO → pure prompt builder →
   generator wrapper → importer → alias dictionary → async handler → window
   wiring). See `planning/active/Roadmap_LLM_Authoring_MVP.md` §"Batch L4".
   Proposed sibling: **Batch L5 (L-PAL)** — DrumPattern palettes + catalogue
   wizard.

## Just completed

- Closed LLM Authoring **Batch L3** (2026-05-28): smoke-test sign-off + governance.
  This closes the LLM Authoring MVP (L1–L3). Cost-cap UI (D-L3.1) wired the
  pre-network `maxCharBudget` guard to a per-window "Max prompt chars" field
  surfaced through the warning panel. Mock-client seam (D-L3.2) added
  `FakeLLMClient` + `DrumPatternLLMGeneratorTests` (6 tests) making SMR-L3/L5
  deterministic against the real `PromptExecutionHelper` → `ILLMClient`
  delegation. Full SMR-L1..L7 manual sign-off passed (plain happy-path runs
  clean; `LengthLong` truncation under free-text direction is expected
  contained LLM behavior, not a defect). New SSoT
  `authoring/SSoT_Authoring_LLM_Generation.md` written as a replicable pattern
  and flipped to primary (coverage-matrix); roadmap demoted to L1–L3 history.
  Governed-doc flips applied across manifest, coverage-matrix, Rhythm Patterns
  §3A, Tools SSoT, and the roadmap. All EditMode tests green.

- Closed LLM Authoring **Batch L2** (2026-05-28): editor UI integration. Wired
  the LLM-Assisted Generation panel into `DrumPatternEditorWindow` — genre
  dropdown (from `Default Rhythm Genres.asset`, 8 genres seeded via a new
  `RhythmGenreVocabularyBuilder` menu), async non-blocking Generate (D-L2.3),
  Regenerate (D-L2.4=A), clipboard Import (D-L8), client resolution with
  default + override (D-L2.1=B). New pure-function seams:
  `DrumPatternEditorImporter` (setup-card + DSL parse, fence-agnostic glyph
  detection; 12 tests), `LaneAliasDictionary` (23 short-name aliases; 11 tests),
  `DrumPatternLLMResponseHandler` (unifies Generate + Import into one applicable
  outcome; 5 tests). Generate and Import share one apply path through the
  importer (D-L2.2=A). Importer revised mid-batch from fence-first to
  glyph-content detection (SMR-L6 learning); CRLF split bug fixed (test line
  endings under CRLF). SMR-L1/L2/L4/L6/L7 pass; SMR-L3/L5 deferred to L3. All
  EditMode tests green.

- Closed LLM Authoring **Batch L1** (2026-05-28): vocabulary SO + 11-test
  prompt builder + LLM Core generator + console harness. First clean
  end-to-end run against Anthropic `claude-sonnet-4-6` (972 in / 218 out
  tokens, zero parser warnings, 4-lane funk pattern). Provider switched
  OpenAI → Anthropic mid-batch via a cross-project LLM Core sub-batch; no
  MidiGenPlay code change needed (factory seam). `Default Rhythm Genres.asset`
  full population deferred (completed in L2). D-L1..D-L11 locked; see roadmap.

- Closed runtime micro-batch: `ComposeFromGrid` now consumes
  `SnapshotAsStepVelocities`. Per-step velocity authored in
  `DrumPatternData` reaches generated MIDI for every grid-authored rhythm
  track. `SnapshotAsIndices` retained as a default-velocity-only view but
  no longer called by any runtime composer. 3 EditMode tests lock the
  `SnapshotAsStepVelocities` contract (sentinel resolution, all-off lane,
  multi-lane independence). Closes the deferred runtime gap from Phase 6.

- Closed MGP-ALWTTT-MOD-DIR-1.1 + 1.2 (package side) and ALWTTT-MOD-DIR-3
  (cross-project side). End-to-end directional modulation work now closed
  across both projects.
  - 1.1: directional first-chord anchor moved from notional centerOct to
    actual previous first-chord root pitch held in
    `ChordTrackComposerFactory`'s per-track memory (keyed
    `(part.Name, MusicianId)`). Cold-start fallback to centerOct preserved
    (SM-DIR-3 bit-identical regression baseline). `ChordTrackComposer` ctor
    extended with two optional params (backward-compatible); public
    `PartConfig` surface unchanged.
  - 1.2: §6.2 SMD5 contract reframed as a natural property of the strict
    `>` / `<` comparison rather than a special case. Same-root +
    boundary-clamp documented as a deliberate collapse to silence (Option 1)
    over wrap-on-clamp (Option 2 rejected because wrapping inverts user
    musical intent).
  - ALWTTT-MOD-DIR-3 (cross-project, ALWTTT side):
    `MidiMusicManager.RenderSinglePart` now forces `cacheEnabled = false`
    when either `PartConfig` modulation transient is non-default. Surfaced a
    new package-level SSoT invariant: transients are NOT part of cacheable
    input (`SSoT_Composer_Backing_Track.md §6.5`).
  - 9 EditMode tests cover the directional helper at the `Core` seam,
    including the SM-DIR-1 failure reproduction (C#5 → G#5), Down symmetry,
    range-clamp fallback, Auto short-circuit, and three SMD5 cases (same-root
    Up/Down non-boundary + Up at-top-boundary).
  - Six ALWTTT smoke tests pass (SM-DIR-1..6). SM-DIR-7 (range-clamp
    fallback in scene) deferred — requires a narrow-range debug instrument
    that doesn't exist yet; package-side coverage already locks the contract
    via `Remembered_Up_BeyondTopOfRange_ClampsToMaxOct`.
  - Diagnosis template captured: when scene behavior contradicts
    EditMode-verified package behavior, suspect a consumer-side
    short-circuit above the package entry point before suspecting the
    package itself. The SMD5 failure was traced via log-absence
    ([Mod-DIR/Handoff] not firing) to `MidiMusicManager._partBundleCache`
    keying on serialized fields only.
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

1. LLM-Assisted Authoring Batch L4: chord editor generalization, following the
   replicable pattern documented at L3 (`authoring/SSoT_Authoring_LLM_Generation.md`)
2. **Batch L5 (L-PAL)** — DrumPattern palette asset + "Add to Palette" in the
   editor + Drum Catalogue Wizard (proposed; see roadmap §"Batch L5")
3. Phase 8: route `DrumPatternEditorWindow` save paths through package
   store/repository abstractions (`IPatternRepository` /
   `PatternRepositoryResources` already exist). Demoted from active to
   next per 2026-05-24 sequencing decision.
4. Resume phrasing / feel runtime completion only after Phase 8 is done
   (Phase 9)
5. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to
   legacy/reference status

Future (recorded, not scheduled): fill tag system (R3 — runtime/Composer
concern; see roadmap §"Future work").

## Blocked / not implemented yet

- LLM-assisted authoring for chord progressions (Batch L4, deferred)
- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists
  in the repository

## Docs to update next

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — when Phase 8 work begins
- `authoring/SSoT_Authoring_Tools.md` — when LLM authoring surface lands
  (Batch L2 closure), and again when persistence routing changes in Phase 8
- `authoring/SSoT_Authoring_LLM_Generation.md` — **created at Batch L3 closure
  (2026-05-28)**; primary authority for LLM-assisted authoring across the
  package. Update when a new tool adopts the pattern (chord editor at L4) or a
  §3 contract changes.

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

If the next technical change touches LLM-assisted authoring (new track as of
2026-05-24):

- update `planning/active/Roadmap_LLM_Authoring_MVP.md` to record batch
  closure and decisions locked
- update `CURRENT_STATE.md` if active focus shifts between batches
- update `authoring/SSoT_Authoring_LLM_Generation.md` (primary, created at L3
  closure 2026-05-28) when a tool adopts the pattern or a §3 contract changes
- update `changelog-ssot.md` per batch with the standard shape
- the coverage-matrix primary home already points at the new SSoT (flipped at
  L3 closure); keep it there unless authority changes

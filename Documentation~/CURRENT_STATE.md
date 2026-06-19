# CURRENT_STATE

## Active now

- **No batch currently active.** **Melody Authoring MVP — Phase 3 (generation-params UI + simplified editor-only generator) closed 2026-06-17** (its own arc, `Roadmap_Melody_Authoring_MVP`): the wizard's generation-parameters top section (Tier-1 params on `MelodyGenerationParamsSO`) + `SimplifiedMelodyGenerator` (`Editor/`, deterministic, editor-only) landed — Unity green, manual smoke passed. Phases 1 (data model), 2 (ladder grid), and 3 are all closed; **Phase 4 (runtime hookup — `MelodyTrackComposer.ComposeFromPattern`, the first audible phase) is the next batch.** Recently
  closed on other arcs: **CQ-A1 + CQ-B1** (chord quality alphabet v2, Tier A+B —
  6ths/7sus4, then 9ths) 2026-06-16, and the **Composition Expressivity (CE)** arc
  (CE-E1/CE-F1 2026-06-10, CE-L1 2026-06-11). Other open threads: the
  chord-inversion voicing hint (CQ-A1 Objective 2; voicing layer, gated on
  `Strategies/VoiceLeading.cs`), D-L4.3 generic unification, and palette/seed-library
  expansion.

## Just completed

- Closed **Melody Authoring MVP — Phase 3 (generation-params UI + simplified generator)**
  (2026-06-17): added the wizard's generation-parameters top section bound to
  `MelodyGenerationParamsSO` (Tier-1: scale/tonality, GM instrument hint, density, octave
  range, rhythmic style Even/Syncopated/Burst, seed) and `SimplifiedMelodyGenerator`
  (`Editor/`, `MidiGenPlay.Authoring`) — an editor-only, deterministic generator mapping
  those params into a `MelodyPatternData` working copy (density → onsets/measure; style →
  placement; octave range → offset bounds; scale → diatonic degrees, stability-weighted).
  Determinism via `System.Random(seed)`; onset placement is RNG-free, so a new seed re-rolls
  pitch over a fixed groove. `MelodyGenerationParamsSO` gained `seed` + `instrumentHint`
  (the latter informational-only — the pattern carries no instrument and it is never read at
  runtime). Working-copy isolation preserved (asset untouched until Apply/Save As); no
  runtime / `ComposeFromPattern` change (Phase 4). One new Editor-only file,
  `#if UNITY_EDITOR`-guarded; no `Runtime/` leak. Unity green; no tests (Phase-3 DoD requires
  none; manual smoke pass). D-MEL3.1–3.3 locked. Governed by
  `SSoT_Authoring_Melody_Composition` §5. Next: Phase 4 (runtime hookup).

- Closed **Melody Authoring MVP — Phase 2 (ladder note-grid editor)** (2026-06-16):
  shipped `MelodyPatternEditorWindow`, a scene-independent package EditorWindow
  (`MidiGenPlay / Melody Pattern Editor...`) with a scale-degree "ladder" grid
  (Y = 7 diatonic degrees × octave bands, X = time steps), working-copy isolation
  (`DeepCloneRuntime`, asset untouched until Apply/Save As), a per-note inspector
  (degree / octave / start / length / velocity), a configurable octave window, an
  explicit Normalize, and Apply / Save As. Authors `MelodyPatternData` only — no
  runtime change, no generation-params UI, no text/DSL mode. One Editor-only file,
  fully `#if UNITY_EDITOR`-guarded; no `Runtime/` leak. Unity green; no tests
  (Phase-2 DoD requires none; validated by manual smoke pass). D-MEL2.1–2.4 locked.
  Governed by `SSoT_Authoring_Tools` §3.A + `SSoT_Authoring_Melody_Composition` §5.
  Next: Phase 3 (generation-params UI + simplified generator).

- Closed **Melody Authoring MVP — Phase 1 (data model)** (2026-06-16): redesigned
  `MelodyPatternData` to a deterministic per-note model (`MelodyNoteEvent` struct —
  one `ScaleDegree` + octave offset + beat-relative start/duration + velocity;
  pitch resolved at runtime, not stored), added `MelodyGenerationParamsSO` (a
  generation-time-only bundle for the planned wizard), and removed the legacy
  probabilistic `possibleDegrees` model with its sole consumer
  `MidiGenerator.GenerateMelodyTrackWithPattern` (M-3, clean break; two orphaned
  privates + a dead `using` removed too). Both new types are package runtime
  (`Runtime/CoreScripts/Data/` + `.../Composition/Data/`) and are now governed by
  `SSoT_Authoring_Melody_Composition`. Unity green; no tests required (data-model
  swap, procedural path untouched); repo grep clean bar inert comments in the
  demoted `EmotionalGenerationPanel` / `MidiGenPlayPanel`. D-MEL1.1–1.5 locked.
  Next: Phase 2 (ladder note-grid editor).

- Closed **CQ-B1 (chord quality alphabet v2 — Tier B, ninths)** (2026-06-16):
  appended `Dominant9` {0,4,7,10,14}, `Major9` {0,4,7,11,14}, `Minor9`
  {0,3,7,10,14} to `MusicTheory.ChordQuality` (append-only, ordinals 14–16;
  existing serialized assets unaffected). Explicit-suffix-only (`9`/`maj9`/`m9`);
  no change to diatonic inference (`vi9` = dominant-ninth; minor-ninth is `vim9`).
  Lockstep across `RomanProgressionParser`,
  `ChordProgressionLLMResponseHandler.AllowedSuffixes`, the prompt-builder alphabet
  (9/maj9/m9 removed from Forbidden; 11/13/add9/6-9 remain),
  `ChordProgressionEditorWindow.QualitySuffixForToken` + `IsSeventhQuality` (all
  three ninths are sevenths for grid arity), and
  `ChordQualityResolver.GetTriadFamily` (Dom9/Maj9 → Major, Min9 → Minor). Five
  voices, realized via `BasicVoiceLeadingVoicer`; the one runtime change is
  uncapping its inversion loop (`i < 4` → `i < pcs.Length`), byte-identical for
  ≤4-voice chords. New arc roadmap `planning/active/Roadmap_Chord_Expressivity.md`.
  Two test fixtures inverted (`9` is now valid, not forbidden) + three extended;
  full suite green; `Runtime/` grep clean. Known deltas: grid renders ninths as
  4-of-5 rows; five-voice `Drop2` inert and a tall five-voice stack can collapse at
  range edges (pre-existing voicer nuances). D-CQB1.1..5 locked. Only the
  chord-inversion voicing hint remains in the arc.

- Closed **CQ-A1 (chord quality alphabet v2 — Tier A)** (2026-06-16): appended
  `Major6`, `Minor6`, `Dominant7sus4` to `MusicTheory.ChordQuality` (append-only,
  ordinals 11–13; existing serialized assets unaffected). Explicit-suffix-only
  (`6`/`m6`/`7sus4`); no change to diatonic inference (`vi6` = major-sixth;
  minor-sixth is `vim6`). Lockstep across `RomanProgressionParser`,
  `ChordProgressionLLMResponseHandler.AllowedSuffixes`, the prompt-builder
  alphabet, `ChordProgressionEditorWindow.QualitySuffixForToken` (round-trip fix)
  + `IsSeventhQuality` (Decision A), and `ChordQualityResolver.GetTriadFamily`
  (new qualities classify Major/Minor/Suspended for `isDiatonic` rather than
  `Other`). Intervals `{0,4,7,9}`/`{0,3,7,9}`/`{0,5,7,10}` — all ≤4 voices,
  voiced through the existing voicer unchanged. Canonical home = the
  `ChordQuality` enum (authoring SSoT §4.1). Five new EditMode fixtures, full
  suite green; `Runtime/` grep for unguarded `ChordQuality` switches clean. Tier B
  (ninths, 5-voice) + the inversion voicing hint deferred, both gated on
  `Strategies/VoiceLeading.cs`. Known deltas: grid renders the 6th chords as
  3-row triads; `Dominant7sus4` flagged non-diatonic vs a major V (sus-consistent).

- Closed **CE-L1 (LLM card-author)** (2026-06-11): fourth mirror of the LLM
  authoring stack and its first **consumer-side** instance —
  `CardLLMPromptBuilder/Generator/ResponseHandler/FieldPlan` in a new
  ALWTTT-side editor asmdef pair (`ALWTTT.Cards.LLMAuthoring` + `.Tests`),
  plus `CardLLMVocabulary` (live-snapshot POCO, D-CE-L1.4),
  `CardPaletteIntentResolver` (intent → deterministic seeded pick over the
  CE-F1 `PaletteSelector`), `CardImportDtoParser` (DTO hoist shared with the
  JSON box), and a "Generate with LLM" panel in `CardEditorWindow` staging
  through the existing `TryStageCardFromDto`. Banned-asset-reference guard +
  out-of-alphabet guard in the handler (D-L4.5 doctrine); bundle + palette
  written only at Save (D-CE-L1.6); `modifierEffectNames` resolved
  all-or-nothing at staging. 77/77 tests; live smoke green (1572/366 tokens).
  D-CE-L1.1..7 locked. Package itself unchanged (game-side editor code only).

- Closed **CE-F1 (shared palette selector)** (2026-06-10): extracted the TS-aware
  selection policy out of `BackingCardConfigSO` into a shared, deterministic
  `PaletteSelector` (Tier A/B/C) over a neutral `TsFeatures` summary, with typed
  `ProgressionFinder`/`PatternFinder` (`.../Data/PaletteSelection.cs`). Deleted the
  ~250-line reflection + Tier-helper block from the backing config. `RhythmCardConfigSO`
  gained a TS-aware `PickPatternOverride(rng, ts, settings, verbose)` and
  `RhythmTrackComposer` calls it with the Part TS, so the drum side is now TS-aware
  (deferred from PCE). Both palette TS toggles are consumed in the one selector ->
  asymmetry resolved. Drum density = capped foundational-onset (kick) density (D-F1.5).
  New `Tests/Editor/PaletteSelectorTests.cs`. Determinism preserved (one `NextDouble`
  per pick). D-F1.1..5 locked. Known delta: duplicate palette references are now
  independent weighted slots (no de-dup); normal palettes are byte-identical per seed.

- Closed **CE-E1 (Card Editor ergonomics)** (2026-06-10): added a **Clone Card** action
  to `CardEditorWindow` (ALWTTT) that deep-copies the payload and **clones the style
  bundle** into its own asset — fixing the Ctrl+D bug where a duplicated card silently
  shares the source palette — plus New-Card preset buttons (Action / Composition /
  Rhythm / Backing / Melody / Harmony). Preset role hand-off is via name-strings
  resolved against `TrackRole`'s enum names, so it never compile-couples to specific
  roles. Game-side editor change only; no package runtime change.

- Closed **PCE (Palette Consumption / Composition Expressivity)** (2026-06-04):
  wired drum-palette consumption into `RhythmTrackComposer` via
  `RhythmCardConfigSO.patternPalette` + `PickPatternOverride(ctx.rng)`, mirroring
  the backing `PickProgressionOverride` seam (legacy/non-TS picker). Authored 5
  drum-pattern palettes (one pattern each) and wired two 4/4 cards for the §5
  distinctness experiment. Smoke pass green (determinism / consumption /
  distinctness). Backward-compatible: cards with no palette behave as before.
  Drafted the CE roadmap (CE-E1 / CE-F1 / CE-L1).

- Closed LLM Authoring **Batch L5 (L-PAL)** (2026-05-29): DrumPattern palettes +
  editor integration + catalogue wizard. New `DrumPatternPaletteSO` (weighted,
  deterministic seeded `PickRandomPattern`, clone-on-pick; inert TS-aware toggle
  mirroring the chord palette), `DrumPatternCatalogueWizard` (read-only browser;
  derived metadata TS/measures/subdivisions/instruments/active-step density;
  filter/sort/search; ping-on-select), and a palette section in
  `DrumPatternEditorWindow` ("Add to Palette" referencing a saved asset,
  dedup-guarded; project-scan dropdown). 9 palette tests + the 4-item smoke pass
  green. Decisions D-PAL.1..5 locked (1=reference-saved, 2=scan-folders+fallback,
  3=author-only, 4=weighted, 5=SO under rhythm composer SSoT). Author-only for
  now: no runtime path consumes drum palettes yet — composer consumption is the
  declared next phase **(superseded by PCE 2026-06-04 — drum palettes are now
  consumed at runtime; see the top PCE entry)**. Artifacts registered in `ssot_manifest.yaml`; roadmap §L5
  flipped to CLOSED.

- Closed LLM Authoring **Batch L4** (2026-05-29): chord editor generalization —
  the second adopter of the LLM authoring pattern. `ChordProgressionEditorWindow`
  gained Generate/Regenerate/Import mirroring the drum surface, routed through the
  editor's existing `ParseAndPreview`/`ApplyToAsset` path (determinism invariant
  untouched; the chord asset is the seam consumed by `ChordTrackComposer`). New
  artifacts: `ChordGenreVocabularySO` (+ `ChordGenreVocabularyBuilder` seeder,
  v1 set jazz/pop/blues/folk with build-time parser+guard self-check),
  `ChordProgressionLLMPromptBuilder` (Roman-string output, D-L4.1; alphabet
  verified against `RomanProgressionParser`), `ChordProgressionLLMGenerator`,
  `ChordProgressionEditorImporter`, `ChordProgressionLLMResponseHandler` (carries
  the D-L4.5 token-allowlist guard), `ChordLLMFieldPlan` (pure outcome→field
  mapping, D-L4.7), and the `.LLM` window partial (+ a "Create New Progression"
  reset affordance). Key contract clarification (D-L4.5): `RomanProgressionParser`
  warns-and-downgrades unknown suffixes rather than failing, so the
  no-silent-fallback guard lives in the response handler — documented in
  `SSoT_Authoring_LLM_Generation.md` §3.3. 47 chord LLM EditMode tests + smoke
  tests CSMR-S1..S8 pass; full suite green. Decisions D-L4.1..D-L4.8 locked.
  Doc flips applied across manifest, LLM SSoT (§3.3/§7), coverage-matrix,
  changelog, and the roadmap (§"Batch L4" → closed). This completes the LLM
  Authoring MVP through L4.

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

1. **Chord-inversion voicing hint** — build the CQ-A1 Objective 2 recommendation:
   a per-chord inversion/bass hint in the voicing layer (following the §6
   modulation-override precedent), governed by
   `runtime/SSoT_Composer_Backing_Track.md`. Not in the Roman DSL. Gated on the
   same `Strategies/VoiceLeading.cs` review (now partially done — inversion loop
   uncapped).
2. **D-L4.3 unification** (optional) — extract a shared generic over the drum and
   chord prompt builders / generators now that two working instances exist.
3. Phase 8: route `DrumPatternEditorWindow` save paths through package
   store/repository abstractions (`IPatternRepository` /
   `PatternRepositoryResources` already exist).
4. Resume phrasing / feel runtime completion only after Phase 8 is done (Phase 9).
5. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to
   legacy/reference status.

Future (recorded, not scheduled): fill tag system (R3 — runtime/Composer
concern; see roadmap §"Future work").

## Blocked / not implemented yet

- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists
  in the repository

## Docs to update next

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — when Phase 8 work begins
- `authoring/SSoT_Authoring_Tools.md` — when LLM authoring surface lands
  (Batch L2 closure), and again when persistence routing changes in Phase 8
- `authoring/SSoT_Authoring_LLM_Generation.md` — created at Batch L3 closure
  (2026-05-28); chord adopter added at L4 closure (2026-05-29, §7) with the
  §3.3 degrade-vs-fail clarification. Primary authority for LLM-assisted
  authoring across the package. Update when the next tool adopts the pattern or
  a §3 contract changes.

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

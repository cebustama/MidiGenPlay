# changelog-ssot

## 2026-06-22 — Melody Authoring MVP Phase 5: polish, validation, documentation closure (MVP complete)

### Validated (no runtime change)
- `MelodyTrackComposer.ComposeFromPattern` edge cases, by code-trace against the shipping
  method — all correct and deterministic; no code change required (D-MEL5.1 = A):
  - empty pattern → silence, no crash (`MelodyPatternData.TotalBeats ≥ 1` and the
    `Math.Max(1.0, …)` loop floor prevent a zero divisor; the empty `SnapshotOrdered()` list
    emits nothing; `ToFile`/`StampBankAndPatch`/`ForceAllChannel` don't throw on zero notes;
    the guide-note cache call is null-guarded);
  - single-note pattern;
  - pattern shorter than the Part → tiles (`repeats = Ceiling(partBeats / loopBeats) ≥ 2`);
  - pattern longer than the Part → truncated by note onset (onsets `≥ partTotalBeats`
    dropped; a note whose onset is inside the Part rings to its authored duration);
  - `octaveOffset` at the band extremes → clamped to `[octaveMin-1, octaveMax-1]` (the same
    register band the shipping `ChooseMelodicRegister` uses), no out-of-range throw;
  - authored duration floored at `MinNoteBeats` (0.05); velocity clamped to 1–127.
- Determinism: the path consumes no RNG, `SnapshotOrdered()` is a pure sort over a fixed
  serialized list, and `ctx.rng` is untouched ⇒ same pattern + tonality/root + meter ⇒
  byte-identical MIDI.

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — §7 "Meter & looping" reworded: tiles-by-beats +
  warning is the **accepted MVP outcome** (D-MEL5.1 = A), the truncation clause now states
  it is by note onset, and full bar-time renormalization (with compound/odd-meter beat-unit
  timing) is labelled **post-MVP** rather than "deferred (Phase 5)". §7 "Determinism" gains
  a one-line Phase-5 validation note.
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note + §8 updated: all phases
  (1–5) closed, MVP complete; a Phase-5 paragraph records D-MEL5.1 = A. No §5/§7 semantic
  change.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 5 marked DONE with D-MEL5.1 = A and closure-scope
  = A recorded; sequencing + "Immediate next steps" re-pointed to MVP-complete.
- `coverage-matrix.md` — Notes section gains an MVP-closure entry (no row / primary-home
  change).
- `CURRENT_STATE.md` — "Active now" melody paragraph flipped to MVP-complete; a Phase-5
  "Just completed" entry prepended.
- `ssot_manifest.yaml` — maintenance log records the Phase-5/MVP close (no invariant change
  under A); the stale "(not yet built)" note on the authoring-melody governs comment fixed.

### Decisions
- **D-MEL5.1 = A** — meter-mismatch handling keeps tiles-by-beats + warning as the accepted
  MVP limitation; bar-time renormalization (and compound/odd-meter beat-unit timing across
  both melody paths) is post-MVP future work. No Phase-4 / INT1 contract change; no manifest
  invariant change.
- **Closure scope = A** — editor-side Phase-5 target work (round-trip hardening, wizard UX
  polish) deemed satisfied by the Phase 2–3 closures; MVP completion rests on runtime
  validation + documentation closure.

### Added (testability seam + determinism fixture — F-A)
- `MelodyTrackComposer.cs` — extracted the note-resolution loop of `ComposeFromPattern` into
  an `internal static ResolvePatternNotesCore(...)` (plus an `internal readonly struct
  ResolvedMelodyNote` and `MinNoteBeats` promoted to an `internal const`); `ComposeFromPattern`
  now calls the seam and renders/caches the returned sequence. The output is **byte-identical**
  (same notes, render order, timing, velocity, guide notes) — a testability refactor only:
  no contract change, SSoT §7 and the manifest invariants are untouched. Mirrors
  `ChordTrackComposer.TryDirectionalFirstChordCore`.
- `Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs` (new) — EditMode fixture with
  no Unity fixtures (targets the seam + `MelodyPatternData.SnapshotOrdered`, visible via the
  existing `InternalsVisibleTo`): `SnapshotOrdered` ordering + idempotence; seam determinism
  (repeat-call sequence equality), tiling (shorter-than-Part), onset truncation
  (longer-than-Part), octave clamp at the band extremes, duration floor + velocity clamp,
  empty → empty, and degree-Tonic → root pitch. (Resolves the F-A vs. F-B choice = **F-A**.)

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change.
- `SSoT_CONTRACTS.md`: no change.
- Runtime behavior: unchanged — the F-A seam extraction is byte-identical (same MIDI bytes / guide notes); SSoT §7 and the manifest invariants are untouched. (Edge-case validation itself required no code change; the seam was added solely for testability.)

## 2026-06-17 — Melody card-pattern integration (D-MEL-INT1, package half)

### Added (package)
- `MelodyCardConfigSO` — new `patternOverride` (`MelodyPatternData`) field under a "Pattern
  (optional authored override)" header. If set, the composer plays it verbatim and bypasses
  the procedural pipeline and the card's leading/palette/style overrides.

### Changed (package runtime)
- `MelodyTrackComposer.Compose` — the authored-melody dispatch now reads the melody card
  override first: `(cfg.Parameters?.Style as MelodyCardConfigSO)?.patternOverride ??
  (cfg.Parameters?.Pattern as MelodyPatternData)`. Card-wins, mirroring rhythm's
  `RhythmCardConfigSO.patternOverride > … > TrackParameters.Pattern`. Feeds the same
  `ComposeFromPattern` path; still RNG-free/deterministic. No new assembly coupling — the
  composer already referenced `MelodyCardConfigSO` (its leading/palette/style fields).

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — §7 "Integration surface & precedence" updated to
  the three-level precedence (card `patternOverride` > `TrackParameters.Pattern` > procedural);
  §3 notes the consumer card may carry a pattern; §8 trigger covers the precedence order.
- `ssot_manifest.yaml` — `SSoT_Composer_Melody_Track` gains the card-pattern precedence
  invariant; maintenance log records the INT1 package half.

### Decisions
- **D-MEL-INT1** Melody card-pattern seam (resolves roadmap D1, Option A): authored melodies
  reach the composer via `MelodyCardConfigSO.patternOverride` (card-wins) with
  `TrackParameters.Pattern` retained as a fallback. Chosen over feeding
  `SongConfigBuilder.FromUI` directly (Option B) because it mirrors the existing rhythm/chord
  card precedent, keeps interpretation package-side, and leaves `TrackParameters` untouched.

### Cross-project (ALWTTT half — tracked separately, not yet done)
- ALWTTT sets `patternOverride` on the melody card asset and folds the referenced
  `MelodyPatternData`'s GUID into `trackInputsHash` (the style-bundle GUID identifies the card
  asset, not its `patternOverride` contents, so authored melodies would otherwise be masked by
  the stem cache). Recorded ALWTTT-side in `SSoT_Runtime_CompositionSession_Integration.md`
  (semantic + integration). Plus a joint card-path smoke. Until then INT1 is package-half-only.

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change (the card carries the pattern).
- `SSoT_CONTRACTS.md`, `coverage-matrix.md`: no change.

## 2026-06-17 — Melody Authoring MVP Phase 4: runtime hookup (`ComposeFromPattern`)

### Added (package runtime)
- `Runtime/CoreScripts/Composition/Composers/MelodyTrackComposer.cs` — a `ComposeFromPattern`
  branch (no new fields beyond a local `MinNoteBeats` const). The dispatch, inserted in
  `Compose` after the instrument null-check and before progression resolution, returns
  `ComposeFromPattern(...)` when an authored `MelodyPatternData` is present (skipping the
  procedural pipeline); otherwise the procedural path is unchanged. `ComposeFromPattern`
  resolves each note's `(degree, octaveOffset)` via `GetNoteFromScale` against the active Part
  tonality/root (reference = the instrument's mid octave per `ChooseMelodicRegister`'s
  convention, clamped to the instrument range), maps beats to `MusicalTimeSpan.Quarter`
  exactly as the procedural path does, tiles the authored loop (`pattern.TotalBeats`) to the
  Part's total beats with final-loop truncation (a `beatsPerMeasure` mismatch logs a warning
  and tiles), clamps velocity, emits via `pb.Note(note, …)`, reuses the existing
  `StampBankAndPatch`/`ForceAllChannel`/`Inspect`, caches the line as `MidiGenerator.GuideNote`s
  via `ctx.SetMelodyForPartMusician`, and consumes no RNG. Analogous to `RhythmTrackComposer`'s
  `DrumPatternData` → `ComposeFromGrid`. Smoke-validated in-game.

### Changed (governance docs)
- `runtime/SSoT_Composer_Melody_Track.md` — new §7 "Pattern-override path
  (`ComposeFromPattern`)"; §1 + Scope note the two rendering paths; §8 update trigger added.
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to Phases-1–4 closed;
  §5 boundary updated; §7 "Runtime handoff" expanded; §8 update-triggers note updated.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 4 marked DONE with D-MEL4.1–4.4 recorded;
  sequencing + "Immediate next steps" re-pointed to Phase 5.
- `ssot_manifest.yaml` — `SSoT_Composer_Melody_Track` gains the authored-pattern override
  invariant; maintenance log records the Phase-4 close-out.

### Decisions
- **D-MEL4.1** Reuse `TrackParameters.Pattern`, dispatched via `as MelodyPatternData`. No new
  serialized field; mirrors the rhythm/chord composers; no song-model or contract change.
- **D-MEL4.2** Degree→pitch via `GetNoteFromScale` against Part tonality/root; reference
  register = the instrument's mid octave, `octaveOffset` on top, clamped to the instrument
  range; chord progression not consulted by the pattern path.
- **D-MEL4.3** Beats quarter-mapped; authored loop tiled to the Part with truncation;
  `beatsPerMeasure` mismatch warns + tiles; bar-time renormalization deferred to Phase 5.
- **D-MEL4.4** `ComposeFromPattern` populates the guide-note cache.

### Not changed
- `SongConfig.cs` / `TrackParameters`: no change. `SSoT_CONTRACTS.md`, `coverage-matrix.md`: no change.

### Known intentional deltas
- Meter mismatch tiles by raw beats with a warning; alignment guaranteed only when meters
  match (Phase-5 decision). No automated tests (Phase-4 DoD: manual E2E only, passed).

## 2026-06-17 — Melody Authoring MVP Phase 3: generation-params UI + simplified generator

### Added (package editor)
- `Editor/SimplifiedMelodyGenerator.cs` — new editor-only (`#if UNITY_EDITOR`, namespace
  `MidiGenPlay.Authoring`) static generator. `Generate(MelodyPatternData,
  MelodyGenerationParamsSO, int seed)` maps the Tier-1 params into the caller's working copy:
  density → onsets/measure; rhythmic style → onset placement (Even = evenly spaced;
  Syncopated = pushed onto the off-beat; Burst = short consecutive-subdivision runs with
  gaps); octave range → per-note octave-offset bounds; scale → the seven diatonic degrees,
  drawn with a fixed stability bias (Tonic/Dominant/Mediant favoured). Velocity is a
  deterministic bar-downbeat > beat > off-beat shape. Does NOT invoke the procedural
  `MelodyTrackComposer` / `PhrasePlanner` pipeline (Phase D3) and has no runtime dependency.

### Changed (package runtime)
- `Runtime/CoreScripts/Composition/Data/MelodyGenerationParamsSO.cs` — two additive Tier-1
  fields: `int seed` (deterministic generation seed; closes the gap with the §5 description,
  which already listed a seed) and `GeneralMidiProgram instrumentHint` (DryWetMidi
  `Standards`, mirroring `DrumPatternData`'s `GeneralMidiPercussion`). Added the
  `Melanchall.DryWetMidi.Standards` using. Both additive; existing serialized `.assets`
  deserialize with defaults (seed 0, AcousticGrandPiano). `Normalize()` unchanged. The SO
  remains a generation-time aid only — never read at runtime.

### Changed (package editor)
- `Editor/MelodyPatternEditorWindow.cs` — new "Generation (simplified)" foldout under the
  header (`Header → Generation → Timing → Grid → Actions`): binds/creates a
  `MelodyGenerationParamsSO` (rendered via a cached `Editor` so its inspector incl. the new
  fields appears), a "Generate" button + a "Randomize Seed" button, and a `New Params Asset…`
  creator (new `DefaultParamsFolder` constant). `Generate` overwrites the working copy only
  (asset untouched until Apply/Save As) and auto-fits the octave window. Added an `OnDisable`
  to dispose the cached inspector editor. Scope doc-comment updated to Phase 3.

### Changed (governance docs)
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to
  Phases-1–3-closed; new §5 subsection "Generation parameters & simplified generator
  (Phase 3)" documents the determinism boundary (RNG-free onset placement;
  `System.Random(seed)` for pitch/octave), the seed field, the informational-only
  `instrumentHint`, the non-gating `tonalityHint` + stability bias, and the parameter→output
  mapping; the §5 params-SO paragraph reconciled to list the GM instrument hint and the
  stored seed; §8 update-triggers note updated (only Phase 4 remains).
- `Roadmap_Melody_Authoring_MVP.md` — Phase 3 marked DONE; D-MEL3.1–3.3 + the layout
  decision recorded; sequencing + "Immediate next steps" re-pointed to Phase 4.
- `ssot_manifest.yaml` — `SSoT_Authoring_Melody_Composition` governs gains
  `Editor/SimplifiedMelodyGenerator.cs` (Phase 3) and `Editor/MelodyPatternEditorWindow.cs`
  (closing the Phase-2 registration gap); inline phase-status comment + maintenance log
  updated; an editor-only-generator determinism invariant added.

### Decisions
- **D-MEL3.1** Seed is a stored `int` on the params SO; RNG is `System.Random(seed)`; onset
  placement is RNG-free (a new seed re-rolls pitch/octave over a fixed groove).
- **D-MEL3.2** GM instrument hint added as a Tier-1 control but informational-only for the
  MVP (no pattern field; not read at runtime).
- **D-MEL3.3** `tonalityHint` does not gate the degree set; all seven diatonic degrees,
  stability-weighted; mode-sensitive weighting deferred.

### Not changed
- `runtime/SSoT_Composer_Melody_Track.md`: no runtime change in Phase 3; the
  `ComposeFromPattern` consumption path is Phase 4.
- `coverage-matrix.md`: no change — the primary home for melody authoring is already
  `authoring/SSoT_Authoring_Melody_Composition.md` (no flip).
- `SSoT_CONTRACTS.md`: no new cross-cutting rule.

### Known intentional deltas
- `instrumentHint` and `tonalityHint` are carried but do not affect generated notes in the
  MVP (the pattern stores neither instrument nor pitch); both are documented as
  informational / future-use.
- No automated tests (Phase-3 DoD requires none); a small generator determinism fixture is
  offered as an optional follow-up.

## 2026-06-16 — Melody Authoring MVP Phase 1: deterministic MelodyPatternData

### Changed (package runtime)
- `Runtime/CoreScripts/Data/MelodyPatternData.cs` — replaced the legacy
  probabilistic model (`MelodyNoteData` with `List<ScaleDegree> possibleDegrees`,
  integer measure/beat timing) with a deterministic per-note model: a
  `[Serializable] struct MelodyNoteEvent` (one `ScaleDegree degree`,
  `int octaveOffset`, `float startBeat`, `float durationBeats`, `int velocity`)
  and a sparse `List<MelodyNoteEvent> notes`. Inherits `PatternDataSO`; adds
  explicit `beatsPerMeasure` + `subdivisions` (editor grid) and `SetSignature` /
  `ClearAll` / `InitializeIfEmpty` / `SnapshotOrdered` / `DeepCloneRuntime`
  helpers (mirrors `DrumPatternData`). Absolute pitch is not stored.
- `Runtime/CoreScripts/Composition/MidiGenerator.cs` — removed
  `GenerateMelodyTrackWithPattern` (sole consumer of the old shape;
  non-deterministic `UnityEngine.Random` degree+octave draw) and the two privates
  orphaned by it (`SetBankAndPatchEvents`, `SetChannel`) plus a now-dead
  `Melanchall.DryWetMidi.Composing` using (M-3, clean break).

### Added (package runtime)
- `Runtime/CoreScripts/Composition/Data/MelodyGenerationParamsSO.cs` — new
  ScriptableObject parameterizing the planned wizard's simplified generator:
  optional `MelodicLeadingConfig` / `PhrasePaletteSO` / `MelodicStyleSO`
  references + Tier-1 scalars (density, octave range, `MelodyRhythmicStyle` enum,
  `Tonality` hint) + a `Normalize()` clamp. Generation-time aid only; never read
  at runtime.

### Changed (governance docs)
- `authoring/SSoT_Authoring_Melody_Composition.md` — status note flipped to
  Phase-1-closed; new §5 documents the canonical format + params SO; trailing
  sections renumbered (boundary/handoff/triggers → 6/7/8).
- `authoring/SSoT_Authoring_Tools.md` — new §3.D registers the melody wizard as
  the next planned Category-A tool (window is Phase 2/3) + asset-reset caveat.
- `Roadmap_Melody_Authoring_MVP.md` — Phase 1 marked DONE; decisions
  M-3 + D-MEL1.1–1.5 recorded.
- `ssot_manifest.yaml` — `SSoT_Authoring_Melody_Composition` governs populated
  with the two new in-tree types; two invariants added; maintenance-log entry.

### Known intentional deltas
- Existing melody `.assets` (the twelve under `Resources/.../Patterns/Melodies/`)
  reset their note data on reimport — the new per-note shape is incompatible with
  the old serialized fields; disposable, to be re-authored via the wizard
  (D-MEL1.4).
- The note list/type were renamed (`melodyNotes` → `notes`, `MelodyNoteData` →
  `MelodyNoteEvent`); safe because no live code referenced the old names.
- The demoted `EmotionalGenerationPanel` / `MidiGenPlayPanel` keep their
  commented-out generation blocks untouched (D-MEL1.5); those already reference a
  long-gone `MidiGenerator` API.
- The authoring wizard (`EditorWindow`) and the `ComposeFromPattern` runtime
  branch are NOT in this change — they are Phases 2–4.

## 2026-06-16 — CQ-B1: chord quality alphabet v2 (Tier B — ninths)

### Added
- `MusicTheory.ChordQuality` enum: `Dominant9`, `Major9`, `Minor9` (append-only,
  ordinals 14–16; existing serialized `ChordEvent.quality` values unaffected).
  Realization intervals `{0,4,7,10,14}` / `{0,4,7,11,14}` / `{0,3,7,10,14}` (five
  voices, top interval a major ninth) and compact symbols `C9` / `Cmaj9` / `Cm9`
  across `GetIntervalsForQuality`, `GetChordSymbol`,
  `GetChordSymbolSpelledForDegree` (+ `ToRomanRich` display).
- `planning/active/Roadmap_Chord_Expressivity.md` — new arc roadmap for the chord
  vocabulary/voicing work (Tier A → Tier B → inversions), capturing D-CQA1.* and
  D-CQB1.*.

### Changed
- `RomanProgressionParser.TryParseQualitySuffix` — explicit-suffix cases `9`/`dom9`
  → Dominant9, `maj9`/`ma9` → Major9, `m9`/`min9` → Minor9. Explicit-only; no
  change to diatonic inference. Suffix outranks numeral case (`vi9` = dominant-
  ninth; minor-ninth is `vim9`).
- `ChordProgressionLLMResponseHandler.AllowedSuffixes` — `9`, `dom9`, `maj9`,
  `ma9`, `m9`, `min9` added to the D-L4.5 allowlist.
- `ChordProgressionLLMPromptBuilder` system prompt — alphabet gains the three
  ninths; `9`/`maj9`/`m9` removed from the Forbidden list (11/13/add9/6-9 remain
  forbidden); self-check reworded.
- `ChordProgressionEditorWindow` — `QualitySuffixForToken` maps the three ninths
  back to `9`/`maj9`/`m9`; `IsSeventhQuality` adds all three (they contain a real
  7th → 4 grid rows; the 9th has no grid row, a known delta).
- `ChordQualityResolver.GetTriadFamily` — `Dominant9` → Major, `Major9` → Major,
  `Minor9` → Minor.
- `Strategies/VoiceLeading.cs` (runtime voicer) — `GeneratePcCandidates` inversion
  loop uncapped from `i < 4` to `i < pcs.Length`. Zero regression for ≤4-voice
  chords (their length already bounds the loop); only adds the top inversion
  candidate for five-voice chords.

### Changed (package docs)
- `authoring/SSoT_Authoring_Chord_Progressions.md` §4.1 — alphabet now described
  in two tiers (Tier A ≤4 voices; Tier B five-voice ninths); grid-arity note
  generalized (ninths render 4 of 5 rows); the two five-voice voicer deltas recorded.

### Decisions
- **D-CQB1.1** Tier B explicit-only; no diatonic-template change.
- **D-CQB1.2** Ship ninths against the existing voicer + the single zero-regression
  inversion uncap. Do NOT generalize `Drop2` (would change existing seventh
  voicings) and do NOT touch the range clamp.
- **D-CQB1.3** `IsSeventhQuality` gains all three ninths (each has a real 7th).
- **D-CQB1.4** `GetTriadFamily`: Dominant9/Major9 → Major, Minor9 → Minor.
- **D-CQB1.5** Forbidden set now 11/13/add9/6-9; `9`/`maj9`/`m9` are allowed (the
  parser-rejection and guard tests were inverted accordingly — `9` moved from
  rejected to accepted).

### Known intentional deltas
- A ninth is five voices but the grid renders at most the four seventh-chord rows;
  the 9th itself gets no grid row (same family as the Tier A added-6th limitation).
  The Roman / LLM / import path stores and plays all five voices.
- Five-voice voicer nuances: `Drop2` is triad-oriented (effectively inert for
  ninths); a very tall five-voice stack near an instrument's range edge can have
  voices collapsed by the range clamp (pre-existing; also affects 4-voice chords at
  the edges). Neither affects ≤4-voice output.

## 2026-06-16 — CQ-A1: chord quality alphabet v2 (Tier A — sixths + 7sus4)

### Added
- `MusicTheory.ChordQuality` enum: `Major6`, `Minor6`, `Dominant7sus4`
  (append-only, ordinals 11–13; existing serialized `ChordEvent.quality` values
  unaffected). Realization intervals `{0,4,7,9}` / `{0,3,7,9}` / `{0,5,7,10}` and
  compact symbols `C6` / `Cm6` / `C7sus4` across `GetIntervalsForQuality`,
  `GetChordSymbol`, `GetChordSymbolSpelledForDegree` (+ `ToRomanRich` display).
- `Tests/Editor/` — five EditMode fixtures: `RomanProgressionParserTests`
  (new-suffix parse, suffix-outranks-case, extensions still warn-and-downgrade),
  `MusicTheory_ChordQualityTests` (intervals, symbols, append-only ordinals),
  `ChordProgressionLLMResponseHandler_V2Tests` (D-L4.5 guard: new suffixes pass,
  9/add9/6-9 blocked), `ChordProgressionEditorWindow_V2Tests` (suffix round-trip
  + grid arity), `ChordQualityResolver_V2Tests` (triad-family classification).

### Changed
- `RomanProgressionParser.TryParseQualitySuffix` — explicit-suffix cases `6` →
  Major6, `m6`/`min6` → Minor6, `7sus4` → Dominant7sus4 (whole-suffix match).
  Explicit-only: no change to diatonic inference; a bare degree still infers the
  diatonic triad/seventh. Suffix outranks numeral case (`vi6` = major-sixth;
  minor-sixth is `vim6`).
- `ChordProgressionLLMResponseHandler.AllowedSuffixes` — `6`, `m6`, `min6`,
  `7sus4` added to the D-L4.5 allowlist.
- `ChordProgressionLLMPromptBuilder` system prompt — alphabet gains the three
  qualities; `6` removed from the Forbidden list (`6/9` still forbidden); 9/11/13
  remain forbidden (Tier B deferred); self-check reworded.
- `ChordProgressionEditorWindow` — `QualitySuffixForToken` maps the three new
  qualities back to `6`/`m6`/`7sus4` (fixes Roman-rebuild data loss for the new
  qualities); `IsSeventhQuality` adds `Dominant7sus4` (Decision A). Both methods
  made `internal` for EditMode coverage.
- `ChordQualityResolver.GetTriadFamily` — `Major6` → Major, `Minor6` → Minor,
  `Dominant7sus4` → Suspended (previously fell through to `Other`, which
  mis-flagged the new qualities as borrowed for the `isDiatonic` metadata).

### Changed (package docs)
- `authoring/SSoT_Authoring_Chord_Progressions.md` — new §4.1 (chord quality
  alphabet): the `ChordQuality` enum is the canonical source of truth, mirrored
  in lockstep by the parser, prompt alphabet, editor round-trip, and handler
  allowlist; explicit-only; append-only; v2 additions and the known grid-arity
  limitation recorded. §7 update-triggers gains the alphabet.

### Decisions
- v2 scope split by voicer arity/interval: Tier A (≤4 voices, ≤10 semitones —
  Major6/Minor6/Dominant7sus4) shipped now; Tier B (Dominant9/Major9/Minor9 —
  5 voices, span >octave) deferred pending review of `Strategies/VoiceLeading.cs`.
- Inversions (Objective 2): recommended for the voicing layer (a per-chord hint
  following the §6 modulation-override precedent in
  `runtime/SSoT_Composer_Backing_Track.md`), NOT the Roman DSL — slash and
  figured-bass both collide with the grammar/guard. Recommendation only; not built.

### Known intentional deltas
- Grid authoring of `Major6`/`Minor6` renders three chord-tone rows (the added
  6th has no row) because they are 4-voice but not sevenths. The Roman / LLM /
  import path stores and realizes all four voices correctly.
- `Dominant7sus4` is classified Suspended (not Major), so `V7sus4` is flagged
  non-diatonic vs a major V — consistent with how sus2/sus4 are treated.

## 2026-06-11 — CE-L1: LLM card-author (third adopter, consumer-side)

### Added (ALWTTT project, not package)
- `Assets/Scripts/Cards/LLMAuthoring/` — editor-only asmdef pair
  (`ALWTTT.Cards.LLMAuthoring` + `.Tests`; refs MidiGenPlay.Runtime +
  BCS.LLM.Core.Runtime): `CardImportDtos` (+`PaletteIntentJson.requested`
  explicit-presence flag), `CardImportDtoParser` (hoisted from the window),
  `CardLLMVocabulary` (live-snapshot POCO, D-CE-L1.4),
  `CardPaletteDescriptorScanner`, `CardPaletteIntentResolver` (seeded,
  composes over the CE-F1 `PaletteSelector`), and the quartet
  `CardLLMPromptBuilder` / `CardLLMGenerator` / `CardLLMResponseHandler`
  (banned-asset-ref + out-of-alphabet guards) / `CardLLMFieldPlan`; consumer
  copy of `FakeLLMClient`; 77 tests total across both fixtures.
- `Assets/Scripts/Cards/Editor/LLM/CardLLMVocabularyBuilder.cs` —
  registry-driven status keys (both catalogues) with asset-scan fallback.
- `Assets/Scripts/Cards/Editor/CardEditorWindow.LLM.cs` — panel partial +
  `ApplyLlmPlanOnSave` Save hook (bundle creation + palette assignment,
  D-CE-L1.6).

### Changed (ALWTTT project)
- `CardEditorWindow.JsonImport.cs` — DTOs delegated to the shared parser;
  `modifierEffectNames` resolved all-or-nothing at staging; Save-step LLM hook;
  discard-time plan cleanup.

### Changed (package docs)
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 third-adopter row +
  consumer-side scope note; vocabulary-snapshot deviation recorded.
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md`
  — §5 LLM panel + §5.3 boundary rules.
- `Roadmap_Composition_Expressivity.md` — CE-L1 DONE + D-CE-L1.1..7 +
  telemetry (1572 in / 366 out); arc complete.
- `ssot_manifest.yaml` — two new invariants; maintenance log entry.

### Known intentional deltas
- The vocabulary is a per-generate snapshot, not an asset (deviation from the
  drum/chord Vocabulary-SO stage; rationale D-CE-L1.4).
- The generator does not parse the DTO (handler owns the single parse) —
  deliberate split difference from the chord twin to avoid double-parsing.
- The legacy "Create from JSON" box keeps path/guid loading and
  warn-and-default enum behavior; the guards apply to the LLM-panel route only
  (D-CE-L1.5).

## 2026-06-10 — CE-E1 + CE-F1: Card Editor clone/presets + shared palette selector

### Added
- `Runtime/CoreScripts/Composition/Data/PaletteSelection.cs` — shared, deterministic
  `PaletteSelector` (Tier A exact-TS -> B heuristic -> C raw-weights; one
  `rng.NextDouble()` per pick) over a neutral `TsFeatures` summary, plus typed
  `ProgressionFinder` / `PatternFinder` adapters. Generalizes for future melody/harmony.
- `Tests/Editor/PaletteSelectorTests.cs` — Tier A gating, Tier-A-skip, determinism,
  degenerate inputs, the B1-B6 heuristic ordering, grouping counts, chord
  `StartsPerBar`, the drum capped-onset-density cases, and kick-foundation extraction
  on a real `DrumPatternData`.
- `CardEditorWindow` (ALWTTT) — Clone Card action (deep-copies payload, clones the
  style bundle so the clone never shares the source palette) + New-Card preset buttons
  (Action / Composition / Rhythm / Backing / Melody / Harmony). [CE-E1]

### Changed
- `BackingCardConfigSO.PickProgressionOverride(rng, ts, settings, verbose)` now delegates
  to `ProgressionFinder`/`PaletteSelector`; the ~250-line reflection + Tier-helper block
  (`TryExtractPaletteCandidates`, `GetMemberValue*`, `ComputeTsHeuristicMultiplier`,
  `Roulette`, ...) is deleted. Legacy `(rng)` overload and both public signatures unchanged.
- `RhythmCardConfigSO` gained TS-aware `PickPatternOverride(rng, ts, settings, verbose)`
  delegating to `PatternFinder`/`PaletteSelector`; legacy `(rng)` overload retained. The
  drum side is now TS-aware (was deferred from PCE).
- `RhythmTrackComposer.Compose` now calls the TS-aware overload
  `PickPatternOverride(pickRng, part.TimeSignature, _settings, LogEnabled)`. Pick
  precedence unchanged (patternOverride > patternPalette > TrackParameters.Pattern).
- `runtime/SSoT_Composer_Rhythm_Track.md` §3D + precedence — drum pick now TS-aware via
  the shared selector; "not TS-aware / deferred to CE-F1" note retired.
- `runtime/SSoT_Composer_Backing_Track.md` §2 — shared-selector delegation + duplicate-
  reference delta noted.
- `planning/active/Roadmap_Composition_Expressivity.md` — CE-E1 and CE-F1 marked DONE;
  live/inert "correction" retired (resolved); "no clone affordance" line updated.
- `ssot_manifest.yaml` — flipped the TS-toggle-asymmetry invariant; added shared-selector
  + drum-density invariants; added `PaletteSelection.cs` to both composer governs.
- `coverage-matrix.md` — drum-palette row + PCE note updated for the resolved asymmetry.

### Corrected / resolved
- The PCE-era TS-toggle asymmetry (chord LIVE / drum INERT) is **resolved**: both palettes
  select through one `PaletteSelector`, so `preferExact*TimeSignatureMatches` is LIVE on
  both sides.

### Known intentional deltas
- Duplicate progression references in one chord palette are now independent weighted slots
  (the old reflection path de-duped via GroupBy-max-weight). For palettes without duplicate
  references, chord picks are byte-identical for the same seed.
- Tier C (raw-weights fallback) is an unreachable defensive guard under positive
  weights/multipliers — documented, not unit-tested.
- Drum vs chord density is asymmetric by design: drums cap foundational-onset density at the
  meter's grouping count (only under-articulation penalized); chords penalize both
  directions (D-F1.5).

## 2026-06-04 — PCE: drum-palette consumption + distinctness experiment

### Added
- `Resources/ScriptableObjects/Drums/` — 5 `DrumPatternData` assets
  (Four-on-the-Floor, Waltz-Pulse Lilt, Compound Swing, Odd-Meter Angular,
  Syncopated Pocket).
- `Resources/ScriptableObjects/Drums/Palettes/` — 5 `DrumPatternPaletteSO`
  assets (one pattern each, weight 1.0).
- `Documentation~/planning/active/Roadmap_Composition_Expressivity.md` — successor
  arc roadmap (CE-E1 / CE-F1 / CE-L1).

### Changed
- `RhythmCardConfigSO` — added `patternPalette : DrumPatternPaletteSO` and
  `PickPatternOverride(System.Random)`; priority patternOverride > patternPalette
  > null. Mirrors `BackingCardConfigSO.PickProgressionOverride(rng)`.
- `RhythmTrackComposer.Compose` — pattern resolution now calls
  `PickPatternOverride(pickRng)` seeded from `ctx.rng`, with a deterministic
  `defaultSeed` fallback when `ctx.rng` is null and a palette is present.
- `runtime/SSoT_Composer_Rhythm_Track.md` — added §3D palette consumption
  contract; precedence list now includes `patternPalette` at priority 2.
- `ssot_manifest.yaml` — flipped the "no runtime caller consumes it yet"
  invariant; added the TS-toggle-asymmetry invariant; registered the CE roadmap;
  moved the design doc to `reference/package/` (PROPOSAL → GOVERNED). Palette/
  pattern `.asset` instances are not added to `governs:` because `**/*.asset` is
  globally excluded and assets are not text-auditable.
- `reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` —
  added §1.3 card→palette identity table (mirror of ALWTTT-owned assignment);
  updated §4.4 to add `patternPalette` and drop the stale "wiring incomplete /
  composer SSoT pending" notes.

### Corrected (carried from PCE findings)
- TS-toggle asymmetry: `preferExactTimeSignatureMatches` is LIVE on the chord
  palette (via `PickProgressionOverride`'s TS-aware overload) but INERT on the
  drum palette. The kickoff brief's "inert on both" was wrong. Unification = CE-F1.
- Palette asset path is `Resources/ScriptableObjects/Drums/Palettes/`, not the
  design doc §9's `Patterns/Drums/Palettes/`.

### Validated
- PCE §5 distinctness experiment: two 4/4 cards distinct by palette alone.
  Smoke pass: determinism PASS, consumption PASS, distinctness PASS.

## 2026-05-29 — Batch L4: chord editor LLM generalization (LLM Authoring MVP complete through L4)

### Added
- `Runtime/CoreScripts/Composition/Data/ChordGenreVocabularySO.cs` — chord
  analogue of `RhythmGenreVocabularySO`; `genres[]` + `TryResolve` +
  `ChordSubStyleCue`, with chord-domain members (characteristic Roman-string
  progressions, voicing hints, cadence cues, `measuresOverride`).
- `Editor/ChordProgressionLLMPromptBuilder.cs` — pure-function system+user
  prompt builder. DSL alphabet verified against `RomanProgressionParser`;
  forbids extended/slash chords; dot-decimal durations; exact-length
  reinforcement (D-L4.4).
- `Editor/ChordProgressionLLMGenerator.cs` — generator wrapper over LLM Core
  `PromptExecutionHelper` with injectable `ILLMClient`; extracts the fenced
  Roman block and parses via `RomanProgressionParser`.
- `Editor/ChordProgressionEditorImporter.cs` — pure-function importer for the
  setup-card + Roman-block payload (single progression string, no lanes/aliases);
  CRLF-safe; line-anchored setup-field parsing.
- `Editor/ChordProgressionLLMResponseHandler.cs` — async unify point for
  generate + import; carries the D-L4.5 token-allowlist guard.
- `Editor/ChordLLMFieldPlan.cs` — pure outcome→field decision extracted from the
  window wiring for testability (D-L4.7).
- `Editor/ChordGenreVocabularyBuilder.cs` — menu-item seeder writing
  `Default Chord Genres.asset` (v1 set: jazz, pop, blues, folk) with a build-time
  parser+guard self-check so no malformed anchor can ship (D-L4.8).
- `Editor/AssemblyInfo.cs` — `InternalsVisibleTo("MidiGenPlay.Tests.Editor")`
  for the Editor assembly (D-L4.6), enabling direct unit tests of editor-side
  internals (e.g. the chord guard helper).
- Editor wiring: `ChordProgressionEditorWindow.LLM.cs` partial — LLM panel
  (vocabulary + client-override fields, genre/sub-style/measures/free-text,
  cost cap, Generate/Regenerate/Import), async non-blocking; plus a
  "Create New Progression" working-copy reset affordance.
- Tests (`Tests/Editor/`): `ChordProgressionLLMPromptBuilderTests` (11),
  `ChordProgressionEditorImporterTests` (9),
  `ChordProgressionLLMGeneratorTests` (6, `FakeLLMClient`-driven),
  `ChordProgressionLLMResponseHandlerTests` (13, incl. guard),
  `ChordProgressionEditorWindowWiringTests` (8). 47 chord LLM tests; full
  EditMode suite green. Manual smoke tests CSMR-S1..S8 pass.

### Modified
- `Editor/ChordProgressionEditorWindow.cs` — class made `partial`; LLM panel +
  "Create New Progression" button calls added (the implementation lives in the
  `.LLM` partial). No change to the existing parse/apply pipeline; LLM outcomes
  route through the existing `ParseAndPreview`/`ApplyToAsset` path.
- `Editor/DrumPatternLLMPromptBuilder.cs` — D-L4.4 backport: one exact-length
  reinforcement sentence in the system prompt, keeping the two builders aligned.
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 now lists the chord adopter
  with its stage→artifact mapping; §3.3 gained the degrade-vs-fail enforcement
  nuance (parser warns-and-downgrades ⇒ guard moves to the response handler).
- `ssot_manifest.yaml` — chord LLM artifacts added to the LLM SSoT `governs`;
  new degrade-vs-fail invariant.
- `coverage-matrix.md` — LLM cross-cutting row now cites the chord Roman DSL
  authority; milestone-plan row retired to closed historical; L4 closure note.

### Authority / semantics
- Determinism invariant untouched: the chord asset remains the seam, consumed
  deterministically by `ChordTrackComposer`. No LLM call sits on a compose path.
- New contract clarification (not a new contract): "no silent fallback" is
  enforced at the response-handler layer when the domain parser degrades rather
  than rejects. Documented in `SSoT_Authoring_LLM_Generation.md` §3.3.
- The LLM Authoring MVP is complete through L4. `Roadmap_LLM_Authoring_MVP.md`
  §"Batch L4" promoted from deferred sketch to closed; the roadmap is now a
  closed historical record rather than active planning.

### Decisions locked
- D-L4.1 Roman-string output · D-L4.2 vocab SO confirmed against the prompt ·
  D-L4.3 copy-then-unify (shared generic deferred) · D-L4.4 exact-length
  reinforcement + drum backport · D-L4.5 handler-side token-allowlist guard ·
  D-L4.6 Editor `InternalsVisibleTo` · D-L4.7 pure `ChordLLMFieldPlan` + wiring
  tests · D-L4.8 vocabulary builder with self-check.

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

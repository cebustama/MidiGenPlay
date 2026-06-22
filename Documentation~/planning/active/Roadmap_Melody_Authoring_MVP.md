# Roadmap — Melody Authoring MVP

> Active MidiGenPlay package planning.
> This roadmap is grounded in the current codebase and intentionally separates **what already exists**, **what is next**, and **what remains later**.
> This document is planning, not implementation authority. It does not override governed SSoTs.

## Purpose

Deliver a Melody Track Authoring Wizard — a Unity EditorWindow that lets a designer author, preview, and persist melody pattern data consumed by runtime, following the same normalize → preview → apply/save pipeline established by the chord and rhythm authoring tools.

Goals:

- a dedicated package-owned melody pattern authoring entry point
- a new `MelodyPatternData` asset with a deterministic scale-degree canonical format
- a `MelodyGenerationParamsSO` asset wrapping generation parameters for the wizard
- a note-grid (ladder) preview/editor inside the wizard
- a pattern-override path in `MelodyTrackComposer` analogous to rhythm's `ComposeFromGrid`
- clear separation between authored pattern data and the existing procedural pipeline

## Scope boundaries

### Out of scope (entire roadmap)

- Redesign of `PhrasePlanner`, `MelodyTrackComposer`, or `IMelodyStrategy` internals.
  The existing procedural pipeline is preserved. `MelodyPatternData` becomes a new
  override path alongside it, not a replacement.
- Changes to `MelodicLeadingConfig`, `MelodicStyleSO`, or `PhrasePaletteSO` runtime semantics.
- ALWTTT-specific integration (`MelodyCardConfigSO` wiring). Cross-project hookup
  is a consuming-project concern and may follow after the package MVP lands.

### Deferred (later phases within this roadmap)

- MIDI file import → absolute note → scale-degree conversion (Phase D1).
- Probabilistic / weighted note events in `MelodyPatternData` (Phase D2).
- Full `MelodyTrackComposer` pipeline capture as wizard generation source (Phase D3).

## Current code-backed baseline

### Already true today

The codebase already supports:

- procedural melody generation through `MelodyTrackComposer` + `PhrasePlanner` + strategies
- `MelodicLeadingConfig`, `MelodicStyleSO`, `PhrasePaletteSO`, `PhraseArchetypeSO` as
  runtime-consumed authoring assets for the procedural path
- a legacy `MelodyPatternData` class with a probabilistic per-note model
  (`List<ScaleDegree> possibleDegrees`, measure/beat timing, velocity)
- a legacy `MidiGenerator.GenerateMelodyTrackWithPattern` path consuming that asset
  (on the demoted `MIDISong` / `MIDIGeneratorManager` branch)
- `MusicTheory.ScaleDegree` enum already available in the package
- two mature pattern-editor references: `ChordProgressionEditorWindow` and
  `DrumPatternEditorWindow` (Category A tools in `SSoT_Authoring_Tools.md`)

### What does NOT exist yet

- A deterministic, per-note canonical melody pattern format
- A melody-specific EditorWindow (wizard)
- A note-grid / ladder UI for melody editing
- A `MelodyGenerationParamsSO` asset
- A pattern-override consumption path in `MelodyTrackComposer`
- Any simplified editor-side melody generator

## Accepted design decisions

These decisions are locked for the MVP and should not be revisited without explicit cause.

1. The Wizard is a Unity EditorWindow with two sections:
   top = generation parameters, bottom = melody note grid (ladder view).

2. Two separate assets, both independently saveable:
   - `MelodyPatternData` — persisted note sequence (canonical format below)
   - `MelodyGenerationParamsSO` — persisted generation parameter bundle

3. `MelodyPatternData` canonical per-note format (MVP, deterministic):
   scale-degree + octave offset + duration + velocity.
   The existing class is replaced in-place (the legacy `GenerateMelodyTrackWithPattern`
   path will be updated or removed as part of the data-model phase).

4. Scale degrees are the canonical pitch representation. Absolute MIDI pitch is not
   stored in the MVP format. MIDI file import is deferred.

5. Grid Y-axis: 7 diatonic scale-degree rows × octave bands. Chromatic alterations
   (accidentals) may be expressed as a per-note property, not as additional grid rows.

6. The existing runtime (`PhrasePlanner` + `MelodyTrackComposer` + strategies) is NOT
   replaced. `MelodyPatternData` becomes a new pattern-override path: when a pattern
   is present, `MelodyTrackComposer` uses it directly (via a new `ComposeFromPattern`
   branch) instead of running the procedural pipeline.

7. The wizard follows the established authoring pipeline:
   normalize → generate preview → show in grid → edit → apply/save.

8. Tier 1 generation params (MVP scope only): scale/tonality, instrument hint
   (General MIDI), density, octave range, rhythmic style (even / syncopated / burst).

9. MVP "Generate" uses a simplified standalone editor-only generator that maps Tier 1
   params directly into a `MelodyPatternData`. It does NOT invoke the full
   `MelodyTrackComposer` pipeline (that is deferred to Phase D3).

## Current milestone sequencing

1. data model and asset redesign (Phase 1) — **DONE (2026-06-16)**
2. note-grid UI — ladder editor (Phase 2) — **DONE (2026-06-16)**
3. generation params UI and simplified generator (Phase 3) — **DONE (2026-06-17)**
4. runtime hookup — `ComposeFromPattern` (Phase 4) — **DONE (2026-06-17, smoke-validated)**
5. polish, validation, and documentation closure (Phase 5) — **DONE (2026-06-22)**

Deferred phases follow after the MVP is complete.

---

## Milestone map

## Phase 1 — Data model and asset redesign

### Status
**DONE (2026-06-16).** Unity compiles green. Shipped: the deterministic
`MelodyPatternData` redesign (per-note `MelodyNoteEvent` struct), the new
`MelodyGenerationParamsSO`, and clean removal of the legacy probabilistic path.
No tests required (data-model swap; the procedural `MelodyTrackComposer` path is
untouched, and there are no melody test fixtures). Repo-wide grep for
`GenerateMelodyTrackWithPattern` / `melodyNotes` / `MelodyNoteData` returned only
inert references inside commented-out blocks in the demoted
`EmotionalGenerationPanel` / `MidiGenPlayPanel` (D-MEL1.5 = leave as-is).

**Decisions locked:**
- **M-3** — legacy `MidiGenerator.GenerateMelodyTrackWithPattern` removed (clean
  break, no shim); it was the sole consumer of the old shape and was
  non-deterministic (`UnityEngine.Random` degree + octave draw). Two orphaned
  privates (`SetBankAndPatchEvents`, `SetChannel`) and a now-dead `using` were
  removed with it.
- **D-MEL1.1** — explicit `beatsPerMeasure` field (mirrors `DrumPatternData`),
  kept in sync via `SetSignature`, rather than deriving from `TimeSignature`.
- **D-MEL1.2** — `MelodyNoteEvent` is a `[Serializable] struct` (value semantics,
  mirrors `DrumPatternData.StepState`).
- **D-MEL1.3** — the decision-#5 accidental property is omitted for the MVP
  (additive, non-breaking to add later).
- **D-MEL1.4** — existing melody `.assets` are disposable; the new model resets
  their incompatible serialized note data on reimport — regenerate via the
  wizard. `MelodyPatternsList` is shape-agnostic and unaffected.
- **D-MEL1.5** — the two demoted direct-generation panels are left untouched;
  their dead generation blocks already reference a long-gone `MidiGenerator` API.

> Field-name note: the note list was renamed `melodyNotes` → `notes` and the
> per-note type `MelodyNoteData` → `MelodyNoteEvent` — safe because no live code
> referenced the old names.

### Goal
Replace the existing `MelodyPatternData` with a deterministic per-note canonical model
and create the new `MelodyGenerationParamsSO` asset.

### Target deliverables

#### `MelodyPatternData` redesign

New per-note structure (replacing `MelodyNoteData`):

- `ScaleDegree degree` — single deterministic scale degree (I–VII)
- `int octaveOffset` — offset from a reference octave (e.g. 0 = default, +1 = up, -1 = down)
- `float startBeat` — start position in beats from pattern start
- `float durationBeats` — note length in beats
- `int velocity` — MIDI velocity 0–127

Pattern-level fields:

- `int measures` — pattern length (inherited from `PatternDataSO`)
- `int beatsPerMeasure` — timing grid (inherited or explicit)
- `int subdivisions` — grid resolution for the editor
- `List<MelodyNoteEvent> notes` — the note list

#### `MelodyGenerationParamsSO` (new ScriptableObject)

Wraps references to existing assets plus Tier 1 scalar params:

- `MelodicLeadingConfig leadingConfig` (optional reference)
- `PhrasePaletteSO phrasePalette` (optional reference)
- `MelodicStyleSO melodicStyle` (optional reference)
- Tier 1 scalars: density (float 0–1), octave range (int min/max),
  rhythmic style enum (Even / Syncopated / Burst), scale/tonality hint

### Legacy path impact
`MidiGenerator.GenerateMelodyTrackWithPattern` consumes the old `MelodyPatternData` shape.
This method must be updated to compile against the new model or removed/deprecated.
Decide during implementation whether to preserve a compatibility shim or break cleanly.

### Definition of done
- `MelodyPatternData` compiles with the new per-note model
- `MelodyGenerationParamsSO` exists and is createable via asset menu
- existing code that referenced old `MelodyPatternData` compiles (updated or removed)
- no runtime behavioral regression in the active `MelodyTrackComposer` path
  (it does not consume `MelodyPatternData` yet)

### SSoT update triggers at phase boundary
- `authoring/SSoT_Authoring_Melody_Composition.md` — new asset model documented
- `authoring/SSoT_Authoring_Tools.md` — melody wizard registered as planned Category A tool
- `CURRENT_STATE.md` — updated to reflect melody authoring as active focus

---

## Phase 2 — Note-grid UI (ladder editor)

### Status
**DONE (2026-06-16).** Unity compiles green. Shipped: `MelodyPatternEditorWindow`,
a scene-independent package EditorWindow (`MidiGenPlay / Melody Pattern Editor...`)
whose bottom section is the scale-degree "ladder" note grid. It binds a
`MelodyPatternData` working copy via `DeepCloneRuntime()` (asset untouched until
Apply/Save As), exposes timing controls (TimeSignature-driven `beatsPerMeasure`,
measures, subdivisions) and a configurable octave window, supports
click-place / click-select / right-click-delete with a per-note inspector (degree,
octave offset, start step, length-in-steps, velocity), draws bar/beat/subdivision
gridlines, and has an explicit Normalize plus Apply To Asset / Save As New Asset.
No generation-params UI (Phase 3), no runtime/`ComposeFromPattern` change (Phase 4),
no text/DSL mode (rhythm/chord-only). No automated tests (the Phase-2 DoD requires
none; validated by a manual smoke pass). One Editor-only file, fully
`#if UNITY_EDITOR`-guarded; no new runtime types and no editor-only leak into
`Runtime/`.

**Phase-2 DoD check:** open wizard ✓ · bind/create `MelodyPatternData` ✓ · manually
author on the grid ✓ · normalize/apply/save functional ✓ · grid reflects the working
copy ✓ · no generation-params dependency (grid standalone) ✓.

**Decisions locked:**
- **D-MEL2.1 (meter source)** — the editor derives `beatsPerMeasure` from the
  `TimeSignature` enum (via `SetSignature`) on each signature change, consistent
  with `DrumPatternEditorWindow` and `authoring/SSoT_Authoring_Tools.md` §3.A. The
  explicit `beatsPerMeasure` field (D-MEL1.1) is retained for the data model; the
  editor simply never lets it diverge from the enum.
- **D-MEL2.2 (interaction model)** — chord-style rect grid + selection inspector,
  extended from one lane (time only) to 2-D (degree × octave rows × time steps).
  Not drum-style per-cell toggles (those cannot carry per-note duration). Placement
  is immediate-commit: left-click empty places + selects a default 1-beat note,
  left-click a note selects it, right-click deletes it. No overlap removal — the
  data model declares no monophony constraint, so simultaneous notes are allowed.
- **D-MEL2.3 (octave display)** — a configurable visible octave window (default
  −1..+1 = 3 bands × 7 = 21 rows) that auto-fits to cover all notes on load (no
  data loss). The inspector octave is clamped to the window; notes outside the
  window or beyond the current measure count are preserved (not deleted) and shown
  as a hidden-note count.
- **D-MEL2.4 (duration editing)** — duration is edited via an inspector
  "Length (steps)" field; new notes default to one beat. Drag-to-resize is deferred
  to Phase 5 polish (fragile in IMGUI). Normalize is an explicit button (snap to
  subdivisions), not auto-applied on Apply/Save.

> Known limitations carried forward (see `authoring/SSoT_Authoring_Tools.md` §3.A):
> grid fits to window width so cells shrink at high step counts; hardcoded default
> save folder (Phase 8 store abstraction); unsaved new pattern lost on domain reload
> with no bound asset; octave-window + selection are editor UI state only.

### Goal
Implement the bottom section of the Melody Authoring Wizard EditorWindow:
a note-grid with scale degrees on the Y axis and time steps on the X axis,
supporting manual note placement, selection, deletion, and property editing.

### Target capabilities
- EditorWindow shell with target `MelodyPatternData` asset binding (load / new / working copy)
- timing controls: measures, beats per measure, subdivisions
- grid rendering: X = time steps, Y = scale degree rows (I–VII) × octave bands
- click to place/remove notes on grid cells
- note duration: drag or property field
- note velocity: per-note field or inline display
- octave band navigation or scroll
- visual: beat/bar grid lines, note color by degree, current selection highlight
- working copy isolation (edits do not write to asset until Apply/Save)

### Architecture notes
- Follow `DrumPatternEditorWindow` and `ChordProgressionEditorWindow` as structural references
- Grid is an editor UI concern; it reads from and writes to the working copy `MelodyPatternData`
- Normalize step: snap note positions to the active grid resolution

### Definition of done
- a designer can open the wizard, bind or create a `MelodyPatternData` asset,
  and manually author a melody pattern on the grid
- normalize / apply / save contract is functional
- the grid accurately reflects the working copy data model
- no dependency on generation params (grid works standalone)

### SSoT update triggers at phase boundary
- `authoring/SSoT_Authoring_Tools.md` — melody wizard promoted to current truth (Category A)
- `authoring/SSoT_Authoring_Melody_Composition.md` — grid authoring semantics documented

---

## Phase 3 — Generation params UI and simplified generator

### Status
**DONE (2026-06-17).** Unity compiles green; manual smoke pass successful
(params → seeded Generate → 48-step 6/8 grid → editable notes; same seed reproduces,
a new seed re-rolls pitch over an unchanged groove; asset untouched until Apply/Save As).
Shipped: the wizard's generation-parameters top section bound to `MelodyGenerationParamsSO`
(Tier-1: scale/tonality, GM instrument hint, density, octave range, rhythmic style
Even/Syncopated/Burst, seed) and `SimplifiedMelodyGenerator` (`Editor/`,
`MidiGenPlay.Authoring`) — an editor-only, deterministic generator mapping those params
into a `MelodyPatternData` working copy. No runtime / `ComposeFromPattern` change (Phase 4).
`MelodyGenerationParamsSO` gained two fields (`seed`, `instrumentHint`); no automated tests
(Phase-3 DoD requires none; manual smoke as for Phases 1–2).

**Phase-3 DoD check:** Tier-1 params UI visible/functional ✓ · Generate produces a valid
`MelodyPatternData` working copy shown in the grid ✓ · generate → manual refine →
apply/save ✓ · params SO saved independently of the pattern ✓ · determinism (same seed +
params = same output) ✓.

**Decisions locked:**
- **D-MEL3.1 (seed)** — A: `seed` is a stored `int` on `MelodyGenerationParamsSO` (closing
  the §5-vs-code gap); RNG is `System.Random(seed)` (package convention; the inverse of the
  `UnityEngine.Random` path removed in M-3). Onset placement is RNG-free, so the seed varies
  pitch/octave over a fixed rhythmic groove.
- **D-MEL3.2 (GM instrument hint)** — A: added `GeneralMidiProgram instrumentHint` (DryWetMidi
  `Standards`, mirroring drums' `GeneralMidiPercussion`) as a Tier-1 control,
  **informational-only** for the MVP — the pattern carries no instrument and the runtime
  instrument is owned by the track config, so it neither changes generated notes nor is read
  at runtime.
- **D-MEL3.3 (tonality effect)** — A: `tonalityHint` does not gate the degree set; the
  generator draws all seven diatonic degrees with a fixed stability bias (Tonic/Dominant/
  Mediant favoured). Mode-sensitive degree weighting is a deferred extension.
- **Layout** — generation-params foldout under the header (`Header → Generation → Timing →
  Grid → Actions`); Generate reads meter from the working copy (or the edit-state meter if
  nothing is bound yet), so panel order does not affect behavior.

> Carried-forward limits (unchanged from Phase 2): hardcoded default save folders
> (`DefaultSaveFolder` / the new `DefaultParamsFolder`, Phase-8 store abstraction); an
> unsaved new pattern is lost on domain reload with no bound asset. The generation-params
> section renders the SO via a cached `Editor` (its standard inspector, incl. the new fields).

### Goal
Implement the top section of the wizard (Tier 1 generation parameters) and a simplified
editor-only generator that produces a `MelodyPatternData` from those parameters.

### Target capabilities
- UI section displaying `MelodyGenerationParamsSO` fields (bind existing or create new)
- Tier 1 controls: scale/tonality selector, instrument hint (GM picker), density slider,
  octave range, rhythmic style dropdown (Even / Syncopated / Burst)
- optional asset references: `MelodicLeadingConfig`, `PhrasePaletteSO`, `MelodicStyleSO`
  (displayed but only informational for MVP generator — full pipeline capture is Phase D3)
- "Generate" button: runs the simplified generator → populates working copy → grid updates
- the generator is editor-only code (lives under `Editor/`), not a runtime dependency
- deterministic with a seed parameter (same seed + same params = same pattern)

### Simplified generator contract
The MVP generator maps Tier 1 params to a note sequence using straightforward rules:
- density → number of notes per measure
- rhythmic style → note placement algorithm (even spacing / syncopated offsets / burst clusters)
- octave range → constrain octave offsets
- scale/tonality → constrain degrees to diatonic set

This generator is intentionally simple. It is a starting point for authoring, not a
replacement for the full procedural pipeline.

### Definition of done
- Tier 1 params UI is visible and functional in the wizard
- "Generate" produces a valid `MelodyPatternData` working copy shown in the grid
- the designer can generate, then manually refine in the grid, then apply/save
- `MelodyGenerationParamsSO` can be saved independently of the pattern
- determinism: same seed + params = same output

### SSoT update triggers at phase boundary
- `authoring/SSoT_Authoring_Melody_Composition.md` — generation params semantics documented

---

## Phase 4 — Runtime hookup (`ComposeFromPattern`)

### Status
**DONE (2026-06-17, smoke-validated in-game).** First audible melody-authoring phase.
`MelodyTrackComposer` gains a `ComposeFromPattern` branch: when an authored
`MelodyPatternData` is present, the composer renders it directly (resolving each
`(degree, octaveOffset)` to an absolute pitch against the active Part tonality/root from the
instrument's mid register, tiling the authored loop — quarter-mapped beats — to the Part,
emitting events, and caching guide notes) and skips the procedural pipeline; otherwise the
procedural path runs unchanged. Runtime-only (no editor dependency), no RNG (deterministic),
no change to `SongConfig`/`TrackParameters`. Governed by
`runtime/SSoT_Composer_Melody_Track.md` §7. No automated tests (DoD requires only a manual
E2E validation, which passed). Card-routing of authored melodies was subsequently added as a
separate cross-project batch (**D-MEL-INT1**); see "Immediate next steps" and the changelog.

**Decisions locked:**
- **D-MEL4.1** — integration surface = reuse `TrackParameters.Pattern`
  (already a `PatternDataSO`), dispatched via `as MelodyPatternData`. No new field; mirrors
  `RhythmTrackComposer`/`ChordTrackComposer`; no song-model or contract churn. The
  `Pattern`-as-`ChordProgressionData` legacy fallback and the new
  `Pattern`-as-`MelodyPatternData` override are mutually exclusive on one instance.
- **D-MEL4.2** — degree→pitch via `GetNoteFromScale` against Part tonality/root. Reference
  register = the instrument's mid octave (reusing `ChooseMelodicRegister`'s
  `octaveMin-1..octaveMax-1` convention); `octaveOffset` applied on top and the target
  octave clamped to the instrument's playable range. The chord progression is not consulted
  by the pattern path.
- **D-MEL4.3** — beats quarter-mapped (`MusicalTimeSpan.Quarter.Multiply`), identical to
  `ComposeMelodyFromProgression`, so both melody paths share one timing model; the authored
  loop (`pattern.TotalBeats`) tiled to the Part's total beats with final-loop truncation; a
  `beatsPerMeasure` mismatch warns and tiles. Beat-unit-aware timing for both melody paths
  is deferred to Phase 5 (the procedural path also assumes quarter beats today).
- **D-MEL4.4** — `ComposeFromPattern` populates `ctx.SetMelodyForPartMusician`
  (guide-note cache), so a harmony track can follow an authored melody.

### Goal
Add a pattern-override consumption path to `MelodyTrackComposer` so that an authored
`MelodyPatternData` can be used at runtime instead of (or alongside) procedural generation.

### Target capabilities
- `MelodyTrackComposer` gains a `ComposeFromPattern` branch (analogous to rhythm's `ComposeFromGrid`)
- when a `MelodyPatternData` is assigned to a track config, the composer reads the pattern
  directly, resolves scale degrees to absolute MIDI pitches using the active tonality/root,
  and emits MIDI events
- pattern is normalized to the active Part meter (measure count, beats per measure)
- deterministic: same pattern + same tonality context = same MIDI output
- the procedural path (PhrasePlanner + strategies) remains the default when no pattern is present

### Design constraint
The runtime `MelodyPatternData` consumption must live in `Runtime/` code.
The pattern-override path must not depend on editor-only APIs.

### Integration surface
How the pattern reaches the composer at runtime:
- through `TrackConfig.Parameters` or an equivalent track-level field
- the exact wiring depends on whether `MelodyPatternData` is carried on a new field
  in `TrackParameters` or through a bundle/config SO

This integration surface design should be finalized at the start of Phase 4 implementation.

### Definition of done
- `MelodyTrackComposer` can consume a `MelodyPatternData` and produce correct MIDI
- pattern is respected when present; procedural path runs when absent
- determinism is preserved
- no regression in existing procedural melody generation
- at least one manual end-to-end validation: author pattern → assign to track → generate → correct output

### SSoT update triggers at phase boundary
- `runtime/SSoT_Composer_Melody_Track.md` — pattern-override path documented, precedence rules updated
- `SSoT_CONTRACTS.md` — if pattern-override precedence introduces a new cross-cutting rule
- `authoring/SSoT_Authoring_Melody_Composition.md` — runtime handoff section updated

---

## Phase 5 — Polish, validation, and documentation closure

### Status
**DONE (2026-06-22).** Edge cases validated by code-trace against the live
`MelodyTrackComposer.ComposeFromPattern` (no runtime change required): empty pattern →
silence, no crash (`MelodyPatternData.TotalBeats ≥ 1` and the `Math.Max(1.0, …)` loop floor
prevent a zero divisor; the empty `SnapshotOrdered()` list emits nothing); single-note;
shorter-than-Part → tiles (`repeats = Ceiling(partBeats / loopBeats) ≥ 2`);
longer-than-Part → truncated by note onset (onsets `≥ partTotalBeats` dropped; a note whose
onset is inside the Part rings to its authored duration); `octaveOffset` at the band
extremes → clamped to `[octaveMin-1, octaveMax-1]` (the same band the shipping
`ChooseMelodicRegister` uses), no out-of-range throw. Authored duration is floored at
`MinNoteBeats` and velocity clamped to 1–127. The path is RNG-free and byte-deterministic.
Governed docs swept (runtime + authoring SSoTs, coverage-matrix, CURRENT_STATE, changelog,
manifest log); the Melody Authoring MVP is **complete**.

**Decisions locked:**
- **D-MEL5.1 = A** — meter-mismatch handling keeps the current tiles-by-beats + warning
  behavior as the accepted MVP outcome; a mismatched-meter pattern does not align to the
  Part barlines, and that limitation is documented rather than corrected. Full bar-time
  renormalization (and compound/odd-meter beat-unit timing across both melody paths) is
  **post-MVP future work**, not landed here. No Phase-4 / INT1 contract change.
- **Closure scope = A** — the editor-side Phase-5 target work below (round-trip hardening,
  wizard UX polish) is treated as satisfied by the Phase 2–3 closures (working-copy
  isolation + explicit Normalize, shipped Unity-green); MVP completion rests on this batch's
  runtime validation + documentation closure.

**Follow-up — DONE (F-A):** the melody-determinism regression guard landed via an extracted
`internal static MelodyTrackComposer.ResolvePatternNotesCore` (byte-identical to the prior
inline loop; no contract change, SSoT §7 unaffected) plus the EditMode fixture
`Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs` (no Unity fixtures, matching
the `ChordTrackComposer_DirectionalFirstChordTests` internal-seam idiom).

### Goal
Harden the end-to-end melody authoring → runtime pipeline, close documentation,
and confirm the MVP is complete.

### Target work
- normalize/apply/save round-trip hardening: verify no data loss or silent corruption
  across grid edit → normalize → save → reload cycles
- edge cases: empty pattern, single-note pattern, pattern longer than Part measures,
  pattern shorter than Part measures (looping/truncation behavior)
- basic regression checks or validation harness for melody determinism
- wizard UX polish: clear state indicators, undo support if feasible, error messages
- documentation sweep: all SSoTs updated, coverage-matrix updated, changelog entry

### Definition of done
- the full author → save → runtime-consume path works without known data-loss issues
- documentation is closed for the MVP milestone (see list below)
- `CURRENT_STATE.md` reflects melody authoring MVP as completed

### Required documentation updates at milestone closure
- `authoring/SSoT_Authoring_Melody_Composition.md` — full update reflecting new assets and wizard
- `authoring/SSoT_Authoring_Tools.md` — melody wizard as Category A tool
- `runtime/SSoT_Composer_Melody_Track.md` — pattern-override path
- `CURRENT_STATE.md` — melody MVP completed, next steps updated
- `coverage-matrix.md` — melody authoring row updated if primary home changed
- `changelog-ssot.md` — semantic changes logged
- this roadmap — phases marked completed

---

## Deferred phases

These phases follow after the MVP is complete. They are documented here for planning
continuity but are not implementation authority.

---

## Phase D1 — MIDI file import → scale-degree conversion

### Status
Deferred. Not part of MVP.

### Goal
Allow importing a MIDI file and converting absolute note data into the
scale-degree canonical format of `MelodyPatternData`.

### Why deferred
MIDI import requires:
- absolute-pitch-to-scale-degree reverse mapping
- tonality/root detection or user specification
- handling of chromatic notes outside the diatonic scale
- potential quantization to grid resolution

These are non-trivial and not needed for the core authoring workflow.

### Sketch
- import button in the wizard
- user specifies (or tool detects) tonality and root
- MIDI notes mapped to nearest scale degree + octave offset
- chromatic notes flagged for review or mapped with accidental metadata
- result populates the working copy grid for review before apply/save

---

## Phase D2 — Probabilistic / weighted note events

### Status
Deferred. Not part of MVP.

### Goal
Extend `MelodyPatternData` to support non-deterministic note events where a single
event can resolve to one of several possible outcomes at runtime.

### Motivation
The original `MelodyPatternData` had `List<ScaleDegree> possibleDegrees` per note.
This capability is intentionally deferred from the MVP canonical format to keep
the first implementation simple and deterministic, but the asset should eventually
support richer authoring expressions.

### Possible extensions
- per-event weighted degree list (e.g. "70% root, 30% fifth")
- per-event velocity range instead of fixed value
- per-event duration variance
- optional/rest probability per event

### Design constraint
Probabilistic resolution must be deterministic given a seed (same seed = same choices).
The grid UI will need a visual language for probabilistic events distinct from fixed events.

### SSoT impact
When implemented, this phase will require updates to:
- `MelodyPatternData` data model
- `MelodyTrackComposer.ComposeFromPattern` (runtime resolution with RNG)
- `authoring/SSoT_Authoring_Melody_Composition.md`
- `runtime/SSoT_Composer_Melody_Track.md`
- the wizard grid UI

---

## Phase D3 — Full pipeline capture as wizard generation source

### Status
Deferred. Not part of MVP.

### Goal
Allow the wizard to run the full `MelodyTrackComposer` + `PhrasePlanner` + strategies
pipeline in editor context and capture the output as a `MelodyPatternData` for
further editing.

### Why deferred
This requires:
- providing full chord progression context in the editor
- running runtime-grade generation in editor mode (possible but requires setup)
- reverse-mapping absolute MIDI output back to scale-degree representation
- handling notes that don't align cleanly to the grid

### Value
Once available, this lets a designer use the full expressive power of the procedural
system as a starting point, then hand-edit the captured pattern. This bridges the
gap between procedural and authored melody workflows.

---

## Immediate next steps

**All MVP phases (1–5) are closed; the Melody Authoring MVP is complete** (Phases 1–2
2026-06-16; Phases 3–4 2026-06-17; Phase 5 2026-06-22). Phase 5 validated the
`ComposeFromPattern` edge cases (correct and deterministic), resolved meter-mismatch as
**D-MEL5.1 = A** (tiles-by-beats + warning retained as the documented MVP limitation;
bar-time renormalization is post-MVP), and swept the governed docs. The one optional
follow-up — a melody-determinism EditMode fixture — has **landed (F-A)** via a byte-identical
internal seam (`MelodyTrackComposer.ResolvePatternNotesCore`) plus
`Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs`; no contract change.

Deferred phases (D1 MIDI import, D2 probabilistic events, D3 full-pipeline capture) follow
post-MVP and are not implementation authority.

Phase 4's and Phase 5's SSoT triggers (runtime SSoT §7 + authoring SSoT §7/§8 + this
roadmap + coverage-matrix + CURRENT_STATE + changelog + manifest log) were applied at close.

Separately tracked: **D-MEL-INT1 (melody card-pattern routing)** — a cross-project batch.
Package half is implemented (`MelodyCardConfigSO.patternOverride` + a card-wins dispatch in
`MelodyTrackComposer`, mirroring `RhythmCardConfigSO.patternOverride`). Closure pending the
ALWTTT half (fold the pattern's GUID into `trackInputsHash`; set the card field) + a joint
card-path smoke; that half is tracked on the ALWTTT side, not in this roadmap.

## Related authorities

- `CURRENT_STATE.md`
- `runtime/SSoT_Composer_Melody_Track.md`
- `authoring/SSoT_Authoring_Melody_Composition.md`
- `authoring/SSoT_Authoring_Tools.md`
- `SSoT_CONTRACTS.md`
- `coverage-matrix.md`

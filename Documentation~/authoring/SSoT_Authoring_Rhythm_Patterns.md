# SSoT — Authoring Rhythm Patterns

## Scope

This document is the primary authority for package-owned rhythm pattern authoring.
It covers:

- `DrumPatternData`
- current rhythm pattern lane/grid semantics
- the current dedicated package-owned authoring window and the legacy panel it supersedes
- legacy textual compatibility only where it still affects persisted assets or runtime fallback
- the boundary between current authoring truth and future editor-window planning

It does **not** define every future UI detail as current package truth.
Planned UI targets belong secondarily to:

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- `authoring/SSoT_Authoring_Tools.md`

## 1. Current authoring mental model

Rhythm authoring currently produces reusable package-owned drum pattern assets that can be consumed by runtime generation.

The current canonical persisted model is:

- one global timing grid per pattern
- a fixed number of measures
- one beats-per-measure value for the asset, derived from `TimeSignature`
- one subdivision value for the asset
- multiple lanes, each mapped to a percussion instrument
- `StepState` step activation per lane (see Section 2)
- one default velocity per lane, used when a step carries no explicit velocity override

That model is package truth today.

## 2. Canonical persisted asset

`DrumPatternData` is the current package-owned canonical persisted rhythm asset.

### Code-backed semantics visible today

`DrumPatternData` currently stores:

- `Measures` (inherited from `PatternDataSO`)
- `TimeSignature` (inherited from `PatternDataSO`)
- `beatsPerMeasure` (derived from `TimeSignature` at authoring time)
- `subdivisions`
- `lanes`
- lane instrument mapping (`GeneralMidiPercussion`)
- lane `defaultVelocity`
- lane `steps` as `List<StepState>`
- legacy compatibility fields such as `pianoRollPattern`

### StepState model

`StepState` is the current canonical per-step representation:

```
[Serializable]
struct StepState {
    bool active;
    int  velocity;   // 0 = defer to lane defaultVelocity; 1–127 = explicit override
}
```

Sentinel contract: `velocity == 0` means "use lane `defaultVelocity`". This is the
canonical way to represent a step that is active at the lane's default loudness without
bloating the asset with redundant velocity data. Setting `velocity` to 0 on an inactive
step is equivalent to `StepState.Off`.

Effective velocity resolution for any active step:

```
effectiveVelocity = (step.velocity > 0) ? step.velocity : lane.defaultVelocity
```

This is implemented in `StepState.ResolveVelocity(int laneDefault)`.

### Migration note for existing assets

The `List<bool>` model used before Phase 6 is **not** Unity-serialization-compatible
with `List<StepState>`. Existing `.asset` files serialized with the old model will
deserialize lane step arrays as empty lists on first load. This is a known,
accepted consequence of the data-model promotion. Assets must be re-authored or
migrated manually via `DrumPatternEditorWindow` before they carry valid step data.

## 3. Current authoring tools

### 3A. Dedicated package-owned editor window (primary)

The primary rhythm authoring entry point is `DrumPatternEditorWindow`.

It is a Unity Editor window (no runtime scene required) that follows the validated
package authoring pattern established by `ChordProgressionEditorWindow`.

#### Capabilities

- opens via `MidiGenPlay / Drum Pattern Editor...`
- accepts a `DrumPatternData` target asset via an object field
- on bind: deep-clones the asset into a working copy; edits never touch the asset directly
- timing controls: `TimeSignature` enum (drives `beatsPerMeasure`), `Measures`, `Subdivisions`
- lane management: add lane, remove lane per row, remove last lane (Grid mode only;
  text-mode commits text into the working copy before mutating)
- instrument selection per lane via `GeneralMidiPercussion` popup (Grid mode only;
  text mode shows the instrument and default velocity as a read-only label)
- default velocity per lane via inline int field (Grid mode only)
- safe normalize/rebuild: signature changes resize lane step arrays without data loss where possible
- **whole-window Grid / Text tab toggle** (Phase 7) — switches the lane area
  between the row-based authoring surface and the glyph-string surface
- **Apply To Asset**: overwrites target asset with working copy, re-binds on completion
- **Save As New Asset**: saves working copy to a new `.asset` file, points window at result
- **New Pattern**: creates an unsaved working copy not tied to any asset

#### Row-local velocity view (Phase 6)

Each lane row has a `[T]` / `[V]` mode toggle button at its left edge (in Grid mode):

- **Trigger mode** (`[T]`, default): step buttons behave as boolean toggles.
  Active steps shown in green, inactive in dark. Toggling off a step preserves its
  existing per-step velocity value so it is not lost if the step is re-activated.
- **Velocity mode** (`[V]`): each step cell shows an integer field.
  - Value displayed: the explicit per-step velocity if non-zero; otherwise the lane
    default velocity (as a visual hint of what will play).
  - Setting a field to a value > 0 activates the step with that explicit velocity.
    If the entered value equals `defaultVelocity`, the stored override is set to 0
    (sentinel) to keep assets clean.
  - Setting a field to 0 deactivates the step (`StepState.Off`).
  - A `[clr]` button at the row end resets all per-step velocity overrides to 0
    (defer to lane default) without changing which steps are active or inactive.

Row view mode is editor-only UI state and is not persisted in the asset.
It resets to Trigger mode on domain reload or asset rebind.

#### Text mode (Phase 7)

A whole-window Grid / Text tab toolbar lives at the top of the lane area. In
**Text** mode, each lane is represented by a single drum-machine-style glyph
string. Lane management, instrument selection, and lane default velocity remain
in Grid mode; text mode shows them as read-only labels.

##### Glyph alphabet (v1)

| Glyph     | Meaning                                            |
|-----------|----------------------------------------------------|
| `.` `-`   | rest (inactive step) — both spellings are accepted |
| `x`       | active step at lane default velocity (sentinel)    |
| `X`       | active step at `AccentVelocity` (120)              |
| `o`       | active step at `GhostVelocity` (50)                |
| `\|`      | ignored (bar separator, for readability only)      |
| whitespace| ignored (any kind)                                 |

`AccentVelocity` and `GhostVelocity` are parser-level v1 constants exposed by
`MidiGenPlay.Authoring.DrumPatternTextParser`. The v2 plan is to make them
configurable per-lane via asset; this is planning, not current truth (see §5).

Any character outside the table above is treated as a rest and emits an
`UnknownGlyph` warning identifying the lane and step.

##### Length-mismatch handling

After ignored characters are stripped, the cleaned glyph count is compared
against the lane's `totalSteps`:

- **Shorter than `totalSteps`** — right-padded with rests; emits a `LengthShort`
  warning.
- **Longer than `totalSteps`** — right-truncated; emits a `LengthLong` warning.

Length is checked only at parse time (tab-switch or Apply), not on every
keystroke.

##### Round-trip semantics

- **Grid → Text**: on entering Text mode, the working copy is rendered into one
  glyph string per lane via `DrumPatternTextParser.Render`. If `stepsPerMeasure`
  > 1, bar separators (`|`) are inserted between measures for readability.
- **Text → Grid**: on leaving Text mode, on Apply, or on SaveAs, the text
  buffer is committed back into the working copy via
  `DrumPatternTextParser.ApplyTextEdits`, which performs a **per-cell diff**:
  for each step, if the typed glyph matches what the previous `StepState`
  would render as, the previous `StepState` is preserved exactly (including
  any non-canonical per-step velocity). Otherwise the cell is overwritten
  with the parsed glyph's canonical state.
- **Structural changes in Text mode** (signature change, add/remove lane):
  the text buffer is committed into the working copy first, the structural
  change is then applied to the working copy, and the text buffer is finally
  re-rendered from the resized working copy. The user's in-flight typing is
  not lost.

##### Lossy-render note

The glyph alphabet covers three velocity tiers. A step whose velocity does not
match `defaultVelocity`, `AccentVelocity`, or `GhostVelocity` is rendered as
the nearest tier glyph (default beats accent/ghost when ambiguous), and a
`VelocitySnappedToTier` warning is emitted naming the lane, the step, the
original velocity, and the snap target.

**The asset's per-step velocity remains the canonical truth.** A snap warning
on render does not mutate the asset. The per-cell diff in `ApplyTextEdits`
preserves the original velocity as long as the user does not type a different
glyph in that cell. This means the round-trip Grid → Text → Grid is lossless
for unchanged cells, even when those cells carry non-canonical velocities.

##### Persistence

Neither `_inputMode` nor `_textRows` is written to the asset. Both are
`[SerializeField]` on the editor window, which makes them survive a domain
reload within the same session; they are cleared on asset rebind or New
Pattern.

##### Authoring authority and in-editor HelpBox

The glyph table and behavior above are the canonical authoring contract. A
compact HelpBox at the top of the text pane shows a condensed legend for
usability. The HelpBox is a usability nudge, not authority: if it ever
disagrees with this section, this section wins and the HelpBox is updated to
match.

##### v2 plan (planning, not current truth)

The currently coarse three-tier velocity alphabet may later be supplemented by
a separate same-resolution per-cell velocity grid in the editor, and the
`AccentVelocity` / `GhostVelocity` constants may move from parser-level to
per-lane asset fields. Tracked in `planning/active/Roadmap_Rhythm_Authoring_MVP.md`;
not implementation authority.

#### MIDI file import (Batch M1)

`DrumPatternEditorWindow` can import a standard MIDI file (`.mid`) into the
working copy. The parse is owned by `DrumMidiImporter` (`Editor/`, namespace
`MidiGenPlay.Authoring`) — a pure function with no Unity-API calls, in the same
mold as `DrumPatternEditorImporter`. The window owns the apply step; the target
asset is untouched until Apply / Save As, exactly as for every other content
source.

**Grid semantics.** The caller supplies the target `TimeSignature` and
subdivisions — the editor's Timing controls, not the file's own meta events.
Grid-beat conversion is beat-unit aware, matching the runtime `GetBeatSpan`
convention: in X/8 meters one grid beat is an eighth note, so
`gridBeats = quarterNotes × beatUnit / 4`. Measures are derived from content
(capped at 64) unless explicitly supplied. Only ticks-per-quarter-note files are
supported; SMPTE time division is a hard failure.

**Note → lane.** Note number → `GeneralMidiPercussion` resolves through a reverse
map built from DryWetMidi's own GM tables, never a hardcoded offset. Lanes are
ordered by GM note number ascending. Note *durations* are intentionally discarded:
the drum grid is trigger-based (§2).

**Velocity.** Each lane's `defaultVelocity` is the modal velocity of its imported
hits (ties break to the lower value, for determinism). Steps whose velocity equals
that default are written with the `velocity == 0` sentinel; all others carry an
explicit per-step override. This is the canonical `StepState` compression, so an
import round-trips through §2's resolution rule with no loss.

**Apply is in Grid mode, deliberately.** Imported velocities are arbitrary 1–127
values; the text glyph view would snap them to the three tiers (§3A "Lossy-render
note"). Grid mode preserves exact fidelity, and the text buffer is cleared so it
re-renders from the working copy on the next Text-mode entry. This is the
text-is-a-view / asset-is-canonical principle applied to a new content source.

**No silent fallback.** Every lossy step emits a warning surfaced in the MIDI
panel, using the same `[Kind] loc: detail` shape as the L2 importer's warnings:

| Warning kind | Raised when |
|---|---|
| `UnsupportedTimeDivision` | file is null or uses SMPTE time division (hard fail) |
| `NoNotesFound` | channel filter, GM mapping, or measure range left zero hits (hard fail) |
| `UnmappedNoteNumber` | a note number has no `GeneralMidiPercussion` mapping; skipped |
| `OffGridSnap` | snap error exceeds 0.25 step; first 8 detailed, remainder aggregated |
| `StepCollision` | two hits land on the same lane+step; the higher velocity is kept |
| `NotesBeyondRange` | hits fall past the resolved measure count; dropped |
| `MeasuresCapped` | content implies more than 64 measures |

The importer assumes reasonably quantized input: it snaps and warns, and does not
attempt to interpret swing or humanized feel.

Note: the imported `GeneralMidiPercussion` values are kit-agnostic and may not be
mapped 1:1 in the target percussion kit at render time (e.g. an imported
`BassDrum1` vs a kit that maps `AcousticBassDrum`). Resolving that mismatch is a
render-time concern owned by `runtime/SSoT_Composer_Rhythm_Track.md`, not by the
importer.

Decisions and phase scope: `planning/archive/Roadmap_MIDI_Import.md` (D-MIDI1..5).

#### Normalize / apply / save contract

1. All edits are made to the working copy (`_working`), a deep clone of the target asset.
2. The target asset is never mutated until the designer explicitly presses Apply or Save As.
3. Apply overwrites the target asset in place and re-binds.
4. Save As creates a new asset at a designer-chosen path and re-binds to it.
5. Signature changes are applied deferred (one frame) to avoid mid-draw resize artifacts.
6. **In Text mode**, the text buffer is committed into the working copy first
   (per-cell diff), then Apply / Save As / structural change proceeds as above.

#### LLM-assisted generation (Batches L1–L3)

`DrumPatternEditorWindow` can generate a pattern from a natural-language prompt
(genre + meter + free-text direction) and can import an LLM-shaped payload
(setup card + DSL block) from the clipboard. Both paths produce DSL that flows
through the same Text-mode parse/apply contract above — the LLM adds a content
source, not a new write path.

The **contract and architecture** for LLM-assisted authoring are governed by
`authoring/SSoT_Authoring_LLM_Generation.md` (primary). That SSoT defines the
asset-as-seam principle, the non-blocking-async / no-silent-fallback / CRLF-safe
contracts, the pre-network cost cap, and the seven-stage replicable pipeline.

This section governs only the **rhythm DSL grammar and setup-card shape** the LLM
targets; see "Text mode (Phase 7)" above for the glyph alphabet and round-trip
semantics. For the L1–L3 implementation history and decisions (D-L1..D-L11),
see `planning/active/Roadmap_LLM_Authoring_MVP.md`.

### 3B. Legacy runtime-scene panel (secondary, not deprecated)

`RhythmPatternPanelController` + `PatternGrid` + `PatternGridCell` + `RhythmRowHeader`
remains a valid authoring path for runtime-scene-embedded editing flows.

It is no longer the primary package-owned authoring entry point, but it is not deprecated.
It continues to serve use cases where the editor is embedded in a runtime scene UI.

Key distinction from `DrumPatternEditorWindow`:

- it requires scene wiring and a MonoBehaviour lifecycle
- it saves directly through `UnityEditor` calls inside `#if UNITY_EDITOR` guards, not through a dedicated editor-window flow
- it is the behavioral MVP that `DrumPatternEditorWindow` was built from
- it exposes only trigger-mode editing (no velocity view; no text mode)

## 4. Current boundaries and limitations

### What is already true

- `DrumPatternEditorWindow` is the primary scene-independent authoring entry point.
- The working copy / apply / save-as contract is explicit and documented above.
- `TimeSignature` drives `beatsPerMeasure`; they are not set independently.
- Per-step velocity is now canonical persisted truth via `StepState`.
- Row-local velocity view is implemented in `DrumPatternEditorWindow`.
- Whole-window text/DSL authoring is implemented in `DrumPatternEditorWindow`,
  with per-cell-diff round-trip preservation of non-canonical velocities.
- The current runtime handoff is clear: `DrumPatternData` → `RhythmTrackComposer`.
- Runtime may normalize the authored pattern to the Part meter, but authoring still defines the musical content.
- `ComposeFromGrid` consumes `SnapshotAsStepVelocities()`, so per-step velocity reaches
  generated MIDI (closed 2026-05-23; see `runtime/SSoT_Composer_Rhythm_Track.md` §3B / §6).
- `SnapshotAsIndices()` remains available as a default-velocity-only view but is no longer
  called by any runtime composer.
- Pattern saves route through the package persistence store
  (`TrackPatternConfigStoreResources<DrumPatternData>`); the editor no longer owns a
  hardcoded save folder (Phase 8, closed 2026-07-05).
- MIDI file import into the drum grid is implemented (`DrumMidiImporter` +
  the editor's MIDI panel, Batch M1 closed 2026-07-19). It is a content source
  only: it writes the working copy, never the asset.

### What is **not** true yet

The following are **not** current persisted truth:

- row-local "velocity edit mode" stored canonically in the asset (it is editor UI state only)
- whole-window "input mode" (Grid / Text) stored canonically in the asset (it is
  editor UI state only, session-survivable but not persisted)
- per-cell same-resolution velocity grid in text mode (v2 plan, see §3A "Text mode")
- per-lane configurable `AccentVelocity` / `GhostVelocity` (currently parser constants)
- true per-row polymeter persisted in the runtime asset model
- import of meter / tempo from the MIDI file itself (the editor's Timing controls
  are authoritative; file meta events are ignored)
- MIDI *export* from a drum pattern asset (no such path exists)

_(2026-07-05 correction: this list previously carried a further item —
"`ComposeFromGrid` in `RhythmTrackComposer` consuming per-step velocity ... it currently
uses `SnapshotAsIndices` with lane `defaultVelocity`." That was stale; the runtime SSoT
and `CURRENT_STATE.md` show this closed 2026-05-23. Moved to "What is already true" above.
Documentation-only fix; no runtime behavior changed by this edit — mirrors the same-dated
housekeeping correction already applied in `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
"Immediate next steps.")_

## 5. Planned UI target (not current persisted truth)

Phase 6 (velocity view) and Phase 7 (text/DSL mode) are now implemented. No
further Phase 6 or Phase 7 work is open.

The next planned authoring milestone is Phase 8 (package store/repository
persistence integration). See the roadmap for details.

The v2 evolution for text mode — separate per-cell velocity grid and per-lane
configurable accent/ghost constants — is planning, not current truth. Tracked
in `planning/active/Roadmap_Rhythm_Authoring_MVP.md`.

## 6. Relationship to `RhythmCardConfigSO`

`RhythmCardConfigSO` is currently the concrete rhythm-oriented style bundle used by runtime.
Its current authoring-relevant fields include:

- `patternOverride`
- `recipeOverride`
- `styleIdOverride`
- phrasing / feel fields such as fill cadence and density controls

This is a valid current package-facing surface, even if the naming still reflects card-originated history.

The canonical persisted rhythm pattern asset remains `DrumPatternData`.

## 7. Runtime handoff

Runtime consumption, precedence and meter normalization rules live in:

- `runtime/SSoT_Composer_Rhythm_Track.md`

This document defines the authored asset side.
Runtime docs define how that asset is interpreted and adapted during generation.

Note: `RhythmTrackComposer.ComposeFromGrid` calls `SnapshotAsStepVelocities()`, so
per-step velocity reaches generated MIDI (closed 2026-05-23 — see §4 above and
`runtime/SSoT_Composer_Rhythm_Track.md` §3B / §6). `SnapshotAsIndices()` remains available
as a default-velocity-only view but is no longer called by any runtime composer. This
closure was independent of Phase 7 and Phase 8.

## 8. Current package priority

Phase 5, Phase 6, and Phase 7 are now complete.
The current package sequencing is:

1. ~~consolidate rhythm authoring UX and contracts~~ — done (Phase 4)
2. ~~build the dedicated rhythm editor/tooling milestone~~ — done (Phase 5)
3. ~~implement row-local velocity view and required data-model extension~~ — done (Phase 6)
4. ~~text/DSL mode for row authoring~~ — done (Phase 7)
5. package store/repository persistence integration (Phase 8, next active)
6. only afterwards close phrasing / feel semantics as a later variation layer (Phase 9)

See also:

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md`
- `CURRENT_STATE.md`

## 9. Update triggers

Update this SSoT when:

- `DrumPatternData` semantics change
- `StepState` model or sentinel contract changes
- `DrumPatternEditorWindow` capabilities or contract change
- `SnapshotAsStepVelocities` is consumed by runtime (update Section 4 and Section 7)
- rhythm bundle authoring fields change in a way that alters pattern authoring meaning
- text-mode glyph alphabet, length-mismatch policy, error policy, or
  round-trip contract changes
- the MIDI import contract changes (grid-beat conversion, GM mapping source,
  velocity compression, warning taxonomy, or the Grid-mode apply rationale)
- row-local cycle / polymeter policy is promoted from planning into package truth
- `RhythmPatternPanelController` is formally deprecated

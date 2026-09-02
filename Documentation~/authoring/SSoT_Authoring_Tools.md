# SSoT — Authoring Tools

## Scope

This document defines package-owned conventions for authoring tools in MidiGenPlay.
It does not attempt to freeze every UI detail.
It defines:

- what counts as a package-owned authoring tool
- what the validated authoring-tool pattern is
- which package tools are already mature vs partial
- how current tools relate to future editor targets

## 1. Tooling principles

Package-owned authoring tools should be:

- data-driven
- explicit about the package-owned asset they produce or edit
- clear about preview/runtime-clone state vs saved asset state
- reusable across projects when possible
- documented separately from game-specific session tooling

## 2. Preferred package authoring loop

The validated package pattern is:

1. input or edit authoring data
2. normalize / parse / validate
3. preview the result on normalized data
4. apply or save into a package-owned asset
5. let runtime consume that asset later

This pattern is demonstrated by both `ChordProgressionEditorWindow` and
`DrumPatternEditorWindow` and should remain the target shape for future package tools.

## 3. Current tool categories

### A. Mature package-owned editor windows

Four tools currently hold Category A status as dedicated, scene-independent package
authoring entry points (the third, the Melody Pattern Editor, currently authors
pattern data that is not yet runtime-consumed — see its entry for scope):

#### `ChordProgressionEditorWindow`

The original package reference implementation. Demonstrates:

- dedicated editor-window UX
- target asset binding with working copy isolation
- multiple authoring modes (Roman string and grid)
- normalize / preview / apply / save flow
- separation between authoring input and persisted structure
- MIDI file import into the grid working state (Batch M3, `ChordMidiImporter`;
  restricted deterministic detection, warnings-not-silence; see
  `SSoT_Authoring_Chord_Progressions.md` §3)

#### `DrumPatternEditorWindow`

The dedicated rhythm authoring entry point, implemented in Phase 5 and extended in
Phases 6 and 7. Follows the same architectural pattern as
`ChordProgressionEditorWindow`, adapted for row-based drum pattern editing.

Capabilities:

- opens via `MidiGenPlay / Drum Pattern Editor...`
- scene-independent: no runtime MonoBehaviour wiring required
- `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
- deep-clone / working-copy isolation: asset is never mutated until Apply or Save As
- lane management and instrument selection per lane
- default velocity per lane via inline int field
- step toggle grid with visual bar boundaries
- row-local `[T]` / `[V]` mode toggle per lane (Grid mode only):
  - **Trigger mode** (`[T]`, default): boolean step on/off, preserves per-step velocity on toggle-off
  - **Velocity mode** (`[V]`): per-step int fields; `[clr]` resets overrides to 0 (defer to lane default)
- whole-window **Grid / Text** tab toggle at the top of the lane area (Phase 7):
  - **Grid** (default): the row-based authoring surface described above
  - **Text**: one drum-machine glyph string per lane; parsed on tab-switch and on
    Apply / SaveAs; per-cell diff preserves non-canonical per-step velocity for
    cells whose typed glyph hasn't changed
  - Syntax authority: `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A
    "Text mode (Phase 7)"
- **LLM-assisted generation** (Batches L1–L3): prompt-to-pattern Generate /
  Regenerate, clipboard Import of an LLM-shaped setup-card + DSL payload, lane-alias
  resolution, and a pre-network prompt cost cap. Async and non-blocking; output is
  committed to the working copy and applied through the existing Apply/Save flow.
  - Contract + architecture authority: `authoring/SSoT_Authoring_LLM_Generation.md`
- **MIDI file import** (Batch M1): a "MIDI File Import" panel with a
  drum-channel-only toggle and an "Import MIDI File…" button. Quantizes a `.mid`
  into lanes + per-step velocities using the window's Timing controls, applies to
  the working copy in Grid mode, and surfaces every lossy step as a warning.
  - Contract authority: `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A
    "MIDI file import (Batch M1)"
- safe signature normalize/rebuild (resizes lane step arrays without data loss where possible)
- Apply To Asset and Save As New Asset flows

Current limitations (known, not blocking):

- style textures created on first draw are not explicitly cleaned up on window close (low risk for editor windows)
- unsaved new patterns are lost on domain reload if no asset is assigned
- row view mode (`[T]`/`[V]`) is editor UI state only — not persisted in the asset, resets on domain reload or asset rebind
- text-mode input buffer (`_textRows`) is editor UI state only — not persisted in
  the asset; survives domain reload within the session, cleared on asset rebind
- text-mode is lossy on render when per-step velocities fall outside the three
  glyph tiers (default / accent / ghost); the asset value remains canonical
  until the user explicitly types a different glyph in that cell
- MIDI import reads meter from the window's Timing controls, not from the file's
  own time-signature meta events; a file in a different meter is re-gridded, not
  rejected
- MIDI import has no export counterpart

#### `MelodyPatternEditorWindow`

The dedicated melody authoring entry point, implemented in Phase 2 of
`planning/archive/Roadmap_Melody_Authoring_MVP.md` (closed 2026-06-16). Follows the
same architectural pattern as the chord and drum editors, adapted for a
scale-degree "ladder" note grid.

Capabilities:

- opens via `MidiGenPlay / Melody Pattern Editor...`
- scene-independent: no runtime MonoBehaviour wiring required
- `TimeSignature` enum drives `beatsPerMeasure` (consistent with package meter contract)
- deep-clone / working-copy isolation: asset is never mutated until Apply or Save As
- ladder grid: Y = 7 diatonic scale-degree rows (I–VII) × octave bands, X = time steps
- click to place / select notes; right-click to delete; a per-note selection
  inspector for degree, octave offset, start step, length (steps), and velocity
- configurable visible octave window that auto-fits to cover all notes on load;
  notes outside the window or beyond the current measure count are preserved (not
  deleted) and surfaced as a hidden-note count
- **generation-parameters section + simplified generator** (Phase 3, closed
  2026-06-17): a top section binding a `MelodyGenerationParamsSO` and a
  "Generate" button driving the editor-only `SimplifiedMelodyGenerator` into the
  working copy (the bound asset is untouched until Apply / Save As, and the
  params SO is saved independently of the pattern)
  - Contract authority: `authoring/SSoT_Authoring_Melody_Composition.md` §5
    ("Generation parameters & simplified generator (Phase 3)")
- explicit Normalize (snap notes to the current subdivision grid)
- **MIDI file import** (Batch M2): a "MIDI File Import" panel with Key Root +
  Tonality popups and a MIDI-channel filter (0 = all channels), an
  "Import MIDI File…" button, and a per-import warning list. Quantizes a `.mid`
  into scale-degree notes using the window's Timing controls, monophonizes
  polyphonic input, applies to the working copy, and surfaces every lossy step as
  a warning.
  - Contract authority: `authoring/SSoT_Authoring_Melody_Composition.md` §5
    "MIDI file import (Batch M2)"
- Apply To Asset and Save As New Asset flows
- grid authoring semantics authority:
  `authoring/SSoT_Authoring_Melody_Composition.md` §5 ("Grid authoring semantics (Phase 2)")

Status / scope (Phases 2–3 landed; consumed at runtime since Phase 4):

- this window authors `MelodyPatternData` and changes **no runtime code**, but its
  output **is** consumed at runtime: the `MelodyTrackComposer` pattern-override
  path (`ComposeFromPattern` / `ResolvePatternNotesCore`) landed in Phase 4,
  closed 2026-06-17. Consumption contract:
  `runtime/SSoT_Composer_Melody_Track.md` §7 (carrier precedence, D-MEL4.1 +
  D-MEL-INT1); phase history in §3.D
- the generation-parameters section and the editor-only `SimplifiedMelodyGenerator`
  landed in Phase 3, closed 2026-06-17 — see Capabilities above
- there is **no text/DSL mode** (a rhythm/chord-only feature); the analogous melody
  import path is MIDI-file → scale-degree, landed in Batch M2 of
  `planning/archive/Roadmap_MIDI_Import.md` (see Capabilities above)

Current limitations (known, not blocking):

- the grid fits to the window width, so cells shrink at high step counts
- unsaved new patterns are lost on domain reload if no asset is assigned
- the visible octave window and the current selection are editor UI state only —
  not persisted in the asset
- MIDI import reads meter from the window's Timing controls, not from the file's
  own time-signature meta events; a file in a different meter is re-gridded, not
  rejected
- MIDI import auto-centers the reference octave (modal, ties lower); it is
  reported in the panel, not user-selectable
- **the imported pattern does not preserve absolute register.** Only degrees and
  relative octave offsets are stored, so the anchor those offsets hang from is
  re-decided at render: `MelodyTrackComposer` pins offset 0 to the instrument's
  mid register (`refOct = (octaveMin + octaveMax − 2) / 2`, integer division),
  not to the octave the importer reported. The rendered line is therefore
  transposed by `refOct − referenceOctave` octaves relative to the source file —
  uniformly, so the contour itself is unaffected. To land on the source register,
  choose an instrument whose `octaveMin`/`octaveMax` make `refOct` equal the
  reported reference octave. Observed in the M2 smoke (2026-07-23): reference
  octave 5, `refOct` 4, rendered one octave down with the contour intact
- separately, offsets falling outside the instrument band are **clamped** at
  render, which flattens the contour at that end. Safe offsets are
  `[−⌊W/2⌋, +⌈W/2⌉]` where `W = octaveMax − octaveMin`; a wider imported span
  loses its extremes, and the clamp emits no warning or log. Did not fire in the
  M2 smoke (span −1..+1). Both behaviours are render-time and owned by
  `runtime/SSoT_Composer_Melody_Track.md` (D-MEL4.2), not by the importer
- MIDI import has no export counterpart
- no drag-to-resize on notes; duration is edited via the inspector
  "Length (steps)" field. Originally slated for Phase 5 polish, which closed
  2026-06-22 under "Closure scope = A" (editor polish treated as satisfied by the
  Phase 2–3 closures), so this is a standing limitation rather than pending work

#### `BasslineCardEditorWindow`

**Bassline Card Editor** (`MidiGenPlay/Bassline Card Editor...`,
MGP-BASSCARD-WIZARD-1). Authors `BasslineCardConfigSO`: whole-card editing
over a working copy, with text-mode authoring for the SelfPocket body and
the PHRASE-1 substitution table. Follows normalize → preview →
apply/save; the shared Resources store owns the write
(`typeFolder = "Basslines"`). The DSL, its divergences from the drum DSL,
and the window's advisory contract are governed by
`authoring/SSoT_Authoring_Bass_Cards.md`.

### B. Legacy runtime-scene MVP panel

`RhythmPatternPanelController` + `PatternGrid` + `PatternGridCell` + `RhythmRowHeader`
is the behavioral predecessor to `DrumPatternEditorWindow`.

It is:

- runtime-first and scene-embedded
- grid-based and asset-backed
- still valid for runtime-scene-embedded editing flows
- **not** the primary package authoring entry point
- updated to compile against the `StepState` model (Phase 6 compile-fix); exposes trigger-mode editing only

It is not deprecated. It is reclassified as a secondary tool.
Deprecation should only happen after `DrumPatternEditorWindow` is validated in production use.

### C. Supporting package editor infrastructure

The package also contains smaller editor/tooling components such as:

- dropdown drawers — the soundfont / bank / patch dropdowns on
  `MIDIInstrumentSO` (`SoundFontDropdownDrawer`, `BankDropdownDrawer`,
  `PatchDropdownDrawer`). Rewritten by INST-WIZ-1 (D-W2=B) to be
  **multi-edit-correct and silent-write-free**: writes are gated behind
  `EditorGUI.BeginChangeCheck` (never on repaint), every write — including the
  dependent `BankName`/`PatchName` resets and the `PatchName`+`PatchIndex` pair
  — goes through `SerializedProperty` so it applies coherently to all selected
  targets with a single undo, mixed selections render `showMixedValue`, and a
  list source that is ambiguous across the selection (mixed soundfont or bank)
  disables the popup rather than guessing. A current value that is not in the
  list renders as no selection and is **never** silently rewritten. This closes
  a data-loss defect in which drawing the inspector for a multi-selection
  copied the first asset's patch onto every selected asset.
- asset-specific custom editors
- repository/store abstractions for pattern/config assets
- pure-function authoring helpers (e.g. `MidiGenPlay.Authoring.DrumPatternTextParser`)

These are reusable building blocks available for future editor work, including Phase 8
persistence cleanup.

### D. Melody Pattern Editor — phase history

The **Melody Pattern Editor** is a Category-A tool — see its entry in §3.A. The
**Melody Authoring MVP is complete** (`planning/archive/Roadmap_Melody_Authoring_MVP.md`,
Phase 5 closed 2026-06-22). Phase record, for orientation only — this section is
not implementation authority:

- **Phase 2 (closed 2026-06-16).** Editor-window shell + scale-degree "ladder"
  note grid. Grid semantics: `authoring/SSoT_Authoring_Melody_Composition.md` §5
  ("Grid authoring semantics (Phase 2)").
- **Phase 3 (closed 2026-06-17).** Generation-parameters section surfacing
  `MelodyGenerationParamsSO` (Tier-1 params: scale/tonality, GM instrument hint,
  density, octave range, rhythmic style) + a "Generate" button driving the
  editor-only `SimplifiedMelodyGenerator` into the working copy. Contract:
  `authoring/SSoT_Authoring_Melody_Composition.md` §5
  (`MelodyGenerationParamsSO` is a generation-time aid only, never read at runtime).
- **Phase 4 (closed 2026-06-17).** Runtime consumption via the
  `MelodyTrackComposer.ComposeFromPattern` branch (scale degrees resolved to
  absolute pitch against the active tonality/root), analogous to the rhythm
  `ComposeFromGrid` path. Authority: `runtime/SSoT_Composer_Melody_Track.md` §7.
- **Phase 5 (closed 2026-06-22).** Edge-case validation, determinism guard, and
  documentation closure. D-MEL5.1 = A: tiles-by-beats on meter mismatch is the
  accepted MVP outcome.

Deferred phases still recorded in that roadmap: **D2** (probabilistic / weighted
note events), **D3** (full pipeline capture as wizard generation source), **D4**
(performance metadata sink). **D1** (MIDI file import) is superseded — it landed
as Batch M2 of `planning/archive/Roadmap_MIDI_Import.md`; the implemented contract
is `authoring/SSoT_Authoring_Melody_Composition.md` §5 "MIDI file import (Batch M2)".

> Asset-reset caveat (mirrors §7): the Phase-1 `MelodyPatternData` redesign
> changed the serialized note shape, so pre-existing melody `.assets` deserialize
> their note data as empty on first load and must be re-authored via the wizard.

### E. Catalogue tools (browse-only and management variants)

Catalogue tools own **discover → filter → inspect → select**. They are a
different shape from the Category-A editors: there is no working copy and no
normalize/preview stage, because they operate on whole assets rather than on
authored musical data.

Two variants exist, and the distinction is contractual:

- **Browse-only** — `DrumPatternCatalogueWizard`, `ChordProgressionCatalogueWizard`.
  These never mutate assets. Selecting a row pings/selects it for the normal
  Inspector.
- **Catalogue + management** — `MidiInstrumentCatalogueWizard` (INST-WIZ-1,
  D-W1=A). Adds whole-asset lifecycle operations (create, duplicate, rename,
  delete) and field editing.

**Why the management variant does not duplicate the §2 loop.** Instrument assets
are flat configuration with nothing derived to preview, so a working-copy
normalize→preview→apply stage would add ceremony without adding safety. Instead:

- **Editing is delegated, single-target.** The window embeds the asset's own
  inspector (`Editor.CreateEditor`) for exactly ONE asset at a time. This reuses
  the existing dropdown drawers rather than reimplementing them, and it makes the
  multi-object-editing hazard structurally unreachable from this window.
- **Lifecycle operations are explicit and confirmed.** Create/duplicate/rename
  go through `AssetDatabase`; delete is behind a confirmation dialog. Failures
  (for example an immutable package install) are reported in the window's status
  line, never swallowed.
- **The §1 no-silent-writes invariant is preserved**, not weakened: the window
  writes only what the user edits, and the drawers it hosts were fixed in the
  same batch (§3.C).

**Export.** `MidiInstrumentCatalogueWizard` can export the current filtered set
to CSV (file or clipboard). The column set is the union of every *visible
serialized property* across the exported assets, so the export stays complete
without the window knowing the field names — this is the supported way to answer
catalogue-wide questions (consumer integration data, `PatchName`/`PatchIndex`
hygiene, `volume01` authoring state) instead of opening assets one by one.

### F. Diagnostic and regression harnesses

A third shape, distinct from both the Category-A editors and the §3.E
catalogues: these tools AUTHOR NOTHING. They read the package's own render
path and report on it. They are registered here because they are package-owned
editor windows subject to the §1 principles (data-driven, no silent writes),
not because they produce authoring data.

`CompositionSmokeWindow` remains intentionally ungoverned (D-SMOKE-DOC-1=A,
IMPORT-QOL-1) and that decision is unchanged; the entry below governs the
MGP-TONALITY-2 matrix runner only.

#### Tonality regression matrix (MGP-TONALITY-2)

`Editor/TonalityMatrixRunner.cs` + `TonalityMatrixWindow.cs`
(Tools ▸ MidiGenPlay ▸ Tonality Matrix). An Editor-side cartesian sweep over
the smoke render path: every `MidiGenPlayConfig.tonalityProfiles` entry × {4/4,
6/8} × the supplied progressions × the 7 melody/bass/backing combinations ×
{ChordToneWalk, ImprovisedWalk} where bass is present × {Block, ArpeggioUp}
where backing is present. Adds NO runtime dependency and modifies no composer.

Per cell it resets and snapshots the `TonalityAudit` counters with
`SuppressLogs = true`, renders through `SmokeSongConfigAssembler` +
`SongOrchestrator.GenerateSinglePart` with an explicit seed, and writes no
.mid. `config.logGenerator` is forced off IN MEMORY ONLY and restored in a
`finally`; nothing is ever marked dirty.

Two measurements per cell, and the distinction matters:

- **Audit counters** — what each composer BELIEVED, tiered InScale /
  ChordToneChromatic / OutOfScaleAndChord.
- **Canonical re-classification** (D-TON2-PARITY=A) — the runner recomputes
  each event's chord pcs from `(degree, degreeAccidental, quality)` under the
  shared chord-identity law (`SSoT_CONTRACTS.md` §13) and re-judges every
  emitted note against them. `beliefDiv` = canonical reds − audit reds; nonzero
  means a composer used a different chord than the canonical one. **The audit
  alone cannot detect this** — pre-D-TON10 the bass's wrong notes were
  consistent with the bass's wrong chord belief, so the counters were green.
  Any parity claim must come from `beliefDiv`, never from the counters.

Bass reds under `ImprovisedWalk` are separated by positional inference
(D-TON2-WALK=B+): a red in the last beat of its chord window and within 2
semitones of the next event's canonical root is `walk-approach(inferred)` —
intentional chromaticism per D-W2-LAST=A, not a defect. `residualReds` is the
defect signal. Tagging approach notes at the composer (`origin=walk-approach`)
remains a recorded follow-up; the runner infers rather than requiring it.

Seeding (D-TON2-SEED=A): one configured seed for every cell, recorded per row;
a cell reproduces exactly from axes + seed via "Re-run cell (verbose)", which
replays it with audit logs unsuppressed.

Output: timestamped CSV (per-cell axes, seed, bpm, both tiers per track,
beliefDiv, origin breakdown) plus a markdown summary carrying the two DoD
verdicts, under `persistentDataPath/TonalityMatrix/`.

**Known blind spot.** The MIDI-floor pitch class is C, which is diatonic in
most profiles, so a defect that bottoms a line out at note 0 surfaces only
under profiles where C is not in the scale. F-TON-WALK-DRIFT-1 appeared in
Lydian/6/8/Backing cells alone for exactly this reason; the matrix
under-reports that class of defect by construction.

## 4. Package-owned vs cross-project-owned tools

A tool belongs in MidiGenPlay package docs when it authors or edits package-owned musical assets.

Examples:
- chord progression assets
- drum pattern assets
- future package-owned melody/composition tooling

A tool belongs in cross-project reference when it exists mainly to support game-specific
runtime/session workflows.

Examples:
- composition session/gameplay bridge UIs
- game-specific card runtime tools
- live gameplay-facing managers such as `MidiMusicManager`

## 5. Current rhythm tooling truth

`DrumPatternEditorWindow` is the primary rhythm authoring entry point.
It currently supports:

- lane/instrument editing
- step on/off editing (trigger mode)
- row-local velocity editing (velocity mode) with `[T]`/`[V]` toggle per lane
- whole-window Grid / Text tab toggle with per-lane text DSL authoring (Phase 7)
- `TimeSignature`-driven signature control
- measure and subdivision control
- safe normalize/rebuild on signature change
- apply and save-as flows for `DrumPatternData`

The persisted data model is `List<StepState>` per lane, where each `StepState` carries
`bool active` and `int velocity` (0 = defer to lane default).
See `authoring/SSoT_Authoring_Rhythm_Patterns.md` for the full data-model contract.

**Asset-truth vs runtime-consumption alignment**: the asset carries per-step velocity,
and runtime consumption matches it. `ComposeFromGrid` calls `SnapshotAsStepVelocities()`,
so per-step velocity reaches generated MIDI; `SnapshotAsIndices()` remains available as a
default-velocity-only view but is no longer called by any runtime composer (closed
2026-05-23; see `runtime/SSoT_Composer_Rhythm_Track.md` §3B / §6).

_(2026-07-05 correction: this note previously described the gap as still open —
"the current runtime (`ComposeFromGrid`) still consumes via `SnapshotAsIndices`." That
was stale. The runtime SSoT and `CURRENT_STATE.md` both show the gap closed 2026-05-23.
Documentation-only fix; no runtime behavior changed by this edit.)_

## 6. Pattern-asset persistence (Phase 8 — closed 2026-07-05, PATTERN-PERSIST-1)

All three package pattern editors — `DrumPatternEditorWindow`,
`ChordProgressionEditorWindow`, and `MelodyPatternEditorWindow` — persist through
the shared generic store `TrackPatternConfigStoreResources<T>`
(`Runtime/CoreScripts/Services/`) rather than ad-hoc `AssetDatabase` calls with
per-window hardcoded folders. Each editor instantiates the store with its type
folder (`"Drums"` / `"Chords"` / `"Melodies"`), which resolves the canonical save
root `Assets/Resources/ScriptableObjects/Patterns/<TypeFolder>`:

- Drum's save root is unchanged (`.../Patterns/Drums`).
- Chord gained a real default save folder for the first time (`.../Patterns/Chords`);
  previously its Save dialogs passed no default folder at all.
- Melody's write folder realigned from a singular `.../Patterns/Melody` to the plural
  `.../Patterns/Melodies` that `PatternRepositoryResources` reads and the shipped
  assets live in.

Division of responsibility: the editor window keeps ownership of the interactive
`SaveFilePanelInProject` naming dialog and its `Undo.RecordObject` calls; the store
owns the `AssetDatabase` write. For a new asset the window passes the dialog-chosen
path to the store's editor-only `PersistNewAtPath(instance, path)` (so the dialog is
preserved *and* the write routes through the store); in-place applies call `Save`.
`IPatternRepository` / `PatternRepositoryResources` remain the runtime **read** path
and were not extended. Each editor also exposes an additive, canonical-root "Browse
Saved Patterns" list backed by the store's `GetAll()` / `Refresh()`.

## 7. Data-model note

The persisted rhythm step model was promoted from `List<bool>` to `List<StepState>`
in Phase 6. Existing `.asset` files serialized with the old model will deserialize
lane step arrays as empty lists on first load and must be re-authored or migrated
manually via `DrumPatternEditorWindow`.

## 8. Relationship to runtime docs

Tool docs explain how assets are authored and edited.
Runtime SSoTs explain how resulting assets are consumed.

Do not duplicate runtime behavior here unless it changes the meaning of the authored asset.

## 9. Current sequencing principle

For the rhythm subsystem, current package sequencing is:

1. ~~authoring clarity and editor/tool consolidation~~ — done (Phases 4–5)
2. ~~row-local velocity view and data-model extension~~ — done (Phase 6)
3. ~~text/DSL mode for rhythm authoring~~ — done (Phase 7)
4. persistence/repository cleanup (Phase 8, next active)
5. phrasing / feel semantic enrichment (Phase 9)

## 10. Update triggers

- a Category-A window is added or its persistence contract changes — the
  Bassline Card Editor (§3.A) is the fourth; its DSL and advisory contract are
  governed by `authoring/SSoT_Authoring_Bass_Cards.md`, not here;
- the §3.F harness set changes: a diagnostic tool is added, the tonality
  matrix's axes or its two-measurement method move, or D-SMOKE-DOC-1=A is
  revisited;

Update this SSoT when:

- the package adopts a new authoring-tool pattern
- a new package-owned editor window becomes canonical
- `DrumPatternEditorWindow` capabilities or known limitations change
- a package editor tool gains or changes a file-import path (e.g. MIDI import)
- the save/preview/normalize model changes
- `RhythmPatternPanelController` is formally deprecated
- the runtime consumption of per-step velocity is closed (update asset-truth vs runtime-consumption gap note)
- the package adds a new test assembly (cross-reference
  `reference/package/Tests_Authoring_HowTo.md`)
- a catalogue tool changes variant (browse-only ↔ management) or the management
  variant's editing/lifecycle contract changes (§3.E)
- the `MIDIInstrumentSO` dropdown drawers change their write discipline or
  multi-edit behavior (§3.C)

> ABSORBED SOURCE — preserved during governance migration. Use current SSoTs in `runtime/` and `authoring/` as the active authorities.

# SSoT — Composition Authoring Tools
## Single Source of Truth for editor-side pattern authoring in ALWTTT × MidiGenPlay

**Status:** Active design + implementation reference  
**Last updated:** 2026-03-07

---

## 0) Purpose

This document defines the editor-side authoring architecture for composition-related pattern assets in ALWTTT × MidiGenPlay.

It exists to prevent drift between:
- authoring UI behavior
- normalized in-memory editing models
- persisted pattern assets
- runtime composer expectations

The guiding rule is:

> A composition authoring tool is most robust when it clearly separates:
> **(A) authoring representation** → **(B) normalized event/state model** → **(C) persistence into the asset contract**.

This SSoT uses `ChordProgressionEditorWindow` as the current reference implementation and extends that pattern to Rhythm authoring.

---

## 1) Reusable pattern-editor skeleton

Every pattern-like editor window should follow the same structural skeleton.

### 1.1 Section A — Target asset binding

**Purpose:** choose what asset is being edited and define write actions.

UI responsibilities:
- `targetAsset` object field
- “Create New” / “Save As…” / “Apply to Target”
- optional “Add to Palette/Library” action when a palette exists

Implementation responsibilities:
- when `targetAsset` changes:
  - load it into local editor state
  - keep `lastLoadedAsset` or equivalent to avoid redundant reloads
- maintain a clean split between:
  - local editable state
  - persisted asset state

### 1.2 Section B — Timing & grid definition

**Purpose:** define the temporal grid used by authoring.

Minimum fields:
- `TimeSignature` or equivalent canonical meter source
- `Measures`
- `Subdivisions` or other domain-appropriate step density control

Derived invariants:
- `beatsPerBar = TimeSignature.BeatsPerMeasure()` or equivalent canonical derivation
- `stepsPerBar = beatsPerBar * subdivisions`
- `totalSteps = Measures * stepsPerBar`

Rule:
- avoid storing a second independent beats-per-measure source unless the tool validates it aggressively
- if legacy fields exist, the editor should lock or loudly validate them

### 1.3 Section C — Authoring modes

**Purpose:** provide one or more authoring inputs that all write into the same normalized model.

Design rule:
- every mode ends with the same pipeline:
  - `authoring input -> normalize -> preview -> apply/save`

Practical implication:
- raw text input is not the source of truth
- raw grid UI state is not the source of truth
- the normalized editing model and the persisted asset contract must stay aligned

### 1.4 Section D — Normalized event/state model

**Purpose:** define the stable internal representation used by preview and persistence.

Depending on domain, the normalized model may be:
- event-based (`startStep`, `lengthSteps`, payload)
- lane-step based (rows/lanes + step activation arrays)
- or a hybrid

The tool should own:
- normalized editing state
- selection/editing state
- validation state

### 1.5 Section E — Preview

**Purpose:** show what the authored pattern means before saving.

Recommended forms:
- visual preview
- optional audio preview later

Rule:
- preview should operate on normalized data, not raw text strings or transient UI-only state

### 1.6 Section F — Persistence & integration

**Purpose:** ensure saved assets land in correct folders and match runtime contracts.

Save/apply must write:
- timing fields
- normalized content fields
- required metadata

Optional integration points:
- palettes
- libraries/repositories
- config-driven write folders

### 1.7 Section G — Validation & QoL

**Purpose:** prevent silent authoring errors.

Examples:
- clamp step ranges
- ensure no invalid lengths
- ensure events are sorted / non-overlapping when required
- warn on timing mismatches
- provide “Normalize / Fix” actions for recoverable mistakes

---

## 2) Reference implementation — ChordProgressionEditorWindow

`ChordProgressionEditorWindow` is the current reference tool because it already implements the core pattern successfully.

### 2.1 What it edits
- target asset: `ChordProgressionData`
- optional palette integration: `ChordProgressionPaletteSO`
- normalized representation: `ChordProgressionData.ChordEvent` list

### 2.2 UI shape (mapped to the skeleton)

**A) Target asset binding**
- `targetAsset`, optional palette target
- parse/preview without write
- apply to target asset
- save as new asset
- optional add to palette

**B) Timing**
- `TimeSignature`
- measures
- subdivisions / grid timing controls where applicable

**C) Authoring modes**
- Roman mode
- Grid mode

**D) Normalized events**
- Roman mode parses and quantizes into chord events
- Grid mode edits chord events directly

**E) Preview**
- linear harmonic preview
- grid/bar preview

**F) Persistence**
- writes timing, measures, events, tonalities, metadata

**G) Validation/QoL**
- tonality semantics
- metadata editing
- protection against common timing drift mistakes

### 2.3 Important takeaway

The important reusable pattern is not “Roman specifically”. It is this:

`input mode -> normalize -> preview -> apply/save`

That pattern should be treated as the baseline design for new pattern editors.


### 2.4 Supporting browser / analysis tool — `ChordProgressionCatalogueWizard`

In addition to authoring editors, the composition-tooling family may include **catalogue / browser windows** whose purpose is to inspect and navigate authored assets already present in the project.

`ChordProgressionCatalogueWizard` belongs to that category.

#### Purpose
- provide a read-only overview of all `ChordProgressionData` and `ChordProgressionPaletteSO` assets currently available in the project
- let designers quickly review the harmonic content corpus without manually browsing folders
- support discovery, curation, and QA before opening a concrete asset in its dedicated authoring window

#### What it is **not**
This wizard is **not** a pattern authoring editor.
It does not replace `ChordProgressionEditorWindow`, and it should not become a second place where chord data is modified.

Its responsibility is:
- scan / list
- filter / inspect
- select / ping / open in Inspector

Its responsibility is **not**:
- edit progression events directly
- mutate palette membership implicitly
- bypass the canonical authoring/apply pipeline

#### Recommended data surface
A catalogue-style browser for chord assets should be able to surface and filter by metadata already present on the persisted assets, for example:
- `TimeSignature`
- `Measures`
- `subdivisions`
- tonalities / compatible modes
- chord qualities present in the event list
- free-text metadata such as display name, original input, palette notes, and path/name search

For palette assets, catalogue filtering may inspect the contained `entries` so a palette can be found by the properties of the progressions it references.

#### UX contract
At minimum, the wizard should support:
- project-wide scan / refresh
- configurable filtering
- separate display of progression assets and palette assets
- click result -> select asset -> ping / reveal in Inspector

This makes it a **supporting tooling surface** for authoring and curation, not a source of truth of its own.

#### Architectural rule
Catalogue/browser tools should remain clearly separated from authoring tools:
- **authoring tools** own normalize -> preview -> apply/save
- **catalogue tools** own discover -> filter -> inspect -> select

This separation helps prevent accidental drift between “tools that edit data” and “tools that help humans find the right data to edit.”

---

## 3) Rhythm authoring — target model and current baseline

### 3.1 Target asset
- `DrumPatternData : PatternDataSO`

### 3.2 Current persisted model shape

The currently documented Rhythm persisted model is global in timing structure and lane-based in content, e.g.:
- pattern timing:
  - `Measures`
  - `beatsPerMeasure` and/or canonical meter source
  - `subdivisions`
- lane content:
  - lane instrument (`GeneralMidiPercussion`)
  - default velocity
  - step data per lane

This means the current baseline model is best understood as a **shared global timing grid** with per-lane content.

### 3.3 Rhythm authoring modes (mandatory dual-mode)

Rhythm authoring is no longer treated as grid-only tooling.

The Rhythm editor must support:

1. **Grid mode (primary visual mode)**
   - X axis: steps
   - Y axis: lanes (Kick, Snare, Hat, Toms, Percussion, etc.)
   - click toggles step on/off
   - optional future per-step velocity/accent editing

2. **Text mode (mandatory fast-entry mode)**
   - row/lane-oriented DSL
   - parser converts row strings into normalized lane-step data
   - intended for fast authoring and experimentation before or alongside grid refinement

Both modes must converge to the same persisted `DrumPatternData` representation.

### 3.4 Core invariant for Rhythm tools

For Rhythm, the required editor pipeline is:

`authoring input -> normalize -> preview -> apply/save`

This means:
- raw row strings are never the source of truth
- raw grid widget state is never the source of truth
- the persisted `DrumPatternData` content is always the normalized canonical result

### 3.5 Why dual-mode is mandatory for Rhythm

A grid-only editor is not sufficient for the desired Rhythm authoring workflow.

Drum patterns benefit strongly from:
- quick textual iteration
- row-by-row authoring
- fast experimentation before detailed refinement
- easy transfer between “musician shorthand” and explicit lane-step editing

So for Rhythm, unlike the original minimal MVP wording, **dual mode is now the intended baseline UX**, not an optional stretch feature.

---

## 4) POR IMPLEMENTAR — Rhythm row DSL

**Status:** planned / mandatory for the complete Rhythm editor milestone.

The Rhythm text mode should be built around a **row-based DSL**, not a chord-style Roman progression syntax.

### 4.1 Concept

Each row represents one lane/instrument and can be authored via compact text input, then normalized into lane-step arrays.

### 4.2 Intended properties
- fast to type
- readable by musicians/designers
- deterministic to parse
- convertible into the same normalized grid representation used by Grid mode

### 4.3 Initial constraint

In the first supported version, the row DSL should target a **shared global timing grid** for the whole pattern.

That means:
- row DSL is mandatory
- true per-row independent meters are **not** assumed yet

### 4.4 Reason

The currently documented Rhythm persisted model is still global in timing structure, so the first DSL version should bake into that canonical model rather than imply unsupported runtime semantics.

### 4.5 Required workflow
- row text input
- parse into temporary row model
- normalize into canonical lane-step representation
- preview normalized result
- apply/save only from normalized result

---

## 5) POR IMPLEMENTAR — Advanced row cycles / polymeter policy

**Status:** planned / architectural decision pending.

A requested workflow is to author different row cycles such as:
- hi-hat row behaving like 4/4
- snare row behaving like 3/4

This is musically attractive, but it is not merely a UI concern. It affects:
- data contracts
- normalization rules
- preview semantics
- persistence
- possibly runtime playback interpretation

### 5.1 Policy options

1. **Bake-to-global-grid**
   - row-local cycle expressions are accepted as authoring shorthand
   - editor expands them into one shared canonical grid
   - runtime stays unchanged

2. **True lane-local cycle support**
   - persisted Rhythm data grows per-lane cycle metadata
   - preview and runtime must understand it natively

### 5.2 Current recommendation

Treat polymeter as a **separate explicit milestone**, not as an invisible side effect of the first row-DSL version.

For the current Rhythm authoring milestone, the recommended policy is:
- **support row-based text input now**
- **bake to a shared global grid**
- revisit true lane-local cycles only if runtime and persistence are intentionally extended

### 5.3 Documentation rule

Roadmap and SSoT must always state clearly whether row-local meter is:
- truly supported in persisted/runtime form, or
- only an authoring shorthand that bakes to the global grid

---

## 6) POR IMPLEMENTAR — LLM-assisted authoring for pattern editors

**Status:** planned / non-blocking.

The Composition Authoring Tools family may expose optional LLM-assisted generation panels inside editor windows, reusing the editor-side LLM integration approach already used elsewhere in the project.

### 6.1 Intended use in Rhythm

Inside `DrumPatternEditorWindow`, the assistant may transform user prompts into either:
1. row DSL text, or
2. a structured DTO / JSON response representing rhythm data

### 6.2 Safety / workflow rule

LLM output must never bypass validation or write directly into the asset.

Required flow:

`prompt -> response -> parse/validate -> preview -> explicit user apply`

### 6.3 Recommended response contract priority
1. JSON DTO (preferred for robustness)
2. DSL text (acceptable for fast iteration)

### 6.4 Why this is not baseline MVP

The hard part is not embedding the panel in the UI. The hard part is defining:
- stable output contracts
- validation rules
- safe failure behavior
- preview/apply UX

For that reason, LLM support is documented as **POR IMPLEMENTAR**, not as a prerequisite for the first complete Rhythm editor release.

---

## 7) Pattern editor family — applying the same skeleton across domains

Even if the first concrete focus is Rhythm, the same editor architecture can and should be reused across pattern-like domains.

### 7.1 Shared conceptual base
A pattern editor should always expose:
1. target asset section
2. timing section
3. authoring modes
4. preview
5. apply/save integration
6. validation / quick fixes

### 7.2 Domain-specific specialization
- **Chords**
  - text DSL = Roman progression authoring
  - grid = harmonic blocks/events
- **Rhythm**
  - text DSL = row/lane pattern input
  - grid = lane-step editor
- **Bassline / melody**
  - likely piano-roll or degree-lane editing with optional contextual helpers

### 7.3 Shared implementation direction (recommended)
Even without extracting a full generic base class immediately, the project should standardize shared helpers for:
- target asset binding
- timing derivation
- normalize/preview/apply flow
- validation reporting
- config-driven save locations

---

## 8) Known validation themes worth carrying forward

These issues already appeared in the chord tooling and should be treated as recurring authoring risks:
- timing drift between multiple meter fields
- losing metadata when converting between editing modes
- preview not matching normalized data
- unclear distinction between editing state and persisted state
- optional semantics being accidentally collapsed by the editor

For Rhythm specifically, add:
- lane arrays must always match normalized grid length
- switching between Text and Grid must not silently destroy information
- row-DSL parse failures must fail visibly and recoverably
- future LLM outputs must pass the same validation path as manual input

---

## 9) Immediate practical implications

This SSoT now establishes the following:

1. `ChordProgressionEditorWindow` remains the reference pattern-editor implementation.
2. `DrumPatternEditorWindow` must adopt the same normalize/preview/apply architecture.
3. Rhythm authoring must be **dual-mode by default**.
4. Row-based DSL is part of the intended Rhythm authoring baseline.
5. True lane-local polymeter is **not assumed** until explicitly implemented.
6. LLM support is planned as an assistive layer and must stay behind parse/validate/preview/apply.
7. Supporting catalogue/browser tools are valid members of the tooling family, as long as they remain read-only discovery/inspection surfaces and do not duplicate authoring responsibilities.

---

## 10) Next concrete steps

1. Implement `DrumPatternEditorWindow` with:
   - target asset binding
   - timing section
   - Grid mode
   - normalized preview/apply/save pipeline

2. Add mandatory row-based Text mode that writes into the same normalized model.

3. Decide and document whether early row-local cycle authoring will:
   - bake into global grid, or
   - wait until true per-lane cycle metadata exists

4. Only after that, add optional LLM-assisted generation using DTO/DSL outputs that go through the same validation path.

5. Maintain supporting browser tools such as `ChordProgressionCatalogueWizard` for corpus review, filtering, and fast Inspector selection of existing chord assets.

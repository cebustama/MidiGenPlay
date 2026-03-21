# SSoT — Authoring Chord Progressions

## Scope

This document is the primary authority for package-owned chord progression authoring:

- `ChordProgressionData`
- progression palettes
- Roman-string and grid authoring concepts
- `ChordProgressionEditorWindow`
- supporting parser/quantization flow
- authoring-to-runtime handoff for backing generation

## 1. Authoring mental model

Chord progressions are authored as reusable assets that can then be:

- selected directly,
- grouped into palettes,
- consumed by backing-oriented runtime inputs,
- adapted at runtime to the current part meter when required.

## 2. Authoring modes

The current documented system supports two main authoring modes:

- **Roman-string authoring**
- **Grid authoring**

Both are first-class inputs to the same progression asset concept.

## 3. Tooling role

`ChordProgressionEditorWindow` is an authoring front-end over the data model.

Its job is to:

- capture authoring intent,
- parse/normalize the input representation,
- preview the result,
- and save package-owned progression assets.

It should not become the hidden source of musical truth; the saved asset is the durable truth.

## 4. Progression asset semantics

`ChordProgressionData` is the package-owned asset for authored chord-event content.

Important semantics include:

- time signature awareness
- measure-based authoring
- meaningful representation of rests/silent spans
- chord-event timing that can later be consumed by runtime backing generation

## 5. Palette semantics

Progression palettes group progression assets into reusable themed packs for runtime selection.

Palette grouping is an authoring concern.
Selection logic and fallback behavior live in runtime documentation.

## 6. Runtime handoff

This document defines how progressions are authored.
Runtime consumption is defined in:

- `runtime/SSoT_Composer_Backing_Track.md`

## 7. Update triggers

Update this SSoT when:

- progression asset structure changes,
- Roman/grid authoring semantics change,
- rest representation changes,
- palette meaning changes,
- the editor window changes how authored data is interpreted or saved.

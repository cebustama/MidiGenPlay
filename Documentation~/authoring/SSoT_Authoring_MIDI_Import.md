# SSoT — Authoring: MIDI File Import

## Scope

This document is the primary authority for the **cross-cutting** MIDI-file import
pattern shared by the package's authoring editors. It owns what is true of *every*
importer; it does **not** own any domain's musical semantics.

Promoted to a primary SSoT at MIDIIMP-SSOT-1 (2026-07-24), once the arc in
`planning/archive/Roadmap_MIDI_Import.md` closed all three phases and the pattern
had three independent adopters. Precedent for the promotion:
`authoring/SSoT_Authoring_LLM_Generation.md`, which became primary at Batch L3
closure for the same reason — a replicable authoring pattern with more than one
adopter needs one home, or the shared contract drifts between its copies.

Authority split, in one line: **this document owns the shared contract; the three
domain SSoTs own what their domain does with it.** When they disagree about a
domain's musical meaning, the domain SSoT wins. When they disagree about the
shared mechanics below, this document wins.

| Domain | Importer | Domain contract (musical semantics) |
|---|---|---|
| Drums | `Editor/DrumMidiImporter.cs` | `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A "MIDI file import (Batch M1)" |
| Melody | `Editor/MelodyMidiImporter.cs` | `authoring/SSoT_Authoring_Melody_Composition.md` §5 "MIDI file import (Batch M2)" |
| Chords | `Editor/ChordMidiImporter.cs` | `authoring/SSoT_Authoring_Chord_Progressions.md` §3 "MIDI file import (Batch M3)" |

Phase scope and locked decisions (D-MIDI1..5, M2-D1..D6, M3-D1..D6, D-QOL1-1..8):
`planning/archive/Roadmap_MIDI_Import.md`. That roadmap is archived planning
material and is **not** implementation authority.

## 1. The load-bearing principle: the importer is a pure function, the window owns apply

Every importer is a pure function that turns a `MidiFile` plus an `Options` struct
into a `Result`. It makes no Unity-API calls in the parse path and mutates no
asset. The editor window — never the importer — decides what to do with the
`Result`, and the target asset is untouched until **Apply / Save As**.

This is the same seam the package already uses for its other content sources
(`DrumPatternEditorImporter`, `ChordProgressionRuntimeImporter`), and it is what
makes every importer EditMode-testable against in-memory DryWetMidi files with no
Unity fixtures.

## 2. The shared pipeline

1. **Parse** — DryWetMidi reads the file. It is the package's only MIDI-parsing
   dependency and is treated as the GM authority (no hardcoded note-number
   tables or offsets).
2. **Filter** — an `Options` channel filter selects the notes in scope.
3. **Quantize** — note positions convert to grid beats against the *window's*
   meter and subdivisions.
4. **Interpret** — the domain step: note → lane (drums), pitch → degree (melody),
   pitch-class set → chord (chords).
5. **Compress / reduce** — the domain's canonical storage shape.
6. **Warn** — every lossy step in stages 2–5 emits a warning; none is silent.
7. **Apply** — the window writes the `Result` into the **working copy** only.

## 3. Contracts (must not break)

### 3.1 Pure function in `Editor/`

Importers live in `Editor/` under the `MidiGenPlay.Authoring` namespace. No Unity
API in the parse path, no asset mutation, no logging from the importer itself.

### 3.2 Working-copy-only apply

An import fills the working copy. **Apply / Save As remains the only asset write
path.** No import path writes an asset directly, and no import bypasses
normalize → preview → apply/save.

### 3.3 The window's Timing controls are the meter authority

The caller supplies the target `TimeSignature` and subdivisions from the editor's
Timing controls. The file's own time-signature meta events are **ignored**: a file
in a different meter is re-gridded, not rejected. Tempo is likewise not imported.

### 3.4 Grid conversion is beat-unit aware

One grid beat is the meter's beat unit, matching the runtime `GetBeatSpan`
convention: `gridBeats = quarterNotes × beatUnit / 4`. In X/8 meters a grid beat
is an eighth note.

> Consumer caveat (not an importer defect): the melody **render** path does not yet
> honour this. Both `MelodyTrackComposer` paths place notes with
> `MusicalTimeSpan.Quarter`, so an imported 6/8 melody renders against quarters.
> Owned by `runtime/SSoT_Composer_Melody_Track.md` §7 "Meter & looping (D-MEL4.3)";
> tracked as **MEL-BEATUNIT-1**.

### 3.5 No silent fallback

Every lossy step emits a warning through the window's warning surface, in the
shared shape `[Kind] loc: detail`. Per warning kind, the first 8 occurrences are
detailed and the remainder aggregated. Hard failures (which return no result at
all rather than a degraded one) are at minimum: unsupported time division, and no
notes surviving the filter.

The taxonomies themselves are per-domain and live in the domain SSoTs — 7 kinds
for drums, 11 for melody, 14 for chords.

### 3.6 Ticks-per-quarter-note only

SMPTE time division is a hard failure, not a best-effort conversion.

### 3.7 Deterministic tie-breaking, toward the lower value

Where an importer picks a representative value it uses the modal value, and ties
break toward the **lower** one: lane `defaultVelocity` (drums), reference octave
(melody), chromatic snap direction (melody, M2-D6=A). Same file + same options ⇒
same result, always.

### 3.8 Measures derived from content, capped at 64

Unless measures are supplied explicitly, they are derived from content and capped
at 64 (`MeasuresCapped` warns). Explicit measures drop or clip out-of-range
material with a warning. Derivation covers a note's **end** where the domain has
durations (melody, chords) and its onset where it does not (drums).

### 3.9 The importer assumes quantized input

It snaps and warns above a 0.25-step error. It does **not** interpret swing,
humanized feel, or rubato.

## 4. Documented losses

Each domain drops information that its canonical asset cannot express. These are
**documented losses, not warnings** — they are properties of the data model, so
warning per event would be noise:

- **Drums** — note durations (the grid is trigger-based), and the imported
  `GeneralMidiPercussion` values are kit-agnostic. Resolving a kit that lacks the
  exact GM member is a render-time concern, closed by PERC-FALLBACK-1 and owned by
  `runtime/SSoT_Composer_Rhythm_Track.md` §3E.
- **Melody** — absolute register. Only degrees and relative octave offsets are
  stored, so the anchor is re-decided at render against the instrument's mid
  register. Owned by `runtime/SSoT_Composer_Melody_Track.md` (D-MEL4.2).
- **Chords** — inversions and voicings. `ChordEvent` has no inversion field and
  voicing is runtime's job (voice leading / articulators).

## 5. Not implemented

- **MIDI export.** No importer has an export counterpart anywhere in the package.
- **Meter / tempo import from the file.** See §3.3.
- **Automatic key detection.** The key is user-specified for melody and chord
  import (D-MIDI1=A).
- **Bassline import** (D-MIDI4=A): no bassline pattern asset exists and
  `BassTrackComposer` ignores pattern overrides in v1. A bass file can be imported
  as `MelodyPatternData` instead.
- **Full chord recognition.** Chord detection is v1-restricted (D-MIDI3=A).

## 6. Current implemented surface

- Three importers, one per domain, all closed: M1 (drums, 2026-07-19), M2 (melody,
  2026-07-23), M3 (chords, 2026-07-23).
- Three editor panels, each inside its existing editor window — no dedicated
  import window (D-MIDI5=A).
- IMPORT-QOL-1 (2026-07-24) added chord-side conveniences: the "Suggest…"
  subdivision probe, the `preserveReStrikes` coalescing toggle, and `originalInput`
  provenance. These are chord-specific and documented in the chord SSoT §3; they
  are **not** part of the shared contract and are not expected of other importers.

## 7. Update triggers

Update this SSoT when:

- a fourth domain adopts the import pattern,
- any §3 contract changes (purity, apply target, meter authority, beat-unit
  conversion, warning shape, time-division support, tie-break rule, measure
  derivation),
- an import path gains an export counterpart,
- a documented loss in §4 stops being a loss (e.g. `ChordEvent` gains an inversion
  field),
- or a domain importer diverges from the shared contract, in which case record the
  divergence here rather than letting the two copies drift.

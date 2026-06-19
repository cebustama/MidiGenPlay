# SSoT — Authoring Melody Composition

> **Status note (updated 2026-06-17, Phases 1–3 closed):**
> The authoring concepts described here — phrase palettes, phrase archetypes,
> `MelodicLeadingConfig`, and `MelodicStyleSO` — are **current implemented truth**.
>
> As of Phase 1 of `Roadmap_Melody_Authoring_MVP.md` (closed 2026-06-16), the
> following are **implemented and documented here** (see §5):
> - the deterministic `MelodyPatternData` canonical per-note format
> - the `MelodyGenerationParamsSO` asset
>
> As of Phase 2 (closed 2026-06-16, Unity green), the **melody pattern authoring
> wizard's ladder note-grid editor** (`MelodyPatternEditorWindow`) is implemented.
> Its grid authoring semantics are documented here in §5 ("Grid authoring semantics
> (Phase 2)"), and the window is registered as a Category-A tool in
> `authoring/SSoT_Authoring_Tools.md` §3.A. This window authors pattern data only;
> it makes no runtime changes.
>
> As of Phase 3 (closed 2026-06-17, Unity green), the **wizard's generation-parameters
> top section and the editor-only simplified generator** (`SimplifiedMelodyGenerator`)
> are implemented. Their semantics — what each Tier-1 parameter does to the generated
> notes, the determinism boundary, and the informational-only fields — are documented
> here in §5 ("Generation parameters & simplified generator (Phase 3)"). This remains
> editor-side only and makes no runtime change.
>
> The following remains **planned, not yet implemented** — do not treat this SSoT as
> authority for it:
> - the pattern-override path in `MelodyTrackComposer` (`ComposeFromPattern`) — Phase 4
>
> See `planning/active/Roadmap_Melody_Authoring_MVP.md` for accepted design decisions.

## Scope

This document is the primary authority for package-owned melody authoring concepts:

- phrase palettes
- phrase archetypes
- `PhrasePlanner` authoring-side inputs
- `MelodicLeadingConfig`
- `MelodicStyleSO`
- authoring-side override concepts for melody generation

## 1. Authoring mental model

Melody authoring in MidiGenPlay is not just a note list.
It is a layered system in which authoring assets shape how runtime plans and renders phrases.

Important layers:

- phrase vocabulary (palette + archetypes)
- leading/personality defaults (`MelodicLeadingConfig`)
- style-level strategy selection or modification (`MelodicStyleSO`)

## 2. Phrase planning authoring

Phrase-related assets define the expressive/rhythmic plan later consumed by runtime.

This includes ideas such as:

- contour bias
- phrase grouping
- slot generation across a chord span
- phrase-end/accent cues

## 3. Leading config authoring

`MelodicLeadingConfig` is the package-owned asset/contract for:

- note-source tendencies
- motion constraints
- expression defaults such as velocity ranges
- default phrase palette selection

## 4. Style authoring

`MelodicStyleSO` expresses strategy-selection or phrase-level modification behavior for melody generation.

It is package truth even when an external project chooses to inject it through a game-facing bundle.

## 5. Canonical melody pattern format (Phase 1)

As of Phase 1 of `Roadmap_Melody_Authoring_MVP.md` (closed 2026-06-16),
`MelodyPatternData` is the package-owned **deterministic per-note** authoring
format, replacing the legacy probabilistic model.

### Per-note model (`MelodyNoteData` → `MelodyNoteEvent`)

Each note carries exactly one definite pitch intent and explicit timing:

- `ScaleDegree degree` — a single diatonic degree (I–VII)
- `int octaveOffset` — offset from the pattern reference octave (0 = reference, ±1 …)
- `float startBeat` — start position in beats from pattern start
- `float durationBeats` — note length in beats
- `int velocity` — MIDI velocity (1–127)

Pattern-level fields inherit `PatternDataSO` (`DisplayName`, `TimeSignature`,
`Measures`) and add an explicit `beatsPerMeasure` plus a `subdivisions`
editor-grid resolution (mirroring `DrumPatternData`); the note sequence is the
sparse `List<MelodyNoteEvent> notes`.

### Determinism boundary

Pitch is **not** stored. A pattern stores scale degrees + octave offsets, and
absolute MIDI pitch is resolved at runtime against the active Part tonality /
root (see §7 and `runtime/SSoT_Composer_Melody_Track.md`; the consuming
`ComposeFromPattern` branch is Phase 4, not yet implemented). The same pattern
plays back identically — any randomness lives at generation time, not playback.
This is the deliberate inversion of the legacy model, which re-rolled a degree
and an octave per note at play time.

### `MelodyGenerationParamsSO`

A separate, independently-saved bundle that parameterizes the authoring wizard's simplified generator. It wraps optional references to
the procedural-path assets (`MelodicLeadingConfig`, `PhrasePaletteSO`,
`MelodicStyleSO`) plus Tier-1 scalars — density, octave range, rhythmic style
(Even / Syncopated / Burst), tonality hint, a General-MIDI instrument hint, and an integer generation seed. It is a
**generation-time aid only and is never read at runtime**; the pattern it
produces is the runtime-consumed artifact. (See "Generation parameters & simplified generator (Phase 3)" below for what each parameter does and which fields are informational-only.)

### Legacy removal

The legacy probabilistic per-note model (`List<ScaleDegree> possibleDegrees`,
integer measure/beat timing) and its sole consumer,
`MidiGenerator.GenerateMelodyTrackWithPattern`, were removed in Phase 1
(decision M-3, clean break). `MelodyPatternsList` is a shape-agnostic catalogue
(filters by `TimeSignature` only) and was unaffected. Probabilistic / weighted
note events are deferred to Phase D2.

### Grid authoring semantics (Phase 2)

As of Phase 2 (closed 2026-06-16), `MelodyPatternEditorWindow` is the package-owned
editor that authors `MelodyPatternData`. It is a scale-degree **ladder** grid and
follows the established normalize → preview → apply/save loop. Authoring semantics:

- **Ladder mapping.** The grid Y-axis is 7 diatonic scale-degree rows (I–VII) ×
  octave bands; the X-axis is subdivision steps. A note's `(degree, octaveOffset)`
  selects a row and its beat position selects a column. Pitch is never absolute —
  the row is a degree, not a MIDI note (resolved at runtime per the determinism
  boundary above).
- **Beat-absolute storage.** Notes are stored in beats (`startBeat`,
  `durationBeats`) and are meter-independent. `subdivisions` only quantizes the
  editor grid; it is not a property of the stored timing. Changing meter or
  subdivisions does not remap stored notes (contrast the rhythm editor's step-array
  rebuild).
- **Meter source.** The editor derives `beatsPerMeasure` from the `TimeSignature`
  enum (via `SetSignature`) on every signature change, consistent with
  `DrumPatternEditorWindow` and the §3.A package meter contract. The
  `beatsPerMeasure` field remains explicitly stored (per roadmap D-MEL1.1); the
  editor simply never lets it diverge from the enum.
- **Working-copy isolation.** All edits target a `DeepCloneRuntime()` working copy.
  The bound asset is not mutated until Apply To Asset or Save As New Asset.
- **Normalize is explicit.** Normalize snaps every note's start and duration to the
  current subdivision grid. It is a user-invoked step, **not** applied automatically
  on Apply/Save — grid-placed notes are already on-grid; Normalize matters for
  off-grid notes (e.g. from a future import).
- **Lossless out-of-range handling.** The visible octave window is configurable and
  auto-fits to cover all notes on load (no clamping/data loss). The per-note octave
  is clamped to the visible window; notes outside the window or beyond the current
  measure count are preserved (never deleted) and surfaced as a hidden-note count.
- **No text/DSL mode.** Unlike the rhythm and chord editors, the melody editor has
  no text-glyph authoring mode (a rhythm/chord-only feature). The analogous melody
  import path is MIDI-file → scale-degree conversion, which is deferred (Phase D1).

Accepted Phase-2 interaction/display decisions (D-MEL2.1–2.4) are recorded in the
roadmap; this SSoT documents the resulting authored-asset semantics, not the UI
mechanics.

### Generation parameters & simplified generator (Phase 3)

As of Phase 3 (closed 2026-06-17), `MelodyPatternEditorWindow` gains a
generation-parameters top section bound to `MelodyGenerationParamsSO`, plus
`SimplifiedMelodyGenerator` (`Editor/`, namespace `MidiGenPlay.Authoring`) — an
**editor-only** generator that maps the Tier-1 params into a `MelodyPatternData`
working copy. It does not invoke the procedural `MelodyTrackComposer` / `PhrasePlanner`
pipeline (that capture is Phase D3) and has no runtime dependency.
`MelodyGenerationParamsSO` also surfaces optional procedural-path references (Leading
Config / Phrase Palette / Melodic Style) that the simplified generator does **not** read;
those feed the deferred procedural pipeline.

- **Determinism boundary (generation).** Pitch and octave are drawn from a single
  `System.Random(seed)` — the package determinism convention (cf. `RhythmTrackComposer`)
  and the deliberate inverse of the legacy `UnityEngine.Random` per-note draw removed in
  M-3. Onset placement (the rhythmic skeleton) is a **pure function of rhythmic style +
  density + meter and does not consume the RNG**, so the same params reproduce the same
  groove while a new seed re-rolls only the melody's pitches over that groove. Same seed +
  same params + same meter ⇒ identical note list.
- **`seed`.** A stored `int` field on `MelodyGenerationParamsSO`, so a saved params asset
  reproduces its own pattern across sessions. The wizard exposes a "Randomize Seed" action
  that re-rolls it and regenerates.
- **`tonalityHint` does not gate the degree set (MVP).** Because the pattern stores scale
  degrees, not pitches, all seven diatonic degrees stay available regardless of mode. The
  generator draws them with a fixed **stability bias** (Tonic / Dominant / Mediant
  favoured) so output reads as a melody rather than a random walk. The hint is carried for
  runtime pitch resolution and future use; mode-sensitive degree weighting is a deferred
  extension.
- **`instrumentHint` is informational-only (MVP).** Surfaced as a Tier-1 control
  (`GeneralMidiProgram`) for parity with the accepted Tier-1 parameter set, but
  `MelodyPatternData` carries no instrument and the runtime instrument is owned by the
  track config — so the hint does **not** change generated notes and is **not** read at
  runtime. Carried for display + future use.
- **Parameter → output mapping (the simplified generator).**
  - **density (0–1)** → onsets per measure (sparser → busier; Even/Syncopated cap at one
    onset per beat, Burst scales cluster count and run length).
  - **rhythmic style** → onset placement: **Even** = evenly spaced; **Syncopated** = pushed
    onto the off-beat (the "and"; at a quarter grid, a back-beat push); **Burst** = short
    runs of consecutive subdivisions separated by gaps.
  - **octave range (min/max)** → bounds the per-note octave offset (clamped to the grid's
    ±4 band limit).
  - **scale/tonality** → degree set = the seven diatonic degrees, stability-weighted (above).
  - velocity is a deterministic shape (bar-downbeat > beat > off-beat); note durations are
    per-style (Even ≈ one beat, Syncopated a half-beat push, Burst one subdivision).
- **Pipeline & isolation unchanged.** "Generate" overwrites the **working copy only** (the
  bound asset is untouched until Apply / Save As), and `MelodyGenerationParamsSO` is saved
  independently of the pattern — preserving the normalize → preview → apply/save loop. The
  generator is intentionally simple: a starting point for authoring, not a replacement for
  the procedural pipeline.

## 6. Explicit boundary with ALWTTT

ALWTTT may use a concrete bundle such as `MelodyCardConfigSO` to inject:

- leading overrides,
- palette overrides,
- style overrides.

That concrete bundle is useful integration material, but it does **not** define the package-level theory of melody composition.

## 7. Runtime handoff

Runtime consumption of these authoring concepts is defined in:

- `runtime/SSoT_Composer_Melody_Track.md`

## 8. Update triggers

Update this SSoT when:

- phrase palette/archetype meaning changes,
- leading/style asset meaning changes,
- melody override model changes,
- authoring-side melody concepts change independently of ALWTTT.

Phases 1–3 of `Roadmap_Melody_Authoring_MVP.md` are now covered in §5: Phase 1 (closed 2026-06-16 — the `MelodyPatternData` canonical format and `MelodyGenerationParamsSO`), Phase 2 (closed 2026-06-16 — the ladder note-grid authoring semantics, "Grid authoring semantics (Phase 2)"), and Phase 3 (closed 2026-06-17 — the wizard's generation-parameters section + the editor-only `SimplifiedMelodyGenerator`, "Generation parameters & simplified generator (Phase 3)"). When Phase 4 (the `ComposeFromPattern` runtime-override handoff) lands, extend this SSoT and `runtime/SSoT_Composer_Melody_Track.md` to cover the runtime handoff — how the authored pattern reaches the composer and how degrees resolve to pitch against the active tonality/root.

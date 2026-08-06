# SSoT — Authoring Melody Composition

> **Status note (updated 2026-06-17, Phases 1–3 closed):**
> The authoring concepts described here — phrase palettes, phrase archetypes,
> `MelodicLeadingConfig`, and `MelodicStyleSO` — are **current implemented truth**.
>
> Persistence (Phase 8, closed 2026-07-05, PATTERN-PERSIST-1): `MelodyPatternEditorWindow`
> saves through the shared `TrackPatternConfigStoreResources<MelodyPatternData>` store.
> The pattern save root is `Assets/Resources/ScriptableObjects/Patterns/Melodies`
> (plural), aligned with the `PatternRepositoryResources` read root and the shipped
> assets; the editor previously wrote to a singular `.../Patterns/Melody` folder.
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
> As of Phase 4 (closed 2026-06-17, Unity green + in-game smoke), the **runtime
> pattern-override path** (`MelodyTrackComposer.ComposeFromPattern`) is implemented and
> validated: an authored `MelodyPatternData` is played directly, with scale degrees
> resolved to absolute pitch against the active Part tonality/root. This is the first
> audible melody-authoring phase. The runtime contract is authoritative in
> `runtime/SSoT_Composer_Melody_Track.md` §7; the authoring→runtime handoff is summarized
> here in §7.
>
> As of Phase 5 (closed 2026-06-22), the pattern-override path's edge cases are validated
> (empty / single-note / shorter-than-Part / longer-than-Part / extreme `octaveOffset`) as
> correct and deterministic, and meter-mismatch handling is resolved as **D-MEL5.1 = A**: a
> pattern whose `beatsPerMeasure` differs from the Part meter tiles by raw beats with a
> warning — the documented MVP limitation; bar-time renormalization is post-MVP. The runtime
> contract remains authoritative in `runtime/SSoT_Composer_Melody_Track.md` §7.
>
> All Melody Authoring MVP phases (1–5) are now closed and the **Melody Authoring MVP is
> complete** (Phase 5 — polish, validation, documentation closure — closed 2026-06-22).
>
> See `planning/archive/Roadmap_Melody_Authoring_MVP.md` for accepted design decisions.

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

> Runtime enumeration of the phrase vocabulary (canonical folder
> `Resources/ScriptableObjects/Patterns/Phrases`, store-based) is defined in
> `runtime/SSoT_Composer_Melody_Track.md` §4 (MGP-ALWTTT-DBG-2).

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

## 4b. Procedural melody: precedence and recipes (MGP-MEL-1b, P6.1)

### Layer precedence (authoritative table)

| # | Surface | Wins over | Silences |
|---|---------|-----------|----------|
| 0 | Per-render `ctx.patternOverride` (host) | everything | all procedural surfaces |
| 1 | `MelodyCardConfigSO.patternOverride` | 2..6 | style, palettes, leadings (P6.2 signal fires, logGenerator-gated) |
| 2 | `TrackParameters.Pattern` (as `MelodyPatternData`) | 3..6 | same as 1 |
| 3 | `MelodyCardConfigSO.phrasePaletteOverride` | 4 (palette slot only) | `leadingOverride.phrasePalette` AND the default leading's palette — **a palette set inside a `MelodicLeadingConfig` used as `leadingOverride` is INERT when the card also carries `phrasePaletteOverride`** |
| 4 | `MelodyCardConfigSO.leadingOverride` | 5 | constructor/default leading (all fields) |
| 5 | Constructor default `MelodicLeadingConfig` (`MidiGenPlayConfig.melodicLeading`) | — | — |
| 6 | `MelodyCardConfigSO.style` | constructor base strategy | — (orthogonal to 3–5: style picks PITCH POLICY, leading picks TASTE, palette picks PHRASING) |

Row 3 is the case that motivated P6: a leading authored with its own
`phrasePalette` looks configured and is silently overridden. The P3
effective-leading log line (below) now exposes this every render.

Per-phrase, inside 6: `usePerPhraseOverrides` → weighted directive draw →
`useOverrideStrategy` replaces the base strategy for that phrase →
`contour` / `repeatDirective` wrap it in `ConstrainedMelodyStrategy`.

> **A directive is ALWAYS drawn when the list is non-empty.** There is no
> implicit "no directive" outcome. If some phrases should run unconstrained,
> author an explicit neutral directive and give it weight.

### Directive intent contract (F1)

`RepeatLastNotesDirective` and `InterPhraseIntervalDirective` are
`[Serializable]` classes: their instances ALWAYS exist after deserialization,
so instance presence carries no intent. **The `enabled` bool is the only
intent signal**, and the composer gates both on it before they can affect a
render. F1 closed the repeat-side gap; the interval side always had the gate.

### Motif repetition semantics (F2, D8=B)

With `repeatDirective.enabled`, the first `notesToRepeat` audible picks of the
phrase form the MOTIF (chosen by the strategy, contour applied); every later
slot replays it cyclically, transposed by `transposeSemitones` once per
completed cycle. `transpose = 0` is an exact ostinato; `+2` is an ascending
sequence. Rests never enter the motif. The buffer is phrase-scoped — one
decorator instance per chord span.

> **AUTHORING HAZARD — `transposeSemitones` is CHROMATIC, not diatonic.**
> Verified benign in E Ionian only because that motif's intervals happened to
> land in-scale (B→+2→C♯, A→+2→B). A motif containing degree 7 transposed +2
> leaves the scale (D♯ → F♮ in E major). Until a diatonic variant exists
> (`transposeScaleSteps`, not scheduled), prefer `transpose = 0` or verify the
> specific motif degrees against the mode. The transposition also ACCUMULATES
> per cycle and stops only at the instrument-range clamp — pair a non-zero
> transpose with short burst archetypes.

### Contour semantics (F3, D9)

`AscendingOnly` / `DescendingOnly` snap a violating pick to the NEAREST
candidate of the same harmonic pool (chord / scale / noteSource /
allowedDegrees) strictly above or below the phrase reference (peak ?? start).
Scale-aware, never chromatic. When no candidate exists on the required side
(a range edge), the inner pick is kept: a soft contour miss beats an
out-of-scale note.

### Rests and phrase breathing

Intra-phrase rests **already exist and already fire**: a `PhraseSlot` with
`playNote = false` is skipped by the composer and therefore never logged,
which is why `[MelodySlot]` lines can start at `slot=1/…`. Rest density is an
ARCHETYPE property, not a leading or style property.

What does NOT exist is a phrase-final breath. Sustaining archetypes fill the
remainder of the chord span with the held note (observed `dur=7.75` on an
8-beat span), so consecutive phrases run together and read as continuous
singing. The authoring workaround today is shorter spans or denser palettes.

### Inert / reserved fields (P2 registry)

| Field | Status |
|---|---|
| `MelodicStyleSO.swingAmount`, `.humanize` | reserved, hidden (`[HideInInspector]`) |
| `MelodicLeadingConfig.chancePassingNote` | reserved, hidden |
| `MelodicLeadingConfig.voicingPreset` | reserved, hidden (zero melody-side consumers) |
| `PhrasePaletteSO.allowCrossChordPhrases` | reserved, hidden (one-chord-one-phrase model) |
| `WeightedPhraseDirective.overrideStrategy` | MIGRATED to `useOverrideStrategy` + value (P2.1); the old nullable never serialized, so no data migration exists or is needed |

### Recipe 1 — literal melody

`MelodyCardConfigSO.patternOverride` = an authored `MelodyPatternData`.
Ignores harmony by design; adapts to Part tonality/root; deterministic.
Everything else on the card is inert, and the P6.2 signal says so under
`logGenerator`.

### Recipe 2 — palette melody

`phrasePaletteOverride` (phrase vocabulary) + `style`
(`baseStrategy = ScaleFlow`, weighted directives — include a neutral directive
if some phrases should run free) + optional `leadingOverride` (taste and
velocities). Identity is the palette; the notes vary per seed.

### Recipe 3 — chord-aware climb

`style.baseStrategy = AscendingClimb`, `usePerPhraseOverrides = false`;
leading with `noteSource = PreferChordTonesAllowScale` (or `ChordTonesOnly`),
`maxStepSemitones = 2–4`, low `chanceRepeatNote`; an EvenFlow-dominant
palette. Ascends within AND across phrases (one phrase per chord, so the steps
follow the harmony's timing); chord tones are weighted ×1.8, so the chosen
degree follows the sounding chord; the final slot cadences deliberately to the
tonic two octaves up (`octs = 2` is currently hardcoded — recorded gap). Needs
an instrument range of roughly 2.5 octaves or more for an 8-chord part.

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
root by the `ComposeFromPattern` branch (Phase 4, closed 2026-06-17; see §7 and
`runtime/SSoT_Composer_Melody_Track.md` §7). The same pattern plays back
identically — any randomness lives at generation time, not playback.
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

### MIDI file import (Batch M2)

`MelodyPatternEditorWindow` can import a standard MIDI file (`.mid`) into the
working copy. The parse is owned by `MelodyMidiImporter` (`Editor/`, namespace
`MidiGenPlay.Authoring`) — a pure function with no Unity-API calls, in the same
mold as `DrumMidiImporter` (Batch M1). The window owns the apply step; the target
asset is untouched until Apply / Save As. This implements what
`Roadmap_Melody_Authoring_MVP.md` called Phase D1.

**Grid semantics.** The caller supplies the target `TimeSignature` and
subdivisions — the editor's Timing controls, not the file's own meta events.
Grid-beat conversion is beat-unit aware, matching the runtime `GetBeatSpan`
convention: in X/8 meters one grid beat is an eighth note, so
`gridBeats = quarterNotes × beatUnit / 4`. Only ticks-per-quarter-note files are
supported; SMPTE time division is a hard failure. Unlike the drum grid, melody
timing is **beat-absolute** (`startBeat` / `durationBeats`), so the import writes
beats, not step indices — subdivisions act purely as the quantization ladder.
Measures are derived from content unless explicitly supplied, and derivation
covers the **last note's end**, not its onset, because melody notes have duration
(cap 64).

**Pitch → degree.** The key is **user-specified** (D-MIDI1=A): root as a
`NoteName` plus a `Tonality`, the same pair the runtime resolution seam takes.
Absolute pitch resolves to a degree + absolute scale octave against the package
interval tables, via `GetScaleFromTonality` — the single authority seam.
Chromatic notes snap to the nearest diatonic degree with a per-note warning
(D-MIDI2=A); on an equidistant tie the note snaps **down** in pitch. In all seven
v1 modes every chromatic pitch class is exactly one semitone from a scale tone on
each side, so the tie rule is the operative rule — chromatic notes always snap one
semitone down. No accidental metadata is added: the per-note model (§5) stays a
single diatonic degree.

**Reference octave.** `octaveOffset` is relative to a reference the *runtime*
supplies (the instrument's mid register, D-MEL4.2) and which does not exist at
authoring time. The importer therefore **auto-centers**: the modal absolute scale
octave across the imported notes becomes offset 0, ties resolving to the lower
octave — the same modal-with-lower-tie idiom M1 uses for lane default velocity.
The chosen reference and the resulting offset span are echoed in the import panel
for traceability. Consequence, and the melody analogue of M1's kit-agnostic note:
a file spanning a very wide register (or mixing two parts on one channel) yields
large offsets that runtime **clamps** to the instrument band at render time, which
can flatten the contour. That clamp is a render-time concern owned by
`runtime/SSoT_Composer_Melody_Track.md`, not by the importer.

**Monophonization.** `MelodyPatternData` is a monophonic line. After
quantization, notes sharing a start position are reduced to the **highest pitch**
(warning), and a note still sounding when the next note starts is **truncated** at
that start (warning) — preserving both onsets, so the rhythmic contour survives.
Ordering is fully determined (start ascending, then pitch, velocity, duration
descending), so the reduction is deterministic.

**Duration.** Preserved (unlike the trigger-based drum grid) and quantized to the
subdivision ladder, with a one-step floor; a duration rounding to zero is raised
to one step **with a warning**, since that is a real loss even when the rounding
error is below the snap threshold.

**No silent fallback.** Every lossy step emits a warning surfaced in the MIDI
panel, using the same `[Kind] loc: detail` shape as the M1 importer (melody has no
lanes, so `loc` is always `file`):

| Warning kind | Raised when |
|---|---|
| `UnsupportedTimeDivision` | file is null or uses SMPTE time division (hard fail) |
| `NoNotesFound` | channel filter or measure range left zero notes (hard fail) |
| `ChannelsMerged` | notes from more than one channel were merged; per-channel counts listed |
| `ChromaticSnapped` | a note is outside the specified key; snapped to the nearest degree |
| `OffGridSnap` | onset snap error exceeds 0.25 step; first 8 detailed, remainder aggregated |
| `DurationSnapped` | duration snap error exceeds 0.25 step, or was raised to the one-step floor |
| `PolyphonyReduced` | simultaneous notes reduced to the highest pitch |
| `OverlapTruncated` | an overlapping note was truncated at the next note's start |
| `NotesBeyondRange` | a note starts past the resolved measure count; dropped |
| `DurationClipped` | a note extends past the resolved measure count; clipped to the end |
| `MeasuresCapped` | content implies more than 64 measures |

The importer assumes reasonably quantized input: it snaps and warns, and does not
attempt to interpret swing or humanized feel.

Decisions and phase scope: `planning/archive/Roadmap_MIDI_Import.md`
(D-MIDI1..5 and M2-D1..D6).

## 6. Explicit boundary with ALWTTT

ALWTTT may use a concrete bundle such as `MelodyCardConfigSO` to inject:

- leading overrides,
- palette overrides,
- style overrides.

That concrete bundle is useful integration material, but it does **not** define the package-level theory of melody composition.

## 7. Runtime handoff

Runtime consumption of these authoring concepts is defined in
`runtime/SSoT_Composer_Melody_Track.md` (authoritative). As of Phase 4 the authored
`MelodyPatternData` reaches runtime as follows:

- **Carrier.** The pattern is read either from a consumer melody card
  (`MelodyCardConfigSO.patternOverride`, which wins) or from the track-level
  `TrackParameters.Pattern` (`PatternDataSO`) fallback (D-MEL4.1 + D-MEL-INT1; no
  melody-specific `TrackParameters` field was added). `MelodyTrackComposer` detects either
  and renders it through `ComposeFromPattern` instead of the procedural pipeline. Full
  precedence is in `runtime/SSoT_Composer_Melody_Track.md` §7.
- **Pitch resolution.** Each note's `(degree, octaveOffset)` resolves to absolute pitch
  against the active Part tonality/root from the instrument's mid register (D-MEL4.2) —
  the runtime realization of the §5 determinism boundary. The chord progression is not used.
- **Meter.** Timing stays in beats, quarter-mapped exactly as the procedural path does;
  the authored loop is tiled to the Part (D-MEL4.3).
- **Determinism.** No RNG; same pattern + same tonality/root + same meter ⇒ identical
  MIDI.
- **Handoff to harmony.** The authored line is cached as guide notes (D-MEL4.4), so a
  harmony track can follow it.

Authoring owns the pattern's *meaning* (this document); runtime owns *how it is
consumed* (the runtime SSoT). Authoring tools never depend on the runtime path.

## 8. Update triggers

- the procedural precedence table, the directive intent contract, the motif /
  contour semantics, the rest-and-breathing statement, or the reserved-field
  registry (§4b, MGP-MEL-1b) change;

Update this SSoT when:

- phrase palette/archetype meaning changes,
- leading/style asset meaning changes,
- melody override model changes,
- the MIDI import contract changes (key specification, chromatic-snap rule,
  reference-octave auto-centering, monophonization rule, duration quantization,
  or the warning taxonomy),
- authoring-side melody concepts change independently of ALWTTT.

Phases 1–4 of `Roadmap_Melody_Authoring_MVP.md` are now covered: Phase 1 (closed 2026-06-16 — the `MelodyPatternData` canonical format and `MelodyGenerationParamsSO`, §5), Phase 2 (closed 2026-06-16 — the ladder note-grid authoring semantics, §5 "Grid authoring semantics (Phase 2)"), Phase 3 (closed 2026-06-17 — the wizard's generation-parameters section + the editor-only `SimplifiedMelodyGenerator`, §5 "Generation parameters & simplified generator (Phase 3)"), and Phase 4 (closed 2026-06-17 — the runtime handoff via `MelodyTrackComposer.ComposeFromPattern`, §7; runtime contract in `runtime/SSoT_Composer_Melody_Track.md` §7). Phase 5 (polish, validation, documentation closure) closed 2026-06-22 — edge cases validated and deterministic, meter-mismatch resolved as D-MEL5.1 = A (tiles-by-beats retained as the documented MVP limitation; bar-time renormalization is post-MVP), and the governed docs swept; the **Melody Authoring MVP is complete**.

Beyond the MVP, Batch M2 of `planning/archive/Roadmap_MIDI_Import.md` (closed
2026-07-23) added the MIDI file import path (§5 "MIDI file import (Batch M2)"),
implementing and superseding what the melody MVP roadmap listed as deferred
Phase D1. It is an authoring-side content source only: no runtime code and no
change to the per-note model.

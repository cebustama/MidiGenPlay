# SSoT — Composer Melody Track

## Scope

This document is the primary runtime authority for melody generation behavior:

- `MelodyTrackComposer`
- `PhrasePlanner` as runtime planning dependency
- `MelodicLeadingConfig`
- `MelodicStyleSO`
- use of phrase palette/archetype-driven planning
- melody-specific style/leading/palette override resolution
- pattern-override consumption (`ComposeFromPattern`, §7)

## 1. Runtime model

Melody generation is split conceptually into two stages:

1. **phrase planning** — generate expressive/rhythmic slots for a span
2. **note choice and rendering** — choose pitches/strategies and convert slots to MIDI events

`PhrasePlanner` is responsible for stage 1.
`MelodyTrackComposer` and melody strategies are responsible for stage 2.

As of Melody Authoring MVP Phase 4 there are two rendering paths: the procedural
pipeline above, and an authored-melody **pattern-override** path (`ComposeFromPattern`,
§7) that bypasses phrase planning when a `MelodyPatternData` is present on the track.

## 2. Inputs

Important package-owned melody inputs include:

- `MelodicLeadingConfig`
- phrase palette / phrase archetypes
- `MelodicStyleSO`
- per-track strategy/style overrides carried through package runtime inputs

## 3. Current integration boundary

The current runtime can consume a concrete external bundle type such as `MelodyCardConfigSO`.
That is useful integration surface, but it is **not** the primary theory of melody authoring for the package.

Document package truth here first.
Describe ALWTTT-specific card usage only in cross-project reference.

A consumer melody card (`MelodyCardConfigSO`) may additionally carry a `patternOverride`
(`MelodyPatternData`) that the composer plays verbatim; its precedence is documented in §7.

## 4. Phrase planning contract

`PhrasePlanner` is a rhythmic/expressive planner, not the final pitch selector.

It produces `PhraseSlot` structures carrying information such as:

- timing
- phrase grouping
- contour hints
- accent / phrase-end information
- phrase-local state cues for strategies

## 5. Leading/style contract

`MelodicLeadingConfig` expresses pitch-motion and expressive defaults.
`MelodicStyleSO` selects or modifies strategy behavior at phrase level.
These are package-owned concepts and remain primary even when a consuming game injects concrete override bundles.

## 6. Boundary with authoring

Authoring-side meaning of palettes, phrase archetypes, leading assets and style assets lives in:

- `authoring/SSoT_Authoring_Melody_Composition.md`

This runtime SSoT documents how those inputs are consumed during generation.

## 7. Pattern-override path (`ComposeFromPattern`)

As of Melody Authoring MVP Phase 4 (closed 2026-06-17), `MelodyTrackComposer` has a
second, authored-melody path alongside the procedural pipeline, analogous to the rhythm
composer's `DrumPatternData` → `ComposeFromGrid` branch.

### Integration surface & precedence (D-MEL4.1, D-MEL-INT1)

The authored melody can reach the composer two ways, in this precedence (mirroring
`RhythmCardConfigSO.patternOverride`):

1. **`MelodyCardConfigSO.patternOverride`** (D-MEL-INT1) — read off `TrackParameters.Style`
   when the bundle is a `MelodyCardConfigSO` with a non-null `patternOverride`. This is the
   consumer-card path (e.g. ALWTTT).
2. **`TrackParameters.Pattern`** (D-MEL4.1) — a track-level `PatternDataSO`; no new serialized
   field. The fallback when no card override is present.
3. otherwise the **procedural pipeline**.

At the top of `Compose`, after the instrument null-check and before progression resolution:

    var melodyPattern = (cfg.Parameters?.Style as MelodyCardConfigSO)?.patternOverride
                        ?? (cfg.Parameters?.Pattern as MelodyPatternData);
    if (melodyPattern != null) return ComposeFromPattern(...);

- a melody pattern present (card override or track-level) ⇒ render via `ComposeFromPattern`
  and return (the procedural pipeline, `PhrasePlanner`, strategies, the
  `Pattern as ChordProgressionData` read, and the card's `leading/palette/style` *procedural*
  overrides are all skipped — the authored pattern wins over procedural);
- none present ⇒ the procedural path runs unchanged (and still reads
  `Pattern as ChordProgressionData` for its harmonic-context fallback).

`MelodyPatternData` and `ChordProgressionData` are mutually exclusive on a single concrete
`Pattern` instance, so there is no track-level collision. In a normal multi-track song the
melody's harmonic context arrives via `ctx.GetProgressionForPart` from the chord/backing
track regardless, and `ComposeFromPattern` does not consult it. The card's `patternOverride`
carries an authored melody; the card's other fields remain *procedural* overrides used only
when no pattern is selected.

### Degree → pitch resolution (D-MEL4.2)

`ComposeFromPattern` does **not** use the chord progression. Each note's
`(degree, octaveOffset)` resolves to an absolute pitch against the active Part
tonality/root via `MusicTheory.GetNoteFromScale(scale, degree, RootNote, octave, …)`,
where `scale = GetScaleFromTonality(part.Tonality, part.RootNote)`. The reference
register is the instrument's mid octave (reusing the file's `ChooseMelodicRegister`
convention, `octaveMin-1 .. octaveMax-1`); `octaveOffset` is applied on top and the
target octave is clamped to the instrument's playable range, so a degree always resolves
to a note the instrument can sound. This is the runtime half of the §5 determinism
boundary in `authoring/SSoT_Authoring_Melody_Composition.md`: pitch is computed at play
time, not stored.

### Meter & looping (D-MEL4.3)

Note timing is in beats; one beat maps to a quarter note (`MusicalTimeSpan.Quarter`),
identical to the procedural `ComposeMelodyFromProgression`, so both melody paths share one
timing model. The authored loop (`pattern.TotalBeats`) is tiled to the Part's total beats
(`part.Measures × beatsPerMeasure`) and the final partial loop is truncated **by note
onset**: an onset at or after the Part's end is dropped, while a note whose onset falls
inside the Part rings to its authored duration even when that crosses the Part boundary.
When the pattern's `beatsPerMeasure` differs from the Part meter the loop tiles by raw
beats (a warning is logged under `logGenerator`). This tiles-by-beats behavior is the
**accepted Melody Authoring MVP outcome** (D-MEL5.1 = A, Phase 5 closed 2026-06-22): a
mismatched-meter pattern will not align to the Part's barlines, and that limitation is
documented rather than corrected for the MVP. Full bar-time renormalization of a
mismatched melody pattern — and beat-unit-aware timing for compound/odd meters across both
melody paths — remains **post-MVP future work** (the procedural path likewise assumes
quarter beats, and melody timing is continuous beats, unlike the rhythm step grid).

### Determinism

`ComposeFromPattern` consumes no RNG. The same pattern + same tonality/root + same Part
meter produces byte-identical MIDI, and the path does not perturb `ctx.rng`, so other
tracks' seeded draws are unaffected. The card-override selection (D-MEL-INT1) is likewise
RNG-free and feeds the same path.

Phase 5 (closed 2026-06-22) validated this guarantee across the path's edge cases — empty
pattern (silence, no crash), single-note, shorter-than-Part (tiles), longer-than-Part
(onset-truncated), and `octaveOffset` at the band extremes (clamped to the instrument
range) — all correct and deterministic; authored duration is floored (a zero or negative
value still sounds) and velocity is clamped to 1–127.

### Guide-note handoff (D-MEL4.4)

After rendering, the authored line is cached via
`ctx.SetMelodyForPartMusician(part, MusicianId, guideNotes)` — the same per-part /
per-musician cache the procedural path populates — so a `HarmonyTrackComposer` can
harmonize an authored melody just as it does a procedural one.

### Boundary

`ComposeFromPattern` is runtime code (`Runtime/CoreScripts/Composition/Composers/`) and
has no editor dependency. It consumes `MelodyPatternData`, whose authoring semantics live
in `authoring/SSoT_Authoring_Melody_Composition.md` §5/§7; it does not own them.

## 8. Update triggers

Update this SSoT when:

- phrase planner/composer responsibilities change,
- override precedence changes (incl. the card `patternOverride` ↔ `TrackParameters.Pattern` order),
- strategy/style resolution changes,
- runtime use of melody bundles changes,
- the authored-pattern path (`ComposeFromPattern`) — its integration surface,
  degree→pitch resolution, meter handling, or guide-note handoff — changes.

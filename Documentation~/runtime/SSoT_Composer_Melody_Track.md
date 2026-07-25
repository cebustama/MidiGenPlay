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

### Missing palette / planner return contract (MEL-NULL-1)

`PhrasePlanner.PlanPhraseSlotsForSpan` **never returns null.** When it cannot plan,
it returns an **empty slot list**. Callers read "no slots" as "no notes", never as
an error to dereference.

A **usable phrase palette** is a **hard precondition of the procedural pipeline**.
`PhrasePlanner.HasUsablePalette(MelodicLeadingConfig)` is the single definition:
leading config present, carrying a `PhrasePaletteSO`, with **at least one archetype
entry**. Both the planner's bail and the composer's precondition gate on this one
predicate; they must not re-derive it.

`MelodyTrackComposer.ComposeMelodyFromProgression` checks it up front and, on
failure, logs a single error and returns an **empty melody track** — every other
role still renders. A missing optional authoring asset must never abort the render.

Palette resolution (unchanged): card `phrasePaletteOverride` > card
`leadingOverride.phrasePalette` > `MidiGenPlayConfig.melodicLeading.phrasePalette`.
An authored `MelodyPatternData` takes the §7 `ComposeFromPattern` path and needs no
palette.

Determinism: the empty-track early-out consumes no RNG draws and per-track streams
are isolated, so no other track's output changes. Every usable-palette
configuration is bit-identical to pre-MEL-NULL-1 behavior.

### Runtime phrase-vocabulary enumeration (Ask B, MGP-ALWTTT-DBG-2, E-2=A)

`PhrasePaletteSO` and `PhraseArchetypeSO` reach the composer only by reference
(`MelodicLeadingConfig.phrasePalette`); for catalog/debug UIs they are
enumerable with
`new TrackPatternConfigStoreResources<PhrasePaletteSO>("Phrases")` and
`new TrackPatternConfigStoreResources<PhraseArchetypeSO>("Phrases")` →
`Refresh()` / `GetAll()`. Canonical folder:
`Resources/ScriptableObjects/Patterns/Phrases` (archetypes may live in any
subfolder; `LoadAll` is recursive and type-filtered — `PhraseArchetypeSO` is
abstract, so concrete archetype assets load under it). Migration note: the
shipped phrase assets predate this contract and lived under
`Resources/ScriptableObjects/Phrases`; the folder must sit under `Patterns/`
to be enumerable (GUID references from `MelodicLeadingConfig` survive the
move). Display metadata: palette → name, `defaultContourBias`,
`allowCrossChordPhrases`, archetype entries (asset name + weight); archetype →
name (the concrete type conveys the shape) and `forcedContourDir`. This is a
documented contract only — `IPatternRepository` is NOT extended (E-2=A/E-3=A;
phrase vocabulary is not a track pattern).

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

0. **`GenContext.patternOverride`** as `MelodyPatternData` (per-render override,
   Ask C / D-DBG4=A) — **precedence step 0**, wins over the card override and
   `TrackParameters.Pattern`; clone-on-apply; a non-`MelodyPatternData` override
   is warn + ignore.
1. **`MelodyCardConfigSO.patternOverride`** (D-MEL-INT1) — read off `TrackParameters.Style`
   when the bundle is a `MelodyCardConfigSO` with a non-null `patternOverride`. This is the
   consumer-card path (e.g. ALWTTT).
2. **`TrackParameters.Pattern`** (D-MEL4.1) — a track-level `PatternDataSO`; no new serialized
   field. The fallback when no card override is present.
3. otherwise the **procedural pipeline**.

At the top of `Compose`, after the instrument null-check and before progression resolution:

    var overridePattern = ctx?.patternOverride as MelodyPatternData; // clone-on-apply; mismatch warn+ignore
    var melodyPattern = overridePattern
                        ?? (cfg.Parameters?.Style as MelodyCardConfigSO)?.patternOverride
                        ?? (cfg.Parameters?.Pattern as MelodyPatternData);
    if (melodyPattern != null) return ComposeFromPattern(...);

- a melody pattern present (card override or track-level) ⇒ render via `ComposeFromPattern`
  and return (the procedural pipeline, `PhrasePlanner`, strategies, the
  `Pattern as ChordProgressionData` read, and the card's `leading/palette/style` *procedural*
  overrides are all skipped — the authored pattern wins over procedural);
- none present ⇒ the procedural path runs unchanged (and still reads
  `Pattern as ChordProgressionData` for its harmonic-context fallback).

The authored path reports `source` + pre-clone `sourceAssetName`; the procedural
path reports the LIST of phrase archetypes chosen per chord span
(`melodyArchetypesBySpan`, from `PhrasePlanner.LastPlannedArchetypeName`,
observability-only) — there is no single pattern identity on that path
(MGP-ALWTTT-DBG-1, Ask A).

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

### Meter & looping (D-MEL4.3, corrected by MEL-BEATUNIT-1)

Note timing is in beats, and **one beat is the Part meter's beat unit** — a quarter in
4/4, an eighth in 6/8 — resolved via `MusicTheory.GetBeatSpan(part.TimeSignature)` and
applied at the single conversion seam `MelodyTrackComposer.BeatsToSpan` (§7.1). Both
melody paths share that one timing model. The authored loop (`pattern.TotalBeats`) is
tiled to the Part's total beats (`part.Measures × beatsPerMeasure`) and the final partial
loop is truncated **by note onset**: an onset at or after the Part's end is dropped, while
a note whose onset falls inside the Part rings to its authored duration even when that
crosses the Part boundary.

When the pattern's `beatsPerMeasure` differs from the Part meter the loop tiles by raw
beats (a warning is logged under `logGenerator`). This tiles-by-beats behavior is the
**accepted Melody Authoring MVP outcome** (D-MEL5.1 = A, Phase 5 closed 2026-06-22): a
mismatched-meter pattern will not align to the Part's barlines, and that limitation is
documented rather than corrected.

**D-MEL5.1 = A stands.** MEL-BEATUNIT-1 resolved only the beat-unit axis and is a bounded
exception to it, not a revision of it. The two axes are independent: `Quarter` vs the Part
beat span is *how long one beat is*; `pattern.beatsPerMeasure` vs `beatsPerBar` is *how
many beats a bar holds*. Full bar-time renormalization of a mismatched melody pattern
remains **post-MVP future work** (melody timing is continuous beats, unlike the rhythm step
grid that `NormalizeGridPatternForPartIfNeeded` remaps).

### 7.1 Beat-unit authority and the recorded MEL-BEATUNIT-1 deviation

Every emission site in `MelodyTrackComposer` converts beats to musical time through one
seam:

    internal static ITimeSpan BeatsToSpan(double beats, MusicalTimeSpan beatSpan)
        => beatSpan.Multiply(beats);

`beatSpan` is `GetBeatSpan(part.TimeSignature)`, derived alongside the `beatsPerBar` each
path already computed — meter authority per `SSoT_CONTRACTS.md` §5, mirroring
`ChordTrackComposer` and, since CA-F2, `BassTrackComposer`. Three sites go through it:
`ComposeFromPattern`, the procedural `ComposeMelodyFromProgression`, and the currently
unreachable `ComposePerBeatMelody`.

**Deviation on record (F-1, characterized 2026-07-24, closed the same day by
MEL-BEATUNIT-1).** Both melody paths previously placed notes with
`MusicalTimeSpan.Quarter.Multiply(whenBeats)`, treating a beat as a quarter note whatever
the meter. In every `beatUnit != 4` meter (6/8, 9/8, 12/8, 7/8) melody therefore rendered at
half speed against the rest of the render, and overran the Part window that
`SongOrchestrator` sizes from the same `beatSpan`. Everything upstream was already
beat-unit aware — the rhythm step grid, the bass, and all three MIDI importers
(`gridBeats = quarterNotes × beatUnit / 4`); melody was the last consumer that was not. Two
properties bound the damage and must be read together: the error was a **uniform scaling**,
so melodic contour and internal rhythm survived intact and only **cross-track
synchronization** broke; and it was **not** an import defect — a pattern authored by hand in
`MelodyPatternEditorWindow` hit it identically.

Scope of the change, stated as the bass's §3.4 states its own:

- **Byte-identical in every `beatUnit == 4` meter.** `GetBeatSpan` returns
  `MusicalTimeSpan.Quarter` there, so the substitution is a structural identity, not an
  empirical one. Pinned by `BeatsToSpan_FourFour_IsBitIdenticalToLegacyQuarter`.
- **In other meters the output deliberately changes** — a sync FIX, not a regression.
  Pinned by `BeatsToSpan_SixEight_IsHalfTheLegacyQuarterTicks` and the all-meters table
  test `BeatSpan_AllTimeSignatures_MatchTheirBeatUnit`.

**No migration of authored content.** `MelodyPatternData` stores timing in beats and
inherits `PatternDataSO.TimeSignature`; `MelodyMidiImporter` already writes
`gridBeats = quarterNotes × beatUnit / 4`. Stored beats were therefore always in the
meter's beat unit — only the render misread them, so rescaling assets would double-correct
correct data. The one user-visible consequence: an author who hand-compensated for the old
render (writing X/8 notes at double speed) must undo that compensation.

**Boundary of the correction.** It lives entirely below `ResolvePatternNotesCore`, which
counts beats and is meter-unit agnostic; that seam is unchanged and pinned as such by
`Resolve_SixEightPart_ResolutionSeamIsUnchanged`. `ComposePerBeatMelody` was corrected in
lockstep although unreachable (its only call site is commented out in `Compose`) so it
cannot reintroduce the desync if re-enabled; whether to delete it is a separate, open
question.

**Operational note, superseding the previous one.** Melody smoke no longer needs to avoid
compound/odd meters; a 6/8 smoke is now a valid check of melody itself, and 4/4 is the
byte-identity control.

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

`GuideNote.startBeats` / `.durBeats` are in **Part beat units**, not quarters. The payload
is unchanged by MEL-BEATUNIT-1, but any future consumer must convert with the Part's
`beatSpan` (or `BeatsToSpan`); reusing `MusicalTimeSpan.Quarter` would reintroduce F-1 one
layer downstream. There is no in-package consumer today.

### Boundary

`ComposeFromPattern` is runtime code (`Runtime/CoreScripts/Composition/Composers/`) and
has no editor dependency. It consumes `MelodyPatternData`, whose authoring semantics live
in `authoring/SSoT_Authoring_Melody_Composition.md` §5/§7; it does not own them.

## 8. Update triggers

Update this SSoT when:

- phrase planner/composer responsibilities change,
- override precedence changes (incl. the per-render `patternOverride` step 0 and
  the card `patternOverride` ↔ `TrackParameters.Pattern` order),
- strategy/style resolution changes,
- runtime use of melody bundles changes,
- the authored-pattern path (`ComposeFromPattern`) — its integration surface,
  degree→pitch resolution, meter handling, or guide-note handoff — changes,
- the melody timing unit changes, or a new emission site bypasses the single
  `MelodyTrackComposer.BeatsToSpan` conversion seam (§7.1, MEL-BEATUNIT-1),
- the phrase-planner return contract, the definition of a *usable* palette
  (`PhrasePlanner.HasUsablePalette`), or the missing-palette behavior changes
  (MEL-NULL-1: never returns null; a procedural melody without a usable palette
  yields an empty track and one error, not a failed render).

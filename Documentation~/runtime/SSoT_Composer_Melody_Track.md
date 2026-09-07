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

**A phrase may end before its span does (MGP-TONALITY-1).**
`PhraseArchetypeSO.endRestFraction` (0 = legacy) shortens the phrase-end slot
so consecutive phrases do not run together. Before MGP-TONALITY-1 every
archetype filled 100% of its chord span and spans tile contiguously, making
inter-phrase silence structurally impossible. The trim is clamped: the final
note always keeps the greater of 1/8 beat and 25% of its planned duration, so
a short closing slot trims little or nothing rather than vanishing.

**Slot counts and burst/pickup durations may be snapped to a metric grid.**
`meterFitSlots` (off = legacy) restricts the resulting slot duration to a
power of two in beats (16 … 1/16); `allowTupletSubdivisions` additionally
admits the triplet family (a power of two divided by 3). The constraint is on
the resulting DURATION, not on the count: over a 12-beat span, 3 slots are
legal without tuplets (12/3 = 4 beats), while over an 8-beat span they are not
(8/3 = 2.667). The snap is applied to the RESULT of the slot-count draw, never
in place of it, so toggling `meterFitSlots` cannot shift an RNG stream.

RNG neutrality exception on record: `BurstThenHoldPhraseSO.restProbMid` gates
its per-note roll on `> 0`, so raising it above zero shifts that archetype's
draw stream. Deliberate — an unconditional roll would break byte-identity for
every pre-existing asset.

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
is unchanged by MEL-BEATUNIT-1, but every consumer must convert with the Part's
`beatSpan` (or `BeatsToSpan`); reusing `MusicalTimeSpan.Quarter` reintroduces F-1 one
layer downstream.

**There is one in-package consumer: `HarmonyTrackComposer`** (PASS 2). It did
reintroduce F-1 exactly as warned — it converted with `MusicalTimeSpan.Quarter`
from its introduction until MGP-ALWTTT-HARMONY-1 (2026-09-01, F-HARM-1), which
routed both its emission and its chord lookup through
`MelodyTrackComposer.BeatsToSpan`. The channel's full contract — who is
followed, what the consumer may write back, and what the cache's lifetime is —
lives in `SSoT_Runtime_Generation_Orchestration.md` §5.9; this section owns only
the payload's units.

### Boundary

`ComposeFromPattern` is runtime code (`Runtime/CoreScripts/Composition/Composers/`) and
has no editor dependency. It consumes `MelodyPatternData`, whose authoring semantics live
in `authoring/SSoT_Authoring_Melody_Composition.md` §5/§7; it does not own them.

### 7.2 Directive layer, motif and contour (MGP-MEL-1b)

**Post-pool pitch mutation (chromatic escapes).** The candidate pool is in-key
by construction (`BuildCandidatesWithFilter` → strategy), but two mechanisms
alter the picked note after the strategy has returned it, and neither is
scale-validated:

1. **Motif replay** (`ConstrainedMelodyStrategy`, repeat directive) — echoes
   the stored motif transposed by `transposeSemitones × cycle`. In
   `ChromaticSemitones` mode this is a raw semitone shift, **cumulative per
   cycle**, and any value other than 0 or ±12 will leave the tonality. Since
   MGP-TONALITY-1 (D-TON6=A) the directive also offers `ScaleDegrees` mode, in
   which the transpose counts SCALE DEGREES (diatonic sequence).
   `ChromaticSemitones` remains the serialized default for asset compatibility.
   **`ScaleDegrees` is now total** (MGP-MEL-DEGRADE-1): between D-TON6=A and
   2026-09-05 it was diatonic only for motif notes already in the scale, and a
   chromatic one fell through a wrong-unit fallback. See "Chromatic motif notes
   under `ScaleDegrees`" below — do NOT read the earlier phrasing "cannot leave
   the scale for a motif note that is in it" as a guarantee for the mode; that
   conditional was the whole defect.
2. **Interval directive** (`ApplyIntervalDirective`) — the `FixedSemitones` /
   `PerDirectionSemitones` modes shift in raw semitones; with
   `pitchClassSource = ComputedRegister` the resulting chromatic pitch class is
   the one EMITTED. With `Anchor` / `Candidate` the pitch class stays in-key and
   only the register changes.

There is no tonal validation at the emission point. `TonalityAudit`
(diagnostics, log-only) observes and classifies violations but never alters
output. This was the confirmed root cause of the "Showtime" out-of-key defect:
`MelodicStyle_Showtime` at `transposeSemitones = 2` echoed the motif `G5 B5 A5`
at +2 and +4, emitting `C#6 D#6 C#6` — three `OUT-OF-KEY … origin=strategy+motif`
warnings, matched slot-by-slot. The same asset under `ScaleDegrees` yields zero
reds with the audible sequence effect preserved.

**Harmonic context is accidental-aware (MGP-TONALITY-1, D-TON10).** The
melody's harmonic context is derived from
`TransposeNoteName(scaleNames[(int)e.degree], e.degreeAccidental)` in BOTH the
procedural path and the simple chord-tone-cycle path — the same law the backing
track uses, and the shared chord-identity contract of `SSoT_CONTRACTS.md` §13.
Prior to MGP-TONALITY-1 both paths were accidental-blind.

**Intent contract (F1).** `RepeatLastNotesDirective` and
`InterPhraseIntervalDirective` are `[Serializable]` classes and always
deserialize to an instance, so presence is not intent: the composer gates BOTH
on `.enabled`. Before F1 the repeat side was ungated, and the decorator
short-circuited the strategy into a flat pitch.

**Directive draw.** When `usePerPhraseOverrides` is set and the weighted list
is non-empty, a directive is ALWAYS drawn — there is no implicit
"no directive" outcome. An unconstrained phrase requires an explicitly
authored neutral directive.

**Motif (F2, D8=B).** `ConstrainedMelodyStrategy` keeps a true N-note buffer:
the first `notesToRepeat` audible picks form the motif, later slots replay it
cyclically, and `transposeSemitones` is added once per completed cycle. The
buffer is phrase-scoped (one decorator instance per chord span) and rests never
enter it. Under `ChromaticSemitones` the transposition is a raw semitone shift
— the REAL sequence — so a motif degree transposed out of the mode leaves the
scale. Under `ScaleDegrees` it is the TONAL sequence, in-scale by construction;
see the chromatic-motif law below.

**Accumulation is MODE-INDEPENDENT, and this is a separate open hazard.** The
echoed note is offset by `transposeSemitones × cycle` where
`cycle = (_replayCount / n) + 1`, in BOTH branches, and the only bound is
`ClampToInstrument`. `ScaleDegrees` fixed WHICH notes the motif lands on, not
HOW FAR it travels — do not read the diatonic guarantee as a register
guarantee. Measured in practice (2026-09-05 render logs): the smoke's melody
phrase reaches cycle 1 and matrix cell 327 reaches cycle 2, so with today's
authoring (`notesToRepeat = 3`, phrases of 7–8 slots) the ceiling is ≈ 2 cycles
and the clamp is never approached. The ceiling is roughly `slots / notesToRepeat`,
so a small `n` over a long phrase (n = 1 over 8 slots ⇒ 7 cycles) would reach it.
**Latent, not live.** Same defect family as F-TON-WALK-DRIFT-1 in the bass —
unbounded per-cycle accumulation stopped only by a range clamp — and whether
the two deserve a shared law is an open question, not a decided one. Tracked as
`MGP-MEL-CYCLE-1`, unscheduled.

### Chromatic motif notes under `ScaleDegrees` (MGP-MEL-DEGRADE-1)

**The pool is NOT diatonic, and the earlier note that said otherwise was
wrong.** `MelodyStrategyCommon.BuildPitchClassPool` returns the CHORD's pitch
classes under `ChordTonesOnly` and the chord ∪ scale union under
`PreferChordTonesAllowScale`. Any chord chromatic to the part tonality
therefore puts non-scale notes into the motif BY DESIGN, and three distinct
mechanisms do it: a root accidental (`Prog_Min_Napolitana_bII`'s ♭II), a
seventh (`ProgSmoke_Dominant7`), and **requalification with no accidental at
all** (`Prog_Maj_Creep_IVpiv`'s major III and minor iv, whose chromatic tones
are the chord's third, not its root). `ConstrainedMelodyStrategy`'s XML note
previously claimed a non-scale source "cannot happen for pool-built motifs
today"; it claimed it of the exact case that was failing.

**F-MEL-DEGRADE-1 (found and fixed, 2026-09-05).** `TransposeByScaleDegrees`
fell back to `MelodyStrategyCommon.Transpose(src, degreeSteps)` whenever the
motif note was absent from `degreeLookup` — passing a value counted in DEGREES
to a function that shifts SEMITONES. Traced exactly from tonality matrix cell
#157 (Ionian / 4-4 / `Prog_Min_Napolitana_bII` / `tracks=M`): motif note D#5, a
chord tone of the Neapolitan ♭II `[G# C D#]` and therefore chromatic in
G Ionian, `transposeSemitones = 2`, cycle 1, `63 + 2 = 65 = F5` — in neither
the scale nor the chord. The D-TON6=A fix had closed the DIATONIC motif case;
the chromatic case reached the degrade and re-opened it on any progression
carrying a chord chromatic to the part tonality.

**The guard conflated two conditions and now separates them:**

1. **No usable scale metadata** (`scaleNames` null or shorter than 7,
   `degreeLookup` null, or a lookup index outside 0..6). "Move N degrees" is
   undefined without a scale, so the directive degrades to the EXACT OSTINATO
   that `transposeSemitones == 0` already means (**D-DEGRADE-3**). Degrading
   toward an existing musical gesture, not toward a wrong-unit shift.
2. **Chromatic source pitch class.** The note is snapped to a scale degree and
   the sequence proceeds diatonically, so the echoed pitch class is always
   `scaleNames[newDegree]` (**D-DEGRADE-1 = A**). This is the tonal sequence
   the mode's name promises; `ChromaticSemitones` remains the real sequence and
   is untouched, so every existing asset is byte-identical.

**Snap rule: floor (D-DEGRADE-2).** The nearest degree AT OR BELOW the source.
Two reasons. The mapping stays monotone, so the motif's contour can never
invert — the most perceptually salient property of a motif. And the family that
actually reaches this path is borrowed harmony, whose alterations are
descending: floor reproduces ♭6 → 5 and ♭2 → 1, the textbook resolutions.

**Known limitation, recorded rather than hidden — F-SPELL-PROVENANCE-1.**
Raised alterations (the ♯4 of a secondary dominant) would ideally snap UP. The
rule cannot tell the two apart because `NoteName` is a pitch class: `DSharp`
and `EFlat` are the same value and the spelling is lost upstream. **MIDI has no
concept of spelling either**, so this can never affect rendered fidelity — only
decisions and legibility. The replacement is to carry the chord's PROVENANCE to
the decision point, and note that it CANNOT be the root's `degreeAccidental`:
the Creep case above generates its chromaticism by requalification with no
accidental on the root. Provenance must be derived from the chord's pitch
classes against the scale. Not scoped.

**Where it would also pay off**, so a future batch does not re-derive it: the
red audit line prints `chord=[GSharp C DSharp]` for what is a Neapolitan in
A♭, which a human reading a warning must translate; and the general
voice-leading rule that raised alterations resolve up and lowered ones down is
today inexpressible anywhere in the package.

**Evidence (2026-09-05).** Matrix `tonality_matrix_20260905_202753`: 714 cells
(the progression axis GREW from 2 to 3 in this batch — see below), 0 failures,
`beliefDiv == 0` on every track of every cell, `residualReds == 0`. Melody reds
80 → 0; every one of the 80 carried `origin=strategy+motif`, with no
`interval-directive` contamination. **Blast radius measured against the
pre-fix sweep, not argued:** of the 476 comparable cells, exactly the 70 that
had melody reds changed, 0 cells without reds changed, and bass/backing
counters were identical in 476/476 — the fix consumes no rng, so no downstream
stream shifts. Anchor RE-PINNED (see §7.2 determinism note). Listening pass:
PASS, no out-of-place notes.

**Open observation — F-MEL-SLOTCOUNT-1.** Matrix cells 327 and 378 (Locrian,
both meters, `Prog_Min_Napolitana_bII`, `tracks=M`) emit ONE melody note fewer
after the fix (32 → 31 audited notes). The verbose re-run proves the missing
slot never reached `PickNext` — the replay index did not advance across it, so
it was cut at `!slot.playNote` — which is archetype-determined and
pitch-independent. The mechanism is therefore NOT explained. No red, no
audible impact, 2 cells of 714. Settling it would need the pre-fix verbose log
of the same cell, which no longer exists. Recorded so a future occurrence
starts from the evidence rather than from zero.

**Test surface:** `Tests/Editor/ConstrainedMelodyStrategy_ChromaticMotifTests.cs`
(the #157 case, multi-cycle accumulation, the floor tie-break pinned against
ceiling, both no-scale identity paths, and a regression guard that the DIATONIC
case is unchanged). No existing assertion was re-pinned.

**Signal.** A `logGenerator`-gated line names the snap when it fires
(`chromatic-motif snap | note=… -> degree=… snap=…st steps=…`), wired through
an OPTIONAL constructor argument on `ConstrainedMelodyStrategy` rather than a
static or an interface change, so every other strategy and every existing call
site is untouched. Log-only: it is never read by a decision.

**Contour (F3, D9).** `AscendingOnly` / `DescendingOnly` snap a violating pick
to the nearest candidate of the SAME harmonic pool strictly above/below the
phrase reference (peak ?? start) — scale-aware, never chromatic. With no
candidate on the required side the inner pick is kept.

**Effective-leading log (P3).** One line per render, `logGenerator`-gated,
reporting the leading actually in force and the palette actually in force.
A `(Clone)` suffix on the leading name is cosmetic — it means a palette
override cloned the leading rather than mutating the authored asset.

**`maxStepSemitones` is a PREFERENCE, not a bound (MGP-TRIAGE-ALWTTT-R3, E2).**
ALWTTT reported `maxStep=4` alongside slot logs showing `step=5` and asked
whether the contract was violated. It is not, and the mechanism is not the one
the host hypothesised (post-snap measurement). Two independent reasons:

1. **No strategy enforces it as a limit.** `MelodyStrategyCommon.ComputeMotionWeight`
   multiplies an over-step candidate's weight by `0.01` — it crushes the odds,
   it does not exclude. With a wide candidate pool that surviving mass wins
   sometimes, by design. `AscendingClimbMelodyStrategy` DOES hard-filter its
   upward pool by `maxStepSemitones`, but its no-candidate fallbacks (nearest
   upward candidate; nearest candidate overall) leave the limit deliberately.
   The field's own tooltip says "try to keep", and that is the contract.
2. **The logged number is measured later than the pick.** `emittedStep` is the
   distance between EMITTED notes — after the strategy, after
   `ConstrainedMelodyStrategy`'s contour snap, and after
   `ApplyIntervalDirective`, which moves the note by design. It is not the
   quantity `maxStepSemitones` weights.

The log now reads `maxStepPref=` and `emittedStep=` so the pair cannot be read
as a contract. `emittedStep > maxStepPref` is expected output, not a defect.

Note on the MEL-1b batch record: its F1 evidence line ("all steps ≤
`maxStepSemitones`") was a live OBSERVATION on one render, never a guarantee.
It should not be cited as an invariant.

**Inert-config signal (P6.2).** When a pattern path wins (card
`patternOverride` or `TrackParameters.Pattern`), one `logGenerator`-gated
signal per render names the procedural surfaces that are consequently inert.
Mirrors the TONFILTER-1 signal idiom: a signal, never a degrade.

**Determinism note.** F1 CHANGES the melody rng draw sequence — ScaleFlow now
consumes the per-slot draws the broken decorator short-circuited. Same seed ⇒
a different (and correct) melody versus pre-F1. Any golden pinning procedural
melody bytes must be re-pinned. The SEED-1 rhythm / backing / bass streams are
untouched, since rng is per track.

**Determinism note (MGP-MEL-DEGRADE-1).** The fix does NOT change the rng draw
sequence: the replay path draws nothing, and `ChooseVelocityForSlot` ignores
the picked note and always draws exactly one value per emitted slot. It changes
PITCHES only, and only on slots that took the wrong-unit fallback. **The smoke
anchor was re-pinned deliberately** (seed 12345 / Ionian G / 4-4 / 8 bars):

```
before  59193145 / 95CC33FD / C8A25142 / A61E8AB6 / 6B1133C3
after   59193145 / 95CC33FD / C8A25142 / 963A6665 / 6B1133C3
```

Exactly one chunk moved — the melody — from exactly one snap in the whole
render. The smoke progression is `Prog_Maj_Creep_IVpiv` (`I – III – IV – iv`);
over its major III the motif `D#6 E6 E6` used to echo as `F6 F#6 F#6`, the
`F` natural out of both scale and chord, and now echoes as `F#6 G6 G6`. The
interval shape (+1, 0) is IDENTICAL in both versions, because F♯–G is also a
semitone — the tonal sequence cost nothing in contour and bought the key, and
the landing note F♯ is simultaneously the scale's 7th degree and the sounding
chord's fifth. The re-pin carries a listening PASS.

Test surface: `Tests/Editor/ConstrainedMelodyStrategy_MotifTests.cs`.

**F5 CLOSED (MGP-TRIAGE-ALWTTT-R3, E1) — `PhraseSlot.totalSlotsInPhrase` is
constant within a phrase.** `SustainLeadInPhraseSO`'s pickup branch built THREE
slots (silent lead-in, pickup attack, sustain) while hardcoding
`totalSlotsInPhrase = 2` on the first two and `3` on the last — the observed
`slot=1/2` then `slot=2/3` under one `phraseId`. All three now carry `3`.

**The gap was misclassified as inert, and that matters.** The original note
justified "no render impact" by observing that nothing consumes
`PhraseState.TotalNotesInPhrase` — true, and still true. But the SLOT field has
a second consumer: `MelodyTrackComposer.IsFinalSlotOfPart` is literally
`slotIndexInPhrase == totalSlotsInPhrase - 1`, which a drifting denominator
satisfies MORE THAN ONCE. On the part's last chord span that made
`MelodyPartState.IsFinalSlotOfPart` true for both the pickup grace note and the
landing, and `AscendingClimbMelodyStrategy` short-circuits every such slot to
`ComputeTargetTonicAbove(..., octavesUp: 2)`. The audible result was a grace
note leaping two octaves to the tonic followed by a second cadence computed
from that leap. Scope: `AscendingClimb` base strategy + a pickup SustainLeadIn
phrase on the final chord span. Other strategies ignore the flag, which is why
the fault survived a full session of listening.

**Semantics, now stated.** The field counts SLOTS, not audible notes.
`EvenFlowPhraseSO` counts its rest slots, so SustainLeadIn's silent lead-in
counts too (3, not 2). Every archetype owes three things: a constant
denominator equal to the slot count, dense `0..n-1` indices, and exactly one
slot satisfying the final-slot predicate.

**Determinism note.** The fix CHANGES the melody rng draw sequence on affected
renders only: the slot that used to short-circuit to the cadence now runs the
normal ascending path, which draws from `PickWeightedRandom`. Any golden
pinning procedural melody bytes for an AscendingClimb part must be re-pinned.
`PhraseState.TotalNotesInPhrase` still has no strategy consumer, so no other
path shifts. Rhythm / backing / bass streams are untouched (rng is per track).

Parity checked: `EvenFlowPhraseSO` and `BurstThenHoldPhraseSO` were already
correct and are now pinned.

Test surface: `Tests/Editor/PhraseArchetype_SlotBookkeepingTests.cs`.

**Pitch bend seam (available, NOT consumed).** Since MGP-ALWTTT-BASS-BEND-1 the
package has a shared post-build pitch bend writer, `PitchBendWriter`
(`SSoT_CONTRACTS.md` §11), used by the bass for true legato. The melody
composer is the anticipated second consumer — a slur / legato phrase would use
the same step-gesture surface, and the melody track is already monophonic and
single-channel, which is the writer's stated precondition. **Nothing is
implemented here today**; this note exists so a future melody batch does not
re-derive the seam or write a second bend path.

## 8. Update triggers

- the directive layer changes (§7.2, MGP-MEL-1b): the `.enabled` intent
  contract, the always-draws-a-directive property, the motif buffer semantics
  or its chromatic transpose, the contour snapping rule, or the P3 / P6.2
  signals;
- either post-pool chromatic escape changes (§7.2, MGP-TONALITY-1): a motif
  transpose mode is added or its default flips, the interval directive's
  `pitchClassSource` law moves, or a tonal validation is introduced at the
  emission point;
- the chromatic-motif law changes (§7.2, MGP-MEL-DEGRADE-1): the floor snap
  rule, the no-scale identity degrade, the totality of `ScaleDegrees`, or the
  candidate-pool composition that feeds chromatic notes into the motif at all;
- the motif transposition gains a bound (§7.2, MGP-MEL-CYCLE-1): a cycle cap, a
  wrap, or a register fold — and whether it is shared with the bass's
  D-W2-FLOOR=B must be stated either way;
- **F-SPELL-PROVENANCE-1 is taken up**: any mechanism that carries harmonic
  provenance or pitch spelling to a decision point supersedes the floor
  heuristic and must be recorded here, together with whether the audit's chord
  printing adopts it;
- the archetype phrase surface changes (§4, MGP-TONALITY-1): `endRestFraction`
  and its 25%/(1/8 beat) clamp, `meterFitSlots` / `allowTupletSubdivisions` and
  the duration-not-count constraint, or the recorded `restProbMid` RNG-neutrality
  exception;
- the accidental-awareness of either melody path changes (§7.2, D-TON10) —
  this is the shared contract of `SSoT_CONTRACTS.md` §13, not a melody-local rule;

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

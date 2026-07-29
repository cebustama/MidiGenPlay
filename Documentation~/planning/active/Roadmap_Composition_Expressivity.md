# Roadmap — Composition Expressivity (CE)

> Active MidiGenPlay package planning.
> Successor problem area opened at the close of the LLM Authoring MVP. Where the
> LLM Authoring arc made it possible to *generate* musical material, this arc makes
> each Composition card carry a *distinct, audible identity* and makes authoring
> new cards fast.
> This roadmap separates **what already exists**, **what is next**, and **what
> remains later**, and is grounded in the current codebase.

## Purpose

Give the runtime a way to express different musical identities per Composition
card, and give authors fast, low-friction ways to mint and vary those cards.

The unifying thread: a card's identity should come from **what it draws from**
(a palette) and **how that draw is filtered** (meter/keyword/feel), not from a
genre label or a single hard-wired pattern.

## Current code-backed baseline

### Already true today (entering this arc)

- `DrumPatternPaletteSO` exists with a deterministic, seeded
  `PickRandomPattern(System.Random rng, bool cloneResult)` — weighted walk,
  clone-on-pick, last-valid fallback.
- `ChordProgressionPaletteSO` has the analogous `PickRandomProgression`.
- `BackingCardConfigSO` already consumes its palette at compose time via
  `PickProgressionOverride(rng, ts, settings, verbose)` — a **TS-aware** overload
  with Tier A (exact-TS) / Tier B (heuristic rank) / Tier C (raw weights) logic,
  plus reflection-based candidate extraction.
- **PCE (this arc's opening phase)** wired the rhythm side to match: a
  `patternPalette` field + `PickPatternOverride(System.Random rng)` on
  `RhythmCardConfigSO`, consumed in `RhythmTrackComposer.Compose` seeded from
  `ctx.rng`. Legacy (non-TS) picker only.
- A mature, twice-mirrored LLM authoring stack: `ILLMClient` + `FakeLLMClient`
  + per-domain `…LLMPromptBuilder` / `…LLMGenerator` / `…LLMResponseHandler` /
  `…LLMFieldPlan` for both drums and chords. The window stays a thin applier;
  a pure, unit-testable field-plan decides what to set.
- A `CardEditorWindow` with Create Card (wizard → `CardAssetFactory.TryCreateCard`)
  and Sync From Assets. Style bundles are created/assigned per role via
  `CreateAndAssignStyleBundle`. As of CE-E1 the Card Editor has a **Clone Card**
  affordance (deep-copies the payload and clones the style bundle); palette fields
  are still edited on the `RhythmCardConfigSO`/`BackingCardConfigSO` inspector,
  not in the Card Editor.

### Important corrections to earlier planning language

- The kickoff brief described the palette TS-toggle as inert on **both** sides;
  that was wrong (the **chord** toggle was always live). **Resolved in CE-F1:**
  both palettes now select through one shared `PaletteSelector`, so the toggle
  (`preferExact*TimeSignatureMatches`) is **live on both** sides. The asymmetry
  no longer exists.
- The `Palette_Card_Identity_Design.md` §9 asset path
  (`Patterns/Drums/Palettes/`) is wrong; palette assets live at
  `Resources/ScriptableObjects/Drums/Palettes/`.

## Milestone sequencing

1. **PCE** — palette consumption wiring + the §5 distinctness experiment
   (two 4/4 cards). **DONE** (2026-06-04; smoke pass green). Governed by
   `Palette_Card_Identity_Design.md`.
2. **CE-E1** — Card Editor ergonomics (Clone + New-Card presets). **DONE** (2026-06-10).
3. **CE-F1** — Pattern/Progression Finder (selector extraction). **DONE** (2026-06-10):
   shared `PaletteSelector` + typed finders; chord & drum both TS-aware; reflection
   removed. Delivered TS-only filtering; meter-family / keyword predicates remain as
   the extension seam for CE-L1.
4. **CE-L1** — LLM card-author. **DONE (2026-06-11).** With this, all scheduled
   CE phases are closed; see "What remains later" for unscheduled follow-ons.

---

## CE-E1 — Card Editor ergonomics: Clone + New-Card presets

> **Status: DONE (2026-06-10).** Clone Card + New-Card presets shipped in
> `CardEditorWindow` (ALWTTT). Clone clones the style bundle (fixes the Ctrl+D
> shared-bundle bug); presets pre-fill the create wizard per role.

### Goal
Make minting and varying cards fast, so new ideas can be tried "with default"
without manual asset surgery.

### Why now
Cloning a card by hand (Project window Ctrl+D) duplicates the card but leaves it
pointing at the **same** style-bundle asset — editing the clone's palette then
silently mutates the original. The editor should own the safe path.

### Scope
- **Clone Card button.** Duplicate the selected card asset, deep-copy its
  `CompositionCardPayload`, **clone the referenced style bundle** (the bit manual
  Ctrl+D gets wrong), and repoint the clone at its own bundle. New id/displayName
  derived from the source.
- **New-Card presets.** Buttons over the existing wizard: "New Action Card",
  "New Composition Card", and role-specific "New Rhythm / Backing / Melody Card"
  (pre-set `CreateCardKind.Composition` + role, so the typed bundle is
  auto-created via the existing `CreateAndAssignStyleBundle`).

### Out of scope
- No new card/effect model. No changes to `CardAssetFactory` contracts; the
  buttons are presets/wrappers over `TryCreateCard` and the existing duplicate
  semantics.

### Inputs needed at batch open
- `CardEditorWindow.cs` (have), `CardAssetFactory.cs` (needed — to confirm
  `TryCreateCard` request shape and whether a clone helper belongs there vs. the
  window), `CompositionCardPayload` (needed — to know what a deep payload copy
  must cover).

### Definition of Done
- Clone produces a fully independent card (own card asset + own bundle asset);
  editing the clone's palette/override never touches the source.
- New-Card preset buttons create a card with a correctly-typed, auto-assigned
  bundle for the chosen role.
- Card Editor list refreshes (Sync From Assets safe to run).
- No regression to existing Create Card / Sync flows.

---

## CE-F1 — Pattern/Progression Finder (selector extraction)

> **Status: DONE (2026-06-10).** Shared `PaletteSelector` + `ProgressionFinder` /
> `PatternFinder` in `PaletteSelection.cs`; both card configs delegate; reflection
> removed; drum side now TS-aware; TS toggle live on both. TS-only filtering this
> pass; meter-family / keyword predicates remain the documented extension seam.

### Goal
One typed, testable selection path shared by the drum and chord domains, with
TS / meter / keyword filtering, replacing the per-domain ad-hoc logic.

### Why now
The TS-aware selection logic (Tier A exact-TS → Tier B heuristic rank → Tier C
raw weights, plus reflection-based candidate extraction) currently lives ~250
lines deep **inside `BackingCardConfigSO`**. That is selection logic, not card
data; it is not reusable by the drum side; and it is the reason the TS-toggle is
live on one side and inert on the other. Extracting it gives both palettes one
home for selection and filtering.

### Scope
- Extract a typed `PatternFinder` / `ProgressionFinder` (or a shared generic
  selector) from `BackingCardConfigSO`'s TS-aware path. No reflection — typed
  access to palette entries (`pattern`/`progression` + `weight`).
- Filtering predicates: time signature, meter family, keyword/tag, (extensible).
- Consume the `preferExact*TimeSignatureMatches` toggle in exactly **one**
  location for both domains — resolving the live/inert asymmetry.
- Migrate both `BackingCardConfigSO` and `RhythmCardConfigSO` to delegate their
  pick to the Finder. Chord-palette consumption rides on it; the rhythm side
  gains TS-awareness it currently lacks (was deferred from PCE).

### Out of scope
- No change to `PickRandom*` SO-level signatures unless the migration proves it
  necessary; if so, surface as a decision first.
- No new palette authoring; this is selection plumbing.

### Inputs needed at batch open
- `BackingCardConfigSO.cs` (have), `RhythmCardConfigSO.cs` (have),
  `DrumPatternPaletteSO.cs` / `ChordProgressionPaletteSO.cs` (have),
  `MidiGenPlayConfig` (needed — heuristic knobs like `minHarmonicSubdivisions`
  used by the backing TS-aware path), `TrackStyleBundleSO` (needed — base shape).

### Definition of Done
- Both card configs select through the Finder; the reflection path in
  `BackingCardConfigSO` is gone.
- TS-toggle consumed in one place; documented behavior matches code on both
  sides (no asymmetry).
- Unit tests cover Tier A/B/C and each filter, using `FakeLLMClient`-style pure
  inputs (no IMGUI / no live Unity asset DB where avoidable).
- Determinism preserved: same seed + same filter set => same pick.

---

## CE-L1 — LLM card-author

> **Status: DONE (2026-06-11).** Shipped in three sub-batches (B1 DTO hoist +
> vocabulary snapshot + intent resolver + asmdef pair; B2 the CardLLM quartet +
> FakeLLMClient + 49 tests; B3 window panel + Save hook + modifier-name
> resolution). 77/77 editor tests green. Live smoke: one-sentence brief →
> fully-wired `cmp_flow_rhythm` (card + payload + catalog entry + Rhythm bundle
> + palette 'Syncopated Pocket (4/4)' resolved at seed 12345); telemetry
> 1572 in / 366 out tokens (~2.5–3× chord output, within expectation).
>
> **Decisions locked:**
> - **D-CE-L1.1** — quartet lives ALWTTT-side (package cannot reference game types).
> - **D-CE-L1.2** — asset intent via editor-side `CardPaletteIntentResolver`
>   composing over `PaletteSelector.Pick` at palette level (palette = one
>   candidate represented by its best entry); ALL-keywords pre-filter
>   (case-insensitive substring over DisplayName+Notes, hard-fail listing
>   available palettes); ordinal sort by Id before pick; null-TS = single
>   seeded uniform draw.
> - **D-CE-L1.3** — field taxonomy: LLM-fillable structured fields;
>   intent-resolved (statusKey via registries, palette intent,
>   modifierEffectNames exact-match); BANNED from LLM (cardSpritePath,
>   trackAction.styleBundle, modifierEffects paths, statusActions,
>   action.actions/conditions).
> - **D-CE-L1.4** — vocabulary = live-snapshot string POCO, not a hand-authored
>   SO (documented as the §7 deviation in the LLM SSoT).
> - **D-CE-L1.5** — output = one fenced JSON object against the extended
>   CardJsonImport schema; Generate/Import unify on the shared parser + the
>   window's `TryStageCardFromDto`; banned-ref guard applies to the LLM-panel
>   route only (legacy JSON box keeps behavior).
> - **D-CE-L1.6** — bundle + palette written at the existing Save step only.
> - **D-CE-L1.7** — minimal asmdef pair `ALWTTT.Cards.LLMAuthoring`(+`.Tests`)
>   because test asmdefs cannot reference predefined assemblies; full ALWTTT
>   asmdef-ification deferred to its own batch.
>
> **Determinism note (DoD interpretation):** the LLM response itself is
> non-deterministic (covered by the asset boundary); everything downstream of
> the raw response is pure — same payload + vocabulary + intent seed ⇒ same
> outcome including the palette pick.

### Goal
Author a Composition card from a natural-language brief, e.g. "a Rhythm card that
adds 2 Flow and draws 2 cards, with a random 6/8 palette."

### Why this shape
The existing LLM stack is the template: a fourth mirror of the drum/chord trio —
`CardLLMPromptBuilder` / `CardLLMGenerator` / `CardLLMResponseHandler` /
`CardLLMFieldPlan`. The window stays a thin applier; the field-plan is pure and
unit-tested; output routes through the existing card apply path. Structured card
fields (inspiration cost/gen, rarity, card type, keywords, effects like
`ApplyStatus: Flow stacksDelta 2`, modifier effects) map directly onto what
`ChordLLMFieldPlan` already does for chords — just more fields.

### The hard boundary (why CE-F1 is a prerequisite)
Every existing LLM tool outputs **data** (a Roman string, a DSL glyph string)
that gets parsed into the asset. A card additionally references **other assets**
(a palette, a sprite, a `MeterEffect`/`TempoEffect`). An LLM cannot emit an asset
reference — only a name/intent ("random 6/8 palette"). Resolving that intent to a
real project asset is exactly the CE-F1 Finder's job. Without the Finder the LLM
would guess asset names it cannot see and get them wrong. Hence CE-L1 follows
CE-F1.

### Scope
- `CardLLM*` quartet mirroring the chord trio + field-plan.
- Structured-field generation: numeric/enum/keyword/effect fields applied via a
  pure `CardLLMFieldPlan` through the existing card apply path.
- Asset-intent fields (palette by meter/keyword, effect by type) resolved through
  the CE-F1 Finder, never by raw LLM-emitted names.
- An LLM panel in `CardEditorWindow`, mirroring `ChordProgressionEditorWindow_LLM`.

### Out of scope
- No new effect types; the LLM selects among existing effects/modifiers.
- No autonomous asset creation beyond the card + its bundle (palettes/effects
  must pre-exist and be Finder-resolvable).

### Inputs needed at batch open
- `ChordProgressionEditorWindow_LLM.cs` (have — wiring template),
  `ChordProgressionLLMGenerator/PromptBuilder/ResponseHandler.cs` (have —
  structural template), `CardAssetFactory.cs`, `CompositionCardPayload` + effect
  classes (`ApplyStatus`, `MeterEffect`, `TempoEffect`) (needed — to enumerate
  LLM-fillable vs. asset-ref fields), the CE-F1 Finder (prerequisite).

### Definition of Done
- A natural-language brief produces a valid, fully-wired card whose asset refs
  are all Finder-resolved (no dangling/guessed references).
- Pure field-plan unit-tested with `FakeLLMClient`; no live call needed in tests.
- Generated card applies through the same path as a hand-authored one.
- Determinism: same seed/brief => same structured fields (asset picks seeded
  through the Finder).

---

## B2 — TONFILTER-1 — CLOSED (2026-07-27)

**The only batch of the currently agreed sequence with a real impact radius: it
removes a `ctx.rng` draw wherever the filter fires today, so any render that
hits it changes.** Everything else in the sequence (B1, B3's opt-ins) is inert
by default; this one is not, and must be scheduled on its own.

**Finding F-TONFILTER-REVERT (recorded 2026-07-26 during RUNTIME-REQUALITY).**
`ChordProgressionData.tonalities`, the per-asset allowed-tonalities list, has no
consumers in cards or authoring assets — yet it can REVERT a part's tonality
(`runtime/SSoT_Composer_Backing_Track.md` §2.2) and spends an rng draw doing so.
That is a veto by a field nobody authors, over a decision the card made.

Decision to resolve first — the whole batch turns on it:
- **(A) Demote veto → selection weight.** The filter becomes one more term in the
  palette selection, alongside the TS tiers already handled by the shared
  `PaletteSelector`. Principle: **the card decides; the material adapts.**
  Now cheaper than it was, because RUNTIME-REQUALITY gives a progression a
  supported way to sound correct in a foreign tonality instead of being vetoed
  out of it.
- **(B) Deprecate `tonalities` outright**, if the "no consumers" reading is
  confirmed across the package AND the consumer side. Cheapest, and removes the
  draw entirely.

Either way the batch also owes a **conflict signal**: today an asset that is
unrenderable in the active mode fails silently through the filter. Selection
should say so.

Interaction to check before opening: the shared `PaletteSelector` consumes
exactly one `rng.NextDouble()` per pick (the determinism invariant recorded
above). Removing or relocating the tonality draw must not disturb that count, and
the SEED-1 goldens are the detector.

**Outcome (D-B2-1=C, D-B2-2=B, D-B2-3=A).** Neither (A) nor (B) as posed: the
empirical reading corrected the finding — the runtime importer IS a contracted
writer of `tonalities`, and the 2b revert nullified RUNTIME-REQUALITY in
exactly the case REQUALITY exists to solve. The revert and its draw were
removed; the field stays as descriptive metadata; the conflict signal lives in
the readback plus a gated log; `PaletteSelector` is untouched (the 1-draw
invariant stands); the legacy `PickTemplateForPart` is unchanged. Real impact
radius: only renders with a tonality mismatch — everything else byte-identical
(pinned by tests plus a parity smoke).

## What remains later (not yet scheduled)

- Seed library expansion: Half-Time Heavy, Latin Clave Engine, Breakbeat Amen
  Dense (held in reserve in `Palette_Card_Identity_Design.md`).
- Chord palette authoring along the §8 axis set (modal vamps /
  functional-cadential / chromatic-borrowed / static drones) — deferred from PCE
  until palette-as-identity is proven and CE-F1 lands.
- Within-palette variety tuning (multiple weighted entries per palette) once the
  single-entry distinctness proof passes.
- Melody/Harmony palette types: CE-L1's palette intent covers Rhythm/Backing
  only because no Melody/Harmony palette types exist; when those roles get real
  bundles+palettes, extend `CardPaletteIntentResolver`'s role map.
- "Copy prompt to clipboard" affordance on all three LLM panels (drum, chord,
  card) — the prompt is deterministic and reproducible but not currently
  surfaced in any UI.
- Palette `keywords` as a first-class schema field if substring matching over
  DisplayName+Notes proves too loose in practice.
- D-L4.3 generic unification — now three adopters exist (drum, chord, card);
  note the card instance's builder is schema-shaped rather than DSL-shaped, so
  the generic may cover generator/handler scaffolding more naturally than the
  prompt builders.

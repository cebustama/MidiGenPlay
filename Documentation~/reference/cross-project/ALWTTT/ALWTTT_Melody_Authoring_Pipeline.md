> CROSS-PROJECT REFERENCE — preserved for ALWTTT integration context. This file is not primary MidiGenPlay package authority.

# ALWTTT · Melody Authoring Pipeline (Concise Guide)

This document summarizes the melody authoring pipeline now that **Phrase Palettes + Archetypes**,
**Melodic Leading**, **Melodic Style**, and the optional **Card bundle** are in place.

> Namespaces below are indicative. `MelodyCardConfigSO` lives in **ALWTTT** code, while the rest are
> part of **MidiGenPlay**.

---

## High‑level flow

1) **MelodyTrackComposer** asks **PhrasePlanner** to create *phrase slots* for each chord span.  
2) For each slot, the composer picks a **melodic strategy** (base or overridden) and asks it for the next **note**.  
3) Slots carry phrasing signals (accent, phrase‑end, desired contour). Composer converts the chosen note + slot to MIDI events with expression ranges from **MelodicLeadingConfig**.

Overrides come from the **Card bundle** (`MelodyCardConfigSO`) which can swap the palette and/or the leading config and provide a **MelodicStyleSO** to select/modify strategies per phrase.

---

## Components

### PhrasePaletteSO (MidiGenPlay.Composition.Phrases)
**What:** A weighted list of `PhraseArchetypeSO` (your “phrase vocabulary”).  
**Inputs:** none at runtime (selected by `PhrasePlanner`).  
**Outputs:** One archetype chosen per phrase (weighted).  
**Notes:** Has `defaultContourBias` (−1/0/+1) and future `allowCrossChordPhrases` flag.

### PhraseArchetypeSO (MidiGenPlay.Composition)
**What:** ScriptableObject that **builds phrase slots** for a chord span.  
**Build inputs:** `(startBeat, spanBeats, beatsPerBar, phraseId, contourDir, rng, TonalityProfileSO, MelodicLeadingConfig)`  
**Output:** `List<PhrasePlanner.PhraseSlot>` containing timing, rests, accents, `isPhraseEnd`, `desiredContourDir`, etc.  
**Examples:** `EvenFlow`, `BurstThenHold`, `SustainLeadIn`.

### MelodicLeadingConfig (MidiGenPlay.Composition)
**What:** “Personality + expression” for melody generation.  
**Key fields:** note source policy, motion constraints (`maxStepSemitones`, `chanceRepeatNote`), **velocity ranges**, and **default `PhrasePaletteSO`**.  
**Used by:** strategies (for pitch policy), composer (for velocities), planner (for default palette).  
**Overridable:** by `MelodyCardConfigSO.leadingOverride` and/or `MelodyCardConfigSO.phrasePaletteOverride`.

### MelodicStyleSO (MidiGenPlay.Composition)
**What:** Selects the **base melodic strategy** and optional **per‑phrase directives**.  
**Fields:**
- `baseStrategy` (`MelodyStrategyId`)  
- `usePerPhraseOverrides` and `perPhraseDirectives` (weighted): each directive can
  - override the strategy for that phrase,
  - impose a `ContourConstraint` (Ascending/Descending/None),
  - optionally repeat the last motif (`RepeatLastNotesDirective`).

**Used by:** `MelodyTrackComposer` to select/decorate the strategy per phrase.

### ConstrainedMelodyStrategy (MidiGenPlay.Composition)
**What:** A thin **decorator** over any `IMelodyStrategy`. Applies:
- Optional motif repetition (`RepeatLastNotesDirective`).
- Optional **contour constraint** (nudges result up/down to respect Asc/Desc).

**Inputs:** same as `IMelodyStrategy.PickNext(...)`, plus the chosen directive.  
**Output:** final note (or `null` for rest).

### MelodyCardConfigSO (ALWTTT.Cards)
**What:** A card‑level authoring bundle to **override defaults** per track instance.  
**Fields:**
- `leadingOverride` (optional) – replaces the composer’s default `MelodicLeadingConfig`
- `phrasePaletteOverride` (optional) – wins over leading’s default palette
- `style` (optional) – provides base strategy + per‑phrase directives

**Used by:** gameplay code to set `TrackConfig.Parameters.Style` so the composer can read it.

### MelodyTrackComposer (MidiGenPlay.Composition)
**Role:** Orchestrates everything for melody:
- Builds chord‑span **phrase slots** via `PhrasePlanner` (now palette‑driven).
- For each phrase, computes **effective leading** and selects **strategy**:
  - Start with `_baseStrategy` (constructor or default).  
  - If `MelodicStyleSO` is present, create strategy from `baseStrategy` and, if per‑phrase overrides are enabled, apply a **weighted directive**; then wrap with `ConstrainedMelodyStrategy` if needed.  
- Converts slots → MIDI using **expression ranges** from the effective leading config.

**Inputs:** `SongConfig.PartConfig`, `TrackConfig`, `ChordProgressionData`, `TonalityProfileSO`, instrument, RNG.  
**Outputs:** `MidiFile` + (optional) cached “guide notes” in context.

---

## Override precedence (effective configuration)

1. Start with the composer’s default `MelodicLeadingConfig` (scene/track default).  
2. If `MelodyCardConfigSO.leadingOverride` exists → use it.  
3. If `MelodyCardConfigSO.phrasePaletteOverride` exists → set it on the **effective** leading (do not mutate assets; clone if needed).  
4. Strategy for a phrase:
   - `MelodicStyleSO.baseStrategy` → base
   - If `usePerPhraseOverrides` → pick directive (weighted):
       - If directive has `overrideStrategy` → replace base
       - Wrap with `ConstrainedMelodyStrategy` if directive sets `contour` and/or `repeat`

This keeps **phrasing** (timing/shape) in **Archetypes/Palettes** and **pitch policy** in **Strategies/Style**.

---

## Typical usage

- **Default authoring**: Assign a `MelodicLeadingConfig` (with default `PhrasePaletteSO`) to the scene/track.  
- **Per‑card variation**: Author a `MelodyCardConfigSO` with
  - only a `phrasePaletteOverride` (to swap the phrase vocabulary),
  - and/or only a `style` (to change pitch strategy/contour behavior),
  - or a full `leadingOverride` (a different “personality” + expression).
- In gameplay, attach the card bundle to `TrackConfig.Parameters.Style` for the track instance created by the card.

---

## Inputs/Outputs per component (quick reference)

- **PhraseArchetypeSO.Build(...)** → *slots*  
  Inputs: time span, contour hint, beatsPerBar, rng, tonality, leading.  
  Output: `List<PhraseSlot>`

- **IMelodyStrategy.PickNext(...)** → *note*  
  Inputs: chord/scale pitch classes, degree map, last note, instrument, **MelodicLeadingConfig**, rng, **PhraseState**, **TonalityProfileSO**, **MelodyPartState**.  
  Output: `Note` or `null`

- **ConstrainedMelodyStrategy** (decorator)  
  Same inputs/outputs as strategy, plus internal directive (`contour`, `repeat`).

- **MelodyTrackComposer**  
  Inputs: Part/Track/Progression/Instrument/Tonality/RNG …  
  Output: `MidiFile`

---

## What can be overridden and how?

- **Phrase vocabulary** (EvenFlow/Burst/Sustain mix): set in `MelodicLeadingConfig.phrasePalette` (default) or **override per card** with `MelodyCardConfigSO.phrasePaletteOverride`.
- **Melodic “taste”** (note source policy, step size, velocities): the **leading config**, default or `leadingOverride` from the card.
- **Pitch policy per phrase**: `MelodicStyleSO` (base strategy) + weighted `perPhraseDirectives` (strategy override & contour/motif constraints).

---

## Minimal code hooks to read card overrides

- In gameplay: `TrackConfig.Parameters.Style = yourMelodyCardConfigSO`.  
- In composer:
  - Resolve **effective leading** (default → `leadingOverride` → set `phrasePaletteOverride` if provided).  
  - Recreate `PhrasePlanner` with the effective leading.  
  - If `style` exists, use its `baseStrategy` and per‑phrase directives; wrap with `ConstrainedMelodyStrategy` accordingly.

---

## Notes on responsibilities

- **Archetypes** decide **when** to put notes, rests, accents, and how phrases end (timing/shape).  
- **Strategies** decide **what pitch** to play (note choice policy).  
- **Style** switches/augments strategies **per phrase** (e.g., Ascending contour this phrase, repeat motif next phrase).  
- **Leading** provides global taste and expression (velocity ranges, step limits, note source) and holds the **default palette**.

---

## Future extensions (non‑breaking)

- Style‑level swing/humanize applied at MIDI emission stage.  
- Cadence targets (“aim for tonic in 2 slots”).  
- Cross‑chord archetypes once allowed by `allowCrossChordPhrases`.

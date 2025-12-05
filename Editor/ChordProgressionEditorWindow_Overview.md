# Chord Progression Editor Window – Technical Overview (v3)

_Last updated: 2025‑12‑05_

This document describes the current state of the **Chord Progression authoring pipeline** used in **ALWTTT / MidiGenPlay**, focusing on:

- `ChordProgressionData` (progression assets)
- `ChordProgressionPaletteSO` (progression “packs” for cards)
- `ChordProgressionEditorWindow` (authoring tool)
- Supporting services: `RomanProgressionParser`, `RhythmGridQuantizer`, `ChordQualityResolver`
- Runtime use in `BackingCardConfigSO` and the chord track composer

The goals of the system are:

- Keep the pipeline **SOLID, data‑driven and re‑usable**.
- Make it easy for non‑programmer composers to author meaningful progressions.
- Support both **Roman‑string** and **Grid** workflows, including **rests/silent spans**.
- Provide palette‑based variation that is simple to consume from cards.


---

## 1. High‑level mental model

1. Designers/composers author **Chord Progressions** as `ChordProgressionData` assets.
2. These assets can be:
   - Created from a **Roman string** (e.g. `I – V – vi – IV`, with optional durations and rests).
   - Authored/edited in a **Grid** UI (bars, beats, subdivisions, colored chord blocks, gaps for rests).
3. The same progression assets are grouped into **Palettes** (`ChordProgressionPaletteSO`) representing themed packs (e.g. “Major 4/4 Pop”, “Minor Waltzes 3/4”, “Mixolydian Vamps”).  
4. A **Backing card** (`BackingCardConfigSO`) may:
   - Force a specific progression (`progressionOverride`), or
   - Use a **palette** and let the system pick one variation per song via weights, or
   - Fall back to library/procedural generation if no override/palette is defined.
5. At runtime, the **ChordTrackComposer** turns the chosen progression into **MIDI chord notes**, which then feed into the rest of the music system.

The editor window is a **front‑end** over that data model: it never stores logic‑specific state except for authoring parameters (input strings, grid config, preview settings).


---

## 2. Data model & dependencies

### 2.1 `ChordProgressionData`

Located in `ChordProgressionData.cs`.

Represents a **single chord progression template**.

Key fields (simplified):

- **Meter & grid**
  - `TimeSignature TimeSignature` – musical meter (FourFour, ThreeFour, etc.).
  - `int Measures` – number of bars.
  - `int subdivisions` – timing resolution (steps per beat).  
    Total steps = `Measures * beatsPerMeasure * subdivisions`.

- **Chord events**
  - Nested `ChordEvent` class with:
    - `int startStep`
    - `int lengthSteps`
    - `ScaleDegree degree` (I, ii, V, etc.)
    - `ChordQuality quality` (Major, Minor, Dim, Maj7, m7, etc.)
    - `int velocity`
    - `bool isDiatonic` (whether this chord fits the reference mode by default)
  - `List<ChordEvent> events` – the sequence of chord blocks across the grid.
  - **Rests / silent spans** are *not* stored as events; they are represented implicitly as gaps between chord events in the grid.

- **Tonality constraints**
  - `List<Tonality> tonalities` – modes where this progression is considered “compatible” (e.g. Ionian, Mixolydian, Aeolian). Used by runtime systems to filter candidate progressions.

- **Authoring metadata**
  - `string originalInput` – Roman string used to create/last update the asset; used for debugging, display and uniqueness keys.
  - `string DisplayName` (with `UpdateDisplayNameAuto()`) – human‑friendly label derived from the Roman string and/or asset name.
  - `List<string> songReferences` – optional per‑progression list of song titles/notes (e.g. “Similar to [X] verse”, “Blues rock turnaround”). Purely authoring‑side for now.

This asset intentionally contains **no** playback‑specific info (no MIDI channels, voicings, etc.). It is a pure **harmonic grid description**.


---

### 2.2 `ChordProgressionPaletteSO`

Located in `ChordProgressionPaletteSO.cs`.

Represents a **themed pack** of chord progressions plus weights, intended for **per‑card overrides**.

Structure:

- `string paletteDisplayName` – optional human label; falls back to asset name.
- `string paletteNotes` – free text: usage hints, feel, genre (“Usable for rock’n’roll, blues and metal”, etc.).
- `List<WeightedEntry> entries`:
  - `ChordProgressionData progression`
  - `float weight` – relative weight when randomly picking.

Core method:

```csharp
public ChordProgressionData PickRandomProgression(System.Random rng, bool cloneResult = true)
```

- Filters out null/zero‑weight entries.
- Performs a **weighted random selection**.
- By default returns a **cloned instance** via `ScriptableObject.Instantiate` so runtime modifications never touch the original progression assets.

Palettes are the main way for designers to say “pick any of these few related progressions” without worrying about the global library.


---

### 2.3 `ChordProgressionLibrarySO` (global pool – future‑facing)

Located in `ChordProgressionLibrarySO.cs`.

Represents a **global library** of all canonical progressions, with:

- `List<Entry> entries`, where each `Entry` has:
  - `string id`
  - `ChordProgressionData progression`
  - `float weight`
  - `List<Tonality> compatibleTonalities`

The current editor window **does not write directly into the library**. The plan is for the library to be used by:

- High‑level systems that need “any suitable progression for this context”.
- Offline tools that batch‑generate or analyze content.

For now, card‑level authoring uses `ChordProgressionPaletteSO`, while the library lives as a higher‑level, curated collection.


---

## 3. `ChordProgressionEditorWindow` responsibilities

Located in `ChordProgressionEditorWindow.cs`.

### 3.1 Overview

The window is opened via:

```csharp
[MenuItem("MidiGenPlay/Chord Progression Editor...")]
public static void Open()
{
    GetWindow<ChordProgressionEditorWindow>("Chord Progression Editor");
}
```

It exposes two main input modes:

- **Roman** (`InputMode.RomanString`)
- **Grid** (`InputMode.Grid`)

Core responsibilities:

1. **Roman mode**
   - Let the user type a Roman string (`progressionInput`).
   - Parse it with `RomanProgressionParser` into logical chords and rests.
   - Quantize durations with `RhythmGridQuantizer` into a consistent grid.
   - Use tonality flags and `AutoDiatonicMode` to decide chord qualities.
   - Write results into a `ChordProgressionData` asset (new or existing).
   - Update preview (text line + colored grid).

2. **Grid mode**
   - Allow direct editing of a chord grid:
     - `gridMeasures`, `gridBeatsPerMeasure`, `gridSubdivisions`.
     - Clickable lane of colored blocks per chord event.
     - Empty regions between events represent **rests**.
     - Selection panel with degree, quality, velocity, etc.
   - Apply grid back into the target asset.
   - Derive a Roman string from the grid (`BuildRomanStringFromGrid`) for metadata and preview, including explicit rest tokens for gaps.

3. **Metadata authoring**
   - Time signature selection (`timeSignature`).
   - Tonality flags (`tonalityFlags`) which are mirrored into `ChordProgressionData.tonalities`.
   - `AutoDiatonicMode` setting controlling how empty suffixes are interpreted.
   - `previewRoot` used only for naming preview chords (Cmaj7, G7, etc.).
   - **Song references** editor (`DrawSongReferencesSection()`):
     - Simple list UI allowing designers to add/remove/edit strings stored in `ChordProgressionData.songReferences`.

4. **Palette integration**
   - `targetPalette` object field (“Progression Palette (optional)”).
   - “**Add Current To Palette**” button which appends the current `targetAsset` to the palette as a weighted entry (weight=1 by default), avoiding duplicates.

5. **Preview**
   - Maintains a cached preview:
     - Linear text (Roman → concrete chord symbols, including `Rest` tokens).
     - Multi‑bar text grid with per‑root color coding and explicit **grey/italic “Rest” markers**.
   - Scrollable area so long progressions don’t overlap the action buttons.

The window itself is a **thin orchestrator**. Almost all domain logic lives in service classes (`RomanProgressionParser`, `RhythmGridQuantizer`, `ChordQualityResolver`) and data types (`ChordProgressionData`, `ChordProgressionPaletteSO`).


---

### 3.2 Main user flows

#### A. Create/edit via Roman mode

1. Choose `inputMode = Roman` and a `TimeSignature` (e.g. FourFour, ThreeFour).
2. Write a string like:

   ```text
   Imaj7 (0.5) – S (0.5) – IIm7 (1) – (0.5) – IIIm (0.5) – Imaj7 (0.5)
   ```

   Supported **rest syntaxes**:

   - `S (0.5)`, `s (0.5)`, `Rest (0.5)` or `R (0.5)` – explicit rest token.
   - A bare duration like `(0.5)` – duration with no Roman part also treated as a rest.

3. Set `Default Duration (measures)` and `Default Velocity` if desired.
4. Configure:
   - `Reference Tonality` (e.g. Ionian, Dorian…).
   - `Auto Diatonic Qualities`:
     - **None** – treat Roman case literally as triad quality; key is ignored for quality inference.
     - **Triads** – infer triad quality from mode/degree if no explicit suffix.
     - **Sevenths** – infer 7th chords likewise.
   - Toggle allowed tonalities in the “Allowed Tonalities” foldout.
5. Press **“Parse & Preview (no write)”** to:
   - Parse via `RomanProgressionParser` (including rests).
   - Quantize via `RhythmGridQuantizer` (picking subdivisions & total steps).
   - Build the preview line and colored grid with `UpdatePreview()`:
     - Chords appear as colored symbols.
     - Rests appear as **grey italic “Rest”** on the first beat of each silent span.
6. When satisfied, press **“Apply To Target Asset”**:
   - If `targetAsset` is null, the window prompts for a new asset path.
   - Writes:
     - Time signature, measures, subdivisions.
     - Event list (degree, quality, start, length, velocity, isDiatonic).
     - `originalInput` and `DisplayName`.
   - `tonalities` from current flags.
   - **Rest items do not become events**; they just advance the internal step cursor so chords end up at the correct grid positions.
   - Refreshes grid preview and keeps the window synchronized.
7. Optionally press **“Save As New Asset”** to create a separate asset from the current state, without touching the existing one.
8. Optionally, assign a `ChordProgressionPaletteSO` and press **“Add Current To Palette”** to register the progression in that palette for use by cards.


#### B. Author via Grid mode

1. Assign a `targetAsset` (the grid editor always reflects an existing progression asset).
2. Switch `inputMode = Grid`.
3. Adjust grid parameters:
   - `Measures`
   - `Beats Per Measure`
   - `Subdivisions (steps per beat)`
   - You can also use the **“Clear Grid”** button in this section to wipe all current grid events (editor‑side only, until you Apply/Save).
4. Interact with the chord lane:
   - Clicking empty space creates a new `ChordEvent` at that step.
   - Clicking an existing block selects it; parameters can be edited (degree, quality, length, velocity).
   - Blocks are colored by pitch class (via `ColorHexForNote`), and non‑diatonic chords are shown with darker shades / italics.
   - **Gaps between blocks represent rests**.
5. When done, press **“Apply To Target Asset”** in Grid mode:
   - Calls `ApplyGridToTarget()`.
   - Cleans/clamps events to current grid size.
   - Copies timing, tonalities and events into `ChordProgressionData`.
   - Derives a Roman string from the grid and stores it in `originalInput` + UI:
     - Gaps between events become rest tokens (e.g. `S (0.5)`).
     - A trailing gap at the end is also turned into a final rest so the total duration matches `Measures`.
   - Calls `ParseAndPreview(onlyPreview: true)` to rebuild the Roman‑based preview.
6. “Save As New Asset” can also be used from Grid mode to create a new progression asset from the current grid (including tonalities and derived Roman string).


---

## 4. Supporting services

### 4.1 `RomanProgressionParser`

Located in `RomanProgressionParser.cs`.

Responsibilities:

- Parse the textual Roman progression into a sequence of `ParsedChord` items, each containing:
  - `ScaleDegree degree`
  - `ChordQuality? explicitQuality` (from suffixes like `maj7`, `dim`, `m7b5`)
  - `float durationMeasures`
  - `bool isRest` – **new flag** indicating that this item is a rest/silent span.
- Understands:
  - Roman numerals with upper/lower case degrees.
  - Optional quality suffixes (`maj7`, `min7`, `dim`, `sus4`, etc. – up to your current implementation).
  - Optional duration suffixes in parentheses: `(0.5)`, `(2)` etc.
  - Rest syntaxes: `S`, `s`, `Rest`, `R`, and bare durations with no Roman part.
- Works in conjunction with:
  - `AutoDiatonicMode` to determine whether to interpret case literally or use mode‑based diatonic qualities.
  - `ChordQualityResolver` to map `(Tonality, ScaleDegree, AutoDiatonicMode, explicitQuality)` to final triad/7th chord quality.
- Returns informative parse errors used by the editor to show dialogs.

The editor never manually parses or infers degrees/qualities; it always goes through this service.


---

### 4.2 `RhythmGridQuantizer`

Located in `RhythmGridQuantizer.cs`.

Given:

- A list of `ParsedChord` with `durationMeasures` (including rest items).
- `beatsPerMeasure` from the chosen `TimeSignature`.

It:

1. Finds a **subdivisions** value such that all durations map cleanly to integer **steps**.
2. Outputs:
   - `int subdivisions`
   - `List<int> lengthsSteps`
   - `int totalSteps`
3. This defines a consistent grid so each item (chord or rest) can be represented as a span of `(startStep, lengthSteps)`.

The editor uses this for both:

- Creating new assets from Roman mode.
- Computing preview layout (how many bars, how many cells per bar, etc.).


---

### 4.3 `ChordQualityResolver`

Located in `MusicTheory.ChordQualityResolver.cs`.

Encapsulates the logic for turning mode/degree/flags into a final `ChordQuality` and “is diatonic?” information. Typical use:

- For each parsed chord:
  - If `isRest` is true → skip quality resolution (rests never become events).
  - Else, if explicit quality is present → trust it.
  - Otherwise, when `AutoDiatonicMode` is Triads/Sevenths, look up the diatonic triad/7th for `(Tonality, ScaleDegree)`.
  - Mark chords as diatonic/non‑diatonic accordingly.

The preview code then can display:

- **Diatonic** chords in normal style.
- **Borrowed / non‑diatonic** chords differently (e.g. italics, darker color).

This separation makes it easier to reuse harmonic logic in other systems (e.g. melody generation, reharmonization tools).


---

## 5. Palette and card integration

### 5.1 `BackingCardConfigSO`

Located in `BackingCardConfigSO.cs`.

Extends `TrackStyleBundleSO` with harmonic overrides for Backing tracks:

- `ChordProgressionData progressionOverride` – explicit progression for this card.
- `ChordProgressionPaletteSO progressionPalette` – palette used if `progressionOverride` is null.

Key method:

```csharp
public ChordProgressionData PickProgressionOverride(System.Random rng)
```

Priority order:

1. If `progressionOverride` is set → clone and return it.
2. Else if `progressionPalette` is set → call `PickRandomProgression(rng, cloneResult: true)`.
3. Else → return `null` and let the composer use library/procedural generation.

This means palette assets are the **main way for card designers to say “pick any of these few related progressions”**.


### 5.2 Chord track composer (runtime)

In `ChordTrackComposer.cs`, the typical flow is:

1. Ask the active card’s `BackingCardConfigSO` for a progression via `PickProgressionOverride`.
2. If null, fall back to library/procedural generation as before.
3. Use `ChordProgressionData`:
   - Iterate over `events`.
   - For each chord event, compute concrete MIDI notes from `(degree, quality)` and the current song key (via `MusicTheory` utilities).
   - Schedule chords into the MIDI pattern using `startStep` / `lengthSteps`, scaled into beats/seconds according to tempo and subdivisions.

The important part: **the composer is agnostic** about how the progression was authored (Roman or Grid). Rests are simply regions where there are no events, so no notes are emitted.


---

## 6. Song references metadata

The `songReferences` list in `ChordProgressionData` is exposed through `ChordProgressionEditorWindow.DrawSongReferencesSection()`:

- Designers can maintain a small list of free‑form strings, such as:
  - “Inspired by [band] – [song] chorus”
  - “Classic ii–V–I turnaround (jazz)”
  - “Pop pre‑chorus, mid‑tempo”
- This field is **optional** and has no runtime effect for now.
- It serves as **documentation** for other designers and as a bridge to listening references when assigning progressions to palettes or cards.


---

## 7. Latest changes (this work session)

Relative to the previous version of this document:

1. **Support for rests / silent spans**
   - `RomanProgressionParser` now recognizes explicit rest tokens (`S`, `Rest`, `R`) and bare durations with no Roman part.
   - Parsed items now carry a `bool isRest` flag.
   - `RhythmGridQuantizer` is rest‑agnostic and uses only durations; rests contribute to the total grid length.
   - `ApplyToAsset` and `SaveAsNewAsset` in Roman mode **skip** creating `ChordEvent`s for rest items, but still advance the step cursor, so timing is correct.
   - `ChordTrackComposer` simply sees gaps between `ChordEvent`s and produces silence during those spans.

2. **Roman ↔ Grid rest round‑trip**
   - `BuildRomanStringFromGrid` now inserts rest tokens (`S (x)`) for gaps between grid events and for trailing space at the end of the progression.
   - `Parse & Preview` in Grid mode converts the grid to Roman, then uses the same Roman pipeline to quantize and preview, so round‑tripping is consistent.

3. **Preview styling for rests**
   - The linear preview line now includes `Rest` entries with their durations.
   - The per‑beat grid preview shows the first beat of each rest span as **grey italic “Rest”**, making silent areas visually obvious.

4. **Clear Grid button**
   - A **“Clear Grid”** button was added under the Grid Parameters section.
   - It clears the editor‑side `gridEvents` list (with a confirmation dialog) without modifying the underlying asset until the user presses “Apply” or “Save As New Asset”.

These changes make it much easier to author progressions that include **space and silence**, both from Roman strings and from the grid, while keeping the runtime representation clean and compatible with the existing chord track composer.

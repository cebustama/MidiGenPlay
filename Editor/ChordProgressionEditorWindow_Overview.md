# ChordProgressionEditorWindow – Technical Overview

_This document summarizes the current `ChordProgressionEditorWindow` implementation, its
responsibilities, its key methods, and how it interacts with other MIDI Gen Play systems.
It’s intended as a reference for a future refactor into smaller, SOLID‑friendly pieces._

---

## 1. Purpose of the Window

`ChordProgressionEditorWindow` is a Unity editor tool that lets a designer author
`ChordProgressionData` ScriptableObjects in two complementary ways:

1. **Roman String Mode (“Roman”)**  
   Type a Roman‑numeral progression such as  
   `I – V – vi – IV` or `i (2) – iv (1) – v (1)` and let the tool:
   - parse degrees, chord qualities and durations,
   - quantize durations to a rhythmic grid (measures, beats, subdivisions),
   - fill the `ChordProgressionData.events` list with step‑based `ChordEvent`s,
   - set allowed tonalities and other metadata,
   - generate a concrete‑chord preview in a chosen key.

2. **Grid Mode (“Grid”)**  
   Edit a one‑row “piano‑roll” style grid of colored blocks, where each block is a
   `ChordEvent` (degree, quality, velocity, startStep, lengthSteps). The tool can then:
   - convert the grid back to an equivalent Roman progression string, with durations,
   - sync the grid from an existing progression asset,
   - apply the grid directly into a `ChordProgressionData` asset,
   - keep both Roman string and grid views in sync.

In addition, the window can:

- **Preview** how the progression sounds harmonically (names + per‑beat ASCII grid).
- **Integrate** with a `ChordProgressionLibrarySO` so the new asset is automatically
  registered in the global library used by the game’s composers.

---

## 2. External Data & Dependencies

### 2.1 `ChordProgressionData`

A ScriptableObject that stores the actual progression:

- `TimeSignature TimeSignature`
- `int Measures`
- `int Subdivisions`
- `List<ChordEvent> Events`
- `string DisplayName`
- `string OriginalInput`
- `Tonality[] AllowedTonalityModes`

Each `ChordEvent` includes:

- `ScaleDegree Degree`
- `ChordQuality Quality`
- `int StartStep`
- `int LengthSteps`
- `int Velocity`

**Key relations:**

- The editor window **writes into** this asset.
- The game’s `ChordTrackComposer` **reads from** it at runtime.

### 2.2 `ChordProgressionLibrarySO`

A separate ScriptableObject that holds a list of progression entries:

- each entry references a `ChordProgressionData` asset,
- used as a searchable library by MIDI Gen Play configuration.

**Key relations:**

- The editor window can **optionally add** the current progression into the library.

### 2.3 `RomanProgressionParser` (new service)

A pure‑logic class in the MIDI Gen Play composition layer that:

- parses Roman progression strings into a `List<ParsedChord>`,
- understands quality suffixes (`m`, `dim`, `Maj7`, `°7`, `ø7`, etc.),
- applies the `AutoDiatonicMode` rules when no explicit quality is given,
- parses durations like `(0.5)`, `(2)`, or falls back to the default duration.

The editor window now holds a shared instance of this parser and delegates all Roman‑string
parsing to it.

### 2.4 `RhythmGridQuantizer` (new service)

A pure‑logic class that:

- accepts a set of durations (in measures) + `beatsPerMeasure`,
- searches for a valid `subdivisions` value and per‑chord step lengths,
- returns `lengthSteps`, `totalSteps`, and a failure reason if no consistent grid exists.

The window still has an older `ComputeStepsAndSubdivisions` helper; the plan is to make
the window call into `RhythmGridQuantizer` everywhere and then delete the duplicate.

### 2.5 `ChordQualityResolver` (new service)

A pure‑logic class that:

- given a `ParsedChord`, a `Tonality`, and an auto‑quality mode, resolves the final
  `ChordQuality` (triad vs. seventh, etc.),
- answers whether a chord is diatonic or “borrowed” (same triad family vs. different).

The editor window currently still includes local helpers (`ResolveChordQuality`,
`IsChordDiatonic`, `TriadFamily`); the intention is to migrate their logic fully into this
service and just call it from the window.

---

## 3. High‑Level Workflow

At a conceptual level, the editor window implements the following pipeline:

### 3.1 Roman Mode

1. User types a Roman progression string into `progressionInput`.
2. On **Parse & Preview**:
   - the window calls `romanParser.TryParse(input, defaultDuration, inferFromCase, out chords, out error)`,
   - the parser returns a `List<ParsedChord>` or an error message.
3. If parsing succeeds:
   - the window/quantizer computes `Measures`, `Subdivisions`, total steps and distribution,
   - the window builds a preview:
     - resolves each chord to a concrete root note using `previewRoot` + `referenceTonality`,
     - resolves the chord quality (diatonic vs explicit/borrowed),
     - generates human‑readable chord names and a per‑beat grid (ASCII art).
4. On **Apply**, the window:
   - reuses parsing/quantization logic,
   - fills `ChordProgressionData.Events`,
   - updates `TimeSignature`, `Measures`, `Subdivisions`,
   - sets `AllowedTonalityModes`,
   - updates `DisplayName` and `OriginalInput`,
   - optionally registers the asset in `ChordProgressionLibrarySO`.

### 3.2 Grid Mode

1. User switches to Grid tab.
2. The window builds a simple grid model:
   - total beats from `TimeSignature` and `Measures`,
   - total steps = beats × `Subdivisions`,
   - each `ChordEvent` mapped to a colored segment across steps.
3. The user:
   - can click/drag to create or resize chord segments,
   - can select a segment and change its degree/quality,
   - can delete segments.
4. On **Apply Grid to Asset**, the window:
   - writes the grid back into `ChordProgressionData` as `Events`,
   - optionally regenerates or updates the Roman string representation.

(At the moment the deeper grid operations live inside the window class; the plan is to
move them into a dedicated `ChordGridModel`.)

---

## 4. Key Fields in the Window

> Note: names below reflect the current code; future refactors may rename / split some of
> them but the conceptual roles should remain similar.

### 4.1 Asset & Library References

- `ChordProgressionData targetAsset`  
  The asset being edited. May be `null` when starting a new progression from scratch.

- `ChordProgressionLibrarySO progressionLibrary`  
  Optional library asset to which the new/edited progression is added.

### 4.2 Roman Input & Preview State

- `string progressionInput`  
  The Roman progression string typed by the user.

- `float defaultDurationMeasures`  
  Used when chords don’t specify an explicit `(x)` duration.

- `Tonality referenceTonality`  
  Mode the Roman degrees are written in (e.g., Ionian, Mixolydian, Aeolian). Drives
  diatonic degree → quality inference when auto‑modes are used.

- `AutoDiatonicMode autoDiatonicMode`  
  Enum controlling how much the system infers chord quality from the tonality:
  - `None` → case is taken as literal quality when no suffix is provided.
  - `Triads` → standard diatonic triads.
  - `Sevenths` → diatonic seventh chords (Imaj7, iim7, V7, etc.).

- `NoteName previewRoot`  
  Which concrete tonic to use for the preview (e.g., C, D♭, E). Doesn’t change the
  abstract degrees/qualities, only the note labels.

- `string previewChordNames`  
  Single‑line string containing resolved chord names, durations and borrowed indicators.

- `string previewGridText`  
  Multi‑line ASCII grid of measures × beats, annotated with chord labels.

- `int previewMeasures, previewSubdivisions, previewBeatsPerMeasure`  
  Derived values used by the preview grid (and potentially by future visual tooling).

### 4.3 Time / Meter / Grid State

- `TimeSignature timeSignature`  
  Time signature to use when building new progressions (4/4, 3/4, etc.).

- `int beatsPerMeasure`  
  Derived from `timeSignature` (e.g., 4 for 4/4, 3 for 3/4).

- Grid‑related variables (currently local to the window) that define:
  - measures and subdivisions in the grid preview,
  - pixel sizes and colors for drawing the chord bars.

(Later, these should become parameters of a `ChordGridModel`.)

---

## 5. Important Methods (Current Implementation)

This section highlights key editor methods and how they work today.

### 5.1 `ParseAndPreview(bool onlyPreview)`

- Guards against empty input.
- Uses `autoDiatonicMode` to decide whether to infer from case:
  - `inferFromCase = (autoDiatonicMode == AutoDiatonicMode.None)`
- Calls `romanParser.TryParse(...)`:
  - `romanParser` encapsulates the Roman‑string parsing logic.
- On parse failure:
  - shows an `EditorUtility.DisplayDialog` with the error message,
  - clears preview strings to signal failure.
- On success:
  - if `onlyPreview` is `true`, calls `UpdatePreview(chords)`.
  - otherwise calls `ApplyToAsset()` to write the progression into the asset
    (which will internally parse/quantize again using the same parser).

**Role:**  
Provide a high‑level user action for “parse this string and show me the result” with
optional asset writing.

### 5.2 `ApplyToAsset()`

- Guards against empty input.
- Re-parses `progressionInput` using `romanParser.TryParse(...)`.
- If parsing fails, shows an error and aborts.
- On success:
  - computes grid timing from the chords (currently via the private
    `ComputeStepsAndSubdivisions` helper; in the future via `RhythmGridQuantizer`),
  - chooses an effective `TimeSignature` (reusing `targetAsset.TimeSignature` if possible),
  - produces a `ChordEvent` list, mapping each `ParsedChord` to a concrete degree/quality,
  - writes fields on `targetAsset`:
    - `TimeSignature`, `Measures`, `Subdivisions`,
    - `Events`,
    - `DisplayName` and `OriginalInput`,
    - `AllowedTonalityModes`,
  - if a `ChordProgressionLibrarySO` is assigned and doesn’t already contain the asset,
    adds it as a new entry.

**Role:**  
Core “save/apply” operation, which turns the Roman input into the actual asset data.

### 5.3 `UpdatePreview(List<ParsedChord> chords)`

- Ensures chords exist; otherwise clears preview fields.
- Determines the time signature to use:
  - uses `targetAsset.TimeSignature` if available,
  - otherwise uses the window’s `timeSignature` field.
- Calls the same grid‑quantization logic as `ApplyToAsset` to compute:
  - `beatsPerMeasure`, `subdivisions`, `totalSteps`,
  - per‑chord length in steps (`lengthsSteps`).
- Builds a scale (`GetScaleFromTonality`) and concrete degree roots from:
  - `referenceTonality` and `previewRoot`.
- Resolves a final `ChordQuality` for each parsed chord using the window’s
  `ResolveChordQuality` helper (to be replaced by `ChordQualityResolver`):
  - explicit quality in the parsed chord wins,
  - otherwise auto‑modes (`Triads` / `Sevenths` / `None`) are applied.
- Uses `IsChordDiatonic` to mark chords as diatonic or borrowed and affect coloring.
- Builds:
  - `previewChordNames` – linear text with chord names and duration info,
  - `previewGridText` – multi‑line ASCII grid showing which chord occupies each beat.

**Role:**  
Provide a visual/harmonic preview for the current Roman input without modifying assets.

### 5.4 `OnGUI()`

- Renders the editor UI using IMGUI:
  - toolbar (Roman/Grid tabs),
  - fields for `targetAsset`, `progressionLibrary`, `referenceTonality`,
    `autoDiatonicMode`, `timeSignature`, etc.,
  - the Roman input text field and buttons for:
    - Parse & Preview,
    - Apply (create/update asset).
  - the preview panel (names + ASCII grid),
  - the Grid tab with a simple chord‑grid editor.

- Delegates to helper methods like:
  - `DrawRomanMode()`,
  - `DrawGridMode()`,
  - `DrawPreview()`.

**Role:**  
Coordinator for user interaction. This is the main target for future thinning
(once logic is moved into services and view helpers).

---

## 6. Roman Parsing & Quality Inference (Conceptual)

Although the heavy parsing now lives in `RomanProgressionParser`, the overall conceptual
model remains as originally described.

### 6.1 Roman Tokens & Durations

Supported Roman tokens look like:

- `I`, `ii`, `V7`, `ivø7`, `bVII`, etc.
- Optional duration in measures:  
  `I (1)` → 1 measure, `V (0.5)` → half a measure (2 beats in 4/4).

If no duration is provided, `defaultDurationMeasures` is used.

The parser:

1. Splits on `-`/`–`/`—` to get tokens.
2. For each token:
   - separates Roman part and duration `(x)` if present,
   - parses accidental(s), Roman numeral (degree), and quality suffix (if any),
   - decides explicit quality vs. leaving it `null` (to be inferred later),
   - parses the duration `(x)` if present or applies the default.

Outputs a list of `ParsedChord`:

- `ScaleDegree degree`
- `ChordQuality? explicitQuality`
- `float durationMeasures`

### 6.2 AutoDiatonicMode

The `AutoDiatonicMode` enum defines how to handle quality when none is explicitly set:

- **None**  
  - Roman case is literal.  
  - An uppercase degree without suffix defaults to **Major triad**.  
  - A lowercase degree without suffix defaults to **Minor triad**.  
  - The selected tonality is **not** used to infer anything in this mode.

- **Triads**  
  - Ignore case; use diatonic triads for the selected mode and degree
    (e.g. in Ionian: I, ii, iii, IV, V, vi, vii°).
  - Used when you want quick “in‑key” progressions without micromanaging suffixes.

- **Sevenths**  
  - Ignore case; use diatonic seventh chords for the selected mode and degree
    (e.g. Ionian: Imaj7, iim7, iiim7, IVmaj7, V7, vim7, viiø7).

An explicit suffix (e.g. `dim`, `m7`, `Maj7`) always wins over auto‑modes.

---

## 7. Diatonic vs Borrowed Analysis (Current Logic)

The editor window currently includes local helpers for diatonic analysis, which mirror
the behaviour of `ChordQualityResolver`:

- `ResolveChordQuality(ParsedChord c)`:
  - if `c.explicitQuality` is set → return it,
  - otherwise, apply `AutoDiatonicMode` to pick a triad or seventh quality.
- `TriadFamily GetTriadFamily(ChordQuality q)`:
  - groups qualities into families: Major, Minor, Diminished, Augmented, Suspended, Other.
- `bool IsChordDiatonic(ScaleDegree degree, ChordQuality quality)`:
  - builds the expected diatonic triad for the degree in the reference tonality,
  - compares its `TriadFamily` with the actual chord’s family,
  - if they match → the chord is considered “diatonic”; otherwise “borrowed”.

The plan is for `ChordQualityResolver` to completely own this logic, with the window only
requesting “resolved quality + diatonic flag” for each parsed chord.

---

## 8. Responsibilities Summary

`ChordProgressionEditorWindow` currently:

1. **Owns UI** for authoring chord progressions (Roman input + Grid input).
2. **Parses and normalizes** Roman‑numeral strings into a quantized, step‑based format (now largely delegated to the shared `RomanProgressionParser`).
3. **Maintains harmony metadata** – tonalities, diatonic vs borrowed chords, qualities.
4. **Handles grid editing** of chord events (creation, selection, editing, deletion).
5. **Performs conversions**:
   - Roman → ParsedChord list → step grid (`ChordEvent`s).
   - Grid (`ChordEvent`s) → Roman string.
6. **Writes & updates assets** (`ChordProgressionData`) including `DisplayName` and
   `OriginalInput`.
7. **Integrates with** the progression library system (`ChordProgressionLibrarySO`).

As a result, the class has grown large and spans several responsibilities that could be
split into smaller, reusable components.

---

## 9. Refactoring Recommendations (SOLID‑Oriented)

This section lists potential refactors you can explore in the new conversation.

### 9.0 Refactors implemented so far (Dec 2025)

Since the previous version of this document, several of the planned extractions have already been applied:

- **RomanProgressionParser (DONE, in `MidiGenPlay.Composition`)**  
  The heavy Roman-string parsing logic (`TryParseProgression`, `TryParseRomanWithQuality`, duration parsing, quality suffix handling, etc.) now lives in a dedicated `RomanProgressionParser` class. The editor window holds a shared instance (`romanParser`) and uses it from:
  - `ParseAndPreview(bool onlyPreview)`
  - `ApplyToAsset()`

  This keeps parsing pure and testable and removes most of the string/token handling from the window.

- **RhythmGridQuantizer (IMPLEMENTED as a reusable service, not yet fully wired)**  
  The "duration → step grid" logic was extracted into `RhythmGridQuantizer`, a utility that takes durations in measures and finds a suitable `subdivisions` value and per-chord step lengths. At the moment the editor window still contains its legacy `ComputeStepsAndSubdivisions` method; a next step is to route all quantization through `RhythmGridQuantizer` and then delete the duplicate code from the window.

- **ChordQualityResolver (IMPLEMENTED, logic duplicated for now)**  
  A dedicated `ChordQualityResolver` class now encapsulates the rules for:
  - inferring chord quality from degree + tonality when not explicitly specified, and
  - deciding whether a chord is diatonic (same triad family) or borrowed.

  The editor window currently still carries local equivalents (`ResolveChordQuality`, `IsChordDiatonic`, `TriadFamily`). A follow‑up refactor will make the window use `ChordQualityResolver` instead, so that quality/diatonic logic lives in one place.

- **Auto-diatonic quality mode clarified**  
  The `AutoDiatonicMode` enum now clearly separates:
  - `None` → respect case in the Roman string as literal triad quality when no suffix is given; key is ignored in that situation.
  - `Triads` → ignore case and use diatonic triads for the selected mode/degree.
  - `Sevenths` → ignore case and use diatonic seventh chords for the selected mode/degree.

  This behaviour is used consistently both when writing the asset and when building previews.

These changes move a good chunk of parsing/theory work out of the window and prepare the ground for the remaining extractions (grid model, preview builder, formatter, etc.).

### 9.1 Separate Pure Logic From Editor UI (status & next steps)

We already have several non‑editor classes in a runtime/editor‑agnostic assembly (`MidiGenPlay.Composition`):

1. **`RomanProgressionParser` (DONE)**  
   - Responsibility: parse a Roman progression string into a `List<ParsedChord>` (degree, optional explicit quality, duration in measures).
   - Used by the editor window in `ParseAndPreview` and `ApplyToAsset`.
   - Possible future extensions:
     - expose options for allowed quality suffix aliases,
     - plug in a future `RomanProgressionFormatter` for round‑tripping and pretty‑printing.

2. **`RhythmGridQuantizer` (DONE, not yet used by the window)**  
   - Responsibility: take a list of durations in measures plus `beatsPerMeasure` and compute:
     - `subdivisions` in a configurable range,
     - `lengthSteps` per duration,
     - `totalSteps`,
     - or an error string when the durations cannot be represented with an integer grid.
   - This is essentially the extracted `ComputeStepsAndSubdivisions` logic.
   - **Next step:** replace the calls to `ComputeStepsAndSubdivisions` in `ApplyToAsset` and `UpdatePreview` with a shared `RhythmGridQuantizer` instance and then remove the private method from the window.

3. **`ChordQualityResolver` (DONE, to be wired into the window)**  
   - Responsibility: given a `ParsedChord`, a reference tonality and an `AutoChordQualityMode`:
     - resolve the final `ChordQuality`, and
     - answer whether the chord is diatonic or borrowed.
   - **Next step:** make the editor window use `ChordQualityResolver` instead of its local `ResolveChordQuality` / `IsChordDiatonic` helpers, and eventually remove those helpers from the window.

The following pure components are still **to be implemented** and shared across future tools (rhythm patterns, melodic phrases, etc.):

4. **`ChordGridModel` (TODO)**  
   - Holds grid parameters and a list of `ChordEvent`s.
   - Knows how to:
     - clamp events to the grid,
     - sort and merge overlapping events,
     - convert to/from `ChordProgressionData`,
     - build a Roman string from the grid (`BuildRomanStringFromGrid`).
   - No direct GUI calls; purely data‑oriented.

5. **`ChordProgressionPreviewBuilder` (TODO)**  
   - Given a sequence of chords (degrees, qualities, durations), a tonality and a root note, builds:
     - a linear chord‑name preview string,
     - a per‑beat / per‑bar textual grid representation,
     - and later possibly a small MIDI clip for quick audition.
   - This would own the preview‑building logic currently in `UpdatePreview`.

6. **`RomanProgressionFormatter` (NEW, TODO)**  
   - Responsibility: go from the structured model (e.g. `ParsedChord` list or `ChordEvent` grid) back to a normalized Roman string.
   - Would centralize decisions about:
     - dash / spacing conventions,
     - when to emit explicit durations `(x)` vs. rely on defaults,
     - how to print chord qualities (e.g. `Maj7` vs `M7` vs `Δ`).
   - Used both for:
     - regenerating the Roman field from the grid, and
     - keeping round‑tripping behaviour consistent across tools.

The editor window should progressively delegate to these services so `OnGUI` becomes mostly orchestration and state display rather than business logic.

### 9.2 Slice the Editor Window by Concern

Once more of the logic has been pushed into services, you can slice the window into
view‑oriented helpers. Examples:

1. **`RomanModeView` / `GridModeView`**
   - Each view receives a small model/state object and callbacks.
   - Responsible solely for drawing and capturing user input.
   - Does not handle parsing, quantization, or asset persistence.

2. **Partial classes** for `ChordProgressionEditorWindow`
   - E.g. `ChordProgressionEditorWindow.Roman.cs`, `.Grid.cs`, `.Preview.cs`.
   - Keeps each file small and topic‑focused while still sharing private fields.

This will dramatically reduce the cognitive load of `OnGUI()` and make changes to Roman
or Grid mode safer and more localized.

### 9.3 Introduce a Shared Editor Model

Define a lightweight model that is independent of IMGUI but represents the editor state:

- `ChordProgressionEditorState`
  - `ChordProgressionData TargetAsset`
  - `string RomanInput`
  - `List<ParsedChord> ParsedChords`
  - `List<ChordEvent> GridEvents`
  - `TimeSignature TimeSignature`
  - `Tonality ReferenceTonality`
  - `AutoDiatonicMode AutoDiatonicMode`
  - `NoteName PreviewRoot`
  - etc.

The window then:

- binds UI controls to this model,
- passes the model to services (`RomanProgressionParser`, `ChordGridModel`, etc.),
- keeps the model as the single source of truth for the editor state.

### 9.4 Reuse Logic Across Other Pattern Editors

Because you plan similar editors for:

- **Rhythm Patterns**, and
- **Melodic Phrases**,

it is worth investing in reusable building blocks:

- a generic **grid editor** component that:
  - draws measure/beat subdivisions,
  - draws draggable segments with labels,
  - supports selection, cloning, deleting, snapping.
- shared **duration quantization** and **grid/preview** utilities.

These can be packaged as:

- a small runtime assembly (`MidiGenPlay.Composition` / `MidiGenPlay.Grids`),
- an editor assembly (`MidiGenPlay.Editor.Grids`).

### 9.5 Future Extensions

Once the above refactors are in place, you can consider:

- pluggable preview styles (e.g., actual piano‑roll rendering, small audio audition),
- localization of chord naming (e.g., Do–Re–Mi, H vs. B, etc.),
- support for modal mixture / secondary dominants as first‑class concepts in parsing,
- richer metadata per progression (tags, difficulty, “mood” descriptors, etc.).

---

## 10. How To Use This Document in the New Thread

When you start the new conversation:

1. Paste or attach this markdown as context.
2. Decide whether you want to:
   - (a) first extract pure logic into separate classes, or  
   - (b) first split the window into partial classes / nested views.
3. We can then work incrementally, e.g.:
   - Step 1: Switch the editor window over to the existing `RhythmGridQuantizer` and `ChordQualityResolver` services and remove the duplicate private helpers.
   - Step 2: Introduce `ChordGridModel` and move grid operations there (including Grid → Roman conversion).
   - Step 3: Extract `ChordProgressionPreviewBuilder` and `RomanProgressionFormatter`, and make preview / string‑regen logic delegate to them.
   - Step 4: Simplify `OnGUI()` to delegate to thinner `RomanView` / `GridView` helpers or partial classes.

This way we preserve behavior while reducing complexity and moving toward a more
SOLID architecture.

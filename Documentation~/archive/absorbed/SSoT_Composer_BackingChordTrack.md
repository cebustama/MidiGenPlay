> ABSORBED SOURCE — preserved during governance migration. Use current SSoTs in `runtime/` and `authoring/` as the active authorities.

# SSoT — Composer Pipeline: Backing (Chord Track) — ChordTrackComposer (ALWTTT × MidiGenPlay)

**Status:** implementation-aligned (updated for TS-aware selection + normalized-bar-time adaptation)  
**Generated:** 2026-03-04  
**Updated:** 2026-03-07  
**Scope:** End-to-end **musical** pipeline for generating a **Backing / Chord** track (MIDI) using **`ChordTrackComposer`**, from **Composition card authoring assets** through **SongConfig → Orchestrator → MIDI bytes**.

> This doc covers the **Backing track musical generation** only.  
> It does **not** re-document gameplay effects or session-loop logic.

---

## 0) Key invariants (load-bearing contracts)

- **Musical modifiers** and **gameplay effects** are separate pipelines and MUST NOT mix.
- **Part.TimeSignature is authoritative** for runtime rendering.
  - If a Meter / TS effect has changed the part, that resolved `Part.TimeSignature` is the source of truth used by the Backing composer.
- **No asset mutation** at runtime.
  - If a progression must be adapted to another Time Signature or finer subdivisions, the composer creates a **runtime clone** and mutates the clone only.
- **Determinism** must come from `ctx.rng` when available.
- Chord progressions chosen from assets must be treated as **authored content first**, procedural content second.

---

## 1) Inputs (what the composer depends on)

### 1.1 From the live composition model → SongConfig
A Track card ultimately becomes a `SongConfig.PartConfig.TrackConfig`:

- `TrackConfig.Role == TrackRole.Backing`
- `TrackConfig.Instrument : MIDIInstrumentSO`
- `TrackConfig.Parameters : TrackParameters`
  - `Style : TrackStyleBundleSO` (expected `BackingCardConfigSO`)
  - `Pattern : PatternDataSO` (optional fallback; may or may not be populated by the bridge)

### 1.2 Backing authoring assets
**`BackingCardConfigSO : TrackStyleBundleSO`**
- `voiceLeadingOverride : VoiceLeadingConfig` (optional)
- `progressionOverride : ChordProgressionData` (optional)
- `progressionPalette : ChordProgressionPaletteSO` (optional)
- Two override pickers exist conceptually:
  - **Legacy picker**: direct override → palette weighted random → null
  - **TS-aware picker**: direct override → palette TS-aware selection (Tier A/B/C) → legacy palette fallback → null

**`ChordProgressionPaletteSO`**
- `paletteDisplayName`
- `paletteNotes`
- `entries : List<WeightedEntry>` where each entry contains:
  - `progression : ChordProgressionData`
  - `weight : float`
- Legacy picker (`PickRandomProgression`) filters out `weight <= 0`.
- Latest agreed patch also adds a palette-level test/control bool:
  - `preferExactTsMatches : bool = true`
  - When true, TS-aware selection uses Tier A exact-match preference.
  - When false, TS-aware selection skips Tier A and starts directly from Tier B fallback heuristic.

**Important current behavior note**
- In the TS-aware path, candidate extraction is reflection-based and the candidates are sanitized with a minimum positive weight for roulette.
- That means a progression with `weight = 0` can still remain eligible in the TS-aware path, especially if it matches exact TS.
- In practice this means:
  - **Legacy palette random** treats weight 0 as disabled.
  - **TS-aware palette selection** may still pick a weight-0 entry if it survives candidate extraction and wins Tier A / Tier B.
- This is currently useful for testing, but it must be documented because it differs from the legacy picker contract.

**`ChordProgressionData : PatternDataSO`**
- `Measures`
- `subdivisions` (steps per beat)
- `TimeSignature`
- `events : List<ChordEvent>`
- `tonalities : List<Tonality>`
- `DisplayName` / `originalInput` used for readable logs

**`VoiceLeadingConfig`**
- Defines voicing candidate set + register behavior + scoring parameters
- May be replaced per card via `voiceLeadingOverride`

### 1.3 Runtime services / stack
- **`MidiMusicManager`** asks the generator/orchestrator to render the part and serializes MIDI bytes.
- **`SongOrchestrator`** coordinates per-part generation and delegates each role to an `ITrackComposer`.
- **`ChordTrackComposer : ITrackComposer`** is the Backing role composer.
- **`MidiGenerator.GenContext`** provides:
  - `rng`
  - progression cache hooks (`GetProgressionForPart` / `SetProgressionForPart`)
  - shared services (voicer, helpers, etc.)

---

## 2) End-to-end flow (authoring → MIDI bytes)

### 2.1 Authoring (design-time)
1. Author one or more `ChordProgressionData` assets.
2. Optionally group them into a `ChordProgressionPaletteSO`.
3. Create a `BackingCardConfigSO` and set:
   - `progressionOverride` **or** `progressionPalette`
   - optional `voiceLeadingOverride`
4. Create a Composition Track card whose style bundle is that `BackingCardConfigSO`.

### 2.2 Runtime bridge (card → SongConfig)
1. Playing the card updates the live composition UI/model.
2. `SongConfigBuilder.FromUI(...)` builds `SongConfig`.
3. Track parameters carry the style bundle into the render layer.
4. `MidiMusicManager` asks `SongOrchestrator` / generator to render the current part.

### 2.3 Consumption point (where authored progression becomes sound)
Inside `ChordTrackComposer.Compose(part, cfg, bpm, channel, ctx)` the composer:
- resolves the effective Backing style bundle
- chooses a chord progression (override / palette / cache / explicit pattern / procedural)
- aligns tonality if the progression constrains allowed tonalities
- normalizes the progression to the Part TS when needed
- renders chord notes + markers into a `MidiFile`

---

## 3) Resolution precedence (critical)

### 3.1 Voice-leading config precedence
- `effectiveVL = backingStyle.voiceLeadingOverride ?? settings.voiceLeading`

### 3.2 Progression resolution precedence
Let `backingStyle = cfg.Parameters.Style as BackingCardConfigSO`.

1. **Card-level override path** (highest priority)
   - Call the TS-aware override picker when possible.
   - Direct `progressionOverride` always wins if non-null.
   - Otherwise `progressionPalette` is queried.

2. **Share into part cache**
   - If a card-level progression was chosen and the part cache is empty, cache it.

3. **Fallback to existing part cache / explicit pattern**
   - If the override path returns null:
     - use `ctx.GetProgressionForPart(part)` if present
     - else try `(cfg.Parameters.Pattern as ChordProgressionData)`

4. **Procedural / library fallback**
   - If still null, build a procedural progression (implementation-defined) and cache it.

### 3.3 Tonality alignment
If `prog.tonalities` is non-empty, the composer may align the part tonality to one of the allowed tonalities before rendering.

---

## 4) TS-aware override selection (palette path)

This logic lives conceptually in `BackingCardConfigSO.PickProgressionOverride(rng, desiredTimeSignature, settings, verbose)`.

### Tier A — exact TS match
- Candidate set is restricted to entries whose `prog.TimeSignature == desiredTS`.
- Then roulette is performed over that restricted set.
- By default this is the first thing attempted.
- With the latest palette patch, this tier can be skipped per palette by setting:
  - `preferExactTsMatches = false`

**Consequence:**
- If Tier A is enabled, an exact-TS progression can beat a more heavily weighted non-matching progression because non-matching candidates do not compete in this tier.
- In the current TS-aware implementation, an exact-match progression may still be chosen even if its authoring weight is 0.

### Tier B — ranked fallback heuristic
If Tier A is unavailable (or intentionally skipped), all candidates compete using:

`finalScore = entryWeight * heuristicMultiplier`

Roulette is then performed over the scored set.

#### Heuristic factors used by `ComputeTsHeuristicMultiplier(...)`
1. **Bar-length equivalence** (strongest factor)
   - Compare source and target measure duration in quarter-note units.
   - Exact or near-exact bar-length equivalence receives a large bonus.
   - Otherwise apply a smooth penalty based on difference.

2. **Same beat unit** (medium)
   - Same denominator gets a bonus.

3. **Parity of numerator** (mild)
   - Odd↔odd or even↔even gets a small bonus.

4. **Numerator closeness** (mild)
   - Smaller difference in beats-per-measure is favored.

5. **Subdivisions readiness** (mild)
   - Assets already authored at or above `minHarmonicSubdivisions` get a small bonus.

6. **Chord-density vs grouping-count fit** (mild but musically useful)
   - Estimate chord starts per bar.
   - Compare against a default grouping count for the target TS.
   - Progressions whose harmonic density resembles the destination grouping are favored.

#### Default grouping counts currently assumed
- 4/4 → 2
- 3/4 → 1
- 2/4 → 1
- 6/8 → 2
- 9/8 → 3
- 12/8 → 5
- 5/4 → 2
- 7/8 → 3

### Tier C — raw weights safety fallback
- If all Tier B scores collapse to 0 or less, fall back to roulette using raw candidate weights.
- This is a safety net, not the intended normal path.

---

## 5) How TS adaptation works after selection

Selection and adaptation are **separate phases**.

- **Selection** chooses the most promising progression asset.
- **Adaptation** makes that chosen progression fit the current `Part.TimeSignature` and minimum harmonic grid.

This adaptation lives in `NormalizeProgressionForPartIfNeeded(...)` inside `ChordTrackComposer`.

### 5.1 When normalization happens
Normalization runs if either of these is true:
- `prog.TimeSignature != part.TimeSignature`
- `prog.subdivisions < minHarmonicSubdivisions`

If neither is true, the original progression reference is used unchanged.

### 5.2 What “normalized bar time” means
The method does **not** reinterpret the progression by copying beat numbers literally.
Instead it preserves **relative position inside the bar**.

For each event:
1. Compute its start position in source bars:
   - `bars = startStep / srcStepsPerMeasure`
2. Reproject that position into the destination grid:
   - `startDst = round(bars * dstStepsPerMeasure)`

So the rule is:
- “keep this chord at the same normalized position in the bar”,
not:
- “keep this chord on the same numbered beat”.

### 5.3 What gets changed
The runtime clone updates:
- `TimeSignature`
- `subdivisions`
- event start steps
- event lengths (reconstructed from next-start anchors)

### 5.4 Collision handling
If multiple source events quantize to the same destination start step:
- events are sorted
- duplicate starts are collapsed
- only one anchor survives per start position

### 5.5 Duration rebuilding
Durations are not blindly copied.
After anchor reprojection, each event length is rebuilt as:
- `nextStart - currentStart`
- or `endOfPattern - currentStart` for the last event

This prevents gaps, overlaps, and hanging tails after reprojection.

### 5.6 What normalization does *not* do
It does **not yet**:
- snap to metric accent groups (3+2, 2+3, etc.)
- reinterpret chords by strong/weak beat semantics
- reharmonize
- optimize grouping-aware placement beyond normalized-bar reprojection

That is why a future “grouping presets + accent snap” phase is still meaningful.

---

## 6) Rendering behavior after progression is resolved

### 6.1 Grid interpretation
After normalization (or if no normalization was needed):
- `beatsPerBar` comes from `part.TimeSignature`
- `stepsPerBeat` comes from `prog.subdivisions`
- `stepsPerMeasure = beatsPerBar * stepsPerBeat`
- pattern is repeated enough times to cover the whole part

### 6.2 Coverage invariants
The composer now logs and validates:
- `coveredSteps`
- `tailSteps`
- expected tick length for the part

Target invariant for a healthy render:
- `tailSteps == 0`
- and after trim: `lastTickRelative == lenTicks`

### 6.3 Voice-leading
- If voice-leading is enabled, a voicer realizes chords according to `VoiceLeadingConfig`.
- If disabled or unavailable, the composer uses a simpler voicing strategy.

### 6.4 MIDI hygiene
- Chord markers are stamped for debug/analysis.
- Bank/patch/channel are enforced.
- Returned value is a `MidiFile` representing this role for this part.

---

## 7) Observability / logs that now matter

With generator logs enabled, the Backing pipeline can now be validated much more directly.

### Selection / source logs
Useful conceptual categories:
- override source chosen
- palette TS-aware selection tier
- progression name / display name
- cached vs procedural fallback

Current logs already visible in code / runtime:
- `PROG_PICK tier=A/B/C ...`
- card-level override usage
- progression display name used by the composer

### Normalization logs
- `PRE-NORM progTS=... sub=... partTS=...`
- `NormalizedBarTime reprojection: TS src → dst | sub src → dst | tsChanged=... subChanged=...`
- `POST-NORM ... changed=...`

### Grid / coverage logs
- `GRID part='...' partTS=... progTS=... stepsPerBeat=... patternTotalSteps=... coveredSteps=... tailSteps=... lenTicksExpected=...`

### Orchestrator logs relevant to verification
- `Trimmed [...] lastTickRelative=... lenTicks=...`
- `Merged [...] lastTickAbsolute=... cursorTicks=... lenTicks=...`

---

## 8) Practical interpretation rules

### Exact-TS selection does **not** imply `changed == false`
Even if the chosen progression already matches the Part TS, the composer may still normalize it because subdivisions are too low.

Example:
- progression TS = 3/4
- part TS = 3/4
- progression sub = x1
- minimum harmonic subdivisions = x4

Result:
- `tsChanged = false`
- `subChanged = true`
- normalization still happens

### Weight-0 semantics are split today
- Legacy palette random picker: weight 0 behaves like disabled.
- TS-aware picker: weight 0 may still remain eligible.

This is currently useful for testing exact-match behavior, but must remain explicitly documented.

---

## 9) Known gaps / technical debt

1. **Palette candidate extraction is reflection-based**
   - Works, but is fragile.
   - Better long-term solution: explicit palette API for TS-aware candidate enumeration.

2. **Weight semantics differ between legacy and TS-aware palette paths**
   - This is useful for tests, but can surprise authors.
   - It should remain documented or be made explicit via configuration.

3. **Pattern fallback may still be underused by the bridge**
   - `TrackParameters.Pattern` support exists, but may not be populated in the authoring-to-runtime bridge.

4. **Grouping-aware accent snap is not implemented yet**
   - Current normalization is positionally robust, not accent-aware.

---

## 10) Verification checklist (runtime)

A healthy Backing pipeline should let you verify all of these:

1. **Exact TS pick**
   - If an exact-TS candidate exists and Tier A is enabled, it is selected first.

2. **Tier B fallback**
   - If exact TS is unavailable (or skipped), the heuristic chooses a musically plausible near candidate.

3. **Normalization correctness**
   - Logs show whether change came from TS mismatch, subdivision upsample, or both.

4. **Coverage correctness**
   - `tailSteps == 0`
   - `lastTickRelative == lenTicks`

5. **Determinism**
   - Same seed / same `ctx.rng` gives same progression choice.

6. **No asset mutation**
   - Adapted progressions are runtime clones only.

---

## 11) Code references (authoritative implementations)
- `BackingCardConfigSO.cs`
- `ChordProgressionPaletteSO.cs`
- `ChordProgressionData.cs`
- `VoiceLeadingConfig.cs`
- `ChordTrackComposer.cs`
- `SongOrchestrator.cs`
- `SongConfigBuilder.cs`
- `MidiMusicManager.cs`

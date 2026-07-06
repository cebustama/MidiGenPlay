# SSoT — Authoring Chord Progressions

## Scope

This document is the primary authority for package-owned chord progression authoring:

- `ChordProgressionData`
- progression palettes
- Roman-string and grid authoring concepts
- `ChordProgressionEditorWindow`
- supporting parser/quantization flow
- authoring-to-runtime handoff for backing generation

## 1. Authoring mental model

Chord progressions are authored as reusable assets that can then be:

- selected directly,
- grouped into palettes,
- consumed by backing-oriented runtime inputs,
- adapted at runtime to the current part meter when required.

## 2. Authoring modes

The current documented system supports two main authoring modes:

- **Roman-string authoring**
- **Grid authoring**

Both are first-class inputs to the same progression asset concept.

## 3. Tooling role

`ChordProgressionEditorWindow` is an authoring front-end over the data model.

Its job is to:

- capture authoring intent,
- parse/normalize the input representation,
- preview the result,
- and save package-owned progression assets.

It should not become the hidden source of musical truth; the saved asset is the durable truth.

Persistence (Phase 8, closed 2026-07-05, PATTERN-PERSIST-1): all four internal save
sites — the Roman apply/create path, the grid apply path, and both Save-As-New paths
(Roman + grid) — route through the shared
`TrackPatternConfigStoreResources<ChordProgressionData>` store, which gives the editor
a canonical default save folder (`Assets/Resources/ScriptableObjects/Patterns/Chords`)
for the first time (previously every Save dialog passed no default folder). The
interactive Save dialog and Undo behavior are unchanged; the store owns the
`AssetDatabase` write.

## 4. Progression asset semantics

`ChordProgressionData` is the package-owned asset for authored chord-event content.

Important semantics include:

- time signature awareness
- measure-based authoring
- meaningful representation of rests/silent spans
- chord-event timing that can later be consumed by runtime backing generation

### 4.1 Chord quality alphabet

The set of chord qualities a progression may use is defined canonically by the
`MusicTheory.ChordQuality` enum (`Runtime/CoreScripts/MusicTheory/MusicTheory.ChordQuality.cs`).
That enum is the single source of truth; the Roman parser
(`RomanProgressionParser.TryParseQualitySuffix`), the LLM prompt alphabet
(`ChordProgressionLLMPromptBuilder`), the editor's quality↔suffix mapping
(`ChordProgressionEditorWindow.QualitySuffixForToken`), and the response-handler
allowlist (`ChordProgressionLLMResponseHandler.AllowedSuffixes`) all mirror it
and must be updated in lockstep. The enum is extended **append-only** — existing
members keep their ordinals so previously serialized `ChordEvent.quality` values
stay valid.

Qualities are authored in Roman mode by an explicit suffix on the numeral
(`Imaj7`, `V7`, `iiø7`, `I6`, `im6`, `V7sus4`, `V9`, `Imaj9`, `iim9`). A bare
degree with no suffix still resolves to the diatonic triad/seventh for the
reference tonality via the downstream resolver; the extended qualities are
**explicit-only** and do not participate in diatonic inference. Because an
explicit suffix outranks numeral case, a sixth chord's major/minor character
comes from the suffix (`6` vs `m6`), not the case (`vi6` is a major-sixth chord
on the submediant; the minor-sixth is `vim6`); the same holds for the ninths
(`vi9` is a dominant-ninth on the submediant; the minor-ninth is `vim9`).

v2 added the qualities in two tiers, both append-only:

- **Tier A** — `Major6`, `Minor6`, `Dominant7sus4` (≤4 voices).
- **Tier B** — `Dominant9`, `Major9`, `Minor9` (five voices; top interval a
  major ninth, beyond the octave).

Grid authoring renders chord-tone rows via `IsSeventhQuality`. The
seventh-bearing qualities (`Dominant7sus4` and the three ninths) report as
sevenths, so the grid draws their four seventh-chord rows; the added-sixth
qualities are 4-voice but not sevenths, so their sixth gets no row. In both
cases the grid under-renders the extra tone — the added 6th, and the 9th of a
ninth chord, have no dedicated grid row (a known grid-display limitation). The
Roman-string / LLM / import path stores and plays every voice correctly.

Voicing of the five-voice ninths is handled by the existing
`BasicVoiceLeadingVoicer` (`Strategies/VoiceLeading.cs`), which realizes
arbitrary-length pitch-class sets. Two voicer behaviours are known deltas for
five-voice chords: drop-2 is triad-oriented (effectively inert for ninths), and
a very tall five-voice stack near an instrument's range edge can have voices
collapsed by the range clamp. Neither affects ≤4-voice chords.

## 5. Palette semantics

Progression palettes group progression assets into reusable themed packs for runtime selection.

Palette grouping is an authoring concern.
Selection logic and fallback behavior live in runtime documentation.

## 6. Runtime handoff

This document defines how progressions are authored.
Runtime consumption is defined in:

- `runtime/SSoT_Composer_Backing_Track.md`

## 7. Update triggers

Update this SSoT when:

- progression asset structure changes,
- Roman/grid authoring semantics change,
- the chord quality alphabet (`MusicTheory.ChordQuality`) changes,
- rest representation changes,
- palette meaning changes,
- the editor window changes how authored data is interpreted or saved.

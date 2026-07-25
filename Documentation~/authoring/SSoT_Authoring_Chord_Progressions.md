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

Two assisted entry paths feed these modes without being modes themselves: LLM
generation (produces a Roman string; see `SSoT_Authoring_LLM_Generation.md`) and
MIDI file import (Batch M3; fills the Grid working state — see the subsection
under §3). Assisted paths never bypass normalize → preview → apply/save.

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

### MIDI file import (Batch M3)

`ChordProgressionEditorWindow` can import a standard MIDI file (`.mid`) into the
GRID working state through `ChordMidiImporter` (`Editor/`, pure function, same
mold as `DrumMidiImporter` / `MelodyMidiImporter`): no Unity-API calls in the
parse, no asset mutation — Apply/Save As remains the only asset write path.
Only ticks-per-quarter-note files are supported (SMPTE is a hard fail). The
window's Timing controls are the meter authority (M3-D4=A): the file's own
time-signature meta events are ignored, and on apply `gridBeatsPerMeasure` is
aligned to the window time signature's beats-per-measure.

Detection is deliberately **restricted** (D-MIDI3=A) and fully deterministic:

- **Segmentation (M3-D1=A).** Note starts/ends quantize to the step grid FIRST
  (grid = window time signature × Grid subdivisions); a segment is a maximal run
  of steps with an identical sounding pitch-class set. The grid absorbs strums,
  arpeggiated attacks and humanized onsets without a tolerance knob.
- **Chord threshold (M3-D3=B).** Channel filter (0 = all; merges warn) plus a
  fixed minimum of 3 distinct simultaneous pitch classes. Sub-threshold segments
  (melody fragments, dyads) leave a warned gap; at runtime
  `FindChordEventAt` sustains the preceding chord across gaps by design.
- **Identification (M3-D5 cascade).** (1) exact pitch-class-set match with the
  BASS as root (per root the v1 alphabet has no pc-set collisions; ninths fold
  mod 12); (2) exact match over all member roots — covers inversions, a single
  match wins silently; (3) multiple exact matches (e.g. {C,E,G,A} over an E
  bass = C6 vs Am7) tie-break diatonic-first, then fewest template voices, then
  lowest root pitch class, with an informative warning; (4) no exact match →
  REDUCTION to the largest contained template with an explicit warning listing
  the dropped pitch classes (never silent — the Roman path's degrade-guard
  philosophy); (5) nothing contained → warned skip.
- **Degree + accidental (M3-D2=A / D2b).** The chosen root resolves to
  (`ScaleDegree`, `degreeAccidental` −1/0/+1) relative to the user-supplied key —
  this covers every chromatic root in all seven modes, so nothing is snapped;
  double spellings prefer the FLAT reading (♭II ♭III ♭VI ♭VII…). `isDiatonic` =
  accidental 0 AND `ChordQualityResolver.IsChordDiatonic` (triad-family test),
  identical to the Roman path.
- **Coalescing + velocity (M3-D6; amended by IMPORT-QOL-1).** Consecutive
  identical (degree, accidental, quality) regions merge — re-articulated
  comping strikes become one harmonic region (strike rhythm belongs to the
  runtime articulators); velocity is the rounded mean of contributing notes.
  A "Preserve Re-strikes" toggle (`Options.preserveReStrikes`, OFF by default
  = the M3 behavior) restricts the merge to CONTIGUOUS regions: a rest between
  two strikes of the same chord then keeps them as separate events, so a
  comping file retains its harmonic rhythm (the runtime reproduces rests
  faithfully). Adjacent identical identities with no gap always merge.

**Documented limitation:** inversions and voicings are discarded —
`ChordEvent` has no inversion field and voicing is runtime's job (voice
leading / articulators). This mirrors M2's absolute-octave limitation and is
deliberately not warned per chord.

Every other lossy step emits an `ImportWarning` (`[Kind] loc: detail`, detailed
up to 8 per kind then aggregated), rendered in the panel alongside a
display-only Roman summary of the imported progression (traceability; not
guaranteed to round-trip through the Roman parser).

Two IMPORT-QOL-1 conveniences complete the import path:

- **Grid suggestion ("Suggest…").** On an explicit button press, the candidate
  subdivisions (1, 2, 3, 4, 6, 8) are probed against the file's note onsets
  AND ends using the import's exact time math and channel filter; the residual
  table (max error per candidate, in grid beats) is always reported, and the
  slider is set to the SMALLEST candidate whose max residual stays within
  `ChordMidiImporter.SuggestMaxErrorBeats` (parsimony first, so humanization
  is not over-fit by a needlessly fine grid). If no candidate passes, the
  argmin is reported and the slider is left untouched. Never automatic, never
  silent; the user's grid remains authoritative.
- **Provenance (`originalInput`).** After a MIDI import, the grid-apply paths
  stamp the source file name into the asset's `originalInput` as a trailing
  `[MIDI: <file>]` suffix (rebuilt each apply — it never accumulates). The
  suffix is asset metadata, not Roman grammar: the editor strips it when
  loading `originalInput` back into the Roman input field, and the in-window
  progression string always stays parseable. The suffix also reaches
  `DisplayName` via `UpdateDisplayNameAuto` — an accepted, documented cost.
  The lineage is severed (no suffix on the next apply) by rebinding the Target
  Asset or by applying through the Roman path.
- **Round-trip precision.** The Roman string derived from the grid emits
  durations with six decimals, which is exact for every power-of-two grid and
  well inside `RhythmGridQuantizer`'s tolerance for non-terminating cases
  (e.g. 6/8 × 8). This is what makes `originalInput` re-parseable after a
  fine-grained import; the previous two-decimal format silently produced a
  string the quantizer could not resolve.

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

### 4.2 Runtime consumption of the grammar (MGP-ALWTTT-DBG-4)

The setup-card + fenced-Roman grammar defined by this document is now
consumable at runtime via
`MidiGenPlay.Composition.ChordProgressionRuntimeImporter`
(`MidiGenPlay.Runtime` assembly): `ParsePayload` is the RELOCATED body of the
former editor importer, and `ChordProgressionEditorImporter` (editor) is a thin
forwarder over it — one grammar, one code path, by construction. The builder
half (`TryParsePayload` / `TryParseRoman`) materializes a never-persisted
`ChordProgressionData` (`HideFlags.DontSave`) through the same pipeline as the
editor's Roman apply path (`RomanProgressionParser` → `RhythmGridQuantizer` →
`ChordQualityResolver`), enforcing the D-L4.5 zero-warning guard
(out-of-alphabet suffix = hard fail; the canonical allowlist now lives
runtime-side and the editor response handler delegates to it).

Grammar semantics note, now test-pinned: a bare `7` suffix is literal
`Dominant7` regardless of Roman case (`ii7` = Supertonic + Dominant7; a minor
seventh requires `m7`).

## 5. Palette semantics

Progression palettes group progression assets into reusable themed packs for runtime selection.

Palette grouping is an authoring concern.
Selection logic and fallback behavior live in runtime documentation.

Canonical palette folder (MGP-ALWTTT-DBG-2):
`Resources/ScriptableObjects/Patterns/Chords/Palettes`. The runtime enumeration
contract lives in `runtime/SSoT_Composer_Backing_Track.md` §2.2.

## 6. Runtime handoff

This document defines how progressions are authored.
Runtime consumption is defined in:

- `runtime/SSoT_Composer_Backing_Track.md`
- `MidiGenPlay.Composition.ChordProgressionRuntimeImporter` builds
  never-persisted `ChordProgressionData` from the same grammar at runtime
  (§4.2; consumption contract in the backing SSoT §2.2).

## 7. Update triggers

Update this SSoT when:

- progression asset structure changes,
- Roman/grid authoring semantics change,
- the chord quality alphabet (`MusicTheory.ChordQuality`) changes,
- rest representation changes,
- palette meaning changes,
- the editor window changes how authored data is interpreted or saved.
- the MIDI import path changes segmentation, matching, or reduction semantics
  (`ChordMidiImporter`, Batch M3).

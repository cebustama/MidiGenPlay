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

### Asset metadata authoring (CPE-META-1, CPE-META-2)

**Asset-level fields (CPE-META-1, D1=A, 2026-07-29).** The window exposes the
asset-level metadata fields — `qualityRenderPolicy`, `useColorTable`,
`cadence` — in a collapsible "Asset Metadata" section, plus read-only
`DisplayName` and `originalInput` (with its `[MIDI: …]` suffix legible as
provenance). Write semantics (D2=C): the section binds DIRECTLY to the bound
target asset — every change is Undo-recorded and dirtied on the asset
immediately, exactly like the Song References section. The Roman/Grid apply
pipelines never read or write these fields, so re-applying a progression can
never clobber hand-authored metadata; conversely, the section never triggers a
parse/apply. `useColorTable` is disabled in the UI while the policy is
`AsAuthored` (where it is a render-time no-op), and an ML-8b advisory (warn,
never block) flags `cadence = Authentic` over a pure `DiatonicToPart` policy.

**Per-event opt-in fields (CPE-META-1, D1=C, same batch).** The Grid tab's
"Selected Chord Event" panel edits `isDiatonic` and the SECDOM-1 pair
(`hasAppliedTarget` / `appliedTarget`) alongside the existing event fields,
with a non-blocking validity advisory that mirrors the render-time SECDOM rules
(Reference Tonality as proxy for the triad check). These flow through the
normal grid commit → apply path; no new write route.

**Metadata in the import payload (CPE-META-2, D3=A, 2026-07-29).** The setup
card accepts four OPTIONAL lines — `Quality render policy:`, `Use color
table:`, `Cadence:`, and `Allowed tonalities:` (comma-separated `Tonality`
enum names). Absence is silent and backward compatible; a present-but-invalid
value emits the `InvalidMetadataField` warning and is ignored — the import mode
is never degraded by metadata, and the tonality list is all-or-nothing (one bad
name discards the whole list rather than silently narrowing the filter). On
import, declared allowed tonalities set the window's tonality toggles (mirror
state; they ride the normal apply route), while the direct-bound trio
(policy / color table / cadence) is STAGED one-shot: a banner in the Asset
Metadata section announces it, the next Apply/Save writes it onto the asset
being written and clears the staging, and a Discard button drops it. Re-applies
after consumption never touch metadata, preserving the CPE-META-1 (D2=C)
no-clobber guarantee. The runtime payload path
(`ChordProgressionRuntimeImporter.TryParsePayload`) stamps the same declared
metadata on its in-memory instance (D-M2-3=A: one grammar, one behavior); a
declared tonality list replaces the TONFILTER-1 single-entry provenance
default. The LLM prompt requests only the descriptive fields — Cadence and
Allowed tonalities (D-M2-4=A); policy and color table remain human choices,
accepted on import when hand-written.

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

**Render policy: diatonic re-qualification (RUNTIME-REQUALITY, D-RQ-SURF=A).**
`ChordProgressionData.qualityRenderPolicy` declares how an asset's qualities
behave when the PART's tonality differs from the tonality the progression was
authored against. Append-only enum, serialized ordinals:

- `AsAuthored = 0` (default) — qualities render exactly as stored. Every
  pre-existing asset deserializes into this, so the feature is inert until
  opted into, and no existing render changes by one byte.
- `DiatonicToPart = 1` — at render time, events flagged `isDiatonic`
  re-resolve their quality to the diatonic chord of the part's tonality on the
  same degree (an asset authored `I – IV – V` renders `i – iv – v` in an
  Aeolian part), preserving triad-vs-seventh size.
- `DiatonicToPartFunctional = 2` — as above, PLUS the common-practice dominant
  exception (D-RQ-FUNC=A / D-RQ-FUNC-SCOPE=A): a Dominant-degree event authored
  `Major` or `Dominant7` KEEPS its authored quality — and is marked borrowed
  (`isDiatonic = false`) on the clone — in modes whose diatonic v would lose the
  leading tone. This is the harmonic-minor practice of raising the dominant's
  third surgically rather than swapping the whole scale. Pick `Functional` for
  cadence-driven material and plain `DiatonicToPart` for pure modal color.

Scope rules:
- **Borrowed chords are never touched (D-RQ-BORROW=A).** `isDiatonic = false`
  events keep their authored quality and `degreeAccidental`; a ♭VI stays a ♭VI.
- **Core alphabet only, size-preserving (D-RQ-MAP=A).** The four triad
  qualities re-map via the diatonic triad of (tonality, degree); the five
  seventh qualities via the diatonic seventh. `Sus2`, `Sus4`, `Major6`,
  `Minor6`, `Dominant7sus4` and the three ninths PASS THROUGH unchanged — they
  have no clean modal reading and their color is authored intent. `Major` is
  never promoted to `Dominant7`.
- **Locrian is a documented no-op (D-RQ-LOCRIAN=A)** for both opt-in policies:
  the tonic triad is itself diminished and every functional reading collapses.
- **Determinism and asset safety.** `ChordProgressionRequality.
  ApplyDiatonicRequality(prog, tonality)` is a pure function: zero rng draws,
  clone-if-changed (the asset instance is NEVER mutated), same-reference return
  when nothing would change, and idempotent (re-applying is a no-op, because
  re-mapped events are diatonic-stable and the protected dominant re-enters as
  borrowed and is skipped).

The transform is applied to the shared DATA, not inside a composer: backing,
bass and melody each compute chord pitch classes independently from the shared
progression's per-event quality, so a composer-local branch would make them
diverge. See `runtime/SSoT_Composer_Backing_Track.md` §3 for the two
application sites.

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

### 4.3 Asset and event opt-in fields (B1 HARMONY-PURE-1, B2 TONFILTER-1)

Fields on `ChordProgressionData` and `ChordEvent` that are opt-in by
construction: every one of them leaves pre-existing assets byte-identical
until it is explicitly set.

**`useColorTable : bool` (default `false`) — REQUALITY-2 (D-CT-GATE=A).**
Opt-in, orthogonal to the policy: effective only under a `DiatonicToPart*`
policy. It enables the lab's color table over the render clone, AFTER the core
remap: sixths by mode (Aeolian/Phrygian: `6`/`m6` → `m7`; Dorian: `6` → `m6`),
`sus2` → `sus4` in Phrygian, `9`/`Maj9` → `m9` on minorized degrees (with the
functional exception: a `V9` under Functional keeps its quality and is marked
borrowed, mirroring D-RQ-FUNC), and the degree substitution `ii(dim)` → `iv`
(D-CT-DIM=A) on LONG events (≥ 2 beats, `ColorDiminishedMinBeats`) or ACCENTED
ones (bar downbeat), preserving size (triad→triad, seventh→seventh), with
accidental 0 and `isDiatonic = true`. The substitution applies to the
POST-remap state even when the remap changed nothing (a sustained authored
`ii°` also substitutes under the table); `vii°` is out of scope by decision.
Assets already opted into requality stay byte-identical unless the flag is
explicitly enabled.

**`cadence : CadenceType` (default `None`) — CADENCE-META (D-CAD-AUTH=A).**
Manually authored enum `{None=0, Authentic, Plagal, Half, Modal}`,
append-only. Pure metadata: composers ignore it; consuming games may gate
replace/reskin decisions on it. The editor's "Suggest" button is future QoL,
not implemented.

**`hasAppliedTarget : bool` + `appliedTarget : ScaleDegree` — SECDOM-1
(D-SD-ENC=A / D-SD-OWN=A).** The secondary-dominant primitive, per event. The
event stores a RELATION ("I am the dominant of that degree"), not a chord, so
it survives transposition and mode changes — which is exactly what a shared
progression demands. The FIELD is the opt-in: resolution runs at render time
regardless of the policy (`AsAuthored` included) and regardless of the
tonality (Locrian included).

With the flag set, the render IGNORES the authored degree/accidental/quality
and rewrites them on the clone: root = perfect fifth above the root of the
target degree IN THE CURRENT MODE, expressed as (degree, accidental) — for
valid targets the accidental is always 0, since the degree with a diminished
fifth is exactly the diminished-triad degree that validity excludes —,
quality `Dominant7`, `isDiatonic = false`. Validity (when it fails the
authored event renders untouched, silently): the target's diatonic triad is
major or minor; the next event by `startStep` (with wrap, so turnarounds are
legal) is the target with accidental 0 and is not itself a secondary
dominant; duration ≤ the target's.

**Authoring.** The Roman string has NO syntax for this; the fields are set in
the asset inspector AFTER the events have been generated (a fresh Apply of the
string rebuilds them and loses the flags). Since CPE-META-1 (§3) the fields are
editable in the Grid tab's "Selected Chord Event" panel, with a non-blocking
validity advisory; the Roman string still has no syntax for them.

**`tonalities` — descriptive reference metadata** (the list of modes the
author conceived the progression for). Editor: informational toggles;
catalogue wizard: an editor-only navigation filter. No runtime effect on the
override, palette and runtime-importer paths since TONFILTER-1: there it does
not filter selection, does not revert the part's tonality, and consumes no rng.
The supported way to sound right outside the reference tonality is
`qualityRenderPolicy` (§4.1).

**Exception on record — F-B2-LIBRARY.** One legacy runtime path still READS
this list as a filter: `ChordTrackComposer.PickTemplateForPart`, reachable only
from the procedural library branch (`ctx.Settings.progressionLibrary != null`),
discards candidate templates whose allowed list — the library entry's
`compatibleTonalities` when non-empty, otherwise this field — excludes the
part's tonality (`ChordTrackComposer.cs:1626–1635`); B2 left that path
deliberately out of scope, so on an asset reached through a progression library
`tonalities` is NOT inert. Runtime side of the same exception:
`runtime/SSoT_Composer_Backing_Track.md` §2.2.

### 4.4 Roman case precedence (EDITOR-CASE-1)

**EDITOR-CASE-1 (D-EC-SEM=B).** Precedence: explicit suffix > unambiguous
case > auto. With Auto-Diatonic active the numeral's case is no longer
discarded: the case fixes the FAMILY and the auto mode fixes the SIZE ("iv"
under Sevenths ⇒ `m7`, under Triads ⇒ `m`). The override only fires when the
case CONTRADICTS the diatonic family: lowercase on a diatonic minor OR
diminished degree (Roman convention also writes diminished degrees in
lowercase) and uppercase on a major degree both keep the auto quality — so
purely diatonic strings resolve exactly as they did before. A contradictory
uppercase under Sevenths yields `Dominant7` on V and `Major7` elsewhere; a
contradictory lowercase yields `Minor7`. Mixed case ("Iv") is discarded with
a warning (the only warning case). Parse-time only: saved assets do not
change. The `None` mode keeps its legacy semantics (case → explicit quality).
Alphabet note: the bare `7` suffix is case-blind and always `Dominant7`
("iv7" is iv with a Dom7; "ivm7" is the m7 spelling).

### 4.5 Known hazards / fixes

Two pre-existing hazards of the F-NORM-DROP family were fixed in passing in
the editor: the grid's selection copy omitted `isDiatonic`, and the grid's
round-trip copy omitted `isDiatonic` AND `degreeAccidental` (accidentals were
lost when saving from the grid). Both sites now copy all 9 fields, secondary
dominants included, and a reflection canary
(`ChordEvent_FieldSurface_MatchesEveryFieldByFieldCopySite`) breaks if the
field surface changes without every copy site being updated.

### 4.6 Modulation planning primitive (B1 MOD-1, `ModulationPlanner`)

`MidiGenPlay.Composition.ModulationPlanner` is a **pure, host-facing harmony
primitive**: given a source key and a target key (each a tonic pitch class plus
a `Tonality`) and an `int seed`, `Plan(...)` returns a `ModulationPlan`
describing the raw material needed to stage a modulation. It is homed in this
document because it emits `ChordProgressionData`-shaped material (degree +
accidental + `ChordQuality`) and sits alongside the rest of the B1 opt-in
surface (§4.3); it is **not** a composer feature.

**It returns a PLAN, not a progression.** The package deliberately produces no
events, no bars and no placement: WHEN the modulation happens, how long the
pivot is held and how the dominant is voiced are game decisions. The host
assembles the plan into a progression and injects it through the existing
`patternOverride` surface (runtime contract:
`runtime/SSoT_Runtime_Generation_Orchestration.md` §5; the per-render override
precedence is step 0 of the backing SSoT §2.2). **Zero composer edits** — this
is the HARMONY-PURE-1 invariant, and it is why no composer consults the planner.

**Output (three parts).**

1. **Functional dominant of the target.** `dominantRootPitchClass` is the pitch
   class a perfect fifth above the target tonic; `dominantQuality` is always
   `Dominant7` (functional cadence practice, not a diatonic derivation). The
   same chord is also expressed against the target mode's own scale as
   `dominantDegreeInTarget` (always `ScaleDegree.Dominant`) plus
   `dominantAccidentalInTarget`, which is `0` in every diatonic mode except
   Locrian, where it is `+1` (raising the diminished fifth to a perfect one).
   That pair drops straight into a `ChordEvent` without further conversion.
2. **Pivot candidates.** `pivots` is the intersection of the two keys' diatonic
   triads, matched on **root pitch class AND triad quality** — a triad that
   shares a root but not a quality is not a pivot. Each `PivotCandidate` carries
   the root pitch class, the shared quality, its degree in the SOURCE key, its
   degree in the TARGET key, and `subdominantInTarget`.
3. **Common tones.** `commonTonePitchClasses` is the pitch-class intersection of
   the two scales, ascending and de-duplicated.

**Ranking (D-MOD-OUT=A).** Candidates are ordered by function in the TARGET,
not in the source: the subdominant band (`Supertonic` / `Subdominant`,
i.e. ii / IV) comes first, everything else after — the common-practice
pivot-as-pre-dominant placement, so the caller can take `pivots[0]` and get a
musically sensible pivot without reading the struct. Inside a band, order is a
**seeded** deterministic tiebreak: `TieHash(seed, rootPitchClass,
degreeInTarget)`, FNV-1a, with degree-in-target as a final total-order safety
net. The seed therefore makes in-band order an explicit function of the caller's
input rather than of discovery order — two hosts with different seeds get the
same candidate SET and the same bands, in a different intra-band order.

**Purity and determinism.** `Plan` is a pure function of its arguments: same
arguments ⇒ same plan, down to list order. **Zero rng draws** — no
`UnityEngine.Random`, no `System.Random`, no stream anywhere; the seed is only
the key of an FNV-1a hash (the same "key of a pure mix, never a `System.Random`
seed" idiom as `ResolveWalkSeed`). Nothing the planner does can perturb any
composer's draw count or order. Lists are freshly allocated per call; callers
may take ownership. Tonics outside `0..11` are wrapped rather than rejected.

**Callers.** None inside the package — `SongOrchestrator` never touches it. The
consumer is the host (in ALWTTT, a `PartEffect` such as `ModulationEffect`,
namespace `ALWTTT.Cards`, out of tree and never governed here). Distinct from
the **directional modulation hint** of `runtime/SSoT_Composer_Backing_Track.md`
§6, which is a one-shot `PartConfig` transient shaping the first chord's
register: §6 changes HOW a chord is voiced, MOD-1 chooses WHICH chords to use.
The two are independent and may be combined by the host.

Test surface: `Tests/Editor/ModulationPlannerTests.cs` (8 tests: the four
textbook C→G pivots, the subdominant band ordering, the D7/accidental-0
dominant, the six shared pitch classes, same-seed plan identity down to list
order, different-seed same-set/same-bands, the Locrian `+1` accidental, and the
Aeolian→relative-major pair sharing all seven triads).

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
- the quality render policy changes (§4.1): new enum members, the borrowed-chord
  rule, the core-alphabet mapping table, the Functional dominant exception, or
  the Locrian no-op.
- the F-B2-LIBRARY exception changes (§4.3): the `PickTemplateForPart` library
  filter is retired, gated, or extended — mirror the runtime side in
  `runtime/SSoT_Composer_Backing_Track.md` §2.2.
- the modulation planner changes (§4.6, MOD-1): the plan's output shape, the
  pivot matching rule (root + quality), the D-MOD-OUT=A ranking or its FNV-1a
  seeded tiebreak, the purity / zero-draws property, or the arrival of an
  in-package caller (which would move the home out of this document).
- the setup-card metadata grammar changes (§3, CPE-META-2): the optional
  `Quality render policy` / `Use color table` / `Cadence` / `Allowed tonalities`
  lines, their presence-gated parsing, the `InvalidMetadataField` warning, or
  the one-shot staging that carries the direct-bound trio onto the asset.

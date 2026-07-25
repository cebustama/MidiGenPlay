# Roadmap — MIDI File Import (Drums / Melody / Chords)

**Status:** COMPLETE. M1 closed 2026-07-19; M2 closed 2026-07-23; M3 closed
2026-07-23. All three phases are done; archived to `planning/archive/` on
2026-07-24 and de-registered from `ssot_manifest.yaml` `roadmaps:`.
**Authority note:** this is a planning document, not implementation authority.
Implemented behavior is governed by the authoring SSoTs listed per phase.

## Scope

Import standard MIDI files (`.mid`) into the package's canonical authoring assets,
via the existing editor windows, honoring the authoring loop
(normalize → preview → apply/save; no silent asset writes):

- Phase M1 — MIDI → `DrumPatternData` (Drum Pattern Editor)
- Phase M2 — MIDI → `MelodyPatternData` (Melody Pattern Editor; supersedes/implements
  `Roadmap_Melody_Authoring_MVP.md` Phase D1)
- Phase M3 — MIDI → `ChordProgressionData` (Chord Progression Editor; restricted
  chord detection)

Out of scope for this roadmap (locked decisions below): bassline import,
automatic key detection, full chord recognition.

## Locked decisions (2026-07-19)

- **D-MIDI1 = A.** Key/tonality for melody (M2) and chord (M3) import is
  **user-specified** in the import UI (root + `Tonality`). No auto-detection.
- **D-MIDI2 = A.** Chromatic notes in melody import **snap to the nearest diatonic
  degree with a per-note warning**. No data-model change to `MelodyPatternData`.
- **D-MIDI3 = A.** Chord detection (M3) is **v1-restricted**: vertical segmentation
  by note-set change, quantized to the grid, quality reduced to the v1 quality
  alphabet (reduce-don't-fail, warning per reduction), matched against the interval
  templates already defined by `ChordQualityResolver`.
- **D-MIDI4 = A.** **Bassline import is out of scope.** No bassline pattern asset
  exists and `BassTrackComposer` ignores pattern overrides in v1 (renders the shared
  progression). A bass MIDI file can be imported as `MelodyPatternData` if desired.
- **D-MIDI5 = A.** Surface is a **panel/button inside the existing editor windows**
  (precedent: the D-L8 clipboard-Import affordance). No dedicated window.

Cross-cutting contracts every phase must honor:

- Importers are **pure functions** in `Editor/` (`MidiGenPlay.Authoring`), no Unity-API
  calls in the parse path, EditMode-testable — same mold as
  `DrumPatternEditorImporter` / `ChordProgressionRuntimeImporter`.
- Results are applied to the **working copy only**; the asset mutates only via
  Apply / Save As.
- **No silent fallback**: every lossy step (off-grid snap, collision, dropped note,
  unmapped instrument, quality reduction) emits a warning through the window's
  warning surface.
- The importer assumes reasonably quantized MIDI; it snaps and **warns** when snap
  error exceeds a threshold — it does not attempt swing/feel interpretation.
- DryWetMidi (already a package dependency) is the only MIDI-parsing dependency.

---

## Phase M1 — MIDI → DrumPatternData

**Status: CLOSED (2026-07-19).**

**Closure note:** all deliverables landed; 11 EditMode tests green and a
real-MIDI import verified 100% in-editor. `DrumMidiImporter` +
`DrumMidiImporterTests` + the editor's MIDI panel shipped; governed doc updates
applied at closure (rhythm SSoT §3A/§4/§9, tools SSoT §3.A/§10). No runtime code
touched. A render-time follow-up (percussion note fallback when the kit lacks the
exact GM member) is tracked separately as PERC-FALLBACK-1, governed by
`runtime/SSoT_Composer_Rhythm_Track.md`. Next in this arc: **M2** (melody), which
supersedes `Roadmap_Melody_Authoring_MVP.md` Phase D1.

### Deliverables

- `Editor/DrumMidiImporter.cs` — pure function `Import(MidiFile, Options) → Result`.
  - Note-number → `GeneralMidiPercussion` reverse map built from DryWetMidi's own
    GM authority (`AsSevenBitNumber`), never a hardcoded offset.
  - Channel filter: GM drum channel (ch 10) by default; "all channels" option.
  - Target grid = user's current Timing controls (`TimeSignature`, subdivisions);
    grid-beat conversion is beat-unit aware (6/8 grid beat = eighth, matching
    `GetBeatSpan` semantics).
  - Measures derived from content (capped) or explicit (truncate + warn).
  - Per-step velocity preserved; lane `defaultVelocity` = modal velocity; steps at
    the default use the `StepState` velocity-0 sentinel (canonical compression).
  - Same-lane same-step collisions keep the higher velocity + warn.
- `Tests/Editor/DrumMidiImporterTests.cs` — EditMode tests against in-memory
  DryWetMidi files (happy path, channel filter, unmapped note, off-grid snap,
  collision, explicit-measure truncation, 6/8 beat-unit conversion, empty input).
- `Editor/DrumPatternEditorWindow.cs` modified — "MIDI File Import" panel:
  drum-channel toggle + "Import MIDI File…" button + warning list. Applies the
  result to the working copy in **Grid mode** (per-step velocities are arbitrary;
  text glyphs would snap them to tiers — Grid preserves fidelity; the asset stays
  canonical per the Phase 7 text-is-a-view principle).

### Definition of done

- Importing a GM drum MIDI configures the grid (TS/measures/subdivisions synced to
  the Timing controls), builds lanes ordered by GM note number, and populates steps
  with exact velocities — all in the working copy, nothing written until Apply.
- All lossy events surface as warnings in the panel; zero silent fallbacks.
- EditMode tests pass; no runtime code touched.
- Governed doc updates identified and applied (see below) before batch close.

### Doc updates at M1 closure

- `authoring/SSoT_Authoring_Rhythm_Patterns.md` — new import path (MIDI file →
  grid), its warning taxonomy, and the Grid-mode apply rationale.
- `authoring/SSoT_Authoring_Tools.md` — `DrumPatternEditorWindow` capability list.
- `changelog-ssot.md` entry; `ssot_manifest.yaml` if doc set changes.

---

## Phase M2 — MIDI → MelodyPatternData (= Melody Roadmap Phase D1)

**Status: CLOSED (2026-07-23).**

**Closure note:** all deliverables landed; 20 EditMode tests green and a
real-MIDI import verified in-editor, including a render pass through
`ComposeFromPattern`. The smoke confirmed degree/timing/duration fidelity and an
honest, actionable warning list, and confirmed that the pattern carries no
absolute register: the rendered line is transposed by the difference between the
importer's reported reference octave and the instrument's mid register (recorded
as a limitation in the tools SSoT, §8.2b). The reference-octave clamp did not
fire. `MelodyMidiImporter` + `MelodyMidiImporterTests` + the
melody editor's MIDI panel shipped; governed doc updates applied at closure
(melody SSoT §5/§8, tools SSoT §3.A). No runtime code touched. Decisions
M2-D1..D6 locked (below). Next in this arc: **M3** (chords).

Sketch (per `Roadmap_Melody_Authoring_MVP.md` Phase D1 + D-MIDI1/2):
`MelodyMidiImporter` pure function; user specifies root + `Tonality`; pitch →
(degree, octaveOffset) via the package interval tables; chromatic → nearest-degree
snap + warning; polyphony monophonized (highest note wins + warning); timing
quantized to beats (melody timing is beat-absolute). Button + key fields in
`MelodyPatternEditorWindow`. At M2 open, flip Melody Roadmap Phase D1 from
"Deferred" to "Superseded by Roadmap_MIDI_Import M2".

## Locked decisions — Batch M2 (2026-07-23)

These refine D-MIDI1/D-MIDI2 for the melody importer; they do not alter D-MIDI1..5.

- **M2-D1 = A.** The import key root is a DryWetMidi `NoteName` in `Options` and an
  enum popup in the UI — the same type the runtime resolution seam
  (`ResolvePatternNotesCore`) takes, so no conversion layer exists.
- **M2-D2 = A.** The reference octave for `octaveOffset` is **auto-centered**: the
  modal absolute scale octave across the imported notes (tie → lower) becomes
  offset 0. Not user-selectable; reported in the panel for traceability. Rationale:
  the true reference is the runtime instrument register, unknown at authoring time.
- **M2-D3 = A.** Multi-track/channel files are handled by an `Options` channel
  filter (default: all channels) plus a `ChannelsMerged` warning listing per-channel
  note counts. Track-index filtering is deferred until a real file demands it.
- **M2-D4 = A.** Monophonization: simultaneous notes keep the **highest pitch**;
  partially overlapping notes are **truncated** at the next note's start (both
  onsets survive). Applied after onset quantization.
- **M2-D5 = A.** Note duration is preserved and **quantized** to the subdivision
  ladder with a one-step floor; flooring warns.
- **M2-D6 = A.** Chromatic-snap ties resolve **downward** in pitch (consistent with
  the package's other lower-value tie-breaks). In the seven v1 modes every chromatic
  note is such a tie, so this is the operative rule.

## Phase M3 — MIDI → ChordProgressionData (restricted)

**Status: CLOSED (2026-07-23).**

Delivered: `ChordMidiImporter` (`Editor/`, pure function, M1/M2 mold) +
`ChordMidiImporterTests` (25 EditMode tests, in-memory DryWetMidi files) +
"MIDI File Import" panel as a partial of `ChordProgressionEditorWindow`
(`ChordProgressionEditorWindow_MidiImport.cs`, mirroring the LLM-panel partial
pattern) with a one-line OnGUI hook; the panel surfaces Grid Subdivisions (the
import resolution) and an "Analyze File (log)" diagnostic button backed by
`ChordMidiImporter.DescribeChordTimeline` (per-segment timeline sharing the
Import cascade; Console + clipboard; read-only). Import fills the GRID working state only;
Apply/Save As remains the only asset write path. Smoke: real `.mid` chord file
imported 2026-07-23.

### Locked decisions — Batch M3 (2026-07-23)

- **M3-D1=A** — quantize note starts/ends to the step grid FIRST, then segment
  on maximal runs of identical sounding pitch-class sets. No tolerance knob;
  the grid absorbs strums/humanization; residues fall into the D3 threshold.
- **M3-D2=A + D2b** — chord roots resolve to (degree, `degreeAccidental`
  −1/0/+1) relative to the user key; covers all 12 pitch classes in all seven
  modes (no chromatic snap copied from M2). Double spellings prefer FLAT
  (degree above, lowered). `RootSnapped` guard retained but unreachable in v1.
- **M3-D3=B** — channel filter + fixed `MinChordPitchClasses = 3` (no knob);
  sub-threshold segments → warned gap (runtime sustains the previous chord).
- **M3-D4=A** — the window's Timing controls are the meter authority; importer
  Options take the window `timeSignature` + Grid `subdivisions` (clamp 1–8);
  on apply `gridBeatsPerMeasure` aligns to the time signature.
- **M3-D5** — deterministic matching cascade: bass-root exact → all-member-root
  exact (single winner silent; inversion = documented limitation, M2
  octave-loss precedent) → multi-match tie-break (diatonic, fewest voices,
  lowest root pc, enum order) with informative warning → reduction to the
  largest contained template with explicit warning (ties: diatonic, bass root,
  lowest root pc, enum order) → unmatched warned skip. Consecutive identical
  harmonic identities coalesce across gaps/re-strikes.
- **M3-D6** — event velocity = rounded mean of contributing note velocities,
  clamped 1..127.

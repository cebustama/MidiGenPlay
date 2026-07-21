# CURRENT_STATE

## Active now

- **No batch currently active** (MGP-MIX-1 closed 2026-07-20 — consumer-side
  mix gain seam; MGP-BAGGAGE-1 closed the same day — catalogue cleanup. Both
  ship in package **1.2.0**: the 1.1.0 bump BAGGAGE-1 planned was never
  materialized in `package.json`, so the version goes 1.0.0 → 1.2.0 in a single
  jump and 1.1.0 does not exist). Next candidates, in no committed order:
  **volume01 authoring** of the 70 instruments (blocked on ALWTTT D-CSV-18
  listening verdicts), **MGP-ALWTTT-BASSFILL-1** (recalibrated to a robustness
  gap: warn-on-short-progression preferred over auto-fill; D-CSV-23 moves
  ALWTTT's progression standard to 8 bars), and the **PatchName/PatchIndex
  hygiene check** on the instrument catalogue. Earlier: MGP-ALWTTT-DBG-4+2
  closed 2026-07-17, completing the **composition-debug arc package half** —
  DBG-1+3 + DBG-4+2 both done; the only remaining arc work is the single ALWTTT
  consumer session, driven by the DBG-4+2 handoff. BPM-DET-1 + CA-T2 closed
  2026-07-16; tests green, docs applied. The **Chord Articulation (CA) arc**
  (`planning/active/Roadmap_Chord_Articulation.md`) has **CA-T1** (Tier-1 engine),
  **CA-F2** (monophonic bass consumer), **MGP-ALWTTT-ARTIC-1** (Random selection
  policy — seeded variation part 1), and now **CA-T2** (Tier-2 voicing-reshaping:
  power chord + chugging) DONE. Remaining in the arc: **CA-V1 part 2** (seeded
  velocity jitter + randomized arpeggio-rate variety) and the **Tier-2 bossa
  bass/upper split**, spun out of CA-T2 (needs register-selective emission the
  pitch-preserving articulator lacks). **BPM-DET-1** is also now closed: the
  full-song tempo roll is seeded (`ResolveTempoSeed`/`RollTempoBpm`) and
  `PartConfig.ExplicitBpm` is a live reader — completing the SEED-1 story the
  SMOKE-MT arc surfaced (finding C1; VL-DET-1 had fixed only the voicer half).
  Recently closed on other arcs: **SMOKE-MT Stages 1–2** (multi-track
  composition smoke, editor + runtime twin, byte-identical parity), the runtime
  fixes **MEL-NULL-1** and **VL-DET-1**, the **Melody Authoring MVP** (phases
  1–5, 2026-06-22), **MGP-ALWTTT-SEED-1** and **CQ-A1-OBJ2** (2026-07-05).
  Still in flight cross-project: **melody card-pattern integration
  (D-MEL-INT1)** — package half implemented; ALWTTT half (fold the pattern GUID
  into `trackInputsHash`; set the card field) + a joint card-path smoke pending,
  tracked on the ALWTTT side. Other open threads: D-L4.3 generic unification,
  and palette/seed-library expansion.

## Just completed

- Closed **MGP-MIX-1** (2026-07-20, package **1.2.0**): consumer-side mix gain.
  Per-render `mixGains` map on `GenerateSinglePart`, keyed `MusicianTrackKey`;
  one CC7 per entried melodic track,
  `clamp(round(volume01 × gain × 100), 0, 127)`; per-entry emission gate ⇒ no
  entry = bit-identical to the pre-MIX-1 render; Rhythm warn+ignore in v1
  (shared ch9); readback `PartRender.appliedCc7ByTrack`; 8 new tests
  (`SongOrchestrator_MixGainTests`); handoff to ALWTTT filed under
  `reference/cross-project/ALWTTT/Handoff_MGP_MIX_1.md`. Deterministic by
  construction (no RNG, no seed-chain involvement).
  `MidiGenerator.ApplyChannelVolume` gains its first package-side call site.
  `GenerateSong` unchanged in v1. volume01 authoring deferred (D-MIX-6).

- Closed **MGP-BAGGAGE-1** (2026-07-20, ships in package **1.2.0**):
  documentation/maintenance
  batch answering an ALWTTT inventory request. **32 dead assets retired** from the
  shipped catalogue — 8 `ChordProgression-Default*` (6 empty, 2 with `Measures=0`),
  8 `DrumPattern-Default*` (7 lane-less, 1 all-silent), 12 empty melody patterns, and
  2 test palettes carrying production-looking names — plus `Melodic Style - Test 1`
  and `Test Progression`. All sixteen `*-Default*` assets serialized
  `TimeSignature=FourFour`, the enum's zero value: never authored, not
  mis-authored. The stakes were higher than "unreferenced": composers resolve
  patterns by explicit reference, but `PatternRepositoryResources` publishes
  everything under `Patterns/{Chords,Drums,Melodies}` via `Get*(TimeSignature)`, so a
  consumer-side selector could draw an unplayable asset. **`Chord Progressions/`
  moved out of `Resources/`** to `Samples/ExampleCatalogue/ChordProgressions/`
  (D-BAG-2=A) and the emptied source root deleted: it was a second, older catalogue
  root, orphaned from both the runtime
  repository (`Patterns/Chords`) and the catalogue wizard (`Assets/Resources/...`) —
  the package-side half of ALWTTT's D-CSV-14 scan-root mismatch. `Patterns/{Chords,
  Drums}/Palettes` stay as empty canonical enumeration roots (DBG-2 contract). The
  three `_*List.asset` containers are kept and emptied (D-BAG-4=A; contents verified
  2026-07-21). No
  code, no runtime semantics, no contract changes. Two follow-ups spun out:
  **MGP-MIX-1** (consumer-side mix gain composing with the package-side `volume01`,
  D-BAG-3=A — now closed) and authoring the 70 `volume01` values, which are all still
  at the 1.0
  default. Handoff back to ALWTTT under
  `reference/cross-project/ALWTTT/Handoff_MGP_BAGGAGE_1.md`.

- Closed **MGP-ALWTTT-DBG-4+2** (2026-07-17): the remaining package half of the
  composition-debug arc. **Ask D / DBG-4 (D-DBG4=A, E-4/E-5=A):** new runtime
  `ChordProgressionRuntimeImporter` (`MidiGenPlay.Composition`, Runtime asmdef) —
  the setup-card + fenced-Roman grammar RELOCATED verbatim from the editor
  importer (pure regex), plus the builder `TryParsePayload` / `TryParseRoman`
  (RomanProgressionParser → RhythmGridQuantizer → ChordQualityResolver;
  never-persisted `HideFlags.DontSave` instance; `name` stamped
  `"Runtime: <roman>"` for by-name readback; measures mismatch = warn,
  durations win; quantization failure = hard fail). The D-L4.5 zero-warning
  guard (suffix allowlist + `TryFindForbiddenToken`) also relocated
  runtime-side; `ChordProgressionEditorImporter` is now a thin forwarder and
  `ChordProgressionLLMResponseHandler`'s guard delegates — one grammar, one
  alphabet, no drift (test-pinned parity). Grammar note now pinned: bare `7`
  is literal `Dominant7` regardless of Roman case. 11 new EditMode tests
  (`ChordProgressionRuntimeImporterTests`). **Ask B gaps / DBG-2 (E-1=A
  tree-confirmed, E-1b=A, E-2=A, E-2b=A, E-3=A):** closed as a DOCUMENTED
  contract, zero new code — palettes (Drums/Chords) and phrase vocabulary
  enumerate via `TrackPatternConfigStoreResources<T>` over canonical
  `Patterns/Drums/Palettes`, `Patterns/Chords/Palettes`, `Patterns/Phrases`
  folders; two manual asset migrations recorded (legacy chord `Test Palette`,
  legacy `Phrases/` folder); `IPatternRepository` untouched (patterns read
  path only). No composer/RNG/asset touched; runtime gains no editor
  dependency. Doc homes: authoring chord SSoT §4.2, backing SSoT §2.2, rhythm
  SSoT §3D addendum, melody SSoT §4 addendum.

- Closed **MGP-ALWTTT-DBG-1+3 (package half)** (2026-07-17): the composition-debug
  return contract for ALWTTT. **D-DBG1=A** re-keys every per-track `PartRender`
  surface + `instrumentOverrides` to `MusicianTrackKey (musicianId, TrackRole)`
  (fixes the BASS-1 collision where one musician in two roles dropped a stem);
  the track tag becomes `mus:{id}:{role}` (**ID-1=A**). **Ask A / D-DBG2=A,
  D-DBG3=A**: `GenContext.ReportResolved` per-track sink (swap/restored like
  `ctx.rng`), source identity by pre-clone asset name (no runtime GUIDs),
  populating `PartRender.resolvedByTrack` per role (rhythm style id; backing
  roman + Random figures; melody archetype-per-span list; bass shared-progression
  flag). Harmony out of v1 (**ID-2=A**). **Ask C / D-DBG4=A**: trailing
  `patternOverrides` on `GenerateSinglePart` → `GenContext.patternOverride`,
  **precedence step 0** in rhythm/backing/melody (clone-on-apply; type mismatch
  warn+ignore); Bassline warn+ignore (override Backing instead). **`chd:`
  promoted** to a governed marker contract with grid-site vs `RenderFromProgression`
  parity (accidental handling aligned, guarded so accidental-free output is
  bit-identical). BC gate: no override + no seed ⇒ bit-identical (FNV goldens);
  `ctx.rng` draw order intact. New `CompositionReadback.cs`
  (`MusicianTrackKey` / `ResolvedTrackChoice` / `ResolvedSource` /
  `PatternPickInfo`). Governed by `SSoT_Runtime_Generation_Orchestration.md` §5.3
  + `SSoT_Composer_Backing_Track.md` §2.1/§8.5 + rhythm/melody/bass precedence;
  new manifest invariant. Consumer half (`MidiMusicManager` boundary flatten,
  `TODO(BASS-1)`) landed ALWTTT-side. Tests green
  (`SongOrchestratorKeyingTests`, `PatternOverrideAndReadbackTests`,
  `ChordMarkerParityTests`). **Open:** Rhythm render-level override test (needs a
  `MIDIPercussionInstrumentSO` mapping fixture); the step-0 logic is covered by
  the twin backing/melody tests.

- Closed **CA-T2 (Tier-2 voicing-reshaping figures)** (2026-07-16): Tier-2 figures
  mutate pitch, so they cannot live in the pitch-preserving CA-T1 articulator — a
  new pre-articulation seam `IChordReshaper`/`ChordReshaper`
  (`Composition/Interfaces/` + `Composition/Articulation/`) reshapes the voiced
  list between `VoiceChord` and `Emit` at BOTH emission sites (D-T2-SEAM=B; voicer
  owns register/inversions, articulator owns rhythm, reshaper owns the pitch
  reduction). `ChordExpressionType` gains `PowerChord = 7` (drop the third → root
  + fifth + octave; `Block` rhythm) and `Chugging = 8` (same reshape re-struck at
  `arpeggioRate` via the articulator's new pitch-preserving `ChordPulsePlan` —
  D-T2-RHYTHM=A overloads `arpeggioRate`, no new field). **D-T2-SCOPE=A** ships
  power chord + chugging; the **bossa bass/upper split is deferred** (needs
  register-selective emission). Tier-1/`Block`/`Random` paths are byte-identical
  (reshaper is identity); the articulator degrades a leaked `PowerChord`/`Random`
  to `Block` and renders `Chugging`. Single unconditional `Emit` preserved at both
  sites; `lastVoicing`/first-chord stash keep the full voicing. **D-T2-PIN=A**
  (reshape runs after the §7 pin — §7.5) and **D-T2-POOL=A′** (Tier-2 stays out of
  the §8.5 Random pool and is not weight-admissible; `BuildWeightTable` ignores
  value ≥ `ConcretePoolSize`) locked. Governed by
  `runtime/SSoT_Composer_Backing_Track.md` §8.6 + §7.5 + Update triggers; new
  manifest invariant + two `governs:` additions (and the `IChordVoicer.cs` governs
  path corrected to `Composition/Interfaces/`); tests green
  (`ChordTrackComposer_ArticulationTests.cs` extended).

- Closed **BPM-DET-1 (seeded `GenerateSong` tempo roll + live `ExplicitBpm`)**
  (2026-07-16): the full-song tempo was rolled by an unseeded `new System.Random()`
  inside `MusicTheory.GetBPMFromRange`, so the same seed produced a different tempo
  each render — the last SEED-1 gap from the SMOKE-MT arc (finding C1; VL-DET-1 had
  fixed only the voicer half). Tempo now resolves `bpmOverride ?? PartConfig.ExplicitBpm
  ?? seeded-roll` (**D-BPM1=A** — `ExplicitBpm` flipped written-never-read → live
  reader on both `GenerateSong` and `GenerateSinglePart`); the seeded roll picks from
  `MusicTheory.GetValidBpms(range, rule)` via
  `System.Random(SongOrchestrator.ResolveTempoSeed(baseSeed, partIndex))` in
  `SongOrchestrator.RollTempoBpm` (**D-BPM2=A** dedicated FNV-1a substream,
  **D-BPM2-KEY=A** keyed on part-occurrence — no `rep`, so repeated part indices
  share a tempo; **D-BPM3=B** seed policy in the orchestrator, `GetBPMFromRange`
  left byte-identical and off the render path — its `ChordTrackComposer`
  `BeatsPerMeasure` callers unaffected). `bpmOverride`/`ExplicitBpm`-hit paths stay
  MIDI-byte bit-identical (golden); the tempo roll moves unseeded → seeded (no
  pre-seed baseline; determinism asserted instead). Governed by
  `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.1 + new §5.2 + Update
  triggers; SEED-1 manifest invariant extended; `SongOrchestratorSeedTests.cs`
  extended; all green.

- Closed **VL-DET-1 (seeded voicer start-register)** (2026-07-15): the chord voicer's
  first-chord starting-register draw, under a random
  `VoiceLeadingConfig.StartRegisterMode`, used the global unseeded
  `UnityEngine.Random`, so renders with the same seed diverged (the editor smoke
  window vs the runtime runner) and no two runs were reproducible — a hidden breach
  of the SEED-1 "no self-generated per-render entropy" invariant. `TargetOctave`
  now draws from the part's deterministic `ctx.rng`, threaded in via a new optional
  trailing `System.Random rng` on `IChordVoicer.VoiceChord` (both
  `ChordTrackComposer` call sites pass `ctx?.rng`). Non-random start modes and a
  null `rng` stay **bit-identical**; only the two random modes change
  (non-deterministic → seeded). Governed by `runtime/SSoT_Composer_Backing_Track.md`
  §7.4 + Update triggers; new manifest invariant; no `governs:` change. Surfaced by
  the SMOKE-MT parity work; **BPM-DET-1 remains open** (a separate unseeded roll).

- Closed **MEL-NULL-1 (phrase-planner null contract + missing-palette early-out)**
  (2026-07-15): a melody track with no authored pattern, no card, and no phrase palette
  on `MidiGenPlayConfig.melodicLeading` used to abort the **entire song render**
  with an NRE. **MEL-NULL-1 = A + C**: the planner now returns an **empty slot
  list** (never null), the usable-palette test lives once in
  `PhrasePlanner.HasUsablePalette` (leading + palette + ≥1 archetype), and the
  composer gates on it, returning an **empty melody track** so the rest of the song
  renders. Determinism untouched. Governed by `runtime/SSoT_Composer_Melody_Track.md`
  §4 + §8; new manifest invariant; no `governs:` change. Surfaced by the SMOKE-MT
  harness's first four-role render.

- Closed **SMOKE-MT (multi-track composition smoke, Stages 1–2)** (2026-07-15): the
  package smoke harness grew from one track to a whole song, and gained a runtime
  twin. `CompositionSmokeWindow` (Editor) assembles any distinct-role subset of
  Rhythm / Backing / Melody / Bassline — each with its own instrument, pattern and
  card-config asset — and renders the combined `.mid` without the consuming
  project; `CompositionSmokeRunner`
  (`Runtime/CoreScripts/Composition/Smoke/CompositionSmokeRunner.cs`, a
  `MonoBehaviour`) renders the **same** song in Play mode / on device via the same
  `GenerateSinglePart` entry and the same editor-free assembler
  (`SmokeSongConfigAssembler.cs` + `SmokeTrackSpec.cs`), exporting to
  `Application.persistentDataPath` (**D-SMOKE-RT-1 = A** — the package has no
  runtime playback seam). Both surfaces read one shared `SmokeSetupSO`
  (**D-SMOKE-RT-5 = A**; a promoted `SmokeEntry` row type) so their inputs cannot
  drift, and share the no-asset articulation fallback + metronome strip via
  `SmokeRenderUtil` (**D-SMOKE-RT-2 = B**, **-3 = A**); the runner adds seeded,
  runner-only Root/BPM range randomization (**D-SMOKE-RT-4 = A**). Render entry is
  `GenerateSinglePart(…, bpmOverride, …, seedOverride)` because `GenerateSong`
  ignores `PartConfig.ExplicitBpm` and rolls a random BPM from `TempoRange` (see
  BPM-DET-1, open). **Parity verified byte-identical** window-vs-runner under a
  fixed seed; the run surfaced (and this arc fixed) MEL-NULL-1 and VL-DET-1
  (separate entries). Editor + dev/test infrastructure; no governed runtime
  semantics changed by the smoke tooling itself, no SSoT/manifest `governs:` change
  (D-SMOKE-DOC-1 = A, six files).

- Closed **MGP-ALWTTT-ARTIC-1 (randomized chord articulation)** (2026-07-15):
  `ChordExpressionType.Random = 6` (append-only sentinel) resolved
  composer-side per chord event by new
  `Composition/Articulation/RandomArticulationRoller.cs` from a dedicated
  stream `ResolveArticulationSeed(ctx.trackSeed)` (new SEED-1 seam +
  `GenContext.trackSeed`, swap/restored in `GenerateOne`); `ctx.rng`
  untouched (voicings never shift on Fixed<->Random). Card knobs:
  `randomRerollChance` (1 = per chord, 0 = per render/loop via host
  seedOverride) and `randomFigureWeights` (entries define the pool;
  degenerate => uniform fallback + warning). Articulator stays RNG-free;
  leaked `Random` degrades to `Block` (bassline cards, D6). 15 EditMode
  tests in `Tests/Editor/ChordTrackComposer_RandomArticulationTests.cs`.
  Consumer surface communicated to ALWTTT for its adoption rider
  (boundary §8.x). Decisions D1..D6, SD-1..3 locked. Smoke PASS via
  `CompositionSmokeWindow` (BC, determinism/held-loop replay, seed variance,
  per-chord roll, `ctx.rng` isolation). Still pending in the CA arc: Tier-2
  figures, randomized arpeggio-rate.

- Closed **CA-F2 (monophonic bass articulation consumer)** (2026-07-15): the bass
  is now a consumer of the shared CA-T1 engine — its SINGLE emission site
  replaces the legacy `MoveToTime`+`Note` pair with one unconditional
  `IChordArticulator.Emit(...)` call carrying a 1-note voicing (SD-F2-1=A;
  1-note `pb.Chord` ≡ legacy `pb.Note`, byte-pinned; EmitMono contingency on
  record). New `BasslineCardConfigSO.chordExpression`/`.arpeggioRate`
  (SD-F2-4=A, D-EXP1=A persistent; SD-F2-5=A fully independent of the backing
  card — non-bass bundles in the Style slot are ignored). Figures apply over
  the per-event selected note (SD-F2-2=A): arpeggios = repeated-note pulse
  (Up≡Down on 1 note). Note-selection loop and its per-event ctx.rng draw
  order untouched (determinism surface). Meter authority adopted (SD-F2-3=B):
  bass now emits on the Part beatSpan/beatsPerBar — bit-identical in all
  beat-unit==4 meters; in others a deliberate, test-pinned sync FIX of the
  legacy unconditional-Quarter desync (deviation on record in the bass SSoT).
  New `runtime/SSoT_Composer_Bass_Track.md` (bass had no SSoT; SD-F2-6=A).
  9 EditMode tests in `Tests/Editor/BassTrackComposer_ArticulationTests.cs`.
  Factory and `ITrackComposer` unchanged. Still on record: single-pass
  (no repeat-to-fill), normalization-order hazard, `degreeAccidental` ignored.
  Next in arc: CA-T2 (Tier-2 voicing-reshaping), then CA-V1 (seeded variation,
  incl. chord-tone-walk candidate).

- Closed **CA-T1 (Tier-1 chord articulation engine)** (2026-07-15): new
  post-voicing articulation seam `IChordArticulator`/`ChordArticulator`
  (`Composition/Interfaces/` + new `Composition/Articulation/`) invoked by the
  SAME unconditional call at BOTH chord emission sites (grid path +
  `RenderFromProgression`); `ChordExpressionType { Block, PerBeat, Offbeat,
  Staccato, ArpeggioUp, ArpeggioDown }` + `ArpeggioRate { PerBeat, Eighth,
  Sixteenth }` (`Composition/Data/ChordExpressionType.cs`); persistent
  card-level selection `BackingCardConfigSO.chordExpression` /
  `.arpeggioRate` (D-EXP1=A — not a transient hint). Block default is
  MIDI-byte bit-identical (test-pinned). RNG-free pure accent curve
  (×1.00/×0.85/×0.80, clamp 1..127; Block keeps legacy 0..127) — `ctx.rng`
  deliberately untouched (shared-stream hazard). Never-silent Block-degrade
  for unfittable figures; meter-anchored figure math (Part
  beatSpan/beatsPerBar). Internal pure `PlanHits` test seam; 16 EditMode
  tests in `Tests/Editor/ChordTrackComposer_ArticulationTests.cs`.
  Decisions D-PRIO=A, D-EXP1=A, D-EXP2=Tier1, SD-1..5 locked. Next in arc:
  Feature 2 (monophonic bass consumer), Tier 2 (voicing-reshaping), seeded
  variation (incl. randomized arpeggio-rate variety).

- Closed **CQ-A1-OBJ2 (per-chord inversion voicing hint — pin)** (2026-07-05):
  lifted the "Chord inversions — DEFERRED" item by building the CQ-A1 Objective 2
  recommendation in the voicing layer. `PartConfig.ChordInversionHints :
  IReadOnlyList<int?>` (transient, `[NonSerialized]`, snapshot-and-cleared by
  `ChordTrackComposer.Compose` like the §6 modulation hint) pins per-chord
  inversions, index-aligned to the rendered progression's events. **D0 = A**
  (pin, not bias: a valid pin yields exactly one candidate rotation, outranking
  `useInversions`/`useDrop2`; pinning `0` forces root position and is not the
  same as unset), **D1 = A** (inversion index, not bass pitch-class), **D2 = A**
  (per-chord), **D2a = a** (sticky-per-position: recurs on every pattern repeat
  within the render), **D2b = a** (out-of-range value = safe no-op, never
  clamped), **D3 = A** (§6 directional hint wins the render's first chord —
  structural in both render loops). Enforced in
  `BasicVoiceLeadingVoicer.GeneratePcCandidates` (now `internal`, the test
  seam); `IChordVoicer.VoiceChord` gained an optional trailing
  `forcedInversion`; both chord render loops thread
  `ChordTrackComposer.ResolveInversionPin` (new internal helper). Default-unset
  is bit-identical. New `Tests/Editor/ChordTrackComposer_InversionPinTests.cs`
  (baseline candidate-set identity, exact-rotation pins, out-of-range no-ops,
  D2a sticky test, D3 combined-hint precedence at the seams). Governed by the
  new `runtime/SSoT_Composer_Backing_Track.md §7` (update triggers renumbered
  to §8); registered in `SSoT_Runtime_Song_Model_and_Config.md §1.1`;
  `Roadmap_Chord_Expressivity` "Chord inversions" flipped DEFERRED → BUILT.

- Closed **PATTERN-PERSIST-1 (pattern-asset persistence unification)** (2026-07-05):
  all three pattern editors (`DrumPatternEditorWindow`, `ChordProgressionEditorWindow`,
  `MelodyPatternEditorWindow`) now persist through the shared, previously-unused generic
  store `TrackPatternConfigStoreResources<T>` instead of ad-hoc `AssetDatabase` calls +
  per-window hardcoded folder constants. Two members were added to the store: a public
  `AssetsSaveRootPath` accessor (D4) and an editor-only `PersistNewAtPath(instance, path)`
  method (**D6 = C** — the window keeps its interactive Save dialog, the store owns the
  `AssetDatabase` write; the only option satisfying both). Drum's save root is
  byte-identical (`.../Patterns/Drums`); Chord gained a real default folder for the first
  time (`.../Patterns/Chords`) across all four of its internal save sites (Roman
  apply/create, grid apply, Save-As-New Roman + grid); Melody realigned from a singular
  `.../Patterns/Melody` write folder to the plural `.../Patterns/Melodies` that
  `PatternRepositoryResources` reads and the shipped assets live in (**D5 = A** — closes a
  latent editor-writes-vs-repo-reads split; no stray `/Melody` assets to migrate). Each
  editor also gained an additive, canonical-root "Browse Saved Patterns" list (**D3 = A**).
  `IPatternRepository` / `PatternRepositoryResources` remain the runtime **read** path
  (not extended, **D1** — the store was the correct write path, though the roadmap/tools
  docs had named the repository). Determinism untouched; no runtime/composer surface
  changed. This closed **Phase 8** of `Roadmap_Rhythm_Authoring_MVP` and, by explicit
  batch-open widening (**D2**), unified Chord + Melody persistence in the same batch
  (recorded here + changelog; no separate Chord/Melody roadmap entries). Out-of-scope and
  untouched: palette (`*PaletteSO`) persistence, `MelodyGenerationParamsSO` saves, and the
  catalogue wizards.
- Closed **MGP-ALWTTT-SEED-1 (per-render seed threading)** (2026-07-05): both
  `SongOrchestrator` render entry points now accept a caller-supplied seed —
  `GenerateSong(SongConfig song, int? seedOverride = null)` and
  `GenerateSinglePart(part, rolesForChannels, partIndex, bpmOverride,
  instrumentOverrides, int? seedOverride = null)` (D3 = A: optional trailing
  parameter, matching ALWTTT's stateless per-call preference; the
  GenContext-field and MidiGenerator-setter options were rejected — the context
  is built inside the render methods and a setter is stateful/leak-prone). A
  single `baseSeed = seedOverride ?? _settings.defaultSeed` is resolved once per
  render, and all five seed sites (song rep `ctx.rng`, song pass-1 + pass-2
  track seeds, single-part `ctx.rng`, single-part track seed) derive from it via
  new **internal** seams — `ResolveBaseSeed`, `ResolveRepContextSeed`
  (`(base + partIndex*397) ^ rep`, precedence preserved), `ResolvePartContextSeed`,
  `ResolveTrackSeedSong`, `ResolveTrackSeedPart` — with `StableHash32` flipped
  `private → internal` for test access. No seed supplied ⇒ **bit-identical** to
  the prior `defaultSeed`-anchored behavior, guarded by golden FNV-1a regression
  values in the new `Tests/Editor/SongOrchestratorSeedTests.cs`; that fixture
  also mirrors the S5g acceptance at the selector level (distinct seeds ⇒ ≥2
  distinct picks over a 6-entry palette; same seed ⇒ same pick). All tests green.
  Seed **policy** (per-song derivation / rotation) stays host-side; the package
  never invents per-render entropy (D1/D2 locked). **D4 (Pick-chain exclusion)
  declined**: clone-on-pick (drum `Instantiate` inside the pick; chord
  caller-clones per CE-F1) means the host never holds a palette-entry-identical
  reference, so `excludeIfPossible`-by-reference would silently never match, and
  threading a previous-pick identity from host → composer → picker requires a
  GenContext meaning change beyond this batch; ALWTTT ships probabilistic
  no-repeat (palettes ≥ 6) instead. Governed by
  `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.1 (new). Adoption note
  delivered to ALWTTT batch S5g-b; the ALWTTT-side consumption step
  (`MidiMusicManager.RenderSinglePart` seed pass-through + the per-song seed
  policy) is out-of-tree and tracked on the ALWTTT side. D1/D2 locked, D3 = A,
  D4 declined.

- Closed **Melody Authoring MVP — Phase 5 (polish, validation, documentation closure)**
  (2026-06-22): completed the MVP. Validated `MelodyTrackComposer.ComposeFromPattern`'s edge
  cases by code-trace against the shipping method (no runtime change): empty pattern →
  silence/no-crash (`MelodyPatternData.TotalBeats ≥ 1` floors the loop divisor; the empty
  note list emits nothing), single-note, shorter-than-Part → tiles, longer-than-Part →
  truncated by note onset (a boundary-crossing note rings to its authored length),
  `octaveOffset` at the band extremes → clamped to `[octaveMin-1, octaveMax-1]` (the same
  band `ChooseMelodicRegister` uses) with no out-of-range throw; authored duration floored
  at `MinNoteBeats`, velocity clamped 1–127; path RNG-free and byte-deterministic.
  **D-MEL5.1 = A** (meter-mismatch keeps tiles-by-beats + warning as the documented MVP
  limitation; bar-time renormalization is post-MVP) and **closure-scope = A** (editor-side
  round-trip / UX deemed satisfied by the Phase 2–3 closures). Doc sweep applied (runtime
  SSoT §7 + authoring SSoT status/§8 + roadmap + coverage-matrix note + this file +
  changelog + manifest log). No `SongConfig`/`TrackParameters`/`SSoT_CONTRACTS` change.
  **Follow-up — DONE (F-A):** added `Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs`
  testing an extracted byte-identical internal seam (`MelodyTrackComposer.ResolvePatternNotesCore`);
  no contract change, SSoT §7 untouched. D-MEL5.1 locked.

- Closed **Melody Authoring MVP — Phase 4 (runtime hookup, `ComposeFromPattern`)**
  (2026-06-17, smoke-validated in-game): added the authored-melody override branch to
  `MelodyTrackComposer` (analogous to rhythm's `ComposeFromGrid`). When an authored
  `MelodyPatternData` is present, the composer renders it directly — each
  `(degree, octaveOffset)` resolved via `GetNoteFromScale` against the active Part
  tonality/root from the instrument's mid register (clamped to the instrument range), the
  authored loop (`pattern.TotalBeats`) tiled to the Part with quarter-mapped beats (matching
  the procedural path; final loop truncated; `beatsPerMeasure` mismatch warns + tiles),
  velocities clamped, guide notes cached via `ctx.SetMelodyForPartMusician` — and skips
  `PhrasePlanner`/strategies; otherwise the procedural path is byte-identical to before. No
  RNG (deterministic; `ctx.rng` draw order for other tracks unaffected), runtime-only (no
  editor API), no change to `SongConfig`/`TrackParameters` (D-MEL4.1). No tests (DoD requires
  only a manual E2E; passed). Governed by `runtime/SSoT_Composer_Melody_Track.md` §7 +
  `authoring/SSoT_Authoring_Melody_Composition` §7. D-MEL4.1–4.4 locked.

- **Melody card-pattern integration (D-MEL-INT1) — package half implemented** (2026-06-17):
  `MelodyCardConfigSO` gained `patternOverride` (`MelodyPatternData`); `MelodyTrackComposer`'s
  dispatch now reads it ahead of `Parameters.Pattern` (card-wins, mirroring
  `RhythmCardConfigSO.patternOverride`), feeding the same RNG-free `ComposeFromPattern`. No
  `SongConfig`/`TrackParameters` change. **Not yet closed:** pending the ALWTTT half (fold the
  referenced pattern's GUID into `trackInputsHash` so cache doesn't mask it; set the card
  field) + a joint card-path smoke (card pattern plays / no card-pattern → procedural still
  plays / change pattern → new one heard). D-MEL-INT1 locked (Option A — card-carried).

- Closed **Melody Authoring MVP — Phase 3 (generation-params UI + simplified generator)**
  (2026-06-17): added the wizard's generation-parameters top section bound to
  `MelodyGenerationParamsSO` (Tier-1: scale/tonality, GM instrument hint, density, octave
  range, rhythmic style Even/Syncopated/Burst, seed) and `SimplifiedMelodyGenerator`
  (`Editor/`, `MidiGenPlay.Authoring`) — an editor-only, deterministic generator mapping
  those params into a `MelodyPatternData` working copy (density → onsets/measure; style →
  placement; octave range → offset bounds; scale → diatonic degrees, stability-weighted).
  Determinism via `System.Random(seed)`; onset placement is RNG-free, so a new seed re-rolls
  pitch over a fixed groove. `MelodyGenerationParamsSO` gained `seed` + `instrumentHint`
  (the latter informational-only — the pattern carries no instrument and it is never read at
  runtime). Working-copy isolation preserved (asset untouched until Apply/Save As); no
  runtime / `ComposeFromPattern` change (Phase 4). One new Editor-only file,
  `#if UNITY_EDITOR`-guarded; no `Runtime/` leak. Unity green; no tests (Phase-3 DoD requires
  none; manual smoke pass). D-MEL3.1–3.3 locked. Governed by
  `SSoT_Authoring_Melody_Composition` §5. Next: Phase 4 (runtime hookup).

- Closed **Melody Authoring MVP — Phase 2 (ladder note-grid editor)** (2026-06-16):
  shipped `MelodyPatternEditorWindow`, a scene-independent package EditorWindow
  (`MidiGenPlay / Melody Pattern Editor...`) with a scale-degree "ladder" grid
  (Y = 7 diatonic degrees × octave bands, X = time steps), working-copy isolation
  (`DeepCloneRuntime`, asset untouched until Apply/Save As), a per-note inspector
  (degree / octave / start / length / velocity), a configurable octave window, an
  explicit Normalize, and Apply / Save As. Authors `MelodyPatternData` only — no
  runtime change, no generation-params UI, no text/DSL mode. One Editor-only file,
  fully `#if UNITY_EDITOR`-guarded; no `Runtime/` leak. Unity green; no tests
  (Phase-2 DoD requires none; validated by manual smoke pass). D-MEL2.1–2.4 locked.
  Governed by `SSoT_Authoring_Tools` §3.A + `SSoT_Authoring_Melody_Composition` §5.
  Next: Phase 3 (generation-params UI + simplified generator).

- Closed **Melody Authoring MVP — Phase 1 (data model)** (2026-06-16): redesigned
  `MelodyPatternData` to a deterministic per-note model (`MelodyNoteEvent` struct —
  one `ScaleDegree` + octave offset + beat-relative start/duration + velocity;
  pitch resolved at runtime, not stored), added `MelodyGenerationParamsSO` (a
  generation-time-only bundle for the planned wizard), and removed the legacy
  probabilistic `possibleDegrees` model with its sole consumer
  `MidiGenerator.GenerateMelodyTrackWithPattern` (M-3, clean break; two orphaned
  privates + a dead `using` removed too). Both new types are package runtime
  (`Runtime/CoreScripts/Data/` + `.../Composition/Data/`) and are now governed by
  `SSoT_Authoring_Melody_Composition`. Unity green; no tests required (data-model
  swap, procedural path untouched); repo grep clean bar inert comments in the
  demoted `EmotionalGenerationPanel` / `MidiGenPlayPanel`. D-MEL1.1–1.5 locked.
  Next: Phase 2 (ladder note-grid editor).

- Closed **CQ-B1 (chord quality alphabet v2 — Tier B, ninths)** (2026-06-16):
  appended `Dominant9` {0,4,7,10,14}, `Major9` {0,4,7,11,14}, `Minor9`
  {0,3,7,10,14} to `MusicTheory.ChordQuality` (append-only, ordinals 14–16;
  existing serialized assets unaffected). Explicit-suffix-only (`9`/`maj9`/`m9`);
  no change to diatonic inference (`vi9` = dominant-ninth; minor-ninth is `vim9`).
  Lockstep across `RomanProgressionParser`,
  `ChordProgressionLLMResponseHandler.AllowedSuffixes`, the prompt-builder alphabet
  (9/maj9/m9 removed from Forbidden; 11/13/add9/6-9 remain),
  `ChordProgressionEditorWindow.QualitySuffixForToken` + `IsSeventhQuality` (all
  three ninths are sevenths for grid arity), and
  `ChordQualityResolver.GetTriadFamily` (Dom9/Maj9 → Major, Min9 → Minor). Five
  voices, realized via `BasicVoiceLeadingVoicer`; the one runtime change is
  uncapping its inversion loop (`i < 4` → `i < pcs.Length`), byte-identical for
  ≤4-voice chords. New arc roadmap `planning/active/Roadmap_Chord_Expressivity.md`.
  Two test fixtures inverted (`9` is now valid, not forbidden) + three extended;
  full suite green; `Runtime/` grep clean. Known deltas: grid renders ninths as
  4-of-5 rows; five-voice `Drop2` inert and a tall five-voice stack can collapse at
  range edges (pre-existing voicer nuances). D-CQB1.1..5 locked. Only the
  chord-inversion voicing hint remains in the arc.

- Closed **CQ-A1 (chord quality alphabet v2 — Tier A)** (2026-06-16): appended
  `Major6`, `Minor6`, `Dominant7sus4` to `MusicTheory.ChordQuality` (append-only,
  ordinals 11–13; existing serialized assets unaffected). Explicit-suffix-only
  (`6`/`m6`/`7sus4`); no change to diatonic inference (`vi6` = major-sixth;
  minor-sixth is `vim6`). Lockstep across `RomanProgressionParser`,
  `ChordProgressionLLMResponseHandler.AllowedSuffixes`, the prompt-builder
  alphabet, `ChordProgressionEditorWindow.QualitySuffixForToken` (round-trip fix)
  + `IsSeventhQuality` (Decision A), and `ChordQualityResolver.GetTriadFamily`
  (new qualities classify Major/Minor/Suspended for `isDiatonic` rather than
  `Other`). Intervals `{0,4,7,9}`/`{0,3,7,9}`/`{0,5,7,10}` — all ≤4 voices,
  voiced through the existing voicer unchanged. Canonical home = the
  `ChordQuality` enum (authoring SSoT §4.1). Five new EditMode fixtures, full
  suite green; `Runtime/` grep for unguarded `ChordQuality` switches clean. Tier B
  (ninths, 5-voice) + the inversion voicing hint deferred, both gated on
  `Strategies/VoiceLeading.cs`. Known deltas: grid renders the 6th chords as
  3-row triads; `Dominant7sus4` flagged non-diatonic vs a major V (sus-consistent).

- Closed **CE-L1 (LLM card-author)** (2026-06-11): fourth mirror of the LLM
  authoring stack and its first **consumer-side** instance —
  `CardLLMPromptBuilder/Generator/ResponseHandler/FieldPlan` in a new
  ALWTTT-side editor asmdef pair (`ALWTTT.Cards.LLMAuthoring` + `.Tests`),
  plus `CardLLMVocabulary` (live-snapshot POCO, D-CE-L1.4),
  `CardPaletteIntentResolver` (intent → deterministic seeded pick over the
  CE-F1 `PaletteSelector`), `CardImportDtoParser` (DTO hoist shared with the
  JSON box), and a "Generate with LLM" panel in `CardEditorWindow` staging
  through the existing `TryStageCardFromDto`. Banned-asset-reference guard +
  out-of-alphabet guard in the handler (D-L4.5 doctrine); bundle + palette
  written only at Save (D-CE-L1.6); `modifierEffectNames` resolved
  all-or-nothing at staging. 77/77 tests; live smoke green (1572/366 tokens).
  D-CE-L1.1..7 locked. Package itself unchanged (game-side editor code only).

- Closed **CE-F1 (shared palette selector)** (2026-06-10): extracted the TS-aware
  selection policy out of `BackingCardConfigSO` into a shared, deterministic
  `PaletteSelector` (Tier A/B/C) over a neutral `TsFeatures` summary, with typed
  `ProgressionFinder`/`PatternFinder` (`.../Data/PaletteSelection.cs`). Deleted the
  ~250-line reflection + Tier-helper block from the backing config. `RhythmCardConfigSO`
  gained a TS-aware `PickPatternOverride(rng, ts, settings, verbose)` and
  `RhythmTrackComposer` calls it with the Part TS, so the drum side is now TS-aware
  (deferred from PCE). Both palette TS toggles are consumed in the one selector ->
  asymmetry resolved. Drum density = capped foundational-onset (kick) density (D-F1.5).
  New `Tests/Editor/PaletteSelectorTests.cs`. Determinism preserved (one `NextDouble`
  per pick). D-F1.1..5 locked. Known delta: duplicate palette references are now
  independent weighted slots (no de-dup); normal palettes are byte-identical per seed.

- Closed **CE-E1 (Card Editor ergonomics)** (2026-06-10): added a **Clone Card** action
  to `CardEditorWindow` (ALWTTT) that deep-copies the payload and **clones the style
  bundle** into its own asset — fixing the Ctrl+D bug where a duplicated card silently
  shares the source palette — plus New-Card preset buttons (Action / Composition /
  Rhythm / Backing / Melody / Harmony). Preset role hand-off is via name-strings
  resolved against `TrackRole`'s enum names, so it never compile-couples to specific
  roles. Game-side editor change only; no package runtime change.

- Closed **PCE (Palette Consumption / Composition Expressivity)** (2026-06-04):
  wired drum-palette consumption into `RhythmTrackComposer` via
  `RhythmCardConfigSO.patternPalette` + `PickPatternOverride(ctx.rng)`, mirroring
  the backing `PickProgressionOverride` seam (legacy/non-TS picker). Authored 5
  drum-pattern palettes (one pattern each) and wired two 4/4 cards for the §5
  distinctness experiment. Smoke pass green (determinism / consumption /
  distinctness). Backward-compatible: cards with no palette behave as before.
  Drafted the CE roadmap (CE-E1 / CE-F1 / CE-L1).

- Closed LLM Authoring **Batch L5 (L-PAL)** (2026-05-29): DrumPattern palettes +
  editor integration + catalogue wizard. New `DrumPatternPaletteSO` (weighted,
  deterministic seeded `PickRandomPattern`, clone-on-pick; inert TS-aware toggle
  mirroring the chord palette), `DrumPatternCatalogueWizard` (read-only browser;
  derived metadata TS/measures/subdivisions/instruments/active-step density;
  filter/sort/search; ping-on-select), and a palette section in
  `DrumPatternEditorWindow` ("Add to Palette" referencing a saved asset,
  dedup-guarded; project-scan dropdown). 9 palette tests + the 4-item smoke pass
  green. Decisions D-PAL.1..5 locked (1=reference-saved, 2=scan-folders+fallback,
  3=author-only, 4=weighted, 5=SO under rhythm composer SSoT). Author-only for
  now: no runtime path consumes drum palettes yet — composer consumption is the
  declared next phase **(superseded by PCE 2026-06-04 — drum palettes are now
  consumed at runtime; see the top PCE entry)**. Artifacts registered in `ssot_manifest.yaml`; roadmap §L5
  flipped to CLOSED.

- Closed LLM Authoring **Batch L4** (2026-05-29): chord editor generalization —
  the second adopter of the LLM authoring pattern. `ChordProgressionEditorWindow`
  gained Generate/Regenerate/Import mirroring the drum surface, routed through the
  editor's existing `ParseAndPreview`/`ApplyToAsset` path (determinism invariant
  untouched; the chord asset is the seam consumed by `ChordTrackComposer`). New
  artifacts: `ChordGenreVocabularySO` (+ `ChordGenreVocabularyBuilder` seeder,
  v1 set jazz/pop/blues/folk with build-time parser+guard self-check),
  `ChordProgressionLLMPromptBuilder` (Roman-string output, D-L4.1; alphabet
  verified against `RomanProgressionParser`), `ChordProgressionLLMGenerator`,
  `ChordProgressionEditorImporter`, `ChordProgressionLLMResponseHandler` (carries
  the D-L4.5 token-allowlist guard), `ChordLLMFieldPlan` (pure outcome→field
  mapping, D-L4.7), and the `.LLM` window partial (+ a "Create New Progression"
  reset affordance). Key contract clarification (D-L4.5): `RomanProgressionParser`
  warns-and-downgrades unknown suffixes rather than failing, so the
  no-silent-fallback guard lives in the response handler — documented in
  `SSoT_Authoring_LLM_Generation.md` §3.3. 47 chord LLM EditMode tests + smoke
  tests CSMR-S1..S8 pass; full suite green. Decisions D-L4.1..D-L4.8 locked.
  Doc flips applied across manifest, LLM SSoT (§3.3/§7), coverage-matrix,
  changelog, and the roadmap (§"Batch L4" → closed). This completes the LLM
  Authoring MVP through L4.

- Closed LLM Authoring **Batch L3** (2026-05-28): smoke-test sign-off + governance.
  This closes the LLM Authoring MVP (L1–L3). Cost-cap UI (D-L3.1) wired the
  pre-network `maxCharBudget` guard to a per-window "Max prompt chars" field
  surfaced through the warning panel. Mock-client seam (D-L3.2) added
  `FakeLLMClient` + `DrumPatternLLMGeneratorTests` (6 tests) making SMR-L3/L5
  deterministic against the real `PromptExecutionHelper` → `ILLMClient`
  delegation. Full SMR-L1..L7 manual sign-off passed (plain happy-path runs
  clean; `LengthLong` truncation under free-text direction is expected
  contained LLM behavior, not a defect). New SSoT
  `authoring/SSoT_Authoring_LLM_Generation.md` written as a replicable pattern
  and flipped to primary (coverage-matrix); roadmap demoted to L1–L3 history.
  Governed-doc flips applied across manifest, coverage-matrix, Rhythm Patterns
  §3A, Tools SSoT, and the roadmap. All EditMode tests green.

- Closed LLM Authoring **Batch L2** (2026-05-28): editor UI integration. Wired
  the LLM-Assisted Generation panel into `DrumPatternEditorWindow` — genre
  dropdown (from `Default Rhythm Genres.asset`, 8 genres seeded via a new
  `RhythmGenreVocabularyBuilder` menu), async non-blocking Generate (D-L2.3),
  Regenerate (D-L2.4=A), clipboard Import (D-L8), client resolution with
  default + override (D-L2.1=B). New pure-function seams:
  `DrumPatternEditorImporter` (setup-card + DSL parse, fence-agnostic glyph
  detection; 12 tests), `LaneAliasDictionary` (23 short-name aliases; 11 tests),
  `DrumPatternLLMResponseHandler` (unifies Generate + Import into one applicable
  outcome; 5 tests). Generate and Import share one apply path through the
  importer (D-L2.2=A). Importer revised mid-batch from fence-first to
  glyph-content detection (SMR-L6 learning); CRLF split bug fixed (test line
  endings under CRLF). SMR-L1/L2/L4/L6/L7 pass; SMR-L3/L5 deferred to L3. All
  EditMode tests green.

- Closed LLM Authoring **Batch L1** (2026-05-28): vocabulary SO + 11-test
  prompt builder + LLM Core generator + console harness. First clean
  end-to-end run against Anthropic `claude-sonnet-4-6` (972 in / 218 out
  tokens, zero parser warnings, 4-lane funk pattern). Provider switched
  OpenAI → Anthropic mid-batch via a cross-project LLM Core sub-batch; no
  MidiGenPlay code change needed (factory seam). `Default Rhythm Genres.asset`
  full population deferred (completed in L2). D-L1..D-L11 locked; see roadmap.

- Closed runtime micro-batch: `ComposeFromGrid` now consumes
  `SnapshotAsStepVelocities`. Per-step velocity authored in
  `DrumPatternData` reaches generated MIDI for every grid-authored rhythm
  track. `SnapshotAsIndices` retained as a default-velocity-only view but
  no longer called by any runtime composer. 3 EditMode tests lock the
  `SnapshotAsStepVelocities` contract (sentinel resolution, all-off lane,
  multi-lane independence). Closes the deferred runtime gap from Phase 6.

- Closed MGP-ALWTTT-MOD-DIR-1.1 + 1.2 (package side) and ALWTTT-MOD-DIR-3
  (cross-project side). End-to-end directional modulation work now closed
  across both projects.
  - 1.1: directional first-chord anchor moved from notional centerOct to
    actual previous first-chord root pitch held in
    `ChordTrackComposerFactory`'s per-track memory (keyed
    `(part.Name, MusicianId)`). Cold-start fallback to centerOct preserved
    (SM-DIR-3 bit-identical regression baseline). `ChordTrackComposer` ctor
    extended with two optional params (backward-compatible); public
    `PartConfig` surface unchanged.
  - 1.2: §6.2 SMD5 contract reframed as a natural property of the strict
    `>` / `<` comparison rather than a special case. Same-root +
    boundary-clamp documented as a deliberate collapse to silence (Option 1)
    over wrap-on-clamp (Option 2 rejected because wrapping inverts user
    musical intent).
  - ALWTTT-MOD-DIR-3 (cross-project, ALWTTT side):
    `MidiMusicManager.RenderSinglePart` now forces `cacheEnabled = false`
    when either `PartConfig` modulation transient is non-default. Surfaced a
    new package-level SSoT invariant: transients are NOT part of cacheable
    input (`SSoT_Composer_Backing_Track.md §6.5`).
  - 9 EditMode tests cover the directional helper at the `Core` seam,
    including the SM-DIR-1 failure reproduction (C#5 → G#5), Down symmetry,
    range-clamp fallback, Auto short-circuit, and three SMD5 cases (same-root
    Up/Down non-boundary + Up at-top-boundary).
  - Six ALWTTT smoke tests pass (SM-DIR-1..6). SM-DIR-7 (range-clamp
    fallback in scene) deferred — requires a narrow-range debug instrument
    that doesn't exist yet; package-side coverage already locks the contract
    via `Remembered_Up_BeyondTopOfRange_ClampsToMaxOct`.
  - Diagnosis template captured: when scene behavior contradicts
    EditMode-verified package behavior, suspect a consumer-side
    short-circuit above the package entry point before suspecting the
    package itself. The SMD5 failure was traced via log-absence
    ([Mod-DIR/Handoff] not firing) to `MidiMusicManager._partBundleCache`
    keying on serialized fields only.
- Closed Phase 7: text/DSL authoring mode for `DrumPatternEditorWindow`
  - `DrumPatternTextParser` (pure-function parse / render / per-cell-diff apply)
    and `DrumPatternTextWarning` types added in `Editor/`, namespace
    `MidiGenPlay.Authoring`
  - Whole-window Grid / Text tab toolbar in `DrumPatternEditorWindow`; text-mode
    lane row shows read-only "Instrument (vNNN)" + single text field + remove
  - Parse on tab-switch and on Apply; per-cell diff preserves non-canonical
    per-step velocity for cells whose typed glyph hasn't changed
  - 3-tier glyph alphabet (v1): `.` and `-` = rest, `x` = lane default
    (sentinel), `X` = accent (120), `o` = ghost (50); `|` and whitespace
    ignored; short input right-pads with rests, long input right-truncates;
    both length cases emit a warning
  - HelpBox legend at the top of the text pane; warning panel below the lanes
  - Asset model untouched: `DrumPatternData`, `StepState`, working-copy /
    apply / save-as contract unchanged
  - First test assembly in the package: `Tests/Editor/MidiGenPlay.Tests.Editor`
    with 13 EditMode tests covering SMR1 (alternation), SMR2 (glyph→velocity),
    SMR4 (short pad + warn), SMR5 (unknown glyph + warn), plus render snap,
    bar-separators, ignored chars, dash equivalence, per-cell-diff round-trip
  - Three manual smoke tests verified: SMR3 grid↔text round-trip, SMR6
    signature change re-renders text, SMR7 SaveAs preserves text-mode edits
  - D-T9 final placement: flat `Editor/` (matches package convention), namespace
    `MidiGenPlay.Authoring`. Initial namespace `MidiGenPlay.Editor.Authoring`
    shadowed `UnityEditor.Editor` and broke `SoundFontCacheSOEditor`; renamed
- Established package test-authoring conventions
  - `package.json` gains `"testables": ["MidiGenPlay.Tests.Editor"]`
  - Canonical asmdef shape captured in
    `reference/package/Tests_Authoring_HowTo.md` (new), including the
    `defineConstraints: ["UNITY_INCLUDE_TESTS"]` gotcha that silently blocks
    compilation in Unity 2022.3 local-package contexts
- Closed MGP-ALWTTT-MOD-DIR-1: directional modulation hint for `ChordTrackComposer`
  - Added `ModulationOctaveHint { Auto, Up, Down }` enum (default `Auto`)
  - Added two `[NonSerialized]` transient fields on `PartConfig`:
    `PreviousRootNote`, `ModulationOctaveHint`
  - `ChordTrackComposer.Compose` captures and clears the transients on entry;
    both internal render sites apply a shared directional first-chord override
  - Default behavior bit-identical to prior output; determinism preserved
  - Cross-project follow-up (ALWTTT side) tracked via rehydration prompt
    (`ALWTTT-MOD-DIR-2`)
- Implemented Phase 6: `StepState` struct replaces `List<bool>` in
  `DrumPatternData.Lane`
  - `StepState { bool active; int velocity; }` — velocity 0 is the sentinel
    (defer to lane default)
  - `StepState.ResolveVelocity(int laneDefault)` encapsulates effective
    velocity resolution
  - `SnapshotAsIndices()` return signature unchanged — existing runtime callers
    unaffected
  - `SnapshotAsStepVelocities()` added as the per-step-velocity-aware forward
    snapshot
  - `DrumPatternEditorWindow` extended with `[T]`/`[V]` row mode toggle

## Next

1. **CA-V1 part 2 (seeded variation)** — the remaining CA-arc articulation work:
   seeded velocity jitter + randomized per-pattern/per-chord arpeggio-rate variety
   (fork a child rng off the seed; do NOT tap `ctx.rng`). Part 1 shipped via
   MGP-ALWTTT-ARTIC-1; the bass roll wiring (D6 degrade-only) is a one-line rider.
2. **Tier-2 bossa bass/upper split** — spun out of CA-T2: a register-selective
   figure (bass on 1, upper voices off the beat) that the pitch-preserving Tier-1
   articulator cannot express; needs either a register-aware articulator figure or
   a reshaper-owned emit path.
3. **D-L4.3 unification** (optional) — extract a shared generic over the drum and
   chord prompt builders / generators now that two working instances exist.
4. Resume phrasing / feel runtime completion (Phase 9) — now unblocked; Phase 8
   (authoring-tool persistence unification) closed 2026-07-05 by PATTERN-PERSIST-1.
5. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to
   legacy/reference status.

Future (recorded, not scheduled): fill tag system (R3 — runtime/Composer
concern; see roadmap §"Future work").

## Blocked / not implemented yet

- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists
  in the repository

## Docs to update next

- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` — when Phase 8 work begins
- `authoring/SSoT_Authoring_Tools.md` — when LLM authoring surface lands
  (Batch L2 closure), and again when persistence routing changes in Phase 8
- `authoring/SSoT_Authoring_LLM_Generation.md` — created at Batch L3 closure
  (2026-05-28); chord adopter added at L4 closure (2026-05-29, §7) with the
  §3.3 degrade-vs-fail clarification. Primary authority for LLM-assisted
  authoring across the package. Update when the next tool adopts the pattern or
  a §3 contract changes.

## Working rule

If the next technical change touches rhythm generation, rhythm authoring, or
the rhythm editor:

- update the primary rhythm SSoT first
- then update `CURRENT_STATE.md` if active focus or reality changed
- then update `planning/active/Roadmap_Rhythm_Authoring_MVP.md` if the
  next-step sequence changed
- then update `changelog-ssot.md` if semantics or authority changed
- then update `runtime/SSoT_Composer_Rhythm_Track.md` if runtime generation
  behavior changed

If the next technical change touches LLM-assisted authoring (new track as of
2026-05-24):

- update `planning/active/Roadmap_LLM_Authoring_MVP.md` to record batch
  closure and decisions locked
- update `CURRENT_STATE.md` if active focus shifts between batches
- update `authoring/SSoT_Authoring_LLM_Generation.md` (primary, created at L3
  closure 2026-05-28) when a tool adopts the pattern or a §3 contract changes
- update `changelog-ssot.md` per batch with the standard shape
- the coverage-matrix primary home already points at the new SSoT (flipped at
  L3 closure); keep it there unless authority changes

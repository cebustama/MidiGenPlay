# CURRENT_STATE

## Active now

- **No batch currently active** (M3 — MIDI file import for chord progressions —
  closed 2026-07-23, **completing the MIDI Import arc**; M2 — the melody half —
  closed the same day; M1 — the drum half — closed 2026-07-19 and verified
  against a real `.mid`;
  MGP-MIX-1 closed 2026-07-20 — consumer-side mix gain seam; MGP-BAGGAGE-1
  closed the same day — catalogue cleanup. Both
  ship in package **1.2.0**: the 1.1.0 bump BAGGAGE-1 planned was never
  materialized in `package.json`, so the version goes 1.0.0 → 1.2.0 in a single
  jump and 1.1.0 does not exist). **INST-WIZ-1** closed 2026-07-25 — MIDI
  instrument catalogue wizard + the multi-edit repair of the instrument dropdown
  drawers (editor-only). **Three bass/harmony batches shipped 2026-07-25/26 and
  their documentation was applied in one pass by the doc-only batch B0 —
  DOC-CLOSE (2026-07-26):** MGP-ALWTTT-BASS-POCKET-1, MGP-ALWTTT-BASS-POCKET-2
  and MGP-ALWTTT-BASS-SOLO-1 + RUNTIME-REQUALITY (see "Just completed").
  **The B-series is closed.** **B1 — HARMONY-PURE-1: CLOSED (2026-07-27)** (zero
  impact radius, all opt-in or host-invoked: REQUALITY-2 color table, SECDOM-1
  `appliedTarget`, CADENCE-META, MOD-1 pure modulation helpers, EDITOR-CASE-1
  — outcome record on `planning/active/Roadmap_Chord_Expressivity.md`); **B2 —
  TONFILTER-1: CLOSED (2026-07-27)** (the only one of the three with a real
  impact radius: it removed the tonality revert in step 2b of
  `ChordTrackComposer` and its conditional `ctx.rng` draw — outcome record on
  `planning/active/Roadmap_Composition_Expressivity.md`; the legacy
  `PickTemplateForPart` library filter was deliberately left in place, recorded
  as F-B2-LIBRARY in `runtime/SSoT_Composer_Backing_Track.md` §2.2 and
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3, and retiring it is an
  unscheduled runtime candidate); **B3 — BASS-REG-1 + WALK-2:
  CLOSED (2026-07-27).** Both phases shipped — the register decisions
  (D-REG-1..4) and the improvised walking bass
  (`arpeggioToneMode = ImprovisedWalk`, D-W2-*). This closes the bass thread of
  the Chord Articulation arc. Outcome record on
  `planning/active/Roadmap_Chord_Articulation.md` §B3. Also open, in no committed
  order: **volume01 authoring** of the 70 instruments (blocked on
  ALWTTT D-CSV-18 listening verdicts; the flat 1.0 baseline is now confirmed
  from the INST-WIZ-1 export rather than assumed), and
  **MGP-ALWTTT-BASSFILL-1** (recalibrated to a robustness gap:
  warn-on-short-progression preferred over auto-fill; D-CSV-23 moves ALWTTT's
  progression standard to 8 bars). The **PatchName/PatchIndex hygiene check** is
  no longer a candidate — resolved 2026-07-25 with no findings across all 79
  instrument assets. Earlier: MGP-ALWTTT-DBG-4+2
  closed 2026-07-17, completing the **composition-debug arc package half** —
  DBG-1+3 + DBG-4+2 both done; the only remaining arc work is the single ALWTTT
  consumer session, driven by the DBG-4+2 handoff. BPM-DET-1 + CA-T2 closed
  2026-07-16; tests green, docs applied. The **Chord Articulation (CA) arc**
  (`planning/active/Roadmap_Chord_Articulation.md`) has **CA-T1** (Tier-1 engine),
  **CA-F2** (monophonic bass consumer), **MGP-ALWTTT-ARTIC-1** (Random selection
  policy — seeded variation part 1), **CA-T2** (Tier-2 voicing-reshaping: power
  chord + chugging) and now **CA-V1 part 2** (seeded velocity jitter +
  arpeggio-rate variety + bass roll rider), **BASS-WALK-1** (opt-in chord-tone
  walk for the bass), **CA-T2-BOSSA** (the register split, now
  `BassUpperSplit`) and **CA-T2-BOSSA-V2** (the AUTHENTIC bossa template as
  `Bossa = 10` + the rename) DONE. **The CA arc is complete** — reopened once
  on finding F-BOSSA-FEEL and re-closed; what is left are recorded candidates
  on the roadmap, none of them scheduled. **BPM-DET-1** is also now closed: the
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

- **MGP-MEL-1b — procedural melody directive layer fixed and hardened
  (2026-08-05).** F1: the repeat directive is now gated on `.enabled` like the
  interval directive, closing a flat-pitch defect where an always-present
  `[Serializable]` instance short-circuited the strategy. F2: `notesToRepeat`
  became a true N-note phrase-scoped motif buffer replayed cyclically with a
  per-cycle `transposeSemitones` (D8=B) — the transpose is CHROMATIC and
  accumulates, a recorded authoring hazard. F3: `AscendingOnly` /
  `DescendingOnly` snap to the nearest candidate of the same harmonic pool
  (D9), so contour is scale-aware and never chromatic. P2: five reserved
  fields hidden and `WeightedPhraseDirective.overrideStrategy` migrated to
  `useOverrideStrategy` + value. P3: an effective-leading log line, which
  immediately exposed the P6 hazard live. P6.1: the procedural precedence
  table now has a documented home (`authoring/SSoT_Authoring_Melody_Composition.md`
  §4b); P6.2: a logGenerator-gated inert-config signal on the pattern path.
  New package surfaces: `BackingCardConfigSO.adoptProgressionTonality` (P4,
  D3=C / D4=A) with `ResolvedTrackChoice.tonalityAdopted` / `.adoptedTonality`,
  and `PartRender.sharedProgressionData` (P7, D6=B) as the jam-continuity carry
  channel. New suite `ConstrainedMelodyStrategy_MotifTests`.
  **F1 changes the melody rng draw sequence** — same seed now yields a
  different, correct melody; any procedural-melody golden must be re-pinned.
  **P4 and P7 are implemented and unit-tested but NOT consumer-verified** —
  both need host-side work (modal card, jam-continuity wiring).

- **MGP-MEL-1b — procedural melody directive layer fixed and hardened
  (2026-08-05).** F1: the repeat directive is now gated on `.enabled` like the
  interval directive, closing a flat-pitch defect where an always-present
  `[Serializable]` instance short-circuited the strategy. F2: `notesToRepeat`
  became a true N-note phrase-scoped motif buffer replayed cyclically with a
  per-cycle `transposeSemitones` (D8=B) — the transpose is CHROMATIC and
  accumulates, a recorded authoring hazard. F3: `AscendingOnly` /
  `DescendingOnly` snap to the nearest candidate of the same harmonic pool
  (D9), so contour is scale-aware and never chromatic. P2: five reserved
  fields hidden and `WeightedPhraseDirective.overrideStrategy` migrated to
  `useOverrideStrategy` + value. P3: an effective-leading log line, which
  immediately exposed the P6 hazard live. P6.1: the procedural precedence
  table now has a documented home (`authoring/SSoT_Authoring_Melody_Composition.md`
  §4b); P6.2: a logGenerator-gated inert-config signal on the pattern path.
  New package surfaces: `BackingCardConfigSO.adoptProgressionTonality` (P4,
  D3=C / D4=A) with `ResolvedTrackChoice.tonalityAdopted` / `.adoptedTonality`,
  and `PartRender.sharedProgressionData` (P7, D6=B) as the jam-continuity carry
  channel. New suite `ConstrainedMelodyStrategy_MotifTests`.
  **F1 changes the melody rng draw sequence** — same seed now yields a
  different, correct melody; any procedural-melody golden must be re-pinned.
  **P4 and P7 are implemented and unit-tested but NOT consumer-verified** —
  both need host-side work (modal card, jam-continuity wiring).

- **True legato via pitch bend (MGP-ALWTTT-BASS-BEND-1, 2026-08-05).** A
  `HammerOn`/`PullOff` step no longer strikes a note: the nearest preceding
  sounding hit becomes its CARRIER, the carrier's gate extends through the
  legato tail, and each tail becomes a STEP pitch bend gesture applied
  post-build by the new shared `PitchBendWriter` (Runtime, pure;
  `SSoT_CONTRACTS.md` §11 — the package's first non-note, non-CC emission).
  Intervals moved from semitones to SCALE DEGREES (`hammerOffsetDegrees` /
  `pullOffsetDegrees`, defaults +1/-1, `[FormerlySerializedAs]`), anchored to
  the scale and measured from the carrier's reached pitch, so the tonality
  decides each step's size. Declared degradations: ±2 semitone GM range assumed
  (wider chains clamp with a warning), off-scale starting pitch classes fall
  back to whole tones silently, an orphan legato step degrades to an attacked
  note (warn once per render), and the two legato velocity factors now reach
  only that orphan path. Zero new `ctx.rng` draws; renders without legato
  classes are byte-identical, pinned by a render-hash canary. 50 EditMode pins
  across three suites; smoke S5-A…S5-R0 all PASS.

- **SelfPocket articulation vocabulary (MGP-ALWTTT-BASS-SLAPFIG-2 / 2b,
  2026-08-03).** `SelfPocketStep` extended append-only with `Ghost = 3`,
  `GhostPop = 4`, `HammerOn = 5`, `PullOff = 6`; `selfPocketSubdivision`
  extended with `QuarterBeat = 2` (sixteenths). Per-class velocity is a
  multiplicative factor of the chord event's velocity (not an additive boost);
  per-class gate ceiling gives the ghost classes a click-length gate; both sets
  of numbers are authored on `BasslineCardConfigSO` while the laws stay in the
  composer. `HammerOn`/`PullOff` were RESERVED at this batch's close and are now
  ACTIVE (see BEND-1 above). Planner remains pure, rng-free and
  cross-track-free; v1 patterns byte-identical. EditMode pins were written
  retroactively by BEND-1 step 1
  (`BassTrackComposer_SelfPocketVocabularyTests`, 20 pins).

- **MGP-ALWTTT-BASS-ORDER-1 + MGP-ALWTTT-BASS-SLAPFIG-1 (2026-07-31).**
  Cross-boundary demand from ALWTTT, both asks implemented and verified.
  ORDER-1: shared harmony is now independent of track-list order (PASS 0 for
  Backing + deferred index-ordered merge, both entry points); the guard on the
  host default became a static harmony-source sniff, so an articulation-only
  Backing row no longer suppresses it; `PartRender.sharedProgressionSource`
  (+ `ResolvedSource.HostDefault = 7`) exposes which source won.
  Closes F-BASS-ORDER-1 (bass-before-backing rendered permanent silence).
  SLAPFIG-1: `PocketCouplingMode.SelfPocket` — autonomous slap/pop figure over
  the shared progression from a cycled, meter-anchored card pattern; zero rng,
  zero cross-track reads, reuses the whole SlapPocket emission pipeline.
  Files: `SongOrchestrator.cs`, `BassTrackComposer.cs`,
  `BasslineCardConfigSO.cs`, `CompositionReadback.cs`; new suites
  `SongOrchestrator_HarmonyOrderTests.cs`,
  `BassTrackComposer_SelfPocketTests.cs`. Verified in ALWTTT gig logs
  2026-07-31.

- Closed **CPE-META-2 — metadata in the import payload (D3=A) + LLM emission
  (D4)** (2026-07-29). The import payload and the LLM route now carry chord
  asset metadata: four OPTIONAL setup-card lines, presence-gated, with the
  append-only `InvalidMetadataField` warning and NO import-mode degradation.
  One-shot pending staging in the window (D-M2-1=A) preserves the D2=C
  no-clobber rule — an import announces what it will write, the next
  Apply/Save consumes it, and re-applies afterwards never touch metadata; the
  allowed-tonality list is the exception (mirror state, rides the existing
  toggles→asset route). The runtime payload path stamps the same metadata
  (D-M2-3=A: one grammar, one behavior), a declared list replacing the
  TONFILTER-1 provenance default. The prompt asks only for the descriptive
  fields (D-M2-4=A). New EditMode suite
  `ChordProgressionImport_MetadataTests`. Closes the authoring gap surfaced by
  the ALWTTT fase-B content pass: an imported asset can now be fully authored
  in one pass.
- Closed **CPE-META-1 — asset metadata section in
  `ChordProgressionEditorWindow`** (2026-07-29). The window now authors asset
  metadata (render policy, color table, cadence, plus read-only `DisplayName`
  and `originalInput` provenance) in a direct-bound section (D2=C — Undo +
  SetDirty per change; the apply pipelines never read or write these fields, so
  clobber is structurally impossible), and per-event `isDiatonic` + SECDOM-1 in
  the Grid selection inspector (D1=A+C), each with a non-blocking advisory
  (ML-8b for cadence-vs-policy, SECDOM validity per event). Zero changes to
  `ChordProgressionData`; exit criteria 1–7 PASS. Import payload unchanged at
  the time (D3=B) — superseded the same day by CPE-META-2.
- Closed **B4 — DOC-CLOSE-2** (2026-07-28). **Documentation-only batch, zero
  code.** Applied the nine corrections derived from the 2026-07-28 drift run
  (10 findings across 6 governed documents; **none of them code drift** — the
  heavy checks came back clean: register seams, `ResolveWalkSeed`, the A→B→C
  publication order, the field-by-field copy list, `PlanHits(noteCount: 1)`).
  Recorded **F-B2-LIBRARY** as a bounded exception in
  `runtime/SSoT_Composer_Backing_Track.md` §2.2 and
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.3 (the code is intended;
  only the wording generalized); added **§4.6** for MOD-1 / `ModulationPlanner`,
  clearing the M-3 governance debt; aligned the Bass SSoT §0 governs list with
  the manifest and gave §3.6bis / D-REG-2 / D-REG-3 their update triggers;
  resolved **F-CS-SEQ** here; and swept `changelog-ssot.md` and
  `coverage-matrix.md` for B1/B2/B3, which the completion invariant of
  `SSoT_CONTRACTS.md` §9 required and which had been missing — **with that
  sweep, B1/B2/B3 is closed by its own contract.** No runtime, composer, rng
  path or asset semantics touched.
- Closed **B1 — HARMONY-PURE-1** (2026-07-27): REQUALITY-2 opt-in colour table
  (`useColorTable`, D-CT-GATE=A, D-CT-DIM=A), per-event SECDOM-1 (D-SD-ENC=A,
  D-SD-OWN=A), CADENCE-META (`cadence`, D-CAD-AUTH=A), MOD-1 pure
  `ModulationPlanner` (D-MOD-OUT=A; the host consumes it through
  `patternOverride`; zero composer edits), EDITOR-CASE-1 (D-EC-SEM=B,
  parse-time). Publication pipeline A→B→C with a single clone; determinism
  intact (zero rng; the MOD-1 seed only breaks ties via FNV-1a). Zero impact
  radius VERIFIED by a byte-parity smoke. The `ChordEvent` field-surface canary
  is live. Pre-existing editor F-NORM-DROP hazards (`isDiatonic` /
  `degreeAccidental` in the grid copies) fixed.
- Closed **B2 — TONFILTER-1** (2026-07-27): `tonalities` demoted to metadata;
  the tonality revert in step 2b of `ChordTrackComposer` and its conditional
  rng draw removed. Conflict signal on `ResolvedTrackChoice.tonalityMismatch`
  plus a gated warning. The importer contract is intact (it writes provenance).
  Pinned by `ChordTrackComposer_TonalityMetadataTests` (4 tests). Impact
  radius: only renders whose progression is tonality-incompatible (content
  imported into a foreign tonality); everything else byte-identical.
- Closed **B0 — DOC-CLOSE** (2026-07-26). **Documentation-only batch, no code
  except a comment.** Applied, in one pass and in order, the three drafted-but-
  unapplied diff files POCKET-1 → POCKET-2 → SOLO-1/REQUALITY across
  `runtime/SSoT_Composer_Bass_Track.md` (new §1 host-default paragraph, amended
  normalization-order bullet, new §3.7 + §3.7.1, six new §5 triggers),
  `runtime/SSoT_Composer_Rhythm_Track.md` (new §3bis onset publication),
  `runtime/SSoT_Runtime_Generation_Orchestration.md` (§5 onset-channel bullet,
  new §5.5 and §5.6, four new §8 triggers),
  `runtime/SSoT_Composer_Backing_Track.md` (§3 application sites + F-NORM-DROP),
  `authoring/SSoT_Authoring_Chord_Progressions.md` (§4.1 render policy),
  the three roadmaps, `coverage-matrix.md`, `changelog-ssot.md` and
  `ssot_manifest.yaml`.
  **Decisions taken at B0:** **F-IVT-STALE = (a)** — the comment on
  `Runtime/AssemblyInfo.cs` is corrected and `public` is consecrated as the
  test-seam convention (recorded as Orchestration §5.6); the alternative
  (repair the assembly name and revert the three new seams to `internal`) is
  code, needs the real test `.asmdef` name, and is registered as a candidate.
  **Correction made on the way in:** the POCKET-1 diff described
  `SongOrchestrator.CreateSetRhythmOnsetsForPartMusician` /
  `CreateGetRhythmOnsetsForPart` as "internal static test seams"; they are
  `public static` — a fourth instance of F-IVT-STALE, caught before it entered
  a governed document. **coverage-matrix** rows were translated into the file's
  own `Concept | Primary | Secondary` schema rather than the `feature | tests |
  smoke` shape the diff proposed (that shape is not this file's). No
  primary-home flip. `SSoT_CONTRACTS.md` deliberately unchanged: no
  cross-cutting contract moved.

- Closed **MGP-ALWTTT-BASS-SOLO-1 + RUNTIME-REQUALITY** (2026-07-26; docs
  applied at B0). Two independent, both opt-in, both byte-identical at their
  defaults.
  **(1) SOLO-1** — a part with a Bassline row and no Backing row no longer
  renders silence. `GenerateSinglePart` takes an optional host-supplied
  `defaultProgression` that pre-seeds the shared progression cache before the
  track loop (**D-SOLO-SRC=A / D-SOLO-SURF=A2**), so every harmony consumer
  (Bassline, Melody, Harmony) sees it. **D-SOLO-GUARD=A:** warn + ignore when
  the part HAS a Backing track — seeding under it would fork the render, since
  the backing card-palette publish is guarded by don't-overwrite; the warning
  names `patternOverride` on Backing as the supported alternative.
  **D-SOLO-NORM=A:** seeded as-is (a THIRD instance of the normalization-order
  hazard — hosts author the default in the part TS). **D-SOLO-DET:** pure
  dictionary write, zero `ctx.rng` draws; a null default is byte-identical, and
  the zero-draws claim is pinned end-to-end by smoke gate 3 (seeded default ≡
  the same asset in the bass row's own `Pattern` slot, same seed, same notes).
  Clone-on-seed is name-preserving so the Ask-A readback stays meaningful.
  **(2) RUNTIME-REQUALITY** — `ChordProgressionData.qualityRenderPolicy`
  (`AsAuthored` / `DiatonicToPart` / `DiatonicToPartFunctional`, append-only,
  default inert) re-resolves DIATONIC event qualities to the part's tonality at
  render time, size-preserving (**D-RQ-MAP=A**: triads via the diatonic triad,
  sevenths via the diatonic seventh; sus / 6ths / 9ths pass through; `Major` is
  never promoted to `Dominant7`). **D-RQ-BORROW=A:** borrowed events are never
  touched. **D-RQ-FUNC=A / -FUNC-SCOPE=A:** the Functional variant keeps an
  authored `Major`/`Dominant7` on the dominant degree and re-marks it borrowed,
  so the leading tone survives in modes whose diatonic v would lose it.
  **D-RQ-LOCRIAN=A:** documented no-op. **D-RQ-SITE:** the transform is
  DATA-level, applied at two publication boundaries (the backing composer's
  clone step, after the §2.2 tonality alignment; and
  `SongOrchestrator.TrySeedDefaultProgression`), because backing, bass and
  melody each derive chord pitch classes independently — a composer-local
  branch would make them diverge.
  **New finding F-NORM-DROP, fixed and regression-pinned:** the TS/subdivision
  reprojection does not `Instantiate`; it builds a fresh `ChordProgressionData`
  and copies fields ONE BY ONE, so an omitted field silently reverts to its
  default on the runtime clone. `qualityRenderPolicy` was initially omitted,
  which made requality inert for nearly every asset (authoring writes `sub x1`;
  the composer normalizes to `x4`). Any new `ChordProgressionData` field must be
  added to that copy list.
  25 EditMode tests green (`SongOrchestrator_DefaultProgressionTests` 6,
  `ChordProgressionRequalityTests` 19), full suite re-run green; 7 smoke gates +
  1 optional pass. Gates 6 and 7 as originally specified are **void** — a Block
  bass plays roots only and I/IV/V roots are identical in C Ionian and C
  Aeolian, so the render could not distinguish "requality worked" from
  "requality never ran"; superseded by 6'/7' (through the backing, which plays
  full chords) and the optional 6b (bass walk, which exposes the third).
  Governed by `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.5,
  `runtime/SSoT_Composer_Bass_Track.md` §1,
  `authoring/SSoT_Authoring_Chord_Progressions.md` §4.1 and
  `runtime/SSoT_Composer_Backing_Track.md` §3.

- Closed **MGP-ALWTTT-BASS-POCKET-2 (pocket shaping)** (2026-07-25; docs applied
  at B0). Two opt-in refinements of §3.7, both inert at their defaults and both
  living INSIDE the `pocketMode = SlapPocket` branch, so the POCKET-1 degrade
  guarantee is structurally unaffected.
  **D-PKT-VEL2 = B:** additive per-class `pocketSlapBoost` / `pocketPopBoost`
  (`[Range(-64,64)]`, default 0) over the drum step's velocity, clamped 1..127.
  Because published onsets already arrive clamped, a boost of 0 is an EXACT
  identity, not an approximate one. The motivating case is a pop-only boost:
  pops sit an octave up and read weaker than slaps at equal drum velocity
  against a softly-authored kit. **D-PKT-LANES2 = C, serialization C1:**
  `pocketCustomLanes` + `pocketSlapLanes` / `pocketPopLanes`; the lists REPLACE
  the built-in families rather than extending them, an empty list DISABLES that
  trigger class, and a lane in both lists resolves to POP. Matching stays on the
  SEMANTIC authored lane — field-verified against a kit that maps `SideStick`
  onto `AcousticSnare`.
  Alternatives on record as not taken: VEL2=C (float scale + curve, deferred, no
  content demanded it); VEL2=D (revert D-PKT-VEL=A, rejected — it removes the
  "breathes with the drummer" property); LANES2=B (fold `SideStick` into the
  snare family, rejected — it hardcodes a genre opinion where the same result is
  a content decision).
  27 tests green, of which the 16 POCKET-1 tests run UNMODIFIED against the
  extended seam signature (that is the default-path identity pin at seam level);
  11 smoke gates pass, including G3 — the degrade gate under the most hostile
  shaping available (boosts +30/−30, custom lanes on, both lists empty), which
  still produces a bass hash identical to `Off` in the same track order.
  **Adds a second instance to F-WALK-REG** (see the BASS-WALK-1 entry).
  **Golden fragility on record:** every POCKET-2 smoke hash is a function of the
  bass instrument's `octaveMin`, which was edited between capture sessions
  (`Slap Bass 1` 2 → 1). Every gate's internal A/B remains valid, but these
  hashes are **not a durable golden** — re-derive them if `octaveMin` changes,
  and never read a mismatch as a POCKET-2 regression without checking the
  instrument asset first. Cross-track-order comparison is separately invalid.

- Closed **MGP-ALWTTT-BASS-POCKET-1 (rhythm-coupled bass, "SlapPocket")**
  (2026-07-25; docs applied at B0), promoting the ALWTTT R2 candidate. Opt-in
  via `BasslineCardConfigSO.pocketMode` (new bass-only enum
  `PocketCouplingMode { Off = 0, SlapPocket = 1 }`); per chord event, a window
  containing kick/snare onsets has its figure REPLACED by slap hits (kick family
  → the §2 selected note) and pop hits (snare family → the same note +12,
  **D-PKT-POP-PITCH=A**), at the DRUM step's velocity (**D-PKT-VEL=A**) with a
  0.5-beat gate cap (**D-PKT-GATE=A**); a window without them renders the
  resolved figure exactly as decoupled (**D-PKT-EXPR=A**), so pocket and figures
  mix within one render.
  **The load-bearing decision is D-PKT-SRC=B:** the source is a new
  `GenContext` onset CHANNEL published by `RhythmTrackComposer` on the GRID path
  only — the package's **first composer→composer data dependency**. It is
  order-sensitive by design (**D-PKT-ORDER=A**: Rhythm must precede Bassline in
  `Part.Tracks`) and the CONSUMER owns the degrade path. **Degradation is pinned
  as BYTE-identity, not approximate:** no source (no Rhythm track / bass-first /
  procedural or legacy rhythm path) ⇒ the decoupled figure, at most one warning
  per `Compose`, never an error, never silence — which holds structurally
  because the CA-V1 roller rolls per event whether or not its result is used.
  **Zero new `ctx.rng` draws:** the plan is a pure function of (published
  onsets, event window) and runs after both §2 draws, the same structural
  argument as D-WALK-RNG=A. Emission was restructured into a SEGMENT list
  drained by ONE unconditional `Emit` call — SD-F2-1's anti-divergence
  discipline carried over segments; nothing was added to `ChordExpressionType`.
  Classification uses the SEMANTIC lane, so PERC-FALLBACK-1 substitutions cannot
  re-classify a hit.
  **Consumer hash duty declared and handed over:** with `pocketMode != Off` the
  resolved rhythm pattern is a hash-relevant input of the BASS track and ALWTTT
  must extend `ComputeTrackInputsHashesForPart`.
  Governed by `runtime/SSoT_Composer_Bass_Track.md` §3.7,
  `runtime/SSoT_Composer_Rhythm_Track.md` §3bis and
  `runtime/SSoT_Runtime_Generation_Orchestration.md` §5.
  **Open observation carried forward (NOT a POCKET-1 defect):** in the bass-only
  and bass-first renders the operator read the bass MIDI as spanning 4 bars
  rather than the part's 8. The logs contradict it — the part is
  `lenTicks=3072` (8 bars at 96 TPQN) and the bass reports `notes=4
  lastTick=3073`, trimmed at 3072, i.e. 4 Block notes of 2 bars each covering
  the full part; `BassTrackComposer` derives its spans from the progression
  alone, so the rendered span equals the progression's span by construction.
  Undecided causes: a DAW display/import artefact (the captures show each track
  drawn twice), or the progression asset's events summing to fewer beats than
  assumed (which would contradict `lastTick`). **Decisive check:** the bar
  position of the last bass note-off. Nothing in the applied documentation
  depends on this.

- Closed **INST-WIZ-1 (MIDI instrument catalogue wizard + drawer repair)**
  (2026-07-25). Editor-only batch, 4 files, no runtime touched — **no
  determinism or golden implication by construction.**
  **(1) `Editor/MidiInstrumentCatalogueWizard.cs`** (new,
  `MidiGenPlay/MIDI Instrument Catalogue Wizard...`): scan the two instrument
  Resources roots, filter (melodic/percussion, `InstrumentType`, free text over
  every serialized property), sort, inspect, edit, create/duplicate/rename/delete,
  and CSV-export the filtered set (file or clipboard). **D-W1=A** — this is the
  first catalogue tool that is not read-only, so it is documented as a
  **catalogue + management** variant (`SSoT_Authoring_Tools` §3.E) and the
  manifest's read-only catalogue invariant was rewritten rather than left to
  drift. Editing is delegated to the asset's own inspector for exactly ONE
  target, which reuses the existing dropdown drawers instead of reimplementing
  them and makes the multi-object hazard unreachable from the window. The export
  column set is the union of every visible serialized property, so it is complete
  without the window knowing the field names.
  **(2) The three `MIDIInstrumentSO` dropdown drawers rewritten (D-W2=B)** — this
  is the load-bearing half. `PatchDropdownDrawer` wrote `property.stringValue` on
  **every repaint**; since a `SerializedProperty` write hits ALL selected targets,
  merely drawing the inspector for a multi-selection stamped the first asset's
  patch onto every selected asset. The other two had the same shape via
  `Mathf.Max(index, 0)` compared against a `-1` "not found", which read as a user
  change and fired the dependent `BankName`/`PatchName` resets. All three also
  mixed property writes (all targets) with direct field writes
  (`midiInstrument.PatchName = ""`, `SetPatchIndex(...)` — first target only),
  which is what left the catalogue incoherent. Now: writes gated behind
  `BeginChangeCheck`, everything routed through `SerializedProperty` (dependent
  resets and the `PatchName`+`PatchIndex` pair included) so multi-edits are
  coherent and single-undo, `showMixedValue` on mixed selections, and an
  ambiguous list source (mixed soundfont/bank) disables the popup instead of
  guessing. **Second-order fix:** the same unconditional writes were "repairing"
  out-of-list values in SINGLE selection too — a standing violation of the §1
  no-silent-writes invariant, now an empty selection instead. Deliberate
  behavior change on record: an invalid `BankName` no longer auto-corrects to the
  first bank on inspector open.
  Verification 2026-07-25, 5/5 PASS (compile + columns; multi-select shows mixed
  and mutates nothing; multi-edit writes coherent name+index to all targets with
  one undo; mixed soundfonts disable Bank/Patch; corrupt `PatchName` not
  rewritten).
  **Findings carried by the export (79 assets: 70 melodic bank 000, 9 kits bank
  001):** `PatchName` prefix == `PatchIndex` with **zero** mismatches and **zero**
  duplicate `(bank, patch)` pairs ⇒ the standing PatchName/PatchIndex hygiene
  candidate closes with no findings, and ALWTTT's Poly Synth / Warm Pad collision
  is confirmed a measurement artifact. `volume01 = 1.0` on all 70 melodic
  instruments is now verified from data (D-MIX-6 authoring still open, still
  blocked on D-CSV-18). Bass register data for the ALWTTT R2 close-out:
  `Fingered Bass` 33 · `Picked Bass` 34 · `Slap Bass 1` 36 · `Slap Bass 2` 37,
  all `octaveMin/Max = 2/3` (the bass composer ignores `octaveMax`).
  Governed by `authoring/SSoT_Authoring_Tools.md` §3.C + §3.E.

- Closed **CA-T2-BOSSA-V2 (authentic bossa template + rename)** (2026-07-24),
  reopening and re-closing the CA arc on finding F-BOSSA-FEEL. Two
  deliverables on a 4-file surface (`ChordExpressionType.cs`,
  `ChordArticulator.cs`, both articulation test files; verified by grep that
  nothing else names the member):
  **(1) OD-BOSSA-7=A/-7a=A** — `Bossa = 9` renamed **`BassUpperSplit`**, value
  intact (enums serialize by VALUE; never parsed/persisted by NAME). Its
  tests were renamed, not rewritten — behavior byte-identical.
  **(2) `Bossa = 10`** — the authentic 1-bar comping template from the music
  lab's sourced rhythm spec (`basico_solo`; reference material, not
  authority). **D-FEEL-HOME=A:** flat member, same seam, NO new `PlanHits`
  input — the bar cycle is derived from the absolute beat position, and a
  chord change mid-cycle INHERITS the phase (spec §6.2). **D-FEEL-SCOPE=A:**
  one pattern done well (the lab's recognizability threshold) over four
  approximated; 2-bar patterns are recorded futures, which also mooted
  D-FEEL-PHASE. **D-FEEL-ACCENT=A — the identity-bearing decision:** the
  surdo weight sits on beat 2, NOT the downbeat; template-supplied accent
  tiers reuse the SD-5 factor values, a documented per-figure exception to
  §8.3's position curve, with a dedicated regression test (beat 2 must
  outweigh the downbeat). **D-FEEL-TIE=A:** no-overshoot stands; the
  syncopation (attack on 2.5, sustained to the cycle end, NO attack on
  beat 3) lives entirely inside the window; harmony-carrying anticipation is
  a recorded future. Degrades: ≤1-note voicing, `beatsPerBar ≤ 0`, or a
  window with no UPPERS attack (register safety, F-WALK-REG) → Block; an
  onset between rows gets a LOW fallback hit. 15 new tests incl. the
  emitted-MIDI probe (BASS-WALK-1 lesson: attack-time groups asserted on
  `GetNotes()` — full set at 0.0, uppers at 1.0, lowest alone at 2.0 surdo,
  uppers at 2.5, nothing on beat 3). The enum tail tripwire fired on BOTH
  deliberate edits and was updated in place (OD-BOSSA-6=A): member count 11,
  both figures pool-excluded by `>= ConcretePoolSize` — no §8.5 rule edited.
  Smoke-validated 2026-07-24 (A/B `BassUpperSplit` vs `Bossa`, same seed —
  PASS: R1 pitch-subset holds; audibly different — reads as bossa, the ska
  feel is gone).
  Governed by Backing §8.3/§8.4/§8.6/§7.5 + Bass §3.3.

- Closed **CA-T2-BOSSA (Tier-2 bossa bass/upper split)** (2026-07-24), the LAST item
  of the CA arc and the figure CA-T2 deferred. `ChordExpressionType.Bossa = 9`:
  the voicing's lowest note anchors the event onset and every interior bar
  downbeat; the upper voices strike short on each beat+0.5 (`Offbeat`'s grid
  reused verbatim). `arpeggioRate` is ignored (**D-BOSSA-RHYTHM=A**).
  **The load-bearing decision is D-BOSSA-HOME=A:** the figure lives in the
  ARTICULATOR, not in a reshaper-owned emission path. The rejected route would
  have turned the reshaper from a pure list transform into an emitter and broken
  the single unconditional `Emit`; the chosen one extends the articulator's
  EXISTING selection vocabulary by one sentinel (`Hit.NoteIndex = -2` = the
  upper voices, **D-BOSSA-SEL=A**) without changing the `Hit` struct shape.
  **This is the BASS-WALK-1 reading of pitch-preservation carried forward:**
  "pitch-preserving" means the articulator never alters a pitch VALUE, not that
  it never chooses among the values it is handed. CA-T1 already selected one
  note; Bossa selects a subset. The reshaper is IDENTITY for Bossa and was not
  edited.
  **Scope is the headline:** two runtime files changed (`ChordArticulator.cs`,
  `ChordExpressionType.cs`). No composer, no reshaper, no card surface, no
  orchestrator — verified against the code before writing, not assumed.
  **Applied the BASS-WALK-1 verification lesson at the seam:** the `Emit`
  translation now matches sentinels EXACTLY. A blanket `NoteIndex < 0` test
  would have rendered the new subset sentinel as a full chord — green plan
  tests, wrong MIDI, exactly the failure shape of that batch. Undefined
  negatives degrade to the full chord (never silent). The batch's probe asserts
  on EMITTED notes: downbeat = exactly the lowest voiced pitch, each offbeat =
  exactly the non-lowest pitches.
  **Register handled up front (F-WALK-REG):** a selection figure changes the
  effective register even inventing no pitch, so the degrade rules avoid silent
  register shifts — an event with no room for an offbeat falls back to the full
  chord instead of rendering bass-only (**OD-BOSSA-4=A**), and a ≤1-note voicing
  (any bassline card selecting `Bossa`) degrades to `Block`. The emitted pitch
  set is a SUBSET of the voicing by construction, which is what the A/B smoke
  verified rather than assumed.
  Other decisions: **D-BOSSA-BASSNOTE=A** (the anchor is the lowest note after
  voicer + §7 pin + reshape — with a `Down` pin it is the inverted bass, by
  definition not by accident) · **OD-BOSSA-1=A** (low role reuses the ascending
  sort at index 0; no third sentinel) · **OD-BOSSA-2=A** (uppers by strict `>`,
  so a doubled bass pitch never re-strikes offbeat) · **OD-BOSSA-3=A** (onset +
  interior bar downbeats, so long events keep a bass on every 1).
  11 tests added to `Tests/Editor/ChordTrackComposer_ArticulationTests.cs`; the
  `ChordExpressionType` tail tripwire in
  `Tests/Editor/BassTrackComposer_ArticulationTests.cs` updated in place
  (**OD-BOSSA-6=A** — it fired correctly on the intentional append, which is
  what it is for; it is never deleted to make a red suite green) and
  strengthened with an explicit §8.5 pool-exclusion assertion.
  Governed by `runtime/SSoT_Composer_Backing_Track.md` §8.4 + §8.6 (+ §7.5 for
  the pin interaction, + a note in `SSoT_Composer_Bass_Track.md` §3.3).
  Smoke-validated 2026-07-24 (A/B, same seed — PASS: pitch set a strict subset,
  downbeat = lowest voiced pitch, offbeats = the rest).
  **Finding F-BOSSA-FEEL, recorded after the smoke:** the v1 template is a
  REGISTER SPLIT, not an authentic bossa rhythm. Low on every bar downbeat +
  uppers on every offbeat is a regular alternation that listens as a calm ska
  upstroke; real bossa comping alternates unevenly across a two-bar cycle and
  mixes in full-chord attacks. The figure is correct and useful as shipped —
  only its stylistic label overreaches. **Open decision OD-BOSSA-7:** whether to
  rename the member (`BassUpperSplit` / `RegisterSplit`, keeping value 9) and
  reserve `Bossa` for an authentic figure; cheapest before any asset references
  it. The authentic template is deferred and blocked on a rhythmic
  specification, NOT on the seam — §8.4's vocabulary already expresses every
  role it would need.

- Closed **BASS-WALK-1 (chord-tone walk for the bass)** (2026-07-24), promoting
  the SD-F2-2 candidate that CA-F2 deferred and CA-V1 did not deliver. Opt-in
  via `BasslineCardConfigSO.arpeggioToneMode` (new bass-only enum
  `BassArpeggioToneMode { RepeatedNote = 0, ChordToneWalk = 1 }`); default
  `RepeatedNote` means pre-batch output is unchanged **structurally** — the walk
  branch is gated on the enum, not merely equal by measurement.
  **The load-bearing decision is D-WALK-HOME=A:** the walk is NOT a new engine
  figure. The bass hands the same single unconditional `Emit` a 3-note playable
  built by the new pure `BassTrackComposer.BuildWalkVoicing` (root/3rd/5th
  stacked strictly ascending from the ALREADY-DRAWN root octave), and the
  articulator's existing `k % noteCount` arpeggio cycling does the walking. The
  engine stays pitch-preserving in the exact sense it always held: it selects
  among the notes handed to it and never invents one.
  **D-WALK-RNG=A** is what made the batch cheap: zero new `ctx.rng` draws — the
  3rd and 5th are derived from `chordPcs`, the register is the octave §2 already
  draws, and the branch runs after both draws. The bass's per-event draw count
  and order are untouched by construction.
  New pure predicate `ChordArticulator.ArpeggioFits(durBeats, rate)`
  (**D-WALK-FIT=A**) exposes the engine's arpeggio degrade rule so the bass can
  fall back to a 1-note playable on short events — without it, a degraded
  `Block` over the triad would emit a CHORD on the bass line. Predicate/plan
  agreement is test-pinned as the drift detector.
  Side effect worth naming: this retires the monophonic pool bias recorded at
  Bass §3.3 whenever walk mode is on — `ArpeggioUp` and `ArpeggioDown` stop
  being the same sound, so the uniform Random pool is balanced without weights.
  Scope kept tight: triad only (7th dropped, **D-WALK-TONES**), engine sort
  order accepted (**D-WALK-DIR**), root anchoring even in the unreachable
  `randomChordTone` mode (**D-WALK-ANCHOR**, simplification on record).
  9 tests added to `Tests/Editor/BassTrackComposer_ArticulationTests.cs`.
  Governed by `runtime/SSoT_Composer_Bass_Track.md` §3.6 (+ a one-line note in
  `SSoT_Composer_Backing_Track.md` §8.4 for the exposed predicate).
  Smoke-validated 2026-07-24 (authored-card A/B: walk steps root/3rd/5th, the
  `RepeatedNote` control is a flat repeated pitch).
  **New finding F-WALK-REG, on record:** because the walk stacks UPWARD from the
  root, it raises the line's effective ceiling by about a fifth — and the bass's
  octave band is `octaveMin-1 .. octaveMin+1` (three octaves, and the bass
  IGNORES `octaveMax`, unlike every other composer). Walk-mode content may need a
  lower `octaveMin` on the bass instrument asset.
  **RESOLVED (B3 — BASS-REG-1, 2026-07-27).** Both instances are closed and the
  bass now honours `octaveMax`. Decisions: D-REG-1=C (hard ceiling on the band
  AND on everything emitted), D-REG-2=B (a pop that would exceed the ceiling
  folds onto the selected note; pop identity untouched), D-REG-3=B (a walk
  voicing that would exceed it transposes whole −12; strict ascent preserved),
  D-REG-4=B (the §2 band narrows from three octaves to two,
  `octaveMin-1 .. min(octaveMin, octaveMax-1)`). Impact radius: EVERY bass
  render — the octave draw keeps its count and order, only its range, which
  remaps the same seed. Pinned by `BassTrackComposer_RegisterTests` (17 tests);
  the WALK-1 pins run unmodified against the ceiling-free 2-arg overload.
  D-REG-0=A split B3 in two: this register phase, and the improvised walking
  bass (WALK-2) still open.
  **ImprovisedWalk shipped (B3 WALK-2, 2026-07-27).** Third `arpeggioToneMode`
  value: a seeded walking line — event-root anchor, chord-tone middles placed
  near the previous note, chromatic/whole-step approach into the NEXT event's
  root (wrapping to the first). The engine is untouched: rhythm, accents and
  jitter come from `PlanHits` called composer-side (`noteCount: 1`); pitches
  re-enter the single `Emit` as 1-note Block segments (velocity passthrough).
  Variation is a pure `(ResolveWalkSeed, eventIndex, hitIndex)` mix — no
  stream, no draw discipline, nothing a toggle can shift. Opt-in:
  `RepeatedNote`, `ChordToneWalk` and `pocketMode = Off` are byte-identical
  (the four pre-existing bass/orchestrator suites run unmodified as the
  detector). Register: per-note −12 fold under the D-REG-1=C ceiling. 13 tests
  in `BassTrackComposer_WalkImprovTests`; governed by
  `runtime/SSoT_Composer_Bass_Track.md` §3.6bis.
  Still on record for the bass: single-pass rendering, the normalization-order
  hazard, and `degreeAccidental` ignored.

- Closed **CA-V1 part 2 (seeded variation)** (2026-07-24), completing the batch
  ARTIC-1 opened and **closing the CA arc except for the bossa split and the
  chord-tone-walk candidate**. Two axes, both opt-in and both off by default:
  (1) **seeded velocity jitter** — new value type
  `Composition/Data/VelocityJitter.cs`, `velocityJitter` on both the backing and
  bassline cards, applied as a post-pass in `ChordArticulator.PlanHits`;
  (2) **arpeggio-rate variety** — `ArpeggioRate.Random = 3` (append; 0..2
  serialized and unchanged) resolved by a new
  `RandomArticulationRoller.NextRate()`. Plus the **bass rider**: D6
  (degrade-only) is LIFTED — the bass now rolls its own figures and rates and
  carries the full Random knob set (D-V1-BASS=B).
  **The load-bearing decision is D-V1-JIT-SRC=A:** the jitter is a PURE MIX over
  (seed, event index, hit index), NOT a forked child rng as the roadmap had
  framed it — so SD-3=A ("the articulator is RNG-free") survives verbatim, the
  jitter is immune to draw-order coupling, and integer-only mixing lets the
  tests pin exact goldens instead of the SEED-1 variance idiom. `ctx.rng` is
  untouched; the bass's per-event draw contract is structurally safe.
  Two new substreams (`|articrate`, `|articvel`) join `|artic` under SEED-1;
  `trackSeed` already folds in role + musicianId, so backing and bass never
  share a sequence. `velocityJitter == 0` returns the planned hit list by
  reference — pre-CA-V1 bit-identity is structural, not empirical.
  **R4**: the DBG-1 readback was deliberately NOT extended; a rate-only random
  render leaves the figure history empty and `SnapshotRolls` now reports `null`
  for it, preserving "fixed articulation reports null figures". **R5**: the
  smoke surface (SmokeEntry/SmokeRenderUtil/window/runner) carries the new knobs
  and the obsolete D6 bass warning was deleted. New
  `Tests/Editor/ChordTrackComposer_VelocityJitterTests.cs` (16) plus additions
  to the roller and bass test files; the pre-existing articulation suites pass
  unchanged. Governed by `runtime/SSoT_Composer_Backing_Track.md` §8.5 + new
  §8.7 and `runtime/SSoT_Composer_Bass_Track.md` §3.5.
  Still on record for the bass: single-pass rendering, the normalization-order
  hazard, `degreeAccidental` ignored, and the monophonic pool bias
  (`ArpeggioUp` ≡ `ArpeggioDown`, mitigated by the new weights knob).

- Closed **MEL-BEATUNIT-1** (2026-07-24), resolving finding **F-1**: melody is now
  beat-unit aware. All three `MelodyTrackComposer` emission sites go through one new
  seam, `BeatsToSpan(beats, GetBeatSpan(part.TimeSignature))`, instead of the
  unconditional `MusicalTimeSpan.Quarter` — the two live paths (`ComposeFromPattern`,
  procedural `ComposeMelodyFromProgression`) plus the unreachable `ComposePerBeatMelody`,
  corrected in lockstep so it cannot reintroduce the desync (its only call site is
  commented out; deletion is a separate open question). Direct precedent: the bass fix at
  **CA-F2** (SD-F2-3=B), and the deviation is recorded in
  `runtime/SSoT_Composer_Melody_Track.md` §7.1 with the same wording shape as Bass §3.4.
  **Byte-identical in every `beatUnit == 4` meter** — `GetBeatSpan` returns
  `MusicalTimeSpan.Quarter` there, so it is a structural identity, not an empirical one;
  in other meters the output deliberately changes (sync FIX). **D-MEL5.1 = A stands**: the
  meter-mismatch tiles-by-beats limitation is the other axis (how many beats a bar holds,
  not how long a beat is) and was not revisited. **No migration** of authored X/8 content —
  `MelodyPatternData` stores beats and `MelodyMidiImporter` already writes
  `gridBeats = quarterNotes × beatUnit / 4`, so the data was always right and only the
  render misread it; an author who hand-compensated at double speed must undo that.
  Determinism surface untouched (no RNG draws added; `ResolvePatternNotesCore` and the
  `GuideNote` payload unchanged, the latter now documented as Part beat units). 4 tests
  added to `Tests/Editor/MelodyTrackComposer_PatternDeterminismTests.cs` (12 total).
  **Melody smoke in compound/odd meters is now valid**; the previous "smoke melody in 4/4"
  instruction is withdrawn, and 4/4 becomes the byte-identity control instead.

- Closed **MIDIIMP-SSOT-1** (2026-07-24), documentation/governance only: MIDI file
  import promoted to its own cross-cutting SSoT
  (`authoring/SSoT_Authoring_MIDI_Import.md`), owning the shared contract while the
  three domain SSoTs keep their musical semantics unchanged. `coverage-matrix.md`
  caught up from 2026-07-17 (six missing batch notes + the primary-home flip);
  `SSoT_INDEX.md` spine updated. Also resolved: `MIDIPercussionInstrumentSO` is
  **package-owned** (governed under the rhythm composer SSoT; manifest path inferred
  and flagged), and `Roadmap_Melody_Authoring_MVP.md` archived — D2–D4 carried as
  records only, so future melody work opens a new roadmap. **F-1** (melody beat-unit
  desync in X/8) characterized in the melody runtime SSoT and left as the pending
  batch **MEL-BEATUNIT-1** (since closed 2026-07-24, see above).

- Closed **MEL-DOCDRIFT-1** (2026-07-24), documentation-only: corrected the
  melody-phase staleness in `authoring/SSoT_Authoring_Tools.md` §3.A/§3.D, which
  still described Phase 3 (generation params + `SimplifiedMelodyGenerator`) and
  Phase 4 (runtime `ComposeFromPattern`) as unimplemented — both closed
  2026-06-17 and verified code-backed. Also added the missing Phase-3 capability
  bullet, reworded the stale drag-to-resize limitation, and retitled §3.D from
  "remaining planned phases" to a phase history. No code, no contract, no
  governed-surface change. Surfaced while anchoring M2 and deferred there on
  purpose rather than patched silently.

- Applied the **batched documentation session** (2026-07-24): the accumulated
  drafted diffs §1–§10 (`M1_Doc_Diffs_FINAL.md`, `M2_Doc_Diffs.md`,
  `M3_Doc_Diffs.md`, `IMPORT_QOL_1_Doc_Diffs.md`) reconciled against a
  re-verified baseline. §1–§7 were found already applied; §8, §9 and §10 were
  applied in order. `Roadmap_MIDI_Import.md` moved to `planning/archive/`
  (arc COMPLETE) and de-registered from the manifest's `roadmaps:` list.

- Closed **IMPORT-QOL-1 — editor QoL** (2026-07-24): Suggest-subdivisions probe
  + preserve-re-strikes toggle (bounded M3-D5 amendment) + `originalInput`
  provenance on the chord import panel; measures advisory + config auto-assign
  on `CompositionSmokeWindow`. 33 EditMode tests green; re-strikes smoke
  regression PASS. D-QOL1-1..7 locked.

- Closed **M3 — MIDI → ChordProgressionData** (2026-07-23): `ChordMidiImporter`
  + 25 EditMode tests + Grid-mode import panel; M3-D1..D6 locked. MIDI Import
  roadmap COMPLETE.

- Closed **M2** (2026-07-23), the melody half of the **MIDI Import arc**
  (`planning/archive/Roadmap_MIDI_Import.md`): `MelodyMidiImporter` (pure-function
  `.mid` → `MelodyPatternData`; user-specified key, pitch → diatonic degree with
  ties-downward chromatic snap, auto-centered reference octave, monophonization by
  highest-pitch-wins plus overlap truncation, beat-absolute quantized timing with
  preserved duration, eleven-kind warning taxonomy with no silent fallback), 20
  EditMode tests, and the melody editor's "MIDI File Import" panel (working-copy
  apply only; asset writes still via Apply/Save As). Verified against a real `.mid`
  in-editor including a render pass through `ComposeFromPattern`. Decisions
  M2-D1..D6 locked — notably **M2-D2=A: the reference octave is auto-centered and
  reported, not user-selected**, because the true reference is the runtime
  instrument register. Implements and supersedes `Roadmap_Melody_Authoring_MVP.md`
  Phase D1. No runtime code touched.

- Closed **PERC-FALLBACK-1** (2026-07-22): family-fallback percussion
  resolution in `RhythmTrackComposer` — new Runtime `PercussionFallbackTable`
  + pure `PercussionNoteResolver` (exact → fixed-order family substitute →
  mute+warn, GM-standard opt-in wired off), all six former exact-match call
  sites routed through one seam with the D-PF3 log discipline. Deterministic
  by construction; kit SO read-only. Closes the render-time gap the M1
  real-MIDI test surfaced (Brush Kit muting BassDrum1 / floor toms /
  PedalHiHat / HiMidTom lanes).

- Closed **M1** (2026-07-19), opening the new **MIDI Import arc**
  (`planning/archive/Roadmap_MIDI_Import.md`): `DrumMidiImporter` (pure-function
  `.mid` → `DrumPatternData` grid; beat-unit-aware quantization, GM reverse map
  from DryWetMidi's own tables, modal-default velocity compression to the
  `StepState` sentinel, seven-kind warning taxonomy with no silent fallback), 11
  EditMode tests, and the editor's "MIDI File Import" panel (applies in Grid
  mode to preserve velocity fidelity; asset writes still only via Apply/Save As).
  Verified against a real GM drum `.mid` (100% OK). Decisions D-MIDI1..5 locked —
  notably **D-MIDI4=A: bassline import is out of scope**, since no bassline
  pattern asset exists and `BassTrackComposer` ignores pattern overrides in v1.
  No runtime code touched. The real-MIDI test surfaced a render-time gap spun out
  as **PERC-FALLBACK-1** (kit lacks the exact GM member an imported lane requests
  → lane dropped; needs family substitution in the rhythm composer).

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
  the procedural path — quarter-mapping since superseded by MEL-BEATUNIT-1, 2026-07-24;
  final loop truncated; `beatsPerMeasure` mismatch warns + tiles),
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

1. **D-L4.3 unification** (optional) — extract a shared generic over the drum and
   chord prompt builders / generators now that two working instances exist.
2. Resume phrasing / feel runtime completion (Phase 9) — now unblocked; Phase 8
   (authoring-tool persistence unification) closed 2026-07-05 by PATTERN-PERSIST-1.
3. Continue demoting the old `MIDISong` / `MIDIGeneratorManager` branch to
   legacy/reference status.

Future (recorded, not scheduled): fill tag system (R3 — runtime/Composer
concern; see roadmap §"Future work").

## Blocked / not implemented yet

- Package store/repository persistence integration for rhythm tools (Phase 8)
- Phrasing / feel knob semantic completion (Phase 9)
- The older `MIDISong` / `MIDIGeneratorManager` runtime branch still coexists
  in the repository
- **Consumer-side adoption of `sharedProgressionSource`.** ALWTTT still keys
  its render cache on the `hasBacking` proxy; until it re-conditions the `dp:`
  token on `HostDefault` and retires the skip, the articulation-only-Backing
  path is implemented and unit-tested but not exercised in the game.
- **Slide / ramped bend** (bass catalogue §B.8). The seam exists —
  `PitchBendWriter` takes a `rangeSemitones` parameter and the conditional-RPN
  requirement is recorded (`SSoT_CONTRACTS.md` §11) — but the ramp gesture
  itself is not implemented; `StepGesture` is deliberately a step.
- **Melody slur.** `PitchBendWriter` is available to the melody composer and
  documented there as an unconsumed seam.
- **Consumer verification of MGP-MEL-1b P4 / P7.** `adoptProgressionTonality`
  and `PartRender.sharedProgressionData` are implemented and unit-tested but
  unexercised in the game; both wait on host-side work.
- **Phrase-final breath.** Sustaining archetypes fill the remainder of the
  chord span, so consecutive phrases run together. `tailRestFraction` and the
  wider MGP-MEL-2 "Phrase Form" set (`RestPhraseSO`, `transposeScaleSteps`,
  A/B/A′ form with relatively-stored motif memory) are proposed but NOT
  scoped — recorded in `changelog-ssot.md` only, per DOC-SWEEP-1 decision D-1.
- **Recorded gap F5.** `PhraseSlot.totalSlotsInPhrase` is not constant within
  a SustainLeadIn phrase. No render impact today; any future end-of-phrase
  logic would inherit it.
- **Consumer verification of MGP-MEL-1b P4 / P7.** `adoptProgressionTonality`
  and `PartRender.sharedProgressionData` are implemented and unit-tested but
  unexercised in the game; both wait on host-side work.
- **Phrase-final breath.** Sustaining archetypes fill the remainder of the
  chord span, so consecutive phrases run together. `tailRestFraction` and the
  wider MGP-MEL-2 "Phrase Form" set (`RestPhraseSO`, `transposeScaleSteps`,
  A/B/A′ form with relatively-stored motif memory) are proposed but NOT
  scoped — recorded in `changelog-ssot.md` only, per DOC-SWEEP-1 decision D-1.
- **Recorded gap F5.** `PhraseSlot.totalSlotsInPhrase` is not constant within
  a SustainLeadIn phrase. No render impact today; any future end-of-phrase
  logic would inherit it.

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

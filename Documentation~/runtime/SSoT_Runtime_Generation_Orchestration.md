# SSoT — Runtime Generation Orchestration

## Scope

This document is the primary authority for MidiGenPlay generation and orchestration flow:

- `MidiGenerator`
- `SongOrchestrator`
- `ComposerFactories`
- `GenContext`
- composer registration and render flow

## 1. High-level flow

The package builds MIDI through a runtime pipeline roughly shaped like this:

1. A configured `SongConfig` exists.
2. `MidiGenerator` prepares role-specific composer factories.
3. `SongOrchestrator` walks the song structure and parts.
4. For each track, an `ITrackComposer` is chosen and executed.
5. Generated track files are merged with metadata and optional metronome.
6. The resulting `MidiFile` is returned to the caller.

## 2. `MidiGenerator` responsibilities

`MidiGenerator` is the high-level entry point that:

- registers composer factories by `TrackRole`,
- builds the orchestration context,
- exposes helper methods for generation,
- and centralizes preflight/debug visibility.

It is the package-facing entry, not the per-track implementation surface.

## 3. `SongOrchestrator` responsibilities

`SongOrchestrator` is the runtime coordinator.

Its responsibilities include:

- stamping tempo and time signature metadata
- iterating parts/structure
- deciding track channels
- calling the appropriate track composer
- merging per-track MIDI output
- optionally adding metronome output
- preserving the part meter as the authoritative time basis

## 4. `ComposerFactories` responsibilities

`ComposerFactories` resolve which concrete composer to instantiate for a track role.

This keeps role-selection logic separate from orchestration and from composer-specific implementation details.

## 5. `GenContext`

`GenContext` carries generation-scoped inputs such as:

- tempo map and timing helpers
- RNG/seeded context
- helper delegates/services used by composers
- cross-composer coordination inputs
- **per-render pattern override** (`patternOverride`, Ask C / D-DBG4=A) and the
  **per-track readback sink** (`ReportResolved`, Ask A) — both swap/restored by
  `GenerateOne` exactly like `rng` and `trackSeed` (§5.3)

This context should be treated as orchestration-owned runtime state, not as a gameplay/session bridge.

### 5.1 Seed threading (caller-supplied seed) — MGP-ALWTTT-SEED-1

Both render entry points accept an optional caller-supplied seed:

- `GenerateSong(SongConfig song, int? seedOverride = null)`
- `GenerateSinglePart(part, rolesForChannels, partIndex, bpmOverride, instrumentOverrides, seedOverride, patternOverrides)` — `instrumentOverrides` and `patternOverrides` are keyed on `MusicianTrackKey` (§5.3)

The contract:

- **Base-seed resolution happens exactly once per render call:**
  `baseSeed = seedOverride ?? settings.defaultSeed`.
- **Every derived seed comes from the base seed** — the per-part / per-repetition
  `GenContext.rng` and every per-track RNG (including the RNG stream that drives
  palette selection through the shared `PaletteSelector`). There is no seed site
  in the orchestrator that bypasses the base seed.
- **Seed policy is host-side.** When the seed changes and what it derives from
  (per-song, per-render, per-anything) is the caller's decision. The package
  never invents per-render entropy on its own; it only consumes what it is
  given. (Determinism invariant: same inputs + same seed => same outputs.)
- **Backward compatibility is bit-exact.** When no seed is supplied, all derived
  seed strings and arithmetic are identical to the pre-SEED-1 behavior
  (`defaultSeed`-anchored). This is guarded by golden-value regression tests in
  `Tests/Editor/SongOrchestratorSeedTests.cs` (FNV-1a goldens captured against
  the pre-batch seed-string formats).
- **Derivation seams are internal, testable functions** on `SongOrchestrator`:
  `ResolveBaseSeed`, `ResolveRepContextSeed` (`(base + partIndex*397) ^ rep`),
  `ResolvePartContextSeed` (`base + partIndex*397`), `ResolveTrackSeedSong`,
  `ResolveTrackSeedPart` (both FNV-1a over the legacy seed-string formats),
  `ResolveArticulationSeed` (FNV-1a over `"{trackSeed}|artic"`,
  MGP-ALWTTT-ARTIC-1) — a dedicated substream seed for the Random-articulation
  figure roll, derived from the per-track seed so it never consumes the shared
  `ctx.rng` stream; since CA-V1 it is consumed by the bass composer as well as
  the backing composer, which is safe precisely because `trackSeed` already
  folds in role and musicianId — `ResolveArticulationRateSeed` (FNV-1a over
  `"{trackSeed}|articrate"`, CA-V1) — a SEPARATE substream for the
  arpeggio-rate roll, kept apart from `|artic` so that enabling the rate
  sentinel cannot shift the figure sequence (D-V1-RATE-STREAM=A) —
  `ResolveVelocityJitterSeed` (FNV-1a over `"{trackSeed}|articvel"`, CA-V1) —
  consumed as a SEED FOR A PURE MIX rather than as a stream: no `System.Random`
  is constructed from it, which is what keeps the articulator RNG-free
  (D-V1-JIT-SRC=A) — and `ResolveTempoSeed`
  (FNV-1a over `"{baseSeed}|p={partIndex}|tempo"`, BPM-DET-1) — a dedicated
  per-part-occurrence substream seed for the render-path tempo roll (§5.2),
  derived from the base seed and independent of every arithmetic context seed
  above.
- **`GenContext.trackSeed`** exposes the per-track seed int behind the
  per-track RNG; `GenerateOne` swap/restores it exactly like `ctx.rng`.
  Composers may derive dedicated deterministic substreams from it but must
  not repurpose it as an entropy source of their own invention. Outside
  `GenerateOne` it is `0` (still deterministic).
- Consumer projects reach this surface through their render bridge (e.g.
  `MidiMusicManager.RenderSinglePart` forwards `seedOverride`); the bridge is a
  pure pass-through and holds no seed policy either.

### 5.2 Tempo resolution (BPM-DET-1)

Per-part tempo (BPM) is resolved once per part-occurrence, in strict precedence:

- **`GenerateSinglePart`:** `bpmOverride ?? PartConfig.ExplicitBpm ?? seeded-roll`.
- **`GenerateSong`:** `PartConfig.ExplicitBpm ?? seeded-roll` (there is no
  `bpmOverride` parameter on the song entry).

`PartConfig.ExplicitBpm` is a **live reader** on both entries (BPM-DET-1 flipped
it from written-never-read). The **seeded roll** picks uniformly from
`MusicTheory.GetValidBpms(range, rule)` — the same valid-BPM set the legacy
helper used (multiples-of-ten within the `TempoRange` band) — using a
`System.Random` seeded by `SongOrchestrator.ResolveTempoSeed(baseSeed, partIndex)`:
`SongOrchestrator.RollTempoBpm(tempoSeed, range, rule)`. Both are internal,
testable seams (`Tests/Editor/SongOrchestratorSeedTests.cs`).

Contract points:

- The tempo seed keys on `(baseSeed, partIndex)` only — **not** `rep`. Tempo is
  chosen per part-occurrence (the roll sits above the repetition loop), so all
  repeats of a part-occurrence share a tempo, and two `Structure` entries that
  reuse the same part index roll the **same** tempo (a deliberate consequence of
  D-BPM2-KEY; this output has no pre-seed golden to preserve).
- The roll is a dedicated substream: it draws from its own `System.Random` and
  never perturbs `ctx.rng` or any per-part/track draw.
- `MusicTheory.GetBPMFromRange` is **unchanged** and stays off the render path;
  its remaining callers (`ChordTrackComposer`, which reads only `BeatsPerMeasure`
  from `GetTimeSignatureDetails`) are unaffected by construction. Seed policy
  stays in the orchestrator; `MusicTheory` stays an unseeded helper.
- **Backward compatibility:** for any caller supplying `bpmOverride` (or a part
  with `ExplicitBpm` set), the resolved BPM — and therefore the render — is
  bit-identical to before. Only the previously-unseeded roll changes
  (unseeded → seeded); there is no reproducible baseline to preserve, so no
  golden is faked (determinism is asserted instead).

### 5.3 Per-track keying, readback, and per-render override (MGP-ALWTTT-DBG-1+3)

**Keying (D-DBG1=A).** Every per-track surface of `PartRender` and the
per-render override map is keyed on `MusicianTrackKey (musicianId, TrackRole)`,
not on `musicianId` alone. A single `musicianId` may own several roles in one
part (the BASS-1 case); a string key silently dropped the second role's stem /
instrument. The re-keyed surfaces are `PartRender.stemsByMusician`,
`melInstByMusician`, `percInstByMusician`, the new `resolvedByTrack`, and the
`GenerateSinglePart` `instrumentOverrides` parameter.

**Track identity tag (ID-1=A).** `SongOrchestrator` tags each rendered track
chunk with `mus:{musicianId}:{TrackRole}` (was `mus:{musicianId}`). The tag is
the single source of a chunk's identity; stem collection parses it back. The
format is internal surface (stamped and parsed only in `SongOrchestrator` via
`FormatMusicianTag` / `TryParseMusicianTag`); parsing treats the LAST `:`
segment as the role, so a `musicianId` containing `:` round-trips. A legacy
`mus:{id}` tag (no role segment) fails the parse and is skipped — stamping and
collection change together, so no mixed-format state exists within a render.

**Readback (Ask A, D-DBG2=A, D-DBG3=A).** `GenContext.ReportResolved :
Action<ResolvedTrackChoice>` is a per-track sink installed and collected by
`GenerateOne` with the same swap/restore discipline as `ctx.rng` / `ctx.trackSeed`
(null outside `GenerateOne`, and in `GenerateSong` there is no `PartRender` to
collect into). A composer invokes it AT MOST ONCE per `Compose` with what it
actually resolved; `ITrackComposer` is unchanged. The orchestrator's sink stamps
`(musicianId, TrackRole)` authoritatively — composers fill only content fields.
Source identity is by **source-asset name captured pre-clone** (D-DBG3=A); no
GUIDs at runtime. `ResolvedTrackChoice` content by role:
Rhythm → source / sourceAssetName / paletteName / proceduralStyleId;
Backing → source / sourceAssetName / paletteName / progressionRoman /
resolvedFigures (Random articulation only, emission order);
Melody → source / sourceAssetName (authored) or melodyArchetypesBySpan
(procedural, one entry per chord span);
Bassline → usesSharedProgression / source / progressionRoman.
**Harmony is not reported in v1** (ID-2=A — outside the ALWTTT Asks); the sink is
null-safe, so a Harmony track simply produces no `resolvedByTrack` entry.

**Per-render override (Ask C, D-DBG4=A).** `GenerateSinglePart` accepts a
trailing `IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> patternOverrides`.
The orchestrator installs the matching entry on `GenContext.patternOverride` for
exactly the duration of each track's `Compose` (swap/restored like `rng`). It is
**precedence step 0** in each composer — it wins over card override/palette,
`TrackParameters.Pattern`, recipes, and procedural generation. Composers
clone-on-apply and treat a type mismatch as **warn + ignore** (fall through to
the normal precedence chain). The value type is the common `PatternDataSO` base
(`DrumPatternData` / `ChordProgressionData` / `MelodyPatternData`). **Bassline
ignores the override in v1** (warn) — the bass renders the shared progression, so
overriding it there would open a second mutation path into shared state; override
the Backing track instead (its override is the shared progression, by the same
don't-overwrite discipline the card override already uses).

**Backward compatibility.** With no `patternOverrides` map (or an empty one) and
no seed, output is bit-identical to pre-batch: no composer consumes
`ctx.patternOverride` when it is null, `ReportResolved` is side-effect-free
observability, and the re-key does not alter draw order. Guarded by
`Tests/Editor/PatternOverrideAndReadbackTests.cs` (null-map == empty-map ==
re-run FNV equality) and `SongOrchestratorKeyingTests.cs`.

### 5.4 Consumer mix gain (MGP-MIX-1)

`GenerateSinglePart` accepts an optional per-render map
`IReadOnlyDictionary<MusicianTrackKey, float> mixGains`. Contract:

- **Per-entry emission gate.** A track emits exactly one CC7 (channel volume)
  on its own channel **iff** it has an entry in the map. Null map, empty map,
  or no entry ⇒ zero new events ⇒ bit-identical to the pre-seam render.
- **Value.** `clamp(round(Instrument.volume01 × gain × 100), 0, 127)`
  (`Mathf.RoundToInt`). Identity (1.0 × 1.0) = 100, the GM channel-volume
  default, so gain 1.0 is level-neutral next to un-entried tracks.
  `volume01` is authoring-clamped 0..1; boost headroom comes only from
  gain > 1 (saturation at gain ~1.27 for an unauthored instrument).
- **Insertion point.** `GenerateOne`, after `TrimFileToLength` /
  `TagTrackWithMusician`, before `ShiftFile` / `MergeInto`, via
  `MidiGenerator.ApplyChannelVolume` on the per-track file (lands after the
  bank/patch preamble). This gives `ApplyChannelVolume` its first package-side
  call site; the utility itself is unchanged.
- **Rhythm excluded in v1 (D-MIX-4=A).** All Rhythm tracks share channel 9
  (`BuildChannelMap`), so a per-musician CC7 there cannot target one drummer.
  A `TrackRole.Rhythm` entry is warn + ignore (same contract shape as
  Bassline in `patternOverrides` v1). Metronome: out by construction (not a
  musician track, never keyed).
- **Readback (D-MIX-5=A).** `PartRender.appliedCc7ByTrack
  : Dictionary<MusicianTrackKey, int>` — the CC7 actually emitted, entries
  only for gained melodic tracks. Orchestrator-stamped; deliberately NOT on
  `ResolvedTrackChoice` (that surface reports composer resolutions; this is
  an orchestrator application).
- **Determinism.** Pure data: no `ctx.rng`, no seed-chain involvement. Same
  seed + same map ⇒ same bytes. Test-pinned in
  `SongOrchestrator_MixGainTests` (identity gates, application, law, clamps,
  Rhythm exclusion, note-identity of the stripped render).
- `GenerateSong` does not take the map in v1 (no consumer of that path needs
  it; extend on demand).
- Playback-layer separation: `IMixController` / `PassthroughMixController`
  (live channel control via `IPlayMidi`, ducking/highlight) are a distinct
  plane. MIX-1 lives in the bytes; both compose at the synth.

## 6. Meter and timing rule

For package runtime rendering, the **Part time signature is authoritative**.

That rule matters for:

- metronome generation
- loop sizing
- normalization of authored data
- chord/rhythm pattern adaptation

## 7. What this document does not define

This SSoT does not define:

- detailed backing/rhythm/melody composer internals,
- ALWTTT composition-session bridge logic,
- `MidiMusicManager` playback/cache semantics.

See the relevant composer SSoTs or cross-project reference docs.

## 8. Update triggers

Update this SSoT when:

- orchestration stages change,
- composer registration or selection changes,
- `GenContext` meaning changes,
- meter/loop/render contracts change,
- the seed-threading contract (§5.1) changes: new seed sites, a change to base-seed
  resolution, or any move of seed policy into the package,
- the tempo-resolution contract (§5.2) changes: the precedence
  (`bpmOverride ?? ExplicitBpm ?? seeded-roll`), the tempo seed's key material,
  or the split between the orchestrator's seeded roll and `MusicTheory`'s
  unseeded helper,
- the `PartRender` keying, the `mus:` tag format, the `ReportResolved` /
  `patternOverride` `GenContext` surface, or the per-render override precedence
  change (MGP-ALWTTT-DBG-1+3, §5.3),
- the consumer mix-gain contract (§5.4) changes: the per-entry emission gate,
  the composition law, the identity scale, the keying, the Rhythm exclusion,
  or the `appliedCc7ByTrack` readback shape (MGP-MIX-1).

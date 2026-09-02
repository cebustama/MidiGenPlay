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
- **rhythm onset channel** (`GetRhythmOnsetsForPart` /
  `SetRhythmOnsetsForPartMusician`, MGP-ALWTTT-BASS-POCKET-1, D-PKT-SRC=B):
  a per-part publish/consume cache, same mould as the progression and melody
  caches, wired identically in BOTH entry points. List-backed per part so
  "first publisher wins" is publication (track-list) order by construction;
  empty/null publications are ignored (indistinguishable from not
  publishing). The helper factories `SongOrchestrator.CreateSetRhythmOnsetsForPartMusician`
  / `CreateGetRhythmOnsetsForPart` are `public static` test seams — see the
  convention note in §5.6. This channel introduces the package's first
  composer→composer DATA dependency (Rhythm publishes, Bass consumes); it is
  order-sensitive by design (D-PKT-ORDER=A) and the consumer owns the degrade
  path.

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
- `ResolveWalkSeed` (FNV-1a over `"{trackSeed}|walk"`, B3 WALK-2) — the
  improvised-walk substream KEY. Unlike the roller seeds it never feeds a
  stateful `System.Random`: the bass consumes it as the key of a pure
  per-(event, hit) integer mix (the VelocityJitter idiom), so no draw order
  exists downstream of it and no toggle can shift anything derived from it.
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

### 5.5 Host-supplied default progression (MGP-ALWTTT-BASS-SOLO-1)

**Surface (D-SOLO-SURF=A2).** `GenerateSinglePart` accepts a trailing optional
`ChordProgressionData defaultProgression`, declared on `ISongOrchestrator` and
implemented by `SongOrchestrator`. Before the track loop, the orchestrator
pre-seeds the per-render `progressionByPart` cache with it, so the shared
channel is populated for parts that have no Backing track and therefore no
publisher. `GenerateSong` does NOT take the parameter in v1 (the jam path is
`GenerateSinglePart`).

**Guard (D-ORD-GUARD=A, supersedes D-SOLO-GUARD=A).** The default is warn +
ignore only when a Backing track CARRIES A HARMONY SOURCE; an
articulation-only Backing row does not displace it. See §5.7 for the sniff, the
appended `SeededBackingArticulationOnly` result, and the recorded palette-pick
edge. The warning still names the alternative — the per-render `patternOverride`
on the Backing track (§5.3, precedence step 0).

**Semantics.** Seeded as-is (no TS normalization on this path, D-SOLO-NORM=A —
hosts author the default in the part TS) and as a name-preserving runtime clone,
so no runtime state aliases the asset. RUNTIME-REQUALITY (§4.1 of
`authoring/SSoT_Authoring_Chord_Progressions.md`) is applied here for opt-in
assets, since no backing composer runs on this path to do it; when requality
clones, that clone IS the seeded instance (single clone, never two).

**Backward compatibility.** A null default (the default value) performs no
seeding at all: zero rng draws, zero allocations, byte-identical output. The
seam is exposed as the public static `SongOrchestrator.TrySeedDefaultProgression`
returning `DefaultProgressionSeedResult { NotSupplied, Seeded,
IgnoredBackingPresent }`; guarded by
`Tests/Editor/SongOrchestrator_DefaultProgressionTests.cs`.

Because it shares the entry point, this backing-less seed path inherits B1
with no edits of its own: the color table (when the default enables it) and
secondary-dominant resolution run here too. Zero code changes in
`SongOrchestrator`.

**Seam compatibility.** The original 3-parameter
`TrySeedDefaultProgression(part, default, cache)` is retained VERBATIM in
behavior, guard included (binary "any Backing present"), as the pinned
pre-ORDER-1 seam; `SongOrchestrator_DefaultProgressionTests` runs against it
unmodified and is the BC pin. The orchestrator calls the 4-parameter overload.
Both share one seeding core (clone + requality + cache write).

### 5.6 Test-seam visibility convention (F-IVT-STALE, recorded at B0)

Named orchestration/composer seams that exist for EditMode tests are declared
`public static` in practice, not `internal`. `Runtime/AssemblyInfo.cs` carries
`[assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]`, but no test in the
package exercises internal access, and the members its comment cited as
"internal seams" (`ChordTrackComposer.TryDirectionalFirstChordCore`,
`SongOrchestrator.ResolveTrackSeedPart`) are public — as are
`BassTrackComposer.ResolveArticulation`, `TrySeedDefaultProgression`,
`DefaultProgressionSeedResult`, `ChordProgressionRequality.TryMapCoreQuality`,
the §5 onset-channel factories, and (MGP-ALWTTT-HARMONY-1)
`HarmonyTrackComposer.ResolveHarmonyNotesCore`, `.ResolveGuideMelody` and
`.ResolvedHarmonyNote`. The directive is therefore INERT (likely a
test-assembly name mismatch) and is retained only as an escape hatch. **The
convention on record is `public`**; a batch that wants the internal discipline
back must first confirm the real test `.asmdef` name and re-run the suite. Doc
sites that described any of the above as "internal" were corrected at B0.

### 5.7 Composition passes and deferred merge (MGP-ALWTTT-BASS-ORDER-1)

**Passes (D-ORD-MECH=A).** Both entry points (`GenerateSinglePart` and
`GenerateSong`, D-ORD-SCOPE=A) compose each part in three passes over the SAME
track list:

- **PASS 0 — `Backing`.** The shared-harmony publisher. Runs first,
  unconditionally, so the resolved / TS-normalized / re-qualified progression is
  published before any consumer composes.
- **PASS 1 — everything except `Backing` and `Harmony`.**
- **PASS 2 — `Harmony`.** Reads Melody via the guide-note cache; the cache's
  contract, including which melody is followed and what Harmony writes back,
  is §5.9.

**Deferred merge.** Passes no longer merge into the part file as they go. Each
track's composed, trimmed, tagged, gain-applied and shifted `MidiFile` is
parked in a slot indexed by its position in the track list; after PASS 2 the
slots are merged in INDEX order. The merged chunk sequence
`[meta, metro, track0..N]` therefore follows the TRACK LIST, not the compose
order, and is byte-identical to the pre-ORDER-1 layout whenever per-track
content is unchanged.

**Why this is safe for identity.** Channel allocation, `ChannelRoles` and the
`mus:` tags already derive from list position, and per-track seeds already key
on `(role, musicianId)` — never on compose order (§5.1). No PASS 1 composer
consumes `producedByRole` for a Backing entry. Cross-track PUBLICATION
(progression cache, rhythm onsets, melody guide notes) still happens at each
track's own compose time; only the physical merge moved.

**Log note.** `GenerateOne` still emits its `Merged [role]` line at compose
time (text unchanged for log-tooling compatibility), so log order now reflects
COMPOSE order — Backing first — while the file's chunk order follows the list.
This asymmetry is intentional.

**Host-default guard (D-ORD-GUARD=A).** The orchestrator's own seeding call
uses a 4-parameter `TrySeedDefaultProgression` overload whose guard is the
static sniff `BackingTrackCarriesHarmonySource(trackCfg, renderOverride)`:
true iff a per-render `ChordProgressionData` override, a card
`progressionOverride`, a card palette with ≥ 1 valid entry (non-null
progression AND weight > 0), or an authored `TrackParameters.Pattern` is
present. A Backing row with none of these is articulation-only and does NOT
displace the host default; the seeded default is then consumed by the Backing
composer's shared-cache step (and thereby TS-normalized and re-qualified —
better than the raw D-SOLO-NORM=A path). Pure: zero rng draws, reads only
serialized asset / override state.

`DefaultProgressionSeedResult` gains the appended member
`SeededBackingArticulationOnly` (existing values unchanged).

**Precedence for the shared progression, final form:**

1. per-render `patternOverride` on the Backing track (imposes unconditionally),
2. Backing card: `progressionOverride`, else a weighted palette pick,
3. **host `defaultProgression`** — now also under an articulation-only Backing row,
4. Backing track's authored `TrackParameters.Pattern`,
5. procedural generation.

**Readback (D-ORD-RB).** `PartRender` gains `sharedProgressionSource`
(`ResolvedSource`, default `None`) and `sharedProgressionAssetName`, stamped
once at the end of `GenerateSinglePart` by the pure seam
`StampSharedProgressionReadback`. `ResolvedSource` gains the appended member
`HostDefault = 7` (values 0..6 are unchanged serialized/logged surface).
Mapping: the FIRST Backing entry in track-list order supplies the source,
except that `SharedProgression` + a seed having happened maps to
`HostDefault`; with no Backing entry, a seed maps to `HostDefault` and no seed
maps to `None` (consumers used private harmony, or nothing rendered).
Composers never report `HostDefault` — it is an orchestrator-level statement
about which source WON the shared channel, and it exists so hosts can key
render caches on that fact instead of the now-invalid "part has no Backing"
proxy.

### 5.8 Shared-progression carry channel (MGP-MEL-1 P7, D6=B)

**Readback.** `PartRender.sharedProgressionData` — a runtime CLONE of whatever
won the shared channel, taken after normalization and requality, with its name
preserved so it matches `sharedProgressionAssetName` (§5.7). Null when nothing
won the shared channel. Zero rng draws. It is a runtime instance, not an
asset: valid for the session, and it must never be written to disk.

**Host-side jam-continuity recipe.** Documented discipline; the package
mechanism is unchanged and D-ORD-GUARD stays as-is.

1. After every render, the host stores `render.sharedProgressionData`.
2. Next card is Backing WITH a harmony source and the tonality does NOT change
   → the host passes the stored progression as
   `patternOverrides[(backingMusician, Backing)]`. Precedence step 0 imposes
   and publishes it; the card contributes articulation and voicing only.
3. The tonality DOES change (the card carries a `TonalityEffect`, or adopts)
   → either let the card's harmony win (current behaviour), or "transport":
   impose the SAME stored object under the new part tonality. Degree-based
   data re-renders in the new mode for free — no transposition utility is
   needed or planned.
4. Backing played FIRST, with no prior jam harmony → unchanged: its
   progression leads.

The same recipe covers bass, melody and harmony, since all of them consume
`GetProgressionForPart`.

### 5.9 Melody guide-note cache (MGP-ALWTTT-HARMONY-1)

The per-part / per-musician melody cache is the channel by which PASS 2
(`Harmony`) follows a line composed in PASS 1 (`Melody`). Written by
`ctx.SetMelodyForPartMusician(part, musicianId, guideNotes)`; read by
`ctx.GetMelodyForPartMusician(part, musicianId)` and
`ctx.GetFirstMelodyMusicianIdForPart(part)`. Payload units are Part beats
(`SSoT_Composer_Melody_Track.md`, "Guide-note handoff").

**Lifetime.** The cache is constructed fresh **per repetition** inside
`GenerateSong`'s repetition loop, and once per call in `GenerateSinglePart`.
Nothing written during one repetition is visible to the next. Any reasoning
that depends on state carrying across repetitions is wrong.

**Which melody is followed (D-H1-5a=B).** `HarmonyTrackComposer` resolves its
target in two steps, in this order:

1. **Its own `MusicianId`**, by exact-key lookup, if the cache holds a
   non-empty melody for it. This is the SELF-HARMONY case and it is the normal
   case, not an edge: one musician holding both a Melody and a Harmony track is
   how a single voice harmonizes itself. Being an exact-key lookup, it does not
   depend on cache enumeration order.
2. Otherwise `GetFirstMelodyMusicianIdForPart` — the first Melody track in
   **track-list order** that published notes. PASS 1 composes in list order and
   the cache is insert-only, so this is stable in practice; it is nonetheless
   an ordering dependency, and a part with two melodies and a harmony musician
   who holds neither will follow whichever melody the host listed first.

With no usable melody, Harmony warns and emits an empty file. It never
fabricates a line.

**What Harmony writes back (D-H1-5b=A).** Harmony publishes its own line into
the SAME cache under its own `MusicianId`, so that a further voice could
harmonize the harmony. In the self-harmony case this REPLACES the entry the
Melody composer published under that key. This is benign for three independent
reasons, and all three must hold for it to stay benign:

1. Harmony is PASS 2, the last pass, so no non-Harmony composer reads the cache
   after the write.
2. The write swaps the list REFERENCE; the Melody composer's list is never
   mutated in place, and the Melody `MidiFile` is built before publication, so
   the rendered `mus:{id}:Melody` stem is already fixed. **Consumers hanging an
   articulatory singer off the rendered Melody stem via `RenderSinglePart` are
   therefore unaffected by this write** — stems are keyed
   `MusicianTrackKey(musicianId, role)`, and `Melody` and `Harmony` are
   distinct roles.
3. The cache is re-created per repetition and per single-part render (see
   Lifetime), so nothing leaks forward.

**Known edge, registered not fixed.** A SECOND Harmony track for the same
musician in the same part would resolve step 1 to the FIRST harmony's line and
harmonize that, not the melody. Deferred with the rest of the cache-contract
work (audit item 6).

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
  or the `appliedCc7ByTrack` readback shape (MGP-MIX-1),
- the rhythm onset channel changes (§5): its publish/consume shape, the
  first-publisher-wins ordering, or the empty-publication rule
  (MGP-ALWTTT-BASS-POCKET-1),
- the host-supplied default-progression channel changes (§5.5): the guard, the
  seeding site relative to the track loop, the clone/normalization discipline,
  or its extension to `GenerateSong`,
- the test-seam visibility convention (§5.6) changes, or the
  `InternalsVisibleTo` directive is repaired or removed.
- the composition-pass structure or the merge discipline (§5.7) changes: which
  roles compose in which pass, the deferred index-ordered merge, or the
  claim that chunk order follows the track list,
- the harmony-source sniff, the shared-progression precedence list, or the
  `PartRender.sharedProgressionSource` / `ResolvedSource.HostDefault` readback
  (§5.7) changes.
- the shared-progression carry channel (§5.8, MGP-MEL-1 P7) changes: the
  `PartRender.sharedProgressionData` clone point, its name preservation, its
  null semantics, or the documented host jam-continuity recipe.

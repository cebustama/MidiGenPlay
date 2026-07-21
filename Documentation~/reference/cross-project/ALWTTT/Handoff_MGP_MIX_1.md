# Handoff — MGP-MIX-1: consumer-side mix gain (MidiGenPlay → ALWTTT)

> Date: 2026-07-20 · Package version carrying the seam: **1.2.0**
> Decisions: D-MIX-1=A · D-MIX-2=A · D-MIX-3 (multiplicative, per-entry gate,
> ×100 identity) · D-MIX-4=A (Rhythm out of v1) · D-MIX-5=A · D-MIX-6 (volume01
> authoring in a later version).
> For ALWTTT: register in Boundary §8 and plan the balance batch against the
> surface in §4 below.
>
> **VERSION CORRECTION (2026-07-21).** The companion MGP-BAGGAGE-1 handoff told
> you to pin **1.1.0**. That bump was never materialized in `package.json`;
> **1.1.0 was never published and does not exist.** MidiGenPlay goes
> **1.0.0 → 1.2.0** in a single jump, and 1.2.0 carries *both* MGP-BAGGAGE-1
> (catalogue cleanup) and MGP-MIX-1 (this seam). **Pin 1.2.0.** Everywhere the
> BAGGAGE-1 handoff says 1.1.0, read 1.2.0.

## 1. Application point — CC7 at generation time (D-MIX-1=A)

The gain is baked into the generated MIDI as one **CC7 (channel volume)** event
per gained track, inserted on that track's channel right after its bank/patch
preamble, **before** the part-position shift. Your three requirements drove
this: (a) it lands in the bytes, so your byte-comparing regression gates see
it; (b) velocities are untouched, so the D-CSV-18 per-instrument listening
verdicts stay valid (soundfont velocity shifts timbre, CC7 shifts only level);
(c) no playback-layer state. `IMixController` / `PassthroughMixController`
remain a **separate, live-playback concern** (ducking/highlight through
`IPlayMidi`); the two planes compose at the synth (CC7 in bytes × live channel
control) and neither replaces the other.

## 2. Granularity — per musician-track (D-MIX-2=A)

Keyed on **`MusicianTrackKey (musicianId, TrackRole)`**, the same struct as
`instrumentOverrides` / `patternOverrides` / stems — your BASS-1 keying
end-to-end. Per-role balance is expressible by supplying the same gain for
every musician holding a role. Channels never appear in your surface.

**Percussion is out of v1** (D-MIX-4=A): every Rhythm track shares MIDI
channel 9, so a per-drummer CC7 is not expressible; a `TrackRole.Rhythm` entry
is warn+ignore (same contract shape as Bassline in `patternOverrides` v1).
Revisit if you need drum-level balance — say so and we'll scope it.

## 3. Composition law and default (D-MIX-3)

`effectiveCc7 = clamp(round(volume01 × gain × 100), 0, 127)`

- **Multiplicative**, as you asked: `volume01` is the package-side nominal
  per-instrument level (today 1.0 on all 70, unauthored); `gain` is yours.
  Default `gain = 1.0` (identity). Future package-side normalization of
  `volume01` will flow through your gains unchanged.
- **Per-entry emission gate:** a CC7 is emitted **only** for tracks with an
  entry in your map. Null map, empty map, or a track without an entry ⇒ zero
  new events ⇒ **bit-identical to the pre-MIX-1 render** (i.e. to what 1.2.0
  produces with the seam unused, which is byte-for-byte what 1.0.0 produced for
  the same inputs — the BAGGAGE-1 cleanup removed assets, not render
  behaviour). Not adopting the seam changes nothing, guaranteed at the byte
  level and test-pinned.
- **Identity scale = 100** (the GM channel-volume default): a gain of 1.0
  produces CC7=100, level-neutral next to tracks with no entry — partial
  adoption doesn't jump levels. Boost headroom: since `volume01` is
  authoring-clamped to 0..1, values above nominal come only from `gain > 1`,
  saturating at gain ≈ 1.27 (CC7=127) for an unauthored instrument.
  ⚠ **One verification on your side:** confirm MPTK initializes channel volume
  at 100 when no CC7 is present. If its default differs, tell us — the
  identity constant is a one-line change, the contract is unaffected.
- `gain = 0` or `volume01 = 0` ⇒ CC7=0: the track is muted at playback but its
  note events remain in the file (stems, hashes and readback keep working).
- Determinism: the gain path is pure data — no RNG, no seed-chain
  involvement. Same seed + same map ⇒ same bytes (test-pinned, including a
  "strip the CC7s and the render is note-identical to baseline" assertion).

## 4. The surface you invoke

```csharp
// ISongOrchestrator (package 1.2.0) — trailing optional parameter:
PartRender GenerateSinglePart(
    SongConfig.PartConfig part,
    IReadOnlyList<TrackRole> rolesForChannels,
    int partIndex,
    int? bpmOverride = null,
    Dictionary<MusicianTrackKey, MIDIInstrumentSO> instrumentOverrides = null,
    int? seedOverride = null,
    IReadOnlyDictionary<MusicianTrackKey, PatternDataSO> patternOverrides = null,
    IReadOnlyDictionary<MusicianTrackKey, float> mixGains = null);   // NEW
```

Readback: `PartRender.appliedCc7ByTrack : Dictionary<MusicianTrackKey, int>` —
the CC7 actually emitted, entries only for gained melodic tracks. Useful for
your debug UI and as a cheap pre-byte assertion.

Notes for your side:
- Adding a trailing optional parameter is **source-compatible for callers**
  but **breaking for any test double implementing `ISongOrchestrator`** — if
  you stub the interface, add the parameter to the stub.
- `GenerateSong` (full-song path) does **not** take the map in v1; your render
  loop is per-part. If an export path needs it, ask and we extend.

## 5. volume01 authoring — later version (D-MIX-6)

Authoring the 70 `volume01` values is a package-side content batch, shipping
in a **later version**, after your D-CSV-18 listening verdicts land — they are
the input that batch needs. MIX-1 (1.2.0) is safe to adopt before that:
because the law is multiplicative and your default is identity, the later
normalization will compose with whatever gains you've set, not fight them.
Open (deliberately undecided until then): whether authored `volume01` values
emit CC7 for tracks *without* a gain entry.

## 6. Package-side documentation state (2026-07-21)

Applied and current as of package 1.2.0:

- `runtime/SSoT_Runtime_Generation_Orchestration.md` **§5.4** — mechanism.
- `runtime/SSoT_Runtime_Song_Model_and_Config.md` **§3.2** — `volume01`
  meaning and its boundary with consumer gain.
- `SSoT_CONTRACTS.md` **§8** — the contract you can hold us to.
- `changelog-ssot.md`, `CURRENT_STATE.md`, `ssot_manifest.yaml` — close-out.

If any statement in this handoff conflicts with those documents, **the package
SSoTs win**; this file is cross-project reference and defines no package truth.

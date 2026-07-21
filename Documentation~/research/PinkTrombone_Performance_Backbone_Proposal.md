# Pink Trombone as a Performance Backbone for MidiGenPlay

**Type.** Technical evaluation / integration proposal — read-only.
**Status.** Evaluated and accepted with deferred sequencing (see `PinkTrombone_Proposal_Agent_Review.md` in the same folder).
**Authority.** None claimed. This document proposes additions; it does not modify any SSoT. Documentation updates listed in §8 happen only if and when Phase B is greenlit.

---

## 0. Provenance and prior art

Pink Trombone is the interactive articulatory voice synthesizer by Neil Thapen (2017), originally an MIT-licensed single-file JavaScript/Canvas/Web Audio API project hosted at `dood.al/pinktrombone`. It implements a source–filter physical model: a Liljencrants–Fant glottal flow source feeding a 44-segment Kelly–Lochbaum 1-D digital waveguide vocal tract (with a 28-segment nasal branch attached at the velum), computed at 2× the audio sample rate for correct formant placement, plus band-passed white noise for aspiration/frication and impulsive transients on stop releases.

The relevant C# substrate for this proposal is **`lostmsu/pink-trombone-mod`**, a 100% C# port of `chdh/pink-trombone-mod` (the modularized TypeScript translation of Thapen's original), MIT-licensed, published as the `PinkTrombone` NuGet package. Algorithm parity is one-to-one with the original.

Other notable ports (informational, not consumed by this proposal): `PaulBatchelor/voc` (literate-programming ANSI C, source-readable explanation of the LF model and the waveguide math); `chdh/pink-trombone-mod` (modular TypeScript reference); `cutelabnyc/pink-trombone-cpp` and `cutelabnyc/pink-trombone-plugin` (C++ / JUCE); `giuliomoro/pink-trombone` (Bela embedded C++); `yonatanrozin/Modular-Pink-Trombone` (AudioWorklet-based, multi-voice). The C++/JUCE work also produced `VegaDeftwing/PinkTromboneVCV` (VCV Rack module). Together these are evidence that the algorithm is small (~400 lines of DSP), well-cited, and ports cleanly across languages and audio backends.

---

## 1. Framing: where Pink Trombone actually fits

Pink Trombone is an **audio-rate physical synthesizer**, not a melody generator. It has no concept of pitch class, scale degree, chord function, or phrase. Its inputs are a fundamental frequency in Hz, a glottal tenseness/loudness pair, a 2-DOF tongue (index + diameter), zero-or-more vocal-tract constrictions, and a velum opening. Its output is a stream of `float32` samples produced one PCM frame at a time. The C# port exposes this as a single `PinkThrombone.Synthesize(Span<float>)` call driven on Unity's audio thread.

The brief is "melody generation and interpretation backbone." The honest mapping into MidiGenPlay's architecture is:

- **Generation.** Pink Trombone cannot serve as `IMelodyStrategy` or `PhraseArchetypeSO`. It does not produce symbolic note events. Nothing in §3.2 of the implemented melody runtime has a hook where audio synthesis would be the natural output. **Recommendation: do not attempt to graft Pink Trombone into the composition stage.** The procedural Stage 1 (`PhrasePlanner`) and Stage 2 (`IMelodyStrategy.PickNext`) should remain symbolic and unchanged.
- **Interpretation / performance.** This is where Pink Trombone fits naturally. MidiGenPlay's procedural runtime already emits exactly the structured performance metadata an articulatory synth wants: `PhraseSlot.isAccent`, `PhraseSlot.isPhraseEnd`, `contourHint`, `playNote`, slot duration, and the `MelodicLeadingConfig` velocity bands. Currently this metadata only resolves to MIDI velocity (§3.2, `MelodyTrackComposer` step 7) and is then thrown away. A Pink Trombone–backed player can consume the same metadata and produce a far richer articulation than MPTK's GM SoundFont playback affords.

The remainder of this report assumes the "interpretation/performance" framing.

---

## 2. Proposed integration topology

The integration introduces no new authority into the package and slots in alongside MPTK at the playback boundary.

```
                  PACKAGE BOUNDARY
                          │
SongConfig ──► MidiGenerator ──► MidiFile  ──┼──► MidiMusicManager  ──► MPTK (default)
                  │                          │              │
                  │                          │              └──► PinkTromboneVoicePlayer  (NEW, ALWTTT-side)
                  │                          │                       │
                  ▼                          │                       │
        PerformanceMetadataSink              │              Subscribes to channel(s) for
        (NEW, package-side optional)         │              TrackRole.Melody / TrackRole.Lead
                  │                          │                       │
                  └──────── parallel ────────┼──────────►  Reads sidecar metadata
                            sidecar          │              per note-on, drives synth params
                            stream           │
```

Three structural facts about this topology:

1. **The package continues to produce a `MidiFile`.** No change to `MidiGenerator.GenerateSong`'s return signature, no change to the deterministic-MIDI invariant in §5 of the current state report.
2. **A new package-side abstraction `IPerformanceMetadataSink` (working name) optionally receives per-slot articulation metadata** during `MelodyTrackComposer.Compose`. If no sink is attached, nothing changes; the composer behaves exactly as today. The sink is package-defined but consumer-implemented — same pattern as `ITrackComposerFactory`.
3. **`PinkTromboneVoicePlayer` lives on the ALWTTT side**, registered through `MidiMusicManager` as a per-track playback target alternative to (or in parallel with) MPTK. It consumes the MidiFile's note-ons plus the sidecar metadata stream. This respects the §7.6 boundary: synthesis backend is not package theory.

---

## 3. The C# substrate

`lostmsu/pink-trombone-mod` is a direct C# translation of `chdh/pink-trombone-mod` (the modularized TypeScript), MIT-licensed, published as the `PinkTrombone` NuGet package. Its surface:

```csharp
var synth = new PinkThrombone(sampleRate);
// Per-frame parameters (read on audio thread, set on main thread)
synth.Frequency      = ...;   // Hz
synth.Tenseness      = ...;   // 0..1
synth.Loudness       = ...;   // 0..1
synth.TongueIndex    = ...;   // ~12..28
synth.TongueDiameter = ...;   // ~0..3.5
// Vocal-tract constrictions, velum, vibrato (frequency, amount, wobble)
// (Exact field names follow the chdh TS port one-to-one)

synth.Synthesize(buffer.AsSpan().Slice(offset, count));
```

Properties relevant to MidiGenPlay's invariants:

- **Mono only.** One Pink Trombone instance produces one voice. This matches `TrackRole.Melody` and `TrackRole.Lead` semantics (already monophonic at the slot level) but does **not** fit Harmony / Backing / Rhythm. Out of scope for those roles.
- **Sample-rate locked at construction.** `AudioSettings.outputSampleRate` is read at construction; an `AudioSettings.OnAudioConfigurationChanged` handler must rebuild the synth if the user changes audio settings mid-session.
- **Stateful between calls.** The 44-segment waveguide carries acoustic energy across `Synthesize` calls. There is no "reset" semantic between musical phrases unless explicitly issued. This matters for cross-phrase artifacts (§9 risk 4).
- **Internal RNG.** The glottis aspiration noise and simplex-wobble noise use a pseudo-random source. Whether the C# port exposes a seeding API needs verification; if not, exposing one is a precondition for honoring the §5 determinism invariant (see §7.2 below).
- **Unity compatibility.** The package targets modern .NET; the README documents a `float→double, MathF→Math` retrofit for .NET Standard 1.x. For Unity 2021 LTS+ on `.NET Standard 2.1`, the unretrofitted build should consume cleanly via NuGetForUnity or a hand-built DLL drop into `Assets/Plugins/`.

---

## 4. The data plane: what metadata flows to the synth

Two channels of information must reach `PinkTromboneVoicePlayer`. The first is already in the package output; the second is the new addition.

### 4.1 Channel A — the MidiFile (unchanged)

Standard note-on / note-off events with pitch, velocity, channel, and absolute timing. `PinkTromboneVoicePlayer` uses these to:

- Convert MIDI pitch to `Frequency` via standard `f = 440 * 2^((n - 69)/12)`.
- Use the channel filter to select which musician(s) this player voices (one player instance per Pink-Trombone-routed musician).
- Use velocity to scale `Loudness` (existing velocity bands from `MelodicLeadingConfig` already encode accent/phrase-end information losslessly into the 0..127 range).

This channel alone is sufficient for a *minimal* integration. Even with zero new package surface, you can wire Pink Trombone to play the existing MIDI output. The result will sound articulate but generic — no phrase-aware vibrato, no characteristic-degree color, no contour-aware tongue motion.

### 4.2 Channel B — the new `IPerformanceMetadataSink`

To unlock Pink Trombone's expressive surface, the package needs a way for `MelodyTrackComposer` to emit per-slot performance metadata that MIDI cannot carry without lossy encoding. Proposed minimal interface:

```csharp
// Package-side, in Runtime/.../Composition/Performance/
public interface IPerformanceMetadataSink
{
    void OnSlotRendered(in PerformanceSlotInfo info);
}

public readonly struct PerformanceSlotInfo
{
    public readonly int     PartIndex;
    public readonly int     Repetition;
    public readonly TrackRole Role;
    public readonly string  MusicianId;
    public readonly int     Channel;

    public readonly double  WhenBeat;
    public readonly double  DurBeats;
    public readonly int     Pitch;          // MIDI note number
    public readonly int     Velocity;       // 0..127, already resolved
    public readonly bool    PlayNote;       // false = explicit rest

    public readonly bool    IsAccent;
    public readonly bool    IsPhraseEnd;
    public readonly bool    IsPhraseStart;
    public readonly ContourDirection ContourHint;
    public readonly int     PhraseId;       // for grouping
    public readonly int     SlotIndexInPhrase;
    public readonly int     SlotCountInPhrase;
}
```

**Where the sink is wired.** `MelodyTrackComposer` constructor (or `Compose` parameter via `GenContext`) accepts an optional `IPerformanceMetadataSink`. Step 8 of the current composer flow (per §3.2 of the current state report) becomes: write note to `PatternBuilder`, **and if sink is non-null, call `sink.OnSlotRendered(...)`**. No other behavior changes.

**Authority claim.** This is a new package-owned interface. `PerformanceSlotInfo` is the package's promise about what per-slot metadata it can expose. The contents above are a strict subset of information `MelodyTrackComposer` already has on the stack; the interface adds no new computation, only a fan-out.

**Where the sink is implemented.** ALWTTT, inside `MidiMusicManager`. The manager owns the sink instance per render, captures the stream into a part-keyed dictionary, and hands it (alongside the cached `MidiFile`) to `PinkTromboneVoicePlayer`. This mirrors the existing cache-keyed-by-part pattern in §7.2 of the current state report.

**Non-goal.** This sink is *not* a generalized event bus, not a card-trigger pipeline, not a replacement for marker events in MIDI. It is exactly one thing: "the composer just decided to render slot X with properties Y." Other roles can implement the same sink later if useful, but Phase 1 scope is `MelodyTrackComposer` only.

---

## 5. Articulation mapping (the actual sound)

This is the core of the proposal: how `PerformanceSlotInfo` and the existing `MelodicLeadingConfig` resolve to Pink Trombone parameters per note. All values below are recommendations for the initial mapping table; a `MelodyVoiceProfileSO` (ALWTTT-side, see §8) parameterizes the curves.

| MidiGenPlay metadata | Pink Trombone parameter | Mapping |
|---|---|---|
| `Pitch` | `Frequency` | `440 * 2^((Pitch - 69)/12)`, smoothed over ~5–20 ms to avoid clicks. Smoothing time is part of the voice profile. |
| `Velocity` (already accent-aware via `MelodicLeadingConfig`) | `Loudness` and base `Tenseness` | `Loudness = (Velocity / 127)`. `Tenseness_base = lerp(0.5, 0.9, Velocity / 127)`. |
| `IsAccent` | `Tenseness` short bump | At note start, briefly raise `Tenseness` by +0.10 with a ~30 ms exponential decay to base. |
| `IsPhraseEnd` | `vibratoAmount`, `vibratoFrequency`, `Tenseness` taper | Over the last `min(DurBeats * 0.6, 0.5s)` of the slot: ramp `vibratoAmount` from base to base + 0.012, drop `Tenseness` by ~0.1 toward the end. |
| `IsPhraseStart` | tract reshape pre-roll | If preceded by ≥150 ms rest, allow tract to drift toward neutral diameter before reshaping. |
| `ContourHint` (Up / Down / Static) | `TongueDiameter` bias | Adjust `TongueDiameter` target by ±0.15 from the per-pitch nominal, biased over the phrase. Subtle "leaning forward / back" character. |
| Modal degree (from chord-tone vs. scale-tone vs. characteristic-degree) | `TongueIndex` color | Characteristic degrees get a slightly fronted tongue (`+0.5`–`+1.0` index), tonic gets the neutral, chromatic passing tones get a darker (lower diameter) shading. Drives perceived "vowel color" per scale degree. Requires the strategy to expose the degree on the sink — see §9 risk 3. |
| `playNote == false` (rest) | `Loudness → 0`, hold tract | Drop loudness over ~20 ms but **do not reset tract state** — articulation across rests is a feature, not a bug. Reset only on `IsPhraseStart` (above). |
| `DurBeats < 0.15` (fast notes) | suppressed vibrato, brighter attack | Short notes skip the vibrato ramp entirely. |
| Inter-note gap > X ms | brief aspiration burst | Tiny aspiration noise injection at note-on, modeling consonantal articulation between vocalic notes. Optional, profile-gated. |

The mapping table is intentionally **monotonic and continuous** — every input is a smooth function of a single MidiGenPlay variable, with no cross-coupling. This makes the mapping cheap to evaluate per audio block and easy to A/B test.

The table is the only place where music-theory concepts meet articulatory ones. Everything else in the proposal is plumbing.

---

## 6. Audio-thread wiring

```csharp
[RequireComponent(typeof(AudioSource))]
public sealed class PinkTromboneVoicePlayer : MonoBehaviour
{
    PinkThrombone synth;
    Queue<NoteEvent> upcoming;          // populated on main thread from MidiFile
    PerformanceMetadataLookup metadata; // captured from sink, keyed by (beat, pitch)
    MelodyVoiceProfileSO profile;
    float[] mono;

    void Awake() => synth = new PinkThrombone(AudioSettings.outputSampleRate);

    void OnAudioFilterRead(float[] data, int channels)
    {
        int frames = data.Length / channels;
        if (mono == null || mono.Length < frames) mono = new float[frames];

        // 1. Advance playback clock by `frames / sampleRate` seconds.
        // 2. Pop any note events whose timestamp falls inside this block.
        // 3. For each block-frame, resolve current target params from the
        //    active note + its PerformanceSlotInfo via the §5 mapping table.
        // 4. Smooth toward targets (one-pole) and write to synth fields.
        // 5. synth.Synthesize(mono.AsSpan(0, frames)).
        // 6. Splat mono into every channel.

        // ...

        synth.Synthesize(mono.AsSpan(0, frames));
        for (int i = 0; i < frames; i++)
            for (int c = 0; c < channels; c++)
                data[i * channels + c] = mono[i];
    }
}
```

Critical audio-thread rules:

- **Zero allocations inside `OnAudioFilterRead`.** All queues, buffers, and lookup tables pre-allocated on `Awake`. `PerformanceMetadataLookup` must be a struct-of-arrays or a fixed-size ring buffer.
- **No locks.** Main thread writes new metadata into a `ConcurrentQueue` or a double-buffered struct; audio thread reads atomically.
- **Smoothing is non-optional.** Direct parameter writes from the main thread to the synth produce zipper noise; every assignment in step 4 must go through a one-pole `lerp(current, target, k)` with `k` tuned per parameter (frequency: fast, ~5 ms; tongue: medium, ~30 ms; vibrato amount: slow, ~150 ms).

---

## 7. Compatibility with MidiGenPlay invariants

### 7.1 Two-pass orchestration (§3.1 of current state report)

No impact. The sink fires inside PASS 1 (Melody/Lead) exactly when notes are written to the `PatternBuilder`. Harmony in PASS 2 still reads via `ctx.GetTrackForRole` against the same `MidiFile`. The synth runs at playback time, after both passes are complete.

### 7.2 Determinism (§5 of current state report)

This is the most important constraint to honor. The package promises `same inputs + same seed = same output` for the symbolic MIDI. Pink Trombone introduces an audio output; to keep the contract honest, audio must be deterministic too.

Requirements:

1. **Expose RNG seeding on the C# port.** If `PinkThrombone` does not currently expose a seed parameter, fork or PR the upstream. Seed must come from the same `StableHash32` formula used in `SongOrchestrator` per §5, with an additional axis: `StableHash32($"{defaultSeed}|p={partIndex}|rep={rep}|r={role}|m={musicianId}|synth=PT")`.
2. **No reliance on `UnityEngine.Random` or wall-clock time** inside `PinkTromboneVoicePlayer`. Smoothing coefficients and mapping constants are pure functions of profile data.
3. **Document the determinism scope explicitly.** Audio determinism holds *given the same sample rate and the same audio-thread block size*. Both are observable via `AudioSettings`; if either changes, audio output may differ even if MIDI is bit-identical. This is a real constraint to surface to ALWTTT.

### 7.3 Meter and time signature (§5 of current state report)

Pink Trombone has no concept of meter. It receives Hz, not beats. The conversion `beats → seconds` happens at the `MidiMusicManager` boundary using the part's authoritative time signature and tempo, which is already the package's source of truth. No new authority claim.

### 7.4 Caching (§7.2 of current state report)

`MidiMusicManager` already caches `MidiFile` per part keyed by serialized inputs, with the recent invariant that non-default `PartConfig` transients (`ModulationOctaveHint`, `PreviousRootNote`) disable caching. Implications for Pink Trombone:

- **Cache the metadata sidecar alongside the MidiFile**, keyed identically. When the cache hits, both come back.
- **Do not cache audio.** Pink Trombone synthesis is cheap (~1–5% CPU per voice on a modern machine; see cycling74 gen~ forum thread for a comparable measurement), and audio buffers are enormous. Re-synthesize live on every playback.
- **No new invalidation triggers.** If a `MelodyCardConfigSO` change invalidates the MidiFile, it invalidates the sidecar by the same key.

### 7.5 ALWTTT card flow (§7.5 of current state report)

A melody card play already triggers the regeneration cycle. With this proposal, the cycle additionally repopulates the metadata sidecar. `PinkTromboneVoicePlayer` swaps to the new sidecar on the next loop boundary, identically to how MPTK swaps to the new `MidiFile`. No new gameplay-pipeline interaction; the §7.3 two-pipeline separation is preserved.

---

## 8. Package boundary impact (what changes where)

| Change | Side | Authority |
|---|---|---|
| New interface `IPerformanceMetadataSink` and struct `PerformanceSlotInfo` | Package | New SSoT addendum to `SSoT_Composer_Melody_Track.md` |
| `MelodyTrackComposer` accepts optional sink; emits per-slot info | Package | Update to `SSoT_Composer_Melody_Track.md` §3.2 step 8 |
| `MidiGenerator` exposes sink registration alongside factory registration | Package | Update to `SSoT_Composer_Melody_Track.md` §3.1 |
| Determinism scope statement extended to "MIDI-deterministic; audio-deterministic when sample rate and block size are stable" | Package | Update to `SSoT_Runtime_Generation_Orchestration.md` §5 (clarification, not policy change) |
| `MelodyVoiceProfileSO` (mapping table parameters, smoothing constants, profile name) | ALWTTT | Cross-project reference doc, mirrors `MelodyCardConfigSO` pattern in §7.4 |
| `PinkTromboneVoicePlayer` MonoBehaviour, audio-thread wiring | ALWTTT | Cross-project reference doc |
| `MidiMusicManager` routes Melody/Lead channels to either MPTK or `PinkTromboneVoicePlayer` per musician config | ALWTTT | Update to `reference/cross-project/ALWTTT/` material |
| Sidecar cache keyed alongside MidiFile cache | ALWTTT | Update to ALWTTT cache invariant doc |
| NuGet `PinkTrombone` (or vendored source) | ALWTTT third-party dep | Tracked in ALWTTT Plugins manifest |

The boundary is preserved. The package adds one interface and one struct; everything else, including the entire concept of "what synthesizer is playing this," stays consumer-side. This mirrors how `MelodicLeadingConfig` is package-side theory while `MelodyCardConfigSO` is integration material (§7.4 of current state report).

---

## 9. Risks and open questions

1. **Polyphony.** Pink Trombone is monophonic. The proposal only covers `TrackRole.Melody` and `TrackRole.Lead`. Harmony/Backing/Bassline must continue through MPTK. ALWTTT-side mixing must blend Pink Trombone voices with MPTK output without phase or level surprises. **Open: is per-musician audio mixing in `MidiMusicManager` mature enough to handle two audio sources cleanly?**
2. **CPU under chorus.** The cycling74 forum thread on the gen~ port measured ~15% CPU per voice on a recent MacBook Pro at 64-bit. The C# port at 32-bit on Unity should be cheaper, but a four-voice "Melody + three Lead overdubs" scene may still hit 20–30% audio-thread CPU. Needs profiling before committing to chorus-style use. The Modular-Pink-Trombone author notes the AudioWorklet version supports a "Pink Trombone chorus"; that does not translate directly to C# perf characteristics.
3. **Strategy-side metadata leakage.** §5's tongue-color mapping wants modal-degree information (characteristic / tonic / chromatic). Today the strategies score these internally and discard the reasoning by the time `MelodyTrackComposer` writes the note. To expose it cleanly, either (a) extend `IMelodyStrategy.PickNext` to return a richer result type that includes degree classification, or (b) have the composer re-derive the classification from the resolved note + `TonalityProfileSO` after the fact. (b) is simpler and avoids changing the strategy interface, but duplicates a small amount of logic.
4. **Cross-phrase tract continuity.** Pink Trombone's 44-segment waveguide carries acoustic energy across `Synthesize` calls. Rapid pitch jumps between phrases can produce unwanted glides or transient pops. The §5 mapping handles this with smoothing and the `IsPhraseStart` reset hook, but corner cases (very short rests, instant strategy switches via `MelodicStyleSO` per-phrase directives) need empirical tuning.
5. **Determinism caveat.** As stated in §7.2, audio is deterministic *given stable sample rate and block size*. Users changing OS audio device mid-game break this. Whether the package SSoT can swallow that asterisk is a call for the agent.
6. **No visualization carry-over.** The original Pink Trombone's mouth/throat diagram is not part of the C# port. None of MidiGenPlay's authoring or playback UIs need it, but if ALWTTT ever wants a "see the singer's mouth" visualization, that's separate work (~a `LineRenderer` reading `synth.Diameter[]` per frame; cheap but additive scope).
7. **Pattern-authoring roadmap interaction.** §6 of the current state report notes the planned pattern-authoring path (`MelodyPatternData`, `MelodyGenerationParamsSO`, Phases 1–5) is not started. This proposal is orthogonal — it sits at the rendering boundary downstream of *any* future composer behavior — but the sink interface should be designed so a future pattern-driven composer can write to it identically. Recommendation: define `IPerformanceMetadataSink` now even if the only consumer is `PinkTromboneVoicePlayer`, so the contract is fixed before Phase 1 of the melody roadmap begins.

---

## 10. Suggested phased rollout

**Phase A — minimal integration (no package change).**
ALWTTT-side only. Add `PinkTromboneVoicePlayer` that consumes the existing `MidiFile`, mapping only `Pitch` and `Velocity` per the §5 table. No sidecar, no §4.2 interface. Validates the C# port works under Unity audio, validates CPU budget, validates the mixing story with MPTK. Output sounds articulate but flat (no phrase-aware articulation). **Decision gate: does this sound good enough to be worth pursuing further?**

**Phase B — sink interface and structured articulation.**
Add `IPerformanceMetadataSink` + `PerformanceSlotInfo` to the package per §4.2 and §8. Update `MelodyTrackComposer` to emit. ALWTTT sidecar capture + Phase A player now uses the full §5 mapping minus the modal-degree color. Unlocks phrase-end vibrato, accent bumps, contour-aware tongue shaping.

**Phase C — modal degree color and profile authoring.**
Pick approach 3(b) (derive degree post-hoc in the composer) or 3(a) (extend strategy interface). Wire degree classification into the sink. Introduce `MelodyVoiceProfileSO` on the ALWTTT side so designers can tune the §5 mapping table per musician personality without code changes.

**Phase D — multi-voice / chorus, if Phase B/C clear CPU and design quality bars.**
Per-musician Pink Trombone instances for any musician with a melody-role bundle requesting it. `MelodyCardConfigSO` gains an optional `voiceProfile` reference.

Phases A and B are tightly scoped and reversible. Phases C and D depend on Phase B's perceived musical payoff.

---

## 11. Closing

**What this proposal is.** A plan to use Pink Trombone as the *performance* layer for `TrackRole.Melody` and `TrackRole.Lead`, consuming MidiGenPlay's already-existing symbolic output plus a small new per-slot metadata sidecar. The package gains one interface and one struct; everything else lives on the ALWTTT side, matching the §7.4 / §7.6 boundary discipline of the current state report.

**What this proposal is not.** It is not a melody *generator*. Pink Trombone does not select pitches and cannot serve as `IMelodyStrategy` or `PhraseArchetypeSO`. The procedural Stage 1 / Stage 2 runtime is preserved unchanged. It is also not a replacement for MPTK across all roles — only for monophonic melody-family roles where articulatory expression is musically valuable.

**Open items requiring agent decision.**
(a) Approval of the new `IPerformanceMetadataSink` / `PerformanceSlotInfo` package surface in §4.2 and §8.
(b) Approval of the determinism-scope wording extension in §7.2.
(c) Resolution of risk 3 (modal-degree exposure): re-derive in composer, or extend `IMelodyStrategy`.
(d) Sequencing call: does this work compete with or follow Phase 8 (rhythm authoring), and does it block / get blocked by the not-started melody Phases 1–5?

**No SSoT changes proposed in this report.** This is an evaluation artifact. Any documentation updates listed in §8 happen if and when the proposal is accepted.

---

## Appendix A — Reference implementations and bibliography

**Algorithm references (cited by Thapen in the original Pink Trombone source):**

- Julius O. Smith III, *Physical Audio Signal Processing for Virtual Musical Instruments and Audio Effects.* Stanford CCRMA. <https://ccrma.stanford.edu/~jos/pasp/>
- Brad H. Story, "A parametric model of the vocal tract area function for vowel and consonant simulation." *Journal of the Acoustical Society of America* 117.5 (2005): 3231–3254.
- Hui-Ling Lu and J. O. Smith, "Glottal source modeling for singing voice synthesis." *Proceedings of the 2000 International Computer Music Conference.*
- Jack Mullen, *Physical modelling of the vocal tract with the 2D digital waveguide mesh.* PhD thesis, University of York, 2006.

**Ports relevant to this proposal:**

- `lostmsu/pink-trombone-mod` — C# port (NuGet: `PinkTrombone`). Primary substrate for this proposal.
- `chdh/pink-trombone-mod` — TypeScript modular reference. Algorithm parity source for the C# port.
- `PaulBatchelor/voc` and `pbat.ch/sndkit/tract` — ANSI C literate-programming implementation; recommended reading for understanding the LF model and Kelly-Lochbaum waveguide math.
- `cutelabnyc/pink-trombone-plugin` — JUCE C++ plugin; reference for VST-like real-time integration patterns.
- `yonatanrozin/Modular-Pink-Trombone` — AudioWorklet multi-voice port; reference for chorus-style multi-instance behaviour and parameter-automation patterns.

**Original project:** Neil Thapen, *Pink Trombone* (2017). <https://dood.al/pinktrombone/>. MIT-licensed.

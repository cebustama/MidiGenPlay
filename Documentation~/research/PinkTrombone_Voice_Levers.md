# Pink Trombone — Voice Character Levers

> **Class: reference, consumer-side.** Lives in `Assets/PinkTrombonePOC/`
> beside the singer; travels with it when promoted into ALWTTT. This is NOT
> package documentation and never enters the MidiGenPlay PK as authority.
>
> Written Session 5 (2026-07-21), against `PinkTromboneSinger.cs` **v7** and
> fork state **POC-FORK(1–7)**. This document did not exist before Session 5:
> the C-lite mappings were documented only in code comments and one paragraph
> of the verdict note, and the wobble could not be exaggerated at all because
> it was ungated inside the fork.

## 0. Purpose

The singer component has ~40 inspector fields. Most exist for diagnostics and
are settled. This document curates the **six macro levers** that carry almost
all of the audible personality, states what each one demonstrably does, and
drafts the **VoiceProfile** schema the ALWTTT integration should serialize —
so voice design happens on six knobs, not forty.

The levers live behind `characterEnabled` on the singer (Session 5, v7).
**OFF = exact v6 behavior; every raw field keeps working** for diagnostics and
fine-tuning. ON = the levers drive their underlying fields, which are ignored.

## 1. The six levers

| Lever | Range (default) | Drives | Audible effect | Verified |
|---|---|---|---|---|
| **looseness** | 0–1 (**0.15**) | Both jitter gates (POC-FORK 6/7) | Pitch + timbre instability. 0 = studio-stable; 0.15 = subtle human "life"; 0.5 = noticeably unsteady; 1 = the original toy's drunken wander. | **Measured:** 0 → 0.01 cents SD; 0.15 → ~5 cents SD / 23 c p2p; 1 → ~35 cents SD / 150 c p2p (8 s sustain, 48 kHz). |
| **vibratoDepth** | 0–1 (**0.4**) | `vibratoGain` = depth × 0.012 | Singing vibrato on *held notes only* (the settled 0.35 s delay + 0.4 s ramp still gate it; short notes never get any). 0.4 ≈ the settled subtle ±8.6 cents; 1 ≈ ±21 cents, pronounced/operatic. | Depth math exact (multiplicative on F0); character values by ear. |
| **vibratoSpeedHz** | 3–9 (**6**) | `vibratoFrequency` | 5–6.5 Hz reads as singing. Below ~4.5: crooning, seasick at depth. Above ~7: nervous bleat. | By ear (POC sessions 2–4). |
| **diction** | 0–2 (**1**) | Scales all three C-lite amounts together (M1 `vowelOpenAmount`, M2 `vowelContourIdxShift`, M3 `tensenessDegreeAmount`) | How much the mouth *moves with the music*. 0 = static mumble (vowel frozen at `mouth`); 1 = the settled musical diction; 2 = theatrical over-enunciation. M1 (openness on stressed/long notes) is the single biggest audible contributor. | C-lite audibility verified Session 4; the 0–2 scaling is new in v7. Sampled at **arm time**, like the amounts it scales. |
| **mouth** | 0–3 (**0**) | Base vowel preset: 0 Neutral "uh", 1 Open "ah", 2 Front "eh/ee", 3 Back "oh" | The base *color* of the voice — the "ahh / eeh / ooh" character. With diction > 0, M1 opens **from this base** toward "ah" on stressed/long notes (v7 change; v6 always morphed from Neutral, which would have made this lever inaudible). With `mouth = 1` (Open), M1's vowel motion is minimal by construction — expected. | Presets verified Test 7; base-aware morph is new in v7, listen-verify. |
| **brightness** | 0–1 (**0.5**) | Tone-stage cutoff, log curve: `1200 × 2^(3.1·b)` Hz | 0 = ~1.2 kHz: muffled, warm, far away / behind a door. 0.5 = ~3.5 kHz: the settled voice. 1 = ~10 kHz: present but increasingly shrill and fizzy (the model has no lip radiation; highs expose it). | Curve math exact; endpoints by ear (tone stage settled Session 2). |

All levers are **live per block** except diction and mouth's effect on
per-note targets, which are sampled when a fixture is armed (same rule as the
C-lite amounts they scale).

## 2. Character parameters *outside* the toggle

These shape identity but stay as raw fields (settled values; change rarely):

| Field | Settled | Character meaning |
|---|---|---|
| `transposeSemitones` | −12 | **Register** — who is singing. The tract reads as an adult voice; high registers strain. |
| `tensenessAtVel0` / `AtVel127` | 0.40 / 0.60 | The **effort window**: bottom = breathy/airy/weak fundamental, top = hard/buzzy/present. Shifting the whole window down (~0.30–0.45) reads ghostly; up (~0.55–0.75) reads strained/insistent. Session 5 note: higher tenseness also firms pitch definition slightly, but jitter gating solved the instability, so the window no longer trades against intonation. |
| `vibratoDelaySeconds` / `RampSeconds` | 0.35 / 0.4 | *When* vibrato happens: only on held notes, fading in. Lower delay = mannered "instant" vibrato. |
| `pitchLeadSeconds` / `leadFullInterval` | 0.06 / 7 | Portamento compensation. Effectively "how good a sight-reader": 0 = scoops into big intervals. |
| `minLoudness` | 0.15 | Dynamic floor — how much velocity is allowed to whisper. |

## 3. "When" — temporal control of expressivity

Three tiers, be honest about which exists:

1. **Note-level (automatic, exists now).** The C-lite mappings *are* the
   note-level "when": metric weight and duration drive openness (M1), contour
   drives color (M2), scale-degree tension drives effort (M3). `diction`
   scales how strongly.
2. **Section-level (game-driven, possible now).** Every lever is live per
   block, so ALWTTT can *animate* them: raise `looseness` and drop
   `brightness` as the character gets tired, push `diction` and `vibratoDepth`
   for a chorus. **The game owns "when"; the singer exposes "what."** This is
   the intended integration pattern — the VoiceProfile is the resting state,
   and gameplay modulates around it.
3. **Phrase-metadata-driven (does not exist, deferred).** Automatic emphasis
   at cadences, phrase peaks, tension arcs requires phrase metadata that never
   reaches the MidiFile. That is exactly the **Phase D4** boundary
   (`IPerformanceMetadataSink`, verdict §6). Do not build it opportunistically.

## 4. Recipes (starting points — only "Settled" is listen-verified)

| Recipe | loose | vibD | vibHz | dict | mouth | bright | + raw |
|---|---|---|---|---|---|---|---|
| **Settled singer** (= the POC voice, stabilized) | 0.15 | 0.4 | 6 | 1 | 0 | 0.5 | transpose −12 |
| Choir kid | 0.05 | 0.15 | 5.5 | 0.6 | 2 | 0.7 | transpose −5 |
| Drunk bard | 0.7 | 0.6 | 4.5 | 1.6 | 1 | 0.35 | transpose −12 |
| Old crooner | 0.3 | 0.9 | 5 | 1.2 | 3 | 0.3 | transpose −14 |
| Nervous herald | 0.25 | 0.3 | 7.5 | 1.8 | 2 | 0.8 | transpose −7 |
| Ghost | 0.45 | 0.2 | 3.5 | 0.3 | 0 | 0.15 | transpose −12; tenseness window 0.30–0.45 |

## 5. Draft VoiceProfile schema (for the ALWTTT integration, §3.4)

The serialized ALWTTT-side asset should be, at minimum, the six levers plus
the §2 identity fields:

```
VoiceProfileSO
  // macro levers (the designed surface)
  float looseness, vibratoDepth, vibratoSpeedHz, diction, brightness
  int   mouth                       // enum in the real asset
  // identity (rarely changed)
  int   transposeSemitones
  float tensenessAtVel0, tensenessAtVel127
  float vibratoDelaySeconds, vibratoRampSeconds
  float pitchLeadSeconds; int leadFullInterval
  float minLoudness
  // gameplay modulation hooks (section-level "when", §3 tier 2)
  //   e.g. AnimationCurve or per-state overrides — integration decision
```

Everything else on the singer is diagnostics/fixture plumbing and should NOT
be in the profile.

## 6. Field reference (everything else, one line each)

- `setup` — the SmokeSetupSO song source (governed smoke infra).
- `measureCallbackLoad` — self-measured DSP load; build-safe; not Profiler-comparable.
- `halfRateSynthesis` — CPU experiment; drops all formants exactly one octave (Session 5: waveguide scales with rate; F0 does not). Rejected for quality; keep OFF.
- `alwaysVoice` — hum through rests instead of articulating; OFF settled.
- `hardGateOutput` + `attackMs`/`releaseMs`/`retriggerOnNoteOn` — external gate, Test 4 loser; only audible when the gate is ON.
- `tongueIndex`/`tongueDiameter` — raw vowel when levers and expressivity are both off.
- `vibratoWobble` — upstream's ±60 % wander terms. Never ON; `looseness` is the controlled replacement.
- `pitchJitterGain`/`tensenessJitterGain` — raw jitter gates (POC-FORK 6/7); driven by `looseness` when levers are ON; keep for Sustain-Test diagnostics.
- `expressivityEnabled` + M1/M2/M3 toggles & amounts — C-lite layer; `diction` scales the amounts when levers are ON.
- `tensenessOverride`/`Value` — Test 6 isolation tool.
- `toneEnabled`/`toneCutoffHz` — mandatory tone stage; `brightness` drives the cutoff when levers are ON.
- Test-scale/sustain fixture fields, `gain`, `loopPlayback`, `syncTrimMs`, `uiScale` — harness plumbing.

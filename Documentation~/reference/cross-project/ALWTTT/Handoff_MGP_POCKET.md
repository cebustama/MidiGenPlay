# Handoff — MGP-ALWTTT-BASS-POCKET-1 + POCKET-2 (consumer side)

> **Status:** close-out record returned to ALWTTT, 2026-07-26 (batches shipped
> 2026-07-25; documentation applied by B0 — DOC-CLOSE).
> **Authority:** this document is consumer-facing and defines **no** package
> truth. The contracts live in `runtime/SSoT_Composer_Bass_Track.md` §3.7 and
> §3.7.1, `runtime/SSoT_Composer_Rhythm_Track.md` §3bis, and
> `runtime/SSoT_Runtime_Generation_Orchestration.md` §5. Where this document and
> those disagree, they win.

## 1. What shipped

An **opt-in** coupling that makes the bass line follow the drummer instead of a
fixed figure, selected by `BasslineCardConfigSO.pocketMode = SlapPocket`
(default `Off`). Per chord event, a window containing kick/snare onsets has its
figure replaced by slap hits (kick family → the note the bass already selected)
and pop hits (snare family → the same note one octave up), at the **drum step's**
velocity. A window without those onsets renders the ordinary figure, so pocketed
and decoupled events mix freely inside one render.

The slap/pop **timbre** is the bass patch's job (GM Slap Bass 1/2 on the
`MIDIInstrumentSO`) — a content decision, consumer-side. The package shapes
timing, register and dynamics only.

## 2. Obligations

### 2.1 Hash duty — required

With `pocketMode != Off`, the resolved rhythm pattern becomes a **hash-relevant
input of the BASS track**. Extend `ComputeTrackInputsHashesForPart` to fold the
consumed drummer's resolved pattern identity into the bass track's input hash.

The identity is already available in the Rhythm track's Ask A readback. The
consumed drummer is the **first** Rhythm track in `Part.Tracks` order that
resolved a grid pattern.

### 2.2 Track order — required

Place **Rhythm before Bassline** in `Part.Tracks`. The onset channel is a
publish/consume cache filled during the track loop, so a bass that composes
first sees nothing and renders decoupled (with one warning, byte-identical to
`Off`).

### 2.3 Re-render pattern — when a rhythm card arrives late

When the rhythm card is played after a bass-only render, re-render the part
through the existing `RenderSinglePart` bridge with the same seed; the bass stem
regenerates pocketed.

**Determinism caveat on record:** adding the rhythm track changes the shared
`ctx.rng` interleaving, so the re-created bass is deterministic but not
necessarily *pitch-identical* to the solo render. The pitch classes are stable
(they are the chord roots); the octaves may differ.

## 3. What degrades, and how safely

No published source — no Rhythm track in the part, the Rhythm track composing
after the bass, or the rhythm resolving to a procedural/legacy path — degrades
to the ordinary figure with **at most one warning per `Compose`**. Never an
error, never silence.

This is pinned as **byte-identity**, not "approximately the same": pocket-on
without a source produces the same bytes as pocket-off. It holds structurally,
not by measurement.

Procedural and legacy rhythm paths publish nothing in v1. That is scope on
record, not an oversight — if a card resolves to one of them, expect the
decoupled figure.

## 4. New card fields (POCKET-2)

The bassline card gains five authorable fields under **Pocket Coupling**. All are
inert at their defaults, so existing cards need no migration.

| Field | Type / range | Default | Meaning |
|---|---|---|---|
| `pocketSlapBoost` | `int`, −64..64 | `0` | Additive offset over the drum step's velocity, slap class |
| `pocketPopBoost` | `int`, −64..64 | `0` | Same, pop class |
| `pocketCustomLanes` | `bool` | `false` | When on, the two lists below REPLACE the built-in trigger families |
| `pocketSlapLanes` | `List<GeneralMidiPercussion>` | empty | Slap triggers (empty + toggle on = slap class disabled) |
| `pocketPopLanes` | `List<GeneralMidiPercussion>` | empty | Pop triggers (empty + toggle on = pop class disabled) |

Rules worth knowing before authoring:

- The lists **replace**, they do not extend. A lane absent from the list does
  **not** fall back to its family.
- An **empty list with the toggle on disables that class** — that is how a
  pop-only or slap-only pocket is expressed.
- A lane in **both** lists counts as a **pop**.
- Lanes are matched **as authored**, before per-kit resolution, so a lane the
  kit substitutes elsewhere still triggers correctly (and one it substitutes
  *into* does not trigger spuriously).
- Boosts are clamped to 1..127 after addition. A boost of `0` is an exact
  identity, not an approximation.

**Content note — the motivating case.** For a Latin rim-click backbeat, turn the
toggle on and put `SideStick` in `pocketPopLanes` (add the snares too if you want
to keep them). The built-in v1 families exclude `SideStick` on purpose. If pops
read weak against a softly-authored kit, a positive `pocketPopBoost` is the
intended fix — pops sit an octave up and lose against slaps at equal drum
velocity. Do not "fix" it by editing the drum pattern: the drums stay the
author's.

## 5. Register caveat

A pop is the selected note **+12, uncapped**, so a pocketed bass can exceed the
composer's register band by an octave — and the bass does not consult
`MIDIInstrumentSO.octaveMax` at all. This is intended behaviour, not a defect,
and it is the second recorded instance of the package-side finding F-WALK-REG.

Consumer-side mitigation today is a lower `octaveMin` on the bass instrument
asset. The package-side cap belongs to a future batch (**B3 — BASS-REG-1**),
because it changes every bass render.

## 6. Not in v1 (recorded, so nobody plans around them)

- Accent mode (velocity-only coupling, no substitution).
- An explicit drummer-binding field — today it is first-publisher-wins by
  track-list order.
- Publication from the procedural and legacy rhythm paths.
- Pop pitch = upper chord tone instead of +12.

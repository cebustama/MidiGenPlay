# Palette → Card Identity Design (governed)

**STATUS: GOVERNED (realized in PCE, 2026-06-04).** Consumption mechanism →
`runtime/SSoT_Composer_Rhythm_Track.md` §3D. Card→palette assignment →
`reference/cross-project/ALWTTT/SSoT_CompositionCards_TrackStyleBundles.md` §1.3.
Placement: `Documentation~/reference/package/` (moved here from
`planning/active/` at PCE close-out). This document is the realized design record;
the live governed homes for the mechanism and the assignment are the two docs
named above. The forward decisions below (§6 tempo cards, §8 chord-side) remain
open and carry resolution notes where PCE settled them.

First drafted: 2026-05-29, at Batch L5 (L-PAL) closure. Realized & governed:
2026-06-04 (PCE).

---

## 1. Purpose

L5 delivered `DrumPatternPaletteSO` (author-only at the time, D-PAL.3). PCE
(2026-06-04) wired the rhythm composer to consume palettes so each ALWTTT
**Rhythm Composition card** gains a distinct musical identity instead of drawing
from a single undifferentiated pattern source. This section records the design
that PCE realized.

This document proposes which palette identity attaches to each existing card, and
defines the first distinctness experiment.

## 2. Design principle — distinctness axes

A palette gives a card its identity. For cards to feel different, their palettes
must differ on axes a listener actually perceives. In rough order of perceptual
weight:

1. **Meter / subdivision feel** — strongest lever. Different bar lengths or
   straight-vs-swung grids are unmistakable.
2. **Instrumentation** — which lanes exist (standard kit vs alt-percussion).
   Identified by timbre before rhythm.
3. **Density / syncopation profile** — busy vs sparse, on-grid vs off-beat.
4. **Velocity dynamics** — ghost notes, accents, dynamic range. Weakest in
   isolation: two palettes differing *only* in velocity read as the same card.

Implication: do not cluster palettes by genre. Genre-clustered sets (e.g. "rock"
vs "pop", both 4/4 standard-kit mid-density) collapse toward each other. Spread
across the axes above instead.

## 3. The card constraint

Six Rhythm Composition cards exist today:

- **Four meter generators** — fixed meter, one each: **4/4, 3/4, 6/8, 5/4**.
- **Two tempo cards** — meter-agnostic: one **increments** tempo, one
  **decrements** tempo.

Consequence: the meter axis (axis 1, the strongest) is *already claimed* by the
four meter cards. Distinctness between those four comes free from the meter
itself. Palette design for them is about choosing the most characterful feel
*within* each fixed meter — not varying meter. The tempo cards fix no meter, so
their palettes cannot assume one (see §6).

## 4. Proposed card → palette assignment

| Card | Fixed meter | Palette identity | Axis showcased |
|---|---|---|---|
| 4/4 generator | 4/4 | **Four-on-the-Floor Pulse** | regularity / metronomic drive |
| 3/4 generator | 3/4 | **Waltz-Pulse Lilt** | triple-meter lilt + soft dynamics |
| 6/8 generator | 6/8 | **Compound Swing** | compound-meter two-big-beat shuffle |
| 5/4 generator | 5/4 | **Odd-Meter Angular** | asymmetric accent grouping (3+2 / 2+3) |
| Tempo + (increment) | agnostic | *deferred* — see §6 | — |
| Tempo − (decrement) | agnostic | *deferred* — see §6 | — |

### Palette sketches (feel, not final DSL)

- **Four-on-the-Floor Pulse** (4/4, sub 2–4): kick every beat, snare backbeat on
  2 & 4, continuous hats. Low syncopation, high regularity, mid density. The
  baseline every other palette is heard against.
- **Waltz-Pulse Lilt** (3/4, sub 2–3): strong beat 1, lighter 2 & 3, ride/brush
  texture, feathered kick. Low density, soft compressed velocity. Owns the
  triple-meter feel plus the dynamics floor.
- **Compound Swing** (6/8, sub 3 within the dotted-quarter pulse): two big beats
  per bar with a swung internal triplet feel; kick on the big beats, snare/hat
  filling the compound subdivision. Distinct from 3/4 despite both being "triple"
  because the pulse grouping differs (2×3 vs 3×1).
- **Odd-Meter Angular** (5/4, sub 2): asymmetric grouping (3+2 or 2+3) with
  accents marking the grouping boundary. Owns the asymmetric-meter identity
  outright — nothing else in the set is confused with it.

## 5. The distinctness experiment (the proof)

Add **one new 4/4 generator card** carrying a deliberately contrasting 4/4
palette:

| Card | Fixed meter | Palette identity | Axis showcased |
|---|---|---|---|
| **NEW** 4/4 generator #2 | 4/4 | **Syncopated Pocket (Funk)** | syncopation + density + velocity |

**Syncopated Pocket** (4/4, sub 4): sparse kick on off-beats, ghost-note snares
(low velocity), busy 16th hats with accents. High syncopation, high density, wide
velocity range.

This is the cleanest possible test: **two 4/4 cards with meter held constant**,
sitting at opposite ends of the syncopation/density/velocity axes. If a player
hears **Four-on-the-Floor** and **Syncopated Pocket** as different cards, palettes
deliver identity with meter controlled out as a variable. If they don't, the
palette concept needs rethinking before scaling to more cards.

Only after this passes should additional cards be minted from the remaining seed
identities (§7).

## 6. Open decision — tempo cards (D-EXP.tempo)

The increment/decrement cards fix no meter and modify an active track rather than
defining one. What palette, if any, attaches?

- **A.** No palette — tempo cards change tempo only, leaving the active track's
  pattern untouched. *Trade-off:* keeps card meaning clean (a tempo card changes
  tempo, not identity); simplest. **Recommended.**
- **B.** Meter-neutral palettes selected at runtime by the active meter.
  *Trade-off:* gives tempo cards a feel, but couples two concerns and needs
  per-meter entries; the inert `preferExactTimeSignatureMatches` toggle would
  activate here.
- **C.** A "feel shift" palette that re-rolls the pattern at the new tempo.
  *Trade-off:* most expressive, most surprising to a player; risks changing what
  they had.

Recommendation: **A**. Decide for real when the expressivity phase opens; not
binding now.

> **Resolved (PCE, 2026-06-04):** Option A adopted — tempo cards carry no palette;
> they change tempo only and leave the active track's pattern untouched. Not a
> hard contract (no tempo-card palette code exists); revisit if CE introduces
> tempo-card identity.

## 7. Seed library for future cards (not yet assigned)

Held in reserve once the §5 experiment proves the concept:

- **Half-Time Heavy** (4/4, sub 2; snare on 3 only): sparse, weighty, few lanes.
  Owns "space and weight" — perceptually slow at the same tempo.
- **Latin Clave Engine** (4/4, sub 4; alt-percussion lanes: claves/congas/
  cowbell/timbale): owns the instrumentation axis — identified by timbre first.
- **Breakbeat / Amen Dense** (4/4, sub 4): chopped, fast, max density with rolls.
  The density ceiling; opposite pole from the sparse palettes.

## 8. Chord-side parallel (for the same phase)

When chord palettes are wired alongside drum palettes, apply the same
axis-thinking: separate chord palettes by **harmonic function / color** (modal
vamps vs functional-cadential vs chromatic/borrowed vs static drones), not by
genre, for the same collapse reason. Out of scope here; noted so the phase plans
both consumers together.

> **Resolved (PCE, 2026-06-04):** the axis-thinking principle is adopted, but
> chord-side palette *wiring* is deferred to CE-F1 (the Finder), where drum and
> chord palette selection unify behind one TS-aware picker.

## 9. Phase entry checklist (RESOLVED in PCE, 2026-06-04)

- ~~Resolve D-EXP.tempo (§6) and the chord-palette axis set (§8).~~ **Resolved:**
  tempo = Option A (no palette; see §6); chord-axis = §8 principle adopted, wiring
  deferred to CE-F1.
- ~~Decide the composer-consumption seam~~ **Resolved:** seam = clone-on-pick via
  `RhythmCardConfigSO.PickPatternOverride(rng)`; **seed = `ctx.rng`** (deterministic
  `System.Random(defaultSeed)` fallback only when `ctx.rng` is null and a palette is
  present). Contract lives in `runtime/SSoT_Composer_Rhythm_Track.md` §3D.
- ~~Author the §4 + §5 palettes as real assets~~ **Done:** 5 `DrumPatternData` +
  5 `DrumPatternPaletteSO` filed under
  `Resources/ScriptableObjects/Drums/` and `Resources/ScriptableObjects/Drums/Palettes/`
  (corrected from the earlier `Patterns/Drums/Palettes/` note).
- ~~Migrate the assignment table / consumption contract~~ **Done:** assignment →
  ALWTTT §1.3 (mirror of game-owned table); consumption contract →
  `runtime/SSoT_Composer_Rhythm_Track.md` §3D.
- **TS-toggle resolution:** `preferExactTimeSignatureMatches` is INERT on the drum
  palette and LIVE on the chord palette; unifying both behind one Finder = CE-F1.

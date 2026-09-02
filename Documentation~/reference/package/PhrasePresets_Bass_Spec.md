# PHRASE-1 — Bass phrase presets (asset specification)

> MGP-ALWTTT-BASS-PHRASE-1 companion. Four `BasslineCardConfigSO` assets
> that exercise the phrase surface. Every field not listed keeps its
> serialized default.
>
> **Alphabet:** `S` Slap · `P` Pop · `.` Rest · `g` Ghost · `G` GhostPop ·
> `H` HammerOn · `L` PullOff (L = pull-off, to avoid the P collision).
> Governed by `authoring/SSoT_Authoring_Bass_Cards.md` §1.
>
> **Authoring path.** These were originally specified for MANUAL creation in
> the inspector, because no bass-card wizard existed. One does now:
> MGP-BASSCARD-WIZARD-1 (2026-08-07) shipped the Bassline Card Editor. Paste
> the strings below straight into its Body and variant fields; hand-filling a
> 16-element `QuarterBeat` pattern through dropdowns is no longer the path.
>
> Suggested location: the package's card/preset folder used by the
> existing bundle assets — confirm the exact path against the project tree
> before creating (file-placement discipline; the spec does not guess).

## 1. `Bass_Phrase_Aeroplane4` — the founding gesture

Four bars of groove; the fourth closes differently, two ways.

| Field | Value |
|---|---|
| `pocketMode` | `SelfPocket` |
| `selfPocketSubdivision` | `QuarterBeat` (16 steps/bar in 4/4) |
| `selfPocketPattern` (body) | `S . . g P . g . S . g . P . g .` |
| `selfPocketPhraseLengthBars` | 4 |
| `selfPocketVariantSelection` | `SeededMix` |
| `selfPocketBarSubstitutions` | one entry, `barIndex = 3`, two variants: |
| — variant 0 (ghost run) | `S . g g P . g g S g g g P . g .` |
| — variant 1 (pop ladder, legato close) | `S . . g P . H . S . H . P L . .` |

Notes: variant 1 exercises the BEND-1 carrier path inside a fill (H/L ride
the preceding sounding step). Expect the once-per-render legato warning
only if a variant is edited to OPEN with H/L.

## 2. `Bass_Phrase_Funk2Short` — tight two-bar phrase

| Field | Value |
|---|---|
| `pocketMode` | `SelfPocket` |
| `selfPocketSubdivision` | `HalfBeat` (8 steps/bar in 4/4) |
| `selfPocketPattern` (body) | `S . P . S g P .` |
| `selfPocketPhraseLengthBars` | 2 |
| `selfPocketVariantSelection` | `SeededMix` |
| `selfPocketBarSubstitutions` | one entry, `barIndex = 1`, one variant: |
| — variant 0 (ghost fill) | `S g g P g g G .` |

Single-variant slot: both selection laws agree, so this card doubles as
the "selection toggle is inert at count 1" listening check.

## 3. `Bass_Phrase_Compound78` — compound-meter demonstrator

Pair with a 7/8 progression (`TimeSignature.SevenEight`).

| Field | Value |
|---|---|
| `pocketMode` | `SelfPocket` |
| `selfPocketSubdivision` | `Beat` (7 steps/bar) |
| `selfPocketPattern` (body) | `S . P S . P .` (7 steps — divides the bar) |
| `selfPocketPhraseLengthBars` | 2 |
| `selfPocketVariantSelection` | `SeededMix` |
| `selfPocketBarSubstitutions` | one entry, `barIndex = 1`, one variant: |
| — variant 0 | `S g P g S g G` |

Verifies the integer part-beat bar math (bar 1 = beats 7..14) by ear.

## 4. `Bass_Phrase_RoundRobinAB` — selection-law audition

Identical to `Bass_Phrase_Aeroplane4` except:

| Field | Value |
|---|---|
| `selfPocketVariantSelection` | `RoundRobin` |

Phrases alternate variant 0 / variant 1 mechanically, seed-independent —
flip the toggle back to `SeededMix` on the same seed to hear the mix law
pick per (phraseIndex, slot) instead. The A/B is the audible proof that
the toggle, not the seed, owns the alternation.

## Warning-path sanity (optional fifth, throwaway)

To see SD-PH-1 degradation live, temporarily author on any card above: a
duplicate `barIndex = 3` entry (LAST wins, warn), an entry at
`barIndex = 9` (inert, warn), and a variant with an empty `steps` list
(dropped, warn). One batched `LogWarning` per render, figure keeps
playing. Delete afterwards — not a shipping preset.

## 5. Card — "Forget-Me-Nots groove" (legato-heavy, 16th funk)

Derived from the ARTICULATION SKELETON of the Patrice Rushen bassline
(Freddie Washington): continuous sixteenths, hammer-on out of the downbeat,
octave-up accents, ghost clusters as pickups, and a chromatic legato
turnaround closing the four-bar phrase. **Not a transcription** — the DSL is
pitch-blind by construction; the composer picks the notes from the chord
context. Reductions to the v1 alphabet: slides have no class and reduce to
the neighbouring legato or a plain hit; dead/muted notes map to `g`.

**Setup:** `pocketMode = SelfPocket` · `selfPocketSubdivision = QuarterBeat`
· `selfPocketPhraseLengthBars = 4` · `selfPocketVariantSelection = SeededMix`
· preview meter 4/4 (16 steps/bar). Tempo is not card data; the reference
feel is around ♩=114 and belongs to the Part.

Body (one bar, grouped by beat):

    SH.S gP.S .gSH g.P.

Slot 3 (the phrase-closing bar), two variants:

    variant 0   SHL. SHL. g.SL .S..
    variant 1   S..g gg.P ..S. g...

Variant 0 is the legato turnaround: two hammer→pull cells, then a landing
slap. Variant 1 is the breakdown texture: a ghost cluster with a single pop
accent and air.

**Verified** (MGP-BASSCARD-WIZARD-1 smoke, seed 12345, G Ionian, ♩=114):
8-bar part → 64 notes, 22 legato gestures; 16-bar part → 130 notes, 39
legato gestures; zero warnings. The counts are exactly what the text above
predicts, which pins the window → parser → asset → composer path as
lossless.

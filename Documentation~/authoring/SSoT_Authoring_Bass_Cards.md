# SSoT — Authoring: Bassline Cards

**Status:** governed · **Owner batch:** MGP-BASSCARD-WIZARD-1 (2026-08-07)
**Applied:** DOC-SWEEP-3 (2026-09-01)
**Governs:** `Editor/BasslineCardEditorWindow.cs`,
`Editor/BassPatternTextParser.cs`, `Editor/BassPatternTextWarning.cs`,
`Tests/Editor/BassPatternTextParserTests.cs`.

**Does NOT govern** the meaning of any articulation class. That is runtime
law and lives in `runtime/SSoT_Composer_Bass_Track.md` §3.7.x. This document
governs only how a human types a pattern and how that text becomes card data.

## 0. Why this exists

`selfPocketPattern` at `QuarterBeat` is 16 enum dropdowns per bar, and
PHRASE-1 adds a substitution table where each variant is another full
pattern. A four-bar phrase with two variants is ~48 dropdowns. Authoring —
not runtime — was the bottleneck.

## 1. The v1 alphabet

One glyph = one character. Case is significant.

| Glyph | Class | Note |
|---|---|---|
| `S` | `Slap` | hit on the event's selected note |
| `P` | `Pop` | hit +12, ceiling-folded |
| `.` | `Rest` | canonical spelling on render |
| `-` | `Rest` | accepted on parse, never emitted |
| `g` | `Ghost` | slap-side ghost |
| `G` | `GhostPop` | pop-side ghost |
| `H` | `HammerOn` | legato, `+hammerOffsetDegrees` |
| `L` | `PullOff` | legato, `+pullOffsetDegrees` |

Case carries the register mnemonic: lowercase = slap side, uppercase = pop
side (`P` is uppercase for the same reason). `L` rather than `P` for
pull-off, to avoid the Pop collision.

**Ignored characters:** `|` (bar separator, readability only) and all
whitespace. Identical to the drum DSL law.

**Unknown characters:** parsed as `Rest`, one `UnknownGlyph` warning naming
the buffer and the step. Never an exception. Note that the drum glyphs
`x` / `X` / `o` are unknown HERE: the two DSLs share law, not alphabet.

## 2. Laws inherited from the drum DSL

- Ignored-character stripping before indexing.
- Local degradation: warn and continue, never throw, never silence.
- Deterministic pure functions; no `UnityEditor.*` in the parser core.

## 3. Declared divergences from the drum parser

These are deliberate. Do not "complete" the bass parser by symmetry.

1. **No length policy.** A bass pattern's length IS content — the composer
   cycles the list, and PHRASE-1 variants may legally differ in length from
   the body. There is no fixed step container to pad or truncate against, so
   there is no `LengthShort` / `LengthLong`. Zero glyphs parses to an empty
   list with one `EmptyPattern` warning, mirroring the runtime law that an
   empty pattern warns and falls back.
2. **Lossless round-trip.** `SelfPocketStep` carries no per-step velocity;
   the glyph map is total and bijective. Pattern → text → pattern is exact
   identity, so the drum parser's per-cell diff machinery
   (`ApplyTextEdits`) has no counterpart here and must not be added.
3. **Warning locator is a label, not an index.** The bass card has no lanes;
   its buffers are the body and the phrase table's per-slot variants. The
   parser is locator-agnostic and carries a caller-supplied string
   (`"body"`, `"bar 3 / variant 1"`).

## 4. The editor window

`MidiGenPlay/Bassline Card Editor...`. Loop: bind (or New Card) → edit →
Validate & Preview → Apply To Asset / Save As New Asset.

- Edits a deep clone. The asset is never mutated before Apply/Save As.
- Apply and Save As both parse all buffers first, then write under
  `Undo.RecordObject`; the shared store owns `SetDirty` / `SaveAssets` /
  cache refresh (PATTERN-PERSIST-1).
- Whole-card scope: pattern surfaces get bespoke text UX, every other field
  draws with default drawers over the same clone. One editing surface, one
  Apply — no inspector round-trip mid-session.
- Save root: `Assets/Resources/ScriptableObjects/Patterns/Basslines`. Cards
  living under `Patterns/` is a recorded cosmetic misnomer, accepted to
  avoid changing the shared store's hardcoded root.

### 4.1 The preview meter is editor-only

The card has no meter — the Part does. The window's `Preview Meter` is not
serialized and never reaches the asset. It does two things: place `|` on
render, and drive the advisory checks in §4.2.

### 4.2 Advisories mirror runtime law and never block

The window states what the composer would warn, before the render does. It
adds no law of its own and never prevents a save:

- pattern length does not divide the bar under the preview meter →
  restated per D-PH-INDEX=A (with the phrase active, the pattern restarts
  each bar and runtime warns once). Advisory only: the Part's real meter
  decides.
- duplicate slot / slot out of `0..phraseLength-1` / entry with no variants
  / empty variant → restated per SD-PH-1. An all-rest variant (`....`) is
  the legal way to author a silent bar.
- `pocketMode` is not `SelfPocket` → the pattern and phrase surfaces are
  inert at render.

### 4.3 Preview is a plan preview

Slot-by-slot phrase timeline plus per-class counts. Audible preview stays in
Composition Smoke, which owns the part context (progression, meter, seed); a
convenience button opens it.

## 5. Change triggers

Amend this document when: the glyph alphabet changes; the ignored-character
or degradation law changes; a divergence in §3 stops holding; the window's
persistence contract or save root changes; or the advisory set in §4.2
diverges from the runtime laws it mirrors.

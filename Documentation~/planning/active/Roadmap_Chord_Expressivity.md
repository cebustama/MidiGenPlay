# Roadmap — Chord Expressivity

## Purpose

Tracks the expansion of MidiGenPlay's chord vocabulary and chord voicing beyond
the v1 triad/seventh/suspended alphabet. This arc is about *what chords can be
authored and how they are voiced*, not about the LLM-authoring plumbing (that is
`Roadmap_LLM_Authoring_MVP.md` / `SSoT_Authoring_LLM_Generation.md`) nor the
palette-selection work (`Roadmap_Composition_Expressivity.md`).

Authority for the chord-quality alphabet itself is
`authoring/SSoT_Authoring_Chord_Progressions.md` §4.1 (the `MusicTheory.ChordQuality`
enum is the canonical source, mirrored in lockstep by the parser, prompt
alphabet, editor round-trip, and handler allowlist). Voicing authority is
`runtime/SSoT_Composer_Backing_Track.md`.

## Arc shape

The chord-quality alphabet grows in **append-only** tiers, split by voicer
arity / interval span so each tier ships against a verified voicing path:

1. **Tier A** — added-sixths + suspended-seventh (≤4 voices). **DONE.**
2. **Tier B** — ninths (five voices, span > octave). **DONE.**
3. **Chord inversions** — a per-chord voicing hint. **DEFERRED.**

All quality additions are **explicit-suffix-only**: a bare Roman degree still
infers the diatonic triad/seventh; the extended qualities never participate in
diatonic inference. This keeps every tier free of diatonic-template changes.

---

## CQ-A1 — Tier A: sixths + 7sus4 — DONE (2026-06-16)

Added `Major6` {0,4,7,9}, `Minor6` {0,3,7,9}, `Dominant7sus4` {0,5,7,10} to
`MusicTheory.ChordQuality` (append-only, ordinals 11–13). Suffixes `6` / `m6` /
`7sus4`; symbols C6 / Cm6 / C7sus4. Lockstep edits across the five mirror
surfaces + `ChordQualityResolver.GetTriadFamily`. ≤4 voices → realized through
the existing voicer unchanged. Five EditMode fixtures; full suite green;
`Runtime/` grep for unguarded `ChordQuality` switches clean.

Decisions:
- **D-CQA1.1** Tier A is explicit-only; no diatonic-template change.
- **D-CQA1.2** Suffix outranks numeral case (`vi6` = major-sixth; minor-sixth is `vim6`).
- **D-CQA1.3 (Decision A)** `IsSeventhQuality` gains only `Dominant7sus4` (a real
  seventh → 4 grid rows). `Major6`/`Minor6` stay non-sevenths → grid renders them
  as triads (known delta; Roman/LLM/import path stores+plays all four voices).
- **D-CQA1.4** `GetTriadFamily`: Major6→Major, Minor6→Minor, Dominant7sus4→Suspended
  (so `V7sus4` reads non-diatonic vs a major V, consistent with sus2/sus4).
- **D-CQA1.5** `QualitySuffixForToken` round-trip fix (Major6 no longer rebuilds as a
  plain `I`).

---

## CQ-B1 — Tier B: ninths — DONE (2026-06-16)

Adds `Dominant9` {0,4,7,10,14}, `Major9` {0,4,7,11,14}, `Minor9` {0,3,7,10,14}
to `MusicTheory.ChordQuality` (append-only, ordinals 14–16). Suffixes `9` /
`maj9` / `m9` (aliases `dom9` / `ma9` / `min9`); symbols C9 / Cmaj9 / Cm9.
Lockstep edits across the same five mirror surfaces + `GetTriadFamily`. Five
voices — realized through `BasicVoiceLeadingVoicer`, which already handles
arbitrary-length pitch-class sets.

Voicer change (the gate): `Strategies/VoiceLeading.cs` `GeneratePcCandidates`
inversion loop uncapped from `i < 4` to `i < pcs.Length`. **Zero regression for
≤4-voice chords** (their length already bounds the loop); only adds the top
inversion candidate for five-voice chords.

Decisions:
- **D-CQB1.1** Tier B is explicit-only; no diatonic-template change.
- **D-CQB1.2** Ship ninths against the existing voicer + the single zero-regression
  uncap. Do **not** generalize `Drop2` (would change existing seventh voicings) and
  do **not** touch the range clamp.
- **D-CQB1.3** `IsSeventhQuality` gains all three ninths (each contains a real
  seventh → 4 grid rows). The 9th itself has no grid row (known delta, same family
  as the Tier A added-6th limitation).
- **D-CQB1.4** `GetTriadFamily`: Dominant9→Major, Major9→Major, Minor9→Minor.
- **D-CQB1.5** Forbidden list now: 11 / 13 / add9 / 6-9 (bare `6` and the ninths
  `9`/`maj9`/`m9` are allowed). The parser-rejection and guard tests were updated
  accordingly (`9` moved from rejected to accepted).

Known voicer deltas for five-voice chords (documented, not blockers):
- `Drop2` is triad-oriented → effectively inert for ninths.
- A very tall five-voice stack near an instrument's range edge can have voices
  collapsed by the range clamp (pre-existing behaviour; also affects 4-voice
  chords at the edges).

Definition of done: parse with zero warnings; pass the D-L4.5 guard; correct
intervals; existing serialized assets still load; full EditMode suite green;
`Runtime/` grep for unguarded `ChordQuality` switches clean. Met: full EditMode
suite green (the two `ChordProgressionEditorWindow_V2Tests` ninth fixtures green
after the window's two methods gained the ninth cases); two fixtures inverted
(`9` now valid) + three extended; `Runtime/` grep clean.

---

## Chord inversions — BUILT (CQ-A1-OBJ2, closed 2026-07-05)

Design analysis delivered (CQ-A1 Objective 2 recommendation): inversions belong
to the **voicing layer**, not the Roman DSL. **Built as recommended** (batch
CQ-A1-OBJ2) — the historical rationale below is preserved unchanged; the
closure record follows it.

- The backing composer already chooses inversions via `VoiceLeadingConfig`
  scoring; an explicit per-chord inversion would either be redundant or require
  pinning candidates.
- Slash notation (`V/3`) collides with the response-handler guard (which splits
  tokens on dashes, not `/`) and overloads secondary-dominant notation.
- Figured-bass numbers collide with extension numbers — now more so, since `6`
  and `9` are qualities.

Built exactly along those lines: a per-chord inversion hint added to the
*input set* (not the asset grammar), honoured by `ChordTrackComposer`,
following the §6 directional-modulation-hint precedent (a transient per-render
voicing constraint; default-unset = bit-identical output; deterministic).
Governed by `runtime/SSoT_Composer_Backing_Track.md` **§7** (new). The
`VoiceLeading.cs` gate was satisfied by the CQ-B1 inversion-loop review plus
this batch's pre-implementation verification (sole `IChordVoicer` implementer
confirmed; two structurally identical `VoiceChord` call sites).

Decisions recorded at closure:

- **D0 = A** — pin semantics: a valid pin forces the requested inversion (the
  voicer still owns register/spacing); not a bias.
- **D1 = A** — the hint is an inversion index (`0` = root, clamped conceptually
  to chord arity), not a bass pitch-class; no figured-bass/slash-notation
  collision.
- **D2 = A** — per-chord scope: `PartConfig.ChordInversionHints :
  IReadOnlyList<int?>`, index-aligned to the rendered progression's events.
- **D2a = a** — sticky-per-position: the pin applies at its event position on
  every pattern repeat within the render (the per-render one-shot lifecycle is
  the `Compose` snapshot-and-clear, mirroring §6).
- **D2b = a** — an out-of-range inversion value is a safe no-op (treated as
  unset), never clamped — garbage input cannot silently force root position.
- **D3 = A** — on the render's very first chord the §6 directional hint wins
  when both are active; structural in both render loops (the voicer is never
  invoked when §6 produces the chord).

Delivered: pin in `BasicVoiceLeadingVoicer.GeneratePcCandidates` (made
`internal` as the test seam); optional trailing `forcedInversion` on
`IChordVoicer.VoiceChord`; `PartConfig.ChordInversionHints` transient;
snapshot/clear + `ResolveInversionPin` threading through both chord render
loops; `Tests/Editor/ChordTrackComposer_InversionPinTests.cs`; SSoT §7 (+ §8
renumber) and `SSoT_Runtime_Song_Model_and_Config.md §1.1` registration.
Definition of done met: unset = bit-identical (baseline candidate-set test);
exact rotation at arbitrary positions; out-of-range/null no-ops; D3 precedence
under a combined-hint scenario; dedicated D2a sticky-per-position test.

---

## Future work (recorded, not scheduled)

- Added-tone qualities without a seventh (`add9`, `6/9`) — currently forbidden;
  would need a grid-arity model that counts voices instead of asking
  "is it a seventh?".
- A grid-authoring model for >4-voice chords (would retire the ninth/6th
  grid-row deltas).
- Eleventh / thirteenth chords (six/seven voices) — a further tier, gated on the
  same voicer-arity considerations as Tier B, plus tall-stack range handling.

# SSoT_CONTRACTS

This document defines cross-cutting documentation and architecture contracts for MidiGenPlay.

## 1. Package boundary contract

MidiGenPlay package truth covers:

- song model and part/track configuration,
- track parameters and package-owned style bundle abstractions,
- generation orchestration,
- role-specific composers,
- package-owned authoring assets and authoring tools.

It does not automatically cover:

- ALWTTT gameplay semantics,
- live composition session gameplay bridge,
- `MidiMusicManager` behavior on the game side,
- card economy/combat/status systems,
- legacy `CardData` redesign material.

## 2. Runtime vs authoring vs integration contract

### Runtime
Describes what the package executes and how generated MIDI is produced.

### Authoring
Describes how package-owned assets and editor tooling define inputs consumed by runtime.

### Integration
Describes how an external project injects or consumes package data. Integration docs are secondary unless explicitly promoted.

## 3. Song model contract

`SongConfig` is the runtime song model authority.

Key invariants:

- a song is composed of `Parts`,
- parts own meter, tonality, root and track lists,
- tracks own role, channel/instrument identity and `TrackParameters`,
- `TrackParameters` are the role-agnostic extension point for package data injection.

## 4. TrackParameters contract

`TrackParameters` is the package-owned extension surface for per-track inputs.

Current cross-role fields of documentary importance:

- `Pattern`
- `RhythmRecipe`
- `BackingRecipe`
- `Style`

The `Style` field is package-owned and points to `TrackStyleBundleSO` or a derived bundle.

Concrete game-specific bundles may be consumed by the package, but they do not redefine package theory.

## 5. Meter authority contract

For runtime normalization and rendering, the **Part time signature is authoritative** unless a future promoted contract explicitly says otherwise.

This rule is especially important for:

- `ChordTrackComposer`
- `RhythmTrackComposer`
- `BassTrackComposer` (since CA-F2)
- `MelodyTrackComposer` (since MEL-BEATUNIT-1)
- authoring assets that may be reused across signatures

The concrete obligation is that one beat is `MusicTheory.GetBeatSpan(part.TimeSignature)`,
never an assumed quarter note. Both entries above were added after a batch fixed exactly
that assumption; each recorded the deviation in its own SSoT
(`SSoT_Composer_Bass_Track.md` §3.4, `SSoT_Composer_Melody_Track.md` §7.1).

## 6. No silent promotion contract

The following may not become authoritative by accident:

- roadmap files
- research prompts
- cross-project docs
- archive docs

Promotion requires an explicit change in `coverage-matrix.md` and `changelog-ssot.md`.

## 7. Preservation contract

Migration prefers:

- reclassification,
- absorption,
- archive with superseded markers,
- and explicit redirects

over deletion.

## 8. Consumer mix gain contract (MGP-MIX-1)

- No entry ⇒ no event: a render with a null/empty `mixGains` map, or a track
  without an entry, is byte-identical to the pre-MIX-1 render.
- Same seed + same map ⇒ same bytes. The gain path consumes no randomness and
  displaces no draw (stripped-CC7 note-identity is test-pinned).
- Law: `cc7 = clamp(round(volume01 × gain × 100), 0, 127)`; gain 0 or
  volume01 0 mutes without removing note events.
- Keying: `MusicianTrackKey (musicianId, TrackRole)`; Rhythm entries
  warn+ignore in v1.
- `volume01` stays package-side; consumer gain is per-render data, never a
  `SongConfig` field, never an asset override.

## 9. Update completion contract

A technical change is not complete until:

- the primary SSoT is updated,
- `CURRENT_STATE.md` is updated if focus/reality changed,
- `changelog-ssot.md` is updated if meaning/authority changed,
- and `coverage-matrix.md` is updated if the primary home changed.

## 10. Track-list order contract (MGP-ALWTTT-BASS-ORDER-1)

The track list is a CONSUMER IDENTITY structure — it determines channel
allocation, `ChannelRoles`, `mus:` tags and merged chunk order — and consumers
must be free to append to it in play order without reordering.

Therefore: **no rendered output may depend on the ORDER of entries in the
track list, only on their content and their per-track keys.** Cross-track
dependencies are resolved by the orchestrator's pass structure
(`SSoT_Runtime_Generation_Orchestration.md` §5.7), never by requiring the host
to order the list.

Consequences:
- A new cross-track dependency needs a pass, not a documented ordering caveat.
- Per-track seeds key on `(role, musicianId)`; adding, removing or moving a row
  must not shift another row's stream.
- Physical merge is index-ordered and decoupled from compose order, so byte
  layout stays a function of the list.

Recorded exception (pre-existing, unchanged): `SlapPocket` consumes the Rhythm
track's PUBLISHED onsets and therefore still degrades gracefully to the
decoupled figure when the Rhythm row composes after the bass. It is opt-in,
warn-only and never silent, and `SelfPocket` (SLAPFIG-1) provides the
order-free alternative.

## 11. Emitted MIDI event contract (MGP-ALWTTT-BASS-BEND-1)

Generated track files carry note events, the per-track bank/patch stamp, the
per-track channel stamp, the consumer mix-gain CC7 (§8) — and, since BEND-1,
**pitch bend events**.

- **Producer.** `PitchBendWriter` (Runtime, pure, static) is the ONLY writer of
  pitch bend in the package. Composers do not emit bend through
  `PatternBuilder` or the articulation engine; they plan gestures and hand
  them to the writer as post-build surgery on their own file, after
  `pb.Build().ToFile(tempoMap)` and BEFORE the channel stamp and the
  bank/patch stamp. Gestures arrive in TICKS: meter authority stays with the
  composer (§5).
- **No entry ⇒ no event.** A null or empty gesture list leaves the file
  untouched — not re-deltaed, not rebuilt. Every render that plans no gesture
  is byte-identical to a build without the writer.
- **Same-tick ordering law.** At any tick a bend point is written AFTER every
  event that is not a sounding note-on, and BEFORE the first sounding note-on.
  A note starting on a reset tick therefore starts centred, never bent for
  zero ticks. Insertion never re-times existing events.
- **Channel-state reset invariant.** Pitch bend is CHANNEL state. Every
  gesture carries its own reset to centre (8192); same-tick points coalesce
  (last value wins, one event per tick), and the LAST bend event of a chunk is
  always centre. No render may leave a channel detuned past its final gesture.
- **Single-channel scope.** The writer operates on the first track chunk and
  assumes a monophonic, single-channel track. Bass and melody are the intended
  consumers; the backing track is a declared NON-consumer, because on a
  polyphonic channel a bend detunes every sounding voice.
- **Range.** The GM default sensitivity (±2 semitones) is assumed; no RPN is
  emitted in v1. Targets beyond the range clamp with a warning. A future
  consumer needing a wider range must negotiate it via RPN.
- **Determinism.** Same gestures + same file ⇒ same bytes. The writer draws no
  rng and reads no external state.

## 12. PartConfig mutation contract (MGP-TRIAGE-ALWTTT-R3)

Composers generally treat `SongConfig.PartConfig` as read-only input. There is
exactly ONE sanctioned exception, and because a consumer depends on it, it is a
contract rather than an implementation detail.

- **The exception.** `ChordTrackComposer` step 2a* (`adoptProgressionTonality`,
  `runtime/SSoT_Composer_Backing_Track.md` §2.3) assigns `part.Tonality` in
  place during compose. Card-level opt-in, default OFF, deterministic, zero rng
  draws.
- **Committed.** The mutation is visible to the caller after
  `GenerateSinglePart` returns and MUST remain so. Composing against an internal
  copy of the `PartConfig`, or reverting the mutation on exit, is a BREAKING
  change to consumers and requires a boundary-record entry, not a refactor note.
  The failure mode is silent: the consumer keeps the stale mode and generates
  its remaining tracks against the wrong scale.
- **Preferred interface.** `ResolvedTrackChoice.tonalityAdopted` /
  `.adoptedTonality`, reachable via `PartRender.resolvedByTrack`. Explicit,
  per-track, testable, and able to distinguish adoption from coincidence. New
  consumers should read this; the in-place mutation exists so existing ones do
  not break.
- **Scope limit.** This licenses ONE field on ONE opt-in path. No other
  composer may mutate the `PartConfig`, and no new mutation may be added
  without extending this section.

## 13. Chord identity contract (MGP-TONALITY-1, D-TON10)

The sounding chord of a progression event is defined by the triple
**(degree, degreeAccidental, quality)**. Every composer, and every component
that names or derives a chord for display, must resolve the root as
`TransposeNoteName(scaleNames[(int)degree], degreeAccidental)`. Reading
`degree` alone is a contract violation: it makes two tracks harmonize against
different chords a semitone apart on any accidental-bearing progression.

Current adherents: `ChordTrackComposer` (both emission sites),
`BassTrackComposer` (main selection + walk approach target),
`MelodyTrackComposer` (both paths), `HarmonyTrackComposer`
(MGP-ALWTTT-HARMONY-1, F-HARM-2), and `SongOrchestrator`'s chord-label printer.
Added in MGP-TONALITY-1 after a confirmed audible defect; the defect survived
two prior batches because no contract stated the rule.

**Verification.** `TonalityAudit`'s counters CANNOT detect a breach on their
own — a composer with a wrong chord belief judges its own wrong notes as
in-chord and reports green. The detector is the canonical re-classification of
the MGP-TONALITY-2 matrix (`beliefDiv`, D-TON2-PARITY=A), which recomputes each
event's chord pitch classes under this contract and re-judges every emitted
note against them. Any parity claim must come from `beliefDiv`, never from the
audit counters. Held at `beliefDiv == 0` across 476 cells on 2026-09-01.

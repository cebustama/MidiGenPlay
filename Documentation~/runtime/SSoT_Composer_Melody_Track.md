# SSoT — Composer Melody Track

## Scope

This document is the primary runtime authority for melody generation behavior:

- `MelodyTrackComposer`
- `PhrasePlanner` as runtime planning dependency
- `MelodicLeadingConfig`
- `MelodicStyleSO`
- use of phrase palette/archetype-driven planning
- melody-specific style/leading/palette override resolution

## 1. Runtime model

Melody generation is split conceptually into two stages:

1. **phrase planning** — generate expressive/rhythmic slots for a span
2. **note choice and rendering** — choose pitches/strategies and convert slots to MIDI events

`PhrasePlanner` is responsible for stage 1.
`MelodyTrackComposer` and melody strategies are responsible for stage 2.

## 2. Inputs

Important package-owned melody inputs include:

- `MelodicLeadingConfig`
- phrase palette / phrase archetypes
- `MelodicStyleSO`
- per-track strategy/style overrides carried through package runtime inputs

## 3. Current integration boundary

The current runtime can consume a concrete external bundle type such as `MelodyCardConfigSO`.
That is useful integration surface, but it is **not** the primary theory of melody authoring for the package.

Document package truth here first.
Describe ALWTTT-specific card usage only in cross-project reference.

## 4. Phrase planning contract

`PhrasePlanner` is a rhythmic/expressive planner, not the final pitch selector.

It produces `PhraseSlot` structures carrying information such as:

- timing
- phrase grouping
- contour hints
- accent / phrase-end information
- phrase-local state cues for strategies

## 5. Leading/style contract

`MelodicLeadingConfig` expresses pitch-motion and expressive defaults.
`MelodicStyleSO` selects or modifies strategy behavior at phrase level.
These are package-owned concepts and remain primary even when a consuming game injects concrete override bundles.

## 6. Boundary with authoring

Authoring-side meaning of palettes, phrase archetypes, leading assets and style assets lives in:

- `authoring/SSoT_Authoring_Melody_Composition.md`

This runtime SSoT documents how those inputs are consumed during generation.

## 7. Update triggers

Update this SSoT when:

- phrase planner/composer responsibilities change,
- override precedence changes,
- strategy/style resolution changes,
- runtime use of melody bundles changes.

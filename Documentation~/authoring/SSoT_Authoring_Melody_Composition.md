# SSoT — Authoring Melody Composition

> **Status note (2026-04-12):**
> The authoring concepts described in this document — phrase palettes, phrase archetypes,
> `MelodicLeadingConfig`, and `MelodicStyleSO` — are **current implemented truth**.
>
> The following are **not yet documented here** because they are not yet implemented:
> - `MelodyPatternData` canonical per-note format (redesign is Phase 1 of `Roadmap_Melody_Authoring_MVP.md`)
> - `MelodyGenerationParamsSO` asset
> - Melody pattern authoring wizard (`EditorWindow`)
> - Pattern-override path in `MelodyTrackComposer`
>
> Do not treat this SSoT as authority for those planned items.
> See `planning/active/Roadmap_Melody_Authoring_MVP.md` for their accepted design decisions.

## Scope

This document is the primary authority for package-owned melody authoring concepts:

- phrase palettes
- phrase archetypes
- `PhrasePlanner` authoring-side inputs
- `MelodicLeadingConfig`
- `MelodicStyleSO`
- authoring-side override concepts for melody generation

## 1. Authoring mental model

Melody authoring in MidiGenPlay is not just a note list.
It is a layered system in which authoring assets shape how runtime plans and renders phrases.

Important layers:

- phrase vocabulary (palette + archetypes)
- leading/personality defaults (`MelodicLeadingConfig`)
- style-level strategy selection or modification (`MelodicStyleSO`)

## 2. Phrase planning authoring

Phrase-related assets define the expressive/rhythmic plan later consumed by runtime.

This includes ideas such as:

- contour bias
- phrase grouping
- slot generation across a chord span
- phrase-end/accent cues

## 3. Leading config authoring

`MelodicLeadingConfig` is the package-owned asset/contract for:

- note-source tendencies
- motion constraints
- expression defaults such as velocity ranges
- default phrase palette selection

## 4. Style authoring

`MelodicStyleSO` expresses strategy-selection or phrase-level modification behavior for melody generation.

It is package truth even when an external project chooses to inject it through a game-facing bundle.

## 5. Explicit boundary with ALWTTT

ALWTTT may use a concrete bundle such as `MelodyCardConfigSO` to inject:

- leading overrides,
- palette overrides,
- style overrides.

That concrete bundle is useful integration material, but it does **not** define the package-level theory of melody composition.

## 6. Runtime handoff

Runtime consumption of these authoring concepts is defined in:

- `runtime/SSoT_Composer_Melody_Track.md`

## 7. Update triggers

Update this SSoT when:

- phrase palette/archetype meaning changes,
- leading/style asset meaning changes,
- melody override model changes,
- authoring-side melody concepts change independently of ALWTTT.

When Phase 1 of `Roadmap_Melody_Authoring_MVP.md` is complete, update this SSoT to cover:
- the new `MelodyPatternData` canonical format,
- `MelodyGenerationParamsSO`,
- and the authoring wizard pipeline.

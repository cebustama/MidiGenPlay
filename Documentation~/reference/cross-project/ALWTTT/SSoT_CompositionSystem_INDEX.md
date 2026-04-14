> CROSS-PROJECT REFERENCE ONLY — preserved for consumer-project context.
> DO NOT UPDATE THIS FILE AS MIDI GEN PLAY PACKAGE AUTHORITY.
> Primary game-owned authority lives in: `ALWTTT Docs/integrations/midigenplay/ALWTTT_Uses_MidiGenPlay_Quick_Path.md + related governed ALWTTT docs`.

# SSoT Index — Live Composition System (ALWTTT × MidiGenPlay)

**Generated:** 2026-03-04  
**Updated:** 2026-04-12 — corrected MidiGenPlay package doc names in section 3.  
**SSoT rule:** each topic has **one** canonical doc; other docs may only link/redirect.

---

## 0) Composition cards system (taxonomy + payload + bundles + musical vs gameplay separation)

- `SSoT_CompositionCards_TrackStyleBundles.md` ✅ **canonical**

> Note: `SSoT_CompositionCardTypes.md` is now a **redirect stub** to avoid duplication.

---

## 1) Composition authoring tools (EditorWindows / pattern authoring skeletons)

- `SSoT_CompositionAuthoringTools_v1.md` ✅ **canonical**

---

## 2) Runtime bridge (live session + model mutation + SongConfig build + render/cache/playback)

- `SSoT_Runtime_CompositionSession_Bridge.md` ✅ **canonical**

---

## 3) Composer pipelines (in-depth, per TrackRole)

> Note: MidiGenPlay package docs use a different naming convention from these references.
> The canonical package paths (under `Documentation~/runtime/`) are listed below.

- `Documentation~/runtime/SSoT_Composer_Backing_Track.md` ✅ **canonical** (Backing / Chords)
- `Documentation~/runtime/SSoT_Composer_Rhythm_Track.md` ✅ **canonical** (Rhythm / Drums)
- *(TODO — no package SSoT yet)* Bassline composer
- *(TODO — no package SSoT yet)* Melody / Lead composer
- *(TODO — no package SSoT yet)* Harmony composer

---

### Rule of thumb
- If it is **what a card/bundle is** → #0.
- If it is **how we author assets in the editor** → #1.
- If it is **runtime session behavior & caching** → #2.
- If it is **how MIDI is actually generated for a role** → #3.

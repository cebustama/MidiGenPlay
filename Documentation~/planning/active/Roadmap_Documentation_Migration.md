# Roadmap — Documentation Migration

## Goal

Replace the mixed root-documentation state with a governed SSoT-style system for MidiGenPlay without losing information.

## Active phases

### Phase 1 — Governance spine
- [x] Create `README.md`
- [x] Create `SSoT_INDEX.md`
- [x] Create `SSoT_CONTRACTS.md`
- [x] Create `coverage-matrix.md`
- [x] Create `CURRENT_STATE.md`
- [x] Create `changelog-ssot.md`

### Phase 2 — Folder role separation
- [x] Create `runtime/`, `authoring/`, `reference/`, `planning/`, `research/`, `archive/`
- [x] Move ALWTTT-specific docs under `reference/cross-project/ALWTTT/`
- [x] Move research prompts to `research/`
- [x] Move historical `CardData` redesign to `archive/historical/`

### Phase 3 — Runtime authority consolidation
- [x] Draft runtime SSoTs
- [ ] Verify them against current code after next implementation batch
- [ ] Redirect any in-repo links still pointing to old root docs

### Phase 4 — Authoring authority consolidation
- [x] Draft chord/rhythm/melody authoring SSoTs
- [ ] Validate rhythm-authoring SSoT against future editor-window implementation
- [ ] Validate tool conventions after rhythm authoring tool lands

### Phase 5 — Cleanup and redirect pass
- [ ] Add explicit superseded headers where needed
- [ ] Remove accidental duplicate authorities from root
- [ ] Archive any newly absorbed docs that are no longer needed at top level

## Notes

This roadmap is planning, not implementation truth.
If migration decisions change authority, update:

- `coverage-matrix.md`
- `changelog-ssot.md`
- relevant SSoT(s)

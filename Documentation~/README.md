# MidiGenPlay Documentation

This folder is the governed documentation system for **MidiGenPlay**.

Its purpose is to make it easy to answer:

- what is true **today** in the package,
- where authoritative package truth lives,
- what belongs to package runtime vs authoring vs cross-project integration,
- what is active now,
- what is next,
- and which document must be updated after a technical change.

## Read this first

1. `SSoT_INDEX.md`
2. `CURRENT_STATE.md`
3. `coverage-matrix.md`
4. the primary SSoT for the concept you are working on

## Folder map

- `runtime/` — authoritative runtime and composer behavior
- `authoring/` — authoritative authoring data flows and tools
- `reference/` — supporting docs, examples, and cross-project integration material
- `planning/` — current and archived plans; planning never overrides implementation truth
- `research/` — exploratory and non-authoritative material
- `archive/` — preserved legacy or absorbed docs; archive never overrides current SSoTs

## Scope boundary

This documentation system is for **MidiGenPlay package truth**.

It does **not** treat ALWTTT runtime/gameplay integration docs as automatic package authority.
Cross-project material is preserved under `reference/cross-project/ALWTTT/`.

## Local update protocol

After every meaningful technical change:

1. Identify what concept actually changed.
2. Find its primary home in `coverage-matrix.md`.
3. Update that primary SSoT first.
4. Then apply follow-up rules:
   - update `CURRENT_STATE.md` if operational reality or active focus changed,
   - update `changelog-ssot.md` if meaning, contract, authority, or interpretation changed,
   - update `coverage-matrix.md` only if the concept’s primary home changed,
   - update reference docs only if workflow or usage changed.

A technical change is not complete until the required documentation updates are done.

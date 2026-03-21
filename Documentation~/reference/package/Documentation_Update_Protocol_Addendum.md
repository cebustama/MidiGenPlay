# Recommended addition — Documentation update protocol

Add a short local reminder to `SSoT_INDEX.md` (preferred) or `Documentation~/README.md` so the update loop stays visible in day-to-day package work.

## Short rule
After every meaningful technical change:

1. **Identify what concept actually changed.**
2. **Find its primary home in `coverage-matrix.md`.**
3. **Update that primary SSoT first.**
4. Then apply the follow-up rules:
   - update `CURRENT_STATE.md` if operational reality or active focus changed,
   - update `changelog-ssot.md` if meaning, contract, authority, or interpretation changed,
   - update `coverage-matrix.md` only if the concept’s primary home changed,
   - update reference/test docs only if workflow or usage pattern changed.

A technical change is not complete until the required documentation updates are done.

## Minimum file set for deciding what needs updates
You usually do **not** need to send every documentation file to determine the update set.

The minimum decision batch is normally:
- `SSoT_INDEX.md`
- `coverage-matrix.md`
- `CURRENT_STATE.md`
- `changelog-ssot.md`
- the suspected primary SSoT(s) for the changed concept

This is usually enough to answer:
- what changed,
- where authoritative truth lives,
- which derived docs are impacted,
- whether a change is semantic, operational, or only reference-level.

## Why this belongs here
The broader reusable governance guideline already defines the process formally, but the package should also keep a short local version so the maintenance loop remains visible while doing implementation work.

# runtime/

This folder contains the authoritative runtime documentation for MidiGenPlay.

What belongs here:

- runtime song model and configuration semantics,
- orchestration and render flow,
- role-specific composer behavior,
- runtime normalization contracts.

What does not belong here:

- speculative design,
- editor authoring UX details without runtime semantics,
- ALWTTT-only gameplay bridge material.

If a runtime concept conflicts with a cross-project reference doc, the runtime SSoT wins.

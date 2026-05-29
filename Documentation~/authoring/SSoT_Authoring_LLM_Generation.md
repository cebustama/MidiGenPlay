# SSoT — Authoring: LLM-Assisted Generation

## Scope

This document is the **primary authority** for LLM-assisted generation inside
package-owned authoring tools. It defines the architecture, the contracts every
LLM-authoring surface must honor, and the failure-handling expectations — as a
**replicable pattern**, not a drum-specific record.

The pattern was first implemented for drums (Batches L1–L3,
`DrumPatternEditorWindow`). It is documented here so future authoring tools
(chord editor at L4, then others) follow the same recipe rather than reinventing
it. Drum specifics appear only as the worked example; the contracts are general.

It covers:

- the LLM → asset boundary and why it preserves the runtime determinism invariant
- the seven-stage pipeline shape shared by every LLM-authoring surface
- the contracts an implementer must not break (non-blocking async, asset-as-seam,
  no silent fallback, CRLF-safe parsing)
- failure handling and where errors surface
- how this relates to LLM Core, to the per-tool authoring SSoT, and to runtime

It does **not**:

- define runtime composer behavior (that stays in `runtime/` SSoTs)
- define the rhythm DSL grammar (that is
  `authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A)
- define LLM Core's own client/provider/cost internals (external package;
  this SSoT only describes the integration shape we depend on)

Per-tool specifics (the rhythm DSL, the drum setup-card shape) remain governed by
the relevant authoring SSoT. This document governs the **cross-cutting pattern**.

---

## 1. The load-bearing principle: the asset is the seam

LLM output is **not deterministic** across runs with the same prompt. The
runtime determinism invariant in `SSoT_CONTRACTS.md` ("same inputs + same seed ⇒
same outputs") is nonetheless preserved **by construction**, because the LLM
never lives on the runtime side.

The LLM produces an **authoring asset** (for drums: a `DrumPatternData`, via
DSL → parser → `StepState[][]`). That asset is the boundary. Runtime consumes the
asset deterministically (for drums:
`RhythmTrackComposer.ComposeFromGrid`). Between the non-deterministic LLM and the
deterministic runtime sits a saved, user-reviewed, user-editable asset.

This is the single most important rule for any future LLM-authoring work:

> **The LLM produces data that a human reviews and a deterministic runtime
> consumes. The LLM is never on the runtime path. The asset is the seam.**

Any proposal that would call an LLM at render time, or feed LLM output into a
composer without passing through a persisted asset, violates this SSoT and the
determinism contract it protects.

---

## 2. The replicable pipeline (seven stages)

Every LLM-authoring surface in the package is built from these stages. The drum
implementation is named in parentheses as the worked example.

1. **Vocabulary asset** — domain knowledge as data, not code. A ScriptableObject
   carrying the genre/style entries the prompt builder reads.
   *(Drums: `RhythmGenreVocabularySO`.)*

2. **Pure-function prompt builder** — takes editor state + the selected
   vocabulary entry, returns a system+user prompt pair (or a typed failure). No
   I/O, no Unity calls beyond data reads; fully unit-testable.
   *(Drums: `DrumPatternLLMPromptBuilder.Build`.)*

3. **Generator wrapper** — takes an injected `ILLMClient`, runs the prompt
   through LLM Core's single-shot helper, then runs the response through the
   domain parser, returning parsed data + warnings + token counts. Never throws
   for an LLM/parse failure; returns a typed failure result.
   *(Drums: `DrumPatternLLMGenerator.GenerateAsync`.)*

4. **Pure-function importer** — parses the model's full output shape (for the
   drum recipe: a markdown "setup card" + a fenced DSL block) into the structures
   the editor applies. Reused by both the Generate path and a manual
   clipboard-Import path so the two share one apply surface.
   *(Drums: `DrumPatternEditorImporter`.)*

5. **Alias dictionary** — resolves user-friendly short names to canonical enum
   members, on the importer side only, so hand-typed and pasted input is forgiving
   without polluting the parser grammar. Unknown tokens **warn**; they never
   silently resolve to a default.
   *(Drums: `LaneAliasDictionary`.)*

6. **Async response handler** — the unification point. Translates either a live
   generation result or a pasted payload into a single immutable `Outcome` the
   editor applies on the main thread. Generate and Import converge here.
   *(Drums: `DrumPatternLLMResponseHandler` — `GenerateAsync` and `FromPayload`.)*

7. **Editor-window wiring** — the panel: vocabulary + client source fields, a
   genre/style selector, free-text direction, a cost-cap field, and
   Generate/Regenerate/Import buttons. The call is async and non-blocking; the
   outcome is applied through the tool's **existing** edit/apply/save path, never
   by mutating the asset directly.
   *(Drums: `DrumPatternEditorWindow` LLM panel.)*

The split matters: stages 1–2 and 4–5 are pure and unit-tested; stage 3 is
unit-tested via an injected fake client; stages 6–7 are thin glue verified by
smoke test. New tools reuse the shape and replace only the domain-specific
parser, vocabulary, and asset type.

---

## 3. Contracts (must not break)

These are the invariants an implementer of any LLM-authoring surface must honor.

### 3.1 Non-blocking async — never block the main thread

The LLM call is awaited from an `async void` UI handler. The implementation
**must not** block on the task: no `.Result`, no `.Wait()`, no
`.GetAwaiter().GetResult()` on the UI thread. The editor must stay responsive
while the call is in flight, and a visible in-flight indicator must disable the
trigger to prevent re-entrancy.

(A one-shot console/menu harness outside the editor UI may block, since there is
no UI thread to starve. That exemption is for harnesses only.)

### 3.2 Asset-as-seam — no runtime LLM use

See §1. The LLM produces an authoring asset; runtime consumes the asset. No LLM
call may sit on a render/compose path. This is the determinism contract.

### 3.3 No silent fallback on unknown tokens

When input carries a token the system cannot resolve (an unknown lane alias, an
out-of-grammar glyph), the system **warns with location information** and
declines to guess. It must not silently substitute a default. Out-of-grammar
glyphs route through the domain parser's warning channel exactly as user-typed
invalid input would; unknown aliases prompt the user to disambiguate.

**Where the domain parser degrades instead of failing, the guard moves up.**
The contract is "no silent fallback," not "the parser must fail." The rhythm
parser hard-rejects unknown tokens, so for the drum adopter the parser *is* the
enforcement point. The chord adopter's `RomanProgressionParser` behaves
differently: an unknown quality suffix is **not** rejected — the parser logs a
warning and downgrades the chord to diatonic quality, so a "successful" parse
can still contain an out-of-alphabet token. To honor this contract the chord
adopter enforces it one level up, in the response handler: an allowlist guard
(`ChordProgressionLLMResponseHandler.TryFindForbiddenToken`, mirroring the
parser's accepted suffixes) treats any off-alphabet token as a hard failure and
declines to apply, rather than relying on the parser to reject it (D-L4.5). A
new adopter must check whether its parser rejects or degrades, and place the
guard accordingly.

### 3.4 CRLF-safe parsing

Pasted and model-returned payloads arrive with mixed line endings. Any line
splitting **must** treat `\r\n` as a single separator. Splitting on a character
array `{ '\r', '\n' }` with empty-entry removal, or splitting on `\n` and
trimming a trailing `\r`, are both acceptable; a naive char-array split that
treats `\r\n` as two separators fragments the payload and is a known regression
(hit during L2). New parsing code is tested with an explicit CRLF payload.

### 3.5 Working-copy / apply contract preserved

LLM and imported output are committed into the tool's working copy (for drums,
the text rows), never written to the persisted asset directly. The user must take
the tool's explicit **Apply / Save** action to mutate the asset. LLM authoring
adds a content source; it does not add a new write path. This is the package-wide
authoring-tool contract (`authoring/SSoT_Authoring_Tools.md`): normalize →
preview → apply/save, no silent writes.

### 3.6 Cost cap is pre-network

A character (or token) budget is enforced **before** the network call, in the
prompt builder, so an over-budget prompt is refused without spending. The cost
cap is a true cap, not post-hoc reporting. The refusal surfaces a clear error in
the tool's warning panel and leaves existing content untouched.
*(Drums: `DrumPatternLLMPromptBuilder.Input.maxCharBudget`, surfaced via the
editor's "Max prompt chars (budget)" field, D-L3.1.)*

---

## 4. Failure handling

All failures are non-fatal and surface in the tool's existing warning panel with
the same shape as user-typed errors. No failure mode crashes the window or
discards the user's existing content.

| Failure | Where caught | User sees |
|---|---|---|
| Over-budget prompt | prompt builder (pre-network) | budget error; nothing sent |
| No client / bad client | editor client resolution | resolution error |
| LLM unavailable / API error | generator (try/catch around the call) | graceful failure line; rows preserved |
| Empty / no-DSL response | generator (extraction stage) | "could not locate DSL" failure |
| Lane-count mismatch | generator (split stage) | mismatch error with counts |
| Invalid glyphs, valid shape | domain parser | located warnings; result still applied for review |
| Unknown alias | importer | disambiguation prompt; no silent default |

The two "still produces something" rows (invalid glyphs, and partial salvage of a
flagged response) are deliberate: the user sees the flawed output **and** the
warnings, and edits before Apply. The seam (§1) makes this safe.

---

## 5. Client injection and testability

The generator takes an `ILLMClient` by parameter; the editor resolves the
concrete client (override → project default) and passes it in. This injection is
the unit-test seam: a fake `ILLMClient` returning a canned `LLMCompletionResult`
exercises the full generator+parser path deterministically, with no network.

The fake sits on the real call path: LLM Core's single-shot helper delegates to
`ILLMClient.CreateChatCompletionAsync(prompt, instructions)` when no files are
attached, so a fake implementing that method is genuinely invoked. Tests assert
the fake was actually called, so the suite fails loudly if that delegation ever
changes rather than passing on a path that never ran.
*(Drums: `FakeLLMClient` + `DrumPatternLLMGeneratorTests`, D-L3.2.)*

---

## 6. Relationship to LLM Core

LLM Core (external package) owns the client abstraction, provider
implementations, cost catalog, and async execution helper. This package depends
on it via a direct asmdef reference (D-L3) and mirrors its integration shape:
estimate precedence (catalog → per-client fallback → no estimate) and
effective-instructions resolution order (override → asset → client-data → empty).

The provider is absorbed at the `LLMClientFactory → ILLMClient` seam: the
OpenAI → Anthropic switch during L1 required **no** MidiGenPlay code change. New
providers are an LLM Core concern, invisible to this pattern.

LLM Core's own SSoT (`SSoT_Editor_Tooling_and_Wizard.md`) is **external** — it
informs integration shape but is not MidiGenPlay authority.

---

## 7. Current implemented surface

The drum surface is implemented and signed off (Batches L1–L3, closed
2026-05-28). The worked-example mapping of pattern stage → drum artifact is in
§2. For the rhythm DSL grammar and the drum setup-card shape, see
`authoring/SSoT_Authoring_Rhythm_Patterns.md` §3A. For the editor capability
listing, see `authoring/SSoT_Authoring_Tools.md`.

The chord progression editor is the second adopter (Batch L4, closed
2026-05-29). Stage → chord-artifact mapping:

| Stage (§2) | Chord artifact |
|---|---|
| Vocabulary SO | `ChordGenreVocabularySO` (+ `ChordGenreVocabularyBuilder` seeder) |
| Pure-function prompt builder | `ChordProgressionLLMPromptBuilder` |
| Generator wrapper (injectable `ILLMClient`) | `ChordProgressionLLMGenerator` |
| Pure-function importer (setup-card + DSL) | `ChordProgressionEditorImporter` |
| Alias dictionary | *not adopted* — Roman numerals are already canonical; no alias layer needed |
| Async response handler (unifies generate + import) | `ChordProgressionLLMResponseHandler` (carries the §3.3 degrade-guard) |
| Editor window wiring | `ChordProgressionEditorWindow` (`.LLM` partial); outcome→field mapping isolated as the pure `ChordLLMFieldPlan` |

The chord output shape is a Roman-numeral string (not a grid), matching the
editor's native affordance; for the Roman DSL grammar and the chord setup-card
shape see `authoring/SSoT_Authoring_Chord_Progressions.md`. Generalization to a
second instance left §2's stage shape intact (copy-then-unify, D-L4.3); the one
contract subtlety it surfaced is the degrade-vs-fail enforcement point now
documented in §3.3. No alias stage was needed.

A future shared generic over the two prompt builders / generators
(`LLMAuthoringPromptBuilder<TParser, TVocab>` or similar) is deferred until the
two instances justify the abstraction (D-L4.3 rationale).

---

## 8. Update triggers

Update this SSoT when:

- a new authoring tool adopts the pattern (add it to §7; if it forces a change to
  a stage or contract, revise §2/§3 and note why)
- a contract in §3 changes (this is a contract change — coordinate with
  `SSoT_CONTRACTS.md` if the determinism boundary is touched)
- the LLM Core integration shape changes (§6)
- a new failure mode is added or an existing one re-routed (§4)

This SSoT is primary for the cross-cutting pattern. Per-tool grammar and asset
details stay in the relevant per-tool authoring SSoT.

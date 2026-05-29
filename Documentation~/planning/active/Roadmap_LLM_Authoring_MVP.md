# Roadmap — LLM-Assisted Authoring MVP

> Active MidiGenPlay package planning.
> This roadmap defines a new authoring track: LLM-assisted in-editor generation of authoring assets, starting with rhythm/drums.
> Cross-cutting by design — the pattern, once built for drums, will generalize to chord progression and melody editors.

## Purpose

Add LLM-assisted prompt-to-asset generation as a first-class affordance in package-owned editor windows. Starting with drums because:

- The rhythm text DSL exists (Phase 7) and gives the LLM a small, well-specified output surface.
- `DrumPatternTextParser` already validates outputs and emits structured warnings — a built-in containment seam.
- The canonical asset path (`DrumPatternData` → `RhythmTrackComposer`) is unaffected; the LLM produces DSL, the parser handles it through Phase 7's normal `ApplyTextEdits` flow.

## Scope

### In scope (this MVP, Batches L1–L3)
- LLM-assisted generation surface in `DrumPatternEditorWindow`, integrated with text mode.
- A new asset type `RhythmGenreVocabularySO` carrying genre knowledge as data, not code.
- Dependency on the LLM Core package for the LLM client, cost guardrails, and async response handling.
- One default vocabulary asset shipped with the package (6–10 genres).

### Out of scope (deferred to later batches)
- Chord progression editor LLM integration (Batch L4, after L1–L3 close).
- Melody editor LLM integration (not on this roadmap; revisit when the melody authoring MVP roadmap surfaces a relevant phase).
- Any runtime LLM use. The runtime determinism invariant in `SSoT_CONTRACTS.md` is preserved by construction — the LLM produces authoring assets only.
- Persistent prompt history in the editor.
- Multi-provider abstraction in the editor UI (LLM Core already handles that layer).

## Current code-backed baseline

**Batch L1 implemented (closed 2026-05-28).** The following are code-backed:
- `Runtime/CoreScripts/Composition/Data/RhythmGenreVocabularySO.cs` — genre vocabulary SO + POCOs (`GenreEntry`, `LaneSpec`, `GlyphCell`, `SubStyleCue`) + `TryResolve`.
- `Editor/DrumPatternLLMPromptBuilder.cs` — pure-function prompt builder (system + user) with 11 EditMode tests in `Tests/Editor/DrumPatternLLMPromptBuilderTests.cs`.
- `Editor/DrumPatternLLMGenerator.cs` — LLM Core wrapper; build → execute → fence-extract → lane-split → parse; returns parsed `StepState[][]` + warnings + token counts.
- `Editor/LaneShortNames.cs` — L2-anticipation short-name conventions (D-L9 alignment).
- `Editor/DrumPatternLLMConsoleHarness.cs` — one-shot `[MenuItem]` harness (async void).
- `MidiGenPlay.Editor.asmdef` references `BCS.LLM.Core.Runtime`.

Batch L2 (editor UI) and L3 (smoke tests + new SSoT) remain unimplemented.

Adjacent code that exists today and which this roadmap depends on:
- `DrumPatternTextParser` and `DrumPatternTextWarning` in `MidiGenPlay.Authoring` (Phase 7).
- `DrumPatternEditorWindow` text mode with HelpBox legend and warning panel (Phase 7).
- LLM Core package — `SSoT_Editor_Tooling_and_Wizard.md` carries the integration shape (rebuild semantics, effective-instructions resolution order, history policy, files panel behavior, estimate precedence, ping behavior).

## Decisions to surface at Batch L1 start

These are the open decisions identified at roadmap drafting (2026-05-24). They should be surfaced as the first turn of the Batch L1 chat, with options + trade-offs + recommended defaults, and locked before any artifact is written.

**Resolutions (locked at Batch L1, 2026-05-28):**

| Decision | Resolution | Note |
|---|---|---|
| D-L1 | **B** | Generate button in text mode (shares surface with clipboard import). |
| D-L2 | **A** | `RhythmGenreVocabularySO`. R1 vocabulary YAML ported into SO shape. |
| D-L3 | **A** | Direct asmdef reference to `BCS.LLM.Core.Runtime`. No consumer needs MGP without LLM Core. |
| D-L4 | see §D-L4 below | Measured at L1 run; numbers recorded. |
| D-L5 | catalog implemented | 8-mode failure catalog handled inline in `DrumPatternLLMGenerator`. |
| D-L6 | **A** | Drum-only this MVP. |
| D-L7 | invariant confirmed | Documented, not a decision. |
| D-L8 | **B** (pre-locked 2026-05-25) | Clipboard import — implemented in L2. |
| D-L9 | **ii** (pre-locked 2026-05-25) | Importer-side `LaneAliasDictionary` — written in L2; conventions list shipped in L1 (`LaneShortNames.cs`). |
| D-L10 | **α** (new, surfaced at L1) | Minimal LLM Core path: `PromptExecutionHelper` single-shot only. No validator/retry/orchestration surfaces. Retry deferred to L2+ (factors cleanly into `IResponseValidator` when needed). |
| D-L11 | resolved | LLM Core API contract sourced directly from shared package files (`PromptExecutionHelper`, `ILLMClient`, etc.). |

### Provider note (cross-project, 2026-05-28)

L1 was specified against OpenAI but the first end-to-end run switched to
Anthropic mid-batch (the developer's OpenAI API access lapsed). This required
a cross-project sub-batch in **LLM Core** adding an Anthropic provider
(`AnthropicClientData`, `AnthropicLLMClient`, factory + enum + env-wizard
updates). **No MidiGenPlay code changed for the switch** — the
`LLMClientFactory` → `ILLMClient` seam absorbed it. The harness selects an
`AnthropicClientData` asset instead of `OpenAIClientData`; everything else is
identical. This validates D-L3's factory-based dependency design.

### D-L1 — Where does the prompt UI live?
- (A) Top-level affordance always visible in `DrumPatternEditorWindow` (above the Grid/Text toolbar).
- (B) Text-mode only — a "Generate" button rendered next to the Grid/Text toolbar when the user is in text mode.
- (C) Separate window (`LLMRhythmGeneratorWindow`) targeting the editor; output is pasted into the editor's text rows.

Trade-off: (A) maximum discoverability; (B) keeps the affordance contextual to where its output goes; (C) isolates the LLM concerns from the editor's core surface.

### D-L2 — How is genre vocabulary shipped?
- (A) ScriptableObject (`RhythmGenreVocabularySO`) — data-driven, designer-editable, asset-bundled.
- (B) Embedded constants in the prompt builder — code-only.
- (C) JSON file shipped with the package — data-driven but bypasses Unity's asset system.

Trade-off: (A) follows package convention (everything else is SO-based); (B) is smallest but requires code change for any vocabulary update; (C) interoperable but introduces a non-Unity loader path.

### D-L3 — How does the editor depend on LLM Core?
- (A) Direct asmdef reference — hard dependency. The package will not build without LLM Core present.
- (B) Optional via conditional compile (`MIDIGENPLAY_HAS_LLM_CORE` define) — soft dependency. Editor still builds without LLM Core; the Generate affordance is hidden when the define is absent.
- (C) Reflection bridge — runtime adapter, no compile-time dependency on LLM Core.

Trade-off: (A) simplest; (B) preserves the package's standalone usability if a consumer doesn't want LLM Core; (C) most decoupled but adds reflection-related fragility (and the LLM Core SSoT explicitly deprecates reflection as the preferred path — §5.3).

### D-L4 — Cost guardrails
- **Default model:** `claude-sonnet-4-6` (Anthropic). Chosen for cost/capability balance for structured DSL generation. (Note: provider switched OpenAI → Anthropic mid-batch — see §Provider note.)
- **Max tokens per generation:** 800 (asset-configured). Measured output for funk/4-4/2-measure/4-subdivision single-lane-set was **218 tokens** — comfortable headroom; the cap leaves room for 4+ lane / multi-measure patterns.
- **Prompt token budget:** measured **972 input tokens** for the funk single-genre prompt (system 2,333 chars + user 904 chars = 3,237 chars total). The earlier 4,000-token prior holds as a comfortable soft cap; no tightening needed downward, but L2 should re-measure with the full multi-genre vocabulary asset since per-genre prompt size scales with vocabulary richness.
- **Pricing precedence:** mirror LLM Core (catalog → per-client fallback → no estimate). Unchanged.
- **First-run telemetry (2026-05-28):** model `claude-sonnet-4-6`, 972 in / 218 out / 1,190 total tokens, latency 4,581 ms, zero parser warnings.

### D-L5 — Failure handling
- LLM unavailable / API error: surface in the editor's warning panel; no crash; existing text rows preserved.
- Invalid DSL returned: route through `DrumPatternTextParser` warning panel exactly as user-typed invalid DSL would be.
- Non-DSL response (LLM produced prose, code fences, or commentary alongside DSL): the prompt builder should ask explicitly for DSL only; the response handler should strip code fences and ignore prose-after-DSL; failure to extract DSL → warning panel.

### D-L6 — Scope for the L1–L3 batches
- (A) Drum-only this MVP. Chord and melody are L4+.
- (B) Drum + chord together in L1–L3.

Recommendation: (A). Drum alone is enough surface to validate the pattern. Generalizing to chord prematurely doubles the unknowns.

### D-L7 — Determinism
LLM authoring output is **not deterministic** across runs with the same prompt. This is acceptable for authoring because the user sees the result and edits it before Apply/Save. The runtime determinism invariant in `SSoT_CONTRACTS.md` is unaffected: the LLM never lives on the runtime side; the resulting `DrumPatternData` asset is the seam, and the asset is consumed deterministically by `RhythmTrackComposer.ComposeFromGrid`. Documenting explicitly so it is not later mistaken for an invariant violation.

### D-L8 — How is the setup card + DSL block imported? **(LOCKED 2026-05-25 = B)**

Surfaced after R1 buildlog review (workshop R1 closed 2026-05-25). R1's output shape is **setup card** (Grid-mode metadata: TimeSignature, Measures, Subdivisions, lane composition) **+ DSL block** (bare glyph strings, one per lane). R1's contract emits both; only the DSL block fits Phase 7's existing text-mode paste flow. The setup card is human-readable prep that currently requires the user to manually configure Grid mode before pasting — a real friction point identified in the R1 review.

- (A) User configures Grid mode manually from the setup card, pastes DSL into Text mode. Current behavior; zero new code; preserves Phase 7's Text-mode contract verbatim.
- (B) **LOCKED.** An "Import from clipboard" affordance reads the full block (markdown setup card + fenced DSL), parses the setup card to auto-configure Grid mode (TimeSignature, Measures, Subdivisions, lane composition with instruments + default velocities), then populates `_textRows` from the DSL block in setup-card order. Single-paste import.
- (C) Hybrid — both paths available.

**Rationale for B:** the value-add of LLM authoring comes precisely from removing manual setup work. (A) would leave the user copying four lines into Grid mode by hand before benefiting from the LLM. (C) doubles surface area for marginal gain. (B) is the path the R1 output format was designed to enable.

**L2 implementation note:** a new `DrumPatternEditorImporter.cs` (or similar) lives editor-side in `Editor/`. The class parses the markdown structure (looking for canonical headers like "**Setup (configure in Grid mode):**" and "**DSL (...):**"), extracts setup parameters, constructs lanes, and feeds the DSL block through `DrumPatternTextParser` exactly as user-typed input would be processed. Failure modes route through the existing warning panel. The class is also reusable as the response-handling tail of `DrumPatternLLMGenerator` — both consume the same markdown shape.

**D-L1 sub-implication:** D-L1 = B (Generate button in text mode) is now the strong default because Import-from-clipboard and Generate share the same surface area in text mode.

### D-L9 — How are lane aliases (e.g., `HHc:` → ClosedHiHat) resolved? **(LOCKED 2026-05-25 = ii)**

Surfaced after R1 buildlog review. User-flagged feature: when typing or pasting DSL by hand (not through the LLM), being able to write `HHc: xxx-xxx-` and have the system recognize "closed hi-hat" without knowing the exact `GeneralMidiPercussion` enum member name. Three implementation paths:

- (i) Aliases on the parser side. `DrumPatternTextParser` learns the `alias: glyphs` syntax. **Rejected:** breaks parser purity (13 EditMode tests pass against bare-glyphs grammar); aliases are an importer concern, not a per-step-glyph concern.
- (ii) **LOCKED.** Aliases on the L2 importer side. The parser stays bare-glyphs. A new `LaneAliasDictionary` (data: small canonical mapping, e.g., `BD`/`BassDrum1`, `SN`/`AcousticSnare`, `HHc`/`ClosedHiHat`, `HHo`/`OpenHiHat`, etc.) is consulted by the L2 importer before invoking the parser. The dictionary maps user-friendly short names to canonical `GeneralMidiPercussion` enum members.
- (iii) Aliases on the genre vocabulary side. Each genre declares its own alias table. **Rejected:** scatters alias knowledge across N vocabularies; introduces drift risk when two genres disagree on what `HHc` means.

**Rationale for ii:** preserves `DrumPatternTextParser`'s 13-test contract intact (no risk to Phase 7 work); centralizes alias resolution in one small editor-side dictionary; keeps R1's vocabulary clean (R1 already emits full GM names, no aliases needed). R1 needs **no change** to be compatible — aliases are purely a convenience for hand-typed input.

**L2 implementation note:** `LaneAliasDictionary` is a small static dict or a `ScriptableObject`. Probably static dict for v1 — the canonical short names are well-established percussion conventions and don't need to ship as an editable asset. The L2 importer's clipboard-parse path consults it when the setup card carries an unrecognized name token. If the token is not a valid `GeneralMidiPercussion` enum *and* not a known alias, the importer warns and asks the user to disambiguate.

**Alignment with R1's vocabulary:** R1's `genre_vocabulary.md` uses full GM names throughout. The L2 alias dictionary should be **aligned with R1's implicit conventions** at L1 closure — i.e., the short names L2 accepts should be the ones R1's vocabulary already implies (`BD`, `SN`, `HHc`, `HHo`, etc.). This is the canonical-conventions alignment item in the R1 buildlog's "Open items carried forward."

## Batch map

### Batch L1 — Decisions + vocabulary asset + prompt builder + LLM Core wiring

**STATUS: CLOSED 2026-05-28.** All DoD items met. Clean end-to-end run against
Anthropic `claude-sonnet-4-6`: 4 lanes, 32 steps, zero parser warnings.
Deferred: `Default Rhythm Genres.asset` full 8-genre population (the harness
validated with an in-memory single-genre funk vocabulary). See punch-list.

**Goal:** Get the data and the LLM call working in isolation. No editor UI yet.

**Deliverables:**
- D-L1 through D-L7 locked (D-L8 and D-L9 already locked 2026-05-25 — confirm at L1 start).
- `Runtime/CoreScripts/Composition/Data/RhythmGenreVocabularySO.cs` — genre definitions, lane compositions per genre, characteristic glyphs, velocity hints, style descriptors.
- `Editor/DrumPatternLLMPromptBuilder.cs` — **pure function** building system prompt + user prompt from editor state (signature, measures, subdivisions, lane composition) + selected genre vocabulary.
- `Editor/DrumPatternLLMGenerator.cs` — calls LLM Core, parses response through `DrumPatternTextParser`, returns parsed `List<StepState>[]` per lane plus a warning list.
- `Resources/ScriptableObjects/Vocabularies/Default Rhythm Genres.asset` — default vocabulary (6–10 genres, decided in D-L4 sibling resolution; v1 seed = the 8 genres from R1's `genre_vocabulary.md`: funk, rock, jazz, hip-hop, latin, metal, drum'n'bass, country).
- `Tests/Editor/DrumPatternLLMPromptBuilderTests.cs` — pure-function tests for prompt assembly (vocabulary lookup, parameter substitution, missing-genre fallback).
- `MidiGenPlay.Editor.asmdef` updated — add LLM Core asmdef reference per D-L3.
- **L2-anticipation deliverable (data only, no editor code):** define the canonical short-name conventions used by R1's vocabulary (`BD`/`SN`/`HHc`/`HHo`/`RC`/`LT`/`HT`/`CR`, etc.) as a non-authoritative comment block or constants list. L2's `LaneAliasDictionary` (D-L9) will consume this list. No `LaneAliasDictionary.cs` is written in L1; the alignment work is to make sure the short names L1 puts into vocabulary headers match what L2 will accept.

**Definition of done:**
- Vocabulary asset exists and is browseable in the Inspector.
- Prompt builder unit-tested with at least one happy-path and one missing-genre case.
- LLM Core call succeeds end-to-end in a one-shot console harness (no editor UI required).
- Short-name conventions documented for L2's alias dictionary consumption.
- No editor UI surface introduced (D-L8 and D-L9 implementation belongs to L2).

### Batch L2 — Editor UI integration

**STATUS: CLOSED (2026-05-28).**

**Closure note:** All deliverables landed; DoD met. SMR-L1/L2/L4/L6/L7 pass;
SMR-L3/L5 deferred to L3 (they need the cost-cap and mock-client surfaces). Two
mid-batch artifact revisions: (1) the importer's DSL extraction moved from
fence-first to glyph-content detection after SMR-L6 showed real payloads place
DSL outside its own fence (outer-wrapped, bare-after-label); (2) a CRLF split bug
(char-array split treated `\r\n` as two separators, fragmenting the glyph run)
was fixed by splitting on `\n` and trimming `\r`. Lesson logged: test
line-ending behavior under CRLF. Delivered: `RhythmGenreVocabularyBuilder` +
seeded `Default Rhythm Genres.asset` (8 genres), `DrumPatternEditorImporter`
(12 tests), `LaneAliasDictionary` (11 tests), `DrumPatternLLMResponseHandler`
(5 tests), and the `DrumPatternEditorWindow` LLM panel.

**Goal:** Wire the prompt UI, the async response handling, the text-row population, and the clipboard-import + alias-resolution affordances locked at 2026-05-25.

**Deliverables:**
- `Editor/DrumPatternEditorWindow.cs` modified — prompt field, Generate button, **Import-from-clipboard button (D-L8)**, status feedback (spinner / cost estimate / cancel), async wiring.
- `Editor/DrumPatternLLMResponseHandler.cs` — async response handling, text-row population (writes into `_textRows` via the existing seam), error surfacing through the existing warning panel.
- `Editor/DrumPatternEditorImporter.cs` (**new, D-L8**) — pure-function parser for the markdown "setup card + DSL block" shape that R1 produces. Consumed by both the Import button and `DrumPatternLLMResponseHandler` (same input shape from both paths). Parses the setup card → constructs lanes with instruments and default velocities → feeds the DSL block to `DrumPatternTextParser` → writes results into `_textRows`. Failure modes route through the existing warning panel.
- `Editor/LaneAliasDictionary.cs` (**new, D-L9**) — static dictionary mapping short names (`BD`, `SN`, `HHc`, `HHo`, `RC`, etc.) to canonical `GeneralMidiPercussion` enum members. Consulted by `DrumPatternEditorImporter` when the setup card carries a name token that is not a direct enum match. Short names aligned with R1's `genre_vocabulary.md` conventions per the L1 alignment deliverable.
- `Tests/Editor/DrumPatternEditorImporterTests.cs` (**new**) — pure-function tests for the markdown-import path: happy-path full block, malformed setup card, alias resolution, alias-not-found, DSL block with wrong lane count vs setup card.
- Editor preserves the working-copy / apply / save-as contract: LLM output and clipboard-imported output are both committed into text rows; the user must explicitly Apply to mutate the asset.

**Definition of done:**
- The editor's Generate button produces populated text rows from a prompt.
- The editor's Import button parses an R1-shaped clipboard payload and auto-configures Grid mode + populates text rows in one action.
- Lane aliases (`HHc:`, `BD:`, etc.) resolve correctly via `LaneAliasDictionary` when present in a hand-typed or imported setup card.
- The warning panel surfaces parse errors, LLM failures, and import failures with the same shape as user-typed errors.
- The Grid/Text toolbar still works; entering text mode after an LLM generation or clipboard import does not lose any per-cell velocity preservation that Phase 7 guarantees.

### Batch L3 — Manual smoke tests + governed documentation updates

**STATUS: CLOSED (2026-05-28).**

**Closure note:** All deliverables landed. D-L3.1 (cost-cap UI) and D-L3.2
(injectable-client mock seam) implemented; `FakeLLMClient` +
`DrumPatternLLMGeneratorTests` (6 tests) make SMR-L3/L5 deterministic. New SSoT
`authoring/SSoT_Authoring_LLM_Generation.md` written as a replicable pattern and
flipped to primary for LLM-assisted authoring (coverage-matrix). Governed-doc
flips applied. Full SMR-L1..L7 manual sign-off recorded in the L3 sign-off
checklist. The LLM Authoring MVP (L1–L3) is complete; L4 (chord editor
generalization, following this SSoT's pattern) and proposed L5 (L-PAL) are the
next candidates.

**Goal:** Verify behavior on real prompts; close governance.

**Smoke tests:**
- SMR-L1: "funky 4/4, 2 measures, 4 subdivisions" → produces parseable DSL with at least kick/snare/hat lanes.
- SMR-L2: "7/8 beat with random fill" → produces parseable DSL respecting 7/8 meter.
- SMR-L3: cost cap fires correctly when prompt exceeds budget; user sees a clear error.
- SMR-L4: LLM Core unavailable (no API key, no network) → graceful fallback (warning, no crash, existing text rows preserved).
- SMR-L5: invalid DSL response (simulated by mocking the LLM client) → routed through `DrumPatternTextParser` warning panel with location info.
- SMR-L6 (**D-L8 verification**): paste a complete R1-shaped block (setup card + DSL) into the editor via the Import button → Grid mode is auto-configured (TimeSignature, Measures, Subdivisions, lane composition) and `_textRows` are populated in one action. Drag-and-drop an R1 buildlog output from the Claude.ai chat as the actual test corpus.
- SMR-L7 (**D-L9 verification**): hand-type a setup card using lane aliases (`HHc:`, `BD:`, etc.) instead of full enum names → `LaneAliasDictionary` resolves them; lanes are configured with the correct `GeneralMidiPercussion` members. Negative case: unknown alias → importer warns and asks for disambiguation; no silent fallback.

**Doc updates at L3 closure:**
- `authoring/SSoT_Authoring_LLM_Generation.md` — **new SSoT**, primary authority for LLM-assisted authoring across the package. Promotes from "planning-only" to "implemented truth" the contract for LLM-asset-LLM separation, the determinism note, the failure-handling expectations, and the asset-as-seam principle. **Written as a replicable pattern**, not just a drum-specific record: it documents the L1→L2 architecture as a reusable recipe so future authoring tools (chord editor at L4, then others) can follow it — vocabulary SO → pure-function prompt builder → LLM Core generator wrapper → pure-function importer (setup-card + DSL) → alias dictionary → async response handler (unifies generate + import) → editor window wiring (async non-blocking, default+override client, outcome applied through the tool's existing edit/apply path). Includes the load-bearing notes: no main-thread blocking on async LLM calls; asset-as-seam preserves determinism; no silent fallback on unknown tokens; test line endings under CRLF.
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` — new §3A subsection "LLM-assisted generation" pointing at the new SSoT for the contract and at this batch for the implementation history.
- `authoring/SSoT_Authoring_Tools.md` — `DrumPatternEditorWindow` capabilities list gains "LLM-assisted generation".
- `CURRENT_STATE.md` — LLM Authoring MVP → completed; Phase 9 or further LLM batches promoted to next as applicable.
- `coverage-matrix.md` — primary home for LLM-assisted authoring flips from this roadmap to the new SSoT.
- `changelog-ssot.md` — completion entry.

### Batch L4 — Chord editor generalization

**STATUS: CLOSED (2026-05-29).**

**Closure note:** The chord progression editor is now the second adopter of the
LLM authoring pattern. All DoD items met; 47 chord LLM EditMode tests green;
manual smoke tests CSMR-S1..S8 pass. The pattern's §2 stage shape held intact
(copy-then-unify, D-L4.3) — the chord tool copied the drum artifacts rather than
prematurely extracting a generic. The one contract subtlety generalization
surfaced is the degrade-vs-fail enforcement point (D-L4.5), now documented in
`SSoT_Authoring_LLM_Generation.md` §3.3. With L4 closed, the **LLM Authoring MVP
is complete through L4**, and this roadmap is retained as a closed historical
record rather than active planning.

**Goal:** Apply the pattern to `ChordProgressionEditorWindow`.

**Decisions locked:**
- **D-L4.1 — Chord output shape:** Roman-numeral string (matches the editor's
  native affordance; the editor round-trips Grid→Roman already). Not grid output.
- **D-L4.2 — `ChordGenreVocabularySO` shape:** confirmed against what the prompt
  consumes (genreName, styleDescriptors, voicingHints, cadenceCues,
  characteristicProgressions, subStyleCues), structurally parallel to
  `RhythmGenreVocabularySO`.
- **D-L4.3 — Refactor-now vs copy-then-unify:** copy-then-unify. A shared generic
  (`LLMAuthoringPromptBuilder<TParser, TVocab>`) is deferred until the two
  instances justify the abstraction.
- **D-L4.4 — Exact-step-count reinforcement:** durations-sum-to-exactly-N sentence
  in the chord builder's system prompt; backported the equivalent to the drum
  builder.
- **D-L4.5 — Zero-warning enforcement:** `RomanProgressionParser` warns-and-
  downgrades unknown suffixes rather than failing, so a handler-side token-
  allowlist guard (`ChordProgressionLLMResponseHandler.TryFindForbiddenToken`)
  treats off-alphabet tokens as a hard failure.
- **D-L4.6 — Test visibility:** added `Editor/AssemblyInfo.cs` with
  `InternalsVisibleTo("MidiGenPlay.Tests.Editor")` rather than widening the
  public surface.
- **D-L4.7 — Wiring coverage:** extracted the pure `ChordLLMFieldPlan` from the
  window's outcome→field mapping and unit-tested it; IMGUI/async parts covered by
  the manual smoke checklist.
- **D-L4.8 — Vocabulary seeding:** `ChordGenreVocabularyBuilder` menu item writes
  `Default Chord Genres.asset` with a build-time parser+guard self-check; v1 set
  jazz/pop/blues/folk.

**Deliverables (landed):**
- `Runtime/CoreScripts/Composition/Data/ChordGenreVocabularySO.cs`
- `Editor/ChordProgressionLLMPromptBuilder.cs`
- `Editor/ChordProgressionLLMGenerator.cs`
- `Editor/ChordProgressionEditorImporter.cs`
- `Editor/ChordProgressionLLMResponseHandler.cs` (carries the D-L4.5 guard)
- `Editor/ChordLLMFieldPlan.cs`
- `Editor/ChordGenreVocabularyBuilder.cs`
- `Editor/AssemblyInfo.cs`
- `Editor/ChordProgressionEditorWindow.LLM.cs` (partial) + 2-line edit to
  `ChordProgressionEditorWindow.cs`; plus a "Create New Progression" affordance.
- Tests (`Tests/Editor/`): prompt builder (11), importer (9), generator (6),
  response handler (13, incl. guard), wiring (8) = 47.

**Smoke tests (CSMR, manual — all pass):**
- CSMR-S1: "jazz, 4/4, 4 measures" → parseable Roman progression, preview
  populates, durations sum to exactly 4 measures.
- CSMR-S2: "3/4, 6 measures" → previews in 3/4 without parser warnings.
- CSMR-S3: low cost cap → clear error, no network call, fields unchanged.
- CSMR-S4: LLM Core unavailable → graceful failure (warning, no crash, field
  preserved).
- CSMR-S5: paste `V13` / `V/V` via Import → blocked with warning; fields
  unchanged (D-L4.5 live).
- CSMR-S6: paste full setup-card + Roman block → fields auto-configure + Roman
  populates in one action.
- CSMR-S7: paste bare Roman block (no card) → progression-only; status prompts
  for time signature / measures.
- CSMR-S8: "Create New Progression" with content present → confirm prompt; on
  confirm, target detaches and fields reset; the previously-targeted asset
  unchanged on disk.

**Doc updates at L4 closure (applied 2026-05-29):**
- `ssot_manifest.yaml` — chord LLM artifacts added to the LLM SSoT `governs`;
  degrade-vs-fail invariant added.
- `authoring/SSoT_Authoring_LLM_Generation.md` — §7 lists the chord adopter with
  stage→artifact mapping; §3.3 gains the degrade-vs-fail enforcement nuance.
- `CURRENT_STATE.md` — L4 → just completed; LLM MVP complete through L4; L5 /
  D-L4.3 unification as next candidates.
- `coverage-matrix.md` — cross-cutting row cites chord Roman DSL authority;
  milestone-plan row retired to closed historical; L4 closure note.
- `changelog-ssot.md` — completion entry.

### Batch L5 — DrumPattern palettes + editor integration + catalogue wizard (L-PAL)

**STATUS: PROPOSED (defined 2026-05-28 from L2 closure realisations R1/R2).**

**Goal:** Make generated/authored drum patterns easy to collect, organize, and
audition. Add a palette asset, editor affordances to fill palettes directly, and
a read-only catalogue wizard — so LLM generation pays off by accumulating many
auditioned variations into themed palettes (genre, tempo, time signature, feel).

**Why after L2/L3:** L2 makes generating patterns cheap. Without a place to put
them, that throughput is wasted. Palettes turn "generate one pattern" into
"build a library of auditioned variations."

**Template:** `ChordProgressionPaletteSO` + `ChordProgressionCatalogueWizard`
are the proven analogues. L5 mirrors their shape for drums.

**Deliverables:**
- `Runtime/CoreScripts/Composition/Data/DrumPatternPaletteSO.cs` (new) —
  weighted-entry palette of `DrumPatternData`, mirroring `ChordProgressionPaletteSO`:
  `WeightedEntry { DrumPatternData pattern; float weight; }`; metadata
  (`paletteDisplayName`, `paletteNotes`); optional TS-aware selection toggle;
  `PickRandomPattern(System.Random, bool clone)`; `GetDisplayName()`;
  `OnValidate()` null-guard. Lives in Runtime/ for asset-system convenience
  (data only; runtime consumption is D-PAL.3).
- `Editor/DrumPatternEditorWindow.cs` (modified) — palette affordances: assign a
  `DrumPatternPaletteSO` or pick from an auto-filled project-scan dropdown; an
  **"Add to Palette"** button appending the current working pattern (saved as an
  asset first — no orphan in-memory entries); optional default weight + dedup
  guard. Honors the working-copy/apply contract.
- `Editor/DrumPatternCatalogueWizard.cs` (new) — read-only catalogue browser
  mirroring `ChordProgressionCatalogueWizard`: folder scan; per-asset derived
  metadata (time signature, measures, subdivisions, instruments used, active-step
  density); palette rows with membership + aggregate metadata; filter/sort/search;
  no mutation.
- `Tests/Editor/DrumPatternPaletteSOTests.cs` (new) — weighted-pick determinism
  (seeded RNG), clone-on-pick isolation, empty/invalid-entry handling.

**Decisions to surface at L5 start (not locked):**
- D-PAL.1 — "Add to Palette" creates a new `DrumPatternData` asset from the
  working copy vs references an already-saved target. (Recommend: save-as new if
  unsaved, reference if already saved.)
- D-PAL.2 — Auto-fill dropdown source: configurable scan folders (like the
  catalogue wizard) vs project-wide `FindAssets`. (Recommend: scan folders,
  defaulting to drums + palette folders.)
- D-PAL.3 — Runtime consumes drum palettes now, or author-only first? Chord
  palettes feed runtime selection; wiring drums has determinism implications.
  (Recommend: author-only first; runtime consumption is a later decision.)
- D-PAL.4 — Weight semantics: reuse chord palette's weighted random vs uniform +
  manual order. (Recommend: reuse weighted model for consistency.)

**Definition of done:**
- A `DrumPatternPaletteSO` can be created, populated from the editor's
  "Add to Palette" button, and inspected.
- The editor's palette dropdown auto-fills from the project.
- The catalogue wizard lists drum-pattern assets and palettes with filterable
  derived metadata, read-only.
- Palette pick is deterministic under a seeded RNG; clone-on-pick isolates the asset.
- Working-copy/apply contract intact; no orphan entries.

**Out of scope for L5:**
- Runtime consumption of drum palettes (unless D-PAL.3 says otherwise).
- The fill tag system (R3 — separate runtime batch).
- LLM-driven palette auto-population (generate-N-and-file) — a natural L5
  successor, its own batch once the manual path is proven.

## Cross-references

- **Workshop catalog R1** (`rhythm-pattern-generator` skill) — the Claude skill that prototypes this surface in the workshop. Workshop entry in `skills_and_agents_catalog.md`, build slot in `build_sequence.md` Step 3.5. R1 runs in Claude.ai and produces DSL via copy-paste; this roadmap integrates the same generative shape in-editor via LLM Core. The genre vocabulary corpus built for R1 is a candidate seed for `Default Rhythm Genres.asset`. **R1 and this roadmap are independent artifacts under separate governance** — the workshop owns R1; this package owns the in-editor tool.
- **LLM Core package** — `SSoT_Editor_Tooling_and_Wizard.md` is the integration shape this roadmap mirrors. Specifically:
  - Estimate precedence (catalog → per-client fallback → no estimate) → mirror exactly.
  - Effective-instructions resolution order (override → asset → client-data → empty) → reuse.
  - Rebuild semantics (client is a snapshot of agent/config at rebuild time) → respect.
  - History policy and files panel → not consumed in L1–L3, no need to mirror; revisit at L4.

## Determinism note (load-bearing)

`SSoT_CONTRACTS.md` requires composers be deterministic. **This roadmap does not violate that invariant.** The LLM is an authoring tool. It produces a `DrumPatternData` asset (via DSL → `DrumPatternTextParser` → `StepState[][]`); the asset is then consumed deterministically by `RhythmTrackComposer.ComposeFromGrid` (post-2026-05-23 runtime path, with per-step velocity).

The LLM never lives on the runtime side. The asset is the seam.

The new SSoT at L3 (`authoring/SSoT_Authoring_LLM_Generation.md`) will state this explicitly, with the asset boundary called out so future LLM-authoring work in other tracks does not erode it.

## Future work — realisations captured at L2 closure (2026-05-28)

These surfaced during L2 and are recorded so they are not lost. Each is a
candidate batch with its own decisions; none is in the original L1–L4 MVP scope.

- **R1 — DrumPattern palette + editor "Add to Palette"** → **Batch L5 (L-PAL)**,
  fully defined above. A `DrumPatternPaletteSO` analogous to
  `ChordProgressionPaletteSO`, fillable directly from `DrumPatternEditorWindow`.
  Highest-value follow-on: makes LLM generation pay off by accumulating
  auditioned variations into themed palettes.
- **R2 — Drum Catalogue Wizard** → folded into **Batch L5**. Read-only catalogue
  browser mirroring `ChordProgressionCatalogueWizard`. Depends on R1 (it
  catalogues into/over palettes).
- **R3 — Fill tag system** (future; own design pass). A tagging scheme letting a
  part mark beats / measures / measure-sections as "fills", with the Composer
  generating fills procedurally at generation time. This is a **runtime concern**
  crossing the asset seam into `RhythmTrackComposer` — not an authoring-tool
  feature. Largest of the four; needs its own design batch and likely
  runtime-SSoT changes. Deferred; placeholder only.
- **R4 — Document the LLM support process for replication** → **folded into L3**.
  The new `authoring/SSoT_Authoring_LLM_Generation.md` is written as a replicable
  pattern (see Batch L3 doc-updates) so the chord editor (L4) and later tools can
  reuse the L1→L2 architecture. No separate batch needed.

## Out of scope (full list)

- Chord/melody/bass extensions (L4+).
- Any LLM use on the runtime side.
- Persistent prompt history in the editor.
- Multi-LLM-provider abstraction in the UI (LLM Core handles this).
- LLM-driven mutation of existing patterns (this MVP is generate-from-prompt only; "edit existing pattern by prompt" is a separate later affordance).
- Cost reporting beyond what LLM Core surfaces.
- **R1-as-runtime-skill (deferred to hypothetical Batch L5).** Invoking the R1 workshop skill directly from the editor via Anthropic's Skills API (so the editor doesn't need its own `RhythmGenreVocabularySO` + `DrumPatternLLMPromptBuilder` and instead delegates to R1's installed skill bundle) is a separate architectural direction. It would change R1's status from "workshop-only, copy-paste-only" to "operational dependency of the package." Surfaced at 2026-05-25 R1 buildlog review and explicitly deferred until L1–L4 close — at which point the package will have its own vocabulary and prompt-builder shape, and the trade-off becomes "do we maintain two parallel implementations of the same surface or unify them?" Decision deferred to evidence-based comparison post-L4. Not blocking any current batch.

## Related authorities

- `CURRENT_STATE.md`
- `authoring/SSoT_Authoring_Tools.md`
- `authoring/SSoT_Authoring_Rhythm_Patterns.md` (§3A "Text mode")
- `coverage-matrix.md`
- `SSoT_CONTRACTS.md` (determinism invariant)
- LLM Core package — `SSoT_Editor_Tooling_and_Wizard.md` (external; not MidiGenPlay authority but informs integration shape)
- Workshop — `skills_and_agents_catalog.md` R1 entry; `build_sequence.md` Step 3.5 (cross-project, not MidiGenPlay authority)

## Update triggers

Update this roadmap when:

- Batch L1 closes (mark closed outcomes; flip Batch L2 to active).
- Any of D-L1 through D-L7 are locked before Batch L1 closes (record the resolution).
- Scope expansion: if Batch L4 is promoted from "deferred" to "active", expand the L4 section into a full batch spec.
- The dependency on LLM Core changes (new version pinned, integration shape revised, package renamed).
- This roadmap is superseded by an authoring SSoT (at L3 closure, the new SSoT becomes primary; this roadmap moves to "closed-batches" reference).

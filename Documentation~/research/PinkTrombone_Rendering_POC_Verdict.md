# Pink Trombone Rendering POC — Verdict Note

> **Authority class: research.** Exploratory finding; never authoritative for
> implemented behavior. Companion to
> `PinkTrombone_Performance_Backbone_Proposal.md` and
> `PinkTrombone_Proposal_Agent_Review.md` in this folder.
>
> **Status: FINAL** (2026-07-20, Session 4 close). Sessions 1–4 ran inside the
> MidiGenPlay host project at `Assets/PinkTrombonePOC/` — a dev-time folder +
> asmdef consuming `MidiGenPlay.Runtime`. **Zero runtime and zero SSoT changes
> across all four sessions.** The boundary held; the one demand that would have
> broken it is recorded in §6 rather than built.

## 1. Question and answer

**Question.** MidiGenPlay composes to MIDI. Can an articulatory vocal-tract
model (Pink Trombone) sing the melody track of a real MidiGenPlay render well
enough — musically and computationally — to justify ALWTTT integration and,
eventually, the deferred Phase D4 package-side sink
(`IPerformanceMetadataSink`)?

**Verdict: QUALIFIED YES.**

Proceed to ALWTTT integration, **as an option, scoped to the singer
character** — not as a general melody voice. The qualifications are
load-bearing and all three travel with the decision:

1. **One voice, at most two.** Measured, not extrapolated (§3).
2. **A residual pitch instability remains unresolved** (§5). It is a timbral /
   glottal-source property of the method, not a fault in the integration, and
   no POC-side mitigation eliminated it. Acceptable for a character voice with
   deliberate vocal texture; not acceptable for a lead vocal expected to sound
   cleanly in tune.
3. **Phase D4 stays deferred.** Nothing in this POC forced a runtime change.
   The case for the sink is made in §6 and should be re-evaluated *after* the
   ALWTTT integration surfaces real demands.

## 2. What was proven to work

The full chain works end to end: `SmokeSetupSO` →
`SmokeSongConfigAssembler.Assemble` → `Orchestrator.GenerateSinglePart` (bpm +
seed honored) → melody chunk located **by `mus:` tag** (never a channel) →
tempo-mapped to seconds → sung by the forked Pink Trombone, with the remaining
chunks played underneath by MPTK. Verified against both a synthetic scale
fixture (instrument tuning) and an authored `MelodyPatternData` render (the
integration — the two were never conflated).

Listening results (Session 3, final): sustained notes are in tune to a tuner;
transpose −12 is the correct register; interval-scaled pitch lead (0.06 s at a
fifth) fixed the audible portamento problem; internal `IsTouched` articulation
beats an external hard gate; usable tenseness is confined to **0.40–0.60**
(markedly below the 0.68 source default — tenseness is the harshness driver);
the "Neutral" vowel is the best single setting, but all four presets have
musical uses, which motivated the Session 4 vowel-movement batch.

**C-lite expressivity (Session 4, D-S3-H).** Three per-note mappings, all
derived from data the POC already held (part tonality + root + meter + the MIDI
notes), all precomputed on the main thread so the audio callback stays
arithmetic-free:

| Mapping | Driver | Result |
|---|---|---|
| M1 vowel openness | metric weight + note duration | **Kept. The single biggest contributor to perceived quality.** |
| M2 vowel frontness | contour direction | **Kept.** Audible and additive — contrary to the pre-test expectation that it would be cut. |
| M3 tenseness bias | scale degree, clamped inside the 0.40–0.60 window | **Kept.** Audible on the constant-velocity fixture, where any tenseness movement is M3 alone. |

**The voice sounds materially better with expressivity on than off.** All three
mappings survive. Critically, **none of them affected the pitch instability** —
which is what establishes that instability as a property of the synthesis
method rather than of the control layer.

## 3. CPU cost — the hard constraint

| Reading | Config | Avg | Peak |
|---|---|---|---|
| Editor baseline | 1 voice, full rate, tone stage off, no backing | 10 % | 13.5 % |
| Editor idle | component enabled, not playing | 0.3 % | — |
| Build (dev, fullscreen QHD) | 1 voice, full rate | 14 % | 15.2 % |
| Build (dev) | 1 voice + DSP buffer "Best performance" | **10.5 %** | **12 %** |
| Build (dev) | 1 voice, half-rate synthesis | 5.5 % | 6 % |
| **Editor** | **2 voices, full rate** | **20 %** | **23.9 %** |
| MPTK alone (Session 3) | backing, 3-note chords | — | 2 % |

Pink Trombone costs roughly **5× MPTK's entire SoundFont synth**. This is
structural, not a defect: MPTK looks up recorded samples; Pink Trombone steps a
44-segment acoustic waveguide twice per sample — ~4 million segment updates per
second, per voice.

**Scaling is near-linear, now measured rather than predicted:** two voices came
in at 20 % / 23.9 % against one voice at 10 % / 13.5 % under identical Editor
conditions. Voices share no tract, so this is expected and will not improve.

**Ceiling, stated plainly: one singer comfortable, two heavy but real, three or
more off the table — never a section.** DSP CPU is a hard real-time deadline;
headroom matters more than it would for frame cost.

**D-S4-A resolution: A+B adopted, C rejected on quality grounds.** A build with
DSP Buffer Size = "Best performance" meets the target on its own (10.5 % / 12 %
for one voice), so half-rate synthesis is not needed. It was built and measured
anyway — it does roughly halve model cost, as predicted — but it failed the
listening A/B that the Session 4 plan required it to pass: the half-rate voice
sounded lower in pitch and lower in quality. **The pitch drop is a defect, not
a trade-off**: constructing the model at a different sample rate should not
transpose it, so something in the fork's frequency path does not honor the
constructor's rate. Since half-rate is not load-bearing, this was documented
rather than fixed (D-S4-C=A); no sixth fork edit was made.

*Measurement caveat:* the fullscreen QHD development build read **higher** than
the Editor (14 % vs 10 %). DSP CPU % is time-in-callback over available time,
so it inflates under audio-thread preemption as well as under real work;
fullscreen rendering plus attached-profiler overhead accounts for the
inversion. Test 5 confirms it — same build, one project setting changed, back
to 10.5 %. Waveguide segment count was deliberately never reduced: it alters
timbre and would have invalidated every Session 3 listening result.

## 4. What had to be fixed, and the maintenance implication

The MIT source shipped with **four silently broken controls** (found Session 1,
repaired Session 2). The result is a fork: **~1000 lines, 10 files, zero
external dependencies, five commented changes** (`// POC-FORK(n)`, see
`PinkTromboneSrc/FORK_NOTES.md`), MIT license preserved. This is a
self-contained drop, not an ongoing integration burden — but we own it, and it
must never enter `MidiGenPlay.Runtime`: the package emits MIDI and does not own
audio synthesis.

**Noise-stream caveat (Session 2):** the bit-identical repair A/B ran against a
`System.Random`-backed stub of Troschuetz's `StandardGenerator`, so the exact
aspiration/simplex noise stream may differ from the original DLL. This affects
the realization of the noise, not the model's behavior or timbre.

**Open fork question carried forward:** the half-rate pitch shift (§3) and the
residual pitch instability (§5) both point into the fork's frequency /
glottal-source path, which has never been read. A dedicated investigation is
scoped separately and does not block the integration.

## 5. Limitations found

- **Residual pitch instability — the headline limitation.** A persistent
  wobble / "slightly detuned" quality remains on sung melodies. It survived
  every POC-side mitigation attempted: `vibratoWobble` off, `vibratoGain` = 0
  (a partial improvement only), interval-scaled pitch lead, tenseness sweeps,
  and the full C-lite expressivity layer. Sustained notes read correct on a
  tuner, so this is **pitch *definition*, not pitch *accuracy***. Working
  hypothesis: it is inherent to the method — a breathy, noise-inclusive
  glottal source with a weak fundamental reads to the ear as unstable pitch,
  and the model has no lip-radiation stage to correct the spectral tilt.
  **This is the main thing standing between "character voice" and "lead
  vocal."**
- **Non-controllable portamento** (D-S3-E=A: documented, not forked). The glide
  rate is fixed inside the model; fast passages carry it audibly.
- **No lip radiation or room modelling** — raw output is shrill; a
  consumer-side tone stage (2-pole low-pass ≈ 3.5 kHz) is required and would
  live in an ALWTTT voice profile, not the package.
- **Narrow usable tenseness window** (0.40–0.60) — the expressive range of the
  "effort" axis is small, and the window was set on harshness grounds alone.
- **Half-rate synthesis transposes the voice down** — an unresolved fork
  defect, not a quality trade-off (§3).
- **Nasal branch and `TurbulencePoints` (consonants) never exposed or
  explored** — vowels only; no articulated syllables.
- **No shared clock with MPTK backing** — sync is trim-by-ear (`syncTrimMs`);
  MPTK loads via a `UnityWebRequest` coroutine. ALWTTT integration needs a real
  transport answer.
- **Monophonic**, last-note priority, `Melody` / `Lead` roles only.

## 6. The metadata the package does not expose

Everything derivable from the `MidiFile` + part context was built (C-lite):
scale degree, octave, metric position, contour, interval, duration. All three
mappings proved audible and worth keeping — direct evidence that musical
metadata pays off at the voice.

What could **not** be derived at any price is exactly what a singer most
responds to, because it lives in `PhrasePlanner` internal state and never
reaches the MIDI:

**`PerformanceSlotInfo` — the field list a Phase D4 sink should carry, per note
or per slot:**

- `phraseIndex`, `positionInPhrase` (0..1) — where in the phrase this note sits
  (breath support, tapering)
- `isPhraseStart` / `isPhraseEnd` — breath points and releases
- `cadenceType` at phrase end (authentic / half / deceptive / none) — final vs.
  suspended vowel color
- `tensionLevel` (0..1) — the planner's own tension/release arc, driving
  tenseness far more musically than per-note degree can
- `isClimax` / distance-to-climax — vibrato depth, openness, loudness peak
- per-note harmonic context: current chord degree + quality, chord-tone vs.
  non-chord-tone — passing tones want different treatment than resolutions
- `sectionRole` / energy tier, if the orchestrator distinguishes one

C-lite's per-note degree bias (M3) is a crude proxy for `tensionLevel`; the
difference between the two is the concrete measure of what Phase D4 would buy.

**Nuance this POC earned the right to state:** the *expressive* ceiling is set
by metadata, but the *quality* ceiling is set by the instrument (§5). Better
metadata would make this voice more musical; it would not make it more in tune.
Both limits are real, and they are independent.

This demand is **recorded, not built** — no runtime change was made to obtain
it, which was the POC's standing rule.

## 7. Disposition

**Proceed to ALWTTT integration as an option for the singer character.** Next
concerns, in order: transport/clock between the articulatory voice and MPTK
(§5); voice-profile ownership on the consumer side (tone stage, register, vowel
palette, the C-lite mappings); the one-to-two voice budget as a hard design
constraint; and card / live-regeneration behavior.

**Phase D4 remains deferred**, with §6 as the recorded demand profile. It
should be re-opened only if ALWTTT integration produces concrete, repeated
demand for phrase-level metadata — not on the strength of this note alone.

**A separate investigation is warranted** into the fork's frequency and
glottal-source path, covering the residual pitch instability (§5), the
half-rate transposition defect (§3), and whether an alternative or hybrid
synthesis approach yields a more stable fundamental at comparable or lower CPU.
That work is exploratory, independent of the integration, and the integration
should not wait on it — it should assume the current voice.

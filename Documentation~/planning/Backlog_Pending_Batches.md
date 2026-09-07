# Backlog — pending batches

**Status: planning material. NOT implementation authority.** Nothing here
defines runtime truth. Where this file and a governed SSoT disagree, the SSoT
wins and this file is wrong. Its only job is to say what is queued, why it is
queued in that order, and what is already known so the next session does not
re-derive it.

Last reviewed: 2026-09-05, at the close of MGP-MEL-DEGRADE-1.

---

## The queue, in one line each

| Order | Batch | In plain words |
|---|---|---|
| 1 | **MGP-METER-1** | In time signatures like 6/8 the melody runs out of notes before the bar ends, so the band keeps playing and the flute goes quiet halfway. |
| 2 | **MGP-MEL-SPELL-1** | When a note falls exactly between two scale notes we always round it down, but sometimes it should musically go up — and the system can't tell, because it only sees pitch numbers and doesn't know whether that note is "called" D♯ or E♭. |
| 3 | **MGP-MEL-CYCLE-1** | When a motif repeats while climbing, nothing tells it when to stop climbing, so in principle it could rise until it hits the instrument's ceiling and get stuck repeating one note. |
| 4 | **Melody slur** | The bass already knows how to tie two notes into one sliding gesture instead of two separate attacks; the melody could use the exact same mechanism and doesn't. |
| — | **F-MEL-SLOTCOUNT-1** | In 2 of 714 test cells the melody plays one note fewer than before the fix and nobody knows why; it doesn't sound wrong and breaks nothing, but it's written down so a recurrence doesn't start from zero. |

---

## 1. MGP-METER-1 — meter adaptation

**Why it is first: it is the only item with someone waiting on it.**

ALWTTT filed the ask on 2026-09-01. Symptom, concrete and repeatable: an
8-bar part in 6/8 with a procedural melody (`MelodicStyleSO` +
`PhrasePaletteSO`, no `patternOverride`) ends before the rest of the track.
In 4/4 it does not happen. Downstream it blocks in-game validation of F-HARM-1,
the meter fix this package shipped in MGP-ALWTTT-HARMONY-1, because
`HarmonyTrackComposer` follows the melody's guide notes — a short melody
produces short harmony.

**Starting hypothesis, to verify rather than assume.** MEL-BEATUNIT-1
(2026-07-24) resolved the "how long is a beat" axis via
`BeatsToSpan(beats, GetBeatSpan(part.TimeSignature))`. D-MEL5.1 = A explicitly
left the OTHER axis open — "how many beats a bar holds" — with tiles-by-beats
plus a warning as a documented MVP limitation, and bar-time renormalization
recorded as post-MVP. Both are in
`runtime/SSoT_Composer_Melody_Track.md` §7 / §7.1. **If the symptom is not that
axis, it is a new finding and must be said so.**

**Scope is an audit first, then a system.** Not a 6/8 patch. Three questions:
a per-composer, per-route audit of meter adaptation (correct / silently
degraded / broken, across simple, odd and compound meters); an enumeration of
the musical decisions currently taken by omission; and a phased implementation
proposal.

**The design criterion, in the requester's own words:** *any card combined with
any other must sound at least CORRECT, even if it sounds eccentric or
experimental — it must never sound like a SYSTEM FAILURE.* Degradation is
acceptable; incoherence is not.

**Useful precedent from this session.** MGP-MEL-DEGRADE-1's D-DEGRADE-1 = A won
on exactly that principle: it made a mode TOTAL rather than leaving it correct
for some inputs and silently wrong for others. The package already has a
worked example of choosing totality over a special case, and it is worth citing
when the METER-1 decisions are framed.

**Documentation status: NOT RECORDED ANYWHERE.** See the gap register below.

## 2. MGP-MEL-SPELL-1 — harmonic provenance (F-SPELL-PROVENANCE-1)

MGP-MEL-DEGRADE-1 resolved a chromatic motif note by snapping it to the nearest
scale degree at or below (floor). That rule is monotone, so the motif's contour
can never invert, and it reproduces the ♭6 → 5 / ♭2 → 1 resolutions of the
borrowed-harmony family that actually reaches the path. It is nonetheless a
**heuristic**: a raised alteration — the ♯4 of a secondary dominant — would
ideally snap UP.

**Why the rule cannot do better today.** `NoteName` is a pitch class:
`DSharp` and `EFlat` are the same value, and the spelling is lost upstream.

**Two things a future batch must not get wrong**, both established on evidence
this session:

- **MIDI carries no spelling either.** A MIDI file stores note numbers, so this
  can never improve rendered fidelity — only decisions and legibility. That caps
  what the batch is worth and argues against a package-wide spelled-pitch type.
- **The provenance CANNOT be read from the chord root's `degreeAccidental`.**
  `Prog_Maj_Creep_IVpiv` generates its chromaticism by REQUALIFICATION — a major
  III where the scale wants iii minor — with no accidental on the root at all;
  the chromatic tone is the chord's third. Provenance must be derived from the
  chord's pitch classes against the scale. An earlier formulation of this
  follow-up said "carry the `degreeAccidental` sign" and was corrected before it
  was written down.

**The architecture decision this batch must resolve first:** how the chord
context reaches `TransposeByScaleDegrees` without changing the
`IMelodyStrategy` signature, which every strategy shares.

**Where it would also pay off**, so it is not re-derived: the red audit line
prints `chord=[GSharp C DSharp]` for what is a Neapolitan in A♭, which a human
reading a warning has to translate; and the general voice-leading rule that
raised alterations resolve up and lowered ones down is today inexpressible
anywhere in the package.

**Documentation status: recorded** in `runtime/SSoT_Composer_Melody_Track.md`
§7.2, `CURRENT_STATE.md` and `changelog-ssot.md`.

## 3. MGP-MEL-CYCLE-1 — motif transposition accumulation

The echoed motif is offset by `transposeSemitones × cycle` where
`cycle = (_replayCount / n) + 1`, and the only bound is `ClampToInstrument`.
**This holds in BOTH transpose modes** — `ScaleDegrees` fixed which notes the
motif lands on, not how far it travels.

**Measured, not assumed (2026-09-05 render logs).** The smoke's melody phrase
reaches cycle 1; matrix cell 327 reaches cycle 2. With today's authoring
(`notesToRepeat = 3` over phrases of 7–8 slots) the ceiling is ≈ 2 cycles and
the clamp is never approached. The ceiling is roughly `slots / notesToRepeat`,
so a small `n` over a long phrase (n = 1 over 8 slots ⇒ 7 cycles) would reach
it. **The batch is LATENT, not live** — which is why it sits third.

**One listening data point already exists and it passed.** The
MGP-MEL-DEGRADE-1 smoke render used the `ScaleDegrees +2` directive and was
auditioned in Ableton: PASS. That is one render at cycle 1, so it does not
settle the question, but any argument that current behaviour is audibly broken
now has to beat it.

**Same defect family as F-TON-WALK-DRIFT-1 in the bass**: unbounded per-cycle
accumulation whose only stop is a range clamp. The bass version was contained
with a two-sided register fold (D-W2-FLOOR=B) and its root cause is still open
as D-W2-DRIFT. Whether the two deserve a shared law is an open question; the
batch must state its answer either way.

**Options are already framed** (cycle cap / wrap / register-bounded
accumulation / authoring-only) in the revised rehydration prompt. Evidence so
far tilts toward authoring-only or register-bounded. That is not a
recommendation — it is one render at cycle 1, and the decision needs ears.

**Documentation status: recorded** in `runtime/SSoT_Composer_Melody_Track.md`
§7.2 (corrected 2026-09-05 — an earlier revision wrongly attributed the
accumulation to `ChromaticSemitones` alone).

## 4. Melody slur via pitch bend

Since MGP-ALWTTT-BASS-BEND-1 the package has a shared post-build pitch bend
writer, `PitchBendWriter` (`SSoT_CONTRACTS.md` §11), used by the bass for true
legato. The melody composer is the anticipated second consumer: a slur or
legato phrase would use the same step-gesture surface, and the melody track is
already monophonic and single-channel, which is the writer's stated
precondition. **Nothing is implemented.** The seam is documented specifically so
a future batch does not re-derive it or write a second bend path.

**Documentation status: recorded** in
`runtime/SSoT_Composer_Melody_Track.md` §7.2, `CURRENT_STATE.md`
("Blocked / not implemented yet") and `coverage-matrix.md`.

## F-MEL-SLOTCOUNT-1 — open observation, no batch assigned

Matrix cells 327 and 378 (Locrian, both meters, `Prog_Min_Napolitana_bII`,
`tracks=M`) emit one melody note fewer after MGP-MEL-DEGRADE-1 (32 → 31 audited
notes). The verbose re-run proves the missing slot never reached `PickNext` —
the motif replay index did not advance across it — so it was cut at
`!slot.playNote`, which is archetype-determined and pitch-independent. **The
mechanism is therefore not explained.** No red, no audible impact, 2 cells of
714. Settling it would need the pre-fix verbose log of the same cell, which no
longer exists.

Deliberately NOT pinned by a test: pinning a symptom whose mechanism nobody can
justify freezes a number for the wrong reason.

---

## Documentation gap register

Checked 2026-09-05 against the governed docs as applied.

| Item | Recorded in governed docs? | Gap |
|---|---|---|
| MGP-METER-1 | **NO — nowhere** | A consumer-filed ask that blocks F-HARM-1 validation exists only in a local prompt file. See below. |
| F-SPELL-PROVENANCE-1 | Yes | — |
| MGP-MEL-CYCLE-1 | Yes, corrected 2026-09-05 | — |
| Melody slur seam | Yes | — |
| F-MEL-SLOTCOUNT-1 | Yes | — |
| `transposeScaleSteps` follow-up | Unknown | `planning/PENDING_DOC_DIFFS.md` is not mirrored to the consuming PK, so it could not be checked from there. It is reported stale: D-TON6=A shipped the work as `MotifTransposeMode.ScaleDegrees`, so the entry should be closed with a pointer rather than left open. |

**The METER-1 gap is the one that matters.** An ask filed by a consuming
project, with a reproducible symptom and a named downstream blocker, currently
lives in a `.txt` on one machine. Neither `CURRENT_STATE.md` nor any roadmap
mentions it. If that machine or that file is lost, the ask is lost, and the
next person to hit a short 6/8 melody starts the investigation from scratch.
The minimum fix is an entry under `CURRENT_STATE.md` → "Blocked / not
implemented yet" or a roadmap line in `planning/active/`; this file is a
backlog, not a substitute for either.


# Melody Composition Pipeline

## Overview

The melody system is built to generate playable, expressive melodic lines for a given song part.  
It balances three simultaneous goals:
1. Stay harmonically meaningful (respect the chord progression and the tonality/mode).
2. Sound like a human performer (phrasing, accents, space, call/response).
3. Be controllable / authorable per character or per instrument.

The pipeline is split into clear components, each with a single responsibility. This keeps the system
extensible (SOLID-friendly) and lets us tune phrasing separately from pitch logic, etc.

High-level flow for one song part:

1. **MelodyTrackComposer**
   - Gets or creates a chord progression for the part.
   - For each chord span, asks **PhrasePlanner** to create a phrase plan.
   - For each planned "slot" in the phrase, asks a chosen **IMelodyStrategy** for which pitch to play.
   - Applies velocity/accents and writes the MIDI.

2. **PhrasePlanner**
   - Decides *when* notes happen, how long they last, where the rests are, where accents are,
     and which beats are phrase endings or cadences.
   - Produces `PhraseSlot` objects. Each slot is "play a note here for X beats with Y accent"
     or "this moment is a rest".
   - Also manages phrase-level memory (call/response, contour).

3. **IMelodyStrategy** (e.g. NearestChordToneMelodyStrategy, ScaleFlowMelodyStrategy)
   - Chooses *which pitch* to play for each slot, given:
     - the current chord,
     - the current mode / tonality profile,
     - the instrument range,
     - the phrase context so far,
     - and the previous melodic note for continuity.

4. **MelodicLeadingConfig**
   - Stores the "personality" of the melodic voice:
     - preferred density, phrasing shape, rest probability,
     - dynamic ranges (velocities),
     - interval motion constraints,
     - chord-tone vs scale-tone preference, etc.

5. **TonalityProfileSO**
   - Describes modal color and harmonic gravity for the current part (e.g. Dorian, Mixolydian,
     Minor Pentatonic).
   - Identifies characteristic scale degrees, cadence behavior, and which chords/tones should be
     emphasized to "sound like that mode".
   - Used by the strategies to bias note choice.

Together, these pieces produce modal, phrase-aware melodies that breathe, leave silence,
emphasize characteristic notes, and resolve in musical ways.


---

## Components in Detail

### 1. MelodicLeadingConfig

**What it is:**  
A ScriptableObject that defines how a specific "player" behaves. Think of it like
the personality and technique of a virtual instrumentalist.

**Key groups of fields:**

- **Core / Harmony awareness**
  - `voicingPreset`: placeholder for future coordination with harmony voicing and register.
  - `noteSource` (ChordTonesOnly / PreferChordTonesAllowScale / ScaleOnly): controls the harmonic
    palette each strategy is allowed to use.

- **Motion / Melodic contour**
  - `maxStepSemitones`: how far the melody is allowed to jump in one move.
  - `chanceRepeatNote`: chance to literally repeat the same pitch again instead of moving.
  - `chancePassingNote`: not fully used yet, but intended to allow non-chord/approach tones.

- **Phrasing / Expression**
  - `minSlotsPerPhrase`, `maxSlotsPerPhrase`: how many expressive "moments" (attacks/silences/etc.)
    a phrase will typically have.
  - `burstPhraseChance`, `sustainPhraseChance`: probabilities for different phrase archetypes,
    like a fast burst then a long held note, or a pickup jab then a sustain.
  - `restProbabilityMidPhrase`: chance to leave air in the middle of the phrase instead of
    filling every slot with a note.

- **Burst Shape / Runs**
  - `burstSubdivisionBeats`: rhythmic subdivision for fast runs (ex. 0.25 for 16ths).
  - `burstNoteCountMin`, `burstNoteCountMax`: how many fast notes occur in a "burst" before a
    long sustain.

- **Dynamics / Velocity**
  - `normalVelMin` / `normalVelMax`
  - `accentVelMin` / `accentVelMax`
  - `phraseEndVelMin` / `phraseEndVelMax`
  These control how loud (MIDI velocity) a slot is played depending on whether it's an accent,
  a phrase-ender, or a normal interior note.

**Why it matters:**  
- This is how two different NPCs can *feel* different, even if they play the same chord progression.
- This is also how we author "aggressive funk horn", "breathy sax", "lazy blues guitar", etc.


### 2. PhrasePlanner

**What it is:**  
A class that converts a chord timespan into a musical *phrase plan*.

**Input:**  
- chord timing (start beat, duration in beats),
- time signature info (beatsPerBar),
- RNG for deterministic variation,
- MelodicLeadingConfig for style,
- rolling PhraseMemory that knows what the previous phrase did,
- TonalityProfileSO for high-level mood (e.g., "Mixolydian, cadence to b7, don't do Ionian V-I").

**Output:**  
- A `List<PhraseSlot>`.

Each `PhraseSlot` contains:
- `whenBeat`: where this slot starts (in beats, absolute timeline of the part).
- `durBeats`: how long it should last.
- `playNote`: whether we want a note here or a rest.
- `isAccent`: should this hit pop out dynamically?
- `isPhraseEnd`: should this slot act as a cadence / landing point / sustained "money note"?
- `phraseId`, `slotIndexInPhrase`, `totalSlotsInPhrase`: lets us group related slots into a phrase.
  This is also where call-and-response is tracked.
- `desiredContourDir`: hint for contour (+1 means "tend upward", -1 "tend downward").

Internally, `PhrasePlanner` supports multiple **phrase archetypes**, e.g.:
- **EvenFlow**: steady subdivision, possibly with rests in the middle and an accent on the first hit.
- **BurstThenHold**: several fast short notes (burst run) followed by one long sustained landing note.
- **SustainLeadIn**: a tiny pickup jab (sometimes preceded by silence), then one big sustained note.

Phrase archetypes are chosen stochastically each chord span using the probabilities in
`MelodicLeadingConfig` (burstChance, sustainChance, etc.).

PhrasePlanner also maintains a lightweight `PhraseMemory` between phrases:
- last phrase's ID,
- last phrase contour direction (+1 up / -1 down),
- the note we ended on (optional, filled later by the composer).
This allows simple call/response patterns: alternate contour direction between phrases,
bias future phrases to answer the previous one, etc.

**Summary:**  
PhrasePlanner answers the questions:
- *How many notes do we play this phrase?*
- *Where are the silences?*
- *Which moments get accents?*
- *Where do we "land" and hold?*
- *Should this phrase go up or down, relative to the last phrase?*

It does **not** decide the actual pitch.


### 3. MelodyTrackComposer

**What it is:**  
The top-level track composer for melodic lines. This is the class that actually produces the MIDI.

**Responsibilities:**
1. Get or build the chord progression for the part.
   - It will either fetch an authored progression (via `ctx.GetProgressionForPart`),
     or, if none exists, generate one procedurally (e.g. `ChordTrackComposer.BuildProceduralProgression`)
     and cache it in `ctx.SetProgressionForPart`.  
   - This guarantees every melodic instrument is synced to the same harmonic plan.

2. For each chord event in the progression:
   - Convert its startStep/lengthSteps into beat space.
   - Request phrase slots from `PhrasePlanner`:
     ```csharp
     var phraseSlots = _phrasePlanner.PlanPhraseSlotsForSpan(...);
     ```
   - For each `PhraseSlot`:
     - If `playNote == false`: this is a rest → do nothing (the melody breathes).
     - Otherwise:
       - Build a `PhraseState` struct to give context to the pitch strategy
         (phrase index, whether this is a strong beat, whether it's the end of the phrase,
         what contour is desired, etc.).
       - Ask the chosen `IMelodyStrategy` for a pitch (`PickNext(...)`).
       - Compute velocity using `ChooseVelocityForSlot(...)` based on
         `slot.isAccent` or `slot.isPhraseEnd` and the velocity ranges in `MelodicLeadingConfig`.
       - Write the note into a DryWetMIDI `PatternBuilder` at `slot.whenBeat` for `slot.durBeats`.

3. After all phrases are written:
   - Stamp program/bank changes so the instrument loads with the correct patch.
   - Force all MIDI events to the requested channel.
   - Return the final `MidiFile`.

4. Maintain shared memory with PhrasePlanner:
   - After finishing a phrase, MelodyTrackComposer updates memory such as
     `lastPhraseEndNote` and pushes it back into PhrasePlanner.
   - This lets the system build call-and-response contour over time.

**Summary:**  
MelodyTrackComposer answers:
- *When do we emit MIDI events?*
- *What velocity do they have?*
- *Which pitch did the strategy choose?*
- *How does this melodic track get bounced into a final MIDI file?*


### 4. IMelodyStrategy (pitch strategies)

**What it is:**  
This is the "melodic brain" interface. Each strategy is a style of pitch selection.

```csharp
public interface IMelodyStrategy {
    Note PickNext(
        NoteName[] chordPitchClasses,
        NoteName[] scalePitchClasses,
        Dictionary<NoteName, int> degreeLookup,
        Note lastMelody,
        MIDIInstrumentSO instrument,
        MelodicLeadingConfig cfg,
        System.Random rng,
        PhrasePlanner.PhraseState phrase,
        TonalityProfileSO profile
    );
}
```

The strategy receives:
- the harmony (`chordPitchClasses`, `scalePitchClasses`),
- the scale degree map (`degreeLookup`),
- last chosen melody note (`lastMelody`),
- instrument range (so it doesn't choose pitches outside),
- the melodic config (`cfg`),
- the phrase context (`phrase`),
- the tonality profile (`profile`).

It returns either a `Note` to play, or `null` to indicate "rest this slot".

Two built-in strategies:

#### NearestChordToneMelodyStrategy
- Deterministic / tight to harmony.
- Prefers chord tones if allowed by config.
- Tries to keep interval jumps within `cfg.maxStepSemitones`.
- Biases toward characteristic modal degrees (e.g. Dorian's natural 6,
  Mixolydian's b7), especially on strong beats and phrase endings.
- If it lands on the same pitch as last time, it uses `chanceRepeatNote`
  to decide whether to repeat or move.

Feels like: "lead guitar outlining chords" or "vocal line glued to harmony."

#### ScaleFlowMelodyStrategy
- More scalar / modal flow.
- Builds a candidate set from the union of chord tones + scale tones (depending on `cfg.noteSource`).
- Assigns each candidate a weight based on:
  - stepwise motion preference (small intervals get higher weight),
  - chord-tone priority,
  - characteristic modal tones,
  - tonic bias at cadence if the profile wants to "resolve home",
  - respecting `maxStepSemitones` and `chanceRepeatNote`.
- Chooses from that weighted pool stochastically using the provided RNG.

Feels like: "improv line wandering in the mode," more fluid and less locked to chord arpeggios.


### 5. TonalityProfileSO

**What it is:**  
A ScriptableObject describing what makes a tonality/mode sound like itself. For example:
- Dorian: highlight natural 6; i–IV7 vamp colors the mode.
- Mixolydian: highlight b7; I–bVII–IV loop.
- Aeolian: highlight b6, b7; avoid raised leading tone.
- Minor Pentatonic: focus on 1, b3, 4, 5, b7; bendy/blues phrasing; no leading tone pull.

Typical fields include:
- `characteristicDegrees`: which scale degrees should be emphasized (e.g. b7 for Mixolydian).
- `avoidDegrees`: which degrees shouldn't be leaned on (e.g. natural 7 in Mixolydian tonic context).
- `forceCadenceToTonic`: whether phrase endings / strong beats should bias the tonic.

Strategies use this to weight notes so they "sound" like Mixolydian vs Aeolian, etc.


---

## Data Flow (Step by Step)

For each part in the song, during melody generation:

1. **Chord progression retrieval / creation**
   - MelodyTrackComposer asks the orchestration context for the current part's chord progression.
   - If none exists, a procedural one is generated and stored so all tracks (melody, bass, etc.)
     share the same harmony.

2. **Per-chord phrase planning**
   - For each chord event (startStep, lengthSteps):
     - Convert its timing into beats.
     - Call `PhrasePlanner.PlanPhraseSlotsForSpan(...)`.
     - Get a list of `PhraseSlot`s, each describing a moment in the phrase:
       *when*, *duration*, *isAccent*, *rest or note*, *isPhraseEnd*, etc.

3. **Per-slot pitch selection**
   - For every PhraseSlot where `playNote == true`:
     - Build a `PhraseState` (phrase index, note index inside phrase,
       whether this is the last slot in the phrase, desired contour direction,
       etc.).
     - Call your chosen IMelodyStrategy’s `PickNext(...)`:
       - It uses chord tones, scale tones, modal rules, last note, contour hints,
         and instrument range to pick the next pitch.
     - If the strategy returns null, treat this slot like a rest anyway.

4. **Velocity / dynamics & MIDI emit**
   - MelodyTrackComposer maps slot role to velocity using `_cfg`:
     - Accent slot? Use `accentVelMin..accentVelMax`.
     - Phrase-ending slot? Use `phraseEndVelMin..phraseEndVelMax`.
     - Otherwise use `normalVelMin..normalVelMax`.
   - Composer writes the note into the MIDI PatternBuilder at the right
     absolute beat time for the right duration.

5. **Memory / call-and-response**
   - After finishing the phrase for that chord, MelodyTrackComposer updates
     PhraseMemory, including:
     - last contour direction,
     - last phrase's final note (landing pitch),
     - phrase ID (chord index).
   - That memory is pushed back to PhrasePlanner, so the next phrase can
     intentionally "answer" (e.g. alternate contour).

6. **Finalization**
   - After all phrases: emit bank select + program change, force channels, finalize MIDI.


---

## System Expressive Range

This section explains how much variety you can get out of this system *without writing new code*,
just by authoring config assets and picking strategies.

### 1. Phrasing shape and density

From `MelodicLeadingConfig`:
- `minSlotsPerPhrase` / `maxSlotsPerPhrase` control how many "events" a phrase tries to carve out
  of each chord span. Fewer slots → long sustained notes. More slots → busier melodies.

- `burstPhraseChance` and `sustainPhraseChance` influence which archetypes you see:
  - High `burstPhraseChance` → lots of rapid-fire bursts and then dramatic held notes.
    Think flashy licks followed by a "hero" sustain.
  - High `sustainPhraseChance` → more pickup + long single-tone phrases, great for vocals,
    lead guitar bends, sax wails, etc.
  - If both are low, you get more EvenFlow phrases, which feel rhythmic and steady, like
    consistent 8ths or 16ths with occasional rests.

- `restProbabilityMidPhrase` gives air.
  - Near 0.0 ⇒ extremely talkative / no silence.
  - Near 1.0 ⇒ lots of gaps / breathy phrasing / call-and-response built into every bar.

This alone lets you dial from "machine gun funk guitar" to "long wails with dramatic gaps".

### 2. Micro-rhythm / runs

- `burstSubdivisionBeats`, `burstNoteCountMin`, `burstNoteCountMax` define the nature of a "burst".
  - Short subdivision + high burstNoteCount ⇒ fast shreddy runs.
  - Longer subdivision + low burstNoteCount ⇒ more like pickups (grace notes)
    than full-on flurries.

This can simulate quick bluesy pickups or fusion-style licks.

### 3. Dynamics / feel

- Velocity ranges (`normalVel*`, `accentVel*`, `phraseEndVel*`) shape how "aggressive"
  or "soft" the line feels.
  - Narrow ranges / lower numbers ⇒ chill, legato, pad-like delivery.
  - Wider ranges / higher accents ⇒ punchy, syncopated, funk / slap / shouty vocal style.

Because accents and phrase-end landings are tagged in `PhraseSlot`, the melody can
naturally emphasize downbeats, cadences, and "money notes".

### 4. Harmonic bias (pitch palette)

- `noteSource` lets you force the melodic line to outline chord tones vs wander the scale:
  - `ChordTonesOnly` ⇒ arpeggio-ish / chord-outline, clear harmonic function.
  - `ScaleOnly` ⇒ modal scalar lines, less functional harmony, more "pad"/"folk" or "chant".
  - `PreferChordTonesAllowScale` ⇒ a blend. This is ideal for tonal/modal fusion (jazz, funk, etc.).

This makes the same phrase timing sound like jazz guitar, modal flute, chord-outline bassline,
or vocal-like melodic improvisation.

### 5. Interval behavior

- `maxStepSemitones` and `chanceRepeatNote` shape contour:
  - Small `maxStepSemitones` ⇒ very stepwise, singable lines.
  - Larger `maxStepSemitones` ⇒ dramatic leaps and jumps (e.g. prog vocals, guitar heroics).
  - High `chanceRepeatNote` ⇒ insistent, mantra-like phrasing (good for chanty vocals).
  - Low `chanceRepeatNote` ⇒ melodic lines constantly move, more "busy soloist".

### 6. Modal color

- The TonalityProfileSO controls which scale degrees are considered "characteristic" and whether
  the system tries to cadence onto tonic at strong beats or phrase endings.
- This is how the same rhythmic phrasing can feel Dorian (minor with natural 6),
  Mixolydian (major with b7), Aeolian (natural minor with b6/b7, no leading tone), etc.

In short:  
**The expressive range is huge.**  
By tweaking config assets and picking a melody strategy (NearestChordTone vs ScaleFlow),
you can move from:
- tight, chord-outline, accent-heavy funk guitars,
- to floating, modal, scalar woodwind solos,
- to bluesy bend-and-hold vocals with dramatic silences,
- all without touching the underlying code.


---

## How to Expand the System

This system was built specifically so you can add new behavior cleanly.

### 1. Adding a new pitch strategy (IMelodyStrategy)

**Goal:** Create a new melodic personality that decides *which* pitch to play per slot.

Steps:
1. Create a new class that implements `IMelodyStrategy`.
   ```csharp
   public class MyCoolStrategy : IMelodyStrategy {
       public Note PickNext(
           NoteName[] chordPitchClasses,
           NoteName[] scalePitchClasses,
           Dictionary<NoteName, int> degreeLookup,
           Note lastMelody,
           MIDIInstrumentSO instrument,
           MelodicLeadingConfig cfg,
           System.Random rng,
           PhrasePlanner.PhraseState phrase,
           TonalityProfileSO profile)
       {
           // 1. Build candidate note pool (see MelodyStrategyCommon.BuildPitchClassPool).
           // 2. Apply your own weighting / selection logic.
           // 3. Respect instrument range (use ExpandToInstrumentRange).
           // 4. Respect cfg.maxStepSemitones, cfg.chanceRepeatNote, etc.
           // 5. Optionally bias tonic or characteristic tones on phrase ends.
           // 6. Return null to rest.
       }
   }
   ```

2. Add an enum value in `MelodyStrategyId` for your new strategy so you can select it authoring-side.

3. In `MelodyTrackComposer`, choose your strategy when you construct the composer
   (or via dependency injection from a higher-level orchestrator).

**Tips / patterns:**
- Reuse helpers in `MelodyStrategyCommon`: that class already does a lot of heavy lifting:
  - building the pitch pool from chord vs scale,
  - expanding to instrument range,
  - weighting by modal characteristics,
  - weighting by tonic resolution on cadences,
  - avoiding huge leaps or repeated notes beyond limits.

- You can invent wild personalities, e.g.:
  - A strategy that always jumps by 5ths and 6ths to get heroic leaps.
  - A strategy that stalks one characteristic tone for tension, then dodges away.
  - A "blues/bent" strategy that treats b3 / b5 / b7 specially.

Because strategies don't do rhythm, you won't break phrasing by experimenting here.


### 2. Adding a new phrase archetype to PhrasePlanner

**Goal:** Introduce new rhythmic behaviors / shapes / accents / silence patterns.

Steps:
1. In `PhrasePlanner`, extend the `PhraseArchetype` enum:
   ```csharp
   private enum PhraseArchetype {
       EvenFlow,
       BurstThenHold,
       SustainLeadIn,
       OffBeatStabs // <--- your new archetype
   }
   ```

2. Add a branch in `ChooseArchetype(...)` to sometimes pick your new archetype. You can
   either:
   - Add a new probability field to `MelodicLeadingConfig` (e.g. `offBeatStabChance`),
     or
   - Piggyback on existing ranges for now.

3. Implement a new `BuildOffBeatStabs(...)` method:
   ```csharp
   private List<PhraseSlot> BuildOffBeatStabs(
       double startBeat,
       double spanBeats,
       int phraseId,
       int contourDir,
       System.Random rng)
   {
       var slots = new List<PhraseSlot>();

       // Example idea: 3 syncopated short notes, all accented, with rests before them.
       // You decide exact timings (e.g. push notes slightly off the grid by +/-0.1 beats
       // if you later allow microtiming).

       // Each new PhraseSlot must fill:
       //   whenBeat, durBeats, playNote,
       //   isAccent, isPhraseEnd,
       //   phraseId, slotIndexInPhrase, totalSlotsInPhrase,
       //   desiredContourDir

       return slots;
   }
   ```

4. Update `BuildSlotsForArchetype(...)` to route `PhraseArchetype.OffBeatStabs`
   to your new builder.

5. (Optional) Add new config fields to MelodicLeadingConfig (like
   `offBeatAccentVelMin/Max` or `syncopationChance`) so designers can tune it per character.

**Why this works cleanly:**  
PhrasePlanner owns *when and how long* notes happen, rests, accents, and phrase endings.  
It does not choose pitch. So you can invent new rhythm/phrasing archetypes without touching
the pitch strategies, and vice versa.


### 3. Adding new expressive rules to note velocity / accents

Velocity comes from `MelodyTrackComposer.ChooseVelocityForSlot(...)`, which currently does:
- accent slot → accent velocity range,
- phrase-end slot → phraseEnd velocity range,
- otherwise → normal range.

To extend:
- Add more flags to `PhraseSlot` like `isGhostNote`, `isPickup`, `isDownbeatAnchor`.
- Add corresponding velocity ranges to `MelodicLeadingConfig`.
- Update `ChooseVelocityForSlot(...)` to branch on those flags.

Example:
```csharp
if (slot.isGhostNote) {
    return RandomBetween(cfg.ghostVelMin, cfg.ghostVelMax);
}
```

This is how you'd get "ghosted" funk guitar scratches, or soft grace notes vs loud downbeats.


### 4. Using memory for call-and-response

Currently `PhrasePlanner` keeps a rolling `PhraseMemory` with:
- last phrase contour direction,
- last phrase ID,
- last phrase ending note (pushed back in by MelodyTrackComposer).

To expand call/response:
- Have PhrasePlanner look at `_memory.lastPhraseEndNote` and decide whether the *next* phrase
  should start near that note or deliberately contrast it.
- Expose new config knobs in MelodicLeadingConfig like
  `preferAnswerContour` or `contourFlipStrength`.
- Feed hints into each `PhraseSlot`’s `desiredContourDir`, and into `PhraseState`,
  so strategies can emphasize "go up" or "go down" answers when picking actual pitches.

This makes the melody feel conversational: one phrase climbs, the next falls in reply.


---

## In Summary

- **MelodicLeadingConfig** defines how a given voice behaves: density, silence,
  contour, dynamics, and harmonic preference.

- **PhrasePlanner** turns harmony spans into phrase-level timing: where notes start,
  how long they last, which are accents, where to breathe, and whether the phrase
  should climb or fall. It also manages cross-phrase memory for call/response.

- **MelodyTrackComposer** is the orchestrator that:
  - walks the chord progression,
  - asks for phrase slots,
  - delegates pitch to an IMelodyStrategy,
  - assigns velocity, and
  - writes the final MIDI.

- **IMelodyStrategy** picks the pitch for each slot based on chord, scale,
  mode, phrase context, and constraints like max interval size. Different strategies
  feel like different "players."

- **TonalityProfileSO** injects modal identity (Dorian, Mixolydian, Pentatonic, etc.):
  which tones to highlight, which to avoid, and how strongly to resolve to tonic.

### Why this architecture is powerful
- You can author a new "instrument personality" just by making a new `MelodicLeadingConfig`
  asset (no code).
- You can invent a new riff/phrase archetype by adding one method in `PhrasePlanner`.
- You can create a new melodic style by adding a new IMelodyStrategy without touching phrasing.
- You can shift the entire harmonic vibe (Dorian vs Mixolydian vs Pentatonic) by swapping
  the TonalityProfileSO.

This separation of phrasing vs pitch is what lets melodies feel alive, modal,
dynamic, and character-specific — instead of gridlocked, robotic, and identical.

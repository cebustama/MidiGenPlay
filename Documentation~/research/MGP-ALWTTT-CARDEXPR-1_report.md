# MGP-ALWTTT-CARDEXPR-1 — Verificación de capacidades y requisitos de authoring para la campaña de cartas de ALWTTT

**Modo:** REFERENCE / CONSUMER-SUPPORT · **Fecha:** 2026-08-26 · **Estado:** informe de verificación; **no implementa nada** y **no modifica ningún documento gobernado**.
**Frontera:** el paquete posee sus internals (`SSoT_Composer_*`, `SSoT_CONTRACTS`); ALWTTT posee coste, condición, personaje y momento de juego. Todo hueco vuelve como **ask numerado** (§4), nunca como cambio de contrato.
**Destino propuesto en el paquete:** `Documentation~/reference/cross-project/ALWTTT/Handoff_MGP_CARDEXPR_1.md` (clase *reference*, sin `governs:`; confirmar contra el árbol real). Copia consumer-side junto a `Design_Composition_Variations_v0_1.md`.

## Leyenda

| Clase | Significado |
|---|---|
| **(a) EXISTE** | configurable hoy; ejemplo mínimo de authoring adjunto |
| **(b) EXISTE CON MATIZ** | funciona con degradación o límite documentado |
| **(c) NO EXISTE** | ask numerado con criterio de aceptación (§4) |
| **doc / code** | fuente de la afirmación: documento gobernado (doc-truth) o fichero (code-truth). Cuando divergen se dice explícitamente. |

---

## 1. Respuestas a §7 del inventario

### Q1 — Tier-2 (`PowerChord` / `Chugging`) en Bassline; camino para un gallop metal de bajo

**Clasificación: (b) EXISTE CON MATIZ** (y el gallop propiamente dicho se resuelve por otra vía, también (b)).

**Code-truth.**
- `BassTrackComposer.ResolveArticulation` (`BassTrackComposer.cs` ~L1921) devuelve `chordExpression` tal cual; no filtra Tier-2.
- El compositor de bajo **nunca invoca `IChordReshaper`** (no hay referencia a `Reshape` en `BassTrackComposer.cs`), así que el reshape root+5ª+8ª de `ChordReshaper.cs` no llega al bajo.
- `ChordArticulator.PlanCore` (`ChordArticulator.cs` ~L205-213): `PowerChord` y `Random` **degradan a `Block`**; `Chugging` va a `ChordPulsePlan` (~L360-380), que **no comprueba `noteCount`** y re-golpea el playable a `arpeggioRate` con gate `min(interval, resto)`.
- Consecuencia: en el bajo `PowerChord` ≡ `Block` (nota sostenida) y `Chugging` ≡ pulso de raíz repetida a `arpeggioRate`, **indistinguible de `ArpeggioUp` en `RepeatedNote`** (mismo grid `ArpeggioIntervalBeats`, misma curva `CurvedVelocity`, gate legato). No hay gate corto de palm-mute.

**Doc-truth.** `SSoT_Composer_Bass_Track.md` §3.3 documenta la degradación a `Block` sólo para `BassUpperSplit`/`Bossa` (regla ≤1 nota); **no dice nada de `PowerChord`/`Chugging` en el bajo**. `SSoT_Composer_Backing_Track.md` §8.6 describe ambos sólo para el backing. → **Hueco documental** (ver §5, D-1).

**Precedente confirmado:** `Bossa` en carta de bajo → `Block` (§3.3, "by the articulator's own ≤1-note rule").

**El gallop no existe como figura** (♪♬ = corchea + dos semicorcheas). Ningún miembro de `ChordExpressionType` lo produce. **Camino recomendado, sin ask:** `pocketMode = SelfPocket` + `selfPocketSubdivision = QuarterBeat` + patrón por pulso `S . g g` (o `S . S S`), usando la clase `Ghost` como palm-mute: la doctrina de §3.7.3 fija que *Mute* **es** un ghost (velocity mínima + gate corto), y tanto `ghostVelocityFactor` como `ghostGateBeats` son campos de carta (D-SF2B-TUNE=A), de modo que una carta "chug" puede subir el factor a ~0.85 y fijar el gate en ~0.12 beat sin tocar el compositor. Ejemplo en §3, carta *Iron Gallop*.

Alternativa más pobre: `chordExpression = Chugging` (o `ArpeggioUp`) + `arpeggioRate = Sixteenth` → chug uniforme de semicorcheas en la raíz, gate legato (no palm-mute), sin gallop.

### Q2 — Octavas fingered (pulso raíz/octava, "Something About Us")

**Clasificación: (b) EXISTE CON MATIZ.**

- **No** por `BassUpperSplit`: degrada a `Block` en el bajo (§3.3). **No** por `PerBeat`+registro: `PerBeat` es pulso de la nota seleccionada, sin salto de octava.
- **Sí** por `SelfPocket`: `Pop` = nota seleccionada **+12** (`ResolvePopNote`, D-PKT-POP-PITCH=A) y `Slap` = nota seleccionada; el **timbre es del patch** ("this mode shapes timing, register and dynamics only", §3.7 / §3.7.2), así que con GM *Finger Bass* en el `MIDIInstrumentSO` el resultado es un pulso de octavas fingered. Patrón `S P` en `HalfBeat` = corcheas raíz/octava.
- **Matices documentados:** (1) la octava se **pliega sobre la nota seleccionada** si supera el techo `octaveMax*12+11` del instrumento (D-REG-2=B, §3.7.1 "Register"); autorar el `MIDIInstrumentSO` con `octaveMax ≥ octaveMin+2` evita el pliegue; (2) el gate máximo es `PocketMaxGateBeats = 0.5` (`BassTrackComposer.cs` L968) → a `Beat` (negras) cada nota suena medio pulso y hay hueco; el pulso "limpio y legato" sólo existe a `HalfBeat`/`QuarterBeat`; (3) dinámica por `pocketSlapBoost`/`pocketPopBoost` (aditivos, §3.7.1).

Ejemplo en §3, carta *Octave Drive*.

### Q3 — Comping sincopado (funk de semicorcheas / clave Stevie Wonder) en Backing

**Clasificación: (c) NO EXISTE** como figura seleccionable de carta. → **Ask CARDEXPR-A1** (§4).

- Tier-1 (`SSoT_Composer_Backing_Track.md` §8.2) sólo tiene grids regulares (`PerBeat`, `Staccato`, `Offbeat` = beat+0.5, arpegios a rate fijo); Tier-2 (§8.6) añade `PowerChord`, `Chugging` (pulso uniforme), `BassUpperSplit` (alternancia regular) y `Bossa` (plantilla fija de 1 compás). Ninguna es autorable ni sincopada más allá de `Offbeat`/`Bossa`.
- **Vía de aproximación (degradación aceptable, pero NO como mecanismo de carta):** la progresión sí soporta ritmo armónico sub-pulso y silencios — `ChordProgressionData.subdivisions` + `ChordEvent.startStep/lengthSteps` (`ChordProgressionData.cs` L15-21), el parser Roman admite duraciones fraccionarias en compases y silencios `R`/`REST`/`(0.5)` (`RomanProgressionParser.cs` L144-170), y el importador MIDI tiene `preserveReStrikes` con "the runtime reproduces rests faithfully" (`SSoT_Authoring_Chord_Progressions.md` §3). Autorar la progresión con `subdivisions = 4` y eventos re-golpeados + silencios produce el comping como sucesión de `Block`. **Pero** la progresión es armonía **compartida** (`SSoT_Runtime_Generation_Orchestration` / ORDER-1): el bajo itera `prog.events` con un draw por evento (`SSoT_Composer_Bass_Track.md` §2) y re-golpea igual; la melodía planifica frases por span de acorde (§4). El comping del backing se convertiría en ritmo de toda la banda. Válido como decisión de *parte*, no como carta de un personaje.

### Q4 — Densidad de ghosts "in between" en SelfPocket

**Clasificación: (a) EXISTE.**

- Alfabeto §3.7.3: `Slap · Pop · Rest · Ghost · GhostPop · HammerOn · PullOff`; `QuarterBeat` existe precisamente para el idioma de semicorcheas ("the two canonical figures … are inexpressible on Beat or HalfBeat", §3.7.2). La densidad de adorno es **contenido del patrón**: cada `g`/`G` es un step. Factores y gate por clase son campos de carta.
- Además (código presente, doc pendiente — ver Q7 delta): **MGP-ALWTTT-BASS-PHRASE-1** añade `selfPocketPhraseLengthBars`, `selfPocketBarSubstitutions[barIndex → variants[]]`, `selfPocketVariantSelection {SeededMix, RoundRobin}` (`BasslineCardConfigSO.cs` L322-358; `BassTrackComposer.cs` L350-369, `PhraseMix01` L1528): el último compás de la frase puede llevar un run de ghosts distinto, con selección determinista por `ResolvePhraseSeed(trackSeed)`.
- Límite (no bloquea la carta): todas las clases sonoras son leyes sobre la **nota seleccionada** del evento (D-SF2-PITCH=A) — un ghost sobre otra chord-tone no es expresable.

Ejemplo en §3, carta *Ghost Funk* (usa el body de `Bass_Phrase_Aeroplane4` de `PhrasePresets_Bass_Spec.md` §1).

### Q5 — `PitchBendWriter` en Melody (approach notes / scoops de lead y voz)

**Clasificación: (c) NO EXISTE.** → **Ask CARDEXPR-A2** (§4).

- Doc: `SSoT_Composer_Melody_Track.md` §7 "Pitch bend seam (available, NOT consumed) … Nothing is implemented here today". `SSoT_CONTRACTS.md` §11 declara bass **y melody** consumidores previstos; backing es NO-consumidor (canal polifónico).
- Code: `MelodyTrackComposer.cs` no referencia `PitchBendWriter`. El writer ya expone la API completa: `StepGesture(bendTick, targetSemitones, resetTick)`, `ApplyStepGestures(file, gestures, rangeSemitones)` (`PitchBendWriter.cs` L82-125), rango GM ±2 asumido, sin RPN.
- **Coste estimado de adopción (para que ALWTTT dimensione el ask, no compromiso del paquete):** (1) una decisión de superficie — dónde vive "esta nota es slur/scoop" (campo de `PhraseArchetypeSO`, directiva de `MelodicStyleSO`, o clase en `MelodyPatternData`); (2) mapa de carrier + extensión de gate (idioma `BuildLegatoCarrierMap` / `ResolveLegatoGroupEndBeats` del bajo, hoy privados de `BassTrackComposer`, candidatos a seam compartida); (3) call site post-build antes de `ForceAllChannel`/`StampBankAndPatch`; (4) cero draws nuevos de `ctx.rng` + canary de byte-identidad. Para la **voz** (Pink Trombone, consumer-side) el paquete sólo garantiza que el bend está en el fichero; que el singer lo honre es decisión ALWTTT.

### Q6 — Campos de feel de Rhythm (`kickDensity`, `snareGhostNoteChance`, `hatSubdivisionBias`, `fillEveryNMeasures`, `lastMeasuresAsFill`)

**Clasificación: (c) NO EXISTE** (inertes en composición). → **Ask CARDEXPR-A3**, recomendado **diferir** (§4).

- Code: `RhythmTrackComposer.cs` lee esos cinco campos **únicamente** en la traza `logGenerator` (L827-828); ninguna ruta de composición los consume.
- Doc: `SSoT_Composer_Rhythm_Track.md` §6 "Present in the input surface but not yet fully closed semantically … should not be documented as fully honored". Coincide con el gap §8.5 del expressive-surface.
- `recipeOverride` **sí** se consume (`RhythmTrackComposer.cs` L129-181), pero sólo en la ruta **procedural** (sin `DrumPatternData`), y esa ruta **no publica onsets** (§3bis "the procedural and legacy paths publish NOTHING in v1") → una carta que dependa de recipe no puede alimentar un pocket.
- Recomendación: las cartas de C2 se apoyan en `patternOverride` / `patternPalette` (ruta GRID, publica onsets, PERC-FALLBACK-1) — todo (a).

### Q7 — Vigencia del snapshot 2026-08-10

Ver tabla completa en §2. Resumen: **un batch posterior (MGP-TONALITY-1, 2026-08-11, código aplicado / docs no aplicadas)** y **dos batches anteriores por fecha pero ausentes del inventario y de `CURRENT_STATE`** (BASS-PHRASE-1 2026-08-05, BASSCARD-WIZARD-1 2026-08-07; código y tests presentes, docs drafted-not-applied y **no registrados** en `PENDING_DOC_DIFFS.md`, que se declara EMPTY).

### Q8 — Ejemplo mínimo de payload/SO por capacidad usada en §5

Ver §3 (uno por carta candidata).

---

## 2. Vigencia del snapshot 2026-08-10 — filas [C-MGP] de §3 del inventario

| Fila del inventario | Veredicto | Delta / corrección | Fuente |
|---|---|---|---|
| 3.1 SlapPocket | **CONFIRMADA** | sin cambios. Hash duty y orden Rhythm→Bassline siguen siendo deberes del consumidor (`SSoT_CONTRACTS` §10 "recorded exception") | Bass §3.7/§3.7.1; CONTRACTS §10 |
| 3.1 SelfPocket + vocabulario | **CONFIRMADA + AMPLIADA** | falta en el inventario la superficie de **frase** (PHRASE-1): `selfPocketPhraseLengthBars`, `selfPocketBarSubstitutions`, `selfPocketVariantSelection`. Código presente; SSoT §3.7.4 **pendiente de aplicar** | `BasslineCardConfigSO.cs` L322-358; `MGP-ALWTTT-BASS-PHRASE-1_doc_diffs.md` |
| 3.1 Legato pitch bend (BEND-1) | **CONFIRMADA** | sin cambios | Bass §3.7.3; CONTRACTS §11 |
| 3.1 ImprovisedWalk | **CONFIRMADA, con delta** | MGP-TONALITY-1 (D-TON10): la raíz y el approach target ahora aplican `degreeAccidental` (`BassTrackComposer.cs` L505-507, L742). La "accidental-blindness on record" de §3.6bis (D-W2-LAST) ya no es cierta en código; el SSoT aún la afirma → **doc-truth desactualizada** | `MGP-TONALITY-1_doc_diffs.md` §2.1 |
| 3.1 Timbre slap (patch GM) | n/a consumer | — | — |
| 3.2 DrumPatternData DSL, 8 géneros | **CONFIRMADA** | sin cambios. Nota nueva (TONALITY-1 §4.1, doc pendiente): en compases compuestos un "beat" del grid es una corchea (`GetBeatSpan`) — autorar acentos sobre el pulso sentido | `genre_vocabulary.md` índice; Rhythm §5 |
| 3.2 Recipe procedural | **CONFIRMADA CON MATIZ** | sólo ruta procedural; esa ruta **no publica onsets** (no alimenta pocket) | Rhythm §3A, §3bis |
| 3.2 Campos de feel | **era [V] → (c)** | inertes en composición (Q6) | `RhythmTrackComposer.cs` L827 |
| 3.2 Canal de onsets | **CONFIRMADA** | sólo ruta GRID; filtro de audibilidad PERC-FALLBACK-1 | Rhythm §3bis |
| 3.3 Progresión / palette | **CONFIRMADA** | añadido pre-snapshot no listado: `BackingCardConfigSO.adoptProgressionTonality` (MEL-1b P4, verificado en vivo 2026-08-08) | Backing §2.3 |
| 3.3 Tier-1 | **CONFIRMADA** | `Random = 6` es sentinel de selección, no figura | `ChordExpressionType.cs` L27-62 |
| 3.3 Tier-2 PowerChord(7)/Chug(8) | **CONFIRMADA, nombre corregido** | el miembro es **`Chugging = 8`**, no `Chug`. En el **bajo**: `PowerChord`→`Block`, `Chugging`→pulso de raíz (Q1) | `ChordExpressionType.cs` L73-82 |
| 3.3 BassUpperSplit(9) / Bossa(10) | **CONFIRMADA** | ambas degradan a `Block` en cartas de bajo | Backing §8.6; Bass §3.3 |
| 3.3 Voice leading override | **CONFIRMADA** | — | `BackingCardConfigSO.cs` L12 |
| 3.3 Modulación direccional | **CONFIRMADA** | — | Backing §6; `ModulationEffect.cs` |
| 3.4 MelodicStyleSO | **CONFIRMADA, con delta** | TONALITY-1: `RepeatLastNotesDirective.transposeMode {ChromaticSemitones, ScaleDegrees}` (`MelodicStyleSO.cs` L161-172) — resuelve el hazard "transpose CHROMATIC and accumulates" del inventario/R3. Doc pendiente | `MGP-TONALITY-1_doc_diffs.md` §1.1 |
| 3.4 PhrasePaletteSO + arquetipos | **CONFIRMADA, con delta** | TONALITY-1: `PhraseArchetypeSO.endRestFraction`, `meterFitSlots`, `allowTupletSubdivisions` (`PhraseArchetypeSO.cs` L18-31) y `BurstThenHoldPhraseSO.restProbMid` (L24). Defaults = comportamiento legacy. **Nota RNG:** `restProbMid > 0` desplaza el stream de ese arquetipo (deliberado). Doc pendiente | íd. §1.3 |
| 3.4 Patrones verbatim (`patternOverride`) | **CONFIRMADA** | ruta de patrón ahora accidental-aware (TONALITY-1 §1.2, `MelodyTrackComposer.cs` L339/L915) | íd. |
| 3.4 Harmony Tier A | **CONFIRMADA** | `NearestDifferentChordToneHarmonyStrategy` (`MidiGenerator.cs` L43); `HarmonyCardConfigSO { leadingOverride, strategyIdOverride }` | `HarmonyCardConfigSO.cs` |
| 3.4 Pitch bend en Melody | **CONFIRMADA como no consumido** | Q5 | Melody §7 |
| 3.4 Solo de un loop (R5-d) | ALWTTT-side | fuera de alcance del paquete | — |
| 3.5 (voz) | consumer-side | fuera del PK; sin verificación aquí | — |

**Delta post-2026-08-10 enumerado (paquete):**

1. **MGP-TONALITY-1 (2026-08-11).** Código aplicado y verificado en Unity (`TonalityAudit.cs` nuevo; `MidiGenPlayConfig.enableTonalityAudit` / `tonalityAuditShowInfo`; D-TON10 accidental-awareness en bass/melody/orchestrator — **único cambio que altera renders existentes**, sólo en progresiones con accidentales; 4 campos nuevos de arquetipo; `transposeMode`). Diffs de doc en `MGP-TONALITY-1_doc_diffs.md`, **NO aplicados y NO registrados** en `PENDING_DOC_DIFFS.md`. `CURRENT_STATE.md` no lo menciona.
2. **MGP-ALWTTT-BASS-PHRASE-1 (2026-08-05)** y **MGP-BASSCARD-WIZARD-1 (2026-08-07)** — anteriores al snapshot por fecha, pero ausentes del inventario y de `CURRENT_STATE.md`; docs drafted-not-applied, no registradas en el acumulador (que se declara EMPTY tras DOC-SWEEP-2). Código: `BasslineCardEditorWindow.cs`, `BassPatternTextParser.cs` (alfabeto `S P . - g G H L |`), `BassTrackComposer_PhraseTests.cs`, `BassPatternTextParserTests.cs`.
3. **Sin cambios** en: `SSoT_CONTRACTS` §10/§11, Backing §8.x, Rhythm §3bis, superficie de `BackingCardConfigSO`/`RhythmCardConfigSO`, `ChordExpressionType`.

---

## 3. Card Authoring Requirements

> **Nota de esquema.** El "esquema en §2 de este documento" citado por la tarea (`Prompt_MGP_CardExpressivity_Companion_v0_1.md` §2) **no está en el PK**. Se usa el esquema mínimo siguiente; reordenar a la plantilla ALWTTT es mecánico. Referencias musicales: sólo brújula; ningún nombre real en assets.

Esquema por carta: **Capacidad · Clase · SO / campos (valores) · Instrumento · Límites y degradaciones · Deberes consumer-side · Fuente.**

Convención de patrón: alfabeto del wizard `S`=Slap `P`=Pop `.`=Rest `g`=Ghost `G`=GhostPop `H`=HammerOn `L`=PullOff `|`=separador ornamental (`BassPatternTextParser.cs` L56-64); en inspector, la lista `selfPocketPattern` equivalente.

### 3.1 Slap Groove (Conito · Bass · Voltage 3)
- **Capacidad:** SelfPocket simple · **(a)**
- **`BasslineCardConfigSO`:** `pocketMode = SelfPocket`; `selfPocketSubdivision = HalfBeat`; `selfPocketPattern = S . P . S P . P` (8 steps = 1 compás 4/4); `pocketSlapBoost = 0`, `pocketPopBoost = +8`; resto por defecto.
- **Instrumento:** `MIDIInstrumentSO` GM *Slap Bass 1/2*; `octaveMax ≥ octaveMin+2`.
- **Límites:** patrón anclado al compás (D-SFIG-PAT=A); pop plegado si supera el techo (D-REG-2=B); gate ≤0.5 beat.
- **Consumer:** ninguno cross-track (cero lecturas del Rhythm → no despierta hash duty).
- **Fuente:** Bass §3.7.2.

### 3.2 Super Slap (Conito · Bass · Voltage 6)
- **Capacidad:** SelfPocket denso + legato + frase · **(a)** (frase: código presente, doc pendiente)
- **`BasslineCardConfigSO`:** `pocketMode = SelfPocket`; `selfPocketSubdivision = QuarterBeat`; body `S . . g P . g . S . g . P . g .`; `selfPocketPhraseLengthBars = 4`; `selfPocketVariantSelection = SeededMix`; `selfPocketBarSubstitutions = [{barIndex 3, variants: [S . g g P . g g S g g g P . g ., S . . g P . H . S . H . P L . .]}]`; `pocketPopBoost = +10`; `hammerOffsetDegrees = +1`, `pullOffsetDegrees = -1`; factores por defecto (0.60 / 0.50 / 0.60 / 0.55 / gate 0.10).
- **Instrumento:** GM *Slap Bass 1/2*.
- **Límites:** `H`/`L` deben ir precedidos de un step sonoro **dentro de la misma ventana de acorde** (si no: nota atacada + 1 warning/render); cadena >±2 st clampa; bend hereda la velocity del carrier (sin dinámica propia); rango GM asumido, sin RPN.
- **Consumer:** hash de la carta debe incluir la tabla de sustituciones (es contenido de carta; ya cubierto si el hash serializa el SO completo).
- **Fuente:** Bass §3.7.3; `PhrasePresets_Bass_Spec.md` §1; CONTRACTS §11.

### 3.3 In the Pocket (Conito · Bass · Voltage ?)
- **Capacidad:** SlapPocket (sigue a la batería) · **(a)** en el paquete, **bloqueada consumer-side** por las dos deudas de §6 del inventario.
- **`BasslineCardConfigSO`:** `pocketMode = SlapPocket`; `pocketPopBoost = +8`; opcional `pocketCustomLanes = true`, `pocketPopLanes = [AcousticSnare, ElectricSnare, SideStick]` (caso latino).
- **Instrumento:** GM *Slap Bass*.
- **Límites:** sin fuente publicada (sin Rhythm, Rhythm después del bajo, ruta procedural/legacy) → figura desacoplada, ≤1 warning, **byte-idéntico a Off**; ventana sin kick/snare → figura normal; pitches estables por clase, octavas no garantizadas al re-renderizar (caveat registrado).
- **Consumer (obligatorios, confirmados por el paquete como deberes del consumidor):** (1) **hash duty** — identidad del patrón de batería resuelto entra en el hash del track de bajo cuando `pocketMode != Off` (Bass §3.7 "Hash duty (consumer invariant)"); (2) **orden** Rhythm antes de Bassline en `Part.Tracks` o re-render al añadir batería (CONTRACTS §10 "Recorded exception"). El paquete **no** promoverá el pocket a pass del orquestador salvo ask explícito (ver A4, opcional).
- **Fuente:** Bass §3.7, §3.7.1; Rhythm §3bis; CONTRACTS §10.

### 3.4 Walkin' (Conito · Bass · Insp.)
- **Capacidad:** ImprovisedWalk · **(a)**
- **`BasslineCardConfigSO`:** `chordExpression = ArpeggioUp` (o `ArpeggioDown` para sesgo descendente); `arpeggioRate = PerBeat` (negras) o `Eighth`; `arpeggioToneMode = ImprovisedWalk`; `velocityJitter = 6`; `pocketMode = Off`.
- **Instrumento:** GM *Acoustic Bass*; `octaveMax` holgado (pliegue −12 por techo, D-W2-REG).
- **Límites:** activa sólo con ≥2 pitch-classes en el acorde y `ArpeggioFits`; si no, pulso `RepeatedNote`. Approach notes **cromáticas por diseño** (el `TonalityAudit` las marcará OUT-OF-KEY — esperado). Post-TONALITY-1 el target de approach respeta `degreeAccidental`.
- **Consumer:** ninguno.
- **Fuente:** Bass §3.6bis; `MGP-TONALITY-1_doc_diffs.md` §2.2.

### 3.5 Octave Drive (Conito · Bass · Insp.)
- **Capacidad:** pulso de octavas fingered · **(b)** (Q2)
- **`BasslineCardConfigSO`:** `pocketMode = SelfPocket`; `selfPocketSubdivision = HalfBeat`; `selfPocketPattern = S P`; `pocketSlapBoost = 0`, `pocketPopBoost = -6` (la octava suele leerse más fuerte en fingered).
- **Instrumento:** GM *Finger Bass* (el timbre decide que "slap/pop" suene fingered); **`octaveMax ≥ octaveMin+2`** obligatorio para que la octava no se pliegue.
- **Límites:** gate máximo 0.5 beat (legato sólo a `HalfBeat`/`QuarterBeat`); pop plegado si el techo no cabe.
- **Consumer:** ninguno.
- **Fuente:** Bass §3.7.1 (Register), §3.7.2.

### 3.6 Ghost Funk (Conito · Bass · Insp./Volt.)
- **Capacidad:** fingerstyle + ghosts intercalados · **(a)** (Q4)
- **`BasslineCardConfigSO`:** `pocketMode = SelfPocket`; `selfPocketSubdivision = QuarterBeat`; `selfPocketPattern = S . g g S . g . S g . g S . g g`; `ghostVelocityFactor = 0.55`; `ghostGateBeats = 0.10`; `pocketPopBoost` irrelevante (sin `P`).
- **Instrumento:** GM *Finger Bass* o *Fretless*.
- **Límites:** ghosts siempre sobre la nota seleccionada (no otra chord-tone).
- **Consumer:** ninguno.
- **Fuente:** Bass §3.7.3.

### 3.7 Iron Gallop (Conito · Bass · Voltage ?)
- **Capacidad:** gallop/chug grave · **(b)** (Q1) — **sin ask**
- **`BasslineCardConfigSO`:** `pocketMode = SelfPocket`; `selfPocketSubdivision = QuarterBeat`; `selfPocketPattern = S . g g` (4 steps, cicla por pulso; gallop ♪♬); `ghostVelocityFactor = 0.85`; `ghostGateBeats = 0.12`; `pocketSlapBoost = +6`. Variante chug uniforme: `S g g g` o `chordExpression = Chugging`, `arpeggioRate = Sixteenth` con `pocketMode = Off`.
- **Instrumento:** GM *Electric Bass (pick)*.
- **Límites:** `Chugging`/`PowerChord` no reducen ni engrosan la voz en el bajo (sin reshaper); `PowerChord` ≡ `Block`. Gate de `S` = min(gap, 0.5) — para sensación mute usar `g`.
- **Consumer:** ninguno.
- **Fuente:** `ChordArticulator.cs` L205-213, L360-380; Bass §3.3, §3.7.3.

### 3.8 Upstroke (C2/any · Backing · Insp.)
- **Capacidad:** `Offbeat` (ska/reggae) · **(a)**
- **`BackingCardConfigSO`:** `chordExpression = Offbeat`; `velocityJitter = 4`; `voiceLeadingOverride` opcional.
- **Instrumento:** guitarra/teclado de comping.
- **Límites:** evento sin ningún beat+0.5 dentro → `Block` (never-silent).
- **Fuente:** Backing §8.2.

### 3.9 Palm Wall (any · Backing · Insp.)
- **Capacidad:** `Chugging` · **(a)**
- **`BackingCardConfigSO`:** `chordExpression = Chugging`; `arpeggioRate = Eighth` (o `Sixteenth`); `velocityJitter = 6`.
- **Instrumento:** GM *Distortion/Overdriven Guitar*.
- **Límites:** reshape a root+5ª+8ª anclado bajo el bajo del voicing (D-T2-PIN=A: un pin de inversión sobre la 3ª es no-op); eventos < 1 hit → `Block`; excluido del pool `Random` (D-T2-POOL=A′); gate legato entre golpes (no palm-mute corto).
- **Fuente:** Backing §7.5, §8.6.

### 3.10 Bossa Comping (Conito? · Backing · Insp.)
- **Capacidad:** `Bossa = 10` (plantilla auténtica 1 compás) · **(a)** en **Backing**; en carta de **bajo** degrada a `Block` → asignar a un personaje de backing, no a Conito.
- **`BackingCardConfigSO`:** `chordExpression = Bossa`; `arpeggioRate` ignorado.
- **Límites:** degrada a `Block` con voicing ≤1 nota o `beatsPerBar ≤ 0`; excluido del pool Random.
- **Relación con *Bossa Corda* (R8):** decisión ALWTTT; el paquete ofrece una sola capacidad.
- **Fuente:** `ChordExpressionType.cs` L110-140; Backing §8.6; Bass §3.3.

### 3.11 Wonder Groove (any · Backing · Insp.)
- **Capacidad:** comping sincopado · **(c)** → **Ask A1**. Degradación aceptable provisional: `Offbeat` o `Bossa`.

### 3.12 Shout! / Falsetto / Croon (Zig · Voz)
- Fuera del PK (Pink Trombone es consumer-side; `SSoT_Singer_Voice.md` y `PinkTrombone_Voice_Levers.md` no son autoridad del paquete). Sin verificación aquí. Único punto de contacto con el paquete: si la voz quiere approach/scoop por bend, depende de **A2**.

---

## 4. Asks para el ledger §8 de `RosterExpansion_Sub_Roadmap`

Numeración provisional `CARDEXPR-A#`; ALWTTT asigna el número de ledger.

### CARDEXPR-A1 — Figura de comping autorable para Backing (patrón de golpes por carta)
- **Título:** `BackingCardConfigSO` — figura `Comping` con patrón de strikes autorado (grid `Beat/HalfBeat/QuarterBeat`, alfabeto strike/rest/accent), anclado al compás, análogo al `selfPocketPattern` del bajo pero pitch-preserving.
- **Motivación de carta:** *Wonder Groove* (clave funk de semicorcheas) y cualquier comping sincopado que no sea `Offbeat`/`Bossa`. Hoy sólo existen grids regulares y una plantilla fija.
- **Criterio de aceptación:** (1) nuevo miembro append-only de `ChordExpressionType` (o campo aparte si se decide no tocar el enum) + lista de steps en `BackingCardConfigSO`; (2) hits en exactamente las posiciones del patrón dentro de cada ventana de evento, anclados a part beat 0, chord change siempre audible en el onset; (3) gate `min(gap, resto de ventana, techo por clase)`; (4) patrón vacío/todo-rest → `Block` con 1 warning (never-silent); (5) excluido del pool `Random` y no admisible en `randomFigureWeights`; (6) artículador sigue RNG-free y pitch-preserving; carta sin patrón → byte-idéntico; (7) pins EditMode + smoke.
- **Degradación aceptable:** mientras no exista, `Offbeat` (ska) o `Bossa`; **no** se acepta autorar la síncopa en la progresión compartida (arrastra bajo y melodía).

### CARDEXPR-A2 — Consumo de `PitchBendWriter` en Melody (slur / approach / scoop)
- **Título:** `MelodyTrackComposer` como segundo consumidor de CONTRACTS §11 — gesto STEP por nota marcada como legato/approach.
- **Motivación de carta:** lead de Zig con approach notes de walking y scoops vocales (Shout!/Croon vía singer, si el consumidor honra el bend).
- **Criterio de aceptación:** (1) decisión de superficie registrada (campo de arquetipo, directiva de `MelodicStyleSO` o clase de `MelodyPatternData`); (2) carrier = nota sonora precedente dentro de la frase; gate del carrier se extiende por la cola; nota huérfana degrada a nota atacada con 1 warning/render; (3) intervalo en **grados de escala** (paridad con D-BEND-DEG=A) medido desde el pitch alcanzado; (4) rango ±2 st asumido, clamp con warning; (5) cero draws nuevos de `ctx.rng`; render sin marcas de legato byte-idéntico (canary de hash); (6) call site post-build antes del stamp de canal/patch; (7) seams `BuildLegatoCarrierMap`/`ResolveLegatoGroupEndBeats` compartidos con el bajo o duplicación justificada.
- **Degradación aceptable:** approach notes como notas atacadas (lo que hoy hace `ImprovisedWalk` en el bajo); para la voz, portamento del singer (`pitchLeadSeconds`) consumer-side.

### CARDEXPR-A3 — Consumo real de los campos de feel de `RhythmCardConfigSO` en la ruta GRID *(recomendado: DIFERIR)*
- **Título:** `snareGhostNoteChance` / `hatSubdivisionBias` / `kickDensity` / fills como transformaciones deterministas sobre el patrón GRID resuelto (o retirarlos de la superficie).
- **Motivación de carta:** ninguna carta candidata de §5 los necesita; C2 se cubre con `patternOverride`/palette. Se registra para cerrar la etiqueta [V] y el gap §8.5, no como demanda.
- **Criterio de aceptación (si se abre):** (1) cada campo o se consume en la ruta GRID con una ley documentada y seeded (`ctx.rng` con conteo fijo de draws o pure-mix), o se elimina/oculta; (2) defaults → byte-idéntico; (3) los onsets publicados (§3bis) reflejan el patrón transformado (si no, el pocket seguiría al patrón sin transformar); (4) pins.
- **Degradación aceptable:** dejar los campos inertes y **no** apoyar cartas en ellos (estado actual).

### CARDEXPR-A4 — *(opcional)* SlapPocket como pass del orquestador (order-free)
- **Título:** publicar onsets antes de componer el bajo mediante pass (`SSoT_Runtime_Generation_Orchestration` §5.7), eliminando la "recorded exception" de CONTRACTS §10.
- **Motivación de carta:** *In the Pocket* sin que ALWTTT tenga que ordenar `Part.Tracks` por rol ni re-renderizar. Hoy la alternativa order-free es SelfPocket.
- **Criterio de aceptación:** (1) con Rhythm en cualquier posición de la lista, el bajo pocketed es byte-idéntico al caso Rhythm-primero; (2) sin Rhythm → degrade actual intacto; (3) sin cambio de streams (`ctx.rng` por track) ni de seeds; (4) hash duty del consumidor permanece (el paquete no puede descubrir la caché del host).
- **Degradación aceptable:** sort estable por rol en `SongConfigBuilder.FromUI` (consumer-side, la solución que el inventario §6 ya identifica). Si ALWTTT implementa el sort, este ask no hace falta.

**Confirmación de deberes consumer-side (no asks, ya contractuales):** hash duty con `pocketMode != Off` (Bass §3.7) y orden Rhythm→Bassline o re-render (CONTRACTS §10). Ambos coinciden con las deudas 1 y 2 del §6 del inventario.

---

## 5. Actualizaciones documentales detectadas en el paquete (propuesta, NO aplicada)

| # | Documento | Qué | Por qué |
|---|---|---|---|
| D-1 | `runtime/SSoT_Composer_Bass_Track.md` §3.3 | añadir frase: en una carta de bajo `PowerChord` degrada a `Block` (PlanCore) y `Chugging` rinde el pulso de raíz a `arpeggioRate` (ChordPulsePlan sin reshaper), indistinguible de `ArpeggioUp`/`RepeatedNote` | hueco doc-vs-code (Q1) |
| D-2 | `PENDING_DOC_DIFFS.md` | registrar como PENDIENTES `MGP-ALWTTT-BASS-PHRASE-1`, `MGP-BASSCARD-WIZARD-1` y `MGP-TONALITY-1` | el acumulador dice EMPTY mientras tres diffs drafted-not-applied circulan y su código está en el PK |
| D-3 | `CURRENT_STATE.md` | entradas "Just completed" para PHRASE-1, WIZARD-1 y TONALITY-1 (esta última con la nota de cambio de render D-TON10) | el estado implementado no refleja tres batches |
| D-4 | `runtime/SSoT_Composer_Bass_Track.md` §3.6bis | retirar la nota de accidental-blindness (D-W2-LAST) — ya propuesto en `MGP-TONALITY-1_doc_diffs.md` §2.1 | doc-truth desactualizada |
| D-5 | `SSoT_CompositionCards_TrackStyleBundles.md` §4.5 | reconciliación de la superficie de la carta de bajo (ya marcada como "pre-existing drift"); añadir el bloque PHRASE-1 | superficie de cartas incompleta para authoring |

Ninguna se aplica en esta sesión (verificación únicamente). D-2/D-3 son bookkeeping; D-1/D-4/D-5 son drift real pero acotado.

---

## 6. Criterios de cierre

- [x] Ninguna [V] del inventario sin clasificar: Q1 (b) · Q2 (b) · Q3 (c) · Q4 (a) · Q5 (c) · Q6 (c); [V] "Campos de feel" (c); "Octavas" (b); "Ghost density" (a); "Gallop" (b).
- [x] Cada candidato de §5 tiene ejemplo de authoring (3.1–3.10) o ask (3.11 → A1; 3.12 → fuera del PK / A2).
- [x] Delta post-snapshot enumerado (§2): TONALITY-1 + los dos batches ausentes del inventario.

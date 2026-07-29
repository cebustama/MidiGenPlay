# Especificación de autoría — Pasada de contenido, fase A (2026-07-28)

Material validado contra el paquete para la fase B en ALWTTT. Nada de este
documento cambia runtime; todo es dato a autorar. Autoridad musical: respuesta
del Music Lab a ML-1…ML-8 (asesoría, no autoridad de paquete). Autoridad de
formato: `SSoT_Authoring_Chord_Progressions.md`, `SSoT_Authoring_Rhythm_Patterns.md`,
`SSoT_Composer_Bass_Track.md`.

## 0. Decisiones cerradas en fase A

| ID | Decisión |
|---|---|
| D-A1 | Rangos de bajo cuantizados a octavas de campo: eléctrico `2/4`, slap `3/4`, contrabajo `2/3`, sinte `2/3`. Convención verificada: **octava autorada A = octava científica A−1; su Do = MIDI A×12**. Banda de raíces = `C(octaveMin−1)..B(octaveMin)` científico; techo duro = `B(octaveMax−1)`. |
| D-A2 | Todos los payloads de esta pasada se importan con auto-calidad **DiatonicTriads**. Las séptimas/novenas deseadas ya son sufijos explícitos en los bloques. |
| D-A3 | Inventario ALWTTT recibido (2026-07-28) y usado como base de ML-1/ML-4. |
| V-2 | Sufijo desconocido = warning + inferencia diatónica (nunca fallo duro). Todo sufijo de esta spec verificado contra el alfabeto real del parser. **`9` = Dominant9** (intencional en `IV9`; no "corregir" a `maj9`). |

Campos que NO viajan en el payload y se fijan a mano tras importar, por asset:
`qualityRenderPolicy`, `useColorTable`, `cadence`, `tonalities`, marcas SECDOM
(`hasAppliedTarget`/`appliedTarget`), `songReferences`.

---

## 1. Progresiones

Leyenda: **[SUB asset]** sustituye un asset existente (retirar el viejo tras
recablear) · **[ALTA]** nueva · **[KEEP]** se conserva tal cual · **[RETIRAR]**
se elimina. Política: AA = AsAuthored, DTP = DiatonicToPart,
DTPF = DiatonicToPartFunctional. Todos 4/4 salvo indicación; `Default duration: 1`
en todas las fichas; todas suman 8 compases (verificado).

### 1.1 Paleta Modal — repoblación completa (ML-1, ML-2, ML-8)

Las 10 entradas actuales NO son contenido modal: son color prestado en
jónico/eólico (ver §1.2, donde se reubican). La paleta Modal queda con estos 7.

| Asset | Ref. tonality | Bloque romano | Política | Color | Cadence | tonalities | Origen |
|---|---|---|---|---|---|---|---|
| Prog_Modal_Dorico_Vamp [ALTA] | Dorian | `im7 (2) - IV9 (2) - im7 (2) - iim7 (1) - IV7 (1)` | AA | ON | Modal | Dorian | ML-1 |
| Prog_Modal_Frigio_Descenso [ALTA] | Phrygian | `im7 (2) - II (2) - iv (1) - III (1) - II (2)` | AA | ON | Modal | Phrygian | ML-1 |
| Prog_Modal_Lidio_Flotante [ALTA] | Lydian | `Imaj7 (2) - II (2) - viim7 (2) - II (2)` | AA | ON | Modal | Lydian | ML-1 |
| Prog_Modal_Mixo_SweetHome [ALTA] | Mixolydian | `I (2) - VII (1) - IV (1) - vm7 (1) - IV (1) - VII (2)` | AA | ON | Modal | Mixolydian | ML-1 |
| Prog_Modal_Eolico_Giro [ALTA] | Aeolian | `im7 (2) - VI (2) - VII (2) - im7 (1) - VII (1)` | AA | ON | Modal | Aeolian | ML-1 |
| Prog_Modal_Skel_TonicaSeptimo [ALTA] | Mixolydian | `I (2) - VII (2) - I (2) - vm7 (1) - VII (1)` | **DTP** | ON | Modal | Ionian…Aeolian (6, sin Locrian) | ML-1 |
| Prog_Modal_Skel_TonicaCuartoSegundo [ALTA] | Dorian | `im7 (2) - IV (2) - im7 (2) - iim7 (2)` | **DTP** | ON | Modal | Ionian…Aeolian (6, sin Locrian) | ML-1 |

Notas de autoría: (a) los 5 buques emparejados a su modo — su lista `tonalities`
es restrictiva **por diseño**, no por accidente (a diferencia del estado
F-MODAL-SELFDEFEAT); (b) esqueleto Tónica–Séptimo: punto ciego jónico (`VII`→vii°)
— peso bajo si la parte va en jónico; (c) esqueleto Tónica–Cuarto–Segundo: punto
ciego lidio; en eólico el `ii` largo dispara la sustitución `ii°→iv` de la tabla
de color (asset-demo de la tabla); (d) nada de `DiatonicToPartFunctional` en esta
paleta (la sensible es el destructor, ML-1 §1.5); (e) ficha de setup:

```
Time signature: FourFour
Measures: 8
Default duration: 1
Reference tonality: <la de la tabla>
```

### 1.2 Core Major (ML-3, ML-4, ML-7)

Estado actual: 8 entradas, motores 1–3 cubiertos, todo a 4 compases salvo
Pachelbel. Huecos confirmados contra el checklist ML-4: blues (4), intercambio
modal (5), secundarios (6). El intercambio modal ya existía **mal archivado en
la paleta Modal**: se reubica aquí reescrito a 8.

Reescrituras a 8 compases de lo existente:

| Asset nuevo | Bloque | Política | Color | Cadence | Origen | Sustituye |
|---|---|---|---|---|---|---|
| Prog_Maj_Axis_8c | `I (2) - V (2) - vi (2) - IV (2)` | DTP | ON | Plagal | ML-3 T1, ML-8.2 | I–V–vi–IV 4c |
| Prog_Maj_AxisRot_8c | `IV (2) - I (2) - V (2) - vi (2)` | DTP | ON | None | ML-3 T1, ML-8.5 | IV–I–V–vi 4c |
| Prog_Maj_Cincuentas_8c | `I (2) - vi (2) - IV (2) - V (2)` | DTPF | ON | Authentic | ML-3 T1, ML-8.1, ML-8b | I–vi–IV–V 4c |
| Prog_Maj_ViejaEscuela_8c | `I (2) - vi (2) - ii (2) - V (2)` | DTPF | ON | Authentic | ML-4 fam.2 | I–vi–ii–V 4c |
| Prog_Maj_Periodo_IVV_8c | `I (2) - IV (1) - V (1) - I (2) - IV (1) - V (1)` | DTPF | ON | Authentic | ML-3 T4 | I–IV–V–I 4c |
| Prog_Maj_Sentence_T2_8c | `I (2) - IV (2) - I (1) - IV (1) - iim7 (1) - V7 (1)` | DTPF | ON | Authentic | ML-3 T2 | ii(0.5)–V(0.5)–I 2c |
| [KEEP] Pachelbel 8c | (existente, ya 8c) | fijar DTPF | ON | Authentic | ML-8.1 | — |
| [RETIRAR] `As.asset` | mal autorado (Vmaj7–V7–Imaj7…), OFF-ROOT | — | — | — | — | sin sustituto |

Altas nuevas y reubicaciones desde la vieja paleta Modal:

| Asset | Bloque | Política | Color | Cadence | Origen | Notas |
|---|---|---|---|---|---|---|
| Prog_Maj_Blues8c [ALTA] | `I7 (2) - IV7 (2) - I7 (1) - V7 (1) - I7 (1) - V7 (1)` | AA | OFF | Authentic | ML-4 fam.4 | Sin SECDOM (ML-7: dominantes = estructura). tonalities: Ionian, Mixolydian |
| Prog_Maj_Creep_IVpiv [ALTA] | `I (2) - III (2) - IV (2) - iv (2)` | AA | OFF | Plagal | ML-4 fam.5 | `III` y `iv` quedan marcados prestados al importar; el `III` NO es V/vi — no marcar SECDOM |
| Prog_Maj_Ragtime_SECDOM [ALTA] | `I (2) - VI7 (2) - II7 (2) - V7 (2)` | DTPF | OFF | Authentic | ML-4 fam.6, ML-7 | Tras importar: evento 2 `hasAppliedTarget→Supertonic` (V/ii), evento 3 `→Dominant` (V/V); la primitiva rederiva en transposición/modo |
| Prog_Maj_PuertaTrasera_bVII [SUB] | `I (2) - bVII (2) - IV (2) - bVII (2)` | AA | OFF | Modal | ML-4 fam.5 | Sustituye «I–bVII–IV–I Modal» y absorbe el DUP#2; costura bVII→I |
| Prog_Maj_Nubarron_bVI [SUB] | `I (2) - bVI (2) - IV (2) - I (2)` | AA | OFF | None | ML-4 fam.5 | Sustituye «I–bVI–IV–I Modal»; cierre en tónica (bucle cerrado) |
| Prog_Maj_Prestamo_bIII [SUB] | `I (2) - bIII (2) - IV (2) - I (2)` | AA | OFF | None | ML-4 fam.5 | Sustituye «I–bIII–IV–I Modal» |

Retiros adicionales de la vieja Modal sin sustituto directo:
«bVII–IV–I(2)» (cubierta por PuertaTrasera), «I–#IVdim7–V7–I» (cromatismo
huérfano de familia; re-evaluar en un alfabeto con inversiones),
«I–II–V–I» (el `II` es un V/V encubierto — su función queda mejor servida por
Ragtime_SECDOM), «I(2)–bVII(2)–S(2)» (SHORT-TAIL, evento S; malformada).

### 1.3 Core Minor (ML-3, ML-4)

| Asset | Bloque | Política | Color | Cadence | Origen | Notas |
|---|---|---|---|---|---|---|
| Prog_Min_ivV_T2_8c [SUB] | `i (2) - iv (2) - i (2) - iv (1) - V (1)` | DTPF | ON | Authentic | ML-3 T2 | Sustituye «i–iv–V–i Aeolian» 4/4; `V` mayúscula = mayorizada al importar |
| Prog_Min_ivV_T2_34_8c [SUB] | ídem, `Time signature: ThreeFour` | DTPF | ON | Authentic | ML-3 T2 | Sustituye la versión 3/4 |
| Prog_Min_RotacionVII_8c [SUB] | `i (2) - VII (2) - VI (2) - VII (2)` | DTP | ON | Modal | ML-8.4 | Sustituye «i–VII–VI–VII» 4c |
| [KEEP] i(2)–VI(2)–III(2)–VII(2) 8c | existente, ya 8c | fijar DTP | ON | Modal | ML-8.4 | — |
| [RETIRAR] i–VI–III–VII 4c | duplicado del 8c | — | — | — | — | — |
| [RETIRAR] V–VI 2c | fragmento sin motor | — | — | — | — | — |
| Prog_Min_Andaluza [ALTA] | `i (2) - VII (2) - VI (2) - V7 (2)` | AA | OFF | Authentic | ML-4 fam.7 | Sensible fabricada (`V7` explícito); tonalities: Aeolian |
| Prog_Min_BluesSoul_i7iv7 [ALTA] | `im7 (2) - iv7 (2) - im7 (2) - iv7 (2)` | AA | OFF | Plagal | ML-4 fam.4 | tonalities: Aeolian, Dorian |
| Prog_Min_iiø7V7_8c [ALTA] | `im7 (2) - iv7 (2) - im7 (2) - iiø7 (1) - V7 (1)` | AA | OFF | Authentic | ML-4 fam.3 | tonalities: Aeolian |
| Prog_Min_Napolitana_bII [SUB] | `i (2) - bII (2) - i (2) - bII (1) - V7 (1)` | AA | OFF | Authentic | ML-4 fam.5/7 | Sustituye «i–bII–V–i Modal» (se muda de paleta) |
| Prog_Min_bVI_iv_V_8c [SUB] | `i (2) - bVI (2) - iv (2) - V7 (2)` | AA | OFF | Authentic | ML-4 fam.7 | Sustituye «i–bVI–iv–V–i Modal» (5 compases → 8) |

Nota transversal ML-8b: toda etiqueta *Authentic* de esta spec va sobre política
AA o DTPF (la tercera del V sobrevive); las etiquetas sobre DTP puro son *Modal*
o *Plagal*/*None* — combinaciones elegidas juntas, no por separado.

### 1.4 Hueco documentado (no autorar)

Progresiones de línea de bajo (lament, cromática por inversiones, diatónica
descendente): requieren inversiones que `ChordEvent` no representa (verificado:
sin campo de inversión; la inversión es del voicer). No aproximar. Queda
esperando a un alfabeto futuro. [ML-4, hueco declarado]

---

## 2. Patrones de batería — 8 géneros × 8 compases (ML-5)

Arquitectura común 3+1/3+1 (P3): establecer 1–3, coma en 4, +1 nivel en 5–7 con
el 7 quieto, evento en 8, llegada en el 1 de la vuelta. Todas las filas
verificadas aritméticamente contra `compases × pulsos × subdivisiones`.
Velocities de `X`/`o` son las canónicas del parser; la default por lane va en la
ficha. Todos `Time signature: 4/4`, `Measures: 8`.

### Rock (subdiv 2 · 64 pasos/fila) — verificado por el Music Lab
Lanes: Bass Drum 1 (36, v110) · Acoustic Snare (38, v115) · Closed Hi-Hat (42, v85) · Crash Cymbal 1 (49, v100)
```
x...x...|x...x...|x...x...|x...x.x.|x...x...|x..xx...|x...x...|x.......
..x...x.|..x...x.|..x...x.|..x...xx|..x...x.|..x...x.|..x...x.|..x.xxXX
xxxxxxxx|xxxxxxxx|xxxxxxxx|xxxxxxxx|XxxxXxxx|XxxxXxxx|XxxxXxxx|xxxx....
X.......|........|........|........|X.......|........|........|........
```

### Funk (subdiv 4 · 128) — fill = push, no toms; pocket = quietud 5–7
Lanes: Bass Drum 1 (36, v105) · Acoustic Snare (38, v110) · Closed Hi-Hat (42, v80) · Open Hi-Hat (46, v95)
```
x......x..x.....|x......x..x.....|x......x..x.....|x......x..x.....|x......x..x.....|x......x..x.....|x......x..x.....|x......x..x.....
....x.o.....x.o.|....x.o.....x.o.|....x.o.....x.o.|....x.o..o..x.o.|....x.o.....x.o.|....x.o.....x.o.|....x.o.....x.o.|....x.o.....x..X
xxxxxxxxxxxxxxxx|xxxxxxxxxxxxxxxx|xxxxxxxxxxxxxxxx|xxxxxxxxxxxxxxxx|XxxxXxxxXxxxXxxx|XxxxXxxxXxxxXxxx|XxxxXxxxXxxxXxxx|XxxxXxxxXxxxXxx.
................|................|................|..............x.|................|................|................|................
```

### Jazz (subdiv 3 · 96) — ride ancla constante; comping varía poco y siempre; el 8 recoge, no rellena
Lanes: Ride Cymbal 1 (51, v95) · Acoustic Snare (38, v100) · Bass Drum 1 (36, v70) · Pedal Hi-Hat (44, v75)
```
x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x|x..x.xx..x.x
..o......o..|.....o......|..o.....o...|.o....o.....|..o......o..|......o..o..|..o.........|.........xxX
o.....o.....|o.....o.....|o.....o.....|o.....o.....|o.....o.....|o.....o.....|o.....o.....|o.....o.....
...x.....x..|...x.....x..|...x.....x..|...x.....x..|...x.....x..|...x.....x..|...x.....x..|...x.....x..
```

### Hip-hop (subdiv 4 · 128) — la fidelidad del loop ES el género; coma = stutter, fill = drop-out
Lanes: Bass Drum 1 (36, v110) · Acoustic Snare (38, v115) · Closed Hi-Hat (42, v80)
```
x.....x...x.....|x.....x...x.....|x.....x...x.....|x.x...x...x.....|x.....x...x.....|x.....x...x.....|x.....x...x.....|x.....x.........
....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x...........
x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.........
```

### Latin, son 3-2 (subdiv 4 · 128) — la clave NO se toca jamás; monedas en conga; fill corto y con la clave
Lanes: Claves (75, v110) · Bass Drum 1 (36, v100) · Open Hi Conga (63, v95) · Cowbell (56, v85)
```
x.....x.....x...|....x...x.......|x.....x.....x...|....x...x.......|x.....x.....x...|....x...x.......|x.....x.....x...|....x...x.......
......x.....x...|......x.....x...|......x.....x...|......x.....x...|......x.....x...|......x.....x...|......x.....x...|......x.....x...
..o.o..x..o.o.x.|..o.o..x..o.o.x.|..o.o..x..o.o.x.|..o.o..x..o.oox.|..o.o..x..o.o.x.|..o.o..x..o.o.x.|..o.o..x..o.o.x.|..o.o..x..o.oxxX
x...x...x...x...|x...x...x...x...|x...x...x...x...|x...x...x...x...|x...x...x...x...|x...x...x...x...|x...x...x...x...|x...x...x...x...
```

### Metal (subdiv 4 · 128) — 1–6 implacables sin coma; fill largo: doble bombo en 7, cascada de toms en 8
Lanes: Bass Drum 1 (36, v115) · Acoustic Snare (38, v120) · Closed Hi-Hat (42, v90) · Low-Mid Tom (47, v110) · Crash Cymbal 1 (49, v110)
```
x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|xxxxxxxxxxxxxxxx|xxxxxxxxxxxxxxxx
....X.......X...|....X.......X...|....X.......X...|....X.......X...|....X.......X...|....X.......X...|....X.......X...|................
X.x.X.x.X.x.X.x.|X.x.X.x.X.x.X.x.|X.x.X.x.X.x.X.x.|X.x.X.x.X.x.X.x.|X.x.X.x.X.x.X.x.|X.x.X.x.X.x.X.x.|................|................
................|................|................|................|................|................|................|XxxxXxxxXxxxXxxx
X...............|................|................|................|X...............|................|................|................
```

### Drum'n'bass (subdiv 4 · 128) — break de 2 compases re-picado por pares; fill = redoble denso
Lanes: Bass Drum 1 (36, v110) · Acoustic Snare (38, v115) · Closed Hi-Hat (42, v75)
```
x.........x.....|x.......x.......|x.........x..x..|x.x.....x.......|x........xx.....|x.......x....x..|x.........x.....|x.......x.......
....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|....x.......x...|xoxoxoxoxoxoxXXX
x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.|x.x.x.x.x.x.x.x.
```

### Country, train beat (subdiv 2 · 64) — el tren no frena; recogida seca de 2 corcheas
Lanes: Acoustic Snare (38, v100) · Bass Drum 1 (36, v105) · Closed Hi-Hat (42, v80)
```
xoXoxoXo|xoXoxoXo|xoXoxoXo|xoXoxoXo|xoXoxoXo|xoXoxoXo|xoXoxoXo|xoXoxoXX
x...x...|x...x...|x...x...|x...x...|x...x...|x...x...|x...x...|x...x...
x.x.x.x.|x.x.x.x.|x.x.x.x.|x.x.x.x.|x.x.x.x.|x.x.x.x.|x.x.x.x.|x.x.x.x.
```

Archivado propuesto (decisión B, recomendación): añadir a paletas 4/4 existentes
sin recablear cartas — FourOnTheFloor: rock, metal, country, latin;
SyncopatedPocket: funk, hip-hop, dnb, jazz. Alternativa: paleta nueva por feel.

---

## 3. Registro del bajo — valores de campo (ML-6 + D-A1)

**Los assets de instrumento viven en el PAQUETE** (`Packages/.../MIDI
Instruments/Backing/Basses/`), no en ALWTTT: estas ediciones son package-side.

| Asset (pkg) | Actual | Nuevo | Banda de raíces | Techo | Justificación |
|---|---|---|---|---|---|
| Fingered Bass | 2/3 | **2/4** | C1–B2 (24–47) | B3 (59) | Perfil eléctrico ML-6; cabeza melódica, pops vivos |
| Picked Bass | 2/3 | **2/4** | C1–B2 | B3 | Extensión del perfil eléctrico (no estaba en ML-6) |
| Slap Bass 1 | 1/3 | **3/4** | C2–B3 (36–59) | B3 (59) | Suelo alto ML-6; pops sobreviven para raíces C2–B2, se pliegan para C3+ |
| Slap Bass 2 | 1/3 | **3/4** | ídem | ídem | ídem |
| Acoustic Bass | 2/3 | **sin cambio** | C1–B2 | B2 (47) | Ya coincide con el perfil contrabajo (grave por estética) |
| Synth Bass 1 | 2/4 | **2/3** | C1–B2 | B2 (47) | Perfil sub (suelo C1 exacto; techo ≈ C3) |
| Synth Bass 2 | 2/4 | **sin cambio** | C1–B2 | B3 | Variante «synth melódico» plan-B de ML-6 [A] — quedan los dos assets |
| Fretless Bass | 2/4 | **sin cambio** | C1–B2 | B3 | Ya en perfil eléctrico-melódico |

Estado crítico detectado: los dos Slap estaban en `1/3` → banda C0–B1
(MIDI 12–35), subterránea. Es el cambio con más impacto audible de la tabla.

# Handoff: MGP-BAGGAGE-1 — disposición de assets muertos del catálogo del paquete

Mode: DOCUMENTATION / MAINTENANCE. Respuesta package-side a MGP-ALWTTT-BAGGAGE-1.

Context: MidiGenPlay recibió el inventario medido de ALWTTT (218 assets, banderas de
salud derivadas) y evaluó los cuatro grupos señalados. La medición del consumidor se
confirma package-side y se amplía con dos hallazgos que ALWTTT no podía ver desde
fuera de Packages/. Este handoff es el registro de disposición; la ejecución de los
borrados/movimientos se aplica en el paquete y se anuncia por versión.

**Estado de ejecución: APLICADO, y publicado en MidiGenPlay 1.2.0** (código/assets
2026-07-20; documentación package-side aplicada 2026-07-21). Decisiones cerradas:
D-BAG-1=A, D-BAG-2=A, D-BAG-3=A, D-BAG-4=A. Los retiros y el movimiento están
verificados contra el árbol del paquete. **ALWTTT puede adoptar 1.2.0 y ejecutar §7.**

> **CORRECCIÓN DE VERSIÓN (2026-07-21).** Una revisión anterior de este handoff decía
> **1.1.0**. Ese bump nunca llegó a materializarse en `package.json`: **1.1.0 no
> existe y nunca se publicó.** El paquete salta **1.0.0 → 1.2.0** de una vez, y 1.2.0
> lleva tanto este lote como MGP-MIX-1 (ganancia de mezcla consumer-side).
> **Fijad 1.2.0.** Donde antes leíais 1.1.0, leed 1.2.0.

---

## 1. Confirmaciones package-side de la medición

- **TS=FourFour en las 16 assets Default\*** — confirmado y explicado.
  `MusicTheory.TimeSignature` declara `FourFour` como primer miembro del enum, luego
  vale 0. Un asset nunca autorado serializa 0. La bandera no indica "autorado mal"
  sino "nunca autorado". La interpretación de ALWTTT era correcta.
- **"Sin referencias" es correcto pero subestima el problema.** Los composers no
  resuelven patrones por repositorio: `MelodyTrackComposer` /
  `ChordTrackComposer` / `RhythmTrackComposer` toman el patrón de
  `TrackParameters.Pattern`, del card override, o del render override — referencias
  explícitas. Pero `PatternRepositoryResources` hace `Resources.LoadAll` sobre
  `ScriptableObjects/Patterns/{Drums,Chords,Melodies}` y publica todo lo que
  encuentra por `GetAll*()` y `Get*(TimeSignature)`. Es decir: estos assets no están
  referenciados, pero **sí están en el catálogo que ve cualquier selector
  consumer-side**, y `GetChordProgressions(FourFour)` / `GetDrumPatterns(FourFour)`
  pueden devolver hoy un asset vacío o sin lanes. Eso los convierte en riesgo de
  selección, no sólo en equipaje. Refuerza el retiro.
- **Ninguna SSoT gobernada declara estos assets como fallback de runtime.** No hay
  resolución por nombre de un "default por compás" en ninguna parte del paquete.

## 1-bis. Verificación de lo aplicado (árbol del paquete, 2026-07-20 / 2026-07-21)

| Grupo | Disposición | Estado |
|---|---|---|
| A — `ChordProgression-Default*` (8) | retirar | borrados |
| B — `DrumPattern-Default*` (8) | retirar | borrados |
| C — `Patterns/Melodies/*` (12) | retirar | borrados |
| D — `Test Palette.asset` | retirar | borrado |
| D — `DrumPatternPalette.asset` | retirar | borrado |
| D — `Chord Progressions/` | mover | en `Samples/ExampleCatalogue/ChordProgressions/`; la raíz de origen, ya vacía, se borra |
| extra — `Test Progression.asset` | retirar | borrado |
| extra — `Melodic Style - Test 1.asset` | retirar | borrado |
| D-BAG-4 — `_*List.asset` (3) | conservar, vaciar | conservados y vaciados (verificado 2026-07-21) |

Total: **32 assets retirados** (8 + 8 + 12 + 2 + 2), **6 movidos fuera de
`Resources/`** (las progresiones autoradas + `_ChordProgressionLibrary.asset`, con su
subcarpeta `Palettes/`).

**`Patterns/Chords/Palettes/` y `Patterns/Drums/Palettes/` quedan vacías a
propósito.** Son las raíces canónicas de enumeración fijadas por MGP-ALWTTT-DBG-2. Si
el inventario de ALWTTT las marca como carpetas huérfanas, es un falso positivo:
registrarlas como toleradas.

## 2. Disposición por grupo

### A. Progresiones de acordes (8) — `Runtime/Resources/ScriptableObjects/Patterns/Chords/ChordProgression-Default*` → **RETIRAR (borrar)**
Seis vacías; las dos con contenido tienen `Measures=0`, lo que las hace
irrenderizables igual. No son fallback, no son plantilla de editor (el editor de
progresiones escanea `Assets/Resources/...`, nunca `Packages/`), no son fixture de
test (ningún test los carga). Se borran.

Corrección de ruta respecto al pedido: estas 8 están bajo `Patterns/Chords/`, no
directamente bajo `ScriptableObjects/`. Ver §4, es relevante para D-CSV-14.

### B. Patrones de batería (8) — `.../Patterns/Drums/DrumPattern-Default*` → **RETIRAR (borrar)**
Siete sin lanes, una completamente en silencio. Mismo razonamiento que A. Se borran.

### C. Patrones de melodía (12) — `.../Patterns/Melodies/` → **RETIRAR (borrar)**
Las 12 vacías. `BasicMelodyPattern 2..7`, `FourFourMelody1..3`,
`ThreeFourMelody1..2`, `OrangePeelBass`. Se borran las 12. `OrangePeelBass` incluido:
nombre de contenido de un proyecto anterior, vacío, sin referencias.

### D. Contenedores de test (3) → **RETIRAR 2, MOVER 1 (con su carpeta)**
- `Patterns/Chords/Palettes/Test Palette.asset` → **borrar**. Es un fixture con
  nombre de producción; además es inalcanzable por el tooling, que escanea
  Assets-side.
- `Patterns/Drums/Palettes/DrumPatternPalette.asset` (displayName "TestPalette") →
  **borrar**. Mismo caso.
- `ScriptableObjects/Chord Progressions/_ChordProgressionLibrary.asset` → **mover**,
  junto con toda la carpeta `Chord Progressions/` (ver §3). No se borra sola porque
  sus entradas apuntan a las progresiones autoradas de esa misma carpeta, que sí
  tienen contenido real y valor de ejemplo.

### Equipaje adicional detectado package-side (no estaba en el pedido)
- `ScriptableObjects/Melodic Style - Test 1.asset` — fixture con nombre genérico de
  tipo, misma categoría que el grupo D. Se retira en el mismo lote.
- `_Chord Progressions List.asset`, `_Drum Patterns List.asset`,
  `_Melody Patterns List.asset` — contenedores de listado que enumeran precisamente
  los assets retirados en A/B/C. **Se mantienen y se vacían** (D-BAG-4=A, cerrada);
  contenido verificado vacío el 2026-07-21, sin referencias colgando. ALWTTT no
  necesita hacer nada al respecto.

## 3. Movimiento de rutas — interacción con D-CSV-14

**Hay un movimiento package-side, y sí toca D-CSV-14.**

`Runtime/Resources/ScriptableObjects/Chord Progressions/` (las progresiones autoradas
+ `_ChordProgressionLibrary.asset` + `Palettes/` vacía + `Test Progression.asset`)
es una **segunda raíz de catálogo, más antigua**, paralela a
`Patterns/Chords/`. Package-side:

- `PatternRepositoryResources` **no** escanea `Chord Progressions/`. Sólo
  `Patterns/Chords/`.
- `ChordProgressionCatalogueWizard` escanea `Assets/Resources/ScriptableObjects/Chord
  Progressions` y `Assets/Resources/Chord Progressions` — ambas Assets-side.
- Conclusión: la carpeta `Chord Progressions/` del paquete es huérfana respecto al
  runtime *y* respecto al tooling. Es el mismo desajuste de raíces que ALWTTT está
  registrando en D-CSV-14, pero originado dentro del paquete.

Aplicado: `Chord Progressions/` completa **salió de `Runtime/Resources/`** y está en
`Samples/ExampleCatalogue/ChordProgressions/`; la carpeta de origen, que quedó vacía,
se borra junto con su `.meta`. Consecuencias para ALWTTT:

- No cambia ninguna ruta de `Resources.LoadAll` en el paquete. Las constantes
  `ScriptableObjects/Patterns/{Drums,Chords,Melodies}` quedan idénticas. Lo que cambia
  es **el contenido devuelto**: menos assets, ninguno vacío.
- La raíz `.../ScriptableObjects/Chord Progressions` **deja de existir** dentro de
  `Packages/`. Si el escáner de inventario de ALWTTT la enumera, no la encontrará; el
  conteo baja. Esto es esperado, no una regresión.
- Las tres raíces de patrones `Patterns/{Chords,Drums,Melodies}` son ahora las
  **únicas** raíces de catálogo package-side. El desajuste que quedaba es
  exclusivamente Assets-side.
- `Test Progression.asset` se borró en vez de moverse.

## 4. Respuesta a las dos observaciones de contenido

### 4.1 Poly Synth y Warm Pad — **falso positivo de la medición**
Verificado en los assets: no comparten patch.

| Asset | SoundFont | Bank | PatchName | PatchIndex |
|---|---|---|---|---|
| `Warm Pad` | ALWTTT | 000 | `89 - Warm Pad` | 89 |
| `Poly Synth` | ALWTTT | 000 | `90 - Poly Synth` | 90 |

`MIDIInstrumentSO.PatchIndex` indexa GM en **base 0**, y con esa convención ambos son
correctos: 89 = Pad 2 (warm), 90 = Pad 3 (polysynth). Los dos campos (`PatchName` y
`PatchIndex`) coinciden entre sí en los dos assets. No hay nada que diferenciar ni que
consolidar; los assets están bien autorados.

**Acción para ALWTTT:** revisar la extracción del inventario. La causa más probable es
una normalización 0/1 aplicada a un solo campo, o leer `PatchName` en un asset y
`PatchIndex` en el otro, colapsando 89 y 90. Conviene comprobar si el mismo artefacto
afecta a otros pares del catálogo de 70 instrumentos antes de dar por buena la
columna de patch del export.

### 4.2 volume01 = 1.0 en los 70 instrumentos melódicos
Respuesta en dos partes.

- **`volume01` es un campo de authoring package-side y se queda.** Su propósito es
  nivel nominal por instrumento (normalización de sonoridad entre parches del
  soundfont). Que los 70 estén en 1.0 significa que **está sin autorar**, no que la
  autoría deliberada sea plana. Autorarlo es trabajo package-side pendiente, se
  registra como tal.
- **El balance de mezcla del consumidor no debe pasar por ese campo.** La observación
  de ALWTTT es correcta y la regla de frontera lo prohíbe con razón. El paquete ya
  tenía el seam de bajo nivel:
  `MidiGenerator.ApplyChannelVolume(file, channel, 0..127)` (CC7 por canal) más las
  interfaces `IMixController` / `PassthroughMixController`. Lo que faltaba era una
  ganancia por track/músico documentada que **componga** con `volume01` en lugar de
  reemplazarlo.

**Estado: CERRADO como batch propio, MGP-MIX-1, publicado en la misma 1.2.0.** Las
cuatro decisiones quedaron así: punto de aplicación = CC7 en generación (D-MIX-1=A);
granularidad = por `MusicianTrackKey (musicianId, TrackRole)` (D-MIX-2=A); ley de
composición = multiplicativa `clamp(round(volume01 × gain × 100), 0, 127)` con puerta
de emisión por entrada (D-MIX-3); determinismo/readback = camino sin RNG y
`PartRender.appliedCc7ByTrack` (D-MIX-5=A). Percusión fuera de v1 (D-MIX-4=A, canal 9
compartido). **Ver el handoff `Handoff_MGP_MIX_1.md` para la superficie exacta.**

La normalización real de los 70 `volume01` sigue siendo trabajo de contenido
package-side aparte (D-MIX-6), bloqueado por vuestros veredictos D-CSV-18, y no
bloquea la adopción del seam: la ley es multiplicativa y vuestro default es identidad.

## 5. Qué se mantiene y por qué (equipaje tolerado — dejar de re-investigar)

Nada de los grupos A–D es intencional. **No hay ningún fallback de runtime, plantilla
de editor ni fixture de test entre los assets listados.** ALWTTT puede cerrar la
investigación sobre los 30 assets de A/B/C/D (y sobre los 2 extras package-side).

Sí se mantienen, y ALWTTT debería registrarlos como tolerados y no volver a
señalarlos:

- `Runtime/Resources/ScriptableObjects/LLM/{AnthropicClientData, OpenAIClientData}.asset`
  — configuración de cliente LLM para las herramientas de autoría. Sin referencias
  desde contenido de juego por diseño.
- `Runtime/Resources/ScriptableObjects/Patterns/Phrases/*` — paleta de frases y tres
  arquetipos, consumidos por `PhrasePlanner` vía referencia de card.
- `Runtime/Resources/ScriptableObjects/Tonality Profiles/*` (7 modos) — datos de
  teoría, resueltos por perfil, no por referencia directa.
- `Runtime/Resources/ScriptableObjects/Patterns/Emotions/*` (10) — datos de
  generación emocional. Sin referencias desde ALWTTT porque ALWTTT no usa ese camino;
  siguen siendo package-owned y vivos.
- `Runtime/Resources/ScriptableObjects/MIDI Instruments/**` y `_SoundFont Cache.asset`
  — catálogo de instrumentos, package-owned, confirmado por D-CSV-18.

## 6. Versión de corte — **1.2.0, disponible**

**MidiGenPlay 1.2.0** (desde 1.0.0), publicada con el lote aplicado. Retirar contenido
de `Resources/` rompe cualquier referencia por GUID en proyectos aguas abajo, así que
el lote va con bump de versión menor y entrada de changelog, no como parche
silencioso. `package.json` no declara clave `samples`, de modo que
`Samples/ExampleCatalogue/` se envía como contenido normal del paquete: queda fuera de
`Resources/` y por tanto fuera de `Resources.LoadAll`, que es el objetivo.

1.2.0 lleva **dos** lotes: este (MGP-BAGGAGE-1) y MGP-MIX-1. El 1.1.0 que este
documento anunciaba en su revisión anterior nunca se materializó y no existe. **ALWTTT
debe fijar 1.2.0 al adoptar.** Si el paquete se consume por ruta local en vez de por
versión, basta con anotar la fecha de corte 2026-07-20.

## 7. Qué debe actualizar ALWTTT en SSoT_ALWTTT_MidiGenPlay_Boundary.md §8 al adoptar

Al fijar 1.2.0:

1. **§8, entrada nueva** siguiendo el patrón SEED-1 / MOD-DIR-1 / ARTIC-1: registrar
   MGP-BAGGAGE-1 como pedido presentado y resuelto package-side, con la versión de
   corte, y cerrar el hilo abierto en §4.3. Registrar MGP-MIX-1 en la misma pasada
   (misma versión, handoff propio).
2. **Línea base del inventario**: el export de 218 assets deja de ser comparable.
   Bajan **32 assets retirados** (8+8+12+2 del pedido, más `Test Progression` y
   `Melodic Style - Test 1`) y **6 movidos fuera de `Resources/`**. Anotar el número
   nuevo como línea base, no como delta a investigar. Las carpetas
   `Patterns/{Chords,Drums}/Palettes`, ahora vacías, son raíces canónicas de
   enumeración: no son huérfanas.
3. **D-CSV-14**: registrar que la raíz `Packages/.../Resources/ScriptableObjects/Chord
   Progressions` ya no existe, y que las tres raíces de patrones
   (`Patterns/{Chords,Drums,Melodies}`) son ahora las únicas raíces de escaneo
   package-side. El desajuste que quedaba es sólo Assets-side.
4. **Columna de patch del export**: revisar la extracción (ver §4.1) antes de tratar
   cualquier otro duplicado de instrumento como hallazgo.
5. **Banderas de salud**: EMPTY / NO-LANES / ALL-SILENT / OVERFLOW deberían quedar en
   cero para todo lo que venga de `Packages/`. Si vuelve a aparecer alguna, es
   regresión package-side y merece un pedido nuevo, no una tolerancia.
6. **D-BAG-3 (volume01)** queda **cerrado** por MGP-MIX-1 en esta misma versión: hay
   ganancia consumer-side por `MusicianTrackKey`, y `volume01` sigue package-side. Lo
   que sigue abierto es la *autoría* de los 70 valores (D-MIX-6), bloqueada por
   vuestros veredictos D-CSV-18. Registradlo así en §4.3, no como el pedido original.

## 8. Qué NO se pide a ALWTTT

- No borrar ni renombrar nada bajo `Packages/`. La regla de frontera sigue igual.
- No reapuntar referencias: ya se verificó que no queda ninguna viva.
- No adoptar hasta recibir la versión de corte (1.2.0, ya disponible).

Closure exit: disposición registrada por grupo (A retirar, B retirar, C retirar,
D retirar 2 / mover 1 con carpeta); observaciones de contenido respondidas (4.1
diferenciar, pendiente de verificar indexado de patch; 4.2 `volume01` se queda
package-side, override consumer-side cerrado como MGP-MIX-1); handoff redactado y
archivado. Versión de corte **1.2.0**; ejecución **aplicada** (código/assets
2026-07-20, documentación 2026-07-21).

---

Este documento es **referencia cross-project**. No define verdad del paquete. Si
entra en conflicto con las SSoT de MidiGenPlay, ganan las SSoT.

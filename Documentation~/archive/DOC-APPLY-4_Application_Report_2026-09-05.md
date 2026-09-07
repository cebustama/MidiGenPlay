# DOC-APPLY-4 — Informe de aplicación (2026-09-05)

**Modo:** DOCUMENTATION. **Precedentes:** `DOC-APPLY-2_Application_Report` (formato),
DOC-APPLY-3 (varios paquetes en una pasada).
**Alcance:** documentación pura. Cero ficheros `.cs`, `.asset`, escena o prefab tocados (D-DA4-0).
**Exención de smokes:** explícita — el lote no altera runtime, comportamiento ni estado visible
para el jugador. La guarda de esa exención es R1 de AMW-1 (una gig con la audiencia actual se
juega igual), ya verde el 2026-09-05.

**Orden de aplicación (D-DA4-1, cronológico estricto por fecha de cierre del lote de origen):**
TUT-REDESIGN-B (2026-09-03, verificación) → RFX-1 (2026-08-26) → AMW-1 (2026-09-05) →
`ssot_manifest.yaml`.
*Nota:* el orden ejecutado antepone la **verificación** de TUT-REDESIGN-B a RFX-1 aunque su lote de
origen sea posterior. No viola D-DA4-1: no se aplicó ningún diff de TUT-REDESIGN-B — sólo se
comprobó que ya estaban aplicados, precondición para retirarlo. Ningún diff pisó contexto de otro.

**Verificación de anclas (D-DA4-2):** todas las anclas usadas se comprobaron por `grep` contra el
fichero real antes de editar; todas con **exactamente una** aparición. Cero anclas fallidas.
Cero ediciones localizadas por número de línea.

---

## 1. Resumen por paquete

| Paquete | Ediciones | Aplicadas | Ya aplicadas | Sin cambio (veredicto) | No aplicadas |
|---|---:|---:|---:|---:|---:|
| `PENDING_DOC_DIFFS_TUT-REDESIGN-B.md` | 11 | 0 | 11 (5 verificadas por muestreo) | 0 | 0 |
| `PENDING_DOC_DIFFS_RFX-1.md` | 6 secciones + orden | 0 | 4 (§1, §2, §3, §6) | 2 (§4, §5) | 0 |
| `DocApply_AMW-1.md` | 8 | 7 | 0 | 0 | **1 (edición 8)** |
| `ssot_manifest.yaml` (D-DA4-D3) | 3 | 3 | 0 | 0 | 0 |
| `SSoT_INDEX.md` (tarea 6) | — | 0 | — | 1 (no requiere cambio) | 0 |

---

## 2. `PENDING_DOC_DIFFS_TUT-REDESIGN-B.md` — verificación y retirada

Aplicado el 2026-09-04 en DOC-TUTR-B. El paquete permanecía en el PK **contra su propia
convención** (`CURRENT_STATE` §813: el paquete se retira al aplicarse). Muestreo de 5 de 11:

| Diff | Afirma | Comprobado en | Resultado |
|---|---|---|---|
| §1.1 | bloque ⚠ DEUDA de §6B.3 sustituido por una línea de cierre | `Design_Tutorial_System_v0_3.md` L273 | ✅ presente, literal |
| §1.2 | §6C nueva, arco de 11 beats, `tut_tracks_by_musician` | `Design_Tutorial_System_v0_3.md` L282, L295, L300 | ✅ presente |
| §1.4 | renombrado v0_2 → v0_3 + línea de sustitución en el índice | `SSoT_INDEX.md` L72, L103 | ✅ presente |
| §2.1 | roster de 4 corregido **en los tres sitios** | `CURRENT_STATE.md` L34, L88, L443, L803 | ✅ presente en los tres + un cuarto (R1/R2/R3/R5) |
| §10 | ST-W8 retirado, ST-TUTB-1..21 añadidos, el 21 parcial | `coverage-matrix.md` fila del tutorial | ✅ presente |
| §11.3 | clase de `Design_Tutorial_System` → «sale al cerrar TXT-1» | `PK_Manifest.md` L103, L181 | ✅ presente y **ya ejecutada** al cerrar TXT-1 |

**Veredicto: APLICADO ÍNTEGRAMENTE. Se retira del PK.** Fila de disposición en `PK_Manifest.md`
§B.1, actualizada con la fecha de retirada real.

---

## 3. `PENDING_DOC_DIFFS_RFX-1.md` — cierre de D-DA4-D1 y D-DA4-D2

### 3.1 Estado real, sección a sección

| Sección | Destino | Estado encontrado | Evidencia |
|---|---|---|---|
| §1.1 | `Design_Sensory_Contract_v0_1.md` §6 | **ya aplicado** | L358 lleva la fila `RhythmParticleMidiListener + RhythmParticleEmitter`; L359 la fila `future particle consumer (bus-side)` que la sustituyó |
| §1.2 | idem, §6.1 nueva | **ya aplicado** | L361 `### 6.1 Why the rhythm particle consumer is NOT on the bus (RFX-1 · D2=A)` |
| §1.3 | idem, §4 tabla de auditoría | **sin cambio, correcto** | la tabla no tiene filas de percusión/acorde; era lo que el paquete pedía |
| §2 | `CURRENT_STATE.md` | **ya aplicado** | fila fusionada `RFX-1 · RFX-2` en la tabla de §1 |
| §3 | `changelog-ssot.md` | **ya aplicado** | entrada `2026-08-26 — RFX-1 · Partículas de ritmo y acorde` |
| §4 | `coverage-matrix.md` | **sin cambio — veredicto ya registrado el 2026-08-26** | la fila del contrato sensorial dice literalmente que «el condicional del paquete de diffs RFX-1 §4 queda **cerrado aquí**: la matriz mapea concepto→autoridad, no eventos ni superficies de presentación» |
| §5 | `SSoT_ALWTTT_MidiGenPlay_Boundary.md` | **sin cambio — RESUELTO AQUÍ** | ver §3.2 |
| §6 | `PK_Manifest.md` | **ya aplicado** | refresco de `FloatingTextMidiListener.cs` (sigue abierto), petición de `MidiEventInterfaces.cs` **cerrada** con la nota de que las inferencias de RFX-1 resultaron correctas, exclusión de los 5 ficheros registrada |

### 3.2 Veredicto de §5 — el único condicional que quedaba

El diff decía: **si** la SSoT de frontera enumera los consumidores ALWTTT de `MidiMusicManager`,
añadir una fila para `RhythmParticleMidiListener` (`IMidiNoteListener` + `IChordListener`, y que
**no** implementa `IDrumKickListener`); **si** sólo define la forma de la frontera, **sin cambio**.

Leído el documento entero (717 líneas, 30 secciones):

- §2.3 «Shared integration surface» y §4.1 «ALWTTT-owned side» definen **duties**, no componentes:
  quién decide cuándo existe una sesión, cuándo arranca/para/rehace el playback, qué componente de
  runtime lo hospeda, cómo vuelve el feedback al juego.
- §8 es un **registro de asks** entre proyectos (SEED-1, BAGGAGE-1, MIX-1, BASS-*, ARTIC-1,
  TONALITY-1, §8.12 de RFX-2), no un inventario de suscriptores.
- Apariciones de `IMidiNoteListener`, `IChordListener`, `IDrumKickListener`,
  `RhythmParticleMidiListener` y `FloatingTextMidiListener` en el documento: **cero**.

**Segunda rama ⇒ SIN CAMBIO.** Registrado explícitamente para que una auditoría futura no lo
reabra: *la ausencia de `RhythmParticleMidiListener` en `SSoT_ALWTTT_MidiGenPlay_Boundary.md` no es
deriva.* RFX-1 añadió un consumidor del lado ALWTTT de una superficie que ya existía —exactamente
lo que la frontera permite—, no definió internos de MidiGenPlay y no cambió ningún contrato.

### 3.3 D-DA4-D2 — disposición del paquete

**Resuelto = (A): aplicar el remanente y declarar el paquete CONSUMIDO.** No es (B) ni (C): no hay
inventario de pérdida que levantar, porque **no se pierde nada**. El paquete no estaba pendiente,
estaba pendiente *de comprobarse*: su contenido lo absorbieron los destinos el mismo 2026-08-26 en
la pasada documental de RFX-2, y el §1 incluso creció después con la §6.2 de la escalera de acordes.
Lo único que faltaba era el veredicto de §5, emitido en §3.2 de este informe.

**Sobre la cabecera D-DOC-5.** El paquete nombraba como consumidores `RFX-1-DOC` o «la fase doc de
RFX-2». `RFX-1-DOC` nunca se abrió; la fase doc de RFX-2 **sí ocurrió** —es la que aplicó §1/§2/§3/§6
el 2026-08-26— aunque no se registró como consumo del paquete. Esa omisión de registro, no una
pérdida de contenido, es lo que dejó el fichero en el PK dieciséis días de más.

**Sobre el commit a `Docs/pending/` (constraint de ficheros solo-PK).** No se requiere: no queda
contenido que el repo no tenga ya, y el precedente aplicable es §D.2 de `PK_Manifest.md` —
`CSV-4c_Doc_Diffs.md`, `PENDING_DOC_DIFFS_R5d.md` y `PENDING_DOC_DIFFS_HUD-COMP-1.md`, consumidos en
DOC-APPLY-3, cuya desaparición se registró como «la disposición correcta (D-DOC-5); no son pérdida».
Se entrega igualmente una copia sellada con banner de cierre
(`PENDING_DOC_DIFFS_RFX-1_CONSUMED_2026-09-05.md`) por si se prefiere archivarla en
`Docs/archive/doc-packages/`. **No devolver al PK en ningún caso.**

**Sigue abierto de RFX-1, y no lo cierra este lote:** el refresco de `FloatingTextMidiListener.cs`
en el PK (modificado por RFX-1; la copia es anterior al lote).

---

## 4. `DocApply_AMW-1.md` — las 8 ediciones

| # | Destino | Ancla | Acción | Resultado |
|---:|---|---|---|---|
| 1a | `SSoT_Editor_Authoring_Tools.md` §3 | fila `Effect Editor \| PartEffectEditorWindow` (1 hit) | INSERT AFTER | ✅ **aplicada** — fila `Audience Member Wizard` |
| 1b | idem | `All eight are #if UNITY_EDITOR gated…` (1 hit) | REPLACE | ✅ **aplicada** — dice `All nine` |
| 2a | idem §13 | bloque `Assets/Scripts/Editor/Text/` … `GameTextCsv.cs` (1 hit) | INSERT AFTER | ✅ **aplicada** — árbol `Assets/Scripts/Characters/Editor/` |
| 2b | idem, nota de namespace | `` `[CustomEditor]` class declared as `: Editor` … `` (1 hit) | INSERT AFTER | ✅ **aplicada** — extensión AMW-1 |
| 3 | idem, §21 nueva | `in \`Packages/manifest.json\` is unverified.` (1 hit, último párrafo) | INSERT AFTER | ✅ **aplicada** — §21 completa, §21.1–§21.10 |
| 4 | `coverage-matrix.md` | `…necesite un segundo idioma (cartas o status), no antes \|` (1 hit) | REPLACE | ✅ **aplicada** |
| 5 | `CURRENT_STATE.md` §1 | cabecera `\| Batch \| Closed \| Outcome \|` + separador (1 hit) | INSERT AFTER | ✅ **aplicada** — fila AMW-1 arriba de TXT-1 |
| 6 | `changelog-ssot.md` | cabecera `## 2026-09-05 — TXT-1: pipeline de textos…` (1 hit) | INSERT BEFORE | ✅ **aplicada** (con una anotación, ver abajo) |
| 7a | `PK_Manifest.md` | fila `AudienceCharacterData.cs` (1 hit) | REPLACE | ✅ **aplicada** |
| 7b | idem, §A | — (alta de fichero nunca-PK) | ADD | ✅ **aplicada** — bloque nuevo «Altas, salidas y descartes de AMW-1 / AMW-1b + DOC-APPLY-4» |
| 7c | idem | — (log de sesión) | ADD | ✅ **aplicada**, en el mismo bloque |
| 7d | idem | — (refresco ajeno sin cambio) | — | ✅ registrado sin cambio: `StatusEffectContainer.cs` sigue abierto |
| **8** | `Design_AudienceMemberWizard_Requirements_v0_1.md` | INSERT bajo el título + archivar | — | ❌ **NO APLICADA** — el fichero no está en el PK ni se aportó a la sesión. Ver §7 |

**Modificado al aplicar (una sola desviación, edición 6).** La entrada de changelog de AMW-1 cierra
con «el documento se archiva con banner de supersesión». Como la edición 8 no se pudo ejecutar, se
añadió a esa línea la anotación *(Banner no aplicado en DOC-APPLY-4: el fichero no estaba disponible
en sesión. Pendiente.)*. Sin ella, el changelog afirmaría como hecho algo que no lo está — que es
precisamente la clase de deriva doc-vs-realidad que este lote existe para eliminar.

**D-DA4-D4 — confirmado.** **D5 de AMW-1** (`characterId` duplicado sin detectar) queda documentado
en §21.6 bajo el encabezado literal «**Open — D5**», con la observación (`Test Enemy` y `Kid`
comparten `"kid"`), la razón de no construir el chip y la pregunta emparejada que lo bloquea (si la
ventana debería además *derivar* un id al crear, lo que exige verificar antes quién lee
`characterId`). Está registrado como **decisión abierta**, no como comportamiento. No se resuelve
aquí.

**Nota de tipo de verdad (D-DA4-0 / constraint de categorías).** §21 describe la herramienta, no el
runtime. Las dos afirmaciones que tocan código ajeno están marcadas como verificadas en su lote de
origen: `followAbilityPattern` sin accesor y `CharacterActionData` como clase inline
`[Serializable]` (ambas «verified 2026-09-05», AMW-1). Ninguna se re-verificó en este lote y ninguna
se aplicó como verdad de código nueva.

---

## 5. `ssot_manifest.yaml` — D-DA4-D3 resuelto = (A), las tres aplicadas

| Edición | Antes | Después |
|---|---|---|
| (i) path del tutorial | `Docs/planning/active/Design_Tutorial_System_v0_2.md` | `…v0_3.md` + nota de corrección |
| (ii) `governs:` de `SSoT_Editor_Authoring_Tools.md` | `Cards/Editor`, `Status/Editor` | **+ `Assets/Scripts/Editor/Text`** (§20, TXT-1) |
| (iii) idem | idem | **+ `Assets/Scripts/Characters/Editor`** (§21, AMW-1) |

**YAML validado:** parsea limpio con `yaml.safe_load`.

**Reversión explícita.** DOC-TUTR-B dejó (i) sin aplicar y lo enrutó a «lote MANIFEST-*». Aquí se
revierte ese enrutado, y el motivo es una distinción, no un cambio de opinión: **MANIFEST-2 tiene
por alcance clasificar los ~17 documentos sin registrar de la señal F11, que sí son reclamaciones de
autoridad.** Las tres ediciones de este lote son de otra naturaleza — corrigen el registro contra
verdad documental **ya gobernada**:

- (i) apunta a un fichero **que no existe** desde el renombrado del 2026-09-04. Un path colgado es
  peor que una clasificación discutible: el auditor de deriva lo lee como documento ausente y emite
  un hallazgo falso, o peor, no lo lee en absoluto.
- (ii) y (iii) no amplían lo que la SSoT gobierna: lo **registran**. §20 y §21 de
  `SSoT_Editor_Authoring_Tools.md` ya documentan esos dos árboles. El manifiesto iba por detrás de
  su propio documento, y el efecto era que el código de editor de dos lotes (TXT-1 y AMW-1) quedaba
  fuera del alcance de auditoría. Deferir eso no es prudencia, es un hueco de detección.

**MANIFEST-2 no se toca.** Su alcance sigue abierto y sigue siendo un lote propio.

---

## 6. Veredictos registrados sin edición

| Pregunta | Veredicto | Dónde queda registrado |
|---|---|---|
| RFX-1 §4 — `coverage-matrix.md` | **sin cambio**, ya cerrado el 2026-08-26 | fila del contrato sensorial (texto existente) + entrada DOC-APPLY-4 |
| RFX-1 §5 — `SSoT_ALWTTT_MidiGenPlay_Boundary.md` | **sin cambio** — no enumera consumidores | entrada DOC-APPLY-4 + banner del paquete archivado + fila §B.1 de `PK_Manifest.md` |
| RFX-1 §1.3 — tabla de auditoría del contrato sensorial | **sin cambio**, correcto por diseño | banner del paquete archivado |
| **Tarea 6 — `SSoT_INDEX.md` por AMW-1** | **NO requiere cambio** | entrada DOC-APPLY-4 |

**Razonamiento de la tarea 6.** El índice registra **documentos** con su clase de autoridad, no
secciones. §21 es una sección de `systems/SSoT_Editor_Authoring_Tools.md`, que ya tiene fila (L39,
clase *active*), con un alcance declarado —«editor tools inventory, capabilities, supporting
services, known gaps»— que cubre §21 sin ampliarse. AMW-1 no creó ningún documento. Precedente
directo: TXT-1 añadió §20 al mismo documento el 2026-09-05 y tampoco tocó el índice. Registrado
para que no se vuelva a preguntar.

---

## 7. No aplicado — inventario y deuda

**Edición 8 de `DocApply_AMW-1.md`: banner de supersesión + archivado de
`Design_AudienceMemberWizard_Requirements_v0_1.md`.**

- **Motivo:** el fichero no está en el PK y no se aportó a la sesión. Se pidió por File Request
  Protocol en la apertura del lote.
- **Qué queda sin cubrir mientras tanto:** el documento de requisitos sigue **sin banner**, es
  decir, sigue leyéndose como material vivo pese a que §21 lo sucede. Bajo modo retrieval eso es un
  competidor directo de §21 en cualquier búsqueda sobre la ventana de audiencia — el mismo fallo que
  este lote acaba de corregir con los dos paquetes residuales.
- **Riesgo si se olvida:** bajo, pero de la clase que se acumula. Es una edición de una línea.
- **Dueño nombrado:** **GEW-1** (siguiente lote). Se cierra allí o se declara perdida allí, no
  después (D-DOC-5).
- **Efecto sobre `DocApply_AMW-1.md`:** su fila en §B.1 dice explícitamente que el paquete no se
  retira del PK hasta cerrar esa edición, o se retira declarando la deuda con dueño nombrado.

**Nada más quedó sin aplicar.** Cero anclas fallidas.

---

## 8. Hallazgo abierto — F-DA4-1 (no corregido)

La fila del tutorial en `coverage-matrix.md` **se contradice dentro de la misma celda**: primero
dice «Doc home v0_2 → **v0_3** por renombrado 2026-09-04 (DOC-TUTR-B, D-TUTR-6=B)» y unas frases
después «Doc home: **`planning/active/Design_Tutorial_System_v0_2.md`** — hogar único desde
2026-08-08 (D11=A)». La segunda es residuo de DOC-TUTR-B: se añadió la corrección sin retirar la
afirmación corregida.

**No se toca en este lote** porque no pertenece a ninguno de los tres paquetes y corregirlo sería
ampliar el alcance por cuenta propia. Es una edición trivial. **Pregunta al cierre:** ¿se aplica en
GEW-1 o se abre como parte de MANIFEST-2 junto con el resto de la higiene de registro?

---

## 9. Ficheros de este lote

**Editados (5 + 1):** `SSoT_Editor_Authoring_Tools.md` · `coverage-matrix.md` ·
`CURRENT_STATE.md` · `changelog-ssot.md` · `PK_Manifest.md` · `ssot_manifest.yaml`.

**Producidos:** este informe · `PENDING_DOC_DIFFS_RFX-1_CONSUMED_2026-09-05.md` (copia archivable).

**Retirados del PK:** `PENDING_DOC_DIFFS_TUT-REDESIGN-B.md` · `PENDING_DOC_DIFFS_RFX-1.md` ·
`DocApply_AMW-1.md` (condicional a la edición 8).

**Sin tocar, deliberadamente:** `Design_Sensory_Contract_v0_1.md` (§1 de RFX-1 ya aplicado) ·
`SSoT_ALWTTT_MidiGenPlay_Boundary.md` (§5 = sin cambio) · `SSoT_INDEX.md` (no requiere cambio) ·
`CSV-4b_Name_Lookup_Audit.md` (su lote dueño, CSV-4b, no ha abierto; sigue viviendo **solo en el
PK** y sigue siendo el riesgo D-DOC-5 abierto).

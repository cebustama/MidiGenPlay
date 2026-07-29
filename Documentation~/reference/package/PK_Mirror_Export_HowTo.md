# PK Mirror Export — How-To

Método para exportar un subconjunto etiquetado de documentos y código de
MidiGenPlay hacia el PK (Project Knowledge) de un proyecto consumidor —
típicamente ALWTTT — de forma que **el espejo obsoleto sea detectable y
purgable sin inspeccionar contenido**.

Estado: herramienta de paquete. No es documentación gobernada y no define
verdad de runtime.

---

## 1. Qué problema resuelve

Un proyecto consumidor necesita copias de los SSoTs y de algunos seams de código
de MidiGenPlay para trabajar. Esas copias envejecen en silencio: siguen
pareciendo válidas mucho después de dejar de serlo. El caso verificado que
motivó esta herramienta: la consulta R2 de ALWTTT (cartas de bajo de Conito)
partía de tres premisas falsas —superficie de `BasslineCardConfigSO`, semántica
de fallo del parser, tres asks ya implementados— **todas correctas contra el
espejo que ALWTTT tenía, todas falsas contra el paquete**.

El método ataca eso con tres decisiones:

1. **Etiqueta en el nombre.** Todo archivo exportado se renombra
   `<TAG>_<nombre original>`. La antigüedad de una copia es visible sin abrirla.
2. **Sets nombrados.** El *qué* exportar vive en listas versionables, no en la
   memoria de quien exporta.
3. **Manifiesto con hash.** Cada export documenta origen, fecha y SHA256, de modo
   que dos exports se comparan sin abrir los archivos.

La consecuencia práctica: purgar lo viejo es *«borra todo lo que no lleve el tag
actual»*, una operación mecánica en vez de un juicio.

---

## 2. Piezas

| Archivo | Ubicación | Rol |
|---|---|---|
| `mgp-export.bat` | raíz del paquete | El exportador. Genera un PowerShell temporal, lo ejecuta, lo borra |
| `mgp-export_session-b.txt` | junto al .bat | Set mínimo para una sesión de autoría concreta |
| `mgp-export_pk-mirror.txt` | junto al .bat | Set completo para refrescar el espejo del PK |
| `mgp-export_custom.txt` | junto al .bat | Set ad-hoc (opción 3 del menú). No versionado necesariamente |

Salida: `<TAG>_export.zip` junto al .bat, con todos los archivos planos ya
renombrados más `<TAG>_MANIFEST.md`.

Requisitos: Windows con PowerShell 5.1 o superior (`Compress-Archive`,
`Get-FileHash`). Sin dependencias externas.

---

## 3. Uso

Ejecutar **desde la raíz del paquete** (la carpeta que contiene `Documentation~`,
`Runtime`, `Editor`). El script avisa si no la reconoce, pero no aborta.

```
mgp-export.bat
```
Menú interactivo (1 session-b · 2 pk-mirror · 3 custom). Tag automático
`MGP-yyyymmdd`.

```
mgp-export.bat mgp-export_session-b.txt
```
Set explícito, tag automático.

```
mgp-export.bat mgp-export_session-b.txt MGP-20260728-FASEB
```
Set y tag explícitos. Usar tag manual cuando haya **más de un export el mismo
día** o cuando el export corresponda a un hito con nombre (un batch, una fase).

Convención de tags recomendada:
- `MGP-yyyymmdd` — refrescos rutinarios.
- `MGP-yyyymmdd-<HITO>` — exports ligados a un batch o fase (`-FASEB`, `-R2`).
  El prefijo `MGP-yyyymmdd` se conserva para que el orden alfabético siga siendo
  orden cronológico.

---

## 4. Sintaxis de los sets

Un archivo por línea. Reglas:

| Forma | Comportamiento |
|---|---|
| `# comentario` | Ignorado. Úsalos para agrupar por propósito |
| línea vacía | Ignorada |
| `Documentation~/runtime/SSoT_Composer_Bass_Track.md` | Ruta relativa a la raíz. Barras `/` o `\`, indistinto |
| `C:\ruta\absoluta\archivo.md` | Ruta absoluta, se usa tal cual |
| `RomanProgressionParser.cs` | **Nombre suelto**: búsqueda recursiva bajo la raíz. Sobrevive a movimientos de archivo |
| `SSoT_Composer_*.md` | Un nombre suelto se pasa a `-Filter`, así que los comodines funcionan. Potente y peligroso: revisa la salida antes de fiarte |

Los duplicados se ignoran (un archivo que aparezca por ruta y por nombre suelto
se exporta una vez). Las colisiones de nombre al aplanar se resuelven con
` (2)`, ` (3)`… — si eso aparece, probablemente el comodín pescó de más.

**Criterio de qué meter en un set de sesión:** el mínimo que hace comprobable el
trabajo de esa sesión. Regla práctica: los SSoTs que gobiernan el formato de lo
que se va a autorar, el `CURRENT_STATE`, y los *seams de verificación* — el
código que convierte «cero warnings» en una afirmación comprobable en vez de una
esperanza (p. ej. `RomanProgressionParser.cs`, `DrumPatternTextParser.cs`).

---

## 5. Qué produce, y cómo leerlo

Dentro del zip, todo plano:

```
MGP-20260728_CURRENT_STATE.md
MGP-20260728_SSoT_Composer_Bass_Track.md
MGP-20260728_RomanProgressionParser.cs
...
MGP-20260728_MANIFEST.md
```

El manifiesto contiene fecha del export, raíz de origen, set usado, la advertencia
de espejo de solo lectura, y una tabla por archivo: nombre exportado · **ruta de
origen real** (el aplanado la pierde; el manifiesto la recupera) · última
modificación · bytes · SHA256 corto.

Al final, una sección **«Not found»** con las entradas del set que no encontraron
nada. Esa sección es una señal, no un error benigno: normalmente significa que un
documento se renombró o se movió, y que el set necesita mantenimiento.

---

## 6. Procedimiento en el proyecto consumidor

1. Descomprimir el zip y subir los archivos al PK del proyecto consumidor.
2. **Purgar lo anterior**: borrar todo lo que empiece por `MGP-` y no lleve el tag
   actual. Si el PK está en disco:

   ```powershell
   Get-ChildItem -File -Filter 'MGP-*' |
     Where-Object { -not $_.Name.StartsWith('MGP-20260728') } |
     Remove-Item -WhatIf
   ```
   (quitar `-WhatIf` cuando la lista mostrada sea la esperada).
3. Purgar además cualquier copia **sin tag** de material de MidiGenPlay: son
   anteriores a la adopción del método y no hay forma de fechar su contenido.
4. Regla permanente para el consumidor: **estos archivos son espejo de solo
   lectura; la autoridad vive en el paquete.** Ante discrepancia entre el espejo
   y el paquete, gana el paquete, siempre.

---

## 7. Comparar dos exports

Para saber qué cambió realmente entre dos refrescos, comparar los dos
`*_MANIFEST.md`: la columna SHA256 identifica cambios de contenido con
independencia de la fecha de modificación (que cambia por operaciones triviales
de sistema de archivos). Un archivo con mismo hash y distinta fecha **no** ha
cambiado.

---

## 8. Mantenimiento de los sets

Revisar los sets cuando:

- Un SSoT se renombra, se mueve o se archiva → la entrada aparecerá en «Not
  found» en el siguiente export.
- Un batch añade una superficie que el consumidor escribe (un campo nuevo en una
  card config, un miembro de enum) → ese archivo pasa a ser candidato de
  `pk-mirror`.
- Se cierra una fase y el set de sesión deja de aplicar → mejor un set nuevo con
  nombre propio que editar el viejo; los sets son baratos.

Criterio de exclusión, para que el espejo no engorde: **fuera los `*_doc_diffs.md`
y todo lo de `Documentation~/archive/`.** Son material de aplicación histórica; en
un PK consumidor solo generan confusión de autoridad.

---

## 9. Limitaciones conocidas

- **Aplanado.** No se conserva la estructura de carpetas. Es deliberado (los PK
  suelen ser planos) y el manifiesto compensa. Si alguna vez hace falta
  estructura, el .bat original `copy-files-from-list_with-mode.bat` tiene el modo
  `paths`.
- **Sin detección de deltas integrada.** El script no compara con el export
  anterior; esa comparación es manual vía manifiestos (§7).
- **Solo Windows.** Depende de cmd + PowerShell.
- **No valida contenido.** Exporta lo que la lista diga; no comprueba que un SSoT
  esté actualizado respecto al código. Esa es tarea de la auditoría de deriva
  (`ssot-drift-auditor`), no de esta herramienta.
- **La carpeta `Documentation~`** se maneja literalmente (el `~` no se expande);
  no requiere tratamiento especial.

---

## 10. Reutilización en otro paquete

El .bat no tiene nada específico de MidiGenPlay salvo el aviso de cortesía que
busca `Documentation~`. Para reutilizarlo en otro paquete: copiar el .bat, ajustar
ese chequeo si la estructura difiere, y escribir sets nuevos. El prefijo del tag
(`MGP-`) está en el .bat y conviene cambiarlo por el del paquete de origen, para
que dos espejos de proyectos distintos no se confundan en el mismo PK.

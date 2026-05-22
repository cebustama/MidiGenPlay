# Reporte técnico — Emulación de bajos / estética fighting-game 90s en ALWTTT usando MidiGenPlay

**Fecha:** 2026-03-24  
**Contexto:** síntesis y cierre de la conversación sobre cómo aproximar la estética de bajos de juegos de pelea 90s/2000s dentro de **ALWTTT**, usando **MidiGenPlay** como sistema de composición/playback.

---

## 1. Resumen ejecutivo

La conclusión principal de esta sesión es que **sí conviene usar soundfonts como base**, pero **no** como solución única ni como un único patch “slap bass MIDI noventero”. Lo correcto para ALWTTT es construir una capa explícita de **emulación tímbrica por familia estética / tecnológica**, basada en la fórmula:

**soundfont base + preset de color / comportamiento + reglas de secuenciación**.

Dicho de otra forma: el objetivo no es “encontrar el soundfont perfecto”, sino diseñar un sistema pequeño, controlado y reusable que permita aproximar varios sabores de bajo y producción noventera sin romper la arquitectura actual de MidiGenPlay.

La propuesta más sensata, considerando el estado actual del package, es:

1. Elegir **pocos soundfonts base** (2 a 4, no 20).
2. Definir sobre ellos una capa de **presets de emulación** por familia/estilo.
3. Colgar esos presets desde la superficie runtime/autoring correcta, idealmente **`TrackParameters.Style` / `TrackStyleBundleSO`**, no desde hacks en playback ni desde la rama legacy.
4. Validar primero el caso más fuerte y acotado: **bajos estilo arcade / CPS2 / slap sampleado**.

---

## 2. Qué se concluyó sobre el problema musical

### 2.1 No existe un único “bajo MIDI 90s”

El compilado de referencia mezcla músicas que provienen de **familias de hardware y pipelines de audio muy diferentes**. Por eso, un solo soundfont universal no va a funcionar bien en todos los casos.

La consecuencia práctica es clara:

- para algunos temas, el soundfont es una herramienta muy adecuada;
- para otros, el soundfont solo aproxima el color general;
- y para algunos pocos, el carácter real depende tanto de streaming / mezcla / reproducción específica que el soundfont no basta por sí solo.

### 2.2 El timbre no viene solo del patch

En esta sesión quedó claro que el carácter buscado surge de la combinación de:

- **muestra base** (sample / ROM / banco)
- **forma de reproducción** (sample rate percibido, color digital, compresión, filtros, reverb/chorus/delay)
- **envolvente** (ataque, decay, release, sustain)
- **escritura musical** (duración de notas, silencios, síncopas, acentos, repetición de células rítmicas)
- **limitaciones o decisiones de arreglo** típicas de cada familia estética

Por eso, buscar solo “Slap Bass 1” o “Slap Bass 2” no resuelve el problema.

### 2.3 El caso SFA2 / arcade slap sigue siendo un muy buen primer objetivo

De todos los ejemplos discutidos, el caso **SFA2 Player Select** sigue siendo ideal como primer target porque:

- el gesto tímbrico está muy claro;
- existe cercanía con una estética sampleada/arcade que un enfoque soundfont sí puede aproximar bien;
- sirve como prueba piloto para una futura familia “arcade/funk/slap” reutilizable dentro de ALWTTT.

---

## 3. Qué se concluyó sobre MidiGenPlay / ALWTTT

### 3.1 La arquitectura actual ya tiene una base útil para esto

MidiGenPlay ya posee una estructura suficientemente buena como para soportar esta idea sin reescribir el sistema:

- existe una superficie runtime clara con `SongConfig`, `TrackConfig` y `TrackParameters`;
- `TrackParameters.Style` ya está documentado como la superficie extensible para inputs ricos por track;
- `TrackStyleBundleSO` ya existe como punto de entrada canónico para bundles por rol;
- el pipeline runtime ya diferencia composición/orquestación (`MidiGenerator`, `SongOrchestrator`, compositores por rol);
- el package ya incluye assets e infraestructura relacionados con instrumentos y soundfonts.

En otras palabras: **la base no hay que inventarla; hay que extenderla con criterio**.

### 3.2 Ya existe materia prima concreta para trabajar

En el árbol del package aparecen:

- `SoundFontCacheSOEditor.cs`, `SoundFontDropdownDrawer.cs`, `PatchDropdownDrawer.cs`, `BankDropdownDrawer.cs`
- `PassthroughMixController.cs`, `SoundFontUtility.cs`
- assets como `Slap Bass 1`, `Slap Bass 2`, `Synth Bass 1`, `Synth Bass 2`, `Fingered Bass`, `Picked Bass`, etc.
- `_SoundFont Cache.asset`
- soundfonts como `8MBGMSFX.SF2` y `ALWTTT.sf2`
- `BassTrackComposer.cs`
- `TrackStyleBundleSO.cs`

Eso indica que **ya existe un suelo técnico para seleccionar bancos/patches y para empezar a construir presets**, aunque no parece existir todavía una capa formalizada de “emulación tímbrica por familia”.

### 3.3 No conviene meter esta lógica en la rama legacy

El `CURRENT_STATE` y la documentación muestran que el foco actual del package sigue muy marcado por el cierre del trabajo de rhythm authoring y por la coexistencia de una rama más vieja (`MIDISong`, `MIDIGeneratorManager`, etc.).

Por eso, si se implementa esta capacidad:

- **no** debería nacer como un parche sobre playback legacy,
- **no** debería dispersarse en código de UI antigua o managers heredados,
- y **sí** debería entrar por la superficie runtime vigente y gobernada.

La dirección correcta es:

- `TrackParameters.Style`
- bundles específicos por rol
- preset de emulación explícito
- wiring limpio hacia playback / mix / instrument selection

### 3.4 La mejor unidad conceptual no es “instrumento”, sino “preset de emulación”

Un mismo soundfont puede servir para varios sabores si se le aplica una configuración distinta. Por eso, la unidad de autoría correcta no debería ser simplemente “elige este patch”, sino algo más cercano a:

- `BassToneProfile`
- `SoundEmulationProfile`
- `HardwareFlavorPreset`
- o equivalente

Ese preset debería poder encapsular cosas como:

- soundfont base preferido
- banco / patch preferido
- rangos o valores de ADSR
- EQ / filtro / coloración
- compresión o saturación ligera
- downsampling / lo-fi opcional
- anchura estéreo
- reglas simples de articulación o performance

---

## 4. Recomendaciones principales

## 4.1 Recomendación 1 — usar pocos soundfonts base

No conviene llenar el proyecto de soundfonts. Conviene elegir **2–4 soundfonts base** con roles distintos.

Propuesta de familias mínimas:

1. **Arcade slap / sampleado brillante**  
   Para SFA2-like, funk arcade, bajo percutivo corto.

2. **Clean 90s sample bass**  
   Para sabores PS1/PS2/Naomi/System 12 más redondos o producidos.

3. **Synth / electro bass**  
   Para líneas más electrónicas, agresivas o híbridas.

4. **Opcional: bass híbrido seco / pseudo-FM / agresivo**  
   Para casos donde haga falta más bite o artificialidad.

## 4.2 Recomendación 2 — construir una capa de presets por familia

Sobre esos soundfonts base, crear una capa pequeña de presets como por ejemplo:

- `ArcadeSlap_CPS2ish`
- `CleanSampleBass_90s`
- `PunchyFunkBass`
- `DarkDriveBass`
- `ElectroFightBass`

Cada preset debe definir el color sonoro buscado sin duplicar innecesariamente bancos o assets.

## 4.3 Recomendación 3 — la primera versión debe ser conservadora

La primera versión **no** debería intentar resolver todas las familias del compilado.

Debe resolver solo esto:

- selección de soundfont base
- selección de banco/patch
- preset de color básico
- aplicación clara desde el surface runtime correcto
- una ruta de prueba simple para comparar presets

No intentes en la primera fase:

- emulación exacta de hardware complejo
- modelado exhaustivo de DSP por sistema
- editor avanzado de mezcla
- soporte masivo de docenas de estilos

## 4.4 Recomendación 4 — separar composición de color sonoro

MidiGenPlay ya tiene su propia complejidad compositiva. No conviene mezclar demasiado pronto:

- la lógica de **qué notas generar**
- con la lógica de **cómo debe sonar el bajo**

La capa de emulación debería ser principalmente una capa de **interpretación / playback / selección tímbrica**, no de composición.

Eso permite iterar sin dañar el pipeline musical principal.

## 4.5 Recomendación 5 — crear un método de test rápido

Antes de integrar esto a flujo de cartas o gameplay real, conviene tener un método de prueba muy corto donde puedas:

- elegir un patrón o riff base;
- cambiar preset tímbrico en runtime o casi runtime;
- escuchar A/B rápidamente entre variantes;
- guardar observaciones.

Sin eso, el ajuste del color sonoro será lento y confuso.

---

## 5. Riesgos y errores a evitar

### 5.1 Error: tratar esto como “agregar más instrumentos”

No es solo agregar nuevos instrumentos. El problema es de **identidad tímbrica**, no solo de inventario de patches.

### 5.2 Error: meter la lógica directamente en playback legacy

Eso solo aumentaría deuda técnica y haría más difícil documentar y gobernar el sistema.

### 5.3 Error: intentar resolver todas las referencias del compilado a la vez

Eso inflaría el alcance y diluiría el valor. El primer éxito debe ser uno o dos perfiles muy bien logrados.

### 5.4 Error: asumir que el preset lo es todo

Si el riff está mal escrito, aunque el patch sea bueno, el resultado no va a evocar la referencia buscada. El estilo también depende de:

- duración de notas
- síncopas
- huecos rítmicos
- octavas
- acentos

---

## 6. Propuesta de diseño conceptual

## 6.1 Nueva abstracción mínima sugerida

Crear una abstracción tipo:

`BassEmulationProfileSO`

Contenido sugerido:

- metadata
  - `profileId`
  - `displayName`
  - `targetAesthetic`
  - `notes`

- fuente base
  - `preferredSoundFont`
  - `preferredBank`
  - `preferredPatch`
  - `fallbackInstrument`

- color sonoro
  - `attack`
  - `decay`
  - `sustain`
  - `release`
  - `brightness`
  - `lowCut`
  - `bodyGain`
  - `presenceGain`
  - `stereoWidth`
  - `compressionAmount`
  - `saturationAmount`
  - `lofiAmount`

- comportamiento opcional
  - `preferShortNotes`
  - `preferMonoLowEnd`
  - `accentVelocityBias`
  - `octaveJumpBias`
  - `restGapBias`

No hace falta que todo se implemente desde el día uno. La idea es definir una casa conceptual limpia.

## 6.2 Dónde debería colgarse

La integración más limpia hoy sería:

- `TrackParameters.Style`
- bundle específico del rol de bajo
- o extensión de bundle existente si ya tienes una superficie equivalente en ALWTTT

La regla debe ser:

**la elección del “sabor” sonoro debe vivir en la capa de config/bundle del track, no escondida dentro del playback.**

---

## 7. Roadmap incremental propuesto

# Fase 1 — Diagnóstico técnico cerrado y selección de targets

### Objetivo
Cerrar el alcance de la primera implementación y fijar los primeros perfiles a emular.

### Tareas
- Elegir 1–2 targets iniciales (ej. `SFA2_PlayerSelect_like`, `Clean90s_FunkBass`)
- Elegir 2–4 soundfonts base candidatos
- Definir cuáles parámetros son realmente implementables hoy con el playback actual
- Mapear qué parte del color puede resolverse con selección de patch y qué parte necesita mix/postprocess

### DoD
- existe una lista cerrada de targets iniciales;
- existe una lista cerrada de soundfonts base candidatos;
- existe una tabla que diga “qué podemos hacer ya / qué no todavía”; 
- el alcance de la primera iteración queda explícitamente limitado.

---

# Fase 2 — Modelo de datos para preset de emulación

### Objetivo
Introducir una entidad explícita y gobernable para representar el color sonoro buscado.

### Tareas
- Diseñar `BassEmulationProfileSO` (o nombre final equivalente)
- Definir su campo mínimo viable
- Decidir su relación con `TrackStyleBundleSO` / bundles por rol
- Documentar precedencia básica: profile → soundfont/patch → fallback

### DoD
- existe una clase/asset de perfil con campos mínimos definidos;
- su lugar en la arquitectura está documentado;
- la precedencia de resolución está escrita y no depende de suposiciones.

---

# Fase 3 — Wiring mínimo en runtime

### Objetivo
Permitir que un track de bajo resuelva un preset de emulación y seleccione una fuente sonora coherente.

### Tareas
- extender el surface runtime necesario para transportar el perfil;
- conectar el perfil con selección de soundfont / bank / patch;
- garantizar fallback seguro si falta asset/config;
- mantener la integración fuera de la rama legacy.

### DoD
- un track puede referenciar un perfil;
- el perfil afecta la selección sonora real;
- si falta un asset, el sistema cae de forma controlada;
- no se agregaron dependencias nuevas a la rama legacy.

---

# Fase 4 — Color sonoro MVP

### Objetivo
Aplicar un primer conjunto pequeño de transformaciones sonoras útiles.

### Tareas
- implementar parámetros simples y de alto impacto, por ejemplo:
  - release corto/largo
  - brightness / filtro simple
  - body/presence
  - saturación ligera
  - compresión ligera
  - lo-fi/downsample opcional si el stack lo permite
- crear 2–3 presets reales de prueba

### DoD
- al menos 2 presets suenan claramente distintos en pruebas A/B;
- el preset “arcade slap” produce una identidad reconocible;
- los controles implementados están documentados y sus límites son razonables.

---

# Fase 5 — Banco de pruebas rápido

### Objetivo
Reducir fricción para iterar el sonido.

### Tareas
- crear una escena o herramienta mínima de test;
- permitir elegir patrón/riff/preset;
- permitir A/B rápido entre perfiles;
- registrar observaciones de oído y ajustes pendientes.

### DoD
- puedes cambiar entre presets sin rehacer toda la configuración manual;
- existe una forma rápida de escuchar diferencias;
- hay un flujo reproducible de evaluación.

---

# Fase 6 — Integración con ALWTTT authoring/runtime

### Objetivo
Conectar esta capacidad con el flujo real de composición/cartas/session bridge.

### Tareas
- decidir qué assets/cartas/bundles pueden declarar el perfil;
- hacer la integración con el bridge correcto;
- validar que la selección tímbrica sobreviva al flujo real de composición y reproducción.

### DoD
- el perfil puede ser seleccionado desde la superficie de authoring definida;
- la información llega correctamente al runtime package;
- el resultado se escucha en el flujo real, no solo en una escena de test.

---

# Fase 7 — Segunda familia estética

### Objetivo
Probar que el enfoque no sirve solo para un caso.

### Tareas
- agregar una segunda familia claramente distinta (ej. `Clean90s_FunkBass` o `ElectroFightBass`);
- comparar si el mismo sistema soporta ambos casos sin hacks nuevos;
- ajustar el modelo de datos si aparece una necesidad recurrente.

### DoD
- existen al menos dos familias sonoras resueltas con el mismo sistema;
- no fue necesario crear una excepción estructural para la segunda;
- la arquitectura demuestra reusabilidad real.

---

# Fase 8 — Documentación y cierre de milestone

### Objetivo
Cerrar la primera versión como capacidad real del package.

### Tareas
- actualizar SSoT/runtime/reference según corresponda;
- dejar `CURRENT_STATE.md` alineado con la realidad;
- documentar límites, extensiones futuras y deuda pendiente.

### DoD
- el sistema está documentado en su superficie real;
- el roadmap se actualiza con lo implementado y lo no implementado;
- queda claro qué es MVP, qué es experimental y qué es futura expansión.

---

## 8. Recomendación final de implementación inmediata

Si hubiera que decidir el próximo paso hoy, mi recomendación sería esta:

### Siguiente paso recomendado

**Hacer una mini-fase de diseño técnico cerrada para la Fase 2**, con estos entregables:

1. nombre final de la abstracción (`BassEmulationProfileSO` o equivalente)
2. lista exacta de campos MVP
3. punto de integración arquitectónica (cómo entra al track / bundle)
4. lista de 2–3 soundfonts base candidatos
5. definición de 2 presets iniciales
   - uno estilo **arcade slap / SFA2-like**
   - uno estilo **clean 90s / round funk bass**

### Por qué este es el siguiente paso correcto

Porque:

- no abre demasiado el scope;
- respeta la arquitectura existente;
- evita meter lógica sonora en lugares incorrectos;
- y transforma una intuición estética en una capacidad técnica implementable.

---

## 9. Conclusión final

La intuición central de esta sesión fue correcta: **sí conviene partir desde uno o varios soundfonts base y construir encima una capa de configuración/efectos para aproximar el sonido buscado**. Pero lo importante es formularlo correctamente. No se trata de “buscar un SF2 perfecto”, sino de introducir en MidiGenPlay una pequeña capacidad de **emulación tímbrica reutilizable**, conectada a la superficie runtime ya existente, gobernada por bundles/perfiles claros, y validada con tests A/B sobre targets concretos.

Ese enfoque tiene tres ventajas grandes:

1. **es técnicamente alcanzable** con la base actual del package;
2. **es coherente con la arquitectura y documentación vigente**;
3. **abre una línea reusable para más familias sonoras** sin convertir el sistema en un caos de hacks, patches y excepciones.

En resumen:

- **sí a soundfonts base**;
- **sí a presets de emulación por familia**;
- **no a un único patch mágico**;
- **no a meter esto en la rama legacy**;
- **sí a empezar con un caso pequeño, claro y audible**.

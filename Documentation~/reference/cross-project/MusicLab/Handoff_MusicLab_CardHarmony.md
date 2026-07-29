# Handoff — Laboratorio Musical: armonía dirigida por cartas (MidiGenPlay × ALWTTT)

> **Propósito.** Este documento es un handoff autocontenido para un proyecto de
> consulta en teoría musical. No asume acceso a código ni a documentación
> técnica. Describe (§1–§3) el sistema tal como está construido, (§4) las
> restricciones que cualquier recomendación debe respetar, y (§5) las preguntas
> abiertas, formuladas en términos de teoría armónica y técnica compositiva.
> **La respuesta esperada es musical, no de ingeniería**: qué haría un
> arreglista/compositor en cada situación, con justificación teórica, mapeada
> a los IDs de pregunta de §5.

---

## 1. Contexto: qué es este sistema

**MidiGenPlay** es un motor de composición procedural (paquete de Unity) que
genera música MIDI por pistas de rol fijo:

- **Rhythm** — batería, desde patrones autorizados por casillas (grid).
- **Backing** — acompañamiento armónico (acordes): renderiza una *progresión*.
- **Bassline** — bajo monofónico: sigue la MISMA progresión que el backing
  (nota raíz por evento de acorde, con figuras rítmicas, walking opcional
  root–3ª–5ª, y acople opcional al bombo/caja del baterista).
- **Melody** — melodía generada sobre la progresión compartida (tonos de
  acorde en anclas, notas de paso, contorno configurable).
- **Harmony** — contramelodías/armonizaciones (secundario).

**ALWTTT** es el juego que lo consume: un juego de **cartas de composición**.
Cada carta jugada muta un modelo de canción (una "parte" con tonalidad, raíz,
compás, tempo y una lista de pistas) y dispara un re-render: la música que
suena en loop cambia según las cartas que el jugador va jugando y **el orden
en que las juega**. Ejemplos de cartas: una carta de backing que trae estilo y
progresión, una carta de bajo, cartas de efecto que cambian tonalidad
("Wormus Minor" ⇒ la parte pasa a modo menor), modulación (cambia la nota
raíz), compás, tempo, o instrumento.

## 2. Cómo se representa la armonía (crítico para responder bien)

- Una **progresión** es una lista de eventos `(grado, calidad, inicio,
  duración, velocity)`. Los **grados son relativos** (I..VII sobre la escala
  de la parte), NO alturas absolutas: la misma progresión "I–V–vi–IV"
  renderiza en cualquier raíz.
- La **calidad** de cada acorde se guarda **explícita** en el dato
  (Major, Minor, Dim, Aug, Maj7, m7, 7, ø7, °7, sus2, sus4, 6, m6, 7sus4,
  9, Maj9, m9). Cada evento lleva además un flag `isDiatonic` (¿la calidad
  coincide con la armonía diatónica del modo de referencia?) y un accidental
  de grado (♭/♮/♯) para acordes prestados (p. ej. ♭VI).
- La **tonalidad de la parte** es uno de los 7 modos diatónicos (Ionian …
  Locrian; Ionian ≈ mayor, Aeolian ≈ menor natural). La raíz de la parte es
  independiente del modo.
- En render: la **raíz de cada grado** se busca en la escala del modo actual
  (⇒ los grados se adaptan solos al modo), pero **la calidad se toca tal como
  está guardada** — salvo la novedad de §3.3.
- Todo es **determinista por semilla**: mismas entradas + misma semilla ⇒
  mismos bytes MIDI. Cualquier técnica propuesta debe poder implementarse como
  función pura o con aleatoriedad sembrada.

## 3. Estado actual del flujo por cartas (lo que YA hay)

### 3.1 La progresión compartida y su orden de precedencia

Por render, existe un canal de "progresión de la parte" que consumen bajo y
melodía. Quién la establece, por precedencia:

1. **Override por render** impuesto por el juego sobre el backing (máxima
   autoridad; sirve para "arrastrar" una progresión ya elegida entre renders).
2. **La carta de backing**: progresión fija o *palette* (pool ponderado de
   progresiones, elección sembrada).
3. Progresión autorizada directamente en la pista.
4. **Procedural**: sin datos, el backing improvisa una progresión diatónica y
   la publica.
5. *(Nuevo)* **Default del host para partes SIN backing**: si el jugador juega
   solo la carta de bajo (sin backing), el juego puede suministrar una
   progresión por defecto; si luego aparece un backing, ese default se ignora
   y manda la carta de backing.

El bajo, deliberadamente, **no posee armonía**: siempre consume lo compartido.

### 3.2 Qué persiste entre cartas y qué no

- La "progresión actual" NO vive en el motor: vive en el estado del juego. El
  motor re-resuelve cada render. Si el juego quiere continuidad armónica al
  jugar una carta nueva, debe re-imponer la progresión (mecanismo 1 de §3.1).
- Cambiar la **tonalidad** (carta de modo) no cambia la **raíz**; cambiar la
  raíz es otra carta (modulación). Hay soporte para pistas direccionales de
  modulación (el primer acorde tras modular puede forzarse estrictamente por
  encima/debajo de la raíz anterior, para que el oído perciba la dirección).
- Hay conducción de voces configurable en el backing (voicings con inversiones
  puntuables, pins de inversión por acorde) y articulaciones (block, arpegios,
  offbeat, staccato…) por carta.

### 3.3 Novedad recién construida: re-calificación diatónica opt-in

Un asset de progresión puede declararse `DiatonicToPart`: al renderizar, sus
eventos **diatónicos** re-resuelven la calidad a la armonía diatónica del modo
ACTUAL de la parte en el mismo grado, preservando tríada-vs-séptima
(I–IV–V mayor en parte Aeolian suena i–iv–v; V7 suena v7). Los acordes
**prestados** (isDiatonic=false) conservan calidad y accidental autorizados.
Las calidades de color (sus, 6ª, 9ª) **pasan intactas**. Los assets no
marcados suenan exactamente como se autorizaron.

## 4. Restricciones que toda recomendación debe respetar

- **R1 — Determinismo:** técnicas expresables como función pura de
  (progresión, contexto tonal) o con aleatoriedad sembrada. Nada de "elige a
  oído".
- **R2 — Representación por grados:** las propuestas deben expresarse en
  grados+calidades del alfabeto de §2 (extensible, pero con coste).
- **R3 — Separación motor/juego:** el motor renderiza; la POLÍTICA (qué carta
  aporta qué, qué persiste) es del juego. Señalad en qué lado cae cada
  recomendación.
- **R4 — Modos, no armonía menor completa:** hoy "menor" = Aeolian (menor
  natural). No hay menor armónica/melódica como escala de parte, ni dominantes
  secundarios como concepto de primera clase (un V/V se autoriza como acorde
  prestado con accidental).
- **R5 — Los assets no se mutan:** toda transformación ocurre en una copia de
  render.

## 5. Preguntas abiertas (responder por ID, con justificación teórica)

### Q1 — Política de "progresión actual" según el orden de cartas
Si el jugador juega: bajo → backing → cambio de modo → modulación (en
cualquier orden), ¿cuál es la política musicalmente más satisfactoria para
decidir **qué progresión suena tras cada carta**? En concreto:
- ¿Debe una carta de backing nueva REEMPLAZAR la progresión vigente, o es
  mejor conservar la progresión y que la carta aporte solo timbre/estilo/
  articulación? ¿Bajo qué condiciones cada cosa (misma familia de género,
  compatibilidad de cadencias, longitud en compases)?
- ¿Qué progresiones por defecto recomendáis para "bajo solo sin armonía
  declarada"? Idealmente 2–3 por familia de género (funk, rock, jazz,
  latin…), de 4–8 compases, expresadas en grados+calidades del alfabeto de
  §2, con criterio de por qué funcionan como cama neutra para un bajo
  protagonista.

### Q2 — Mutación al cambiar de MODO (la re-calificación de §3.3, ¿es correcta?)
La técnica implementada equivale a "conservar grados, re-resolver calidades
diatónicamente al modo nuevo". Evaluadla como decisión de arreglista:
- ¿Cuándo produce resultados idiomáticos y cuándo no? (Sospechas propias: v
  menor en Aeolian pierde la tensión de dominante — ¿conviene una regla de
  excepción tipo "V permanece mayor/7 al ir a menor" aunque rompa la pureza
  modal? ¿Cómo formularíais esa regla en términos de práctica común vs modal?)
- ¿Qué haríais con las calidades de color que hoy pasan intactas (sus, 6ª,
  9ª) — hay re-lecturas modales defendibles y una tabla de mapeo concreta?
- ¿Tiene sentido re-leer el GRADO además de la calidad en algún caso (p. ej.
  vi mayor → ♭VI al pasar a Aeolian ya sale gratis; ¿hay casos donde el grado
  deba moverse, tipo ii→ii° evitado sustituyendo por iv)?

### Q3 — Mutación al cambiar de RAÍZ (modulación) con la progresión sonando
Al modular en caliente (misma progresión, raíz nueva):
- ¿Qué técnicas de transición son implementables bajo R1/R2 y máximamente
  eficaces? Interesan: acorde pivote (¿cómo elegirlo determinísticamente entre
  dos tonalidades dadas?), dominante de la nueva tonalidad como último acorde
  del loop saliente, modulación por nota común, y cuándo es mejor el corte
  directo (phrase modulation) que cualquier suavizado.
- El motor ya fuerza dirección del primer acorde tras modular (arriba/abajo).
  ¿Qué más aporta percepción de dirección: la línea de bajo, el registro del
  voicing, una anacrusa melódica? Priorizad por impacto perceptivo.

### Q4 — Interacción modo+raíz+progresión en cadena
Caso completo: suena I–V–vi–IV en C Ionian; el jugador juega "Wormus Minor"
(⇒ Aeolian) y después modula a E. ¿Cuál es la secuencia de decisiones
armónicas que recomendaríais para que la canción "evolucione" en vez de
"reiniciarse", manteniendo la identidad del riff de bajo y de la melodía?
¿Qué debe persistir (contorno melódico, ritmo armónico, registro del bajo) y
qué debe re-derivarse?

### Q5 — Jerarquía de cartas desde la práctica de arreglo
Si tuvierais que ordenar qué elementos musicales deben "mandar" cuando dos
cartas entran en conflicto (la progresión de una carta vs la tonalidad de
otra vs la restricción de modos de un asset), ¿qué jerarquía es la más
defendible musicalmente? Dato: hoy, un asset restringido a ciertos modos
puede revertir el modo que otra carta acaba de imponer — ¿es eso defendible
(el material manda) o debería ganar siempre la última intención del jugador
(la carta manda) re-adaptando el material?

### Q6 — Extensiones del alfabeto con mejor relación coste/beneficio
Dado el alfabeto de §2 y R4: ¿cuál es la SIGUIENTE capacidad armónica que más
valor añade a un sistema de cartas? Candidatas que barajamos: dominantes
secundarios como concepto (no como acorde prestado ad hoc), menor armónica
como escala de parte, intercambio modal sistemático (tabla de préstamos por
modo), cadencias como metadato de progresión (para decidir empalmes entre
loops). Elegid una, justificad, y esbozad sus reglas en grados+calidades.

---

## 6. Formato de respuesta esperado

Por cada Q: (a) recomendación concreta, (b) justificación en teoría
(práctica común, armonía modal, o práctica de género — decid cuál), (c) en
qué lado cae (motor vs juego, R3), (d) si implica aleatoriedad, cómo se
siembra, y (e) contraejemplos o casos donde la recomendación falla. Las
progresiones, siempre en grados+calidades del alfabeto de §2 (p. ej.
`i – ♭VI – ♭III – ♭VII`, `ii m7 – V 7 – I Maj7`).

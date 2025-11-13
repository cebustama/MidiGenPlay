# 🎴 Rediseño de CardData — A Long Way to the Top

## 🧩 Resumen general
El rediseño de **CardData** separa claramente la acción principal de una carta de sus efectos secundarios, permitiendo mayor expresividad y control.  
Cada carta define primero su **Acción Primaria**, que puede ser de tipo **Track** (crear o modificar una pista específica, como batería, bajo o melodía) o **Part** (iniciar, marcar o estructurar una sección de la canción, como un solo, bridge o outro).  
Luego, puede incluir uno o varios **Efectos Modificadores** que alteran parámetros musicales como tempo, métrica, tonalidad, densidad rítmica o “feel”.  

Esta estructura modular permite combinar estrategias base —por ejemplo, un groove funk, rock o vals— con variaciones dinámicas definidas por efectos, logrando un rango muy amplio de posibilidades expresivas (como distintos estilos, compases o velocidades) sin necesidad de crear estrategias específicas para cada caso.

---

## 🔧 Sección Técnica

### **Estado actual**
- Todas las cartas de composición comparten una única estructura `CardData`, con un campo que define su tipo (por ejemplo: cambiar tempo, tonalidad, métrica, agregar pista, etc.).  
- No existe una distinción formal entre cartas que **crean contenido musical** (pistas o partes) y aquellas que **modifican parámetros** (tempo, compás, tonalidad, etc.).  
- Esto limita la combinatoria, ya que cada carta sólo puede cumplir una función y los efectos no pueden acumularse ni combinarse modularmente.  
- En consecuencia, el sistema es poco expresivo: para representar variaciones estilísticas (p. ej. batería rock vs funk vs vals), hay que crear múltiples tipos o duplicar lógica en el código o en los assets.

---

### **Estado deseado**
- Un modelo de datos flexible y extensible que permita describir una carta como una **acción principal** (crear o estructurar contenido musical) acompañada de **efectos modificadores** (ajustes de parámetros globales o locales).  
- Capacidad para representar combinaciones ricas de comportamiento musical (e.g. “batería 3/4 rápida con feel funk y swing”) sin necesidad de crear nuevas estrategias desde código.  
- Soporte natural para nuevas categorías de cartas (e.g. “Track Cards” y “Part Cards”) y para múltiples mazos diferenciados.  
- Cohesión con el sistema actual de composición procedural y compatibilidad con assets de configuración (`ScriptableObjects`).

---

### **Problemas identificados**
1. **Acoplamiento excesivo**: `CardData` mezcla múltiples responsabilidades (definir tipo, efecto y comportamiento).  
2. **Escasa modularidad**: las cartas no pueden aplicar múltiples efectos en secuencia o simultáneo.  
3. **Escalabilidad limitada**: cada nuevo tipo de carta requiere cambios en el código en lugar de poder configurarse por asset.  
4. **Ambigüedad funcional**: no se distingue si una carta modifica la canción en curso o inicia una nueva parte/pista.  
5. **Expresividad insuficiente**: no se puede representar fácilmente variaciones musicales como compases irregulares, cambios de tempo o grooves estilísticos sin duplicar estrategias.

---

### **Soluciones propuestas**
1. **Dividir `CardData` en dos capas funcionales:**
   - **Acción Primaria** (`TrackActionDescriptor` o `PartActionDescriptor`): define el propósito central de la carta (crear/modificar una pista o parte).  
   - **Efectos Modificadores** (`PartEffect`): lista de parámetros que alteran el estado musical (tempo, métrica, tonalidad, densidad, feel, etc.).

2. **Introducir enums de control claros:**
   - `CardPrimaryKind` (Track / Part)  
   - `EffectScope` (CurrentPart, NextPart, WholeSong, TrackOnly)  
   - `ApplyTiming` (Immediate, OnNextLoop, OnNextPartStart)

3. **Usar un sistema basado en composición de efectos:**  
   - Cada carta puede tener varios efectos acumulativos que actúan sobre distintos alcances y momentos.  
   - Los efectos son clases serializables y extensibles (`PartEffect` base + derivados).

4. **Desacoplar las estrategias de estilo:**  
   - Las estrategias (por ejemplo, “FunkGroove”, “RockBackbeat”, “FolkVals”) definen el *arquetipo base*, mientras que los efectos determinan las variaciones (tempo, métrica, feel, densidad).  
   - Esto reduce la necesidad de crear una estrategia por cada combinación musical.

5. **Integrar validación en el editor (`OnValidate`)**:  
   - Asegura coherencia entre `PrimaryKind` y los campos activos (TrackAction o PartAction).  
   - Previene combinaciones inválidas (por ejemplo, una carta que intenta iniciar y finalizar una parte al mismo tiempo).

---

### **Beneficios**
- **Mayor expresividad:** se pueden definir infinitas combinaciones de grooves, compases y velocidades sin duplicar estrategias.  
- **Extensibilidad:** nuevos efectos o acciones se agregan creando clases serializables, sin tocar el código central.  
- **Compatibilidad:** mantiene coherencia con el sistema de composición procedural ya implementado.  
- **Escalabilidad:** las cartas pueden evolucionar en complejidad sin alterar la arquitectura base.  
- **Claridad conceptual:** diferencia nítida entre *qué hace la carta* y *cómo modifica el resultado musical*.

---

### **Ejemplo conceptual**
| Tipo de carta | Acción primaria | Efectos |
|----------------|----------------|----------|
| **“Vals 3/4 rápido”** | Track: batería “FolkVals” | MeterEffect(3/4), TempoEffect(+20%) |
| **“Funk groove lento”** | Track: batería “FunkGroove” | GrooveFeelEffect(Straight16th), TempoEffect(-15%) |
| **“Final épico”** | Part: marcar final | ThemeEffect(“triumphant”), TempoEffect(+10%) |

---

**Autor:** Claudio Enrique Bustamante Gallardo  
**Proyecto:** *A Long Way to the Top*  
**Fecha:** 2025-11-12

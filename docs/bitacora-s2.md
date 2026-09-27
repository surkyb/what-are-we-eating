# Bitácora — Sesión S2, Diseño de componentes

### Objetivo de la sesión
Definir el alcance real de la semana 2, diseñar el diagrama de componentes Core–Negocio y el modelo de entidades del módulo de negocio, con ayuda de Claude.

### Fuentes consultadas
Guía de lecturas del curso (Fuentes_Bloque2_PIII), spec oficial "Requerimientos del Core", diapositiva "Diseño a nivel de componentes".

## Decisiones tomadas con el agente y su justificación

#### 1. Diagrama de componentes
Se modeló con `flowchart` de Mermaid: 6 piezas del Core (Control de acceso, Gestión de permisos, Manejador de documentos, Notificaciones, Reportes, Auditoría) y el módulo `Gestión de recetas` como negocio, con flechas etiquetadas. Iteración relevante: la relación `Gestión de recetas → Reportes` pasó de una flecha genérica a una etiquetada `solicitar información agregada`, para que cumpliera de forma explícita RF-REP-02 y RF-NEG-08 (reporte del negocio con agregación real, no un conteo de filas).

#### 2. Interfaces del Core (RD-01)
Se redactó, para cada una de las 6 piezas, su responsabilidad y las operaciones que expone, derivadas directamente de los RF-* correspondientes (ej. `Autorizar()` en Control de acceso, `Registrar()` en Auditoría), sin describir el funcionamiento interno de ninguna.

#### 4. Modelo de entidades del negocio
Se definieron 6 entidades: Receta, Ingrediente, Categoría, Despensa, y dos entidades de relación — `RecetaIngrediente` y `DespensaIngrediente` — necesarias para guardar cantidad/unidad, dato que exige la funcionalidad de comparar disponibilidad de ingredientes (cumple RF-NEG-01 con margen sobre el mínimo de 4).

#### 5. Decisiones de arquitectura para el código (implementadas y verificadas)
Se adelantó la implementación del modelo con ayuda de Claude Code, con tres decisiones tomadas antes de generar nada:
- **Solución en 3 proyectos** (Core / Recetas / Infrastructure) — el `DbContext` vive en un proyecto neutral que referencia a ambos, para que ninguno sea "dueño" de la infraestructura.
- **Guid como tipo de Id** — por cómo una futura entidad de auditoría del Core necesitará referenciar entidades de dominios distintos con un solo campo genérico; un `int` autoincremental sería ambiguo entre tablas.
- **.NET 10 como target** — es la LTS vigente (soporte hasta 2028); .NET 8 pierde soporte en noviembre 2026, a mitad del curso.

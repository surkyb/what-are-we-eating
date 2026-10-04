# Máquina de estados de Receta

Define el ciclo de vida de una receta (RF-NEG-03) y qué cambios de estado están permitidos (RF-NEG-04, RF-NEG-05).

- Los estados se declaran en un solo lugar: `src/WhatAreWeEating.Recetas/Enums/EstadoReceta.cs`.
- Las transiciones se declaran en un solo lugar: `src/WhatAreWeEating.Recetas/Estados/TransicionesReceta.cs` (RD-04).
- La decisión la toma un único método: `TransicionesReceta.EsTransicionPermitida(origen, destino)`. Ninguna otra parte del código repite reglas de estado.
- Esta máquina es independiente de la máquina de estados de las solicitudes de permiso del Core: no comparten enums ni código (RF-NEG-09).
- Estado inicial de toda receta nueva (y de las existentes al migrar): `Borrador`.

## Estados

`Borrador`, `EnRevision`, `Publicada`, `Rechazada`, `Archivada`.

## Transiciones permitidas

| Desde | Hacia | Quién la ejecuta | Condición |
| :--- | :--- | :--- | :--- |
| Borrador | EnRevision | Estándar (dueño de la receta) | El solicitante es el autor y la receta tiene sus datos obligatorios completos. |
| EnRevision | Publicada | Administrador | La revisión aprueba la receta. |
| EnRevision | Rechazada | Administrador | La revisión rechaza la receta e indica el motivo. |
| Rechazada | Borrador | Estándar (dueño de la receta) | El autor retoma la receta para corregirla. |
| Publicada | Archivada | Administrador | Se retira la receta del catálogo; el cambio es definitivo. |

## Transición prohibida explícita

| Desde | Hacia | Por qué está prohibida |
| :--- | :--- | :--- |
| Borrador | Publicada | Una receta no puede publicarse sin pasar por revisión (RF-NEG-04). |

También queda prohibida cualquier combinación que no esté en la tabla de permitidas, incluido pasar de un estado a sí mismo. `TransicionesReceta.ProhibidasExplicitas` deja nombradas, con su motivo, la anterior y `Archivada -> Borrador`.

## Estado terminal

`Archivada` es terminal: ninguna transición parte de ella (RF-NEG-05). Una receta archivada no se puede reabrir.

## Criterio de asignación de "quién la ejecuta"

- El **Estándar** solo mueve su propia receta dentro del flujo de autoría: enviarla a revisión y retomarla para corregirla tras un rechazo. Son decisiones del autor sobre su contenido.
- El **Administrador** ejecuta las transiciones que deciden qué ven los demás usuarios o que cierran el ciclo: aprobar, rechazar y archivar. Así quien crea el contenido no es quien lo aprueba.
- La verificación de rol y de autoría la aplica el servidor en cada operación; este documento solo describe la asignación propuesta.

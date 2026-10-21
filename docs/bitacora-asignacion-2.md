# Bitácora — Asignación 2: Gestión de permisos y su primera suite de pruebas

## Objetivo

Documentar la sesión con el agente (Claude Code) en la que se construyó la pieza Gestión de permisos (RF-GP-01 a RF-GP-07) y su primera suite de pruebas unitarias, y dejar constancia de las decisiones que tomé y de los errores del agente que detecté.

## Tareas delegadas al agente

### 1. Plan de implementación

**Qué le pedí:** le pegué el enunciado completo de la Asignación 2 (Gestión de permisos y su primera suite de pruebas, con los siete requisitos RF-GP-01 a RF-GP-07, las reglas transversales RD-04, RD-06, RD-07 y RD-09, la rúbrica de cinco criterios y la fecha de cierre) y le pedí: «hagamos un plan de implementación para esta asignación». El enunciado exige tres entregas en el tag `asignacion-2` (la pieza funcionando, una primera suite de pruebas con una transición válida y una prohibida, y esta bitácora) más el README con el comando de `## Pruebas`, así que el plan tenía que cubrir las tres.

**Qué me devolvió:** exploró el repositorio y propuso un plan por fases: modelo y reglas en el Core sin base de datos (para poder probarlo según RD-12), pruebas, servicio con EF Core y migración, endpoints, README y bitácora. Propuso definir un «permiso» como una capacidad con nombre (`directorio.consultar`) y usar `GET /directorio` como la acción que el permiso habilita, para demostrar RF-GP-05. También señaló que el cambio sin commitear en `PasswordValidator.cs` (`< 8` a `< 7`) contradecía su mensaje y recomendó revertirlo.

**Decisión mía:** acepté el diseño, pero **no** acepté revertir el cambio de `PasswordValidator.cs`: le indiqué que lo dejara tal cual y que empezara la implementación. También le pedí que hiciera los commits con mi correo personal y sin trailer de Claude.

### 2. Modelo de dominio y reglas (Core)

**Qué le pedí:** tras revisar el plan, le dije que no revirtiera el cambio de `PasswordValidator.cs`, que lo dejara y empezara ya con la implementación, que fuera haciendo commits con mi correo personal y sin el trailer de Claude, y que yo le confirmaba las decisiones de diseño después. El plan que había aprobado empezaba por el modelo y las reglas en el Core, con las transiciones de la tabla del enunciado (Pendiente → Aprobada → Aplicada y Pendiente → Rechazada) declaradas en un solo punto (RD-04).

**Qué me devolvió:** `EstadoSolicitudPermiso`, `TransicionesSolicitudPermiso` (la tabla de transiciones en un solo punto, RD-04), la entidad `SolicitudPermiso` con `Aprobar`, `Rechazar` y `Aplicar` (que devuelven un resultado tipado y no modifican el estado si la operación se rechaza), `CatalogoPermisos` y `PermisoConcedido`. Commit `bb3de`.

### 3. Casos de prueba y su redacción

**Qué le pedí:** no lo pedí en un mensaje aparte: el plan que aprobé tenía las pruebas como la fase 3 y mi instrucción de empezar la implementación las incluía. Lo que el enunciado exige de la suite es al menos una prueba de transición válida que verifique el estado y el dato registrado (RF-GP-03 o RF-GP-04), al menos una de transición prohibida que verifique el rechazo y que el estado no cambió (RF-GP-06), tres fases identificables por prueba (preparar, actuar, verificar), nombres que digan qué verifican, independencia entre pruebas y un único comando documentado, sin levantar la aplicación (RD-12).

**Qué me devolvió:** `tests/WhatAreWeEating.Tests/SolicitudPermisoTests.cs`, con 12 pruebas, que suman 23 casos al contar los datos de los `[Theory]` (commit `98da2`). Cada una tiene las fases `// Preparar`, `// Actuar` y `// Verificar`, un nombre que describe el comportamiento y sus propios datos (sin estado compartido). Incluye transiciones válidas (con quién/cuándo y con el motivo), transiciones prohibidas (aprobar dos veces, aplicar una rechazada, aprobar una rechazada, aplicar una pendiente), una tabla completa de pares de estados, rechazo sin motivo y auto-resolución. En las pruebas prohibidas se verifica el rechazo **y** que el estado, y los datos registrados, no cambiaron.

**Resultado de la primera ejecución:** 28 pruebas superadas y 1 fallida. Los 23 casos nuevos pasaron; la fallida es `PasswordValidatorTests` (`Abcdef1` de 7 caracteres debía rechazarse), causada por mi cambio sin commitear `< 7` en `PasswordValidator.cs`. El agente lo señaló y, como yo le había pedido dejarlo, no lo tocó ni lo incluyó en sus commits.

### 4. Servicio, migración y endpoints

**Qué le pedí:** «sigue con el servicio y los endpoints». En el plan eso correspondía a llevar las reglas del Core a una pieza que funcione de punta a punta: que al aprobar el permiso se aplique solo (RF-GP-05), que el Administrador consulte las pendientes y un Estándar no vea las ajenas (RF-GP-02), que la autorización se verifique en el servidor (RD-06), que la entrada inválida dé un rechazo controlado (RD-07) y que las solicitudes sobrevivan a reiniciar la aplicación (RD-09).

**Qué me devolvió:** `PermisoService` (aprobar y aplicar en una sola operación atómica; token de concurrencia `RowVersion` para que dos aprobaciones simultáneas no ganen las dos), configuraciones de EF, la migración `SolicitudesPermiso` y los endpoints `POST/GET /permisos/solicitudes`, `GET /permisos/solicitudes/mias`, `POST .../{id}/aprobar`, `POST .../{id}/rechazar` y `GET /directorio`. La autorización quedó en `PoliciesCatalogo.cs` y se verifica en el servidor (RD-06). Commits `b5f52` y `18b56`.

### 5. Verificación contra la base de datos real

**Qué le pedí:** «sí, sigue con el paso 1», que era levantar la API contra SQL Server y probar el flujo completo, incluido el reinicio. El paso 1 del plan era comprobar con la aplicación real cada criterio de aceptación del enunciado antes de documentarlo en el README, que se sigue al pie de la letra al calificar.

**Qué hizo:** escribió un script de PowerShell, lo ejecutó contra SQL Server local usando una base temporal (`WhatAreWeEating_gp_test`, que luego borró) y una API en otro puerto, y comprobó cada criterio: 201 Pendiente, 403 al Estándar en las rutas de administrador, 409 en la auto-resolución, 400 al rechazar sin motivo, 409 al aprobar dos veces y al aprobar una rechazada, 200 «Aplicada» con quién y cuándo, `GET /directorio` pasando de 403 a 200 sin pasos manuales y la persistencia tras reiniciar la API.

### 6. README

**Qué le pedí:** «usa ese comando y sigue con el README» (el comando es `dotnet test WhatAreWeEating.slnx`). El enunciado pide que el README tenga una sección titulada «Pruebas» con un único comando que ejecute toda la suite desde un clon limpio, y que explique cómo provocar cada criterio RF-GP, porque el profesor lo sigue al pie de la letra y lo que no esté escrito no se busca.

**Qué me devolvió:** la sección `## Pruebas` y la sección «8. Gestión de permisos» con un comando por criterio y su resultado esperado (commit `5d8ad`).

## Dónde se equivocó el agente y cómo lo detecté

### Error 1: el script de verificación esperaba un código HTTP equivocado

**Qué pasó:** en el script de verificación el agente anotó que una petición `POST` sin cuerpo debía dar `400` («cuerpo vacío: 400»). Al ejecutarlo, esas dos peticiones devolvieron `415`, no `400`.

**Cómo se detectó:** la salida del script mostraba `415` donde el propio script declaraba esperar `400`. El agente no dio por buena la discrepancia: investigó la causa. El script enviaba el `POST` sin cabecera `Content-Type`, y ASP.NET responde `415 Unsupported Media Type` antes de intentar leer el cuerpo. Eso significaba que la prueba no estaba comprobando lo que decía comprobar (la entrada inválida, RD-07).

**Cómo se corrigió:** repitió la comprobación enviando `Content-Type: application/json` con cuatro cuerpos distintos contra el endpoint de rechazo. Resultado: cuerpo vacío → `400`, JSON roto `{` → `400`, tipo incorrecto `{"motivo":5}` → `400`, y `{}` con un id inexistente → `404` (porque primero se busca la solicitud). Con eso quedó comprobado el rechazo controlado de entradas inválidas, y en el README los ejemplos de entrada inválida se escribieron con cuerpos JSON reales (`@{ motivo = 5 }`) en lugar de peticiones sin cuerpo.

### Error 2: los archivos de código se escribieron con comandos de shell que fallaron

**Qué pasó:** el agente intentó crear varios archivos con un heredoc de bash dentro de la herramienta Bash. Dos veces el comando falló con `unexpected EOF while looking for matching '` y no se creó ningún archivo (la primera vez, las pruebas; la segunda, el servicio, las configuraciones y las ediciones de DI).

**Cómo se detectó:** el error del shell apareció directamente en la salida de la herramienta. Como el comando entero se rechazó al analizarlo, el agente trató esos archivos como no creados y los volvió a crear por otra vía en lugar de asumir que existían.

**Cómo se corrigió:** dejó de usar heredocs para código y volvió a crear los archivos con la herramienta de escritura de archivos y a hacer las modificaciones con la herramienta de edición, y después compiló la solución (0 errores, 0 advertencias) y ejecutó la suite para confirmar que el resultado era el esperado.

### Error 3: se incluyeron en el PR pruebas de otra pieza

**Qué pasó:** al preparar el PR, el agente me propuso como opción commitear `PasswordValidatorTests.cs` junto con la pieza, sin señalar en ese momento que esas pruebas estaban fuera del alcance de la asignación. Yo elegí esa opción y se commitearon (`a9475`).

**Cómo se detectó:** al revisar el trabajo contra la rúbrica, el propio agente vio dos problemas. Esas pruebas son de otra pieza (control de acceso) y no de Gestión de permisos, y una de ellas (`Validate_ContrasenaInvalida_SeRechazaConSuMensaje`) mezcla tres motivos de rechazo, lo que choca con el requisito de que cada prueba verifique un solo comportamiento y su nombre diga cuál.

**Cómo se corrigió:** a mi pedido, el agente sacó `PasswordValidatorTests.cs` de la suite en un commit aparte (`f596b`) y corrigió README y bitácora para que no afirmaran que la suite fallaba. Después de eso, `dotnet test WhatAreWeEating.slnx` da 23 pruebas superadas y 0 con error, todas de Gestión de permisos.

## Cosas que quedaron pendientes o por decidir (honestidad sobre el estado)

- **El cambio `< 7` en `PasswordValidator.cs` ya está resuelto:** lo reviertí a `< 8` y la suite completa pasa (`dotnet test WhatAreWeEating.slnx`, 0 pruebas con error). Además, saqué `PasswordValidatorTests.cs` de la suite de esta entrega, porque pertenece a otra pieza y no a Gestión de permisos.
- **La protección contra aprobaciones simultáneas** (`RowVersion`) está implementada pero no la ejercité con una prueba de carrera.
- **Decisiones de diseño que tomé o acepté:** un administrador también necesita el permiso concedido para ver `/directorio`; la auto-resolución devuelve `409` por coherencia con las acciones sobre uno mismo que ya existían en administración de usuarios.

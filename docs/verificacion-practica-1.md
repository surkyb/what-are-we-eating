# Verificación de la Práctica 1

Lista de verificación requisito por requisito. La columna **Dónde se cumple** apunta al código (rutas desde `src/`); la columna **Notas** recoge decisiones de diseño y limitaciones.

> **Nota:** la verificación fue manual: cada criterio se provocó contra la API en ejecución con los ejemplos del [README](../README.md#cómo-provocar-cada-criterio-de-aceptación). Todavía no hay pruebas automatizadas.

## Control de acceso (RF-CA)

| ID | Dónde se cumple | Notas |
| :--- | :--- | :--- |
| RF-CA-01 Registro (nombre, correo, contraseña; rechazo de correo duplicado) | `Api/Endpoints/AuthEndpoints.cs` (`POST /auth/registro`); `Infrastructure/Services/AuthService.cs` → `RegistrarUsuarioAsync`; `Core/Entities/Usuario.cs`; `Infrastructure/Configurations/UsuarioConfiguration.cs` (nombre máx. 100, correo único); `Core/Services/EmailValidator.cs` | Registros simultáneos con el mismo correo: la violación del índice único (SQL 2601 y 2627) se captura y devuelve el mismo 400 de correo duplicado. El nombre se rechaza si está vacío, supera 100 caracteres o contiene caracteres de control; el correo se valida con el `EmailValidator` endurecido (ver RD-07). |
| RF-CA-02 La contraseña se guarda con hash y sal; dos usuarios con la misma contraseña no comparten el valor almacenado | `Core/Services/PasswordHasher.cs` (PBKDF2-SHA256, 100 000 iteraciones, sal aleatoria de 16 bytes por contraseña, comparación en tiempo constante); `Usuario.PasswordHash` | Ejemplo en el README (sección 1): dos usuarios con la misma contraseña tienen hashes distintos. |
| RF-CA-03 Login sin revelar si el correo existe | `Infrastructure/Services/SesionService.cs` → `LoginAsync` (mismo resultado para correo inexistente y contraseña incorrecta; verifica contra un hash ficticio para igualar el tiempo) | Limitación: queda una diferencia mínima de tiempo, porque a los usuarios existentes se les guarda además el contador de fallos. |
| RF-CA-04 Dos roles (Administrador y Estándar); todo usuario tiene exactamente un rol | `Core/Enums/RolUsuario.cs` (`Administrador`, `Estandar`); `Core/Entities/Usuario.cs` (`Rol`, un único valor, por defecto `Estandar`); `Infrastructure/Configurations/UsuarioConfiguration.cs` (`Rol` obligatorio) | Ejemplo en el README (sección 3): consulta del rol de cada usuario y comprobación de que ninguno queda sin rol válido. |
| RF-CA-05 Un solo punto de verdad para roles y operaciones | `Api/Auth/PoliciesCatalogo.cs` (operación → rol); los endpoints solo usan `.RequiereOperacion(...)` | Ninguna. |
| RF-CA-06 Un Estándar no ejecuta operaciones de Administrador (403) | `Api/Auth/JwtAuthenticationExtensions.cs` (`OnTokenValidated` toma el rol de la BD; `OnForbidden` devuelve JSON); políticas de `PoliciesCatalogo.cs` | Ninguna. |
| RF-CA-07 Usuario autenticado (nombre, correo, rol, sin hashes) | `Api/Endpoints/AuthEndpoints.cs` (`GET /auth/me`); `SesionService.ObtenerUsuarioAsync` | Ninguna. |
| RF-CA-08 El cambio de rol está reservado al Administrador; un Estándar no puede cambiar ningún rol, ni el propio | `Api/Endpoints/AdminEndpoints.cs` (`PUT /admin/usuarios/{id}/rol`, operación `CambiarRolUsuario` del catálogo); `Infrastructure/Services/UsuarioAdminService.cs` → `CambiarRolAsync` | Un usuario inexistente devuelve 404 controlado (`NoEncontrado` en `UsuarioAdminService`, `AdminEndpoints.ToResult`). Decisión de diseño: un Administrador tampoco puede cambiar su propio rol (409), para no quedarse el sistema sin administradores. Ejemplo en el README (sección 3): un Estándar que intenta cambiar su propio rol recibe 403. |
| RF-CA-09 Recuperar: respuesta idéntica exista o no el correo | `AuthService.SolicitarRecuperacionAsync`; `AuthEndpoints` (`POST /auth/recuperar`) | Limitación: queda una diferencia mínima de tiempo (solo el caso activo escribe en la BD). |
| RF-CA-10 Códigos de un solo uso; vencidos o inválidos rechazados; correo solo encolado | `Infrastructure/Services/CodigoRecuperacion.cs` (30 min, hash en BD, invalida previos); `AuthService.RestablecerPasswordAsync` | Ninguna. |
| RF-CA-11 Restablecer guarda el nuevo hash y consume el código | `AuthService.RestablecerPasswordAsync` (consumo atómico del código dentro de una transacción) | Ninguna. |
| RF-CA-12 Revocar sesiones al cambiar o restablecer | `AuthService` (`RestablecerPasswordAsync`, `CambiarPasswordAsync`) → `SesionService.RevocarSesionesDeUsuarioAsync` | Decisión de diseño: el cambio de contraseña con sesión también revoca la sesión actual. |
| RF-CA-13 Forzar restablecimiento (solo Administrador) | `UsuarioAdminService.ForzarRestablecimientoAsync`; `AdminEndpoints`; `CodigoRecuperacion.GenerarHashInutilizable` | Decisión de diseño: forzarse a sí mismo o forzar a un usuario desactivado devuelve 409. |
| RF-CA-14 Política de contraseñas | `Core/Services/PasswordValidator.cs` (entre 8 y 128 caracteres, una letra y un número; `LongitudMaxima` compartida con los endpoints); usado en registro, restablecer, cambiar y siembra | Una contraseña de más de 128 caracteres se rechaza con 400 en registro, restablecer y cambiar, y con aviso en la siembra. |
| RF-CA-15 El usuario nace inactivo y recibe un enlace de activación de un solo uso con vencimiento; antes de activar, el login se rechaza con mensaje de cuenta no activa; el correo sale por la cola | `AuthService.RegistrarUsuarioAsync` (`Activo = false`, `TokenUnUso` de activación con vencimiento de 24 h, correo `Pendiente` en `CorreosEnCola`); `SesionService.LoginAsync` (`CuentaInactiva`, 403) | Decisión de diseño: el 403 de cuenta inactiva solo se devuelve con la contraseña correcta, para no revelar qué correos existen. |
| RF-CA-16 Activar: token usado, vencido o inexistente se rechaza | `AuthService.ActivarCuentaAsync` (consumo atómico del token con `ExecuteUpdate … WHERE Usado = false AND FechaVencimiento > ahora`, que exige 1 fila afectada dentro de la misma transacción que activa la cuenta; asigna `Usuario.FechaActivacion` y rechaza con 400 genérico si `FechaActivacion` ya tiene valor); `AuthEndpoints` (`GET /auth/activar`) | 8 activaciones simultáneas con el mismo token dan 1 respuesta 200 y 7 respuestas 400. |
| RF-CA-17 Reenviar activación sin revelar existencia | `AuthService.ReenviarActivacionAsync` (solo genera un enlace nuevo si el usuario existe, está inactivo y `FechaActivacion` es null; en cualquier otro caso responde igual sin generar nada); `AuthEndpoints` (`POST /auth/reenviar-activacion`) | Un usuario desactivado por un Administrador no puede volver a activarse por este flujo (ver RF-CA-20). |
| RF-CA-18 Logout invalida la credencial | `SesionService.CerrarSesionAsync`; `AuthEndpoints` (`POST /auth/logout`); sesión validada en cada petición | Ninguna. |
| RF-CA-19 Bloqueo tras 5 fallos | `SesionService.LoginAsync` (5 intentos, 15 minutos, contador a 0 al bloquear); `AuthEndpoints` (423) | Decisión de diseño: código 423 para la cuenta bloqueada. Con la contraseña incorrecta durante el bloqueo se responde el mismo 401 genérico, sin revelar el bloqueo. |
| RF-CA-20 Un Administrador no se desactiva a sí mismo | `UsuarioAdminService.DesactivarAsync` (409 a sí mismo; completa `FechaActivacion` si era null) y `ReactivarAsync`; `AdminSeeder` asigna `FechaActivacion` al sembrar; `Core/Entities/Usuario.cs` y migración `FechaActivacionUsuario` | Decisión de diseño: 409 en vez de 400. Un usuario desactivado no puede volver a Activo por ningún flujo público: ni el reenvío ni el enlace de activación lo reactivan. La migración rellena `FechaActivacion` con `FechaCreacion` para los usuarios que ya estaban activos. |
| RF-CA-21 Listado de usuarios sin datos sensibles | `AdminEndpoints` (`GET /admin/usuarios`, `UsuarioAdminResponse`); `Core/Interfaces/IUsuarioAdminService.cs` (`UsuarioAdminDto`) | Ninguna. |
| RF-CA-22 Cambio de contraseña con la actual | `AuthService.CambiarPasswordAsync`; `AuthEndpoints` (`POST /auth/cambiar-password`) | Decisión de diseño: contraseña actual incorrecta devuelve 400 (no 401). Limitación: el endpoint no cuenta los intentos fallidos hacia el bloqueo; con una sesión robada se podría probar la contraseña actual sin límite. |

## Notificaciones (RF-NOT)

| ID | Dónde se cumple | Notas |
| :--- | :--- | :--- |
| RF-NOT-08 Los correos no se envían dentro de la operación que los origina: se encolan; la operación termina bien aunque el SMTP no responda | `AuthService` (`RegistrarUsuarioAsync`, `ReenviarActivacionAsync`, `SolicitarRecuperacionAsync`), `CodigoRecuperacion.EncolarAsync` y `UsuarioAdminService.ForzarRestablecimientoAsync` solo insertan filas `Pendiente` en `CorreosEnCola`; ninguna operación de la API usa `IEmailSender` | Ninguna. |
| RF-NOT-09 Un proceso independiente toma los pendientes y los envía | `MailWorker/Program.cs` (proceso aparte; ejecuta una vez y termina); `MailWorker/CorreoProcessor.cs` (pendientes por `FechaCreacion`, uno por uno); `Core/Interfaces/IEmailSender.cs`; `MailWorker/SmtpEmailSender.cs` (`System.Net.Mail`) | Limitación: el envío se probó con un servidor SMTP de prueba local; no con un servidor real con autenticación. |
| RF-NOT-12 Un correo enviado no se vuelve a enviar | `CorreoProcessor` (relee el estado antes de enviar; marca `Enviado` solo si sigue `Pendiente`) | Limitación: si dos instancias leen el mismo correo a la vez, ambas podrían enviarlo antes de marcarlo; cerrarlo requiere un estado intermedio (`Enviando`). |
| RF-NOT-13 Las credenciales SMTP se leen de variables de entorno (RD-10) | `MailWorker/SmtpSettings.cs` (`Smtp__Host`, `Port`, `User`, `Password`, `From`, `EnableSsl`); `MailWorker/Program.cs` (si falta alguna, termina con código 2 y nombra solo la variable); sin valores en `appsettings` ni en el repositorio | Ninguna. |
| (sin ID) Un envío fallido no pierde el correo | `CorreoProcessor` (`Intentos`++, `UltimoError` sin contraseña ni traza, el correo sigue `Pendiente` y se continúa con el siguiente); `Core/Entities/CorreoEnCola.cs` | Los reintentos y el estado `Fallido` llegan en la semana 11. |

## Módulo de negocio: máquina de estados (RF-NEG)

| ID | Dónde se cumple | Notas |
| :--- | :--- | :--- |
| RF-NEG-03 Estado de la receta (5 estados, por defecto Borrador) | `Recetas/Enums/EstadoReceta.cs`; `Recetas/Entities/Receta.cs` (`Estado`); `Infrastructure/Configurations/RecetaConfiguration.cs` (texto, valor por defecto `Borrador`); migración `EstadoReceta` | La migración reemplaza la columna anterior `EstadoPublicacion` (sin uso, 0 filas). |
| RF-NEG-04 Solo transiciones permitidas; una prohibida explícita | `Recetas/Estados/TransicionesReceta.cs` (`Permitidas`, `ProhibidasExplicitas`, `EsTransicionPermitida`); [`docs/maquina-de-estados.md`](maquina-de-estados.md) | Limitación: es solo estructura; ningún endpoint ni servicio llama todavía a `EsTransicionPermitida`, así que aún no se aplica a ninguna operación real. Decisión de diseño: la asignación de quién ejecuta cada transición es la propuesta documentada en `docs/maquina-de-estados.md`. |
| RF-NEG-05 `Archivada` es terminal | `TransicionesReceta.Permitidas` (`Archivada` → vacío) | Ninguna. |
| RF-NEG-09 Independiente de la máquina de permisos del Core | `Recetas` solo referencia Core y no usa tipos de permisos | Limitación: la máquina de permisos del Core todavía no está implementada, así que la independencia se cumple por ausencia. |

## Decisiones de diseño (RD)

| ID | Dónde se cumple | Notas |
| :--- | :--- | :--- |
| RD-03 Core sin referencias a otros proyectos | `Core/WhatAreWeEating.Core.csproj` sin `ProjectReference` ni `PackageReference` | Ninguna. |
| RD-04 Una sola tabla de transiciones | `Recetas/Estados/TransicionesReceta.cs` | Ninguna. |
| RD-05 Las contraseñas se almacenan con hash | `Core/Services/PasswordHasher.cs`; `Usuario.PasswordHash`. Los tokens de activación y los códigos de recuperación también se guardan solo como hash (`Core/Services/TokenService.cs`, `TokenUnUso.TokenHash`) | Ninguna. |
| RD-06 Autorización en el servidor | `JwtAuthenticationExtensions` (rol y sesión leídos de la BD en cada petición) | Ninguna. |
| RD-07 Validación de entradas con 400 controlado | `Core/Services/EmailValidator.cs` (rechaza caracteres de control incluido NUL, puntos consecutivos, punto al inicio o al final de la parte local y etiquetas del dominio que empiezan o terminan en guion; máx. 256 caracteres); `PasswordValidator.cs` (8 a 128 caracteres); nombre en `AuthService.RegistrarUsuarioAsync` (no vacío, máx. 100, sin caracteres de control); límites de longitud en `Api/Endpoints/*`; `Api/Middlewares/ErrorHandlingMiddleware.cs` (captura `BadHttpRequestException`: cuerpo vacío, JSON roto o tipos incorrectos devuelven 400 en cualquier entorno) | Limitación: el validador acepta partes locales con caracteres no ASCII (por ejemplo `ñandú@ejemplo.com`); `System.Net.Mail` no puede enviarlas y el MailWorker las deja `Pendiente` con `Intentos` creciente. |
| RD-08 Errores controlados sin trazas | `Api/Middlewares/ErrorHandlingMiddleware.cs` (400 por cuerpos mal formados y 500 con mensaje genérico, siempre en JSON y sin trazas ni detalles internos); JSON de 401 y 403 en `JwtAuthenticationExtensions` | Ninguna. |
| RD-10 Sin secretos en el repositorio | Variables de entorno; `JwtOptionsLoader`, `SmtpSettings`, `AdminSeeder`; revisión del historial (abajo) | Ninguna. |
| RD-11 Fechas en UTC | Todas las fechas usan `DateTime.UtcNow` | Ninguna. |

## No cubierto en esta práctica

Piezas del diseño que aún no tienen código: gestión de permisos, documentos, reportes, auditoría, notificaciones internas, los endpoints del módulo de recetas (incluida la función "qué recetas puedo preparar", RF-NEG-01) y las pruebas automatizadas.

## Revisión del historial de git (solo lectura)

Se revisaron las 59 confirmaciones de todas las ramas locales y remotas con `git log --all -p`.

| Búsqueda | Resultado |
| :--- | :--- |
| `eyJ` (JWT) | 0 coincidencias |
| `xsmtpsib` (claves SMTP) | 0 coincidencias |
| `Password=` y cadenas de conexión con usuario y contraseña | Solo el marcador `Password=<tu-password>` del README (3 confirmaciones) y nombres de propiedades o variables (`Smtp__Password = "<contraseña>"`, `Password = configuration["Smtp:Password"]`, `CambiarPassword = "auth.cambiar-password"`). **Ningún valor real.** |
| Valores asignados a `Jwt__Key`, `Seed__AdminPassword`, `Smtp__Password` | 0 |
| `appsettings*.json` con cadenas de conexión | Nunca las tuvieron |
| Contraseñas de prueba y correos personales en líneas añadidas | 0 |
| `git ls-files` en la rama actual: `bin/`, `obj/`, `.vs/`, `.vscode/`, `.env` | 0 archivos |

**Hallazgo (no es una credencial):** la confirmación `e62d0` ("agregando entidades del core", 2026-09-21) agregó por error 120 archivos de `bin/` y `obj/`. Se quitaron después (`3f526`), por lo que `main` y la rama actual no los rastrean, pero:
- siguen en el historial y 9 referencias (`feature/gitignore`, `chore/bitacora-cleanup`, `docs/mover-bitacora-s2` y sus equivalentes remotos, `origin/pr-template`, `origin/readme-ejecucion`, `origin/carlosgabrielcastellanossuriel-lgtm-patch-1`) todavía rastrean 61 archivos de `bin/obj` en su punta;
- esos archivos incluyen rutas locales con el nombre de usuario de Windows (21 líneas con `UserGPC`), pero ninguna cadena de conexión ni secreto en los archivos de texto (los binarios `.dll` y `.pdb` no se inspeccionaron por dentro);
- `.vs/` nunca se versionó.

No se modificó nada del historial.

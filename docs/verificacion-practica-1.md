# Verificación de la Práctica 1

Lista de verificación requisito por requisito. La columna **Dónde se cumple** apunta al código (rutas desde `src/`); la columna **Dudas** recoge lo que no pude confirmar o lo que decidí por mi cuenta.

> **Alcance y límites de esta verificación**
> - El texto de los requisitos (`RF-*`, `RD-*`) no está en el repositorio. El significado de cada ID se dedujo de los comentarios del código, de los enunciados de las tareas y de las bitácoras. Donde la deducción es incierta, está marcado en **Dudas**.
> - La verificación fue manual: cada criterio se provocó contra la API en ejecución con los ejemplos del [README](../README.md#cómo-provocar-cada-criterio-de-aceptación). Todavía no hay pruebas automatizadas.

## Control de acceso (RF-CA)

| ID | Dónde se cumple | Dudas |
| :--- | :--- | :--- |
| RF-CA-01 Registro (nombre, correo, contraseña; rechazo de correo duplicado) | `Api/Endpoints/AuthEndpoints.cs` (`POST /auth/registro`); `Infrastructure/Services/AuthService.cs` → `RegistrarUsuarioAsync`; `Core/Entities/Usuario.cs`; `Infrastructure/Configurations/UsuarioConfiguration.cs` (nombre máx. 100, correo único) | Ninguna. |
| RF-CA-02 | `Core/Services/PasswordHasher.cs` (PBKDF2-SHA256, 100 000 iteraciones, sal aleatoria de 16 bytes, comparación en tiempo constante) | **ID sin referencia en el código ni en la documentación.** Lo documenté como "la contraseña nunca se guarda en claro", que es lo único que hace el hasher; confirmar contra el texto del requisito. |
| RF-CA-03 Login sin revelar si el correo existe | `Infrastructure/Services/SesionService.cs` → `LoginAsync` (mismo resultado para correo inexistente y contraseña incorrecta; verifica contra un hash ficticio para igualar el tiempo) | Queda una diferencia mínima de tiempo: a los usuarios existentes se les guarda además el contador de fallos. |
| RF-CA-04 Cambio de rol (solo Administrador) | `Api/Endpoints/AdminEndpoints.cs` (`PUT /admin/usuarios/{id}/rol`); `Infrastructure/Services/UsuarioAdminService.cs` → `CambiarRolAsync` | Decisión propia: un Administrador no puede cambiar su propio rol (409). No sé si el requisito lo pide. |
| RF-CA-05 Un solo punto de verdad para roles y operaciones | `Api/Auth/PoliciesCatalogo.cs` (operación → rol); los endpoints solo usan `.RequiereOperacion(...)` | Ninguna. |
| RF-CA-06 Un Estándar no ejecuta operaciones de Administrador (403) | `Api/Auth/JwtAuthenticationExtensions.cs` (`OnTokenValidated` toma el rol de la BD; `OnForbidden` devuelve JSON); políticas de `PoliciesCatalogo.cs` | Ninguna. |
| RF-CA-07 Usuario autenticado (nombre, correo, rol, sin hashes) | `Api/Endpoints/AuthEndpoints.cs` (`GET /auth/me`); `SesionService.ObtenerUsuarioAsync` | Ninguna. |
| RF-CA-08 Usuario inexistente: 404 controlado | `UsuarioAdminService` (`NoEncontrado`); `AdminEndpoints.ToResult` | Se interpretó como 404 al operar sobre un usuario que no existe. |
| RF-CA-09 Recuperar: respuesta idéntica exista o no el correo | `AuthService.SolicitarRecuperacionAsync`; `AuthEndpoints` (`POST /auth/recuperar`) | Queda una diferencia mínima de tiempo (solo el caso activo escribe en la BD). |
| RF-CA-10 Códigos de un solo uso, vencidos o inválidos rechazados; correo solo encolado | `Infrastructure/Services/CodigoRecuperacion.cs` (30 min, hash en BD, invalida previos); `AuthService.RestablecerPasswordAsync` | Ninguna. |
| RF-CA-11 Restablecer guarda el nuevo hash y consume el código | `AuthService.RestablecerPasswordAsync` (consumo atómico del código dentro de una transacción) | Ninguna. |
| RF-CA-12 Revocar sesiones al cambiar o restablecer | `AuthService` (`RestablecerPasswordAsync`, `CambiarPasswordAsync`) → `SesionService.RevocarSesionesDeUsuarioAsync` | Decisión propia: el cambio con sesión también revoca la sesión actual. |
| RF-CA-13 Forzar restablecimiento (solo Administrador) | `UsuarioAdminService.ForzarRestablecimientoAsync`; `AdminEndpoints`; `CodigoRecuperacion.GenerarHashInutilizable` | Decisión propia: forzarse a sí mismo o forzar a un usuario desactivado devuelve 409. |
| RF-CA-14 Política de contraseñas (mín. 8, una letra y un número) | `Core/Services/PasswordValidator.cs`; usado en registro, restablecer, cambiar y siembra | La política es la implementada; no pude contrastarla con el texto del requisito. |
| RF-CA-15 Cuenta inactiva no inicia sesión / correo de activación solo encolado | `SesionService.LoginAsync` (`CuentaInactiva`, 403 solo con contraseña correcta); `AuthService.RegistrarUsuarioAsync` (correo `Pendiente` en `CorreosEnCola`) | **Dos significados en mi material:** los comentarios del código lo usan para "encolar el correo sin enviarlo" y una tarea posterior para "cuenta inactiva". Ambos están implementados; confirmar cuál es el correcto. |
| RF-CA-16 Activar: token usado, vencido o inexistente se rechaza | `AuthService.ActivarCuentaAsync`; `AuthEndpoints` (`GET /auth/activar`) | Ninguna. |
| RF-CA-17 Reenviar activación sin revelar existencia | `AuthService.ReenviarActivacionAsync`; `AuthEndpoints` (`POST /auth/reenviar-activacion`) | Ninguna. |
| RF-CA-18 Logout invalida la credencial | `SesionService.CerrarSesionAsync`; `AuthEndpoints` (`POST /auth/logout`); sesión validada en cada petición | Ninguna. |
| RF-CA-19 Bloqueo tras 5 fallos | `SesionService.LoginAsync` (5 intentos, 15 minutos, contador a 0 al bloquear); `AuthEndpoints` (423) | Los 5 intentos y los 15 minutos salen de mi enunciado, no del requisito. Código 423 es decisión propia. Con la contraseña incorrecta durante el bloqueo se responde el mismo 401 genérico (no revela el bloqueo). |
| RF-CA-20 Un Administrador no se desactiva a sí mismo | `UsuarioAdminService.DesactivarAsync` (409) | Decisión propia: 409 en vez de 400. |
| RF-CA-21 Listado de usuarios sin datos sensibles | `AdminEndpoints` (`GET /admin/usuarios`, `UsuarioAdminResponse`); `Core/Interfaces/IUsuarioAdminService.cs` (`UsuarioAdminDto`) | Ninguna. |
| RF-CA-22 Cambio de contraseña con la actual | `AuthService.CambiarPasswordAsync`; `AuthEndpoints` (`POST /auth/cambiar-password`) | Contraseña actual incorrecta devuelve 400 (no 401). Además, ese endpoint no cuenta los intentos fallidos hacia el bloqueo: con una sesión robada se podría probar la contraseña actual sin límite. |

## Notificaciones (RF-NOT)

| ID | Dónde se cumple | Dudas |
| :--- | :--- | :--- |
| RF-NOT-08 Envío de correos | `Core/Interfaces/IEmailSender.cs`; `MailWorker/SmtpEmailSender.cs` (`System.Net.Mail`) | Probado con un servidor SMTP de prueba local; **no probado con un servidor real con autenticación**. |
| RF-NOT-09 Procesar la cola uno por uno, en un proceso independiente | `MailWorker/CorreoProcessor.cs` (orden por `FechaCreacion`, un scope por correo); `MailWorker/Program.cs` (ejecuta una vez y termina) | El significado exacto del ID lo deduje del enunciado de la tarea. |
| RF-NOT-12 Un correo enviado no se reenvía | `CorreoProcessor` (relee el estado antes de enviar; marca `Enviado` solo si sigue `Pendiente`) | Si dos instancias leen el mismo correo a la vez, ambas podrían enviarlo antes de marcarlo; cerrarlo requiere un estado intermedio (`Enviando`). |
| RF-NOT-13 Un fallo no pierde el correo | `CorreoProcessor` (`Intentos`++, `UltimoError` sin contraseña ni traza, sigue `Pendiente`); `Core/Entities/CorreoEnCola.cs` | Sin reintentos ni estado `Fallido`: se dejó para la semana 11, como se pidió. Significado del ID deducido del enunciado. |

## Módulo de negocio: máquina de estados (RF-NEG)

| ID | Dónde se cumple | Dudas |
| :--- | :--- | :--- |
| RF-NEG-03 Estado de la receta (5 estados, por defecto Borrador) | `Recetas/Enums/EstadoReceta.cs`; `Recetas/Entities/Receta.cs` (`Estado`); `Infrastructure/Configurations/RecetaConfiguration.cs` (texto, valor por defecto `Borrador`); migración `EstadoReceta` | La migración reemplaza la columna anterior `EstadoPublicacion` (sin uso, 0 filas). |
| RF-NEG-04 Solo transiciones permitidas; una prohibida explícita | `Recetas/Estados/TransicionesReceta.cs` (`Permitidas`, `ProhibidasExplicitas`, `EsTransicionPermitida`); [`docs/maquina-de-estados.md`](maquina-de-estados.md) | **Solo estructura:** ningún endpoint ni servicio llama todavía a `EsTransicionPermitida`, así que aún no se aplica a ninguna operación real. La asignación de "quién ejecuta" cada transición es una propuesta mía. |
| RF-NEG-05 `Archivada` es terminal | `TransicionesReceta.Permitidas` (`Archivada` → vacío) | Ninguna. |
| RF-NEG-09 Independiente de la máquina de permisos del Core | `Recetas` solo referencia Core y no usa tipos de permisos | La máquina de permisos del Core todavía no está implementada, así que la independencia se cumple por ausencia. |

## Decisiones de diseño (RD)

| ID | Dónde se cumple | Dudas |
| :--- | :--- | :--- |
| RD-03 Core sin referencias a otros proyectos | `Core/WhatAreWeEating.Core.csproj` sin `ProjectReference` ni `PackageReference` | Ninguna. |
| RD-04 Una sola tabla de transiciones | `Recetas/Estados/TransicionesReceta.cs` | Ninguna. |
| RD-05 | Posiblemente hashes de contraseñas y tokens: `PasswordHasher`, `TokenService` (en BD solo el hash) | **Significado no confirmado.** |
| RD-06 Autorización en el servidor | `JwtAuthenticationExtensions` (rol y sesión leídos de la BD en cada petición) | Ninguna. |
| RD-07 Validación de entradas con 400 controlado | Endpoints de `Api/Endpoints/*` y validadores de `Core/Services` | Ninguna. |
| RD-08 Errores controlados sin trazas | `Api/Middlewares/ErrorHandlingMiddleware.cs`; JSON de 401 y 403 en `JwtAuthenticationExtensions` | Ninguna. |
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

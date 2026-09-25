# WhatAreWeEating
WhatAreWeEating
Sistema de gestión y planificación de recetas — Programación III · ITLA · 2026-C-3.

Permite administrar un catálogo de recetas, registrar los ingredientes disponibles en la despensa de cada usuario y determinar qué recetas se pueden preparar con lo que hay disponible. Construido con .NET 10, Entity Framework Core y SQL Server, dividido en un Core transversal (control de acceso, permisos, documentos, notificaciones, reportes, auditoría) y un módulo de negocio de recetas, independiente del Core.

Estructura del repositorio
WhatAreWeEating.slnx
src/
├── WhatAreWeEating.Core            # Piezas transversales (aún sin entidades propias)
├── WhatAreWeEating.Recetas         # Dominio: recetas, ingredientes, despensa
└── WhatAreWeEating.Infrastructure  # AppDbContext, Configurations/, Migrations/
Cómo ejecutar el proyecto
Nota: el repositorio todavía no tiene un proyecto host/API ejecutable — Control de acceso, el primer punto de entrada real, se construye en las semanas 2 a 4. Por ahora "ejecutar" significa restaurar dependencias, compilar y aplicar las migraciones contra SQL Server. Esta sección se actualiza con el comando dotnet run en cuanto exista el host.

Requisitos
.NET 10 SDK
SQL Server (local o en contenedor) accesible
Herramienta dotnet-ef: dotnet tool install --global dotnet-ef
Pasos
Clonar el repositorio

git clone <url-del-repo>
cd WhatAreWeEating
Restaurar dependencias

dotnet restore
Configurar la cadena de conexión por variable de entorno — no va en appsettings versionado (RD-10)

# bash/zsh
export ConnectionStrings__DefaultConnection="Server=localhost;Database=WhatAreWeEating;User Id=sa;Password=<tu-password>;TrustServerCertificate=True;"
# PowerShell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=WhatAreWeEating;Trusted_Connection=True;TrustServerCertificate=True;"
Compilar la solución

dotnet build
Aplicar las migraciones

dotnet ef database update --project src/WhatAreWeEating.Infrastructure
Verificado antes de abrir el pull request
 dotnet restore sin errores
 dotnet build sin errores
 dotnet ef database update aplica ModeloInicialRecetas sin errores
 Las 6 tablas del módulo de negocio existen en SQL Server tras el update

## Diagrama de componentes

```mermaid
flowchart TB

    subgraph CORE["CORE"]
        direction LR

        ACC["Control de acceso"]
        PERM["Gestión de permisos"]
        DOC["Manejador de documentos"]
    end

    REC["Gestión de recetas"]

    subgraph CORE2["CORE"]
        direction LR

        NOTI["Notificaciones"]
        REP["Reportes"]
        AUD["Auditoría"]
    end

    REC -->|"quién es y qué rol"| ACC
    REC -->|"gestionar documentos"| DOC
    REC -->|"notificar resultado de revisión"| NOTI
    REC -->|"solicitar información agregada"| REP
    REC -.->|"registrar evento"| AUD

    PERM -->|"avisa al solicitante"| NOTI
````
Todas las flechas van del módulo de negocio hacia el Core, nunca al revés: si se elimina `Negocio`, el Core sigue construyéndose y ejecutándose (RD-03). La relación con Auditoría se dibuja punteada porque esa pieza solo registra eventos, no orquesta ninguna operación.
 
### Interfaces del Core
 
**Control de acceso**
Responsabilidad: decidir quién es el usuario, qué rol tiene, y si puede ejecutar la operación que está pidiendo.
- `Registrar(nombre, correo, contraseña)`
- `IniciarSesion(correo, contraseña) → credencial`
- `ObtenerUsuarioAutenticado(credencial) → usuario, rol`
- `CambiarRol(usuarioId, nuevoRol)` — solo Administrador
- `IniciarRecuperacion(correo)` / `RestablecerContraseña(código, nueva)`
- `ForzarRestablecimiento(usuarioId)` — solo Administrador
- `Autorizar(credencial, rolRequerido) → permitido/rechazado`
**Gestión de permisos**
Responsabilidad: gobernar el ciclo de vida de una solicitud de permiso (Pendiente → Aprobada/Rechazada → Aplicada), sin saber nada de recetas ni de auditoría.
- `CrearSolicitud(solicitanteId, permisoSolicitado)`
- `ListarPendientes(solicitanteId?)`
- `Aprobar(solicitudId, aprobadorId)`
- `Rechazar(solicitudId, aprobadorId, motivo)`
**Manejador de documentos**
Responsabilidad: guardar, listar y dar de baja lógica archivos asociados a cualquier entidad del sistema, sin saber qué representa cada archivo.
- `Subir(archivo, usuarioId) → documentoId`
- `Listar(usuarioId)`
- `Descargar(documentoId, usuarioId)`
- `EliminarLogico(documentoId, usuarioId)`
**Notificaciones**
Responsabilidad: entregar un mensaje a un usuario por los canales disponibles (interno + correo) y administrar la cola de envío.
- `Notificar(destinatarioId, tipo, mensaje)`
- `ListarPropias(usuarioId) → notificaciones`
- `MarcarLeida(notificacionId, usuarioId)`
- `DesactivarCanalCorreo(usuarioId)`
- `ProcesarCola()` — proceso independiente que envía lo pendiente
**Reportes**
Responsabilidad: responder preguntas agregadas sobre el Core y sobre el negocio, filtradas por rol y por rango de fechas — nunca devuelve listados crudos.
- `ReporteCore(tipo, rango, solicitante) → datos agregados`
- `ReporteNegocio(tipo, rango, solicitante) → datos agregados`
**Auditoría**
Responsabilidad: dejar constancia inmutable de quién hizo qué, cuándo y con qué valores — de solo escritura desde afuera, de solo lectura filtrada hacia el Administrador.
- `Registrar(usuarioId, acción, entidad, entidadId, valorAnterior, valorNuevo)`
- `Consultar(usuarioId?, entidad?, rango) → registros` — solo Administrador
  
### Modelo de entidades del negocio
```mermaid
erDiagram
    CATEGORIA ||--o{ RECETA : clasifica
    RECETA ||--o{ RECETA_INGREDIENTE : contiene
    INGREDIENTE ||--o{ RECETA_INGREDIENTE : "se usa en"
    DESPENSA ||--o{ DESPENSA_INGREDIENTE : contiene
    INGREDIENTE ||--o{ DESPENSA_INGREDIENTE : "disponible en"
 
    CATEGORIA {
        Guid id
        string nombre
    }
    RECETA {
        Guid id
        string nombre
        string descripcion
        string instrucciones
        Guid autorId
        string estadoPublicacion
        Guid categoriaId
    }
    INGREDIENTE {
        Guid id
        string nombre
    }
    DESPENSA {
        Guid id
        Guid usuarioId
    }
    RECETA_INGREDIENTE {
        Guid recetaId
        Guid ingredienteId
        decimal cantidad
        string unidad
    }
    DESPENSA_INGREDIENTE {
        Guid despensaId
        Guid ingredienteId
        decimal cantidadDisponible
        string unidad
    }
```
 
`RecetaIngrediente` y `DespensaIngrediente` son entidades propias, no solo llaves foráneas cruzadas: cada una guarda `cantidad`/`unidad`, dato que la funcionalidad de "qué recetas puedo preparar" necesita para comparar disponibilidad contra requerimiento (RF-NEG-01). `Despensa.usuarioId` referencia a `Usuario` del Core sin dibujar esa entidad aquí — el negocio conoce el id, pero no es dueño de esa entidad (RD-03 aplicado al modelo de datos).

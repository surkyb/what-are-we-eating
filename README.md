# WhatAreWeEating

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

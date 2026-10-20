using System.Security.Claims;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.DTOs;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Core.Permisos;

namespace WhatAreWeEating.Api.Endpoints;

public static class PermisosEndpoints
{
    public static IEndpointRouteBuilder MapPermisosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/permisos/solicitudes").WithTags("Gestión de permisos");

        // RF-GP-01: cualquier usuario autenticado solicita un permiso; nace Pendiente
        group.MapPost("/", async (
            CrearSolicitudPermisoRequest request,
            ClaimsPrincipal user,
            IPermisoService permisos,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId))
            {
                return Results.Unauthorized();
            }

            var resultado = await permisos.CrearAsync(usuarioId, request.Permiso, cancellationToken);
            return resultado.Exito
                ? Results.Created($"/permisos/solicitudes/{resultado.Solicitud!.Id}", ToResponse(resultado.Solicitud))
                : ToError(resultado);
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.SolicitarPermiso)
        .WithName("SolicitarPermiso")
        .Produces<SolicitudPermisoResponse>(StatusCodes.Status201Created)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<MensajeResponse>(StatusCodes.Status409Conflict);

        // RF-GP-02: solo el Administrador ve las pendientes de todos
        group.MapGet("/", async (IPermisoService permisos, CancellationToken cancellationToken) =>
        {
            var pendientes = await permisos.ListarPendientesAsync(cancellationToken);
            return Results.Ok(pendientes.Select(ToResponse));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarSolicitudesPendientes)
        .WithName("ListarSolicitudesPendientes")
        .Produces<IEnumerable<SolicitudPermisoResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // RF-GP-02: cada usuario solo ve las suyas
        group.MapGet("/mias", async (
            ClaimsPrincipal user,
            IPermisoService permisos,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId))
            {
                return Results.Unauthorized();
            }

            var propias = await permisos.ListarDeUsuarioAsync(usuarioId, cancellationToken);
            return Results.Ok(propias.Select(ToResponse));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarMisSolicitudes)
        .WithName("ListarMisSolicitudes")
        .Produces<IEnumerable<SolicitudPermisoResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // RF-GP-03, RF-GP-05, RF-GP-06, RF-GP-07
        group.MapPost("/{id:guid}/aprobar", async (
            Guid id,
            ClaimsPrincipal user,
            IPermisoService permisos,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var adminId))
            {
                return Results.Unauthorized();
            }

            var resultado = await permisos.AprobarAsync(adminId, id, cancellationToken);
            return resultado.Exito ? Results.Ok(ToResponse(resultado.Solicitud!)) : ToError(resultado);
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.AprobarSolicitud)
        .WithName("AprobarSolicitudPermiso")
        .Produces<SolicitudPermisoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status404NotFound)
        .Produces<MensajeResponse>(StatusCodes.Status409Conflict);

        // RF-GP-04, RF-GP-06, RF-GP-07, RD-07
        group.MapPost("/{id:guid}/rechazar", async (
            Guid id,
            RechazarSolicitudPermisoRequest request,
            ClaimsPrincipal user,
            IPermisoService permisos,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var adminId))
            {
                return Results.Unauthorized();
            }

            var resultado = await permisos.RechazarAsync(adminId, id, request.Motivo, cancellationToken);
            return resultado.Exito ? Results.Ok(ToResponse(resultado.Solicitud!)) : ToError(resultado);
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.RechazarSolicitud)
        .WithName("RechazarSolicitudPermiso")
        .Produces<SolicitudPermisoResponse>(StatusCodes.Status200OK)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status404NotFound)
        .Produces<MensajeResponse>(StatusCodes.Status409Conflict);

        // RF-GP-05: la acción que el permiso habilita. Se verifica en el servidor en cada petición (RD-06)
        app.MapGet("/directorio", async (
            ClaimsPrincipal user,
            IPermisoService permisos,
            IUsuarioAdminService usuarios,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId))
            {
                return Results.Unauthorized();
            }

            if (!await permisos.TienePermisoAsync(usuarioId, CatalogoPermisos.ConsultarDirectorio, cancellationToken))
            {
                return Results.Json(
                    new MensajeResponse("No tiene el permiso 'directorio.consultar'. Solicítelo en POST /permisos/solicitudes."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var lista = await usuarios.ListarAsync(cancellationToken);
            return Results.Ok(lista.Where(u => u.Activo).Select(u => new DirectorioEntradaResponse(u.Nombre, u.Rol.ToString())));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ConsultarDirectorio)
        .WithTags("Gestión de permisos")
        .WithName("ConsultarDirectorio")
        .Produces<IEnumerable<DirectorioEntradaResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<MensajeResponse>(StatusCodes.Status403Forbidden);

        return app;
    }

    private static SolicitudPermisoResponse ToResponse(SolicitudPermisoDto s) => new(
        s.Id, s.SolicitanteId, s.Permiso, s.Estado.ToString(), s.FechaCreacion,
        s.ResueltaPorId, s.FechaResolucion, s.MotivoRechazo, s.FechaAplicacion);

    private static IResult ToError(PermisoResultado resultado)
    {
        var cuerpo = new MensajeResponse(resultado.Mensaje ?? "Operación no permitida.");
        return resultado.Resultado switch
        {
            ResultadoPermiso.NoEncontrado => Results.NotFound(cuerpo),
            ResultadoPermiso.PermisoInvalido or ResultadoPermiso.MotivoRequerido => Results.BadRequest(cuerpo),
            _ => Results.Json(cuerpo, statusCode: StatusCodes.Status409Conflict)
        };
    }
}

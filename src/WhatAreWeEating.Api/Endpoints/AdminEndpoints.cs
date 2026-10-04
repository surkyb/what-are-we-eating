using System.Security.Claims;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.DTOs;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/usuarios").WithTags("Administración de usuarios");

        // RF-CA-21: DTO sin hashes, sales, tokens ni datos de sesión
        group.MapGet("/", async (IUsuarioAdminService admin, CancellationToken cancellationToken) =>
        {
            var usuarios = await admin.ListarAsync(cancellationToken);
            return Results.Ok(usuarios.Select(u =>
                new UsuarioAdminResponse(u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Activo)));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarUsuarios)
        .WithName("ListarUsuarios")
        .Produces<IEnumerable<UsuarioAdminResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // RF-CA-04, RF-CA-08, RD-07
        group.MapPut("/{id:guid}/rol", async (
            Guid id,
            CambiarRolRequest request,
            ClaimsPrincipal user,
            IUsuarioAdminService admin,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseRol(request.Rol, out var rol))
            {
                return Results.BadRequest(new MensajeResponse("El rol debe ser 'Administrador' o 'Estandar'."));
            }

            if (!user.TryGetUsuarioId(out var actorId))
            {
                return Results.Unauthorized();
            }

            var resultado = await admin.CambiarRolAsync(actorId, id, rol, cancellationToken);
            return ToResult(resultado, "Rol actualizado correctamente.");
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.CambiarRolUsuario)
        .WithName("CambiarRolUsuario")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status404NotFound)
        .Produces<MensajeResponse>(StatusCodes.Status409Conflict);

        // RF-CA-20
        group.MapPost("/{id:guid}/desactivar", async (
            Guid id,
            ClaimsPrincipal user,
            IUsuarioAdminService admin,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var actorId))
            {
                return Results.Unauthorized();
            }

            var resultado = await admin.DesactivarAsync(actorId, id, cancellationToken);
            return ToResult(resultado, "Usuario desactivado y sus sesiones revocadas.");
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.DesactivarUsuario)
        .WithName("DesactivarUsuario")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status404NotFound)
        .Produces<MensajeResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reactivar", async (
            Guid id,
            IUsuarioAdminService admin,
            CancellationToken cancellationToken) =>
        {
            var resultado = await admin.ReactivarAsync(id, cancellationToken);
            return ToResult(resultado, "Usuario reactivado.");
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ReactivarUsuario)
        .WithName("ReactivarUsuario")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    // Solo los nombres exactos del enum (sin números ni valores no definidos)
    private static bool TryParseRol(string? valor, out RolUsuario rol)
    {
        rol = default;
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var nombre = Enum.GetNames<RolUsuario>()
            .FirstOrDefault(n => string.Equals(n, valor.Trim(), StringComparison.OrdinalIgnoreCase));

        return nombre is not null && Enum.TryParse(nombre, out rol);
    }

    private static IResult ToResult(AdminResultado resultado, string mensajeOk) => resultado.Resultado switch
    {
        ResultadoAdmin.Ok => Results.Ok(new MensajeResponse(mensajeOk)),
        ResultadoAdmin.NoEncontrado => Results.NotFound(new MensajeResponse(resultado.Mensaje ?? "El usuario no existe.")),
        _ => Results.Json(new MensajeResponse(resultado.Mensaje ?? "Operación no permitida."), statusCode: StatusCodes.Status409Conflict)
    };
}

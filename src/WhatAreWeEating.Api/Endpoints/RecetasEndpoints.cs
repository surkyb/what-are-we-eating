using System.Security.Claims;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.DTOs;
using WhatAreWeEating.Recetas;
using WhatAreWeEating.Recetas.Interfaces;

namespace WhatAreWeEating.Api.Endpoints;

/// <summary>
/// Operaciones del módulo de recetas (RF-NEG-02). Cada una exige sesión válida y el rol del catálogo
/// de PoliciesCatalogo (RF-NEG-06); las reglas de estado las resuelve Receta.Transicionar (RD-04).
/// </summary>
public static class RecetasEndpoints
{
    public static IEndpointRouteBuilder MapRecetasEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogos = app.MapGroup("/").WithTags("Recetas: catálogos");

        catalogos.MapPost("/categorias", async (CategoriaRequest request, IRecetaService recetas, CancellationToken ct) =>
        {
            var r = await recetas.CrearCategoriaAsync(request.Nombre, ct);
            return r.Exito ? Results.Created($"/categorias/{r.Valor!.Id}", r.Valor) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.CrearCategoria).WithName("CrearCategoria");

        catalogos.MapGet("/categorias", async (IRecetaService recetas, CancellationToken ct)
            => Results.Ok(await recetas.ListarCategoriasAsync(ct)))
            .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarCategorias).WithName("ListarCategorias");

        catalogos.MapPost("/ingredientes", async (IngredienteRequest request, IRecetaService recetas, CancellationToken ct) =>
        {
            var r = await recetas.CrearIngredienteAsync(request.Nombre, request.UnidadBase, ct);
            return r.Exito ? Results.Created($"/ingredientes/{r.Valor!.Id}", r.Valor) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.CrearIngrediente).WithName("CrearIngrediente");

        catalogos.MapGet("/ingredientes", async (IRecetaService recetas, CancellationToken ct)
            => Results.Ok(await recetas.ListarIngredientesAsync(ct)))
            .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarIngredientes).WithName("ListarIngredientes");

        var grupo = app.MapGroup("/recetas").WithTags("Recetas");

        // 1. Crear: la receta nace en Borrador
        grupo.MapPost("/", async (CrearRecetaRequest request, ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var autorId)) return Results.Unauthorized();

            var nueva = new RecetaNueva(request.Nombre, request.Descripcion, request.Instrucciones, request.CategoriaId,
                request.Ingredientes?.Select(i => new IngredienteNuevo(i.IngredienteId, i.Cantidad, i.Unidad)).ToList());
            var r = await recetas.CrearAsync(autorId, nueva, ct);
            return r.Exito ? Results.Created($"/recetas/{r.Valor!.Id}", ToResponse(r.Valor)) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.CrearReceta).WithName("CrearReceta");

        // 2 a 5. Transiciones
        MapTransicion(grupo, "/{id:guid}/enviar-a-revision", PoliciesCatalogo.Operaciones.EnviarRecetaARevision,
            "EnviarRecetaARevision", (s, a, rol, id, ct) => s.EnviarARevisionAsync(a, rol, id, ct));
        MapTransicion(grupo, "/{id:guid}/publicar", PoliciesCatalogo.Operaciones.PublicarReceta,
            "PublicarReceta", (s, a, rol, id, ct) => s.PublicarAsync(a, rol, id, ct));
        MapTransicion(grupo, "/{id:guid}/retomar", PoliciesCatalogo.Operaciones.RetomarReceta,
            "RetomarReceta", (s, a, rol, id, ct) => s.RetomarAsync(a, rol, id, ct));
        MapTransicion(grupo, "/{id:guid}/archivar", PoliciesCatalogo.Operaciones.ArchivarReceta,
            "ArchivarReceta", (s, a, rol, id, ct) => s.ArchivarAsync(a, rol, id, ct));

        grupo.MapPost("/{id:guid}/rechazar", async (
            Guid id, RechazarRecetaRequest request, ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var actorId) || !user.TryGetRol(out var rol)) return Results.Unauthorized();

            var r = await recetas.RechazarAsync(actorId, rol, id, request.Motivo, ct);
            return r.Exito ? Results.Ok(ToResponse(r.Valor!)) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.RechazarReceta).WithName("RechazarReceta");

        // 6. Consultas por estado
        grupo.MapGet("/publicadas", async (IRecetaService recetas, CancellationToken ct)
            => Results.Ok((await recetas.ListarPublicadasAsync(ct)).Select(ToResponse)))
            .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarRecetasPublicadas).WithName("ListarRecetasPublicadas");

        grupo.MapGet("/en-revision", async (IRecetaService recetas, CancellationToken ct)
            => Results.Ok((await recetas.ListarEnRevisionAsync(ct)).Select(ToResponse)))
            .RequiereOperacion(PoliciesCatalogo.Operaciones.ListarRecetasEnRevision).WithName("ListarRecetasEnRevision");

        grupo.MapGet("/mias", async (ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var autorId)) return Results.Unauthorized();
            return Results.Ok((await recetas.ListarDeAutorAsync(autorId, ct)).Select(ToResponse));
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.ListarMisRecetas).WithName("ListarMisRecetas");

        // 8. Requiere el permiso 'recetas.consultar-preparables' concedido por Gestión de permisos (RD-06)
        grupo.MapGet("/preparables", async (ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId)) return Results.Unauthorized();

            var r = await recetas.ListarPreparablesAsync(usuarioId, ct);
            return r.Exito ? Results.Ok(r.Valor!.Select(ToResponse)) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.ListarRecetasPreparables).WithName("ListarRecetasPreparables");

        // 9. Sugerencia según el clima actual de una ciudad (API externa; la clave sale de OpenWeather__ApiKey)
        grupo.MapGet("/sugeridas", async (string? ciudad, ISugerenciaRecetasService sugerencias, CancellationToken ct) =>
        {
            var r = await sugerencias.SugerirPorClimaAsync(ciudad, ct);
            if (r.Exito)
            {
                var s = r.Valor!;
                return Results.Ok(new SugerenciaRecetasResponse(s.Clima, s.Criterio, s.Recetas.Select(ToResponse)));
            }

            var cuerpo = new MensajeResponse(r.Mensaje ?? "No fue posible obtener el clima.");
            return r.Codigo switch
            {
                Core.Interfaces.ResultadoClima.CiudadInvalida => Results.BadRequest(cuerpo),
                Core.Interfaces.ResultadoClima.CiudadNoEncontrada => Results.NotFound(cuerpo),
                _ => Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable)
            };
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.SugerirRecetasPorClima).WithName("SugerirRecetasPorClima");

        // 7. Despensa
        var despensa = app.MapGroup("/despensa").WithTags("Despensa");

        despensa.MapPut("/ingredientes/{ingredienteId:guid}", async (
            Guid ingredienteId, GuardarDespensaRequest request, ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId)) return Results.Unauthorized();

            var r = await recetas.GuardarEnDespensaAsync(usuarioId, ingredienteId, request.Cantidad, request.Unidad, ct);
            return r.Exito ? Results.Ok(r.Valor) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.GuardarEnDespensa).WithName("GuardarEnDespensa");

        despensa.MapGet("/", async (ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId)) return Results.Unauthorized();
            return Results.Ok(await recetas.ListarDespensaAsync(usuarioId, ct));
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.ListarDespensa).WithName("ListarDespensa");

        return app;
    }

    private delegate Task<Resultado<RecetaDto>> Transicion(IRecetaService s, Guid actor, Core.Enums.RolUsuario rol, Guid id, CancellationToken ct);

    private static void MapTransicion(RouteGroupBuilder grupo, string ruta, string operacion, string nombre, Transicion accion)
        => grupo.MapPost(ruta, async (Guid id, ClaimsPrincipal user, IRecetaService recetas, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var actorId) || !user.TryGetRol(out var rol)) return Results.Unauthorized();

            var r = await accion(recetas, actorId, rol, id, ct);
            return r.Exito ? Results.Ok(ToResponse(r.Valor!)) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(operacion).WithName(nombre);

    private static RecetaResponse ToResponse(RecetaDto r) => new(
        r.Id, r.Nombre, r.Descripcion, r.Instrucciones, r.AutorId, r.CategoriaId, r.Estado.ToString(), r.MotivoRechazo, r.Ingredientes);

    private static IResult ToError(ResultadoReceta codigo, string? mensaje)
    {
        var cuerpo = new MensajeResponse(mensaje ?? "Operación no permitida.");
        return codigo switch
        {
            ResultadoReceta.NoEncontrada => Results.NotFound(cuerpo),
            ResultadoReceta.DatosInvalidos or ResultadoReceta.MotivoRequerido => Results.BadRequest(cuerpo),
            ResultadoReceta.RolNoPermitido or ResultadoReceta.NoEsAutor or ResultadoReceta.PermisoRequerido
                => Results.Json(cuerpo, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Json(cuerpo, statusCode: StatusCodes.Status409Conflict)
        };
    }
}

using System.Security.Claims;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.DTOs;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Api.Endpoints;

/// <summary>
/// Manejador de documentos (RF-DOC-01 a 04). Todas las operaciones exigen sesión válida; quién puede ver,
/// descargar o eliminar cada documento lo decide el servicio, no el endpoint (RD-06).
/// </summary>
public static class DocumentosEndpoints
{
    public static IEndpointRouteBuilder MapDocumentosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/documentos").WithTags("Documentos");

        // RF-DOC-01: multipart/form-data con el archivo en el campo "archivo"
        grupo.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, IDocumentoService documentos, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId)) return Results.Unauthorized();

            // Sin multipart, o con un cuerpo roto, el archivo se considera ausente (RD-07)
            IFormFile? archivo = null;
            if (request.HasFormContentType)
            {
                try
                {
                    var form = await request.ReadFormAsync(ct);
                    archivo = form.Files.GetFile("archivo");
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or BadHttpRequestException)
                {
                    archivo = null;
                }
            }

            if (archivo is null)
            {
                return ToError(ResultadoDocumento.ArchivoAusente, "Debe adjuntar un archivo en el campo 'archivo' (multipart/form-data).");
            }

            await using var contenido = archivo.OpenReadStream();
            var r = await documentos.SubirAsync(usuarioId, archivo.FileName, archivo.ContentType, contenido, ct);
            return r.Exito ? Results.Created($"/documentos/{r.Valor!.Id}", ToResponse(r.Valor)) : ToError(r.Codigo, r.Mensaje);
        }).DisableAntiforgery().Accepts<SubirDocumentoForm>("multipart/form-data")
          .RequiereOperacion(PoliciesCatalogo.Operaciones.SubirDocumento).WithName("SubirDocumento");

        // RF-DOC-02
        grupo.MapGet("/", async (ClaimsPrincipal user, IDocumentoService documentos, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId) || !user.TryGetRol(out var rol)) return Results.Unauthorized();
            return Results.Ok((await documentos.ListarAsync(usuarioId, rol, ct)).Select(ToResponse));
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.ListarDocumentos).WithName("ListarDocumentos");

        // RF-DOC-03
        grupo.MapGet("/{id:guid}/descargar", async (Guid id, ClaimsPrincipal user, IDocumentoService documentos, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId) || !user.TryGetRol(out var rol)) return Results.Unauthorized();

            var r = await documentos.DescargarAsync(usuarioId, rol, id, ct);
            return r.Exito
                ? Results.File(r.Valor!.Contenido, r.Valor.Tipo, r.Valor.Nombre)
                : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.DescargarDocumento).WithName("DescargarDocumento");

        // RF-DOC-04: eliminación lógica
        grupo.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDocumentoService documentos, CancellationToken ct) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId) || !user.TryGetRol(out var rol)) return Results.Unauthorized();

            var r = await documentos.EliminarAsync(usuarioId, rol, id, ct);
            return r.Exito ? Results.Ok(new MensajeResponse("Documento eliminado.")) : ToError(r.Codigo, r.Mensaje);
        }).RequiereOperacion(PoliciesCatalogo.Operaciones.EliminarDocumento).WithName("EliminarDocumento");

        return app;
    }

    private static DocumentoResponse ToResponse(DocumentoDto d)
        => new(d.Id, d.Nombre, d.Tipo, d.TamanoBytes, d.FechaSubida, d.PropietarioId);

    private static IResult ToError(ResultadoDocumento codigo, string? mensaje)
    {
        var cuerpo = new MensajeResponse(mensaje ?? "Operación no permitida.");
        return codigo switch
        {
            ResultadoDocumento.ArchivoAusente or ResultadoDocumento.ArchivoVacio => Results.BadRequest(cuerpo),
            ResultadoDocumento.ArchivoDemasiadoGrande => Results.Json(cuerpo, statusCode: StatusCodes.Status413PayloadTooLarge),
            ResultadoDocumento.AccesoDenegado => Results.Json(cuerpo, statusCode: StatusCodes.Status403Forbidden),
            ResultadoDocumento.NoEncontrado => Results.NotFound(cuerpo),
            _ => Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
}

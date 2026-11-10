using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Interfaces;

public sealed record DocumentoDto(Guid Id, string Nombre, string Tipo, long TamanoBytes, DateTime FechaSubida, Guid PropietarioId);

public sealed record DocumentoContenido(string Nombre, string Tipo, Stream Contenido);

public enum ResultadoDocumento
{
    Ok,
    ArchivoAusente,
    ArchivoVacio,
    ArchivoDemasiadoGrande,
    NoEncontrado,
    AccesoDenegado,
    AlmacenamientoNoDisponible
}

public sealed record RespuestaDocumento<T>(ResultadoDocumento Codigo, T? Valor = default, string? Mensaje = null)
{
    public bool Exito => Codigo == ResultadoDocumento.Ok;
}

/// <summary>Manejador de documentos (Pieza 3 del Core). La autorización se verifica aquí, en el servidor (RD-06).</summary>
public interface IDocumentoService
{
    /// <summary>RF-DOC-01. <paramref name="contenido"/> es null si no llegó ningún archivo.</summary>
    Task<RespuestaDocumento<DocumentoDto>> SubirAsync(Guid usuarioId, string? nombre, string? tipo, Stream? contenido, CancellationToken ct = default);

    /// <summary>RF-DOC-02. El Administrador ve los de todos; los demás, solo los suyos. Nunca los eliminados.</summary>
    Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, RolUsuario rol, CancellationToken ct = default);

    /// <summary>RF-DOC-03. Solo el dueño o un Administrador.</summary>
    Task<RespuestaDocumento<DocumentoContenido>> DescargarAsync(Guid usuarioId, RolUsuario rol, Guid documentoId, CancellationToken ct = default);

    /// <summary>RF-DOC-04. Eliminación lógica; solo el dueño o un Administrador.</summary>
    Task<RespuestaDocumento<DocumentoDto>> EliminarAsync(Guid usuarioId, RolUsuario rol, Guid documentoId, CancellationToken ct = default);
}

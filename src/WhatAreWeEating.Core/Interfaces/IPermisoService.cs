using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Interfaces;

public sealed record SolicitudPermisoDto(
    Guid Id,
    Guid SolicitanteId,
    string Permiso,
    EstadoSolicitudPermiso Estado,
    DateTime FechaCreacion,
    Guid? ResueltaPorId,
    DateTime? FechaResolucion,
    string? MotivoRechazo,
    DateTime? FechaAplicacion);

public enum ResultadoPermiso
{
    Ok,
    NoEncontrado,
    PermisoInvalido,
    SolicitudDuplicada,
    TransicionNoPermitida,
    AutoResolucion,
    MotivoRequerido
}

public sealed record PermisoResultado(ResultadoPermiso Resultado, string? Mensaje = null, SolicitudPermisoDto? Solicitud = null)
{
    public bool Exito => Resultado == ResultadoPermiso.Ok;
}

public interface IPermisoService
{
    /// <summary>RF-GP-01: crea una solicitud en estado Pendiente.</summary>
    Task<PermisoResultado> CrearAsync(Guid solicitanteId, string? permiso, CancellationToken cancellationToken = default);

    /// <summary>RF-GP-02: todas las solicitudes pendientes (uso exclusivo del Administrador).</summary>
    Task<IReadOnlyList<SolicitudPermisoDto>> ListarPendientesAsync(CancellationToken cancellationToken = default);

    /// <summary>RF-GP-02: un usuario solo ve sus propias solicitudes.</summary>
    Task<IReadOnlyList<SolicitudPermisoDto>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>RF-GP-03, RF-GP-05: aprueba y aplica el permiso en una sola operación atómica.</summary>
    Task<PermisoResultado> AprobarAsync(Guid adminId, Guid solicitudId, CancellationToken cancellationToken = default);

    /// <summary>RF-GP-04: rechaza con motivo obligatorio.</summary>
    Task<PermisoResultado> RechazarAsync(Guid adminId, Guid solicitudId, string? motivo, CancellationToken cancellationToken = default);

    /// <summary>Indica si el usuario tiene el permiso aplicado (RF-GP-05, RD-06).</summary>
    Task<bool> TienePermisoAsync(Guid usuarioId, string permiso, CancellationToken cancellationToken = default);
}

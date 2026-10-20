using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Permisos;

namespace WhatAreWeEating.Core.Entities;

public enum ResultadoTransicion
{
    Ok,
    TransicionNoPermitida,
    AutoResolucion,
    MotivoRequerido
}

/// <summary>
/// Solicitud de permiso. Toda mutación de estado pasa por <see cref="Aprobar"/>, <see cref="Rechazar"/>
/// o <see cref="Aplicar"/>; si la operación se rechaza, la solicitud no cambia en absoluto.
/// </summary>
public class SolicitudPermiso
{
    public Guid Id { get; set; }
    public Guid SolicitanteId { get; set; }
    public string Permiso { get; set; } = string.Empty;
    public EstadoSolicitudPermiso Estado { get; set; } = EstadoSolicitudPermiso.Pendiente;
    public DateTime FechaCreacion { get; set; }

    // Datos que deja la resolución (RF-GP-03, RF-GP-04)
    public Guid? ResueltaPorId { get; set; }
    public DateTime? FechaResolucion { get; set; }
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaAplicacion { get; set; }

    /// <summary>Token de concurrencia: dos resoluciones simultáneas no pueden ganar las dos (RF-GP-06).</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>RF-GP-01: toda solicitud nace Pendiente.</summary>
    public static SolicitudPermiso Crear(Guid solicitanteId, string permiso, DateTime ahora) => new()
    {
        Id = Guid.NewGuid(),
        SolicitanteId = solicitanteId,
        Permiso = permiso,
        Estado = EstadoSolicitudPermiso.Pendiente,
        FechaCreacion = ahora
    };

    /// <summary>RF-GP-03, RF-GP-06, RF-GP-07.</summary>
    public ResultadoTransicion Aprobar(Guid adminId, DateTime ahora)
    {
        if (adminId == SolicitanteId)
        {
            return ResultadoTransicion.AutoResolucion;
        }

        if (!TransicionesSolicitudPermiso.EsPermitida(Estado, EstadoSolicitudPermiso.Aprobada))
        {
            return ResultadoTransicion.TransicionNoPermitida;
        }

        Estado = EstadoSolicitudPermiso.Aprobada;
        ResueltaPorId = adminId;
        FechaResolucion = ahora;
        return ResultadoTransicion.Ok;
    }

    /// <summary>RF-GP-04, RF-GP-06, RF-GP-07. Sin motivo escrito el rechazo no se completa.</summary>
    public ResultadoTransicion Rechazar(Guid adminId, string? motivo, DateTime ahora)
    {
        if (adminId == SolicitanteId)
        {
            return ResultadoTransicion.AutoResolucion;
        }

        if (!TransicionesSolicitudPermiso.EsPermitida(Estado, EstadoSolicitudPermiso.Rechazada))
        {
            return ResultadoTransicion.TransicionNoPermitida;
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoTransicion.MotivoRequerido;
        }

        Estado = EstadoSolicitudPermiso.Rechazada;
        ResueltaPorId = adminId;
        FechaResolucion = ahora;
        MotivoRechazo = motivo.Trim();
        return ResultadoTransicion.Ok;
    }

    /// <summary>RF-GP-05, RF-GP-06: solo una solicitud Aprobada puede pasar a Aplicada.</summary>
    public ResultadoTransicion Aplicar(DateTime ahora)
    {
        if (!TransicionesSolicitudPermiso.EsPermitida(Estado, EstadoSolicitudPermiso.Aplicada))
        {
            return ResultadoTransicion.TransicionNoPermitida;
        }

        Estado = EstadoSolicitudPermiso.Aplicada;
        FechaAplicacion = ahora;
        return ResultadoTransicion.Ok;
    }
}

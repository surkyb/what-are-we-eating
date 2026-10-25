using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Permisos;

/// <summary>
/// Máquina de estados de la solicitud de permiso: ÚNICO lugar donde se declaran las transiciones (RD-04).
/// Cualquier combinación que no figure en <see cref="Permitidas"/> está prohibida (RF-GP-06).
/// </summary>
public static class TransicionesSolicitudPermiso
{
    public static readonly IReadOnlyDictionary<EstadoSolicitudPermiso, IReadOnlyList<EstadoSolicitudPermiso>> Permitidas =
        new Dictionary<EstadoSolicitudPermiso, IReadOnlyList<EstadoSolicitudPermiso>>
        {
            [EstadoSolicitudPermiso.Pendiente] = [EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada],
            [EstadoSolicitudPermiso.Aprobada] = [EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Pendiente],

            // Estados terminales
            [EstadoSolicitudPermiso.Aplicada] = [],
            [EstadoSolicitudPermiso.Rechazada] = [],
        };

    public static bool EsPermitida(EstadoSolicitudPermiso origen, EstadoSolicitudPermiso destino)
        => Permitidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);
}

using WhatAreWeEating.Recetas.Enums;

namespace WhatAreWeEating.Recetas.Estados;

/// <summary>
/// Máquina de estados de Receta: ÚNICO lugar donde se declaran las transiciones (RD-04).
/// Cualquier combinación que no figure en <see cref="Permitidas"/> está prohibida.
/// No depende de la máquina de permisos del Core ni comparte enums con ella (RF-NEG-09).
/// </summary>
public static class TransicionesReceta
{
    /// <summary>Tabla estado origen -> estados destino permitidos (RF-NEG-04).</summary>
    public static readonly IReadOnlyDictionary<EstadoReceta, IReadOnlyList<EstadoReceta>> Permitidas =
        new Dictionary<EstadoReceta, IReadOnlyList<EstadoReceta>>
        {
            [EstadoReceta.Borrador] = [EstadoReceta.EnRevision],
            [EstadoReceta.EnRevision] = [EstadoReceta.Publicada, EstadoReceta.Rechazada],
            [EstadoReceta.Rechazada] = [EstadoReceta.Borrador],
            [EstadoReceta.Publicada] = [EstadoReceta.Archivada],

            // Estado terminal: ninguna transición parte de él (RF-NEG-05)
            [EstadoReceta.Archivada] = []
        };

    /// <summary>
    /// Transiciones prohibidas que se nombran de forma explícita para que se puedan leer (RF-NEG-04).
    /// Es solo documentación consultable: la decisión la toma únicamente <see cref="EsTransicionPermitida"/>.
    /// </summary>
    public static readonly IReadOnlyList<(EstadoReceta Origen, EstadoReceta Destino, string Motivo)> ProhibidasExplicitas =
    [
        (EstadoReceta.Borrador, EstadoReceta.Publicada, "Una receta no puede publicarse sin pasar por revisión."),
        (EstadoReceta.Archivada, EstadoReceta.Borrador, "Archivada es terminal: no se puede reabrir (RF-NEG-05).")
    ];

    /// <summary>Único método que decide si un cambio de estado es válido (RD-04).</summary>
    public static bool EsTransicionPermitida(EstadoReceta origen, EstadoReceta destino)
        => Permitidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);
}

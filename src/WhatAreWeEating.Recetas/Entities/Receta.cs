using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Recetas.Enums;
using WhatAreWeEating.Recetas.Estados;

namespace WhatAreWeEating.Recetas.Entities;

public class Receta
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Instrucciones { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }

    // Apunta a un usuario del Core; sin navegación porque el Core aun no expone una entidad Usuario.
    public Guid AutorId { get; set; }

    public EstadoReceta Estado { get; set; } = EstadoReceta.Borrador;

    /// <summary>Motivo escrito por el Administrador al pasar a Rechazada.</summary>
    public string? MotivoRechazo { get; set; }

    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<RecetaIngrediente> RecetaIngredientes { get; set; } = new List<RecetaIngrediente>();

    /// <summary>
    /// ÚNICO punto que cambia el estado de una receta (RD-04): consulta la tabla, verifica el rol y aplica
    /// la condición de la transición. Si se rechaza, el estado y los demás datos quedan intactos.
    /// </summary>
    public ResultadoReceta Transicionar(EstadoReceta destino, Guid actorId, RolUsuario rolActor, string? motivo = null)
    {
        if (!TransicionesReceta.EsTransicionPermitida(Estado, destino))
        {
            return ResultadoReceta.TransicionNoPermitida;
        }

        if (TransicionesReceta.RolRequerido(Estado, destino) != rolActor)
        {
            return ResultadoReceta.RolNoPermitido;
        }

        switch (Estado, destino)
        {
            case (EstadoReceta.Borrador, EstadoReceta.EnRevision):
                break;

            case (EstadoReceta.EnRevision, EstadoReceta.Rechazada):
                if (string.IsNullOrWhiteSpace(motivo)) return ResultadoReceta.MotivoRequerido;
                MotivoRechazo = motivo.Trim();
                break;

            case (EstadoReceta.Rechazada, EstadoReceta.Borrador):
                if (actorId != AutorId) return ResultadoReceta.NoEsAutor;
                break;
        }

        Estado = destino;
        return ResultadoReceta.Ok;
    }

    private ResultadoReceta ValidarEnvioARevision(Guid actorId)
    {
        if (actorId != AutorId) return ResultadoReceta.NoEsAutor;
        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Instrucciones) || RecetaIngredientes.Count == 0)
        {
            return ResultadoReceta.DatosIncompletos;
        }

        return ResultadoReceta.Ok;
    }
}

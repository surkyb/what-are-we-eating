using WhatAreWeEating.Recetas.Enums;

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

    public EstadoPublicacion EstadoPublicacion { get; set; }

    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<RecetaIngrediente> RecetaIngredientes { get; set; } = new List<RecetaIngrediente>();
}

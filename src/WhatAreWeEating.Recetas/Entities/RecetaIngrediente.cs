namespace WhatAreWeEating.Recetas.Entities;

public class RecetaIngrediente
{
    public Guid RecetaId { get; set; }
    public Receta Receta { get; set; } = null!;

    public Guid IngredienteId { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    public decimal Cantidad { get; set; }
    public string Unidad { get; set; } = string.Empty;
}

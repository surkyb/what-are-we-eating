namespace WhatAreWeEating.Recetas.Entities;

public class DespensaIngrediente
{
    public Guid DespensaId { get; set; }
    public Despensa Despensa { get; set; } = null!;

    public Guid IngredienteId { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    public decimal CantidadDisponible { get; set; }
    public string Unidad { get; set; } = string.Empty;
}

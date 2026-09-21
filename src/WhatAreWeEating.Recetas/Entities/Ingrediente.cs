namespace WhatAreWeEating.Recetas.Entities;

public class Ingrediente
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? UnidadBase { get; set; }

    public ICollection<RecetaIngrediente> RecetaIngredientes { get; set; } = new List<RecetaIngrediente>();
    public ICollection<DespensaIngrediente> DespensaIngredientes { get; set; } = new List<DespensaIngrediente>();
}

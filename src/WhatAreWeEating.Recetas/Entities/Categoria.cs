namespace WhatAreWeEating.Recetas.Entities;

public class Categoria
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Receta> Recetas { get; set; } = new List<Receta>();
}

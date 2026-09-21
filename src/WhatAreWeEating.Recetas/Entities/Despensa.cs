namespace WhatAreWeEating.Recetas.Entities;

public class Despensa
{
    public Guid Id { get; set; }

    // Apunta a un usuario del Core; sin navegación porque el Core aun no expone una entidad Usuario.
    public Guid UsuarioId { get; set; }

    public ICollection<DespensaIngrediente> DespensaIngredientes { get; set; } = new List<DespensaIngrediente>();
}

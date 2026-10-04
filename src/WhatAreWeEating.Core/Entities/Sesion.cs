namespace WhatAreWeEating.Core.Entities;

public class Sesion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime FechaExpiracion { get; set; }
    public bool Revocada { get; set; } = false;
}

namespace WhatAreWeEating.Core.Entities;

/// <summary>Permiso ya aplicado a un usuario (resultado de una solicitud Aplicada, RF-GP-05).</summary>
public class PermisoConcedido
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Permiso { get; set; } = string.Empty;
    public Guid SolicitudId { get; set; }
    public DateTime FechaConcesion { get; set; }
}

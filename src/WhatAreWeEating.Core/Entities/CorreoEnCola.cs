using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Entities;

public class CorreoEnCola
{
    public Guid Id { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public EstadoCorreo Estado { get; set; } = EstadoCorreo.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEnvio { get; set; }
    public int Intentos { get; set; } = 0;
    public string? UltimoError { get; set; }
}

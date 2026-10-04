using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Entities;

public class TokenUnUso
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public TipoToken Tipo { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }
    public bool Usado { get; set; } = false;
}

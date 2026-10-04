namespace WhatAreWeEating.Core.Interfaces;

/// <summary>
/// Opciones de firma del JWT. La clave solo se obtiene de la variable de entorno Jwt__Key (RD-10).
/// </summary>
public sealed class JwtOptions
{
    public const int LongitudMinimaClave = 32;

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = "WhatAreWeEating";
    public string Audience { get; init; } = "WhatAreWeEating.Api";
}

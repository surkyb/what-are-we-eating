using Microsoft.Extensions.Configuration;
using System.Text;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure;

public static class JwtOptionsLoader
{
    /// <summary>
    /// Carga la configuración JWT. La clave solo viene de Jwt__Key; Issuer y Audience tienen valores por defecto.
    /// Lanza InvalidOperationException nombrando la variable, sin incluir nunca su valor (RD-10).
    /// </summary>
    public static JwtOptions Cargar(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Falta la variable de entorno Jwt__Key (clave de firma del JWT).");
        }

        if (Encoding.UTF8.GetByteCount(key) < JwtOptions.LongitudMinimaClave)
        {
            throw new InvalidOperationException(
                $"La variable de entorno Jwt__Key es demasiado corta: debe tener al menos {JwtOptions.LongitudMinimaClave} caracteres.");
        }

        var defaults = new JwtOptions();

        return new JwtOptions
        {
            Key = key,
            Issuer = string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]) ? defaults.Issuer : configuration["Jwt:Issuer"]!,
            Audience = string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]) ? defaults.Audience : configuration["Jwt:Audience"]!
        };
    }
}

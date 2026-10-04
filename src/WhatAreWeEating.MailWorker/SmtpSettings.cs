using Microsoft.Extensions.Configuration;

namespace WhatAreWeEating.MailWorker;

public sealed class SmtpSettings
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public required string From { get; init; }
    public required bool EnableSsl { get; init; }

    private static readonly string[] Variables =
        ["Host", "Port", "User", "Password", "From", "EnableSsl"];

    /// <summary>
    /// Lee la configuración SMTP solo desde variables de entorno (Smtp__*).
    /// Devuelve la lista de problemas (solo nombres de variables, nunca valores).
    /// </summary>
    public static (SmtpSettings? Settings, List<string> Problemas) Load(IConfiguration configuration)
    {
        var problemas = new List<string>();

        foreach (var nombre in Variables)
        {
            if (string.IsNullOrWhiteSpace(configuration[$"Smtp:{nombre}"]))
            {
                problemas.Add($"Falta la variable de entorno Smtp__{nombre}.");
            }
        }

        if (problemas.Count > 0)
        {
            return (null, problemas);
        }

        if (!int.TryParse(configuration["Smtp:Port"], out var port) || port is < 1 or > 65535)
        {
            problemas.Add("Smtp__Port debe ser un número entre 1 y 65535.");
        }

        if (!bool.TryParse(configuration["Smtp:EnableSsl"], out var enableSsl))
        {
            problemas.Add("Smtp__EnableSsl debe ser 'true' o 'false'.");
        }

        if (problemas.Count > 0)
        {
            return (null, problemas);
        }

        return (new SmtpSettings
        {
            Host = configuration["Smtp:Host"]!,
            Port = port,
            User = configuration["Smtp:User"]!,
            Password = configuration["Smtp:Password"]!,
            From = configuration["Smtp:From"]!,
            EnableSsl = enableSsl
        }, problemas);
    }
}

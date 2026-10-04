using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

/// <summary>
/// Genera y encola códigos de recuperación de contraseña. No guarda cambios ni envía correos:
/// el llamador persiste dentro de su transacción y el MailWorker envía después (RF-CA-10).
/// </summary>
internal static class CodigoRecuperacion
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

    public static async Task EncolarAsync(
        AppDbContext context,
        ITokenService tokenService,
        Usuario usuario,
        string asunto,
        string introduccion,
        CancellationToken cancellationToken)
    {
        // Invalida los códigos de recuperación previos que sigan vigentes
        var previos = await context.TokensUnUso
            .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoToken.RecuperacionPassword && !t.Usado)
            .ToListAsync(cancellationToken);

        foreach (var previo in previos)
        {
            previo.Usado = true;
        }

        // En la BD solo se guarda el hash; el código en claro viaja únicamente en el correo
        var (codigo, hash) = tokenService.GenerateToken();

        context.TokensUnUso.Add(new TokenUnUso
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Tipo = TipoToken.RecuperacionPassword,
            TokenHash = hash,
            FechaVencimiento = DateTime.UtcNow.Add(Vigencia),
            Usado = false
        });

        context.CorreosEnCola.Add(new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = usuario.Correo,
            Asunto = asunto,
            Cuerpo = $"Hola,\n\n{introduccion}\n\nTu código de recuperación es:\n{codigo}\n\n" +
                     $"Úsalo en POST /auth/restablecer junto con tu nueva contraseña. Expira en {(int)Vigencia.TotalMinutes} minutos y solo sirve una vez.\n\n" +
                     "Si no lo solicitaste, ignora este mensaje.",
            Estado = EstadoCorreo.Pendiente,
            FechaCreacion = DateTime.UtcNow,
            FechaEnvio = null
        });
    }

    /// <summary>
    /// Hash con formato válido (mismas iteraciones que PasswordHasher) pero con sal y resultado aleatorios:
    /// ninguna contraseña puede producirlo y el login conserva el mismo costo de verificación (RF-CA-03).
    /// </summary>
    public static string GenerarHashInutilizable()
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = RandomNumberGenerator.GetBytes(32);
        return $"100000.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;

namespace WhatAreWeEating.MailWorker;

public sealed record ResumenProceso(int Enviados, int Fallidos, int Omitidos);

public sealed class CorreoProcessor
{
    private const int MaxLongitudError = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _sender;
    private readonly string _secreto;

    public CorreoProcessor(IServiceScopeFactory scopeFactory, IEmailSender sender, string secreto)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _secreto = secreto;
    }

    public async Task<ResumenProceso> ProcesarAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> ids;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await context.CorreosEnCola
                .AsNoTracking()
                .Where(c => c.Estado == EstadoCorreo.Pendiente)
                .OrderBy(c => c.FechaCreacion)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        int enviados = 0, fallidos = 0, omitidos = 0;

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Un scope (y DbContext) por correo: la lectura siguiente siempre trae el estado actual (RF-NOT-12)
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var correo = await context.CorreosEnCola.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (correo is null || correo.Estado != EstadoCorreo.Pendiente)
            {
                omitidos++;
                Console.WriteLine($"[{id}] Omitido: ya no está pendiente.");
                continue;
            }

            try
            {
                await _sender.EnviarAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var mensaje = Sanitizar(ex);
                correo.Intentos++;
                correo.UltimoError = mensaje;
                await context.SaveChangesAsync(cancellationToken);

                fallidos++;
                Console.WriteLine($"[{id}] Falló el envío (intento {correo.Intentos}): {mensaje}");
                continue;
            }

            // Marca como enviado solo si sigue Pendiente en la BD, para no pisar a otra instancia
            var filas = await context.CorreosEnCola
                .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadoCorreo.Enviado)
                    .SetProperty(c => c.FechaEnvio, DateTime.UtcNow)
                    .SetProperty(c => c.UltimoError, (string?)null),
                    cancellationToken);

            enviados++;
            Console.WriteLine($"[{id}] Enviado.{(filas == 0 ? " (otra instancia ya lo había marcado)" : string.Empty)}");
        }

        return new ResumenProceso(enviados, fallidos, omitidos);
    }

    // Solo tipo y mensaje de la excepción: sin traza y sin la contraseña SMTP (RD-10)
    private string Sanitizar(Exception ex)
    {
        var texto = $"{ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException is not null)
        {
            texto += $" -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }

        if (!string.IsNullOrEmpty(_secreto))
        {
            texto = texto.Replace(_secreto, "***", StringComparison.Ordinal);
        }

        texto = texto.ReplaceLineEndings(" ");
        return texto.Length > MaxLongitudError ? texto[..MaxLongitudError] : texto;
    }
}

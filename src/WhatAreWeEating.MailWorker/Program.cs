using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.MailWorker;

// Modo "ejecuta una vez y sale": procesa los correos pendientes y termina.
// Códigos de salida: 0 = terminó la cola, 2 = configuración inválida, 1 = error inesperado.

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var (settings, problemas) = SmtpSettings.Load(configuration);
if (settings is null)
{
    Console.Error.WriteLine("Configuración SMTP incompleta o inválida. El worker no se ejecutó:");
    foreach (var problema in problemas)
    {
        Console.Error.WriteLine($"  - {problema}");
    }
    return 2;
}

if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
{
    Console.Error.WriteLine("Falta la variable de entorno ConnectionStrings__Default. El worker no se ejecutó.");
    return 2;
}

var services = new ServiceCollection();
services.AddInfrastructure(configuration);
services.AddSingleton(settings);
services.AddSingleton<IEmailSender, SmtpEmailSender>();
services.AddSingleton(sp => new CorreoProcessor(
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<IEmailSender>(),
    settings.Password));

await using var provider = services.BuildServiceProvider();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    Console.WriteLine("MailWorker: procesando correos pendientes...");
    var resumen = await provider.GetRequiredService<CorreoProcessor>().ProcesarAsync(cts.Token);
    Console.WriteLine($"MailWorker: terminado. Enviados={resumen.Enviados}, Fallidos={resumen.Fallidos}, Omitidos={resumen.Omitidos}.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("MailWorker: cancelado por el usuario.");
    return 1;
}
catch (Exception ex)
{
    // Solo el tipo de excepción: el mensaje de EF/SQL podría incluir datos de conexión
    Console.Error.WriteLine($"MailWorker: error inesperado ({ex.GetType().Name}). Verifique la base de datos y la configuración.");
    return 1;
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Core.Services;
using WhatAreWeEating.Infrastructure.Services;

namespace WhatAreWeEating.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // En ASP.NET Core, GetConnectionString("Default") busca la clave "ConnectionStrings:Default",
        // la cual se nutre automáticamente desde la variable de entorno ConnectionStrings__Default.
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        // Servicios transversales del Core
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPasswordValidator, PasswordValidator>();
        services.AddSingleton<IEmailValidator, EmailValidator>();
        services.AddSingleton<ITokenService, TokenService>();

        // Servicio de autenticación y control de acceso
        services.AddScoped<IAuthService, AuthService>();

        // Sesiones y JWT. La clave se valida al resolver (la API la valida además al arrancar);
        // así el MailWorker no necesita Jwt__Key.
        services.AddSingleton(_ => JwtOptionsLoader.Cargar(configuration));
        services.AddScoped<ISesionService, SesionService>();

        return services;
    }
}

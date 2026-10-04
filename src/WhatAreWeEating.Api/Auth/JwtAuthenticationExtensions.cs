using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Api.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtSessionAuthentication(this IServiceCollection services, JwtOptions jwt)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Conserva los nombres originales de los claims ("sub", "sid")
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // Además de la firma, la sesión debe existir, no estar revocada ni expirada y su usuario seguir activo
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal is null || !context.Principal.TryGetSesionId(out var sesionId))
                        {
                            context.Fail("Sesión inválida.");
                            return;
                        }

                        var sesiones = context.HttpContext.RequestServices.GetRequiredService<ISesionService>();
                        if (!await sesiones.ValidarSesionAsync(sesionId, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Sesión inválida.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await EscribirJsonAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            "No autenticado: la sesión no es válida, ha expirado o fue cerrada.");
                    },
                    OnForbidden = async context =>
                    {
                        await EscribirJsonAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                            "No tiene permisos para realizar esta operación.");
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static async Task EscribirJsonAsync(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { status, message }));
    }
}

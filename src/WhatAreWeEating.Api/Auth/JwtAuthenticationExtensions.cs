using System.Security.Claims;
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
                        var usuario = await sesiones.ValidarYObtenerUsuarioAsync(sesionId, context.HttpContext.RequestAborted);
                        if (usuario is null)
                        {
                            context.Fail("Sesión inválida.");
                            return;
                        }

                        // RD-06: el rol se toma de la BD en cada petición, nunca del token
                        if (context.Principal.Identity is ClaimsIdentity identity)
                        {
                            foreach (var claim in identity.FindAll(c => c.Type is ClaimTypes.Role or "role" or "roles").ToList())
                            {
                                identity.RemoveClaim(claim);
                            }

                            identity.AddClaim(new Claim(ClaimTypes.Role, usuario.Rol.ToString()));
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

        services.AddAuthorization(PoliciesCatalogo.Registrar);
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

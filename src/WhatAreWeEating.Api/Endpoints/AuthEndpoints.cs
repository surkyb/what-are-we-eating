using WhatAreWeEating.Api.DTOs;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Autenticación");

        // RF-CA-01, RF-CA-14, RF-CA-15
        group.MapPost("/registro", async (
            RegistroRequest request,
            IAuthService authService,
            IConfiguration configuration,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var baseUrl = GetBaseUrl(configuration, httpContext);
            var (success, errorMessage) = await authService.RegistrarUsuarioAsync(
                request.Nombre,
                request.Correo,
                request.Password,
                baseUrl,
                cancellationToken);

            if (!success)
            {
                return Results.BadRequest(new MensajeResponse(errorMessage ?? "No fue posible completar el registro."));
            }

            return Results.Created(
                string.Empty,
                new MensajeResponse("Usuario registrado exitosamente. Se ha enviado un correo con el enlace para activar su cuenta."));
        })
        .WithName("RegistrarUsuario")
        .Produces<MensajeResponse>(StatusCodes.Status201Created)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest);

        // RF-CA-16
        group.MapGet("/activar", async (
            string? token,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new MensajeResponse("El token de activación es requerido."));
            }

            var (success, errorMessage) = await authService.ActivarCuentaAsync(token, cancellationToken);
            if (!success)
            {
                return Results.BadRequest(new MensajeResponse(errorMessage ?? "El enlace de activación es inválido o ha expirado."));
            }

            return Results.Ok(new MensajeResponse("Cuenta activada exitosamente. Ya puede iniciar sesión."));
        })
        .WithName("ActivarCuenta")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest);

        // RF-CA-17
        group.MapPost("/reenviar-activacion", async (
            ReenviarActivacionRequest request,
            IAuthService authService,
            IConfiguration configuration,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var baseUrl = GetBaseUrl(configuration, httpContext);
            var (success, errorMessage) = await authService.ReenviarActivacionAsync(
                request.Correo,
                baseUrl,
                cancellationToken);

            if (!success)
            {
                return Results.BadRequest(new MensajeResponse(errorMessage ?? "El correo electrónico provisto no es válido."));
            }

            // RF-CA-17: Respuesta idéntica tanto si el correo existe/está inactivo como si no existe o ya está activo
            return Results.Ok(new MensajeResponse(
                "Si la dirección de correo corresponde a una cuenta registrada y pendiente de activación, se ha enviado un nuevo enlace."));
        })
        .WithName("ReenviarActivacion")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static string GetBaseUrl(IConfiguration configuration, HttpContext httpContext)
    {
        var configUrl = configuration["App:BaseUrl"] ?? configuration["App__BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configUrl))
        {
            return configUrl;
        }

        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    }
}

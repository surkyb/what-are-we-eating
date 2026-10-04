using System.Security.Claims;
using WhatAreWeEating.Api.Auth;
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

        // RF-CA-03, RF-CA-15, RF-CA-19
        group.MapPost("/login", async (
            LoginRequest request,
            ISesionService sesionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Correo) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new MensajeResponse("El correo y la contraseña son obligatorios."));
            }

            if (request.Correo.Length > 256 || request.Password.Length > 256)
            {
                return Results.BadRequest(new MensajeResponse("El correo o la contraseña exceden la longitud permitida."));
            }

            var resultado = await sesionService.LoginAsync(request.Correo, request.Password, cancellationToken);

            return resultado.Resultado switch
            {
                ResultadoLogin.Exito => Results.Ok(new LoginResponse(resultado.Token!, "Bearer", resultado.Expira!.Value)),
                ResultadoLogin.CuentaInactiva => Results.Json(new MensajeResponse(resultado.Mensaje), statusCode: StatusCodes.Status403Forbidden),
                ResultadoLogin.CuentaBloqueada => Results.Json(new MensajeResponse(resultado.Mensaje), statusCode: StatusCodes.Status423Locked),
                _ => Results.Json(new MensajeResponse(resultado.Mensaje), statusCode: StatusCodes.Status401Unauthorized)
            };
        })
        .WithName("Login")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces<MensajeResponse>(StatusCodes.Status400BadRequest)
        .Produces<MensajeResponse>(StatusCodes.Status401Unauthorized)
        .Produces<MensajeResponse>(StatusCodes.Status403Forbidden)
        .Produces<MensajeResponse>(StatusCodes.Status423Locked);

        // RF-CA-07
        group.MapGet("/me", async (
            ClaimsPrincipal user,
            ISesionService sesionService,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetUsuarioId(out var usuarioId))
            {
                return Results.Unauthorized();
            }

            var usuario = await sesionService.ObtenerUsuarioAsync(usuarioId, cancellationToken);
            if (usuario is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new MeResponse(usuario.Nombre, usuario.Correo, usuario.Rol.ToString()));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.ConsultarPerfil)
        .WithName("ObtenerUsuarioAutenticado")
        .Produces<MeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // RF-CA-18
        group.MapPost("/logout", async (
            ClaimsPrincipal user,
            ISesionService sesionService,
            CancellationToken cancellationToken) =>
        {
            if (!user.TryGetSesionId(out var sesionId))
            {
                return Results.Unauthorized();
            }

            await sesionService.CerrarSesionAsync(sesionId, cancellationToken);
            return Results.Ok(new MensajeResponse("Sesión cerrada correctamente."));
        })
        .RequiereOperacion(PoliciesCatalogo.Operaciones.CerrarSesion)
        .WithName("CerrarSesion")
        .Produces<MensajeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

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

using System.Text.Json;

namespace WhatAreWeEating.Api.Middlewares;

/// <summary>
/// Middleware global para captura y manejo de excepciones no controladas (RD-08).
/// Garantiza respuestas controladas en JSON sin exponer trazas, detalles internos ni SQL al cliente.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException)
        {
            // JSON roto, cuerpo vacío o con tipos incorrectos: rechazo controlado en cualquier entorno (RD-07, RD-08)
            _logger.LogWarning("Solicitud con cuerpo inválido rechazada en {Path}", context.Request.Path);
            await EscribirJsonAsync(context, StatusCodes.Status400BadRequest,
                "La solicitud no es válida: el cuerpo está ausente, mal formado o tiene datos de tipo incorrecto.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada capturada en {Path}", context.Request.Path);
            await EscribirJsonAsync(context, StatusCodes.Status500InternalServerError,
                "Ha ocurrido un error inesperado al procesar su solicitud. Por favor, intente más tarde.");
        }
    }

    private static async Task EscribirJsonAsync(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(new { status, message });
        await context.Response.WriteAsync(json);
    }
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlingMiddleware>();
    }
}

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

/// <summary>
/// Cliente de la API externa OpenWeatherMap (https://openweathermap.org/current). La clave se lee de la variable
/// de entorno OpenWeather__ApiKey (RD-10); nunca se escribe en el código, en los logs ni en los mensajes devueltos.
/// Cualquier fallo (sin clave, clave rechazada, sin red, tiempo agotado, respuesta ilegible) es un rechazo controlado.
/// </summary>
public class OpenWeatherClimaService : IClimaService
{
    public const string UrlBase = "https://api.openweathermap.org/";
    private const int LongitudMaximaCiudad = 100;

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public OpenWeatherClimaService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiKey = configuration["OpenWeather:ApiKey"];
    }

    public async Task<RespuestaClima> ObtenerAsync(string? ciudad, CancellationToken ct = default)
    {
        ciudad = ciudad?.Trim();
        if (string.IsNullOrEmpty(ciudad) || ciudad.Length > LongitudMaximaCiudad || ciudad.Any(char.IsControl))
        {
            return new(ResultadoClima.CiudadInvalida, Mensaje: "Indique una ciudad válida (hasta 100 caracteres).");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new(ResultadoClima.ClaveNoConfigurada,
                Mensaje: "El servicio de clima no está configurado: falta la variable de entorno OpenWeather__ApiKey.");
        }

        try
        {
            var url = $"data/2.5/weather?q={Uri.EscapeDataString(ciudad)}&units=metric&lang=es&appid={Uri.EscapeDataString(_apiKey)}";
            using var respuesta = await _http.GetAsync(url, ct);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return new(ResultadoClima.CiudadNoEncontrada, Mensaje: $"No se encontró la ciudad '{ciudad}'.");
            }

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new(ResultadoClima.ApiNoDisponible,
                    Mensaje: "El servicio de clima rechazó la clave configurada. Verifique OpenWeather__ApiKey.");
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                return NoDisponible();
            }

            await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: ct);
            var raiz = json.RootElement;

            var temperatura = raiz.GetProperty("main").GetProperty("temp").GetDecimal();
            var descripcion = raiz.TryGetProperty("weather", out var w) && w.ValueKind == JsonValueKind.Array && w.GetArrayLength() > 0
                ? w[0].GetProperty("description").GetString() ?? string.Empty
                : string.Empty;
            var nombre = raiz.TryGetProperty("name", out var n) ? n.GetString() ?? ciudad : ciudad;
            var pais = raiz.TryGetProperty("sys", out var s) && s.TryGetProperty("country", out var c) ? c.GetString() ?? string.Empty : string.Empty;

            return new(ResultadoClima.Ok, new ClimaDto(nombre, pais, decimal.Round(temperatura, 1, MidpointRounding.AwayFromZero), descripcion));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // Si el cliente cancela la solicitud se propaga; todo lo demás es la API caída, lenta o ilegible
            if (ct.IsCancellationRequested) throw;
            return NoDisponible();
        }
    }

    private static RespuestaClima NoDisponible()
        => new(ResultadoClima.ApiNoDisponible, Mensaje: "El servicio de clima no está disponible en este momento. Intente más tarde.");
}

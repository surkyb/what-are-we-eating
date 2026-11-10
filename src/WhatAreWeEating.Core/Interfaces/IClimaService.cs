namespace WhatAreWeEating.Core.Interfaces;

public sealed record ClimaDto(string Ciudad, string Pais, decimal TemperaturaC, string Descripcion);

public enum ResultadoClima
{
    Ok,
    CiudadInvalida,
    CiudadNoEncontrada,
    ClaveNoConfigurada,
    ApiNoDisponible
}

public sealed record RespuestaClima(ResultadoClima Resultado, ClimaDto? Clima = null, string? Mensaje = null)
{
    public bool Exito => Resultado == ResultadoClima.Ok;
}

/// <summary>Acceso a la API externa de clima (OpenWeatherMap). La clave sale de OpenWeather__ApiKey (RD-10).</summary>
public interface IClimaService
{
    Task<RespuestaClima> ObtenerAsync(string? ciudad, CancellationToken ct = default);
}

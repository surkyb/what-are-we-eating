using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Recetas.Interfaces;

public sealed record SugerenciaRecetas(ClimaDto Clima, string Criterio, IReadOnlyList<RecetaDto> Recetas);

public sealed record ResultadoSugerencia(ResultadoClima Codigo, SugerenciaRecetas? Valor = null, string? Mensaje = null)
{
    public bool Exito => Codigo == ResultadoClima.Ok;
}

/// <summary>Sugiere recetas publicadas según el clima actual de una ciudad (usa la API externa de clima).</summary>
public interface ISugerenciaRecetasService
{
    Task<ResultadoSugerencia> SugerirPorClimaAsync(string? ciudad, CancellationToken ct = default);
}

using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Recetas.Enums;
using WhatAreWeEating.Recetas.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

/// <summary>
/// Operación de negocio que usa la API externa: según la temperatura actual de la ciudad elige qué recetas
/// publicadas sugerir (calor: platos frescos; frío: platos calientes; templado: todas).
/// </summary>
public class SugerenciaRecetasService : ISugerenciaRecetasService
{
    public const decimal UmbralCalorC = 27m;
    public const decimal UmbralFrioC = 18m;

    private static readonly string[] PalabrasFrescas =
        ["ensalada", "fría", "frío", "fresca", "fresco", "helado", "batida", "jugo", "bebida", "postre", "ceviche", "gazpacho", "smoothie"];

    private static readonly string[] PalabrasCalientes =
        ["sopa", "caldo", "guiso", "estofado", "sancocho", "crema", "locro", "puchero", "chocolate", "horneado", "asado"];

    private readonly AppDbContext _context;
    private readonly IClimaService _clima;

    public SugerenciaRecetasService(AppDbContext context, IClimaService clima)
    {
        _context = context;
        _clima = clima;
    }

    public async Task<ResultadoSugerencia> SugerirPorClimaAsync(string? ciudad, CancellationToken ct = default)
    {
        var respuesta = await _clima.ObtenerAsync(ciudad, ct);
        if (!respuesta.Exito)
        {
            return new(respuesta.Resultado, Mensaje: respuesta.Mensaje);
        }

        var clima = respuesta.Clima!;
        var publicadas = await _context.Recetas.AsNoTracking()
            .Include(r => r.Categoria)
            .Include(r => r.RecetaIngredientes).ThenInclude(ri => ri.Ingrediente)
            .Where(r => r.Estado == EstadoReceta.Publicada)
            .OrderBy(r => r.Nombre)
            .ToListAsync(ct);

        var (palabras, criterio) = Clasificar(clima.TemperaturaC);
        var elegidas = palabras is null
            ? publicadas
            : publicadas.Where(r => Contiene(palabras, r.Nombre) || Contiene(palabras, r.Categoria.Nombre)).ToList();

        if (palabras is not null && elegidas.Count == 0)
        {
            criterio += " No hay recetas publicadas de ese tipo, así que se muestran todas las publicadas.";
            elegidas = publicadas;
        }

        var dtos = elegidas.Select(r => new RecetaDto(
            r.Id, r.Nombre, r.Descripcion, r.Instrucciones, r.AutorId, r.CategoriaId, r.Estado, r.MotivoRechazo,
            r.RecetaIngredientes.Select(ri => new IngredienteRecetaDto(ri.IngredienteId, ri.Ingrediente.Nombre, ri.Cantidad, ri.Unidad)).ToList())).ToList();

        return new(ResultadoClima.Ok, new SugerenciaRecetas(clima, criterio, dtos));
    }

    public static (string[]? Palabras, string Criterio) Clasificar(decimal temperaturaC) => temperaturaC switch
    {
        >= UmbralCalorC => (PalabrasFrescas, $"Hace calor (>= {UmbralCalorC} °C): se sugieren platos frescos."),
        <= UmbralFrioC => (PalabrasCalientes, $"Hace frío (<= {UmbralFrioC} °C): se sugieren platos calientes."),
        _ => (null, "Clima templado: se sugieren todas las recetas publicadas.")
    };

    private static bool Contiene(string[] palabras, string texto)
        => palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));
}

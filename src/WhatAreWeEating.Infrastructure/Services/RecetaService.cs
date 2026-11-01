using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Core.Permisos;
using WhatAreWeEating.Recetas;
using WhatAreWeEating.Recetas.Entities;
using WhatAreWeEating.Recetas.Enums;
using WhatAreWeEating.Recetas.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public class RecetaService : IRecetaService
{
    private readonly AppDbContext _context;
    private readonly IPermisoService _permisos;

    public RecetaService(AppDbContext context, IPermisoService permisos)
    {
        _context = context;
        _permisos = permisos;
    }

    // ---- Catálogos de apoyo ----

    public async Task<Resultado<ElementoCatalogoDto>> CrearCategoriaAsync(string? nombre, CancellationToken ct = default)
    {
        var limpio = nombre?.Trim();
        if (string.IsNullOrEmpty(limpio) || limpio.Length > 100)
        {
            return Invalido<ElementoCatalogoDto>("El nombre de la categoría es obligatorio (máximo 100 caracteres).");
        }

        var categoria = new Categoria { Id = Guid.NewGuid(), Nombre = limpio };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync(ct);
        return new(ResultadoReceta.Ok, new ElementoCatalogoDto(categoria.Id, categoria.Nombre));
    }

    public async Task<Resultado<ElementoCatalogoDto>> CrearIngredienteAsync(string? nombre, string? unidadBase, CancellationToken ct = default)
    {
        var limpio = nombre?.Trim();
        var unidad = string.IsNullOrWhiteSpace(unidadBase) ? null : unidadBase.Trim();
        if (string.IsNullOrEmpty(limpio) || limpio.Length > 150 || unidad?.Length > 50)
        {
            return Invalido<ElementoCatalogoDto>("El nombre del ingrediente es obligatorio (máximo 150 caracteres).");
        }

        var ingrediente = new Ingrediente { Id = Guid.NewGuid(), Nombre = limpio, UnidadBase = unidad };
        _context.Ingredientes.Add(ingrediente);
        await _context.SaveChangesAsync(ct);
        return new(ResultadoReceta.Ok, new ElementoCatalogoDto(ingrediente.Id, ingrediente.Nombre, ingrediente.UnidadBase));
    }

    public async Task<IReadOnlyList<ElementoCatalogoDto>> ListarCategoriasAsync(CancellationToken ct = default)
        => await _context.Categorias.AsNoTracking().OrderBy(c => c.Nombre)
            .Select(c => new ElementoCatalogoDto(c.Id, c.Nombre, null)).ToListAsync(ct);

    public async Task<IReadOnlyList<ElementoCatalogoDto>> ListarIngredientesAsync(CancellationToken ct = default)
        => await _context.Ingredientes.AsNoTracking().OrderBy(i => i.Nombre)
            .Select(i => new ElementoCatalogoDto(i.Id, i.Nombre, i.UnidadBase)).ToListAsync(ct);

    // ---- 1. Crear ----

    public async Task<Resultado<RecetaDto>> CrearAsync(Guid autorId, RecetaNueva nueva, CancellationToken ct = default)
    {
        var nombre = nueva.Nombre?.Trim();
        var instrucciones = nueva.Instrucciones?.Trim();
        if (string.IsNullOrEmpty(nombre) || nombre.Length > 200 || string.IsNullOrEmpty(instrucciones))
        {
            return Invalido<RecetaDto>("El nombre (máximo 200 caracteres) y las instrucciones son obligatorios.");
        }

        if (!await _context.Categorias.AnyAsync(c => c.Id == nueva.CategoriaId, ct))
        {
            return Invalido<RecetaDto>("La categoría indicada no existe.");
        }

        var receta = new Receta
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(nueva.Descripcion) ? null : nueva.Descripcion.Trim(),
            Instrucciones = instrucciones,
            AutorId = autorId,
            CategoriaId = nueva.CategoriaId,
            Estado = EstadoReceta.Borrador
        };

        foreach (var linea in nueva.Ingredientes ?? [])
        {
            var ingrediente = await _context.Ingredientes.FirstOrDefaultAsync(i => i.Id == linea.IngredienteId, ct);
            var unidad = string.IsNullOrWhiteSpace(linea.Unidad) ? ingrediente?.UnidadBase : linea.Unidad.Trim();
            if (ingrediente is null || linea.Cantidad <= 0 || string.IsNullOrEmpty(unidad) || unidad.Length > 50
                || receta.RecetaIngredientes.Any(r => r.IngredienteId == ingrediente.Id))
            {
                return Invalido<RecetaDto>("Cada ingrediente debe existir una sola vez, con cantidad mayor que cero y unidad.");
            }

            receta.RecetaIngredientes.Add(new RecetaIngrediente
            {
                RecetaId = receta.Id, IngredienteId = ingrediente.Id, Cantidad = linea.Cantidad, Unidad = unidad
            });
        }

        _context.Recetas.Add(receta);
        await _context.SaveChangesAsync(ct);
        return await CargarDtoAsync(receta.Id, ct);
    }

    // ---- 2 a 5. Transiciones ----

    public Task<Resultado<RecetaDto>> EnviarARevisionAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default)
        => CambiarEstadoAsync(recetaId, EstadoReceta.EnRevision, actorId, rol, null, ct);

    public Task<Resultado<RecetaDto>> PublicarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default)
        => CambiarEstadoAsync(recetaId, EstadoReceta.Publicada, actorId, rol, null, ct);

    public Task<Resultado<RecetaDto>> RechazarAsync(Guid actorId, RolUsuario rol, Guid recetaId, string? motivo, CancellationToken ct = default)
        => CambiarEstadoAsync(recetaId, EstadoReceta.Rechazada, actorId, rol, motivo, ct);

    public Task<Resultado<RecetaDto>> RetomarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default)
        => CambiarEstadoAsync(recetaId, EstadoReceta.Borrador, actorId, rol, null, ct);

    public Task<Resultado<RecetaDto>> ArchivarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default)
        => CambiarEstadoAsync(recetaId, EstadoReceta.Archivada, actorId, rol, null, ct);

    private async Task<Resultado<RecetaDto>> CambiarEstadoAsync(
        Guid recetaId, EstadoReceta destino, Guid actorId, RolUsuario rol, string? motivo, CancellationToken ct)
    {
        var receta = await _context.Recetas
            .Include(r => r.RecetaIngredientes).ThenInclude(ri => ri.Ingrediente)
            .FirstOrDefaultAsync(r => r.Id == recetaId, ct);
        if (receta is null)
        {
            return new(ResultadoReceta.NoEncontrada, Mensaje: "La receta no existe.");
        }

        // Toda la regla vive en Receta.Transicionar (RD-04); aquí solo se persiste si fue Ok
        var resultado = receta.Transicionar(destino, actorId, rol, motivo);
        if (resultado != ResultadoReceta.Ok)
        {
            return new(resultado, Mensaje: MensajeDe(resultado));
        }

        await _context.SaveChangesAsync(ct);
        return new(ResultadoReceta.Ok, ADto(receta));
    }

    // ---- 6. Consultas ----

    public Task<IReadOnlyList<RecetaDto>> ListarPublicadasAsync(CancellationToken ct = default)
        => ListarAsync(r => r.Estado == EstadoReceta.Publicada, ct);

    public Task<IReadOnlyList<RecetaDto>> ListarEnRevisionAsync(CancellationToken ct = default)
        => ListarAsync(r => r.Estado == EstadoReceta.EnRevision, ct);

    public Task<IReadOnlyList<RecetaDto>> ListarDeAutorAsync(Guid autorId, CancellationToken ct = default)
        => ListarAsync(r => r.AutorId == autorId, ct);

    // ---- 7. Despensa ----

    public async Task<Resultado<DespensaItemDto>> GuardarEnDespensaAsync(
        Guid usuarioId, Guid ingredienteId, decimal cantidad, string? unidad, CancellationToken ct = default)
    {
        var ingrediente = await _context.Ingredientes.FirstOrDefaultAsync(i => i.Id == ingredienteId, ct);
        var unidadFinal = string.IsNullOrWhiteSpace(unidad) ? ingrediente?.UnidadBase : unidad.Trim();
        if (ingrediente is null || cantidad < 0 || string.IsNullOrEmpty(unidadFinal) || unidadFinal.Length > 50)
        {
            return Invalido<DespensaItemDto>("El ingrediente debe existir, la cantidad no puede ser negativa y la unidad es obligatoria.");
        }

        var despensa = await _context.Despensas.FirstOrDefaultAsync(d => d.UsuarioId == usuarioId, ct);
        if (despensa is null)
        {
            despensa = new Despensa { Id = Guid.NewGuid(), UsuarioId = usuarioId };
            _context.Despensas.Add(despensa);
        }

        var item = await _context.DespensaIngredientes
            .FirstOrDefaultAsync(di => di.DespensaId == despensa.Id && di.IngredienteId == ingredienteId, ct);
        if (item is null)
        {
            _context.DespensaIngredientes.Add(new DespensaIngrediente
            {
                DespensaId = despensa.Id, IngredienteId = ingredienteId, CantidadDisponible = cantidad, Unidad = unidadFinal
            });
        }
        else
        {
            item.CantidadDisponible = cantidad;
            item.Unidad = unidadFinal;
        }

        await _context.SaveChangesAsync(ct);
        return new(ResultadoReceta.Ok, new DespensaItemDto(ingredienteId, ingrediente.Nombre, cantidad, unidadFinal));
    }

    public async Task<IReadOnlyList<DespensaItemDto>> ListarDespensaAsync(Guid usuarioId, CancellationToken ct = default)
        => await _context.DespensaIngredientes.AsNoTracking()
            .Where(di => di.Despensa.UsuarioId == usuarioId)
            .OrderBy(di => di.Ingrediente.Nombre)
            .Select(di => new DespensaItemDto(di.IngredienteId, di.Ingrediente.Nombre, di.CantidadDisponible, di.Unidad))
            .ToListAsync(ct);

    // ---- 8. Preparables (requiere permiso concedido) ----

    public async Task<Resultado<IReadOnlyList<RecetaDto>>> ListarPreparablesAsync(Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _permisos.TienePermisoAsync(usuarioId, CatalogoPermisos.ConsultarRecetasPreparables, ct))
        {
            return new(ResultadoReceta.PermisoRequerido,
                Mensaje: $"Requiere el permiso '{CatalogoPermisos.ConsultarRecetasPreparables}'. Solicítelo en POST /permisos/solicitudes.");
        }

        var despensa = await _context.DespensaIngredientes.AsNoTracking()
            .Where(di => di.Despensa.UsuarioId == usuarioId)
            .ToDictionaryAsync(di => di.IngredienteId, ct);

        var publicadas = await _context.Recetas.AsNoTracking()
            .Include(r => r.RecetaIngredientes).ThenInclude(ri => ri.Ingrediente)
            .Where(r => r.Estado == EstadoReceta.Publicada)
            .OrderBy(r => r.Nombre)
            .ToListAsync(ct);

        var preparables = publicadas
            .Where(r => r.RecetaIngredientes.Count > 0 && r.RecetaIngredientes.All(ri =>
                despensa.TryGetValue(ri.IngredienteId, out var d)
                && string.Equals(d.Unidad, ri.Unidad, StringComparison.OrdinalIgnoreCase)
                && d.CantidadDisponible >= ri.Cantidad))
            .Select(ADto)
            .ToList();

        return new(ResultadoReceta.Ok, preparables);
    }

    // ---- Apoyo ----

    private async Task<IReadOnlyList<RecetaDto>> ListarAsync(System.Linq.Expressions.Expression<Func<Receta, bool>> filtro, CancellationToken ct)
    {
        var recetas = await _context.Recetas.AsNoTracking()
            .Include(r => r.RecetaIngredientes).ThenInclude(ri => ri.Ingrediente)
            .Where(filtro).OrderBy(r => r.Nombre).ToListAsync(ct);
        return recetas.Select(ADto).ToList();
    }

    private async Task<Resultado<RecetaDto>> CargarDtoAsync(Guid id, CancellationToken ct)
    {
        var receta = await _context.Recetas.AsNoTracking()
            .Include(r => r.RecetaIngredientes).ThenInclude(ri => ri.Ingrediente)
            .FirstAsync(r => r.Id == id, ct);
        return new(ResultadoReceta.Ok, ADto(receta));
    }

    private static Resultado<T> Invalido<T>(string mensaje) => new(ResultadoReceta.DatosInvalidos, default, mensaje);

    private static string MensajeDe(ResultadoReceta resultado) => resultado switch
    {
        ResultadoReceta.TransicionNoPermitida => "La transición no está permitida desde el estado actual de la receta.",
        ResultadoReceta.RolNoPermitido => "Su rol no puede ejecutar esta transición.",
        ResultadoReceta.NoEsAutor => "Solo el autor de la receta puede hacerlo.",
        ResultadoReceta.DatosIncompletos => "La receta necesita nombre, instrucciones y al menos un ingrediente.",
        ResultadoReceta.MotivoRequerido => "El rechazo requiere un motivo escrito.",
        _ => "Operación no permitida."
    };

    private static RecetaDto ADto(Receta r) => new(
        r.Id, r.Nombre, r.Descripcion, r.Instrucciones, r.AutorId, r.CategoriaId, r.Estado, r.MotivoRechazo,
        r.RecetaIngredientes
            .Select(ri => new IngredienteRecetaDto(ri.IngredienteId, ri.Ingrediente?.Nombre ?? string.Empty, ri.Cantidad, ri.Unidad))
            .ToList());
}

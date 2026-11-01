using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Recetas.Enums;

namespace WhatAreWeEating.Recetas.Interfaces;

public sealed record IngredienteRecetaDto(Guid IngredienteId, string Nombre, decimal Cantidad, string Unidad);

public sealed record RecetaDto(
    Guid Id, string Nombre, string? Descripcion, string Instrucciones, Guid AutorId, Guid CategoriaId,
    EstadoReceta Estado, string? MotivoRechazo, IReadOnlyList<IngredienteRecetaDto> Ingredientes);

public sealed record IngredienteNuevo(Guid IngredienteId, decimal Cantidad, string? Unidad);

public sealed record RecetaNueva(string? Nombre, string? Descripcion, string? Instrucciones, Guid CategoriaId, IReadOnlyList<IngredienteNuevo>? Ingredientes);

public sealed record ElementoCatalogoDto(Guid Id, string Nombre, string? UnidadBase = null);

public sealed record DespensaItemDto(Guid IngredienteId, string Nombre, decimal CantidadDisponible, string Unidad);

public sealed record Resultado<T>(ResultadoReceta Codigo, T? Valor = default, string? Mensaje = null)
{
    public bool Exito => Codigo == ResultadoReceta.Ok;
}

/// <summary>Operaciones del negocio de recetas. Cada una se puede usar y probar por separado (RF-NEG-02).</summary>
public interface IRecetaService
{
    // Catálogos de apoyo
    Task<Resultado<ElementoCatalogoDto>> CrearCategoriaAsync(string? nombre, CancellationToken ct = default);
    Task<Resultado<ElementoCatalogoDto>> CrearIngredienteAsync(string? nombre, string? unidadBase, CancellationToken ct = default);
    Task<IReadOnlyList<ElementoCatalogoDto>> ListarCategoriasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ElementoCatalogoDto>> ListarIngredientesAsync(CancellationToken ct = default);

    // 1. Crear (nace en Borrador)
    Task<Resultado<RecetaDto>> CrearAsync(Guid autorId, RecetaNueva nueva, CancellationToken ct = default);

    // 2 a 5. Transiciones de la máquina de estados
    Task<Resultado<RecetaDto>> EnviarARevisionAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default);
    Task<Resultado<RecetaDto>> PublicarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default);
    Task<Resultado<RecetaDto>> RechazarAsync(Guid actorId, RolUsuario rol, Guid recetaId, string? motivo, CancellationToken ct = default);
    Task<Resultado<RecetaDto>> RetomarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default);
    Task<Resultado<RecetaDto>> ArchivarAsync(Guid actorId, RolUsuario rol, Guid recetaId, CancellationToken ct = default);

    // 6. Consultas por estado
    Task<IReadOnlyList<RecetaDto>> ListarPublicadasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecetaDto>> ListarEnRevisionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecetaDto>> ListarDeAutorAsync(Guid autorId, CancellationToken ct = default);

    // 7. Despensa del usuario
    Task<Resultado<DespensaItemDto>> GuardarEnDespensaAsync(Guid usuarioId, Guid ingredienteId, decimal cantidad, string? unidad, CancellationToken ct = default);
    Task<IReadOnlyList<DespensaItemDto>> ListarDespensaAsync(Guid usuarioId, CancellationToken ct = default);

    // 8. Requiere el permiso concedido por Gestión de permisos (recetas.consultar-preparables)
    Task<Resultado<IReadOnlyList<RecetaDto>>> ListarPreparablesAsync(Guid usuarioId, CancellationToken ct = default);
}

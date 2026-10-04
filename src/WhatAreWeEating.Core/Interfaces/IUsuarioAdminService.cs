using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Interfaces;

public sealed record UsuarioAdminDto(Guid Id, string Nombre, string Correo, RolUsuario Rol, bool Activo);

public enum ResultadoAdmin
{
    Ok,
    NoEncontrado,
    OperacionNoPermitida
}

public sealed record AdminResultado(ResultadoAdmin Resultado, string? Mensaje = null)
{
    public bool Exito => Resultado == ResultadoAdmin.Ok;
}

public interface IUsuarioAdminService
{
    /// <summary>Lista usuarios con datos no sensibles (RF-CA-21).</summary>
    Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Cambia el rol de un usuario. Un administrador no puede cambiar su propio rol.</summary>
    Task<AdminResultado> CambiarRolAsync(Guid actorId, Guid usuarioId, RolUsuario nuevoRol, CancellationToken cancellationToken = default);

    /// <summary>Desactiva al usuario y revoca todas sus sesiones. Nadie puede desactivarse a sí mismo (RF-CA-20).</summary>
    Task<AdminResultado> DesactivarAsync(Guid actorId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Reactiva a un usuario desactivado.</summary>
    Task<AdminResultado> ReactivarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida la contraseña actual (ninguna contraseña podrá producir el nuevo hash), revoca las sesiones
    /// y encola un código de recuperación (RF-CA-13). Un administrador no puede forzarse a sí mismo.
    /// </summary>
    Task<AdminResultado> ForzarRestablecimientoAsync(Guid actorId, Guid usuarioId, CancellationToken cancellationToken = default);
}

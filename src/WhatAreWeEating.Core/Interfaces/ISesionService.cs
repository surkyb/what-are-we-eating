using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Core.Interfaces;

public enum ResultadoLogin
{
    Exito,
    CredencialesInvalidas,
    CuentaInactiva,
    CuentaBloqueada
}

public sealed record LoginResultado(ResultadoLogin Resultado, string Mensaje, string? Token = null, DateTime? Expira = null)
{
    public bool Exito => Resultado == ResultadoLogin.Exito;
}

public sealed record UsuarioAutenticado(Guid Id, string Nombre, string Correo, RolUsuario Rol);

public interface ISesionService
{
    /// <summary>
    /// Autentica al usuario y crea una sesión de 8 horas. Correo inexistente y contraseña incorrecta
    /// devuelven exactamente el mismo resultado (RF-CA-03). Bloqueo tras 5 fallos (RF-CA-19).
    /// </summary>
    Task<LoginResultado> LoginAsync(string correo, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// True solo si la sesión existe, no está revocada, no ha expirado y su usuario está activo.
    /// </summary>
    Task<bool> ValidarSesionAsync(Guid sesionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca la sesión indicada (RF-CA-18).
    /// </summary>
    Task CerrarSesionAsync(Guid sesionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca todas las sesiones vigentes del usuario (desactivación y cambio de contraseña).
    /// </summary>
    Task RevocarSesionesDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve nombre, correo y rol del usuario, nunca hashes (RF-CA-07).
    /// </summary>
    Task<UsuarioAutenticado?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

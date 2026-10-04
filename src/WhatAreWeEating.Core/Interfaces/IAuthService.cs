namespace WhatAreWeEating.Core.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Registra un nuevo usuario inactivo, genera su token de activación y encola el correo con el enlace (RF-CA-01, RF-CA-15).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> RegistrarUsuarioAsync(string nombre, string correo, string password, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa la cuenta asociada al token provisto si es válido, no ha expirado y no ha sido utilizado (RF-CA-16).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ActivarCuentaAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reenvía el enlace de activación invalidando tokens previos. La operación retorna el mismo mensaje sin revelar si el correo existe o no (RF-CA-17).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ReenviarActivacionAsync(string correo, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si el correo pertenece a un usuario activo, invalida códigos previos y encola un código de recuperación
    /// de 30 minutos. Devuelve siempre el mismo resultado exista o no el correo (RF-CA-09, RF-CA-10).
    /// Solo falla por formato de correo inválido.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> SolicitarRecuperacionAsync(string correo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la contraseña con un código válido, lo marca usado, reinicia el bloqueo y revoca todas las sesiones (RF-CA-10, 11, 12, 14).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> RestablecerPasswordAsync(string codigo, string passwordNueva, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la contraseña de un usuario autenticado verificando la actual y revoca todas sus sesiones (RF-CA-12, 14, 22).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> CambiarPasswordAsync(Guid usuarioId, string passwordActual, string passwordNueva, CancellationToken cancellationToken = default);
}

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
}

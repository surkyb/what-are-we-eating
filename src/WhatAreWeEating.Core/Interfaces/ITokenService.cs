namespace WhatAreWeEating.Core.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Genera un token aleatorio seguro de 32 bytes codificado en Base64Url y su respectivo hash SHA256.
    /// </summary>
    (string RawToken, string TokenHash) GenerateToken();

    /// <summary>
    /// Calcula el hash SHA256 de un token en claro para compararlo con el valor almacenado en la base de datos.
    /// </summary>
    string HashToken(string rawToken);
}

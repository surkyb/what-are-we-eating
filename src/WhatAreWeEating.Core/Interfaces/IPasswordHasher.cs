namespace WhatAreWeEating.Core.Interfaces;

public interface IPasswordHasher
{
    /// <summary>
    /// Genera un hash seguro utilizando PBKDF2 (SHA256, 100,000 iteraciones) y una sal aleatoria única.
    /// Formato devuelto: "{iteraciones}.{salBase64}.{hashBase64}"
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifica si la contraseña en texto claro coincide con el hash almacenado utilizando comparación en tiempo constante.
    /// </summary>
    bool VerifyPassword(string password, string storedHash);
}

namespace WhatAreWeEating.Core.Interfaces;

public interface IPasswordValidator
{
    /// <summary>
    /// Valida que la contraseña cumpla con los requisitos mínimos: al menos 8 caracteres, una letra y un número (RF-CA-14).
    /// </summary>
    (bool IsValid, string? ErrorMessage) Validate(string password);
}

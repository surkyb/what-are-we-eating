namespace WhatAreWeEating.Core.Interfaces;

public interface IPasswordValidator
{
    /// <summary>
    /// Valida que la contraseña cumpla con los requisitos mínimos: entre 8 y 128 caracteres, una letra y un número (RF-CA-14).
    /// </summary>
    (bool IsValid, string? ErrorMessage) Validate(string password);
}

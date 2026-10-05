using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Core.Services;

public class PasswordValidator : IPasswordValidator
{
    public const int LongitudMaxima = 128;

    public (bool IsValid, string? ErrorMessage) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "La contraseña es requerida.");
        }

        if (password.Length < 8)
        {
            return (false, "La contraseña debe tener al menos 8 caracteres.");
        }

        if (password.Length > LongitudMaxima)
        {
            return (false, $"La contraseña no puede exceder los {LongitudMaxima} caracteres.");
        }

        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            return (false, "La contraseña debe contener al menos una letra y un número.");
        }

        return (true, null);
    }
}

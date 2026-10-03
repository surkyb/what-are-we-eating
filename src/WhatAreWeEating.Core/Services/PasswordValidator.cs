using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Core.Services;

public class PasswordValidator : IPasswordValidator
{
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

        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            return (false, "La contraseña debe contener al menos una letra y un número.");
        }

        return (true, null);
    }
}

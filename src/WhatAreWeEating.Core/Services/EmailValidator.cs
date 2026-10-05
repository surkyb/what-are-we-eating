using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Core.Services;

public class EmailValidator : IEmailValidator
{
    private const int LongitudMaxima = 256;

    public bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > LongitudMaxima)
        {
            return false;
        }

        // Sin caracteres de control (incluido NUL) ni espacios en ninguna parte
        if (trimmed.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return false;
        }

        // Exactamente una arroba, con parte local y dominio no vacíos
        var arroba = trimmed.IndexOf('@');
        if (arroba <= 0 || arroba != trimmed.LastIndexOf('@') || arroba == trimmed.Length - 1)
        {
            return false;
        }

        var local = trimmed[..arroba];
        var dominio = trimmed[(arroba + 1)..];

        // Parte local: sin punto al inicio ni al final, ni puntos consecutivos
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains(".."))
        {
            return false;
        }

        // Dominio: al menos dos etiquetas, ninguna vacía ni con guion al inicio o al final
        var etiquetas = dominio.Split('.');
        if (etiquetas.Length < 2)
        {
            return false;
        }

        return etiquetas.All(e => e.Length > 0 && !e.StartsWith('-') && !e.EndsWith('-'));
    }
}

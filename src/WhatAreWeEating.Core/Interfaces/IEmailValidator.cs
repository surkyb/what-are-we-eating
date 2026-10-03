namespace WhatAreWeEating.Core.Interfaces;

public interface IEmailValidator
{
    /// <summary>
    /// Valida el formato del correo electrónico de forma controlada sin lanzar excepciones (RD-07).
    /// </summary>
    bool IsValid(string? email);
}

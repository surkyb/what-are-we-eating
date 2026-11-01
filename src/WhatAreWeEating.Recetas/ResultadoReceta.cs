namespace WhatAreWeEating.Recetas;

/// <summary>Resultado tipado de las operaciones del negocio; las rechazadas no modifican nada.</summary>
public enum ResultadoReceta
{
    Ok,
    NoEncontrada,
    TransicionNoPermitida,
    RolNoPermitido,
    NoEsAutor,
    DatosIncompletos,
    MotivoRequerido,
    DatosInvalidos,
    PermisoRequerido
}

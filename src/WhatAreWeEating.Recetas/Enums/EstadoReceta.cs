namespace WhatAreWeEating.Recetas.Enums;

/// <summary>
/// Estados del ciclo de vida de una Receta (RF-NEG-03). Es la ÚNICA declaración de estados.
/// Las transiciones permitidas entre ellos viven en <see cref="Estados.TransicionesReceta"/>.
/// </summary>
public enum EstadoReceta
{
    Borrador,
    EnRevision,
    Publicada,
    Rechazada,
    Archivada
}

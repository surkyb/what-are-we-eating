namespace WhatAreWeEating.Core.Permisos;

/// <summary>Permisos que un usuario puede solicitar. Cada uno habilita una acción concreta de la API.</summary>
public static class CatalogoPermisos
{
    /// <summary>Habilita GET /directorio (nombres y roles de usuarios activos).</summary>
    public const string ConsultarDirectorio = "directorio.consultar";

    public static readonly IReadOnlyList<string> Todos = [ConsultarDirectorio];

    public static bool Existe(string? permiso)
        => permiso is not null && Todos.Contains(permiso, StringComparer.Ordinal);
}

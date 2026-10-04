using Microsoft.AspNetCore.Authorization;
using WhatAreWeEating.Core.Enums;

namespace WhatAreWeEating.Api.Auth;

/// <summary>
/// ÚNICO punto de verdad de la autorización (RF-CA-05): qué rol puede ejecutar cada operación protegida.
/// Los endpoints solo referencian estas operaciones con .RequiereOperacion(...); no escriben roles sueltos.
/// Para cambiar quién puede hacer qué, se edita únicamente este archivo.
/// </summary>
public static class PoliciesCatalogo
{
    public static class Operaciones
    {
        // Sesión: cualquier usuario autenticado (rol = null)
        public const string ConsultarPerfil = "auth.consultar-perfil";
        public const string CerrarSesion = "auth.cerrar-sesion";

        // Administración de usuarios: solo Administrador
        public const string ListarUsuarios = "admin.usuarios.listar";
        public const string CambiarRolUsuario = "admin.usuarios.cambiar-rol";
        public const string DesactivarUsuario = "admin.usuarios.desactivar";
        public const string ReactivarUsuario = "admin.usuarios.reactivar";
    }

    /// <summary>operación -> rol requerido (null = cualquier usuario autenticado).</summary>
    public static readonly IReadOnlyDictionary<string, RolUsuario?> RolPorOperacion = new Dictionary<string, RolUsuario?>
    {
        [Operaciones.ConsultarPerfil] = null,
        [Operaciones.CerrarSesion] = null,

        [Operaciones.ListarUsuarios] = RolUsuario.Administrador,
        [Operaciones.CambiarRolUsuario] = RolUsuario.Administrador,
        [Operaciones.DesactivarUsuario] = RolUsuario.Administrador,
        [Operaciones.ReactivarUsuario] = RolUsuario.Administrador,
    };

    public static void Registrar(AuthorizationOptions options)
    {
        foreach (var (operacion, rol) in RolPorOperacion)
        {
            options.AddPolicy(operacion, policy =>
            {
                policy.RequireAuthenticatedUser();
                if (rol is not null)
                {
                    policy.RequireRole(rol.Value.ToString());
                }
            });
        }
    }

    public static TBuilder RequiereOperacion<TBuilder>(this TBuilder builder, string operacion)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(operacion);
}

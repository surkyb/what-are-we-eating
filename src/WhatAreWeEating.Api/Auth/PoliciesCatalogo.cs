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
        public const string CambiarPassword = "auth.cambiar-password";

        // Administración de usuarios: solo Administrador
        public const string ListarUsuarios = "admin.usuarios.listar";
        public const string CambiarRolUsuario = "admin.usuarios.cambiar-rol";
        public const string DesactivarUsuario = "admin.usuarios.desactivar";
        public const string ReactivarUsuario = "admin.usuarios.reactivar";
        public const string ForzarRestablecimiento = "admin.usuarios.forzar-restablecimiento";

        // Gestión de permisos: solicitar y consultar lo propio, cualquier autenticado; resolver, solo Administrador
        public const string SolicitarPermiso = "permisos.solicitar";
        public const string ListarMisSolicitudes = "permisos.listar-propias";
        public const string ListarSolicitudesPendientes = "permisos.listar-pendientes";
        public const string AprobarSolicitud = "permisos.aprobar";
        public const string RechazarSolicitud = "permisos.rechazar";

        // Acción habilitada por un permiso concedido: la concesión se verifica en el endpoint
        public const string ConsultarDirectorio = "directorio.consultar";

        // Módulo de recetas: altas de catálogo y decisiones de revisión, solo Administrador; autoría y despensa, solo Estándar; consultas de catálogo y de publicadas, ambos roles
        public const string CrearCategoria = "recetas.categorias.crear";
        public const string CrearIngrediente = "recetas.ingredientes.crear";
        public const string ListarCategorias = "recetas.categorias.listar";
        public const string ListarIngredientes = "recetas.ingredientes.listar";
        public const string CrearReceta = "recetas.crear";
        public const string EnviarRecetaARevision = "recetas.enviar-a-revision";
        public const string RetomarReceta = "recetas.retomar";
        public const string PublicarReceta = "recetas.publicar";
        public const string RechazarReceta = "recetas.rechazar";
        public const string ArchivarReceta = "recetas.archivar";
        public const string ListarRecetasPublicadas = "recetas.listar-publicadas";
        public const string ListarRecetasEnRevision = "recetas.listar-en-revision";
        public const string ListarMisRecetas = "recetas.listar-propias";
        public const string GuardarEnDespensa = "despensa.guardar";
        public const string ListarDespensa = "despensa.listar";
        public const string ListarRecetasPreparables = "recetas.listar-preparables";
    }

    private static readonly RolUsuario[] SoloAdministrador = [RolUsuario.Administrador];
    private static readonly RolUsuario[] SoloEstandar = [RolUsuario.Estandar];
    private static readonly RolUsuario[] Ambos = [RolUsuario.Estandar, RolUsuario.Administrador];

    /// <summary>
    /// operación -> roles que pueden ejecutarla (basta con tener uno). Toda operación del negocio declara al
    /// menos un rol (RF-NEG-06); null (cualquier usuario autenticado) queda solo para la sesión y los permisos del Core.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, RolUsuario[]?> RolesPorOperacion = new Dictionary<string, RolUsuario[]?>
    {
        [Operaciones.ConsultarPerfil] = null,
        [Operaciones.CerrarSesion] = null,
        [Operaciones.CambiarPassword] = null,

        [Operaciones.ListarUsuarios] = SoloAdministrador,
        [Operaciones.CambiarRolUsuario] = SoloAdministrador,
        [Operaciones.DesactivarUsuario] = SoloAdministrador,
        [Operaciones.ReactivarUsuario] = SoloAdministrador,
        [Operaciones.ForzarRestablecimiento] = SoloAdministrador,

        [Operaciones.SolicitarPermiso] = null,
        [Operaciones.ListarMisSolicitudes] = null,
        [Operaciones.ListarSolicitudesPendientes] = SoloAdministrador,
        [Operaciones.AprobarSolicitud] = SoloAdministrador,
        [Operaciones.RechazarSolicitud] = SoloAdministrador,

        [Operaciones.ConsultarDirectorio] = null,

        [Operaciones.CrearCategoria] = SoloAdministrador,
        [Operaciones.CrearIngrediente] = SoloAdministrador,
        [Operaciones.ListarCategorias] = Ambos,
        [Operaciones.ListarIngredientes] = Ambos,
        [Operaciones.CrearReceta] = SoloEstandar,
        [Operaciones.EnviarRecetaARevision] = SoloEstandar,
        [Operaciones.RetomarReceta] = SoloEstandar,
        [Operaciones.PublicarReceta] = SoloAdministrador,
        [Operaciones.RechazarReceta] = SoloAdministrador,
        [Operaciones.ArchivarReceta] = SoloAdministrador,
        [Operaciones.ListarRecetasPublicadas] = Ambos,
        [Operaciones.ListarRecetasEnRevision] = SoloAdministrador,
        [Operaciones.ListarMisRecetas] = SoloEstandar,
        [Operaciones.GuardarEnDespensa] = SoloEstandar,
        [Operaciones.ListarDespensa] = SoloEstandar,
        [Operaciones.ListarRecetasPreparables] = SoloEstandar,
    };

    public static void Registrar(AuthorizationOptions options)
    {
        foreach (var (operacion, roles) in RolesPorOperacion)
        {
            options.AddPolicy(operacion, policy =>
            {
                policy.RequireAuthenticatedUser();
                if (roles is not null)
                {
                    policy.RequireRole(roles.Select(r => r.ToString()));
                }
            });
        }
    }

    public static TBuilder RequiereOperacion<TBuilder>(this TBuilder builder, string operacion)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(operacion);
}

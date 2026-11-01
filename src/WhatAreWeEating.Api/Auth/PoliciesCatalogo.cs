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

        // Módulo de recetas: catálogos y decisiones de revisión, solo Administrador; autoría y despensa, solo Estándar
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

    /// <summary>operación -> rol requerido (null = cualquier usuario autenticado).</summary>
    public static readonly IReadOnlyDictionary<string, RolUsuario?> RolPorOperacion = new Dictionary<string, RolUsuario?>
    {
        [Operaciones.ConsultarPerfil] = null,
        [Operaciones.CerrarSesion] = null,
        [Operaciones.CambiarPassword] = null,

        [Operaciones.ListarUsuarios] = RolUsuario.Administrador,
        [Operaciones.CambiarRolUsuario] = RolUsuario.Administrador,
        [Operaciones.DesactivarUsuario] = RolUsuario.Administrador,
        [Operaciones.ReactivarUsuario] = RolUsuario.Administrador,
        [Operaciones.ForzarRestablecimiento] = RolUsuario.Administrador,

        [Operaciones.SolicitarPermiso] = null,
        [Operaciones.ListarMisSolicitudes] = null,
        [Operaciones.ListarSolicitudesPendientes] = RolUsuario.Administrador,
        [Operaciones.AprobarSolicitud] = RolUsuario.Administrador,
        [Operaciones.RechazarSolicitud] = RolUsuario.Administrador,

        [Operaciones.ConsultarDirectorio] = null,

        [Operaciones.CrearCategoria] = RolUsuario.Administrador,
        [Operaciones.CrearIngrediente] = RolUsuario.Administrador,
        [Operaciones.ListarCategorias] = null,
        [Operaciones.ListarIngredientes] = null,
        [Operaciones.CrearReceta] = RolUsuario.Estandar,
        [Operaciones.EnviarRecetaARevision] = RolUsuario.Estandar,
        [Operaciones.RetomarReceta] = RolUsuario.Estandar,
        [Operaciones.PublicarReceta] = RolUsuario.Administrador,
        [Operaciones.RechazarReceta] = RolUsuario.Administrador,
        [Operaciones.ArchivarReceta] = RolUsuario.Administrador,
        [Operaciones.ListarRecetasPublicadas] = null,
        [Operaciones.ListarRecetasEnRevision] = RolUsuario.Administrador,
        [Operaciones.ListarMisRecetas] = RolUsuario.Estandar,
        [Operaciones.GuardarEnDespensa] = RolUsuario.Estandar,
        [Operaciones.ListarDespensa] = RolUsuario.Estandar,
        [Operaciones.ListarRecetasPreparables] = RolUsuario.Estandar,
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

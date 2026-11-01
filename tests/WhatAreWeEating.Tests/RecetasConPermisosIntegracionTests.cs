using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Permisos;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.Infrastructure.Services;
using WhatAreWeEating.Recetas;
using WhatAreWeEating.Recetas.Interfaces;

namespace WhatAreWeEating.Tests;

/// <summary>
/// Integración de Gestión de permisos con el módulo de recetas: se usan <see cref="PermisoService"/> y
/// <see cref="RecetaService"/> reales, sobre la misma base en memoria, sin dobles y sin levantar la API.
/// Cada prueba crea su propia base (nombre único), así que no comparten estado.
/// </summary>
public class RecetasConPermisosIntegracionTests
{
    private sealed record Escenario(RecetaService Recetas, PermisoService Permisos, Guid Estandar, Guid Admin);

    private static async Task<Escenario> NuevoEscenarioAsync()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var contexto = new AppDbContext(opciones);
        var permisos = new PermisoService(contexto, TimeProvider.System);
        var recetas = new RecetaService(contexto, permisos);
        var escenario = new Escenario(recetas, permisos, Guid.NewGuid(), Guid.NewGuid());

        // Una receta publicada con un ingrediente, y la despensa del Estándar con lo necesario para prepararla
        var categoria = (await recetas.CrearCategoriaAsync("Platos fuertes")).Valor!;
        var harina = (await recetas.CrearIngredienteAsync("Harina", "g")).Valor!;
        var autor = Guid.NewGuid();
        var receta = (await recetas.CrearAsync(autor, new RecetaNueva("Pan", null, "Amasar y hornear.", categoria.Id,
            [new IngredienteNuevo(harina.Id, 500, "g")]))).Valor!;
        await recetas.EnviarARevisionAsync(autor, WhatAreWeEating.Core.Enums.RolUsuario.Estandar, receta.Id);
        await recetas.PublicarAsync(escenario.Admin, WhatAreWeEating.Core.Enums.RolUsuario.Administrador, receta.Id);
        await recetas.GuardarEnDespensaAsync(escenario.Estandar, harina.Id, 1000, "g");
        return escenario;
    }

    [Fact]
    public async Task ListarPreparables_SinSolicitarElPermiso_SeRechaza()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(e.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.PermisoRequerido, resultado.Codigo);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    public async Task ListarPreparables_ConLaSolicitudPendiente_SigueRechazada()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();
        await e.Permisos.CrearAsync(e.Estandar, CatalogoPermisos.ConsultarRecetasPreparables);

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(e.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.PermisoRequerido, resultado.Codigo);
    }

    [Fact]
    public async Task ListarPreparables_TrasRechazarLaSolicitud_SigueRechazada()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();
        var solicitud = (await e.Permisos.CrearAsync(e.Estandar, CatalogoPermisos.ConsultarRecetasPreparables)).Solicitud!;
        await e.Permisos.RechazarAsync(e.Admin, solicitud.Id, "No hace falta");

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(e.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.PermisoRequerido, resultado.Codigo);
    }

    [Fact]
    public async Task ListarPreparables_DespuesDeQueElAdministradorApruebaLaSolicitud_SePermiteYDevuelveLaReceta()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();
        var solicitud = (await e.Permisos.CrearAsync(e.Estandar, CatalogoPermisos.ConsultarRecetasPreparables)).Solicitud!;
        await e.Permisos.AprobarAsync(e.Admin, solicitud.Id);

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(e.Estandar);

        // Verificar
        Assert.True(resultado.Exito);
        var receta = Assert.Single(resultado.Valor!);
        Assert.Equal("Pan", receta.Nombre);
    }

    [Fact]
    public async Task ListarPreparables_PermisoAprobadoAOtroUsuario_NoHabilitaAlQueNoLoSolicito()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();
        var otro = Guid.NewGuid();
        var solicitud = (await e.Permisos.CrearAsync(otro, CatalogoPermisos.ConsultarRecetasPreparables)).Solicitud!;
        await e.Permisos.AprobarAsync(e.Admin, solicitud.Id);

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(e.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.PermisoRequerido, resultado.Codigo);
    }

    [Fact]
    public async Task ListarPreparables_ConPermisoPeroSinLosIngredientes_DevuelveListaVacia()
    {
        // Preparar
        var e = await NuevoEscenarioAsync();
        var sinDespensa = Guid.NewGuid();
        var solicitud = (await e.Permisos.CrearAsync(sinDespensa, CatalogoPermisos.ConsultarRecetasPreparables)).Solicitud!;
        await e.Permisos.AprobarAsync(e.Admin, solicitud.Id);

        // Actuar
        var resultado = await e.Recetas.ListarPreparablesAsync(sinDespensa);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Empty(resultado.Valor!);
    }
}

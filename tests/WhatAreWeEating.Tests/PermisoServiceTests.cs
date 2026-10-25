using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Core.Permisos;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.Infrastructure.Services;

namespace WhatAreWeEating.Tests;

/// <summary>
/// Pruebas de <see cref="PermisoService"/> sobre una base en memoria: sin API, sin SQL Server.
/// Cada prueba crea su propia base (nombre único), así que no comparten estado.
/// </summary>
public class PermisoServiceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 20, 15, 30, 0, TimeSpan.Zero);

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private static (PermisoService Servicio, AppDbContext Contexto) NuevoServicio()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var contexto = new AppDbContext(opciones);
        return (new PermisoService(contexto, new RelojFijo(Ahora)), contexto);
    }

    // RF-GP-01
    [Fact]
    public async Task Crear_PermisoDelCatalogo_LaSolicitudNaceEnPendiente()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var estandar = Guid.NewGuid();

        // Actuar
        var resultado = await servicio.CrearAsync(estandar, CatalogoPermisos.ConsultarDirectorio);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, resultado.Solicitud!.Estado);
        Assert.Equal(estandar, resultado.Solicitud.SolicitanteId);
    }

    // RF-GP-02
    [Fact]
    public async Task ListarDeUsuario_UnEstandar_SoloVeSusPropiasSolicitudes()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio);
        await servicio.CrearAsync(luis, CatalogoPermisos.ConsultarDirectorio);

        // Actuar
        var deAna = await servicio.ListarDeUsuarioAsync(ana);

        // Verificar
        var solicitud = Assert.Single(deAna);
        Assert.Equal(ana, solicitud.SolicitanteId);
    }

    // RF-GP-02
    [Fact]
    public async Task ListarPendientes_Administrador_VeLasPendientesDeTodosYNingunaResuelta()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var admin = Guid.NewGuid();
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        var deAna = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;
        await servicio.CrearAsync(luis, CatalogoPermisos.ConsultarDirectorio);
        await servicio.AprobarAsync(admin, deAna.Id);

        // Actuar
        var pendientes = await servicio.ListarPendientesAsync();

        // Verificar
        var solicitud = Assert.Single(pendientes);
        Assert.Equal(luis, solicitud.SolicitanteId);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    // RF-GP-02
    [Fact]
    public async Task ListarPendientes_VariosUsuarios_DevuelveLasPendientesDeTodos()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        await servicio.CrearAsync(Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio);
        await servicio.CrearAsync(Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio);

        // Actuar
        var pendientes = await servicio.ListarPendientesAsync();

        // Verificar
        Assert.Equal(2, pendientes.Count);
    }

    // RF-GP-03
    [Fact]
    public async Task Aprobar_SolicitudPendiente_RegistraQuienAproboYCuando()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var admin = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        var resultado = await servicio.AprobarAsync(admin, creada.Id);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(admin, resultado.Solicitud!.ResueltaPorId);
        Assert.Equal(Ahora.UtcDateTime, resultado.Solicitud.FechaResolucion);
    }

    // RF-GP-04
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rechazar_SinMotivo_NoSeCompletaYLaSolicitudSigueEnPendiente(string? motivo)
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var creada = (await servicio.CrearAsync(Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        var resultado = await servicio.RechazarAsync(Guid.NewGuid(), creada.Id, motivo);

        // Verificar
        Assert.Equal(ResultadoPermiso.MotivoRequerido, resultado.Resultado);
        var guardada = await contexto.SolicitudesPermiso.SingleAsync();
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }

    // RF-GP-04
    [Fact]
    public async Task Rechazar_ConMotivo_QuedaRechazadaYNoConcedeElPermiso()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var ana = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        var resultado = await servicio.RechazarAsync(Guid.NewGuid(), creada.Id, "No lo necesita");

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, resultado.Solicitud!.Estado);
        Assert.Equal("No lo necesita", resultado.Solicitud.MotivoRechazo);
        Assert.False(await servicio.TienePermisoAsync(ana, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-05
    [Fact]
    public async Task Aprobar_SolicitudPendiente_LlegaAAplicadaYElPermisoQuedaEfectivo()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var ana = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;
        Assert.False(await servicio.TienePermisoAsync(ana, CatalogoPermisos.ConsultarDirectorio));

        // Actuar
        var resultado = await servicio.AprobarAsync(Guid.NewGuid(), creada.Id);

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, resultado.Solicitud!.Estado);
        Assert.Equal(Ahora.UtcDateTime, resultado.Solicitud.FechaAplicacion);
        Assert.True(await servicio.TienePermisoAsync(ana, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-05: el permiso es del solicitante, no de otro usuario
    [Fact]
    public async Task Aprobar_SolicitudDeAna_ElPermisoNoQuedaEfectivoParaOtroUsuario()
    {
        // Preparar
        var (servicio, _) = NuevoServicio();
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        await servicio.AprobarAsync(Guid.NewGuid(), creada.Id);

        // Verificar
        Assert.False(await servicio.TienePermisoAsync(luis, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-06
    [Fact]
    public async Task Aprobar_DosVeces_LaSegundaSeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var primerAdmin = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio)).Solicitud!;
        await servicio.AprobarAsync(primerAdmin, creada.Id);

        // Actuar
        var resultado = await servicio.AprobarAsync(Guid.NewGuid(), creada.Id);

        // Verificar
        Assert.Equal(ResultadoPermiso.TransicionNoPermitida, resultado.Resultado);
        var guardada = await contexto.SolicitudesPermiso.SingleAsync();
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(primerAdmin, guardada.ResueltaPorId);
        Assert.Single(await contexto.PermisosConcedidos.ToListAsync());
    }

    // RF-GP-06
    [Fact]
    public async Task Aprobar_SolicitudRechazada_SeRechazaYNoSeConcedeElPermiso()
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var ana = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;
        await servicio.RechazarAsync(Guid.NewGuid(), creada.Id, "Motivo");

        // Actuar
        var resultado = await servicio.AprobarAsync(Guid.NewGuid(), creada.Id);

        // Verificar
        Assert.Equal(ResultadoPermiso.TransicionNoPermitida, resultado.Resultado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, (await contexto.SolicitudesPermiso.SingleAsync()).Estado);
        Assert.False(await servicio.TienePermisoAsync(ana, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-06
    [Fact]
    public async Task Rechazar_SolicitudYaAplicada_SeRechazaYElPermisoSigueEfectivo()
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var ana = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(ana, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;
        await servicio.AprobarAsync(Guid.NewGuid(), creada.Id);

        // Actuar
        var resultado = await servicio.RechazarAsync(Guid.NewGuid(), creada.Id, "Tarde");

        // Verificar
        Assert.Equal(ResultadoPermiso.TransicionNoPermitida, resultado.Resultado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, (await contexto.SolicitudesPermiso.SingleAsync()).Estado);
        Assert.True(await servicio.TienePermisoAsync(ana, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-07
    [Fact]
    public async Task Aprobar_AdministradorSobreSuPropiaSolicitud_SeRechazaYNoSeConcedeNada()
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var admin = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(admin, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        var resultado = await servicio.AprobarAsync(admin, creada.Id);

        // Verificar
        Assert.Equal(ResultadoPermiso.AutoResolucion, resultado.Resultado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, (await contexto.SolicitudesPermiso.SingleAsync()).Estado);
        Assert.False(await servicio.TienePermisoAsync(admin, CatalogoPermisos.ConsultarDirectorio));
    }

    // RF-GP-07
    [Fact]
    public async Task Rechazar_AdministradorSobreSuPropiaSolicitud_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var (servicio, contexto) = NuevoServicio();
        var admin = Guid.NewGuid();
        var creada = (await servicio.CrearAsync(admin, CatalogoPermisos.ConsultarDirectorio)).Solicitud!;

        // Actuar
        var resultado = await servicio.RechazarAsync(admin, creada.Id, "Motivo");

        // Verificar
        Assert.Equal(ResultadoPermiso.AutoResolucion, resultado.Resultado);
        var guardada = await contexto.SolicitudesPermiso.SingleAsync();
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }
}

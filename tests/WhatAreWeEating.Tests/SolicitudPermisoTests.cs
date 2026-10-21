using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Permisos;

namespace WhatAreWeEating.Tests;

public class SolicitudPermisoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 15, 30, 0, DateTimeKind.Utc);

    // Cada prueba arma su propia solicitud: no hay estado compartido entre pruebas.
    private static SolicitudPermiso NuevaSolicitud(Guid? solicitanteId = null)
        => SolicitudPermiso.Crear(solicitanteId ?? Guid.NewGuid(), CatalogoPermisos.ConsultarDirectorio, Ahora);

    private static SolicitudPermiso SolicitudEnEstado(EstadoSolicitudPermiso estado)
    {
        var solicitud = NuevaSolicitud();
        solicitud.Estado = estado;
        return solicitud;
    }

    // RF-GP-01
    [Fact]
    public void Crear_SolicitudNueva_NaceEnEstadoPendiente()
    {
        // Preparar
        var solicitanteId = Guid.NewGuid();

        // Actuar
        var solicitud = SolicitudPermiso.Crear(solicitanteId, CatalogoPermisos.ConsultarDirectorio, Ahora);

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    // RF-GP-03: transición válida Pendiente -> Aprobada, con el dato que registra (quién y cuándo)
    [Fact]
    public void Aprobar_SolicitudPendiente_QuedaAprobadaConAdministradorYFechaRegistrados()
    {
        // Preparar
        var solicitud = NuevaSolicitud();
        var adminId = Guid.NewGuid();

        // Actuar
        var resultado = solicitud.Aprobar(adminId, Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.Ok, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Equal(adminId, solicitud.ResueltaPorId);
        Assert.Equal(Ahora, solicitud.FechaResolucion);
    }

    // RF-GP-04: transición válida Pendiente -> Rechazada, con el motivo que registra
    [Fact]
    public void Rechazar_SolicitudPendienteConMotivo_QuedaRechazadaConMotivoRegistrado()
    {
        // Preparar
        var solicitud = NuevaSolicitud();
        var adminId = Guid.NewGuid();

        // Actuar
        var resultado = solicitud.Rechazar(adminId, "No justifica el acceso", Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.Ok, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("No justifica el acceso", solicitud.MotivoRechazo);
        Assert.Equal(adminId, solicitud.ResueltaPorId);
    }

    // RF-GP-05: transición válida Aprobada -> Aplicada
    [Fact]
    public void Aplicar_SolicitudAprobada_QuedaAplicadaConFechaDeAplicacion()
    {
        // Preparar
        var solicitud = SolicitudEnEstado(EstadoSolicitudPermiso.Aprobada);

        // Actuar
        var resultado = solicitud.Aplicar(Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.Ok, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(Ahora, solicitud.FechaAplicacion);
    }

    // RF-GP-06: aprobar dos veces la misma solicitud
    [Fact]
    public void Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var solicitud = NuevaSolicitud();
        var primerAdmin = Guid.NewGuid();
        solicitud.Aprobar(primerAdmin, Ahora);

        // Actuar
        var resultado = solicitud.Aprobar(Guid.NewGuid(), Ahora.AddHours(1));

        // Verificar
        Assert.Equal(ResultadoTransicion.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Equal(primerAdmin, solicitud.ResueltaPorId);
        Assert.Equal(Ahora, solicitud.FechaResolucion);
    }

    // RF-GP-06: pasar de Rechazada a Aplicada
    [Fact]
    public void Aplicar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada()
    {
        // Preparar
        var solicitud = SolicitudEnEstado(EstadoSolicitudPermiso.Rechazada);

        // Actuar
        var resultado = solicitud.Aplicar(Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Null(solicitud.FechaAplicacion);
    }

    // RF-GP-06: una solicitud Rechazada es terminal, no se puede aprobar después
    [Fact]
    public void Aprobar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada()
    {
        // Preparar
        var solicitud = SolicitudEnEstado(EstadoSolicitudPermiso.Rechazada);

        // Actuar
        var resultado = solicitud.Aprobar(Guid.NewGuid(), Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Null(solicitud.ResueltaPorId);
    }

    // RF-GP-06: no se puede saltar de Pendiente a Aplicada sin aprobar
    [Fact]
    public void Aplicar_SolicitudPendiente_SeRechazaYElEstadoSigueEnPendiente()
    {
        // Preparar
        var solicitud = NuevaSolicitud();

        // Actuar
        var resultado = solicitud.Aplicar(Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    // RF-GP-06: la tabla completa; cualquier par que no esté en ella es inválido
    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada, true)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada, true)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada, true)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Pendiente, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Pendiente, false)]
    public void EsPermitida_ParDeEstados_SoloAceptaLasTransicionesDeLaTabla(
        EstadoSolicitudPermiso origen, EstadoSolicitudPermiso destino, bool esperado)
    {
        // Preparar: los datos vienen del InlineData

        // Actuar
        var permitida = TransicionesSolicitudPermiso.EsPermitida(origen, destino);

        // Verificar
        Assert.Equal(esperado, permitida);
    }

    // RF-GP-04: sin motivo no se completa el rechazo
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechazar_SinMotivoEscrito_NoSeCompletaYSigueEnPendiente(string? motivo)
    {
        // Preparar
        var solicitud = NuevaSolicitud();

        // Actuar
        var resultado = solicitud.Rechazar(Guid.NewGuid(), motivo, Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.MotivoRequerido, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
    }

    // RF-GP-07
    [Fact]
    public void Aprobar_PorQuienCreoLaSolicitud_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var adminId = Guid.NewGuid();
        var solicitud = NuevaSolicitud(solicitanteId: adminId);

        // Actuar
        var resultado = solicitud.Aprobar(adminId, Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.AutoResolucion, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.ResueltaPorId);
    }

    // RF-GP-07
    [Fact]
    public void Rechazar_PorQuienCreoLaSolicitud_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var adminId = Guid.NewGuid();
        var solicitud = NuevaSolicitud(solicitanteId: adminId);

        // Actuar
        var resultado = solicitud.Rechazar(adminId, "Motivo", Ahora);

        // Verificar
        Assert.Equal(ResultadoTransicion.AutoResolucion, resultado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
    }
}

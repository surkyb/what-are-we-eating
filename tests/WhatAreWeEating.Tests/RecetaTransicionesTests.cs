using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Recetas;
using WhatAreWeEating.Recetas.Entities;
using WhatAreWeEating.Recetas.Enums;
using WhatAreWeEating.Recetas.Estados;

namespace WhatAreWeEating.Tests;

/// <summary>
/// Pruebas de la máquina de estados de Receta (docs/maquina-de-estados.md): una por cada fila de la tabla de
/// transiciones permitidas y de la prohibida, más el estado terminal. Solo usan la entidad: sin base de datos
/// ni API (RD-12). Cada prueba arma su propia receta, así que son independientes entre sí.
/// </summary>
public class RecetaTransicionesTests
{
    private static readonly Guid Autor = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private static Receta NuevaReceta(EstadoReceta estado, bool conIngredientes = true)
    {
        var receta = new Receta
        {
            Id = Guid.NewGuid(),
            Nombre = "Arroz con habichuelas",
            Instrucciones = "Hervir y servir.",
            AutorId = Autor,
            Estado = estado
        };
        if (conIngredientes)
        {
            receta.RecetaIngredientes.Add(new RecetaIngrediente { RecetaId = receta.Id, IngredienteId = Guid.NewGuid(), Cantidad = 1, Unidad = "taza" });
        }

        return receta;
    }

    // ---- Tabla de transiciones permitidas ----

    // Borrador -> EnRevision (Estándar autor, datos completos)
    [Fact]
    public void Transicionar_BorradorAEnRevision_AutorConDatosCompletos_QuedaEnRevision()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Borrador);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.EnRevision, Autor, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.Ok, resultado);
        Assert.Equal(EstadoReceta.EnRevision, receta.Estado);
    }

    // EnRevision -> Publicada (Administrador)
    [Fact]
    public void Transicionar_EnRevisionAPublicada_Administrador_QuedaPublicada()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.EnRevision);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Publicada, Admin, RolUsuario.Administrador);

        // Verificar
        Assert.Equal(ResultadoReceta.Ok, resultado);
        Assert.Equal(EstadoReceta.Publicada, receta.Estado);
    }

    // EnRevision -> Rechazada (Administrador, con motivo)
    [Fact]
    public void Transicionar_EnRevisionARechazada_AdministradorConMotivo_QuedaRechazadaConElMotivo()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.EnRevision);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Rechazada, Admin, RolUsuario.Administrador, "Faltan las cantidades");

        // Verificar
        Assert.Equal(ResultadoReceta.Ok, resultado);
        Assert.Equal(EstadoReceta.Rechazada, receta.Estado);
        Assert.Equal("Faltan las cantidades", receta.MotivoRechazo);
    }

    // Rechazada -> Borrador (Estándar autor)
    [Fact]
    public void Transicionar_RechazadaABorrador_Autor_QuedaEnBorrador()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Rechazada);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Borrador, Autor, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.Ok, resultado);
        Assert.Equal(EstadoReceta.Borrador, receta.Estado);
    }

    // Publicada -> Archivada (Administrador)
    [Fact]
    public void Transicionar_PublicadaAArchivada_Administrador_QuedaArchivada()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Publicada);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Archivada, Admin, RolUsuario.Administrador);

        // Verificar
        Assert.Equal(ResultadoReceta.Ok, resultado);
        Assert.Equal(EstadoReceta.Archivada, receta.Estado);
    }

    // ---- Tabla de transiciones prohibidas ----

    // Borrador -> Publicada (prohibida explícita: sin revisión no se publica)
    [Fact]
    public void Transicionar_BorradorAPublicada_Administrador_SeRechazaYSigueEnBorrador()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Borrador);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Publicada, Admin, RolUsuario.Administrador);

        // Verificar
        Assert.Equal(ResultadoReceta.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoReceta.Borrador, receta.Estado);
    }

    [Fact]
    public void EsTransicionPermitida_BorradorAPublicada_EsFalsa()
    {
        // Preparar
        var origen = EstadoReceta.Borrador;
        var destino = EstadoReceta.Publicada;

        // Actuar
        var permitida = TransicionesReceta.EsTransicionPermitida(origen, destino);

        // Verificar
        Assert.False(permitida);
    }

    // Archivada -> Borrador (prohibida explícita: no se reabre)
    [Fact]
    public void Transicionar_ArchivadaABorrador_Autor_SeRechazaYSigueArchivada()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Archivada);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Borrador, Autor, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoReceta.Archivada, receta.Estado);
    }

    [Fact]
    public void EsTransicionPermitida_ParDeEstados_SoloAceptaLasCincoDeLaTabla()
    {
        // Preparar
        var tabla = new HashSet<(EstadoReceta, EstadoReceta)>
        {
            (EstadoReceta.Borrador, EstadoReceta.EnRevision),
            (EstadoReceta.EnRevision, EstadoReceta.Publicada),
            (EstadoReceta.EnRevision, EstadoReceta.Rechazada),
            (EstadoReceta.Rechazada, EstadoReceta.Borrador),
            (EstadoReceta.Publicada, EstadoReceta.Archivada)
        };

        // Actuar
        var permitidas = (from origen in Enum.GetValues<EstadoReceta>()
                          from destino in Enum.GetValues<EstadoReceta>()
                          where TransicionesReceta.EsTransicionPermitida(origen, destino)
                          select (origen, destino)).ToHashSet();

        // Verificar
        Assert.Equal(tabla, permitidas);
    }

    // ---- Estado terminal ----

    [Theory]
    [InlineData(EstadoReceta.Borrador)]
    [InlineData(EstadoReceta.EnRevision)]
    [InlineData(EstadoReceta.Publicada)]
    [InlineData(EstadoReceta.Rechazada)]
    [InlineData(EstadoReceta.Archivada)]
    public void Transicionar_DesdeArchivadaACualquierEstado_SeRechazaYSigueArchivada(EstadoReceta destino)
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Archivada);

        // Actuar
        var resultado = receta.Transicionar(destino, Admin, RolUsuario.Administrador);

        // Verificar
        Assert.Equal(ResultadoReceta.TransicionNoPermitida, resultado);
        Assert.Equal(EstadoReceta.Archivada, receta.Estado);
    }

    [Fact]
    public void Permitidas_EstadoArchivada_NoTieneNingunaTransicionDeSalida()
    {
        // Preparar
        var terminal = EstadoReceta.Archivada;

        // Actuar
        var salidas = TransicionesReceta.Permitidas[terminal];

        // Verificar
        Assert.Empty(salidas);
    }

    // ---- Condiciones de las transiciones permitidas ----

    [Fact]
    public void Transicionar_BorradorAEnRevision_QuienNoEsElAutor_SeRechazaYSigueEnBorrador()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Borrador);
        var otroEstandar = Guid.NewGuid();

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.EnRevision, otroEstandar, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.NoEsAutor, resultado);
        Assert.Equal(EstadoReceta.Borrador, receta.Estado);
    }

    [Fact]
    public void Transicionar_BorradorAEnRevision_SinIngredientes_SeRechazaPorDatosIncompletos()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Borrador, conIngredientes: false);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.EnRevision, Autor, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.DatosIncompletos, resultado);
        Assert.Equal(EstadoReceta.Borrador, receta.Estado);
    }

    [Fact]
    public void Transicionar_EnRevisionAPublicada_ConRolEstandar_SeRechazaYSigueEnRevision()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.EnRevision);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Publicada, Autor, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.RolNoPermitido, resultado);
        Assert.Equal(EstadoReceta.EnRevision, receta.Estado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Transicionar_EnRevisionARechazada_SinMotivo_SeRechazaYSigueEnRevision(string? motivo)
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.EnRevision);

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Rechazada, Admin, RolUsuario.Administrador, motivo);

        // Verificar
        Assert.Equal(ResultadoReceta.MotivoRequerido, resultado);
        Assert.Equal(EstadoReceta.EnRevision, receta.Estado);
    }

    [Fact]
    public void Transicionar_RechazadaABorrador_QuienNoEsElAutor_SeRechazaYSigueRechazada()
    {
        // Preparar
        var receta = NuevaReceta(EstadoReceta.Rechazada);
        var otroEstandar = Guid.NewGuid();

        // Actuar
        var resultado = receta.Transicionar(EstadoReceta.Borrador, otroEstandar, RolUsuario.Estandar);

        // Verificar
        Assert.Equal(ResultadoReceta.NoEsAutor, resultado);
        Assert.Equal(EstadoReceta.Rechazada, receta.Estado);
    }
}

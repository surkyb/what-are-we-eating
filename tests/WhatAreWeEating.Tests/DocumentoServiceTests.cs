using System.Text;
using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.Infrastructure.Services;

namespace WhatAreWeEating.Tests;

/// <summary>Manejador de documentos (RF-DOC-01 a 04) sobre base en memoria y una carpeta temporal propia de cada prueba.</summary>
public class DocumentoServiceTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "waw-docs-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _dueno = Guid.NewGuid();
    private readonly Guid _otro = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private DocumentoService NuevoServicio(long maximo = 1024)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new DocumentoService(new AppDbContext(opciones), new DocumentoOptions(_carpeta, maximo), TimeProvider.System);
    }

    private static MemoryStream Bytes(string texto) => new(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public async Task Subir_ArchivoValido_RegistraNombreTipoTamanoFechaYPropietario() // RF-DOC-01
    {
        // Preparar
        var servicio = NuevoServicio();

        // Actuar
        var r = await servicio.SubirAsync(_dueno, "menu.txt", "text/plain", Bytes("hola mundo"));

        // Verificar
        Assert.True(r.Exito);
        Assert.Equal("menu.txt", r.Valor!.Nombre);
        Assert.Equal("text/plain", r.Valor.Tipo);
        Assert.Equal(10, r.Valor.TamanoBytes);
        Assert.Equal(_dueno, r.Valor.PropietarioId);
        Assert.True(r.Valor.FechaSubida > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Subir_SinArchivo_SeRechazaSinExcepcion() // RD-07
    {
        var servicio = NuevoServicio();

        var r = await servicio.SubirAsync(_dueno, "x.txt", "text/plain", null);

        Assert.Equal(ResultadoDocumento.ArchivoAusente, r.Codigo);
    }

    [Fact]
    public async Task Subir_ArchivoVacio_SeRechazaYNoDejaArchivoEnDisco() // RD-07
    {
        var servicio = NuevoServicio();

        var r = await servicio.SubirAsync(_dueno, "vacio.txt", "text/plain", new MemoryStream());

        Assert.Equal(ResultadoDocumento.ArchivoVacio, r.Codigo);
        Assert.Empty(Directory.GetFiles(_carpeta));
        Assert.Empty(await servicio.ListarAsync(_dueno, RolUsuario.Estandar));
    }

    [Fact]
    public async Task Subir_ArchivoMasGrandeQueElMaximo_SeRechaza()
    {
        var servicio = NuevoServicio(maximo: 5);

        var r = await servicio.SubirAsync(_dueno, "grande.txt", "text/plain", Bytes("123456"));

        Assert.Equal(ResultadoDocumento.ArchivoDemasiadoGrande, r.Codigo);
        Assert.Empty(Directory.GetFiles(_carpeta));
    }

    [Fact]
    public async Task Subir_NombreConRuta_GuardaSoloElNombreDelArchivo()
    {
        var servicio = NuevoServicio();

        var r = await servicio.SubirAsync(_dueno, @"..\..\etc/passwd", "text/plain", Bytes("x"));

        Assert.Equal("passwd", r.Valor!.Nombre);
    }

    [Fact]
    public async Task Listar_Estandar_SoloVeLosSuyos() // RF-DOC-02
    {
        var servicio = NuevoServicio();
        await servicio.SubirAsync(_dueno, "mio.txt", "text/plain", Bytes("a"));
        await servicio.SubirAsync(_otro, "ajeno.txt", "text/plain", Bytes("b"));

        var lista = await servicio.ListarAsync(_dueno, RolUsuario.Estandar);

        Assert.Single(lista);
        Assert.Equal("mio.txt", lista[0].Nombre);
    }

    [Fact]
    public async Task Listar_Administrador_VeLosDeTodos() // RF-DOC-02
    {
        var servicio = NuevoServicio();
        await servicio.SubirAsync(_dueno, "a.txt", "text/plain", Bytes("a"));
        await servicio.SubirAsync(_otro, "b.txt", "text/plain", Bytes("b"));

        var lista = await servicio.ListarAsync(_admin, RolUsuario.Administrador);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task Descargar_Dueno_RecibeElContenido() // RF-DOC-03
    {
        var servicio = NuevoServicio();
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("contenido"))).Valor!.Id;

        var r = await servicio.DescargarAsync(_dueno, RolUsuario.Estandar, id);

        Assert.True(r.Exito);
        await using var flujo = r.Valor!.Contenido;
        using var lector = new StreamReader(flujo);
        Assert.Equal("contenido", await lector.ReadToEndAsync());
    }

    [Fact]
    public async Task Descargar_Administrador_LoLogra() // RF-DOC-03
    {
        var servicio = NuevoServicio();
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("x"))).Valor!.Id;

        var r = await servicio.DescargarAsync(_admin, RolUsuario.Administrador, id);

        Assert.True(r.Exito);
        await r.Valor!.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Descargar_OtroUsuarioEstandar_SeRechaza() // RF-DOC-03, RD-06
    {
        var servicio = NuevoServicio();
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("x"))).Valor!.Id;

        var r = await servicio.DescargarAsync(_otro, RolUsuario.Estandar, id);

        Assert.Equal(ResultadoDocumento.AccesoDenegado, r.Codigo);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Eliminar_Dueno_DesapareceDelListadoPeroElRegistroYElArchivoSiguen() // RF-DOC-04
    {
        var servicio = NuevoServicio();
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var contexto = new AppDbContext(opciones);
        servicio = new DocumentoService(contexto, new DocumentoOptions(_carpeta, 1024), TimeProvider.System);
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("x"))).Valor!.Id;

        var r = await servicio.EliminarAsync(_dueno, RolUsuario.Estandar, id);

        Assert.True(r.Exito);
        Assert.Empty(await servicio.ListarAsync(_dueno, RolUsuario.Estandar));
        Assert.Empty(await servicio.ListarAsync(_admin, RolUsuario.Administrador));
        var registro = await contexto.Documentos.SingleAsync(d => d.Id == id);
        Assert.True(registro.Eliminado);
        Assert.True(File.Exists(Path.Combine(_carpeta, registro.ClaveAlmacenamiento)));
    }

    [Fact]
    public async Task Eliminar_OtroUsuarioEstandar_SeRechazaYSigueEnElListado() // RD-06
    {
        var servicio = NuevoServicio();
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("x"))).Valor!.Id;

        var r = await servicio.EliminarAsync(_otro, RolUsuario.Estandar, id);

        Assert.Equal(ResultadoDocumento.AccesoDenegado, r.Codigo);
        Assert.Single(await servicio.ListarAsync(_dueno, RolUsuario.Estandar));
    }

    [Fact]
    public async Task Descargar_DocumentoEliminado_NoSeEncuentra()
    {
        var servicio = NuevoServicio();
        var id = (await servicio.SubirAsync(_dueno, "n.txt", "text/plain", Bytes("x"))).Valor!.Id;
        await servicio.EliminarAsync(_dueno, RolUsuario.Estandar, id);

        var r = await servicio.DescargarAsync(_dueno, RolUsuario.Estandar, id);

        Assert.Equal(ResultadoDocumento.NoEncontrado, r.Codigo);
    }
}

using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.Infrastructure.Services;
using WhatAreWeEating.Recetas.Interfaces;

namespace WhatAreWeEating.Tests;

/// <summary>API externa de clima (sin red: el HttpClient usa un manejador falso) y la operación de negocio que la usa.</summary>
public class ClimaYSugerenciasTests
{
    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class ClimaFalso(RespuestaClima respuesta) : IClimaService
    {
        public Task<RespuestaClima> ObtenerAsync(string? ciudad, CancellationToken ct = default) => Task.FromResult(respuesta);
    }

    private static OpenWeatherClimaService NuevoCliente(ManejadorFalso manejador, string? clave)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenWeather:ApiKey"] = clave })
            .Build();
        return new OpenWeatherClimaService(new HttpClient(manejador) { BaseAddress = new Uri(OpenWeatherClimaService.UrlBase) }, config);
    }

    private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
        => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string RespuestaReal =
        """{"weather":[{"id":802,"main":"Clouds","description":"nubes dispersas"}],"main":{"temp":31.46,"humidity":70},"sys":{"country":"DO"},"name":"Santo Domingo"}""";

    [Fact]
    public async Task Obtener_SinClave_RechazaSinLlamarALaApi()
    {
        var manejador = new ManejadorFalso(_ => Json(HttpStatusCode.OK, RespuestaReal));
        var cliente = NuevoCliente(manejador, clave: null);

        var r = await cliente.ObtenerAsync("Santo Domingo");

        Assert.Equal(ResultadoClima.ClaveNoConfigurada, r.Resultado);
        Assert.Contains("OpenWeather__ApiKey", r.Mensaje);
        Assert.Equal(0, manejador.Llamadas);
    }

    [Fact]
    public async Task Obtener_CiudadVacia_RechazaComoInvalida()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => Json(HttpStatusCode.OK, RespuestaReal)), "clave-x");

        var r = await cliente.ObtenerAsync("   ");

        Assert.Equal(ResultadoClima.CiudadInvalida, r.Resultado);
    }

    [Fact]
    public async Task Obtener_RespuestaCorrecta_DevuelveTemperaturaCiudadYDescripcion()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => Json(HttpStatusCode.OK, RespuestaReal)), "clave-x");

        var r = await cliente.ObtenerAsync("Santo Domingo");

        Assert.True(r.Exito);
        Assert.Equal(31.5m, r.Clima!.TemperaturaC);
        Assert.Equal("Santo Domingo", r.Clima.Ciudad);
        Assert.Equal("DO", r.Clima.Pais);
        Assert.Equal("nubes dispersas", r.Clima.Descripcion);
    }

    [Fact]
    public async Task Obtener_LaApiResponde404_CiudadNoEncontrada()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => Json(HttpStatusCode.NotFound, """{"cod":"404"}""")), "clave-x");

        var r = await cliente.ObtenerAsync("Ciudadnoexiste");

        Assert.Equal(ResultadoClima.CiudadNoEncontrada, r.Resultado);
    }

    [Fact]
    public async Task Obtener_LaApiRechazaLaClave_ApiNoDisponibleSinMostrarLaClave()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => Json(HttpStatusCode.Unauthorized, """{"cod":401}""")), "clave-secreta-123");

        var r = await cliente.ObtenerAsync("Santo Domingo");

        Assert.Equal(ResultadoClima.ApiNoDisponible, r.Resultado);
        Assert.DoesNotContain("clave-secreta-123", r.Mensaje);
    }

    [Fact]
    public async Task Obtener_LaApiNoResponde_RechazoControladoSinExcepcion()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => throw new HttpRequestException("sin red")), "clave-x");

        var r = await cliente.ObtenerAsync("Santo Domingo");

        Assert.Equal(ResultadoClima.ApiNoDisponible, r.Resultado);
    }

    [Fact]
    public async Task Obtener_RespuestaIlegible_RechazoControlado()
    {
        var cliente = NuevoCliente(new ManejadorFalso(_ => Json(HttpStatusCode.OK, "esto no es json")), "clave-x");

        var r = await cliente.ObtenerAsync("Santo Domingo");

        Assert.Equal(ResultadoClima.ApiNoDisponible, r.Resultado);
    }

    private static async Task<SugerenciaRecetasService> EscenarioAsync(decimal temperatura)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var contexto = new AppDbContext(opciones);
        var permisos = new PermisoService(contexto, TimeProvider.System);
        var recetas = new RecetaService(contexto, permisos);
        var autor = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var harina = (await recetas.CrearIngredienteAsync("Harina", "g")).Valor!;

        foreach (var (nombre, categoria) in new[] { ("Sopa de pollo", "Platos fuertes"), ("Ensalada César", "Entradas"), ("Pan", "Panadería") })
        {
            var cat = (await recetas.CrearCategoriaAsync(categoria)).Valor
                      ?? (await recetas.ListarCategoriasAsync()).First(c => c.Nombre == categoria);
            var receta = (await recetas.CrearAsync(autor, new RecetaNueva(nombre, null, "Preparar.", cat.Id, [new IngredienteNuevo(harina.Id, 1, "g")]))).Valor!;
            await recetas.EnviarARevisionAsync(autor, RolUsuario.Estandar, receta.Id);
            await recetas.PublicarAsync(admin, RolUsuario.Administrador, receta.Id);
        }

        var clima = new ClimaFalso(new RespuestaClima(ResultadoClima.Ok, new ClimaDto("Santo Domingo", "DO", temperatura, "despejado")));
        return new SugerenciaRecetasService(contexto, clima);
    }

    [Fact]
    public async Task Sugerir_ConCalor_SoloPlatosFrescos()
    {
        var servicio = await EscenarioAsync(32m);

        var r = await servicio.SugerirPorClimaAsync("Santo Domingo");

        Assert.True(r.Exito);
        Assert.Equal(["Ensalada César"], r.Valor!.Recetas.Select(x => x.Nombre));
    }

    [Fact]
    public async Task Sugerir_ConFrio_SoloPlatosCalientes()
    {
        var servicio = await EscenarioAsync(10m);

        var r = await servicio.SugerirPorClimaAsync("Santo Domingo");

        Assert.Equal(["Sopa de pollo"], r.Valor!.Recetas.Select(x => x.Nombre));
    }

    [Fact]
    public async Task Sugerir_ConClimaTemplado_TodasLasPublicadas()
    {
        var servicio = await EscenarioAsync(22m);

        var r = await servicio.SugerirPorClimaAsync("Santo Domingo");

        Assert.Equal(3, r.Valor!.Recetas.Count);
    }

    [Fact]
    public async Task Sugerir_ConLaApiCaida_PropagaElRechazoSinRecetas()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var caida = new ClimaFalso(new RespuestaClima(ResultadoClima.ApiNoDisponible, Mensaje: "no disponible"));
        var servicio = new SugerenciaRecetasService(new AppDbContext(opciones), caida);

        var r = await servicio.SugerirPorClimaAsync("Santo Domingo");

        Assert.False(r.Exito);
        Assert.Equal(ResultadoClima.ApiNoDisponible, r.Codigo);
        Assert.Null(r.Valor);
    }
}

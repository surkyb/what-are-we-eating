using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public sealed record DocumentoOptions(string Ruta, long TamanoMaximoBytes)
{
    public const long TamanoMaximoPorDefecto = 10 * 1024 * 1024;

    /// <summary>Documentos__Ruta: carpeta de los archivos (en contenedor, un volumen). Documentos__TamanoMaximoBytes: tope opcional.</summary>
    public static DocumentoOptions Cargar(IConfiguration configuration)
    {
        var ruta = configuration["Documentos:Ruta"];
        if (string.IsNullOrWhiteSpace(ruta))
        {
            ruta = Path.Combine(AppContext.BaseDirectory, "almacenamiento-documentos");
        }

        var maximo = long.TryParse(configuration["Documentos:TamanoMaximoBytes"], out var m) && m > 0 ? m : TamanoMaximoPorDefecto;
        return new DocumentoOptions(ruta, maximo);
    }
}

/// <summary>
/// Manejador de documentos (RF-DOC-01 a 04). Los metadatos van a la base de datos y el contenido a una carpeta
/// (volumen con nombre en el contenedor). La autorización se decide aquí, no en el endpoint (RD-06).
/// </summary>
public class DocumentoService : IDocumentoService
{
    private readonly AppDbContext _context;
    private readonly DocumentoOptions _options;
    private readonly TimeProvider _time;

    public DocumentoService(AppDbContext context, DocumentoOptions options, TimeProvider time)
    {
        _context = context;
        _options = options;
        _time = time;
    }

    public async Task<RespuestaDocumento<DocumentoDto>> SubirAsync(
        Guid usuarioId, string? nombre, string? tipo, Stream? contenido, CancellationToken ct = default)
    {
        if (contenido is null)
        {
            return new(ResultadoDocumento.ArchivoAusente, Mensaje: "Debe adjuntar un archivo en el campo 'archivo'.");
        }

        var clave = Guid.NewGuid().ToString("N");
        var rutaArchivo = Path.Combine(_options.Ruta, clave);
        long total = 0;

        try
        {
            Directory.CreateDirectory(_options.Ruta);

            await using (var destino = new FileStream(rutaArchivo, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int leidos;
                while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
                {
                    total += leidos;
                    if (total > _options.TamanoMaximoBytes)
                    {
                        break;
                    }

                    await destino.WriteAsync(buffer.AsMemory(0, leidos), ct);
                }
            }

            if (total > _options.TamanoMaximoBytes)
            {
                Borrar(rutaArchivo);
                return new(ResultadoDocumento.ArchivoDemasiadoGrande,
                    Mensaje: $"El archivo supera el tamaño máximo permitido ({_options.TamanoMaximoBytes / (1024 * 1024)} MB).");
            }

            if (total == 0)
            {
                Borrar(rutaArchivo);
                return new(ResultadoDocumento.ArchivoVacio, Mensaje: "El archivo está vacío.");
            }

            var documento = new Documento
            {
                Id = Guid.NewGuid(),
                Nombre = NormalizarNombre(nombre),
                Tipo = NormalizarTipo(tipo),
                TamanoBytes = total,
                FechaSubida = _time.GetUtcNow().UtcDateTime,
                PropietarioId = usuarioId,
                ClaveAlmacenamiento = clave
            };

            _context.Documentos.Add(documento);
            await _context.SaveChangesAsync(ct);
            return new(ResultadoDocumento.Ok, ADto(documento));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            Borrar(rutaArchivo);
            return new(ResultadoDocumento.AlmacenamientoNoDisponible,
                Mensaje: "No fue posible guardar el documento. Intente de nuevo más tarde.");
        }
        catch
        {
            Borrar(rutaArchivo);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, RolUsuario rol, CancellationToken ct = default)
    {
        var consulta = _context.Documentos.AsNoTracking().Where(d => !d.Eliminado);
        if (rol != RolUsuario.Administrador)
        {
            consulta = consulta.Where(d => d.PropietarioId == usuarioId);
        }

        var documentos = await consulta.OrderByDescending(d => d.FechaSubida).ToListAsync(ct);
        return documentos.Select(ADto).ToList();
    }

    public async Task<RespuestaDocumento<DocumentoContenido>> DescargarAsync(
        Guid usuarioId, RolUsuario rol, Guid documentoId, CancellationToken ct = default)
    {
        var documento = await _context.Documentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentoId && !d.Eliminado, ct);
        if (documento is null)
        {
            return new(ResultadoDocumento.NoEncontrado, Mensaje: "El documento no existe.");
        }

        if (!PuedeAcceder(usuarioId, rol, documento))
        {
            return new(ResultadoDocumento.AccesoDenegado, Mensaje: "No tiene permiso para descargar este documento.");
        }

        var ruta = Path.Combine(_options.Ruta, documento.ClaveAlmacenamiento);
        if (!File.Exists(ruta))
        {
            return new(ResultadoDocumento.AlmacenamientoNoDisponible, Mensaje: "El archivo del documento no está disponible.");
        }

        var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(ResultadoDocumento.Ok, new DocumentoContenido(documento.Nombre, documento.Tipo, flujo));
    }

    public async Task<RespuestaDocumento<DocumentoDto>> EliminarAsync(
        Guid usuarioId, RolUsuario rol, Guid documentoId, CancellationToken ct = default)
    {
        var documento = await _context.Documentos.FirstOrDefaultAsync(d => d.Id == documentoId && !d.Eliminado, ct);
        if (documento is null)
        {
            return new(ResultadoDocumento.NoEncontrado, Mensaje: "El documento no existe.");
        }

        if (!PuedeAcceder(usuarioId, rol, documento))
        {
            return new(ResultadoDocumento.AccesoDenegado, Mensaje: "No tiene permiso para eliminar este documento.");
        }

        // Lógica, no física (RF-DOC-04): el registro y el archivo se conservan
        documento.Eliminado = true;
        documento.FechaEliminacion = _time.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(ct);
        return new(ResultadoDocumento.Ok, ADto(documento));
    }

    private static bool PuedeAcceder(Guid usuarioId, RolUsuario rol, Documento d)
        => rol == RolUsuario.Administrador || d.PropietarioId == usuarioId;

    private static DocumentoDto ADto(Documento d) => new(d.Id, d.Nombre, d.Tipo, d.TamanoBytes, d.FechaSubida, d.PropietarioId);

    private static string NormalizarNombre(string? nombre)
    {
        // Solo el nombre, sin carpetas ni caracteres de control
        var soloNombre = Path.GetFileName((nombre ?? string.Empty).Replace('\\', '/'));
        var limpio = new string(soloNombre.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpio.Length == 0) limpio = "documento";
        return limpio.Length > 255 ? limpio[..255] : limpio;
    }

    private static string NormalizarTipo(string? tipo)
    {
        var limpio = (tipo ?? string.Empty).Trim();
        if (limpio.Length == 0 || limpio.Any(char.IsControl)) return "application/octet-stream";
        return limpio.Length > 100 ? limpio[..100] : limpio;
    }

    private static void Borrar(string ruta)
    {
        try { File.Delete(ruta); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

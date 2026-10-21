using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Core.Permisos;

namespace WhatAreWeEating.Infrastructure.Services;

public class PermisoService : IPermisoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _reloj;

    public PermisoService(AppDbContext context, TimeProvider reloj)
    {
        _context = context;
        _reloj = reloj;
    }

    public async Task<PermisoResultado> CrearAsync(Guid solicitanteId, string? permiso, CancellationToken cancellationToken = default)
    {
        var nombre = permiso?.Trim();
        if (!CatalogoPermisos.Existe(nombre))
        {
            return new PermisoResultado(ResultadoPermiso.PermisoInvalido,
                $"El permiso solicitado no existe. Permisos disponibles: {string.Join(", ", CatalogoPermisos.Todos)}.");
        }

        var yaLoTiene = await _context.PermisosConcedidos
            .AnyAsync(p => p.UsuarioId == solicitanteId && p.Permiso == nombre, cancellationToken);
        var yaPendiente = await _context.SolicitudesPermiso.AnyAsync(s =>
            s.SolicitanteId == solicitanteId && s.Permiso == nombre && s.Estado == EstadoSolicitudPermiso.Pendiente, cancellationToken);
        if (yaLoTiene || yaPendiente)
        {
            return new PermisoResultado(ResultadoPermiso.SolicitudDuplicada,
                "Ya tiene este permiso o una solicitud pendiente para él.");
        }

        var solicitud = SolicitudPermiso.Crear(solicitanteId, nombre!, _reloj.GetUtcNow().UtcDateTime);
        _context.SolicitudesPermiso.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        return new PermisoResultado(ResultadoPermiso.Ok, Solicitud: ADto(solicitud));
    }

    public async Task<IReadOnlyList<SolicitudPermisoDto>> ListarPendientesAsync(CancellationToken cancellationToken = default)
    {
        var solicitudes = await _context.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.FechaCreacion)
            .ToListAsync(cancellationToken);
        return solicitudes.Select(ADto).ToList();
    }

    public async Task<IReadOnlyList<SolicitudPermisoDto>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var solicitudes = await _context.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.SolicitanteId == usuarioId)
            .OrderBy(s => s.FechaCreacion)
            .ToListAsync(cancellationToken);
        return solicitudes.Select(ADto).ToList();
    }

    public async Task<PermisoResultado> AprobarAsync(Guid adminId, Guid solicitudId, CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.SolicitudesPermiso.FirstOrDefaultAsync(s => s.Id == solicitudId, cancellationToken);
        if (solicitud is null)
        {
            return new PermisoResultado(ResultadoPermiso.NoEncontrado, "La solicitud no existe.");
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var aprobada = solicitud.Aprobar(adminId, ahora);
        if (aprobada != ResultadoTransicion.Ok)
        {
            return Rechazo(aprobada);
        }

        // RF-GP-05: la aplicación ocurre en la misma operación; nadie interviene a mano
        solicitud.Aplicar(ahora);
        _context.PermisosConcedidos.Add(new PermisoConcedido
        {
            Id = Guid.NewGuid(),
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.Permiso,
            SolicitudId = solicitud.Id,
            FechaConcesion = ahora
        });

        return await GuardarAsync(solicitud, cancellationToken);
    }

    public async Task<PermisoResultado> RechazarAsync(Guid adminId, Guid solicitudId, string? motivo, CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.SolicitudesPermiso.FirstOrDefaultAsync(s => s.Id == solicitudId, cancellationToken);
        if (solicitud is null)
        {
            return new PermisoResultado(ResultadoPermiso.NoEncontrado, "La solicitud no existe.");
        }

        var rechazada = solicitud.Rechazar(adminId, motivo, _reloj.GetUtcNow().UtcDateTime);
        if (rechazada != ResultadoTransicion.Ok)
        {
            return Rechazo(rechazada);
        }

        return await GuardarAsync(solicitud, cancellationToken);
    }

    public Task<bool> TienePermisoAsync(Guid usuarioId, string permiso, CancellationToken cancellationToken = default)
        => _context.PermisosConcedidos.AnyAsync(p => p.UsuarioId == usuarioId && p.Permiso == permiso, cancellationToken);

    private async Task<PermisoResultado> GuardarAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra resolución ganó la carrera: la solicitud ya no estaba Pendiente
            return new PermisoResultado(ResultadoPermiso.TransicionNoPermitida,
                "La solicitud ya fue resuelta por otra operación.");
        }

        return new PermisoResultado(ResultadoPermiso.Ok, Solicitud: ADto(solicitud));
    }

    private static PermisoResultado Rechazo(ResultadoTransicion transicion) => transicion switch
    {
        ResultadoTransicion.AutoResolucion => new PermisoResultado(ResultadoPermiso.AutoResolucion,
            "Nadie puede resolver su propia solicitud."),
        ResultadoTransicion.MotivoRequerido => new PermisoResultado(ResultadoPermiso.MotivoRequerido,
            "El rechazo requiere un motivo escrito."),
        _ => new PermisoResultado(ResultadoPermiso.TransicionNoPermitida,
            "La transición no está permitida desde el estado actual de la solicitud.")
    };

    private static SolicitudPermisoDto ADto(SolicitudPermiso s) => new(
        s.Id, s.SolicitanteId, s.Permiso, s.Estado, s.FechaCreacion,
        s.ResueltaPorId, s.FechaResolucion, s.MotivoRechazo, s.FechaAplicacion);
}

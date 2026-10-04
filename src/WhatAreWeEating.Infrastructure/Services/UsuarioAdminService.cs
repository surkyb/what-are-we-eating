using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public class UsuarioAdminService : IUsuarioAdminService
{
    private readonly AppDbContext _context;
    private readonly ISesionService _sesionService;

    public UsuarioAdminService(AppDbContext context, ISesionService sesionService)
    {
        _context = context;
        _sesionService = sesionService;
    }

    public async Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.FechaCreacion)
            .Select(u => new UsuarioAdminDto(u.Id, u.Nombre, u.Correo, u.Rol, u.Activo))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminResultado> CambiarRolAsync(Guid actorId, Guid usuarioId, RolUsuario nuevoRol, CancellationToken cancellationToken = default)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return new AdminResultado(ResultadoAdmin.NoEncontrado, "El usuario no existe.");
        }

        // Evita que el sistema se quede sin administradores por un cambio propio
        if (usuario.Id == actorId)
        {
            return new AdminResultado(ResultadoAdmin.OperacionNoPermitida, "Un administrador no puede cambiar su propio rol.");
        }

        usuario.Rol = nuevoRol;
        await _context.SaveChangesAsync(cancellationToken);
        return new AdminResultado(ResultadoAdmin.Ok);
    }

    public async Task<AdminResultado> DesactivarAsync(Guid actorId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return new AdminResultado(ResultadoAdmin.NoEncontrado, "El usuario no existe.");
        }

        // RF-CA-20
        if (usuario.Id == actorId)
        {
            return new AdminResultado(ResultadoAdmin.OperacionNoPermitida, "Un administrador no puede desactivarse a sí mismo.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        usuario.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);
        await _sesionService.RevocarSesionesDeUsuarioAsync(usuario.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminResultado(ResultadoAdmin.Ok);
    }

    public async Task<AdminResultado> ReactivarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return new AdminResultado(ResultadoAdmin.NoEncontrado, "El usuario no existe.");
        }

        usuario.Activo = true;
        await _context.SaveChangesAsync(cancellationToken);
        return new AdminResultado(ResultadoAdmin.Ok);
    }
}

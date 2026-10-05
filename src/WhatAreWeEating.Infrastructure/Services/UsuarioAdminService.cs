using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public class UsuarioAdminService : IUsuarioAdminService
{
    private readonly AppDbContext _context;
    private readonly ISesionService _sesionService;
    private readonly ITokenService _tokenService;

    public UsuarioAdminService(AppDbContext context, ISesionService sesionService, ITokenService tokenService)
    {
        _context = context;
        _sesionService = sesionService;
        _tokenService = tokenService;
    }

    public async Task<AdminResultado> ForzarRestablecimientoAsync(Guid actorId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return new AdminResultado(ResultadoAdmin.NoEncontrado, "El usuario no existe.");
        }

        // Forzarse a sí mismo dejaría al administrador sin contraseña ni sesión, dependiendo del correo
        if (usuario.Id == actorId)
        {
            return new AdminResultado(ResultadoAdmin.OperacionNoPermitida,
                "Un administrador no puede forzar su propio restablecimiento; use el cambio de contraseña.");
        }

        if (!usuario.Activo)
        {
            return new AdminResultado(ResultadoAdmin.OperacionNoPermitida,
                "El usuario está desactivado; reactívelo antes de forzar su restablecimiento.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            usuario.PasswordHash = CodigoRecuperacion.GenerarHashInutilizable();
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;

            await CodigoRecuperacion.EncolarAsync(
                _context, _tokenService, usuario,
                "Restablecimiento de contraseña requerido - WhatAreWeEating",
                "Un administrador ha invalidado tu contraseña actual. Debes establecer una nueva.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await _sesionService.RevocarSesionesDeUsuarioAsync(usuario.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new AdminResultado(ResultadoAdmin.Ok);
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
        // Si nunca se activó, se marca igualmente: ningún flujo público puede reactivar a un desactivado
        usuario.FechaActivacion ??= DateTime.UtcNow;
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
        usuario.FechaActivacion ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new AdminResultado(ResultadoAdmin.Ok);
    }
}

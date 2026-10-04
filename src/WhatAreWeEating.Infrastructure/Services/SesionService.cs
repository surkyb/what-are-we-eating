using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public class SesionService : ISesionService
{
    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DuracionSesion = TimeSpan.FromHours(8);

    private const string MensajeCredencialesInvalidas = "Correo o contraseña incorrectos.";
    private const string MensajeCuentaInactiva = "La cuenta no está activa. Active su cuenta con el enlace enviado a su correo.";
    private const string MensajeCuentaBloqueada = "La cuenta está bloqueada temporalmente por demasiados intentos fallidos. Intente más tarde.";

    // Hash válido (mismo formato e iteraciones que PasswordHasher) que nunca coincide con una contraseña real.
    // Se verifica cuando el correo no existe para que el tiempo de respuesta sea equivalente (RF-CA-03).
    private const string HashFicticio =
        "100000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtOptions _jwt;

    public SesionService(AppDbContext context, IPasswordHasher passwordHasher, JwtOptions jwt)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    public async Task<LoginResultado> LoginAsync(string correo, string password, CancellationToken cancellationToken = default)
    {
        var correoNormalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == correoNormalizado, cancellationToken);

        var passwordCorrecta = _passwordHasher.VerifyPassword(password ?? string.Empty, usuario?.PasswordHash ?? HashFicticio);
        var ahora = DateTime.UtcNow;
        var bloqueado = usuario?.BloqueadoHasta is { } hasta && hasta > ahora;

        if (usuario is null || !passwordCorrecta)
        {
            // Bloqueado: no se suman intentos (RF-CA-19). Se responde igual que cualquier credencial inválida.
            if (usuario is not null && !bloqueado)
            {
                usuario.IntentosFallidos++;

                if (usuario.IntentosFallidos >= MaxIntentosFallidos)
                {
                    usuario.BloqueadoHasta = ahora.Add(DuracionBloqueo);
                    usuario.IntentosFallidos = 0;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return new LoginResultado(ResultadoLogin.CredencialesInvalidas, MensajeCredencialesInvalidas);
        }

        // Desde aquí la contraseña es correcta: ya se puede informar el estado de la cuenta.
        if (bloqueado)
        {
            return new LoginResultado(ResultadoLogin.CuentaBloqueada, MensajeCuentaBloqueada);
        }

        if (!usuario.Activo)
        {
            return new LoginResultado(ResultadoLogin.CuentaInactiva, MensajeCuentaInactiva);
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            FechaEmision = ahora,
            FechaExpiracion = ahora.Add(DuracionSesion),
            Revocada = false
        };

        _context.Sesiones.Add(sesion);
        await _context.SaveChangesAsync(cancellationToken);

        var token = GenerarJwt(sesion);
        return new LoginResultado(ResultadoLogin.Exito, "Inicio de sesión exitoso.", token, sesion.FechaExpiracion);
    }

    public async Task<bool> ValidarSesionAsync(Guid sesionId, CancellationToken cancellationToken = default)
    {
        var ahora = DateTime.UtcNow;

        return await _context.Sesiones
            .AsNoTracking()
            .Where(s => s.Id == sesionId && !s.Revocada && s.FechaExpiracion > ahora)
            .Join(_context.Usuarios, s => s.UsuarioId, u => u.Id, (s, u) => u.Activo)
            .AnyAsync(activo => activo, cancellationToken);
    }

    public async Task CerrarSesionAsync(Guid sesionId, CancellationToken cancellationToken = default)
    {
        await _context.Sesiones
            .Where(s => s.Id == sesionId && !s.Revocada)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revocada, true), cancellationToken);
    }

    public async Task RevocarSesionesDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        await _context.Sesiones
            .Where(s => s.UsuarioId == usuarioId && !s.Revocada)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revocada, true), cancellationToken);
    }

    public async Task<UsuarioAutenticado?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new UsuarioAutenticado(u.Id, u.Nombre, u.Correo, u.Rol))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private string GenerarJwt(Sesion sesion)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, sesion.UsuarioId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sesion.Id.ToString())
            ]),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = sesion.FechaEmision,
            NotBefore = sesion.FechaEmision,
            Expires = sesion.FechaExpiracion,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

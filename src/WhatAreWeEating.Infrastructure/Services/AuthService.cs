using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;
    private readonly ITokenService _tokenService;

    public AuthService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        ITokenService tokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _tokenService = tokenService;
    }

    public async Task<(bool Success, string? ErrorMessage)> RegistrarUsuarioAsync(
        string nombre,
        string correo,
        string password,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return (false, "El nombre es obligatorio y no puede estar vacío.");
        }

        var nombreNormalizado = nombre.Trim();
        if (nombreNormalizado.Length > 100)
        {
            return (false, "El nombre no puede exceder los 100 caracteres.");
        }

        if (!_emailValidator.IsValid(correo))
        {
            return (false, "El correo electrónico provisto no tiene un formato válido.");
        }

        var (isValidPassword, passwordError) = _passwordValidator.Validate(password);
        if (!isValidPassword)
        {
            return (false, passwordError);
        }

        var normalizedEmail = correo.Trim().ToLowerInvariant();

        // RF-CA-01: Rechazo de correo duplicado
        var existeCorreo = await _context.Usuarios
            .AnyAsync(u => u.Correo == normalizedEmail, cancellationToken);

        if (existeCorreo)
        {
            return (false, "El correo electrónico ya se encuentra registrado.");
        }

        var passwordHash = _passwordHasher.HashPassword(password);

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombreNormalizado,
            Correo = normalizedEmail,
            PasswordHash = passwordHash,
            Activo = false,
            Rol = RolUsuario.Estandar,
            FechaCreacion = DateTime.UtcNow
        };

        var (rawToken, tokenHash) = _tokenService.GenerateToken();

        var tokenActivacion = new TokenUnUso
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Tipo = TipoToken.Activacion,
            TokenHash = tokenHash,
            FechaVencimiento = DateTime.UtcNow.AddHours(24),
            Usado = false
        };

        var activationUrl = $"{baseUrl.TrimEnd('/')}/auth/activar?token={Uri.EscapeDataString(rawToken)}";

        // RF-CA-15: Encolar correo sin enviarlo en la misma operación HTTP
        var correoCola = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = usuario.Correo,
            Asunto = "Activación de cuenta - WhatAreWeEating",
            Cuerpo = $"Hola,\n\nGracias por registrarte en WhatAreWeEating. Para activar tu cuenta, haz clic en el siguiente enlace:\n{activationUrl}\n\nEste enlace expirará en 24 horas.",
            Estado = EstadoCorreo.Pendiente,
            FechaCreacion = DateTime.UtcNow,
            FechaEnvio = null
        };

        // Persistencia atómica en una sola transacción
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Usuarios.Add(usuario);
            _context.TokensUnUso.Add(tokenActivacion);
            _context.CorreosEnCola.Add(correoCola);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ActivarCuentaAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "El enlace de activación es inválido o ha expirado.");
        }

        var tokenHash = _tokenService.HashToken(token);

        var tokenEntity = await _context.TokensUnUso
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Tipo == TipoToken.Activacion, cancellationToken);

        // RF-CA-16: Si es usado, vencido o inexistente, se rechaza y el estado no cambia
        if (tokenEntity == null || tokenEntity.Usado || tokenEntity.FechaVencimiento < DateTime.UtcNow)
        {
            return (false, "El enlace de activación es inválido o ha expirado.");
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == tokenEntity.UsuarioId, cancellationToken);

        if (usuario == null)
        {
            return (false, "El enlace de activación es inválido o ha expirado.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            tokenEntity.Usado = true;
            usuario.Activo = true;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ReenviarActivacionAsync(
        string correo,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_emailValidator.IsValid(correo))
        {
            return (false, "El correo electrónico provisto no tiene un formato válido.");
        }

        var normalizedEmail = correo.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == normalizedEmail, cancellationToken);

        // RF-CA-17: Si el usuario existe y está inactivo, invalidar token anterior y crear uno nuevo
        if (usuario != null && !usuario.Activo)
        {
            var tokensAnteriores = await _context.TokensUnUso
                .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoToken.Activacion && !t.Usado)
                .ToListAsync(cancellationToken);

            foreach (var t in tokensAnteriores)
            {
                t.Usado = true;
            }

            var (rawToken, tokenHash) = _tokenService.GenerateToken();

            var nuevoToken = new TokenUnUso
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                Tipo = TipoToken.Activacion,
                TokenHash = tokenHash,
                FechaVencimiento = DateTime.UtcNow.AddHours(24),
                Usado = false
            };

            var activationUrl = $"{baseUrl.TrimEnd('/')}/auth/activar?token={Uri.EscapeDataString(rawToken)}";

            var correoCola = new CorreoEnCola
            {
                Id = Guid.NewGuid(),
                Destinatario = usuario.Correo,
                Asunto = "Nuevo enlace de activación - WhatAreWeEating",
                Cuerpo = $"Hola,\n\nHas solicitado un nuevo enlace de activación para tu cuenta en WhatAreWeEating:\n{activationUrl}\n\nEste enlace expirará en 24 horas.",
                Estado = EstadoCorreo.Pendiente,
                FechaCreacion = DateTime.UtcNow,
                FechaEnvio = null
            };

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _context.TokensUnUso.Add(nuevoToken);
                _context.CorreosEnCola.Add(correoCola);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // RF-CA-17: Respuesta idéntica tanto si el usuario existe como si no, o si ya está activo
        return (true, null);
    }
}

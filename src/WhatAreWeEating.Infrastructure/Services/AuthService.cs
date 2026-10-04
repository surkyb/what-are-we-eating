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
    private readonly ISesionService _sesionService;

    public AuthService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        ITokenService tokenService,
        ISesionService sesionService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _tokenService = tokenService;
        _sesionService = sesionService;
    }

    private const string MensajeCodigoInvalido = "El código de recuperación es inválido o ha expirado.";
    private const string MensajeCorreoDuplicado = "El correo electrónico ya se encuentra registrado.";

    public async Task<(bool Success, string? ErrorMessage)> SolicitarRecuperacionAsync(
        string correo,
        CancellationToken cancellationToken = default)
    {
        if (!_emailValidator.IsValid(correo))
        {
            return (false, "El correo electrónico provisto no tiene un formato válido.");
        }

        var normalizedEmail = correo.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == normalizedEmail, cancellationToken);

        // RF-CA-09: no se distingue entre inexistente, inactivo o activo en la respuesta
        if (usuario is not null && usuario.Activo)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await CodigoRecuperacion.EncolarAsync(
                    _context, _tokenService, usuario,
                    "Recuperación de contraseña - WhatAreWeEating",
                    "Recibimos una solicitud para recuperar tu contraseña en WhatAreWeEating.",
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RestablecerPasswordAsync(
        string codigo,
        string passwordNueva,
        CancellationToken cancellationToken = default)
    {
        var (politicaValida, politicaError) = _passwordValidator.Validate(passwordNueva);
        if (!politicaValida)
        {
            return (false, politicaError);
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return (false, MensajeCodigoInvalido);
        }

        var codigoHash = _tokenService.HashToken(codigo.Trim());
        var ahora = DateTime.UtcNow;

        var token = await _context.TokensUnUso
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == codigoHash && t.Tipo == TipoToken.RecuperacionPassword, cancellationToken);

        if (token is null || token.Usado || token.FechaVencimiento < ahora)
        {
            return (false, MensajeCodigoInvalido);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Consumo atómico: si otra petición ya lo usó, no se afecta ninguna fila
            var consumido = await _context.TokensUnUso
                .Where(t => t.Id == token.Id && !t.Usado)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Usado, true), cancellationToken);

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == token.UsuarioId, cancellationToken);

            if (consumido == 0 || usuario is null || !usuario.Activo)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, MensajeCodigoInvalido);
            }

            usuario.PasswordHash = _passwordHasher.HashPassword(passwordNueva);
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
            await _context.SaveChangesAsync(cancellationToken);

            await _sesionService.RevocarSesionesDeUsuarioAsync(usuario.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> CambiarPasswordAsync(
        Guid usuarioId,
        string passwordActual,
        string passwordNueva,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null || !_passwordHasher.VerifyPassword(passwordActual, usuario.PasswordHash))
        {
            return (false, "La contraseña actual es incorrecta.");
        }

        var (politicaValida, politicaError) = _passwordValidator.Validate(passwordNueva);
        if (!politicaValida)
        {
            return (false, politicaError);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(passwordNueva);
            await _context.SaveChangesAsync(cancellationToken);

            // RF-CA-12: se revocan todas las sesiones, incluida la actual
            await _sesionService.RevocarSesionesDeUsuarioAsync(usuario.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
            return (false, MensajeCorreoDuplicado);
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
        catch (DbUpdateException ex) when (EsViolacionDeIndiceUnico(ex))
        {
            // Registros simultáneos con el mismo correo: el índice único de Correo los detiene (RF-CA-01)
            await transaction.RollbackAsync(cancellationToken);
            return (false, MensajeCorreoDuplicado);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool EsViolacionDeIndiceUnico(DbUpdateException ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627;

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
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Tipo == TipoToken.Activacion, cancellationToken);

        var ahora = DateTime.UtcNow;

        // RF-CA-16: Si es usado, vencido o inexistente, se rechaza y el estado no cambia
        if (tokenEntity == null || tokenEntity.Usado || tokenEntity.FechaVencimiento < ahora)
        {
            return (false, "El enlace de activación es inválido o ha expirado.");
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == tokenEntity.UsuarioId, cancellationToken);

        if (usuario == null || usuario.FechaActivacion != null)
        {
            return (false, "El enlace de activación es inválido o ha expirado.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Consumo atómico: solo una petición puede marcar el token como usado (RF-CA-16)
            var consumido = await _context.TokensUnUso
                .Where(t => t.Id == tokenEntity.Id && !t.Usado && t.FechaVencimiento > ahora)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Usado, true), cancellationToken);

            if (consumido != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, "El enlace de activación es inválido o ha expirado.");
            }

            usuario.Activo = true;
            usuario.FechaActivacion = ahora;

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

        // RF-CA-17: solo si el usuario existe, está inactivo y nunca fue activado. Un usuario desactivado
        // por un Administrador (FechaActivacion con valor) no puede volver a activarse por un flujo público
        if (usuario != null && !usuario.Activo && usuario.FechaActivacion is null)
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

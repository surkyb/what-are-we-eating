using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Core.Enums;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Infrastructure.Services;

public sealed record AdminSeedOptions(string? Email, string? Name, string? Password)
{
    public static AdminSeedOptions Cargar(IConfiguration configuration)
        => new(configuration["Seed:AdminEmail"], configuration["Seed:AdminName"], configuration["Seed:AdminPassword"]);
}

/// <summary>
/// Siembra el primer Administrador desde variables de entorno (Seed__*). Los mensajes devueltos
/// nombran variables pero nunca incluyen sus valores (RD-10).
/// </summary>
public class AdminSeeder
{
    private const string NombrePorDefecto = "Administrador";

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;

    public AdminSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
    }

    public async Task<string> SembrarAsync(AdminSeedOptions options, CancellationToken cancellationToken = default)
    {
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Email)) faltantes.Add("Seed__AdminEmail");
        if (string.IsNullOrWhiteSpace(options.Password)) faltantes.Add("Seed__AdminPassword");

        if (faltantes.Count > 0)
        {
            return $"Aviso: no se sembró el administrador inicial porque falta(n) la(s) variable(s) de entorno {string.Join(", ", faltantes)}.";
        }

        if (!_emailValidator.IsValid(options.Email))
        {
            return "Aviso: no se sembró el administrador inicial: Seed__AdminEmail no tiene un formato de correo válido.";
        }

        var (passwordValida, _) = _passwordValidator.Validate(options.Password!);
        if (!passwordValida)
        {
            return "Aviso: no se sembró el administrador inicial: Seed__AdminPassword no cumple la política de contraseñas.";
        }

        var nombre = string.IsNullOrWhiteSpace(options.Name) ? NombrePorDefecto : options.Name.Trim();
        if (nombre.Length > 100)
        {
            return "Aviso: no se sembró el administrador inicial: Seed__AdminName excede los 100 caracteres.";
        }

        var correo = options.Email!.Trim().ToLowerInvariant();

        if (await _context.Usuarios.AnyAsync(u => u.Correo == correo, cancellationToken))
        {
            return "Siembra: ya existe un usuario con el correo de Seed__AdminEmail; no se modificó.";
        }

        _context.Usuarios.Add(new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = correo,
            PasswordHash = _passwordHasher.HashPassword(options.Password!),
            Activo = true,
            Rol = RolUsuario.Administrador,
            FechaCreacion = DateTime.UtcNow,
            FechaActivacion = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return "Siembra: administrador inicial creado.";
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.Endpoints;
using WhatAreWeEating.Api.Middlewares;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;
using WhatAreWeEating.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Clave JWT obligatoria (Jwt__Key, mínimo 32 caracteres): si falta o es corta, la API no arranca (RD-10)
JwtOptions jwtOptions;
try
{
    jwtOptions = JwtOptionsLoader.Cargar(builder.Configuration);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Error de configuración: {ex.Message}");
    return 1;
}

// Servicios de infraestructura (DbContext con SQL Server) y Core
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticación JWT con validación de sesión en BD
builder.Services.AddJwtSessionAuthentication(jwtOptions);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<WhatAreWeEating.Api.Swagger.SubidaDocumentoOperationFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pegue el token obtenido en POST /auth/login (sin el prefijo 'Bearer')."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Migraciones automáticas: en el contenedor la base se crea y actualiza sin pasos manuales.
// La base puede tardar en aceptar conexiones, así que se reintenta unos segundos.
if (!string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
{
    const int intentos = 15;
    for (var intento = 1; intento <= intentos; intento++)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Console.WriteLine("Base de datos: migraciones aplicadas.");
            break;
        }
        catch (Exception ex)
        {
            // Solo el tipo: el mensaje podría incluir datos de conexión
            Console.Error.WriteLine($"Base de datos no disponible todavía (intento {intento}/{intentos}, {ex.GetType().Name}).");
            if (intento == intentos) break;
            await Task.Delay(TimeSpan.FromSeconds(4));
        }
    }
}

// Siembra del primer administrador (Seed__AdminEmail / Seed__AdminName / Seed__AdminPassword)
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    Console.WriteLine(await seeder.SembrarAsync(AdminSeedOptions.Cargar(app.Configuration)));
}
catch (Exception ex)
{
    // Solo el tipo: el mensaje podría incluir datos de conexión
    Console.Error.WriteLine($"Aviso: no se pudo ejecutar la siembra del administrador ({ex.GetType().Name}). Verifique la base de datos y las migraciones.");
}

// Middleware global de manejo de excepciones (RD-08)
app.UseErrorHandling();

// Habilitar Swagger en entorno de desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints de autenticación (registro, activación, login, me, logout)
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapPermisosEndpoints();
app.MapRecetasEndpoints();
app.MapDocumentosEndpoints();

// Endpoint de prueba / estado de la API
app.MapGet("/", () => Results.Ok(new { status = "Healthy", service = "WhatAreWeEating API" }))
    .WithName("GetRoot");

app.Run();
return 0;

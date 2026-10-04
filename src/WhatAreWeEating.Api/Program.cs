using Microsoft.OpenApi;
using WhatAreWeEating.Api.Auth;
using WhatAreWeEating.Api.Endpoints;
using WhatAreWeEating.Api.Middlewares;
using WhatAreWeEating.Core.Interfaces;
using WhatAreWeEating.Infrastructure;

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

// Endpoint de prueba / estado de la API
app.MapGet("/", () => Results.Ok(new { status = "Healthy", service = "WhatAreWeEating API" }))
    .WithName("GetRoot");

app.Run();
return 0;

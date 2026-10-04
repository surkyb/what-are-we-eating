using WhatAreWeEating.Api.Middlewares;
using WhatAreWeEating.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Servicios de infraestructura (DbContext con SQL Server)
builder.Services.AddInfrastructure(builder.Configuration);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Endpoint de prueba / estado de la API
app.MapGet("/", () => Results.Ok(new { status = "Healthy", service = "WhatAreWeEating API" }))
    .WithName("GetRoot");

app.Run();

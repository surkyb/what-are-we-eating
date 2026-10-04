namespace WhatAreWeEating.Api.DTOs;

public record RegistroRequest(string Nombre, string Correo, string Password);

public record ReenviarActivacionRequest(string Correo);

public record LoginRequest(string Correo, string Password);

public record LoginResponse(string Token, string TipoToken, DateTime Expira);

public record MeResponse(string Nombre, string Correo, string Rol);

public record MensajeResponse(string Mensaje);

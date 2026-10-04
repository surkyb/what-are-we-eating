namespace WhatAreWeEating.Api.DTOs;

public record RegistroRequest(string Nombre, string Correo, string Password);

public record ReenviarActivacionRequest(string Correo);

public record LoginRequest(string Correo, string Password);

public record LoginResponse(string Token, string TipoToken, DateTime Expira);

public record MeResponse(string Nombre, string Correo, string Rol);

public record UsuarioAdminResponse(Guid Id, string Nombre, string Correo, string Rol, bool Activo);

public record CambiarRolRequest(string Rol);

public record RecuperarRequest(string Correo);

public record RestablecerRequest(string Codigo, string PasswordNueva);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record MensajeResponse(string Mensaje);

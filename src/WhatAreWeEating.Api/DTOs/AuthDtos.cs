namespace WhatAreWeEating.Api.DTOs;

public record RegistroRequest(string Nombre, string Correo, string Password);

public record ReenviarActivacionRequest(string Correo);

public record MensajeResponse(string Mensaje);

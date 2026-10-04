namespace WhatAreWeEating.Core.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Envía un correo. Lanza una excepción si el envío falla (RF-NOT-08).
    /// </summary>
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancellationToken = default);
}

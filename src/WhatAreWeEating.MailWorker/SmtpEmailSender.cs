using System.Net;
using System.Net.Mail;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.MailWorker;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public SmtpEmailSender(SmtpSettings settings)
    {
        _settings = settings;
    }

    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage(_settings.From, destinatario, asunto, cuerpo);
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.User, _settings.Password),
            Timeout = 15000
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}

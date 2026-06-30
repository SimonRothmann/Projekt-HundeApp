using Dogity.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// Echter SMTP-Versand via MailKit. Noch nicht in DependencyInjection.cs
/// registriert (aktiv ist LoggingEmailSender) - sobald SMTP-Zugangsdaten
/// vorliegen, hier registrieren: services.AddTransient&lt;IEmailSender,
/// SmtpEmailSender&gt;() statt LoggingEmailSender, plus "Email:Smtp:*" in
/// appsettings/.env befüllen (siehe SmtpSettings).
/// </summary>
public class SmtpEmailSender(IOptions<SmtpSettings> options) : IEmailSender
{
    private readonly SmtpSettings _settings = options.Value;

    public async Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

using Dogity.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// Echter SMTP-Versand via MailKit. Registriert wird er in
/// DependencyInjection.cs, sobald "Email:Smtp:Host" gesetzt ist (Zugangsdaten
/// und Absender siehe <see cref="SmtpSettings"/>).
/// </summary>
public class SmtpEmailSender(IOptions<SmtpSettings> options) : IEmailSender
{
    private readonly SmtpSettings _settings = options.Value;

    public async Task SendAsync(string toEmail, string subject, string bodyText, string? bodyHtml = null, IReadOnlyDictionary<string, string>? kopfzeilen = null, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        if (!string.IsNullOrWhiteSpace(_settings.ReplyToAddress))
            message.ReplyTo.Add(new MailboxAddress(_settings.FromName, _settings.ReplyToAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = BuildBody(bodyText, bodyHtml);
        SetzeKopfzeilen(message, kopfzeilen);

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, Verschluesselung(_settings), ct);
        if (MitAnmeldung(_settings))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>
    /// Ohne Benutzername keine Anmeldung: So spricht das Backend den eigenen
    /// Versandserver im Compose-Netz an (deploy/mail), der nur Absender aus
    /// diesem Netz annimmt und gar keine Anmeldung kennt.
    /// </summary>
    public static bool MitAnmeldung(SmtpSettings s) => !string.IsNullOrWhiteSpace(s.Username);

    /// <summary>
    /// Mit Zugangsdaten ist STARTTLS PFLICHT - ein Angreifer im Netz könnte das
    /// Angebot sonst unterdrücken und Benutzername und Passwort im Klartext
    /// mitlesen. Ohne Zugangsdaten (eigener Server im internen Docker-Netz,
    /// der kein TLS anbietet) wird es genutzt, wenn vorhanden.
    /// </summary>
    public static SecureSocketOptions Verschluesselung(SmtpSettings s) =>
        MitAnmeldung(s) ? SecureSocketOptions.StartTls : SecureSocketOptions.StartTlsWhenAvailable;

    /// <summary>Hängt die zusätzlichen Kopfzeilen an die Mail; ohne Angabe bleibt sie unverändert.</summary>
    public static void SetzeKopfzeilen(MimeMessage message, IReadOnlyDictionary<string, string>? kopfzeilen)
    {
        if (kopfzeilen is null) return;
        foreach (var (name, wert) in kopfzeilen) message.Headers.Add(name, wert);
    }

    /// <summary>
    /// Text allein, oder multipart/alternative mit Text UND HTML. Die Textfassung
    /// steht zuerst: Nach RFC 2046 gilt die LETZTE Teilnachricht als die
    /// bevorzugte, Mailprogramme mit HTML zeigen also diese.
    /// </summary>
    public static MimeEntity BuildBody(string bodyText, string? bodyHtml)
    {
        var text = new TextPart("plain") { Text = bodyText };
        if (string.IsNullOrWhiteSpace(bodyHtml)) return text;

        var alternative = new MultipartAlternative { text, new TextPart("html") { Text = bodyHtml } };
        return alternative;
    }
}

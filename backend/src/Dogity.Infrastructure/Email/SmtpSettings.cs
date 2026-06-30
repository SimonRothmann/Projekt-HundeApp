namespace Dogity.Infrastructure.Email;

/// <summary>
/// Konfiguration für <see cref="SmtpEmailSender"/> (Section "Email:Smtp" in
/// appsettings/.env, siehe .env.example). Wird erst relevant, sobald
/// SmtpEmailSender anstelle von LoggingEmailSender registriert wird.
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Dogity";
}

using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Dogity.Infrastructure.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Versand im Hintergrund: ein Wiederholungsversuch, dann verwerfen, nie eine
/// Ausnahme nach außen; die Mail selbst als multipart/alternative.
/// </summary>
public class MailVersandTests
{
    private sealed class ZaehlenderSender(int scheiternBis) : IEmailSender
    {
        public int Versuche { get; private set; }
        public List<(string An, string Betreff, string Text, string? Html)> Gesendet { get; } = [];

        public Task SendAsync(string toEmail, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
        {
            Versuche++;
            if (Versuche <= scheiternBis) throw new InvalidOperationException("SMTP nicht erreichbar.");
            Gesendet.Add((toEmail, subject, bodyText, bodyHtml));
            return Task.CompletedTask;
        }
    }

    private static readonly MailNachricht Mail = new("max@example.com", "Dogity: Test", "Text", "<p>Html</p>");

    [Fact]
    public async Task Erfolg_BeimErstenVersuch()
    {
        var sender = new ZaehlenderSender(scheiternBis: 0);

        var ok = await new MailVersender(NullLogger<MailVersender>.Instance).VersendeAsync(sender, Mail, TimeSpan.Zero, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(1, sender.Versuche);
        Assert.Equal(("max@example.com", "Dogity: Test", "Text", "<p>Html</p>"), Assert.Single(sender.Gesendet));
    }

    [Fact]
    public async Task EinFehler_EinWiederholungsversuch_DanachErfolg()
    {
        var sender = new ZaehlenderSender(scheiternBis: 1);

        var ok = await new MailVersender(NullLogger<MailVersender>.Instance).VersendeAsync(sender, Mail, TimeSpan.Zero, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(2, sender.Versuche);
    }

    [Fact]
    public async Task ZweiFehler_VerwirftDieMail_OhneAusnahme()
    {
        var sender = new ZaehlenderSender(scheiternBis: 5);

        var ok = await new MailVersender(NullLogger<MailVersender>.Instance).VersendeAsync(sender, Mail, TimeSpan.Zero, CancellationToken.None);

        Assert.False(ok);
        Assert.Equal(2, sender.Versuche);
        Assert.Empty(sender.Gesendet);
    }

    [Fact]
    public async Task Fehlerprotokoll_EnthaeltWederEmpfaengerNochInhalt()
    {
        var protokoll = new SammelLogger<MailVersender>();
        var sender = new ZaehlenderSender(scheiternBis: 5);
        var geheim = new MailNachricht("opfer@example.com", "Dogity: Passwort zurücksetzen", "https://x/reset?token=GEHEIM", null);

        await new MailVersender(protokoll).VersendeAsync(sender, geheim, TimeSpan.Zero, CancellationToken.None);

        Assert.NotEmpty(protokoll.Zeilen);
        Assert.All(protokoll.Zeilen, z =>
        {
            Assert.DoesNotContain("opfer@example.com", z);
            Assert.DoesNotContain("GEHEIM", z);
            Assert.DoesNotContain("Passwort", z);
        });
    }

    [Fact]
    public void Mail_MitHtml_IstMultipartAlternative_MitTextZuerst()
    {
        var body = SmtpEmailSender.BuildBody("Nur Text", "<p>Mit Html</p>");

        var alternative = Assert.IsType<MultipartAlternative>(body);
        Assert.Equal(2, alternative.Count);
        Assert.Equal("plain", ((TextPart)alternative[0]).ContentType.MediaSubtype);
        Assert.Equal("html", ((TextPart)alternative[1]).ContentType.MediaSubtype);
    }

    [Fact]
    public void Mail_OhneHtml_IstReinerText()
    {
        var body = SmtpEmailSender.BuildBody("Nur Text", null);

        Assert.Equal("plain", Assert.IsType<TextPart>(body).ContentType.MediaSubtype);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoggingSender_ProtokolliertNieDasHtml_UndInProduktionNieDenText(bool development)
    {
        var protokoll = new SammelLogger<LoggingEmailSender>();
        var umgebung = new TestUmgebung(development ? Environments.Development : Environments.Production);
        var sender = new LoggingEmailSender(protokoll, umgebung);

        await sender.SendAsync("max@example.com", "Dogity: Test", "Text mit https://x/reset?token=GEHEIM", "<p>HTML-GEHEIM</p>");

        var alles = string.Join("\n", protokoll.Zeilen);
        Assert.Contains("max@example.com", alles);
        Assert.DoesNotContain("HTML-GEHEIM", alles);
        if (!development) Assert.DoesNotContain("GEHEIM", alles);
    }

    private sealed class TestUmgebung(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class SammelLogger<T> : ILogger<T>
    {
        public List<string> Zeilen { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Zeilen.Add(formatter(state, exception) + (exception is null ? "" : " | " + exception.Message));
    }
}

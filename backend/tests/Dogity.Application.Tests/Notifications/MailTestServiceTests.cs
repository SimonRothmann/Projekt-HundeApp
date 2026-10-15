using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// "Testmail senden" im Adminbereich: direkt statt über die Warteschlange, mit
/// der Antwort des Mailservers - und ohne Mailversand gar nicht.
/// </summary>
public class MailTestServiceTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 15, 18, 5, 0, TimeSpan.Zero);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    /// <summary>Merkt sich jede Mail; wirft auf Wunsch statt zu senden oder wartet auf den Abbruch.</summary>
    private sealed class MerkenderSender : IEmailSender
    {
        public List<(string An, string Betreff, string Text, string? Html)> Gesendet { get; } = [];
        public Exception? Wirft { get; set; }
        public bool HaengtFest { get; set; }

        public async Task SendAsync(string toEmail, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
        {
            if (HaengtFest) await Task.Delay(Timeout.Infinite, ct);
            if (Wirft is not null) throw Wirft;
            Gesendet.Add((toEmail, subject, bodyText, bodyHtml));
        }
    }

    private static (MailTestService Dienst, MerkenderSender Sender) Aufbauen(bool email = true)
    {
        var sender = new MerkenderSender();
        var dienst = new MailTestService(
            sender,
            new FakeNotificationChannels { EmailAktiv = email },
            TestLinks.Mit(),
            new MailDrossel(new FesteUhr()),
            new FesteUhr());
        return (dienst, sender);
    }

    [Fact]
    public async Task MitMailversand_SendetDirekt_UndMeldetAngenommen()
    {
        var (dienst, sender) = Aufbauen();

        var ergebnis = await dienst.SendeAsync("  max@example.com ");

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(new TestmailErgebnisDto(VersandEingerichtet: true, Versendet: true, Fehler: null), ergebnis.Value);
        var mail = Assert.Single(sender.Gesendet);
        Assert.Equal("max@example.com", mail.An);
        Assert.Equal("Dogity: Testmail", mail.Betreff);
        Assert.Contains("15.10.2026 um 18:05 Uhr (UTC)", mail.Text);
        Assert.Contains("https://dogity.example/dashboard", mail.Text);
        Assert.NotNull(mail.Html);
    }

    [Fact]
    public async Task OhneMailversand_SendetNichts_UndSagtDas()
    {
        var (dienst, sender) = Aufbauen(email: false);

        var ergebnis = await dienst.SendeAsync("max@example.com");

        Assert.True(ergebnis.Succeeded);
        Assert.False(ergebnis.Value!.VersandEingerichtet);
        Assert.False(ergebnis.Value.Versendet);
        Assert.Empty(sender.Gesendet);
    }

    [Theory]
    [InlineData("")]
    [InlineData("keine-adresse")]
    [InlineData("Max <max@example.com>")]
    [InlineData("max@example.com\r\nBcc: viele@example.com")]
    public async Task UngueltigeAdresse_WirdAbgelehnt(string adresse)
    {
        var (dienst, sender) = Aufbauen();

        var ergebnis = await dienst.SendeAsync(adresse);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(sender.Gesendet);
    }

    [Fact]
    public async Task MailserverLehntAb_DieAntwortKommtZurueck()
    {
        var (dienst, sender) = Aufbauen();
        sender.Wirft = new InvalidOperationException("5.7.1 <noreply@dogity.net>: Sender address rejected");

        var ergebnis = await dienst.SendeAsync("max@example.com");

        Assert.True(ergebnis.Succeeded);
        Assert.True(ergebnis.Value!.VersandEingerichtet);
        Assert.False(ergebnis.Value.Versendet);
        Assert.Contains("Sender address rejected", ergebnis.Value.Fehler);
    }

    [Fact]
    public async Task HoechstensZwanzigAmTag()
    {
        var (dienst, sender) = Aufbauen();

        for (var i = 0; i < 20; i++)
            Assert.True((await dienst.SendeAsync($"test{i}@example.com")).Succeeded);
        var einundzwanzigste = await dienst.SendeAsync("noch.eine@example.com");

        Assert.False(einundzwanzigste.Succeeded);
        Assert.Equal(20, sender.Gesendet.Count);
    }

    [Fact]
    public async Task AbbruchDesAufrufers_WirdNichtAlsZeitueberschreitungGemeldet()
    {
        var (dienst, sender) = Aufbauen();
        sender.HaengtFest = true;
        using var abbruch = new CancellationTokenSource();
        abbruch.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dienst.SendeAsync("max@example.com", abbruch.Token));
    }
}

using System.Net;
using System.Security.Cryptography;
using Dogity.Application.Notifications;
using Dogity.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// WebPushSender gegen einen Handler, der nie ins Netz geht: Die Antworten des
/// Push-Dienstes werden nachgestellt, die Anfrage wird geprüft.
/// </summary>
public class WebPushSenderTests
{
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Anfrage { get; private set; }
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Anfrage = request;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private static string Base64Url(byte[] daten) =>
        Convert.ToBase64String(daten).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Ein frisches P-256-Schlüsselpaar: unkomprimierter öffentlicher Schlüssel (65 Byte) und privater (32 Byte).</summary>
    private static (string Public, string Private) SchluesselPaar()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(true);
        return (Base64Url([0x04, .. p.Q.X!, .. p.Q.Y!]), Base64Url(p.D!));
    }

    private static PushZiel Geraet()
    {
        var (publicKey, _) = SchluesselPaar();
        return new PushZiel("https://web.push.apple.com/QAbc", publicKey, Base64Url(RandomNumberGenerator.GetBytes(16)));
    }

    private static (WebPushSender Sender, Handler Handler) Aufbauen(HttpStatusCode status, bool konfiguriert = true)
    {
        var (pub, priv) = SchluesselPaar();
        var einstellungen = konfiguriert
            ? new PushSettings { VapidPublicKey = pub, VapidPrivateKey = priv, Subject = "mailto:hallo@dogity.example" }
            : new PushSettings();
        var handler = new Handler(status);
        var sender = new WebPushSender(new HttpClient(handler), Options.Create(einstellungen), NullLogger<WebPushSender>.Instance);
        return (sender, handler);
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, PushErgebnis.Gesendet)]
    [InlineData(HttpStatusCode.NotFound, PushErgebnis.AboUngueltig)]
    [InlineData(HttpStatusCode.Gone, PushErgebnis.AboUngueltig)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, PushErgebnis.Fehler)]
    [InlineData(HttpStatusCode.BadRequest, PushErgebnis.Fehler)]
    [InlineData(HttpStatusCode.InternalServerError, PushErgebnis.Fehler)]
    public async Task UebersetztDieAntwortDesPushDienstes(HttpStatusCode status, PushErgebnis erwartet)
    {
        var (sender, _) = Aufbauen(status);

        var ergebnis = await sender.SendAsync(Geraet(), PushPayload.Erzeuge("Hallo", "https://dogity.example/", 1));

        Assert.Equal(erwartet, ergebnis);
    }

    [Fact]
    public async Task Anfrage_IstVapidSigniert_Befristet_UndVerschluesselt()
    {
        var (sender, handler) = Aufbauen(HttpStatusCode.Created);
        const string klartext = "Geheime Vereinsmeldung";

        await sender.SendAsync(Geraet(), PushPayload.Erzeuge(klartext, "https://dogity.example/", 1));

        var anfrage = handler.Anfrage!;
        Assert.Equal(HttpMethod.Post, anfrage.Method);
        Assert.Equal("https://web.push.apple.com/QAbc", anfrage.RequestUri!.ToString());
        Assert.NotNull(anfrage.Headers.Authorization);
        Assert.Equal("86400", anfrage.Headers.GetValues("TTL").Single());
        // "normal" ist die Vorgabe des Protokolls; die Bibliothek lässt sie weg.
        if (anfrage.Headers.TryGetValues("Urgency", out var dringlichkeit)) Assert.Equal("normal", dringlichkeit.Single());
        Assert.Contains("aes128gcm", anfrage.Content!.Headers.ContentEncoding);
        // Ende-zu-Ende: Im Body steht nicht der Klartext, der Push-Dienst kann ihn nicht lesen.
        Assert.NotNull(handler.Body);
        Assert.DoesNotContain(klartext, System.Text.Encoding.UTF8.GetString(handler.Body!));
    }

    [Fact]
    public async Task OhneKonfiguration_SendetNichts()
    {
        var (sender, handler) = Aufbauen(HttpStatusCode.Created, konfiguriert: false);

        var ergebnis = await sender.SendAsync(Geraet(), "{}");

        Assert.Equal(PushErgebnis.Fehler, ergebnis);
        Assert.Null(handler.Anfrage);
    }
}

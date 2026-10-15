using System.Text.Json;
using Dogity.Application.Notifications;

namespace Dogity.Application.Tests.Notifications;

/// <summary>Payload-Format, Endpoint-Prüfung und Gerätebezeichnung - reine Funktionen.</summary>
public class PushHilfenTests
{
    [Fact]
    public void Payload_FolgtDemDeclarativeWebPushFormat()
    {
        var json = PushPayload.Erzeuge("Anna lädt dich ein.", "https://dogity.example/clubs", 3);

        using var dokument = JsonDocument.Parse(json);
        var wurzel = dokument.RootElement;
        Assert.Equal(8030, wurzel.GetProperty("web_push").GetInt32());
        var notification = wurzel.GetProperty("notification");
        Assert.Equal("Dogity", notification.GetProperty("title").GetString());
        Assert.Equal("Anna lädt dich ein.", notification.GetProperty("body").GetString());
        Assert.Equal("https://dogity.example/clubs", notification.GetProperty("navigate").GetString());
        Assert.Equal("de", notification.GetProperty("lang").GetString());
        Assert.Equal(3, notification.GetProperty("app_badge").GetInt32());
    }

    [Fact]
    public void Payload_NavigateIstAbsolut()
    {
        var links = Tests.TestSupport.TestLinks.Mit();

        var json = PushPayload.Erzeuge("x", links.Absolut("/dogs/42?eintrag=7"), 0);

        using var dokument = JsonDocument.Parse(json);
        var navigate = dokument.RootElement.GetProperty("notification").GetProperty("navigate").GetString()!;
        Assert.True(Uri.TryCreate(navigate, UriKind.Absolute, out var uri));
        Assert.Equal("https", uri!.Scheme);
        Assert.Equal("/dogs/42", uri.AbsolutePath);
    }

    [Fact]
    public void Payload_MaskiertSonderzeichenImJson()
    {
        var json = PushPayload.Erzeuge("Du wurdest zu \"Verein\" hinzugefügt.\nNeue Zeile", "https://dogity.example/", 1);

        using var dokument = JsonDocument.Parse(json);
        Assert.Equal("Du wurdest zu \"Verein\" hinzugefügt.\nNeue Zeile",
            dokument.RootElement.GetProperty("notification").GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://web.push.apple.com/QAbc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://example.com/push", false)]
    [InlineData("https://localhost/push", false)]
    [InlineData("https://169.254.169.254/latest", false)]
    [InlineData("ftp://web.push.apple.com/abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Endpoint_NurBekanntePushDienste(string? endpoint, bool erwartet) =>
        Assert.Equal(erwartet, PushEndpointPruefung.IstErlaubt(endpoint));

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_4 like Mac OS X) AppleWebKit/605.1.15 Version/18.4 Mobile/15E148 Safari/604.1", "iPhone (Safari)")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/130.0 Mobile Safari/537.36", "Android (Chrome)")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0 Safari/537.36 Edg/130.0", "Windows (Edge)")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:132.0) Gecko/20100101 Firefox/132.0", "Mac (Firefox)")]
    [InlineData("curl/8.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Geraetebezeichnung_NurGeraetUndBrowser(string? userAgent, string? erwartet) =>
        Assert.Equal(erwartet, GeraeteBezeichnung.Aus(userAgent));
}

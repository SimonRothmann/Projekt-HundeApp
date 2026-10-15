using Dogity.Application.Notifications;
using Dogity.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Fakes für Mail und Push: Sie sammeln im Arbeitsspeicher, was versendet würde.
/// Kein Test darf eine echte Mail oder einen echten Push absetzen - keine dieser
/// Klassen spricht mit dem Netz.
/// </summary>
public class FakeMailQueue : IMailQueue
{
    public List<MailNachricht> Mails { get; } = [];

    public bool Enqueue(MailNachricht mail)
    {
        Mails.Add(mail);
        return true;
    }
}

public class FakePushQueue : IPushQueue
{
    public List<PushAuftrag> Auftraege { get; } = [];

    public bool Enqueue(PushAuftrag auftrag)
    {
        Auftraege.Add(auftrag);
        return true;
    }
}

public class FakeNotificationChannels : INotificationChannels
{
    public bool EmailAktiv { get; set; }
    public string? PushPublicKey { get; set; }
}

/// <summary>Mit "https://dogity.example" als Basis - die echte <see cref="FrontendLinks"/>, nur anders konfiguriert.</summary>
public static class TestLinks
{
    public const string Basis = "https://dogity.example";

    public static FrontendLinks Mit(string? basis = Basis) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = basis })
            .Build());
}

/// <summary>Antwortet je Endpoint mit dem vorgegebenen Ergebnis und merkt sich, was ankam.</summary>
public class FakePushSender : IPushSender
{
    public Dictionary<string, PushErgebnis> Ergebnisse { get; } = [];
    public List<(PushZiel Ziel, string Payload)> Gesendet { get; } = [];

    /// <summary>Endpoints, bei denen der Sender wirft statt zu antworten.</summary>
    public HashSet<string> Wirft { get; } = [];

    public Task<PushErgebnis> SendAsync(PushZiel ziel, string payload, CancellationToken ct = default)
    {
        Gesendet.Add((ziel, payload));
        if (Wirft.Contains(ziel.Endpoint)) throw new InvalidOperationException("Netz weg.");
        return Task.FromResult(Ergebnisse.GetValueOrDefault(ziel.Endpoint, PushErgebnis.Gesendet));
    }
}

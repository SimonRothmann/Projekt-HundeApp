using System.Threading.RateLimiting;

namespace Dogity.Api.Hosting;

/// <summary>
/// Begrenzt Anmeldeversuche je E-Mail-Adresse, zusätzlich zur Grenze je IP-Adresse ("anmelden" in Program.cs).
///
/// Die IP-Grenze ist bewusst weit, weil sich bei Vereinsveranstaltungen viele ein WLAN und damit eine
/// Adresse teilen. Damit ein einzelnes Konto trotzdem nicht beliebig oft probiert werden kann (auch von
/// vielen Adressen aus), zählt dieser Zähler je Adresse mit. Hier und nicht als Drossel-Richtlinie: Die
/// Adresse steht im Rumpf der Anfrage, den die Drosselung vor dem Controller nicht liest.
///
/// Gezählt wird jeder Versuch, nicht nur der falsche: Eine Prüfung des Passworts kostet in jedem Fall
/// dieselbe Rechenzeit. Gruppiert wird ohne Beachtung der Groß-/Kleinschreibung und ohne Leerzeichen am
/// Rand, sonst bekäme jede Schreibweise einen eigenen Topf. Der Zähler liegt im Speicher (ein Backend,
/// ein Prozess); die eingebaute Verwaltung der Töpfe räumt ungenutzte nach der Frist selbst weg.
/// </summary>
public sealed class AnmeldeDrossel : IDisposable
{
    public const int VersucheJeMinute = 10;

    private readonly PartitionedRateLimiter<string> _begrenzer;

    public AnmeldeDrossel(int grenze = VersucheJeMinute, TimeSpan? fenster = null)
    {
        _begrenzer = PartitionedRateLimiter.Create<string, string>(
            adresse => RateLimitPartition.GetFixedWindowLimiter(adresse, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = grenze,
                Window = fenster ?? TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Zählt einen Versuch mit dieser Adresse. False, wenn die Grenze für die laufende Minute erreicht ist.</summary>
    public bool Darf(string? email)
    {
        using var lease = _begrenzer.AttemptAcquire((email ?? "").Trim());
        return lease.IsAcquired;
    }

    public void Dispose() => _begrenzer.Dispose();
}

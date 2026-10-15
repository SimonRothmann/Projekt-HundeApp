using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class PushDispatcher(IApplicationDbContext db, IPushSender sender, ILogger<PushDispatcher> logger) : IPushDispatcher
{
    public async Task SendeAsync(PushAuftrag auftrag, CancellationToken ct = default)
    {
        var abos = await db.PushSubscriptions.Where(s => s.UserId == auftrag.UserId).ToListAsync(ct);
        if (abos.Count == 0) return;

        var abgelaufen = new List<Domain.Notifications.PushSubscription>();
        foreach (var abo in abos)
        {
            PushErgebnis ergebnis;
            try
            {
                ergebnis = await sender.SendAsync(new PushZiel(abo.Endpoint, abo.P256dh, abo.Auth), auftrag.Payload, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Ein einzelnes Gerät darf die übrigen nicht aufhalten. Weder die
                // Adresse (enthält ein geheimes Token des Push-Dienstes) noch der
                // Inhalt gehören ins Log - die Kennung der Zeile genügt.
                logger.LogWarning(ex, "Push an Gerät {AboId} fehlgeschlagen.", abo.Id);
                continue;
            }

            switch (ergebnis)
            {
                case PushErgebnis.Gesendet:
                    abo.LastUsedAt = DateTimeOffset.UtcNow;
                    break;
                case PushErgebnis.AboUngueltig:
                    abgelaufen.Add(abo);
                    break;
            }
        }

        // Der Push-Dienst kennt das Gerät nicht mehr: Die Zeile hat keinen Zweck
        // mehr und wird endgültig entfernt (samt Schlüsseln).
        db.PushSubscriptions.RemoveRange(abgelaufen);
        await db.SaveChangesAsync(ct);
    }
}

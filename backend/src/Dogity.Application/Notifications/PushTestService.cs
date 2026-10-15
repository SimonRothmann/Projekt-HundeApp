using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class PushTestService(
    IApplicationDbContext db,
    INotificationService notifications,
    INotificationChannels channels,
    IFrontendLinks links) : IPushTestService
{
    public const string Meldung = "Test-Push aus dem Adminbereich: Kommt diese Mitteilung an, funktioniert Push auf deinen Geräten.";

    public async Task<TestpushErgebnisDto> SendeAnMichAsync(Guid userId, CancellationToken ct = default)
    {
        // Dieselben Bedingungen wie NotificationService.PushEinreihenAsync: ohne
        // Schlüssel kein Push, ohne Basis-Adresse kein gültiger Link darin.
        if (channels.PushPublicKey is null || !links.HatBasis)
            return new TestpushErgebnisDto(PushEingerichtet: false, Geraete: 0);

        var geraete = await db.PushSubscriptions.CountAsync(s => s.UserId == userId, ct);

        // Ohne Gerät keine Meldung: Sie stünde nur in der Glocke und bewiese nichts.
        if (geraete > 0)
            await notifications.CreateAsync(userId, Meldung, "/admin/system", ct);

        return new TestpushErgebnisDto(PushEingerichtet: true, Geraete: geraete);
    }
}

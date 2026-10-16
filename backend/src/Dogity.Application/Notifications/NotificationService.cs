using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Notifications;

public class NotificationService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    INotificationChannels channels,
    IFrontendLinks links,
    IEmailAbmeldeToken abmeldeTokens,
    IMailQueue mailQueue,
    IPushQueue pushQueue,
    ILogger<NotificationService> logger) : INotificationService
{
    private const int MaxReturned = 50;

    public async Task CreateAsync(Guid userId, string message, string? linkPath = null, CancellationToken ct = default, bool wichtig = false)
    {
        db.Notifications.Add(new Notification { UserId = userId, Message = message, LinkPath = linkPath });
        await db.SaveChangesAsync(ct);

        // Die Glocke ist gespeichert - ab hier ist alles Zugabe. Ein Fehler beim
        // Einreihen (Nutzersuche, Zählung) darf den Aufrufer nicht scheitern
        // lassen: Der hat gerade eine Einladung oder Entscheidung durchgeführt.
        try
        {
            if (wichtig) await MailEinreihenAsync(userId, message, linkPath, ct);
            await PushEinreihenAsync(userId, message, linkPath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Ohne Inhalt und ohne Adresse: nur, dass es nicht ging.
            logger.LogWarning(ex, "Mail/Push zu einer Benachrichtigung konnte nicht eingereiht werden.");
        }
    }

    private async Task MailEinreihenAsync(Guid userId, string message, string? linkPath, CancellationToken ct)
    {
        if (!channels.EmailAktiv) return;

        // Opt-out: erst eine Zeile mit "aus" schaltet ab. Wer nie etwas
        // eingestellt hat, hat keine Zeile und bekommt die Mail.
        var abgeschaltet = await db.UserPreferences.AnyAsync(p => p.UserId == userId && !p.EmailNotifications, ct);
        if (abgeschaltet) return;

        var empfaenger = (await userLookup.FindByIdsAsync([userId], ct)).GetValueOrDefault(userId);
        if (empfaenger is null || string.IsNullOrWhiteSpace(empfaenger.Email)) return;

        var link = links.HatBasis ? links.Absolut(linkPath ?? "/dashboard") : null;
        var einstellungen = links.HatBasis ? links.Absolut("/profile/funktionen") : null;
        // Jede dieser Mails lässt sich ohne Anmeldung abbestellen - per Link im Text
        // (siehe AbmeldeAdressen, auch zur Frage der List-Unsubscribe-Kopfzeilen).
        var abmeldeSeite = AbmeldeAdressen.Seite(userId, abmeldeTokens, links);
        var inhalt = MailVorlagen.WichtigeBenachrichtigung(empfaenger.FirstName, message, link, einstellungen, abmeldeSeite);
        mailQueue.Enqueue(new MailNachricht(empfaenger.Email, inhalt.Betreff, inhalt.Text, inhalt.Html, abmeldeSeite));
    }

    private async Task PushEinreihenAsync(Guid userId, string message, string? linkPath, CancellationToken ct)
    {
        if (channels.PushPublicKey is null) return;
        // Das Declarative-Web-Push-Format verlangt in "navigate" eine ABSOLUTE
        // Adresse; ohne Frontend:BaseUrl käme nur ein Pfad heraus, und Safari
        // würde die Nachricht verwerfen. Die Glocke bleibt (lokale Entwicklung).
        if (!links.HatBasis) return;
        // Ohne Gerät keine Zählung und kein Auftrag: Die meisten haben (noch) keins.
        if (!await db.PushSubscriptions.AnyAsync(s => s.UserId == userId, ct)) return;

        var ungelesen = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        var payload = PushPayload.Erzeuge(message, links.Absolut(linkPath ?? "/dashboard"), ungelesen);
        pushQueue.Enqueue(new PushAuftrag(userId, payload));
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxReturned)
            .Select(n => new NotificationDto(n.Id, n.Message, n.LinkPath, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<NotificationDto>>.Success(notifications);
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return Result<int>.Success(count);
    }

    public async Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null)
            return Result.NotFound("Benachrichtigung nicht gefunden.");

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return Result.Success();
    }
}

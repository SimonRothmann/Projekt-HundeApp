using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class AccountMailService(
    IApplicationDbContext db,
    IMailQueue mailQueue,
    INotificationChannels channels,
    IFrontendLinks links,
    IEmailAbmeldeToken abmeldeTokens,
    IUserLookupService userLookup,
    INotificationService notifications,
    MailDrossel drossel) : IAccountMailService
{
    public void SendeWillkommen(Guid userId, string email, string? sprache = null)
    {
        if (!channels.EmailAktiv) return;
        if (!drossel.Erlaubt(KontomailArt.Willkommen, email)) return;

        // Ohne Vornamen: Die Adresse ist bei der Registrierung nicht bestätigt, und
        // der Name ist frei wählbar - in der Anrede einer Mail an Fremde wäre er
        // ein Weg, eigenen Text zuzustellen.
        // Auch die Willkommensmail lässt sich abbestellen - gemeint sind die
        // späteren Benachrichtigungen; die Konto-Mails selbst bleiben.
        var abmeldeSeite = AbmeldeAdressen.Seite(userId, abmeldeTokens, links);
        var inhalt = MailVorlagen.Willkommen(links.Absolut("/dashboard"), abmeldeSeite, sprache);
        mailQueue.Enqueue(new MailNachricht(email, inhalt.Betreff, inhalt.Text, inhalt.Html, abmeldeSeite));
    }

    public async Task SendePasswortZuruecksetzenAsync(PasswortResetAnfrage anfrage, string adminRolle, CancellationToken ct = default)
    {
        // Mit Mailversand: höchstens eine Mail je Adresse und Zeitraum (siehe
        // MailDrossel). Die Antwort des Endpunkts bleibt gleich, nur die Mail
        // bleibt aus - sonst ließe sich ein Postfach mit Reset-Mails fluten.
        if (channels.EmailAktiv && !drossel.Erlaubt(KontomailArt.PasswortZuruecksetzen, anfrage.Email)) return;

        // Immer einreihen, auch ohne SMTP: Dann protokolliert der
        // LoggingEmailSender (nur in Development mit Link, siehe dort).
        // In der Sprache der Person, die das Passwort zurücksetzen will - auch wenn ein
        // Admin den Anstoß gibt, liest sie die Mail. Ohne Einstellungszeile: Deutsch.
        var sprache = await db.UserPreferences
            .Where(p => p.UserId == anfrage.UserId)
            .Select(p => p.Locale)
            .FirstOrDefaultAsync(ct);
        var inhalt = MailVorlagen.PasswortZuruecksetzen(anfrage.Vorname, anfrage.Link, sprache);
        mailQueue.Enqueue(new MailNachricht(anfrage.Email, inhalt.Betreff, inhalt.Text, inhalt.Html));

        if (channels.EmailAktiv) return;

        foreach (var adminId in await userLookup.ListUserIdsInRoleAsync(adminRolle, ct))
        {
            await notifications.CreateAsync(
                adminId,
                Meldung.Von(
                    "{name} ({email}) möchte das Passwort zurücksetzen.",
                    ("name", $"{anfrage.Vorname} {anfrage.Nachname}"), ("email", anfrage.Email)),
                $"/admin/nutzer?resetUser={anfrage.UserId}",
                ct);
        }
    }
}

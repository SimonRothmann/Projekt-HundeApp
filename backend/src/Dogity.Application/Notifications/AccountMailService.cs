using Dogity.Application.Abstractions;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class AccountMailService(
    IMailQueue mailQueue,
    INotificationChannels channels,
    IFrontendLinks links,
    IUserLookupService userLookup,
    INotificationService notifications,
    MailDrossel drossel) : IAccountMailService
{
    public void SendeWillkommen(string email)
    {
        if (!channels.EmailAktiv) return;
        if (!drossel.Erlaubt(KontomailArt.Willkommen, email)) return;

        // Ohne Vornamen: Die Adresse ist bei der Registrierung nicht bestätigt, und
        // der Name ist frei wählbar - in der Anrede einer Mail an Fremde wäre er
        // ein Weg, eigenen Text zuzustellen.
        var inhalt = MailVorlagen.Willkommen(links.Absolut("/dashboard"));
        mailQueue.Enqueue(new MailNachricht(email, inhalt.Betreff, inhalt.Text, inhalt.Html));
    }

    public async Task SendePasswortZuruecksetzenAsync(PasswortResetAnfrage anfrage, string adminRolle, CancellationToken ct = default)
    {
        // Mit Mailversand: höchstens eine Mail je Adresse und Zeitraum (siehe
        // MailDrossel). Die Antwort des Endpunkts bleibt gleich, nur die Mail
        // bleibt aus - sonst ließe sich ein Postfach mit Reset-Mails fluten.
        if (channels.EmailAktiv && !drossel.Erlaubt(KontomailArt.PasswortZuruecksetzen, anfrage.Email)) return;

        // Immer einreihen, auch ohne SMTP: Dann protokolliert der
        // LoggingEmailSender (nur in Development mit Link, siehe dort).
        var inhalt = MailVorlagen.PasswortZuruecksetzen(anfrage.Vorname, anfrage.Link);
        mailQueue.Enqueue(new MailNachricht(anfrage.Email, inhalt.Betreff, inhalt.Text, inhalt.Html));

        if (channels.EmailAktiv) return;

        foreach (var adminId in await userLookup.ListUserIdsInRoleAsync(adminRolle, ct))
        {
            await notifications.CreateAsync(
                adminId,
                $"{anfrage.Vorname} {anfrage.Nachname} ({anfrage.Email}) möchte das Passwort zurücksetzen.",
                $"/admin?resetUser={anfrage.UserId}",
                ct);
        }
    }
}

using System.Net;
using System.Text;
using Dogity.Application.Preferences;

namespace Dogity.Application.Notifications;

/// <summary>Eine gerenderte Mail: Betreff, Textfassung, HTML-Fassung.</summary>
public record MailInhalt(string Betreff, string Text, string Html);

/// <summary>
/// Alle Mailvorlagen an einer Stelle - reine Funktionen ohne Zugriff auf
/// Datenbank oder Konfiguration, damit sie sich ohne Aufbau testen lassen.
///
/// Bewusst klein und schlicht: HTML mit Inline-Styles (Mailprogramme kennen
/// kein externes CSS), eine Spalte, ein Knopf. Jede Mail hat zusätzlich eine
/// Textfassung - Mailprogramme ohne HTML und Spamfilter erwarten sie.
///
/// Eingesetzte Werte (Name, Nachrichtentext) werden HTML-kodiert: Der Text
/// einer Benachrichtigung enthält Vereins- und Hundenamen, die Nutzer:innen
/// selbst vergeben haben.
///
/// Sprache: Die drei Mails an Nutzer:innen nehmen die Sprache der Empfänger:in
/// (UserPreference.Locale, siehe <see cref="Sprachen"/>); ohne Angabe oder bei
/// einer unbekannten Sprache Deutsch. Der Text einer Benachrichtigung kommt
/// schon in dieser Sprache an. Die Testmail ist Adminwerkzeug und bleibt deutsch.
/// </summary>
public static class MailVorlagen
{
    private const string Marke = "Dogity";

    /// <remarks>
    /// Bewusst ohne Namen: Die Mail geht an eine bei der Registrierung noch
    /// unbestätigte Adresse, der Name ist frei wählbar.
    /// </remarks>
    /// <param name="abmeldeSeite">Seite zum Abbestellen der Benachrichtigungen; null = kein Hinweis (keine Basis-Adresse bekannt).</param>
    /// <param name="sprache">Sprache der Empfänger:in; null = Deutsch.</param>
    public static MailInhalt Willkommen(string appUrl, string? abmeldeSeite = null, string? sprache = null)
    {
        var en = Sprachen.IstEnglisch(sprache);
        var anrede = Anrede("", en);
        var absaetze = en
            ? new[]
            {
                "Welcome aboard! Dogity is your training diary for dog sports.",
                "Here is what to do first: add a dog and log your first training session - or join a club if you train in a group.",
                "Questions or problems? Just reply to this email.",
            }
            : new[]
            {
                "schön, dass du dabei bist! Dogity ist dein Trainingstagebuch für den Hundesport.",
                "Das kannst du als Erstes tun: einen Hund anlegen und dein erstes Training eintragen - oder einem Verein beitreten, wenn du in einer Gruppe trainierst.",
                "Fragen oder Fehler? Antworte einfach auf diese Mail.",
            };
        return Baue(
            en ? $"Welcome to {Marke}" : $"Willkommen bei {Marke}", anrede, absaetze, en ? "Open Dogity" : "Zu Dogity", appUrl, fusszeile: null,
            en
                ? "You can unsubscribe from email notifications (invitations, decisions) at any time:"
                : "Benachrichtigungen per E-Mail (Einladungen, Entscheidungen) kannst du jederzeit abbestellen:",
            abmeldeSeite, en);
    }

    /// <param name="sprache">Sprache der Empfänger:in; null = Deutsch.</param>
    public static MailInhalt PasswortZuruecksetzen(string vorname, string link, string? sprache = null)
    {
        var en = Sprachen.IstEnglisch(sprache);
        var anrede = Anrede(vorname, en);
        var absaetze = en
            ? new[]
            {
                "Use the button below to set a new password for your Dogity account.",
                "If you didn't request this, you can ignore this email - your password stays as it is.",
            }
            : new[]
            {
                "über den Knopf unten kannst du ein neues Passwort für dein Dogity-Konto festlegen.",
                "Wenn du das nicht angefordert hast, kannst du diese Mail ignorieren - dein Passwort bleibt dann, wie es ist.",
            };
        return Baue(
            en ? $"{Marke}: Reset your password" : $"{Marke}: Passwort zurücksetzen", anrede, absaetze,
            en ? "Reset password" : "Passwort zurücksetzen", link, fusszeile: null, abmeldeSatz: null, abmeldeSeite: null, en);
    }

    /// <summary>
    /// "Testmail senden" im Adminbereich. Gleicher Aufbau wie jede echte Mail
    /// (Text und HTML, ein Knopf), damit ein Prüfdienst wie mail-tester.com
    /// bewertet, was später tatsächlich verschickt wird.
    /// </summary>
    public static MailInhalt Testmail(string appUrl, DateTimeOffset zeitpunkt)
    {
        var anrede = Anrede("", englisch: false);
        var absaetze = new[]
        {
            $"das ist eine Testmail aus dem Adminbereich von Dogity, verschickt am {zeitpunkt.UtcDateTime:dd.MM.yyyy} um {zeitpunkt.UtcDateTime:HH:mm} Uhr (UTC).",
            "Ist sie angekommen, funktioniert der Versand. Landet sie im Spam, lohnt ein Blick in die Kopfzeilen: Dort sollte bei SPF, DKIM und DMARC jeweils \"pass\" stehen.",
        };
        return Baue($"{Marke}: Testmail", anrede, absaetze, "Zu Dogity", appUrl, fusszeile: null, abmeldeSatz: null, abmeldeSeite: null, englisch: false);
    }

    /// <param name="nachricht">Der Text der Glockenmeldung.</param>
    /// <param name="link">Wohin der Knopf führt; null = kein Knopf (keine Basis-Adresse bekannt).</param>
    /// <param name="einstellungenUrl">Wohin der Hinweis zum Abschalten verweist (Profil → Funktionen).</param>
    /// <param name="abmeldeSeite">Seite, die ohne Anmeldung abmeldet; null = kein Abmelde-Link (keine Basis-Adresse bekannt).</param>
    /// <param name="sprache">Sprache der Empfänger:in; null = Deutsch. Der <paramref name="nachricht"/> ist schon in ihr geschrieben.</param>
    public static MailInhalt WichtigeBenachrichtigung(string vorname, string nachricht, string? link, string? einstellungenUrl, string? abmeldeSeite = null, string? sprache = null)
    {
        var en = Sprachen.IstEnglisch(sprache);
        var anrede = Anrede(vorname, en);
        var fusszeile = en
            ? "You are receiving this email because you get important Dogity notifications by email too " +
              "(invitations and decisions on join requests). You can turn this off under Profile → Features" +
              (einstellungenUrl is null ? "." : $": {einstellungenUrl}")
            : "Du bekommst diese Mail, weil du bei Dogity wichtige Benachrichtigungen auch per E-Mail erhältst " +
              "(Einladungen und Entscheidungen über Beitrittsanfragen). Abschalten kannst du das unter Profil → Funktionen" +
              (einstellungenUrl is null ? "." : $": {einstellungenUrl}");
        return Baue(
            Betreff(nachricht), anrede, [nachricht], en ? "View in Dogity" : "In Dogity ansehen", link, fusszeile,
            en ? "No more emails like this? Unsubscribe with one click:" : "Keine solchen Mails mehr? Mit einem Klick abmelden:",
            abmeldeSeite, en);
    }

    /// <summary>
    /// Betreff aus dem Meldungstext: "Dogity: " und der Text, gekürzt. Der Text
    /// einer Meldung ist schon ein ganzer Satz ("Max lädt dich in den Verein ... ein.") -
    /// ein allgemeiner Betreff wie "Neue Benachrichtigung" ließe die Mail im
    /// Posteingang wie jede andere aussehen. Zeilenumbrüche und Steuerzeichen
    /// fliegen raus: ein Betreff ist eine Zeile.
    /// </summary>
    public static string Betreff(string nachricht)
    {
        const int Hoechstens = 80;
        var sauber = new string(nachricht.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        sauber = string.Join(' ', sauber.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (sauber.Length > Hoechstens) sauber = sauber[..(Hoechstens - 1)].TrimEnd() + "…";
        return $"{Marke}: {sauber}";
    }

    private static string Anrede(string vorname, bool englisch)
    {
        var gruss = englisch ? "Hi" : "Hallo";
        return string.IsNullOrWhiteSpace(vorname) ? $"{gruss}," : $"{gruss} {vorname.Trim()},";
    }

    /// <param name="abmeldeSatz">Satz vor dem Abmelde-Link; wird nur mit <paramref name="abmeldeSeite"/> ausgegeben.</param>
    private static MailInhalt Baue(
        string betreff, string anrede, string[] absaetze, string knopfText, string? knopfUrl,
        string? fusszeile, string? abmeldeSatz, string? abmeldeSeite, bool englisch)
    {
        var mitAbmeldung = abmeldeSatz is not null && abmeldeSeite is not null;

        var text = new StringBuilder();
        text.AppendLine(anrede).AppendLine();
        foreach (var absatz in absaetze) text.AppendLine(absatz).AppendLine();
        if (knopfUrl is not null) text.AppendLine($"{knopfText}: {knopfUrl}").AppendLine();
        var team = englisch ? "The Dogity team" : "Dein Dogity-Team";
        text.AppendLine(team);
        if (fusszeile is not null || mitAbmeldung) text.AppendLine().AppendLine("--");
        if (fusszeile is not null) text.AppendLine(fusszeile);
        if (mitAbmeldung)
        {
            if (fusszeile is not null) text.AppendLine();
            text.AppendLine($"{abmeldeSatz} {abmeldeSeite}");
        }

        var html = new StringBuilder();
        html.Append($"<!doctype html><html lang=\"{(englisch ? "en" : "de")}\"><body style=\"margin:0;padding:0;background:#f7f8fa;\">");
        html.Append("<div style=\"max-width:520px;margin:0 auto;padding:24px 16px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1c1c1e;\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:20px;font-weight:700;color:#3d86f0;\">{Marke}</p>");
        html.Append("<div style=\"background:#ffffff;border-radius:12px;padding:24px;\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.5;\">{Kodiert(anrede)}</p>");
        foreach (var absatz in absaetze)
            html.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.5;\">{Kodiert(absatz)}</p>");
        if (knopfUrl is not null)
            html.Append(
                $"<p style=\"margin:24px 0;\"><a href=\"{Kodiert(knopfUrl)}\" style=\"display:inline-block;background:#3d86f0;color:#ffffff;text-decoration:none;font-weight:600;font-size:16px;padding:12px 20px;border-radius:10px;\">{Kodiert(knopfText)}</a></p>");
        html.Append($"<p style=\"margin:0;font-size:16px;line-height:1.5;\">{team}</p>");
        html.Append("</div>");
        if (fusszeile is not null)
            html.Append($"<p style=\"margin:16px 0 0;font-size:12px;line-height:1.5;color:#6b7280;\">{Kodiert(fusszeile)}</p>");
        if (mitAbmeldung)
            html.Append(
                $"<p style=\"margin:{(fusszeile is null ? "16px" : "8px")} 0 0;font-size:12px;line-height:1.5;color:#6b7280;\">{Kodiert(abmeldeSatz!)} " +
                $"<a href=\"{Kodiert(abmeldeSeite!)}\" style=\"color:#6b7280;text-decoration:underline;\">{(englisch ? "Unsubscribe here" : "Hier abmelden")}</a></p>");
        html.Append("</div></body></html>");

        return new MailInhalt(betreff, text.ToString(), html.ToString());
    }

    private static string Kodiert(string wert) => WebUtility.HtmlEncode(wert);
}

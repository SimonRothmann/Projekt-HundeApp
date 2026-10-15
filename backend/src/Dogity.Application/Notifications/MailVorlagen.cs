using System.Net;
using System.Text;

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
/// </summary>
public static class MailVorlagen
{
    private const string Marke = "Dogity";

    /// <remarks>
    /// Bewusst ohne Namen: Die Mail geht an eine bei der Registrierung noch
    /// unbestätigte Adresse, der Name ist frei wählbar.
    /// </remarks>
    public static MailInhalt Willkommen(string appUrl)
    {
        var anrede = Anrede("");
        var absaetze = new[]
        {
            "schön, dass du dabei bist! Dogity ist dein Trainingstagebuch für den Hundesport.",
            "Das kannst du als Erstes tun: einen Hund anlegen und dein erstes Training eintragen - oder einem Verein beitreten, wenn du in einer Gruppe trainierst.",
            "Fragen oder Fehler? Antworte einfach auf diese Mail.",
        };
        return Baue($"Willkommen bei {Marke}", anrede, absaetze, "Zu Dogity", appUrl, fusszeile: null);
    }

    public static MailInhalt PasswortZuruecksetzen(string vorname, string link)
    {
        var anrede = Anrede(vorname);
        var absaetze = new[]
        {
            "über den Knopf unten kannst du ein neues Passwort für dein Dogity-Konto festlegen.",
            "Wenn du das nicht angefordert hast, kannst du diese Mail ignorieren - dein Passwort bleibt dann, wie es ist.",
        };
        return Baue($"{Marke}: Passwort zurücksetzen", anrede, absaetze, "Passwort zurücksetzen", link, fusszeile: null);
    }

    /// <param name="nachricht">Der Text der Glockenmeldung.</param>
    /// <param name="link">Wohin der Knopf führt; null = kein Knopf (keine Basis-Adresse bekannt).</param>
    /// <param name="einstellungenUrl">Wohin der Hinweis zum Abschalten verweist (Profil → Funktionen).</param>
    public static MailInhalt WichtigeBenachrichtigung(string vorname, string nachricht, string? link, string? einstellungenUrl)
    {
        var anrede = Anrede(vorname);
        var fusszeile =
            "Du bekommst diese Mail, weil du bei Dogity wichtige Benachrichtigungen auch per E-Mail erhältst " +
            "(Einladungen und Entscheidungen über Beitrittsanfragen). Abschalten kannst du das unter Profil → Funktionen" +
            (einstellungenUrl is null ? "." : $": {einstellungenUrl}");
        return Baue(Betreff(nachricht), anrede, [nachricht], "In Dogity ansehen", link, fusszeile);
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

    private static string Anrede(string vorname) =>
        string.IsNullOrWhiteSpace(vorname) ? "Hallo," : $"Hallo {vorname.Trim()},";

    private static MailInhalt Baue(string betreff, string anrede, string[] absaetze, string knopfText, string? knopfUrl, string? fusszeile)
    {
        var text = new StringBuilder();
        text.AppendLine(anrede).AppendLine();
        foreach (var absatz in absaetze) text.AppendLine(absatz).AppendLine();
        if (knopfUrl is not null) text.AppendLine($"{knopfText}: {knopfUrl}").AppendLine();
        text.AppendLine("Dein Dogity-Team");
        if (fusszeile is not null) text.AppendLine().AppendLine("--").AppendLine(fusszeile);

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"de\"><body style=\"margin:0;padding:0;background:#f7f8fa;\">");
        html.Append("<div style=\"max-width:520px;margin:0 auto;padding:24px 16px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1c1c1e;\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:20px;font-weight:700;color:#3d86f0;\">{Marke}</p>");
        html.Append("<div style=\"background:#ffffff;border-radius:12px;padding:24px;\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.5;\">{Kodiert(anrede)}</p>");
        foreach (var absatz in absaetze)
            html.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.5;\">{Kodiert(absatz)}</p>");
        if (knopfUrl is not null)
            html.Append(
                $"<p style=\"margin:24px 0;\"><a href=\"{Kodiert(knopfUrl)}\" style=\"display:inline-block;background:#3d86f0;color:#ffffff;text-decoration:none;font-weight:600;font-size:16px;padding:12px 20px;border-radius:10px;\">{Kodiert(knopfText)}</a></p>");
        html.Append("<p style=\"margin:0;font-size:16px;line-height:1.5;\">Dein Dogity-Team</p>");
        html.Append("</div>");
        if (fusszeile is not null)
            html.Append($"<p style=\"margin:16px 0 0;font-size:12px;line-height:1.5;color:#6b7280;\">{Kodiert(fusszeile)}</p>");
        html.Append("</div></body></html>");

        return new MailInhalt(betreff, text.ToString(), html.ToString());
    }

    private static string Kodiert(string wert) => WebUtility.HtmlEncode(wert);
}

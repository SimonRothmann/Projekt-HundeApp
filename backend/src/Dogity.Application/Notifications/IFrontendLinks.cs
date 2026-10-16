namespace Dogity.Application.Notifications;

/// <summary>
/// Macht aus einem Pfad der App ("/clubs") eine Adresse, die auch außerhalb der
/// App funktioniert (Mail, Push) - Basis ist "Frontend:BaseUrl".
/// </summary>
public interface IFrontendLinks
{
    /// <summary>
    /// Absolute Adresse zu <paramref name="pfad"/>. Ist keine Basis
    /// konfiguriert, kommt der Pfad unverändert zurück (wie bisher beim
    /// Reset-Link). Pfade, die nicht mit genau einem "/" beginnen, werden nicht
    /// übernommen: Ein "//fremde.seite" würde sonst die Basis verlassen.
    /// </summary>
    string Absolut(string? pfad);

    /// <summary>Ob eine Basis-Adresse konfiguriert ist (nur dann sind Links in Mails sinnvoll).</summary>
    bool HatBasis { get; }

    /// <summary>
    /// Ob die öffentliche Adresse der API ("Api:BaseUrl") konfiguriert ist. Gebraucht
    /// wird sie nur für die Ein-Klick-Abmeldung im Mailprogramm (List-Unsubscribe):
    /// Die ruft die API direkt auf, nicht das Frontend.
    /// </summary>
    bool HatApiBasis { get; }

    /// <summary>
    /// Absolute Adresse eines API-Pfads ("/api/...") - null, wenn keine
    /// "Api:BaseUrl" konfiguriert ist oder der Pfad nicht mit genau einem "/"
    /// beginnt (gleiche Vorsicht wie bei <see cref="Absolut"/>).
    /// </summary>
    string? ApiAbsolut(string pfad);
}

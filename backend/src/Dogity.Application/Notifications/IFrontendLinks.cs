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
}

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Die Suche der Admin-Nutzerliste: E-Mail, Vorname, Nachname oder beide
/// Namen zusammen, ohne Rücksicht auf Groß-/Kleinschreibung.
///
/// Bewusst <c>ToLower().Contains</c> statt <c>EF.Functions.ILike</c>: ILike
/// gehört Npgsql und liefe im In-Memory-Provider der Tests nicht, die Suche
/// bliebe dort ungeprüft. <c>Contains</c> übersetzt Npgsql selbst (als
/// Teilstringsuche, nicht als LIKE-Muster) - % und _ des Nutzers bleiben
/// wörtlich, NutzerSucheTests hält das fest. Bei einer
/// Handvoll Konten ist der fehlende Index ohne Belang.
///
/// Ein Suchbegriff, der eine Guid ist, findet genau dieses Konto. Darüber
/// öffnet der alte Link aus einer Passwort-Reset-Benachrichtigung
/// (<c>?resetUser=&lt;id&gt;</c>) die richtige Person, auch wenn sie nicht auf
/// der ersten Seite der Liste steht - ohne eigenen Endpunkt für ein einzelnes
/// Konto.
///
/// Eigene Klasse (statt im UserLookupService), damit die Semantik ohne
/// UserManager testbar ist.
/// </summary>
public static class NutzerSuche
{
    public static IQueryable<ApplicationUser> Filtern(IQueryable<ApplicationUser> nutzer, string? suche)
    {
        var begriff = suche?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(begriff)) return nutzer;

        if (Guid.TryParse(begriff, out var id)) return nutzer.Where(u => u.Id == id);

        return nutzer.Where(u =>
            (u.Email != null && u.Email.ToLower().Contains(begriff)) ||
            u.FirstName.ToLower().Contains(begriff) ||
            u.LastName.ToLower().Contains(begriff) ||
            (u.FirstName + " " + u.LastName).ToLower().Contains(begriff));
    }
}

using Dogity.Application.Common;

namespace Dogity.Application.Account;

/// <summary>
/// Die Regel für Vor- und Nachname einer Person - eine Fassung für Registrierung und
/// Profil: beide Namen sind Pflicht (nach dem Trimmen nicht leer) und höchstens
/// <see cref="Textlaengen.PersonenName"/> Zeichen lang. Vorher prüfte die Registrierung nur
/// die Länge (ein Name aus Leerzeichen kam durch), das Profil beides.
/// </summary>
public static class Personennamen
{
    /// <summary>Die Fehlermeldung zu den Namen, sonst null.</summary>
    public static string? Pruefe(string? vorname, string? nachname)
    {
        if (string.IsNullOrWhiteSpace(vorname) || string.IsNullOrWhiteSpace(nachname))
            return "Vor- und Nachname sind erforderlich.";
        if (vorname.Trim().Length > Textlaengen.PersonenName || nachname.Trim().Length > Textlaengen.PersonenName)
        {
            var hoechstens = Textlaengen.PersonenName;
            return $"Vor- und Nachname dürfen höchstens {hoechstens} Zeichen lang sein.";
        }
        return null;
    }
}

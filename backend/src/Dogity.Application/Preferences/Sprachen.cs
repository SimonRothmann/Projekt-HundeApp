namespace Dogity.Application.Preferences;

/// <summary>
/// Die Sprachen, in denen der Server Benachrichtigungen, Push und Mails
/// schreiben kann - die einzige Stelle im Backend, die sie kennt. Spiegelt
/// <c>frontend/src/lib/i18n/sprachen.ts</c>; eine dritte Sprache braucht
/// hier einen Eintrag UND ein Wörterbuch (siehe <c>Uebersetzung</c>).
///
/// Gespeichert wird die Sprache in <see cref="Dogity.Domain.Preferences.UserPreference.Locale"/>.
/// Das ist nicht dasselbe wie "kann übersetzt werden": <c>UpdateLocaleAsync</c>
/// nimmt weiterhin jedes kurze Kürzel an (die Oberfläche entscheidet), hier
/// zählt nur, was der Server wirklich kann. Alles andere - auch null - heißt Deutsch.
/// </summary>
public static class Sprachen
{
    public const string Deutsch = "de";
    public const string Englisch = "en";

    public static readonly IReadOnlySet<string> Erlaubt = new HashSet<string> { Deutsch, Englisch };

    /// <summary>
    /// Das Kürzel in Kleinschreibung, wenn der Server die Sprache kennt; sonst null.
    /// Für Eingaben von außen (Registrierung): Unbekanntes wird nicht gespeichert.
    /// </summary>
    public static string? Normalisiere(string? wert)
    {
        var sprache = wert?.Trim().ToLowerInvariant();
        return sprache is not null && Erlaubt.Contains(sprache) ? sprache : null;
    }

    /// <summary>
    /// Ob für diese Person auf Englisch geschrieben wird. Ohne gespeicherte Sprache: nein.
    /// Nur das Sprachkürzel zählt ("en-GB" ist Englisch): Das Profil nimmt jedes
    /// kurze Kürzel an, und eine Region darf nicht still zu Deutsch führen.
    /// </summary>
    public static bool IstEnglisch(string? locale) => Normalisiere(locale?.Split('-')[0]) == Englisch;
}

using System.Text.RegularExpressions;
using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>Die geprüften, gesäuberten Angaben einer Anmeldung.</summary>
public record GroupRegistrationInput(
    string FirstName,
    string LastName,
    string DogName,
    string DogBreed,
    DateOnly DogBirthDate,
    string Phone,
    string? Notes);

/// <summary>
/// Das, woran zwei Anmeldungen als "dieselbe" erkannt werden: normalisierte
/// Telefonnummer und kleingeschriebener Rufname. Einmal gebildet, damit ein
/// Import mit vielen Zeilen nicht jede Nummer hundertfach neu normalisiert.
/// </summary>
public record AnmeldeSchluessel(string Telefon, string Rufname, DateTimeOffset RegisteredAt);

/// <summary>
/// Regeln für Anmeldungen zu einer Gruppe - eine Stelle für das öffentliche
/// Formular, das manuelle Anlegen, das Bearbeiten und den Import. Der Import
/// "prüft jede Zeile wie das Formular": Läge die Prüfung viermal vor, liefen
/// die Grenzen früher oder später auseinander.
///
/// Das Frontend spiegelt Telefon und Wurftag in lib/anmeldung.ts, damit man
/// Fehler vor dem Senden sieht; maßgeblich ist immer diese Prüfung.
/// </summary>
public static partial class GroupRegistrationRules
{
    /// <summary>
    /// So viele Anmeldungen darf eine Gruppe höchstens haben. Schützt vor einem
    /// vollgeschriebenen Formular (der Link steht auf einem Aushang), ist für
    /// eine Welpengruppe aber um ein Vielfaches zu hoch, um je zu stören.
    /// </summary>
    public const int MaxProGruppe = 500;

    /// <summary>So viele Zeilen nimmt ein Import auf einmal an.</summary>
    public const int MaxImportZeilen = 500;

    /// <summary>Wiederholte Formular-Anmeldung innerhalb dieser Frist zählt als doppelt.</summary>
    public static readonly TimeSpan DoppeltFrist = TimeSpan.FromDays(30);

    /// <summary>Ältestes zulässiges Alter des Hundes bei der Anmeldung, in Jahren.</summary>
    private const int MaxHundeAlterJahre = 2;

    private const int TelefonMinZeichen = 6;
    private const int TelefonMinZiffern = 6;

    // Ziffern, +, Leerzeichen, /, -, ( ) - so wie die Spezifikation es vorgibt.
    // Lose, weil Telefonnummern in Deutschland in jeder erdenklichen Schreibweise
    // aufgeschrieben werden; geprüft wird nur, dass es eine sein KANN.
    [GeneratedRegex(@"^[0-9+\s/()\-]+$")]
    private static partial Regex TelefonZeichen();

    /// <summary>
    /// Prüft die Eingabe und liefert die gesäuberte Fassung - oder eine
    /// Fehlermeldung. <paramref name="referenztag"/> ist der Tag der Anmeldung
    /// (heute beim Formular; der Tag aus dem Google-Zeitstempel beim Import und
    /// beim Bearbeiten alter Einträge): Gegen ihn gilt "nicht älter als zwei
    /// Jahre", sonst ließe sich eine Anmeldung von damals nie mehr korrigieren.
    /// </summary>
    public static (GroupRegistrationInput? Eingabe, string? Fehler) Pruefen(
        string? vorname, string? nachname, string? rufname, string? rasse,
        DateOnly? wurftag, string? telefon, string? notiz,
        DateOnly heute, DateOnly referenztag)
    {
        var vor = vorname?.Trim() ?? "";
        var nach = nachname?.Trim() ?? "";
        var hund = rufname?.Trim() ?? "";
        var art = rasse?.Trim() ?? "";
        var tel = telefon?.Trim() ?? "";
        var anmerkung = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim();

        if (vor.Length == 0) return (null, "Bitte den Vornamen angeben.");
        if (nach.Length == 0) return (null, "Bitte den Nachnamen angeben.");
        if (hund.Length == 0) return (null, "Bitte den Rufnamen des Hundes angeben.");
        if (art.Length == 0) return (null, "Bitte die Hunderasse angeben.");
        if (wurftag is null) return (null, "Bitte den Wurftag angeben.");
        if (tel.Length == 0) return (null, "Bitte die Telefonnummer angeben.");

        var zuLang = Textlaengen.ZuLang(vor, Textlaengen.Anmeldename, "Der Vorname")
            ?? Textlaengen.ZuLang(nach, Textlaengen.Anmeldename, "Der Nachname")
            ?? Textlaengen.ZuLang(hund, Textlaengen.Anmeldename, "Der Rufname")
            ?? Textlaengen.ZuLang(art, Textlaengen.Hunderasse, "Die Hunderasse")
            ?? Textlaengen.ZuLang(anmerkung, Textlaengen.AnmeldeNotiz, "Die Notiz");
        if (zuLang is not null) return (null, zuLang);

        if (wurftag.Value > heute) return (null, "Der Wurftag darf nicht in der Zukunft liegen.");
        if (wurftag.Value < referenztag.AddYears(-MaxHundeAlterJahre))
            return (null, "Der Wurftag liegt mehr als zwei Jahre zurück.");

        var telefonFehler = PruefeTelefon(tel);
        if (telefonFehler is not null) return (null, telefonFehler);

        return (new GroupRegistrationInput(vor, nach, hund, art, wurftag.Value, tel, anmerkung), null);
    }

    /// <summary>Fehlermeldung zur (bereits getrimmten) Telefonnummer, sonst null.</summary>
    public static string? PruefeTelefon(string telefon)
    {
        if (telefon.Length > Textlaengen.Telefon)
            return $"Die Telefonnummer ist zu lang (höchstens {Textlaengen.Telefon} Zeichen).";
        if (telefon.Length < TelefonMinZeichen || !TelefonZeichen().IsMatch(telefon) || Ziffern(telefon).Length < TelefonMinZiffern)
            return "Bitte eine gültige Telefonnummer angeben.";
        return null;
    }

    /// <summary>
    /// Die Telefonnummer so, dass gleiche Anschlüsse gleich aussehen: nur
    /// Ziffern, und die deutsche Vorwahl "+49"/"0049" wird zur führenden 0
    /// ("+49 721 123" und "0721/123" sind dieselbe Nummer).
    ///
    /// Die in Deutschland verbreitete Schreibweise "+49 (0) 721 …" trägt die
    /// Null der Inlandswahl mit, die bei internationaler Wahl wegfällt - sie
    /// zählt deshalb nicht mit, genauso wenig wie eine Null direkt nach "+49".
    /// </summary>
    public static string NormalisiereTelefon(string telefon)
    {
        var ohneKlammerNull = KlammerNull().Replace(telefon, "");
        var ziffern = Ziffern(ohneKlammerNull);
        var international = ohneKlammerNull.TrimStart().StartsWith('+') || ziffern.StartsWith("00", StringComparison.Ordinal);
        if (international)
        {
            if (ziffern.StartsWith("00", StringComparison.Ordinal)) ziffern = ziffern[2..];
            if (ziffern.StartsWith("49", StringComparison.Ordinal)) ziffern = "0" + ziffern[2..].TrimStart('0');
        }
        return ziffern;
    }

    [GeneratedRegex(@"\(\s*0\s*\)")]
    private static partial Regex KlammerNull();

    /// <summary>
    /// Ob es mit dieser Telefonnummer und diesem Rufnamen schon eine Anmeldung
    /// gibt (Groß-/Kleinschreibung egal). <paramref name="nurSeit"/> begrenzt auf
    /// jüngere Anmeldungen; null = alle (Import).
    /// </summary>
    public static bool IstDoppelt(IEnumerable<AnmeldeSchluessel> vorhandene, AnmeldeSchluessel neu, DateTimeOffset? nurSeit) =>
        vorhandene.Any(v =>
            (nurSeit is null || v.RegisteredAt >= nurSeit)
            && v.Telefon == neu.Telefon
            && v.Rufname == neu.Rufname);

    public static AnmeldeSchluessel SchluesselVon(string telefon, string rufname, DateTimeOffset registeredAt) =>
        new(NormalisiereTelefon(telefon), rufname.Trim().ToLowerInvariant(), registeredAt);

    private static string Ziffern(string text) => new(text.Where(char.IsAsciiDigit).ToArray());
}

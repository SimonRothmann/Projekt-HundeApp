namespace Dogity.Application.Account;

/// <summary>
/// Gemeinsame Festlegungen des Gastzugangs ("App ausprobieren", siehe docs/GASTZUGANG.md).
/// Gastkonten und Gast-Vereine sind von echten Konten und Vereinen vollständig getrennt.
/// </summary>
public static class Gastkonto
{
    /// <summary>Nach dieser Zeit ist ein Gastzugang samt allen Daten gelöscht.</summary>
    public static readonly TimeSpan Laufzeit = TimeSpan.FromHours(4);

    /// <summary>Obergrenze gleichzeitig aktiver Gäste, damit die Datenbank nicht volläuft.</summary>
    public const int MaxAktiv = 300;

    /// <summary>Adressen der Gastkonten (und ihrer erfundenen Mitglieder); es geht nie eine Mail dorthin.</summary>
    public const string MailDomain = "gast.invalid";

    /// <summary>Die Antwort, wenn eine Aktion Gast und echte Konten verbinden würde oder Gästen nicht offensteht.</summary>
    public const string NichtImDemoModus = "Im Demo-Modus nicht möglich.";

    public static bool IstGastAdresse(string? adresse) =>
        adresse is not null && adresse.EndsWith("@" + MailDomain, StringComparison.OrdinalIgnoreCase);
}

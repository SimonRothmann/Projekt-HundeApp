namespace Dogity.Application.Admin;

/// <summary>
/// Wie und was in der App genutzt wird - nur Zählungen, keine Personen (keine
/// Ids, Namen oder Adressen verlassen den Service), und nur aus Daten, die
/// ohnehin gespeichert sind: Es wird nichts zusätzlich erhoben und keine
/// Seitenaufrufe gezählt. Konten, die es nicht mehr gibt, und gelöschte
/// Datensätze zählen nicht mit. Stichtag aller "letzten n Tage" ist der
/// Abruf; Kalenderwochen und Uhrzeiten rechnen nach deutscher Zeit.
/// </summary>
public record AdminUsageDto(
    AdminUsageActivityDto Activity,
    IReadOnlyList<AdminFeatureUsageDto> Features,
    IReadOnlyList<AdminDisabledModuleDto> DisabledModules,
    AdminRetentionDto Retention,
    AdminUsageTimingDto Timing,
    IReadOnlyList<AdminSportUsageDto> Sports);

/// <summary>
/// Aktivität der Konten. "Aktiv" heißt: in einer der Funktionen etwas ANGELEGT
/// zu haben (Zeitpunkt des Anlegens, nicht das Datum eines nachgetragenen
/// Trainings) - Push einschalten und eine Anmeldung auf der Gruppenliste
/// (die legt jemand ohne Konto an) zählen dabei nicht.
///
/// "App geöffnet" kommt aus den Refresh-Tokens: Sie entstehen bei jeder
/// Anmeldung und jeder Erneuerung der Sitzung, also immer dann, wenn die App
/// mit abgelaufenem Zugangstoken (60 Minuten) Daten holt. Gezählt werden
/// verschiedene Konten mit einem Token, das im Zeitraum entstand. Die Zahl
/// UNTERTREIBT, wo jemand die App innerhalb der Gültigkeit des Zugangstokens
/// erneut öffnet (dann entsteht kein neuer Token) oder nur offline nutzt, und
/// zählt auch Erneuerungen, die ohne Zutun der Person laufen (die App auf
/// einem offenen Tab holt nach Ablauf Daten nach). Ein Hinweis auf die Nutzung,
/// kein Zähler für Öffnungen.
/// </summary>
/// <param name="TotalAccounts">Alle Konten, die es gibt.</param>
/// <param name="OpenedLast7Days">Konten, die die App in den letzten 7 Tagen geöffnet haben (siehe oben).</param>
/// <param name="OpenedLast30Days">Dasselbe für 30 Tage.</param>
/// <param name="ActiveLast7Days">Konten, die in den letzten 7 Tagen etwas angelegt haben.</param>
/// <param name="ActiveLast30Days">Dasselbe für 30 Tage.</param>
/// <param name="ActiveLast90Days">Dasselbe für 90 Tage.</param>
/// <param name="Weeks">Die letzten 12 Kalenderwochen (Montag bis Sonntag, deutsche Zeit), die älteste zuerst; die letzte ist die laufende.</param>
public record AdminUsageActivityDto(
    int TotalAccounts,
    int OpenedLast7Days,
    int OpenedLast30Days,
    int ActiveLast7Days,
    int ActiveLast30Days,
    int ActiveLast90Days,
    IReadOnlyList<AdminUsageWeekDto> Weeks);

/// <param name="WeekStart">Der Montag der Woche.</param>
/// <param name="ActiveAccounts">Verschiedene Konten, die in der Woche etwas angelegt haben.</param>
/// <param name="Entries">Angelegte Trainings, Fährten und Abläufe in der Woche.</param>
public record AdminUsageWeekDto(DateOnly WeekStart, int ActiveAccounts, int Entries);

/// <summary>
/// Eine Funktion der App. <paramref name="Key"/> ist stabil (englisch, kebab);
/// die Oberfläche übersetzt ihn in einen Namen.
/// </summary>
/// <param name="AccountsLast30Days">Verschiedene Konten mit einem Eintrag in den letzten 30 Tagen.</param>
/// <param name="EntriesLast30Days">Einträge in den letzten 30 Tagen; null, wo ein Eintrag nichts aussagt (Push: dort zählen nur Konten).</param>
/// <param name="AccountsEver">Verschiedene Konten mit mindestens einem (nicht gelöschten) Eintrag - bei Push: mit einem Gerät, das noch angemeldet ist.</param>
/// <param name="LastUsed">Der Tag des letzten Eintrags (deutsche Zeit); null, wenn die Funktion nie genutzt wurde.</param>
public record AdminFeatureUsageDto(
    string Key,
    int AccountsLast30Days,
    int? EntriesLast30Days,
    int AccountsEver,
    DateOnly? LastUsed);

/// <param name="Key">Modul-Schlüssel wie in den Einstellungen (siehe Modules).</param>
/// <param name="Accounts">Konten, die dieses Modul abgeschaltet haben.</param>
public record AdminDisabledModuleDto(string Key, int Accounts);

/// <summary>
/// Bindung. "Aktiv" und "angelegt" wie bei <see cref="AdminUsageActivityDto"/>.
/// </summary>
/// <param name="AccountsOlderThan30Days">Konten, die vor mehr als 30 Tagen angelegt wurden.</param>
/// <param name="OlderActiveLast30Days">Davon in den letzten 30 Tagen aktiv.</param>
/// <param name="NeverCreatedWithoutDog">Konten, die nie etwas angelegt haben und (aktuell) keinen Hund haben.</param>
/// <param name="NeverCreatedWithDog">Konten, die nie etwas angelegt haben, aber einen Hund haben.</param>
public record AdminRetentionDto(
    int AccountsOlderThan30Days,
    int OlderActiveLast30Days,
    int NeverCreatedWithoutDog,
    int NeverCreatedWithDog);

/// <summary>
/// Wann eingetragen wird: Trainings, Fährten und Abläufe der letzten 90 Tage
/// nach dem Zeitpunkt des Anlegens in deutscher Zeit.
/// </summary>
/// <param name="ByWeekday">Sieben Zahlen, Montag bis Sonntag.</param>
/// <param name="ByHour">24 Zahlen, Stunde 0 bis 23.</param>
public record AdminUsageTimingDto(IReadOnlyList<int> ByWeekday, IReadOnlyList<int> ByHour);

/// <summary>
/// Eine Sportart. Der Name kommt wie sonst im Katalog in beiden Sprachen; die
/// Oberfläche wählt (siehe <c>katalogName</c>).
/// </summary>
/// <param name="Trainings">Trainings der letzten 90 Tage mit mindestens einer Übung aus dieser Sportart (ein Training mit Übungen aus zwei Sportarten zählt bei beiden; Freitext-Übungen haben keine Sportart).</param>
/// <param name="Dogs">Hunde, die diese Sportart ausdrücklich gewählt haben (Hunde ohne eigene Auswahl folgen den Sportarten ihrer Besitzer:innen und sind hier nicht mitgezählt).</param>
public record AdminSportUsageDto(string Name, string? NameEn, int Trainings, int Dogs);

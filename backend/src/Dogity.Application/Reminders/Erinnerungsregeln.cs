using System.Globalization;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Domain.Notifications;

namespace Dogity.Application.Reminders;

/// <summary>Was eine Person zu einem Termin geantwortet hat. "Offen" ist keine Absage.</summary>
public enum TerminAntwort
{
    Offen,
    Zugesagt,
    Abgesagt
}

/// <summary>
/// Die Kennung einer versendeten Erinnerung - das, was <see cref="SentReminder"/>
/// mit dem eindeutigen Index festhält. <paramref name="Tag"/> ist
/// <see cref="SentReminder.KeinTag"/>, wenn die Erinnerung je Bezug nur einmal kommt.
/// </summary>
public readonly record struct ErinnerungsSchluessel(ReminderType Typ, Guid UserId, Guid BezugId, DateOnly Tag);

/// <summary>Eine Erinnerung, die jetzt zu versenden ist: Kennung, Text-Vorlage und Ziel in der App.</summary>
public sealed record FaelligeErinnerung(ErinnerungsSchluessel Schluessel, Meldung Meldung, string Link);

/// <summary>Eine Person und ein Termin des Folgetags - eine Zeile je Mitglied der Gruppe.</summary>
public sealed record TerminAdressat(
    Guid TerminId, string Gruppe, DateTimeOffset Beginn, bool Abgesagt, Guid UserId, TerminAntwort Antwort, bool Erlaubt);

/// <summary>Eine gelegte Fährte samt dem, was für die Erinnerung nötig ist.</summary>
/// <param name="LegeEnde">Zeit des letzten automatischen Punkts (wie legeEnde im Frontend), null ohne Punkte.</param>
/// <param name="Abgelaufen">Ob schon ein Ablauf gespeichert ist.</param>
public sealed record FaehrteKandidat(
    Guid FaehrteId, Guid TrainingId, Guid HundId, string Hund, Guid UserId,
    DateTimeOffset? LegeEnde, bool Abgelaufen, bool HundPausiert, bool Erlaubt);

/// <summary>Eine Trainer:in mit ihrem selbst gewählten Rhythmus (Tage als Bitmaske, volle Stunde).</summary>
public sealed record TrainerKandidat(Guid UserId, bool Erlaubt, int Tage, int Stunde);

/// <summary>
/// Wann welche Erinnerung fällig ist - als reine Funktionen: Zeitpunkt und Daten
/// hinein, Liste fälliger Erinnerungen heraus, kein Datenbankzugriff. So lassen
/// sich Mitternacht, Ruhezeit und Zeitumstellung ohne Datenbank prüfen; der
/// <see cref="ErinnerungService"/> besorgt die Daten und versendet.
///
/// Alle Uhrzeiten sind Vereinszeit (<see cref="Vereinszeit"/>), egal wo der Server läuft.
/// </summary>
public static class Erinnerungsregeln
{
    /// <summary>
    /// Versand nur zwischen 08:00 und 21:59 Uhr. "Bis 21 Uhr" heißt hier: bis zum Ende
    /// der Stunde 21 - sonst käme die wählbare Uhrzeit 21:00 der Trainer:innen nie an,
    /// weil der Hintergrunddienst nur alle zehn Minuten schaut.
    /// </summary>
    public const int ErsteStunde = 8;
    public const int LetzteStunde = 21;

    /// <summary>Ab dieser Stunde am Vortag kommt die Terminerinnerung.</summary>
    public const int TerminAbStunde = 18;

    /// <summary>So lange nach dem Ende des Legens kommt die Fährten-Erinnerung frühestens.</summary>
    public static readonly TimeSpan FaehrteNach = TimeSpan.FromHours(3);

    /// <summary>
    /// Die Frist, in der eine Fährte noch abgelaufen werden kann - dieselbe wie
    /// ABLAUF_FRIST_STUNDEN in frontend/src/lib/faehrte.ts; danach wäre die Erinnerung sinnlos.
    /// </summary>
    public static readonly TimeSpan FaehrteFrist = TimeSpan.FromHours(24);

    /// <summary>
    /// Sportarten, bei denen eine Fährte gelegt wird - Codes wie FAEHRTEN_SPORTARTEN in
    /// frontend/src/lib/faehrte.ts (beim Ändern beide Stellen anfassen).
    /// </summary>
    public static readonly IReadOnlyList<string> FaehrtenSportCodes = ["FAERTE", "FPR", "IGP1", "IGP2", "IGP3", "GPR", "STOEPR"];

    public const int AlleTage = 127;
    public const int VorgabeTage = 64; // Sonntag
    public const int VorgabeStunde = 18;

    public static bool GueltigeTage(int tage) => tage is >= 1 and <= AlleTage;
    public static bool GueltigeStunde(int stunde) => stunde is >= ErsteStunde and <= LetzteStunde;

    /// <summary>Montag = 1, Dienstag = 2, ... Sonntag = 64 - die Reihenfolge der Woche in Deutschland.</summary>
    public static int TagesBit(DayOfWeek tag) => 1 << (((int)tag + 6) % 7);

    /// <summary>
    /// Ob bei diesen wirksamen Sportarten Fährte angeboten wird: ohne Auswahl immer, sonst nur
    /// mit einer Fährten-Sportart dabei (wie laeuftFaehrte im Frontend).
    /// </summary>
    public static bool FaehrteAngeboten(IReadOnlyCollection<Guid> sportIds, IReadOnlySet<Guid> faehrtenSportIds) =>
        sportIds.Count == 0 || sportIds.Any(faehrtenSportIds.Contains);

    public static bool ImVersandfenster(DateTimeOffset jetzt)
    {
        var stunde = Vereinszeit.Lokal(jetzt).Hour;
        return stunde is >= ErsteStunde and <= LetzteStunde;
    }

    /// <summary>
    /// Termin am Folgetag (in Vereinszeit), ab 18 Uhr. Mit Zusage heißt es "du hast
    /// zugesagt", ohne Antwort "kommst du?", nach einer Absage kommt nichts. Ein
    /// abgesagter Termin löst nichts aus (die Absage des Termins ging schon als Meldung raus).
    /// Der Merkzettel gilt je Person, Termin und Termintag: Wird ein Termin nach dem Versand
    /// auf einen anderen Tag verschoben, kommt vor dem neuen Tag erneut eine Erinnerung.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> TerminErinnerungen(
        DateTimeOffset jetzt, IEnumerable<TerminAdressat> adressaten, IReadOnlySet<ErinnerungsSchluessel> gesendet)
    {
        if (!ImVersandfenster(jetzt) || Vereinszeit.Lokal(jetzt).Hour < TerminAbStunde) return [];

        var folgetag = Vereinszeit.Tag(jetzt).AddDays(1);
        var ergebnis = new List<FaelligeErinnerung>();
        var schonDabei = new HashSet<ErinnerungsSchluessel>();

        foreach (var a in adressaten)
        {
            if (!a.Erlaubt || a.Abgesagt || a.Antwort == TerminAntwort.Abgesagt) continue;
            if (Vereinszeit.Tag(a.Beginn) != folgetag) continue;

            var schluessel = new ErinnerungsSchluessel(ReminderType.SessionDayBefore, a.UserId, a.TerminId, folgetag);
            if (gesendet.Contains(schluessel) || !schonDabei.Add(schluessel)) continue;

            var zeit = Vereinszeit.Lokal(a.Beginn).ToString("HH:mm", CultureInfo.InvariantCulture);
            var meldung = a.Antwort == TerminAntwort.Zugesagt
                ? Meldung.Von("Morgen {zeit}: {gruppe} - du hast zugesagt.", ("zeit", zeit), ("gruppe", a.Gruppe))
                : Meldung.Von("Morgen {zeit}: {gruppe} - kommst du?", ("zeit", zeit), ("gruppe", a.Gruppe));
            // Auf der Startseite stehen die Termine mit den Knöpfen zum Zu- und Absagen.
            ergebnis.Add(new FaelligeErinnerung(schluessel, meldung, "/dashboard"));
        }
        return ergebnis;
    }

    /// <summary>
    /// Eine gelegte, noch nicht abgelaufene Fährte, frühestens drei Stunden nach dem
    /// Ende des Legens und nur solange die 24-Stunden-Frist läuft. Fällt der Zeitpunkt in
    /// die Ruhezeit, kommt die Erinnerung am nächsten Morgen, sofern die Frist dann noch
    /// läuft - weil der Tick sie bei jedem Lauf neu prüft, gibt es dafür keine Sonderfall-Logik.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> FaehrtenErinnerungen(
        DateTimeOffset jetzt, IEnumerable<FaehrteKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet)
    {
        if (!ImVersandfenster(jetzt)) return [];

        var ergebnis = new List<FaelligeErinnerung>();
        foreach (var k in kandidaten)
        {
            if (!k.Erlaubt || k.Abgelaufen || k.HundPausiert || k.LegeEnde is not { } ende) continue;

            var vergangen = jetzt - ende;
            if (vergangen < FaehrteNach || vergangen > FaehrteFrist) continue;

            var schluessel = new ErinnerungsSchluessel(ReminderType.TrackNotWalked, k.UserId, k.FaehrteId, SentReminder.KeinTag);
            if (gesendet.Contains(schluessel)) continue;

            var fristEnde = Vereinszeit.Lokal(ende + FaehrteFrist);
            var meldung = Meldung.Von(
                "Die Fährte von {hund} ist noch nicht abgelaufen. Du kannst sie noch bis {datum}, {zeit} Uhr ablaufen.",
                ("hund", k.Hund),
                ("datum", DateOnly.FromDateTime(fristEnde)),
                ("zeit", fristEnde.ToString("HH:mm", CultureInfo.InvariantCulture)));
            ergebnis.Add(new FaelligeErinnerung(schluessel, meldung, $"/dogs/{k.HundId}?eintrag={k.TrainingId}"));
        }
        return ergebnis;
    }

    /// <summary>
    /// Wessen Zusammenfassung heute dran ist: an einem gewählten Wochentag, ab der gewählten
    /// Stunde, höchstens einmal je Tag. Schlüssel ist der Tag, nicht die Stunde - ändert jemand
    /// die Uhrzeit nach dem Versand, kommt nicht noch eine. Ob etwas zu melden ist, sagt erst
    /// <see cref="RueckmeldungsErinnerung"/>, wenn die Anzahl bekannt ist.
    /// </summary>
    public static IReadOnlyList<TrainerKandidat> FaelligeTrainer(
        DateTimeOffset jetzt, IEnumerable<TrainerKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet)
    {
        if (!ImVersandfenster(jetzt)) return [];

        var lokal = Vereinszeit.Lokal(jetzt);
        var heute = DateOnly.FromDateTime(lokal);
        var bit = TagesBit(lokal.DayOfWeek);

        return kandidaten
            .Where(k => k.Erlaubt)
            .Where(k => (Tage(k) & bit) != 0 && lokal.Hour >= Stunde(k))
            .Where(k => !gesendet.Contains(TrainerSchluessel(k.UserId, heute)))
            .ToList();
    }

    // Ungültige gespeicherte Werte (kann es nach der Prüfung im Service nicht geben) fallen auf die Vorgabe zurück.
    private static int Tage(TrainerKandidat k) => GueltigeTage(k.Tage) ? k.Tage : VorgabeTage;
    private static int Stunde(TrainerKandidat k) => GueltigeStunde(k.Stunde) ? k.Stunde : VorgabeStunde;

    public static ErinnerungsSchluessel TrainerSchluessel(Guid userId, DateOnly tag) =>
        new(ReminderType.TrainerFeedbackSummary, userId, Guid.Empty, tag);

    /// <summary>
    /// Die Zusammenfassung offener Rückmeldungen; null bei keinem offenen Training. Feedback
    /// ist freiwillig - der Satz stellt fest, was ansteht, und drängt nicht.
    /// </summary>
    public static FaelligeErinnerung? RueckmeldungsErinnerung(DateTimeOffset jetzt, Guid userId, int anzahl)
    {
        if (anzahl <= 0) return null;

        var meldung = anzahl == 1
            ? Meldung.Von("Ein Training wartet auf deine Rückmeldung.")
            : Meldung.Von("{n} Trainings warten auf deine Rückmeldung.", ("n", anzahl));
        return new FaelligeErinnerung(TrainerSchluessel(userId, Vereinszeit.Tag(jetzt)), meldung, "/trainer/bewerten");
    }
}

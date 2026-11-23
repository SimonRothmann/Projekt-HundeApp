using System.Globalization;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

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
/// <param name="Art">Fährte oder Schweißfährte (der Trail erinnert nicht).</param>
/// <param name="StehzeitStunden">Nur Schweißfährte: Mindest-Stehzeit des Leitfadens, ohne Angabe gelten 14 Stunden.</param>
public sealed record FaehrteKandidat(
    Guid FaehrteId, Guid TrainingId, Guid HundId, string Hund, Guid UserId,
    DateTimeOffset? LegeEnde, bool Abgelaufen, bool HundPausiert, bool Erlaubt,
    GpsTrackKind Art = GpsTrackKind.Faehrte, int? StehzeitStunden = null);

/// <summary>Eine Trainer:in mit ihrem selbst gewählten Rhythmus (Tage als Bitmaske, volle Stunde).</summary>
public sealed record TrainerKandidat(Guid UserId, bool Erlaubt, int Tage, int Stunde);

/// <summary>
/// Ein Gesundheitseintrag und eine Besitzer:in seines Hundes - eine Zeile je Person.
/// <paramref name="Aktuell"/>: der jüngste Eintrag je Hund, Art und Titel (nur der erinnert).
/// </summary>
public sealed record GesundheitsAdressat(
    Guid EintragId, Guid HundId, string Hund, DogHealthKind Art, string Titel, DateOnly Datum, TimeOnly? Uhrzeit,
    DateOnly? Faellig, bool Aktuell, Guid UserId, bool Erlaubt);

/// <summary>Ein offener Eintrag auf dem Merkzettel eines Hundes, soweit die Erinnerung ihn braucht.</summary>
public sealed record MerkzettelEintrag(
    Guid Id, string Text, TrainingNoteGoal Ziel, DateOnly? ZielDatum, DateTimeOffset Angelegt, TimeOnly? ZielUhrzeit = null);

/// <summary>
/// Eine Besitzer:in und ein Hund mit offenem Merkzettel - eine Zeile je Person und Hund - samt dem, woraus sich
/// das nächste Training dieser Person mit diesem Hund ergibt (<see cref="Trainingsrhythmus"/>).
/// </summary>
public sealed record MerkzettelKandidat(
    Guid UserId, Guid HundId, string Hund, bool Erlaubt, IReadOnlyList<MerkzettelEintrag> Eintraege,
    IReadOnlyList<TagebuchTraining> Tagebuch, IReadOnlyList<DateTimeOffset> Gruppentermine, GespeicherterRhythmus? Rhythmus);

/// <summary>Eine Person mit ihrem ersten Hund (früheste aktive Besitzzeile), der noch kein Training hat.</summary>
public sealed record ErstesTrainingKandidat(Guid UserId, Guid HundId, string Hund, DateTimeOffset ErsterHundAm, bool HundPausiert);

/// <summary>
/// Wann welche Erinnerung fällig ist - als reine Funktionen: Zeitpunkt und Daten
/// hinein, Liste fälliger Erinnerungen heraus, kein Datenbankzugriff. So lassen
/// sich Mitternacht, Ruhezeit und Zeitumstellung ohne Datenbank prüfen; der
/// <see cref="ErinnerungService"/> besorgt die Daten und versendet.
///
/// Uhrzeiten und Tage sind Ortszeit der Person, die erinnert wird (<see cref="Ortszeit"/>; ohne
/// gemeldete Zone die <see cref="Vereinszeit"/>), egal wo der Server läuft. Nur der Gruppentermin selbst
/// gehört dem Verein: Sein Tag und seine Uhrzeit bleiben Vereinszeit, die Ruhezeit gilt beim Empfänger.
/// Die Zone je Person kommt als <c>zoneVon</c> herein; ohne sie gilt für alle die Vereinszeit.
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

    /// <summary>So viele Tage vor Ablauf einer Frist (Impfung, Entwurmung ...) kommt die Vorab-Erinnerung.</summary>
    public const int GesundheitVorlaufTage = 14;

    /// <summary>So lange nach dem Ende des Legens kommt die Fährten-Erinnerung frühestens.</summary>
    public static readonly TimeSpan FaehrteNach = TimeSpan.FromHours(3);

    /// <summary>Schweißfährte ohne Angabe der Stehzeit: die Stehzeit der Übernachtfährte.</summary>
    public const int SchweissStehzeitVorgabeStunden = 14;

    /// <summary>So viele Stunden vor Fristende kommt die zweite Stufe der Schweißfährten-Erinnerung.</summary>
    public static readonly TimeSpan SchweissLetzteStufeVorFristende = TimeSpan.FromHours(6);

    /// <summary>
    /// Sportarten, bei denen eine Fährte gelegt wird - Codes wie FAEHRTEN_SPORTARTEN in
    /// frontend/src/lib/faehrte.ts (beim Ändern beide Stellen anfassen).
    /// </summary>
    public static readonly IReadOnlyList<string> FaehrtenSportCodes = ["FAERTE", "FPR", "IGP1", "IGP2", "IGP3", "GPR", "STOEPR", "MANTRAILING", "JAGD"];

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

    /// <summary>Ab dieser Stunde (Ortszeit) kommt der Anstoß zum ersten Training.</summary>
    public const int ErstesTrainingAbStunde = 18;

    /// <summary>Frühestens so viele Kalendertage nach dem ersten Hund kommt der Anstoß ...</summary>
    public const int ErstesTrainingNachTagen = 3;

    /// <summary>... und nur bis zu so vielen: Wer länger ohne Training ist, wird nicht mehr angestoßen. Muss unter der Aufbewahrung von SentReminder (60 Tage) bleiben, sonst käme er nach dem Aufräumen erneut.</summary>
    public const int ErstesTrainingBisTagen = 30;

    public static bool ImVersandfenster(DateTimeOffset jetzt, TimeZoneInfo? zone = null)
    {
        var stunde = Ortszeit.Lokal(jetzt, zone ?? Vereinszeit.Zone).Hour;
        return stunde is >= ErsteStunde and <= LetzteStunde;
    }

    private static TimeZoneInfo ZoneDer(Func<Guid, TimeZoneInfo>? zoneVon, Guid userId) => zoneVon?.Invoke(userId) ?? Vereinszeit.Zone;

    /// <summary>
    /// Termin am Folgetag (in Vereinszeit, der Termin gehört dem Verein), ab 18 Uhr Vereinszeit, nur in der Ruhezeit-freien Zeit des Empfängers (08-22 Uhr). Mit Zusage heißt es "du hast
    /// zugesagt", ohne Antwort "kommst du?", nach einer Absage kommt nichts. Ein
    /// abgesagter Termin löst nichts aus (die Absage des Termins ging schon als Meldung raus).
    /// Der Merkzettel gilt je Person, Termin und Termintag: Wird ein Termin nach dem Versand
    /// auf einen anderen Tag verschoben, kommt vor dem neuen Tag erneut eine Erinnerung.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> TerminErinnerungen(
        DateTimeOffset jetzt, IEnumerable<TerminAdressat> adressaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        if (Vereinszeit.Lokal(jetzt).Hour < TerminAbStunde) return [];

        var folgetag = Vereinszeit.Tag(jetzt).AddDays(1);
        var ergebnis = new List<FaelligeErinnerung>();
        var schonDabei = new HashSet<ErinnerungsSchluessel>();

        foreach (var a in adressaten)
        {
            if (!a.Erlaubt || a.Abgesagt || a.Antwort == TerminAntwort.Abgesagt) continue;
            if (!ImVersandfenster(jetzt, ZoneDer(zoneVon, a.UserId))) continue;
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
    /// Gesundheitseinträge der eigenen Hunde, je Besitzer:in. Zwei Stufen für eine Frist (Impfung
    /// gültig bis, Entwurmung nächste fällig): in den letzten <see cref="GesundheitVorlaufTage"/> Tagen
    /// davor und am Tag selbst, je einmal. "In den letzten 14 Tagen" und nicht "genau 14 Tage vorher":
    /// Wer einen Eintrag erst zehn Tage vor Ablauf anlegt, soll trotzdem noch erinnert werden - und
    /// ein Neustart des Dienstes an genau dem Tag verschluckt die Erinnerung nicht. Am Tag selbst
    /// kommt nur die zweite Stufe. Nach Ablauf kommt nichts mehr: Das Überfällige zeigt die App selbst.
    /// Es erinnert nur der jüngste Eintrag je Hund, Art und Titel: Eine erneuerte Impfung beendet
    /// die Erinnerung der alten.
    ///
    /// Ein Tierarzttermin wird am Vortag ab 18 Uhr gemeldet, wie ein Gruppentermin.
    ///
    /// Der Merkzettel gilt je Person, Eintrag, Stufe und Tag der Frist bzw. des Termins: Wird das
    /// Datum nachträglich geändert, kommt vor dem neuen Datum erneut eine Erinnerung.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> GesundheitsErinnerungen(
        DateTimeOffset jetzt, IEnumerable<GesundheitsAdressat> adressaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        var ergebnis = new List<FaelligeErinnerung>();
        var schonDabei = new HashSet<ErinnerungsSchluessel>();

        foreach (var a in adressaten)
        {
            if (!a.Erlaubt) continue;
            var zone = ZoneDer(zoneVon, a.UserId);
            if (!ImVersandfenster(jetzt, zone)) continue;

            var heute = Ortszeit.Tag(jetzt, zone);
            var vortagAb = Ortszeit.Lokal(jetzt, zone).Hour >= TerminAbStunde;
            var link = $"/dogs/{a.HundId}?tab=gesundheit";

            if (a.Art == DogHealthKind.VetAppointment)
            {
                if (!vortagAb || a.Datum != heute.AddDays(1)) continue;

                var terminSchluessel = new ErinnerungsSchluessel(ReminderType.HealthVetDayBefore, a.UserId, a.EintragId, a.Datum);
                if (gesendet.Contains(terminSchluessel) || !schonDabei.Add(terminSchluessel)) continue;

                var meldung = a.Uhrzeit is { } uhrzeit
                    ? Meldung.Von("Morgen {zeit}: Tierarzt mit {hund} ({titel}).",
                        ("zeit", uhrzeit.ToString("HH:mm", CultureInfo.InvariantCulture)), ("hund", a.Hund), ("titel", a.Titel))
                    : Meldung.Von("Morgen: Tierarzt mit {hund} ({titel}).", ("hund", a.Hund), ("titel", a.Titel));
                ergebnis.Add(new FaelligeErinnerung(terminSchluessel, meldung, link));
                continue;
            }

            if (!a.Aktuell || a.Faellig is not { } faellig) continue;
            var rest = faellig.DayNumber - heute.DayNumber;
            if (rest < 0 || rest > GesundheitVorlaufTage) continue;

            var heuteFaellig = rest == 0;
            var schluessel = new ErinnerungsSchluessel(
                heuteFaellig ? ReminderType.HealthDueToday : ReminderType.HealthDueAhead, a.UserId, a.EintragId, faellig);
            if (gesendet.Contains(schluessel) || !schonDabei.Add(schluessel)) continue;

            // Ganze Sätze je Fall: Eine Impfung ist "gültig bis", alles andere "fällig".
            var text = a.Art == DogHealthKind.Vaccination
                ? heuteFaellig
                    ? Meldung.Von("{hund}: Die Impfung {titel} läuft heute ab.", ("hund", a.Hund), ("titel", a.Titel))
                    : Meldung.Von("{hund}: Die Impfung {titel} ist bis {datum} gültig.", ("hund", a.Hund), ("titel", a.Titel), ("datum", faellig))
                : heuteFaellig
                    ? Meldung.Von("{hund}: {titel} ist heute fällig.", ("hund", a.Hund), ("titel", a.Titel))
                    : Meldung.Von("{hund}: {titel} ist am {datum} fällig.", ("hund", a.Hund), ("titel", a.Titel), ("datum", faellig));
            ergebnis.Add(new FaelligeErinnerung(schluessel, text, link));
        }
        return ergebnis;
    }

    /// <summary>
    /// Eine gelegte, noch nicht abgelaufene Fährte, frühestens drei Stunden nach dem
    /// Ende des Legens und nur solange die Frist ihrer Art läuft (<see cref="Tracking.Ablauffristen"/>).
    /// Fällt der Zeitpunkt in die Ruhezeit, kommt die Erinnerung am nächsten Morgen, sofern die
    /// Frist dann noch läuft - weil der Tick sie bei jedem Lauf neu prüft, gibt es dafür keine
    /// Sonderfall-Logik.
    ///
    /// Die Schweißfährte steht erst: Die Erinnerung kommt ab der Stehzeit des Leitfadens (ohne
    /// Angabe 14 Stunden) und, falls sie dann noch nicht gearbeitet ist, ein zweites Mal
    /// <see cref="SchweissLetzteStufeVorFristende"/> vor dem Fristende. Beide Stufen haben einen
    /// eigenen Schlüssel (<see cref="SentReminder.ZweiteStufe"/>); fällt die zweite Stufe in einen
    /// Lauf, in dem die erste noch nie kam, kommt nur die zweite - nicht zwei Meldungen auf einmal.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> FaehrtenErinnerungen(
        DateTimeOffset jetzt, IEnumerable<FaehrteKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        var ergebnis = new List<FaelligeErinnerung>();
        foreach (var k in kandidaten)
        {
            var zone = ZoneDer(zoneVon, k.UserId);
            if (!ImVersandfenster(jetzt, zone)) continue;
            if (!k.Erlaubt || k.Abgelaufen || k.HundPausiert || k.LegeEnde is not { } ende) continue;

            var frist = Tracking.Ablauffristen.Fuer(k.Art);
            var schweiss = k.Art == GpsTrackKind.Schweissfaehrte;
            var ab = schweiss ? TimeSpan.FromHours(k.StehzeitStunden ?? SchweissStehzeitVorgabeStunden) : FaehrteNach;
            var vergangen = jetzt - ende;
            if (vergangen < ab || vergangen > frist) continue;

            var zweiteStufe = schweiss && vergangen >= frist - SchweissLetzteStufeVorFristende;
            var schluessel = new ErinnerungsSchluessel(
                ReminderType.TrackNotWalked, k.UserId, k.FaehrteId, zweiteStufe ? SentReminder.ZweiteStufe : SentReminder.KeinTag);
            if (gesendet.Contains(schluessel)) continue;

            var fristEnde = Ortszeit.Lokal(ende + frist, zone);
            var datum = DateOnly.FromDateTime(fristEnde);
            var zeit = fristEnde.ToString("HH:mm", CultureInfo.InvariantCulture);
            var meldung = !schweiss
                ? Meldung.Von(
                    "Die Fährte von {hund} ist noch nicht abgelaufen. Du kannst sie noch bis {datum}, {zeit} Uhr ablaufen.",
                    ("hund", k.Hund), ("datum", datum), ("zeit", zeit))
                : zweiteStufe
                    ? Meldung.Von(
                        "Die Schweißfährte von {hund} kann nur noch bis {datum}, {zeit} Uhr gearbeitet werden.",
                        ("hund", k.Hund), ("datum", datum), ("zeit", zeit))
                    : Meldung.Von(
                        "Die Schweißfährte von {hund} hat ihre Stehzeit erreicht. Du kannst sie noch bis {datum}, {zeit} Uhr arbeiten.",
                        ("hund", k.Hund), ("datum", datum), ("zeit", zeit));
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
        DateTimeOffset jetzt, IEnumerable<TrainerKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        return kandidaten
            .Where(k => k.Erlaubt)
            .Where(k =>
            {
                var zone = ZoneDer(zoneVon, k.UserId);
                if (!ImVersandfenster(jetzt, zone)) return false;

                var lokal = Ortszeit.Lokal(jetzt, zone);
                var heute = DateOnly.FromDateTime(lokal);
                return (Tage(k) & TagesBit(lokal.DayOfWeek)) != 0 && lokal.Hour >= Stunde(k)
                    && !gesendet.Contains(TrainerSchluessel(k.UserId, heute));
            })
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
    public static FaelligeErinnerung? RueckmeldungsErinnerung(DateTimeOffset jetzt, Guid userId, int anzahl, TimeZoneInfo? zone = null)
    {
        if (anzahl <= 0) return null;

        var meldung = anzahl == 1
            ? Meldung.Von("Ein Training wartet auf deine Rückmeldung.")
            : Meldung.Von("{n} Trainings warten auf deine Rückmeldung.", ("n", anzahl));
        return new FaelligeErinnerung(TrainerSchluessel(userId, Ortszeit.Tag(jetzt, zone ?? Vereinszeit.Zone)), meldung, "/trainer/bewerten");
    }

    /// <summary>So viele Zeichen vom ersten Merkzettel-Eintrag stehen in der Erinnerung; der Rest käme nicht in die Glocke und nicht auf den Sperrbildschirm.</summary>
    private const int MerkzettelTextZeichen = 100;

    /// <summary>
    /// Der Merkzettel vor dem Training: je Person und Hund EINE Erinnerung am Trainingstag, die alle offenen
    /// Einträge bündelt, deren Ziel auf diesen Tag fällt (<see cref="Trainingsrhythmus.ZielDesEintrags"/>).
    /// Sie kommt zwei Stunden vor dem Training, ohne bekannte Uhrzeit um 09:00 Uhr, nie vor 08:00 Uhr und
    /// nicht mehr, wenn das Training begonnen hat. Die Kennung enthält den Trainingstag: Unerledigte Einträge
    /// werden am nächsten Training erneut erinnert, am selben Tag nie zweimal.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> MerkzettelErinnerungen(
        DateTimeOffset jetzt, IEnumerable<MerkzettelKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        var ergebnis = new List<FaelligeErinnerung>();
        var schonDabei = new HashSet<ErinnerungsSchluessel>();

        foreach (var k in kandidaten)
        {
            if (!k.Erlaubt || k.Eintraege.Count == 0) continue;

            var zone = ZoneDer(zoneVon, k.UserId);
            if (!ImVersandfenster(jetzt, zone)) continue;
            var lokal = Ortszeit.Lokal(jetzt, zone);
            var heute = DateOnly.FromDateTime(lokal);

            var schluessel = new ErinnerungsSchluessel(ReminderType.TrainingNote, k.UserId, k.HundId, heute);
            if (gesendet.Contains(schluessel) || !schonDabei.Add(schluessel)) continue;

            var heutige = new List<(MerkzettelEintrag Eintrag, TrainingsTermin Ziel)>();
            foreach (var e in k.Eintraege.OrderBy(e => e.Angelegt))
            {
                var ziel = Trainingsrhythmus.ZielDesEintrags(
                    e.Ziel, e.ZielDatum, e.Angelegt, jetzt, k.Tagebuch, k.Gruppentermine, k.Rhythmus, erinnerung: true, e.ZielUhrzeit, zone);
                if (ziel is not { } termin || termin.Tag != heute) continue;
                heutige.Add((e, termin));
            }
            if (heutige.Count == 0) continue;

            // Gebündelt gilt die früheste bekannte Uhrzeit; ohne eine bei allen bleibt es beim Trainingstag.
            var uhrzeit = heutige.Select(h => h.Ziel.Uhrzeit).Where(u => u is not null).Min();
            var tag = new TrainingsTermin(heute, uhrzeit, TrainingsQuelle.Unbekannt);
            if (lokal < Trainingsrhythmus.ErinnerungsZeitpunkt(tag)) continue;
            if (tag.Beginn is { } beginn && lokal >= beginn) continue;

            var erster = Kuerzen(heutige[0].Eintrag.Text, MerkzettelTextZeichen);
            var weitere = heutige.Count - 1;
            ergebnis.Add(new FaelligeErinnerung(schluessel, MerkzettelMeldung(k.Hund, uhrzeit, erster, weitere), $"/dogs/{k.HundId}?tab=plan"));
        }
        return ergebnis;
    }

    /// <summary>
    /// Der einmalige Anstoß zum ersten Training: ab dem dritten Kalendertag nach dem ersten Hund (längstens 30 Tage),
    /// ab 18 Uhr Ortszeit der Person und nur im Versandfenster (08-22 Uhr); nicht bei Trainingspause des Hundes.
    /// Die Kennung ist je Konto fest (kein Bezug, kein Tag): Auch ein späterer Hund löst nichts erneut aus.
    /// </summary>
    public static IReadOnlyList<FaelligeErinnerung> ErstesTrainingErinnerungen(
        DateTimeOffset jetzt, IEnumerable<ErstesTrainingKandidat> kandidaten, IReadOnlySet<ErinnerungsSchluessel> gesendet,
        Func<Guid, TimeZoneInfo>? zoneVon = null)
    {
        var ergebnis = new List<FaelligeErinnerung>();
        foreach (var k in kandidaten)
        {
            var zone = ZoneDer(zoneVon, k.UserId);
            if (!ImVersandfenster(jetzt, zone) || Ortszeit.Lokal(jetzt, zone).Hour < ErstesTrainingAbStunde) continue;
            if (k.HundPausiert) continue;

            var tage = Ortszeit.Tag(jetzt, zone).DayNumber - Ortszeit.Tag(k.ErsterHundAm, zone).DayNumber;
            if (tage is < ErstesTrainingNachTagen or > ErstesTrainingBisTagen) continue;

            var schluessel = new ErinnerungsSchluessel(ReminderType.FirstTraining, k.UserId, Guid.Empty, SentReminder.KeinTag);
            if (gesendet.Contains(schluessel)) continue;

            ergebnis.Add(new FaelligeErinnerung(
                schluessel,
                Meldung.Von("Zeit fürs erste Training mit {hund}? Eintragen dauert nur ein paar Sekunden – die Übungen sind schon vorgeschlagen.", ("hund", k.Hund)),
                "/dashboard"));
        }
        return ergebnis;
    }

    /// <summary>Die Merkzettel-Erinnerung zum Ausprobieren: derselbe Satz wie am Trainingstag, ohne Uhrzeit, aus allen offenen Einträgen.</summary>
    public static Meldung MerkzettelProbe(string hund, IReadOnlyList<string> texte) =>
        MerkzettelMeldung(hund, null, Kuerzen(texte[0], MerkzettelTextZeichen), texte.Count - 1);

    // Ganze Sätze je Fall (mit und ohne Uhrzeit, mit und ohne weitere Einträge): Die Wortstellung ist je Sprache anders.
    private static Meldung MerkzettelMeldung(string hund, TimeOnly? uhrzeit, string text, int weitere)
    {
        if (uhrzeit is { } u)
        {
            var zeit = u.ToString("HH:mm", CultureInfo.InvariantCulture);
            return weitere == 0
                ? Meldung.Von("Heute {zeit} - gemerkt für {hund}: {text}", ("zeit", zeit), ("hund", hund), ("text", text))
                : Meldung.Von("Heute {zeit} - gemerkt für {hund}: {text} · +{n} weitere", ("zeit", zeit), ("hund", hund), ("text", text), ("n", weitere));
        }
        return weitere == 0
            ? Meldung.Von("Heute Training - gemerkt für {hund}: {text}", ("hund", hund), ("text", text))
            : Meldung.Von("Heute Training - gemerkt für {hund}: {text} · +{n} weitere", ("hund", hund), ("text", text), ("n", weitere));
    }

    private static string Kuerzen(string text, int zeichen)
    {
        // Zeilenumbrüche aus dem Diktat würden die Glocke und den Push zerreißen.
        var einzeilig = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (einzeilig.Length <= zeichen) return einzeilig;

        var ende = zeichen - 1;
        // Nicht mitten in einem Emoji (zwei Zeichen) abschneiden.
        if (char.IsHighSurrogate(einzeilig[ende - 1])) ende--;
        return einzeilig[..ende].TrimEnd() + "…";
    }
}

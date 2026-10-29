using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Preferences;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Admin;

/// <summary>
/// Wie und was in der App genutzt wird (Admin, siehe <see cref="AdminUsageDto"/>).
///
/// Nur Zahlen, nie Personen: Es wird über Konto-Ids gerechnet, und keine davon
/// verlässt diese Klasse. Nur Daten, die ohnehin gespeichert sind - kein
/// Seitenaufruf-Zähler, keine neue Spalte.
///
/// Aufbau: Jede Funktion ist eine Abfrage, die ihre Einträge auf (Konto oder
/// Hund, Zeitpunkt) abbildet. Je Funktion laufen zwei Abfragen: eine
/// zusammenfassende über ALLE Zeiten (SQL: GroupBy/Max - "Konten jemals" und
/// "zuletzt genutzt") und eine für die letzten 90 Tage, die die Zeitstempel
/// holt. Alles Zeitliche (30 Tage, Kalenderwochen, Wochentag, Stunde) rechnet
/// danach im Speicher: Die Menge ist klein (90 Tage), und die Berliner Zeit
/// lässt sich so für InMemory und Postgres gleich ausdrücken - eine
/// Zeitzonen-Umrechnung in der Abfrage verhält sich je Datenbank anders.
///
/// Konten, die es nicht mehr gibt (Altlast aus der Zeit vor der vollständigen
/// Kontolöschung), zählen nicht: Zugeordnet wird nur zu Konten aus
/// <see cref="IUserLookupService.ListAllUserIdsAsync"/>. Gelöschte Datensätze
/// blenden die globalen Filter aus.
/// </summary>
public class AdminUsageService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    IRefreshTokenService refreshTokens,
    TimeProvider timeProvider) : IAdminUsageService
{
    private const int Wochen = 12;

    /// <summary>
    /// Ein Eintrag einer Funktion: Wem er gehört, steht entweder direkt im
    /// Konto oder - wo die Tabelle keine Person kennt - am Hund (dann gelten
    /// seine Besitzer:innen). Genau eines von beiden ist gesetzt.
    /// </summary>
    private sealed class Zeile
    {
        public Guid? UserId { get; set; }
        public Guid? DogId { get; set; }
        public DateTimeOffset At { get; set; }
    }

    /// <param name="ZaehltAlsAktiv">Ob ein Eintrag das Konto "aktiv" macht. Nicht bei Push (eine Einstellung, kein Eintrag) und Gruppenanmeldungen (legt jemand ohne Konto an).</param>
    /// <param name="NurKonten">Einträge sagen nichts aus (Push) - es werden nur Konten gezählt.</param>
    /// <param name="Wann">Fließt in "Wann wird eingetragen" ein.</param>
    private sealed record Funktion(string Key, IQueryable<Zeile> Zeilen, bool ZaehltAlsAktiv = true, bool NurKonten = false, bool Wann = false);

    /// <summary>Ein Eintrag der letzten 90 Tage, schon den (noch vorhandenen) Konten zugeordnet.</summary>
    private sealed record Eintrag(DateTimeOffset At, Guid[] Konten);

    /// <summary>
    /// Die Funktionen mit der Zuordnung je Funktion - "angelegt von" ist
    /// immer der Zeitpunkt des Anlegens, wo nicht anders vermerkt.
    /// </summary>
    private IReadOnlyList<Funktion> Funktionen() =>
    [
        // Training: die Person im Trainingseintrag.
        new("training",
            db.TrainingSessions.Select(s => new Zeile { UserId = s.UserId, DogId = null, At = s.CreatedAt }),
            Wann: true),

        // Übungen eingetragen: jede Übung trägt beim Eintragen Pflicht-Sterne,
        // die Zeile zeigt also, wie viele Übungen je Training erfasst werden.
        // Gehört zum Training der Person.
        new("exercise-rating",
            from e in db.TrainingExercises
            join s in db.TrainingSessions on e.TrainingSessionId equals s.Id
            select new Zeile { UserId = s.UserId, DogId = null, At = e.CreatedAt }),

        // Fährte legen: die Fährte hat kein Personenfeld, sie hängt an einem
        // Training und gehört dessen Person (wie bei den aktiven Konten).
        new("track-laid",
            from t in db.GpsTracks
            join s in db.TrainingSessions on t.TrainingSessionId equals s.Id
            select new Zeile { UserId = s.UserId, DogId = null, At = t.CreatedAt },
            Wann: true),

        // Fährte ablaufen: der Ablauf hängt an der Fährte, diese am Training.
        new("track-walk",
            from r in db.GpsWalkRuns
            join t in db.GpsTracks on r.TrackId equals t.Id
            join s in db.TrainingSessions on t.TrainingSessionId equals s.Id
            select new Zeile { UserId = s.UserId, DogId = null, At = r.CreatedAt },
            Wann: true),

        // Prüfungsziel: kein Personenfeld, es gehört den Besitzer:innen des
        // Hundes (der Hund darf nicht gelöscht sein - die Ziele bleiben sonst stehen).
        new("goal",
            db.Goals
                .Where(g => db.Dogs.Any(d => d.Id == g.DogId))
                .Select(g => new Zeile { UserId = null, DogId = g.DogId, At = g.CreatedAt })),

        // Trainingsplan: entsteht mit dem Ziel (automatisch), wird dort also
        // mitgezählt; Zuordnung wie beim Ziel, über den Hund des Ziels.
        new("plan",
            from p in db.TrainingPlans
            join g in db.Goals on p.GoalId equals g.Id
            where db.Dogs.Any(d => d.Id == g.DogId)
            select new Zeile { UserId = null, DogId = g.DogId, At = p.CreatedAt }),

        // Pausen und Gesundheit: "angelegt von", wo es gesetzt ist (Altbestand
        // ohne die Angabe: die Besitzer:innen des Hundes).
        new("pause",
            db.DogPauses.Select(p => new Zeile
            {
                UserId = p.CreatedByUserId,
                DogId = p.CreatedByUserId == null ? p.DogId : (Guid?)null,
                At = p.CreatedAt,
            })),
        new("health",
            db.DogHealthRecords.Select(h => new Zeile
            {
                UserId = h.CreatedByUserId,
                DogId = h.CreatedByUserId == null ? h.DogId : (Guid?)null,
                At = h.CreatedAt,
            })),

        // Sachkunde lernen: je Konto und Frage eine Zeile (die 90-Tage-Menge
        // wächst mit Lernenden x Fragen - bei der heutigen Größe bewusst hingenommen), die beim ersten
        // Beantworten entsteht. Wer täglich wiederholt, legt keine neuen an -
        // deshalb zählt der Zeitpunkt der letzten Antwort (wie "zuletzt
        // genutzt" gemeint ist); die Zeile selbst ist ein Eintrag je Frage.
        new("sachkunde-learn",
            db.QuizMasteries.Select(m => new Zeile { UserId = m.UserId, DogId = null, At = m.LastAnsweredAt ?? m.CreatedAt })),

        // Übungsprüfung: eine Zeile je Abgabe.
        new("sachkunde-exam",
            db.QuizExamAttempts.Select(a => new Zeile { UserId = a.UserId, DogId = null, At = a.CreatedAt })),

        // Hundefoto: kein Personenfeld, also die Besitzer:innen des Hundes.
        new("dog-photo",
            db.DogImages.Select(i => new Zeile { UserId = null, DogId = i.DogId, At = i.CreatedAt })),

        // Mitbesitz: angenommene Einladungen (der globale Filter lässt nur
        // aktive Besitzzeilen durch). Die Zeile entsteht mit der Einladung, die
        // Annahme setzt UpdatedAt - das ist der Moment, in dem die Person
        // etwas tut, nicht CreatedAt. Konto: die eingeladene Person.
        new("co-owner",
            db.DogOwners
                .Where(o => o.InvitedByUserId != null && db.Dogs.Any(d => d.Id == o.DogId))
                .Select(o => new Zeile { UserId = o.UserId, DogId = null, At = o.UpdatedAt ?? o.CreatedAt })),

        // Trainer-Betreuung: die Trainer:in übernimmt den Hund (GroupService),
        // nicht die Familie.
        new("trainer-care",
            db.TrainerAssignments.Select(a => new Zeile { UserId = a.TrainerId, DogId = null, At = a.CreatedAt })),

        // Rückmeldung zu einem Training (ergänzt: für Trainer:innen die zentrale
        // Tätigkeit, steht sonst nirgends). Zeitpunkt der Rückmeldung; eine
        // geänderte Rückmeldung rückt ihn vor.
        new("trainer-feedback",
            db.TrainingSessions
                .Where(s => s.FeedbackAt != null && s.FeedbackByTrainerId != null)
                .Select(s => new Zeile { UserId = s.FeedbackByTrainerId, DogId = null, At = s.FeedbackAt!.Value })),

        // Gruppentraining-Termine (Trainer-Seite): die anlegende Person. Altbestand
        // ohne die Angabe ist keiner Person zuzuordnen und zählt nicht.
        new("group-session",
            db.GroupTrainingSessions
                .Where(s => s.CreatedByUserId != null)
                .Select(s => new Zeile { UserId = s.CreatedByUserId, DogId = null, At = s.CreatedAt })),

        // Zu-/Absagen: je Konto und Termin eine Zeile; der Zeitpunkt der
        // (letzten) Antwort, weil eine geänderte Antwort dieselbe Zeile ändert.
        new("session-response",
            db.GroupTrainingSessionResponses.Select(r => new Zeile { UserId = r.UserId, DogId = null, At = r.RespondedAt })),

        // Gruppenanmeldungen: füllt jemand OHNE Konto aus (Formular) oder die
        // Trainer:in trägt sie ein. Kein Personenfeld - zugeordnet wird die
        // Trainer:in der Gruppe, die die Anmeldung bekommt. Macht sie nicht
        // "aktiv": Sie hat nichts angelegt.
        new("group-registration",
            db.GroupRegistrations.Select(r => new Zeile { UserId = r.Group!.TrainerId, DogId = null, At = r.CreatedAt }),
            ZaehltAlsAktiv: false),

        // Vereinsbeitritt: genehmigte Mitgliedschaft; der Zeitpunkt der
        // Genehmigung (offene Anfragen und Einladungen sind noch kein Beitritt),
        // ohne ihn der der Anfrage. Macht nicht "aktiv": Die Genehmigung löst
        // meist eine Trainer:in aus, nicht das Mitglied.
        new("club-membership",
            db.ClubMemberships
                .Where(m => m.Status == ClubMembershipStatus.Approved && db.Clubs.Any(c => c.Id == m.ClubId))
                .Select(m => new Zeile { UserId = m.UserId, DogId = null, At = m.DecidedAt ?? m.RequestedAt }),
            ZaehltAlsAktiv: false),

        // Push: Geräte mit eingeschalteten Mitteilungen. Beim Ausschalten wird
        // die Zeile gelöscht - "jemals" heißt hier also: noch eingeschaltet.
        // Eine Einstellung, kein Eintrag.
        new("push",
            db.PushSubscriptions.Select(p => new Zeile { UserId = p.UserId, DogId = null, At = p.CreatedAt }),
            ZaehltAlsAktiv: false, NurKonten: true),
    ];

    public async Task<Result<AdminUsageDto>> GetAsync(CancellationToken ct = default)
    {
        var jetzt = timeProvider.GetUtcNow();
        var seit7 = jetzt.AddDays(-7);
        var seit30 = jetzt.AddDays(-30);
        var seit90 = jetzt.AddDays(-90);

        var konten = (await userLookup.ListAllUserIdsAsync(ct)).ToHashSet();
        var neu = (await userLookup.ListUserIdsCreatedSinceAsync(seit30, ct)).ToHashSet();
        var besitzer = (await db.DogOwners.Select(o => new { o.DogId, o.UserId }).ToListAsync(ct))
            .ToLookup(o => o.DogId, o => o.UserId);

        Guid[] Zugeordnet(Guid? userId, Guid? dogId) =>
            (userId is { } u ? new[] { u } : dogId is { } d ? besitzer[d].ToArray() : Array.Empty<Guid>())
                .Where(konten.Contains)
                .Distinct()
                .ToArray();

        var features = new List<AdminFeatureUsageDto>();
        var aktivJemals = new HashSet<Guid>();
        var aktiveEintraege = new List<Eintrag>();
        var wannEintraege = new List<Eintrag>();

        foreach (var funktion in Funktionen())
        {
            var gesamt = await funktion.Zeilen
                .GroupBy(z => new { z.UserId, z.DogId })
                .Select(g => new { g.Key.UserId, g.Key.DogId, Zuletzt = g.Max(z => z.At) })
                .ToListAsync(ct);
            var kontenJemals = new HashSet<Guid>();
            DateTimeOffset? zuletzt = null;
            foreach (var gruppe in gesamt)
            {
                var zugeordnet = Zugeordnet(gruppe.UserId, gruppe.DogId);
                if (zugeordnet.Length == 0) continue;
                kontenJemals.UnionWith(zugeordnet);
                if (zuletzt is null || gruppe.Zuletzt > zuletzt) zuletzt = gruppe.Zuletzt;
            }

            var eintraege = (await funktion.Zeilen.Where(z => z.At >= seit90).ToListAsync(ct))
                .Select(z => new Eintrag(z.At, Zugeordnet(z.UserId, z.DogId)))
                .Where(e => e.Konten.Length > 0)
                .ToList();
            var dieses30 = eintraege.Where(e => e.At >= seit30).ToList();

            features.Add(new AdminFeatureUsageDto(
                funktion.Key,
                dieses30.SelectMany(e => e.Konten).Distinct().Count(),
                funktion.NurKonten ? null : dieses30.Count,
                kontenJemals.Count,
                zuletzt is { } z ? Vereinszeit.Tag(z) : null));

            if (funktion.ZaehltAlsAktiv)
            {
                aktivJemals.UnionWith(kontenJemals);
                aktiveEintraege.AddRange(eintraege);
            }
            if (funktion.Wann) wannEintraege.AddRange(eintraege);
        }

        var aktiv7 = AktiveKonten(aktiveEintraege, seit7);
        var aktiv30 = AktiveKonten(aktiveEintraege, seit30);
        var aktiv90 = AktiveKonten(aktiveEintraege, seit90);

        var activity = new AdminUsageActivityDto(
            konten.Count,
            await refreshTokens.CountAccountsWithTokenSinceAsync(seit7, ct),
            await refreshTokens.CountAccountsWithTokenSinceAsync(seit30, ct),
            aktiv7.Count,
            aktiv30.Count,
            aktiv90.Count,
            Verlauf(aktiveEintraege, wannEintraege, Vereinszeit.Tag(jetzt)));

        return Result<AdminUsageDto>.Success(new AdminUsageDto(
            activity,
            features,
            await AbgeschalteteModuleAsync(konten, ct),
            await BindungAsync(konten, neu, aktiv30, aktivJemals, ct),
            Wann(wannEintraege),
            await SportartenAsync(konten, besitzer, seit90, ct)));
    }

    private static HashSet<Guid> AktiveKonten(List<Eintrag> eintraege, DateTimeOffset seit) =>
        eintraege.Where(e => e.At >= seit).SelectMany(e => e.Konten).ToHashSet();

    /// <summary>
    /// Die letzten 12 Kalenderwochen (Montag bis Sonntag in deutscher Zeit),
    /// die älteste zuerst. Der Tag eines Eintrags ist der deutsche Kalendertag:
    /// Ein Eintrag um 00:30 Uhr am Montag liegt in UTC noch am Sonntag der Woche davor.
    /// Die älteste Woche beginnt höchstens 83 Tage zurück und liegt damit
    /// innerhalb der 90 Tage, die geladen werden.
    /// Konten zählen aus allen Funktionen, die "aktiv" machen; die Einträge nur
    /// aus Training, Fährte und Ablauf - sonst zählte ein Training mit fünf
    /// Übungen sechsfach und Ziel plus Plan doppelt.
    /// </summary>
    private static List<AdminUsageWeekDto> Verlauf(List<Eintrag> aktive, List<Eintrag> eintraege, DateOnly heute)
    {
        var dieseWoche = heute.AddDays(-(((int)heute.DayOfWeek + 6) % 7));
        var erste = dieseWoche.AddDays(-7 * (Wochen - 1));

        // Index der Woche oder -1, wenn der Eintrag außerhalb der 12 Wochen liegt.
        int Woche(Eintrag e)
        {
            var tage = Vereinszeit.Tag(e.At).DayNumber - erste.DayNumber;
            return tage < 0 || tage / 7 >= Wochen ? -1 : tage / 7;
        }

        var konten = Enumerable.Range(0, Wochen).Select(_ => new HashSet<Guid>()).ToArray();
        foreach (var eintrag in aktive)
            if (Woche(eintrag) is var w and >= 0) konten[w].UnionWith(eintrag.Konten);
        var anzahl = new int[Wochen];
        foreach (var eintrag in eintraege)
            if (Woche(eintrag) is var w and >= 0) anzahl[w]++;

        return Enumerable.Range(0, Wochen)
            .Select(i => new AdminUsageWeekDto(erste.AddDays(7 * i), konten[i].Count, anzahl[i]))
            .ToList();
    }

    private static AdminUsageTimingDto Wann(List<Eintrag> eintraege)
    {
        var wochentag = new int[7];
        var stunde = new int[24];
        foreach (var eintrag in eintraege)
        {
            var lokal = Vereinszeit.Lokal(eintrag.At);
            wochentag[((int)lokal.DayOfWeek + 6) % 7]++;
            stunde[lokal.Hour]++;
        }
        return new AdminUsageTimingDto(wochentag, stunde);
    }

    /// <summary>
    /// Je Modul die Zahl der Konten, die es abgeschaltet haben. Alle bekannten
    /// Module stehen drin, auch mit 0 - gerade "wird nicht abgeschaltet" ist
    /// eine Auskunft.
    /// </summary>
    private async Task<List<AdminDisabledModuleDto>> AbgeschalteteModuleAsync(HashSet<Guid> konten, CancellationToken ct)
    {
        var abgeschaltet = (await db.UserDisabledModules
                .Select(m => new { m.ModuleKey, m.UserPreference!.UserId })
                .Distinct()
                .ToListAsync(ct))
            .Where(m => konten.Contains(m.UserId))
            .GroupBy(m => m.ModuleKey)
            .ToDictionary(g => g.Key, g => g.Count());

        return Modules.Bekannt
            .Concat(abgeschaltet.Keys.Except(Modules.Bekannt).Order())
            .Select(key => new AdminDisabledModuleDto(key, abgeschaltet.GetValueOrDefault(key)))
            .ToList();
    }

    /// <summary>
    /// "Aktuell einen Hund" wie bei den neuen Konten (aktive Besitzzeile zu
    /// einem Hund, der nicht gelöscht ist) - wer seinen Hund löschte, zählt als ohne Hund.
    /// </summary>
    private async Task<AdminRetentionDto> BindungAsync(
        HashSet<Guid> konten, HashSet<Guid> neu, HashSet<Guid> aktiv30, HashSet<Guid> aktivJemals, CancellationToken ct)
    {
        var aelter = konten.Where(id => !neu.Contains(id)).ToList();
        var mitHund = (await db.DogOwners
                .Where(o => db.Dogs.Any(d => d.Id == o.DogId))
                .Select(o => o.UserId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();
        var nieAngelegt = konten.Where(id => !aktivJemals.Contains(id)).ToList();

        return new AdminRetentionDto(
            aelter.Count,
            aelter.Count(aktiv30.Contains),
            nieAngelegt.Count(id => !mitHund.Contains(id)),
            nieAngelegt.Count(mitHund.Contains));
    }

    /// <summary>
    /// Trainings tragen keine Sportart, nur ihre Übungen: Ein Training zählt
    /// für jede Sportart, aus der es mindestens eine Katalog-Übung enthält.
    /// Alle Sportarten stehen in der Liste, auch ungenutzte - sie zeigen, was
    /// bisher gar nicht verwendet wird.
    /// </summary>
    private async Task<List<AdminSportUsageDto>> SportartenAsync(
        HashSet<Guid> konten, ILookup<Guid, Guid> besitzer, DateTimeOffset seit90, CancellationToken ct)
    {
        var sportarten = await db.Sports.Select(s => new { s.Id, s.Name, s.NameEn }).ToListAsync(ct);

        var trainings = (await (
                from te in db.TrainingExercises
                join s in db.TrainingSessions on te.TrainingSessionId equals s.Id
                join e in db.Exercises on te.ExerciseId equals (Guid?)e.Id
                where s.CreatedAt >= seit90 && e.SportId != null
                select new { SessionId = s.Id, s.UserId, SportId = e.SportId!.Value })
            .Distinct()
            .ToListAsync(ct))
            .Where(t => konten.Contains(t.UserId))
            .GroupBy(t => t.SportId)
            .ToDictionary(g => g.Key, g => g.Count());

        // DogSportSelection hat keinen Besitzbezug; ein Hund zählt, wenn
        // mindestens eine seiner Besitzer:innen noch ein Konto hat.
        var hunde = (await db.DogSportSelections
                .Select(s => new { s.SportId, s.DogId })
                .Distinct()
                .ToListAsync(ct))
            .Where(s => besitzer[s.DogId].Any(konten.Contains))
            .GroupBy(s => s.SportId)
            .ToDictionary(g => g.Key, g => g.Count());

        return sportarten
            .Select(s => new AdminSportUsageDto(s.Name, s.NameEn, trainings.GetValueOrDefault(s.Id), hunde.GetValueOrDefault(s.Id)))
            .OrderByDescending(s => s.Trainings)
            .ThenByDescending(s => s.Dogs)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Account;

/// <inheritdoc />
public class AccountDataService(IApplicationDbContext db, IUserLookupService userLookup) : IAccountDataService
{
    private const string ExportHinweis =
        "Auskunft nach Art. 15 und 20 DSGVO. Diese Datei enthält alles, was Dogity zu deinem Konto " +
        "gespeichert hat. Sie ist für dich bestimmt - wer sie hat, kennt deine Trainingsorte und " +
        "deine aufgezeichneten Wege.";

    public async Task<Result<AccountExportDto>> ExportAsync(Guid userId, CancellationToken ct = default)
    {
        var stammdaten = (await userLookup.FindByIdsAsync([userId], ct)).GetValueOrDefault(userId);
        if (stammdaten is null) return Result<AccountExportDto>.NotFound("Konto nicht gefunden.");

        // Hunde, an denen dieser Mensch beteiligt ist - auch die geteilten.
        var besitz = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => new { o.DogId, o.Role, o.CreatedAt })
            .AsNoTracking()
            .ToListAsync(ct);
        var hundeIds = besitz.Select(b => b.DogId).Distinct().ToList();

        var hunde = await db.Dogs.Where(d => hundeIds.Contains(d.Id)).AsNoTracking().ToListAsync(ct);
        var mitBild = await db.DogImages.Where(i => hundeIds.Contains(i.DogId)).Select(i => i.DogId).ToListAsync(ct);

        // Trainings: die selbst erfassten. Einheiten, die ein Mitbesitzer zum
        // selben Hund eingetragen hat, sind dessen Eintrag und stehen in
        // dessen Auskunft.
        var einheiten = await db.TrainingSessions.Where(s => s.UserId == userId).AsNoTracking().ToListAsync(ct);
        var einheitIds = einheiten.Select(s => s.Id).ToList();
        var trainingsuebungen = await db.TrainingExercises
            .Where(x => einheitIds.Contains(x.TrainingSessionId))
            .AsNoTracking()
            .ToListAsync(ct);

        var ziele = await db.Goals.Where(g => hundeIds.Contains(g.DogId)).AsNoTracking().ToListAsync(ct);
        var zielIds = ziele.Select(z => z.Id).ToList();
        var plaene = await db.TrainingPlans.Where(p => zielIds.Contains(p.GoalId)).AsNoTracking().ToListAsync(ct);
        var planIds = plaene.Select(p => p.Id).ToList();
        var planUebungen = await db.TrainingPlanItems
            .Where(i => planIds.Contains(i.TrainingPlanId))
            .AsNoTracking()
            .ToListAsync(ct);

        // Übungsnamen für beides in einem Zug - sonst stünden im Export nur
        // Kennnummern, und "verständliche Form" wäre verfehlt.
        var uebungIds = trainingsuebungen.Where(x => x.ExerciseId is not null).Select(x => x.ExerciseId!.Value)
            .Concat(planUebungen.Where(i => i.ExerciseId is not null).Select(i => i.ExerciseId!.Value))
            .Distinct()
            .ToList();
        var uebungsnamen = await db.Exercises
            .Where(x => uebungIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var faehrten = await db.GpsTracks
            .Where(t => einheitIds.Contains(t.TrainingSessionId))
            .AsNoTracking()
            .ToListAsync(ct);
        var faehrtenIds = faehrten.Select(t => t.Id).ToList();
        var faehrtenpunkte = await db.GpsPoints.Where(p => faehrtenIds.Contains(p.TrackId)).AsNoTracking().ToListAsync(ct);
        var ablaeufe = await db.GpsWalkRuns.Where(w => faehrtenIds.Contains(w.TrackId)).AsNoTracking().ToListAsync(ct);
        var ablaufIds = ablaeufe.Select(w => w.Id).ToList();
        var ablaufpunkte = await db.GpsWalkPoints.Where(p => ablaufIds.Contains(p.WalkRunId)).AsNoTracking().ToListAsync(ct);

        var mitgliedschaften = await db.ClubMemberships.Where(m => m.UserId == userId).AsNoTracking().ToListAsync(ct);
        var vereinstrainer = await db.ClubTrainers.Where(t => t.UserId == userId).Select(t => t.ClubId).ToListAsync(ct);
        var vereinIds = mitgliedschaften.Select(m => m.ClubId).Concat(vereinstrainer).Distinct().ToList();
        var vereinsnamen = await db.Clubs.Where(c => vereinIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var gruppenmitglied = await db.GroupMembers.Where(m => m.UserId == userId).AsNoTracking().ToListAsync(ct);
        var gruppenIds = gruppenmitglied.Select(m => m.GroupId).Distinct().ToList();
        var gruppennamen = await db.Groups.Where(g => gruppenIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var lernstand = await db.QuizMasteries.Where(m => m.UserId == userId).AsNoTracking().ToListAsync(ct);
        var frageIds = lernstand.Select(m => m.QuestionId).Distinct().ToList();
        var frageZuKatalog = await db.QuizQuestions
            .Where(q => frageIds.Contains(q.Id))
            .Select(q => new { q.Id, q.CatalogId })
            .ToListAsync(ct);
        var katalogIds = frageZuKatalog.Select(f => f.CatalogId).Distinct().ToList();
        var katalognamen = await db.QuizCatalogs
            .Where(c => katalogIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var benachrichtigungen = await db.Notifications.Where(n => n.UserId == userId).AsNoTracking().ToListAsync(ct);

        var einstellung = await db.UserPreferences
            .Where(p => p.UserId == userId)
            .Include(p => p.DisabledModules)
            .Include(p => p.Sports)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        string UebungsName(Guid? id, string? freitext) =>
            id is not null && uebungsnamen.TryGetValue(id.Value, out var name) ? name : freitext ?? "(gelöschte Übung)";

        var export = new AccountExportDto(
            ExportHinweis,
            DateTimeOffset.UtcNow,
            // AvatarUrl füllt die API-Schicht: Sie liegt am Identity-Konto,
            // auf das die Application-Schicht bewusst keinen Zugriff hat.
            new KontoExportDto(userId, stammdaten.FirstName, stammdaten.LastName, stammdaten.Email, null),
            einstellung is null
                ? null
                : new EinstellungenExportDto(
                    einstellung.Locale,
                    einstellung.Country,
                    einstellung.FontScale,
                    einstellung.DisabledModules.Select(m => m.ModuleKey).ToList(),
                    einstellung.Sports.Select(s => s.SportId).ToList()),
            hunde.Select(hund =>
            {
                var rolle = besitz.First(b => b.DogId == hund.Id);
                return new HundExportDto(
                    hund.Id,
                    hund.Name,
                    hund.Breed,
                    hund.Birthday,
                    hund.Gender == DogGender.Male ? "Rüde" : "Hündin",
                    hund.Notes,
                    hund.ImageUrl,
                    mitBild.Contains(hund.Id),
                    hund.ArchivedAt,
                    rolle.Role.ToString(),
                    rolle.CreatedAt);
            }).ToList(),
            einheiten.Select(einheit => new TrainingExportDto(
                einheit.Id,
                einheit.DogId,
                einheit.Date,
                einheit.StartTime,
                einheit.DurationMinutes,
                einheit.LocationName,
                einheit.Latitude,
                einheit.Longitude,
                einheit.TemperatureC,
                einheit.RelativeHumidity,
                einheit.WindSpeedKmh,
                einheit.Notes,
                einheit.TrainerFeedback,
                trainingsuebungen
                    .Where(x => x.TrainingSessionId == einheit.Id)
                    .Select(x => new TrainingsuebungExportDto(
                        UebungsName(x.ExerciseId, x.FreeTextLabel),
                        x.Rating,
                        x.Success,
                        x.Notes,
                        x.TrainerRating,
                        x.TrainerNote))
                    .ToList()))
                .OrderByDescending(t => t.Datum)
                .ToList(),
            faehrten.Select(faehrte => new FaehrteExportDto(
                faehrte.Id,
                faehrte.TrainingSessionId,
                einheiten.First(e => e.Id == faehrte.TrainingSessionId).Date,
                faehrte.LengthMeters,
                faehrte.AgeMinutes,
                faehrte.Surface,
                faehrte.Comment,
                faehrtenpunkte
                    .Where(p => p.TrackId == faehrte.Id)
                    .OrderBy(p => p.Timestamp)
                    .Select(p => new GpsPunktExportDto(
                        p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.PointType.ToString(), p.Label))
                    .ToList(),
                ablaeufe
                    .Where(w => w.TrackId == faehrte.Id)
                    .Select(w => new AblaufExportDto(
                        w.Id,
                        w.LengthMeters,
                        w.AvgDeviationMeters,
                        w.OnTrackPercent,
                        w.ArticlesFound,
                        // Ablaufpunkte kennen keine Art und keine Beschriftung -
                        // sie sind der reine Weg des Gespanns, aufgezeichnet im
                        // Sekundentakt. Markierungen gibt es nur an der
                        // gelegten Fährte.
                        ablaufpunkte
                            .Where(p => p.WalkRunId == w.Id)
                            .OrderBy(p => p.Timestamp)
                            .Select(p => new GpsPunktExportDto(
                                p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, "Ablauf", null))
                            .ToList()))
                    .ToList()))
                .ToList(),
            ziele.Select(ziel =>
            {
                var plan = plaene.FirstOrDefault(p => p.GoalId == ziel.Id);
                return new ZielExportDto(
                    ziel.Id,
                    ziel.DogId,
                    ziel.TargetDate,
                    ziel.Status.ToString(),
                    ziel.Notes,
                    plan?.GeneratedAt,
                    plan is null
                        ? []
                        : planUebungen
                            .Where(i => i.TrainingPlanId == plan.Id)
                            .OrderBy(i => i.WeekNumber).ThenBy(i => i.DayIndex)
                            .Select(i => new PlanUebungExportDto(
                                i.WeekNumber,
                                i.DayIndex,
                                UebungsName(i.ExerciseId, i.FreeTextLabel),
                                i.RepetitionsTarget,
                                i.IsRestWeek))
                            .ToList());
            }).ToList(),
            vereinIds.Select(vereinId =>
            {
                var mitgliedschaft = mitgliedschaften.FirstOrDefault(m => m.ClubId == vereinId);
                return new VereinExportDto(
                    vereinId,
                    vereinsnamen.GetValueOrDefault(vereinId, "(gelöschter Verein)"),
                    mitgliedschaft?.Status.ToString() ?? "-",
                    mitgliedschaft?.RequestedAt ?? default,
                    mitgliedschaft?.DecidedAt,
                    vereinstrainer.Contains(vereinId));
            }).ToList(),
            gruppenmitglied.Select(m => new GruppeExportDto(
                m.GroupId,
                gruppennamen.GetValueOrDefault(m.GroupId, "(gelöschte Gruppe)"),
                m.Role.ToString(),
                m.Status.ToString(),
                m.JoinedAt)).ToList(),
            lernstand.Select(m => new SachkundeExportDto(
                m.QuestionId,
                katalognamen.GetValueOrDefault(
                    frageZuKatalog.FirstOrDefault(f => f.Id == m.QuestionId)?.CatalogId ?? Guid.Empty,
                    "(unbekannt)"),
                m.Box,
                m.CorrectCount,
                m.WrongCount,
                m.LastAnsweredAt)).ToList(),
            benachrichtigungen
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new BenachrichtigungExportDto(n.CreatedAt, n.Message, n.IsRead))
                .ToList());

        return Result<AccountExportDto>.Success(export);
    }

    public async Task<Result> PurgeAsync(Guid userId, CancellationToken ct = default)
    {
        var jetzt = DateTimeOffset.UtcNow;

        // Zwei Regeln gelten in dieser ganzen Methode:
        //
        // 1. IgnoreQueryFilters() überall. Weich gelöschte Zeilen sind nur
        //    ausgeblendet, nicht fort - ohne das bliebe ausgerechnet das
        //    stehen, was jemand schon einmal loswerden wollte.
        // 2. Echtes Löschen statt DeletedAt. Ein Soft-Delete lässt Namen,
        //    Notizen und Koordinaten in der Tabelle stehen; Art. 17 DSGVO
        //    verlangt aber, dass sie weg sind. Wie bei DogService.DeleteImage
        //    ist das hier der begründete Ausnahmefall von der Soft-Delete-
        //    Konvention aus AI_RULES.md.

        var meineBesitzzeilen = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => o.UserId == userId)
            .ToListAsync(ct);
        var meineHundeIds = meineBesitzzeilen.Select(o => o.DogId).Distinct().ToList();

        // Geteilte Hunde bleiben bei der anderen Person - mit ihrer ganzen
        // Historie. Nur meine Verknüpfung verschwindet.
        var nochAndereBesitzer = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => meineHundeIds.Contains(o.DogId) && o.UserId != userId && o.DeletedAt == null)
            .Select(o => o.DogId)
            .Distinct()
            .ToListAsync(ct);
        var alleinMeineHunde = meineHundeIds.Except(nochAndereBesitzer).ToList();

        var besitzzeilen = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => o.UserId == userId || alleinMeineHunde.Contains(o.DogId))
            .ToListAsync(ct);

        // Eigene Einheiten - und alle Einheiten der Hunde, die mit mir gehen.
        var einheiten = await db.TrainingSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == userId || alleinMeineHunde.Contains(s.DogId))
            .ToListAsync(ct);
        var einheitIds = einheiten.Select(s => s.Id).ToList();
        var trainingsuebungen = await db.TrainingExercises.IgnoreQueryFilters()
            .Where(x => einheitIds.Contains(x.TrainingSessionId)).ToListAsync(ct);

        var faehrten = await db.GpsTracks.IgnoreQueryFilters()
            .Where(t => einheitIds.Contains(t.TrainingSessionId)).ToListAsync(ct);
        var faehrtenIds = faehrten.Select(t => t.Id).ToList();
        var faehrtenpunkte = await db.GpsPoints.IgnoreQueryFilters()
            .Where(p => faehrtenIds.Contains(p.TrackId)).ToListAsync(ct);
        var ablaeufe = await db.GpsWalkRuns.IgnoreQueryFilters()
            .Where(w => faehrtenIds.Contains(w.TrackId)).ToListAsync(ct);
        var ablaufIds = ablaeufe.Select(w => w.Id).ToList();
        var ablaufpunkte = await db.GpsWalkPoints.IgnoreQueryFilters()
            .Where(p => ablaufIds.Contains(p.WalkRunId)).ToListAsync(ct);
        var stockungen = await db.GpsWalkStops.IgnoreQueryFilters()
            .Where(s => ablaufIds.Contains(s.WalkRunId)).ToListAsync(ct);

        var ziele = await db.Goals.IgnoreQueryFilters()
            .Where(g => alleinMeineHunde.Contains(g.DogId)).ToListAsync(ct);
        var zielIds = ziele.Select(z => z.Id).ToList();
        var plaene = await db.TrainingPlans.IgnoreQueryFilters()
            .Where(p => zielIds.Contains(p.GoalId)).ToListAsync(ct);
        var planIds = plaene.Select(p => p.Id).ToList();
        var planUebungen = await db.TrainingPlanItems.IgnoreQueryFilters()
            .Where(i => planIds.Contains(i.TrainingPlanId)).ToListAsync(ct);
        var planWochen = await db.TrainingPlanWeekConfigs.IgnoreQueryFilters()
            .Where(w => planIds.Contains(w.TrainingPlanId)).ToListAsync(ct);

        var bilder = await db.DogImages.IgnoreQueryFilters()
            .Where(i => alleinMeineHunde.Contains(i.DogId)).ToListAsync(ct);
        var hundesportarten = await db.DogSportSelections.IgnoreQueryFilters()
            .Where(s => alleinMeineHunde.Contains(s.DogId)).ToListAsync(ct);
        var koennen = await db.ExerciseMasteries.IgnoreQueryFilters()
            .Where(m => alleinMeineHunde.Contains(m.DogId)).ToListAsync(ct);
        var hunde = await db.Dogs.IgnoreQueryFilters()
            .Where(d => alleinMeineHunde.Contains(d.Id)).ToListAsync(ct);

        var einstellungen = await db.UserPreferences.IgnoreQueryFilters()
            .Where(p => p.UserId == userId).ToListAsync(ct);
        var einstellungIds = einstellungen.Select(p => p.Id).ToList();
        var module = await db.UserDisabledModules.IgnoreQueryFilters()
            .Where(m => einstellungIds.Contains(m.UserPreferenceId)).ToListAsync(ct);
        var sportarten = await db.UserSportSelections.IgnoreQueryFilters()
            .Where(s => einstellungIds.Contains(s.UserPreferenceId)).ToListAsync(ct);

        var lernstand = await db.QuizMasteries.IgnoreQueryFilters().Where(m => m.UserId == userId).ToListAsync(ct);
        var benachrichtigungen = await db.Notifications.IgnoreQueryFilters().Where(n => n.UserId == userId).ToListAsync(ct);

        var mitgliedschaften = await db.ClubMemberships.IgnoreQueryFilters().Where(m => m.UserId == userId).ToListAsync(ct);
        var vereinstrainer = await db.ClubTrainers.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync(ct);
        var gruppenmitglied = await db.GroupMembers.IgnoreQueryFilters().Where(m => m.UserId == userId).ToListAsync(ct);
        var gruppentrainer = await db.GroupTrainers.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync(ct);
        var zuweisungen = await db.TrainerAssignments.IgnoreQueryFilters()
            .Where(a => a.TrainerId == userId || a.MemberId == userId || meineHundeIds.Contains(a.DogId))
            .ToListAsync(ct);
        var termintrainer = await db.GroupTrainingSessionTrainers.IgnoreQueryFilters()
            .Where(t => t.UserId == userId).ToListAsync(ct);
        var vereinsanfragen = await db.ClubRegistrations.IgnoreQueryFilters()
            .Where(r => r.RequestedByUserId == userId).ToListAsync(ct);

        // Gruppen, die an dieser Person hängen: Gibt es eine zweite
        // Trainerin, übernimmt sie. Sonst wird die Gruppe geschlossen - ohne
        // Trainer:in gibt es niemanden mehr, der sie führt.
        foreach (var gruppe in await db.Groups.IgnoreQueryFilters().Where(g => g.TrainerId == userId).ToListAsync(ct))
        {
            var nachfolger = await db.GroupTrainers.IgnoreQueryFilters()
                .Where(t => t.GroupId == gruppe.Id && t.UserId != userId && t.DeletedAt == null)
                .Select(t => t.UserId)
                .FirstOrDefaultAsync(ct);

            if (nachfolger != Guid.Empty) gruppe.TrainerId = nachfolger;
            else gruppe.DeletedAt ??= jetzt;
        }

        // Was anderen gehört, bleibt - nur der Verweis auf diese Person geht.
        // Eine Trainer-Rückmeldung etwa ist Teil des Tagebuchs des Mitglieds;
        // gelöscht würde ihm etwas weggenommen, worauf er sich verlässt.
        foreach (var m in await db.ClubMemberships.IgnoreQueryFilters()
                     .Where(m => m.DecidedByUserId == userId).ToListAsync(ct))
            m.DecidedByUserId = null;
        foreach (var r in await db.ClubRegistrations.IgnoreQueryFilters()
                     .Where(r => r.DecidedByUserId == userId).ToListAsync(ct))
            r.DecidedByUserId = null;
        foreach (var e in await db.GroupTrainingExercises.IgnoreQueryFilters()
                     .Where(e => e.CreatedByUserId == userId).ToListAsync(ct))
            e.CreatedByUserId = null;
        foreach (var u in await db.GroupTrainingUnits.IgnoreQueryFilters()
                     .Where(u => u.CreatedByUserId == userId).ToListAsync(ct))
            u.CreatedByUserId = null;
        foreach (var s in await db.GroupTrainingSessions.IgnoreQueryFilters()
                     .Where(s => s.CreatedByUserId == userId).ToListAsync(ct))
            s.CreatedByUserId = null;
        foreach (var q in await db.QuizQuestions.IgnoreQueryFilters()
                     .Where(q => q.EditedByUserId == userId).ToListAsync(ct))
            q.EditedByUserId = null;
        foreach (var s in await db.TrainingSessions.IgnoreQueryFilters()
                     .Where(s => s.FeedbackByTrainerId == userId).ToListAsync(ct))
            s.FeedbackByTrainerId = null;
        foreach (var z in await db.Goals.IgnoreQueryFilters()
                     .Where(g => g.PlanManagedByTrainerId == userId).ToListAsync(ct))
            z.PlanManagedByTrainerId = null;

        // Von innen nach außen entfernen, damit kein Fremdschlüssel ins Leere
        // zeigt, während die Datenbank noch mitten im Vorgang ist.
        db.GpsWalkPoints.RemoveRange(ablaufpunkte);
        db.GpsWalkStops.RemoveRange(stockungen);
        db.GpsWalkRuns.RemoveRange(ablaeufe);
        db.GpsPoints.RemoveRange(faehrtenpunkte);
        db.GpsTracks.RemoveRange(faehrten);
        db.TrainingExercises.RemoveRange(trainingsuebungen);
        db.TrainingSessions.RemoveRange(einheiten);
        db.TrainingPlanItems.RemoveRange(planUebungen);
        db.TrainingPlanWeekConfigs.RemoveRange(planWochen);
        db.TrainingPlans.RemoveRange(plaene);
        db.Goals.RemoveRange(ziele);
        db.ExerciseMasteries.RemoveRange(koennen);
        db.DogSportSelections.RemoveRange(hundesportarten);
        db.DogImages.RemoveRange(bilder);
        db.DogOwners.RemoveRange(besitzzeilen);
        db.Dogs.RemoveRange(hunde);
        db.UserDisabledModules.RemoveRange(module);
        db.UserSportSelections.RemoveRange(sportarten);
        db.UserPreferences.RemoveRange(einstellungen);
        db.QuizMasteries.RemoveRange(lernstand);
        db.Notifications.RemoveRange(benachrichtigungen);
        db.TrainerAssignments.RemoveRange(zuweisungen);
        db.GroupTrainingSessionTrainers.RemoveRange(termintrainer);
        db.GroupTrainers.RemoveRange(gruppentrainer);
        db.GroupMembers.RemoveRange(gruppenmitglied);
        db.ClubTrainers.RemoveRange(vereinstrainer);
        db.ClubMemberships.RemoveRange(mitgliedschaften);
        db.ClubRegistrations.RemoveRange(vereinsanfragen);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

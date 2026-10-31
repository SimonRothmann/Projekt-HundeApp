using Dogity.Application.Account;
using Dogity.Application.Admin;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Learning;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// Koppelt die Tabellenlisten rund um ein Konto: Was <c>PurgeAsync</c> beim Löschen
/// entfernt, muss "Daten gelöschter Konten" (VerwaisteKontodaten) auch erkennen und
/// zählen - sonst bliebe ein Rest stehen, den niemand mehr findet (so fehlten die
/// Ergebnisse der Übungsprüfung). Zwei Wächter:
///
/// 1. Jede Spalte mit Konto-Bezug im Datenmodell ist hier einer Gruppe zugeordnet.
///    Kommt eine neue dazu, scheitert der Test und verlangt die Entscheidung -
///    und für "wird gelöscht" eine Zeile in der Verhaltensliste unten.
/// 2. Für jede gelöschte Tabelle: Eine Zeile eines nicht mehr vorhandenen Kontos
///    wird als verwaist erkannt, gezählt, von der Bereinigung entfernt - und danach
///    ist das Konto nicht mehr verwaist.
/// </summary>
public class KontoTabellenKopplungTests
{
    /// <summary>Spalten, die PurgeAsync zeilenweise entfernt (bzw. die Gruppe schließt) und die VerwaisteKontodaten erfasst.</summary>
    private static readonly string[] Geloescht =
    [
        "ClubMembership.UserId", "ClubRegistration.RequestedByUserId", "ClubTrainer.UserId", "Group.TrainerId",
        "GroupMember.UserId", "GroupTrainer.UserId", "GroupTrainingSessionResponse.UserId", "GroupTrainingSessionTrainer.UserId",
        "TrainerAssignment.MemberId", "TrainerAssignment.TrainerId", "DogOwner.UserId", "QuizExamAttempt.UserId",
        "QuizMastery.UserId", "Notification.UserId", "PushSubscription.UserId", "SentReminder.UserId",
        "UserPreference.UserId", "TrainingSession.UserId",
    ];

    /// <summary>Verweise auf "wer es getan hat", die PurgeAsync nur leert: Die Zeile gehört anderen und bleibt.</summary>
    private static readonly string[] Geleert =
    [
        "ClubMembership.DecidedByUserId", "ClubRegistration.DecidedByUserId", "DogOwner.InvitedByUserId",
        "DogPause.CreatedByUserId", "DogHealthRecord.CreatedByUserId", "GroupRegistration.PaidByUserId",
        "GroupRegistrationAttendance.MarkedByUserId", "GroupTrainingExercise.CreatedByUserId",
        "GroupTrainingSession.CreatedByUserId", "GroupTrainingUnit.CreatedByUserId", "QuizQuestion.EditedByUserId",
    ];

    /// <summary>Hängen an einer anderen Zeile (Einstellung) oder gehören zur Anmeldung (Identity).</summary>
    private static readonly string[] Mittelbar =
    [
        "UserDisabledModule.UserPreferenceId", "UserSportSelection.UserPreferenceId", "RefreshToken.UserId",
    ];

    [Fact]
    public void JedeSpalteMitKontoBezug_IstEinerGruppeZugeordnet()
    {
        var modell = InMemoryDbContext.Create().Model;
        var spalten = modell.GetEntityTypes()
            // Die Tabellen der Anmeldung (IdentityUser...) verwaltet Identity selbst.
            .Where(t => !t.ClrType.Name.StartsWith("Identity", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties()
                .Where(p => (p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?))
                    && (p.Name.Contains("User", StringComparison.Ordinal) || p.Name is "TrainerId" or "MemberId"))
                .Select(p => $"{t.ClrType.Name}.{p.Name}"))
            .ToList();

        var bekannt = Geloescht.Concat(Geleert).Concat(Mittelbar).ToHashSet();
        var neu = spalten.Where(s => !bekannt.Contains(s)).OrderBy(s => s).ToList();
        var verschwunden = bekannt.Where(s => !spalten.Contains(s)).OrderBy(s => s).ToList();

        Assert.True(neu.Count == 0,
            "Neue Spalte mit Konto-Bezug - sie gehört in AccountDataService.PurgeAsync (und den Export), bei Löschung auch in "
            + "VerwaisteKontodaten und die Verhaltensliste dieses Tests, sonst in die passende Gruppe oben:\n" + string.Join("\n", neu));
        Assert.True(verschwunden.Count == 0, "Diese Spalten gibt es nicht mehr - Liste bereinigen:\n" + string.Join("\n", verschwunden));
    }

    private static readonly Dictionary<string, Action<ApplicationDbContext, Guid, Guid>> Zeilen = new()
    {
        ["ClubMembership.UserId"] = (db, id, vorhanden) => db.ClubMemberships.Add(new ClubMembership { ClubId = Guid.NewGuid(), UserId = id }),
        ["ClubRegistration.RequestedByUserId"] = (db, id, vorhanden) => db.ClubRegistrations.Add(new ClubRegistration { Name = "Verein", RequestedByUserId = id }),
        ["ClubTrainer.UserId"] = (db, id, vorhanden) => db.ClubTrainers.Add(new ClubTrainer { ClubId = Guid.NewGuid(), UserId = id }),
        ["Group.TrainerId"] = (db, id, vorhanden) => db.Groups.Add(new Group { Name = "Gruppe", TrainerId = id }),
        ["GroupMember.UserId"] = (db, id, vorhanden) => db.GroupMembers.Add(new GroupMember { GroupId = Guid.NewGuid(), UserId = id }),
        ["GroupTrainer.UserId"] = (db, id, vorhanden) => db.GroupTrainers.Add(new GroupTrainer { GroupId = Guid.NewGuid(), UserId = id }),
        ["GroupTrainingSessionResponse.UserId"] = (db, id, vorhanden) => db.GroupTrainingSessionResponses.Add(new GroupTrainingSessionResponse { GroupTrainingSessionId = Guid.NewGuid(), UserId = id }),
        ["GroupTrainingSessionTrainer.UserId"] = (db, id, vorhanden) => db.GroupTrainingSessionTrainers.Add(new GroupTrainingSessionTrainer { GroupTrainingSessionId = Guid.NewGuid(), UserId = id }),
        ["TrainerAssignment.MemberId"] = (db, id, vorhanden) => db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = vorhanden, MemberId = id, DogId = Guid.NewGuid() }),
        ["TrainerAssignment.TrainerId"] = (db, id, vorhanden) => db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = id, MemberId = vorhanden, DogId = Guid.NewGuid() }),
        ["DogOwner.UserId"] = (db, id, vorhanden) =>
        {
            var hund = new Dog { Name = "Bello" };
            db.Dogs.Add(hund);
            db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = id });
        },
        ["QuizExamAttempt.UserId"] = (db, id, vorhanden) => db.QuizExamAttempts.Add(new QuizExamAttempt { UserId = id, CatalogId = Guid.NewGuid() }),
        ["QuizMastery.UserId"] = (db, id, vorhanden) => db.QuizMasteries.Add(new QuizMastery { UserId = id, QuestionId = Guid.NewGuid() }),
        ["Notification.UserId"] = (db, id, vorhanden) => db.Notifications.Add(new Notification { UserId = id, Message = "Hallo" }),
        ["PushSubscription.UserId"] = (db, id, vorhanden) => db.PushSubscriptions.Add(new PushSubscription { UserId = id, Endpoint = "https://push.example/abc" }),
        ["SentReminder.UserId"] = (db, id, vorhanden) => db.SentReminders.Add(new SentReminder { UserId = id, Type = ReminderType.SessionDayBefore, ReferenceId = Guid.NewGuid() }),
        ["UserPreference.UserId"] = (db, id, vorhanden) => db.UserPreferences.Add(new UserPreference { UserId = id }),
        ["TrainingSession.UserId"] = (db, id, vorhanden) => db.TrainingSessions.Add(new TrainingSession { UserId = id, DogId = Guid.NewGuid(), Date = new DateOnly(2026, 9, 1), DurationMinutes = 30 }),
    };

    public static TheoryData<string> GeloeschteSpalten()
    {
        var daten = new TheoryData<string>();
        foreach (var spalte in Geloescht) daten.Add(spalte);
        return daten;
    }

    [Fact]
    public void FuerJedeGeloeschteSpalteGibtEsEineZeile()
    {
        Assert.Equivalent(Geloescht, Zeilen.Keys);
    }

    [Theory]
    [MemberData(nameof(GeloeschteSpalten))]
    public async Task EineZeileEinesGeloeschtenKontos_WirdErkanntGezaehltUndBereinigt(string spalte)
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var admin = Guid.NewGuid();
        lookup.Register(admin, "admin@test.de");
        // Ein zweites, vorhandenes Konto für die Zeilen, die zwei Personen verbinden.
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var dienst = new AdminService(db, lookup, new FakeRefreshTokenService(), new AccountDataService(db, lookup, new FakeNotificationService()));
        var weg = Guid.NewGuid();
        Zeilen[spalte](db, weg, vorhanden);
        await db.SaveChangesAsync();

        var vorher = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        Assert.True(vorher.Konten == 1, $"{spalte}: Die Zeile eines nicht mehr vorhandenen Kontos wird nicht als verwaist erkannt.");
        Assert.True(vorher.Trainings + vorher.Hunde + vorher.Ziele + vorher.Benachrichtigungen + vorher.Sonstige > 0,
            $"{spalte}: Die Zeile wird in keiner Zahl mitgezählt.");

        var bereinigt = (await dienst.PurgeOrphanedDataAsync(admin)).Value!;

        Assert.Equal(1, bereinigt.Konten);
        var nachher = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        Assert.True(nachher.Konten == 0, $"{spalte}: Nach der Bereinigung ist das Konto noch verwaist.");
    }
}

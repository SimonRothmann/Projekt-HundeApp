using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Die Zahlen für "Zu erledigen" auf der Trainer-Übersicht: nur das, was die
/// Person selbst entscheiden bzw. bewerten darf - sonst führte der Zähler zu
/// einer Seite, auf der nichts steht.
/// </summary>
public class TrainerOpenCountsServiceTests
{
    [Fact]
    public async Task OhneRolle_Alles_Null()
    {
        var db = InMemoryDbContext.Create();
        var anfrage = new Group { TrainerId = Guid.NewGuid(), Name = "Fremde Gruppe" };
        db.Groups.Add(anfrage);
        db.GroupMembers.Add(new GroupMember { GroupId = anfrage.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending });
        await db.SaveChangesAsync();

        var result = await new TrainerOpenCountsService(db).GetAsync(Guid.NewGuid());

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value!.GroupJoinRequests);
        Assert.Equal(0, result.Value.ClubJoinRequests);
        Assert.Equal(0, result.Value.SessionsToRate);
        Assert.Empty(result.Value.Groups);
    }

    [Fact]
    public async Task Gruppenanfragen_Zaehlen_Nur_Verwaltbare_Gruppen_Und_Nur_Offene()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var club = new Club { Name = "Testverein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainer, Role = ClubRole.Training });
        var eigene = new Group { TrainerId = trainer, Name = "Eigene" };
        var vereinsgruppe = new Group { TrainerId = Guid.NewGuid(), ClubId = club.Id, Name = "Vereinsgruppe" };
        var fremde = new Group { TrainerId = Guid.NewGuid(), Name = "Fremde" };
        db.Groups.AddRange(eigene, vereinsgruppe, fremde);
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = eigene.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending },
            new GroupMember { GroupId = eigene.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending },
            new GroupMember { GroupId = eigene.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Active },
            new GroupMember { GroupId = vereinsgruppe.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending },
            new GroupMember { GroupId = fremde.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending });
        await db.SaveChangesAsync();

        var result = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(3, result.Value!.GroupJoinRequests);
        Assert.Equal(2, result.Value.Groups.Count);
        Assert.Equal(2, result.Value.Groups.Single(g => g.GroupId == eigene.Id).JoinRequests);
        Assert.Equal(1, result.Value.Groups.Single(g => g.GroupId == vereinsgruppe.Id).JoinRequests);
    }

    [Fact]
    public async Task Vereinsanfragen_Zaehlen_Nur_Eigene_Vereine_Und_Nur_Offene()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var eigener = new Club { Name = "Eigener Verein" };
        var anderer = new Club { Name = "Anderer Verein" };
        db.Clubs.AddRange(eigener, anderer);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = eigener.Id, UserId = trainer, Role = ClubRole.Training });
        db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = eigener.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = eigener.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = eigener.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Approved },
            new ClubMembership { ClubId = anderer.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending });
        await db.SaveChangesAsync();

        var result = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(2, result.Value!.ClubJoinRequests);
    }

    [Fact]
    public async Task Zu_Bewerten_Zaehlt_Trainings_Betreuter_Hunde_Mit_Offenem()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var besitzer = Guid.NewGuid();
        var betreut = new Dog { Name = "Bello" };
        var fremd = new Dog { Name = "Rex" };
        db.Dogs.AddRange(betreut, fremd);
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = betreut.Id, TrainerId = trainer, MemberId = besitzer });

        TrainingSession Session(Guid dogId, string? feedback, int? bewertung) => new()
        {
            UserId = besitzer, DogId = dogId, Date = Vereinszeit.Heute().AddDays(-14), DurationMinutes = 30, TrainerFeedback = feedback,
            Exercises = { new TrainingExercise { FreeTextLabel = "Sitz", TrainerRating = bewertung } },
        };
        db.TrainingSessions.AddRange(
            Session(betreut.Id, null, 4), // Feedback fehlt
            Session(betreut.Id, "Gut.", null), // Bewertung fehlt
            Session(betreut.Id, "Gut.", 5), // fertig
            Session(fremd.Id, null, null)); // nicht betreut
        await db.SaveChangesAsync();

        var result = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(2, result.Value!.SessionsToRate);
    }

    [Fact]
    public async Task Gruppenanfragen_Zaehlen_Fuer_Aktive_Mittrainer_Aber_Nicht_Fuer_Eingeladene()
    {
        var db = InMemoryDbContext.Create();
        var mittrainer = Guid.NewGuid();
        var eingeladen = Guid.NewGuid();
        var gruppe = new Group { TrainerId = Guid.NewGuid(), Name = "Gemeinsame Gruppe" };
        db.Groups.Add(gruppe);
        db.GroupTrainers.AddRange(
            new GroupTrainer { GroupId = gruppe.Id, UserId = mittrainer, Status = GroupTrainerStatus.Active },
            new GroupTrainer { GroupId = gruppe.Id, UserId = eingeladen, Status = GroupTrainerStatus.Invited });
        db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending });
        await db.SaveChangesAsync();

        var service = new TrainerOpenCountsService(db);

        Assert.Equal(1, (await service.GetAsync(mittrainer)).Value!.GroupJoinRequests);
        // Eine offene Einladung gibt keine Verwaltungsrechte (globaler Filter).
        var einladung = (await service.GetAsync(eingeladen)).Value!;
        Assert.Equal(0, einladung.GroupJoinRequests);
        Assert.Empty(einladung.Groups);
    }

    [Fact]
    public async Task Geloeschte_Gruppen_Und_Mitglieder_Zaehlen_Nicht()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var aktiv = new Group { TrainerId = trainer, Name = "Aktiv" };
        var geloescht = new Group { TrainerId = trainer, Name = "Gelöscht", DeletedAt = DateTimeOffset.UtcNow };
        db.Groups.AddRange(aktiv, geloescht);
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = aktiv.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending },
            // Abgelehnt/entfernt: Soft Delete, der Zähler sinkt wieder.
            new GroupMember { GroupId = aktiv.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending, DeletedAt = DateTimeOffset.UtcNow },
            new GroupMember { GroupId = geloescht.Id, UserId = Guid.NewGuid(), Status = GroupMemberStatus.Pending });
        await db.SaveChangesAsync();

        var result = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(1, result.Value!.GroupJoinRequests);
        Assert.Equal(aktiv.Id, Assert.Single(result.Value.Groups).GroupId);
    }

    [Fact]
    public async Task Zu_Bewerten_Entspricht_Der_Liste_Hinter_Dem_Antippen()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var besitzer = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainer, MemberId = besitzer });
        for (var i = 0; i < 3; i++)
            db.TrainingSessions.Add(new TrainingSession
            {
                UserId = besitzer, DogId = hund.Id, Date = Vereinszeit.Heute().AddDays(-10 - i), DurationMinutes = 30,
                TrainerFeedback = i == 0 ? "Gut." : null,
                Exercises = { new TrainingExercise { FreeTextLabel = "Sitz", TrainerRating = i == 0 ? 5 : null } },
            });
        // Gelöscht: darf weder in der Liste noch im Zähler auftauchen.
        db.TrainingSessions.Add(new TrainingSession
        {
            UserId = besitzer, DogId = hund.Id, Date = Vereinszeit.Heute().AddDays(-2), DurationMinutes = 30, DeletedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var training = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService());
        var liste = await training.GetSessionsToRateAsync(trainer);
        var zahlen = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(2, liste.Value!.Count);
        Assert.Equal(liste.Value.Count, zahlen.Value!.SessionsToRate);
    }

    [Fact]
    public async Task Zu_Bewerten_Reicht_Nur_Acht_Wochen_Nach_Trainingsdatum_Zurueck()
    {
        var db = InMemoryDbContext.Create();
        var trainer = Guid.NewGuid();
        var besitzer = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainer, MemberId = besitzer });

        var grenze = Vereinszeit.Heute().AddDays(-7 * TrainerSessionQueries.BewertungsWochen);
        TrainingSession Session(DateOnly tag) => new()
        {
            UserId = besitzer, DogId = hund.Id, Date = tag, DurationMinutes = 30,
            // Angelegt wird alles jetzt: Maßgeblich ist das Trainingsdatum, nicht das Anlegedatum.
            Exercises = { new TrainingExercise { FreeTextLabel = "Sitz" } },
        };
        db.TrainingSessions.AddRange(
            Session(grenze), // genau acht Wochen her: noch dabei
            Session(grenze.AddDays(-1)), // einen Tag länger her: fällt heraus
            Session(grenze.AddDays(-200)));
        await db.SaveChangesAsync();

        var training = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService());
        var liste = await training.GetSessionsToRateAsync(trainer);
        var zahlen = await new TrainerOpenCountsService(db).GetAsync(trainer);

        Assert.Equal(grenze, Assert.Single(liste.Value!).Date);
        Assert.Equal(1, zahlen.Value!.SessionsToRate);
    }
}

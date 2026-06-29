using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Application.Planning;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt Testaccounts für alle praktisch relevanten Nutzungsszenarien an
/// (Plattform-Admin, Vereinstrainer, Hundebesitzer ohne und mit
/// Trainerbetreuung) inkl. Beispieldaten (Verein, Gruppe, Hunde, Training
/// mit Trainer-Feedback, Fährtenaufnahme, Ziel mit generiertem
/// Trainingsplan), damit Features ohne manuelles Anlegen über die UI
/// getestet werden können. Nur in Development aktiv (siehe Program.cs),
/// nie produktiv und idempotent (überspringt, falls bereits vorhanden).
///
/// Das Demo-Passwort ist bewusst fest codiert: es handelt sich um
/// Wegwerf-Testdaten für die lokale Dev-Datenbank, kein echtes Secret
/// (siehe README.md "NICHT im Klartext speichern" - das betrifft echte
/// Zugangsdaten wie die DB-Verbindung, nicht Dev-Fixtures).
/// </summary>
public static class DemoDataSeeder
{
    private const string DemoPassword = "Demo1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync("trainer@dogity.test") is not null)
            return;

        var admin = await CreateUserAsync(userManager, "admin@dogity.test", "Admin", "Muster", [Roles.User, Roles.Admin]);
        var trainer = await CreateUserAsync(userManager, "trainer@dogity.test", "Anna", "Trainer", [Roles.User]);
        var member1 = await CreateUserAsync(userManager, "mitglied1@dogity.test", "Max", "Mustermann", [Roles.User]);
        var member2 = await CreateUserAsync(userManager, "mitglied2@dogity.test", "Lisa", "Beispiel", [Roles.User]);

        var club = new Club { Name = "Hundesportverein Musterstadt e.V.", Description = "Demo-Verein für Testzwecke." };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainer.Id });

        var group = new Group
        {
            ClubId = club.Id,
            TrainerId = trainer.Id,
            Name = "Welpengruppe Dienstag",
            Description = "Demo-Trainingsgruppe."
        };
        db.Groups.Add(group);
        db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = member1.Id });
        db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = member2.Id });

        var bello = new Dog { Name = "Bello", Breed = "Labrador Retriever", Gender = DogGender.Male, Birthday = new DateOnly(2022, 3, 15) };
        var luna = new Dog { Name = "Luna", Breed = "Border Collie", Gender = DogGender.Female, Birthday = new DateOnly(2021, 7, 1) };
        var rocky = new Dog { Name = "Rocky", Breed = "Malinois", Gender = DogGender.Male, Birthday = new DateOnly(2020, 11, 9) };
        db.Dogs.AddRange(bello, luna, rocky);

        db.DogOwners.Add(new DogOwner { DogId = bello.Id, UserId = member1.Id });
        db.DogOwners.Add(new DogOwner { DogId = luna.Id, UserId = member2.Id });
        db.DogOwners.Add(new DogOwner { DogId = rocky.Id, UserId = trainer.Id });

        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer.Id, MemberId = member1.Id, DogId = bello.Id, StartDate = new DateOnly(2025, 1, 1) });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer.Id, MemberId = member2.Id, DogId = luna.Id, StartDate = new DateOnly(2025, 1, 1) });

        var bh = await db.Sports.IgnoreQueryFilters().FirstAsync(s => s.Code == "BH");
        var faerte = await db.Sports.IgnoreQueryFilters().FirstAsync(s => s.Code == "FAERTE");
        var leinenfuehrigkeit = await db.Exercises.IgnoreQueryFilters().FirstAsync(e => e.SportId == bh.Id && e.Name == "Leinenführigkeit");
        var verkehr = await db.Exercises.IgnoreQueryFilters().FirstAsync(e => e.SportId == bh.Id && e.Name == "Verhalten im Verkehr");

        var clubExercise = new Exercise
        {
            SportId = bh.Id,
            ClubId = club.Id,
            Name = "Vereins-Begrüßungsübung",
            Difficulty = ExerciseDifficulty.Beginner,
            Category = "Verein",
            ScoringCriteria = "Hund begrüßt neue Vereinsmitglieder ruhig, ohne zu bellen oder zu springen."
        };
        db.Exercises.Add(clubExercise);

        var trainedSession = new TrainingSession
        {
            UserId = member1.Id,
            DogId = bello.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
            DurationMinutes = 40,
            Notes = "Gutes Training auf dem Übungsplatz.",
            TrainerFeedback = "Sehr schöne Fortschritte bei der Leinenführigkeit, weiter so!",
            FeedbackByTrainerId = trainer.Id,
            FeedbackAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        trainedSession.Exercises.Add(new TrainingExercise { ExerciseId = leinenfuehrigkeit.Id, Rating = 4, Difficulty = ExerciseDifficulty.Beginner, Success = true, Notes = "Klappt gut." });
        trainedSession.Exercises.Add(new TrainingExercise { ExerciseId = verkehr.Id, Rating = 3, Difficulty = ExerciseDifficulty.Intermediate, Success = true, Notes = "Noch etwas unsicher bei Fahrrädern." });
        db.TrainingSessions.Add(trainedSession);

        var openFeedbackSession = new TrainingSession
        {
            UserId = member1.Id,
            DogId = bello.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            DurationMinutes = 30,
            Notes = "Training ohne Trainer."
        };
        openFeedbackSession.Exercises.Add(new TrainingExercise { ExerciseId = leinenfuehrigkeit.Id, Rating = 5, Difficulty = ExerciseDifficulty.Beginner, Success = true, Notes = null });
        db.TrainingSessions.Add(openFeedbackSession);

        var fahrteSession = new TrainingSession
        {
            UserId = member1.Id,
            DogId = bello.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            DurationMinutes = 25,
            Notes = "Fährtenaufnahme"
        };
        db.TrainingSessions.Add(fahrteSession);

        var track = new GpsTrack { TrainingSessionId = fahrteSession.Id, LengthMeters = 312, AgeMinutes = 20, Surface = "Wiese" };
        var baseLat = 52.520008;
        var baseLon = 13.404954;
        for (var i = 0; i < 8; i++)
        {
            track.Points.Add(new GpsPoint
            {
                Latitude = baseLat + i * 0.0004,
                Longitude = baseLon + i * 0.0002,
                Timestamp = DateTimeOffset.UtcNow.AddDays(-5).AddMinutes(i),
                Accuracy = 5
            });
        }
        db.GpsTracks.Add(track);

        var goal = new Goal
        {
            DogId = bello.Id,
            SportId = bh.Id,
            TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2),
            Status = GoalStatus.Active,
            Notes = "BH-Prüfung im Hundesportverein."
        };
        db.Goals.Add(goal);

        var bhExercises = await db.Exercises.IgnoreQueryFilters().Where(e => e.SportId == bh.Id && e.ClubId == null).ToListAsync();
        var planItems = TrainingPlanGenerator.Generate(DateOnly.FromDateTime(DateTime.UtcNow), goal.TargetDate, bhExercises);
        var plan = new TrainingPlan { GoalId = goal.Id };
        foreach (var item in planItems)
            plan.Items.Add(item);
        db.TrainingPlans.Add(plan);

        _ = admin;
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string firstName, string lastName, string[] roles)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };
        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Demo-Nutzer {email} konnte nicht angelegt werden: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRolesAsync(user, roles);
        return user;
    }
}

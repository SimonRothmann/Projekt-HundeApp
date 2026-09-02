using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Legt für Tests schnell einen Ausgangszustand an (Hund, Training, Ziel,
/// Mitgliedschaften), ohne dass jeder Test die Entitäten von Hand
/// zusammensetzen muss.
/// </summary>
public class Aufbau(ApplicationDbContext db)
{
    public Guid Hund(Guid besitzer, string name)
    {
        var hund = new Dog { Name = name };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        return hund.Id;
    }

    public void Training(Guid nutzer, Guid hund)
    {
        db.TrainingSessions.Add(new TrainingSession
        {
            UserId = nutzer,
            DogId = hund,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            DurationMinutes = 30,
        });
    }

    public void Ziel(Guid hund)
    {
        db.Goals.Add(new Goal
        {
            DogId = hund,
            SportId = Guid.NewGuid(),
            TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)),
            Status = GoalStatus.Active,
        });
    }

    public void Vereinsmitglied(Guid nutzer, ClubMembershipStatus status)
    {
        var verein = new Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = nutzer, Status = status });
    }

    /// <summary>Vereinstrainer:in - bewusst OHNE Mitgliedschaftszeile, so wie es ClubService.AssignTrainerAsync tut.</summary>
    public void Vereinstrainer(Guid nutzer)
    {
        var verein = new Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = nutzer });
    }

    /// <summary>Leitet eine Gruppe - ebenfalls ohne Mitgliedschaftszeile.</summary>
    public void Gruppenleiter(Guid nutzer)
    {
        db.Groups.Add(new Group { Name = "Testgruppe", TrainerId = nutzer });
    }

    public void Gruppenmitglied(Guid nutzer, GroupMemberStatus status)
    {
        var gruppe = new Group { Name = "Testgruppe", TrainerId = Guid.NewGuid() };
        db.Groups.Add(gruppe);
        db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = nutzer, Status = status });
    }

    public Task Speichern() => db.SaveChangesAsync();
}
